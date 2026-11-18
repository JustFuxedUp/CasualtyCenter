using System;
using CasualtiesSurgery.Game;
using CasualtiesSurgery.Procedures;

static class DispatcherChecks
{
    public static void Run(Action<bool,string> check)
    {
        var world=Guid.NewGuid();var connection=Guid.NewGuid();var intruder=Guid.NewGuid();double now=0;
        var body=new Body();var limb=new Limb { body=body,skinHealth=80,broken=true,boneHealTimer=100 };
        body.limbs=new[]{limb};
        PlayerCamera.main=null;MinigameBase.main=null;
        CUCoreLib.Networking.MultiplayerApi.IsRunning=true;
        var key=new OperationKey(world,Guid.NewGuid(),"remote-patient",0,ProcedureKind.Fracture);
        bool permitted=true;
        var target=new OpenSurgeryCommandTarget(key,limb,id=>permitted && id==connection);
        var dispatcher=new SurgeryDispatcher(world,()=>now);
        dispatcher.Register(target);
        check(dispatcher.Acquire(key,intruder)==null,"Dispatcher denies lease without authenticated patient access");
        var lease=dispatcher.Acquire(key,connection);
        check(lease!=null,"Dispatcher binds resolved remote patient without local camera");
        SurgeryCommand Cmd(long sequence,long revision,string action,string payload) => new SurgeryCommand(1,key,lease.Token,sequence,revision,action,payload);
        var first=Cmd(1,0,"incision.cut","{\"section\":0,\"x\":0,\"y\":120}");
        check(dispatcher.Execute(intruder,first).Status==CommandStatus.Unauthorized && limb.skinHealth==80,"Wrong transport sender cannot execute clinical command");
        check(dispatcher.Execute(connection,first).Status==CommandStatus.Applied && limb.skinHealth<80,"Remote dispatcher invokes production incision accounting");
        float skin=limb.skinHealth;
        check(dispatcher.Execute(connection,first).Status==CommandStatus.Duplicate && limb.skinHealth==skin,"Network retry cannot duplicate skin charge");
        var bad=Cmd(2,1,"incision.cut","{\"section\":1,\"x\":300,\"y\":90}");
        check(dispatcher.Execute(connection,bad).Status==CommandStatus.Rejected && !FractureStore.Get(limb).Cuts[1],"Host rejects off-target cut evidence");
        check(dispatcher.Execute(connection,Cmd(3,1,"incision.prepare","{\"section\":0,\"x\":0,\"y\":120,\"seconds\":0.2}")).Status==CommandStatus.Rejected,"New lease has no invented gel-time credit");
        now=.2;
        check(dispatcher.Execute(connection,Cmd(4,1,"incision.prepare","{\"section\":0,\"x\":0,\"y\":120,\"seconds\":0.2}")).Status==CommandStatus.Applied && limb.disinfectionTime==60,"Host elapsed time funds actual prep and antiseptic refresh");
        check(dispatcher.Execute(connection,Cmd(5,2,"incision.prepare","{\"section\":1,\"x\":0,\"y\":90,\"seconds\":0.2}")).Status==CommandStatus.Rejected,"Different gel section cannot spend the same host time twice");
        long sequence=6,revision=2;
        for(int i=1;i<9;i++)
        {
            var result=dispatcher.Execute(connection,Cmd(sequence++,revision,"incision.cut","{\"section\":"+i+",\"x\":0,\"y\":"+(120-30*i)+"}"));
            check(result.Status==CommandStatus.Applied,"Authoritative incision progression "+i);revision=result.Revision;
        }
        const string stroke="{\"samples\":[{\"x\":-1,\"y\":0.1},{\"x\":1,\"y\":0.3},{\"x\":-1,\"y\":0.5},{\"x\":1,\"y\":0.7}]}";
        check(dispatcher.Execute(connection,Cmd(sequence++,revision,"incision.close",stroke)).Status==CommandStatus.Rejected,"Valid suture cannot skip unfinished inner repair");
        var state=FractureStore.Get(limb);state.Angles[0]=state.Angles[1]=0;state.Align(0);state.Align(1);
        dispatcher.StateChanged(key);revision++;
        var close=Cmd(sequence++,revision,"incision.close",stroke);
        var closed=dispatcher.Execute(connection,close);
        check(closed.Status==CommandStatus.Applied && state.Stage==FractureStage.Complete && limb.boneHealTimer==25,"Host computes suture quality and commits production fracture closure");
        skin=limb.skinHealth;
        check(dispatcher.Execute(connection,close).Status==CommandStatus.Duplicate && limb.skinHealth==skin && limb.boneHealTimer==25,"Closure retry preserves exact-once refund and timer change");
        permitted=false;
        check(dispatcher.Execute(connection,close).Status==CommandStatus.Unauthorized,"Revoked patient access is checked even for retries");
        permitted=true;dispatcher.Disconnect(connection);
        check(dispatcher.Execute(connection,close).Status==CommandStatus.Unauthorized,"Disconnect invalidates live journal access");
        lease=dispatcher.Acquire(key,connection);
        check(lease!=null,"New lease can inspect completed episode without resetting revision");
        check(dispatcher.Execute(connection,Cmd(1,0,"incision.cut","{}")).Status==CommandStatus.StaleRevision,"Reacquiring lease preserves operation revision");
        now=20;
        check(dispatcher.Execute(connection,Cmd(1,closed.Revision,"incision.cut","{}")).Status==CommandStatus.Unauthorized,"Expired lease cannot execute production target");
        var body2=new Body { internalBleeding=20 };
        var chest=new Limb { body=body2,skinHealth=100 };
        body2.limbs=new[]{new Limb { body=body2,skinHealth=100 },chest};
        var internalKey=new OperationKey(world,Guid.NewGuid(),"patient-b",1,ProcedureKind.InternalBleeding);
        var internalTarget=new OpenSurgeryCommandTarget(internalKey,chest,id=>id==connection);
        dispatcher.Register(internalTarget);
        var internalLease=dispatcher.Acquire(internalKey,connection);
        SurgeryCommand Internal(long seq,long rev,string action,string payload) => new SurgeryCommand(1,internalKey,internalLease.Token,seq,rev,action,payload);
        check(dispatcher.Execute(connection,Internal(1,0,"incision.cut","{\"section\":0,\"x\":0,\"y\":120,\"quality\":100}")).Status==CommandStatus.Rejected,"Client-invented score fields rejected by schema");
        check(dispatcher.Execute(connection,Internal(2,0,"incision.cut","{\"section\":99999999999999999999999,\"x\":0,\"y\":120}")).Status==CommandStatus.Rejected,"Oversized numeric target rejected without faulting lease");
        sequence=3;revision=0;
        for(int i=0;i<9;i++)
        {
            var result=dispatcher.Execute(connection,Internal(sequence++,revision,"incision.cut","{\"section\":"+i+",\"x\":0,\"y\":"+(120-30*i)+"}"));
            check(result.Status==CommandStatus.Applied,"Internal authoritative incision progression "+i);revision=result.Revision;
        }
        var internalState=InternalBleedingStore.Get(chest);
        foreach(var wound in internalState.Wounds)wound.Phase=InternalWoundPhase.Repaired;
        dispatcher.StateChanged(internalKey);revision++;
        var internalClose=Internal(sequence,revision,"incision.close",stroke);
        check(dispatcher.Execute(connection,internalClose).Status==CommandStatus.Applied && internalState.Closed && chest.skinHealth>90 && body2.internalBleeding<20,"Same dispatcher endpoint commits internal closure and bleeding treatment");
        float bleeding=body2.internalBleeding;
        check(dispatcher.Execute(connection,internalClose).Status==CommandStatus.Duplicate && body2.internalBleeding==bleeding,"Internal closure replay cannot treat bleeding twice");
        body2.limbs[1]=new Limb { body=body2,skinHealth=100 };
        check(dispatcher.Execute(connection,internalClose).Status==CommandStatus.Unauthorized,"Replaced limb invalidates resolved patient target");
        CUCoreLib.Networking.MultiplayerApi.IsRunning=false;
        PlayerCamera.main=new PlayerCamera { body=body };
    }
}
