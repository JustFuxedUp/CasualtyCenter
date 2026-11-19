using System;
using System.Collections.Generic;
using System.IO;
using CasualtiesSurgery.Procedures;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using static CasualtiesSurgery.Game.TreatmentInput;

namespace CasualtiesSurgery.Game
{
    // Resolves the authenticated connection to its own current body. Other-patient treatment
    // is deliberately not inferred from a client-supplied body id; it needs a consent policy.
    internal sealed class SurgeryHostRouter
    {
        sealed class Participant {public PatientLease Lease;public Func<bool> BoundAccess;}
        sealed class Registration {public Body Body;public Limb Limb;public ProcedureKind Kind;public ISurgeryStateTarget Target;public readonly Dictionary<Guid,Participant> Members=new Dictionary<Guid,Participant>();}
        readonly Func<Guid,Body> resolve;
        readonly Func<string,Body> inspectPatient;
        readonly SurgeryTreatmentAccess permission;
        readonly Func<Guid,byte[]> playerColor;
        readonly SurgeryDispatcher dispatcher;
        readonly Guid world=Guid.NewGuid();
        readonly List<Registration> registrations=new List<Registration>();
        readonly Dictionary<Body,string> identities=new Dictionary<Body,string>();
        sealed class OpenRequest {public string Text;public Body Body;public PatientLease Lease;}
        readonly Dictionary<string,OpenRequest> opened=new Dictionary<string,OpenRequest>();
        readonly Queue<string> openOrder=new Queue<string>();
        readonly float recovery,reduction,scale;
        public SurgeryHostRouter(Func<Guid,Body> authenticatedBody,Func<double> clock,float recovery=5,float reduction=1,float scale=.4f,Func<string,Body> inspectPatient=null,SurgeryTreatmentAccess permission=null,Func<Guid,byte[]> playerColor=null)
        { this.playerColor=playerColor;resolve=authenticatedBody ?? throw new ArgumentNullException(nameof(authenticatedBody));this.inspectPatient=inspectPatient;this.permission=permission??new SurgeryTreatmentAccess(resolve,null,null,null,clock);dispatcher=new SurgeryDispatcher(world,clock);this.recovery=recovery;this.reduction=reduction;this.scale=scale; }
        public void Tick()
        {
            dispatcher.Tick();
            foreach(var r in registrations)
                foreach(var pair in new List<KeyValuePair<Guid,Participant>>(r.Members))
                    if(!dispatcher.HasLease(pair.Value.Lease))
                    {
                        r.Members.Remove(pair.Key);
                        if(!registrations.Exists(other=>ReferenceEquals(other.Body,r.Body) && other.Members.ContainsKey(pair.Key)))permission.End(pair.Key,r.Body);
                    }
        }
        public void Disconnect(Guid connection) { permission.Disconnect(connection);dispatcher.Disconnect(connection);Tick(); }
        // Forget transient network registrations only; save-backed limb stores stay intact.
        public void ForgetPatient(Body patient)
        {
            if(ReferenceEquals(patient,null))return; // Destroyed Unity bodies still have registrations to retire.
            permission.ForgetBody(patient);
            foreach(var registration in registrations.FindAll(r=>ReferenceEquals(r.Body,patient)))
            {dispatcher.Remove(registration.Target.Key);registrations.Remove(registration);}
            identities.Remove(patient);Tick();
            // Keep bounded open-request tombstones so late retries cannot reopen a new body.
        }
        // Called for each resolved live player after load/reconnect, before any UI request.
        // Persisted incisions therefore resume host timers even if nobody reopens the UI.
        public void Track(Guid connection)
        {
            var body=resolve(connection);if(body?.limbs==null)return;
            for(int i=0;i<body.limbs.Length;i++)
            {
                var limb=body.limbs[i];
                if(InternalBleedingStore.HasOpen(limb))Ensure(body,limb,i,ProcedureKind.InternalBleeding,connection);
                if(FractureStore.HasOpen(limb))Ensure(body,limb,i,ProcedureKind.Fracture,connection);
            }
        }
        Registration Ensure(Body body,Limb limb,int index,ProcedureKind kind,Guid connection)
        {
            var registration=registrations.Find(r=>ReferenceEquals(r.Body,body) && ReferenceEquals(r.Limb,limb) && r.Kind==kind);
            if(registration!=null && (registration.Target is FragmentCommandTarget fragment?fragment.Current:((OpenSurgeryCommandTarget)registration.Target).Current))return registration;
            if(registration!=null)
            {
                dispatcher.Remove(registration.Target.Key);registrations.Remove(registration);
                foreach(var participant in registration.Members.Keys)
                    if(!registrations.Exists(other=>ReferenceEquals(other.Body,body) && other.Members.ContainsKey(participant)))permission.End(participant,body);
            }
            if(registrations.Count>=256)return null;
            if(!identities.TryGetValue(body,out string patient)) {patient=Guid.NewGuid().ToString("N");identities.Add(body,patient);}
            var key=new OperationKey(world,Guid.NewGuid(),patient,index,kind);
            registration=new Registration {Body=body,Limb=limb,Kind=kind};
            var captured=registration;
            Func<Guid,bool> access=c=>captured.Members.TryGetValue(c,out var member)?member.BoundAccess():permission.Check(c,body)==null;
            ISurgeryStateTarget MakeTarget()=>kind==ProcedureKind.Fragment?(ISurgeryStateTarget)new FragmentCommandTarget(key,limb,access,recovery,reduction,scale):new OpenSurgeryCommandTarget(key,limb,access);
            registration.Target=MakeTarget();registrations.Add(registration);dispatcher.Register(registration.Target,c=>MakeTarget());return registration;
        }
        public string Handle(Guid connection,string text)
        {
            JObject request;
            try
            {
                if(text==null || text.Length>65536)return Error(null,"invalid");
                using(var reader=new JsonTextReader(new StringReader(text)) {MaxDepth=12,DateParseHandling=DateParseHandling.None})
                {request=JObject.Load(reader);if(reader.Read())return Error(null,"invalid");}
            }
            catch(JsonException) {return Error(null,"invalid");}
            string id=request["requestId"]?.Type==JTokenType.String?(string)request["requestId"]:null;
            if(id==null || id.Length==0 || id.Length>64 || connection==Guid.Empty)return Error(null,"invalid");
            string type=request["type"]?.Type==JTokenType.String?(string)request["type"]:null;
            if(type=="overview")
            {
                if(!(Fields(request,"requestId","type","version") || Fields(request,"requestId","type","version","patient")) || request["version"]?.Type!=JTokenType.Integer ||
                    !Number(request["version"],out float version) || version!=SurgeryCommand.ProtocolVersion)return Error(id,"protocol-version");
                var viewer=resolve(connection);if(viewer?.limbs==null)return Error(id,"patient-unavailable");
                var patient=viewer;
                string selected=null;
                if(request["patient"]!=null)
                {
                    if(request["patient"].Type!=JTokenType.String || !ushort.TryParse((string)request["patient"],out ushort peerId) ||
                        peerId.ToString(System.Globalization.CultureInfo.InvariantCulture)!=(string)request["patient"])return Error(id,"invalid");
                    selected=(string)request["patient"];patient=inspectPatient?.Invoke(selected);
                    if(patient?.limbs==null)return Error(id,"patient-unavailable");
                    float distance=UnityEngine.Vector3.Distance(viewer.transform.position,patient.transform.position);
                    if(viewer.gameObject.scene.handle!=patient.gameObject.scene.handle || float.IsNaN(distance) || float.IsInfinity(distance) || distance>5f)
                        return Error(id,"patient-out-of-range");
                }
                var limbs=new JArray();
                foreach (var limb in patient.limbs)
                    limbs.Add(new JObject
                    {
                        ["fracture"] = FractureStore.HasOpen(limb),
                        ["internal"] = InternalBleedingStore.HasOpen(limb),
                        ["suture"] = FragmentBoardStore.HasPendingSuture(limb),
                        ["eligibility"] = new JObject
                        {
                            ["Fragment"] = ClinicalEntryReason(patient, limb, ProcedureKind.Fragment),
                            ["Fracture"] = ClinicalEntryReason(patient, limb, ProcedureKind.Fracture),
                            ["InternalBleeding"] = ClinicalEntryReason(patient, limb, ProcedureKind.InternalBleeding)
                        }
                    });
                return Reply(id,new JObject { ["overview"]=limbs,["patient"]=selected });
            }
            if(type=="open")
            {
                if(!(Fields(request,"requestId","type","version","procedure","limb") || Fields(request,"requestId","type","version","procedure","limb","patient")) || !Index(request["procedure"],3,out int kind) || !Index(request["limb"],256,out int index))return Error(id,"invalid");
                if(request["version"]?.Type!=JTokenType.Integer || !Number(request["version"],out float version) || version!=SurgeryCommand.ProtocolVersion)return Error(id,"protocol-version");
                var body=resolve(connection);
                if(body==null)return Error(id,"patient-unavailable");
                if(request["patient"]!=null)
                {
                    if(request["patient"].Type!=JTokenType.String || !ushort.TryParse((string)request["patient"],out ushort selected) || selected.ToString(System.Globalization.CultureInfo.InvariantCulture)!=(string)request["patient"])return Error(id,"invalid");
                    body=inspectPatient?.Invoke((string)request["patient"]);
                }
                if(body?.limbs==null || index>=body.limbs.Length)return Error(id,"patient-unavailable");
                Tick();
                string accessReason=permission.Check(connection,body,true);if(accessReason!=null)return Error(id,accessReason);
                string cacheKey=connection.ToString("N")+":"+id;
                if(opened.TryGetValue(cacheKey,out var prior))
                {
                    if(prior.Text!=text || !ReferenceEquals(prior.Body,body))return Error(id,"request-reused");
                    if(!dispatcher.Renew(prior.Lease.Operation,connection,prior.Lease.Token))return Error(id,"lease-expired");
                    dispatcher.Tick();
                    return Reply(id,new JObject { ["lease"]=JObject.FromObject(prior.Lease),["snapshot"]=JObject.FromObject(dispatcher.Snapshot(prior.Lease.Operation,connection)) });
                }
                var limb=body.limbs[index];
                string reason = ClinicalEntryReason(body, limb, (ProcedureKind)kind);
                if(reason!=null)return Error(id,"ineligible");
                // Get starts a new internal episode only after the old one is closed.
                if(kind==(int)ProcedureKind.InternalBleeding)InternalBleedingStore.Get(limb);
                var registration=Ensure(body,limb,index,(ProcedureKind)kind,connection);
                if(registration==null)return Error(id,"capacity");
                var lease=dispatcher.Acquire(registration.Target.Key,connection);
                if(lease==null)return Error(id,"busy");
                registration.Members.Add(connection,new Participant {BoundAccess=permission.Bind(connection,body),Lease=lease});
                opened.Add(cacheKey,new OpenRequest {Text=text,Body=body,Lease=lease});openOrder.Enqueue(cacheKey);
                if(openOrder.Count>256)opened.Remove(openOrder.Dequeue());
                dispatcher.Tick();
                return Reply(id,new JObject { ["lease"]=JObject.FromObject(lease),["snapshot"]=JObject.FromObject(dispatcher.Snapshot(lease.Operation,connection)) });
            }
            if(type=="presence")
            {
                if(!Fields(request,"requestId","type","operation","lease","visual"))return Error(id,"invalid");
                try
                {
                    var key=request["operation"].ToObject<OperationKey>();var token=request["lease"].ToObject<Guid>();
                    var visual=request["visual"].ToObject<SurgeryVisualState>();var color=playerColor?.Invoke(connection);
                    bool accepted=dispatcher.PublishVisual(key,connection,token,visual,color!=null&&color.Length==3?color[0]:(byte)180,color!=null&&color.Length==3?color[1]:(byte)210,color!=null&&color.Length==3?color[2]:(byte)255);
                    return Reply(id,new JObject {["accepted"]=accepted});
                }
                catch(Exception e) when(e is JsonException || e is ArgumentException || e is FormatException || e is OverflowException || e is NullReferenceException) {return Error(id,"invalid");}
            }
            if(type=="command")
            {
                if(!Fields(request,"requestId","type","command") || !(request["command"] is JObject commandJson))return Error(id,"invalid");
                SurgeryCommand command;
                try {command=commandJson.ToObject<SurgeryCommand>();}catch(Exception e) when(e is JsonException || e is ArgumentException || e is OverflowException) {return Error(id,"invalid");}
                if(command==null || !command.Valid)return Error(id,"invalid");
                Tick();
                var receipt=dispatcher.Execute(connection,command);
                var snapshot=dispatcher.Snapshot(command.Operation,connection);
                var response=new JObject { ["receipt"]=JObject.FromObject(receipt) };
                if(snapshot!=null)response["snapshot"]=JObject.FromObject(snapshot);
                return Reply(id,response);
            }
            if(type=="snapshot" || type=="renew" || type=="release")
            {
                if(!Fields(request,"requestId","type","operation","lease"))return Error(id,"invalid");
                OperationKey key;Guid lease;
                try {key=request["operation"].ToObject<OperationKey>();lease=request["lease"].ToObject<Guid>();}
                catch(Exception e) when(e is JsonException || e is ArgumentException || e is FormatException) {return Error(id,"invalid");}
                if(key==null)return Error(id,"invalid");
                dispatcher.Tick();
                if(type=="release") {dispatcher.Release(key,connection,lease);Tick();return Reply(id,new JObject());}
                if(type=="renew" && !dispatcher.Renew(key,connection,lease))return Error(id,"lease-expired");
                var snapshot=dispatcher.Snapshot(key,connection);
                return snapshot==null?Error(id,"unavailable"):Reply(id,new JObject { ["snapshot"]=JObject.FromObject(snapshot) });
            }
            return Error(id,"unsupported");
        }
        internal static string ClinicalEntryReason(Body patient, Limb limb, ProcedureKind kind)
        {
            var reason = kind == ProcedureKind.Fragment
                ? OperationSession.ClinicalIneligible(patient, limb)
                : kind == ProcedureKind.Fracture
                    ? FractureStore.ClinicalIneligible(patient, limb)
                    : InternalBleedingStore.ClinicalIneligible(patient, limb);
            return reason ?? (limb.shrapnel > 255 ? "Too many fragments for this operation." : null);
        }

        public Body ResultPatient(JObject response)
        {
            var token=response?["snapshot"]?["Operation"];if(token==null)return null;
            var key=token.ToObject<OperationKey>();return registrations.Find(r=>r.Target.Key.Equals(key))?.Body;
        }
        static string Reply(string id,JObject data) {data["requestId"]=id;data["ok"]=true;return data.ToString(Formatting.None);}
        static string Error(string id,string reason)=>new JObject { ["requestId"]=id,["ok"]=false,["error"]=reason }.ToString(Formatting.None);
    }
}
