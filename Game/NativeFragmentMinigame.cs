using System;
using System.Collections.Generic;
using System.Linq;
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
        OperationSession session;
        readonly Limb patientLimb;
        readonly float recovery, reduction, handScale;
        readonly FragmentBoard board;
        FragmentSite selectedSite;
        sealed class SiteView
        {
            public FragmentSite Site;
            public SiteLayout Layout;
            public RectTransform Wound, Fragment, Mask;
            public Vector2 FragmentOrigin;
            public float Grip, Pull;
            public TextMeshProUGUI Label;
        }
        readonly List<SiteView> sites=new List<SiteView>();
        int page;
        Sprite nativeShard;
        float gripPosition, pullDistance;
        readonly List<GameObject> activeGuides=new List<GameObject>();
        readonly Action<string> log;
        RectTransform root, field, path, startMarker, endMarker;
        RectTransform wound, strokeRoot, wheel;
        ToolWheelGraphic wheelGraphic;
        WristFade fade;
        Vector3 originalHandScale;
        RectTransform windowBounds, fragment, previewLine;
        Vector2 fragmentOrigin;
        bool scaleApplied, wheelOpen;
        Vector2 wheelScreenOrigin, frozenHand, lastStroke;
        readonly SutureStroke suture = new SutureStroke(2f);
        readonly KeyCode wheelKey;
        readonly float viewScale;
        float woundLength, woundWidth;
        int lineCount;
        Vector3 grabOffset;
        sealed class FadingShard { public Image Image; public float Age; }
        readonly List<FadingShard> fading=new List<FadingShard>();
        public bool BlocksToolInput => !Ended && (wheelOpen || Input.GetKey(wheelKey) || Input.GetKeyUp(wheelKey) || Input.GetMouseButton(1) || Input.GetMouseButtonUp(1));
        TextMeshProUGUI heading, startLabel;
        FragmentPull gesture;
        bool forceps = true, readyForPress;
        string message = "Use the native hand: grip SHARD, follow the path, release at END.";
        public bool Ended { get; private set; }

        public NativeFragmentMinigame(MinigameBase host, WoundView source, Limb limb,
            float recovery, float reduction, KeyCode wheelKey, float viewScale, float handScale, Action<string> log)
        {
            this.host = host; this.source = source; this.log = log;
            this.wheelKey = wheelKey; this.viewScale = viewScale;
            patientLimb=limb; this.recovery=recovery; this.reduction=reduction; this.handScale=handScale;
            board=FragmentBoardStore.Get(limb);
        }
        public float HandScale => handScale;
        public override HandSpriteType HandType() => forceps ? HandSpriteType.Tweezers : HandSpriteType.Point;
        // The host looks up this existing key once at startup; RefreshGuide then supplies our stage instructions.
        public override string GuideLocaleString() => "shrapnelMinigameGuide";
        public override bool NeedsItem() => false;
        public override bool CanExit() => true;

        public override void Start()
        {
            originalHandScale = host.handTransform.localScale;
            // UpdateHandSprite rewrites scale every frame; the scoped postfix applies our factor.
            scaleApplied = true;
            fade = host.handTransform.gameObject.AddComponent<WristFade>();
            var go = new GameObject("CasualtyCenterFragmentMinigame", typeof(RectTransform));
            root = (RectTransform)go.transform;
            root.SetParent(host.minigameScreen, false);
            root.anchorMin = Vector2.zero; root.anchorMax = Vector2.one;
            root.offsetMin = root.offsetMax = Vector2.zero;
            root.SetSiblingIndex(0); host.spawnedMiniGame = root;
            // Match the host's +/-524 hand space. All visuals live inside its screen.
            Image background = Box("Background", root, Vector2.zero, new Vector2(1000, 1000), new Color(0.08f, 0.12f, 0.13f));
            background.raycastTarget = true; windowBounds=background.rectTransform;
            heading = Text("Heading", root, new Vector2(0, 440), new Vector2(940, 95), 24);
            field = Box("BodySection", root, new Vector2(0, 5), new Vector2(900, 700), new Color(0.24f, 0.18f, 0.18f)).rectTransform;
            var nativePrefab=Resources.Load<GameObject>("Special/ShrapnelMinigame");
            var nativeImage=nativePrefab!=null && nativePrefab.transform.childCount>0 ? nativePrefab.transform.GetChild(0).GetComponent<Image>() : null;
            if(nativeImage==null || nativeImage.sprite==null) throw new InvalidOperationException("Native shrapnel sprite missing.");
            nativeShard=nativeImage.sprite;
            foreach(var site in board.Sites)
                if(!site.Completed) sites.Add(new SiteView { Site=site,Layout=board.Layout(site.Id) });
            log("Opened fragment board "+board.Seed+" on "+patientLimb.fullName+" with "+board.Remaining+" remaining sites.");
            suture.TurnCommitted += CommitThreadTurn;

            wheel = new GameObject("ToolWheel", typeof(RectTransform), typeof(ToolWheelGraphic)).GetComponent<RectTransform>();
            wheel.SetParent(root, false); wheel.localScale=Vector3.one*viewScale; wheel.sizeDelta = new Vector2(380, 380);
            wheelGraphic = wheel.GetComponent<ToolWheelGraphic>(); wheelGraphic.raycastTarget = false;
            Text("Forceps", wheel, new Vector2(118, 0), new Vector2(145, 90), 30).text = "Forceps";
            Text("Suture", wheel, new Vector2(-118, 0), new Vector2(145, 90), 30).text = "Suture";
            wheel.gameObject.SetActive(false);
            SelectFirst(); RefreshGuide();
        }

        void EnsureView(SiteView view)
        {
            if(view.Wound!=null) return;
                var site=view.Site; var layout=view.Layout;
                var art=Box("Wound "+site.Id,field,new Vector2(layout.X,layout.Y),new Vector2(35,layout.Length),new Color(0.48f,0.09f,0.12f)).rectTransform;
                art.localScale=Vector3.one*viewScale; art.localEulerAngles=new Vector3(0,0,layout.Angle);
                // The wound runs vertically in local space. The shard crosses it horizontally.
                var mask=new GameObject("ExposedFragment",typeof(RectTransform),typeof(Image),typeof(Mask)).GetComponent<RectTransform>();
                mask.GetComponent<Mask>().showMaskGraphic=false; mask.GetComponent<Image>().raycastTarget=false;
                mask.SetParent(art,false); mask.anchorMin=mask.anchorMax=new Vector2(0.5f,0.5f); mask.pivot=new Vector2(0,0.5f);
                mask.anchoredPosition=new Vector2(17.5f,0); mask.sizeDelta=new Vector2(360,240);
                var shard=Box("NativeShrapnel",mask,Vector2.zero,Vector2.one,Color.white);
                shard.sprite=nativeShard; shard.raycastTarget=false;
                var shardRect=shard.rectTransform;
                // Anchor to the mask's left edge so the concealed base stays at the cut.
                shardRect.anchorMin=shardRect.anchorMax=new Vector2(0,0.5f);
                float shardLength=layout.Length*0.84f;
                Vector2 shardSize=nativeShard.rect.size*(shardLength/nativeShard.rect.height);
                shardSize.x*=1.25f; // Broader silhouette; the long axis stays perpendicular to the cut.
                shardRect.sizeDelta=shardSize;
                shardRect.localEulerAngles=new Vector3(0,0,-90);
                var origin=new Vector2(shardLength*0.15f,0); shardRect.anchoredPosition=origin;
                var label=Text("SiteLabel",field,new Vector2(layout.X,layout.Y-layout.Length*viewScale/2-18),new Vector2(210,28),14);
                view.Wound=art; view.Fragment=shardRect; view.Mask=mask; view.FragmentOrigin=origin;
                view.Grip=17.5f+shardLength*0.47f; view.Pull=shardLength*0.35f+8; view.Label=label;
        }
        void SelectFirst()
        {
            var next=sites.Find(v=>!v.Site.Completed && v.Layout.Page==page) ?? sites.Find(v=>!v.Site.Completed);
            if(next==null) { Finish("All fragments on this limb treated."); return; }
            page=next.Layout.Page; SelectSite(next);
        }
        void SelectSite(SiteView view)
        {
            EnsureView(view);
            session?.Cancel();
            if(strokeRoot!=null) UnityEngine.Object.Destroy(strokeRoot.gameObject);
            foreach(var guide in activeGuides) if(guide!=null) {guide.SetActive(false);UnityEngine.Object.Destroy(guide);}
            activeGuides.Clear(); previewLine=null;
            selectedSite=view.Site; wound=view.Wound; fragment=view.Fragment; fragmentOrigin=view.FragmentOrigin;
            woundLength=view.Layout.Length; woundWidth=view.Layout.Width; gripPosition=view.Grip; pullDistance=view.Pull;
            session=new OperationSession(source.body,patientLimb,recovery,reduction,this);
            if(selectedSite.Extracted && !session.Extract()) throw new InvalidOperationException("Could not resume pending suture.");
            strokeRoot=new GameObject("SutureTrace",typeof(RectTransform)).GetComponent<RectTransform>(); strokeRoot.SetParent(wound,false);
            path=Box("ExtractionPath",wound,Vector2.zero,Vector2.one,new Color(0.2f,0.85f,0.65f,0.4f)).rectTransform;
            startMarker=Box("Start",wound,Vector2.zero,new Vector2(100,48),new Color(0.22f,0.45f,0.4f)).rectTransform;
            startLabel=Text("StartLabel",startMarker,Vector2.zero,new Vector2(110,60),28);
            endMarker=Box("End",wound,Vector2.zero,new Vector2(100,48),new Color(0.22f,0.45f,0.4f)).rectTransform;
            Text("EndLabel",endMarker,Vector2.zero,new Vector2(110,60),28).text="END";
            activeGuides.Add(path.gameObject); activeGuides.Add(startMarker.gameObject); activeGuides.Add(endMarker.gameObject);
            // Keep the exposed shard above the active guide, still clipped by its mask.
            view.Mask.SetAsLastSibling();
            forceps=!selectedSite.Extracted; suture.Reset(); readyForPress=false;
            PrepareGesture(); host.UpdateHandSprite(true); RefreshSites();
        }
        void RefreshSites()
        {
            foreach(var v in sites)
            {
                bool visible=!v.Site.Completed && v.Layout.Page==page;
                if(visible) EnsureView(v);
                if(v.Wound==null) continue;
                v.Wound.gameObject.SetActive(visible); v.Label.gameObject.SetActive(visible);
                v.Fragment.gameObject.SetActive(!v.Site.Extracted);
                v.Label.text=v.Site==selectedSite ? "•" : "";
            }
        }
        bool ChangeSite()
        {
            if(gesture.Active || suture.Active || wheelOpen) return false;
            if(Input.GetKeyDown(KeyCode.PageDown) || Input.GetKeyDown(KeyCode.PageUp))
            {
                var pages=sites.Where(v=>!v.Site.Completed).Select(v=>v.Layout.Page).Distinct().OrderBy(n=>n).ToList();
                int index=pages.IndexOf(page), direction=Input.GetKeyDown(KeyCode.PageDown)?1:-1;
                page=pages[(index+direction+pages.Count)%pages.Count];
                SelectFirst(); return true;
            }
            if(!host.handStartedClicking) return false;
            Vector2 point=field.InverseTransformPoint(host.handTransform.position);
            var target=sites.Where(v=>!v.Site.Completed && v.Layout.Page==page)
                .OrderBy(v=>(new Vector2(v.Layout.X,v.Layout.Y)-point).sqrMagnitude).FirstOrDefault();
            if(target!=null && (new Vector2(target.Layout.X,target.Layout.Y)-point).sqrMagnitude>110*110) target=null;
            if(target==null || target.Site==selectedSite) return false;
            SelectSite(target); return true;
        }

        public string InvalidReason()
        {
            if (Ended) return "Operation ended.";
            if (host == null || !ReferenceEquals(MinigameBase.main, host) || !ReferenceEquals(host.currentMinigame, this))
                return "Native minigame was closed or replaced.";
            if (patientLimb == null || source == null || !source.gameObject.activeInHierarchy || source.body != patientLimb.body)
                return "Health screen or patient changed.";
            if(board.Remaining!=patientLimb.shrapnel) return "Fragment count changed outside surgery; reopen to reconcile the board.";
            return session==null ? null : session.InvalidReason();
        }

        public override void Update(List<RaycastResult> hits)
        {
            if (Ended) return;
            UpdateFades();
            string invalid = InvalidReason();
            if (invalid != null) { Finish(invalid); return; }
            if (!wheelOpen && Input.GetMouseButtonDown(1) && !PointerInsideWindow())
            { Finish("Surgery closed by right-click outside the window."); return; }
            if (BlocksToolInput && Input.GetKeyDown(KeyBinds.GetBind("pause"))) { Finish("Surgery cancelled."); return; }
            if (HandleWheel()) { RefreshGuide(); return; }
            if(ChangeSite()) { RefreshGuide(); return; }
            if (!readyForPress)
            {
                if (!host.handClicking) readyForPress = true;
                RefreshGuide(); return;
            }
            Vector3 local = wound.InverseTransformPoint(host.handTransform.position);
            bool extraction = session.Procedure.Stage == FragmentStage.Extraction;
            float x=extraction ? local.y/woundWidth : local.x/woundWidth;
            float y=extraction ? 0.38f-(local.x-gripPosition)/woundLength : 0.5f-local.y/woundLength;
            if (host.handStartedClicking)
            {
                if (forceps != extraction) message = "Hold " + wheelKey + " to select " + (extraction ? "Forceps." : "Suture.");
                else if (extraction)
                { if (gesture.Begin(x,y)) grabOffset=fragment.position-host.handTransform.position; }
                else
                {
                    ClearTrace(); lastStroke = new Vector2(local.x, local.y);
                    if (!suture.Begin(x, y)) message = "Begin at START, then zigzag down across both sides.";
                }
            }
            bool drawing = extraction ? gesture.Active : suture.Active;
            if (drawing && (host.handClicking || host.handStoppedClicking))
            {

                if (host.handStoppedClicking)
                {
                    if (extraction)
                    {
                        if (!gesture.End(x, y)) Slip();
                        else if (!session.Extract()) { Finish("Wound changed; treatment cancelled."); return; }
                        else { int score=gesture.Quality; FadeShard(); selectedSite.Extracted=true; RefreshSites(); PrepareGesture(); PlayerCamera.main.DoAlert("Extraction: "+score+"/100",true); }
                    }
                    else if (!suture.End(x, y, out int quality)) Slip();
                    else if (!session.CompleteContinuousSuture()) { Finish("Wound changed; treatment cancelled."); return; }
                    else
                    {
                        if(!board.Complete(selectedSite.Id)) { Finish("Board state mismatch after treatment."); return; }
                        PlayerCamera.main.DoAlert("Suture quality: "+quality+"/100",true);
                        log("Treated site "+selectedSite.Id+" on board "+board.Seed+"; "+board.Remaining+" remaining.");
                        RefreshSites(); SelectFirst(); return;
                    }
                }
                else if (!(extraction ? gesture.Move(x, y) : suture.Move(x, y))) Slip();
                if (extraction && gesture.Active)
                {
                    fragment.GetComponentInParent<Mask>().enabled=!gesture.Loose;
                    if(gesture.Loose) fragment.position=host.handTransform.position+grabOffset;
                    else fragment.anchoredPosition=fragmentOrigin+Vector2.right*(gesture.Travel*woundLength);
                }
                else if (!extraction && suture.Active) PreviewThread(new Vector2(local.x,local.y));
            }
            RefreshGuide();
        }
        public override void PhysicsUpdate(float delta)
        {
            if (wheelOpen) { host.handPos = frozenHand; host.handVelocity = Vector2.zero; }
        }
        bool HandleWheel()
        {
            if (Input.GetKeyDown(wheelKey) && PointerInsideWindow())
            {
                wheelOpen = true; wheelScreenOrigin = Input.mousePosition; frozenHand = host.handPos;
                gesture.Reset(); ResetFragment(); suture.Reset(); ClearTrace(); readyForPress = false; host.ForceUngrab();
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
        bool PointerInsideWindow()
        {
            var canvas = root.GetComponentInParent<Canvas>();
            return RectTransformUtility.RectangleContainsScreenPoint(windowBounds, Input.mousePosition,
                canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera);
        }
        void FadeShard()
        {
            fragment.position=host.handTransform.position+grabOffset;
            fragment.GetComponentInParent<Mask>().enabled=false;
            var copy=UnityEngine.Object.Instantiate(fragment.gameObject,field,true);
            var image=copy.GetComponent<Image>(); image.raycastTarget=false;
            fading.Add(new FadingShard {Image=image});
        }
        void UpdateFades()
        {
            for(int i=fading.Count-1;i>=0;i--)
            {
                var shard=fading[i]; shard.Age+=Time.unscaledDeltaTime;
                if(shard.Image==null || shard.Age>=0.4f)
                {if(shard.Image!=null)UnityEngine.Object.Destroy(shard.Image.gameObject);fading.RemoveAt(i);continue;}
                var color=shard.Image.color;color.a=1-shard.Age/0.4f;shard.Image.color=color;
            }
        }
        void ResetFragment()
        {
            if (fragment != null) {fragment.anchoredPosition=fragmentOrigin;fragment.GetComponentInParent<Mask>().enabled=true;}
        }
        void ClearTrace()
        {
            if (strokeRoot == null) return;
            for (int i=strokeRoot.childCount-1;i>=0;i--)
            { var child=strokeRoot.GetChild(i).gameObject; child.SetActive(false); UnityEngine.Object.Destroy(child); }
            previewLine=null; lineCount=0;
        }
        void CommitThreadTurn(float x,float y)
        {
            Vector2 point=new Vector2(x*woundWidth,(0.5f-y)*woundLength);
            var line=Box("CommittedThread",strokeRoot,Vector2.zero,Vector2.one,new Color(0.9f,0.96f,0.92f)).rectTransform;
            SetThreadLine(line,lastStroke,point); lastStroke=point; lineCount++;
        }
        void PreviewThread(Vector2 point)
        {
            if (previewLine==null)
                previewLine=Box("LiveThread",strokeRoot,Vector2.zero,Vector2.one,new Color(0.9f,0.96f,0.92f,0.55f)).rectTransform;
            SetThreadLine(previewLine,lastStroke,point);
        }
        static void SetThreadLine(RectTransform line,Vector2 a,Vector2 b)
        {
            Vector2 delta=b-a; line.anchoredPosition=(a+b)*0.5f;
            line.sizeDelta=new Vector2(delta.magnitude,5);
            line.localEulerAngles=new Vector3(0,0,Mathf.Atan2(delta.y,delta.x)*Mathf.Rad2Deg);
            line.GetComponent<Image>().raycastTarget=false;
        }

        void Slip()
        {
            bool extraction=session.Procedure.Stage==FragmentStage.Extraction;
            gesture.Reset(); ResetFragment(); suture.Reset(); ClearTrace();
            if (!session.Slip()) { Finish("Wound changed; treatment cancelled."); return; }
            host.ForceUngrab();
            message = extraction ? "Fragment reset: -1 skin, up to +0.2 bleeding. Grip its exposed tip and retry."
                : "Suture reset. No damage or resource cost; release and try again.";
        }
        void PrepareGesture()
        {
            bool extraction = session.Procedure.Stage == FragmentStage.Extraction;
            gesture = new FragmentPull(2f,pullDistance/woundLength,0.005f);
            fragment.gameObject.SetActive(extraction); ResetFragment();
            // Gameplay feedback comes from the shard, thread and score, not debug targets.
            startMarker.gameObject.SetActive(false); endMarker.gameObject.SetActive(false); path.gameObject.SetActive(false);
        }
        public void RefreshGuide()
        {
            if (Ended || heading == null) return;
            bool extraction = session.Procedure.Stage == FragmentStage.Extraction;
            heading.text = patientLimb.fullName+" · "+board.Remaining+" wounds"+ (board.NextId>12 ? " · page "+(page+1)+" (PgUp/PgDn)" : "");
            host.guideText.text = "Hold " + wheelKey + " inside for tools. Right-click outside to exit. " + (forceps ? "Pull until loose, then release." : "Cross the wound three times, then release.");
        }
        public void Finish(string reason)
        {
            if (Ended) return;
            log(reason);
            log(message);
            OnEnded();
            if (host != null && ReferenceEquals(host.currentMinigame, this)) host.EndMinigame();
        }
        public void OnEnded()
        {
            if (Ended) return;
            Ended = true; Plugin.ProtectExitClick(); gesture?.Reset(); suture.Reset(); session?.Cancel();
            if (fade != null) { fade.enabled = false; UnityEngine.Object.Destroy(fade); }
            if (host != null && host.handTransform != null && scaleApplied) { host.handTransform.localScale = originalHandScale; scaleApplied = false; }
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

    [HarmonyPatch(typeof(MinigameBase), nameof(MinigameBase.UpdateHandSprite))]
    internal static class SurgeryHandScale
    {
        static void Postfix(MinigameBase __instance)
        {
            if (__instance.currentMinigame is NativeFragmentMinigame surgery && !surgery.Ended)
            {
                // Native code just set (+/-1,1,1). Scale its result, preserving the hand-slot mirror.
                // It resets on every call, so this multiplication cannot compound across frames.
                __instance.handTransform.localScale *= surgery.HandScale;
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
