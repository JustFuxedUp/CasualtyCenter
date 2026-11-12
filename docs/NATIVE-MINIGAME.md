# 0.5.0: native surgery minigame

For 0.6.0 radial tools, compact scale and continuous suturing, see
[the current controls](COMPACT-SURGERY.md). The three-stitch controls below are historical.

## Player flow

Open the health screen, right-click a limb, and choose **Extract fragment + suture**.
The procedure now starts in the game's actual minigame screen, with its native
hand cursor, hand physics/shake, grip input, exit hint and screen transition.

Use the game's grip/attack binding (normally left mouse). Move the **hand** onto
SHARD, grip, follow the green path, and release at END. After extraction, grip
the Suture selector with the hand and complete three START-to-END stitches.
The visible native hand is the interaction point; it can lag behind the mouse.
Forceps uses the existing tweezers hand sprite; Suture temporarily uses the point
hand sprite until custom tool art is specified.

Use the game's usual minigame exit bindings (pause/back or item-interact).
Completion returns to the health screen. An unfinished exit grants no reward;
any slip damage remains. F8 and the standalone Unity window are removed.
Old debug config entries can be ignored; no treatment settings were renamed.

Free tools, three stitches, treatment values and singleplayer-only restrictions
remain unchanged. No crafting or inventory feature is introduced.

## Implementation / review order

1. `Game/NativeFragmentMinigame.cs`: subclasses the game's `Minigame`. Launch uses
   `StartMinigame`, input comes from `handStartedClicking`, `handClicking` and
   `handStoppedClicking`; positions come from the native hand transform. Tool
   selection uses the host-supplied UI raycast results. No raw mouse drag handling.
2. Its UI is created under `MinigameBase.minigameScreen` and registered as
   `spawnedMiniGame`, below the native hand and host UI. No separate canvas,
   IMGUI window, cursor lock override or full-screen surgery input shield remains.
3. `OperationSession` now accepts an owning native minigame. New operations still
   reject a busy host. An active operation accepts only its own host slot and
   cannot apply changes after the slot closes or is replaced.
4. `NativeSurgeryExit` observes `EndMinigame` only for our subclass and cancels the
   session. Normal screen destruction remains with the game. Replacement/scene
   cleanup also removes an orphaned surgery root without ending someone else's minigame.
5. `Plugin.cs` is now a small launcher/lifecycle coordinator. Focus loss, scene
   change, invalid patient or loss of host ownership ends the operation.
6. Existing `InputGuard` protects only the health context menu and its dismissal;
   ordinary native minigame controls remain under the game's control.

The raw game API is used because the pinned CUCoreLib minigame wrapper is still
documented as in development. API signatures and lifecycle were inspected in the
installed game assemblies. No game implementation code/assets are redistributed.
See [library documentation](LIBRARIES.md) for dependency and minigame references.

## Validation

- net472 Release build against installed game assemblies; no warnings/errors.
- 64 procedure, gesture and session checks, including owning-host completion,
  foreign-host rejection, loss of ownership and exit after completion.
- These tests use small game doubles and do not simulate Unity UI or physics.
- User verified 0.4.0 health-dummy access before this migration.

## Required live playtest

- Launch from several limbs; confirm the correct limb name and the native hand/screen.
- Let go of the launch click before beginning. It must not start a gesture or damage the limb.
- Move the hand to both tool selectors and confirm the native hand sprite changes.
- Complete extraction and all three stitches; confirm exactly one fragment removed
  with the same skin/bleeding benefits. Check cancellation and slip behavior too.
- Try the game's exit controls before extraction and after two stitches. Confirm
  the slot/screen closes, the health screen is usable and no partial reward occurs.
- Start another surgery, then a vanilla minigame. Check for lingering UI/input.
- Check unconsciousness, focus loss, death and scene changes during surgery.
- Check your UI scale, resolution, hand-shake setting and any rebound grip/exit controls.
- Check Together still cannot start a treatment during an active multiplayer session.

Review increment: `codex/health-dummy-menu...codex/native-surgery-minigame`.
Next art request should follow confirmation of the native canvas and hand/tool
anchors. The [surgery kit concept](SURGERY-KIT-DESIGN.md) is recorded separately.
