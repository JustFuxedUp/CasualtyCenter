using System;
using System.Collections.Generic;
using System.Linq;
using CasualtiesSurgery.Procedures;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object=UnityEngine.Object;

namespace CasualtiesSurgery.Game
{
    // Experimental common view for the same production handlers in local loopback and
    // Together. All clinical state comes from detached replicas, never from view writes.
    internal sealed partial class NativeAuthorityMinigame : Minigame,ISurgery
    {
        readonly MinigameBase host;
        readonly WoundView source;
        readonly Body body;
        readonly Body operatorBody;
        readonly Vector3 operatorOrigin;
        readonly string patientTarget;
        readonly Limb limb;
        readonly ProcedureKind kind;
        readonly SurgeryPresentation presentation;
        readonly KeyCode wheelKey;
        float scale;
        readonly Action<string> log;
        readonly Vector3 origin;
        readonly int scene;
        RectTransform root,field,wheel,boneViewport;
        TextMeshProUGUI heading;
        ToolWheelGraphic wheelGraphic;
        SurgeryEffects effects;
        SurgeryPeerVisuals peerVisuals;
        long visualSequence;double nextVisual;
        bool visualHeld;SurgeryTool visualTool;
        readonly List<float> visualTrail=new List<float>();
        void UpdatePeerVisuals(bool allowInput)
        {
            peerVisuals?.Refresh(presentation.Visuals,presentation.SnapshotAge,Stage,kind==ProcedureKind.Fragment?page:0);
            bool held=allowInput && host.handClicking;
            bool changed=held!=visualHeld || Tool!=visualTool;
            if(!held || changed)visualTrail.Clear();
            Vector2 point=field.InverseTransformPoint(host.handTransform.position);
            if(held && (Tool==SurgeryTool.Suture || Tool==SurgeryTool.Scalpel || Tool==SurgeryTool.Gel))
            {
                int n=visualTrail.Count;
                if(n==0 || (point-new Vector2(visualTrail[n-2],visualTrail[n-1])).sqrMagnitude>=9)
                {if(n>=128)visualTrail.RemoveRange(0,2);visualTrail.Add(point.x);visualTrail.Add(point.y);}
            }
            visualHeld=held;visualTool=Tool;
            if(!changed && Time.unscaledTimeAsDouble<nextVisual)return;
            nextVisual=Time.unscaledTimeAsDouble+.1;
            var image=host.handTransform.GetComponent<Image>();int sprite=Array.IndexOf(host.handSprites,image.sprite);if(sprite<0)return;
            var rect=(RectTransform)host.handTransform;
            presentation.PublishVisual(new SurgeryVisualState(++visualSequence,(int)Tool,sprite,Stage,kind==ProcedureKind.Fragment?page:0,
                point.x,point.y,Mathf.DeltaAngle(field.eulerAngles.z,rect.eulerAngles.z),HandScale,rect.rect.width,rect.rect.height,rect.localScale.x<0,held,visualTrail.ToArray()));
        }
        IncisionPrep prep;
        SutureThread thread;
        WristFade fade;
        KnifeTipVisual knife;
        Vector3 originalScale;
        Vector2 frozen,wheelOrigin;
        bool handSetup,wheelOpen,ready;
        int displayed=-1,displayedCount=-1,page;
        float nextMistake;
        readonly List<Image> dots=new List<Image>();
        readonly List<RectTransform> bones=new List<RectTransform>();
        readonly List<Image> boneArtwork = new List<Image>();
        sealed class Part
        {
            public int Id;
            public RectTransform Wound,Fragment;
            public Vector2 FragmentOrigin;
            public TextMeshProUGUI Label;
            public BloodPoolVisual Pools;
            public SiteLayout Layout;
            public LacerationArt Laceration;
            public BruiseArt Bruise;
        }
        readonly List<Part> parts=new List<Part>();
        BloodPoolVisual outerPool;
        FractureState F=>presentation.Fracture;
        InternalBleedingState I=>presentation.Internal;
        FragmentBoard B=>presentation.Fragments;
        int Stage=>kind==ProcedureKind.Fragment?0:kind==ProcedureKind.Fracture?(int)F.Stage:(int)I.Stage;
        bool Complete=>kind==ProcedureKind.Fragment?B.Remaining==0:Stage==3;
        SurgeryTool Tool=>Plugin.SelectedTool;
        BloodPools Outer=>kind==ProcedureKind.Fracture?F.Pools[Math.Min(Stage,2)]:Stage==0?I.IncisionPools:I.ClosurePools;
        float[] Coverage=>kind==ProcedureKind.Fracture?F.GelCoverage:I.GelCoverage;
        public bool Ended {get;private set;}
        public float HandScale {get;}
        public bool BlocksToolInput=>!Ended&&(wheelOpen||Input.GetKey(wheelKey)||Input.GetKeyUp(wheelKey)||Input.GetMouseButton(1)||Input.GetMouseButtonUp(1));
        public NativeAuthorityMinigame(MinigameBase host,WoundView source,Limb limb,ProcedureKind kind,SurgeryClientSession client,
            float handScale,float scale,KeyCode wheelKey,Action<string> log,string patientTarget=null)
        {
            this.host=host;this.source=source;this.limb=limb;this.kind=kind;this.scale=scale;this.wheelKey=wheelKey;this.log=log;
            HandScale=handScale;body=source.body;origin=body.transform.position;scene=body.gameObject.scene.handle;
            operatorBody=PlayerCamera.main.body;operatorOrigin=operatorBody.transform.position;this.patientTarget=patientTarget;
            presentation=new SurgeryPresentation(client,kind,()=>Time.unscaledTimeAsDouble);
            presentation.Result+=Result;presentation.InputReset+=()=>ResetGesture(false);
        }
        public override HandSpriteType HandType()=>Tool==SurgeryTool.Scalpel?HandSpriteType.Blade:Tool==SurgeryTool.Forceps?HandSpriteType.Tweezers:Tool==SurgeryTool.Suture?HandSpriteType.Point:HandSpriteType.Grasp;
        public override string GuideLocaleString()=>"shrapnelMinigameGuide";
        public override bool NeedsItem()=>false;
        public override bool CanExit()=>true;
        public override void Start()
        {
            log("Surgery native Start entered: "+kind+" limb="+Array.IndexOf(body.limbs,limb));
            originalScale=host.handTransform.localScale;handSetup=true;
            fade=host.handTransform.gameObject.AddComponent<WristFade>();knife=host.handTransform.gameObject.AddComponent<KnifeTipVisual>();
            root=Box("AuthoritySurgery",host.minigameScreen,Vector2.zero,Vector2.one*1000,new Color(.025f,.05f,.11f)).rectTransform;
            root.SetSiblingIndex(0);host.spawnedMiniGame=root;
            heading=Label(root,new Vector2(0,440),new Vector2(940,80),24);
            wheelGraphic=ToolWheelGraphic.Create(root,host.guideText.font,kind==ProcedureKind.Fragment?scale:.4f);wheel=wheelGraphic.rectTransform;
            if(!presentation.Begin(Array.IndexOf(body.limbs,limb),patientTarget)) {Finish("Another surgery request is still settling; reopen shortly.");return;}
            log("Authority view opened: "+kind+" limb="+Array.IndexOf(body.limbs,limb));RefreshGuide();
        }
        public string InvalidReason()
        {
            if(Ended)return "Operation ended.";
            if(host==null || MinigameBase.main!=host || !ReferenceEquals(host.currentMinigame,this))return "Native operation closed.";
            if(source==null || !source.gameObject.activeInHierarchy || source.body!=body || PlayerCamera.main==null || PlayerCamera.main.body!=operatorBody)return "Patient or operator view changed.";
            if(operatorBody==null || !operatorBody.alive || !operatorBody.conscious || operatorBody.gameObject.scene.handle!=scene || Vector3.Distance(operatorOrigin,operatorBody.transform.position)>SurgeryTreatmentAccess.MovementLimit || !SurgeryTreatmentAccess.Nearby(operatorBody,body,SurgeryTreatmentAccess.Reach))return "Operator unavailable or moved away.";
            if(body==null || body.gameObject.scene.handle!=scene || Vector3.Distance(origin,body.transform.position)>SurgeryTreatmentAccess.MovementLimit)return "Patient moved or changed scene.";
            return presentation.Error ?? OperationSession.ClinicalIneligible(body,limb,false);
        }
        public override void Update(List<RaycastResult> hits)
        {
            string invalid=InvalidReason();if(invalid!=null){Finish(invalid);return;}
            presentation.Tick();
            if(presentation.Error!=null){Finish(presentation.Error);return;}
            if(!wheelOpen && Input.GetMouseButtonDown(1) && !Inside()){Finish("Surgery interrupted; host progress saved.");return;}
            if(BlocksToolInput && Input.GetKeyDown(KeyBinds.GetBind("pause"))){Finish("Surgery interrupted.");return;}
            if(!presentation.Ready){RefreshGuide();return;}
            if(Complete){Finish("Surgery completed.");return;}
            if (kind == ProcedureKind.InternalBleeding && displayed == (int)InternalStage.Repair && Stage == (int)InternalStage.Closure && UpdateWoundArt())
            {
                Plugin.Feedback?.StopLoop(this);
                return;
            }
            if(displayed!=Stage || (kind==ProcedureKind.Fragment && displayedCount!=B.NextId))BuildStage();
            effects.Tick();RefreshState();
            bool wheelHandled=HandleWheel();UpdatePeerVisuals(!wheelHandled && ready && Time.deltaTime>0);
            if(wheelHandled || !ready || Time.deltaTime<=0)
            {
                effects.Stop();Plugin.Feedback?.StopLoop(this);
                if(!wheelOpen && !host.handClicking)ready=true;
                RefreshGuide();return;
            }
            if(kind==ProcedureKind.Fragment && grip<0 && stroke==null && (Input.GetKeyDown(KeyCode.PageDown)||Input.GetKeyDown(KeyCode.PageUp)))
            {
                var pages=B.Sites.Where(s=>!s.Completed).Select(s=>s.Id/12).Distinct().OrderBy(p=>p).ToList();
                int current=Math.Max(0,pages.IndexOf(page));page=pages[(current+(Input.GetKeyDown(KeyCode.PageDown)?1:pages.Count-1))%pages.Count];BuildStage();return;
            }
            Vector3 contact=SurgeryContact.Point(host,Tool);Vector2 point=field.InverseTransformPoint(contact);
            effects.Draw(Tool,host.handClicking,contact,Time.deltaTime);Plugin.Feedback?.HeldTool(this,Tool,host.handClicking);
            InputStep(contact,point);RefreshGuide();
        }
        void BuildStage()
        {
            if(kind==ProcedureKind.Fragment)scale=presentation.ViewScale;
            ResetGesture(true);effects?.Detach();peerVisuals?.Dispose();visualTrail.Clear();visualHeld=false;
            if(field!=null){field.gameObject.SetActive(false);Object.Destroy(field.gameObject);}
            parts.Clear();bones.Clear();boneArtwork.Clear();dots.Clear();outerPool=null;prep=null;
            field=Box("OperatingField",root,kind==ProcedureKind.InternalBleeding?Vector2.zero:new Vector2(0,5),new Vector2(900,700),new Color(.045f,.085f,.16f)).rectTransform;
            field.SetSiblingIndex(1);peerVisuals=new SurgeryPeerVisuals(field,host);thread=new SutureThread(field);displayed=Stage;displayedCount=kind==ProcedureKind.Fragment?B.NextId:0;
            log("Authority stage: "+kind+" limb="+Array.IndexOf(body.limbs,limb)+" phase="+Stage+" sample="+presentation.Sample);
            if(kind==ProcedureKind.Fragment)BuildFragments();
            else if(Stage==1 && kind==ProcedureKind.Fracture)BuildBones();
            else if(Stage==1)BuildInternal();
            else
            {
                Box("Incision",field,Vector2.zero,new Vector2(Stage==0?5:14,kind==ProcedureKind.Fracture?(Stage==0?240:180):252),new Color(.51f,.1f,.14f));
                if(Stage==0)for(int n=0;n<9;n++)dots.Add(Box("CutDot",field,IncisionPrep.Section(n),Vector2.one*16,new Color(.9f,.65f,.15f)));
            }
            if(kind!=ProcedureKind.Fragment)
            {
                prep=new IncisionPrep(field,Coverage);
                if(kind==ProcedureKind.Fracture || Stage!=1)outerPool=new BloodPoolVisual(Outer,field,Vector2.zero,host,null);
            }
            if(effects==null)effects=new SurgeryEffects(root,host.guideText.font,field,kind!=ProcedureKind.InternalBleeding);else effects.Bind(field);
            Plugin.Feedback?.StopLoop(this);host.ForceUngrab();ready=false;RefreshState();
        }
        void BuildBones()
        {
            Box("OpenIncision",field,Vector2.zero,new Vector2(300,410),SurgeryArt.OpenField);field.SetAsFirstSibling();
            boneViewport=new GameObject("BoneViewport",typeof(RectTransform),typeof(RectMask2D)).GetComponent<RectTransform>();
            boneViewport.SetParent(field,false);boneViewport.position=root.position;boneViewport.sizeDelta=root.rect.size;
            for(int n=0;n<2;n++)
            {
                bool lower=n==0;var pivot=new Vector2(0,lower?-532:532);
                var target=Box("AlignmentTarget",boneViewport,pivot,new Vector2(18,532),new Color(.25f,.45f,.65f,.6f)).rectTransform;
                var bone=Box("BonePiece",boneViewport,pivot,new Vector2(56,532),new Color(.88f,.84f,.7f)).rectTransform;
                boneArtwork.Add(SurgeryArt.Bone(bone, !lower));
                target.pivot=bone.pivot=new Vector2(.5f,lower?0:1);target.anchoredPosition=bone.anchoredPosition=pivot;bones.Add(bone);
            }
        }
        void BuildInternal()
        {
            Box("OpenTorso",field,Vector2.zero,new Vector2(730,610),SurgeryArt.OpenField);
            for(int n=0;n<I.Wounds.Length;n++)
            {
                var w=I.Wounds[n];var pos=new Vector2(w.X,w.Y);
                var part=new Part {Id=n,Wound=Box(w.Laceration?"Laceration":"Bruise",field,pos,w.Laceration?new Vector2(64,108):Vector2.one*SurgeryArt.BruiseSize,Color.white).rectTransform};
                part.Wound.localEulerAngles=new Vector3(0,0,w.Angle);
                var image = part.Wound.GetComponent<Image>();
                if (w.Laceration)
                {
                    part.Laceration = image.gameObject.AddComponent<LacerationArt>();
                    part.Laceration.Setup(image, w.Phase);
                }
                else part.Bruise = new BruiseArt(image, w.Phase);
                part.Pools=new BloodPoolVisual(w.Pools,field,pos,host,null);part.Label=Label(field,pos+Vector2.down*80,new Vector2(210,32),18);parts.Add(part);
            }
        }
        void BuildFragments()
        {
            if(!B.Sites.Any(s=>!s.Completed && s.Id/12==page))page=B.Sites.First(s=>!s.Completed).Id/12;
            var sprite = SurgeryArt.Get("fragment1");
            foreach(var site in B.Sites.Where(s=>!s.Completed && s.Id/12==page))
            {
                var layout=B.Layout(site.Id);var pos=new Vector2(layout.X,layout.Y);
                var part=new Part {Id=site.Id,Layout=layout,Wound=Box("Wound "+site.Id,field,pos,new Vector2(35,layout.Length),new Color(.48f,.09f,.12f)).rectTransform};
                part.Wound.GetComponent<Image>().sprite = SurgeryArt.Get("smallwound");
                part.Wound.localScale=Vector3.one*scale;part.Wound.localEulerAngles=new Vector3(0,0,layout.Angle);
                var mask=new GameObject("ExposedFragment",typeof(RectTransform),typeof(Image),typeof(Mask)).GetComponent<RectTransform>();mask.SetParent(part.Wound,false);
                mask.GetComponent<Mask>().showMaskGraphic=false;mask.GetComponent<Image>().raycastTarget=false;
                mask.pivot=new Vector2(0,.5f);mask.anchoredPosition=Vector2.zero;mask.sizeDelta=new Vector2(360,240);
                var image=Box("FragmentArtwork",mask,Vector2.zero,Vector2.one,Color.white);image.sprite=sprite;part.Fragment=image.rectTransform;
                part.Fragment.anchorMin=part.Fragment.anchorMax=new Vector2(0,.5f);float length=layout.Length*.84f;
                part.Fragment.sizeDelta = new Vector2(length * .28f, length);
                part.Fragment.localEulerAngles = new Vector3(0, 0, -90);
                part.FragmentOrigin=new Vector2(length*.15f,0);part.Fragment.anchoredPosition=part.FragmentOrigin;
                part.Pools=new BloodPoolVisual(site.Pools,field,pos,host,null);part.Label=Label(field,pos+Vector2.down*60,new Vector2(160,26),16);parts.Add(part);
            }
        }
        bool UpdateWoundArt()
        {
            bool fading = false;
            foreach (var part in parts)
            {
                var wound = I.Wounds[part.Id];
                if (part.Laceration != null)
                    fading |= part.Laceration.Refresh(wound.Phase, grip == part.Id ? (float?)LacerationDrag.Width(gripLocal.x) : null);
                else
                    fading |= part.Bruise.Refresh(wound.Phase, wound.GelMl);
            }
            return fading;
        }

