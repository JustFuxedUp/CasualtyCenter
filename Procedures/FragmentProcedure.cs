using System;

namespace CasualtiesSurgery.Procedures
{
    public enum FragmentStage { Extraction, Suture, Completed, Cancelled }

    // Pure rules only: the future game adapter must validate the patient, limb,
    // inventory and authority, then apply a returned result exactly once.
    public sealed class FragmentProcedure
    {
        public FragmentStage Stage { get; private set; } = FragmentStage.Extraction;

        public bool RecordSuccessfulExtraction()
        {
            if (Stage != FragmentStage.Extraction) return false;
            Stage = FragmentStage.Suture;
            return true;
        }

        public void Cancel()
        {
            if (Stage != FragmentStage.Completed) Stage = FragmentStage.Cancelled;
        }

        // Values must be freshly read at completion, not captured at operation start.
        // Completion consumes one fragment and awards both benefits together.
        public bool TryCompleteSuture(int fragments, float skin, float bleeding,
            float skinRecovery, float bleedingReduction, out FragmentOutcome outcome)
        {
            outcome = default(FragmentOutcome);
            if (Stage != FragmentStage.Suture || fragments <= 0 ||
                !Finite(skin) || !Finite(bleeding) || !Finite(skinRecovery) ||
                !Finite(bleedingReduction) || skin < 0 || skin > 100 || bleeding < 0 ||
                skinRecovery <= 0 || bleedingReduction <= 0)
                return false;

            outcome = new FragmentOutcome(fragments - 1,
                skin + Math.Min(skinRecovery, 100f - skin),
                bleeding - Math.Min(bleedingReduction, bleeding));
            Stage = FragmentStage.Completed;
            return true;
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }

    public readonly struct FragmentOutcome
    {
        public int Fragments { get; }
        public float SkinHealth { get; }
        public float Bleeding { get; }
        public FragmentOutcome(int fragments, float skinHealth, float bleeding)
        {
            Fragments = fragments;
            SkinHealth = skinHealth;
            Bleeding = bleeding;
        }
    }
}
