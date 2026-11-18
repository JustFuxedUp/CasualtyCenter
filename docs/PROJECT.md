# Project specification

## Purpose and scope

Casualty Center brings quick, readable, Trauma Center-inspired operations to Casualties: Unknown. Players should eventually craft/find appropriate tools and medicines and use skillful interaction to treat otherwise difficult injuries on themselves or others. Present development is an offline self-surgery prototype. Multiplayer, inventory and full anatomy remain future milestones.

The user owns gameplay direction and will provide pixel art. Prefer small, playable increments with placeholder visuals. Do not add realism-driven complexity that conflicts with the requested fast surgical interactions.

## Interaction contract

- Enter through the actual health dummy. Procedures use the native minigame window at full size, not a separate Unity debug window.
- Native hand momentum, pain/RES sway and hand-slot mirroring remain part of play. Default hand scale is 0.33; fragment artwork scale is 0.4. Arm fades below the wrist.
- Hold right mouse for a cursor-origin radial tool menu; release toward a tool. The five tools are Forceps, Suture, Scalpel, Gel and Suction. Keep the selection until the player changes it.
- Right-click outside the window exits and preserves unfinished work. Prevent that exit click from triggering the world. Tool menus cancel transient gestures, not paid clinical progress.
- Sutures stay above wound/effect artwork. Thin translucent red trails track held Forceps, Suture and Scalpel and fade quickly. Gel uses translucent green square particles until art is supplied.
- Successful actions show small fading Bad/Good/Great text, not numeric score popups. Bad <=40, Good 41–74, Great >=75. Mistakes have sound but no rating popup.
- Bottom debug counters measure per-open-session suture distance and gel dispensed; they are not actual inventory charges.

## Fragment operation

Use the real limb shrapnel count to create a seeded board. Wounds have random center positions with spacing and, for new layout-version-2 boards, full-circle rotation. Saved layouts and completed holes remain stable; 12 IDs per page can produce sparse pages after reinjury.

Grip the exposed upper shard area directly. The shard is perpendicular to the wound and partly masked inside it. Pull outward until it loosens, then move/release freely; release scores and fades it. A mistake resets the shard. Extraction is persisted as pending closure, but vanilla fragment count and healing are committed only on closure.

Suture closure accepts any start/end and three actual wound crossings in one held stroke. Accuracy grades the result but is not an extra success gate. Eligible wound crossings are tracked independently per gesture, and the best-crossed wound is selected at release. Failed sutures are harmless for now.

Alternatively, hold Gel on the extracted wound until its saved dose reaches 2 mL. A completed wound removes one real fragment, restores 5 skin points and reduces bleeding by 1 by default, using current live values. Skin recovery and bleed reduction are configuration entries. Never duplicate a reward by reopening, retrying release, applying extra gel or saving/loading.

Extraction mistakes currently cost 1 skin and add up to 0.2 bleeding. This is separate from fracture mistakes.

## Incision and fracture operation

Cut nine objective markers with held Scalpel in any order/path. Productive contacts disappear silently. Each new marker resets a 0.4-second off-target grace. Beyond that, unnecessary active cutting causes continuous tissue damage: 5 skin, 1 muscle and 10 pain per second; no added bleeding. Scalpel, Gel and Suction sounds loop while held and stops on release, wheel/tool change, pause or exit.

The incision costs 60% of skin health measured at its first cut, distributed across markers. Persist the actual skin removed, not a snapshot to restore later. Good/Great closure refunds that debt; Bad closure returns a fraction. Unrelated damage must remain. Partial cuts, skin debt and phase survive interruption.

The interior has two extended bone pieces hinging beyond the top and bottom edges of the minigame frame. Art is clipped at the frame, and either visible piece can be gripped. Align both within 5 degrees so their inner tips meet at center. Failed release adds 2 fracture timer, 25 pain and 2 muscle damage, without bleeding. On eventual suture closure, reduce the live fracture timer to 25%, keeping it positive; do not call MendBone or clear broken. A genuinely new fracture may start a new episode, but reinjury must not erase an open incision.

Incision score measures elapsed game time from first cut to finished outline, regardless of route; it includes idle/wheel time in the screen but excludes time away/paused. Alignment grades final angle. Closure uses suture accuracy. These are prototype balance choices, not immutable targets.

