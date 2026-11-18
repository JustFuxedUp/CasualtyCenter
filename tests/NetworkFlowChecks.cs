using System;
using System.Collections.Generic;
using CasualtiesSurgery.Game;
using CasualtiesSurgery.Procedures;
using Newtonsoft.Json.Linq;

static class NetworkFlowChecks
{
    public static void Run(Action<bool,string> check)
    {
        double now=0;var actor=Guid.NewGuid();var other=Guid.NewGuid();
        var body=new Body();var limb=new Limb {body=body,skinHealth=80,broken=true,boneHealTimer=100};body.limbs=new[]{limb};
        var router=new SurgeryHostRouter(id=>id==actor?body:null,()=>now);
        var packets=new Queue<string>();
        var client=new SurgeryClientSession(text=>{packets.Enqueue(text);return true;},()=>now);
        int snapshots=0,completed=0;CommandReceipt last=null;string error=null;
        client.SnapshotChanged+=s=>snapshots++;
        client.Completed+=r=>{completed++;last=r;};client.Failed+=e=>error=e;
        check(client.Begin(ProcedureKind.Fracture,0) && client.Busy,"Client queues an explicit open handshake");
        string open=packets.Dequeue();string lostReply=router.Handle(actor,open);
        check((bool)JObject.Parse(lostReply)["ok"],"Host routes authenticated open to real procedure target");
        now=1.1;client.Tick();string retry=packets.Dequeue();
        check(retry==open,"Client retries identical open bytes after lost reply");
        client.Receive(router.Handle(actor,retry));
        check(client.Active && !client.Busy && snapshots==1 && error==null,"Retried open returns same lease and current snapshot");
        client.Command("incision.cut",new JObject { ["section"]=0,["x"]=0,["y"]=120 });
        string cut=packets.Dequeue();string cutReply=router.Handle(actor,cut);float skin=limb.skinHealth;
        check(skin<80 && client.Busy,"Host commits while client waits without speculative health write");
        now+=1.1;client.Tick();retry=packets.Dequeue();client.Receive(router.Handle(actor,retry));
        check(retry==cut && limb.skinHealth==skin && last.Status==CommandStatus.Duplicate && last.OriginalStatus==CommandStatus.Applied,"Lost action acknowledgement retries without charging skin twice");
        client.Receive(cutReply);
        check(completed==1,"Late first acknowledgement cannot duplicate completion event");
        var forged=JObject.Parse(cut);forged["requestId"]="forged";
        var denied=JObject.Parse(router.Handle(other,forged.ToString()));
        check((int)denied["receipt"]["Status"]==(int)CommandStatus.Unauthorized && limb.skinHealth==skin,"Other connection cannot replay stolen command envelope");
        now+=5.1;client.Tick();client.Receive(router.Handle(actor,packets.Dequeue()));
        check(client.Active && !client.Busy && error==null,"Idle client renews lease and consumes fresh snapshot");
        check(client.Release(),"Client can explicitly release operation lease");client.Receive(router.Handle(actor,packets.Dequeue()));
        check(!client.Active && FractureStore.HasOpen(limb),"Release preserves clinical incision and debt");
        check(client.Begin(ProcedureKind.Fracture,0),"Client can resume saved operation");client.Receive(router.Handle(actor,packets.Dequeue()));
        check(client.Active && client.Snapshot.Revision>0,"Resume retains authoritative revision");
        client.Command("incision.cut",new JObject { ["section"]=1,["x"]=0,["y"]=90 });packets.Dequeue();
        for(int i=0;i<5;i++){now+=1.1;client.Tick();}
        check(!client.Active && error!=null && !FractureStore.Get(limb).Cuts[1],"Unanswered request times out without inventing treatment");
        check(!(bool)JObject.Parse(router.Handle(actor,"{broken"))["ok"],"Malformed wire JSON rejected without state changes");
        var loadedBody=new Body {internalBleeding=20};var chest=new Limb {body=loadedBody,skinHealth=100};loadedBody.limbs=new[]{new Limb {body=loadedBody},chest};
        var pending=InternalBleedingStore.Get(chest);
        for(int i=0;i<9;i++)chest.skinHealth-=pending.Cut(i,chest.skinHealth,loadedBody.internalBleeding,InternalBleedingStore.Rate(loadedBody),42);
        pending.Wounds[0].Pinch();pending.Wounds[0].Seconds=.1f;
        double loadedTime=0;var loadedRouter=new SurgeryHostRouter(c=>c==actor?loadedBody:null,()=>loadedTime);
        loadedRouter.Track(actor);loadedTime=.2;loadedRouter.Tick();
        check(pending.Wounds[0].Phase==InternalWoundPhase.Open && pending.Wounds[0].Pools.Blocking,"Loaded open incision resumes host timers before any UI request");

        var incompatible=JObject.Parse(open);incompatible["requestId"]="wrong-version";incompatible["version"]=99;
        check((string)JObject.Parse(router.Handle(actor,incompatible.ToString()))["error"]=="protocol-version","Handshake rejects unsupported protocol before opening a target");
        client.Reset();packets.Clear();error=null;
        now+=16;router.Tick();
        client.Begin(ProcedureKind.Fracture,0);var delayedOpen=router.Handle(actor,packets.Dequeue());
        check(client.Release() && client.Busy,"Closing during open handshake defers release until authority replies");
        client.Receive(delayedOpen);
        var deferredRelease=packets.Dequeue();
        check((string)JObject.Parse(deferredRelease)["type"]=="release" && !client.Command("incision.cut",new JObject()),"Deferred release prevents additional treatment intents");
        client.Receive(router.Handle(actor,deferredRelease));
        check(!client.Active && !client.Busy && FractureStore.HasOpen(limb),"Deferred release ends lease while preserving incision");
        client.Begin(ProcedureKind.Fracture,0);var malformed=JObject.Parse(router.Handle(actor,packets.Dequeue()));
        malformed.Remove("snapshot");client.Receive(malformed.ToString());
        check(!client.Active && error!=null,"Missing opening snapshot fails closed without creating a usable client session");
    }
}
