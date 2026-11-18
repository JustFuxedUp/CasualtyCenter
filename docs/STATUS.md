# Current handoff status

Updated: 2026-11-18. Verify Git/process/file state before acting.

## Unreleased fragment prediction pass (2026-11-18)

Latest scan found a mixed-version test: attached client log loaded 0.15.0 (line 69), host loaded 0.15.1 (line 56). Neither had startup refusals. Client recorded 10 suction, 8 fragment-gel, 2 wound-gel, 2 scalpel and 1 laceration-drag rejections; host recorded 1 laceration-drag rejection. Client also logged 298 Together CLIENT_Tutorial_GlobalState errors; no causal link to surgery established. Both complete logs are preserved under work/diagnostics/mixed-versions-20261118-093838. The new evidence does not test 0.15.1's client scalpel correction. Retest with matching versions before another anchor change.

Implemented an unreleased presentation-only fragment extraction preview: a locally successful release retains the released shard position and fades it over 0.18 seconds instead of resetting it to the wound while waiting. Predictions belong to the exact queued release intent and site within the active presentation. Host confirmation reconciles them; rejection, cancellation, overflow, session failure and disposal clear previews. Predicted extraction does not alter wound eligibility, clinical state, scores or completion audio. Existing ordered gesture samples and wire protocol remain unchanged; this reduces perceived delay, not network round trips. Shared multi-surgeon sessions remain future work.

Verification: 506 checks passed; plugin Release build zero warnings/errors; diff check passed. Added delayed/duplicate acknowledgement, rejected/cancelled preview, site isolation and disposal checks. Native hooks unchanged, so no Harmony harness rerun. Source version string remains 0.15.1 for this unreleased pass. Installed and packaged 0.15.1 remain unchanged at E08680A66513434F52C1F627332CBC5F343156E999B5A9D687C9FBBD7C1A98E2; the new build has NOT been installed or packaged. Next live acceptance: matching peer versions, both scalpel hand slots, immediate fragment fade under latency, rollback after rejected/cancelled release, exactly one host-confirmed score/sound.

## 0.15.1 smoothing test - delivered state (2026-11-18)

Supersedes the 0.15.0 delivery state below. Source/build/installed/package version is 0.15.1. Release build passed with zero warnings/errors; 496 procedure checks passed, including delayed suction completion queue pruning and expanded treatment reach. Native lifecycle hooks were not changed; the 23-check Harmony harness was not rerun for this pass. Live acceptance remains pending: use [TEST-0.15.1](TEST-0.15.1.md).

Changes: marker snapshot tolerance increased from two to six seconds (selection/failure invalidation retained); knife correction identifies the native Blade sprite by reference instead of its asset name; suction opacity previews locally without clearing a host pool, and Good completion removes redundant queued held samples for that target. Consent uses verified ScavPlayer.playerName and an authenticated server notification to offer a Proceed popup; proceeding still checks host permission. Treatment reach is four units; participant movement allowance is three. No same-patient concurrent surgery support added.

Installed with game closed into the single existing plugin folder; DLL and archive DLL SHA-256: E08680A66513434F52C1F627332CBC5F343156E999B5A9D687C9FBBD7C1A98E2. Backup: work/backups/CasualtiesSurgery-0.15.0-before-0.15.1-20261118-055038.dll. Client package CasualtyCenter-0.15.1-multiplayer-test.zip is in the established outputs directory, includes eight WAVs and TESTING.md; archive hash 50503D6F48836B68D32661D6571FF311F1B1BB6C7C96AC320B94FF29AEED991E. Existing config/audio retained. Host log preserved under work/diagnostics/smoothing-0.15.0-20261118-055038; Downloads/LogOutput (4).log was read for diagnosis but no longer existed when archiving was attempted.

Work remains on codex/development; check Git for commit/push state. No merge or public release. The marker/knife/fade fixes and approval popup need host/client rendering checks; passing isolated tests does not certify them.

## Exact stopping point (historical 0.15.0 delivery)

