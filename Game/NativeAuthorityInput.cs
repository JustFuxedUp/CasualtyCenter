using System;
using System.Collections.Generic;
using System.Linq;
using CasualtiesSurgery.Procedures;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace CasualtiesSurgery.Game
{
    internal sealed partial class NativeAuthorityMinigame
    {
        int grip=-1;
        Vector2 gripLocal;
        float startPointer,startAngle,heldSeconds,dragSeconds;
        bool cutting;
        FragmentPull pull;
        Vector3 grabOffset;
        StrokeCandidate stroke;
        readonly List<StrokeCandidate> candidates=new List<StrokeCandidate>();
        float strokeSeconds;
        sealed class StrokeCandidate
        {
            public int Id;
            public Part Part;
            public readonly JArray Samples=new JArray();
            public readonly SutureStroke Preview=new SutureStroke(2);
            public bool Overflow;
        }
        static JObject Point(Vector2 p)=>new JObject { ["x"]=Math.Round(p.x,4),["y"]=Math.Round(p.y,4) };
        JObject TargetPoint(int target,Vector2 p)
        {var value=Point(p);value[kind==ProcedureKind.Fragment?"site":"wound"]=target;return value;}
        void ResetGesture(bool notify,bool preserveReleased=false)
        {
            grip=-1;pull?.Reset();pull=null;stroke=null;candidates.Clear();thread?.Clear();
            heldSeconds=dragSeconds=strokeSeconds=0;cutting=false;ready=false;
            effects?.Stop();Plugin.Feedback?.StopLoop(this);
            outerPool?.ResetPreview();foreach(var part in parts)part.Pools?.ResetPreview();
            if(notify)presentation.CancelInput(preserveReleased);
        }
        void InputStep(Vector3 contact,Vector2 point)
        {
            bool held=host.handClicking;
            heldSeconds=held?Mathf.Min(.25f,heldSeconds+Time.deltaTime):0;
            if(Tool==SurgeryTool.Suction || Tool==SurgeryTool.Gel || Tool==SurgeryTool.Scalpel)
            {
                if(held && heldSeconds>=.08f)
                {
                    float seconds=heldSeconds;heldSeconds=0;
                    if(Tool==SurgeryTool.Suction)Suction(contact,point,seconds);
                    else if(Tool==SurgeryTool.Gel)Gel(contact,point,seconds);
                    else
                    {
                        if(presentation.ResourceBusy("*"))return;
                        if(kind!=ProcedureKind.Fragment && ((kind==ProcedureKind.Fracture || Stage!=1)&&Outer.Blocking))return;
                        var payload=Point(point);payload["active"]=true;payload["seconds"]=seconds;
                        presentation.Submit("scalpel.sample",payload,"scalpel");cutting=true;
                    }
                }
                if(!held && cutting)
                {var payload=Point(point);payload["active"]=false;payload["seconds"]=0;presentation.Submit("scalpel.sample",payload);cutting=false;}
                return;
            }
            if(Tool==SurgeryTool.Suture){Stitch(contact,point);return;}
            if(Tool!=SurgeryTool.Forceps)return;
            if(kind==ProcedureKind.Fracture && Stage==1)Rotate(contact);
            else if(kind==ProcedureKind.InternalBleeding && Stage==1)Pinch(point);
            else if(kind==ProcedureKind.Fragment)Extract(contact);
        }
        void Suction(Vector3 contact,Vector2 point,float seconds)
        {
            int target=-1,slot=-1;float nearest=float.PositiveInfinity;
            if(outerPool!=null)nearest=outerPool.Distance(contact,out slot);
            foreach(var part in parts)
            {
                if(!part.Wound.gameObject.activeSelf)continue;
                float distance=part.Pools.Distance(contact,out int candidate);
                if(distance>=nearest)continue;nearest=distance;slot=candidate;target=part.Id;
            }
            if(slot<0 || presentation.ResourceBusy(target<0?"*":"wound:"+target))return;
            var payload=TargetPoint(target,point);payload["slot"]=slot;payload["seconds"]=seconds;
            if(presentation.Submit("pool.suction",payload,"suction:"+target+":"+slot))
                (target<0?outerPool:parts.Find(p=>p.Id==target)?.Pools)?.Preview(slot,seconds);
        }
        void Gel(Vector3 contact,Vector2 point,float seconds)
        {
            if(kind==ProcedureKind.Fragment)
            {
                Part selected=null;Vector2 selectedLocal=Vector2.zero;float best=44*44;
                foreach(var part in parts)
                {
                    var site=B.Sites.Find(s=>s.Id==part.Id);if(site.Completed || !site.Extracted || site.Pools.Blocking || presentation.ResourceBusy("wound:"+part.Id))continue;
                    Vector2 local=part.Wound.InverseTransformPoint(contact);
                    float beyond=Mathf.Max(0,Mathf.Abs(local.y)-part.Layout.Length/2);float distance=(local.x*local.x+beyond*beyond)*scale*scale;
                    if(distance>=best)continue;selected=part;selectedLocal=local;best=distance;
                }
                if(selected!=null)
                {var payload=TargetPoint(selected.Id,selectedLocal);payload["seconds"]=seconds;presentation.Submit("fragment.gel",payload,"gel:"+selected.Id);}
                return;
            }
            if(kind==ProcedureKind.InternalBleeding && Stage==1)
            {
                foreach(var part in parts)
                {
                    var w=I.Wounds[part.Id];if(w.Laceration || w.Phase!=InternalWoundPhase.Open || w.Pools.Blocking || (point-new Vector2(w.X,w.Y)).sqrMagnitude>48*48)continue;
                    var payload=TargetPoint(part.Id,point);payload["seconds"]=seconds;presentation.Submit("wound.gel",payload,"gel:"+part.Id);return;
                }
            }
            if((kind==ProcedureKind.Fracture || Stage!=1) && Outer.Blocking)return;
            prep.Apply(point,seconds*GelRules.FlowMlPerSecond,(section,dose)=>
            {var payload=Point(point);payload["section"]=section;payload["seconds"]=seconds;return presentation.Submit("incision.prepare",payload,"prep:"+section);});
        }
        void Rotate(Vector3 contact)
        {
            if(Outer.Blocking){if(grip>=0)ResetGesture(true);return;}
            Vector2 point=boneViewport.InverseTransformPoint(contact);
            if(host.handStartedClicking && boneViewport.rect.Contains(point))
                for(int n=0;n<bones.Count;n++)
                {
                    if(F.Aligned[n] || presentation.ResourceBusy("bone:"+n))continue;Vector2 local=bones[n].InverseTransformPoint(contact);var rect=bones[n].rect;
                    if(Mathf.Abs(local.x)>42 || local.y<rect.yMin-12 || local.y>rect.yMax+12)continue;
                    grip=n;Vector2 delta=point-bones[n].anchoredPosition;startPointer=Mathf.Atan2(delta.y,delta.x)*Mathf.Rad2Deg;startAngle=F.Angles[n];
                    var payload=Point(point);payload["bone"]=n;presentation.Submit("bone.grip",payload);Plugin.Feedback?.Grab();break;
                }
            if(grip<0)return;
            Vector2 d=point-bones[grip].anchoredPosition;
            if(d.sqrMagnitude>100)bones[grip].localEulerAngles=new Vector3(0,0,startAngle+Mathf.DeltaAngle(startPointer,Mathf.Atan2(d.y,d.x)*Mathf.Rad2Deg));
            dragSeconds+=Time.deltaTime;
            if(host.handStoppedClicking || (host.handClicking && dragSeconds>=.08f))
            {
                var payload=Point(point);payload["bone"]=grip;
                presentation.Submit(host.handStoppedClicking?"bone.release":"bone.drag",payload,host.handStoppedClicking?null:"bone:"+grip);dragSeconds=0;
                if(host.handStoppedClicking)grip=-1;
            }
        }
        void Pinch(Vector2 point)
        {
            if(host.handStartedClicking)
                foreach(var part in parts)
                {
                    var w=I.Wounds[part.Id];Vector2 local=part.Wound.InverseTransformPoint(field.TransformPoint(point));
                    if(presentation.ResourceBusy("wound:"+part.Id) || !w.Laceration || w.Phase!=InternalWoundPhase.Open || w.Pools.Blocking || Mathf.Abs(local.x)<17 || Mathf.Abs(local.x)>48 || Mathf.Abs(local.y)>66)continue;
                    grip=part.Id;gripLocal=local;presentation.Submit("laceration.grip",TargetPoint(grip,point));Plugin.Feedback?.Grab();break;
                }
            if(grip<0)return;
            if(!host.handClicking || I.Wounds[grip].Pools.Blocking || I.Wounds[grip].Phase!=InternalWoundPhase.Open)
            {grip=-1;presentation.Submit("gesture.cancel",new JObject());return;}
            var selected=parts.Find(p=>p.Id==grip);Vector2 next=selected.Wound.InverseTransformPoint(field.TransformPoint(point));
            var result=LacerationDrag.Evaluate(gripLocal.x,gripLocal.y,next.x,next.y,108);gripLocal=next;dragSeconds+=Time.deltaTime;
            if(result!=LacerationDragResult.Hold || dragSeconds>=.08f)
            {
                presentation.Submit("laceration.drag",TargetPoint(grip,point),result==LacerationDragResult.Hold?"pinch:"+grip:null);dragSeconds=0;
                if(result!=LacerationDragResult.Hold)grip=-1;
            }
        }
        Vector2 ExtractionPoint(Part part,Vector3 contact)
        {
            Vector2 local=part.Wound.InverseTransformPoint(contact);
            return new Vector2(local.y/part.Layout.Width,.38f-(local.x-(17.5f+part.Layout.Length*.84f*.47f))/part.Layout.Length);
        }
        void Extract(Vector3 contact)
        {
            if(host.handStartedClicking)
                foreach(var part in parts)
                {
                    var site=B.Sites.Find(s=>s.Id==part.Id);if(site.Completed || site.Extracted || site.Pools.Blocking || presentation.ResourceBusy("wound:"+part.Id) || presentation.PreviewingExtraction(part.Id))continue;
                    var point=ExtractionPoint(part,contact);if(!FragmentPull.CanGrip(point.x,point.y))continue;
                    grip=part.Id;pull=new FragmentPull(2,(part.Layout.Length*.84f*.35f+8)/part.Layout.Length,.005f);pull.Begin(point.x,point.y);
                    grabOffset=part.Fragment.position-contact;presentation.Submit("fragment.grip",TargetPoint(grip,point));Plugin.Feedback?.Grab();break;
                }
            if(grip<0 || pull==null)return;
            var selected=parts.Find(p=>p.Id==grip);var wound=B.Sites.Find(s=>s.Id==grip);
            if(wound.Completed || wound.Extracted || wound.Pools.Blocking){ResetGesture(true);return;}
            var next=ExtractionPoint(selected,contact);bool released=host.handStoppedClicking;
            bool wasLoose=pull.Loose;
            bool success=released?pull.End(next.x,next.y):pull.Move(next.x,next.y);bool becameLoose=!wasLoose && pull.Loose;dragSeconds+=Time.deltaTime;
            // Preserve each sampled extraction point: backtracking/deviation contribute to
            // the production score. Overflow cancels rather than discarding a release.
            if(released || !success || becameLoose || dragSeconds>=.08f)
            {
                if(released && success)
                {
                    selected.Fragment.position=contact+grabOffset;
                    selected.Fragment.GetComponent<UnityEngine.UI.Image>().maskable=false;
                    presentation.ReleaseFragment(TargetPoint(grip,next));
                }
                else presentation.Submit(released?"fragment.release":"fragment.drag",TargetPoint(grip,next));
                dragSeconds=0;
            }
            if(pull==null)return; // Queue overflow may have synchronously cancelled this gesture.
            if(pull.Active)
            {
                selected.Fragment.GetComponent<UnityEngine.UI.Image>().maskable=!pull.Loose;
                if(pull.Loose)selected.Fragment.position=contact+grabOffset;
                else selected.Fragment.anchoredPosition=selected.FragmentOrigin+Vector2.right*(pull.Travel*selected.Layout.Length);
            }
            if(released || !success){grip=-1;pull=null;}
        }
        Vector2 SuturePoint(StrokeCandidate candidate,Vector3 contact)
        {
            Vector2 local=candidate.Part==null?(Vector2)field.InverseTransformPoint(contact):(Vector2)candidate.Part.Wound.InverseTransformPoint(contact);
            float width=kind==ProcedureKind.Fragment?candidate.Part.Layout.Width:22;
            float length=kind==ProcedureKind.Fragment?candidate.Part.Layout.Length:candidate.Part!=null?108:kind==ProcedureKind.Fracture?180:252;
            return new Vector2(local.x/width,.5f-local.y/length);
        }
        void Sample(StrokeCandidate candidate,Vector2 point)
        {
            if(candidate.Overflow)return;
            if(candidate.Samples.Count>=128 || Mathf.Abs(point.x)>20 || Mathf.Abs(point.y)>20){candidate.Overflow=true;return;}
            candidate.Samples.Add(Point(point));
        }
        bool SutureEligible(Part part)
        {
            if(presentation.ResourceBusy("wound:"+part.Id))return false;
            if(kind==ProcedureKind.Fragment){var site=B.Sites.Find(s=>s.Id==part.Id);return !site.Completed && site.Extracted && !site.Pools.Blocking;}
            var w=I.Wounds[part.Id];return w.Laceration && w.Phase==InternalWoundPhase.Pinched && !w.Pools.Blocking;
        }
        void Stitch(Vector3 contact,Vector2 point)
        {
            bool outer=kind!=ProcedureKind.Fragment && Stage==2;
            if(kind==ProcedureKind.Fracture && !outer || kind==ProcedureKind.InternalBleeding && Stage==0)return;
            if(outer && Outer.Blocking){if(stroke!=null)ResetGesture(true);return;}
            if(host.handStartedClicking)
            {
                candidates.Clear();stroke=null;thread.Begin(point);strokeSeconds=0;
                if(outer)candidates.Add(new StrokeCandidate {Id=-1});
                else foreach(var part in parts.Where(SutureEligible))candidates.Add(new StrokeCandidate {Id=part.Id,Part=part});
                float best=float.MaxValue;
                foreach(var c in candidates)
                {
                    var local=SuturePoint(c,contact);Sample(c,local);c.Preview.Begin(local.x,local.y);
                    float metric=c.Part==null?0:((Vector2)(c.Part.Wound.position-contact)).sqrMagnitude;
                    if(metric<best){best=metric;stroke=c;}
                }
                if(stroke!=null)
                {
                    var preview=stroke;
                    preview.Preview.TurnCommitted+=(x,y)=>
                    {
                        float length=kind==ProcedureKind.Fragment?preview.Part.Layout.Length:preview.Part!=null?108:kind==ProcedureKind.Fracture?180:252;
                        float width=kind==ProcedureKind.Fragment?preview.Part.Layout.Width:22;
                        Vector2 local=new Vector2(x*width,(.5f-y)*length);
                        thread.Commit(preview.Part==null?local:(Vector2)field.InverseTransformPoint(preview.Part.Wound.TransformPoint(local)));
                    };
                }
            }
            if(stroke==null)return;
            if(!host.handClicking && !host.handStoppedClicking)return;
            strokeSeconds+=Time.deltaTime;bool sample=strokeSeconds>=.05f || host.handStoppedClicking;
            foreach(var candidate in candidates)
            {
                if(candidate.Part!=null && !SutureEligible(candidate.Part)){candidate.Overflow=true;continue;}
                var next=SuturePoint(candidate,contact);candidate.Preview.Move(next.x,next.y);if(sample)Sample(candidate,next);
            }
            if(sample)strokeSeconds=0;
            if(!host.handStoppedClicking){thread.Preview(point);return;}
            StrokeCandidate chosen=null;int bestCrossings=0;
            foreach(var candidate in candidates)
            {
                if(candidate.Overflow || !TreatmentInput.Suture(candidate.Samples,out _))continue;
                if(candidate.Preview.Crossings>bestCrossings){bestCrossings=candidate.Preview.Crossings;chosen=candidate;}
            }
            if(chosen!=null)
            {
                var payload=new JObject { ["samples"]=chosen.Samples };
                string action=outer?"incision.close":kind==ProcedureKind.Fragment?"fragment.stitch":"wound.stitch";
                if(!outer)payload[kind==ProcedureKind.Fragment?"site":"wound"]=chosen.Id;
                presentation.Submit(action,payload);
            }
            else Plugin.Feedback?.Mistake();
            thread.Clear();stroke=null;candidates.Clear();
        }
    }
}
