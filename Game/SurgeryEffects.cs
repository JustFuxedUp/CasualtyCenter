using System.Collections.Generic;
using CasualtiesSurgery.Procedures;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
namespace CasualtiesSurgery.Game
{
    internal static class SurgeryContact
    {
        // Native hand physics remain the sole interaction point for every tool.
        public static Vector3 Point(MinigameBase host,SurgeryTool tool)=>host.handTransform.position;
    }
    // Applied after WristFade: move rendered vertices, never the physics/input transform.
    public sealed class KnifeTipVisual : BaseMeshEffect
    {
        public override void ModifyMesh(VertexHelper mesh)
        {
            if(!IsActive() || Plugin.SelectedTool!=SurgeryTool.Scalpel ||
                !(MinigameBase.main?.currentMinigame is ISurgery surgery) || surgery.Ended)return;
            var image=graphic as Image;
            if(image==null || image.sprite==null || !image.sprite.name.StartsWith("handSharp"))return;
            var rect=image.GetPixelAdjustedRect();
            if(image.preserveAspect)
            {
                float ratio=image.sprite.rect.width/image.sprite.rect.height;
                if(rect.width/rect.height>ratio){float width=rect.height*ratio;rect.x+=(rect.width-width)*image.rectTransform.pivot.x;rect.width=width;}
                else {float height=rect.width/ratio;rect.y+=(rect.height-height)*image.rectTransform.pivot.y;rect.height=height;}
            }
            // Annotated image: 210 x 630, red marker centered at (62,1) from top left.
            // Native sprite: 256 x 630; exported texture has a 31.07612 px left trim.
            const float tipU=(31.07612f+209.88097f*(62f/210f))/256f;
            const float tipV=1f-1f/630f;
            Vector3 offset=new Vector3(rect.xMin+rect.width*tipU,rect.yMin+rect.height*tipV,0);
            var vertex=new UIVertex();
            for(int i=0;i<mesh.currentVertCount;i++){mesh.PopulateUIVertex(ref vertex,i);vertex.position-=offset;mesh.SetUIVertex(vertex,i);}
        }
    }

    internal sealed class SurgeryEffects
    {
        sealed class Mark {public Image Image;public float Age,Life,Alpha;}
        readonly List<Mark> marks=new List<Mark>();
        readonly RectTransform layer,root;readonly TextMeshProUGUI counter;
        Vector2 previous;bool tracing;SurgeryTool previousTool;float gelTick;
        public float SutureCm {get;private set;} public float GelMl {get;private set;}
        public SurgeryEffects(RectTransform root,TMP_FontAsset font,RectTransform field)
        {
            this.root=root;
            layer=new GameObject("TransientToolTrail",typeof(RectTransform)).GetComponent<RectTransform>();layer.SetParent(field,false);
            var go=new GameObject("UsageDebug",typeof(RectTransform),typeof(TextMeshProUGUI));var r=(RectTransform)go.transform;r.SetParent(root,false);r.anchoredPosition=new Vector2(0,-430);r.sizeDelta=new Vector2(940,34);
            counter=go.GetComponent<TextMeshProUGUI>();counter.font=font;counter.fontSize=18;counter.color=new Color(.7f,.85f,.9f);counter.alignment=TextAlignmentOptions.Center;counter.raycastTarget=false;
            UpdateCounter();
        }
        public void Detach(){layer.SetParent(root,false);Stop();foreach(var m in marks)if(m.Image!=null)Object.Destroy(m.Image.gameObject);marks.Clear();}
        public void Bind(RectTransform field){layer.SetParent(field,false);layer.localPosition=Vector3.zero;}
        public void Tick()
        {
            for(int i=marks.Count-1;i>=0;i--){var m=marks[i];m.Age+=Time.unscaledDeltaTime;if(m.Image==null||m.Age>=m.Life){if(m.Image!=null)Object.Destroy(m.Image.gameObject);marks.RemoveAt(i);continue;}var c=m.Image.color;c.a=m.Alpha*(1-m.Age/m.Life);m.Image.color=c;}
        }
        public void Stop(){tracing=false;gelTick=0;}
        public float Draw(SurgeryTool tool,bool held,Vector3 world,float dt)
        {
            if(!held || dt<=0){Stop();return 0;}
            Vector2 p=layer.InverseTransformPoint(world);float dispensed=0;
            if(tracing && previousTool==tool && tool!=SurgeryTool.Gel)
            {
                Vector2 d=p-previous;
                if(tool==SurgeryTool.Suture)SutureCm+=d.magnitude/100;
                if(d.sqrMagnitude>1){var line=MarkAt((p+previous)*.5f,new Vector2(d.magnitude,1.4f),new Color(.85f,.08f,.12f,.22f),.35f);line.localEulerAngles=new Vector3(0,0,Mathf.Atan2(d.y,d.x)*Mathf.Rad2Deg);}
            }
            if(tool==SurgeryTool.Gel)
            {
                dispensed=GelRules.FlowMlPerSecond*dt;GelMl+=dispensed;gelTick+=dt;
                if(gelTick>=.04f){gelTick=0;float size=Random.Range(18f,34f);MarkAt(p+Random.insideUnitCircle*14,Vector2.one*size,new Color(.25f,.9f,.65f,.13f),.8f);}
            }
            previous=p;previousTool=tool;tracing=true;UpdateCounter();return dispensed;
        }
        RectTransform MarkAt(Vector2 p,Vector2 size,Color color,float life)
        {
            if(marks.Count>=160){Object.Destroy(marks[0].Image.gameObject);marks.RemoveAt(0);}
            var go=new GameObject("ToolTrace",typeof(RectTransform),typeof(Image));var r=(RectTransform)go.transform;r.SetParent(layer,false);r.anchoredPosition=p;r.sizeDelta=size;
            var im=go.GetComponent<Image>();im.color=color;im.raycastTarget=false;marks.Add(new Mark{Image=im,Life=life,Alpha=color.a});return r;
        }
        void UpdateCounter(){counter.text="DEBUG / this session   Suture: "+SutureCm.ToString("0.00")+" cm   Gel: "+GelMl.ToString("0.0")+" mL";}
    }
}
