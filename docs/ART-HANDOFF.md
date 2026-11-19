# Art handoff

## Follow-up artwork and menu pass

Bruise edges feather toward transparency at sprite load. Gel casts rotate through the full angle range, and remote splashes retain a steady angle across refreshes. Blood pools now cover 128 or 98 board units per side, with the same bounds used for suction. The text beneath internal wounds is removed. The incision guide and treatment ratings remain visible.

For an in-game check, inspect a healthy limb, a fragment wound, a fracture, and a chest with internal bleeding. The surgery menu should highlight each clinically available procedure and dim the others. In Together, wait for the host's overview before testing the choices. Check suction near the new edges of both rotated pool sizes and verify the bruise center remains readable against the open field.

## 0.16.2 solo feedback

The shard faces the opposite direction from 0.16.1, with clipping starting at the center seam of the wound. The grip anchor follows the shifted shard. Shard pickups allow more vertical and along-shard reach. Lacerations accept pickups farther from either horizontal side.

Bruise sprites are 78 � 78 and fade after healing. Gel draws at 26�48 units and uses lower opacity. Blood pools retain their original 68/52 sizes and use lower opacity. Open surgery flesh uses #8B4100. Internal bleeding hides the usage counter and retains wound labels and procedure feedback.

## First supplied art pass

The original PNGs live in [assets/art](../assets/art/) and are embedded in the plugin. Both the legacy and authority views use them. Tool wheel icons use the local Together roster color in multiplayer. Gel uses green, blood pools use #FFC900, bruises use an amber tint, and exposed fracture and chest fields use #BE6200.

The 56 � 312 fallback bone uses a sliced sprite. Its shaft fills the existing 56 � 532 bone layout while the ends keep their proportions. The upper piece flips the artwork independently of the forceps contact transform. The supplied shard and small-wound sprites are trimmed at load time, with the visible end positioned by the wound mask.

The laceration uses one sprite and a shared center seam. Forceps narrow it, reopening eases it to full width, and confirmed suturing fades it over 0.3 seconds. The repair view briefly holds the last fade before showing closure. Live suture thread is still drawn separately.

Playtest both surgery views. Check shard grip and masking through extraction, pool visibility over the orange backdrop, bone fit at both angles, tool wheel labels and multiplayer color, and both a reopened laceration and the fade of the final sutured laceration.

## Historical placeholder dimensions in 0.13.7

Sizes are width × height in logical Unity UI units at default settings. Use them as pixel-canvas targets for flat sprites. Transparent padding may go outside the visible bounds. Draw wounds upright with their seam centered so rotation, grips and suturing share the same origin.

| Asset | Current size | Art notes |
| --- | --- | --- |
| Outer minigame frame | 1000 × 1000 | Shared frame. |
| Operating field | 900 × 700 | Shared inner board. |
| Exposed abdomen | 730 × 610 | Background behind the scattered wounds. |
| Open fracture incision backdrop | 300 × 410 | Central incision background. |
| Fracture bone, each piece | 56 × 532 | Vertical. Pivot at the center of the outer short edge. |
| Fracture alignment guide, each piece | 18 × 532 | Uses the same pivot and inner endpoint as the bone. |
| Shrapnel wound | 14 × 120–138 | Default 40% artwork scale. A 64 × 160 transparent canvas leaves room around the cut. |
| Shrapnel shard | about 24–27 × 101–116 | Upright at default scale before rotation and masking. The native source is 26 × 138. Include the buried part. |
| Fracture incision guide / outer seam | 5 × 240 / 14 × 180 | Vertical and centered. |
| Abdominal incision guide / outer seam | 5 × 252 / 14 × 252 | Vertical and centered. |
| Cut marker / disinfected band | 16 × 16 / 10 × 26 | Both incision operations share these sizes. |
| Internal bruise | 58 × 58 | Center the artwork for full-angle rotation. |
| Laceration, open / pinched | 64 × 108 / 12 × 108 | Width animates during the forceps drag. |
| Sutured laceration | 24 × 108 | Overlay canvas. Align its seam with the other states. |
| Blood pool | 68 × 68 or 52 × 52 | One transparent square sprite can serve both sizes. |
| Gel particle | 18–34 square | One 32 × 32 or 64 × 64 sprite can serve the range. |
| Tool wheel | 380 × 380 before scaling | 152 × 152 at default scale. For new icons, use 64 × 64 per tool, with the silhouette inside 48 × 48. |
| Health procedure panel | Up to 280 × 260 or 280 × 222 | Procedure buttons are 256 × 32. Back is 256 × 26. |

