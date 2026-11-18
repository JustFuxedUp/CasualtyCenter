using System.Runtime.CompilerServices;
using CasualtiesSurgery.Procedures;
using CUCoreLib.Saving;
using Newtonsoft.Json.Linq;

namespace CasualtiesSurgery.Game
{
    internal sealed class InternalBleedingStore : ILimbSaveProvider
    {
        static readonly ConditionalWeakTable<Limb, InternalBleedingState> States =
            new ConditionalWeakTable<Limb, InternalBleedingState>();

        static Limb Torso(Body body, Body.LimbNum section) =>
            body != null && body.limbs != null && body.limbs.Length > (int)section ? body.limbs[(int)section] : null;

        public static bool ShowsProcedure(Body body, Limb limb) => limb != null &&
            (limb == Torso(body, Body.LimbNum.UpTorso) || HasOpen(limb));

        static float RatePerPoint(Body body) => body.bloodToLiters(InternalBleedingState.NativeLossPerPoint * body.bloodScale) * 60f;

        public static float Rate(Body body) => System.Math.Min(body.internalBleeding, InternalBleedingState.MaxEffectiveBleeding) * RatePerPoint(body);

        public static bool HasOpen(Limb limb) => limb != null && States.TryGetValue(limb, out var state) &&
            state.Started && !state.Closed;

        public static string Ineligible(Body body, Limb limb, Minigame owner = null)
        {
            var reason = OperationSession.Ineligible(body, limb, owner, false);
            if (reason != null) return reason;
            return ClinicalIneligible(body,limb);
        }
        internal static string ClinicalIneligible(Body body,Limb limb)
        {
            var reason=OperationSession.ClinicalIneligible(body,limb,false);
            if(reason!=null)return reason;
            var chest = Torso(body, Body.LimbNum.UpTorso);
            var abdomen = Torso(body, Body.LimbNum.DownTorso);
            if (limb != chest && (limb != abdomen || !HasOpen(abdomen)))
                return "New internal bleeding surgery opens through the chest.";
            if (HasOpen(limb == chest ? abdomen : chest)) return "Resume the other open internal bleeding operation.";
            if (float.IsNaN(body.internalBleeding) || float.IsInfinity(body.internalBleeding) || body.internalBleeding < 0)
                return "Invalid internal bleeding value.";
            if (HasOpen(limb)) return null;
            if (!InternalBleedingState.CanStart(body.internalBleeding, InternalBleedingState.MinimumRate / RatePerPoint(body)))
                return "Internal bleeding must exceed 0.08 L/min.";
            return limb.skinHealth > 0 ? null : "No viable skin for an incision.";
        }

        public static InternalBleedingState Get(Limb limb)
        {
            if (States.TryGetValue(limb, out var prior) && prior.Closed) States.Remove(limb);
            return States.GetValue(limb, _ => new InternalBleedingState());
        }

        public static bool Contains(Limb limb, InternalBleedingState state) => limb != null &&
            States.TryGetValue(limb, out var current) && ReferenceEquals(current,state);
        public static bool OwnsPool(Limb limb, BloodPools pool)
        {
            if (limb == null || !States.TryGetValue(limb,out var state) || state.Closed) return false;
            if (state.Stage == InternalStage.Incision) return ReferenceEquals(pool,state.IncisionPools);
            if (state.Stage == InternalStage.Closure) return ReferenceEquals(pool,state.ClosurePools);
            return System.Array.Exists(state.Wounds,w => w.Phase != InternalWoundPhase.Repaired && ReferenceEquals(w.Pools,pool));
        }

        public static void Tick(Body body, float seconds)
        {
            Tick(Torso(body, Body.LimbNum.UpTorso), seconds);
            Tick(Torso(body, Body.LimbNum.DownTorso), seconds);
        }

        static void Tick(Limb limb, float seconds)
        {
            if (limb != null && States.TryGetValue(limb, out var state)) state.Tick(seconds);
        }

        public static bool TryClose(Limb limb, InternalBleedingState state, int quality, Minigame owner)
        {
            return limb!=null && Ineligible(limb.body,limb,owner)==null && TryCloseClinical(limb,state,quality);
        }
        internal static bool TryCloseClinical(Limb limb, InternalBleedingState state, int quality)
        {
            if (limb==null || quality < 0 || quality > 100 || ClinicalIneligible(limb.body, limb) != null || !States.TryGetValue(limb, out var saved) ||
                !ReferenceEquals(state, saved) ||
                !state.TryClose(limb.skinHealth, limb.body.internalBleeding, InternalBleedingState.TreatmentRate / RatePerPoint(limb.body), quality, out float skin, out float bleeding)) return false;
            SurgeryInfection.Apply(limb, state.Coverage, quality);
            limb.skinHealth = skin;
            limb.body.internalBleeding = bleeding;
            return true;
        }

        public static void LacerationMistake(Limb limb, InternalBleedingState state, Minigame owner)
        {
            if(limb==null || Ineligible(limb.body,limb,owner)!=null)return;
            LacerationMistakeClinical(limb,state);
        }
        internal static void LacerationMistakeClinical(Limb limb, InternalBleedingState state)
        {
            if (limb==null || state==null || state.Stage != InternalStage.Repair || ClinicalIneligible(limb.body, limb) != null ||
                !States.TryGetValue(limb, out var saved) || !ReferenceEquals(state, saved)) return;
            limb.muscleHealth = System.Math.Max(0, limb.muscleHealth - 1);
            if (limb.body.internalBleeding < 100)
                limb.body.internalBleeding = System.Math.Min(100, limb.body.internalBleeding + .25f);
        }

        public int GetVersion() => 2;
        public JToken Capture(Limb limb, int index) => States.TryGetValue(limb, out var state) && !state.Closed
            ? JObject.FromObject(state) : JValue.CreateNull();

        public void Restore(Limb limb, int index, JToken data, int version, SaveRestoreContext context)
        {
            if ((version != 1 && version != 2) || data == null || data.Type == JTokenType.Null) return;
            var state = data.ToObject<InternalBleedingState>();
            state?.RestoreLayout(version);
            if (state == null || !state.Valid()) throw new System.ArgumentException("Invalid saved internal operation.");
            States.Remove(limb);
            States.Add(limb, state);
        }
    }
}
