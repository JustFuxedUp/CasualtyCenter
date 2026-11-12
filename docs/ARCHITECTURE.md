# Architecture and integration map

## Runtime ownership

`Plugin.cs` registers Harmony patches and the two CUCoreLib limb-save providers. It owns HealthMenu, feedback audio, one active ISurgery, selected tool preference, input-exit protection and config. `NativeFragmentMinigame` and `NativeFractureMinigame` implement the game's Minigame plus our ISurgery lifecycle. HealthMenu captures the actual clicked limb, validates it, and dispatches the appropriate operation.

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
| Radial menu and wrist fade | Game/SurgeryGraphics.cs |
| Trail, counters, contact/knife visuals | Game/SurgeryEffects.cs |
| Audio and floating ratings | Game/SurgeryFeedback.cs; Audio/WaveData.cs |
| Existing regression suite | tests/Program.cs and feature *Checks.cs files |

Keep pure mechanics in Procedures where feasible; Unity/input presentation stays in Game. Do not initiate a large abstraction rewrite just because the two native operation classes contain some similar code. Extract common behavior when a real change needs it and its verification boundary is clear.

## Persistence and transactions

Save IDs are `local.casualties.surgery.fragment-board` and `local.casualties.surgery.fracture`. Both providers currently report version 1; board LayoutVersion is a separate geometry field.

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

Wound coordinates, native hand coordinates and rendered sprite coordinates are distinct. Current pending knife patch translates the sprite mesh so the annotated tip is at the native hand origin. It runs after wrist fading, before transform rotation/scale/mirroring, and is removed on exit. Do not restore the rejected action-point offset.

Loose fragments use Image.maskable=false while keeping their Mask enabled/hidden. Turning off Mask exposes the white mask Image. Stitches are on an upper artwork layer; temporary trails remain below them. Gel prep overlays hide after incision while coverage remains persisted. Dummy [OPEN]/[SUTURE] indicators read saved state and do not intercept clicks.

Feedback loads six local PCM WAVs once, tolerates missing files, and owns a separate held-scalpel loop channel. A loop is refreshed by its operation and stops when ownership is lost. Floating ratings can outlive the minigame root briefly. Pause must not advance continuous tissue damage or supply use.

## Deliberate limits

No multiplayer mutation path, inventory authority, final scalpel item or anatomy-specific bleeding procedure exists. Save adapters and pure checks are not a simulated Unity runtime. Treat renderer, grip feel, audio and inter-mod behavior as actual-game acceptance work.
