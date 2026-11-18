using System;
using System.Collections.Generic;
using CasualtiesSurgery.Game;
using CasualtiesSurgery.Procedures;
using Newtonsoft.Json.Linq;

static class SharedTreatmentChecks
{
    public static void Run(Action<bool,string> check)
    {
        double now=0;bool consent=true;var surgeonId=Guid.NewGuid();var patientId=Guid.NewGuid();
        var surgeon=new Body();surgeon.limbs=new[]{new Limb {body=surgeon,skinHealth=90}};
        var patient=new Body();var limb=new Limb {body=patient,skinHealth=80,broken=true,boneHealTimer=100};patient.limbs=new[]{limb,new Limb {body=patient,skinHealth=80,broken=true,boneHealTimer=100}};
        Body liveSurgeon=surgeon,livePatient=patient;Action<bool> answer=null;int prompts=0;
        Body Resolve(Guid id)=>id==surgeonId?liveSurgeon:id==patientId?livePatient:null;
        var policy=new SurgeryTreatmentAccess(Resolve,p=>ReferenceEquals(p,livePatient)||ReferenceEquals(p,liveSurgeon),()=>consent,(a,p,reply)=>{prompts++;answer=reply;},()=>now);
        var router=new SurgeryHostRouter(Resolve,()=>now,inspectPatient:id=>id=="7"?livePatient:null,permission:policy);
        var packets=new Queue<string>();var client=new SurgeryClientSession(t=>{packets.Enqueue(t);return true;},()=>now);
        string error=null;client.Failed+=e=>error=e;
        string Deliver(){string request=packets.Dequeue();string response=router.Handle(surgeonId,request);client.Receive(response);return request;}
        client.Begin(ProcedureKind.Fracture,0,"7");Deliver();
        check(!client.Active && prompts==1 && error.Contains("consent") && limb.skinHealth==80,"Remote operation asks patient before acquiring a lease or changing health");
        client.Begin(ProcedureKind.Fracture,0,"7");Deliver();check(prompts==1,"Repeated requests reuse the pending prompt");
        answer(false);client.Begin(ProcedureKind.Fracture,0,"7");Deliver();check(error.Contains("declined") && prompts==1,"Declined offer does not immediately reprompt");
        now=31;client.Begin(ProcedureKind.Fracture,0,"7");Deliver();answer(true);answer(false);
        client.Begin(ProcedureKind.Fracture,0,"7");Deliver();check(client.Active,"Accepted patient prompt permits one operator lease; repeated callback cannot reverse it");
        string selfOpen=new JObject { ["requestId"]="patient-competes",["type"]="open",["version"]=1,["procedure"]=(int)ProcedureKind.Fracture,["limb"]=0 }.ToString();
        check((bool)JObject.Parse(router.Handle(patientId,selfOpen))["ok"] && client.Active,"Patient can join a remote surgeon without evicting the existing participant");
        var otherLimb=JObject.Parse(selfOpen);otherLimb["limb"]=1;otherLimb["requestId"]="other-limb";
        check((bool)JObject.Parse(router.Handle(patientId,otherLimb.ToString()))["ok"],"Independent limbs permit concurrent operations on the same patient");
        client.Command("incision.cut",new JObject { ["section"]=0,["x"]=0,["y"]=120 });string cut=packets.Dequeue();
        var applied=JObject.Parse(router.Handle(surgeonId,cut));float skin=limb.skinHealth;
        check(skin<80 && surgeon.limbs[0].skinHealth==90 && ReferenceEquals(router.ResultPatient(applied),patient),"Remote action changes and identifies the patient, never the operator");
        var retry=JObject.Parse(router.Handle(surgeonId,cut));client.Receive(retry.ToString());
        check((int)retry["receipt"]["Status"]==(int)CommandStatus.Duplicate && limb.skinHealth==skin,"Lost remote action acknowledgement cannot charge the patient twice");
        surgeon.transform.position=new UnityEngine.Vector3 {x=3.1f};router.Tick();
        check((int)JObject.Parse(router.Handle(surgeonId,cut))["receipt"]["Status"]==(int)CommandStatus.Unauthorized && limb.skinHealth==skin,"Operator movement invalidates lease and even duplicate commands");
        surgeon.transform.position=new UnityEngine.Vector3();client.Reset();client.Begin(ProcedureKind.Fracture,0,"7");Deliver();
        check(!client.Active && prompts==3,"Interrupted remote operation requires renewed consent");
        answer(true);client.Begin(ProcedureKind.Fracture,0,"7");Deliver();
        check(client.Active && FractureStore.Get(limb).Cuts[0],"Consented resume preserves completed incision work");
        client.Release();Deliver();check(policy.Check(surgeonId,patient)!=null,"Releasing surgery ends the consent grant");
        patient.conscious=false;client.Begin(ProcedureKind.Fracture,0,"7");Deliver();check(client.Active && prompts==3,"Unconscious patient can be treated without a prompt");
        client.Command("incision.cut",new JObject { ["section"]=1,["x"]=0,["y"]=90 });Deliver();
        check(FractureStore.Get(limb).Cuts[1],"Clinical treatment allows unconscious patient with a conscious operator");
        patient.conscious=true;router.Tick();client.Command("incision.cut",new JObject { ["section"]=2,["x"]=0,["y"]=60 });Deliver();
        check(!client.Active && !FractureStore.Get(limb).Cuts[2],"Waking patient requires consent before further treatment");
        consent=false;client.Begin(ProcedureKind.Fracture,0,"7");Deliver();check(client.Active,"Host feeding-consent rule off permits nearby remote treatment");
        liveSurgeon=new Body {limbs=surgeon.limbs};router.Tick();
        client.Command("incision.cut",new JObject { ["section"]=2,["x"]=0,["y"]=60 });Deliver();
        check(!client.Active && !FractureStore.Get(limb).Cuts[2],"Changing operator body cannot inherit an existing lease");liveSurgeon=surgeon;
        consent=true;client.Begin(ProcedureKind.Fracture,0,"7");Deliver();var oldAnswer=answer;policy.Reset();oldAnswer(true);
        check(policy.Check(surgeonId,patient)!=null,"Delayed consent cannot survive a world/transport reset");
        patient.conscious=false;livePatient=null;
        check(policy.Check(surgeonId,patient)!=null,"Unconscious exception never bypasses patient disconnect");livePatient=patient;
        surgeon.conscious=false;check(policy.Check(surgeonId,patient)!=null,"Unconscious operator cannot treat an unconscious patient");surgeon.conscious=true;
        patient.conscious=false;patient.transform.position=new UnityEngine.Vector3 {x=3.5f};
        check(policy.Check(surgeonId,patient)==null,"Remote treatment permits the expanded reach");
        patient.transform.position=new UnityEngine.Vector3 {x=4.1f};
        check(policy.Check(surgeonId,patient)!=null,"Expanded reach still rejects distant treatment");
        patient.transform.position=new UnityEngine.Vector3();
        patient.alive=false;check(policy.Check(surgeonId,patient)!=null,"Consent never permits treatment of a dead patient");
    }
}