Suture strokes and red trails are generated lines. Standard thread is 2 units wide, the shrapnel preview can be 5 units, and the red trail is 1.4 units. Labels, scores, and counters are generated text. The five icon subjects are Forceps, Suture, Scalpel, Gel, and Suction. The native scalpel hand uses a 256 × 630 canvas, with its trim and tip anchor recorded in [Knife visual anchor](KNIFE-ANCHOR.md). For a reusable fracture bone sprite, draw the hinge at bottom center and the inner tip at top center. Rotate it 180 degrees for the upper piece. Each pivot sits 32 units beyond the frame, so 500 units of each bone's length are visible when straight.

## Historical guidance in 0.10.1

## Ready to draw

Use transparent PNGs, with layered source files retained. Dimensions below are authoring canvases at the intended logical UI size, not a promise of physical monitor pixels: Unity scales the native canvas with resolution. Keep pixels crisp; point filtering and sprite sizing will be configured when the assets are integrated. Do not bake UI text, tool cursors, stitches or scores into wound sprites.

| Asset | Suggested canvas (pixels) | Visible artwork / alignment |
| --- | --- | --- |
| Fragment wound: open and closed variants | 64 x 160 | Vertical, centered; cut about 14–20 wide and 120–138 tall. Leave transparent margin for bruising. Same pivot and cut endpoints in every state. |
| Fragment variants | 128 x 160 | Upright long axis, tip at top, about 100–120 tall; draw the entire buried base too. Runtime rotates it perpendicular to the wound and masks the base. Keep width within roughly 80 pixels initially. |
| Body-section background | 960 x 768 | Working board is 900 x 700 logical units; keep anatomy inside that central area. Extra edge art permits cropping. One neutral skin section is enough to start. |
| Tool-wheel icons: forceps, suture, scalpel | 64 x 64 each | Center the silhouette within 48 x 48. Transparent background, no labels. These are menu icons, not replacement hand sprites. |
| Scalpel incision: proposed open/closed states | 64 x 192 | Vertical cut up to 160 tall; consistent center and endpoints. Provisional until incision interaction is implemented. |
| Internal injury overlays: fracture / bleeding site | 192 x 192 each | Keep key art inside 160 x 160; transparent edge padding. Separate intact/injured/repaired layers. Provisional anatomy and interaction anchors. |

Current measurements come from NativeFragmentMinigame: wound length 300–345 multiplied by default viewScale 0.4 = 120–138; red placeholder width 35 x 0.4 = 14. Fragment long axis is 0.84 of that = about 101–116 before masking; width currently follows the native sprite ratio with a 1.25 multiplier. Suggested canvases intentionally leave extra room. They are new asset targets, not dimensions of the game's source textures. Artwork bounds will be integrated separately from hitboxes; transparent padding must not shrink the cut or move the grip point.

Draw assets upright only; runtime supports arbitrary rotation. No need to draw 360 directional variants. Prioritize the wound pair, fragment variants and a neutral body section now. Scalpel icons can follow; hold detailed internal anatomy until we confirm the injury mapping and operation sequence. Hand replacement dimensions need a separate anchor/sprite measurement before commissioning hand art.

## Behavior changes

New boards use seeded 0–360 degree wound angles. Existing saved boards keep their angles and progress. ForcepsGrab plays once when a valid shard grip begins, not on empty clicks or every held frame. The supplied fifth WAV belongs beside the other four under the plugin's Audio folder and is excluded from Git.

## Visibility report to track

User report: after repeated injury/surgery cycles, only two wounds/fragments initially appeared; completing those revealed the rest. No logs remain. Not confirmed as a rendering fault. A plausible cause is pagination: stable wound IDs accumulate across repeated injuries, and each group of 12 IDs occupies a page; completed holes are intentionally retained. Two survivors on the first page can therefore precede more sites on the next. PageUp/PageDown already navigates pages. Added log details for initial/automatic page selection: page, visible count, total remaining and next ID. Preserve this report and reproduce before changing stable layout/paging. This release does not claim to fix the reported visibility issue.

## Next gameplay milestone

Scalpel access, internal bleeding repair and fracture treatment are planned, not implemented here. Before art-specific minigames, confirm the game's actual injury fields, treatment limits, and which internal wounds require incision/closure. Reuse the selected-tool behavior and continuous suture model without adding extra healing beyond real injuries.


## Follow-up: 0.10.2

The visibility report recurred: specifically two fragments appear first, then three after those are treated. The heal command may have been used during these repeated injury/surgery tests. Treat that as reproduction context, not a confirmed cause or a resolved issue. Check counts and page IDs before and after healing on the next reproduction.

Removed the white square exposed when a shard loosened: disabling Unity's Mask also exposed its Image, despite showMaskGraphic=false. The mask now stays enabled and hidden; only the loose shard opts out of masking. Embedded/reset shards opt back in, and the fading copy remains unmasked.

Playtest: pull a shard until loose, move it around, release it, and confirm there is no white rectangle during movement or fade. Also release too early and verify the reset shard remains embedded and clipped.
