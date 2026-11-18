# Architecture and integration map

Unreleased shared-treatment addition: SurgeryTreatmentAccess separates the conscious operator from the clinical patient, owns host-side consent offers and per-operation body/movement guards, and follows Together ConsentForFeeding with an unconscious-patient exception. SurgeryHostRouter resolves optional roster targets and retains one lease across a patient's limbs; target validity is independent of caller permission so competing requests cannot destroy an active registration. SurgeryTogetherRuntime queues native health for the treated patient and uses Together's recipient-authenticated consent prompt. Remote read-only markers use a separate bounded overview request and do not acquire a lease. See MULTIPLAYER.md for limits and STATUS.md for unreleased versus installed state.

## Runtime ownership

`Plugin.cs` registers Harmony patches and three CUCoreLib limb-save providers (fragment, fracture, internal bleeding). It owns HealthMenu, feedback audio, one active ISurgery, selected tool preference, input-exit protection and config. The three `Native*Minigame` implementations use the game's Minigame plus our ISurgery lifecycle. HealthMenu captures the actual clicked limb, validates it, and dispatches the appropriate operation. See [MULTIPLAYER](MULTIPLAYER.md) before changing authority or moving clinical mutations out of views.

`InputGuard` prevents background player controls while our context menu/wheel owns input. Native MinigameBase remains responsible for hand simulation and grip flags. On scene/focus/host changes, close the transient UI without undoing saved progress.

## Files to inspect for a task

| Concern | Primary files |
| --- | --- |
| Lifecycle, registration, config | Plugin.cs; Game/HealthMenu.cs; Game/InputGuard.cs |
| Fragment view, grip and target selection | Game/NativeFragmentMinigame.cs |
| Fragment live-stat commit | Game/OperationSession.cs; Procedures/FragmentProcedure.cs |
| Fracture view, bone transforms | Game/NativeFractureMinigame.cs |
| Fracture eligibility, closure, infection adapter | Game/FractureStore.cs |
| Suture crossing/quality | Procedures/SutureStroke.cs |
| Extraction/loose latch | Procedures/FragmentPull.cs |
| Scalpel grace and fracture state | Procedures/ScalpelTrace.cs (currently contains both) |
| Board placement and save adapter | Procedures/FragmentBoard.cs; Game/FragmentBoardStore.cs |
| Gel dose/risk math | Procedures/GelRules.cs |
| Shared incision phase gates, debt and closure | Procedures/OpenSurgery.cs; IOpenSurgeryState implemented by fracture/internal states |
| Local/host clinical application boundary | Game/SurgeryActions.cs |
| Identity, leases, journal and result feedback | Procedures/SurgeryAuthority.cs; docs/MULTIPLAYER.md |
| Dispatcher, snapshots and host ticking | Procedures/SurgeryDispatcher.cs |
| Production authority targets and gesture validation | Game/OpenSurgeryCommandTarget.cs; Game/OpenSurgeryRepairTarget.cs; Game/FragmentCommandTarget.cs; Game/TreatmentInput.cs |
| Self-patient routing and asynchronous client | Game/SurgeryHostRouter.cs; Game/SurgeryClientSession.cs |
| Opt-in Together transport/runtime | Game/TogetherSurgeryTransport.cs; Game/SurgeryTogetherRuntime.cs |
| Scoped Together native minigame lifecycle compatibility | Game/TogetherMinigameCompatibility.cs; tests/MinigameInterop |
| Shared local/network native view, detached replicas and input scheduling | Game/NativeAuthorityMinigame.cs; Game/NativeAuthorityInput.cs; Game/SurgeryPresentation.cs |
| Radial menu and wrist fade | Game/SurgeryGraphics.cs |
| Trail, counters, contact/knife visuals | Game/SurgeryEffects.cs |
| Audio and floating ratings | Game/SurgeryFeedback.cs; Audio/WaveData.cs |
| Existing regression suite | tests/Program.cs and feature *Checks.cs files |

Keep pure mechanics in Procedures where feasible; Unity/input presentation stays in Game. Do not initiate a large abstraction rewrite just because the two native operation classes contain some similar code. Extract common behavior when a real change needs it and its verification boundary is clear.

## Planned open-surgery framework

Most future procedures are expected to be open surgeries. Shared rule lifecycle now lives in OpenSurgery/IOpenSurgeryState with save-compatible adapters in both existing states. Future procedures should implement that contract; shared Unity transition presentation is still pending. Its baseline contract is:

- begin with an incision/open-up phase and account for incision skin debt through `IncisionSkin`;
- finish with a close-up phase that commits the incision, applies the shared skin refund, and performs the common infection/disinfection completion checks;
- route ordinary damaging mistakes to muscle health instead of skin health while the incision is open (tool-specific exceptions may still apply);
- own explicit transitions into and out of incision gameplay so minigames can change presentation/input without losing saved progress;
- reuse the existing gel coverage, blood-pool, suture, feedback, and persistence boundaries where applicable.

