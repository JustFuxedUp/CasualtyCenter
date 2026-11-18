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
        readonly SurgeryActions actions;
        readonly Vector3 origin;readonly int scene;
        readonly ScalpelTrace scalpel=new ScalpelTrace();readonly SutureStroke suture=new SutureStroke(2);
        RectTransform root,field,wheel,boneViewport;ToolWheelGraphic wheelGraphic;TextMeshProUGUI heading;
        readonly List<Image> dots=new List<Image>();
        IncisionPrep prep;
        SutureThread thread;
        SurgeryEffects effects;readonly List<RectTransform> bones=new List<RectTransform>();
        BloodPoolVisual poolVisual;
        BloodPools Pools => state.Pools[(int)displayed];
        WristFade fade;KnifeTipVisual knifeVisual;Vector3 originalScale;Vector2 wheelOrigin,frozen;
        bool wheelOpen,ready;int rotating=-1;float startPointer,startAngle,mistakeSound;FractureStage displayed;
        public bool Ended {get;private set;} public float HandScale=>handScale;
        public bool BlocksToolInput=>!Ended&&(wheelOpen||Input.GetKey(wheelKey)||Input.GetKeyUp(wheelKey)||Input.GetMouseButton(1)||Input.GetMouseButtonUp(1));
        SurgeryTool Tool=>Plugin.SelectedTool;
        public NativeFractureMinigame(MinigameBase host,WoundView source,Limb limb,float handScale,KeyCode wheelKey,Action<string> log)
        {this.host=host;this.source=source;this.limb=limb;this.handScale=handScale;this.wheelKey=wheelKey;this.log=log;body=source.body;state=FractureStore.Get(limb);origin=body.transform.position;scene=body.gameObject.scene.handle;actions=new SurgeryActions(limb,this,InvalidReason,ProcedureKind.Fracture);}
        public override HandSpriteType HandType()=>(Tool==SurgeryTool.Gel || Tool==SurgeryTool.Suction)?HandSpriteType.Grasp:Tool==SurgeryTool.Scalpel?HandSpriteType.Blade:Tool==SurgeryTool.Forceps?HandSpriteType.Tweezers:HandSpriteType.Point;
        public override string GuideLocaleString()=>"shrapnelMinigameGuide";
        public override bool NeedsItem()=>false;
        public override void Start()
        {
            originalScale=host.handTransform.localScale;fade=host.handTransform.gameObject.AddComponent<WristFade>();knifeVisual=host.handTransform.gameObject.AddComponent<KnifeTipVisual>();
            root=Box("FractureSurgery",host.minigameScreen,Vector2.zero,new Vector2(1000,1000),new Color(.025f,.05f,.11f)).rectTransform;
            root.SetSiblingIndex(0);host.spawnedMiniGame=root;
            heading=Text(root,new Vector2(0,440),new Vector2(940,80),24);
            wheelGraphic=ToolWheelGraphic.Create(root,host.guideText.font,.4f);
            wheel=wheelGraphic.rectTransform;
            suture.TurnCommitted+=ThreadTurn;BuildStage();effects=new SurgeryEffects(root,host.guideText.font,field);RefreshGuide();
        }
        void BuildStage()
        {
            effects?.Detach();
            if(field!=null){field.gameObject.SetActive(false);UnityEngine.Object.Destroy(field.gameObject);}dots.Clear();bones.Clear();
            field=Box("OperatingSection",root,new Vector2(0,5),new Vector2(900,700),new Color(.045f,.085f,.16f)).rectTransform;field.SetSiblingIndex(1);
            displayed=state.Stage;thread=new SutureThread(field);
            if(displayed==FractureStage.Incision)
            {
                Box("IncisionGuide",field,Vector2.zero,new Vector2(5,240),new Color(.3f,.18f,.22f));
                for(int i=0;i<9;i++){var dot=Box("CutDot",field,new Vector2(0,120-i*30),new Vector2(16,16),new Color(.9f,.65f,.15f));dot.gameObject.SetActive(!state.Cuts[i]);dots.Add(dot);}
            }
            else if(displayed==FractureStage.Alignment)
            {
                Box("OpenIncision",field,Vector2.zero,new Vector2(300,410),new Color(.25f,.06f,.09f));
                BuildBones();
            }
            else Box("ClosureWound",field,Vector2.zero,new Vector2(14,180),new Color(.48f,.09f,.12f));
            prep=new IncisionPrep(field,state.GelCoverage);
            actions.Prepare(Pools);
            poolVisual=new BloodPoolVisual(Pools,field,Vector2.zero,host,actions);
            prep.Refresh(state.Stage==FractureStage.Incision);effects?.Bind(field);Plugin.Feedback?.StopLoop(this);
            ready=false;rotating=-1;suture.Reset();scalpel.Reset();host.ForceUngrab();
        }
        void BuildBones()
        {
            field.SetAsFirstSibling();
            boneViewport = new GameObject("BoneViewport", typeof(RectTransform), typeof(RectMask2D))
                .GetComponent<RectTransform>();
            boneViewport.SetParent(field, false);
            boneViewport.position = root.position;
            boneViewport.sizeDelta = root.rect.size;

            // Each pivot is beyond the frame. Both inner tips land at the frame center when straight.
            float pivotDistance = boneViewport.rect.height / 2 + 32;
            for (int i = 0; i < 2; i++)
            {
                bool lower = i == 0;
                Vector2 pivot = new Vector2(0, lower ? -pivotDistance : pivotDistance);
                var target = Box("AlignmentTarget", boneViewport, pivot, new Vector2(18, pivotDistance),
                    new Color(.25f, .45f, .65f, .6f)).rectTransform;
                var bone = Box("BonePiece", boneViewport, pivot, new Vector2(56, pivotDistance),
                    new Color(.88f, .84f, .7f)).rectTransform;
                target.pivot = bone.pivot = new Vector2(.5f, lower ? 0 : 1);
                target.anchoredPosition = bone.anchoredPosition = pivot;
                bone.localEulerAngles = new Vector3(0, 0, state.Angles[i]);
                bones.Add(bone);
            }
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
            if(Ended)return;effects.Tick();string invalid=InvalidReason();if(invalid!=null){Finish(invalid);return;}
            actions.Advance(state,Time.deltaTime);
            if(state.Stage!=displayed)BuildStage();
            if(!wheelOpen && Input.GetMouseButtonDown(1) && !Inside()){Finish("Surgery closed; incision saved.");return;}
            if(BlocksToolInput && Input.GetKeyDown(KeyBinds.GetBind("pause"))){Finish("Surgery cancelled.");return;}
            if(Wheel()){effects.Stop();Plugin.Feedback?.StopLoop(this);RefreshGuide();return;}
            if(!ready){effects.Stop();Plugin.Feedback?.StopLoop(this);if(!host.handClicking)ready=true;RefreshGuide();return;}
            Vector3 contact=SurgeryContact.Point(host,Tool);
            Vector3 p=field.InverseTransformPoint(contact);
            actions.Observe(Pools,limb.bleedAmount > 0);
            poolVisual.Refresh();
            float gel=effects.Draw(Tool,host.handClicking,contact,Time.deltaTime);
            Plugin.Feedback?.HeldTool(this,Tool,host.handClicking);
            if (Pools.Blocking || Tool==SurgeryTool.Suction)
            {
                if (Tool==SurgeryTool.Suction && host.handClicking)
                {
                    poolVisual.Distance(contact,out int slot);
                    poolVisual.Suction(slot,Time.deltaTime);
                    poolVisual.Refresh();
                }
                if (suture.Active) ClearTrace();
                scalpel.Reset();suture.Reset();rotating=-1;
                RefreshGuide();
                return;
            }
            if(Tool==SurgeryTool.Gel && gel>0)ApplyGel(p,gel);
            if(Tool==SurgeryTool.Scalpel)Cut(p);else scalpel.Reset();
            if(Tool==SurgeryTool.Forceps && state.Stage==FractureStage.Alignment)Rotate(contact);
            if(Tool==SurgeryTool.Suture && state.Stage==FractureStage.Closure)Close(p);
            if(!Ended && state.Stage!=displayed)BuildStage();RefreshGuide();
        }
        void ApplyGel(Vector2 point,float ml)
        {
            prep.Apply(point,ml,(section,dose)=>actions.Disinfect(state,section,dose));
            prep.Refresh(state.Stage==FractureStage.Incision);
        }
        void Cut(Vector2 p)
        {
            bool productive=false;
            if(host.handClicking && state.Stage==FractureStage.Incision)
            {
                for(int i=0;i<9;i++)if(!state.Cuts[i] && scalpel.Touch(p.x,p.y,0,120-i*30,14))
                {if(actions.Cut(state,i)){dots[i].gameObject.SetActive(false);productive=true;}}

                if(state.Stage==FractureStage.Alignment)Result(state.CutScore);
            }
            float damage=scalpel.Step(host.handClicking,productive,Time.deltaTime);
            if(damage>0)
            {actions.Damage(damage);if(Time.unscaledTime>=mistakeSound){Plugin.Feedback?.Mistake();mistakeSound=Time.unscaledTime+.5f;}}
            if(host.handClicking)scalpel.Sample(p.x,p.y);
        }
        void Rotate(Vector3 contact)
        {
            Vector2 p = boneViewport.InverseTransformPoint(contact);
            if(host.handStartedClicking && boneViewport.rect.Contains(p))
                for(int i=0;i<bones.Count;i++)if(!state.Aligned[i])
                {
                    Vector3 local=bones[i].InverseTransformPoint(contact);
                    Rect boneRect=bones[i].rect;
                    if(Mathf.Abs(local.x)>boneRect.width/2+14 || local.y<boneRect.yMin-12 || local.y>boneRect.yMax+12)continue;
                    rotating=i;Vector2 d=p-bones[i].anchoredPosition;startPointer=Mathf.Atan2(d.y,d.x)*Mathf.Rad2Deg;startAngle=state.Angles[i];Plugin.Feedback?.Grab();break;
                }
            if(rotating<0)return;
            Vector2 delta=p-bones[rotating].anchoredPosition;
            if(delta.sqrMagnitude>100){actions.Rotate(state,rotating,startAngle+Mathf.DeltaAngle(startPointer,Mathf.Atan2(delta.y,delta.x)*Mathf.Rad2Deg));bones[rotating].localEulerAngles=new Vector3(0,0,state.Angles[rotating]);}
            if(host.handStoppedClicking)
            {
                int score=Mathf.RoundToInt(100*(1-FractureState.AngleError(state.Angles[rotating])/24));
                if(actions.Align(state,rotating)){bones[rotating].localEulerAngles=Vector3.zero;Result(score);}
                else {Plugin.Feedback?.Mistake();}
                rotating=-1;
            }
        }
        void Close(Vector2 p)
        {
            float x=p.x/22,y=.5f-p.y/180;
            if(host.handStartedClicking){thread.Begin(p);suture.Begin(x,y);}
            if(!suture.Active)return;
            if(host.handStoppedClicking)
            {
                if(!suture.End(x,y,out int score)){log("Fracture suture rejected on "+limb.fullName+"; crossings="+suture.Crossings+"; stage="+state.Stage);ClearTrace();Plugin.Feedback?.Mistake();return;}
                if(!actions.Close(state,score)){Finish("Patient changed before closure.");return;}
                thread.Clear();Result(score);Finish("Fracture operation closed; natural bone healing continues.");
            }
            else if(host.handClicking)
            {
                suture.Move(x,y);
                thread.Preview(p);
            }
        }
        void ThreadTurn(float x,float y)=>thread.Commit(new Vector2(x*22,(.5f-y)*180));
        void ClearTrace()=>thread.Clear();
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
            int selection=ToolWheelGraphic.Pick((Vector2)Input.mousePosition-wheelOrigin,wheelGraphic.ToolCount);wheelGraphic.Selected=selection;wheelGraphic.SetVerticesDirty();
            if(Input.GetKeyUp(wheelKey)){if(selection>=0)Plugin.SelectedTool=(SurgeryTool)selection;wheelOpen=false;wheel.gameObject.SetActive(false);host.UpdateHandSprite(true);}
            return true;
        }
        public override void PhysicsUpdate(float dt){if(wheelOpen){host.handPos=frozen;host.handVelocity=Vector2.zero;}}
        public void RefreshGuide()
        {
            if(Ended||heading==null)return;
            heading.text=limb.fullName+" / "+state.Stage+" / "+Tool+" / disinfected "+Mathf.RoundToInt(state.Coverage*100)+"%";
            host.guideText.text="Hold right mouse: tools. Right-click outside: exit. "+(Tool==SurgeryTool.Suction || Pools.Blocking?"Suction: hold over each red pool.":Tool==SurgeryTool.Gel?"Gel: coat the incision from end to end; blue-green marks show coverage.":state.Stage==FractureStage.Incision?"Scalpel: touch all gold dots while cutting.":state.Stage==FractureStage.Alignment?"Forceps: grip near a bone end, rotate to the blue guide, release.":"Suture: cross the incision three times, then release.");
        }
        public void Finish(string reason){if(Ended)return;log(reason);OnEnded();if(host!=null && ReferenceEquals(host.currentMinigame,this))host.EndMinigame();}
        public void OnEnded()
        {
            if(Ended)return;Ended=true;Plugin.Feedback?.StopLoop(this);Plugin.ProtectExitClick();scalpel.Reset();suture.Reset();
            if(knifeVisual!=null){knifeVisual.enabled=false;UnityEngine.Object.Destroy(knifeVisual);}
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
