using BepInEx;
using BepInEx.Configuration;
using CasualtiesSurgery.Game;
using HarmonyLib;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CasualtiesSurgery
{
    [BepInPlugin("local.casualties.surgery", "Casualty Center", "0.5.0")]
    [BepInDependency("net.cucorelib", "1.1")]
    public sealed class Plugin : BaseUnityPlugin
    {
        static Plugin instance;
        // Only our context menu needs custom input protection. The native minigame owns its input.
        public static bool BlockInput => instance != null &&
            ((instance.healthMenu != null && instance.healthMenu.IsOpen) || Time.frameCount <= instance.releaseFrame);
        public static bool OperationVisible => instance != null && instance.active != null;
        HealthMenu healthMenu;
        NativeFragmentMinigame active;
        ConfigEntry<float> recovery, reduction;
        Harmony harmony;
        int releaseFrame = -1;

        void Awake()
        {
            instance = this;
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
            healthMenu = new HealthMenu(BeginFromHealth, () => releaseFrame = Time.frameCount + 1);
            SceneManager.activeSceneChanged += SceneChanged;
            Logger.LogInfo("Casualty Center 0.5.0 loaded. Health-dummy surgery uses the native minigame system.");
        }
        void BeginFromHealth(WoundView source, Limb limb)
        {
            if (!enabled || active != null || source == null || !source.gameObject.activeInHierarchy ||
                OperationSession.Ineligible(source.body, limb) != null || MinigameBase.main == null) return;
            var host = MinigameBase.main;
            active = new NativeFragmentMinigame(host, source, limb, recovery.Value, reduction.Value,
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
            harmony?.UnpatchSelf(); if (instance == this) instance = null;
        }
    }
}
