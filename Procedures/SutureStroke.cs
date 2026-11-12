using System;
using System.Collections.Generic;
namespace CasualtiesSurgery.Procedures
{
    // The wound is x=0, y=0..1. Start/end are unrestricted; only crossings over the wound count.
    public sealed class SutureStroke
    {
        readonly float grace;
        public event Action<float,float> TurnCommitted;
        public SutureStroke(float grace=1f) { this.grace=grace; }
        float Threshold => Math.Max(0.2f,1f-0.35f*grace);
        readonly List<float> gaps=new List<float>(), angles=new List<float>();
        float px,py,turnX,turnY,peakX,peakY,distance,direct;
        int side,direction;
        bool pendingCross;
        public bool Active { get; private set; }
        public int Crossings { get; private set; }
        public bool Begin(float x,float y)
        {
            Reset(); if(!Finite(x)||!Finite(y)) return false;
            px=peakX=turnX=x; py=peakY=turnY=y;
            side=Side(x); Active=true; return true;
        }
        public bool Move(float x,float y)
        {
            if(!Active) return false;
            if(!Finite(x)||!Finite(y)) { Active=false; return false; }
            distance+=Distance(px,py,x,y);
            if((px<0 && x>=0)||(px>0 && x<=0))
            {
                float t=-px/(x-px), crossY=py+(y-py)*t;
                pendingCross=crossY>=-0.12f*grace && crossY<=1f+0.12f*grace;
            }
            int next=Side(x);
            if(next!=0 && next!=side)
            {
                if(side!=0 && pendingCross) Crossings++;
                side=next; pendingCross=false;
            }
            // Use outer turns for the visible straight thread, with hysteresis against hand jitter.
            if(direction==0 && Math.Abs(x-peakX)>0.001f) direction=x>peakX?1:-1;
            if((direction>0 && x<peakX-0.08f*grace)||(direction<0 && x>peakX+0.08f*grace))
            { Commit(peakX,peakY); direction=-direction; peakX=x; peakY=y; }
            else if((direction>0 && x>=peakX)||(direction<0 && x<=peakX)) {peakX=x;peakY=y;}
            px=x;py=y;return true;
        }
        public bool End(float x,float y,out int quality)
        {
            quality=0;
            bool valid=Move(x,y); Active=false;
            if(!valid || Crossings<3) return false;
            Commit(x,y);
            float efficiency=Math.Min(1,direct/Math.Max(distance,0.0001f));
            float consistency=Math.Max(0,1-(Variation(gaps)+Variation(angles))*0.5f);
            quality=(int)Math.Round(100*(0.6f*consistency+0.4f*efficiency));
            return true; // Quality is feedback, never an extra completion gate.
        }
        void Commit(float x,float y)
        {
            float length=Distance(turnX,turnY,x,y); if(length<0.00001f) return;
            float gap=Math.Abs(y-turnY); gaps.Add(gap);
            angles.Add((float)Math.Atan2(Math.Abs(x-turnX)*0.15f,gap));
            direct+=length; turnX=x;turnY=y;TurnCommitted?.Invoke(x,y);
        }
        static float Variation(List<float> values)
        {
            if(values.Count==0) return 0;
            float mean=0;foreach(float v in values)mean+=v;mean/=values.Count;
            if(mean<0.0001f) return 0;
            float sum=0;foreach(float v in values)sum+=(v-mean)*(v-mean);
            return (float)Math.Sqrt(sum/values.Count)/mean;
        }
        public void Reset() {Active=false;Crossings=0;side=direction=0;pendingCross=false;distance=direct=0;gaps.Clear();angles.Clear();}
        int Side(float x)=>x>=Threshold?1:x<=-Threshold?-1:0;
        static bool Finite(float v)=>!float.IsNaN(v)&&!float.IsInfinity(v)&&Math.Abs(v)<100000;
        static float Distance(float x,float y,float a,float b)=>(float)Math.Sqrt((x-a)*(x-a)*0.0225f+(y-b)*(y-b));
    }
}
