# 0.14.2 two-instance test checklist

Historical notes. For the current installed/package build, use [the 0.15.0 testing guide](TEST-0.15.0.md).

## Pending unreleased pass

The source retains version 0.14.2 at the user's request but differs from the installed/package DLL. No new package or installation was made. Use matching verified builds on both peers when release work resumes; STATUS records hashes. Automated checks: 492 procedure plus 23 real-Harmony checks. Live acceptance is pending:

1. Start a fresh session and open each surgery BEFORE opening any vanilla medical minigame, as both host and client. Expect native Start entered without the vanilla prerequisite.
2. Inspect the other player: OPEN/SUTURE markers should update in both directions and clear after closure. Switch bodies while waiting for replies; markers must not cross patients. Walk away/disconnect: stale remote markers must disappear.
3. With ConsentForFeeding enabled, request surgery on a conscious teammate. Patient receives a native consent prompt; surgeon sees a waiting notice. Accept and reselect surgery. Decline/timeout must leave health unchanged. Verify host-as-patient and client-as-patient separately.
4. Repeat with an unconscious patient: no consent prompt, conscious surgeon required. If the patient wakes without prior consent, further treatment must stop. Turning ConsentForFeeding off permits nearby treatment without prompts.
5. Attempt competing procedures on the same patient, including different limbs. Exactly one operator owns the patient; the competing request must not cancel or steal the first lease.
6. Move either participant, disconnect, change scenes, and resume. Saved cuts, gel, wound IDs and skin debt survive; gestures and approval do not. Fresh consent is required for a conscious patient's new operation.
7. Measure skin, bone timer and internal bleeding before/after treatment on each role, including 0.04 L/min successful internal bleeding treatment. Confirm changes affect the patient rather than surgeon. Repeat interruption/reconnect and check for duplicated rewards.
8. Try treatment through a wall while within range, then obstruct an accepted operation. Neither consent nor unconsciousness should bypass blocked access. Try answering an old prompt after timeout, disconnect, or respawn: it must not authorize the new body.
9. Restore a save with an open incision and partly treated fragments, then resume. Debt, gel, fragment IDs, pending sutures and partial suction must persist. Repeated respawns/reconnects must not eventually report router capacity exhaustion. Automated recovery coverage now brings the procedure suite to 492 checks; the unchanged startup harness remains at 23.

Simultaneous surgeons and a spectator surgery board remain out of scope. The historical checklist below describes the installed diagnostic build, not these unreleased features.

0.14.2 adds permanent event-level startup logging; it does not claim to fix the silent entry failure reported with 0.14.1. Both peers should update. After one attempted operation, preserve both logs. Expected sequence: Surgery entry menu opened / clicked / validation accepted / begin / calling native StartMinigame; Surgery Together startup decision; Surgery native Start entered; Authority view opened; native startup accepted; Authority stage. A refusal records its boundary and reason. Together's decision includes its pre-call occupied and ignore-next state. Existing cleanup logs mark the end. No per-frame/input-sample logging is added. Verification: 18 real-Harmony lifecycle checks and 435 procedure checks passed; plugin build has zero warnings/errors. Harness NU1900 vulnerability-feed warning remains.

Updated 2026-11-17. This is an **opt-in integration test build**, not a multiplayer compatibility certification. Automated verification: 435 procedure checks plus 13 real-Harmony lifecycle checks; plugin Release build with zero warnings/errors. The new .NET Framework harness build reported NU1900 because NuGet vulnerability data was unreachable; it built and ran successfully. Actual Unity/Together retesting remains pending.

0.14.1 fixes the observed 0.14.0 startup exception: Together tried to look up NativeAuthorityMinigame in its native minigame-ID table. A scoped guard now keeps native screen startup/cleanup and Together's startup reset, but skips its native start/end notifications only for our independently networked surgery views. It does not rebuild or renumber Together's IDs. The harness reproduces the original exception using actual HarmonyX patches before verifying the guard; it does not run Unity or network sockets.

**Immediate retest:** update both peers, keep EnableAuthorityViews=true, confirm the new startup log prints the effective flags and config path, then open a fragment operation on host and client. Expect “Authority view opened” followed by “Authority stage”, with no missing-type-ID exception. Then test one cut/exit/resume and a vanilla minigame to confirm unrelated minigames still work. If the client still cannot enter, report the health menu's exact eligibility text along with both logs.

