using System;
using System.Collections.Generic;
using CasualtiesSurgery.Game;
using CasualtiesSurgery.Procedures;
using CUCoreLib.Saving;
using Newtonsoft.Json.Linq;

static class RecoveryChecks
{
    public static void Run(Action<bool,string> check)
    {
        double now=0;var actorId=Guid.NewGuid();var actor=new Body();actor.limbs=new[]{new Limb {body=actor}};
        var patient=new Body();patient.limbs=new[]{new Limb {body=patient}};
        bool path=true;Action<bool> answer=null;int prompts=0;
        var permission=new SurgeryTreatmentAccess(c=>c==actorId?actor:null,b=>ReferenceEquals(b,patient),()=>true,(a,p,r)=>{answer=r;prompts++;},()=>now,(a,p)=>path);
        path=false;check(permission.Check(actorId,patient,true).Contains("blocked") && prompts==0,"Obstruction rejects treatment before prompting");
        path=true;permission.Check(actorId,patient,true);var expired=answer;now=31;expired(true);
        check(permission.Check(actorId,patient)!=null,"Late native consent callback cannot approve an expired offer");
        permission.Check(actorId,patient,true);var disconnected=answer;permission.Disconnect(actorId);disconnected(true);
        check(permission.Check(actorId,patient)!=null,"Disconnect cancels pending consent even if the same body returns");
        permission.Check(actorId,patient,true);var replaced=answer;permission.ForgetBody(patient);replaced(true);
        check(permission.Check(actorId,patient)!=null,"Retiring a patient cancels its pending consent callback");
        permission.Check(actorId,patient,true);path=false;answer(true);path=true;
        check(permission.Check(actorId,patient)!=null,"Approval revalidates obstruction instead of trusting the earlier request");
        permission.Reset();permission.Check(actorId,patient,true);answer(true);var access=permission.Bind(actorId,patient);
        check(access(),"Approved operation binds current participants");path=false;
        check(!access(),"New obstruction revokes ongoing treatment access");path=true;permission.End(actorId,patient);

        // Restore onto a replacement body while old network commands are still in flight.
        var body=new Body();var limb=new Limb {body=body,skinHealth=80,broken=true,boneHealTimer=100};body.limbs=new[]{limb};
        var router=new SurgeryHostRouter(c=>c==actorId?body:null,()=>now);
        var queue=new Queue<string>();var client=new SurgeryClientSession(t=>{queue.Enqueue(t);return true;},()=>now);
        void Deliver()=>client.Receive(router.Handle(actorId,queue.Dequeue()));
        client.Begin(ProcedureKind.Fracture,0);string oldOpen=queue.Dequeue();client.Receive(router.Handle(actorId,oldOpen));
        client.Command("incision.cut",new JObject { ["section"]=0,["x"]=0,["y"]=120 });string oldCut=queue.Dequeue();client.Receive(router.Handle(actorId,oldCut));
        now+=.25;client.Command("incision.prepare",new JObject { ["section"]=1,["x"]=0,["y"]=90,["seconds"]=.25 });Deliver();
        var oldBody=body;float skin=limb.skinHealth;var provider=new FractureStore();var saved=provider.Capture(limb,0);var before=FractureStore.Get(limb);
        var oldOperation=client.Snapshot.Operation;
        router.ForgetPatient(oldBody);body=new Body();limb=new Limb {body=body,skinHealth=skin,broken=true,boneHealTimer=100};body.limbs=new[]{limb};
        provider.Restore(limb,0,saved,provider.GetVersion(),new SaveRestoreContext());router.Track(actorId);
        check((int)JObject.Parse(router.Handle(actorId,oldCut))["receipt"]["Status"]==(int)CommandStatus.Unauthorized && limb.skinHealth==skin,"Retired-body command cannot mutate a restored replacement");
        check((string)JObject.Parse(router.Handle(actorId,oldOpen))["error"]=="request-reused","Old opening request cannot silently target a replacement body");
        client.Reset();client.Begin(ProcedureKind.Fracture,0);Deliver();var restored=FractureStore.Get(limb);
        check(client.Active && !client.Snapshot.Operation.Equals(oldOperation) && restored.Cuts[0] && restored.SkinDebt==before.SkinDebt && restored.GelCoverage[1]==before.GelCoverage[1],"Save restore creates a fresh network episode while preserving skin debt, cut and gel");
        client.Release();Deliver();

        // Fragment save progress is independent of transient network registration cleanup.
        var fragments=new Body();var shard=new Limb {body=fragments,skinHealth=70,shrapnel=2};fragments.limbs=new[]{shard};
        var board=FragmentBoardStore.Get(shard);board.Sites[0].Extracted=true;board.Sites[0].GelMl=.2f;board.Sites[1].Pools.Observe(true,0);board.Sites[1].Pools.Suction(0,.1f);
        var fragmentProvider=new FragmentBoardStore();var fragmentSave=fragmentProvider.Capture(shard,0);int seed=board.Seed,id=board.Sites[0].Id;
        var loaded=new Body();var loadedShard=new Limb {body=loaded,skinHealth=70,shrapnel=2};loaded.limbs=new[]{loadedShard};var restore=new SaveRestoreContext();
        fragmentProvider.Restore(loadedShard,0,fragmentSave,fragmentProvider.GetVersion(),restore);restore.Flush();
        body=loaded;client.Reset();client.Begin(ProcedureKind.Fragment,0);Deliver();var restoredBoard=FragmentBoardStore.Get(loadedShard);
        check(client.Active && restoredBoard.Seed==seed && restoredBoard.Sites[0].Id==id && restoredBoard.Sites[0].Extracted && restoredBoard.Sites[0].GelMl==.2f && Math.Abs(restoredBoard.Sites[1].Pools.First-.15f)<.001f,"Restored fragment session preserves stable layout, pending suture, gel and partial suction");
        router.Disconnect(actorId);check(FragmentBoardStore.HasPendingSuture(loadedShard),"Disconnect preserves pending fragment closure");router.ForgetPatient(loaded);

        // Repeated respawns must not exhaust the router's bounded target registry.
        bool allOpened=true;
        for(int generation=0;generation<270;generation++)
        {
            body=new Body();body.limbs=new[]{new Limb {body=body,skinHealth=80,broken=true,boneHealTimer=100}};
            client.Reset();client.Begin(ProcedureKind.Fracture,0);Deliver();allOpened&=client.Active;router.ForgetPatient(body);
        }
        check(allOpened,"Retiring disconnected body registrations prevents capacity exhaustion across respawns");
    }
}
