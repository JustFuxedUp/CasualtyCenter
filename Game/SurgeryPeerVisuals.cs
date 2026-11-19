using System;
using System.Collections.Generic;
using CasualtiesSurgery.Procedures;
using UnityEngine;
using UnityEngine.UI;
using Object=UnityEngine.Object;

namespace CasualtiesSurgery.Game
{
    // Cosmetic overlay only: no colliders, input handlers, sounds or treatment calls.
    internal sealed class SurgeryPeerVisuals : IDisposable
    {
        sealed class Peer
        {
            public RectTransform Root,Hand;public Image Image;public KnifeTipVisual Knife;
            public readonly List<Image> Marks=new List<Image>();
        }
        readonly RectTransform field;readonly MinigameBase host;
        readonly Dictionary<Guid,Peer> peers=new Dictionary<Guid,Peer>();
        public SurgeryPeerVisuals(RectTransform field,MinigameBase host){this.field=field;this.host=host;}
        Image Make(string name,Transform parent)
        {
            var go=new GameObject(name,typeof(RectTransform),typeof(Image));go.transform.SetParent(parent,false);
            var image=go.GetComponent<Image>();image.raycastTarget=false;return image;
        }
        Peer Create(SurgeryPeerVisual data)
        {
            var root=new GameObject("RemoteSurgeon",typeof(RectTransform)).GetComponent<RectTransform>();root.SetParent(field,false);
            var image=Make("RemoteHand",root);var hand=image.rectTransform;
            var local=host.handTransform.GetComponent<Image>();hand.pivot=((RectTransform)host.handTransform).pivot;image.preserveAspect=local.preserveAspect;
            hand.gameObject.AddComponent<WristFade>();var knife=hand.gameObject.AddComponent<KnifeTipVisual>();knife.Remote=true;
            hand.anchoredPosition=new Vector2(data.State.X,data.State.Y);
            var peer=new Peer {Root=root,Hand=hand,Image=image,Knife=knife};peers.Add(data.Participant,peer);return peer;
        }
        public void Refresh(SurgeryPeerVisual[] samples,double snapshotAge,int phase,int page)
        {
            var seen=new HashSet<Guid>();
            foreach(var data in samples)
            {
                var s=data.State;double age=data.Age+snapshotAge;
                if(age>1.2 || s.Phase!=phase || s.Page!=page)continue;
                seen.Add(data.Participant);if(!peers.TryGetValue(data.Participant,out var peer))peer=Create(data);
                float fade=1-Mathf.InverseLerp(.65f,1.2f,(float)age);
                Color tint=new Color(data.Red/255f,data.Green/255f,data.Blue/255f,.28f*fade);
                // Lift black slightly so a selected black multiplayer color remains visible.
                tint.r=Mathf.Max(.12f,tint.r);tint.g=Mathf.Max(.12f,tint.g);tint.b=Mathf.Max(.12f,tint.b);
                peer.Image.sprite=s.Sprite<host.handSprites.Length?host.handSprites[s.Sprite]:null;peer.Image.color=tint;
                peer.Hand.sizeDelta=new Vector2(s.Width,s.Height);peer.Hand.localScale=new Vector3(s.Mirrored?-s.Scale:s.Scale,s.Scale,1);
                peer.Hand.anchoredPosition=Vector2.Lerp(peer.Hand.anchoredPosition,new Vector2(s.X,s.Y),1-Mathf.Exp(-18*Time.unscaledDeltaTime));
                peer.Hand.localEulerAngles=new Vector3(0,0,s.Angle);peer.Knife.RemoteScalpel=s.Tool==2;peer.Image.SetVerticesDirty();
                bool line=s.Tool==1 || s.Tool==2;int count=s.Pressed?(line?Math.Max(0,s.Trail.Length/2-1):s.Tool==3?s.Trail.Length/2:s.Tool==4?1:0):0;
                while(peer.Marks.Count<count)peer.Marks.Add(Make("RemoteToolEffect",peer.Root));
                for(int i=0;i<peer.Marks.Count;i++)
                {
                    var mark=peer.Marks[i];mark.gameObject.SetActive(i<count);if(i>=count)continue;
                    var rect=mark.rectTransform;var color=tint;color.a=(s.Tool==3?.12f:.3f)*fade;mark.color=color;mark.sprite=s.Tool==3?SurgeryArt.Get("gel1"):null;
                    if(line)
                    {
                        var a=new Vector2(s.Trail[i*2],s.Trail[i*2+1]);var b=new Vector2(s.Trail[i*2+2],s.Trail[i*2+3]);var delta=b-a;
                        rect.anchoredPosition=(a+b)*.5f;rect.sizeDelta=new Vector2(delta.magnitude,s.Tool==1?2:3);rect.localEulerAngles=new Vector3(0,0,Mathf.Atan2(delta.y,delta.x)*Mathf.Rad2Deg);
                    }
                    else {rect.anchoredPosition=s.Tool==4?new Vector2(s.X,s.Y):new Vector2(s.Trail[i*2],s.Trail[i*2+1]);rect.sizeDelta=Vector2.one*(s.Tool==4?32:24);rect.localEulerAngles=Vector3.zero;}
                }
                peer.Hand.SetAsLastSibling();
            }
            foreach(var id in new List<Guid>(peers.Keys))if(!seen.Contains(id)){Object.Destroy(peers[id].Root.gameObject);peers.Remove(id);}
        }
        public void Dispose(){foreach(var peer in peers.Values)if(peer.Root!=null)Object.Destroy(peer.Root.gameObject);peers.Clear();}
    }
}
