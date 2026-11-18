using System;
using System.Collections.Generic;
using CasualtiesSurgery.Procedures;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace CasualtiesSurgery.Game
{
    // Presentation-only replicas and bounded input scheduling. Never resolves a Limb or
    // writes clinical state. Both local loopback and Together use this same boundary.
    internal sealed class SurgeryPresentation : IDisposable
    {
        sealed class Intent { public string Action,Key;public JObject Payload;public int Retries; }
        readonly SurgeryClientSession client;
        readonly Func<double> clock;
        readonly List<Intent> queued=new List<Intent>();
        Intent flight;
        sealed class ExtractionPreview {public Intent Intent;public double Started;public bool Confirmed;}
        readonly Dictionary<int,ExtractionPreview> extractions=new Dictionary<int,ExtractionPreview>();
        const double ExtractionFadeSeconds=.18;
        public bool PreviewingExtraction(int site)=>extractions.ContainsKey(site);
        public float ExtractionOpacity(int site)=>extractions.TryGetValue(site,out var preview)?
            (float)Math.Max(0,1-Math.Max(0,clock()-preview.Started)/ExtractionFadeSeconds):1;
        // Scoped to this presentation/session and the exact queued release intent. Preview
        // never changes a replica, clinical state, eligibility, score or completion sound.
        public bool ReleaseFragment(JObject payload)
        {
            if(Kind!=ProcedureKind.Fragment || payload["site"]?.Type!=JTokenType.Integer)return false;
            int site=(int)payload["site"];
            if(extractions.ContainsKey(site) || Fragments?.Sites.Find(s=>s.Id==site && !s.Extracted && !s.Completed)==null)return false;
            if(!Submit("fragment.release",payload))return false;
            extractions.Add(site,new ExtractionPreview {Intent=queued[queued.Count-1],Started=clock()});return true;
        }
        bool disposed;
        double sampledAt;
        public ProcedureKind Kind { get; }
        public FractureState Fracture { get; private set; }
        public InternalBleedingState Internal { get; private set; }
        public FragmentBoard Fragments { get; private set; }
        public bool Ready=>client.Active && (Fracture!=null || Internal!=null || Fragments!=null);
        public string Error { get; private set; }
        public long Sample { get; private set; }
        public float ViewScale { get; private set; }=.4f;
        public int Participants {get;private set;}=1;
        public SurgeryPeerVisual[] Visuals {get;private set;}=new SurgeryPeerVisual[0];
        public bool PublishVisual(SurgeryVisualState visual)=>!disposed && Error==null && Ready && client.PublishVisual(visual);
        string[] busyResources=new string[0];
        public bool ResourceBusy(string resource)
        {foreach(var held in busyResources)if(resource=="*" || held=="*" || resource==held)return true;return false;}
        public double SnapshotAge=>Math.Max(0,clock()-sampledAt);
        public event Action<string,CommandReceipt> Result;
        public event Action InputReset;
        public SurgeryPresentation(SurgeryClientSession client,ProcedureKind kind,Func<double> clock)
        {
            this.client=client;Kind=kind;this.clock=clock;
            client.SnapshotChanged+=Snapshot;client.Completed+=Completed;client.Failed+=Failed;
        }
        public bool Begin(int limb,string patient=null)=>client.Begin(Kind,limb,patient);
        void Snapshot(SurgerySnapshot snapshot)
        {
            try
            {
                if(snapshot.Operation.Procedure!=Kind)throw new ArgumentException("Wrong procedure snapshot.");
                if(snapshot.Visuals.Length>7)throw new ArgumentException("Too many peer visuals.");
                foreach(var peer in snapshot.Visuals)if(peer==null || peer.Participant==Guid.Empty || peer.State==null || !peer.State.Valid || double.IsNaN(peer.Age) || double.IsInfinity(peer.Age) || peer.Age<0 || peer.Age>1.2)throw new ArgumentException("Invalid peer visual.");
                if(snapshot.Participants<0 || snapshot.Participants>8 || snapshot.BusyResources.Length>8)throw new ArgumentException("Invalid participant snapshot.");
                if(!(snapshot.ViewScale>=.3f && snapshot.ViewScale<=.8f))throw new ArgumentException("Invalid view geometry.");
                var settings=new JsonSerializerSettings {MaxDepth=12};
                if(Kind==ProcedureKind.Fracture)
                {var value=JsonConvert.DeserializeObject<FractureState>(snapshot.State,settings);value.Validate();Fracture=value;}
                else if(Kind==ProcedureKind.InternalBleeding)
                {var value=JsonConvert.DeserializeObject<InternalBleedingState>(snapshot.State,settings);if(!value.Valid())throw new ArgumentException("Invalid internal state.");Internal=value;}
                else
                {var value=JsonConvert.DeserializeObject<FragmentBoard>(snapshot.State,settings);value.Validate();Fragments=value;}
                sampledAt=clock();Sample=snapshot.Sample;ViewScale=snapshot.ViewScale;Participants=Math.Max(1,snapshot.Participants);busyResources=(string[])snapshot.BusyResources.Clone();Visuals=snapshot.Visuals;
            }
            catch(Exception e) when(e is JsonException || e is ArgumentException || e is NullReferenceException)
            {Failed("Invalid surgery snapshot; treatment stopped.");client.Release();}
        }
        void Failed(string error) {Error=error;queued.Clear();extractions.Clear();flight=null;InputReset?.Invoke();}
        void Completed(CommandReceipt receipt)
        {
            var done=flight;flight=null;if(done==null || disposed)return;
            if(receipt.Status==CommandStatus.StaleRevision && done.Retries++<2 && queued.Count<32)
            {queued.Insert(0,done);return;}
            if(receipt.Status==CommandStatus.Rejected || receipt.Status==CommandStatus.StaleRevision || receipt.Status==CommandStatus.OutOfOrder)
            {
                // Dependent drags/releases must not outlive a rejected grip or timer transition.
                queued.Clear();extractions.Clear();InputReset?.Invoke();
                if(done.Action!="gesture.cancel")queued.Add(new Intent {Action="gesture.cancel",Payload=new JObject()});
            }
            foreach(var pair in new List<KeyValuePair<int,ExtractionPreview>>(extractions))
                if(ReferenceEquals(pair.Value.Intent,done))
                {
                    if(Fragments?.Sites.Find(s=>s.Id==pair.Key)?.Extracted==true)pair.Value.Confirmed=true;
                    else extractions.Remove(pair.Key);
                }
            // Held samples queued during the round trip must not target a now-completed
            // pool/wound. Keep other targets and ordered gesture evidence untouched.
            if(done.Key!=null && receipt.Feedback?.Rating==TreatmentRating.Good)
                queued.RemoveAll(intent=>intent.Key==done.Key);
            Result?.Invoke(done.Action,receipt);
        }
        // Coalesce only replaceable samples; grip, terminal gesture, release and stitch
        // intents retain their order. Held time belongs to the same action/target/slot only.
        public bool Submit(string action,JObject payload,string replaceKey=null)
        {
            if(disposed || Error!=null || !Ready)return false;
            var copy=(JObject)payload.DeepClone();
            if(replaceKey!=null && queued.Count>0 && queued[queued.Count-1].Key==replaceKey)
            {
                var previous=queued[queued.Count-1];
                if(copy["seconds"]!=null && previous.Payload["seconds"]!=null)
                    copy["seconds"]=Math.Min(.25,(double)copy["seconds"]+(double)previous.Payload["seconds"]);
                previous.Payload=copy;return true;
            }
            if(queued.Count>=32) {CancelInput();InputReset?.Invoke();return false;}
            queued.Add(new Intent {Action=action,Payload=copy,Key=replaceKey});return true;
        }
        public void CancelInput(bool preserveReleased=false)
        {
            // A tool change cancels the unfinished gesture, not already submitted
            // terminal actions or the ordered grip/drag evidence preceding them.
            int keep=preserveReleased?queued.FindLastIndex(i=>i.Action=="fragment.release" || i.Action=="bone.release" || i.Action.EndsWith(".stitch",StringComparison.Ordinal) || i.Action=="incision.close")+1:0;
            if(keep<queued.Count)queued.RemoveRange(keep,queued.Count-keep);
            foreach(var pair in new List<KeyValuePair<int,ExtractionPreview>>(extractions))
                if(!preserveReleased || (!pair.Value.Confirmed && !ReferenceEquals(pair.Value.Intent,flight) && !queued.Contains(pair.Value.Intent)))extractions.Remove(pair.Key);
            if(!disposed && Ready)queued.Add(new Intent {Action="gesture.cancel",Payload=new JObject()});
        }
        public void Tick()
        {
            if(disposed || Error!=null)return;
            if(!client.Active && !client.Busy && Sample>0) {Failed("Surgery connection ended; progress remains on the host.");return;}
            foreach(var pair in new List<KeyValuePair<int,ExtractionPreview>>(extractions))
                if(pair.Value.Confirmed && clock()-pair.Value.Started>=ExtractionFadeSeconds)extractions.Remove(pair.Key);
            if(client.Busy || !Ready)return;
            if(queued.Count>0)
            {
                flight=queued[0];queued.RemoveAt(0);
                if(!client.Command(flight.Action,flight.Payload)) {flight=null;Failed("Surgery command could not be sent.");}
            }
            else if(SnapshotAge>=.2)client.Refresh();
        }
        public void Dispose()
        {
            if(disposed)return;disposed=true;queued.Clear();extractions.Clear();
            client.SnapshotChanged-=Snapshot;client.Completed-=Completed;client.Failed-=Failed;
            client.Release();
        }
    }
}
