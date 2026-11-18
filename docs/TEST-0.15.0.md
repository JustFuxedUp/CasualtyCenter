# Casualty Center 0.15.0 multiplayer test

Prepared 2026-11-18. Use this checklist for the 0.15.0 package. This is an integration test build, not a multiplayer compatibility certification. Automated checks: 492 procedure/session checks and 23 real-Harmony lifecycle checks. Plugin Release build: zero warnings/errors. The Harmony harness has a NuGet vulnerability-feed availability warning, not a failed test.

## Install and establish the baseline

1. Close each game. Replace the existing CasualtiesSurgery.dll with the packaged DLL and keep Audio beside it. Do not add a second copy elsewhere under BepInEx/plugins. The host's existing folder may still be named CasualtyCenter-0.14.0-multiplayer-test; its folder name does not determine the loaded version.
2. Both peers need identical surgery DLLs, compatible CUCoreLib 1.1 and matching Together binaries. The manifest records the surgery DLL hash; Together's loader/internal version labels differed in earlier logs, so compare the Together DLL hashes if uncertain. Dependencies are not included in this package. Keep the current mod set unchanged for the first run and record any differences.
3. In BepInEx/config/local.casualties.surgery.cfg, keep EnableAuthorityViews=true under [Development]. Keeping EnableSurgeryTransport=true is fine; AuthorityViews also enables transport. Installation does not change these settings. Both logs should show Casualty Center 0.15.0 loaded and the effective flags/config path.
4. Enable Together's Consent for Feeding rule initially. Surgery uses that rule: conscious remote patients must approve; unconscious patients do not prompt. With the rule off, nearby remote treatment is permitted without approval. No separate surgery host-menu rule was added.
5. Use a disposable test save. Begin with the tutorial map as planned. Record tutorial, host/client roles, injury setup method and any commands used. Never heal, reinjure or change relevant settings in the middle of a measurement unless that is the specific test.

Tutorial is useful for repeatable entry, consent, gestures, marker and synchronization tests. Given the reported tutorial death caps, do not count survival as proof that a death/disconnect safety check works. If the tutorial prevents unconsciousness, waking, death, respawn, or a normal save transition, record that case as NOT EXERCISED and repeat it on a normal test map later. Earlier client logs contained Together tutorial/settings errors; preserve them with the surgery logs, but do not assume they caused a surgery failure.

## First run: prove startup without the workaround

Do this before any vanilla medical minigame. If one opens accidentally, restart both game processes and repeat this section.

| ID | Action | Expected |
| --- | --- | --- |
| S1 | Host right-clicks an injured limb and starts self-surgery | Surgery opens without first clicking a shard or playing a vanilla minigame |
| S2 | Client repeats on their own injured limb | Same result; no missing-type-ID error or silent menu close |
| S3 | Each completes one incision cut, exits, then reopens | Cut remains completed; no repeated skin charge or rerolled wound layout |
| S4 | After S1-S3, play a vanilla medical minigame | Native minigame still starts and ends normally |

Expected log sequence: Surgery entry clicked/validation accepted/begin/calling native StartMinigame; Surgery Together startup decision allowed=True; Surgery native Start entered; Authority view opened; native startup accepted; Authority stage. If startup fails, stop this run and send both logs before relaunch. Note the exact menu notice and last action; do not mask failure by opening a vanilla minigame.

## Second run: one surgeon treating another player

Run every case in both directions: HOST surgeon -> CLIENT patient, then CLIENT surgeon -> HOST patient. Stay close with a clear path. Only one surgeon may operate on a patient at a time, across all limbs/procedures. The patient does not get a spectator minigame board.

| ID | Action | Expected |
| --- | --- | --- |
| C1 | Request treatment of a conscious patient, then decline | Patient sees a native surgery request. No incision or health change. Surgeon receives a waiting/refusal notice. Declines have a short cooldown before a new prompt |
| C2 | Request, approve, then surgeon selects the operation again | Surgery opens. Approval is for that operation; interruption/end requires a fresh approval |
| C3 | Leave a request unanswered for over 30 seconds; attempt a late approval | Expired request cannot authorize surgery; make a fresh request |
| C4 | While surgeon operates, patient attempts a different procedure/limb on themselves | Second operation is refused; first lease is not stolen or cancelled |
| C5 | Open incision or extract a fragment without closing it, then inspect the patient from the other peer | OPEN or SUTURE marker appears on the correct limb in both viewing directions; clears after completion |
| C6 | Switch viewed patients quickly, walk out of inspection range, or disconnect the viewed patient | Markers do not jump to the wrong body or remain indefinitely stale |
| C7 | Stand nearby but behind a wall, then request treatment | Blocked path is refused; approval and unconsciousness do not bypass the obstruction |