## Setup

1. Use separate test saves/profiles for the two instances. Keep your normal save available separately. Both peers need the **same 0.14.2 DLL**, CUCoreLib 1.1 and matching Together binaries. Together's loader and internal version labels differed in the supplied logs, so compare DLL hashes rather than relying only on those labels. Do not mix this build with 0.14.0 or the earlier development backend under version 0.13.8.
2. With each game closed, replace its existing CasualtiesSurgery.dll; keep exactly one loadable copy per instance. Keep the supplied Audio folder beside it. The package contains the DLL, eight local test WAVs and this checklist; it does not include game/native art or dependencies.
3. In each instance's `BepInEx/config/local.casualties.surgery.cfg`, set the following in its existing `[Development]` section (add the section if absent), then restart:

   ```ini
   [Development]
   EnableAuthorityViews = true
   ```

   This also enables the surgery transport. `EnableSurgeryTransport` alone still does not unlock the test views. With EnableAuthorityViews=false, the prior singleplayer views remain selected and multiplayer surgery stays blocked. The package/installation does not automatically edit this setting.
4. Confirm both logs say **Casualty Center 0.14.2 loaded**, with EnableAuthorityViews=True and the intended config path. In a multiplayer session, look for **Surgery development transport active**. Do not continue if there is a transport/API initialization error. Together's native health-sync switch must remain enabled; its inspected default is enabled, and the plugin does not force it on.
5. Test self-treatment on each peer. Treating another player's body and watching their custom surgery board are not implemented. Native health synchronization is separate from the operator's custom board. Keep the log from each instance, identifying which is host/client.

Losing application focus deliberately closes surgery and preserves progress. When switching between two windows on one PC, count this as an interruption; do not expect the first window to keep operating in the background. Test simultaneous held input on two machines if necessary.

## First: connection and one-cut smoke test

Stop here and send the logs if a step fails; there is no need to run the entire matrix first.

| Step | Expected |
| --- | --- |
| Singleplayer with the switch enabled: open an eligible fracture operation | Native minigame opens, then shows the nine cut dots; no separate debug window |
| Join multiplayer and repeat as the host, then as the client | Brief “Connecting surgery…” resolves; both can operate on their own body |
| Gel one section, then cut one dot | Coverage appears only after acceptance; one dot disappears; one incision skin cost, no repeated loss while held on that completed dot |
| Exit, wait briefly, reopen the same limb | Same cut and gel coverage; no rerolled layout or repeated incision charge |
| Watch the health dummy after exit | Host-backed [OPEN] marker appears; it clears after successful outer closure |

Capture any immediate close, stuck Connecting screen, missing hand, missing window, native minigame denial or repeated “Authority input reset” log before proceeding.

## Full procedure passes

Run each procedure once as host and once as client. Note starting and ending native health values where available; native network compression means tiny displayed differences are not automatically a failure.

### Fracture

- Gel the full incision, cut all nine dots, align both pieces and suture the outer incision.
- Bones remain 56 units wide, extend/pivot outside the frame, and accept release within five degrees. Releasing clearly farther away produces one mistake and the existing muscle/pain/bone-timer consequence.
- Try both hand slots, rotated grips and a changed interface scale. Knife tip, tool trail and native contact must still coincide. The selected tool should survive transitions and reopening.
- Verify skin loss/refund uses actual incision debt. Bone healing time should be reduced once, with natural healing still required; exit/reopen must not grant a second refund/reduction.
- Hold the scalpel away from objectives long enough to exceed its grace period: damage and mistake feedback should occur without rapidly multiplying under network delay.

### Internal bleeding

- Enter through the chest above 0.08 L/min. Complete incision preparation/opening, the generated bruises/lacerations, and outer closure. Successful treatment targets 0.04 L/min; new injury during the operation must not be erased by restoring old whole-body values.
- Confirm saved scattered positions, full-angle rotations, labels and wound count up to five. Bruises turn healed after gel; lacerations shrink visually as the forceps approach the center.
- Grip a laceration's long edge and drag inward. Closure must produce Good once. Drag too far outward to verify one minor bleeding/muscle mistake. Merely clicking the center must not close it.
- Close two lacerations at different times. Each should reopen about six host seconds after its own closure if left unsutured. Closing one must not reset the other's timer.
- Stitch a closed laceration before its timer expires. Temporary drawn thread disappears on release/cancel; completed stitched artwork remains. If a timer expires mid-stroke, the stale stitch must not heal the reopened wound.
- Leave during repair and return after six seconds. Timers/pools should have continued on the host; cancelling must not heal the operation or erase the incision.

