using System;
using CasualtiesSurgery.Procedures;

namespace CasualtiesSurgery.Game
{
    // Clinical application boundary. Presentation submits actions here; a future host adapter
    // must resolve identities and validate gesture evidence before invoking these methods.
    internal sealed class SurgeryActions
    {
        readonly Limb limb;
        readonly Minigame owner;
        readonly Func<string> invalid;
        readonly ProcedureKind procedure;
        readonly bool authoritative;
        public SurgeryActions(Limb limb, Minigame owner, Func<string> invalid, ProcedureKind procedure)
        { this.limb = limb; this.owner = owner; this.invalid = invalid; this.procedure=procedure; }
        internal SurgeryActions(Limb limb, ProcedureKind procedure, Func<string> authorityGuard)
        { this.limb=limb; this.procedure=procedure; invalid=authorityGuard ?? throw new ArgumentNullException(nameof(authorityGuard)); authoritative=true; }
        bool Ready => invalid() == null && (authoritative ? OperationSession.ClinicalIneligible(limb?.body,limb,false) : OperationSession.Ineligible(limb?.body, limb, owner, false)) == null;
        static bool Positive(float value) => value > 0 && !float.IsInfinity(value);
        bool Current(FractureState state) => procedure==ProcedureKind.Fracture && FractureStore.Contains(limb,state);
        bool Current(InternalBleedingState state) => procedure==ProcedureKind.InternalBleeding && InternalBleedingStore.Contains(limb,state);
        bool OwnsPool(BloodPools pools) => pools!=null && (procedure==ProcedureKind.Fracture ? FractureStore.OwnsPool(limb,pools) :
            procedure==ProcedureKind.InternalBleeding ? InternalBleedingStore.OwnsPool(limb,pools) : FragmentBoardStore.OwnsPool(limb,pools));

        public void Observe(BloodPools pools, bool bleeding)
        {
            if (!Ready || !OwnsPool(pools)) return;
            if (!pools.Rolled && bleeding) pools.Observe(true, UnityEngine.Random.value);
            PrepareAngles(pools);
        }
        public void Prepare(BloodPools pools)
        { if (Ready && OwnsPool(pools)) PrepareAngles(pools); }
        static void PrepareAngles(BloodPools pools)
        {
            for (int i = 0; i < 2; i++)
                if (pools.Remaining(i) > 0) pools.Angle(i, () => UnityEngine.Random.value * 360);
        }
        public bool Suction(BloodPools pools, int slot, float seconds) =>
            Ready && OwnsPool(pools) && Positive(seconds) && pools.Suction(slot, seconds);

        public void Damage(float seconds)
        { if (Ready && Positive(seconds)) FractureStore.ScalpelDamage(limb, seconds); }

