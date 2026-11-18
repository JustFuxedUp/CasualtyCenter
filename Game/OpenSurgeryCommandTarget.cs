using System;
using System.IO;
using System.Linq;
using CasualtiesSurgery.Procedures;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace CasualtiesSurgery.Game
{
    // Authoritative open-surgery endpoint; inner repair handlers live in the partial class.
    // Construct only after the host resolves a body-generation identity, never from payload.
    internal sealed partial class OpenSurgeryCommandTarget : ISurgeryStateTarget,ISurgeryFeedbackTarget,ISurgeryLeaseTarget,ISurgeryResourceTarget
    {
        public string CommandResource(SurgeryCommand command)=>TreatmentInput.Resource(command,"wound");
        public int SharedPhase=>state is FractureState f?(int)f.Stage:(int)((InternalBleedingState)state).Stage;
        public string HeldResource=>rotating>=0?"bone:"+rotating:gripping>=0?"wound:"+gripping:null;
        readonly Limb limb;
        readonly Body body;
        readonly object state;
        readonly Func<Guid,bool> canAccess;
        readonly TreatmentTime timeBudget=new TreatmentTime();
        readonly ScalpelTrace scalpel=new ScalpelTrace();
        public OperationKey Key { get; }
        public TreatmentFeedback Feedback { get; private set; }
        public void BeginLease(Guid token,double hostTime)=>timeBudget.Begin(token,hostTime);
        public OpenSurgeryCommandTarget(OperationKey key,Limb limb,Func<Guid,bool> canAccess)
        {
            if(key==null || limb==null || canAccess==null || key.Procedure==ProcedureKind.Fragment ||
                limb.body?.limbs==null || key.Limb>=limb.body.limbs.Length || !ReferenceEquals(limb.body.limbs[key.Limb],limb))
                throw new ArgumentException("Invalid authoritative open-surgery target.");
            Key=key; this.limb=limb; body=limb.body; this.canAccess=canAccess;
            state=key.Procedure==ProcedureKind.Fracture ? (object)FractureStore.Get(limb) : InternalBleedingStore.Get(limb);
        }
        public bool CanAccess(Guid connection) => connection!=Guid.Empty && canAccess(connection) && Current;
        internal bool Current=>ReferenceEquals(limb.body,body) && body.limbs!=null && Key.Limb<body.limbs.Length && ReferenceEquals(body.limbs[Key.Limb],limb) &&
            (state is FractureState fracture ? FractureStore.Contains(limb,fracture) : InternalBleedingStore.Contains(limb,(InternalBleedingState)state));
        string ClinicalReason() => Key.Procedure==ProcedureKind.Fracture ? FractureStore.ClinicalIneligible(body,limb) : InternalBleedingStore.ClinicalIneligible(body,limb);

        public CommandStatus Apply(Guid connection,SurgeryCommand command,double hostTime)
        {
            Feedback=null;
            if(!CanAccess(connection) || command==null || !command.Valid || !Key.Equals(command.Operation) || ClinicalReason()!=null ||
                double.IsNaN(hostTime) || double.IsInfinity(hostTime) || hostTime<0)return CommandStatus.Rejected;
            JObject payload;
            try
            {
                using(var reader=new JsonTextReader(new StringReader(command.Payload)) { MaxDepth=6, DateParseHandling=DateParseHandling.None })
                {
                    payload=JObject.Load(reader);
                    if(reader.Read())return CommandStatus.Rejected;
                }
            }
            catch(JsonException) { return CommandStatus.Rejected; }
            var actions=new SurgeryActions(limb,Key.Procedure,()=>CanAccess(connection) ? ClinicalReason() : "Access revoked.");
            // Changing tools cancels forceps evidence, even when the new intent is rejected.
            if(!command.Action.StartsWith("laceration.",StringComparison.Ordinal) && !command.Action.StartsWith("bone.",StringComparison.Ordinal) && command.Action!="scalpel.sample")ResetInput();
            if(command.Action!="scalpel.sample")scalpel.Reset();
            if(command.Action=="scalpel.sample")return Scalpel(command,payload,actions,hostTime);
            if(command.Action=="incision.cut")
            {
                if(!Fields(payload,"section","x","y") || !Section(payload,out int section) || !Contact(payload,section,14))return CommandStatus.Rejected;
                bool applied=state is FractureState fracture ? actions.Cut(fracture,section) : actions.Cut((InternalBleedingState)state,section);
                if(applied && state is FractureState cut && cut.Stage==FractureStage.Alignment)
                    Feedback=new TreatmentFeedback(TreatmentRating.Scored,cut.CutScore);
                return applied ? CommandStatus.Applied : CommandStatus.NoChange;
            }
            if(command.Action=="incision.prepare")
            {
                if(!Fields(payload,"section","x","y","seconds") || !Section(payload,out int section) || !Contact(payload,section,48) ||
                    !Number(payload["seconds"],out float seconds) || !(seconds>0 && seconds<=.25f))return CommandStatus.Rejected;
                // One shared host-time budget across all incision sections. A new lease gets
                // no free dose; duplicate requests never enter this callback via the journal.
                if(!timeBudget.Spend(command.Lease,hostTime,seconds))return CommandStatus.Rejected;
                bool applied=state is FractureState fracture ? actions.Disinfect(fracture,section,seconds*GelRules.FlowMlPerSecond) :
                    actions.Disinfect((InternalBleedingState)state,section,seconds*GelRules.FlowMlPerSecond);
                return applied ? CommandStatus.Applied : CommandStatus.NoChange;
            }
            if(command.Action=="incision.close")
            {
                if(!Fields(payload,"samples") || !(payload["samples"] is JArray samples) || samples.Count<4 || samples.Count>128)
                    return CommandStatus.Rejected;
                var stroke=new SutureStroke(2);
                int quality=0;
                for(int i=0;i<samples.Count;i++)
                {
                    if(!(samples[i] is JObject sample) || !Fields(sample,"x","y") || !Number(sample["x"],out float x) ||
                        !Number(sample["y"],out float y) || Math.Abs(x)>20 || Math.Abs(y)>20)return CommandStatus.Rejected;
                    if(i==0)stroke.Begin(x,y);
                    else if(i==samples.Count-1) { if(!stroke.End(x,y,out quality))return CommandStatus.Rejected; }
                    else stroke.Move(x,y);
                }
                bool applied=state is FractureState fracture ? actions.Close(fracture,quality) : actions.Close((InternalBleedingState)state,quality);
                if(applied)Feedback=new TreatmentFeedback(TreatmentRating.Scored,quality);
                return applied ? CommandStatus.Applied : CommandStatus.Rejected;
            }
            return ApplyRepair(command,payload,actions,hostTime);
        }
        CommandStatus Scalpel(SurgeryCommand command,JObject payload,SurgeryActions actions,double hostTime)
        {
            if(!TreatmentInput.Fields(payload,"x","y","seconds","active") || !TreatmentInput.Point(payload,out float x,out float y) ||
                payload["active"]?.Type!=JTokenType.Boolean || !Number(payload["seconds"],out float seconds) || seconds<0 || seconds>.25f)return CommandStatus.Rejected;
            if(gestureLease!=command.Lease) {ResetInput();gestureLease=command.Lease;}
            gripping=rotating=-1;
            if(!(bool)payload["active"]) {scalpel.Reset();return CommandStatus.NoChange;}
            var open=(IOpenSurgeryState)state;
            if(open.Phase==OpenSurgeryPhase.Complete || open.ActivePools?.Blocking==true ||
                (state is InternalBleedingState && open.Phase!=OpenSurgeryPhase.Incision)) {scalpel.Reset();return CommandStatus.Rejected;}
            if(!timeBudget.Spend(command.Lease,hostTime,seconds))return CommandStatus.Rejected;
            bool productive=false;
            if(open.Phase==OpenSurgeryPhase.Incision)
                for(int i=0;i<open.IncisionCuts.Length;i++)
                    if(!open.IncisionCuts[i] && scalpel.Touch(x,y,0,120-i*30,14))
                        productive|=state is FractureState fracture?actions.Cut(fracture,i):actions.Cut((InternalBleedingState)state,i);
            float damage=scalpel.Step(true,productive,seconds);scalpel.Sample(x,y);
            // Match existing gameplay: continuous off-target scalpel damage belongs to
            // fracture surgery. Internal surgery does not yet impose this damage rule.
            if(state is FractureState bones)
            {
                if(damage>0) {actions.Damage(damage);Feedback=new TreatmentFeedback(TreatmentRating.Mistake);}
                else if(productive && bones.Stage==FractureStage.Alignment)Feedback=new TreatmentFeedback(TreatmentRating.Scored,bones.CutScore);
                if(damage>0)return CommandStatus.Applied;
            }
            return productive?CommandStatus.Applied:CommandStatus.NoChange;
        }
        static bool Fields(JObject value,params string[] names) => value.Count==names.Length && value.Properties().All(p=>names.Contains(p.Name));
        static bool Number(JToken value,out float number)
        {
            number=0;
            if(value==null || (value.Type!=JTokenType.Float && value.Type!=JTokenType.Integer))return false;
            try { number=value.Value<float>(); }
            catch(Exception error) when(error is OverflowException || error is FormatException || error is InvalidCastException) { return false; }
            return !float.IsNaN(number) && !float.IsInfinity(number);
        }
        static bool Section(JObject payload,out int section)
        {
            section=-1;
            if(payload["section"]?.Type!=JTokenType.Integer || !Number(payload["section"],out float value) || value<0 || value>=GelRules.IncisionSections)return false;
            section=(int)value;return true;
        }
        static bool Contact(JObject payload,int section,float radius) => Number(payload["x"],out float x) && Number(payload["y"],out float y) &&
            x*x+(y-(120-section*30))*(y-(120-section*30))<radius*radius;
    }
}
