using System;
using System.Collections.Generic;
using CasualtiesSurgery.Procedures;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CasualtiesSurgery.Game
{
    // A real game Minigame: the host supplies the hand physics, grip input, exit and cleanup.
    public sealed class NativeFragmentMinigame : Minigame
    {
        readonly MinigameBase host;
        readonly WoundView source;
        readonly OperationSession session;
        readonly Action<string> log;
        RectTransform root, field, path, startMarker, endMarker;
        RectTransform wound, strokeRoot, wheel;
        ToolWheelGraphic wheelGraphic;
        WristFade fade;
        Vector3 originalScreenScale;
        bool scaleApplied, wheelOpen;
        Vector2 wheelScreenOrigin, frozenHand, lastStroke;
        readonly SutureStroke suture = new SutureStroke();
        readonly KeyCode wheelKey;
        readonly float viewScale;
        float woundLength, woundWidth;
        int lineCount;
        public bool BlocksToolInput => !Ended && (wheelOpen || Input.GetKey(wheelKey) || Input.GetKeyUp(wheelKey));
        public float ViewScale => viewScale;
        TextMeshProUGUI heading, status, startLabel;
        PrecisionDrag gesture;
        bool forceps = true, readyForPress;
        string message = "Use the native hand: grip SHARD, follow the path, release at END.";
        public bool Ended { get; private set; }

        public NativeFragmentMinigame(MinigameBase host, WoundView source, Limb limb,
            float recovery, float reduction, KeyCode wheelKey, float viewScale, Action<string> log)
        {
            this.host = host; this.source = source; this.log = log;
            this.wheelKey = wheelKey; this.viewScale = viewScale;
            session = new OperationSession(source.body, limb, recovery, reduction, this);
        }
        public override HandSpriteType HandType() => forceps ? HandSpriteType.Tweezers : HandSpriteType.Point;
        // The host looks up this existing key once at startup; RefreshGuide then supplies our stage instructions.
        public override string GuideLocaleString() => "shrapnelMinigameGuide";
        public override bool NeedsItem() => false;
        public override bool CanExit() => true;

        public override void Start()
        {
            originalScreenScale = host.minigameScreen.localScale;
            host.minigameScreen.localScale = originalScreenScale * viewScale; scaleApplied = true;
            fade = host.handTransform.gameObject.AddComponent<WristFade>();
            var go = new GameObject("CasualtyCenterFragmentMinigame", typeof(RectTransform));
            root = (RectTransform)go.transform;
            root.SetParent(host.minigameScreen, false);
            root.anchorMin = Vector2.zero; root.anchorMax = Vector2.one;
            root.offsetMin = root.offsetMax = Vector2.zero;
            root.SetSiblingIndex(0); host.spawnedMiniGame = root;
            // Match the host's +/-524 hand space. All visuals live inside its screen.
            Image background = Box("Background", root, Vector2.zero, new Vector2(1000, 1000), new Color(0.08f, 0.12f, 0.13f));
            background.raycastTarget = true;
            heading = Text("Heading", root, new Vector2(0, 440), new Vector2(940, 95), 38);
            field = Box("BodySection", root, new Vector2(0, 5), new Vector2(900, 700), new Color(0.24f, 0.18f, 0.18f)).rectTransform;
            // One randomized wound per treatment for now; body-section selection is a later content layer.
            woundLength = UnityEngine.Random.Range(350f, 460f); woundWidth = UnityEngine.Random.Range(50f, 70f);
            wound = Box("Wound", field, new Vector2(UnityEngine.Random.Range(-120f, 120f), UnityEngine.Random.Range(-35f, 35f)),
                new Vector2(35, woundLength), new Color(0.48f, 0.09f, 0.12f)).rectTransform;
            wound.localEulerAngles = new Vector3(0, 0, UnityEngine.Random.Range(-30f, 30f));
            wound.GetComponent<Image>().raycastTarget = false;
            strokeRoot = new GameObject("SutureTrace", typeof(RectTransform)).GetComponent<RectTransform>();
            strokeRoot.SetParent(wound, false);
            path = Box("ExtractionPath", wound, Vector2.zero, Vector2.one, new Color(0.2f, 0.85f, 0.65f, 0.4f)).rectTransform;
            startMarker = Box("Start", wound, Vector2.zero, new Vector2(100, 48), new Color(0.22f, 0.45f, 0.4f)).rectTransform;
            startLabel = Text("StartLabel", startMarker, Vector2.zero, new Vector2(110, 60), 28);
            endMarker = Box("End", wound, Vector2.zero, new Vector2(100, 48), new Color(0.22f, 0.45f, 0.4f)).rectTransform;
            Text("EndLabel", endMarker, Vector2.zero, new Vector2(110, 60), 28).text = "END";
            status = Text("Status", root, new Vector2(0, -415), new Vector2(940, 145), 32);
            wheel = new GameObject("ToolWheel", typeof(RectTransform), typeof(ToolWheelGraphic)).GetComponent<RectTransform>();
            wheel.SetParent(root, false); wheel.sizeDelta = new Vector2(380, 380);
            wheelGraphic = wheel.GetComponent<ToolWheelGraphic>(); wheelGraphic.raycastTarget = false;
            Text("Forceps", wheel, new Vector2(118, 0), new Vector2(145, 90), 30).text = "Forceps";
            Text("Suture", wheel, new Vector2(-118, 0), new Vector2(145, 90), 30).text = "Suture";
            wheel.gameObject.SetActive(false);
            PrepareGesture(); RefreshGuide();
        }

        public string InvalidReason()
        {
            if (Ended) return "Operation ended.";
            if (host == null || !ReferenceEquals(MinigameBase.main, host) || !ReferenceEquals(host.currentMinigame, this))
                return "Native minigame was closed or replaced.";
            if (source == null || !source.gameObject.activeInHierarchy || source.body != session.Patient)
                return "Health screen or patient changed.";
            return session.InvalidReason();
        }

        public override void Update(List<RaycastResult> hits)
        {
            if (Ended) return;
            string invalid = InvalidReason();
            if (invalid != null) { Finish(invalid); return; }
            if (BlocksToolInput && Input.GetKeyDown(KeyBinds.GetBind("pause"))) { Finish("Surgery cancelled."); return; }
            if (HandleWheel()) { RefreshGuide(); return; }
            if (!readyForPress)
            {
                if (!host.handClicking) readyForPress = true;
                RefreshGuide(); return;
            }
            Vector3 local = wound.InverseTransformPoint(host.handTransform.position);
            float x = local.x / woundWidth, y = 0.5f - local.y / woundLength;
            bool extraction = session.Procedure.Stage == FragmentStage.Extraction;
            if (host.handStartedClicking)
            {
                if (forceps != extraction) message = "Hold " + wheelKey + " to select " + (extraction ? "Forceps." : "Suture.");
                else if (extraction)
                { if (!gesture.Begin(x, y)) message = "Grip SHARD first."; }
                else
                {
                    ClearTrace(); lastStroke = new Vector2(local.x, local.y);
                    if (!suture.Begin(x, y)) message = "Begin at START, then zigzag down across both sides.";
                }
            }
            bool drawing = extraction ? gesture.Active : suture.Active;
            if (drawing && (host.handClicking || host.handStoppedClicking))
            {
                if (!extraction) Trace(new Vector2(local.x, local.y));
                if (host.handStoppedClicking)
                {
                    if (extraction)
                    {
                        if (!gesture.End(x, y)) Slip();
                        else if (!session.Extract()) { Finish("Wound changed; treatment cancelled."); return; }
                        else { PrepareGesture(); message = "Fragment lifted. Switch to Suture and draw one zigzag down the wound."; }
                    }
                    else if (!suture.End(x, y, out int quality)) Slip();
                    else if (!session.CompleteContinuousSuture()) { Finish("Wound changed; treatment cancelled."); return; }
                    else { PlayerCamera.main.DoAlert("Suture quality: " + quality + "/100", true); Finish("Suture quality " + quality + "/100. Fragment treated on " + session.Limb.fullName + "."); return; }
                }
                else if (!(extraction ? gesture.Move(x, y) : suture.Move(x, y))) Slip();
            }
            RefreshGuide();
        }
        public override void PhysicsUpdate(float delta)
        {
            if (wheelOpen) { host.handPos = frozenHand; host.handVelocity = Vector2.zero; }
        }
        bool HandleWheel()
        {
            if (Input.GetKeyDown(wheelKey))
            {
                wheelOpen = true; wheelScreenOrigin = Input.mousePosition; frozenHand = host.handPos;
                gesture.Reset(); suture.Reset(); ClearTrace(); readyForPress = false; host.ForceUngrab();
                var canvas = root.GetComponentInParent<Canvas>();
                RectTransformUtility.ScreenPointToLocalPointInRectangle(root, wheelScreenOrigin,
                    canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera, out var point);
                wheel.localPosition = point; wheel.SetAsLastSibling(); wheel.gameObject.SetActive(true);
            }
            if (!wheelOpen) return false;
            Vector2 delta = (Vector2)Input.mousePosition - wheelScreenOrigin;
            int selected = delta.magnitude < 14 ? -1 : (delta.x >= 0 ? 0 : 1);
            wheelGraphic.Selected = selected; wheelGraphic.SetVerticesDirty();
            if (Input.GetKeyUp(wheelKey))
            {
                if (selected >= 0) forceps = selected == 0;
                wheelOpen = false; wheel.gameObject.SetActive(false); host.UpdateHandSprite(true);
                message = forceps ? "Forceps selected." : "Suture selected: hold and zigzag down the wound, then release.";
            }
            return true;
        }
        void ClearTrace()
        {
            if (strokeRoot == null) return;
            for (int i = strokeRoot.childCount-1; i >= 0; i--) UnityEngine.Object.Destroy(strokeRoot.GetChild(i).gameObject);
            lineCount = 0;
        }
        void Trace(Vector2 point)
        {
            Vector2 delta = point-lastStroke;
            if (delta.magnitude < 3 || lineCount >= 512) return;
            var line = Box("Thread", strokeRoot, (point+lastStroke)*0.5f, new Vector2(delta.magnitude, 5), new Color(0.8f, 0.95f, 0.87f)).rectTransform;
            line.localEulerAngles = new Vector3(0,0,Mathf.Atan2(delta.y,delta.x)*Mathf.Rad2Deg);
            line.GetComponent<Image>().raycastTarget = false; lastStroke = point; lineCount++;
        }

        void Slip()
        {
            gesture.Reset(); suture.Reset(); ClearTrace();
            if (!session.Slip()) { Finish("Wound changed; treatment cancelled."); return; }
            host.ForceUngrab();
            message = "Tool slipped: -1 skin, up to +0.2 bleeding. Release and try again.";
        }
        void PrepareGesture()
        {
            bool extraction = session.Procedure.Stage == FragmentStage.Extraction;
            gesture = new PrecisionDrag(0, 0.65f, 0, 0.15f, 0.18f);
            startMarker.anchoredPosition = extraction ? new Vector2(0, -woundLength*0.15f) : new Vector2(-woundWidth, woundLength*0.5f);
            endMarker.anchoredPosition = extraction ? new Vector2(0, woundLength*0.35f) : new Vector2(0, -woundLength*0.5f);
            path.gameObject.SetActive(extraction); path.anchoredPosition = new Vector2(0, woundLength*0.1f);
            path.sizeDelta = new Vector2(woundWidth*0.36f, woundLength*0.5f);
            startLabel.text = extraction ? "SHARD" : "START";
            if (!extraction)
            {
                // Faint example marks suggest the motion without requiring exact tracing.
                for (int i = 0; i <= 6; i++)
                    Box("StitchGuide", wound, new Vector2(i%2 == 0 ? -woundWidth : woundWidth, woundLength*(0.5f-i/6f)),
                        new Vector2(12,12), new Color(0.4f,0.7f,0.6f,0.5f)).raycastTarget = false;
            }
        }
        public void RefreshGuide()
        {
            if (Ended || heading == null) return;
            bool extraction = session.Procedure.Stage == FragmentStage.Extraction;
            heading.text = session.Limb.fullName + (extraction ? " / EXTRACT FRAGMENT" : " / CONTINUOUS SUTURE");
            status.text = message;
            host.guideText.text = "Hold " + wheelKey + " for tools. " + (forceps ? "Forceps" : "Suture") + " selected. Free test kit.";
        }
        public void Finish(string reason)
        {
            if (Ended) return;
            log(reason);
            OnEnded();
            if (host != null && ReferenceEquals(host.currentMinigame, this)) host.EndMinigame();
        }
        public void OnEnded()
        {
            if (Ended) return;
            Ended = true; gesture?.Reset(); suture.Reset(); session.Cancel();
            if (fade != null) { fade.enabled = false; UnityEngine.Object.Destroy(fade); }
            if (host != null && host.minigameScreen != null && scaleApplied) { host.minigameScreen.localScale = originalScreenScale; scaleApplied = false; }
            // EndMinigame owns destruction normally. Also clean our root if the host was replaced.
            if (root != null && (host == null || !ReferenceEquals(host.currentMinigame, this)))
            { root.gameObject.SetActive(false); UnityEngine.Object.Destroy(root.gameObject); }
        }
        static Image Box(string name, Transform parent, Vector2 position, Vector2 size, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            var rect = (RectTransform)go.transform; rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position; rect.sizeDelta = size;
            var image = go.GetComponent<Image>(); image.color = color; return image;
        }
        TextMeshProUGUI Text(string name, Transform parent, Vector2 position, Vector2 size, float fontSize)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            var rect = (RectTransform)go.transform; rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position; rect.sizeDelta = size;
            var text = go.GetComponent<TextMeshProUGUI>(); text.font = host.guideText.font;
            text.fontSize = fontSize; text.color = Color.white; text.alignment = TextAlignmentOptions.Center;
            text.enableWordWrapping = true; text.raycastTarget = false; return text;
        }
    }

    [HarmonyPatch(typeof(MinigameBase), nameof(MinigameBase.GetMousePos))]
    internal static class CompactHandCoordinates
    {
        static void Postfix(MinigameBase __instance, ref Vector2 __result)
        {
            if (__instance.currentMinigame is NativeFragmentMinigame surgery && !surgery.Ended)
            {
                // Rescale pointer coordinates, but keep the native tremor in local units.
                Vector2 pointer = ((Vector2)Input.mousePosition - (Vector2)__instance.minigameScreen.position) / PlayerCamera.uiScale;
                __result += pointer * (1f / surgery.ViewScale - 1f);
            }
        }
    }

    [HarmonyPatch(typeof(MinigameBase), nameof(MinigameBase.EndMinigame))]
    internal static class NativeSurgeryExit
    {
        static void Prefix(MinigameBase __instance)
        {
            if (__instance.currentMinigame is NativeFragmentMinigame surgery) surgery.OnEnded();
        }
    }
}
