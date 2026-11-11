# Gel, tool trails and usage counters — 0.12.0

## Controls

The four-tool wheel is now Forceps right, Suture up, Scalpel left, Gel down. Hold right mouse and release toward the tool. Gel uses the native grasp-hand visuals from the organic dislocation minigame. Selected tools remain unchanged between stages and operations.

Holding Gel dispenses 2 mL per second. Hold over an extracted fragment wound to accumulate a 2 mL dose and close it instead of suturing. This gives the existing one-fragment removal, +5 skin and −1 bleeding reward (or configured recovery values); it cannot remove an embedded fragment, close a fracture incision, or repeat a completed wound's reward. Partial dose is saved per wound.

For a fracture incision, move Gel along the central incision guide before cutting, or over it while the operation is open. Each of nine sections takes 0.4 mL, totaling 3.6 mL of productive application. Sections become sea-green and the header shows overall disinfected percentage. Coverage is stored per limb and survives interruption/save/load. It persists for this operation in the prototype. The normal game antiseptic timer is refreshed to 60 seconds by productive incision application, or 30 seconds by productive fragment application; that timer still decays normally.

Gel leaves very transparent green square particles, ready to replace with texture variants later. A thin, translucent red trail follows held suturing and scalpel motion and fades in 0.35 seconds. Permanent thread stays above the trail and wound art. The scalpel hit point and red trail use the visible native knife tip rather than the hand center. The inspected handSharp/handSharpalt sprite is 256 x 630, with the tip near x=60 at the top; conversion respects aspect scaling, hand rotation and mirroring. No game artwork is redistributed.

## Debug consumption

The bottom line shows **Suture: … cm** and **Gel: … mL**, explicitly for this open session. Suture uses a generous prototype conversion of 100 logical UI units traveled to 1 cm, counting held suture motion whether or not it succeeds. Gel counts all dispensed volume, including off-target waste. Neither consumes inventory yet. Counters reset when the screen closes; actual partial doses and incision coverage are saved independently and do not reset.

## Surgery-related infection

Closing an untreated or partly treated operation can start infection or increase an existing one. Full gel coverage suppresses this additional surgery roll. Native infection progression and ordinary world infection checks remain active; gel is not blanket immunity or an instant infection cure.

Prototype chance = 0.30 × untreated fraction × immunity factor × accuracy factor × native infection multipliers, clamped to 0–100%. Immunity factor is max(0.25, 2 − immunity/100), with immunity clamped to its native 0–200 range. Accuracy factor is 1.5 − overall score/100. Fracture overall accuracy averages incision, recorded bone-alignment results and closure; fragment sutures average extraction and closure. A successful gel fragment closure is fully treated, so has no additional surgery infection roll.

On a failed infection roll, the limb's native infected flag is set and infectionAmount rises by 1–5 points depending on accuracy, capped at 100. Existing infection is never cleared directly. Both the patient's infectionChanceMultiplier and the native infectionchance run setting are respected. If infection is disabled, surgery does not override that. The added risk is committed only at successful closure, never on failed sutures or repeatedly on completed wounds.

The relevant native fields were inspected in the installed game: Body.immunity (0–200), infectionChanceMultiplier, Limb.infected, infectionAmount, disinfectionTime and SetDisinfect. Native GetInfectionSpeed uses immunity and reduces growth while disinfectionTime is positive. Old saves load with zero gel coverage and default extraction score 100 where that score was not previously tracked.

## Acceptance pass

1. Check all four tool sectors, grasp-hand gel visuals and the footer. Hold Gel off-target: the mL counter should increase without healing.
2. Extract a shard and apply Gel over its wound for about one second. It should close once with the normal reward. Interrupt at half dose, save/reload and finish; dose must persist without early healing.
3. Coat the whole fracture incision. Confirm green squares, sea-green sections and 100% coverage. Resume after exit/save: coverage stays. Bone work and final suture are still required.
4. Compare treated and untreated closures on test saves with low/high immunity. The outcome is probabilistic unless risk is zero; review native infection values rather than assuming every untreated surgery must infect.
5. Confirm thin red trails follow both sutures and the actual blade tip, fade quickly and remain beneath permanent thread. Check both hand slots and different screen scales. Inspect the corrected tip before judging scalpel hit accuracy.
6. Verify counters reset on a new screen but paid gel doses do not. No inventory resources should be deducted yet.

Internal-bleeding surgery is the next separate procedure; it is not implemented by this update. These checks exercise the actual rule and save adapters, but visual fit, native hand feel and infection progression over real game time still need gameplay testing.
