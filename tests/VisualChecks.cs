using System;
using System.Collections.Generic;
using CasualtiesSurgery.Game;
using CasualtiesSurgery.Procedures;
using Newtonsoft.Json.Linq;
static class VisualChecks
{
    public static void Run(Action<bool,string> check)
    {
        double now=0;var a=Guid.NewGuid();var b=Guid.NewGuid();var c=Guid.NewGuid();var patient=new Body();var actor=new Body();
        var limb=new Limb {body=patient,shrapnel=2,skinHealth=80};patient.limbs=new[]{limb,new Limb {body=patient,shrapnel=1,skinHealth=90}};
        var policy=new SurgeryTreatmentAccess(id=>actor,p=>p==patient,()=>false,null,()=>now);
        var router=new SurgeryHostRouter(id=>actor,()=>now,inspectPatient:id=>patient,permission:policy,playerColor:id=>id==a?new byte[]{12,34,56}:new byte[]{90,100,110});
        var packets=new Queue<string>();var first=new SurgeryClientSession(t=>{packets.Enqueue(t);return true;},()=>now);
        SurgeryClientSession second=null,third=null;
        second=new SurgeryClientSession(t=>{second.Receive(router.Handle(b,t));return true;},()=>now);
        third=new SurgeryClientSession(t=>{third.Receive(router.Handle(c,t));return true;},()=>now);
        first.Begin(ProcedureKind.Fragment,0,"7");first.Receive(router.Handle(a,packets.Dequeue()));second.Begin(ProcedureKind.Fragment,0,"7");third.Begin(ProcedureKind.Fragment,1,"7");
        SurgeryVisualState Visual(long seq,float x=10,float[] trail=null)=>new SurgeryVisualState(seq,1,3,0,0,x,20,30,.33f,256,630,false,true,trail??new float[]{1,2,3,4});
        first.Command("fragment.grip",new JObject {["site"]=0,["x"]=0,["y"]=.38});string clinical=packets.Dequeue();
        check(first.PublishVisual(Visual(1)) && first.Busy,"Cosmetic hand update can publish without replacing an in-flight clinical request");
        string packet=packets.Dequeue();first.Receive(router.Handle(a,packet));
        check(first.Busy && limb.skinHealth==80 && limb.shrapnel==2,"Presence acknowledgement cannot complete clinical input or mutate health");
        second.Refresh();third.Refresh();
        var peer=second.Snapshot.Visuals[0];
        check(second.Snapshot.Visuals.Length==1 && peer.Participant==a && peer.Red==12 && peer.Green==34 && peer.Blue==56,"Remote hand identity and multiplayer color come from the host roster");
        check(second.Snapshot.Revision==0 && third.Snapshot.Visuals.Length==0,"Cosmetic updates change no clinical revision and do not cross operations");
        peer.State.Trail[0]=999;second.Refresh();
        check(second.Snapshot.Visuals[0].State.Trail[0]==1,"Remote trail replicas cannot mutate host visual storage");
        check(!(bool)JObject.Parse(router.Handle(c,packet))["accepted"],"Another participant cannot publish using a different surgeon's lease");
        var peek=JObject.Parse(packet);peek.Remove("visual");peek["type"]="snapshot";
        var outsider=JObject.Parse(router.Handle(c,peek.ToString()));
        check(((JArray)outsider["snapshot"]["Visuals"]).Count==0,"An outsider with a known operation key cannot observe its remote hands");
        first.Receive(router.Handle(a,clinical));check(!first.Busy,"Clinical acknowledgement still resolves after interleaved hand updates");
        now=.1;first.PublishVisual(Visual(2,40));router.Handle(a,packets.Dequeue());router.Handle(a,packet);second.Refresh();
        check(second.Snapshot.Visuals[0].State.X==40,"Delayed old presence packets cannot rewind a newer hand sample");
        check(!first.PublishVisual(Visual(3,float.NaN)) && !first.PublishVisual(Visual(3,trail:new float[130])) && !first.PublishVisual(Visual(3,trail:new float[3])),"Non-finite and oversized or odd-length trails are rejected locally");
        var invalid=JObject.Parse(packet);invalid["visual"]["X"]=3000;invalid["visual"]["Sequence"]=3;
        check(!(bool)JObject.Parse(router.Handle(a,invalid.ToString()))["accepted"],"Host independently rejects out-of-bounds visual coordinates");
        now=1.4;second.Refresh();check(second.Snapshot.Visuals.Length==0,"A silent participant's ghost expires without ending their clinical lease");
        now=14;first.PublishVisual(Visual(4));router.Handle(a,packets.Dequeue());second.Refresh();
        now=16;first.PublishVisual(Visual(5));
        check(!(bool)JObject.Parse(router.Handle(a,packets.Dequeue()))["accepted"],"Presence traffic cannot renew an expired treatment lease");
        second.Refresh();check(second.Snapshot.Visuals.Length==0,"Expired lease cannot retain a visible remote hand");
        first.Reset();second.Release();third.Release();
    }
}
