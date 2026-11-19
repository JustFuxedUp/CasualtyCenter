using System;
using CasualtiesSurgery.Game;
using CasualtiesSurgery.Procedures;
using Newtonsoft.Json.Linq;

static class RepairCommandChecks
{
    const string Stroke="[{\"x\":-1,\"y\":0.1},{\"x\":1,\"y\":0.3},{\"x\":-1,\"y\":0.5},{\"x\":1,\"y\":0.7}]";
    public static void Run(Action<bool,string> check)
    {
        double now=0;var actor=Guid.NewGuid();var world=Guid.NewGuid();
        var body=new Body {internalBleeding=20};var limb=new Limb {body=body,skinHealth=100};body.limbs=new[]{new Limb {body=body},limb};
        var key=new OperationKey(world,Guid.NewGuid(),"patient",1,ProcedureKind.InternalBleeding);
        var target=new OpenSurgeryCommandTarget(key,limb,c=>c==actor);var dispatcher=new SurgeryDispatcher(world,()=>now);dispatcher.Register(target);
        var lease=dispatcher.Acquire(key,actor);long seq=1,rev=0;
        CommandReceipt Send(string action,JObject payload)
        {
            var receipt=dispatcher.Execute(actor,new SurgeryCommand(1,key,lease.Token,seq,rev,action,payload.ToString(Newtonsoft.Json.Formatting.None)));
            seq=receipt.NextSequence;rev=receipt.Revision;return receipt;
        }
        for(int i=0;i<9;i++)Send("incision.cut",new JObject { ["section"]=i,["x"]=0,["y"]=120-i*30 });
        var state=InternalBleedingStore.Get(limb);
        int index=0;var wound=state.Wounds[index];
        JObject Point(float x,float y) {double angle=wound.Angle*Math.PI/180;return new JObject { ["wound"]=index,["x"]=wound.X+x*Math.Cos(angle)-y*Math.Sin(angle),["y"]=wound.Y+x*Math.Sin(angle)+y*Math.Cos(angle) };}
        check(Send("laceration.drag",Point(0,0)).Status==CommandStatus.Rejected && wound.Phase==InternalWoundPhase.Open,"Laceration closure requires a prior grip");
        check(Send("laceration.grip",Point(32,0)).Status==CommandStatus.NoChange,"Rotated long edge accepts authoritative grip");
        var closed=Send("laceration.drag",Point(0,0));
        check(closed.Status==CommandStatus.Applied && closed.Feedback?.Rating==TreatmentRating.Good && wound.Seconds==6 && wound.Phase==InternalWoundPhase.Pinched,"Drag closes rotated wound with full independent timer and Good result");
        var snapshot=dispatcher.Snapshot(key,actor);
        var copy=JObject.Parse(snapshot.State);copy["SkinDebt"]=0;
        check(state.SkinDebt>0 && dispatcher.Snapshot(key,actor).Sample>snapshot.Sample,"Snapshot is detached and has monotonic display sample");
        dispatcher.Release(key,actor,lease.Token);now=6.1;dispatcher.Tick();
        check(wound.Phase==InternalWoundPhase.Open && wound.Pools.Blocking,"Interrupted operation continues host laceration timer");
        lease=dispatcher.Acquire(key,actor);seq=1;rev=dispatcher.Snapshot(key,actor).Revision;
        JObject pool=new JObject { ["wound"]=index,["slot"]=0,["x"]=wound.X,["y"]=wound.Y,["seconds"]=.25f };
        check(Send("pool.suction",pool).Status==CommandStatus.Rejected,"Suction cannot invent elapsed contact time");
        now+=.25;
        double poolAngle = (wound.Pools.FirstAngle ?? 0) * Math.PI / 180;
        pool["x"] = wound.X + 75 * Math.Cos(poolAngle);
        pool["y"] = wound.Y + 75 * Math.Sin(poolAngle);
        check(Send("pool.suction",pool).Status == CommandStatus.Rejected, "Host rejects suction outside the enlarged pool");
        pool["x"] = wound.X + 55 * Math.Cos(poolAngle);
        pool["y"] = wound.Y + 55 * Math.Sin(poolAngle);
        var cleared=Send("pool.suction",pool);
        check(cleared.Status==CommandStatus.Applied && cleared.Feedback?.Rating==TreatmentRating.Good && cleared.Feedback.Slot==0 && !wound.Pools.Blocking,"Host-timed suction works at the enlarged rotated pool edge and reports its slot");
        float muscle=limb.muscleHealth,bleeding=body.internalBleeding;
        Send("laceration.grip",Point(32,0));
        check(Send("laceration.drag",Point(100,0)).Status==CommandStatus.Applied && limb.muscleHealth==muscle-1 && body.internalBleeding>bleeding,"Remote overpull applies muscle damage and minor internal bleeding");
        Send("laceration.grip",Point(32,0));
        Send("incision.prepare",new JObject());
        check(Send("laceration.drag",Point(0,0)).Status==CommandStatus.Rejected,"Switching tools cancels retained forceps evidence even if new contact is rejected");
        Send("laceration.grip",Point(32,0));Send("laceration.drag",Point(0,0));
        check(Send("wound.stitch",new JObject { ["wound"]=index,["samples"]=JArray.Parse(Stroke) }).Status==CommandStatus.Applied && wound.Phase==InternalWoundPhase.Repaired,"Internal suture evidence repairs pinched wound");
        index=1;wound=state.Wounds[index];
        var gel=Point(0,0);gel["seconds"]=.25f;
        for(int i=0;i<4;i++){now+=.25;Send("wound.gel",gel);}
        check(wound.Phase==InternalWoundPhase.Repaired,"Host-timed bruise gel reaches production dose");

        var fragmentsBody=new Body();var fragmentLimb=new Limb {body=fragmentsBody,skinHealth=70,shrapnel=2};fragmentsBody.limbs=new[]{fragmentLimb};
        var fragmentKey=new OperationKey(world,Guid.NewGuid(),"fragment-patient",0,ProcedureKind.Fragment);
        var fragmentTarget=new FragmentCommandTarget(fragmentKey,fragmentLimb,c=>c==actor);dispatcher.Register(fragmentTarget);
        var fragmentLease=dispatcher.Acquire(fragmentKey,actor);long fseq=1,frev=0;
        CommandReceipt Fragment(string action,JObject payload)
        {var r=dispatcher.Execute(actor,new SurgeryCommand(1,fragmentKey,fragmentLease.Token,fseq,frev,action,payload.ToString(Newtonsoft.Json.Formatting.None)));fseq=r.NextSequence;frev=r.Revision;return r;}
        var point=new JObject { ["site"]=0,["x"]=0,["y"]=.38f };
        check(Fragment("fragment.grip",point).Status==CommandStatus.NoChange,"Fragment target accepts grip in normalized wound coordinates");
        point["y"]=-.2;
        check(Fragment("fragment.release",point).Status==CommandStatus.Applied && FragmentBoardStore.Get(fragmentLimb).Sites[0].Extracted,"Host reconstructs extraction travel and score");
        check(Fragment("fragment.stitch",new JObject { ["site"]=0,["samples"]=JArray.Parse(Stroke) }).Status==CommandStatus.Applied && fragmentLimb.shrapnel==1 && fragmentLimb.skinHealth==75,"Host fragment suture commits board and native health");
        point["site"]=1;point["y"]=.38f;Fragment("fragment.grip",point);point["y"]=-.2;Fragment("fragment.release",point);
        var fg=new JObject { ["site"]=1,["x"]=0,["y"]=0,["seconds"]=.25f };
        Fragment("fragment.gel",fg);
        for(int i=0;i<4;i++){now+=.25;Fragment("fragment.gel",fg);}
        check(fragmentLimb.shrapnel==0 && fragmentLimb.skinHealth==80,"Host fragment gel closure gives one reward at full dose");

        var boneBody=new Body();var boneLimb=new Limb {body=boneBody,skinHealth=80,broken=true,boneHealTimer=100};boneBody.limbs=new[]{boneLimb};
        var bones=FractureStore.Get(boneLimb);for(int i=0;i<9;i++)boneLimb.skinHealth-=bones.Cut(i,boneLimb.skinHealth);
        var boneKey=new OperationKey(world,Guid.NewGuid(),"bone-patient",0,ProcedureKind.Fracture);var boneTarget=new OpenSurgeryCommandTarget(boneKey,boneLimb,c=>c==actor);
        dispatcher.Register(boneTarget);var boneLease=dispatcher.Acquire(boneKey,actor);long bseq=1,brev=0;
        for(int bone=0;bone<2;bone++)
        {
            float y=bone==0?500:-500,pivot=bone==0?-532:532;double angle=bones.Angles[bone]*Math.PI/180;
            var grip=new JObject { ["bone"]=bone,["x"]=-y*Math.Sin(angle),["y"]=pivot+y*Math.Cos(angle) };
            var r=dispatcher.Execute(actor,new SurgeryCommand(1,boneKey,boneLease.Token,bseq++,brev,"bone.grip",grip.ToString()));brev=r.Revision;
            var release=new JObject { ["bone"]=bone,["x"]=0,["y"]=pivot+y };
            r=dispatcher.Execute(actor,new SurgeryCommand(1,boneKey,boneLease.Token,bseq++,brev,"bone.release",release.ToString()));brev=r.Revision;
            check(r.Status==CommandStatus.Applied && bones.Aligned[bone],"Host bone pivot gesture aligns piece "+bone);
        }
        check(bones.Stage==FractureStage.Closure,"Authoritative bone gestures unlock outer closure");

        double cuttingTime=0;var cutter=new Body();var cuttingLimb=new Limb {body=cutter,skinHealth=80,broken=true,boneHealTimer=100};cutter.limbs=new[]{cuttingLimb};
        var cuttingKey=new OperationKey(world,Guid.NewGuid(),"cutting-patient",0,ProcedureKind.Fracture);
        var cuttingTarget=new OpenSurgeryCommandTarget(cuttingKey,cuttingLimb,c=>c==actor);
        var cuttingDispatcher=new SurgeryDispatcher(world,()=>cuttingTime);cuttingDispatcher.Register(cuttingTarget);
        var cuttingLease=cuttingDispatcher.Acquire(cuttingKey,actor);long cseq=1,crev=0;
        SurgeryCommand latest=null;
        CommandReceipt Cut(string action,JObject payload)
        {
            latest=new SurgeryCommand(1,cuttingKey,cuttingLease.Token,cseq,crev,action,payload.ToString());
            var r=cuttingDispatcher.Execute(actor,latest);cseq=r.NextSequence;crev=r.Revision;return r;
        }
        var sample=new JObject { ["x"]=200,["y"]=0,["seconds"]=.25f,["active"]=true };
        check(Cut("scalpel.sample",sample).Status==CommandStatus.Rejected,"Scalpel sample cannot mint elapsed cutting time");
        cuttingTime=.25;
        check(Cut("scalpel.sample",sample).Status==CommandStatus.NoChange && cuttingLimb.skinHealth==80,"Host scalpel preserves productive-cut grace period");
        cuttingTime=.5;var mistake=Cut("scalpel.sample",sample);float afterMistake=cuttingLimb.skinHealth;
        check(mistake.Status==CommandStatus.Applied && mistake.Feedback?.Rating==TreatmentRating.Mistake && Math.Abs(afterMistake-79.5f)<.001f,"Host scalpel applies only off-target time beyond grace");
        var repeated=cuttingDispatcher.Execute(actor,latest);
        check(repeated.Status==CommandStatus.Duplicate && repeated.Feedback?.Rating==TreatmentRating.Mistake && cuttingLimb.skinHealth==afterMistake,"Duplicate scalpel receipt retains feedback without repeating damage");
        Cut("gesture.cancel",new JObject());cuttingTime=.75;
        check(Cut("scalpel.sample",sample).Status==CommandStatus.NoChange && cuttingLimb.skinHealth==afterMistake,"Gesture cancellation clears off-target cutting accumulation");
        var cuttingState=FractureStore.Get(cuttingLimb);cuttingState.Pools[0].Observe(true,0);
        for(int slot=0;slot<2;slot++)
        {
            cuttingTime+=.25;
            var r=Cut("pool.suction",new JObject { ["wound"]=-1,["slot"]=slot,["x"]=slot==0?0:37,["y"]=slot==0?0:-25,["seconds"]=.25f });
            check(r.Status==CommandStatus.Applied && r.Feedback?.Rating==TreatmentRating.Good && r.Feedback.Slot==slot,"Each outer pool slot receives completion feedback: "+slot);
        }
    }
}
