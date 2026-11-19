using System;
using System.Linq;

namespace CasualtiesSurgery.Procedures
{
    public enum InternalStage { Incision, Repair, Closure, Complete }
    public enum InternalWoundPhase { Open, Pinched, Repaired }

    public enum LacerationDragResult { Hold, Close, Overpull }

    public static class LacerationDrag
    {
        public const float OpenWidth = 64f;
        public const float ClosedWidth = 12f;

        public static bool CanGrip(float x, float y, float length)
        {
            float edge = Math.Abs(x);
            return edge >= 11 && edge <= 62 && Math.Abs(y) <= length / 2 + 12;
        }

        public static float Width(float forcepsX) =>
            Math.Max(ClosedWidth, Math.Min(OpenWidth, Math.Abs(forcepsX) * 2));

        public static LacerationDragResult Evaluate(float previousX, float previousY, float x, float y, float length)
        {
            if (Math.Abs(x) > 78 || Math.Abs(y) > length / 2 + 36) return LacerationDragResult.Overpull;
            if (Math.Abs(x) <= 14 && Math.Abs(y) <= length / 2 + 12) return LacerationDragResult.Close;
            if ((previousX < 0 && x > 0) || (previousX > 0 && x < 0))
            {
                float centerY = previousY + (y - previousY) * (-previousX / (x - previousX));
                if (Math.Abs(centerY) <= length / 2 + 12) return LacerationDragResult.Close;
            }
            return LacerationDragResult.Hold;
        }
    }

    public sealed class InternalWound
    {
        public const float Interval = 6f;
        public const float BruiseGelReach = 56f;
        public InternalWoundPhase Phase;
        public float Seconds = Interval;
        public float GelMl;
        public bool Laceration;
        public float X, Y, Angle;
        public BloodPools Pools = new BloodPools();

        public void Tick(float seconds)
        {
            if (Phase == InternalWoundPhase.Repaired || !(seconds > 0) || float.IsInfinity(seconds)) return;
            Seconds -= seconds;
            if (Seconds > 0) return;
            Phase = InternalWoundPhase.Open;
            Pools.Spill();
            Seconds = Interval - (-Seconds % Interval);
        }

        public bool Pinch()
        {
            if (Phase != InternalWoundPhase.Open || Pools.Blocking) return false;
            Phase = InternalWoundPhase.Pinched;
            Seconds = Interval;
            return true;
        }

        public bool Stitch()
        {
            if (Phase != InternalWoundPhase.Pinched || Pools.Blocking) return false;
            Phase = InternalWoundPhase.Repaired;
            return true;
        }

        public bool Gel(float ml)
        {
            if (Phase != InternalWoundPhase.Open || Pools.Blocking || !(ml > 0) || float.IsInfinity(ml)) return false;
            GelMl = GelRules.Deposit(GelMl, ml, GelRules.FragmentMl);
            if (GelMl < GelRules.FragmentMl) return false;
            Phase = InternalWoundPhase.Repaired;
            return true;
        }

        public bool Valid() => Enum.IsDefined(typeof(InternalWoundPhase), Phase) &&
            Seconds > 0 && Seconds <= Interval && GelMl >= 0 && GelMl <= GelRules.FragmentMl &&
            X >= -290 && X <= 290 && Y >= -205 && Y <= 205 && Angle >= 0 && Angle <= 360 && Pools != null && Pools.Valid();
    }

    public sealed class InternalBleedingState : IOpenSurgeryState
    {
        public const int CutCount = GelRules.IncisionSections;
        public const int MaxWounds = 5;
        public const float MinimumRate = .08f;
        public const float TreatmentRate = MinimumRate / 2;
        public const float MaxEffectiveBleeding = 25f;
        public const float NativeLossPerPoint = .0057f;
        public int LayoutVersion;
        public float TreatmentBudget, SkinDebt;
        public float? PlannedSkinCost; // Legacy operations keep their original 10-point plan.
        public bool[] Cuts = new bool[CutCount];
        public InternalWound[] Wounds = Array.Empty<InternalWound>();
        public float[] GelCoverage = new float[CutCount];
        public BloodPools IncisionPools = new BloodPools();
        public BloodPools ClosurePools = new BloodPools();
        public bool Closed;

        public static bool CanStart(float bleeding, float floor) => floor > 0 && floor < MaxEffectiveBleeding && bleeding > floor;

        public static int CountForRate(float litersPerMinute) =>
            2 + (int)Math.Min(MaxWounds - 2, Math.Max(0, (litersPerMinute - MinimumRate) / .03f));

        void GenerateWounds(float rate, int seed)
        {
            int count = CountForRate(rate);
            var positions = FragmentBoard.Scatter(seed, 0, count, 290, 205, 160);
            Wounds = new InternalWound[count];
            for (int i = 0; i < count; i++)
            {
                Wounds[i] = new InternalWound
                {
                    Laceration = i % 2 == 0,
                    X = positions[i].X,
                    Y = positions[i].Y,
                    Angle = FragmentBoard.FullAngle(seed, i),
                    Seconds = InternalWound.Interval - i * .5f
                };
            }
            LayoutVersion = 2;
        }

