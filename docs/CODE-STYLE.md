# C# readability and explanation guide

This guide applies to new code and the small area being edited. Existing compact code is not a request to reformat the repository. Follow [AGENTS](../AGENTS.md) for scope and [WORKFLOW](WORKFLOW.md) for verification and delivery. User instructions take precedence.

## What the reader should be able to answer

For a method that changes state, make these points apparent from names, structure and a short comment where needed:

- What triggers it?
- Whose state does it own: patient, limb, operation, participant or local view?
- Which checks must pass before it acts?
- Does it change health, saved progress, temporary input or only appearance?
- What survives cancellation, retry, disconnect or another surgeon's action?

Do not add a five-item comment to every method. Explain the parts that are otherwise easy to misunderstand.

## Formatting

Use four spaces, braces on their own lines for block bodies, spaces around operators and after commas, and one statement per line. Put separate fields/properties on separate lines. Keep related declarations together, separated by a blank line from the next responsibility.

Expand compound control flow: a line should not validate, mutate state, send a command and return. Use braces for loops and conditional bodies that mutate state or contain multiple actions. Short, obvious expression-bodied getters and forwarding methods are fine.

Aim for lines around 100-120 characters when practical. Break long signatures, conditions and object initializers at natural boundaries. This is a readability preference, not a mechanical limit. Keep one consistent wrapping style within the edited method.

For example, expand this existing pattern:

```csharp
if(disposed)return;disposed=true;queued.Clear();extractions.Clear();
```

into:

```csharp
if (disposed)
{
    return;
}

disposed = true;
queued.Clear();
extractions.Clear();
```

Formatting must preserve evaluation order, short-circuit behavior, event subscription order and cleanup order. Do not turn several checks into eager local variables when some checks depend on earlier ones succeeding. Do not replace Unity null comparisons with ReferenceEquals, or vice versa, as a style change.

## Names and structure

Use PascalCase for types/public members and camelCase for local variables/private fields, consistent with nearby code. Prefer names that identify the subject: operatorBody, patientBody, participant, releasedFragment, authoritativeSnapshot. Short x/y coordinates and small loop indices are fine; avoid unexplained one-letter names across a long method.

Include units or coordinate meaning where ambiguous: elapsedSeconds, localPoint, worldPosition, snapshotAge. Use named constants for shared limits or meaningful tuning values when changing behavior intentionally; do not extract every literal during a readability pass.

A method should tell one coherent story. Separate validation, state changes and feedback with whitespace before introducing helpers. Extract a helper only when its name explains a reusable operation and doing so does not hide ownership or ordering. Readability work is not authorization to introduce new abstraction layers.

Do not rename serialized fields/properties, network keys, enum values, reflection targets, Harmony hook signatures or Unity lifecycle methods for style. Renaming a private/local symbol also belongs in a separate, explicitly scoped change when it would make a formatting diff hard to audit.

## Comments that earn their space

Explain **why this behavior must exist**, especially the failure that would result from simplifying it. Useful subjects include ownership boundaries, native API constraints, ordering, identity checks, duplicate requests, coordinate transforms and saved-state compatibility.

A good default is one to three sentences immediately above the relevant block: the requirement, the failure it prevents, and optionally a documentation pointer. Use a class-level comment for a boundary shared by all its methods; do not repeat it above every method.

Examples based on this project:

```csharp
// Opening the tool wheel must not discard a completed release that is still
// waiting for the host. Keep its preceding grip/drag evidence in order, then cancel
// the unfinished gesture. See docs/MULTIPLAYER.md, "Unreleased cosmetic participant presence".
```

```csharp
// Move rendered knife vertices only. Moving the native hand/contact transform
// changes hit detection and breaks the accepted alignment with hand sway/mirroring.
// See docs/ARCHITECTURE.md, "Visual/audio boundaries".
```

```csharp
// A cosmetic hand update must not renew a treatment lease. Otherwise a client
// could keep treatment access alive by sending cursor positions alone.
// See docs/MULTIPLAYER.md, "Unreleased cosmetic participant presence".
```

Avoid comments such as `// Clear the queue`, `// Loop through participants` or `// Check if null`. They repeat syntax. Avoid speculative explanations, old model names, conversation transcripts and unsupported claims that something is safe. If the reason is uncertain, record the question and the evidence needed in the handoff instead of inventing a rationale.

Keep existing useful comments. Update a comment when behavior changes; an obsolete explanation is worse than no explanation. TODOs should name a concrete unresolved condition and point to the issue/document that tracks it. Do not sprinkle TODOs for hypothetical future features.

## Documentation pointers

In C# comments, use a repository-root path plus the exact section title, as in the examples above. In Markdown, use relative links with stable heading anchors. Avoid local drive paths, fragile line numbers or huge quoted excerpts. A pointer supplements the local explanation; it does not replace it.

Use the narrowest relevant source:

| Question | Read |
| --- | --- |
| Who owns runtime objects and saved state? | [ARCHITECTURE: runtime ownership](ARCHITECTURE.md#runtime-ownership), [persistence and transactions](ARCHITECTURE.md#persistence-and-transactions) |
| Why are participant inputs separate? | [MULTIPLAYER: shared ownership and input](MULTIPLAYER.md#shared-ownership-and-input) |
| How do commands, retries and identity work? | [MULTIPLAYER: identity, authority and retries](MULTIPLAYER.md#identity-authority-and-retries) |
| Why do remote visuals grant no authority? | [MULTIPLAYER: cosmetic participant presence](MULTIPLAYER.md#unreleased-cosmetic-participant-presence) |
| Why are hand/contact/mesh coordinates different? | [ARCHITECTURE: visual/audio boundaries](ARCHITECTURE.md#visualaudio-boundaries) |
| Where are native-member findings recorded? | [ARCHITECTURE: native findings](ARCHITECTURE.md#native-findings-already-verified-locally) and [LIBRARIES](LIBRARIES.md) |
| What is deployed or still unverified? | [STATUS](STATUS.md) |

STATUS is a handoff record, not the long-term source for a code invariant. If a necessary explanation exists only there, move the durable explanation into the relevant architecture/feature section and link it; retain historical delivery evidence in STATUS. If a heading moves, repair its comment/document references in the same change.

## Budget-conscious adoption and verification

Work on one responsibility and usually one to three files at a time. Avoid combining formatting, renaming and functional changes. Do not install a new formatter or run one across the repository just for this guide.

For a formatting/comment pass:

1. Read AGENTS, STATUS, the selected files and only the documentation sections needed to explain them.
2. Establish that each new explanation matches the actual code/callers. Inspect a linked test if it supplies the relevant invariant.
3. Expand compressed statements and add only necessary explanations. Preserve symbols, literals, operators, branch order, signatures and serialized shapes.
4. Review both the normal diff and a whitespace-insensitive diff; the latter is a review aid, not proof of identical behavior. Run diff checks and verify documentation references.
5. Build after editing C#. For formatting/comments alone, do not add tests or repeatedly run the full suite. If behavior or ordering changed, classify it as a functional change and use the existing relevant regression checks.
6. Report the files made clearer, important interactions explained, checks actually run, and any unresolved questions. Do not bump versions, install or package a readability-only pass.

## Escalation boundary

Luna can format existing methods and annotate behavior supported by code and documentation. Sol can handle bounded implementation changes that follow established patterns. Seek architectural review when an explanation would require changing ownership, lease/revision rules, saved-state migration, clinical commit order or retry semantics. Report the specific uncertainty and evidence already collected; do not restart a broad audit.
