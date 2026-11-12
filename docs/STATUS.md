# Current handoff status

Updated: 2026-11-11. This is the short operational handoff, not a release announcement. Verify Git/process/file state before acting; snapshots can become stale.

## Exact stopping point

| Item | State |
| --- | --- |
| Latest installed and pushed gameplay baseline | 0.12.1, commit `c61bcc8`, branch `codex/gel-reach-held-trails` |
| Local source/build | 0.12.2 knife visual-anchor correction, built successfully; gameplay edits remain uncommitted |
| Pending patch origin | Started on `codex/knife-visual-anchor`; documentation handoff is on `codex/project-handoff`, carrying those same uncommitted gameplay changes |
| Why deployment stopped | Automatic approval review could not complete because the account usage limit was reached. Install/package/commit/push command was not executed. This was not an unsafe-action finding. |
| Rule/adapter checks | Last full relevant run: 217 passed on 0.12.0 with all six local WAV checks enabled. Subsequent 0.12.1 and pending 0.12.2 visual edits were built successfully; do not describe 217 as newly run on them. |
| Gameplay validation | User playtested earlier builds. The pending knife correction has not had in-game validation. |

Built DLL SHA-256 at handoff: `83026C4D4E8B13E178509665F425683EE7A9C240CB7B3EEA52CA1F6E3C9364E3`.

Installed DLL SHA-256 at handoff: `E37AF63F2FB34BA13D9A93479A435D8215022A2915BDE20E5A28566680D52B97`.

These deliberately differ. Rebuilds can change hashes; compare actual deployment files, not these strings alone.

## First task for the next coding session

1. Inspect status and the existing 0.12.2 diff. Do not recreate or discard it.
2. Read `KNIFE-ANCHOR.md`. The user marked the tip and explicitly wants the sprite moved to the native hand interaction point, not the action point moved away from the hand.
3. Review the new `KnifeTipVisual` mesh effect and its lifecycle; validate both hand variants/mirroring. The source annotation is a 210 x 630 trimmed image; the native sprite canvas is 256 x 630 with a 31.07612 px left trim.
4. If tools/approval review are available, commit the gameplay patch separately from the handoff docs, package it, and install only after checking the game is closed. Preserve the old DLL and verify the installed hash.
5. Have the user confirm tip/trail/contact alignment. Do not claim visual correctness solely from a build.

Pending source files at this handoff: `CasualtiesSurgery.csproj`, `Plugin.cs`, `Game/SurgeryEffects.cs`, `Game/NativeFragmentMinigame.cs`, `Game/NativeFractureMinigame.cs`. The knife review note `docs/KNIFE-ANCHOR.md` is included in the documentation commit. README is intentionally rewritten by the documentation handoff and is not an uncommitted gameplay requirement.

## Known issues and confidence

- **Knife alignment:** user rejected 0.12.0's moved interaction point. Pending 0.12.2 changes render vertices only and restores contact to native handTransform.position. Build passed; not deployed/tested.
- **Cross-limb suture failure:** user reported mistakes after leaving an incision and treating fragments elsewhere. Stores/rule instances were already separate; exact failure was not reproduced. 0.11.1 changed fragment targeting to evaluate actual wound crossings and added per-wound diagnostics. Regression checks cover cross-limb state isolation. In-game confirmation still needed.
- **Two wounds then three:** logs confirmed stable-ID pagination, with two remaining IDs on page 1 and three on page 2 after reinjury. Not evidence of missing wounds. UX improvement is deferred; do not reset saved IDs as a shortcut.
- **Heal-command edge cases:** healing/reinjury can reconcile vanilla counts while custom open-incision debt remains. Preserve open work; do not interpret console-driven testing as a new production bug without evidence.
- **Gel/infection tuning:** implemented, tested at rule/adapter level; real-game timing, transparency and balance still need feedback. Coverage persists for the operation; native antiseptic time separately decays.
- **Multiplayer:** blocked intentionally by eligibility, not merely untested UI. Earlier Together desync reports are historical and not a current compatibility certification.

## Latest accepted feedback

Keep native health menu/minigame flow, 33% hand scale, generous gesture tolerance, all held non-gel red trails, retained tool selection, full-circle new wound layouts, and saved progress. Gel reach/particle side length doubled in 0.12.1. Persistent green prep overlays disappear when the main incision is complete, without erasing coverage.

The next requested feature direction is internal-bleeding surgery after current interaction polish. Art is being prepared by the user; do not block logic work on final art or commission extra assets without need.
