using System;
using System.Linq;
using CasualtiesSurgery.Procedures;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using static CasualtiesSurgery.Game.TreatmentInput;

namespace CasualtiesSurgery.Game
{
    internal sealed partial class OpenSurgeryCommandTarget
    {
        Guid gestureLease;
        int gripping=-1,rotating=-1;
        float previousX,previousY,startPointer,startAngle;
        public void ResetInput() { gripping=rotating=-1;gestureLease=Guid.Empty;scalpel.Reset(); }
        public string CaptureState() => JObject.FromObject(state).ToString(Formatting.None);
        public bool Advance(float seconds,bool operated)
        {
            if(!(seconds>0) || OperationSession.ClinicalIneligible(body,limb,false)!=null)return false;
            if(state is InternalBleedingState internalState)
            {
                if(!InternalBleedingStore.Contains(limb,internalState))return false;
                string before=Material(internalState);
                internalState.Tick(seconds);
                if(internalState.Stage==InternalStage.Repair)
                    foreach(var wound in internalState.Wounds.Where(w=>w.Phase!=InternalWoundPhase.Repaired))PreparePool(wound.Pools,body.internalBleeding>0 || limb.bleedAmount>0);
                else if(!internalState.Closed)PreparePool(internalState.Stage==InternalStage.Incision?internalState.IncisionPools:internalState.ClosurePools,limb.bleedAmount>0);
                return before!=Material(internalState);
            }
            var fracture=(FractureState)state;
            if(!FractureStore.Contains(limb,fracture) || fracture.Stage==FractureStage.Complete)return false;
            var stage=fracture.Stage;var pool=fracture.Pools[(int)stage];string poolBefore=JObject.FromObject(pool).ToString(Formatting.None);
            if(operated && fracture.Started && stage==FractureStage.Incision)fracture.CutSeconds+=seconds;
            if(!limb.broken && fracture.Started && stage==FractureStage.Alignment)fracture.Stage=FractureStage.Closure;
            PreparePool(fracture.Pools[(int)fracture.Stage],limb.bleedAmount>0);
            return stage!=fracture.Stage || poolBefore!=JObject.FromObject(pool).ToString(Formatting.None);
        }
        static string Material(InternalBleedingState value)
        {
            var json=JObject.FromObject(value);
            foreach(var wound in (JArray)json["Wounds"])((JObject)wound).Remove("Seconds");
            return json.ToString(Formatting.None);
        }
        static void PreparePool(BloodPools pool,bool bleeding)
        {
            if(!pool.Rolled && bleeding)pool.Observe(true,UnityEngine.Random.value);
            for(int i=0;i<2;i++)if(pool.Remaining(i)>0)pool.Angle(i,()=>UnityEngine.Random.value*360);
        }
        CommandStatus ApplyRepair(SurgeryCommand command,JObject payload,SurgeryActions actions,double hostTime)
        {
            if(gestureLease!=command.Lease) { ResetInput();gestureLease=command.Lease; }
            if(command.Action=="gesture.cancel" && Fields(payload)) { ResetInput();return CommandStatus.NoChange; }
            if(command.Action=="pool.suction")
            {
                if(!Fields(payload,"wound","slot","x","y","seconds") || !Index(payload["slot"],2,out int slot) || !Point(payload,out float x,out float y) ||
                    !Number(payload["wound"],out float woundIndex) || payload["wound"].Type!=JTokenType.Integer ||
                    !Number(payload["seconds"],out float seconds))return CommandStatus.Rejected;
                BloodPools pool;float cx=0,cy=0;
                if(state is FractureState fracture)
                { if(woundIndex!=-1 || fracture.Stage==FractureStage.Complete)return CommandStatus.Rejected;pool=fracture.Pools[(int)fracture.Stage]; }
                else
                {
                    var inner=(InternalBleedingState)state;
                    if(inner.Stage==InternalStage.Repair)
                    {
                        if(!Index(payload["wound"],inner.Wounds.Length,out int index) || inner.Wounds[index].Phase==InternalWoundPhase.Repaired)return CommandStatus.Rejected;
                        pool=inner.Wounds[index].Pools;cx=inner.Wounds[index].X;cy=inner.Wounds[index].Y;
                    }
                    else {if(woundIndex!=-1 || inner.Closed)return CommandStatus.Rejected;pool=inner.Stage==InternalStage.Incision?inner.IncisionPools:inner.ClosurePools;}
                }
                if(!PoolHit(pool,slot,x,y,cx,cy) || !timeBudget.Spend(command.Lease,hostTime,seconds))return CommandStatus.Rejected;
                float before=pool.Remaining(slot);actions.Suction(pool,slot,seconds);ResetInput();
                if(before>0 && pool.Remaining(slot)<=0)Feedback=new TreatmentFeedback(TreatmentRating.Good,target:(int)woundIndex,slot:slot);
                return pool.Remaining(slot)<before?CommandStatus.Applied:CommandStatus.NoChange;
            }
            if(state is FractureState bones)return BoneCommand(command,payload,actions,bones);
            var stateInternal=(InternalBleedingState)state;
            if(stateInternal.Stage!=InternalStage.Repair || !Index(payload["wound"],stateInternal.Wounds.Length,out int woundId))return CommandStatus.Rejected;
            var wound=stateInternal.Wounds[woundId];
            if(wound.Pools.Blocking || wound.Phase==InternalWoundPhase.Repaired)return CommandStatus.Rejected;
            if(command.Action=="wound.stitch")
            {
                if(!Fields(payload,"wound","samples") || !wound.Laceration || !Suture(payload["samples"],out _))return CommandStatus.Rejected;
                if(!actions.Stitch(stateInternal,woundId))return CommandStatus.Rejected;
                Feedback=new TreatmentFeedback(TreatmentRating.Good,target:woundId);return CommandStatus.Applied;
            }
            if(!Point(payload,out float px,out float py))return CommandStatus.Rejected;
            Local(px,py,wound.X,wound.Y,wound.Angle,out float lx,out float ly);
            if(command.Action=="wound.gel")
            {
                if(!Fields(payload,"wound","x","y","seconds") || wound.Laceration || lx*lx+ly*ly>InternalWound.BruiseGelReach * InternalWound.BruiseGelReach || !Number(payload["seconds"],out float seconds) ||
                    !timeBudget.Spend(command.Lease,hostTime,seconds))return CommandStatus.Rejected;
                float before=wound.GelMl;actions.Bruise(stateInternal,woundId,seconds*GelRules.FlowMlPerSecond);ResetInput();
                if(wound.Phase==InternalWoundPhase.Repaired)Feedback=new TreatmentFeedback(TreatmentRating.Good,target:woundId);
                return wound.GelMl>before?CommandStatus.Applied:CommandStatus.NoChange;
            }
            if(!Fields(payload,"wound","x","y") || !wound.Laceration || wound.Phase!=InternalWoundPhase.Open)return CommandStatus.Rejected;
            if(command.Action=="laceration.grip")
            {
                if(!LacerationDrag.CanGrip(lx, ly, 108))return CommandStatus.Rejected;
                gripping=woundId;previousX=lx;previousY=ly;return CommandStatus.NoChange;
            }
            if(command.Action!="laceration.drag" || gripping!=woundId)return CommandStatus.Rejected;
            var result=LacerationDrag.Evaluate(previousX,previousY,lx,ly,108);
            previousX=lx;previousY=ly;
            if(result==LacerationDragResult.Overpull) {actions.Overpull(stateInternal,woundId);ResetInput();Feedback=new TreatmentFeedback(TreatmentRating.Mistake,target:woundId);return CommandStatus.Applied;}
            if(result==LacerationDragResult.Close) {bool closed=actions.Pinch(stateInternal,woundId);ResetInput();if(closed)Feedback=new TreatmentFeedback(TreatmentRating.Good,target:woundId);return closed?CommandStatus.Applied:CommandStatus.Rejected;}
            return CommandStatus.NoChange;
        }
        CommandStatus BoneCommand(SurgeryCommand command,JObject payload,SurgeryActions actions,FractureState bones)
        {
            if(bones.Stage!=FractureStage.Alignment || bones.Pools[1].Blocking || !Fields(payload,"bone","x","y") ||
                !Index(payload["bone"],2,out int bone) || bones.Aligned[bone] || !Point(payload,out float x,out float y))return CommandStatus.Rejected;
            float pivot=bone==0?-532:532;
            float pointer=(float)(Math.Atan2(y-pivot,x)*180/Math.PI);
            if(command.Action=="bone.grip")
            {
                Local(x,y,0,pivot,bones.Angles[bone],out float lx,out float ly);
                if(Math.Abs(lx)>42 || (bone==0 ? ly< -12 || ly>544 : ly< -544 || ly>12))return CommandStatus.Rejected;
                rotating=bone;startPointer=pointer;startAngle=bones.Angles[bone];return CommandStatus.NoChange;
            }
            if(rotating!=bone || (command.Action!="bone.drag" && command.Action!="bone.release"))return CommandStatus.Rejected;
            if(x*x+(y-pivot)*(y-pivot)>100)
            {
                float delta=((pointer-startPointer)%360+540)%360-180;
                actions.Rotate(bones,bone,startAngle+delta);
            }
            if(command.Action=="bone.release")
            {
                int quality=(int)Math.Round(100*(1-FractureState.AngleError(bones.Angles[bone])/24));
                bool aligned=actions.Align(bones,bone);ResetInput();
                Feedback=new TreatmentFeedback(aligned?TreatmentRating.Scored:TreatmentRating.Mistake,aligned?Math.Max(0,Math.Min(100,quality)):0,bone);
            }
            return CommandStatus.Applied;
        }
    }
}
