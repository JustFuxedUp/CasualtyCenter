using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using CUCoreLib.Networking;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace CasualtiesSurgery.Game
{
    // Opt-in development transport. Existing playable views remain singleplayer gated until
    // their asynchronous command/snapshot presentation and two-instance checks are complete.
    internal sealed class SurgeryTogetherRuntime : IDisposable
    {
        readonly Dictionary<object,Guid> connections=new Dictionary<object,Guid>();
        readonly Dictionary<Guid,object> peers=new Dictionary<Guid,object>();
        readonly Dictionary<Guid,Body> trackedBodies=new Dictionary<Guid,Body>();
        readonly Action<string> log;
        readonly float recovery,reduction,scale;
        TogetherSurgeryTransport transport;
        SurgeryHostRouter host;
        SurgeryTreatmentAccess permission;
        FieldInfo rules,consentForFeeding;
        MethodInfo promptPlayer,unobstructed;
        FieldInfo players,body;
        Type netBody;
        MethodInfo queueHealth,canSync;
        bool running,server,failed;
        public event Action<string> Reply;
        public SurgeryClientSession Client { get; }
        public SurgeryClientSession Overview { get; }
        public bool Available=>running && transport!=null && (!server || host!=null);
        public SurgeryTogetherRuntime(float recovery,float reduction,float scale,Action<string> log)
        {this.recovery=recovery;this.reduction=reduction;this.scale=scale;this.log=log;Client=new SurgeryClientSession(Send,()=>Time.unscaledTimeAsDouble);Overview=new SurgeryClientSession(Send,()=>Time.unscaledTimeAsDouble);}
        Body Resolve(Guid connection)
        {
            if(!peers.TryGetValue(connection,out var peer) || !(players.GetValue(null) is IDictionary live) || !live.Values.Cast<object>().Any(p=>ReferenceEquals(p,peer)))return null;
            var value=body.GetValue(peer) as Body;
            return value!=null?value:null; // Normalize Unity's destroyed-object null before identity comparisons.
        }
        // Use the live Together roster key, not display names or caller-supplied Unity IDs.
        public string InspectionTarget(Body patient)
        {
            if(!Available || patient==null || !(players?.GetValue(null) is IDictionary live))return null;
            foreach(DictionaryEntry entry in live)
                if(ReferenceEquals(body.GetValue(entry.Value),patient))return entry.Key.ToString();
            return null;
        }
        Body ResolveInspection(string target)
        {
            if(!(players?.GetValue(null) is IDictionary live))return null;
            foreach(DictionaryEntry entry in live)
                if(entry.Key.ToString()==target){var value=body.GetValue(entry.Value) as Body;return value!=null?value:null;}
            return null;
        }
        SurgeryHostRouter CreateHost()=>new SurgeryHostRouter(Resolve,()=>Time.unscaledTimeAsDouble,recovery,reduction,scale,ResolveInspection,permission);
        void PromptConsent(Body actor,Body patient,Action<bool> answer)
        {
            if(!(players.GetValue(null) is IDictionary live))throw new InvalidOperationException("Player roster unavailable.");
            object recipient=live.Values.Cast<object>().FirstOrDefault(p=>ReferenceEquals(body.GetValue(p),patient));
            string actorId=InspectionTarget(actor);
            if(recipient==null || actorId==null)throw new InvalidOperationException("Treatment participant left.");
            log?.Invoke("Surgery consent requested for remote treatment.");
            promptPlayer.Invoke(null,new object[]{recipient,"SURGERY_OFFER","Surgery request","<body:"+actorId+"> wants to operate on you. Allow this operation?",new Action<byte>(value=>{answer(value==0);log?.Invoke("Surgery consent answer: "+(value==0?"accepted":"declined"));})});
        }
        public void Tick()
        {
            bool current=MultiplayerApi.IsRunning, currentServer=MultiplayerApi.IsServer;
            if(!current) { if(running)Reset();running=false;return; }
            if(failed)return;
            try
            {
                if(!running || server!=currentServer)
                {
                    Reset();server=currentServer;running=true;
                    var assembly=AppDomain.CurrentDomain.GetAssemblies().First(a=>a.GetName().Name=="CasualtiesMP");
                    TogetherMinigameCompatibility.Install(new HarmonyLib.Harmony("local.casualties.surgery"),assembly);
                    TogetherMinigameCompatibility.Log=log;
                    try {TogetherMinigameCompatibility.ObserveStartup(new HarmonyLib.Harmony("local.casualties.surgery"),assembly);}
                    catch(Exception observationError){log?.Invoke("Surgery startup observation unavailable: "+observationError.Message);}
                    var player=assembly.GetType("Together.ScavPlayer",true);players=player.GetField("ClientIdToPlayerDict");body=player.GetField("body");
                    var medical=assembly.GetType("Together.MedicalSync",true);netBody=assembly.GetType("Together.NetBody",true);
                    queueHealth=medical.GetMethod("Server_QueueSendCharacterHealth",new[]{netBody,typeof(bool)});canSync=medical.GetMethod("CanSyncHealth",Type.EmptyTypes);
                    if(players==null || body==null || queueHealth==null || canSync==null)throw new NotSupportedException("Together identity or health API mismatch.");
                    rules=assembly.GetType("Together.Multiplayer",true).GetField("rules");
                    consentForFeeding=rules?.FieldType.GetField("ConsentForFeeding");
                    promptPlayer=assembly.GetType("Together.ServerMain",true).GetMethod("PromptPlayer",new[]{player,typeof(string),typeof(string),typeof(string),typeof(Action<byte>)});
                    unobstructed=assembly.GetType("CasualtiesTogetherUtils.Util",true).GetMethod("AccurateRaycastInteractionCheckObstruction",new[]{typeof(Body),typeof(Body),typeof(bool)});
                    if(rules==null || consentForFeeding?.FieldType!=typeof(bool) || promptPlayer==null || unobstructed?.ReturnType!=typeof(bool))throw new NotSupportedException("Together consent/reach API mismatch.");
                    permission=new SurgeryTreatmentAccess(Resolve,p=>InspectionTarget(p)!=null,()=> (bool)consentForFeeding.GetValue(rules.GetValue(null)),PromptConsent,()=>Time.unscaledTimeAsDouble,
                        (a,b)=>(bool)unobstructed.Invoke(null,new object[]{a,b,false}));
                    if(server)host=CreateHost();
                    transport=TogetherSurgeryTransport.Install(Receive,text=>{Client.Receive(text);Overview.Receive(text);Reply?.Invoke(text);},log);
                    log?.Invoke("Surgery development transport active; authority views require their separate test switch.");
                }
                if(server && !(bool)canSync.Invoke(null,null))
                {
                    // Native authority settings can change during a session. Stop custom
                    // state advancement too, and mint new leases when authority returns.
                    if(host!=null) {host=null;permission.Reset();Client.Reset();log?.Invoke("Surgery authority suspended by Together health-sync policy.");}
                    transport?.Pump();Client.Tick();return;
                }
                if(server && host==null)host=CreateHost();
                foreach(var pair in peers.ToArray())
                {
                    Body currentBody=Resolve(pair.Key);
                    if(trackedBodies.TryGetValue(pair.Key,out var previousBody) && !ReferenceEquals(previousBody,currentBody))
                    {host?.ForgetPatient(previousBody);trackedBodies.Remove(pair.Key);log?.Invoke("Surgery patient network identity retired; saved limb progress retained.");}
                    if(currentBody==null) {host?.Disconnect(pair.Key);connections.Remove(pair.Value);peers.Remove(pair.Key);}
                }
                if(server && players.GetValue(null) is IDictionary live)
                {
                    foreach(var peer in live.Values)
                    {
                        if(peer==null)continue;
                        var liveBody=body.GetValue(peer) as Body;if(liveBody==null)continue;
                        if(!connections.TryGetValue(peer,out Guid connection))
                        {if(connections.Count>=64)break;connection=Guid.NewGuid();connections.Add(peer,connection);peers.Add(connection,peer);}
                        trackedBodies[connection]=liveBody;
                        host.Track(connection);
                    }
                }
                host?.Tick();transport?.Pump();Client.Tick();Overview.Tick();
            }
            catch(Exception e) {failed=true;Reset();log?.Invoke("Surgery development transport disabled: "+e.GetType().Name+": "+e.Message);}
        }
        void Receive(object authenticatedPeer,string text)
        {
            if(host==null || !MultiplayerApi.IsServer)return;
            if(!connections.TryGetValue(authenticatedPeer,out Guid connection))
            {
                if(connections.Count>=64)return;
                connection=Guid.NewGuid();connections.Add(authenticatedPeer,connection);peers.Add(connection,authenticatedPeer);
            }
            Body patient=Resolve(connection);if(patient==null)return;
            if(!(bool)canSync.Invoke(null,null))return; // Respect native health authority mode.
            var component=patient.GetComponent(netBody);if(component==null)return;
            string response=host.Handle(connection,text);
            var parsed=JObject.Parse(response);var receipt=parsed["receipt"];
            if(receipt!=null && (receipt["Status"]?.Value<int>()==(int)Procedures.CommandStatus.Applied ||
                (receipt["Status"]?.Value<int>()==(int)Procedures.CommandStatus.Duplicate && receipt["OriginalStatus"]?.Value<int>()==(int)Procedures.CommandStatus.Applied)))
            {
                // Retry queues the current absolute health again if an earlier native send
                // failed. This never repeats the clinical mutation or its feedback event.
                var treated=host.ResultPatient(parsed);
                if(treated!=null) {var treatedComponent=treated.GetComponent(netBody);if(treatedComponent!=null)queueHealth.Invoke(null,new object[]{treatedComponent,false});}
            }
            transport.SendReply(authenticatedPeer,response);
        }
        public bool Send(string request)=>transport!=null && transport.SendRequest(request);
        public void Reset()
        {transport?.Dispose();transport=null;host=null;permission?.Reset();permission=null;connections.Clear();peers.Clear();trackedBodies.Clear();running=false;Client.Reset();Overview.Reset();}
        public void Dispose()=>Reset();
    }
}
