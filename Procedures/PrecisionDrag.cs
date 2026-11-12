using System;
namespace CasualtiesSurgery.Procedures
{
    // Coordinates are normalized to the operation canvas. No Unity dependency.
    public sealed class PrecisionDrag
    {
        readonly float ax, ay, bx, by, radius;
        public bool Active { get; private set; }
        public PrecisionDrag(float ax, float ay, float bx, float by, float radius)
        {
            this.ax = ax; this.ay = ay; this.bx = bx; this.by = by; this.radius = radius;
        }
        public bool Begin(float x, float y)
        {
            Active = Finite(x) && Finite(y) && Distance(x, y, ax, ay) <= radius;
            return Active;
        }
        public bool Move(float x, float y)
        {
            if (!Active) return false;
            float dx = bx - ax, dy = by - ay;
            float length = dx * dx + dy * dy;
            float t = length > 0 ? ((x - ax) * dx + (y - ay) * dy) / length : 0;
            t = Math.Max(0, Math.Min(1, t));
            Active = Finite(x) && Finite(y) && Distance(x, y, ax + t * dx, ay + t * dy) <= radius;
            return Active;
        }
        public bool End(float x, float y)
        {
            bool success = Move(x, y) && Distance(x, y, bx, by) <= radius;
            Active = false;
            return success;
        }
        public void Reset() { Active = false; }
        static bool Finite(float v) => !float.IsNaN(v) && !float.IsInfinity(v);
        static float Distance(float x, float y, float a, float b)
            => (float)Math.Sqrt((x - a) * (x - a) + (y - b) * (y - b));
    }
}
