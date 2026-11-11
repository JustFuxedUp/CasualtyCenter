using System;
using System.Collections.Generic;
using CasualtiesSurgery.Procedures;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
namespace CasualtiesSurgery.Game
{
    // Placeholder art; the native host supplies hand motion, grip and exit handling.
    internal sealed class NativeFractureMinigame : Minigame,ISurgery
    {
        readonly MinigameBase host;readonly WoundView source;readonly Limb limb;readonly Body body;
        readonly FractureState state;readonly float handScale;readonly KeyCode wheelKey;readonly Action<string> log;
        readonly Vector3 origin;readonly int scene;
        readonly ScalpelTrace scalpel=new ScalpelTrace();readonly SutureStroke suture=new SutureStroke(2);
        RectTransform root,field,wheel,trace,preview;ToolWheelGraphic wheelGraphic;TextMeshProUGUI heading;
        readonly List<Image> dots=new List<Image>();readonly List<RectTransform> bones=new List<RectTransform>();
        WristFade fade;Vector3 originalScale;Vector2 wheelOrigin,frozen,lastThread;
        bool wheelOpen,ready;int rotating=-1;float startPointer,startAngle,mistakeSound;FractureStage displayed;
        public bool Ended {get;private set;} public float HandScale=>handScale;
        public bool BlocksToolInput=>!Ended&&(wheelOpen||Input.GetKey(wheelKey)||Input.GetKeyUp(wheelKey)||Input.GetMouseButton(1)||Input.GetMouseButtonUp(1));
        SurgeryTool Tool=>Plugin.SelectedTool;
        public NativeFractureMinigame(MinigameBase host,WoundView source,Limb limb,float handScale,KeyCode wheelKey,Action<string> log)
        {this.host=host;this.source=source;this.limb=limb;this.handScale=handScale;this.wheelKey=wheelKey;this.log=log;body=source.body;state=FractureStore.Get(limb);origin=body.transform.position;scene=body.gameObject.scene.handle;}
        public override HandSpriteType HandType()=>Tool==SurgeryTool.Scalpel?HandSpriteType.Blade:Tool==SurgeryTool.Forceps?HandSpriteType.Tweezers:HandSpriteType.Point;
        public override string GuideLocaleString()=>"shrapnelMinigameGuide";
        public override bool NeedsItem()=>false;
        public override void Start()
        {
            originalScale=host.handTransform.localScale;fade=host.handTransform.gameObject.AddComponent<WristFade>();
            root=Box("FractureSurgery",host.minigameScreen,Vector2.zero,new Vector2(1000,1000),new Color(.025f,.05f,.11f)).rectTransform;
            root.SetSiblingIndex(0);host.spawnedMiniGame=root;
            heading=Text(root,new Vector2(0,440),new Vector2(940,80),24);
            wheel=new GameObject("Tools",typeof(RectTransform),typeof(ToolWheelGraphic)).GetComponent<RectTransform>();wheel.SetParent(root,false);wheel.sizeDelta=new Vector2(380,380);wheel.localScale=Vector3.one*.4f;
            wheelGraphic=wheel.GetComponent<ToolWheelGraphic>();wheelGraphic.ToolCount=3;
            string[] names={"Forceps","Suture","Scalpel"};for(int i=0;i<3;i++){float a=i*Mathf.PI*2/3;Text(wheel,new Vector2(Mathf.Cos(a),Mathf.Sin(a))*120,new Vector2(145,70),25).text=names[i];}
            wheel.gameObject.SetActive(false);suture.TurnCommitted+=ThreadTurn;BuildStage();RefreshGuide();
        }
        void BuildStage()
        {
            if(field!=null){field.gameObject.SetActive(false);UnityEngine.Object.Destroy(field.gameObject);}dots.Clear();bones.Clear();
            field=Box("OperatingSection",root,new Vector2(0,5),new Vector2(900,700),new Color(.045f,.085f,.16f)).rectTransform;field.SetSiblingIndex(1);
            displayed=state.Stage;trace=new GameObject("Thread",typeof(RectTransform)).GetComponent<RectTransform>();trace.SetParent(field,false);
            if(displayed==FractureStage.Incision)
            {
                Box("IncisionGuide",field,Vector2.zero,new Vector2(5,240),new Color(.3f,.18f,.22f));
                for(int i=0;i<9;i++){var dot=Box("CutDot",field,new Vector2(0,120-i*30),new Vector2(16,16),state.Cuts[i]?new Color(.5f,.1f,.12f):new Color(.9f,.65f,.15f));dots.Add(dot);}
            }
            else if(displayed==FractureStage.Alignment)
            {
                Box("OpenIncision",field,Vector2.zero,new Vector2(250,300),new Color(.25f,.06f,.09f));
                for(int i=0;i<2;i++)
                {
                    Vector2 pos=new Vector2(i==0?-90:90,0);
                    Box("AlignmentTarget",field,pos,new Vector2(18,180),new Color(.25f,.45f,.65f,.6f));
                    var bone=Box("BonePiece",field,pos,new Vector2(24,180),new Color(.88f,.84f,.7f)).rectTransform;
                    bone.localEulerAngles=new Vector3(0,0,state.Angles[i]);bones.Add(bone);
                }
            }
            else Box("ClosureWound",field,Vector2.zero,new Vector2(14,180),new Color(.48f,.09f,.12f));
            ready=false;rotating=-1;suture.Reset();scalpel.Reset();host.ForceUngrab();
        }
        public string InvalidReason()
        {
            if(Ended)return "Operation ended.";
            if(host==null || !ReferenceEquals(host.currentMinigame,this))return "Native operation closed.";
            if(source==null || !source.gameObject.activeInHierarchy || source.body!=body)return "Patient view changed.";
            if(body.gameObject.scene.handle!=scene || Vector3.Distance(origin,body.transform.position)>1.5f)return "Patient moved or changed scene.";
            return FractureStore.Ineligible(body,limb,this);
        }
        public override void Update(List<RaycastResult> hits)
        {
            if(Ended)return;string invalid=InvalidReason();if(invalid!=null){Finish(invalid);return;}
            if(state.Started && state.Stage==FractureStage.Incision)state.CutSeconds+=Time.deltaTime;
            if(!limb.broken && state.Started && state.Stage==FractureStage.Alignment){state.Stage=FractureStage.Closure;BuildStage();}
            if(!wheelOpen && Input.GetMouseButtonDown(1) && !Inside()){Finish("Surgery closed; incision saved.");return;}
            if(BlocksToolInput && Input.GetKeyDown(KeyBinds.GetBind("pause"))){Finish("Surgery cancelled.");return;}
            if(Wheel()){RefreshGuide();return;}
            if(!ready){if(!host.handClicking)ready=true;RefreshGuide();return;}
            Vector3 p=field.InverseTransformPoint(host.handTransform.position);
            if(Tool==SurgeryTool.Scalpel)Cut(p);else scalpel.Reset();
            if(Tool==SurgeryTool.Forceps && state.Stage==FractureStage.Alignment)Rotate(p);
            if(Tool==SurgeryTool.Suture && state.Stage==FractureStage.Closure)Close(p);
            if(!Ended && state.Stage!=displayed)BuildStage();RefreshGuide();
        }
        void Cut(Vector2 p)
        {
            bool productive=false;
            if(host.handClicking && state.Stage==FractureStage.Incision)
            {
                for(int i=0;i<9;i++)if(!state.Cuts[i] && scalpel.Touch(p.x,p.y,0,120-i*30,14))
                {limb.skinHealth-=state.Cut(i,limb.skinHealth);dots[i].color=new Color(.5f,.1f,.12f);productive=true;}
                if(productive)Plugin.Feedback?.Cut();
                if(state.Stage==FractureStage.Alignment)Result(state.CutScore);
            }
            float damage=scalpel.Step(host.handClicking,productive,Time.deltaTime);
            if(damage>0)
            {FractureStore.ScalpelDamage(limb,damage);if(Time.unscaledTime>=mistakeSound){Plugin.Feedback?.Mistake();mistakeSound=Time.unscaledTime+.5f;}}
            if(host.handClicking)scalpel.Sample(p.x,p.y);
        }
        void Rotate(Vector2 p)
        {
            if(host.handStartedClicking)
                for(int i=0;i<bones.Count;i++)if(!state.Aligned[i])
                {
                    Vector3 local=bones[i].InverseTransformPoint(host.handTransform.position);
                    if(Mathf.Abs(local.x)>26 || Mathf.Abs(local.y)>100)continue;
                    rotating=i;Vector2 d=p-bones[i].anchoredPosition;startPointer=Mathf.Atan2(d.y,d.x)*Mathf.Rad2Deg;startAngle=state.Angles[i];Plugin.Feedback?.Grab();break;
                }
            if(rotating<0)return;
            Vector2 delta=p-bones[rotating].anchoredPosition;
            if(delta.sqrMagnitude>100){state.Angles[rotating]=startAngle+Mathf.DeltaAngle(startPointer,Mathf.Atan2(delta.y,delta.x)*Mathf.Rad2Deg);bones[rotating].localEulerAngles=new Vector3(0,0,state.Angles[rotating]);}
            if(host.handStoppedClicking)
            {
                int score=Mathf.RoundToInt(100*(1-FractureState.AngleError(state.Angles[rotating])/24));
                if(state.Align(rotating)){bones[rotating].localEulerAngles=Vector3.zero;Result(score);}
                else {FractureStore.AlignmentMistake(limb);Plugin.Feedback?.Mistake();}
                rotating=-1;
            }
        }
        void Close(Vector2 p)
        {
            float x=p.x/22,y=.5f-p.y/180;
            if(host.handStartedClicking){ClearTrace();lastThread=p;suture.Begin(x,y);}
            if(!suture.Active)return;
            if(host.handStoppedClicking)
            {
                if(!suture.End(x,y,out int score)){ClearTrace();Plugin.Feedback?.Mistake();return;}
                if(!FractureStore.TryClose(limb,score,this)){Finish("Patient changed before closure.");return;}
                Result(score);Finish("Fracture operation closed; natural bone healing continues.");
            }
            else if(host.handClicking)
            {
                suture.Move(x,y);
                if(preview==null)preview=Box("LiveThread",trace,Vector2.zero,Vector2.one,new Color(1,1,1,.5f)).rectTransform;
                Vector2 delta=p-lastThread;preview.anchoredPosition=(p+lastThread)*.5f;preview.sizeDelta=new Vector2(delta.magnitude,2);preview.localEulerAngles=new Vector3(0,0,Mathf.Atan2(delta.y,delta.x)*Mathf.Rad2Deg);
            }
        }
        void ThreadTurn(float x,float y)
        {
            Vector2 p=new Vector2(x*22,(.5f-y)*180),d=p-lastThread;
            var line=Box("Stitch",trace,(p+lastThread)*.5f,new Vector2(d.magnitude,2),Color.white).rectTransform;
            line.localEulerAngles=new Vector3(0,0,Mathf.Atan2(d.y,d.x)*Mathf.Rad2Deg);lastThread=p;Plugin.Feedback?.Stitch();
        }
        void ClearTrace(){preview=null;for(int i=trace.childCount-1;i>=0;i--)UnityEngine.Object.Destroy(trace.GetChild(i).gameObject);}
        void Result(int score)=>Plugin.Feedback?.Result(score,field.TransformPoint(new Vector3(0,160,0)),host.guideText.font,root.GetComponentInParent<Canvas>());
        bool Inside(){var c=root.GetComponentInParent<Canvas>();return RectTransformUtility.RectangleContainsScreenPoint(root,Input.mousePosition,c.renderMode==RenderMode.ScreenSpaceOverlay?null:c.worldCamera);}
        bool Wheel()
        {
            if(Input.GetKeyDown(wheelKey)&&Inside())
            {
                wheelOpen=true;wheelOrigin=Input.mousePosition;frozen=host.handPos;rotating=-1;suture.Reset();scalpel.Reset();ClearTrace();ready=false;host.ForceUngrab();
                var c=root.GetComponentInParent<Canvas>();RectTransformUtility.ScreenPointToLocalPointInRectangle(root,wheelOrigin,c.renderMode==RenderMode.ScreenSpaceOverlay?null:c.worldCamera,out var point);
                wheel.localPosition=point;wheel.SetAsLastSibling();wheel.gameObject.SetActive(true);
            }
            if(!wheelOpen)return false;
            int selection=ToolWheelGraphic.Pick((Vector2)Input.mousePosition-wheelOrigin,3);wheelGraphic.Selected=selection;wheelGraphic.SetVerticesDirty();
            if(Input.GetKeyUp(wheelKey)){if(selection>=0)Plugin.SelectedTool=(SurgeryTool)selection;wheelOpen=false;wheel.gameObject.SetActive(false);host.UpdateHandSprite(true);}
            return true;
        }
        public override void PhysicsUpdate(float dt){if(wheelOpen){host.handPos=frozen;host.handVelocity=Vector2.zero;}}
        public void RefreshGuide()
        {
            if(Ended||heading==null)return;
            heading.text=limb.fullName+" / "+state.Stage+" / "+Tool;
            host.guideText.text="Hold right mouse: tools. Right-click outside: exit. "+(state.Stage==FractureStage.Incision?"Scalpel: touch all gold dots while cutting.":state.Stage==FractureStage.Alignment?"Forceps: grip near a bone end, rotate to the blue guide, release.":"Suture: cross the incision three times, then release.");
        }
        public void Finish(string reason){if(Ended)return;log(reason);OnEnded();if(host!=null && ReferenceEquals(host.currentMinigame,this))host.EndMinigame();}
        public void OnEnded()
        {
            if(Ended)return;Ended=true;Plugin.ProtectExitClick();scalpel.Reset();suture.Reset();
            if(fade!=null){fade.enabled=false;UnityEngine.Object.Destroy(fade);}if(host!=null)host.handTransform.localScale=originalScale;
            if(root!=null && (host==null || !ReferenceEquals(host.currentMinigame,this))){root.gameObject.SetActive(false);UnityEngine.Object.Destroy(root.gameObject);}
        }
        static Image Box(string name,Transform parent,Vector2 pos,Vector2 size,Color color)
        {
            var go=new GameObject(name,typeof(RectTransform),typeof(Image));var r=(RectTransform)go.transform;r.SetParent(parent,false);r.sizeDelta=size;r.anchoredPosition=pos;
            var image=go.GetComponent<Image>();image.color=color;image.raycastTarget=false;return image;
        }
        TextMeshProUGUI Text(Transform parent,Vector2 pos,Vector2 size,float fontSize)
        {
            var go=new GameObject("Label",typeof(RectTransform),typeof(TextMeshProUGUI));var r=(RectTransform)go.transform;r.SetParent(parent,false);r.sizeDelta=size;r.anchoredPosition=pos;
            var t=go.GetComponent<TextMeshProUGUI>();t.font=host.guideText.font;t.fontSize=fontSize;t.alignment=TextAlignmentOptions.Center;t.color=Color.white;t.raycastTarget=false;return t;
        }
    }
}
