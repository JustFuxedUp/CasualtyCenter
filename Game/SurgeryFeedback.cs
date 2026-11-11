using System;
using System.Collections.Generic;
using System.IO;
using CasualtiesSurgery.Audio;
using TMPro;
using UnityEngine;
namespace CasualtiesSurgery.Game
{
    public sealed class SurgeryFeedback : MonoBehaviour
    {
        AudioSource source;
        readonly Dictionary<string,AudioClip> clips=new Dictionary<string,AudioClip>();
        sealed class Popup { public TextMeshProUGUI Text;public float Age;public Vector2 Origin; }
        readonly List<Popup> popups=new List<Popup>();
        float lastStitch=-10;
        public void Initialize(string folder,float volume,Action<string> warn)
        {
            source=gameObject.AddComponent<AudioSource>();source.playOnAwake=false;source.spatialBlend=0;source.volume=volume;
            foreach(string name in new[]{"Bad-Ok","Great","Mistake","SutureStitch","ForcepsGrab"})
                try
                {
                    var wave=WaveData.Read(File.ReadAllBytes(Path.Combine(folder,name+".wav")));
                    var clip=AudioClip.Create(name,wave.Samples.Length/wave.Channels,wave.Channels,wave.Rate,false);
                    clip.SetData(wave.Samples,0);clips.Add(name,clip);
                }
                catch(Exception error) {warn("Could not load surgery sound "+name+": "+error.Message);}
        }
        void Play(string name) {if(source!=null && clips.TryGetValue(name,out var clip))source.PlayOneShot(clip);}
        public void Grab() {Play("ForcepsGrab");}
        public void Mistake() {Play("Mistake");}
        public void Stitch()
        {
            if(Time.unscaledTime-lastStitch<0.07f)return;
            lastStitch=Time.unscaledTime;Play("SutureStitch");
        }
        public void Result(int score,Vector3 position,TMP_FontAsset font,Canvas canvas)
        {
            string rating=ResultRating.Label(score);Play(rating=="Great"?"Great":"Bad-Ok");
            if(canvas==null)return;
            var go=new GameObject("SurgeryResult",typeof(RectTransform),typeof(TextMeshProUGUI));
            var rect=(RectTransform)go.transform;rect.SetParent(canvas.transform,false);rect.sizeDelta=new Vector2(140,40);
            rect.position=position;rect.anchoredPosition+=Vector2.up*22;rect.SetAsLastSibling();
            var text=go.GetComponent<TextMeshProUGUI>();text.font=font;text.fontSize=24;text.alignment=TextAlignmentOptions.Center;
            text.raycastTarget=false;text.text=rating;text.color=new Color(0.78f,0.88f,1);
            popups.Add(new Popup {Text=text,Origin=rect.anchoredPosition});
        }
        void Update()
        {
            for(int i=popups.Count-1;i>=0;i--)
            {
                var p=popups[i];p.Age+=Time.unscaledDeltaTime;
                if(p.Text==null || p.Age>=0.9f){if(p.Text!=null)Destroy(p.Text.gameObject);popups.RemoveAt(i);continue;}
                p.Text.rectTransform.anchoredPosition=p.Origin+Vector2.up*(p.Age*24);
                var color=p.Text.color;color.a=1-Mathf.InverseLerp(0.2f,0.9f,p.Age);p.Text.color=color;
            }
        }
        void OnDestroy()
        {
            foreach(var popup in popups)if(popup.Text!=null)Destroy(popup.Text.gameObject);
            foreach(var clip in clips.Values)if(clip!=null)Destroy(clip);
        }
    }
}
