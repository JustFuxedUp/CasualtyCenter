# 0.9.0: loose shards and free-form sutures

## Controls and feedback

- Hand scale is 33% of vanilla. The native sprite-update fix remains in place.
- Fragments are about 11% longer than 0.8.1 and about 35% embedded, up from 20%.
- Pull outward until the shard clears the cut. It then stays loose, follows the
  hand freely, and can be released anywhere. Returning over the cut does not re-embed it.
- Release a loose shard to fade it over 0.4 seconds and show its extraction score.
  Releasing before it loosens still resets the failed attempt under the existing rules.
- Debug status paragraphs, numbered per-wound status labels, and START/END/path
  markers are removed from the visible board. A small selection dot, wound count,
  concise controls and scores remain. Diagnostic messages remain in the log.
- Suture strokes may start/end anywhere and move in either direction. At least
  three crossings across the selected wound on release complete the suture.
- A crossing must travel between opposing sides and intersect the wound itself,
  with the existing end tolerance. Jitter around its center or crossing only an
  imaginary extension beyond the wound does not count.
- No required full-length coverage, top-to-bottom progression, fixed endpoints,
  spacing minimum or quality threshold. Quality is scored as feedback; it does
  not block a stroke with three valid crossings. Failed sutures remain harmless.

The current simple rule permits retracing the same portion of a wound three times.
That follows the requested crossing-count behavior; future procedure rules may
choose stricter coverage without changing this default interaction.

## Seeded placement and saves

Wound centers are scattered across each page using deterministic random placement
with center separation, instead of the previous grid. Candidate selection is bounded
and chooses the best spacing found; the automated seed sweep checks at least 110
board units between centers. Art can overlap at the edges; center spacing is the goal.

Positions are computed from seed, page and site ID independently of the remaining
count. Completed or newly added sites do not rearrange surviving sites.

Older grid saves migrate once to the scattered layout. Their seed, wound IDs,
completed sites and extracted-but-unsutured progress are preserved. After migration,
reopen/save/load keeps the scattered arrangement. Save-provider format remains compatible.

Extraction score and suturing score are feedback only. Actual game fragment-count
reduction and healing still commit together when suturing completes, as before.

## Review and validation

- `FragmentPull`: latched loose state, release-anywhere completion and extraction quality.
- `SutureStroke`: three true wound crossings with side hysteresis; free start/end;
  turn-based thread rendering and quality without a completion gate.
- `FragmentBoard` / `FragmentBoardStore`: seeded scattering and legacy layout migration.
- `NativeFragmentMinigame`: loose-shard motion/fade, less UI text and nearest-wound selection.
- `FlowChecks` and updated suture checks: loose-state replay/reset, crossing count,
  dense samples, 200-seed center-spacing sweep and legacy-save progress preservation.

139 automated checks passed. Native screen rendering and feel still need playtesting:

1. Verify 33% hand scale, larger/deeper fragments and clean masking.
2. Pull loose, move the shard sideways/backward, then release: fade and one score.
3. Try free-start sutures in both directions; two crosses fail, three score.
4. Check scattered positions, selecting neighboring wounds and PageUp/PageDown.
5. Load an old board: one-time position change is expected, loss of progress is not.
6. Reopen/save/load again: the new positions and remaining wounds must stay stable.
