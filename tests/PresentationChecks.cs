using System;
using System.Collections.Generic;
using CasualtiesSurgery.Game;
using CasualtiesSurgery.Procedures;
using Newtonsoft.Json.Linq;

static class PresentationChecks
{
    public static void Run(Action<bool,string> check)
    {
        double now=0;var actor=Guid.NewGuid();var body=new Body();var limb=new Limb {body=body,skinHealth=80,broken=true,boneHealTimer=100};body.limbs=new[]{limb};
        var router=new SurgeryHostRouter(c=>c==actor?body:null,()=>now);
        var packets=new Queue<string>();var client=new SurgeryClientSession(text=>{packets.Enqueue(text);return true;},()=>now);
        var view=new SurgeryPresentation(client,ProcedureKind.Fracture,()=>now);int results=0,resets=0;
        view.Result+=(a,r)=>results++;view.InputReset+=()=>resets++;
        void Deliver(){client.Receive(router.Handle(actor,packets.Dequeue()));}
        check(view.Begin(0) && !view.Ready,"Authority view waits for opening snapshot before input");Deliver();
        check(view.Ready && !ReferenceEquals(view.Fracture,FractureStore.Get(limb)),"Authority view renders a detached validated replica");
        view.Submit("incision.cut",new JObject { ["section"]=0,["x"]=0,["y"]=120 });view.Tick();
        check(!view.Fracture.Cuts[0] && limb.skinHealth==80,"Queued view intent does not predict clinical state");
        string cut=packets.Dequeue();string lost=router.Handle(actor,cut);float skin=limb.skinHealth;
        now=1.1;client.Tick();check(packets.Peek()==cut,"Presentation retains exact in-flight bytes across lost acknowledgement");Deliver();
        client.Receive(lost);
        check(view.Fracture.Cuts[0] && limb.skinHealth==skin && results==1,"Retried view action refreshes replica and emits result once");
        view.Fracture.SkinDebt=0;
        check(FractureStore.Get(limb).SkinDebt>0,"Changing a presentation replica cannot alter host incision debt");
        // Queue a held dose behind a pending poll, replace its latest contact, retain release.
        now+=.3;view.Tick();
        view.Submit("incision.prepare",new JObject { ["section"]=1,["x"]=0,["y"]=90,["seconds"]=.1 },"prep:1");
        view.Submit("incision.prepare",new JObject { ["section"]=1,["x"]=1,["y"]=90,["seconds"]=.1 },"prep:1");
        view.Submit("gesture.cancel",new JObject());Deliver();view.Tick();
        var coalesced=JObject.Parse(packets.Peek());var payload=JObject.Parse((string)coalesced["command"]["Payload"]);
        check(Math.Abs((double)payload["seconds"]-.2)<.0001 && (int)payload["x"]==1,"Held input coalesces time only for the same target and keeps latest contact");
        Deliver();view.Tick();
        check((string)JObject.Parse(packets.Peek())["command"]["Action"]=="gesture.cancel","Cancellation remains ordered after the held intent");Deliver();
        view.Submit("bone.drag",new JObject { ["bone"]=0,["x"]=0,["y"]=0 });
        view.Submit("incision.cut",new JObject { ["section"]=1,["x"]=0,["y"]=90 });view.Tick();Deliver();view.Tick();
        check(resets==1 && (string)JObject.Parse(packets.Peek())["command"]["Action"]=="gesture.cancel" && !FractureStore.Get(limb).Cuts[1],"Rejected gesture clears dependent queued input instead of applying it later");Deliver();
        for(int n=0;n<33;n++)view.Submit("gesture.cancel",new JObject());view.Tick();
        check(resets==2 && packets.Count==1 && (string)JObject.Parse(packets.Peek())["command"]["Action"]=="gesture.cancel","Input queue overflow resets to cancellation rather than losing terminal evidence");Deliver();
        // Read-only overview uses a separate client and never competes for the operation lease.
        JArray overview=null;SurgeryClientSession inspector=null;
        inspector=new SurgeryClientSession(text=>{inspector.Receive(router.Handle(actor,text));return true;},()=>now);inspector.OverviewReceived+=v=>overview=v;
        inspector.Inspect();check((bool)overview[0]["fracture"] && client.Active,"Patient overview reports open incision without taking or releasing its lease");
        view.Submit("incision.cut",new JObject { ["section"]=1,["x"]=0,["y"]=90 });view.Tick();view.Dispose();Deliver();
        check(client.Busy && (string)JObject.Parse(packets.Peek())["type"]=="release","View disposal defers release until a pending action is acknowledged");Deliver();
        check(!client.Active && FractureStore.HasOpen(limb),"Disposed view leaves completed host work and debt intact");

        // A real timer change makes a queued intent stale; the presentation resubmits against
        // the returned revision without issuing another sequence or a duplicate reward.
        var chestBody=new Body {internalBleeding=20};var chest=new Limb {body=chestBody,skinHealth=100};chestBody.limbs=new[]{new Limb {body=chestBody},chest};
        var inner=InternalBleedingStore.Get(chest);for(int i=0;i<9;i++)chest.skinHealth-=inner.Cut(i,chest.skinHealth,20,InternalBleedingStore.Rate(chestBody),42);
        inner.Wounds[0].Pinch();inner.Wounds[0].Seconds=.1f;
        var innerRouter=new SurgeryHostRouter(c=>c==actor?chestBody:null,()=>now);var innerPackets=new Queue<string>();
        var innerClient=new SurgeryClientSession(text=>{innerPackets.Enqueue(text);return true;},()=>now);var innerView=new SurgeryPresentation(innerClient,ProcedureKind.InternalBleeding,()=>now);
        innerView.Begin(1);innerClient.Receive(innerRouter.Handle(actor,innerPackets.Dequeue()));
        innerView.Submit("gesture.cancel",new JObject());innerView.Tick();now+=.2;
        innerClient.Receive(innerRouter.Handle(actor,innerPackets.Dequeue()));innerView.Tick();
        var resend=JObject.Parse(innerPackets.Peek())["command"];
        check((long)resend["Sequence"]==1 && (long)resend["Revision"]>0,"Stale view intent retries with refreshed revision and unconsumed sequence");
        innerClient.Receive(innerRouter.Handle(actor,innerPackets.Dequeue()));innerView.Dispose();

        var fragmentBody=new Body();var fragment=new Limb {body=fragmentBody,skinHealth=80,shrapnel=1};fragmentBody.limbs=new[]{fragment};
        var fragmentRouter=new SurgeryHostRouter(c=>c==actor?fragmentBody:null,()=>now,scale:.7f);SurgeryClientSession local=null;
        local=new SurgeryClientSession(text=>{local.Receive(fragmentRouter.Handle(actor,text));return true;},()=>now);
        var localView=new SurgeryPresentation(local,ProcedureKind.Fragment,()=>now);localView.Begin(0);
        check(localView.Ready && Math.Abs(localView.ViewScale-.7f)<.0001,"Local loopback uses the same opening handshake and host geometry settings");
        CommandReceipt feedback=null;localView.Result+=(a,r)=>feedback=r;
        var sample=new JObject { ["x"]=0,["y"]=0,["active"]=true,["seconds"]=.25 };
        now+=.25;localView.Submit("scalpel.sample",sample);localView.Tick();now+=.25;localView.Submit("scalpel.sample",sample);localView.Tick();
        check(fragment.skinHealth<80 && feedback.Feedback?.Rating==TreatmentRating.Mistake,"Fragment authority view preserves scalpel mistake behavior through the shared command path");
        localView.Dispose();
    }
}