### Fragments

- Extract a shard, then close one wound with sutures and another with gel. Pull until loose, move the loose shard around, then release. Loose-state transition must survive even a quick pull/release.
- Verify native shard artwork, masking, randomized rotation and saved positions. For more than twelve sites, check PgUp/PgDn and retained IDs/pages after reopening.
- An invalid pull should cause the existing small skin/bleeding mistake once. A successful extraction alone must not grant the full closure reward. Suture/gel completion must remove one fragment and grant one skin/bleeding reward.
- Start without bleeding and make an extraction mistake: it must not suddenly roll new pools during that same entry. Existing pools and a fresh entry are separate cases.
- Try different SurgeryScale settings on host/client. Fragment geometry follows the host-provided scale so gel hit detection matches the rendered wound.

## Feedback and interruption checks

| Check | Required result |
| --- | --- |
| Every visible pool slot: fragment, fracture incision/alignment/closure, internal incision/repair/closure | Each pool clears after roughly 0.25 seconds of accepted suction plus transport delay; **one Good indicator and completion sound per slot** |
| Bruise gel, fragment gel closure, laceration closure | Good completion, not an accuracy rating; partial dose has no completion event |
| Gel/scalpel/suction hold, release, tool wheel, exit, focus loss | Correct loop starts/stops; no loop survives the view |
| Suture completion, invalid stroke, wheel change, timer expiry and exit | No player-drawn permanent thread; final repaired-wound art remains where applicable |
| Close the view immediately after a grip, cut, suction request or suture release | The acknowledged action may finish once; queued future work is cancelled and the lease releases; no duplicate reward |
| Open a different limb after interrupting an incision | No cross-limb gesture/debt/gel state; original incision remains resumable |
| Hold a tool during a delayed reply, then release/change tools | No endless stuck grip, continued gel dosing or discarded release; a rejected gesture resets safely |

The main board and scores are host-confirmed. Temporary hand/shard/bone motion and tool trails are local presentation, so a correction after a reply is possible; report severe snapping or unresponsive controls.

## Disconnect, reload and native replication

1. Client disconnects after several cuts, after pinching a laceration, during suction, and while awaiting closure. Reconnect and reopen: completed work, debt, layout and doses remain; no extra refund. The host continues laceration timers while its patient remains valid.
2. Save/reload on the host with an open operation. The client rejoins and resumes: [OPEN]/[SUTURE] markers, saved IDs, gel and wounds are correct. Do not judge success solely by the client's pre-disconnect view.
3. End the host session during surgery. The view must stop safely; no host migration or client-side healing should occur. Reconnecting creates a fresh session rather than replaying an old completion.
4. During a pending operation, exercise external injury/healing, death/unconsciousness or limb loss if convenient with your existing test setup. Expect current-health validation and interruption, with no cached full-health overwrite.
5. Compare available native health readouts on host/client after a completed wound/operation and after several seconds of ordinary play. Watch specifically for a successful heal snapping back, repeated skin refunds, fragment counts disagreeing, or bleeding returning to an old value. Report any native readout you cannot inspect rather than inferring success.
6. If you already have a delay/loss test setup, repeat one extraction and one outer closure. Duplicate/retry events must not repeat rewards or completion sounds. Automated tests cover packet retry logic, but they cannot certify Together's native health confirmation.

## Report format

For each failure send:

- host/client role, DLL version/hash, Together version, and whether it also occurs in singleplayer with the test switch;
- procedure, limb, stage, tool, and exact actions immediately before failure;
- expected versus observed result, including before/after health if relevant;
- whether focus loss, pause, reconnect, save/reload or external healing occurred;
- both `BepInEx/LogOutput.log` files from that run (preserve them before relaunch), plus a short clip/screenshot when geometry or sound is involved.

Useful surgery log lines begin “Authority view opened”, “Authority stage”, “Authority result”, or “Authority input reset”. Record pass/fail separately for host and client; a host-only pass does not verify client replication.
