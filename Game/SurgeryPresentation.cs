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
                if(!(snapshot.ViewScale>=.3f && snapshot.ViewScale<=.8f))throw new ArgumentException("Invalid view geometry.");
                var settings=new JsonSerializerSettings {MaxDepth=12};
                if(Kind==ProcedureKind.Fracture)
                {var value=JsonConvert.DeserializeObject<FractureState>(snapshot.State,settings);value.Validate();Fracture=value;}
                else if(Kind==ProcedureKind.InternalBleeding)
                {var value=JsonConvert.DeserializeObject<InternalBleedingState>(snapshot.State,settings);if(!value.Valid())throw new ArgumentException("Invalid internal state.");Internal=value;}
                else
                {var value=JsonConvert.DeserializeObject<FragmentBoard>(snapshot.State,settings);value.Validate();Fragments=value;}
                sampledAt=clock();Sample=snapshot.Sample;ViewScale=snapshot.ViewScale;
            }
            catch(Exception e) when(e is JsonException || e is ArgumentException || e is NullReferenceException)
            {Failed("Invalid surgery snapshot; treatment stopped.");client.Release();}
        }
        void Failed(string error) {Error=error;queued.Clear();flight=null;InputReset?.Invoke();}
        void Completed(CommandReceipt receipt)
        {
            var done=flight;flight=null;if(done==null || disposed)return;
            if(receipt.Status==CommandStatus.StaleRevision && done.Retries++<2 && queued.Count<32)
            {queued.Insert(0,done);return;}
            if(receipt.Status==CommandStatus.Rejected || receipt.Status==CommandStatus.StaleRevision || receipt.Status==CommandStatus.OutOfOrder)
            {
                // Dependent drags/releases must not outlive a rejected grip or timer transition.
                queued.Clear();InputReset?.Invoke();
                if(done.Action!="gesture.cancel")queued.Add(new Intent {Action="gesture.cancel",Payload=new JObject()});
            }
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
        public void CancelInput()
        {
            queued.Clear();
            if(!disposed && Ready)queued.Add(new Intent {Action="gesture.cancel",Payload=new JObject()});
        }
        public void Tick()
        {
            if(disposed || Error!=null)return;
            if(!client.Active && !client.Busy && Sample>0) {Failed("Surgery connection ended; progress remains on the host.");return;}
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
            if(disposed)return;disposed=true;queued.Clear();
            client.SnapshotChanged-=Snapshot;client.Completed-=Completed;client.Failed-=Failed;
            client.Release();
        }
    }
}