        public bool Cut(FractureState state, int section)
        {
            if (!Ready || !Current(state) || section < 0 || section >= state.Cuts.Length ||
                state.Stage != FractureStage.Incision || state.Cuts[section] || state.Pools[0].Blocking) return false;
            limb.skinHealth -= state.Cut(section, limb.skinHealth);
            return true;
        }
        public bool Cut(InternalBleedingState state, int section)
        {
            if (!Ready || !Current(state) || section < 0 || section >= state.Cuts.Length ||
                state.Stage != InternalStage.Incision || state.Cuts[section] || state.IncisionPools.Blocking) return false;
            int seed = state.Started ? 0 : Guid.NewGuid().GetHashCode() & int.MaxValue;
            limb.skinHealth -= state.Cut(section, limb.skinHealth, limb.body.internalBleeding, InternalBleedingStore.Rate(limb.body), seed);
            return state.Cuts[section];
        }
        public bool Disinfect(FractureState state, int section, float ml)
        {
            if (!Ready || !Current(state) || !Positive(ml) ||
                state.Stage == FractureStage.Complete || state.Pools[(int)state.Stage].Blocking || !state.Disinfect(section, ml)) return false;
            limb.SetDisinfect(60); return true;
        }
        public bool Disinfect(InternalBleedingState state, int section, float ml)
        {
            if (!Ready || !Current(state) || !Positive(ml) ||
                state.Closed || (state.Stage == InternalStage.Incision && state.IncisionPools.Blocking) ||
                (state.Stage == InternalStage.Closure && state.ClosurePools.Blocking) || !state.Disinfect(section, ml)) return false;
            limb.SetDisinfect(60); return true;
        }
        public void Advance(FractureState state, float seconds)
        {
            if (!Ready || !Current(state)) return;
            if (state.Started && state.Stage == FractureStage.Incision && Positive(seconds)) state.CutSeconds += seconds;
            if (!limb.broken && state.Started && state.Stage == FractureStage.Alignment) state.Stage = FractureStage.Closure;
        }
        public bool Rotate(FractureState state, int bone, float angle)
        {
            if (!Ready || !Current(state) || state.Stage != FractureStage.Alignment ||
                bone < 0 || bone >= state.Angles.Length || state.Aligned[bone] || state.Pools[1].Blocking ||
                float.IsNaN(angle) || float.IsInfinity(angle)) return false;
            state.Angles[bone] = angle; return true;
        }
        public bool Align(FractureState state, int bone)
        {
            if (!Ready || !Current(state) || state.Stage != FractureStage.Alignment ||
                bone < 0 || bone >= state.Angles.Length || state.Aligned[bone] || state.Pools[1].Blocking) return false;
            if (state.Align(bone)) return true;
            FractureStore.AlignmentMistake(limb); return false;
        }
        bool Wound(InternalBleedingState state, int index, bool laceration) => Ready &&
            Current(state) && state.Stage == InternalStage.Repair &&
            index >= 0 && index < state.Wounds.Length && state.Wounds[index].Laceration == laceration;
        public bool Bruise(InternalBleedingState state, int index, float ml) =>
            Wound(state, index, false) && Positive(ml) && state.Wounds[index].Gel(ml);
        public bool Pinch(InternalBleedingState state, int index) => Wound(state, index, true) && state.Wounds[index].Pinch();
        public bool Stitch(InternalBleedingState state, int index) => Wound(state, index, true) && state.Wounds[index].Stitch();
        public void Overpull(InternalBleedingState state, int index)
        { if (Wound(state, index, true) && state.Wounds[index].Phase == InternalWoundPhase.Open && !state.Wounds[index].Pools.Blocking)
            { if(authoritative)InternalBleedingStore.LacerationMistakeClinical(limb,state);else InternalBleedingStore.LacerationMistake(limb,state,owner); } }
        public bool Close(FractureState state, int quality) => Ready && Current(state) &&
            (authoritative ? FractureStore.TryCloseClinical(limb,quality) : FractureStore.TryClose(limb, quality, owner));
        public bool Close(InternalBleedingState state, int quality) => Ready && Current(state) &&
            (authoritative ? InternalBleedingStore.TryCloseClinical(limb,state,quality) : InternalBleedingStore.TryClose(limb, state, quality, owner));

        public bool FragmentGel(FragmentBoard board, FragmentSite site, float ml)
        {
            if (!Ready || procedure!=ProcedureKind.Fragment || board == null || site == null || !Positive(ml) || !board.Sites.Contains(site) || site.Completed || !site.Extracted || site.Pools.Blocking ||
                !FragmentBoardStore.Contains(limb,board)) return false;
            site.GelMl = GelRules.Deposit(site.GelMl, ml, GelRules.FragmentMl);
            limb.SetDisinfect(30); return true;
        }
        public bool Extract(OperationSession session, FragmentSite site, int quality)
        {
            if (!Ready || procedure!=ProcedureKind.Fragment || session == null || session.Limb != limb || session.Patient != limb.body || site == null || quality < 0 || quality > 100 || site.Completed || site.Extracted || site.Pools.Blocking ||
                !FragmentBoardStore.Get(limb).Sites.Contains(site) || !session.Extract()) return false;
            site.Extracted = true; site.ExtractionScore = quality; return true;
        }
        public bool Complete(OperationSession session, FragmentSite site, int quality, bool gel)
        {
            if (!Ready || procedure!=ProcedureKind.Fragment || session == null || session.Limb != limb || session.Patient != limb.body || site == null || quality < 0 || quality > 100 || site.Completed || !site.Extracted || site.Pools.Blocking) return false;
            var board = FragmentBoardStore.Get(limb);
            if (!board.Sites.Contains(site) || (gel && site.GelMl < GelRules.FragmentMl) || !session.CompleteContinuousSuture()) return false;
            board.Complete(site.Id);
            if (!gel) SurgeryInfection.Apply(limb, site.GelMl / GelRules.FragmentMl, (site.ExtractionScore + quality) * .5f);
            return true;
        }
    }
}
