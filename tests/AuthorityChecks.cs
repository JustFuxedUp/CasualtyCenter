using System;
using CasualtiesSurgery.Procedures;

static class AuthorityChecks
{
    public static void Run(Action<bool,string> check)
    {
        var world=Guid.NewGuid(); var actor=Guid.NewGuid(); var other=Guid.NewGuid();
        var key=new OperationKey(world,Guid.NewGuid(),"patient-a",1,ProcedureKind.Fracture);
        var leases=new PatientLeases(world);
        var lease=leases.Acquire(key,actor,0,10);
        check(lease!=null && leases.Validate(key,actor,lease.Token,0),"Authority grants a patient lease to authenticated connection");
        var alternate=new OperationKey(world,Guid.NewGuid(),"patient-a",2,ProcedureKind.InternalBleeding);
        check(leases.Acquire(alternate,other,1,10)==null,"Another limb/procedure cannot bypass patient-wide exclusion");
        check(!leases.Renew(key,other,lease.Token,1,10),"Another connection cannot renew lease");
        check(!leases.Release(key,other,lease.Token),"Another connection cannot release lease");
        check(leases.Renew(key,actor,lease.Token,2,10),"Owner can renew before expiry");
        check(!leases.Validate(key,actor,lease.Token,1),"Host clock rollback rejected");
        check(!leases.Validate(key,actor,lease.Token,12),"Lease expires at its deadline");
        var replacement=leases.Acquire(alternate,other,12,10);
        check(replacement!=null && replacement.Token!=lease.Token,"Expired lease replaced with fresh token");
        check(!leases.Validate(key,actor,lease.Token,12),"Old episode cannot use replacement lease");
        leases.Disconnect(other);
        check(!leases.Validate(alternate,other,replacement.Token,12),"Disconnect revokes treatment access");

        var journal=new SurgeryCommandJournal(key,lease.Token,actor);
        SurgeryCommand Cmd(long sequence,long revision,string payload="{}") =>
            new SurgeryCommand(1,key,lease.Token,sequence,revision,"cut",payload);
        int writes=0;
        Func<SurgeryCommand,CommandStatus> apply=c=>{writes++;return CommandStatus.Applied;};
        check(journal.Execute(other,Cmd(1,0),apply).Status==CommandStatus.Unauthorized && writes==0,"Spoofed connection rejected before application");
        check(journal.Execute(actor,Cmd(2,0),apply).Status==CommandStatus.OutOfOrder && writes==0,"Sequence gap cannot execute");
        check(journal.Execute(actor,Cmd(1,7),apply).Status==CommandStatus.StaleRevision && writes==0,"Stale revision requires resync");
        var first=Cmd(1,0);
        check(journal.Execute(actor,first,apply).Status==CommandStatus.Applied && writes==1 && journal.Revision==1,"Accepted action increments revision once");
        var duplicate=journal.Execute(actor,first,apply);
        check(duplicate.Status==CommandStatus.Duplicate && duplicate.OriginalStatus==CommandStatus.Applied && writes==1,"Retry returns receipt without repeating treatment");
        check(journal.Execute(actor,Cmd(1,0,"changed"),apply).Status==CommandStatus.Invalid && writes==1,"Sequence reuse with altered payload rejected");
        journal.AdvanceRevision();
        check(journal.Execute(actor,Cmd(2,1),apply).Status==CommandStatus.StaleRevision,"Host timer invalidates stale command revision");
        check(journal.Execute(actor,Cmd(2,2),c=>CommandStatus.Rejected).Status==CommandStatus.Rejected && journal.Revision==2,"Rejected gameplay consumes sequence without changing clinical revision");
        check(journal.Execute(actor,Cmd(2,2),apply).OriginalStatus==CommandStatus.Rejected && writes==1,"Rejected request cannot be replayed as successful");
        for(int i=3;i<=70;i++)journal.Execute(actor,Cmd(i,journal.Revision),apply);
        check(journal.Execute(actor,first,apply).Status==CommandStatus.Expired && writes==69,"Evicted receipts never reopen old sequence numbers");
        check(journal.Execute(actor,Cmd(71,journal.Revision,new string('x',8193)),apply).Status==CommandStatus.Invalid,"Oversized command payload rejected");
        check(journal.Execute(actor,new SurgeryCommand(99,key,lease.Token,71,journal.Revision,"cut","{}"),apply).Status==CommandStatus.Invalid,"Unsupported protocol rejected");
        check(journal.Execute(actor,Cmd(71,journal.Revision),c=>throw new Exception("partial commit")).Status==CommandStatus.Faulted,"Application exception quarantines operation");
        check(journal.Execute(actor,Cmd(71,journal.Revision),apply).Status==CommandStatus.Faulted && writes==69,"Fault cannot be retried into duplicate health writes");
        var feedback=new CommandReceipt(CommandStatus.Duplicate,5,4,CommandStatus.Applied,new TreatmentFeedback(TreatmentRating.Good,target:2,slot:1));
        var decoded=Newtonsoft.Json.JsonConvert.DeserializeObject<CommandReceipt>(Newtonsoft.Json.JsonConvert.SerializeObject(feedback));
        check(decoded.OriginalStatus==CommandStatus.Applied && decoded.Feedback.Rating==TreatmentRating.Good && decoded.Feedback.Target==2 && decoded.Feedback.Slot==1,"Cached completion feedback survives receipt wire roundtrip");

        var fracture=new FractureState(); float skin=80;
        check(fracture.Cut(-1,skin)==0 && !fracture.Started,"Malformed incision section has no side effect");
        check(!fracture.Disinfect(99,1),"Malformed prep section rejected");
        for(int i=0;i<9;i++)skin-=fracture.Cut(i,skin);
        check(((IOpenSurgeryState)fracture).Phase==OpenSurgeryPhase.Repair && Math.Abs(skin-32)<.001f,"Shared incision enters repair with original debt accounting");
        check(!OpenSurgery.Close(fracture,skin,100,out _),"Shared closure rejects unfinished repair");
        fracture.Angles[0]=fracture.Angles[1]=0;fracture.Align(0);fracture.Align(1);
        var saved=Newtonsoft.Json.Linq.JObject.FromObject(fracture);
        check(saved["IncisionDebt"]==null && saved["SkinDebt"]!=null,"Shared interface does not alter saved field names");
        fracture=saved.ToObject<FractureState>();
        check(OpenSurgery.Close(fracture,skin,100,out float healed) && Math.Abs(healed-80)<.001f,"Shared closure refunds restored actual debt");
        check(!OpenSurgery.Close(fracture,healed,100,out _),"Shared closure rejects duplicate refund");
        var fullSkin=new FractureState();fullSkin.Cut(0,100);
        var fullSkinSaved=Newtonsoft.Json.Linq.JObject.FromObject(fullSkin).ToObject<FractureState>();
        fullSkinSaved.Validate();
        check(fullSkinSaved.SkinDebt>0,"Incision at full skin survives save validation despite float rounding");
    }
}
