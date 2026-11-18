using System;

namespace CasualtiesSurgery.Procedures
{
    public enum OpenSurgeryPhase { Incision, Repair, Closure, Complete }

    // Implement explicitly so existing save field names remain unchanged. Repair-specific
    // state stays with the procedure; incision accounting and phase gates are shared.
    public interface IOpenSurgeryState
    {
        OpenSurgeryPhase Phase { get; }
        bool[] IncisionCuts { get; }
        float[] Preparation { get; }
        float IncisionPlan { get; }
        float IncisionDebt { get; set; }
        BloodPools ActivePools { get; }
        void EnterRepair();
        void FinishClosure();
    }

    public static class OpenSurgery
    {
        public static bool CanCut(IOpenSurgeryState state, int section, float skin) =>
            state != null && state.Phase == OpenSurgeryPhase.Incision &&
            state.IncisionCuts != null && section >= 0 && section < state.IncisionCuts.Length &&
            !state.IncisionCuts[section] && !(state.ActivePools?.Blocking ?? false) && skin >= 0 && skin <= 100;

        // Procedure must initialize its plan and repair layout before calling this.
        public static float Cut(IOpenSurgeryState state, int section, float skin)
        {
            if (!CanCut(state, section, skin)) return 0;
            float plan = state.IncisionPlan;
            if (!(plan >= 0 && plan <= 60.01f)) return 0;
            float cost = IncisionSkin.CutCost(skin, plan, state.IncisionCuts.Length);
            state.IncisionDebt += cost;
            state.IncisionCuts[section] = true;
            if (Array.TrueForAll(state.IncisionCuts, cut => cut)) state.EnterRepair();
            return cost;
        }

        public static bool Disinfect(IOpenSurgeryState state, int section, float ml) =>
            state != null && state.Phase != OpenSurgeryPhase.Complete &&
            !(state.ActivePools?.Blocking ?? false) && GelRules.Disinfect(state.Preparation, section, ml);

        public static bool Close(IOpenSurgeryState state, float skin, int quality, out float healedSkin)
        {
            healedSkin = skin;
            if (state == null || state.Phase != OpenSurgeryPhase.Closure ||
                state.IncisionCuts == null || !Array.TrueForAll(state.IncisionCuts, cut => cut) ||
                (state.ActivePools?.Blocking ?? false) || !(skin >= 0 && skin <= 100) || quality < 0 || quality > 100 ||
                !(state.IncisionDebt >= 0 && state.IncisionDebt <= 60.01f)) return false;
            healedSkin = Math.Min(100, skin + IncisionSkin.Refund(state.IncisionDebt, quality));
            state.IncisionDebt = 0;
            state.FinishClosure();
            return true;
        }
    }
}
