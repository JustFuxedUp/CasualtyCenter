using BepInEx;
using BepInEx.Configuration;
using CUCoreLib.Networking;
using HarmonyLib;
using CasualtiesSurgery.Game;
using CasualtiesSurgery.Procedures;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace CasualtiesSurgery
{
    [BepInPlugin("local.casualties.surgery", "Casualty Center", "0.4.0")]
    [BepInDependency("net.cucorelib", "1.1")]
    public sealed class Plugin : BaseUnityPlugin
    {
        static Plugin instance;
        public static bool BlockInput => instance != null && (instance.visible || (instance.healthMenu != null && instance.healthMenu.IsOpen) || Time.frameCount <= instance.releaseFrame);
        public static bool OperationVisible => instance != null && instance.visible;
        HealthMenu healthMenu;
        WoundView sourceHealthView;
        ConfigEntry<bool> debugPanel;
        ConfigEntry<KeyCode> hotkey;
        ConfigEntry<float> recovery, reduction;
        bool visible;
        int releaseFrame = -1;
        Rect window = new Rect(30, 60, 610, 590);
        Vector2 scroll;
        OperationSession operation;
        PrecisionDrag gesture;
        bool forceps = true;
        int gestureControl;
        string message = "Select an injured limb to begin.";
        Harmony harmony;
        GameObject blocker;
        CursorLockMode oldLock;
        bool oldCursor;
        Vector2 start, end;

        void Awake()
        {
            instance = this;
            debugPanel = Config.Bind("Interface", "EnableDebugPanel", false, "Enable the standalone F8 inspector. Normal surgery starts by right-clicking a limb on the health dummy.");
            hotkey = Config.Bind("Interface", "ToggleKey", KeyCode.F8, "Open or close the surgery panel.");
            recovery = Config.Bind("Treatment", "SkinRecovery", 5f,
                new ConfigDescription("Skin points restored per completed fragment and suture.", new AcceptableValueRange<float>(0.1f, 100f)));
            reduction = Config.Bind("Treatment", "BleedingReduction", 1f,
                new ConfigDescription("Bleeding points removed per completed fragment and suture.", new AcceptableValueRange<float>(0.1f, 100f)));
            harmony = new Harmony("local.casualties.surgery");
            try { harmony.PatchAll(typeof(Plugin).Assembly); }
            catch (System.Exception error)
            {
                harmony.UnpatchSelf(); instance = null; enabled = false;
                Logger.LogError("Input protection could not initialize; surgery disabled. " + error);
                return;
            }
            healthMenu = new HealthMenu(BeginFromHealth, () => releaseFrame = Time.frameCount + 1);
            SceneManager.activeSceneChanged += SceneChanged;
            Logger.LogInfo("Casualty Center 0.4.0 loaded. Right-click a limb on the health dummy for surgery.");
        }

        void Update()
        {
            healthMenu?.Tick();
            if (healthMenu != null && healthMenu.IsOpen && Input.GetKeyDown(KeyCode.Escape))
            { healthMenu.Close(); return; }
            if ((debugPanel.Value || visible) && Input.GetKeyDown(hotkey.Value))
            {
                if (visible) Close();
                else if (MultiplayerApi.IsRunning) Logger.LogWarning("Surgery MVP is disabled during multiplayer.");
                else if (MinigameBase.main != null && MinigameBase.main.currentMinigame != null)
                    Logger.LogWarning("Finish the current minigame before opening surgery.");
                else Open();
            }
            if (!visible) return;
            if (sourceHealthView != null && (!sourceHealthView.gameObject.activeInHierarchy ||
                (operation != null && sourceHealthView.body != operation.Patient))) { Close(); return; }
            if (Input.GetKeyDown(KeyCode.Escape) || MultiplayerApi.IsRunning) { Close(); return; }
            Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
            if (operation != null)
            {
                string reason = operation.InvalidReason();
                if (reason != null) { if (sourceHealthView != null) Close(); else Cancel(reason); }
            }
        }

        void BeginFromHealth(WoundView source, Limb limb)
        {
            if (!enabled || visible || source == null || !source.gameObject.activeInHierarchy ||
                OperationSession.Ineligible(source.body, limb) != null) return;
            sourceHealthView = source;
            operation = new OperationSession(source.body, limb, recovery.Value, reduction.Value);
            forceps = true; PrepareGesture();
            message = "Grip the fragment at SHARD and pull to END. Release there.";
            Open();
        }

        void Open()
        {
            healthMenu?.Close();
            oldLock = Cursor.lockState; oldCursor = Cursor.visible;
            if (blocker == null)
            {
                blocker = new GameObject("CasualtyCenterInputShield", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
                DontDestroyOnLoad(blocker);
                Canvas canvas = blocker.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 32767;
                var shield = new GameObject("Shield", typeof(RectTransform), typeof(Image));
                shield.transform.SetParent(blocker.transform, false);
                RectTransform rect = shield.GetComponent<RectTransform>();
                rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
                rect.offsetMin = Vector2.zero; rect.offsetMax = Vector2.zero;
                shield.GetComponent<Image>().color = Color.clear;
            }
            blocker.SetActive(true); visible = true;
        }
        void Close()
        {
            Cancel("Operation cancelled. Completed treatments and slip damage remain.");
            sourceHealthView = null;
            visible = false; releaseFrame = Time.frameCount + 1;
            if (blocker != null) blocker.SetActive(false);
            Cursor.lockState = oldLock; Cursor.visible = oldCursor;
        }
        void Abort(string reason)
        {
            if (sourceHealthView != null) Close(); else Cancel(reason);
        }
        void Cancel(string reason)
        {
            ReleasePointer();
            operation?.Cancel(); operation = null; gesture = null; message = reason;
        }
        void ReleasePointer()
        {
            if (gestureControl != 0 && GUIUtility.hotControl == gestureControl) GUIUtility.hotControl = 0;
            gestureControl = 0;
        }
        void SceneChanged(Scene from, Scene to) { healthMenu?.Dispose(); if (visible) Close(); }
        void OnApplicationFocus(bool focus) { if (!focus) { healthMenu?.Close(); if (visible) Close(); } }
        void OnDisable() { healthMenu?.Dispose(); if (visible) Close(); }
        void OnDestroy()
        {
            if (visible) Close();
            healthMenu?.Dispose();
            SceneManager.activeSceneChanged -= SceneChanged;
            harmony?.UnpatchSelf();
            if (blocker != null) Destroy(blocker);
            if (instance == this) instance = null;
        }

        void OnGUI()
        {
            if (!visible) return;
            // Scale the entire panel uniformly for smaller game windows.
            Matrix4x4 previous = GUI.matrix;
            int previousDepth = GUI.depth;
            float scale = Mathf.Min(1f, Screen.width / 650f, Screen.height / 680f);
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1));
            window.x = Mathf.Clamp(window.x, 0, Mathf.Max(0, Screen.width / scale - window.width));
            window.y = Mathf.Clamp(window.y, 0, Mathf.Max(0, Screen.height / scale - window.height));
            GUI.depth = -1000;
            window = GUI.Window(GetInstanceID(), window, DrawWindow, "CASUALTY CENTER  /  FIELD SURGERY MVP");
            GUI.matrix = previous; GUI.depth = previousDepth;
        }
        void DrawWindow(int id)
        {
            GUI.Label(new Rect(18, 30, 575, 24), "Singleplayer test kit: tools are free. The world keeps running.");
            GUI.Label(new Rect(18, 55, 575, 24), "+" + recovery.Value + " skin / -" + reduction.Value + " bleeding per extraction + suture.");
            if (operation == null) DrawPatient(); else DrawOperation();
            GUI.Label(new Rect(18, 496, 575, 48), message, new GUIStyle(GUI.skin.label) { wordWrap = true });
            if (GUI.Button(new Rect(18, 550, 280, 28), sourceHealthView != null ? "Back to health screen" : "Close (F8 / Escape)")) Close();
            if (operation != null && GUI.Button(new Rect(310, 550, 280, 28), "Cancel operation")) { if (sourceHealthView != null) Close(); else Cancel("Cancelled. No completion reward; slip damage remains."); }
            GUI.DragWindow(new Rect(0, 0, 610, 25));
        }
        void DrawPatient()
        {
            Body patient = PlayerCamera.main != null ? PlayerCamera.main.body : null;
            if (patient == null || patient.limbs == null) { GUI.Label(new Rect(18, 100, 570, 30), "Enter a run to inspect your character."); return; }
            scroll = GUI.BeginScrollView(new Rect(18, 92, 574, 390), scroll, new Rect(0, 0, 550, patient.limbs.Length * 68));
            for (int i = 0; i < patient.limbs.Length; i++)
            {
                Limb limb = patient.limbs[i]; if (limb == null) continue;
                float y = i * 68;
                GUI.Label(new Rect(0, y, 390, 23), limb.fullName + "  |  fragments: " + limb.shrapnel);
                GUI.Label(new Rect(0, y + 23, 390, 23), string.Format("Skin {0:0.0}/100   Bleeding {1:0.00}", limb.skinHealth, limb.bleedAmount));
                string reason = OperationSession.Ineligible(patient, limb);
                GUI.enabled = reason == null;
                if (GUI.Button(new Rect(400, y + 4, 135, 28), "Operate"))
                {
                    operation = new OperationSession(patient, limb, recovery.Value, reduction.Value);
                    forceps = true; PrepareGesture(); message = "Grip the fragment at START and pull to END. Release there.";
                }
                GUI.enabled = true;
                if (reason != null) GUI.Label(new Rect(0, y + 44, 545, 22), reason);
            }
            GUI.EndScrollView();
        }
        void PrepareGesture()
        {
            if (operation.Procedure.Stage == FragmentStage.Extraction)
            { start = new Vector2(0.5f, 0.78f); end = new Vector2(0.5f, 0.2f); }
            else
            { float y = 0.3f + operation.Stitches * 0.2f; start = new Vector2(0.28f, y); end = new Vector2(0.72f, y); }
            gesture = new PrecisionDrag(start.x, start.y, end.x, end.y, 0.075f);
        }
        void DrawOperation()
        {
            var current = operation;
            string invalid = current.InvalidReason();
            if (invalid != null) { Abort(invalid); return; }
            bool extraction = current.Procedure.Stage == FragmentStage.Extraction;
            GUI.Label(new Rect(18, 90, 575, 24), current.Limb.fullName + (extraction ? " / EXTRACT ONE FRAGMENT" : " / SUTURE " + (current.Stitches + 1) + " OF 3"));
            if (GUI.Button(new Rect(18, 122, 180, 28), (forceps ? "[Selected] " : "") + "Forceps")) { forceps = true; ReleasePointer(); gesture.Reset(); }
            if (GUI.Button(new Rect(210, 122, 180, 28), (!forceps ? "[Selected] " : "") + "Suture")) { forceps = false; ReleasePointer(); gesture.Reset(); }
            Rect field = new Rect(18, 166, 574, 310);
            GUI.Box(field, "");
            GUI.BeginGroup(field);
            Color old = GUI.color;
            GUI.color = new Color(0.42f, 0.16f, 0.18f); GUI.DrawTexture(new Rect(230, 35, 114, 240), Texture2D.whiteTexture);
            GUI.color = new Color(0.2f, 0.85f, 0.65f, 0.4f);
            if (extraction) GUI.DrawTexture(new Rect(start.x * field.width - 22, end.y * field.height, 44, (start.y - end.y) * field.height), Texture2D.whiteTexture);
            else GUI.DrawTexture(new Rect(start.x * field.width, start.y * field.height - 15, (end.x - start.x) * field.width, 30), Texture2D.whiteTexture);
            GUI.color = old;
            GUI.Box(new Rect(start.x * field.width - 26, start.y * field.height - 13, 52, 26), extraction ? "SHARD" : "START");
            GUI.Box(new Rect(end.x * field.width - 25, end.y * field.height - 13, 50, 26), "END");
            GUI.Label(new Rect(8, 5, 550, 23), "Hold left mouse at the start, follow the green path, release at END.");
            int control = GUIUtility.GetControlID(73031, FocusType.Passive);
            Event ev = Event.current;
            Vector2 point = new Vector2(ev.mousePosition.x / field.width, ev.mousePosition.y / field.height);
            if (ev.button == 0 && ev.type == EventType.MouseDown && new Rect(0, 0, field.width, field.height).Contains(ev.mousePosition))
            {
                if (forceps != extraction) message = extraction ? "Select forceps first." : "Select the suture tool first.";
                else if (!gesture.Begin(point.x, point.y)) message = "Begin on the marked start point.";
                else { gestureControl = control; GUIUtility.hotControl = control; }
                ev.Use();
            }
            else if (ev.type == EventType.MouseDrag && gesture.Active)
            {
                if (!gesture.Move(point.x, point.y)) Slip();
                ev.Use();
            }
            else if (ev.type == EventType.MouseUp && ev.button == 0 && gesture.Active)
            {
                ReleasePointer();
                if (!gesture.End(point.x, point.y)) Slip();
                else if (extraction)
                {
                    if (current.Extract()) { PrepareGesture(); message = "Fragment lifted. Select Suture, then complete three stitches."; }
                    else Abort("Wound changed. Treatment cancelled.");
                }
                else if (current.Stitch(out bool completed))
                {
                    if (completed)
                    {
                        message = string.Format("Complete: 1 fragment removed. Skin {0:0.0}/100; bleeding {1:0.00}.", current.Limb.skinHealth, current.Limb.bleedAmount);
                        Logger.LogInfo("Completed fragment treatment on " + current.Limb.fullName + ". " + message);
                        operation = null; gesture = null;
                        if (sourceHealthView != null) Close();
                    }
                    else { PrepareGesture(); message = "Stitch placed. Continue with the next stitch."; }
                }
                else Abort("Wound changed. Treatment cancelled.");
                ev.Use();
            }
            GUI.EndGroup();
        }
        void Slip()
        {
            ReleasePointer();
            gesture.Reset();
            if (operation.Slip()) message = "Tool slipped: -1 skin, +0.2 bleeding (within wound limits). Try that step again.";
            else Abort("Wound changed. Treatment cancelled.");
        }
    }
}
