# Development, review and credit-conscious workflow

## One focused task

Use a short request: intended player-visible behavior, relevant version, reproduction if any, and acceptance criteria. The agent reads AGENTS + STATUS, then searches only the involved code. Historical docs are opt-in. Start with one agent and one reviewable change. Documentation is a shared memory, not a reason to expand every task.

Suggested task prompt:

> Read AGENTS.md and docs/STATUS.md. Implement only [behavior]. Preserve pending work. Acceptance: [2–4 observable outcomes]. Run the checks appropriate to this change once. Update status and report installed versus packaged. Do not add unrelated refactors or spawn agents.

For unexplained bugs, supply build version, limb/procedure/stage, selected tool, whether a save/reload/heal command was involved, and the newest log excerpt. One good reproduction is more useful than repeated speculative edits.

## Model choice for this project

My project-specific starting recommendation is **GPT-6.1 Sol, medium** for ordinary implementation and game-adapter work. Use **Luna, low/medium** for precise small edits, documentation and simple test maintenance; **Terra** is an optional fit for screenshot-led visual tasks or scoped UI fixes. Reserve **Astra** for a bounded difficult diagnosis, save/transaction design or multiplayer authority review after ordinary evidence gathering leaves a real ambiguity. These are routing suggestions, not automatic permissions to change model or spawn helpers.

Official guidance describes Luna as cost-efficient, Sol as balancing capability and cost, Terra as suited to visual/scoped work, and Astra as the most capable option for demanding tasks. Product availability and usage limits vary; this document does not promise a credit multiplier or use API prices to predict a Codex subscription bill. Source checked 2026-11-11: [OpenAI model selection](https://developers.openai.com/api/docs/guides/model-selection).

To escalate, hand over just the reproduction, failing evidence, relevant files/diff, current hypothesis and unresolved question. Avoid forwarding every historical document. Use higher reasoning only for a demonstrably difficult slice, not every sprite/constant edit. No expensive parallel review by default.

## Local build

The repository's ignored `work/LOCAL-SETUP.md` records this machine's paths. If missing, inspect GamePath.local.props and installed tools rather than assuming another machine's directories. Main project is CasualtiesSurgery.csproj, output bin/Release/net472/CasualtiesSurgery.dll. Rule runner is tests/ProcedureChecks.csproj, targeting net10.0.

```powershell
# From the repository, using the current SDK executable:
dotnet build .\CasualtiesSurgery.csproj -c Release --no-restore
dotnet run --project .\tests\ProcedureChecks.csproj -c Release --no-restore
git diff --check
```

Use --no-restore only when dependencies have already been restored. Run the pinned Restore-Libraries script only if needed. Do not download/update CUCoreLib, BepInEx or Harmony for a cosmetic request. Audio checks use optional SURGERY_AUDIO_TEST_DIR pointing to six local WAVs; they verify decoding, not playback.

| Change | Minimum useful validation |
| --- | --- |
| Markdown-only | Check facts, status and relative links; inspect doc diff; no build |
| Visual constant, layering, sprite anchor | Build; targeted real-game acceptance notes; no mirrored constant test |
| Health, dose, phase or save logic | Relevant regression cases + existing runner once; build; real-game checklist |
| Hooks, game update, dependencies | Verify current native signatures, build, inspect startup log and affected gameplay |
| Multiplayer | Explicit host/client scenarios before enabling the gate |

## Installation and packages

1. Discover existing CasualtiesSurgery.dll files under BepInEx/plugins. The directory may be nested (currently under Unused); don't create a second loadable copy. Stop and resolve ambiguity if more than one exists.
2. Check whether CasualtiesUnknown is running. Never kill it. If running, package the update and ask once whether the user will close it; a response is not a substitute for checking the process again.
3. Back up the installed DLL to ignored work/backups before replacement. Preserve relevant logs before a new launch overwrites them.
4. Copy the net472 DLL; retain Audio beside it. Audio contains Bad-Ok, Great, Mistake, SutureStitch, ForcepsGrab and Scalpel WAVs. No new sound changes are required for a code-only patch.
5. Compare SHA-256 of source and installed files. Record installed version/hash separately from built version/hash. Package to the user's designated outputs directory when requested/needed.
6. Report whether it was installed, only packaged, or blocked. Do not call a branch push a PR or a build a playtest. Preserve the old DLL for rollback; warn that older code may not understand new saves before proposing a downgrade.

BepInEx HideManagerGameObject=true was required in the initial setup. The F8 panel is retired; do not resurrect old loader troubleshooting unless current startup evidence warrants it.

## Git and review

Inspect branch/status first. Use codex/ branches; stage explicit task files. Keep unrelated user/pending code out of the commit. Commit/push when authorized by the current development task/session. Do not auto-merge, force-push or publish a release. If the UI cannot create a PR, a verified compare link is enough; don't churn through tools trying repeatedly.

PR description should state the observed problem, resulting behavior, implementation boundaries, tests actually run and live checks remaining. Include dependency docs when adding a library. Keep supplied placeholder sounds and native assets out of Git. Check .gitignore before staging.

## Approval/tool failure

An approval-review usage-limit failure is not a code failure or a safety finding. Do not bypass it. Preserve the built files and exact pending action, tell the user what did not run, and resume only when the tool/access condition is resolved. Do unaffected documentation or read-only work if available; don't claim a successful installation based on an attempted command.

## End-of-task handoff

Update STATUS with only meaningful changes: branch/commit, source versus installed version, actual validation, remaining reproduction, and the next bounded step. Put new durable behavior in PROJECT/ARCHITECTURE and retain release notes as history. Never make the next agent infer progress from a falsely updated version label. Short final response: outcome, checks, install state, one limitation and the relevant review/artifact link.
