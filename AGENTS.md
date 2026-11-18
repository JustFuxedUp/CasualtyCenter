# Casualty Center: agent instructions

## Start small

Read this file and `docs/STATUS.md`, then the user's requested task. Inspect Git status before edits. Read only the relevant sections of `docs/PROJECT.md`, `docs/ARCHITECTURE.md` or a linked feature note. `docs/INDEX.md` is a routing map, not a reading assignment. Do not replay the chat history or read every historical document.

User instructions take precedence over these repository defaults. Preserve unrelated changes, especially an unfinished patch recorded in STATUS. Never infer installed version from the source version string.

## Credit and scope defaults

- Use one agent. Do not spawn agents or switch to a more expensive model without the user's request. Recommendations are in `docs/WORKFLOW.md`.
- Implement the requested slice completely; avoid unsolicited refactors, extra features, dependency refreshes, broad audits and repeated whole-repo scans.
- Search targeted files with `rg`; batch independent reads. Reuse verified API findings in ARCHITECTURE and LIBRARIES before decompiling or browsing again.
- For a bug, collect one concise reproduction/evidence packet, form a concrete hypothesis, then test it. After two substantially similar failed fixes, report the evidence and unknowns instead of spending more on blind retries.
- Documentation-only changes need links/facts/diff checks, not a game build. Cosmetic changes normally need a build and a short playtest checklist. State, healing, infection and save changes need meaningful rule/adapter regression tests plus a build.
- Run the appropriate checks once after the final relevant edit. Repeat only for a new change, failure or unresolved concern. Do not add tests that merely restate constants or duplicate implementation.

## Product invariants

- Native health-dummy entry and native Minigame host; no return to an F8/Unity debug window.
- Retain the player's selected tool. Keep native hand physics, pain/RES sway, hand-slot mirroring and input. Visual alignment must not silently relocate the gameplay contact point.
- State belongs to the exact patient, limb, procedure and wound. Revalidate before writes, use current health values and commit each reward once.
- Interruption preserves completed work, pending closure, gel coverage and actual incision skin debt. Cancellation is not healing. Never discard an open incision to reset a puzzle.
- Preserve stable saved IDs/layouts. Migrate explicitly; do not silently re-roll unfinished operations.
- Keep singleplayer gating until host-authoritative multiplayer is implemented and tested. Never assume Together synchronization from a field write.
- No real inventory consumption, recipes or surgery-kit behavior until that work is requested. Counters currently measure only debug usage.
- Retain concise normal-flow lifecycle logs permanently: entry clicks/validation, startup acceptance or refusal, session/stage transitions and cleanup. Every silent startup return needs a reason. Log at events, not each frame or held input sample; never let diagnostics change a native decision. Passing isolated tests does not cover unobserved live startup paths.

## Delivery discipline

Use the existing `codex/development` branch for ongoing commits and pushes; review through PRs into `main`. Preserve `codex/project-handoff` as history. Do not create a branch per adjustment. Stage specific files; do not absorb unrelated local work. Do not reset, force-push, merge or publish releases without authorization. Keep gameplay fixes separate from documentation work when practical.

## Lessons from accepted user feedback

- Before adding a procedure, trace an existing comparable operation end to end. Account for entry, gel prep, incision cost/refund, mistakes, completion ratings and audio, temporary sutures, interruption and save/load. Reuse production helpers and verify both callers; copying the visible puzzle is insufficient.
- Treat latest explicit tuning as authoritative: distinguish a requested shorter timer from a reported timer bug. Read current PROJECT values before changing balance. Keep per-wound timers independent, reset on the specified action, and test elapsed time through interruption.
- Completion includes visible and audible feedback for every applicable slot and procedure. Suction, bruise healing, fragment gel closure and laceration closure are Good completions, not accuracy contests. Temporary drawn sutures clear on cancellation and completion; final wound art is separate.
- Preserve saved randomized layouts and full-angle rotations. Validate input and rendered geometry together, including rotated wounds and both hands. Do not repair visual offsets by moving native contact points.
- Most upcoming procedures use open surgery: follow ARCHITECTURE's shared lifecycle direction and MULTIPLAYER's authority boundaries. Surface-operation architecture remains undecided.
- Complete authorized work without repeatedly asking for the same approval. Report actual source, committed, pushed and installed states distinctly; a merged historical PR does not include later uncommitted gameplay.
- Usage reset timing is context, not a request to spend the entire allowance or run indefinitely. Keep the requested pass substantive, bounded and reviewable.

Follow `docs/WORKFLOW.md` for build/install. Never kill the user's game. Install only into the one discovered plugin location, with a backup and hash verification; otherwise leave a clearly labeled package. A folder named `Unused` is not proof a BepInEx plugin is disabled. Do not add a duplicate DLL.

Do not commit game binaries, decompiled game source, extracted native art, supplied placeholder audio, local machine paths/configs, caches or backups. Use ignored `work/` for local inspection, and the authorized deliverables directory for user-facing packages.

Before finishing, update STATUS only when state actually changed. Report changed behavior, checks actually run, installed versus packaged status, and remaining playtest uncertainty. A passing rule suite does not prove Unity rendering or gameplay. If tools or approval review block a step, name the unfinished action and actual reason; do not claim it ran or bypass the block.