Surface procedures should remain a separate path until their relationship to the open-surgery lifecycle is specified. Do not force surface work into this section merely because it shares tools or feedback.

## Persistence and transactions

Save IDs are `local.casualties.surgery.fragment-board` and `local.casualties.surgery.fracture` (version 1), and `local.casualties.surgery.internal-bleeding` (version 2, accepts version 1). Board LayoutVersion is a separate geometry field. Internal bleeding is body-wide despite the chest entry; never create independent per-limb healing rewards from that scalar.

FragmentBoardStore uses a ConditionalWeakTable per Limb. Board state includes seed, next stable ID, layout version and sites with Extracted/Completed, GelMl and ExtractionScore. Reconcile maps native count changes deterministically: append new IDs; external removals prefer unextracted sites and descending IDs. Restore defers count reconciliation because other providers may restore counts later. Layout 0 migrates to scatter 1; layout 1 retains its limited angles; newly created layout 2 permits 360-degree orientation. Do not bump old layouts casually.

FractureStore is a separate per-limb table. FractureState includes stage, nine cut flags, two bone angles/locks, actual SkinDebt, PlannedCost, elapsed cut time, nine gel doses and recorded bone quality. Progress is updated incrementally. TryClose validates then applies infection risk, debt refund and timer reduction once. A BreakBone hook starts a new eligible episode only when appropriate; open incisions are preserved.

OperationSession captures patient/limb/native owner, starting fragment count, origin and scene. It rejects wrong ownership, invalid stats, detached/prosthetic limbs, loss of consciousness, substantial movement and competing minigames. Successful extraction transitions its rule state; completion writes current skin/bleeding/count values. CompleteGelClosure also verifies board membership and dose. UI should call these boundaries, not reimplement them in effects callbacks.

## Native findings already verified locally

- .NET Framework 4.7.2 plugin, BepInEx 5, pinned CUCoreLib 1.1, Harmony supplied by BepInEx, Unity UI/TMP. No second Harmony distribution.
- Limb fields: shrapnel int; skinHealth, muscleHealth, bleedAmount, pain, boneHealTimer, infectionAmount, disinfectionTime; booleans broken/infected/dismembered; prostheticInfo.hasProsthetic.
- Body has immunity (0–200), infectionChanceMultiplier, internalBleeding and native immune-growth logic. The existence of internalBleeding does not establish a per-limb bleeding map.
- Native bone welder multiplies boneHealTimer by 0.25. Limb update naturally mends at timer <=0; our surgery keeps positive time.
- SetDisinfect extends the native timer; GetInfectionSpeed includes immunity and a disinfection modifier. Infection chance run settings must remain respected.
- UpdateHandVisuals sets handTransform position from handPos, rotation from velocity, then updates sprite. UpdateHandSprite overwrites scale and selects the grip sprite. Scoped patches must cooperate with those writes.
- HandSpriteType choices used: Tweezers, Point, Blade, Grasp. Grasp is the organic dislocation hand.
- Native shrapnel sprite is loaded from `Special/ShrapnelMinigame`; extracted art is not packaged.

These are installed-game observations, not promises about future game versions. For moved APIs inspect the current installed assembly narrowly. LIBRARIES points to official library docs and pinned hashes.

## Visual/audio boundaries

Wound coordinates, native hand coordinates and rendered sprite coordinates are distinct. The accepted knife correction translates the sprite mesh so the annotated tip is at the native hand origin. It runs after wrist fading, before transform rotation/scale/mirroring, and is removed on exit. Do not restore the rejected action-point offset.

Loose fragments use Image.maskable=false while keeping their Mask enabled/hidden. Turning off Mask exposes the white mask Image. Stitches are on an upper artwork layer; temporary trails remain below them. Gel prep overlays hide after incision while coverage remains persisted. Dummy [OPEN]/[SUTURE] indicators read saved state and do not intercept clicks.

Feedback loads eight local PCM WAVs once, tolerates missing files, and supports held scalpel, gel and suction loops. A loop is refreshed by its operation and stops when ownership is lost. Floating ratings can outlive the minigame root briefly. Singleplayer pause must not advance continuous tissue damage or supply use; multiplayer clock policy is specified separately in MULTIPLAYER.

## Deliberate limits

0.14.0 adds an opt-in shared authority view for all current procedures, using local loopback or Together. The default remains the prior singleplayer views until actual two-instance acceptance. No inventory authority, final scalpel item or anatomy-specific bleeding map exists. Save adapters and transport-contract doubles are not a simulated Unity/Together runtime. Treat renderer, grip feel, audio and real two-peer replication as actual-game acceptance work; use MULTIPLAYER-TEST for the test switch and checklist.