| Item | State |
| --- | --- |
| Branch / committed baseline | `codex/development`; 0.15.0 release source prepared from baseline `e4f8e06`. Preserve `codex/project-handoff` as historical; review remains development -> main. Check Git for current commit/push state. |
| Working source | Accumulated accepted gameplay and multiplayer implementation included in the 0.15.0 test release. No merge into main or public release publication requested. |
| Source / build version | **0.15.0 multiplayer integration test**. Release net472 succeeded with zero warnings/errors. |
| Built DLL SHA-256 | `6B6C72A2D99F738960C3DF5EA20BAC5B4275AF848CAE7446C1CA41A2F56B9BF0` |
| Installed DLL SHA-256 | `6B6C72A2D99F738960C3DF5EA20BAC5B4275AF848CAE7446C1CA41A2F56B9BF0` |
| Installation | Installed with the game closed into the single existing CasualtyCenter-0.14.0-multiplayer-test folder; folder name retained to avoid duplicates. Configuration/audio unchanged. |
| Backup | Stable 0.13.8 saved under ignored `work/backups/CasualtiesSurgery-0.13.8-before-0.14.0-2026-11-17.dll`; backup hash verified as `5BFD919451F4114DDFFC59E6B3CFAAAB2467F372E1035ED0CED5E6A788F2ADCB`. |
| Package | `CasualtyCenter-0.15.0-multiplayer-test.zip` in the established local outputs directory: matching DLL, eight WAVs, README, TESTING.md and checksum manifest. Archive hash: `DFBD3B19E483012C799C0A6DB7204FA75EB2AD6B5016898A7B7B9018138D2A52`. Archive DLL hash/audio count verified. |
| Verification | **492 procedure checks with eight WAVs and 23 real-Harmony lifecycle checks rerun for release**. Plugin Release zero warnings/errors; diff check passed. Harness NU1900 is the unavailable NuGet vulnerability feed. Live 0.15.0 acceptance pending. |
| Default availability | Prior singleplayer views remain the default. Set `Development.EnableAuthorityViews=true` on both peers to test the new shared view; this also enables transport. `EnableSurgeryTransport` alone does not enable test views. |

## 0.15.0 delivery and next test

User authorized version push, installation and client package on 2026-11-18, superseding the earlier no-release instruction. Prior 0.14.2 DLL saved as work/backups/CasualtiesSurgery-0.14.2-before-0.15.0-20261118-043408.dll; host/client logs preserved under work/diagnostics/before-0.15.0-20261118-043408. Source and installed/archive hashes match. See [TEST-0.15.0](TEST-0.15.0.md) for the current tutorial-first test sequence and report template. First test surgery before any vanilla medical minigame; then consent in both directions and measured patient-versus-surgeon effects. Tutorial-limited death/unconsciousness/save cases must be marked not exercised and repeated on a normal test map. Preserve both complete logs before relaunch.

## Implementation history: priorities and shared-treatment pass

Recovery continuation completed 2026-11-18: remote permission also invokes Together's native AccurateRaycastInteractionCheckObstruction(Body,Body,bool), without effects, before prompting, on approval and during the operation. Unconscious-patient and disabled-consent exceptions do not bypass obstruction. Native method signature was inspected in CasualtiesTogetherUtils.Util. Runtime detects disconnected/replaced/destroyed Unity bodies and retires transient registrations, identities and consent; save-backed limb progress is retained. Destroyed Unity objects are normalized before identity comparisons, while ForgetPatient uses ReferenceEquals so destroyed references can still be removed. Bounded open-request tombstones reject late attempts to reopen a replacement body.

Thirteen added recovery checks cover expired/disconnected/retired consent callbacks, obstruction changes, restored fracture debt/cuts/gel, stale old-body commands/open requests, restored fragment IDs/pending closure/partial suction, and 270 successive body generations without registry exhaustion. Native health inspection confirmed Server_QueueSendCharacterHealth sends absolute current patient health to clients and normal packet application uses tick ordering; no forced synchronization bypass was added. Live Unity destroyed-object behavior, raycasts and socket health timing remain acceptance tests. Installed DLL hash was rechecked unchanged; no release artifacts or version changes.

User authorized priority fixes, then as much shared-operation preparation as practical. Keep everything unreleased for a later major push. Startup now returns an empty native Together target for ISurgery only, preventing reflected limb classification from imposing vanilla wound-item prerequisites. Actual native startup, occupied/ignore-next guards and reset still execute; unrelated minigame targets are untouched. Real-Harmony checks reproduce cold refusal and the prior missing-ID failure before installing the production guards.