        void RefreshState()
        {
            if (kind == ProcedureKind.InternalBleeding && Stage == (int)InternalStage.Repair) UpdateWoundArt();
            if(prep!=null){prep.Bind(Coverage);prep.Refresh(Stage==0);}
            if(outerPool!=null){outerPool.Bind(Outer);outerPool.Refresh();}
            for(int n=0;n<dots.Count;n++)dots[n].gameObject.SetActive(!(kind==ProcedureKind.Fracture?F.Cuts:I.Cuts)[n]);
            for(int n=0;n<bones.Count;n++)
            {
                if(grip!=n)bones[n].localEulerAngles=new Vector3(0,0,F.Angles[n]);
                boneArtwork[n].color=presentation.ResourceBusy("bone:"+n)?new Color(.55f,.65f,.8f):Color.white;
            }
            foreach(var part in parts)
            {
                if(kind==ProcedureKind.Fragment)
                {
                    var site=B.Sites.Find(s=>s.Id==part.Id);bool visible=site!=null && !site.Completed;
                    part.Wound.gameObject.SetActive(visible);part.Pools.Bind(site.Pools);part.Pools.Refresh(visible);
                    part.Label.text=presentation.ResourceBusy("wound:"+part.Id)?"In use by surgeon":"";
                    bool preview=presentation.PreviewingExtraction(part.Id);
                    float opacity=presentation.ExtractionOpacity(part.Id);
                    part.Fragment.gameObject.SetActive(visible && (preview?opacity>0:!site.Extracted));
                    part.Fragment.GetComponent<Image>().color=new Color(1,1,1,opacity);
                    part.Wound.GetComponent<Image>().color=SurgeryArt.FragmentWound(site.GelMl);
                    if(grip!=part.Id && !preview){part.Fragment.anchoredPosition=part.FragmentOrigin;part.Fragment.GetComponent<Image>().maskable=true;}
                }
                else
                {
                    var w=I.Wounds[part.Id];bool repaired=w.Phase==InternalWoundPhase.Repaired;
                    part.Pools.Bind(w.Pools);part.Pools.Refresh();
                    part.Label.text=presentation.ResourceBusy("wound:"+part.Id)?"In use by surgeon":repaired?(w.Laceration?"Stitched":"Healed"):w.Phase==InternalWoundPhase.Pinched?"Suture · "+Mathf.CeilToInt(Mathf.Max(0,w.Seconds-(float)presentation.SnapshotAge))+"s":w.Laceration?"Laceration":"Bruise";
                }
            }
            if(kind==ProcedureKind.Fragment && parts.All(p=>!p.Wound.gameObject.activeSelf) && B.Remaining>0)BuildStage();
        }
        void Result(string action,CommandReceipt receipt)
        {
            if(receipt.Status==CommandStatus.Rejected || receipt.Status==CommandStatus.StaleRevision || receipt.Status==CommandStatus.OutOfOrder)
                log("Authority input reset: "+action+" "+receipt.Status+" revision="+receipt.Revision);
            var result=receipt.Feedback;
            if(result==null || Ended || root==null)return;
            Vector3 position=field!=null?field.TransformPoint(new Vector3(0,160,0)):root.position;
            var part=parts.Find(p=>p.Id==result.Target);if(part!=null)position=part.Wound.position;
            if(action=="pool.suction" && field!=null)
            {
                Vector2 center=Vector2.zero;
                if(part!=null)center=part.Wound.anchoredPosition;
                position=field.TransformPoint(center+(result.Slot==1?new Vector2(37,-25):Vector2.zero));
            }
            if(result.Rating==TreatmentRating.Good)Plugin.Feedback?.Good(position,host.guideText.font,root.GetComponentInParent<Canvas>());
            else if(result.Rating==TreatmentRating.Scored)Plugin.Feedback?.Result(result.Quality,position,host.guideText.font,root.GetComponentInParent<Canvas>());
            else if(Time.unscaledTime>=nextMistake){Plugin.Feedback?.Mistake();nextMistake=Time.unscaledTime+.5f;}
            log("Authority result: "+action+" "+receipt.Status+" revision="+receipt.Revision+" rating="+result.Rating+" target="+result.Target+" slot="+result.Slot);
        }
        bool Inside(){var c=root.GetComponentInParent<Canvas>();return RectTransformUtility.RectangleContainsScreenPoint(root,Input.mousePosition,c.renderMode==RenderMode.ScreenSpaceOverlay?null:c.worldCamera);}
        bool HandleWheel()
        {
            if(Input.GetKeyDown(wheelKey)&&Inside())
            {
                if(kind==ProcedureKind.Fragment && Tool==SurgeryTool.Forceps && host.handStoppedClicking && grip>=0)
                    Extract(SurgeryContact.Point(host,Tool));
                ResetGesture(true,true);wheelOpen=true;wheelOrigin=Input.mousePosition;frozen=host.handPos;host.ForceUngrab();
                var c=root.GetComponentInParent<Canvas>();RectTransformUtility.ScreenPointToLocalPointInRectangle(root,wheelOrigin,c.renderMode==RenderMode.ScreenSpaceOverlay?null:c.worldCamera,out var point);
                wheel.localPosition=point;wheel.SetAsLastSibling();wheel.gameObject.SetActive(true);
            }
            if(!wheelOpen)return false;
            int pick=ToolWheelGraphic.Pick((Vector2)Input.mousePosition-wheelOrigin,wheelGraphic.ToolCount);wheelGraphic.Selected=pick;wheelGraphic.SetVerticesDirty();
            if(Input.GetKeyUp(wheelKey)){if(pick>=0)Plugin.SelectedTool=(SurgeryTool)pick;wheelOpen=false;wheel.gameObject.SetActive(false);host.UpdateHandSprite(true);}
            return true;
        }
        public override void PhysicsUpdate(float dt){if(wheelOpen){host.handPos=frozen;host.handVelocity=Vector2.zero;}}
        public void RefreshGuide()
        {
            if(heading==null || Ended)return;
            if(!presentation.Ready){heading.text="Connecting surgery…";host.guideText.text="Waiting for the host. Right-click outside to cancel.";return;}
            heading.text=limb.fullName+" / "+(kind==ProcedureKind.Fragment?B.Remaining+" wounds / page "+(page+1):kind==ProcedureKind.Fracture?F.Stage.ToString():I.Stage.ToString())+" / "+Tool+(presentation.Participants>1?" / "+presentation.Participants+" surgeons":"");
            host.guideText.text="Hold right mouse: tools. Right-click outside: exit. "+(Tool==SurgeryTool.Suction?"Hold over each blood pool.":Tool==SurgeryTool.Gel?"Coat the incision or hold over a bruise/extracted wound.":Tool==SurgeryTool.Suture?"Cross the wound three times, then release.":Tool==SurgeryTool.Scalpel?"Touch the gold dots while cutting.":kind==ProcedureKind.Fracture?"Grip a bone, align to the blue guide and release.":kind==ProcedureKind.InternalBleeding?"Grip a long edge and drag into the center.":"Pull a shard until loose, then release. PgUp/PgDn: pages.");
        }
        public void Finish(string reason){if(Ended)return;bool refused=presentation.Error!=null;log(reason);OnEnded();if(host!=null && ReferenceEquals(host.currentMinigame,this))host.EndMinigame();if(refused)Plugin.EntryNotice(source,limb,reason);}
        public void OnEnded()
        {
            if(!Ended)log("Surgery view cleanup: "+kind+" limb="+Array.IndexOf(body.limbs,limb));
            if(Ended)return;Ended=true;peerVisuals?.Dispose();ResetGesture(false);presentation.Dispose();Plugin.Feedback?.StopLoop(this);Plugin.ProtectExitClick();
            if(knife!=null){knife.enabled=false;Object.Destroy(knife);}if(fade!=null){fade.enabled=false;Object.Destroy(fade);}
            if(handSetup && host!=null && host.handTransform!=null)host.handTransform.localScale=originalScale;
            if(root!=null && (host==null || !ReferenceEquals(host.currentMinigame,this))){root.gameObject.SetActive(false);Object.Destroy(root.gameObject);}
        }
        static Image Box(string name,Transform parent,Vector2 point,Vector2 size,Color color)
        {
            var go=new GameObject(name,typeof(RectTransform),typeof(Image));var r=(RectTransform)go.transform;r.SetParent(parent,false);r.sizeDelta=size;r.anchoredPosition=point;
            var image=go.GetComponent<Image>();image.color=color;image.raycastTarget=false;return image;
        }
        TextMeshProUGUI Label(Transform parent,Vector2 point,Vector2 size,float font)
        {
            var go=new GameObject("Label",typeof(RectTransform),typeof(TextMeshProUGUI));var r=(RectTransform)go.transform;r.SetParent(parent,false);r.sizeDelta=size;r.anchoredPosition=point;
            var label=go.GetComponent<TextMeshProUGUI>();label.font=host.guideText.font;label.fontSize=font;label.alignment=TextAlignmentOptions.Center;label.color=Color.white;label.raycastTarget=false;return label;
        }
    }
}
