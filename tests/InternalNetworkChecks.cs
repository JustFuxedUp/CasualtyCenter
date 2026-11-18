using System;
using System.Collections.Generic;
using CasualtiesSurgery.Game;
using CasualtiesSurgery.Procedures;
using Newtonsoft.Json.Linq;

static class InternalNetworkChecks
{
    public static void Run(Action<bool,string> check)
    {
        // Same wire path for self-treatment and a remote unconscious patient.
        foreach(bool remote in new[]{false,true})
        {
            double now=0;var actorId=Guid.NewGuid();var ownerId=Guid.NewGuid();
            var patient=new Body {internalBleeding=20,conscious=!remote};var chest=new Limb {body=patient,skinHealth=100};patient.limbs=new[]{new Limb {body=patient},chest};
            var actor=remote?new Body():patient;if(remote)actor.limbs=new[]{new Limb {body=actor}};
            Body Resolve(Guid id)=>id==actorId?actor:id==ownerId?patient:null;
            var permission=new SurgeryTreatmentAccess(Resolve,p=>ReferenceEquals(p,patient),()=>true,(a,p,r)=>throw new Exception("Unexpected prompt"),()=>now);
            var router=new SurgeryHostRouter(Resolve,()=>now,inspectPatient:id=>id=="1"?patient:null,permission:permission);
            var queue=new Queue<string>();var client=new SurgeryClientSession(t=>{queue.Enqueue(t);return true;},()=>now);string failure=null;client.Failed+=e=>failure=e;
            void Deliver()=>client.Receive(router.Handle(actorId,queue.Dequeue()));
            void Send(string action,JObject payload){if(!client.Command(action,payload))throw new Exception("Command not queued: "+action);Deliver();}
            client.Begin(ProcedureKind.InternalBleeding,1,remote?"1":null);Deliver();
            for(int i=0;i<9;i++)Send("incision.cut",new JObject { ["section"]=i,["x"]=0,["y"]=120-i*30 });
            var state=InternalBleedingStore.Get(chest);
            check(state.Stage==InternalStage.Repair && state.SkinDebt>0 && patient.internalBleeding==20,"Wire incision charges patient skin without prematurely healing internal bleeding; remote="+remote);
            float skin=chest.skinHealth;var saved=state.Wounds[0];
            client.Release();Deliver();client.Begin(ProcedureKind.InternalBleeding,1,remote?"1":null);Deliver();
            check(ReferenceEquals(state.Wounds[0],saved) && chest.skinHealth==skin,"Wire interruption/resume preserves wound layout and skin debt; remote="+remote);
            const string stroke="[{\"x\":-1,\"y\":0.1},{\"x\":1,\"y\":0.3},{\"x\":-1,\"y\":0.5},{\"x\":1,\"y\":0.7}]";
            for(int index=0;index<state.Wounds.Length;index++)
            {
                var wound=state.Wounds[index];
                // Renew before selecting contacts so timer/pool changes reach the client revision.
                now+=.25;client.Refresh();Deliver();
                for(int slot=0;slot<2;slot++)if(wound.Pools.Remaining(slot)>0)
                {now+=.25;client.Refresh();Deliver();Send("pool.suction",new JObject { ["wound"]=index,["slot"]=slot,["x"]=wound.X+(slot==0?0:37),["y"]=wound.Y+(slot==0?0:-25),["seconds"]=.25f });}
                if(wound.Laceration)
                {
                    double angle=wound.Angle*Math.PI/180;
                    Send("laceration.grip",new JObject { ["wound"]=index,["x"]=wound.X+32*Math.Cos(angle),["y"]=wound.Y+32*Math.Sin(angle) });
                    Send("laceration.drag",new JObject { ["wound"]=index,["x"]=wound.X,["y"]=wound.Y });
                    Send("wound.stitch",new JObject { ["wound"]=index,["samples"]=JArray.Parse(stroke) });
                }
                else for(int dose=0;dose<4;dose++)
                {now+=.25;client.Refresh();Deliver();Send("wound.gel",new JObject { ["wound"]=index,["x"]=wound.X,["y"]=wound.Y,["seconds"]=.25f });}
            }
            check(state.Stage==InternalStage.Closure && failure==null,"Wire forceps, suture, gel and suction finish all internal repairs; remote="+remote);
            client.Command("incision.close",new JObject { ["samples"]=JArray.Parse(stroke) });string closure=queue.Dequeue();
            var response=router.Handle(actorId,closure);float healed=patient.internalBleeding;skin=chest.skinHealth;
            check(state.Closed && Math.Abs(InternalBleedingStore.Rate(patient)-.04f)<.0001f && skin>90,"Wire internal completion restores skin and reaches 0.04 L/min floor; remote="+remote);
            var duplicate=router.Handle(actorId,closure);client.Receive(duplicate);client.Receive(response);
            check(patient.internalBleeding==healed && chest.skinHealth==skin && (int)JObject.Parse(duplicate)["receipt"]["Status"]==(int)CommandStatus.Duplicate,"Lost closure acknowledgement cannot heal twice; remote="+remote);
        }
    }
}