Remote read-only overviews use canonical live Together roster IDs, authenticated viewer identity, same scene and a five-unit range. Responses echo the requested patient and validate boolean markers. Switching patient resets pending requests; late replies are ignored, failures clear markers, and displayed snapshots expire after two seconds. Self-overview remains compatible. Markers do not grant treatment permission.

One surgeon can now target another player's body through the native health menu. Host permission checks require a live conscious operator, live patient, same scene and two-unit reach. Leases bind both exact body instances and their starting positions; moving either more than 1.5 units, changing scene/body, disconnecting or losing access stops further actions. Registration validity is separate from permission so a competing patient/operator cannot remove another surgeon's lease. One patient lease spans all limbs/procedures. Native health replication now targets the treated patient, not the sender.

Consent follows the user's approved fallback: Together ConsentForFeeding. Its rule struct is fixed-layout INetSerializeByMemcpy, so no custom field was injected into the host menu/network layout. When enabled, conscious patients receive Together's PromptPlayer request; unconscious patients do not require consent. Only answer zero grants approval, tied to live bodies and a 30-second offer. Surgeon reselects the operation after approval; refusal/waiting messages reopen the surgery menu. Approval lasts for the acquired operation and ends on release/interruption. Waking without prior consent revokes further actions. When ConsentForFeeding is disabled, nearby treatment does not prompt. Self-treatment still requires consciousness; clinical patient checks now permit an unconscious patient with an authorized conscious operator.

New checks cover remote markers and stale selection, declined/duplicate/reset consent, competing limbs/operators, exact-once remote effects, operator body replacement/movement, waking patients, and complete self/remote internal-bleeding wire flows through incision/suction/forceps/stitches/gel/closure. They verify 0.04 L/min final rate and no duplicate healing. A stale shared compiler process denied build writes; UseSharedCompilation=false resolved it.

Remaining live acceptance: fresh-session startup before ANY vanilla medical minigame; both host/client surgeon directions; prompt accept/decline/timeout including host as patient; remote markers; unconscious/waking treatment; disconnect/reconnect and save/reload; measured health before/after all three procedures. Simultaneous two-surgeon work, a spectator minigame board, host migration and a dedicated surgery host-menu rule remain outside this pass. Do not install or package until the user resumes release work.

## Historical 0.14.2 live retest: startup prerequisite identified

Fresh host/client logs preserved in ignored work/diagnostics/startup-0.14.2-20261117-140917. Host recorded seven Together startup refusals then six accepts; client five refusals then six accepts. All observed decisions had occupied=False and ignoreNext=False. User reports that playing a vanilla minigame first unlocked surgery on each peer. Earlier ignore-next hypothesis is ruled out for these failures.

Inspected Together.MinigameMPManager.GetMinigameTargetObjectNetId: it reflects a field named limb on any Minigame, including NativeAuthorityMinigame. Together's startup prefix therefore classifies surgery as limb-targeted, and rejects this unknown wound-item minigame when PlayerCamera_ApplyWoundItem_MultiplayerPatch.applying_on_limb is null. Vanilla Shrapnel/Dislocation/CPR startup populates that field. This explains the observed prerequisite; prior harness doubles omitted this reflected-limb path. Next fix should isolate ISurgery from Together's vanilla wound-item prerequisite while preserving native startup/reset and occupied/ignore-next rules, with a regression reproducing the cold-start limb classification. Do not fake a prior vanilla action or depend on stale global limb state.

After the prerequisite, both peers logged applied actions and completed fracture surgery (incision.close Applied). Client also completed a fragment procedure. User observed health effects on both sides, without strict before/after measurement. This is live flow evidence, not exact clinical synchronization certification. Internal bleeding was not exercised successfully in this test.

Host saw OPEN/SUTURE markers on client; client did not see host markers. Current overview requests are self-patient only; host has authoritative stores for both peers, whereas client falls back to local stores for remote bodies. Remote read-only marker replication remains missing and is separate from permission to operate on another player. Preserve that treatment restriction. No source/build/install change made during this analysis; installed version remains 0.14.2.

