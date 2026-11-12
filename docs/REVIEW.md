# First review: fragment foundation

## Problem and result

The verified F8 starter could display a window but knew nothing about actual
injuries. This change adds CUCoreLib 1.1 and a read-only limb inspector, plus
testable rules for the agreed extraction-then-suture reward. It does not enable
live treatment yet.

## Suggested review order

1. `dependencies.json` and `scripts/Restore-Libraries.ps1`: pinned official
   downloads, checksums, no automatic deployment or surprise version upgrades.
2. `CasualtiesSurgery.csproj`: target net472 matches CUCoreLib; references stay
   local; game and library DLLs do not get copied into our build output.
3. `Plugin.cs`: required dependency, null-safe patient lookup, read-only fields.
4. `Procedures/FragmentProcedure.cs`: extraction precedes suturing; cancellation
   and repeated completion grant no reward; completion uses freshly supplied values.
5. `tests/Program.cs`: missing fragments, invalid numbers, duplicate completion,
   cancellation, boundary clamping and the combined outcome.
6. `docs/FRAGMENT-DESIGN.md`: accepted behavior vs provisional design decisions.

## Review locally

```powershell
git diff main...codex/fragment-foundation
git log --oneline --all
```

## Manual verification still needed

After installing the new build with CUCoreLib, confirm both plugins load without
errors, F8 opens in a run, the limb values match the game's health panel, and
closing/reopening the panel never changes them. Check a menu with no active
player as well. Build and rule checks cannot establish runtime compatibility.

Foundation PR: https://github.com/JustFuxedUp/CasualtyCenter/pull/1.
For the next implementation, see [MVP review notes](MVP-REVIEW.md).

## Automated validation

- Release build: 0 warnings, 0 errors.
- 27 procedure checks passed.
- CUCoreLib DLL and XML hashes verified against the official release assets.
- User verified 0.2.0 loading, F8, changing limb stats and detached-limb inspection.
