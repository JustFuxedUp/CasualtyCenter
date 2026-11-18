using System;
using System.Collections.Generic;
using UnityEngine;

namespace CasualtiesSurgery.Game
{
    // Host-only consent and body-generation checks. Prompts never grant a lease themselves.
    internal sealed class SurgeryTreatmentAccess
    {
        sealed class Offer {public Guid Connection;public Body Actor,Patient;public bool Answered,Approved;public double Expires;}
        readonly Func<Guid,Body> resolve;
        readonly Func<Body,bool> live;
        readonly Func<bool> consentRequired;
        readonly Action<Body,Body,Action<bool>> prompt;
        readonly Func<double> clock;
        readonly Func<Body,Body,bool> unobstructed;
        readonly List<Offer> offers=new List<Offer>();
        public SurgeryTreatmentAccess(Func<Guid,Body> resolve,Func<Body,bool> live,Func<bool> consentRequired,Action<Body,Body,Action<bool>> prompt,Func<double> clock,Func<Body,Body,bool> unobstructed=null)
        {this.resolve=resolve;this.live=live;this.consentRequired=consentRequired;this.prompt=prompt;this.clock=clock;this.unobstructed=unobstructed??((a,b)=>true);}
        public const float Reach=4f;
        public const float MovementLimit=3f;
        public static bool Nearby(Body a,Body b,float range)
        {if(a==null || b==null || a.gameObject.scene.handle!=b.gameObject.scene.handle)return false;float d=Vector3.Distance(a.transform.position,b.transform.position);return !float.IsNaN(d) && !float.IsInfinity(d) && d<=range;}
        public string Check(Guid connection,Body patient,bool request=false)
        {
            var actor=resolve(connection);
            if(actor==null || !actor.alive || !actor.conscious)return "Operator must be alive and conscious.";
            if(patient==null || !patient.alive)return "Patient is unavailable.";
            if(ReferenceEquals(actor,patient))return null;
            if(live==null || !live(patient) || !Nearby(actor,patient,SurgeryTreatmentAccess.Reach))return "Patient is no longer within treatment range.";
            if(!unobstructed(actor,patient))return "The path to the patient is blocked.";
            if(consentRequired==null)return "Remote treatment is not enabled.";
            if(!patient.conscious || !consentRequired())return null;
            offers.RemoveAll(o=>o.Expires<=clock() || !ReferenceEquals(resolve(o.Connection),o.Actor) || !live(o.Patient) || !Nearby(o.Actor,o.Patient,SurgeryTreatmentAccess.Reach));
            var offer=offers.Find(o=>o.Connection==connection && ReferenceEquals(o.Actor,actor) && ReferenceEquals(o.Patient,patient));
            if(offer!=null)return offer.Approved?null:offer.Answered?"Patient declined treatment.":"Waiting for patient consent. Approval will offer a Proceed button.";
            if(!request || prompt==null)return "Patient consent is required.";
            if(offers.Count>=64)return "Too many pending treatment requests.";
            offer=new Offer {Connection=connection,Actor=actor,Patient=patient,Expires=clock()+30};offers.Add(offer);
            try {prompt(actor,patient,approved=>{
                if(!offers.Contains(offer) || offer.Answered || offer.Expires<=clock())return;
                offer.Answered=true;
                offer.Approved=approved && ReferenceEquals(resolve(connection),actor) && actor.alive && actor.conscious && patient.alive && live(patient) && Nearby(actor,patient,SurgeryTreatmentAccess.Reach) && unobstructed(actor,patient);
                offer.Expires=clock()+30;
            });}
            catch {offers.Remove(offer);return "Patient consent prompt is unavailable.";}
            return offer.Approved?null:"Waiting for patient consent. Approval will offer a Proceed button.";
        }
        public Func<bool> Bind(Guid connection,Body patient)
        {
            var actor=resolve(connection);var actorOrigin=actor.transform.position;var patientOrigin=patient.transform.position;int scene=actor.gameObject.scene.handle;
            var offer=offers.Find(o=>o.Connection==connection && ReferenceEquals(o.Patient,patient) && o.Approved);
            if(offer!=null)offer.Expires=double.PositiveInfinity;
            return ()=>ReferenceEquals(resolve(connection),actor) && actor.gameObject.scene.handle==scene && patient.gameObject.scene.handle==scene &&
                Vector3.Distance(actor.transform.position,actorOrigin)<=SurgeryTreatmentAccess.MovementLimit && Vector3.Distance(patient.transform.position,patientOrigin)<=SurgeryTreatmentAccess.MovementLimit && Check(connection,patient)==null;
        }
        public void End(Guid connection,Body patient)=>offers.RemoveAll(o=>o.Connection==connection && ReferenceEquals(o.Patient,patient));
        public void Disconnect(Guid connection)=>offers.RemoveAll(o=>o.Connection==connection);
        public void ForgetBody(Body body)=>offers.RemoveAll(o=>ReferenceEquals(o.Patient,body)||ReferenceEquals(o.Actor,body));
        public void Reset()=>offers.Clear();
    }
}