## 0.14.2 diagnostic implementation

The user's 0.14.1 retest loaded the correct version with both flags true on host/client. Enabled operation clicks closed the menu without opening a minigame. Neither log contained Authority view opened or the earlier missing-ID exception. The exact silent-return/veto boundary is still unknown; do not claim the original guard was exercised. No local patient on another player's body is expected. Client also logged 110 Together tutorial errors and two run-settings errors, with no demonstrated causal connection to surgery entry.

0.14.2 retains event-level logs for menu open/click/validation, entry refusal and role, client settling, native call/result, actual Start entry and cleanup. A read-only observer logs Together's startup prefix result and its pre-call occupied/ignore-next state. Observation failures do not disable transport; logger failures cannot change native decisions. Existing view-open/stage/finish logs remain. No gameplay/veto policy was changed. The user explicitly requires normal-flow logs to remain permanently; this convention is now in AGENTS.

Verification: 18 real-Harmony lifecycle checks, 435 existing checks, plugin Release zero warnings/errors. New checks cover consumed ignore-next rejection followed by successful retry, captured pre-call state, observer idempotence, unrelated native silence and throwing log sinks. The harness still warns NU1900 for unreachable vulnerability data. These checks do not simulate Unity UI or establish live multiplayer acceptance.

Installed with game closed into the single existing plugin folder; source/installed/archive DLL hashes match. Configuration/audio unchanged. Backup: work/backups/CasualtiesSurgery-0.14.1-before-0.14.2-20261117-135439.dll. Logs: work/diagnostics/startup-0.14.1-20261117-135439. Package includes eight WAVs and updated checklist. All current work remains uncommitted/unpushed. Next: update both peers, attempt one operation, preserve both logs, and fix the specific refusal boundary those logs establish.

## 0.14.1 startup correction

The user's 0.14.0 host log confirmed that Together's native type-ID lookup threw for NativeAuthorityMinigame after opening the view. Both flags were enabled; this was not resolved by configuration. Client entry failure is not independently explained by the host exception: the client log also contains separate Together tutorial/settings errors. If entry remains blocked, capture its exact eligibility text and both logs.

TogetherMinigameCompatibility now guards Together's start postfix/end prefix only for ISurgery views, preserving native screen/hand startup, cleanup, Together's startup reset and unrelated native minigames. Surgery continues using its own protocol; native IDs are not rebuilt or renumbered. Signature validation fails closed before transport availability. Startup logging includes effective flags and config path.

The new net472 harness uses the installed real Harmony library, reproduces the original missing-ID failure with an already-patched native method, and verifies startup, cleanup, native notifications, reset/veto, idempotence, unchanged IDs, exception propagation and unpatch behavior. Its build reported NU1900 (unreachable vulnerability-data feed), but compiled and ran successfully. Plugin build had zero warnings/errors. This does not certify Unity/Mono or live multiplayer behavior.

Installed with the game confirmed closed into the existing CasualtyCenter-0.14.0-multiplayer-test folder (folder name retained to avoid duplicates). Flags/audio were unchanged. Previous DLL backup: work/backups/CasualtiesSurgery-0.14.0-before-0.14.1-20261117-134223.dll. Host/client logs preserved in ignored work/diagnostics/startup-0.14.0-20261117-134223. Installed and archive DLL hashes match the build; archive includes all eight WAVs. Changes remain uncommitted/unpushed.

## Implemented integration

The existing authority foundation now has a real native command/snapshot view. `NativeAuthorityMinigame` and `NativeAuthorityInput` share incision/repair/closure presentation for fracture and internal bleeding and handle fragment boards. They reuse native hand physics, selected tool, the knife visual correction, existing effect/audio helpers, wound/bone geometry and final stitch artwork. Temporary hand/shard/bone motion is presentation only; clinical progress and completion ratings come from the host.

`SurgeryPresentation` owns validated detached replicas and a bounded input queue. Held samples can coalesce for the same target, while grip, terminal, release and suture evidence retain order. Rejected gestures clear dependent work; stale revisions retry with the host revision; overflow cancels safely. Closing during pending work defers lease release until acknowledgement. The same controller runs through an offline router in singleplayer or Together in multiplayer.