## Gel and infection

Flow: 2 mL/s, including off-target waste in the debug counter. Fragment dose: 2 mL. Incision: nine sections at 0.4 mL each; 3.6 mL productive coverage. Reach currently doubles the original values: 44 logical units around fragment wounds, 48 around incision sections. Coverage and partial doses persist.

Gel uses the native grasp-hand visuals. It closes extracted-fragment wounds or disinfects incision sections; it does not substitute for closing a large fracture incision. Productive application refreshes native antiseptic time (30 s fragment, 60 s incision). It does not directly zero infection or add a general vitals-healing bonus.

Incomplete preparation can start/worsen infection at successful closure. Prototype risk is 0.30 × untreated fraction × immunity factor × accuracy factor × native infection multipliers, bounded to 0–1. Immunity factor is max(0.25, 2 − immunity/100), with native immunity bounded 0–200. Accuracy factor is 1.5 − score/100. A triggering roll adds 1–5 infection points and sets infected, respecting native infection settings. Full coverage suppresses this extra surgery roll; ordinary game infection remains active.

Fracture accuracy averages incision, recorded alignments and closure. Fragment sutures average extraction and closure. Gel closure is fully prepared. Rules and exact formulas live in GelRules/FractureState; update this specification when balance changes.

## Internal bleeding and suction

Use the chest health-menu entry when native internal bleeding exceeds 0.08 L/min. Previously opened abdominal incisions can still be resumed. At default blood scale this is about 9.36 native points. Use the internal contribution to the HUD rate before blood regeneration or external bleeding. One open internal operation belongs to its original torso entry. The nine cuts share fracture surgery's skin cost of 60% of skin health at the first cut. The procedure records actual skin debt and the native body-wide bleeding value at the first cut.

Each new board places two to five labeled wounds, with one additional wound per 0.03 L/min above the threshold until capped. New boards use the shrapnel placement algorithm with spacing and full-angle wound rotation. Positions and angles are saved. Gel each bruise with 2 mL. Hold the forceps on a laceration's long side and drag to the center. Wound width tracks the distance across the seam up to its original width. Overpulling adds 0.25 internal bleeding points and costs 1 muscle point. The pinched seam must be sutured with three crossings in one held stroke. Open lacerations have independent spill timers with a six-second repeat. Dragging the forceps to the seam grants that laceration a fresh six seconds. After six seconds without a successful suture the wound reopens and spills. Repaired lacerations stop spilling. Timers use elapsed real seconds while the game is unpaused. They continue when the operation is interrupted and persist across saves.

Bleeding tasks can roll zero, one or two translucent red squares. Each pool keeps its rotation and partial suction progress across saves. Suction clears each pool after 0.25 seconds of contact. Pools block their associated tasks until cleared. Incidental pools roll once per bleeding task and stay cleared. The fragment board evaluates existing bleeding only on entry. Lacerations can refill their primary square.

Internal incisions share fracture gel coverage, prep overlays, the native 60-second disinfection refresh, and the closure infection roll. Both procedures use the same live thread renderer. Drawn threads disappear on completion or cancellation. Repaired lacerations display the sutured wound graphic. Completed suction, bruise gel, fragment gel closure, and laceration closure show Good.

The final incision suture refunds the recorded skin debt using the fracture closure quality rules, up to the skin cap and reduces current internal bleeding by up to the amount recorded at the first cut, with a treatment floor of 0.04 L/min. Closure leaves a rate already below the floor as it is. The operation commits once. Partial cuts, repairs and pools persist with the original torso section.

## Future direction, not implemented
- Anatomy sections: several possible sections per body part; appropriate internal layouts, translated/misplaced bone segments and varied operations later.
- Surgery kit: craftable fixed-weight kit whose contents add effectively no encumbrance; vanilla/modded items/liquids can supply capabilities. Recipes, capacity, access rules and consumption/reservation are undecided.
- Tools may be dragged onto a limb or checked from the surgery menu; this is still open design, not permission to add both flows now.
- Multiplayer surgery needs explicit patient identity and host-authoritative, once-only treatment/resource commits. Together compatibility must be verified against the installed version.
- Final textures and tool art come later; use ART-HANDOFF for initial canvases, and confirm anchors before commissioning hand sprites.
