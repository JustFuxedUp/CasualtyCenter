using System;
using System.Collections.Generic;
using System.Linq;
using CasualtiesSurgery.Game;
using CasualtiesSurgery.Procedures;
using Newtonsoft.Json.Linq;

static class CooperativeChecks
{
    sealed class Peer
    {
        public readonly SurgeryClientSession Client;
        public CommandReceipt Receipt;
        public string Request,Error;
        public Peer(Guid id,SurgeryHostRouter router,Func<double> clock)
        {
            SurgeryClientSession client=null;
            client=new SurgeryClientSession(t=>{Request=t;client.Receive(router.Handle(id,t));return true;},clock);
            Client=client;Client.Completed+=r=>Receipt=r;Client.Failed+=e=>Error=e;
        }
        public CommandStatus Do(string action,JObject payload)
        {
            Receipt=null;Client.Command(action,payload);
            if(Receipt?.Status==CommandStatus.StaleRevision)Client.Command(action,payload);
            return Receipt?.Status??CommandStatus.Invalid;
        }
    }
    static JObject Point(int site,float y)=>new JObject {["site"]=site,["x"]=0,["y"]=y};
    static JObject Stitch(int site)=>new JObject {["site"]=site,["samples"]=JArray.Parse("[{\"x\":-1,\"y\":0.1},{\"x\":1,\"y\":0.3},{\"x\":-1,\"y\":0.5},{\"x\":1,\"y\":0.7}]")};
    public static void Run(Action<bool,string> check)
    {
        double now=0;var a=Guid.NewGuid();var b=Guid.NewGuid();var c=Guid.NewGuid();
        var patient=new Body {conscious=false,internalBleeding=20};
        var fragment=new Limb {body=patient,shrapnel=3,skinHealth=70};
        var chest=new Limb {body=patient,broken=true,boneHealTimer=100,skinHealth=90};patient.limbs=new[]{fragment,chest};
        var actors=new Dictionary<Guid,Body>{{a,new Body()},{b,new Body()},{c,new Body()}};
        Body Resolve(Guid id)=>actors.TryGetValue(id,out var actor)?actor:null;
        var permission=new SurgeryTreatmentAccess(Resolve,p=>p==patient,()=>false,null,()=>now);
        var router=new SurgeryHostRouter(Resolve,()=>now,inspectPatient:id=>id=="7"?patient:null,permission:permission);
        var first=new Peer(a,router,()=>now);var second=new Peer(b,router,()=>now);var third=new Peer(c,router,()=>now);
        first.Client.Begin(ProcedureKind.Fragment,0,"7");second.Client.Begin(ProcedureKind.Fragment,0,"7");first.Client.Refresh();
        check(first.Client.Active && second.Client.Active && first.Client.Snapshot.Operation.Equals(second.Client.Snapshot.Operation) && first.Client.Snapshot.Participants==2,"Two surgeons join the same saved operation with separate live leases");
        var visitors=new List<Peer>();
        for(int n=0;n<6;n++){var id=Guid.NewGuid();actors.Add(id,new Body());var peer=new Peer(id,router,()=>now);peer.Client.Begin(ProcedureKind.Fragment,0,"7");visitors.Add(peer);}
        third.Client.Begin(ProcedureKind.Fragment,0,"7");first.Client.Refresh();
        check(!third.Client.Active && third.Error=="busy" && first.Client.Snapshot.Participants==8,"Shared operation capacity is bounded without evicting existing surgeons");
        foreach(var peer in visitors)peer.Client.Release();
        check(first.Do("fragment.grip",Point(0,.38f))==CommandStatus.NoChange,"First surgeon owns the first fragment gesture");
        check(second.Do("fragment.grip",Point(0,.38f))==CommandStatus.Rejected,"Second surgeon cannot steal an actively held fragment");
        check(second.Do("fragment.grip",Point(1,.38f))==CommandStatus.NoChange,"Second surgeon can hold a different fragment concurrently");
        first.Client.Refresh();second.Client.Refresh();
        check(Array.IndexOf(first.Client.Snapshot.BusyResources,"wound:1")>=0 && Array.IndexOf(first.Client.Snapshot.BusyResources,"wound:0")<0,"Snapshot exposes other surgeons' reserved wounds without blocking the viewer's own grip");
        third.Client.Begin(ProcedureKind.Fragment,0,"7");
        check(first.Client.Active && second.Client.Active,"Late join preserves existing participants");
        check(first.Do("fragment.release",Point(0,-1))==CommandStatus.Applied,"First surgeon extracts without resetting the other grip");
        check(first.Do("fragment.stitch",Stitch(0))==CommandStatus.Applied && fragment.shrapnel==2,"First surgeon completes one wound while another remains held");
        string stitchRequest=first.Request;float skin=fragment.skinHealth;
        check((int)JObject.Parse(router.Handle(a,stitchRequest))["receipt"]["Status"]==(int)CommandStatus.Duplicate && fragment.skinHealth==skin && fragment.shrapnel==2,"Cooperative completion replay cannot heal or remove a fragment twice");
        third.Client.Command("fragment.stitch",Stitch(0));
        check(third.Receipt.Status==CommandStatus.StaleRevision && fragment.shrapnel==2,"Same-wound stale completion is not rebased or healed twice");
        third.Client.Release();
        second.Client.Command("fragment.release",Point(1,-1));
        check(second.Receipt.Status==CommandStatus.Applied,"Other surgeon's held extraction survives unrelated completion without a stale-retry loop");
        check(second.Do("fragment.stitch",Stitch(1))==CommandStatus.Applied && fragment.shrapnel==1,"Second wound completes against current patient values");
        check(first.Do("fragment.grip",Point(2,.38f))==CommandStatus.NoChange,"Remaining wound can be held after concurrent completions");
        second.Client.Release();
        check(first.Do("fragment.release",Point(2,-1))==CommandStatus.Applied,"One participant leaving does not cancel another participant's grip");
        first.Client.Refresh();check(first.Client.Snapshot.Participants==1,"Shared snapshot removes a departed surgeon");
        third.Client.Begin(ProcedureKind.Fracture,1,"7");
        check(first.Client.Active && third.Client.Active,"Separate limbs on the same patient can run different procedures simultaneously");
        second.Client.Begin(ProcedureKind.InternalBleeding,1,"7");
        check(!second.Client.Active && second.Error=="busy","Body-wide internal bleeding conflicts with separate active procedures");
        var cut=new JObject {["section"]=0,["x"]=0,["y"]=120};
        check(third.Do("incision.cut",cut)==CommandStatus.Applied,"Independent limb operation mutates only its own incision");
        float debt=FractureStore.Get(chest).SkinDebt;
        router.Disconnect(a);first.Client.Refresh();
        check(!first.Client.Active && third.Client.Active && FractureStore.Get(chest).SkinDebt==debt,"Disconnect retires only that surgeon and preserves another operation's debt");
        third.Client.Release();second.Client.Reset();
        // Join internal surgery and verify host timers advance once, not once per participant.
        var state=InternalBleedingStore.Get(chest);
        for(int n=0;n<9;n++)chest.skinHealth-=state.Cut(n,chest.skinHealth,20,InternalBleedingStore.Rate(patient),42);
        state.Wounds[0].Pinch();state.Wounds[0].Seconds=5;
        second.Client.Begin(ProcedureKind.InternalBleeding,1,"7");third.Client.Begin(ProcedureKind.InternalBleeding,1,"7");
        check(second.Client.Active && third.Client.Active,"Multiple surgeons may join the same body-wide internal operation");
        now=.5;router.Tick();
        check(Math.Abs(state.Wounds[0].Seconds-4.5)<.001,"Shared internal timer advances once regardless of participant count");
        actors.Remove(b);router.Tick();third.Client.Refresh();
        check(third.Client.Active && third.Client.Snapshot.Participants==1,"Disconnected participant cannot retire a remaining internal surgeon");
        third.Client.Release();
        Consent(check);
        Bones(check);
        LeaseIsolation(check);
        InternalRepair(check);
    }
    static void Consent(Action<bool,string> check)
    {
        double now=0;var a=Guid.NewGuid();var b=Guid.NewGuid();
        var aa=new Body();var bb=new Body();var patient=new Body();patient.limbs=new[]{new Limb {body=patient,shrapnel=2,skinHealth=80}};
        Body Resolve(Guid id)=>id==a?aa:id==b?bb:null;
        var replies=new Dictionary<Body,Action<bool>>();
        var policy=new SurgeryTreatmentAccess(Resolve,p=>p==patient,()=>true,(actor,p,reply)=>replies[actor]=reply,()=>now);
        var router=new SurgeryHostRouter(Resolve,()=>now,inspectPatient:id=>patient,permission:policy);
        var first=new Peer(a,router,()=>now);var second=new Peer(b,router,()=>now);
        first.Client.Begin(ProcedureKind.Fragment,0,"7");replies[aa](true);first.Client.Begin(ProcedureKind.Fragment,0,"7");
        second.Client.Begin(ProcedureKind.Fragment,0,"7");
        check(first.Client.Active && !second.Client.Active && replies.ContainsKey(bb),"Joining surgeon needs independent patient consent");
        replies[bb](false);second.Client.Begin(ProcedureKind.Fragment,0,"7");
        check(!second.Client.Active && first.Client.Active,"Declining a joining surgeon does not revoke the existing surgeon");
        now=14;first.Client.Refresh();now=28;first.Client.Refresh();now=31;
        second.Client.Begin(ProcedureKind.Fragment,0,"7");replies[bb](true);second.Client.Begin(ProcedureKind.Fragment,0,"7");
        check(second.Client.Active,"Approved joining surgeon acquires an independent lease");
        second.Do("fragment.grip",Point(1,.38f));first.Client.Release();
        check(second.Do("fragment.release",Point(1,-1))==CommandStatus.Applied,"One surgeon leaving cannot revoke another surgeon's consent or grip");
        first.Client.Begin(ProcedureKind.Fragment,0,"7");replies[aa](true);first.Client.Begin(ProcedureKind.Fragment,0,"7");
        bb.transform.position=new UnityEngine.Vector3 {x=3.1f};router.Tick();first.Client.Refresh();second.Client.Refresh();
        check(first.Client.Active && !second.Client.Active && first.Client.Snapshot.Participants==1,"Movement revokes only the participant whose binding was violated");
        first.Client.Release();
    }
    static void Bones(Action<bool,string> check)
    {
        double now=0;var a=Guid.NewGuid();var b=Guid.NewGuid();var patient=new Body();
        var limb=new Limb {body=patient,broken=true,boneHealTimer=100,skinHealth=100};patient.limbs=new[]{limb};
        var state=FractureStore.Get(limb);for(int n=0;n<9;n++)limb.skinHealth-=state.Cut(n,limb.skinHealth);
        state.Angles[0]=state.Angles[1]=0;
        var actor=new Body();var policy=new SurgeryTreatmentAccess(id=>actor,p=>p==patient,()=>false,null,()=>now);
        var router=new SurgeryHostRouter(id=>actor,()=>now,inspectPatient:id=>patient,permission:policy);
        var first=new Peer(a,router,()=>now);var second=new Peer(b,router,()=>now);
        first.Client.Begin(ProcedureKind.Fracture,0,"7");second.Client.Begin(ProcedureKind.Fracture,0,"7");
        JObject Bone(int n)=>new JObject {["bone"]=n,["x"]=0,["y"]=n==0?-300:300};
        check(first.Do("bone.grip",Bone(0))==CommandStatus.NoChange && second.Do("bone.grip",Bone(1))==CommandStatus.NoChange,"Each surgeon can independently grip a different bone");
        check(second.Do("bone.grip",Bone(0))==CommandStatus.Rejected,"A surgeon cannot take another surgeon's bone");
        check(first.Do("bone.release",Bone(0))==CommandStatus.Applied,"First bone alignment preserves the other bone's gesture");
        second.Client.Command("bone.release",Bone(1));
        check(second.Receipt.Status==CommandStatus.Applied && state.Stage==FractureStage.Closure && state.BoneQualityCount==2,"Independent bone releases advance the shared operation once to closure");
        first.Client.Release();second.Client.Release();
    }
    static void LeaseIsolation(Action<bool,string> check)
    {
        var world=Guid.NewGuid();var a=Guid.NewGuid();var b=Guid.NewGuid();var leases=new PatientLeases(world);
        var first=new OperationKey(world,Guid.NewGuid(),"a",0,ProcedureKind.Fragment);
        var second=new OperationKey(world,Guid.NewGuid(),"b",0,ProcedureKind.Fragment);
        var aa=leases.Acquire(first,a,0,15,true);var bb=leases.Acquire(first,b,0,15,true);
        var other=leases.Acquire(second,a,0,15,true);
        check(aa!=null && bb!=null && other!=null,"Independent patients and explicit same-operation joins have separate tokens");
        check(leases.Acquire(new OperationKey(world,Guid.NewGuid(),"a",0,ProcedureKind.Fracture),Guid.NewGuid(),0,15,true)==null,"Joining cannot bypass a different procedure on the same limb");
        check(leases.Renew(first,b,bb.Token,10,15),"Remaining participant renews its own lease independently");
        check(!leases.Validate(first,a,aa.Token,16) && leases.Validate(first,b,bb.Token,16),"One participant can expire while another stays active");
        check(!leases.Release(first,a,bb.Token),"Participant cannot release another surgeon's token");
        leases.Disconnect(a);check(leases.Validate(first,b,bb.Token,16),"Disconnect leaves other participants' leases intact");
    }

