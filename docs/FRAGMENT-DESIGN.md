# Fragment extraction and suturing

## Accepted behavior

One successful extraction followed by one successful suture immediately reduces
bleeding and improves skin health on the treated limb. Repeat for each fragment.
Extraction alone grants no healing reward. Health caps at 100; bleeding floors at 0.
The amount of each benefit remains a balance decision. Tests use +5 skin and -1
bleeding as examples, not approved gameplay tuning.

## Confirmed in the installed full-game assembly

- `Limb.shrapnel` is an integer fragment count.
- `Limb.skinHealth` is a float with a normal maximum of 100.
- `Limb.bleedAmount` is a float; `totalBleedAmount` accounts for blocked bleeding.
- `Limb.Update` clamps bleeding against missing skin health and handles natural recovery.
- `ShrapnelMinigame` already implements extraction. Failed grasp can damage skin
  and increase bleeding. We should preserve the existing option while introducing
  the surgical extraction-and-suture sequence.
- Detached and prosthetic limbs need separate treatment rules; the first organic
  skin procedure will exclude both.
- `PlayerCamera.main.body.limbs` exposes the local player's limb list.
- Unity's `Application.version` reports 1.0 here. It is not evidence that the
  gameplay build is or is not 7.1.0; CUCoreLib provides full-game detection.

These are inspection findings, not redistributed decompiled game source.

## First implementation boundary

`FragmentProcedure` contains the pure sequence and result calculation. It does
not yet write to game fields. The F8 panel reads the actual limb data to verify
the integration points before treatment is enabled.

The initial design commits fragment removal and both rewards together after
suturing. Visually extracted fragments remain staged during the procedure;
cancellation leaves game health and fragment count unchanged. This avoids
invisible pending wounds after save/reload while the prototype has no custom
persistence. Revisit this behavior when implementing interruption consequences.

Before live treatment, the adapter must revalidate the same patient and limb,
remaining fragment, living/conscious patient as appropriate, organic attached
limb, tool/medicine availability, and active operation. It must apply the three
field changes and inventory costs exactly once using current values. A pure
procedure result is not authorization to heal an arbitrary limb.

## Upcoming review-sized changes

1. Current: dependencies, documentation, read-only inspector, tested pure rules.
2. Custom extraction/suture interaction and singleplayer adapter, including
   interrupted actions and validation. Preserve the vanilla extraction option.
3. Register craftable tools and consumable suture supplies; tune rewards.
4. Explicit patient selection and host-authoritative Together integration.

Multiplayer testing must cover treating self/another player, duplicate messages,
concurrent treatments, disconnects, death, layer changes, and joining mid-operation.
