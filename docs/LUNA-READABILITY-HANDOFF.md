# Luna readability handoff

This is a task brief for a future Luna session, not a request to launch another agent automatically. The style guide is ready; the source readability pass has not been performed.

## Suggested first task

Read AGENTS.md, docs/STATUS.md and docs/CODE-STYLE.md. Improve readability only in Procedures/SurgeryVisualState.cs and Game/SurgeryPeerVisuals.cs. Keep the current development branch and preserve all existing changes.

Expand compressed declarations and control flow. Add short comments only where the reason is non-obvious: bounded cosmetic state, detached trail arrays, phase/page filtering, stale-visual expiry, player-color tint, knife mesh alignment and teardown. Verify those explanations against the code and docs/MULTIPLAYER.md's "Unreleased cosmetic participant presence" section and docs/ARCHITECTURE.md's "Visual/audio boundaries" section.

Preserve all behavior, field/property names, signatures, literals, timing, alpha values, validation conditions and serialized shapes. Do not rename symbols, extract helpers, change network/state logic, add dependencies or reformat other files. If you find a functional defect, record its location and evidence separately; do not quietly fix it in the readability diff.

Review the ordinary and whitespace-insensitive diffs, verify referenced sections, run git diff --check and one Release plugin build using the established local build setup. No new tests, full-suite reruns, version bump, packaging or installation are needed for a behavior-preserving pass. If the edit is no longer behavior-preserving, report that change in scope and follow the functional validation rules instead.

Finish with a concise explanation of the interactions clarified and the validation results. Follow the user's current Git instructions for committing/pushing. Keep the diff small enough to review as one unit.

## Follow-up candidates, separately scoped

- Game/SurgeryPresentation.cs: queue ordering, completed releases versus unfinished gestures, prediction reconciliation, and cleanup. This is more sensitive; begin with formatting/comments only and preserve every ordering decision.
- Game/SurgeryClientSession.cs: one in-flight clinical request, request identity/retries, independent cosmetic messages and deferred release.
- Game/SurgeryPeerVisuals.cs later art changes: distinguish intentional visual tuning from readability work; do not mix the two in one pass.

Do not start with a whole dispatcher/router cleanup. Its authorization, revisions, resource ownership and clinical ordering deserve a focused review even when the proposed edits look stylistic.

## Useful completion report

List the selected files, two or three non-obvious interactions now explained, the build/diff/link checks actually run, and any questions left for architectural review. State explicitly whether behavior changed. Avoid a long inventory of whitespace edits.
