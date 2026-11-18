# 0.15.1 smoothing retest

Install the matching DLL on both peers with the game closed; keep exactly one surgery DLL and the existing Audio/config. EnableAuthorityViews must remain true. The tutorial is suitable for this pass. Do not open a vanilla minigame before the first surgery attempt.

1. Test host treating client, then client treating host. With Consent for Feeding enabled, the patient should see the surgeon's actual player name. Accept: the surgeon gets a Proceed popup for the requested operation. Decline: no Proceed popup. Also try waiting beyond 30 seconds, changing the inspected patient, and closing the health screen before answering; stale permission must not start an operation.
2. Open an incision and interrupt. Inspect that patient on the other peer: OPEN should persist during ordinary network delay, then clear after closure. Repeat with an extracted fragment awaiting suture and the SUTURE marker. Switching patients must never carry over the prior patient's markers.
3. On both peers select scalpel, change tools and return to it. Test both hand slots if available. The visible knife tip should match contact with incision dots; the native hand must still sway normally. Capture a screenshot if alignment differs by peer, character or hand.
4. Suction several pools in all three operations. Expect immediate fading while held, then one Good sound/rating when the host confirms completion. Keep holding briefly after completion, move to another pool, and release/repress. No repeated input reset or stuck tool should be needed. Ratings should disappear in about 0.9 seconds. Check gel completion and temporary suture cleanup too.
5. Start treatment farther apart (up to four world units instead of two). During treatment, movement is allowed up to three units from either participant's starting position, subject to the four-unit separation and obstruction checks. Walking away or placing a wall between participants must still stop treatment.

Report role, procedure/limb, selected tool and approximate timestamp for each failure. Preserve BOTH complete BepInEx/LogOutput.log files before restarting; include screenshots for markers/knife/fading. These changes do not enable multiple surgeons on one patient. Tutorial-limited death/unconsciousness cases remain for a normal test map.

## Before the next retest

Verify the startup line in BOTH logs says the intended version before testing. The latest supplied pair actually loaded client 0.15.0 and host 0.15.1, so the client-side 0.15.1 changes were not exercised.

The unreleased extraction-preview pass additionally needs: release a loose shard under client latency and verify it fades at the release position immediately, rather than jumping into the wound; host score/sound should occur once on confirmation. Interrupt or cancel a pending extraction and verify host progress remains authoritative. Do not expect this new preview in the already distributed 0.15.1 package.
