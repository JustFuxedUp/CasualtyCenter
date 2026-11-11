# Documentation map

Read AGENTS.md and STATUS first for development, then choose one relevant document. No need to read this entire history.

## Living project documents

- [STATUS](STATUS.md): exact pending work, installed versus built version, evidence and known issues.
- [PROJECT](PROJECT.md): gameplay contract, balance defaults, accepted future direction.
- [ARCHITECTURE](ARCHITECTURE.md): code ownership, native findings, persistence and transaction boundaries.
- [WORKFLOW](WORKFLOW.md): model routing, budget discipline, build/test/install/review process.
- [ROADMAP](ROADMAP.md): bounded next work packets with acceptance criteria.
- [LIBRARIES](LIBRARIES.md): pinned CUCoreLib, official API links, Harmony/BepInEx references.
- [ART-HANDOFF](ART-HANDOFF.md): initial pixel canvases. Some incision/internal sizes are provisional; later implementation notes take precedence.
- [SURGERY-KIT-DESIGN](SURGERY-KIT-DESIGN.md): accepted kit concept and unresolved inventory decisions.

## Feature history and targeted playtests

These are versioned records, not necessarily current requirements. For conflicts, use latest user instructions, current verified code and the living documents. Do not blindly reapply an older tutorial or test checklist.

| Topic | Most relevant notes |
| --- | --- |
| Pending knife mesh anchor | [KNIFE-ANCHOR](KNIFE-ANCHOR.md) |
| Doubled gel reach and held-tool trails | [GEL-VISUAL-TUNING](GEL-VISUAL-TUNING.md) |
| Gel/infection/counters | [GEL-AND-TRAILS](GEL-AND-TRAILS.md) |
| Open markers, scalpel loop, end-pivot bones, suture targeting | [SURGERY-REFINEMENTS](SURGERY-REFINEMENTS.md) |
| First fracture MVP | [FRACTURE-MVP](FRACTURE-MVP.md) |
| Direct grabbing and rating audio | [DIRECT-FEEDBACK](DIRECT-FEEDBACK.md) |
| Loose shards and permissive sutures | [GAMEPLAY-FLOW](GAMEPLAY-FLOW.md) |
| Seeded boards and migration | [PERSISTENT-BOARDS](PERSISTENT-BOARDS.md) |
| Earlier UI/gesture revisions | [SURGERY-GRACE](SURGERY-GRACE.md), [VISUAL-SCALE-FIX](VISUAL-SCALE-FIX.md), [COMPACT-SURGERY](COMPACT-SURGERY.md) |
| Native host integration | [NATIVE-MINIGAME](NATIVE-MINIGAME.md), [HEALTH-MENU](HEALTH-MENU.md) |
| Early design/foundation | [FRAGMENT-DESIGN](FRAGMENT-DESIGN.md), [MVP-REVIEW](MVP-REVIEW.md), [MVP-PLAYTEST](MVP-PLAYTEST.md), [REVIEW](REVIEW.md) |
| Future anatomy concept | [BODY-SECTIONS-DESIGN](BODY-SECTIONS-DESIGN.md); its single-wound description is historical |

Machine-specific paths are in ignored work/LOCAL-SETUP.md. Inspect current files/processes before acting on them. Do not commit that local guide or redistribute local native asset exports.