        // Version 1 saves used implicit types and a fixed four-wound board.
        public void RestoreLayout(int version)
        {
            if (version != 1 || Wounds == null) return;
            if (Wounds.Length == 4)
            {
                int[] x = { -145, 145, -150, 150 };
                int[] y = { 75, -60, -130, 135 };
                for (int i = 0; i < Wounds.Length; i++)
                {
                    var wound = Wounds[i];
                    if (wound == null) continue;
                    wound.Laceration = i < 2;
                    wound.X = x[i];
                    wound.Y = y[i];
                    wound.Seconds = Math.Min(wound.Seconds, InternalWound.Interval);
                }
            }
            LayoutVersion = 1;
        }
        public InternalStage Stage => Closed ? InternalStage.Complete : Cuts.Any(cut => !cut) ? InternalStage.Incision :
            Wounds.Any(wound => wound.Phase != InternalWoundPhase.Repaired) ? InternalStage.Repair : InternalStage.Closure;
        public bool Started => Cuts.Any(cut => cut);
        public float Coverage => GelRules.IncisionCoverage(GelCoverage);
        public bool Disinfect(int index, float ml) => OpenSurgery.Disinfect(this,index,ml);
        OpenSurgeryPhase IOpenSurgeryState.Phase => (OpenSurgeryPhase)Stage;
        bool[] IOpenSurgeryState.IncisionCuts => Cuts;
        float[] IOpenSurgeryState.Preparation => GelCoverage;
        float IOpenSurgeryState.IncisionPlan => PlannedSkinCost ?? 10f;
        float IOpenSurgeryState.IncisionDebt { get => SkinDebt; set => SkinDebt = value; }
        BloodPools IOpenSurgeryState.ActivePools => Stage == InternalStage.Incision ? IncisionPools :
            Stage == InternalStage.Closure ? ClosurePools : null;
        void IOpenSurgeryState.EnterRepair() { } // Phase is derived from completed cuts and repair state.
        void IOpenSurgeryState.FinishClosure() { Closed = true; }

        public float Cut(int index, float skin, float bleeding, float rate, int seed)
        {
            if (Stage != InternalStage.Incision || index < 0 || index >= Cuts.Length || Cuts[index] ||
                IncisionPools.Blocking || !(skin >= 0 && skin <= 100) || (!Started && skin == 0) ||
                !(bleeding >= 0) || float.IsInfinity(bleeding)) return 0;
            if (!Started)
            {
                if (!(rate >= MinimumRate) || float.IsInfinity(rate)) return 0;
                TreatmentBudget = bleeding;
                PlannedSkinCost = IncisionSkin.Plan(skin);
                GenerateWounds(rate, seed);
            }
            return OpenSurgery.Cut(this,index,skin);
        }

        public void Tick(float seconds)
        {
            if (Stage != InternalStage.Repair) return;
            foreach (var wound in Wounds)
                if (wound.Laceration) wound.Tick(seconds);
        }

        public bool TryClose(float skin, float bleeding, float floor, int quality, out float newSkin, out float newBleeding)
        {
            newSkin = skin;
            newBleeding = bleeding;
            if (Stage != InternalStage.Closure || ClosurePools.Blocking || quality < 0 || quality > 100 ||
                !(skin >= 0 && skin <= 100) || !(bleeding >= 0) || float.IsInfinity(bleeding) || !(floor > 0)) return false;
            if (!OpenSurgery.Close(this,skin,quality,out newSkin)) return false;
            newBleeding = Math.Max(Math.Min(bleeding, floor), bleeding - TreatmentBudget);
            return true;
        }

        public bool Valid() => Cuts != null && Cuts.Length == CutCount &&
            Wounds != null && (Started
                ? (LayoutVersion == 1 || LayoutVersion == 2) && Wounds.Length >= 2 && Wounds.Length <= MaxWounds &&
                    Wounds.All(wound => wound != null && wound.Valid()) &&
                    Wounds.Any(wound => wound.Laceration) && Wounds.Any(wound => !wound.Laceration)
                : Wounds.Length == 0 || (LayoutVersion == 1 && Wounds.Length == 4 && Wounds.All(wound => wound != null && wound.Valid()))) &&
            SkinDebt >= 0 && SkinDebt <= (PlannedSkinCost ?? 10f) + .001f &&
            (!PlannedSkinCost.HasValue || (PlannedSkinCost >= 0 && PlannedSkinCost <= 60.01f)) && TreatmentBudget >= 0 && !float.IsInfinity(TreatmentBudget) &&
            GelRules.ValidIncisionCoverage(GelCoverage) && IncisionPools != null && IncisionPools.Valid() && ClosurePools != null && ClosurePools.Valid() &&
            (!Closed || (SkinDebt == 0 && Cuts.All(cut => cut) && Wounds.All(w => w.Phase == InternalWoundPhase.Repaired)));
    }
}
