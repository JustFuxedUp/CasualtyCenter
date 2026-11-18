# Multiplayer integration contract

Release status 2026-11-18: the implementation below is now built, installed and packaged as 0.15.0 at the user's request. Earlier unreleased wording is historical. Use [TEST-0.15.0](TEST-0.15.0.md) for current testing; live acceptance is still pending. Both peers need the same 0.15.0 DLL and AuthorityViews enabled.

Current working-tree addition (unreleased, no version bump or installation): one surgeon may treat another connected player with host-validated consent, reach and exact body identity. See STATUS for source/installed hashes. Together ConsentForFeeding controls the prompt requirement; unconscious patients are exempt, operators must be conscious. A native patient prompt grants one operation; the surgeon reselects after approval. Offers expire in 30 seconds; release/interruption clears the grant. Waking without consent stops further treatment. Permission and both participants' movement are rechecked on commands, renewals and host ticks. A two-unit reach and maximum 1.5-unit movement from lease start apply. Single-surgeon patient leases span every limb/procedure. Spectator boards and simultaneous surgeons are not implemented.

Optional canonical roster `patient` strings are accepted on open and overview requests only. The host resolves the selected live body and never treats the identifier as permission. Overview access is read-only, same-scene and within five units. Responses echo patient identity; clients reject malformed markers and stale request IDs, clear failed reads and expire marker display after two seconds. Applied native health replication uses the actual treated patient from the authoritative response.

Startup guards also exclude ISurgery from Together's reflected native limb-target lookup, preserving occupied/ignore-next checks and resets. Verification now totals 492 procedure checks plus 23 real-Harmony lifecycle checks; plugin Release build has zero warnings/errors. The unreleased changes require fresh-session and two-peer playtests. Remote treatment also uses Together's obstruction check, and disconnected/replaced body registrations are retired without erasing saved clinical progress. Historical build notes below describe the original self-treatment implementation.

0.14.1 startup correction: a scoped Together lifecycle guard bypasses native type-ID notifications for independently networked ISurgery views while preserving native startup/cleanup and other minigames. No native IDs are assigned or renumbered. Thirteen real-Harmony lifecycle checks supplement the 435 procedure checks; live retesting is pending. See MULTIPLAYER-TEST.md and STATUS.md for the current build and installation.

Updated 2026-11-17 for **0.14.0**, an opt-in multiplayer test build. `Development.EnableAuthorityViews=true` selects the shared native command/snapshot view and also enables transport. It uses local loopback in singleplayer and Together in multiplayer. Both switches default to false; the prior local views and multiplayer gate remain the default until actual two-instance validation. `EnableSurgeryTransport` alone still does not enable the test views. Follow [MULTIPLAYER-TEST](MULTIPLAYER-TEST.md).

## Implemented ownership

| Layer | Responsibility |
| --- | --- |
| `OpenSurgery` / `IOpenSurgeryState` | Shared incision preparation, phase gates, actual skin debt and closure refund; existing save fields preserved |
| `SurgeryActions` and stores | Clinical mutations for all three operations; local guards retained, internal host path uses resolved permission and clinical checks |
| `SurgeryAuthority` | Immutable operation keys/envelopes, patient leases, bounded receipt journal and host-computed feedback |
| `SurgeryDispatcher` | Registered targets, authenticated access, revisions, snapshots, host ticks and fault quarantine |
| `OpenSurgeryCommandTarget` / `OpenSurgeryRepairTarget` | Incisions, scalpel samples, fracture rotation, laceration grips, bruises, stitches and suction |
| `FragmentCommandTarget` | Stable-site extraction, suture/gel closure, suction and entry-only blood-pool rolls |
| `SurgeryHostRouter` | Versioned open handshake, authenticated self-patient resolution, target registration and request routing |
| `TogetherSurgeryTransport` | Optional reflected Together 5.5 adapter, strict UTF-8 framing and bounded game-thread queue |
| `SurgeryTogetherRuntime` | Live sender/body mapping, host lifecycle, native health replication and opt-in plugin integration |
| `SurgeryClientSession` | One in-flight intent, immutable retries, snapshot ordering, deferred release and completion/failure events |
| `SurgeryPresentation` | Validated detached replicas, bounded input queue, replaceable held samples, stale-revision retry and ordered terminal input |
| `NativeAuthorityMinigame` / `NativeAuthorityInput` | Shared native presentation/input for all three operations; local hand effects and gestures, host-confirmed clinical state/results |

The previous three local minigames remain available with the test switch off. The new shared view is a real native Minigame/ISurgery, uses the existing hand/contact/effect helpers and layouts, and never writes patient health or persisted procedure state. It captures input into the same production commands for offline/network use. Do not remove the default gate based only on the automated checks.

The view polls authoritative snapshots while active (up to five per second when idle), retaining independent display countdowns without locally ticking clinical wounds. A 32-intent queue coalesces only replaceable samples for the same target; grip/terminal/release/stitch intents retain order. Overflow cancels input. Rejected gestures clear dependent queued work. Stale revisions retry against the returned snapshot up to twice, then reset safely. Stage transitions stop loops, clear temporary sutures and gestures, retain selected tool and rebuild presentation without erasing saved state.

