# 0.8.1: hand scale fix and larger fragments

The game resets the hand image's scale to (+/-1, 1, 1) inside every
`MinigameBase.UpdateHandSprite` call. The previous startup-only assignment was
therefore overwritten. A scoped Harmony postfix now applies `HandScale` after
that native reset, preserving the hand-slot mirror. It applies only during our
minigame, and the existing cleanup restores the previous hand scale on exit.

Fragment length is doubled relative to 0.8.0. Its short axis is also broadened
by 25% beyond that uniform enlargement. Approximately 80% of the fragment is
exposed outside the cut, instead of roughly half. The grip point and short pull
distance follow the new geometry. The native sprite, perpendicular orientation
and buried-base mask are retained.

Board IDs, seeds, positions, save-provider schema and completion flags are untouched.
Existing saves show the same wounds and progress with the new fragment presentation.
The user verified persistence and carryover in 0.8.0.

## Live checks

- The hand stays small during movement, grip/release, tool changes and hand-slot mirroring.
- Open a vanilla minigame afterwards; its hand must be normal size.
- Fragments are readable, buried portions stay concealed, and the grip is near
  the exposed tip. Pulling clears the mask after a short movement.
- Reopen an existing saved board and confirm the same site positions and progress.

The hand override was confirmed by inspecting the installed game's method body.
Build and regression checks do not replace visual verification of the new proportions.
