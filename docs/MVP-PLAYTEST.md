# 0.3.0 self-surgery MVP playtest

F8 opens the field surgery panel. Choose an attached, organic limb with fragments.
Using forceps, hold left mouse on SHARD, follow the green path, and release at END.
Select Suture and drag START to END three times. The final stitch removes one
fragment, restores 5 skin points and reduces bleeding by 1 (configurable, clamped).
The reward is immediate and applies only once to the treated limb.

This prototype provides free tools, without recipes or inventory costs. It does
not pause the world. A slip costs 1 skin and adds up to 0.2 bleeding, bounded by
missing skin. Missed initial clicks and tool selection do not cause damage.
Closing, Escape, losing app focus, an invalid patient/limb, changed fragment count,
or moving more than 1.5 game units cancels the operation. An unfinished operation
removes no fragments and grants no healing. Any slip damage already incurred stays.
Vanilla extraction remains available. Multiplayer surgery is disabled.

## Before merging

- Verify both plugins load and F8 works in a run and without a player in the menu.
- Use an already-injured test character; confirm one operation removes exactly one fragment.
- Record skin/bleeding immediately before the last stitch and compare afterwards.
  Natural healing, clotting and ongoing damage continue throughout the procedure.
- Complete a second fragment. Confirm no reward from simply opening/closing the panel.
- Try wrong tool, incorrect start, off-path drag, early release, and retry.
- Cancel after extraction and after two stitches; no reward or fragment removal.
- Confirm detached, prosthetic and uninjured limbs cannot start treatment.
- Confirm death/unconsciousness cancels, and a new run does not retain the old patient.
- Check movement, attack, item dragging and background UI clicks are blocked while
  the panel is visible and restored after closing. Pause/death/scene transitions
  need in-game coverage; the world simulation remains live.
- Verify F8 refuses surgery when Together is running or another minigame is active.
- Test several resolutions and window sizes; panel scales to fit.

The new adapter and input patches are compiled against the local game, but the
playtest above is required before calling this release verified in-game.
