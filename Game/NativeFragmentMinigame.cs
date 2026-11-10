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
        Image forcepsButton, sutureButton;
        TextMeshProUGUI heading, status, forcepsLabel, sutureLabel, startLabel;
        PrecisionDrag gesture;
        bool forceps = true, readyForPress;
        string message = "Use the native hand: grip SHARD, follow the path, release at END.";
        public bool Ended { get; private set; }

        public NativeFragmentMinigame(MinigameBase host, WoundView source, Limb limb,
            float recovery, float reduction, Action<string> log)
        {
            this.host = host; this.source = source; this.log = log;
            session = new OperationSession(source.body, limb, recovery, reduction, this);
        }
        public override HandSpriteType HandType() => forceps ? HandSpriteType.Tweezers : HandSpriteType.Point;
        // The host looks up this existing key once at startup; RefreshGuide then supplies our stage instructions.
        public override string GuideLocaleString() => "shrapnelMinigameGuide";
        public override bool NeedsItem() => false;
        public override bool CanExit() => true;

        public override void Start()
        {
            var go = new GameObject("CasualtyCenterFragmentMinigame", typeof(RectTransform));
            root = (RectTransform)go.transform;
            root.SetParent(host.minigameScreen, false);
            root.anchorMin = Vector2.zero; root.anchorMax = Vector2.one;
            root.offsetMin = root.offsetMax = Vector2.zero;
            root.SetSiblingIndex(0); host.spawnedMiniGame = root;
            // Match the host's +/-524 hand space. All visuals live inside its screen.
            Image background = Box("Background", root, Vector2.zero, new Vector2(1000, 1000), new Color(0.08f, 0.12f, 0.13f));
            background.raycastTarget = true;
            heading = Text("Heading", root, new Vector2(0, 445), new Vector2(940, 60), 28);
            forcepsButton = Box("Forceps", root, new Vector2(-210, 365), new Vector2(380, 64), new Color(0.2f, 0.38f, 0.35f));
            forcepsLabel = Text("ForcepsLabel", forcepsButton.transform, Vector2.zero, new Vector2(370, 60), 24);
            sutureButton = Box("Suture", root, new Vector2(210, 365), new Vector2(380, 64), new Color(0.2f, 0.38f, 0.35f));
            sutureLabel = Text("SutureLabel", sutureButton.transform, Vector2.zero, new Vector2(370, 60), 24);
            field = Box("WoundField", root, new Vector2(0, -10), new Vector2(800, 600), new Color(0.16f, 0.11f, 0.12f)).rectTransform;
            Box("Wound", field, Vector2.zero, new Vector2(160, 480), new Color(0.42f, 0.16f, 0.18f)).raycastTarget = false;
            path = Box("Path", field, Vector2.zero, Vector2.one, new Color(0.2f, 0.85f, 0.65f, 0.4f)).rectTransform;
            path.GetComponent<Image>().raycastTarget = false;
            startMarker = Box("Start", field, Vector2.zero, new Vector2(100, 48), new Color(0.22f, 0.45f, 0.4f)).rectTransform;
            startMarker.GetComponent<Image>().raycastTarget = false;
            startLabel = Text("StartLabel", startMarker, Vector2.zero, new Vector2(100, 48), 20);
            endMarker = Box("End", field, Vector2.zero, new Vector2(100, 48), new Color(0.22f, 0.45f, 0.4f)).rectTransform;
            endMarker.GetComponent<Image>().raycastTarget = false;
            Text("EndLabel", endMarker, Vector2.zero, new Vector2(100, 48), 20).text = "END";
            status = Text("Status", root, new Vector2(0, -410), new Vector2(940, 140), 24);
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
            // The health-menu launch click must be released before any procedure input is accepted.
            if (!readyForPress)
            {
                if (!host.handClicking) readyForPress = true;
                RefreshGuide(); return;
            }
            Vector3 local = field.InverseTransformPoint(host.handTransform.position);
            float x = local.x / 800f + 0.5f, y = 0.5f - local.y / 600f;
            bool extraction = session.Procedure.Stage == FragmentStage.Extraction;
            if (host.handStartedClicking)
            {
                if (Hit(hits, forcepsButton) || Hit(hits, sutureButton))
                {
                    forceps = Hit(hits, forcepsButton); gesture.Reset();
                    host.UpdateHandSprite(true); RefreshGuide(); return;
                }
                if (forceps != extraction) message = extraction ? "Select Forceps with the hand first." : "Select Suture with the hand first.";
                else if (!gesture.Begin(x, y)) message = "Grip the marked start point with the native hand.";
            }
            if (gesture.Active && host.handStoppedClicking)
            {
                if (!gesture.End(x, y)) Slip();
                else if (extraction)
                {
                    if (!session.Extract()) { Finish("Wound changed; treatment cancelled."); return; }
                    PrepareGesture(); message = "Fragment lifted. Select Suture, then place three stitches.";
                }
                else if (session.Stitch(out bool complete))
                {
                    if (complete)
                    {
                        Finish(string.Format("Completed fragment treatment on {0}: skin {1:0.0}, bleeding {2:0.00}.",
                            session.Limb.fullName, session.Limb.skinHealth, session.Limb.bleedAmount)); return;
                    }
                    PrepareGesture(); message = "Stitch placed. Continue with the next stitch.";
                }
                else { Finish("Wound changed; treatment cancelled."); return; }
            }
            else if (gesture.Active && host.handClicking && !gesture.Move(x, y)) Slip();
            RefreshGuide();
        }

        void Slip()
        {
            gesture.Reset();
            if (!session.Slip()) { Finish("Wound changed; treatment cancelled."); return; }
            host.ForceUngrab();
            message = "Tool slipped: -1 skin, up to +0.2 bleeding. Release and try again.";
        }
        void PrepareGesture()
        {
            bool extraction = session.Procedure.Stage == FragmentStage.Extraction;
            Vector2 a = extraction ? new Vector2(0.5f, 0.78f) : new Vector2(0.28f, 0.3f + session.Stitches * 0.2f);
            Vector2 b = extraction ? new Vector2(0.5f, 0.2f) : new Vector2(0.72f, a.y);
            gesture = new PrecisionDrag(a.x, a.y, b.x, b.y, 0.075f);
            Vector2 start = CanvasPoint(a), end = CanvasPoint(b);
            startMarker.anchoredPosition = start; endMarker.anchoredPosition = end;
            path.anchoredPosition = (start + end) * 0.5f;
            path.sizeDelta = extraction ? new Vector2(120, Mathf.Abs(end.y - start.y)) : new Vector2(Mathf.Abs(end.x - start.x), 90);
            startLabel.text = extraction ? "SHARD" : "START";
        }
        static Vector2 CanvasPoint(Vector2 normalized) => new Vector2((normalized.x - 0.5f) * 800, (0.5f - normalized.y) * 600);
        public void RefreshGuide()
        {
            if (Ended || heading == null) return;
            bool extraction = session.Procedure.Stage == FragmentStage.Extraction;
            heading.text = session.Limb.fullName + (extraction ? " / EXTRACT FRAGMENT" : " / STITCH " + (session.Stitches + 1) + " OF 3");
            forcepsLabel.text = (forceps ? "[Selected] " : "") + "Forceps";
            sutureLabel.text = (!forceps ? "[Selected] " : "") + "Suture";
            status.text = message;
            host.guideText.text = "Grip START, follow the path, release at END. Free test kit; the world keeps running.";
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
            Ended = true; gesture?.Reset(); session.Cancel();
            // EndMinigame owns destruction normally. Also clean our root if the host was replaced.
            if (root != null && (host == null || !ReferenceEquals(host.currentMinigame, this)))
            { root.gameObject.SetActive(false); UnityEngine.Object.Destroy(root.gameObject); }
        }
        static bool Hit(List<RaycastResult> hits, Image image)
        {
            foreach (var hit in hits) if (hit.gameObject == image.gameObject) return true;
            return false;
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

    [HarmonyPatch(typeof(MinigameBase), nameof(MinigameBase.EndMinigame))]
    internal static class NativeSurgeryExit
    {
        static void Prefix(MinigameBase __instance)
        {
            if (__instance.currentMinigame is NativeFragmentMinigame surgery) surgery.OnEnded();
        }
    }
}
