using System;
namespace CasualtiesSurgery.Procedures
{
    // Wound-local coordinates: x in half-widths, y in wound lengths (downward).
    public sealed class FragmentPull
    {
        readonly float grace, requiredTravel, endMargin;
        float gripX, gripY, furthest;
        public bool Active { get; private set; }
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
            float travel=gripY-y;
            if (!Finite(x) || !Finite(y) || Math.Abs(x-gripX)>0.18f*grace || travel<furthest-0.06f*grace)
            { Reset(); return false; }
            Travel=Math.Max(0,travel); furthest=Math.Max(furthest,Travel); return true;
        }
        public bool End(float x, float y)
        {
            bool success=Move(x,y) && Travel>=requiredTravel-endMargin*grace;
            Active=false; if (!success) Travel=0; return success;
        }
        public void Reset() { Active=false; Travel=furthest=0; }
        static bool Finite(float v) => !float.IsNaN(v) && !float.IsInfinity(v);
    }
}
