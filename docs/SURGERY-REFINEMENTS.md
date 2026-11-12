# Surgery refinements — 0.11.1

Stitches now render in a top layer above wound, bone and fragment art. Fragment thread rendering follows turns in either screen direction, including rotated wounds.

Scalpel audio loops while held in either surgery type, independently of objective hits. Release, tool wheel, tool change, stage transition, pause or exit stop it. A separate audio channel prevents ratings and mistakes from interrupting the loop. Cut markers disappear silently. Each newly cut marker resets the off-target grace to 0.4 seconds, including after damage has already begun. Existing cut markers do not provide endless safe contact.

The two bone halves now hinge from their outer ends and meet at the center in a single vertical line when aligned. Grab near a free end and rotate toward the blue guide. Existing saved angles and aligned flags remain valid.

The health dummy shows amber [OPEN] on limbs with a partial or complete incision awaiting closure, and [SUTURE] on limbs with extracted fragments awaiting closure. The fracture menu says Resume open surgery for that limb. Indicators are derived from per-limb saved state, survive reload, and do not intercept clicks. They are debugging/UX markers, not multiplayer synchronization.

## Cross-limb suture report

The fracture and fragment operation classes already own separate stroke rules; their persistent states use separate per-limb stores. No shared procedure-state instance was found. The initial-click fragment targeting could, however, select a different pending wound than the one crossed by a fast stroke. Fragment suturing now tracks every eligible pending wound separately for the current gesture and completes the one with the most qualifying crossings at release. It never carries those candidates into another operation. Failure logs include limb, board and candidate crossing counts; fracture failures include stage and crossing count. This addresses targeting ambiguity, but the reported cross-limb failure still needs an in-game reproduction check.

Regression checks cover opening one limb, extracting/suturing another, preserving the first limb's paid skin cost and cut markers, and returning to close it. No cross-limb healing or shared stroke progress is allowed.

The saved log also confirmed the older two-then-three visibility report: a board with next ID 15 had two remaining sites on page 1, followed by three on page 2. This is stable-ID pagination, not vanished fragments; pagination is unchanged in this patch.

## Playtest

1. Hold scalpel off target, touch a fresh marker and move away: sound should remain continuous, the marker disappear, and damage wait a fresh 0.4 seconds. Release/change tool/open wheel/exit: loop stops.
2. Interrupt an incision. Confirm [OPEN] appears on that limb; remove fragments on another limb, stitch them, then resume the first incision. Each limb retains only its own work.
3. Begin a suture away from a wound center and cross the intended wound three times. Repeat with rotated wounds. Thread should remain visible over all art and choose the crossed pending wound.
4. Rotate both bone halves around their outside ends until they form one line. Finish closure and confirm [OPEN] disappears.
5. Save/reload with both an incision and an extracted fragment pending; check both markers and resumed operations.

Build and standalone checks do not verify Unity draw order, audio loop perception, native hand contact, or the reported in-game failure. Those remain the acceptance pass above.
