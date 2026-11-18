using System;
using System.IO;
using CasualtiesSurgery.Procedures;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace CasualtiesSurgery.Game
{
    // Single in-flight intent, immutable retry bytes, no speculative health writes. The UI
    // consumes SnapshotChanged/Completed and explicitly resubmits after a stale-state reply.
    internal sealed class SurgeryClientSession
    {
        readonly Func<string,bool> send;
        readonly Func<double> clock;
        string pending,id,kind;
        string inspectedPatient;
        double sentAt,lastRenew;
        int attempts;
        bool closing;
        OperationKey operation;
        Guid lease;
        long sequence=1,revision,sample;
        public bool Busy=>pending!=null;
        public bool Active=>operation!=null && lease!=Guid.Empty;
        public SurgerySnapshot Snapshot { get; private set; }
        public event Action<SurgerySnapshot> SnapshotChanged;
        public event Action<CommandReceipt> Completed;
        public event Action<string> Failed;
        public event Action<JArray> OverviewReceived;
        public bool Inspect(string patient=null)
        {
            if(Active || Busy)return false;
            if(patient!=null && (!ushort.TryParse(patient,out ushort peerId) || peerId.ToString(System.Globalization.CultureInfo.InvariantCulture)!=patient))return false;
            inspectedPatient=patient;
            var request=new JObject { ["version"]=SurgeryCommand.ProtocolVersion };
            if(patient!=null)request["patient"]=patient;
            return Queue("overview",request);
        }
        public SurgeryClientSession(Func<string,bool> send,Func<double> clock)
        {this.send=send ?? throw new ArgumentNullException(nameof(send));this.clock=clock ?? throw new ArgumentNullException(nameof(clock));}
        public bool Begin(ProcedureKind procedure,int limb,string patient=null)
        {
            if(Busy || Active || !Enum.IsDefined(typeof(ProcedureKind),procedure) || limb<0 || limb>255)return false;
            if(patient!=null && (!ushort.TryParse(patient,out ushort selected) || selected.ToString(System.Globalization.CultureInfo.InvariantCulture)!=patient))return false;
            var request=new JObject { ["version"]=SurgeryCommand.ProtocolVersion,["procedure"]=(int)procedure,["limb"]=limb };
            if(patient!=null)request["patient"]=patient;
            return Queue("open",request);
        }
        public bool Command(string action,JObject payload)
        {
            if(!Active || Busy || closing || payload==null)return false;
            var command=new SurgeryCommand(1,operation,lease,sequence,revision,action,payload.ToString(Formatting.None));
            if(!command.Valid)return false;
            return Queue("command",new JObject { ["command"]=JObject.FromObject(command) });
        }
        bool Queue(string type,JObject body)
        {
            id=Guid.NewGuid().ToString("N");kind=type;body["requestId"]=id;body["type"]=type;
            pending=body.ToString(Formatting.None);attempts=1;sentAt=clock();
            // Set pending before send: local-host delivery may synchronously invoke Receive.
            send(pending);return true;
        }
        JObject Identity()=>new JObject { ["operation"]=JObject.FromObject(operation),["lease"]=lease.ToString() };
        public void Tick()
        {
            double now=clock();
            if(Busy)
            {
                if(now-sentAt<1)return;
                if(attempts>=5) {Reset();Failed?.Invoke("Surgery request timed out; authoritative progress is preserved.");return;}
                attempts++;sentAt=now;send(pending);return;
            }
            if(Active && now-lastRenew>=5)Queue("renew",Identity());
        }
        public bool Release()
        {
            if(!Active && !Busy)return false;
            closing=true;
            // Closing the view during an in-flight action still acknowledges that exact
            // action, then releases. A delayed reply must never strand a renewed lease.
            if(Busy)return true;
            return Queue("release",Identity());
        }
        public bool Refresh()=>Active && !Busy && !closing && Queue("renew",Identity());
        public void Receive(string text)
        {
            if(!Busy || text==null || text.Length>65536)return;
            JObject reply;
            try
            {
                using(var reader=new JsonTextReader(new StringReader(text)) {MaxDepth=12,DateParseHandling=DateParseHandling.None})
                {reply=JObject.Load(reader);if(reader.Read())return;}
            }
            catch(JsonException){return;}
            if(reply["requestId"]?.Type!=JTokenType.String || (string)reply["requestId"]!=id)return;
            string completedKind=kind;
            if(reply["ok"]?.Type!=JTokenType.Boolean || !(bool)reply["ok"])
            {string error=reply["error"]?.Type==JTokenType.String?(string)reply["error"]:"Invalid surgery reply.";Reset();Failed?.Invoke(error);return;}
            try
            {
                if(completedKind=="open")
                {
                    var grant=reply["lease"];
                    operation=grant["Operation"].ToObject<OperationKey>();lease=grant["Token"].ToObject<Guid>();
                    if(operation==null || lease==Guid.Empty)throw new JsonException("Invalid lease.");
                    sequence=1;sample=0;
                }
                CommandReceipt receipt=null;
                if(completedKind=="command" && reply["receipt"]==null)throw new JsonException("Missing receipt.");
                if(reply["receipt"]!=null)
                {
                    receipt=reply["receipt"].ToObject<CommandReceipt>();
                    if(receipt==null || receipt.NextSequence<1 || receipt.Revision<0 || !Enum.IsDefined(typeof(CommandStatus),receipt.Status) ||
                        !Enum.IsDefined(typeof(CommandStatus),receipt.OriginalStatus))throw new JsonException("Invalid receipt.");
                    sequence=receipt.NextSequence;revision=receipt.Revision;
                }
                SurgerySnapshot next=null;
                if((completedKind=="open" || completedKind=="renew") && reply["snapshot"]==null)throw new JsonException("Missing snapshot.");
                if(reply["snapshot"]!=null)
                {
                    next=reply["snapshot"].ToObject<SurgerySnapshot>();
                    if(next==null || operation==null || !operation.Equals(next.Operation) || next.Revision<revision || next.Sample<=sample || next.State==null ||
                        next.State.Length>65536 || double.IsNaN(next.HostTime) || double.IsInfinity(next.HostTime) || next.HostTime<0)
                        throw new JsonException("Invalid snapshot identity or ordering.");
                    revision=next.Revision;sample=next.Sample;Snapshot=next;
                }
                pending=id=kind=null;lastRenew=clock();
                if(completedKind=="release")Reset();
                if(completedKind=="overview")
                {
                    if(!(reply["overview"] is JArray overview) || overview.Count>256)throw new JsonException("Invalid patient overview.");
                    if(inspectedPatient!=null && (reply["patient"]?.Type!=JTokenType.String || (string)reply["patient"]!=inspectedPatient))throw new JsonException("Wrong overview patient.");
                    if(inspectedPatient==null && reply["patient"]!=null && reply["patient"].Type!=JTokenType.Null)throw new JsonException("Unexpected overview patient.");
                    foreach(var limb in overview)
                        if(!(limb is JObject) || limb["fracture"]?.Type!=JTokenType.Boolean || limb["internal"]?.Type!=JTokenType.Boolean || limb["suture"]?.Type!=JTokenType.Boolean)
                            throw new JsonException("Invalid overview markers.");
                    OverviewReceived?.Invoke((JArray)overview.DeepClone());
                }
                if(next!=null)SnapshotChanged?.Invoke(next);
                if(receipt!=null)
                {
                    if(receipt.Status==CommandStatus.Unauthorized || receipt.Status==CommandStatus.Faulted || receipt.Status==CommandStatus.Expired || receipt.Status==CommandStatus.Invalid)
                    {Reset();Failed?.Invoke("Surgery authority revoked the session.");}
                    else Completed?.Invoke(receipt);
                }
                if(closing && Active && !Busy)Queue("release",Identity());
            }
            catch(Exception e) when(e is JsonException || e is ArgumentException || e is FormatException || e is NullReferenceException || e is InvalidCastException || e is OverflowException)
            {Reset();Failed?.Invoke("Invalid surgery authority reply.");}
        }
        public void Reset() {pending=id=kind=inspectedPatient=null;operation=null;lease=Guid.Empty;sequence=1;revision=sample=0;Snapshot=null;closing=false;}
    }
}
