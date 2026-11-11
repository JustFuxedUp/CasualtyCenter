# Unity/game adapter rules

Inherits the repository AGENTS.md. Read only the adapter relevant to the change and its pure rule class.

- `Plugin` owns one active `ISurgery`; each native operation owns its gesture instances. Only selected tool preference is deliberately shared. Audio loop ownership is scoped to the requesting operation.
- Every live-stat commit must validate patient, attached organic limb, local ownership, operation phase and current wound eligibility. Use latest values; never restore a cached whole-body/limb snapshot.
- `OperationSession` commits fragment healing; `FractureStore.TryClose` commits incision refunds/timer changes. Reuse these boundaries instead of implementing rewards in particle or sound callbacks.
- Save providers use separate per-limb stores. Persist incremental skin cost and gel dose at application, and gate completion exactly once. Keep old-save defaults and deliberate migrations.
- Native `UpdateHandSprite` overwrites hand scale; the scoped postfix reapplies scale. Native `UpdateHandVisuals` owns position/rotation and grip flags. Do not fight it with raw mouse positioning.
- Keep the pending knife mesh-anchor change described in STATUS. Render offsets, physics transforms and contact coordinates are distinct concerns. Test both mirrored hands and screen scaling when changing alignment.
- Stitches render above wound/tool effects. A hidden Unity Mask can expose its backing Image if disabled; loose shards opt out through Image.maskable instead.
- On close, focus loss, stage/tool change and host replacement: stop held audio, reset transient strokes, clean effects, restore hand visuals and suppress the exit click leaking to the world. Preserve persistent clinical progress.
- No UI status spam. Keep concise instructions, floating ratings, requested usage counters and open-surgery markers. Diagnostic details belong in logs.

For an unfamiliar game member, inspect the installed assembly narrowly, document the finding, and keep decompiled output ignored. Do not guess health fields or infer limb-specific internal bleeding from a body-wide scalar.
