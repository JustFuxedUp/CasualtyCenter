using System;

namespace CasualtiesSurgery.Procedures
{
    public sealed class BloodPools
    {
        public const float ClearSeconds = .25f;
        public bool Rolled;
        public float First, Second;
        public float? FirstAngle, SecondAngle;
        public bool Blocking => First > 0 || Second > 0;

        public void Observe(bool bleeding, float roll)
        {
            if (Rolled || !bleeding) return;
            Rolled = true;
            if (roll < .65f) First = ClearSeconds;
            if (roll < .25f) Second = ClearSeconds;
        }

        public void Spill()
        {
            if (First <= 0) FirstAngle = null;
            First = ClearSeconds;
        }

        public float Angle(int index, Func<float> randomAngle)
        {
            if (index == 0) return FirstAngle ?? (FirstAngle = randomAngle()).Value;
            return SecondAngle ?? (SecondAngle = randomAngle()).Value;
        }

        public float Remaining(int index) => index == 0 ? First : Second;

        public bool Suction(int index, float seconds)
        {
            if (index < 0 || index > 1 || !(seconds > 0) || float.IsInfinity(seconds)) return false;
            float before = Remaining(index);
            float after = Math.Max(0, before - seconds);
            if (index == 0) First = after;
            else Second = after;
            return before > 0 && after == 0;
        }

        public bool Valid() => First >= 0 && First <= ClearSeconds && Second >= 0 && Second <= ClearSeconds &&
            ValidAngle(FirstAngle) && ValidAngle(SecondAngle);

        static bool ValidAngle(float? angle) => !angle.HasValue || (angle >= 0 && angle <= 360);
    }
}
