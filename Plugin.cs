using BepInEx;
using BepInEx.Configuration;
using CasualtiesSurgery.Game;
using HarmonyLib;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CasualtiesSurgery
{
    [BepInPlugin("local.casualties.surgery", "Casualty Center", "0.10.2")]
    [BepInDependency("net.cucorelib", "1.1")]
    public sealed class Plugin : BaseUnityPlugin
    {
        static Plugin instance;
        // Only our context menu needs custom input protection. The native minigame owns its input.
        public static bool BlockInput => instance != null &&
            ((instance.healthMenu != null && instance.healthMenu.IsOpen) || Time.frameCount <= instance.releaseFrame || instance.waitForRightRelease || (instance.active != null && instance.active.BlocksToolInput));
        public static bool OperationVisible => instance != null && instance.active != null;
        internal static SurgeryFeedback Feedback => instance?.feedback;
        internal static bool SelectedForceps { get; set; } = true;
        SurgeryFeedback feedback;
        HealthMenu healthMenu;
        NativeFragmentMinigame active;
        ConfigEntry<float> recovery, reduction, viewScale, handScale;
        ConfigEntry<KeyCode> wheelKey;
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
            healthMenu = new HealthMenu(BeginFromHealth, () => releaseFrame = Time.frameCount + 1);
            SceneManager.activeSceneChanged += SceneChanged;
            Logger.LogInfo("Casualty Center 0.10.2 loaded. Health-dummy surgery uses the native minigame system.");
        }
        void BeginFromHealth(WoundView source, Limb limb)
        {
            if (!enabled || active != null || source == null || !source.gameObject.activeInHierarchy ||
                OperationSession.Ineligible(source.body, limb) != null || MinigameBase.main == null) return;
            var host = MinigameBase.main;
            active = new NativeFragmentMinigame(host, source, limb, recovery.Value, reduction.Value, wheelKey.Value, viewScale.Value, handScale.Value,
                text => Logger.LogInfo(text));
            try
            {
                host.StartMinigame(active, null);
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
        void Update()
        {
            if (waitForRightRelease && !Input.GetMouseButton(1) && !Input.GetMouseButtonUp(1)) waitForRightRelease=false;
            healthMenu?.Tick();
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
        void SceneChanged(Scene from, Scene to) { Stop(); healthMenu?.Dispose(); }
        void OnApplicationFocus(bool focus) { if (!focus) Stop(); }
        void OnDisable() { Stop(); healthMenu?.Dispose(); }
        void OnDestroy()
        {
            Stop(); healthMenu?.Dispose(); SceneManager.activeSceneChanged -= SceneChanged;
            harmony?.UnpatchSelf(); if(feedback!=null)Destroy(feedback); if (instance == this) instance = null;
        }
    }
}