    static void InternalRepair(Action<bool,string> check)
    {
        double now=0;var a=Guid.NewGuid();var b=Guid.NewGuid();var actor=new Body();var patient=new Body {internalBleeding=20};
        var chest=new Limb {body=patient,skinHealth=100};patient.limbs=new[]{new Limb {body=patient},chest};
        var state=InternalBleedingStore.Get(chest);
        for(int n=0;n<9;n++)chest.skinHealth-=state.Cut(n,chest.skinHealth,20,InternalBleedingStore.Rate(patient),42);
        foreach(var wound in state.Wounds){wound.Pools.Rolled=true;wound.Pools.First=wound.Pools.Second=0;}
        var policy=new SurgeryTreatmentAccess(id=>actor,p=>p==patient,()=>false,null,()=>now);
        var router=new SurgeryHostRouter(id=>actor,()=>now,inspectPatient:id=>patient,permission:policy);
        var first=new Peer(a,router,()=>now);var second=new Peer(b,router,()=>now);
        first.Client.Begin(ProcedureKind.InternalBleeding,1,"7");second.Client.Begin(ProcedureKind.InternalBleeding,1,"7");
        int[] cuts=Enumerable.Range(0,state.Wounds.Length).Where(i=>state.Wounds[i].Laceration).ToArray();
        JObject Wound(int index,bool edge=false)
        {
            var w=state.Wounds[index];double angle=w.Angle*Math.PI/180;
            return new JObject {["wound"]=index,["x"]=w.X+(edge?32*Math.Cos(angle):0),["y"]=w.Y+(edge?32*Math.Sin(angle):0)};
        }
        check(first.Do("laceration.grip",Wound(cuts[0],true))==CommandStatus.NoChange && second.Do("laceration.grip",Wound(cuts[1],true))==CommandStatus.NoChange,"Different internal lacerations have independent surgeon grips");
        check(first.Do("laceration.drag",Wound(cuts[0]))==CommandStatus.Applied,"First laceration closes independently");
        second.Client.Command("laceration.drag",Wound(cuts[1]));
        check(second.Receipt.Status==CommandStatus.Applied && state.Wounds[cuts[0]].Phase==InternalWoundPhase.Pinched && state.Wounds[cuts[1]].Phase==InternalWoundPhase.Pinched,"Unrelated laceration closure rebases without resetting either wound timer");
        foreach(int index in cuts)
        {
            if(state.Wounds[index].Phase==InternalWoundPhase.Open){first.Do("laceration.grip",Wound(index,true));first.Do("laceration.drag",Wound(index));}
            var stitch=Stitch(index);stitch["wound"]=index;stitch.Remove("site");
            check(first.Do("wound.stitch",stitch)==CommandStatus.Applied,"Shared internal laceration sutures exactly once: "+index);
        }
        foreach(int index in Enumerable.Range(0,state.Wounds.Length).Where(i=>!state.Wounds[i].Laceration))
        {
            for(int n=0;n<4;n++){now+=.25;var gel=Wound(index);gel["seconds"]=.25;second.Do("wound.gel",gel);}
        }
        check(state.Stage==InternalStage.Closure,"Shared repair converges to a single closure stage");
        // Keep this test about shared clinical completion, not random outer pool generation.
        state.ClosurePools.Rolled=true;state.ClosurePools.First=state.ClosurePools.Second=0;
        first.Client.Refresh();second.Client.Refresh();
        var closure=Stitch(0);closure.Remove("site");
        check(first.Do("incision.close",closure)==CommandStatus.Applied && state.Closed,"One surgeon commits shared incision closure");
        float skin=chest.skinHealth,bleeding=patient.internalBleeding;
        second.Do("incision.close",closure);
        check(chest.skinHealth==skin && patient.internalBleeding==bleeding,"Second surgeon cannot repeat shared closure refunds or internal healing");
        first.Client.Release();second.Client.Release();
    }

}