Host-provided fragment scale is part of the snapshot, so client geometry matches gel reach even when local display config differs. A separate read-only overview request updates [OPEN]/[SUTURE] markers without acquiring a patient lease or blocking operation requests. It resolves the authenticated self-body and is refreshed while the native health screen is visible.

## Identity, authority and retries

The host creates a world epoch, opaque patient identity, operation episode and lease token. Each operation key includes the limb index and procedure. Targets pin the exact body, limb and stored state instance; replacement requires a new episode. Authenticated Together sender identity is passed separately from JSON. The runtime verifies that the sender remains in the native player dictionary, then resolves its current Body.

Only self-treatment is implemented. Other-patient treatment requires an explicit consent/access/reach policy; never infer permission from a supplied patient ID. There is one lease per patient, including across limbs/procedures, because internal bleeding is body-wide.

Open requests carry `requestId,type:"open",version:1,procedure,limb`. An identical retry renews its existing lease and returns a fresh snapshot; changed bytes under the same request ID are rejected. The open retry cache holds 256 requests. Commands carry protocol 1, operation, lease, sequence, expected revision, action and serialized payload. Action payloads are capped at 8,192 characters; transport frames are capped at 65,536 UTF-8 bytes, with 128 queued messages.

A journal retains 64 command receipts. Applied/NoChange/Rejected consume sequence; only Applied increments clinical revision. StaleRevision/OutOfOrder do not consume sequence and require the caller to reconsider its intent against the returned snapshot. Exact retries return Duplicate plus the original result/feedback without rerunning clinical code. Evicted sequences cannot execute again. Application exceptions quarantine the target rather than retrying potentially partial health writes. A new lease cannot clear that quarantine.

Leases last 15 seconds; idle clients renew every five seconds. One pending request retries its exact bytes every second, at most five attempts. Timeout clears the client session without undoing clinical progress. Closing during a pending open/action defers release until its reply is processed. The client never predicts permanent health changes.

## Production command payloads

All action objects require exactly the documented fields. Numeric values must be finite. Coordinates are bounded; arbitrary client scores, health totals and Unity references are not accepted.

| Action | Payload | Host interpretation |
| --- | --- | --- |
| `incision.cut` | section,x,y | Discrete incision contact in board coordinates, radius 14 |
| `scalpel.sample` | x,y,seconds,active | Replay held contact segments through ScalpelTrace; cut intersected sections, apply existing fracture/fragment off-target grace/damage |
| `incision.prepare` | section,x,y,seconds | Radius 48 around the incision section; production gel dose/disinfection |
| `incision.close` | samples:[{x,y},...] | Replay normalized SutureStroke, calculate quality, commit closure/refund/infection |
| `bone.grip/drag/release` | bone,x,y | Bone-viewport coordinates, production pivots/width and 5-degree alignment tolerance |
| `laceration.grip/drag` | wound,x,y | Board coordinates transformed into saved wound rotation; long-edge grip, center closure or overpull damage |
| `wound.stitch` | wound,samples | Replay suture evidence and repair a pinched internal laceration |
| `wound.gel` | wound,x,y,seconds | Board contact at a bruise, production dose and completion |
| `fragment.grip/drag/release` | site,x,y | Stable site ID and normalized wound coordinates; production FragmentPull evidence/quality |
| `fragment.stitch` | site,samples | Production suture replay then board/native-health completion |
| `fragment.gel` | site,x,y,seconds | Local wound coordinates, production gel reach/dose and completion |
| `pool.suction` (open surgery) | wound,slot,x,y,seconds | Board coordinates; wound=-1 for outer pools, otherwise internal wound index |
| `pool.suction` (fragment) | site,slot,x,y,seconds | Board coordinates at the saved site layout and rotated pool slot |
| `gesture.cancel` | {} | Clear transient evidence without undoing clinical progress |

Held tool doses and scalpel samples share a host elapsed-time budget, capped at 0.25 seconds per request. The budget begins at lease acquisition, with no free initial credit. Suture evidence is 4–128 bounded normalized points; the view samples while drawing and on release, cancelling rather than truncating overlong evidence. This validates gesture geometry, not proof of actual physical mouse input. Current internal surgery has no continuous off-target scalpel damage; the endpoint preserves that behavior instead of silently changing balance.

Saved pool angles affect suction hit tests. Fragment pool checks occur at each accepted operation entry and do not start new rolls from mistakes during that session. Lacerations retain independent six-second timers; closure resets only its own timer. Interrupted and loaded open incisions are tracked by the host even before a view reopens.

## Snapshots, timing and feedback

Snapshots contain operation identity, revision, monotonic sample number, host time and detached serialized procedure state. State includes layout/IDs, wound phases, pools, gel, debt and timers. These are wire snapshots, not native health replacements. The view must never write them into a patient's live health fields.

Host ticks advance saved laceration timers without an operator. Material phase/pool changes advance revision; display-only countdown changes do not, so countdowns do not starve in-flight commands. Fracture incision scoring time advances only while leased. Client menus do not pause the host clock. Host/save/scene replacement invalidates wire sessions while preserving stored clinical state.

