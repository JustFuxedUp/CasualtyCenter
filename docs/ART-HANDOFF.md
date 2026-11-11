# Art handoff and rotation — 0.10.1

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
