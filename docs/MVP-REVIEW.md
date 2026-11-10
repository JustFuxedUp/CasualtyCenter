# Review: playable fragment self-surgery

## Problem and result

The 0.2.0 inspector could read limb injuries but could not treat them. This MVP
adds a forceps drag followed by three suture drags. Only the last stitch removes
one fragment and applies configurable skin recovery and bleeding reduction.
It uses current wound values so damage or healing during surgery is not undone.

The foundation inspector was verified in-game by the user, including detached
limbs and changing stats. This new surgery UI still requires in-game playtesting.

## Review order

1. `Game/OperationSession.cs`: real field writes, eligibility checks, current
   stats at completion, cancellation, slips and duplicate prevention.
2. `Procedures/FragmentProcedure.cs`: existing extraction/suture rules, unchanged.
3. `Procedures/PrecisionDrag.cs`: pointer corridor, endpoint and retry rules.
4. `Plugin.cs`: tool selection, three stitches, pointer capture, F8/Escape,
   focus/scene cancellation, cursor restoration and transparent UI input shield.
5. `Game/InputGuard.cs`: four PlayerCamera Harmony prefixes block input while the
   panel is open. Startup fails closed if hook setup fails. Closing restores input.
6. `tests/Program.cs`, `tests/SessionChecks.cs`: procedure, pointer and adapter checks.
   `GameStubs.cs` supplies test doubles, not a game simulation. Unity lifecycle,
   input ordering and actual multiplayer detection still require a playtest.

## Scope and balance choices

- Local self-surgery only; CUCoreLib reporting an active multiplayer session blocks it.
- Free tools for now; no recipes, inventory costs or item registration.
- Each operation: one fragment, three stitches, default +5 skin / -1 bleeding.
- Slip: -1 skin / up to +0.2 bleeding; wrong tool or missed initial click is harmless.
- Until all stitches finish, the fragment remains in the actual limb. Canceling
  grants no partial reward; existing slip damage remains. This avoids rewarding
  extraction without suturing and makes completion a single main-thread update.
- Detached/prosthetic limbs cannot be treated; the inspector still displays them.
- The world runs normally. Movement beyond 1.5 units, patient/scene changes,
  death/unconsciousness and changed fragment counts invalidate a procedure.
- No new downloaded libraries: Harmony and Unity UI come from the installed game;
  CUCoreLib remains pinned to 1.1. Its BepInEx dependency version must stay `1.1`.

## Validation

- Production net472 Release build: zero warnings or errors against the installed full-game assemblies.
- 57 standalone procedure, gesture and session checks passed (including intervening damage,
  duplicate completion, invalid patients, detached limbs and multiplayer gating).
- Follow [MVP-PLAYTEST.md](MVP-PLAYTEST.md) for required live validation.

Review the MVP increment against `codex/fragment-foundation`, not the older starter:

```powershell
git diff codex/fragment-foundation...codex/fragment-mvp
```
