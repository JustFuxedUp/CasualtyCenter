using System;
using System.Linq;
using CasualtiesSurgery.Game;
using CasualtiesSurgery.Procedures;

static class InternalBleedingChecks
{
    public static void Run(Action<bool, string> check)
    {
        var pools = new BloodPools();
        pools.Observe(false, 0);
        check(!pools.Rolled && !pools.Blocking, "Dry task leaves its bleed roll available");
        pools.Observe(true, .1f);
        check(!pools.Suction(0, .24f), "Partial suction does not report completion");
        check(pools.First > 0 && pools.Second > 0, "Partial suction affects only its targeted pool");
        check(pools.Suction(0, .02f) && !pools.Suction(0, .25f) && pools.Suction(1, .25f),
            "Suction reports each pool's completion exactly once");
        pools.Observe(true, .1f);
        check(!pools.Blocking, "Cleared incidental pools do not regenerate");

        var rotated = new BloodPools();
        rotated.Spill();
        check(rotated.Angle(0, () => 135) == 135 && rotated.Angle(1, () => 271) == 271 &&
            rotated.Angle(0, () => 99) == 135, "Each pool keeps its independent angle while active");
        var savedRotation = Newtonsoft.Json.Linq.JObject.FromObject(rotated).ToObject<BloodPools>();
        check(savedRotation.Angle(0, () => 1) == 135 && savedRotation.Angle(1, () => 1) == 271,
            "Pool rotation survives a save round trip");
        rotated.Suction(0, .25f);
        rotated.Spill();
        check(rotated.Angle(0, () => 45) == 45, "Fresh laceration spill receives a new angle");

        var onePool = new BloodPools();
        onePool.Observe(true, .5f);
        var dryRoll = new BloodPools();
        dryRoll.Observe(true, .9f);
        check(onePool.First > 0 && onePool.Second == 0 && dryRoll.Rolled && !dryRoll.Blocking,
            "A bleeding task can receive either one pool or none");
        var legacySite = Newtonsoft.Json.Linq.JObject.Parse("{\"Id\":0,\"GelMl\":0.5}").ToObject<FragmentSite>();
        var legacyFracture = new Newtonsoft.Json.Linq.JObject().ToObject<FractureState>();
        check(!legacySite.Pools.Rolled && legacySite.GelMl == .5f && legacyFracture.Pools.Length == 3,
            "Older saves retain wound progress and default to unrolled pools");

        var body = new Body { internalBleeding = 26 };
        var chest = new Limb { body = body, skinHealth = 75 };
        var abdomen = new Limb { body = body, skinHealth = 80 };
        var arm = new Limb { body = body, skinHealth = 90 };
        body.limbs = new[] { arm, chest, abdomen };
        PlayerCamera.main = new PlayerCamera { body = body };
        MinigameBase.main = new MinigameBase();
        check(InternalBleedingStore.Ineligible(body, arm) != null &&
            InternalBleedingStore.Ineligible(body, abdomen) != null && !InternalBleedingStore.ShowsProcedure(body, abdomen) &&
            InternalBleedingStore.Ineligible(body, chest) == null && InternalBleedingStore.ShowsProcedure(body, chest),
            "New internal surgery is available through the chest only");
        float perPoint = .0057f * .025f * 60f;
        float floor = .04f / perPoint;
        float entry = .08f / perPoint;
        body.internalBleeding = entry;
        check(InternalBleedingStore.Ineligible(body, chest) != null, "A new procedure requires bleeding strictly above the rate floor");
        body.internalBleeding = entry + .01f;
        check(InternalBleedingStore.Ineligible(body, chest) == null, "A rate just above the floor is eligible");
        body.bloodScale = .5f;
        check(InternalBleedingStore.Ineligible(body, chest) != null, "Eligibility uses the patient's blood scale");
        body.bloodScale = 1;
        check(InternalBleedingState.CountForRate(.081f) == 2 && InternalBleedingState.CountForRate(.14f) == 4 &&
            InternalBleedingState.CountForRate(4) == 5, "Wound count scales from two to a maximum of five");
        check(LacerationDrag.Evaluate(31, 0, 19, 0, 108) == LacerationDragResult.Hold &&
            LacerationDrag.Evaluate(31, 0, 10, 3, 108) == LacerationDragResult.Close &&
            LacerationDrag.Evaluate(31, 0, -23, 3, 108) == LacerationDragResult.Close &&
            LacerationDrag.Evaluate(31, 0, 85, 3, 108) == LacerationDragResult.Overpull &&
            LacerationDrag.Evaluate(31, 0, 31, 95, 108) == LacerationDragResult.Overpull,
            "Held inward drag closes, including a swept crossing, while excess pull causes a mistake");

        body.internalBleeding = 26;
        check(LacerationDrag.Width(-40) == 64 && LacerationDrag.Width(24) == 48 &&
            LacerationDrag.Width(-16) == 32 && LacerationDrag.Width(2) == 12,
            "Width follows forceps distance with the open and closed widths as limits");
        var state = InternalBleedingStore.Get(chest);
        float rate = InternalBleedingStore.Rate(body);
        check(Math.Abs(rate - 25 * perPoint) < .0001f, "Severity uses the native 25-point cap");
        state.IncisionPools.Observe(true, .5f);
        check(state.Cut(0, chest.skinHealth, body.internalBleeding, rate, 7) == 0 && !state.Started,
            "Pooled blood blocks opening incision");
        state.IncisionPools.Suction(0, .25f);
        for (int i = 0; i < state.Cuts.Length; i++)
            chest.skinHealth -= state.Cut(i, chest.skinHealth, body.internalBleeding, rate, 7);
        check(state.Stage == InternalStage.Repair && InternalBleedingStore.Ineligible(body, abdomen) != null,
            "Only the original torso entry resumes its open operation");
        check(state.Wounds.Length == 5 && state.Wounds[0].Laceration && !state.Wounds[1].Laceration &&
            state.Wounds.Select(w => (w.X, w.Y, w.Angle)).Distinct().Count() == 5, "High bleeding generates five distinct saved wound positions");
        check(state.Wounds.Any(w => w.Angle > 180) &&
            state.Wounds.All(w => w.Angle >= 0 && w.Angle < 360) &&
            state.Wounds.Select((w, i) => w.Angle == new FragmentBoard(7, 5).Layout(i).Angle).All(match => match),
            "Internal wounds use the same full-angle rotation as shrapnel");
        var otherLayout = new InternalBleedingState();
        otherLayout.Cut(0, 100, 26, rate, 8);
        check(!otherLayout.Wounds.Select(w => (w.X, w.Y, w.Angle)).SequenceEqual(state.Wounds.Select(w => (w.X, w.Y, w.Angle))),
            "A different board seed changes wound placement");
        InternalBleedingStore.LacerationMistake(chest, state, null);
        check(chest.muscleHealth == 99 && body.internalBleeding == 26.25f && chest.bleedAmount == 0,
            "Laceration mistake adds minor internal bleeding and muscle damage");
        check(state.Disinfect(0, .2f) && state.Disinfect(0, 4) &&
            !state.Disinfect(0, .1f) && Math.Abs(state.Coverage - 1f / 9) < .001f,
            "Internal incision uses the fracture gel cap and coverage");
        var wound = state.Wounds[0];
        var neighbor = state.Wounds[2];
        state.Tick(5);
        float neighborBefore = neighbor.Seconds;
        check(wound.Pinch() && wound.Seconds == 6 && neighbor.Seconds == neighborBefore && neighbor.Pools.Blocking,
            "Closing a laceration grants six seconds without resetting its neighbor");
        state.Tick(5);
        check(wound.Phase == InternalWoundPhase.Pinched && wound.Seconds == 1, "Unstitched pinch holds before deadline");

        neighbor.Pools.Suction(0, .1f);
        var store = new InternalBleedingStore();
        var positions = state.Wounds.Select(w => (w.X, w.Y, w.Angle)).ToArray();
        var saved = store.Capture(chest, 1);
        store.Restore(chest, 1, saved, store.GetVersion(), null);
        state = InternalBleedingStore.Get(chest);
        wound = state.Wounds[0];
        check(wound.Phase == InternalWoundPhase.Pinched && wound.Seconds == 1 && Math.Abs(state.SkinDebt - 45) < .001f && state.PlannedSkinCost == 45,
            "Save preserves wound timer and incision debt");
        check(state.Wounds.Select(w => (w.X, w.Y, w.Angle)).SequenceEqual(positions), "Save preserves random wound placement");
        check(Math.Abs(state.Wounds[2].Pools.First - .15f) < .001f, "Save preserves partial suction");
        check(Math.Abs(state.Coverage - 1f / 9) < .001f, "Save preserves internal incision disinfection");
        state.Tick(1);
        check(wound.Phase == InternalWoundPhase.Open && wound.Pools.Blocking && !wound.Pinch() && !wound.Stitch(),
            "Expired pinch reopens and pooled blood blocks repair");
        wound.Pools.Suction(0, .25f);
        check(wound.Pinch() && wound.Stitch(), "Cleared laceration can be pinched and permanently sutured");
        state.Tick(6);
        check(!wound.Pools.Blocking && wound.Phase == InternalWoundPhase.Repaired, "Sutured laceration stops spilling");
        foreach (var lesion in state.Wounds.Where(w => w.Phase != InternalWoundPhase.Repaired))
        {
            if (lesion.Laceration)
            {
                check(lesion.Pools.Blocking, "Untreated lacerations spill independently");
                lesion.Pools.Suction(0, .25f);
                check(lesion.Pinch() && lesion.Stitch(), "Other lacerations repair independently");
            }
            else
            {
                check(!lesion.Gel(.5f) && lesion.Phase == InternalWoundPhase.Open, "Partial gel does not finish a bruise");
                check(lesion.Gel(1.5f) && !lesion.Gel(2), "Bruise completion is reported exactly once");
            }
        }
        state.ClosurePools.Observe(true, .5f);
        check(!InternalBleedingStore.TryClose(chest, state, 65, null), "Pooled blood blocks final closure");
        state.ClosurePools.Suction(0, .25f);
        var alreadyBelow = Newtonsoft.Json.Linq.JObject.FromObject(state).ToObject<InternalBleedingState>();
        check(alreadyBelow.TryClose(75, 4, floor, 65, out _, out float remained) && remained == 4,
            "Closure never raises bleeding that already fell below the floor");
        var poorClosure = Newtonsoft.Json.Linq.JObject.FromObject(state).ToObject<InternalBleedingState>();
        check(poorClosure.TryClose(30, 26, floor, 0, out float partialSkin, out _) &&
            Math.Abs(partialSkin - 52.5f) < .001f, "Bad internal closure refunds half of the recorded skin debt");
        var newBleeding = Newtonsoft.Json.Linq.JObject.FromObject(state).ToObject<InternalBleedingState>();
        check(newBleeding.TryClose(75, 43, floor, 65, out _, out float left) && left == 17,
            "Closure preserves newer bleeding above the episode's treatment budget");
        body.internalBleeding = 30;
        chest.skinHealth -= 2;
        check(!InternalBleedingStore.TryClose(abdomen, state, 65, null), "Wrong torso cannot collect treatment");
        check(InternalBleedingStore.TryClose(chest, state, 65, null) &&
            Math.Abs(InternalBleedingStore.Rate(body) - .04f) < .0001f &&
            Math.Abs(chest.skinHealth - 73) < .001f, "Closure refunds actual skin debt and stops at 0.04 L/min");
        check(!InternalBleedingStore.TryClose(chest, state, 65, null), "Closure applies once");

        var legacy = new InternalBleedingState
        {
            Cuts = Enumerable.Repeat(true, 9).ToArray(),
            Wounds = new[] { new InternalWound { Seconds = 12 }, new InternalWound(),
                new InternalWound { GelMl = .5f }, new InternalWound() },
            TreatmentBudget = 26
        };
        store.Restore(abdomen, 2, Newtonsoft.Json.Linq.JObject.FromObject(legacy), 1, null);
        var migrated = InternalBleedingStore.Get(abdomen);
        check(InternalBleedingStore.Ineligible(body, abdomen) == null && InternalBleedingStore.ShowsProcedure(body, abdomen) &&
            InternalBleedingStore.Ineligible(body, chest) != null, "An existing abdomen incision remains resumable");
        check(migrated.Wounds[0].Angle == 0 && migrated.Wounds[0].Laceration && migrated.Wounds[0].X == -145 && migrated.Wounds[0].Seconds == 6 &&
            !migrated.Wounds[2].Laceration && migrated.Wounds[2].GelMl == .5f,
            "Legacy operation keeps its original wound positions and progress under the new deadline");
    }
}
