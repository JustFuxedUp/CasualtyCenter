using System;
using CasualtiesSurgery.Game;
using CasualtiesSurgery.Procedures;
using Newtonsoft.Json.Linq;

static class RemoteOverviewChecks
{
    public static void Run(Action<bool,string> check)
    {
        var actor=Guid.NewGuid();var owner=Guid.NewGuid();double now=0;
        var viewer=new Body();viewer.limbs=new[]{new Limb {body=viewer}};
        var patient=new Body();var limb=new Limb {body=patient,skinHealth=80,broken=true,boneHealTimer=100};patient.limbs=new[]{limb};
        Body visible=patient;
        var router=new SurgeryHostRouter(c=>c==actor?viewer:c==owner?patient:null,()=>now,inspectPatient:id=>id=="7"?visible:null);
        SurgeryClientSession surgeon=null,reader=null;JArray markers=null;string failure=null;
        surgeon=new SurgeryClientSession(t=>{surgeon.Receive(router.Handle(owner,t));return true;},()=>now);
        reader=new SurgeryClientSession(t=>{reader.Receive(router.Handle(actor,t));return true;},()=>now);
        reader.OverviewReceived+=v=>markers=v;reader.Failed+=v=>failure=v;
        surgeon.Begin(ProcedureKind.Fracture,0);
        surgeon.Command("incision.cut",new JObject { ["section"]=0,["x"]=0,["y"]=120 });
        float skin=limb.skinHealth;
        reader.Inspect("7");
        check((bool)markers[0]["fracture"] && !reader.Active && surgeon.Active && limb.skinHealth==skin,"Remote overview reports host-owned incision without a lease or mutation");
        var choices = markers[0]["eligibility"];
        check(choices["Fracture"].Type == JTokenType.Null &&
            choices["Fragment"].Type == JTokenType.String && choices["InternalBleeding"].Type == JTokenType.String,
            "Host overview distinguishes available fracture from unavailable procedures on the same limb");
        reader.Inspect();check(!(bool)markers[0]["fracture"],"Self overview stays bound to authenticated viewer rather than last remote patient");
        patient.transform.position=new UnityEngine.Vector3 {x=6};reader.Inspect("7");
        check(failure=="patient-out-of-range","Host rejects distant remote inspection");
        patient.transform.position=new UnityEngine.Vector3 {x=float.NaN};failure=null;reader.Inspect("7");
        check(failure=="patient-out-of-range","Nonfinite positions cannot bypass inspection range");
        patient.transform.position=new UnityEngine.Vector3();patient.gameObject.scene=new UnityEngine.Scene {handle=2};reader.Inspect("7");
        check(failure=="patient-out-of-range","Different-scene inspection is rejected");
        patient.gameObject.scene=new UnityEngine.Scene();visible=null;reader.Inspect("7");
        check(failure=="patient-unavailable","Disconnected target cannot return cached markers");visible=patient;
        var attack=new JObject { ["requestId"]="cross-patient-open",["type"]="open",["version"]=1,["procedure"]=1,["limb"]=0,["patient"]="7" };
        check(!(bool)JObject.Parse(router.Handle(actor,attack.ToString()))["ok"] && limb.skinHealth==skin,"Read-only target identifier grants no treatment authority");
        check(!reader.Inspect("-1") && !reader.Inspect("007") && !reader.Inspect("65536"),"Client rejects ambiguous or invalid roster identities");
        string wire=null;var delayed=new SurgeryClientSession(t=>{wire=t;return true;},()=>now);int updates=0;delayed.OverviewReceived+=v=>updates++;
        delayed.Inspect("7");var stale=router.Handle(actor,wire);delayed.Reset();delayed.Inspect();delayed.Receive(stale);
        check(delayed.Busy && updates==0,"Late prior-patient reply cannot populate a newly selected patient's markers");
        delayed.Receive(router.Handle(actor,wire));check(!delayed.Busy && updates==1,"Current selection receives its own matching overview");
        delayed.Inspect("7");var wrong=JObject.Parse(router.Handle(actor,wire));wrong["patient"]="8";delayed.Receive(wrong.ToString());
        check(!delayed.Busy && updates==1,"Mismatched echoed patient is rejected");
        delayed.Inspect("7");wrong=JObject.Parse(router.Handle(actor,wire));wrong["overview"][0]["fracture"]="true";delayed.Receive(wrong.ToString());
        check(!delayed.Busy && updates==1,"Malformed marker booleans never reach presentation");
        delayed.Inspect("7");wrong=JObject.Parse(router.Handle(actor,wire));
        wrong["overview"][0]["eligibility"]["Fragment"]=true;delayed.Receive(wrong.ToString());
        check(!delayed.Busy && updates==1,"Invalid eligibility values never reach the surgery menu");
        surgeon.Release();reader.Inspect("7");check((bool)markers[0]["fracture"],"Remote markers survive operator interruption");
    }
}
