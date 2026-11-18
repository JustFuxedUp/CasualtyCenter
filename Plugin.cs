using BepInEx;
using BepInEx.Configuration;
using CasualtiesSurgery.Game;
using CasualtiesSurgery.Procedures;
using HarmonyLib;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CasualtiesSurgery
{
    [BepInPlugin("local.casualties.surgery", "Casualty Center", "0.16.0")]
    [BepInDependency("net.cucorelib", "1.1")]
    public sealed class Plugin : BaseUnityPlugin
    {
        static Plugin instance;
        // Only our context menu needs custom input protection. The native minigame owns its input.
        public static bool BlockInput => instance != null &&
            ((instance.healthMenu != null && instance.healthMenu.IsOpen) || Time.frameCount <= instance.releaseFrame || instance.waitForRightRelease || (instance.active != null && instance.active.BlocksToolInput));
        public static bool OperationVisible => instance != null && instance.active != null;
        internal static SurgeryFeedback Feedback => instance?.feedback;
        internal static SurgeryTool SelectedTool {get;set;} = SurgeryTool.Forceps;
        SurgeryFeedback feedback;
        HealthMenu healthMenu;
        ISurgery active;
        ConfigEntry<float> recovery, reduction, viewScale, handScale;
        ConfigEntry<KeyCode> wheelKey;
        ConfigEntry<bool> developmentTransport;
        ConfigEntry<bool> authorityViews;
        SurgeryTogetherRuntime network;
        SurgeryHostRouter offlineHost;
        SurgeryClientSession offlineClient;
        SurgeryClientSession offlineOverview;
        Newtonsoft.Json.Linq.JArray patientOverview;
        Body overviewBody;
        double nextOverview;
        double overviewReceivedAt;
        string overviewTarget;
        WoundView consentSource;
        Limb consentLimb;
        Body consentOperator,consentPatient;
        ProcedureKind consentKind;
        string consentTarget;
        double consentUntil;
        bool consentApproved;
        void ConsentAnswered(string target,bool approved)
        {if(target==consentTarget && Time.unscaledTimeAsDouble<consentUntil)consentApproved=approved;}
        void OfferApprovedSurgery()
        {
            if(consentSource==null || Time.unscaledTimeAsDouble>=consentUntil || PlayerCamera.main==null ||
                PlayerCamera.main.body!=consentOperator || !consentSource.gameObject.activeInHierarchy || consentSource.body!=consentPatient)
            {consentSource=null;consentApproved=false;return;}
            if(!consentApproved || active!=null || network?.Client.Busy==true || network?.Client.Active==true)return;
            var source=consentSource;var limb=consentLimb;var kind=consentKind;var patient=consentPatient;var actor=consentOperator;
            consentApproved=false;consentSource=null;
            healthMenu.ShowProceed(source,limb,()=>{
                if(source!=null && source.body==patient && PlayerCamera.main!=null && PlayerCamera.main.body==actor)BeginAuthority(source,limb,kind);
            });
        }
        readonly System.Guid offlineConnection=System.Guid.NewGuid();
        bool previousMultiplayer;
        bool previousAuthorityViews;
        internal static bool AuthorityViews=>instance!=null && instance.authorityViews!=null && instance.authorityViews.Value;
        internal static void LogEntry(string text)=>instance?.Logger.LogInfo("Surgery entry: "+text);
        internal static void EntryNotice(WoundView source,Limb limb,string message)=>instance?.healthMenu?.ShowEntryNotice(source,limb,message);
        internal static bool Marker(Limb limb,string name,bool fallback)
        {
            if(!AuthorityViews || limb==null)return fallback;
            if(CUCoreLib.Networking.MultiplayerApi.IsRunning)fallback=false;
            if(limb.body!=instance.overviewBody || instance.patientOverview==null || Time.unscaledTimeAsDouble-instance.overviewReceivedAt>6)return fallback;
            int index=System.Array.IndexOf(limb.body.limbs,limb);
            var token=index>=0 && index<instance.patientOverview.Count?instance.patientOverview[index]?[name]:null;
            return token?.Type==Newtonsoft.Json.Linq.JTokenType.Boolean?(bool)token:fallback;
        }
        internal static void InspectPatient(Body body)
        {
            if(!AuthorityViews || body==null || PlayerCamera.main==null)return;
            var self=instance;
            bool multiplayer=CUCoreLib.Networking.MultiplayerApi.IsRunning;
            bool remote=PlayerCamera.main.body!=body;
            string target=remote && multiplayer?self.network?.InspectionTarget(body):null;
            if(self.overviewBody!=body || self.overviewTarget!=target){self.network?.Overview.Reset();self.offlineOverview?.Reset();self.overviewBody=body;self.overviewTarget=target;self.patientOverview=null;self.nextOverview=0;}
            if(remote && target==null){self.patientOverview=null;return;}
            if(Time.unscaledTimeAsDouble<self.nextOverview)return;
            var client=CUCoreLib.Networking.MultiplayerApi.IsRunning?self.network?.Overview:self.offlineOverview;
            if(client==null || client.Busy)return;
            self.nextOverview=Time.unscaledTimeAsDouble+.5;client.Inspect(target);
        }
        void SetOverview(Newtonsoft.Json.Linq.JArray value) {patientOverview=value;overviewReceivedAt=Time.unscaledTimeAsDouble;}
        internal static string EntryReason(Body patient,Limb limb,ProcedureKind kind)
        {
            if(!AuthorityViews)return kind==ProcedureKind.Fragment?OperationSession.Ineligible(patient,limb):kind==ProcedureKind.Fracture?FractureStore.Ineligible(patient,limb):InternalBleedingStore.Ineligible(patient,limb);
            var actor=PlayerCamera.main!=null?PlayerCamera.main.body:null;
            if(actor==null || !actor.alive || !actor.conscious)return "Operator must be alive and conscious.";
            if(actor!=patient && (!CUCoreLib.Networking.MultiplayerApi.IsRunning || instance.network?.InspectionTarget(patient)==null))return "No connected patient.";
            if(!SurgeryTreatmentAccess.Nearby(actor,patient,SurgeryTreatmentAccess.Reach))return "Move closer to the patient.";
            string reason=OperationSession.ClinicalIneligible(patient,limb,false);if(reason!=null)return reason;
            if(MinigameBase.main==null || MinigameBase.main.currentMinigame!=null)return "Finish the current minigame first.";
            if(CUCoreLib.Networking.MultiplayerApi.IsRunning && !(instance.network?.Available ?? false))return "Surgery transport is not ready; check host health-sync settings.";
            // The host decides clinical eligibility and pending-incision resume. Client
            // stores are not authoritative, especially after reconnect or native healing.
            return null;
        }
        Harmony harmony;
        int releaseFrame = -1;
        bool waitForRightRelease;
        internal static void ProtectExitClick()
        {
            if (instance == null) return;
            instance.releaseFrame=Time.frameCount+1;
            instance.waitForRightRelease=Input.GetMouseButton(1);
        }

        void Awake()
        {
            instance = this;
            wheelKey = Config.Bind("Interface", "ToolWheelKey", KeyCode.Mouse1, "Hold to choose a tool at the cursor; release to select.");
            viewScale = Config.Bind("Interface", "SurgeryScale", 0.4f,
                new ConfigDescription("Wound assets, radial menu and hand scale. The native window stays full size.", new AcceptableValueRange<float>(0.3f, 0.8f)));
            handScale=Config.Bind("Interface","HandScale",0.33f,
                new ConfigDescription("Hand size independent of wound and wheel scale.",new AcceptableValueRange<float>(0.1f,0.4f)));
            CUCoreLib.Registries.SaveRegistry.RegisterLimbProvider("local.casualties.surgery.fragment-board",new FragmentBoardStore());
            CUCoreLib.Registries.SaveRegistry.RegisterLimbProvider("local.casualties.surgery.fracture",new FractureStore());
            CUCoreLib.Registries.SaveRegistry.RegisterLimbProvider("local.casualties.surgery.internal-bleeding", new InternalBleedingStore());
            recovery = Config.Bind("Treatment", "SkinRecovery", 5f,
                new ConfigDescription("Skin points restored per completed fragment and suture.", new AcceptableValueRange<float>(0.1f, 100f)));
            reduction = Config.Bind("Treatment", "BleedingReduction", 1f,
                new ConfigDescription("Bleeding points removed per completed fragment and suture.", new AcceptableValueRange<float>(0.1f, 100f)));
            harmony = new Harmony("local.casualties.surgery");
            try { harmony.PatchAll(typeof(Plugin).Assembly); }
            catch (System.Exception error)
            {
                harmony.UnpatchSelf(); instance = null; enabled = false;
                Logger.LogError("Surgery hooks could not initialize; surgery disabled. " + error);
                return;
            }
            feedback=gameObject.AddComponent<SurgeryFeedback>();
            var soundVolume=Config.Bind("Audio","FeedbackVolume",0.7f,new ConfigDescription("Surgery feedback volume.",new AcceptableValueRange<float>(0,1)));
            feedback.Initialize(System.IO.Path.Combine(System.IO.Path.GetDirectoryName(typeof(Plugin).Assembly.Location),"Audio"),soundVolume.Value,text=>Logger.LogWarning(text));
            healthMenu = new HealthMenu(BeginFromHealth, BeginFracture, BeginInternalBleeding, () => releaseFrame = Time.frameCount + 1);
            developmentTransport=Config.Bind("Development","EnableSurgeryTransport",false,"Enable the experimental authority transport for integration testing. Does not enable multiplayer surgery UI.");
            authorityViews=Config.Bind("Development","EnableAuthorityViews",false,"Opt in to the multiplayer surgery test views. Also enables transport; in singleplayer uses the same command handlers locally. Keep disabled for the stable local views.");
            SceneManager.activeSceneChanged += SceneChanged;
            Logger.LogInfo("Casualty Center 0.16.0 loaded. EnableAuthorityViews="+authorityViews.Value+", EnableSurgeryTransport="+developmentTransport.Value+"; config="+Config.ConfigFilePath);
        }
        void BeginFromHealth(WoundView source, Limb limb)
        {
            if(AuthorityViews){BeginAuthority(source,limb,ProcedureKind.Fragment);return;}
            if (!enabled || active != null || source == null || !source.gameObject.activeInHierarchy ||
                OperationSession.Ineligible(source.body, limb) != null || MinigameBase.main == null) return;
            var host = MinigameBase.main;
            active = new NativeFragmentMinigame(host, source, limb, recovery.Value, reduction.Value, wheelKey.Value, viewScale.Value, handScale.Value,
                text => Logger.LogInfo(text));
            try
            {
                host.StartMinigame((Minigame)active, null);
                if (!ReferenceEquals(host.currentMinigame, active))
                { active.OnEnded(); active = null; return; }
                active.RefreshGuide();
            }
            catch (System.Exception error)
            {
                active?.Finish("Surgery could not start."); active = null;
                Logger.LogError("Native surgery startup failed: " + error);
            }
        }
        void BeginFracture(WoundView source,Limb limb)
        {
            if(AuthorityViews){BeginAuthority(source,limb,ProcedureKind.Fracture);return;}
            if(!enabled || active!=null || source==null || !source.gameObject.activeInHierarchy || FractureStore.Ineligible(source.body,limb)!=null || MinigameBase.main==null)return;
            var host=MinigameBase.main;
            active=new NativeFractureMinigame(host,source,limb,handScale.Value,wheelKey.Value,text=>Logger.LogInfo(text));
            try {host.StartMinigame((Minigame)active,null);if(!ReferenceEquals(host.currentMinigame,active)){active.OnEnded();active=null;return;}active.RefreshGuide();}
            catch(System.Exception error){active?.Finish("Fracture surgery startup failed.");active=null;Logger.LogError(error);}
        }
        void BeginInternalBleeding(WoundView source, Limb limb)
        {
            if(AuthorityViews){BeginAuthority(source,limb,ProcedureKind.InternalBleeding);return;}
            if (!enabled || active != null || source == null || !source.gameObject.activeInHierarchy ||
                InternalBleedingStore.Ineligible(source.body, limb) != null || MinigameBase.main == null) return;
            var host = MinigameBase.main;
            active = new NativeInternalBleedingMinigame(host, source, limb, handScale.Value, wheelKey.Value,
                text => Logger.LogInfo(text));
            try
            {
                host.StartMinigame((Minigame)active, null);
                if (!ReferenceEquals(host.currentMinigame, active)) { active.OnEnded(); active = null; return; }
                active.RefreshGuide();
            }
            catch (System.Exception error)
            {
                active?.Finish("Internal bleeding surgery startup failed.");
                active = null;
                Logger.LogError(error);
            }
        }
        void EnsureOffline()
        {
            if(offlineHost!=null)return;
            offlineHost=new SurgeryHostRouter(id=>id==offlineConnection && PlayerCamera.main!=null?PlayerCamera.main.body:null,()=>Time.timeAsDouble,recovery.Value,reduction.Value,viewScale.Value);
            System.Func<string,bool> send=text=>{string reply=offlineHost.Handle(offlineConnection,text);offlineClient.Receive(reply);offlineOverview.Receive(reply);return true;};
            offlineClient=new SurgeryClientSession(send,()=>Time.unscaledTimeAsDouble);
            offlineOverview=new SurgeryClientSession(send,()=>Time.unscaledTimeAsDouble);offlineOverview.OverviewReceived+=SetOverview;
        }
        void BeginAuthority(WoundView source,Limb limb,ProcedureKind kind)
        {
            LogEntry("begin "+kind+"; role="+(!CUCoreLib.Networking.MultiplayerApi.IsRunning?"offline":CUCoreLib.Networking.MultiplayerApi.IsServer?"host":"client"));
            string refusal=!enabled?"Plugin disabled.":active!=null?"Surgery already active.":source==null?"No source view.":!source.gameObject.activeInHierarchy?"Source view inactive.":EntryReason(source.body,limb,kind);
            if(refusal!=null){LogEntry("refused: "+refusal);return;}
            SurgeryClientSession client;
            if(CUCoreLib.Networking.MultiplayerApi.IsRunning)client=network?.Client;
            else {EnsureOffline();client=offlineClient;}
            if(client==null || client.Active || client.Busy){LogEntry("client unavailable or settling: exists="+(client!=null)+", active="+client?.Active+", busy="+client?.Busy);return;}
            var host=MinigameBase.main;
            string target=source.body==PlayerCamera.main.body?null:network.InspectionTarget(source.body);
            consentSource=target==null?null:source;consentLimb=limb;consentKind=kind;consentTarget=target;
            consentPatient=source.body;consentOperator=PlayerCamera.main.body;consentUntil=Time.unscaledTimeAsDouble+60;consentApproved=false;
            active=new NativeAuthorityMinigame(host,source,limb,kind,client,handScale.Value,viewScale.Value,wheelKey.Value,text=>Logger.LogInfo(text),target);
            try {
                LogEntry("calling native StartMinigame; current="+(host.currentMinigame?.GetType().FullName??"none"));
                host.StartMinigame((Minigame)active,null);
                if(!ReferenceEquals(host.currentMinigame,active)){
                    LogEntry("native startup did not retain surgery; current="+(host.currentMinigame?.GetType().FullName??"none"));
                    active.OnEnded();active=null;return;
                }
                LogEntry("native startup accepted; ended="+active.Ended);active.RefreshGuide();
            }
            catch(System.Exception error){active?.Finish("Authority view startup failed.");active=null;Logger.LogError(error);}
        }
        void Update()
        {
            if(previousAuthorityViews!=AuthorityViews){Stop();previousAuthorityViews=AuthorityViews;patientOverview=null;overviewBody=null;}
            bool multiplayer=CUCoreLib.Networking.MultiplayerApi.IsRunning;
            if(previousMultiplayer!=multiplayer){Stop();offlineClient?.Reset();offlineOverview?.Reset();offlineClient=null;offlineOverview=null;offlineHost=null;patientOverview=null;overviewBody=null;previousMultiplayer=multiplayer;}
            if(developmentTransport.Value || AuthorityViews)
            {
                if(network==null){network=new SurgeryTogetherRuntime(recovery.Value,reduction.Value,viewScale.Value,text=>Logger.LogWarning(text));network.ConsentAnswered+=ConsentAnswered;network.Overview.OverviewReceived+=SetOverview;network.Overview.Failed+=reason=>patientOverview=null;}
                network.Tick();
            }
            else if(network!=null) {network.Dispose();network=null;}
            if(AuthorityViews && !multiplayer)
            {EnsureOffline();offlineHost.Track(offlineConnection);offlineHost.Tick();offlineClient.Tick();offlineOverview.Tick();}
            else if(offlineHost!=null){offlineClient?.Reset();offlineOverview?.Reset();offlineClient=null;offlineOverview=null;offlineHost=null;}
            if(!AuthorityViews && active is NativeAuthorityMinigame)Stop();
            if (Time.timeScale > 0 && PlayerCamera.main != null && !multiplayer && !AuthorityViews)
                InternalBleedingStore.Tick(PlayerCamera.main.body, Time.unscaledDeltaTime);
            if (waitForRightRelease && !Input.GetMouseButton(1) && !Input.GetMouseButtonUp(1)) waitForRightRelease=false;
            healthMenu?.Tick();OfferApprovedSurgery();
            if (healthMenu != null && healthMenu.IsOpen && Input.GetKeyDown(KeyCode.Escape)) healthMenu.Close();
            if (active == null) return;
            if (active.Ended) { active = null; return; }
            string reason = active.InvalidReason();
            if (reason != null) { active.Finish(reason); active = null; }
        }
        void Stop()
        {
            healthMenu?.Close();
            active?.Finish("Surgery cancelled; unfinished treatment grants no reward."); active = null;
        }
        void SceneChanged(Scene from, Scene to) { Stop(); healthMenu?.Dispose(); network?.Reset();offlineClient?.Reset();offlineOverview?.Reset();offlineClient=null;offlineOverview=null;offlineHost=null;patientOverview=null;overviewBody=null; }
        void OnApplicationFocus(bool focus) { if (!focus) Stop(); }
        void OnDisable() { Stop(); healthMenu?.Dispose(); network?.Dispose();network=null; }
        void OnDestroy()
        {
            Stop(); healthMenu?.Dispose(); SceneManager.activeSceneChanged -= SceneChanged;
            network?.Dispose();network=null;
            harmony?.UnpatchSelf(); if(feedback!=null)Destroy(feedback); if (instance == this) instance = null;
        }
    }
}
