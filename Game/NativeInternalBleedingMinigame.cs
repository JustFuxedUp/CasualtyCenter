using System;
using System.Collections.Generic;
using CasualtiesSurgery.Procedures;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace CasualtiesSurgery.Game
{
    internal sealed class NativeInternalBleedingMinigame : Minigame, ISurgery
    {
        const float SeamLength = 108, IncisionLength = 252;
        readonly MinigameBase host;
        readonly WoundView source;
        readonly Body body;
        readonly Limb limb;
        readonly KeyCode wheelKey;
        readonly Action<string> log;
        readonly Vector3 origin;
        readonly int scene;
        readonly InternalBleedingState state;
        readonly SurgeryActions actions;
        readonly SutureStroke suture = new SutureStroke(2);
        readonly ScalpelTrace scalpel = new ScalpelTrace();
        readonly Image[] lesions = new Image[InternalBleedingState.MaxWounds];
        readonly TextMeshProUGUI[] woundLabels = new TextMeshProUGUI[InternalBleedingState.MaxWounds];
        readonly LacerationArt[] lacerationArt = new LacerationArt[InternalBleedingState.MaxWounds];
        readonly BloodPoolVisual[] pools = new BloodPoolVisual[InternalBleedingState.MaxWounds];
        RectTransform root, field, wheel;
        TextMeshProUGUI heading;
        ToolWheelGraphic wheelGraphic;
        SurgeryEffects effects;
        IncisionPrep prep;
        SutureThread thread;
        WristFade fade;
        KnifeTipVisual knifeVisual;
        Image[] dots;
        BloodPoolVisual outerPool;
        Vector3 originalScale;
        Vector2 wheelOrigin, frozenHand;
        InternalStage displayed;
        bool ready, wheelOpen, handSetup;
        int gripping = -1, stitching = -1;
        Vector2 previousGrip;
        public bool Ended { get; private set; }
        public float HandScale { get; }
        public bool BlocksToolInput => !Ended && (wheelOpen || Input.GetKey(wheelKey) || Input.GetKeyUp(wheelKey) ||
            Input.GetMouseButton(1) || Input.GetMouseButtonUp(1));
        SurgeryTool Tool => Plugin.SelectedTool;

        public NativeInternalBleedingMinigame(MinigameBase host, WoundView source, Limb limb,
            float handScale, KeyCode wheelKey, Action<string> log)
        {
            this.host = host;
            this.source = source;
            this.limb = limb;
            this.wheelKey = wheelKey;
            this.log = log;
            HandScale = handScale;
            body = source.body;
            origin = body.transform.position;
            scene = body.gameObject.scene.handle;
            state = InternalBleedingStore.Get(limb);
            actions = new SurgeryActions(limb, this, InvalidReason, ProcedureKind.InternalBleeding);
            suture.TurnCommitted += ThreadTurn;
        }

        public override HandSpriteType HandType() => Tool == SurgeryTool.Scalpel ? HandSpriteType.Blade :
            Tool == SurgeryTool.Forceps ? HandSpriteType.Tweezers :
            Tool == SurgeryTool.Suture ? HandSpriteType.Point : HandSpriteType.Grasp;
        public override string GuideLocaleString() => "shrapnelMinigameGuide";
        public override bool NeedsItem() => false;

        public override void Start()
        {
            originalScale = host.handTransform.localScale;
            handSetup = true;
            fade = host.handTransform.gameObject.AddComponent<WristFade>();
            knifeVisual = host.handTransform.gameObject.AddComponent<KnifeTipVisual>();
            root = Box("InternalBleedingSurgery", host.minigameScreen, Vector2.zero, Vector2.one * 1000,
                new Color(.025f, .05f, .11f)).rectTransform;
            root.SetSiblingIndex(0);
            host.spawnedMiniGame = root;
            heading = Label(root, new Vector2(0, 440), new Vector2(940, 70), 24);
            wheelGraphic = ToolWheelGraphic.Create(root, host.guideText.font, .4f);
            wheel = wheelGraphic.rectTransform;
            BuildStage();
            effects = new SurgeryEffects(root, host.guideText.font, field);
        }

        void BuildStage()
        {
            effects?.Detach();
            if (field != null) { field.gameObject.SetActive(false); Object.Destroy(field.gameObject); }
            field = Box("OperatingField", root, Vector2.zero, new Vector2(900, 700),
                new Color(.045f, .085f, .16f)).rectTransform;
            field.SetSiblingIndex(1);
            displayed = state.Stage;
            thread = new SutureThread(field);
            var seam = new Color(.51f, .10f, .14f);
            if (displayed == InternalStage.Repair)
            {
                Box("OpenTorso", field, Vector2.zero, new Vector2(730, 610), SurgeryArt.OpenField);
                for (int i = 0; i < state.Wounds.Length; i++)
                {
                    var position = WoundPosition(i);
                    var rotation = new Vector3(0, 0, state.Wounds[i].Angle);
                    bool laceration = state.Wounds[i].Laceration;
                    lesions[i] = Box(laceration ? "Laceration" : "Bruise", field, position,
                        laceration ? new Vector2(LacerationDrag.OpenWidth, SeamLength) : Vector2.one * 58, seam);
                    lesions[i].rectTransform.localEulerAngles = rotation;
                    if (laceration)
                    {
                        lacerationArt[i] = lesions[i].gameObject.AddComponent<LacerationArt>();
                        lacerationArt[i].Setup(lesions[i], state.Wounds[i].Phase);
                    }
                    else lesions[i].sprite = SurgeryArt.Get("bruise");
                    actions.Prepare(state.Wounds[i].Pools);
                    pools[i] = new BloodPoolVisual(state.Wounds[i].Pools, field, position, host, actions);
                    woundLabels[i] = Label(field, position + Vector2.down * 80, new Vector2(210, 32), 18);
                    woundLabels[i].text = laceration ? "Laceration" : "Bruise";
                }
            }
            else
            {
                bool incision = displayed == InternalStage.Incision;
                Box(incision ? "IncisionGuide" : "IncisionSeam", field, Vector2.zero,
                    new Vector2(incision ? 5 : 14, IncisionLength), seam);
                if (incision)
                {
                    dots = new Image[InternalBleedingState.CutCount];
                    for (int i = 0; i < dots.Length; i++)
                        dots[i] = Box("CutDot", field, CutPoint(i), Vector2.one * 16, new Color(.9f, .65f, .15f));
                }
                actions.Prepare(incision ? state.IncisionPools : state.ClosurePools);
                outerPool = new BloodPoolVisual(incision ? state.IncisionPools : state.ClosurePools, field, Vector2.zero, host, actions);
            }
            prep = new IncisionPrep(field, state.GelCoverage);
            prep.Refresh(displayed == InternalStage.Incision);
            effects?.Bind(field);
            ResetInput();
        }

        public string InvalidReason()
        {
            if (Ended || host == null || MinigameBase.main != host || !ReferenceEquals(host.currentMinigame, this))
                return "Native internal operation closed.";
            if (source == null || !source.gameObject.activeInHierarchy || source.body != body || body == null)
                return "Patient view changed.";
            if (body.gameObject.scene.handle != scene || Vector3.Distance(origin, body.transform.position) > 1.5f)
                return "Patient moved or changed scene.";
            return state.Closed ? null : InternalBleedingStore.Ineligible(body, limb, this);
        }

        public override void Update(List<RaycastResult> hits)
        {
            string invalid = InvalidReason();
            if (invalid != null) { Finish(invalid); return; }
            effects.Tick();
            if (!wheelOpen && Input.GetMouseButtonDown(1) && !Inside()) { Finish("Internal operation interrupted; progress saved."); return; }
            if (BlocksToolInput && Input.GetKeyDown(KeyBinds.GetBind("pause"))) { Finish("Internal operation interrupted."); return; }
            if (state.Stage != displayed)
            {
                effects.Stop();
                Plugin.Feedback?.StopLoop(this);
                if (state.Closed) { Finish("Internal bleeding operation completed."); return; }
                if (displayed == InternalStage.Repair && UpdateLacerations()) return;
                BuildStage();
            }
            RefreshField();
            if (HandleWheel() || !ready || Time.deltaTime <= 0)
            {
                effects.Stop();
                Plugin.Feedback?.StopLoop(this);
                if (!wheelOpen && !host.handClicking) ready = true;
                RefreshGuide();
                return;
            }

            Vector3 contact = SurgeryContact.Point(host, Tool);
            Vector2 point = field.InverseTransformPoint(contact);
            float gel = effects.Draw(Tool, host.handClicking, contact, Time.deltaTime);
            Plugin.Feedback?.HeldTool(this, Tool, host.handClicking);
            if (Tool == SurgeryTool.Suction)
            {
                if (host.handClicking) Suction(contact);
            }
            else if (Tool == SurgeryTool.Gel && gel > 0)
                ApplyGel(point, gel);
            else if (displayed == InternalStage.Repair)
            {
                if (Tool == SurgeryTool.Forceps) Pinch(point);
                if (Tool == SurgeryTool.Suture) Stitch(point);
            }
            else if (displayed == InternalStage.Incision && !state.IncisionPools.Blocking && Tool == SurgeryTool.Scalpel)
                Cut(point);
            else if (displayed == InternalStage.Closure && Tool == SurgeryTool.Suture)
            {
                if (!state.ClosurePools.Blocking) Stitch(point);
                else if (suture.Active) { suture.Reset(); thread.Clear(); }
            }

            if (state.Stage != displayed)
            {
                effects.Stop();
                Plugin.Feedback?.StopLoop(this);
            }
            RefreshField();
            RefreshGuide();
        }

        bool UpdateLacerations()
        {
            bool fading = false;
            for (int i = 0; i < state.Wounds.Length; i++)
                if (lacerationArt[i] != null)
                    fading |= lacerationArt[i].Refresh(state.Wounds[i].Phase, gripping == i ? (float?)LacerationDrag.Width(previousGrip.x) : null);
            return fading;
        }

        void RefreshField()
        {
            if (displayed == InternalStage.Repair)
            {
                UpdateLacerations();
                for (int i = 0; i < state.Wounds.Length; i++)
                {
                    var wound = state.Wounds[i];
                    if (!wound.Pools.Rolled && wound.Phase != InternalWoundPhase.Repaired &&
                        (body.internalBleeding > 0 || limb.bleedAmount > 0))
                        actions.Observe(wound.Pools, true);
                    actions.Prepare(wound.Pools);
                    pools[i].Refresh();
                    var image = lesions[i];
                    bool laceration = state.Wounds[i].Laceration;
                    bool repaired = wound.Phase == InternalWoundPhase.Repaired;
                    if (!laceration)
                        image.color = Color.Lerp(SurgeryArt.Bruise, SurgeryArt.Gel, wound.GelMl / GelRules.FragmentMl);
                    if (laceration)
                    {
                        woundLabels[i].text = repaired ? "Stitched" : wound.Phase == InternalWoundPhase.Pinched
                            ? "Suture · " + Mathf.CeilToInt(wound.Seconds) + "s" : "Laceration";
                    }
                }
                return;
            }
            bool incision = displayed == InternalStage.Incision;
            var pool = incision ? state.IncisionPools : state.ClosurePools;
            actions.Observe(pool, limb.bleedAmount > 0);
            outerPool.Refresh();
            if (incision)
                for (int i = 0; i < dots.Length; i++) dots[i].gameObject.SetActive(!state.Cuts[i]);
        }

        void Suction(Vector3 contact)
        {
            if (displayed != InternalStage.Repair)
            {
                outerPool.Distance(contact, out int slot);
                outerPool.Suction(slot, Time.deltaTime);
                return;
            }
            BloodPoolVisual target = null;
            int selected = -1;
            float best = float.PositiveInfinity;
            for (int i = 0; i < state.Wounds.Length; i++)
            {
                var pool = pools[i];
                float distance = pool.Distance(contact, out int slot);
                if (distance >= best) continue;
                target = pool;
                selected = slot;
                best = distance;
            }
            target?.Suction(selected, Time.deltaTime);
        }

        Vector2 WoundPosition(int index) => new Vector2(state.Wounds[index].X, state.Wounds[index].Y);

        Vector2 WoundLocalPoint(int index, Vector2 point) =>
            lesions[index].rectTransform.InverseTransformPoint(field.TransformPoint(point));

        static Vector2 CutPoint(int index) => IncisionPrep.Section(index);

        void Cut(Vector2 point)
        {
            if (!host.handClicking) { scalpel.Reset(); return; }
            for (int i = 0; i < dots.Length; i++)
            {
                Vector2 target = CutPoint(i);
                if (scalpel.Touch(point.x, point.y, target.x, target.y, 14))
                {
                    actions.Cut(state, i);
                }
            }
            scalpel.Sample(point.x, point.y);
        }

        void ApplyGel(Vector2 point, float ml)
        {
            if (displayed == InternalStage.Repair)
            {
                UpdateLacerations();
                for (int i = 0; i < state.Wounds.Length; i++)
                {
                    if (state.Wounds[i].Laceration || (point - WoundPosition(i)).sqrMagnitude > 48 * 48) continue;
                    if (actions.Bruise(state, i, ml)) Good(WoundPosition(i));
                    return;
                }
            }
            else if ((displayed == InternalStage.Incision ? state.IncisionPools : state.ClosurePools).Blocking)
                return;
            prep.Apply(point, ml, (section, dose) => actions.Disinfect(state, section, dose));
            prep.Refresh(displayed == InternalStage.Incision);
        }

        void Pinch(Vector2 point)
        {
            if (host.handStartedClicking)
                for (int i = 0; i < state.Wounds.Length; i++)
                {
                    var wound = state.Wounds[i];
                    Vector2 relative = WoundLocalPoint(i, point);
                    float edge = Mathf.Abs(relative.x);
                    if (!wound.Laceration || wound.Phase != InternalWoundPhase.Open || wound.Pools.Blocking ||
                        edge < 17 || edge > 48 || Mathf.Abs(relative.y) > SeamLength / 2 + 12) continue;
                    gripping = i;
                    previousGrip = relative;
                    Plugin.Feedback?.Grab();
                    break;
                }
            if (gripping < 0) return;
            var target = state.Wounds[gripping];
            if (host.handStoppedClicking || !host.handClicking || target.Pools.Blocking)
            {
                gripping = -1;
                return;
            }
            Vector2 delta = WoundLocalPoint(gripping, point);
            switch (LacerationDrag.Evaluate(previousGrip.x, previousGrip.y, delta.x, delta.y, SeamLength))
            {
                case LacerationDragResult.Overpull:
                    actions.Overpull(state, gripping);
                    Plugin.Feedback?.Mistake();
                    gripping = -1;
                    return;
                case LacerationDragResult.Close:
                    if (actions.Pinch(state, gripping)) Good(WoundPosition(gripping));
                    gripping = -1;
                    return;
            }
            previousGrip = delta;
        }

        void Stitch(Vector2 point)
        {
            bool outer = displayed == InternalStage.Closure;
            if (host.handStartedClicking)
            {
                stitching = -1;
                if (!outer)
                {
                    float best = 115 * 115;
                    for (int i = 0; i < state.Wounds.Length; i++)
                    {
                        var wound = state.Wounds[i];
                        float distance = (point - WoundPosition(i)).sqrMagnitude;
                        if (!wound.Laceration || wound.Phase != InternalWoundPhase.Pinched || wound.Pools.Blocking || distance >= best) continue;
                        best = distance;
                        stitching = i;
                    }
                }
                if (outer || stitching >= 0)
                {
                    Vector2 sample = SuturePoint(point, outer);
                    thread.Begin(point);
                    suture.Begin(sample.x, sample.y);
                }
            }
            if (!suture.Active) return;
            if (!outer && (state.Wounds[stitching].Phase != InternalWoundPhase.Pinched || state.Wounds[stitching].Pools.Blocking))
            {
                suture.Reset();
                thread.Clear();
                return;
            }
            Vector2 next = SuturePoint(point, outer);
            if (host.handClicking)
            {
                suture.Move(next.x, next.y);
                thread.Preview(point);
            }
            if (!host.handStoppedClicking) return;
            if (!suture.End(next.x, next.y, out int quality))
            {
                thread.Clear();
                Plugin.Feedback?.Mistake();
                return;
            }
            if (outer)
            {
                if (!actions.Close(state, quality))
                {
                    thread.Clear();
                    return;
                }
                Result(quality, Vector2.zero);
            }
            else
            {
                if (!actions.Stitch(state, stitching)) { thread.Clear(); return; }
                Good(WoundPosition(stitching));
            }
            thread.Clear();
        }

        Vector2 SuturePoint(Vector2 point, bool outer)
        {
            if (!outer) point = WoundLocalPoint(stitching, point);
            return new Vector2(point.x / 22, .5f - point.y / (outer ? IncisionLength : SeamLength));
        }

        void ThreadTurn(float x, float y)
        {
            bool outer = displayed == InternalStage.Closure;
            var point = new Vector2(x * 22, (.5f - y) * (outer ? IncisionLength : SeamLength));
            if (!outer) point = field.InverseTransformPoint(lesions[stitching].rectTransform.TransformPoint(point));
            thread.Commit(point);
        }

        void Good(Vector2 point) => Plugin.Feedback?.Good(field.TransformPoint(point),
            host.guideText.font, root.GetComponentInParent<Canvas>());

        void Result(int quality, Vector2 point) => Plugin.Feedback?.Result(quality, field.TransformPoint(point),
            host.guideText.font, root.GetComponentInParent<Canvas>());

        Camera CanvasCamera()
        {
            var canvas = root.GetComponentInParent<Canvas>();
            return canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        }

        bool Inside() => RectTransformUtility.RectangleContainsScreenPoint(root, Input.mousePosition, CanvasCamera());

        bool HandleWheel()
        {
            if (Input.GetKeyDown(wheelKey) && Inside())
            {
                wheelOpen = true;
                wheelOrigin = Input.mousePosition;
                frozenHand = host.handPos;
                ResetInput();
                RectTransformUtility.ScreenPointToLocalPointInRectangle(root, wheelOrigin, CanvasCamera(), out var point);
                wheel.localPosition = point;
                wheel.SetAsLastSibling();
                wheel.gameObject.SetActive(true);
            }
            if (!wheelOpen) return false;
            int selected = ToolWheelGraphic.Pick((Vector2)Input.mousePosition - wheelOrigin, wheelGraphic.ToolCount);
            wheelGraphic.Selected = selected;
            wheelGraphic.SetVerticesDirty();
            if (Input.GetKeyUp(wheelKey))
            {
                if (selected >= 0) Plugin.SelectedTool = (SurgeryTool)selected;
                wheelOpen = false;
                wheel.gameObject.SetActive(false);
                host.UpdateHandSprite(true);
            }
            return true;
        }

        void ResetInput()
        {
            ready = false;
            gripping = stitching = -1;
            suture.Reset();
            thread.Clear();
            scalpel.Reset();
            Plugin.Feedback?.StopLoop(this);
            host.ForceUngrab();
        }

        public override void PhysicsUpdate(float dt)
        {
            if (wheelOpen) { host.handPos = frozenHand; host.handVelocity = Vector2.zero; }
        }

        public void RefreshGuide()
        {
            if (Ended || heading == null) return;
            heading.text = limb.fullName + " / Internal bleeding / " + Tool +
                " / disinfected " + Mathf.RoundToInt(state.Coverage * 100) + "%";
            string instruction = displayed == InternalStage.Incision ? "Scalpel: cut along the gold dots." :
                displayed == InternalStage.Closure ? "Suture: cross the incision three times, then release." :
                "Gel bruises. Grip a laceration edge, hold and drag inward, then zigzag across it with Suture within 6 seconds.";
            bool blocked = displayed == InternalStage.Incision ? state.IncisionPools.Blocking :
                displayed == InternalStage.Closure && state.ClosurePools.Blocking;
            host.guideText.text = "Hold " + wheelKey + " for tools. Right-click outside to exit. " +
                (Tool == SurgeryTool.Suction || blocked ? "Hold suction over each blood pool." :
                    Tool == SurgeryTool.Gel && displayed != InternalStage.Repair
                        ? "Gel: coat the incision from end to end."
                        : instruction + (displayed == InternalStage.Repair ? " Suction clears blood pools." : ""));
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
            Ended = true;
            Plugin.Feedback?.StopLoop(this);
            Plugin.ProtectExitClick();
            if (knifeVisual != null) { knifeVisual.enabled = false; Object.Destroy(knifeVisual); }
            if (fade != null) { fade.enabled = false; Object.Destroy(fade); }
            if (handSetup && host != null && host.handTransform != null) host.handTransform.localScale = originalScale;
            if (root != null && (host == null || !ReferenceEquals(host.currentMinigame, this)))
            {
                root.gameObject.SetActive(false);
                Object.Destroy(root.gameObject);
            }
        }

        static Image Box(string name, Transform parent, Vector2 position, Vector2 size, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            var image = go.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        TextMeshProUGUI Label(Transform parent, Vector2 position, Vector2 size, float fontSize)
        {
            var go = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            var label = go.GetComponent<TextMeshProUGUI>();
            label.font = host.guideText.font;
            label.fontSize = fontSize;
            label.alignment = TextAlignmentOptions.Center;
            label.raycastTarget = false;
            return label;
        }
    }
}
