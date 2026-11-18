# Prioritized work packets

Do one packet per task unless the user groups them. Suggested order is a starting point; explicit user priorities win. This file is not authorization to implement the whole roadmap.

## Current priority: multiplayer preparation and integration

The knife correction and initial internal-bleeding implementation below are completed historical packets, retained for context only. Do not implement them again. Current gameplay is recorded in STATUS. The 0.14.0 test build adds a shared native authority view to the existing handlers/transport. Next run [MULTIPLAYER-TEST](MULTIPLAYER-TEST.md), diagnose observed failures and verify actual host/client health confirmation before changing the default gate. Do not repeat the completed view/queue integration from scratch.

## Completed: knife correction

Input: STATUS and KNIFE-ANCHOR. Review/preserve the pending patch, commit it separately from docs, install when permitted and game closed, then confirm visible tip = trail origin = native hand contact. Test both hand slots, rotation, scale and tool switching. Finish by recording actual installed hash/version and the user's visual result. Do not design a new scalpel asset yet.

## P1: close the existing playtest gaps

- Reproduce: incision on limb A -> exit -> extract and suture/gel limb B -> resume/close A. Include save/load once. Capture diagnostic crossing counts if the failure recurs. Do not label this fixed solely because the standalone isolation test passes.
- Confirm 0.12.1/0.12.2 visuals: larger gel reach, prep overlays gone after incision, red trails for held non-gel tools, stitches above effects, [OPEN] cleared only on closure.
- Check gel saved dose and actual infection behavior. Keep balance adjustments narrowly scoped; don't also rewrite resource architecture.

Completion: a short pass/fail record with reproduction, version and remaining issues, not another broad audit.

## Completed: initial internal-bleeding procedure design slice

Before implementation, inspect only relevant native fields, causes, healing methods and save behavior. Known: Body.internalBleeding exists. Unknown: how reliably to attribute it to a limb/section, and how surgery should distinguish external injury changes from its own progress.

Produce a small decision record: eligible patient/section, source of injury count/severity, proposed scalpel/forceps/gel/suture sequence, one-time treatment amount, interruption/save model, mistake effects and multiplayer boundary. Ask the user only for a gameplay decision that evidence cannot resolve. Do not invent separate healing opportunities from a single global scalar.

Then implement one end-to-end placeholder operation: real health-menu entry, reusable native controls, validated live-stat commit, saved partial state, existing feedback/counters and focused regression checks. Keep it singleplayer. No new art dependency or full anatomy overhaul is required.

## P3: art integration in bounded batches

Start with one wound pair, one fragment variant and one section background. Preserve apparent size independently of transparent padding and contact geometry. Confirm the sprite's logical canvas, cropped dimensions, pivot, hand mirroring and visual tip before changing interaction coordinates. See ART-HANDOFF for starting dimensions; its incision/internal-art sizes are provisional.

Custom scalpel/tool checks come after anchor validation. Keep supplied assets local until their distribution status is settled; don't commit extracted native sprites or ripped placeholder audio.

## P4: real tools, liquids and surgery kit

Design item capabilities rather than hard-coding every vanilla/modded item name. Resolve accessible inventories, recipes, capacity/weight behavior, gel liquid units/mixtures and consumption points. Replace debug counters with actual validated consumption only when that boundary is agreed. Test cancellation, save/load, insufficient supplies and duplicate-completion attempts. Preserve free-test mode if requested, explicitly labeled.

## P5: host-authoritative multiplayer

The backend for self-treatment is implemented against inspected Together 5.5 APIs; do not repeat that foundation. Complete view/command routing and live acceptance from MULTIPLAYER. Other-patient access and host migration remain unsupported. Keep the existing multiplayer gate until actual self-treatment tests pass for host/client, duplication, latency, disconnects, death, limb loss, joining mid-operation and native health confirmation. Treat other-patient support as a separate access-policy feature.

## Deferred polish

Improve sparse-page UX without relocating saved wounds; add anatomy-specific bone pieces/translation; tune infection/gel economy and scoring; streamline duplicate UI code only around a concrete need. None of these should block a small requested visual fix.
