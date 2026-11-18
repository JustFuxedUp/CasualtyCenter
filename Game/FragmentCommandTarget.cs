using System;
using CasualtiesSurgery.Procedures;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using static CasualtiesSurgery.Game.TreatmentInput;

namespace CasualtiesSurgery.Game
{
    internal sealed class FragmentCommandTarget : ISurgeryStateTarget,ISurgeryLeaseTarget,ISurgeryFeedbackTarget,ISurgeryViewSettings,ISurgeryResourceTarget
    {
        public string CommandResource(SurgeryCommand command)=>TreatmentInput.Resource(command,"site");
        public int SharedPhase=>0;
        public string HeldResource=>pull!=null && pull.Active && gripping>=0?"wound:"+gripping:null;
        readonly Limb limb;
        readonly Body body;
        readonly FragmentBoard board;
        readonly Func<Guid,bool> permitted;
        readonly float recovery,reduction,scale;
        bool bleedingAtEntry;
        readonly TreatmentTime timeBudget=new TreatmentTime();
        readonly ScalpelTrace scalpel=new ScalpelTrace();
        bool observed;
        Guid gestureLease;
        int gripping=-1;
        FragmentPull pull;
        OperationSession session;
        public OperationKey Key { get; }
        public float ViewScale=>scale;
        public TreatmentFeedback Feedback { get; private set; }
        public FragmentCommandTarget(OperationKey key,Limb limb,Func<Guid,bool> permitted,float recovery=5,float reduction=1,float scale=.4f)
        {
            if(key==null || key.Procedure!=ProcedureKind.Fragment || limb?.body?.limbs==null || key.Limb>=limb.body.limbs.Length ||
                !ReferenceEquals(limb.body.limbs[key.Limb],limb) || permitted==null || !(recovery>0 && recovery<=100) ||
                !(reduction>0 && reduction<=100) || !(scale>=.3f && scale<=.8f))throw new ArgumentException("Invalid fragment target.");
            Key=key;this.limb=limb;body=limb.body;this.permitted=permitted;this.recovery=recovery;this.reduction=reduction;this.scale=scale;
            board=FragmentBoardStore.Get(limb);bleedingAtEntry=limb.bleedAmount>0;
        }
        public bool CanAccess(Guid connection) => connection!=Guid.Empty && permitted(connection) && Current;
        internal bool Current=>ReferenceEquals(limb.body,body) &&
            body.limbs!=null && Key.Limb<body.limbs.Length && ReferenceEquals(body.limbs[Key.Limb],limb) && FragmentBoardStore.Contains(limb,board);
        public void ResetInput() { pull?.Reset();session?.Cancel();session=null;gripping=-1;gestureLease=Guid.Empty;scalpel.Reset(); }
        public void BeginLease(Guid token,double hostTime) { bleedingAtEntry=limb.bleedAmount>0;observed=false;timeBudget.Begin(token,hostTime); }
        public string CaptureState() => JObject.FromObject(board).ToString(Formatting.None);
        public bool Advance(float seconds,bool operated)
        {
            if(OperationSession.ClinicalIneligible(body,limb,false)!=null || !FragmentBoardStore.Contains(limb,board))return false;
            string before=CaptureState();
            if(board.Remaining!=limb.shrapnel) { board.Reconcile(limb.shrapnel);ResetInput(); }
            if(!observed)
            {
                observed=true;
                foreach(var site in board.Sites)
                {
                    if(site.Completed)continue;
                    if(bleedingAtEntry && !site.Pools.Rolled)site.Pools.Observe(true,UnityEngine.Random.value);
                    for(int i=0;i<2;i++)if(site.Pools.Remaining(i)>0)site.Pools.Angle(i,()=>UnityEngine.Random.value*360);
                }
            }
            return before!=CaptureState();
        }
        public CommandStatus Apply(Guid connection,SurgeryCommand command,double hostTime)
        {
            Feedback=null;
            if(!CanAccess(connection) || command==null || !command.Valid || !Key.Equals(command.Operation) ||
                OperationSession.ClinicalIneligible(body,limb)!=null || board.Remaining!=limb.shrapnel)return CommandStatus.Rejected;
            var payload=Parse(command.Payload);if(payload==null)return CommandStatus.Rejected;
            if(gestureLease!=command.Lease) {ResetInput();gestureLease=command.Lease;}
            if(command.Action!="fragment.grip" && command.Action!="fragment.drag" && command.Action!="fragment.release" && command.Action!="scalpel.sample")ResetInput();
            if(command.Action!="scalpel.sample")scalpel.Reset();
            if(command.Action=="gesture.cancel" && Fields(payload)) { ResetInput();return CommandStatus.NoChange; }
            if(command.Action=="scalpel.sample")
            {
                if(!Fields(payload,"x","y","seconds","active") || !Point(payload,out _,out _) || payload["active"]?.Type!=JTokenType.Boolean ||
                    !Number(payload["seconds"],out float seconds) || seconds<0 || seconds>.25f)return CommandStatus.Rejected;
                if(!(bool)payload["active"]) {ResetInput();return CommandStatus.NoChange;}
                if(!timeBudget.Spend(command.Lease,hostTime,seconds))return CommandStatus.Rejected;
                pull?.Reset();session?.Cancel();session=null;gripping=-1;
                float damage=scalpel.Step(true,false,seconds);
                if(damage<=0)return CommandStatus.NoChange;
                new SurgeryActions(limb,ProcedureKind.Fragment,()=>CanAccess(connection)?null:"Access revoked.").Damage(damage);
                Feedback=new TreatmentFeedback(TreatmentRating.Mistake);return CommandStatus.Applied;
            }
            if(!Index(payload["site"],board.NextId,out int siteId))return CommandStatus.Rejected;
            var site=board.Sites.Find(s=>s.Id==siteId);
            if(site==null || site.Completed)return CommandStatus.Rejected;
            var actions=new SurgeryActions(limb,ProcedureKind.Fragment,()=>CanAccess(connection)?null:"Access revoked.");
            Func<OperationSession> fresh=()=>new OperationSession(body,limb,recovery,reduction,()=>CanAccess(connection)?null:"Access revoked.");
            if(command.Action=="pool.suction")
            {
                var layout=board.Layout(siteId);
                if(!Fields(payload,"site","slot","x","y","seconds") || !Index(payload["slot"],2,out int slot) || !Point(payload,out float x,out float y) ||
                    !Number(payload["seconds"],out float seconds) || !PoolHit(site.Pools,slot,x,y,layout.X,layout.Y) ||
                    !timeBudget.Spend(command.Lease,hostTime,seconds))return CommandStatus.Rejected;
                float before=site.Pools.Remaining(slot);actions.Suction(site.Pools,slot,seconds);ResetInput();
                if(before>0 && site.Pools.Remaining(slot)<=0)Feedback=new TreatmentFeedback(TreatmentRating.Good,target:siteId,slot:slot);
                return site.Pools.Remaining(slot)<before?CommandStatus.Applied:CommandStatus.NoChange;
            }
            if(site.Pools.Blocking)return CommandStatus.Rejected;
            if(command.Action=="fragment.stitch")
            {
                if(!Fields(payload,"site","samples") || !site.Extracted || !Suture(payload["samples"],out int quality))return CommandStatus.Rejected;
                var completion=fresh();completion.Extract();
                bool applied=actions.Complete(completion,site,quality,false);ResetInput();
                if(applied)Feedback=new TreatmentFeedback(TreatmentRating.Scored,quality,siteId);
                return applied?CommandStatus.Applied:CommandStatus.Rejected;
            }
            if(command.Action=="fragment.gel")
            {
                if(!Fields(payload,"site","x","y","seconds") || !site.Extracted || !Point(payload,out float x,out float y) || !Number(payload["seconds"],out float seconds))return CommandStatus.Rejected;
                float beyond=Math.Max(0,Math.Abs(y)-board.Layout(siteId).Length/2);
                if((x*x+beyond*beyond)*scale*scale>44*44 || !timeBudget.Spend(command.Lease,hostTime,seconds))return CommandStatus.Rejected;
                float before=site.GelMl;
                if(!actions.FragmentGel(board,site,seconds*GelRules.FlowMlPerSecond))return CommandStatus.Rejected;
                if(site.GelMl>=GelRules.FragmentMl)
                {
                    var completion=fresh();completion.Extract();
                    if(!actions.Complete(completion,site,100,true))throw new InvalidOperationException("Fragment gel completion failed after dose commit.");
                    Feedback=new TreatmentFeedback(TreatmentRating.Good,target:siteId);
                }
                ResetInput();return site.GelMl>before || site.Completed?CommandStatus.Applied:CommandStatus.NoChange;
            }
            if(site.Extracted || !Fields(payload,"site","x","y") || !Point(payload,out float px,out float py))return CommandStatus.Rejected;
            if(command.Action=="fragment.grip")
            {
                float length=board.Layout(siteId).Length;
                var candidate=new FragmentPull(2,(length*.84f*.35f+8)/length,.005f);
                if(!candidate.Begin(px,py))return CommandStatus.Rejected;
                ResetInput();gestureLease=command.Lease;gripping=siteId;pull=candidate;session=fresh();return CommandStatus.NoChange;
            }
            if(gripping!=siteId || session==null || !pull.Active || (command.Action!="fragment.drag" && command.Action!="fragment.release"))return CommandStatus.Rejected;
            bool release=command.Action=="fragment.release";
            bool success=release?pull.End(px,py):pull.Move(px,py);
            if(!success) {bool damaged=fresh().Slip();ResetInput();if(damaged)Feedback=new TreatmentFeedback(TreatmentRating.Mistake,target:siteId);return damaged?CommandStatus.Applied:CommandStatus.Rejected;}
            if(!release)return CommandStatus.NoChange;
            int extractionQuality=pull.Quality;
            bool extracted=actions.Extract(fresh(),site,extractionQuality);ResetInput();
            if(extracted)Feedback=new TreatmentFeedback(TreatmentRating.Scored,extractionQuality,siteId);
            return extracted?CommandStatus.Applied:CommandStatus.Rejected;
        }
    }
}