Receipts carry optional `TreatmentFeedback`: Good, Scored or Mistake; host quality; target index; pool slot. Both pool slots, bruise completion, fragment gel closure and laceration closure report Good. Partial doses report no completion. Bone release and scalpel mistakes report their actual outcome. Internal sutures retain their existing Good feedback. Cached retries preserve the original feedback, and the client accepts a correlated completion once, ignoring late duplicate replies. The shared native view consumes this for rating/audio, stops held loops on interruption and clears temporary sutures on completion/cancellation.

## Together integration

The adapter uses verified public sender-aware receiver APIs; it has no compile-time dependency on CasualtiesMP. See [LIBRARIES](LIBRARIES.md) for installed hashes and method evidence. Strict framing validates remaining bytes before reading the payload; network callbacks only enqueue. The plugin pumps commands on the Unity thread.

The runtime respects `MedicalSync.CanSyncHealth()`. Loss of native health authority suspends the host router and invalidates leases; it does not force health updates. Applied results queue the current native health state through `Server_QueueSendCharacterHealth(NetBody,false)`. An Applied duplicate may requeue that absolute state to recover a failed earlier send, but never repeats the treatment delta. Native health and custom procedure snapshots each have one replication owner.

Static receiver callbacks become inert on disposal and can route to a replacement adapter without duplicate registration. Host/client changes and scene replacement clear transient wire state. Actual Together receiver lifetime, native health confirmation and custom-minigame interoperability still require game testing. Host migration is not supported.

## Verified evidence and remaining work

**435 checks passed with eight WAVs enabled; Release net472 build passed with zero warnings/errors.** Production rules, targets, dispatcher, router, client and presentation queue are exercised together. Coverage includes lost open/action replies, exact retries, forged sender, lease expiry/release, restored timers, rotated gestures, held-time limits, both pool feedback slots, deferred close, protocol rejection, detached replicas, queue overflow, rejected dependent input, overview reads and host geometry.

Transport tests use narrow reflected API doubles matching the inspected signatures. An integrated check sends framed requests through the production adapter/router/client and verifies no mutation before the game-thread pump. These are **not socket tests**, actual Unity playtests or proof of two-peer health synchronization. Audio tests decode WAVs; they do not verify playback.

Remaining acceptance work:

1. Run [MULTIPLAYER-TEST](MULTIPLAYER-TEST.md) in real instances: first connection/one-cut smoke tests, then all three procedures as host and client. Verify native minigame hooks, geometry, feedback, lifecycle cleanup and health confirmation. The new Unity view has compiled but has not been played here.
2. Verify save/reload/reconnect, external injury/healing and delayed/retried input. Large historical fragment boards remain subject to the 64 KiB wire limit; report any stuck connection/snapshot case with both logs.
3. Fix observed integration failures before promoting the shared view to the default. Other-patient access and host migration remain separate unsupported features.

| Actual-game scenario | Required result |
| --- | --- |
| Host and client self-treatment | Same authoritative progress/health; native quantization tolerated |
| Lost/reordered packets, delayed old completion | One clinical effect and one feedback event; stale episode rejected |
| Competing operations on one patient | One lease, no mixed body-wide bleeding rewards |
| Disconnect during incision, pinch, suction or closure | Debt/doses preserved; host timers continue; correct resume snapshot |
| Join/reconnect; save/reload open debt | Stable IDs/layouts and no repeated reward/reroll |
| External injury/healing, death, limb loss, respawn, scene change | Revalidate current identity/state; no stale health overwrite |
| Version mismatch or unavailable authority | Fail closed without speculative treatment |
| Host departure | Stop safely; no implicit host migration |
| Presentation under latency | Ratings/sounds once; loops/strokes stop; native contact unchanged |

No inventory consumption is introduced. Future supplies must commit through the same authority boundary.

## 0.15.1 smoothing contract

Treatment reach is four world units, with a three-unit movement allowance for each participant; obstruction and host access checks still apply. Marker snapshots tolerate six seconds between refreshes and clear on selection changes or failed inspections. Local suction preview changes opacity only; completion remains host-authoritative. Good completion removes queued held samples for the completed target while preserving other input.

The installed Together assembly exposes ScavPlayer.playerName. Consent prompts use it, escaping rich-text delimiters. A server reply of type surgery.consent carries the roster patient ID and approved boolean after permission validation; it offers a Proceed button only for the pending patient/view/operator. The notification grants no authority: proceeding performs normal access and lease checks. The verified native Blade sprite is handSprites[13], used by the knife mesh correction instead of an asset-name test. Live acceptance: [TEST-0.15.1](TEST-0.15.1.md).

### Unreleased extraction prediction

A successful local fragment release starts a 0.18-second visual fade at the release position. Preview identity is the exact queued intent plus site within its operation presentation. The preview never edits the snapshot or unlocks suturing/gel; authoritative extraction still does that. Confirmation retires the preview after its fade; rejected actions, cancelled input, overflow, connection failure and view disposal clear it. Final score/audio remain host-confirmed. Ordered drag evidence is retained, so this does not reduce network request latency or add shared participants.
