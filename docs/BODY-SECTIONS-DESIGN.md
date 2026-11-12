# Body sections and procedure composition — design note

The selected limb will eventually expose several anatomical sections. Opening a
section should show that part's relevant wounds and procedure interactions together,
with a small hand and a cursor-origin tool wheel to preserve working space.

The current build displays a placeholder section with a single randomized wound.
Future work must define section selection, how actual limb injuries map to wounds,
stable placement across exits/re-entry, and interactions among multiple wounds.
Random generation must not create extra healing opportunities beyond the actual
injury count, and rewards must belong to an identifiable wound/fragment.

Tool selection follows the Trauma Center-inspired hold-to-open radial concept.
Suturing uses a continuous zigzag across a wound, judged for coverage, consistency
and efficiency rather than separate click-and-release stitches. It should be quick;
demonstrations and playtests will guide thresholds and hand-response tuning.

Art handoff follows confirmation of section dimensions and hand anchors. Likely
assets: section backgrounds, wound states, fragment variants and tool-wheel icons.
The exact pixel sizes, fade boundary and state lists are not finalized yet.
