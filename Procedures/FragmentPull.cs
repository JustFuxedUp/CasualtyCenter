using System;
namespace CasualtiesSurgery.Procedures
{
    // Wound-local coordinates: x in half-widths, y in wound lengths (downward).
    public sealed class FragmentPull
    {
        readonly float grace, requiredTravel, endMargin;
        float gripX, gripY, furthest, previousTravel, backtrack, deviation;
        public int Quality { get; private set; }
        public bool Active { get; private set; }
        public bool Loose { get; private set; }
        public float Travel { get; private set; }
        public FragmentPull(float grace = 2f, float requiredTravel = 0.8f, float endMargin = 0.05f)
        { this.grace=grace; this.requiredTravel=requiredTravel; this.endMargin=endMargin; }
        public bool Begin(float x, float y)
        {
            Reset();
            if (!Finite(x) || !Finite(y) || Math.Abs(x) > 0.18f*grace || Math.Abs(y-0.38f) > 0.09f*grace) return false;
            gripX=x; gripY=y; Active=true; return true;
        }
        public bool Move(float x, float y)
        {
            if (!Active) return false;
            if (!Finite(x) || !Finite(y)) { Reset(); return false; }
            if (Loose) return true;
            float travel=gripY-y;
            if (!Finite(x) || !Finite(y) || Math.Abs(x-gripX)>0.18f*grace || travel<furthest-0.06f*grace)
            { Reset(); return false; }
            backtrack+=Math.Max(0,previousTravel-travel);previousTravel=travel;
            deviation=Math.Max(deviation,Math.Abs(x-gripX));
            Travel=Math.Max(0,travel); furthest=Math.Max(furthest,Travel);
            Loose=Travel>=requiredTravel-endMargin*grace;
            if(Loose) Quality=(int)Math.Round(100*Math.Max(0,1-0.5f*deviation/(0.18f*grace)-0.5f*backtrack/Math.Max(requiredTravel,0.01f)));
            return true;
        }
        public bool End(float x, float y)
        {
            bool success=Move(x,y) && Loose;
            Active=false; if (!success) Travel=0; return success;
        }
        public void Reset() { Active=false; Loose=false; Travel=furthest=previousTravel=backtrack=deviation=0;Quality=0; }
        static bool Finite(float v) => !float.IsNaN(v) && !float.IsInfinity(v);
    }
}
