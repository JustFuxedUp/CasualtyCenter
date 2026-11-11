# 0.8.0: persistent per-limb fragment boards

## Player changes

- Suture outer width increased from 2.2 to 2.8 wound half-widths. Coverage and
  turn checks remain; messy strokes have more room. Failed sutures still cost nothing.
- Hand defaults to 22% of vanilla size, independently of the 40% wound/tool-wheel
  setting. `Interface.HandScale` is new; existing `SurgeryScale` values are retained.
- Fragments cross the wound perpendicularly. The buried base is masked inside the
  cut; the exposed tip is the grip point. Pull outward across the cut a short distance.
- Each actual fragment on the selected limb receives its own stable wound ID and
  layout. Click a wound with the native hand to select it, release, then begin.
- After treating a wound, the board stays open and selects the next remaining site.
  Finished sites disappear without moving other wounds.
- Up to 12 sites are shown per page. PageUp/PageDown cycle pages with remaining
  wounds when a limb has more fragments. Art is created as pages are visited.
- Exiting and reopening preserves the same seed, sites and progress. An extracted
  but unsutured site stays ready for suturing, including when another site is selected.
- The same state is attached to limb saves using CUCoreLib's save provider API.

## Treatment semantics

The existing combined completion rule remains: actual `Limb.shrapnel` reduction
and healing happen together after successful suturing. Extraction alone marks a
pending suture on the board and hides its sprite; it grants no healing and does
not yet decrement the game's count. This staging is now preserved across interruption
and saves instead of being discarded. One completed site grants exactly one reward.

Persistence follows the game's normal saves. It does not autosave or protect
unsaved gameplay from a crash. Loading an older save restores that save's board
and injuries together, not progress from a later session.

Other mods and vanilla extraction expose a count, not individual fragment IDs.
When that count falls outside surgery, reconciliation retires unextracted sites
with the highest IDs first, then pending sutures if necessary. New fragments get
new IDs/positions; existing surviving sites retain their placement. The identity
of a specific externally removed fragment cannot be inferred from the count alone.

RES, strength, consciousness, pain and native hand-physics calculations are unchanged.
There is still no multiplayer treatment, custom XP, tool consumption or anatomical
section selection in this build.

## Review map

- `Procedures/FragmentBoard.cs`: stable IDs, fixed grid pages with seeded variation,
  completed/pending flags, validation, deterministic external-count reconciliation.
  Generation uses a local integer PRNG and does not disturb Unity's random stream.
- `Game/FragmentBoardStore.cs`: weak per-limb runtime storage and a version-1
  `ILimbSaveProvider`. Restore defers count reconciliation until the save pass ends.
  Payload includes seed, next ID, and per-site state. Invalid duplicate IDs are rejected.
- `Game/NativeFragmentMinigame.cs`: multi-site selection, short perpendicular pulls,
  a rotated stencil mask for buried sprite portions, and independent hand scale.
- `Plugin.cs`: provider registration and the new hand setting.
- `tests/BoardChecks.cs`: stable layouts, retirement/appends, many-site paging,
  actual JSON provider round-trip, pending sutures and cross-limb isolation.

Save API signatures were verified against the pinned local CUCoreLib DLL. The
upstream [saving documentation](https://cucorelib.jimmyking.dev/docs/saving/) could
not be retrieved during this change; do not treat it as validation of this build.
Newtonsoft.Json is referenced from the game, not downloaded or installed separately.

## Validation and live playtest

Automated checks cover 128 procedure/gesture/session/board cases and the actual
provider's JSON serialization. They cannot verify the game's live save callbacks,
sprite masking or input layout. Verify:

1. A limb with several fragments shows that many sites across its board/pages.
2. The fragment crosses the cut, its buried section is hidden, and the pull is short.
3. Complete a site: the count drops once, it disappears, and remaining positions stay fixed.
4. Extract another site without suturing; exit/reopen. Its sprite stays absent and
   the pending suture is selectable. Finish it once; no duplicate healing.
5. Save normally with partial board progress, load, and compare positions/state.
6. Exit mid-pull or mid-suture: only the unfinished gesture resets. Previous progress remains.
7. Test another limb and a fresh run; board state must not leak between patients/limbs.
8. Try a vanilla extraction or new fragment injury between visits; check count reconciliation.
9. Check the smaller hand, rotated masking, and the wider suture allowance at your UI scale.

Review increment: `codex/surgery-grace-and-feedback...codex/persistent-fragment-boards`.