Current limits: two world units for treatment reach, five for read-only marker inspection, and at most 1.5 units of movement by either participant from the start of an operation. Moving away or changing scenes interrupts treatment. Stay comfortably inside the range for ordinary tests.

## Third run: measure actual effects

Complete fragment, fracture and chest internal-bleeding surgery as host and client surgeon. At minimum cover one self-treatment and both remote-treatment directions. Do not judge synchronization from animations alone.

For each operation record patient AND surgeon values immediately before, immediately after closing, and again roughly five seconds later. Have both peers read the same patient's health where the UI permits. Screenshots are useful if values are not easily copied. Record naturally changing bleeding/health rather than demanding identical rounded numbers on every frame.

- **Fragments:** fragment count, skin, bleeding and SUTURE marker; extraction alone must leave required closure work. Completion feedback/sound should occur once, including both suction pool slots.
- **Fracture:** skin before incision/after closure, fracture state and displayed recovery measure if available, OPEN marker. Incision costs skin; completed closure refunds recorded incision debt. Do not invent timer measurements if the UI does not show them.
- **Internal bleeding:** chest entry above 0.08 L/min, skin cost/refund, wound progress, and final bleeding rate. Successful treatment targets 0.04 L/min, subject to display rounding and subsequent natural changes. Pinched but unsutured lacerations reopen after six seconds independently.
- **Remote patient:** effects belong to the patient. The surgeon must not receive the patient's healing or incision damage.

## Fourth run: interruption and recovery

| ID | Action | Expected |
| --- | --- | --- |
| R1 | Exit midway through cuts, gel, suction or a pinched laceration; approve and reopen | Completed work, partial doses, layout and skin debt persist. Temporary drawn sutures/gestures clear; laceration timers continue |
| R2 | Move either participant away or behind an obstruction during treatment | Further actions stop; progress persists; fresh consent is required for a conscious patient's next operation |
| R3 | Disconnect client during treatment, reconnect and resume; test each client role | No duplicate healing or old-session input; saved state survives where the game preserves the body |
| R4 | Save/reload open surgery and partly treated fragments where the map supports it | Stable wound IDs/layout, gel, incision debt and pending sutures; stale commands cannot affect a new body |
| R5 | Treat an unconscious patient, then allow them to wake without prior consent | Conscious surgeon may begin without prompt; waking stops further treatment until approval |
| R6 | Operator becomes unconscious, or a participant dies/respawns/changes scenes | Current operation stops. Old consent/commands cannot authorize a replacement body |
| R7 | Toggle Consent for Feeding off for a separate test, then restore it | Off allows nearby treatment without a prompt; distance, obstruction and identity checks still apply |

R5/R6 and unsupported tutorial save transitions need a normal-map pass if tutorial rules prevent the state change. Losing application focus also interrupts surgery, so note any alt-tab; on one PC, switching windows can explain a cancellation.

## What to send back

Preserve BOTH BepInEx/LogOutput.log files before restarting either game. Rename copies clearly, e.g. host-0.15.0-S1.log and client-0.15.0-S1.log. Include the entire session log, not only the final exception. Never overwrite your only failing log with a new run.

Copy this small report per failure or completed measurement:

```text
Test ID / local time:
Map: tutorial / normal (name)
Surgeon: host/client; patient: host/client/self
Procedure / limb / stage / tool:
Consent for Feeding: on/off; patient conscious: yes/no
Injury setup (including commands):
Fresh processes? Any vanilla medical minigame opened before surgery?
Action immediately before result (include movement, alt-tab, reconnect):
Exact menu notice / observed result:
Patient before -> after -> five seconds later:
Surgeon before -> after:
What the other peer saw:
Reproducible? Which test cases were NOT EXERCISED?
Attached host and client log filenames:
```

Send the S1-S4 result first if startup still blocks progress. Once entry works, consent-direction and measured-healing results are the highest-value next data. Stop on a wrong-patient mutation, duplicated reward, or lease theft and preserve both logs immediately.
