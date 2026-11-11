# Scalpel and fracture surgery — 0.11.0

## Player flow

Right-click an attached organic limb on the health dummy and choose **Open / align fracture / close**. A fracture is required for a new procedure; a saved open incision can always be resumed while the patient is otherwise eligible. This prototype remains singleplayer, uses free tools, and does not pause the world.

Hold right mouse inside the operation to select Forceps (right), Suture (upper left), or Scalpel (lower left). Release to select. The chosen tool stays selected between stages and operations. Right-click outside to leave. The native Blade hand is the scalpel placeholder; custom artwork is not required for testing.

1. **Incision:** hold the normal grip/attack control and touch all nine gold cut markers with the scalpel. Any order or path works. Swept contact catches markers between frames. Completed markers turn red and cannot be credited again. Release/regrip is allowed. The rating depends on time from first productive contact until the outline is complete, accumulated while this operation is open and game time is running, including idle time and the tool menu. Time away from the operation is not charged. Paused game time does not advance cutting time or continuous scalpel damage.
2. **Alignment:** choose Forceps, grip near an end of either bone piece, rotate around its center until it lines up with its blue guide, then release. Both pieces must finish within 12 degrees. Successful pieces snap into place and lock. A failed release leaves the piece at its current angle for retry. Moving entire displaced bone sections is deferred.
3. **Closure:** choose Suture and make at least three crossings of the central incision in one held stroke. Release to close. Suturing failures retain the previous no-damage rule; retry freely.

The art is temporary: markers, a red incision and two bone strips. No infection/disinfecting system or internal-bleeding treatment is added yet.

## Initial balance values

| Event | Effect |
| --- | --- |
| Incision | Removes 60% of skin health measured at the first marker, spread across nine contacts. Records the actual amount removed. |
| Good/Great closure (41+) | Returns the recorded incision cost to current skin health, capped at 100. Other damage is not refunded. |
| Bad closure (0–40) | Returns 50% to just under 100% of the recorded cost, proportional to score. |
| Completed fracture operation | Remaining live bone timer becomes 25%, stays positive, and `broken` stays set. Natural healing finishes it. |
| Failed bone alignment | +2 bone timer while fractured, +25 pain (cap 100), −2 muscle health (floor 0). No bleeding change. |
| Scalpel without new objective contact | After 0.2 continuous seconds, −5 skin, −1 muscle and +10 pain per second. No bleeding is added. |

These are initial code constants for playtest tuning, not new configuration entries. Productive contact means reaching an unfinished cut marker; resting on an already-cut marker does not prevent damage. Releasing the scalpel resets the grace interval. Scalpel damage also applies on the fragment board and in alignment/closure, where there are no valid cutting objectives. Only active scalpel use in our surgery screens is affected; vanilla item behavior is unchanged.

Cut rating is 100 for up to two seconds, then loses eight points per further second, bounded at 0–100. Alignment rating is based on final angle error. Closure uses the existing continuous suture score. Bad/Good/Great labels and audio keep the established boundaries. `Scalpel.wav` plays on productive cuts with a short audio throttle; it is a local placeholder excluded from Git.

## Persistence and treatment integrity

The limb save provider stores cut markers, incision cost, phase, angles, completed bone pieces and elapsed cutting time. Leaving an operation, losing focus, or saving does not return skin or reduce the fracture timer. Reopening resumes it. Closure changes the state once, preventing repeat refunds or repeat timer reductions on the same fracture. A newly broken bone can start a new episode, but an existing open incision is retained across reinjury.

Health, pain and timer changes use current live values. If a heal command or another treatment mends the fracture during alignment, the operation proceeds to skin closure without rebreaking it or awarding another fracture reduction. Healing commands during an incision remain an edge case for playtesting. The previously reported two-then-three fragment visibility issue remains tracked separately and is not claimed fixed here.

## Verified installed-game fields

Inspected the local installed Assembly-CSharp with ILSpy 11.2. No game source or binaries are committed. `Limb.broken`, `boneHealTimer`, `pain`, `muscleHealth`, and `skinHealth` are the relevant fields. Limb update naturally decrements the timer and calls MendBone at zero. The vanilla bone-welding tool multiplies the timer by 0.25; this prototype uses that same reduction but deliberately keeps the timer positive and applies our operation-specific costs. Skin-health changes feed the game's normal limb damage display.

## Code review map

- `Procedures/ScalpelTrace.cs`: swept marker contact, off-target grace, persistent fracture state and pure outcome rules.
- `Game/FractureStore.cs`: limb save adapter, eligibility, closure commit, mistake effects and new-fracture reset hook.
- `Game/NativeFractureMinigame.cs`: native-host incision/alignment/closure UI and input, temporary art, bone rotation and suture rendering.
- `Game/HealthMenu.cs`, `Plugin.cs`: separate fracture action, shared operation lifecycle and save registration.
- `Game/SurgeryGraphics.cs`, `NativeFragmentMinigame.cs`: three-tool radial selection, retained tool choice and scalpel consequences on fragment boards.
- `tests/FractureChecks.cs`: grace, frame-to-frame cutting, partial saves, angular tolerance, live-stat closure, repeat prevention, mistakes and treatment gating.

## In-game acceptance pass

1. Back up a test save. Open an eligible fracture through the health dummy, choose Scalpel and cut markers in a nonsequential order. Skin should drop progressively; untouched markers stay visible.
2. Hold the active scalpel away from remaining markers: a brief grace, then repeated tissue damage. Verify no added bleeding. Repeat on the fragment board and in the bone phase.
3. Exit after several cuts, save/load and reopen. Markers and paid skin cost should remain. Finish the outline and verify the interior view appears without a tool switch.
4. Rotate each piece. A near alignment should lock; a bad release should increase pain strongly, muscle damage slightly and the timer slightly, with bleeding unchanged. Exit/reopen between pieces to verify saved alignment.
5. Close with three crossings. Good closure should restore the incision cost while preserving unrelated damage, and reduce the remaining timer to one quarter without instantly healing the fracture.
6. Confirm the same treated fracture cannot repeat the reward, a subsequent genuinely new fracture can be operated on, and external healing during an open operation still permits closure.
7. Check the Blade hand's grip point, rotation feel, temporary target size, and the Scalpel sound level. These require Unity gameplay testing; the standalone suite does not simulate the renderer or native hand physics.
