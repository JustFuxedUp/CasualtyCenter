using System;
using System.Collections.Generic;
namespace CasualtiesSurgery.Procedures
{
    // Wound coordinates: x crosses its width (-1..1); y progresses along it (0..1).
    public sealed class SutureStroke
    {
        readonly float grace;
        public event Action<float,float> TurnCommitted;
        public SutureStroke(float grace = 1f) { this.grace = grace; }
        float SideThreshold => Math.Max(0.2f, 1f - 0.35f*grace);
        float WidthLimit => 1f + 0.6f*grace;
        readonly List<float> gaps = new List<float>();
        readonly List<float> angles = new List<float>();
        float previousX, previousY, lastTurnX, lastTurnY, distance, direct, furthest;
        int side;
        bool reachedSide;
        float peakX, peakY;
        public bool Active { get; private set; }
        public int Crossings => gaps.Count;
        public bool Begin(float x, float y)
        {
            Reset();
            if (!Finite(x) || !Finite(y) || x > -SideThreshold || x < -WidthLimit || y < -0.12f*grace || y > 0.12f*grace) return false;
            previousX = lastTurnX = x; previousY = lastTurnY = furthest = y;
            side = -1; Active = true; return true;
        }
        public bool Move(float x, float y)
        {
            if (!Active) return false;
            if (!Finite(x) || !Finite(y) || Math.Abs(x) > WidthLimit || y < -0.12f*grace || y > 1f+0.12f*grace || y < furthest - 0.12f*grace)
            { Active = false; return false; }
            distance += Distance(previousX, previousY, x, y);
            previousX = x; previousY = y; furthest = Math.Max(furthest, y);
            // Commit a turn at its outermost point, not at the first sample that
            // crosses a threshold. This keeps scoring stable across frame rates.
            if (reachedSide && ((side < 0 && x < peakX - 0.08f*grace) || (side > 0 && x > peakX + 0.08f*grace)))
                if (!CommitTurn()) return false;
            if ((side < 0 && x >= SideThreshold) || (side > 0 && x <= -SideThreshold))
            {
                if (!reachedSide || (side < 0 ? x >= peakX : x <= peakX)) { peakX = x; peakY = y; }
                reachedSide = true;
            }
            return true;
        }
        public bool End(float x, float y, out int quality)
        {
            quality = 0;
            bool valid = Move(x, y);
            if (valid && reachedSide) valid = CommitTurn();
            Active = false;
            if (!valid || gaps.Count < 4 || y < 1f-0.1f*grace || Math.Abs(x) < SideThreshold || lastTurnY < 1f-0.1f*grace) return false;
            float efficiency = Math.Min(1, direct / Math.Max(distance, 0.0001f));
            float mean = 0; foreach (float gap in gaps) mean += gap; mean /= gaps.Count;
            float variance = 0; foreach (float gap in gaps) variance += (gap - mean) * (gap - mean);
            float variation = (float)Math.Sqrt(variance / gaps.Count) / Math.Max(mean, 0.001f);
            float meanAngle = 0; foreach (float angle in angles) meanAngle += angle; meanAngle /= angles.Count;
            float angleVariance = 0; foreach (float angle in angles) angleVariance += (angle-meanAngle)*(angle-meanAngle);
            float angleVariation = (float)Math.Sqrt(angleVariance/angles.Count) / Math.Max(meanAngle,0.001f);
            float consistency = Math.Max(0, 1 - (variation + angleVariation)*0.5f);
            quality = (int)Math.Round(100 * (0.6f * consistency + 0.4f * efficiency));
            return efficiency >= 0.45f && quality >= 35;
        }
        bool CommitTurn()
        {
            float progress = peakY - lastTurnY;
            if (progress < 0.045f/grace || gaps.Count >= 10) { Active = false; return false; }
            gaps.Add(progress);
            angles.Add((float)Math.Atan2(Math.Abs(peakX-lastTurnX)*0.15f, progress));
            direct += Distance(lastTurnX, lastTurnY, peakX, peakY);
            lastTurnX = peakX; lastTurnY = peakY; side = -side; reachedSide = false;
            TurnCommitted?.Invoke(peakX,peakY);
            return true;
        }
        public void Reset() { Active = false; reachedSide = false; gaps.Clear(); angles.Clear(); distance = direct = 0; }
        static bool Finite(float v) => !float.IsNaN(v) && !float.IsInfinity(v);
        static float Distance(float x, float y, float a, float b)
            => (float)Math.Sqrt((x-a)*(x-a)*0.0225f + (y-b)*(y-b));
    }
}
