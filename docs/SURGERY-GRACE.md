# 0.7.0: full-size window, forgiving gestures and clearer feedback

## Changes

- The native window is back at its original size. Wound/fragment assets, tool
  wheel and hand retain their small scale (default 40%). `SurgeryScale` now controls
  these assets, not the window. Native pointer coordinates no longer need a scaling patch.
- Right-click **outside** the visible window exits; hold right mouse **inside**
  to select tools. A wheel opened inside remains a selection gesture even if the
  pointer moves outside. Exit clicks are suppressed through release to prevent
  interacting with the health UI underneath. The minigame guide explains this.
- Gesture grace is doubled. Extraction doubles grip/corridor/backtracking margins;
  suturing doubles margins around the start, sides, end and backward movement.
  Crossing/coverage requirements still reject a single diagonal or unfinished wound.
- Failed sutures have no health, bleeding or resource consequence. They clear the
  attempted thread and allow retry. Resource waste is a later tool-design decision.
- Sutures draw straight segments between detected outer turns, plus one translucent
  live segment from the latest turn to the hand. The score uses those same turns.
- Fragment art is read from the game's `Special/ShrapnelMinigame` prefab. Only its
  sprite is reused; native prefab scripts are not instantiated or redistributed.
- Grip the upper part of the fragment, then pull out along the wound's axis.
  The sprite moves with outward progress and returns to its exact starting position
  on early release, lateral error, inward reversal, or tool switching.
- Extraction errors retain the prior -1 skin / up to +0.2 bleeding penalty.
  Switching tools adds no new penalty. Fragment removal and healing still commit
  only after a successful extraction and complete suture.

## Review map

- `Procedures/FragmentPull.cs`: upper grip area, parallel travel and reset rules.
- `Procedures/SutureStroke.cs`: configurable grace and turn-commit notification.
- `Game/OperationSession.cs`: suture-stage failures are explicitly harmless.
- `Game/NativeFragmentMinigame.cs`: independent hand/asset sizing, runtime sprite
  reuse, thread rendering and pointer-inside/outside routing.
- `Plugin.cs`: protects the exit click until release and updates the scale setting description.
- `tests/GraceChecks.cs`: doubled margins, turn-based rendering notifications,
  fragment travel/reset, invalid input and completion requirements.

The screenshot supplied by the user is a visual reference for straight thread
segments joined at direction changes, not an asset copied into this build.

## Playtest

1. Check original window size with small hand/wound assets and readable messages.
2. Grip the native fragment near its top, pull parallel, release when clear.
   Test an early release and sideways pull: the sprite must return to its start.
3. Try the wider tolerance near each boundary; verify the path still needs coverage.
4. Attempt several bad sutures. Compare health and bleeding immediately before/after;
   failures themselves must change neither, though the world still runs normally.
5. Watch thread segments form at turns instead of following every tiny mouse movement.
6. Right-click outside to exit; confirm it does not use an item or click the health
   menu behind it. Hold inside to select tools and move outside before release.
7. Open a vanilla minigame afterwards and check its hand scale is restored.

Automated checks cannot confirm sprite alignment, screen-edge hit testing or how
forgiving the motion feels. These need the next gameplay test.