Active views refresh snapshots for timer/pool changes. Host-provided fragment scale keeps rendered gel reach consistent across differing local settings. Read-only self-patient overviews update native health-screen [OPEN]/[SUTURE] markers without taking an operation lease. Fragment scalpel mistakes now have the same authoritative sample path as fracture; current internal scalpel balance is unchanged. No inventory consumption or new balance tuning was introduced.

New checks exercise detached replicas, no speculative health changes, lost replies, held-input coalescing, cancellation order, rejected dependent input, queue overflow, delayed view disposal, stale-revision recovery, marker overview, local loopback and host geometry. Tests do not simulate Unity rendering or actual Together sockets.

## Next action: user two-instance testing

Use [MULTIPLAYER-TEST](MULTIPLAYER-TEST.md). Start with the connection/one-cut smoke test in opt-in singleplayer, then host and client, before doing full procedures. The user offered to run these tests. Preserve both logs before relaunch; investigate observed failures with procedure/limb/stage/tool and role-specific evidence.

Do not redo completed view/transport implementation or remove the default gate yet. Verify the unreleased startup, remote treatment/consent and markers along with actual Together native health confirmation, save/reload/reconnect and input/feedback under latency. Observer surgery boards, simultaneous surgeons and host migration remain unsupported.

See [MULTIPLAYER](MULTIPLAYER.md) for the implemented payload and lifecycle contract. A successful build and 435 adapter checks do not establish playable multiplayer compatibility.

## Accepted gameplay baseline

0.13.8 includes chest internal-bleeding entry above 0.08 L/min and a successful treatment floor of 0.04 L/min, shared incision skin costs/refunds, independent six-second laceration timers, rotated scattered wounds, forceps drag closure, suction, gel/suction loops and completion feedback. Fracture bones are 56 units wide with external pivots and a 5-degree release window. [ART-HANDOFF](ART-HANDOFF.md) records dimensions.

Historical validation: the user confirmed the knife correction, reported 0.13.2 mostly functional, tested fracture tuning and accepted 0.13.7 as the first minor milestone pending art. The installed 0.13.8 baseline previously passed 270 checks; that count does not describe the current source.

## Known issues and confidence

- **Open-surgery architecture direction:** most upcoming procedures are expected to use a shared open-surgery lifecycle. Future implementation should centralize open-up/incision skin debt, muscle-first mistake damage, incision-to-procedure transitions, and close-up/`IncisionSkin` completion. Surface procedures remain intentionally undefined and should not be folded into that section until their boundary is decided.

- **Knife alignment:** user confirmed the 0.12.2 correction works. 0.13.7 retains its native hand contact and knife mesh offset.
- **Cross-limb suture failure:** user reported mistakes after leaving an incision and treating fragments elsewhere. Stores/rule instances were already separate; exact failure was not reproduced. 0.11.1 changed fragment targeting to evaluate actual wound crossings and added per-wound diagnostics. Regression checks cover cross-limb state isolation. In-game confirmation still needed.
- **Two wounds then three:** logs confirmed stable-ID pagination, with two remaining IDs on page 1 and three on page 2 after reinjury. Not evidence of missing wounds. UX improvement is deferred; do not reset saved IDs as a shortcut.
- **Heal-command edge cases:** healing/reinjury can reconcile vanilla counts while custom open-incision debt remains. Preserve open work; do not interpret console-driven testing as a new production bug without evidence.
- **Gel/infection tuning:** implemented, tested at rule/adapter level; real-game timing, transparency and balance still need feedback. Coverage persists for the operation; native antiseptic time separately decays.
- **Multiplayer:** 0.14.1 has opt-in test views; default eligibility remains singleplayer. Actual host/client acceptance is pending. Earlier Together desync reports are historical, not a compatibility certification.

## Latest accepted feedback

Keep native health menu/minigame flow, 33% hand scale, generous gesture tolerance, all held non-gel red trails, retained tool selection, full-circle new wound layouts, and saved progress. Gel reach/particle side length doubled in 0.12.1. Persistent green prep overlays disappear when the main incision is complete, without erasing coverage.

Internal bleeding uses temporary square artwork and the native grasp hand for suction while user art is in progress.
