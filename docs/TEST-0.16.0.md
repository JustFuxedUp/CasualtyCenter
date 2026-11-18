# 0.16.0 simultaneous and cooperative surgery test

Use the tutorial for normal interaction tests. Death/unconsciousness behavior capped by the tutorial must be marked NOT EXERCISED and repeated on a normal test map.

## Install and verify BOTH peers

Close the game before replacing the existing CasualtiesSurgery.dll. Keep exactly one installed surgery DLL, its Audio folder, and existing config/dependencies. Set Development.EnableAuthorityViews=true on every participant. After launch, confirm BOTH logs say `Casualty Center 0.16.0 loaded` before testing. The last supplied pair actually used client 0.15.0 and host 0.15.1; folder names and downloaded ZIP names do not prove which DLL loaded.

Do the first surgery before opening any vanilla medical minigame. Preserve each complete BepInEx/LogOutput.log before relaunching.

## Test order

1. **Solo baseline on both peers.** Open each procedure, use tools, interrupt and resume. Check the client scalpel tip in both hand slots, tool switching, OPEN/SUTURE markers, gel/suction loops and rating fades. A successful local fragment release should fade immediately at its release position; the host-confirmed score/sound may arrive later. Prediction must not unlock suturing or award health before confirmation.
2. **Separate patients simultaneously.** Both players treat themselves at the same time. Complete one operation while the other remains open. Neither should interrupt or change the other's wound, tool, markers or health.
3. **Separate limbs on one patient.** Player A self-treats a fragment or fracture on one limb. Player B requests treatment on a different limb of A. Verify A sees B's name and can approve or decline independently. After approval, B gets Proceed. Both operations should remain active and affect only their own limbs. Different procedures on the SAME limb must remain blocked. Internal-bleeding surgery blocks separate procedures anywhere on that patient; joining that exact internal operation is allowed.
4. **Join the same fragment operation.** A operates on themselves; B selects the same patient, limb and procedure and obtains consent. With three players, use two remote surgeons and a separate patient. Both should see the same wound layout and a two-surgeon count after a refresh. Work on different shards simultaneously. Complete and close one wound while the other surgeon holds another shard; that hold must survive. Confirm each shard reduces the count and grants healing once.
5. **Contested grip.** Try grabbing the same shard, bone or laceration at nearly the same time. Only one surgeon should hold it. The other should see an in-use wound label or tinted reserved bone and be unable to steal it. A reservation race should not cause extra patient damage. Release or cancel the owner and retry with the other surgeon. No permanent lock should remain.
6. **Shared fracture.** Each surgeon grips a different bone; align and release in either order. Both should reach closure once. Have one surgeon close while the other attempts closure just afterward: one skin refund and one treatment result only. Verify a late join does not reset the current alignment or another surgeon's grip.
7. **Shared internal bleeding.** Close different lacerations concurrently, then suture them; divide bruises and suction pools. Six-second timers must not run twice as fast with two surgeons. Completing repairs should bring both views to closure. Only one closing action may heal the bleeding and refund incision skin cost.
8. **Leave and reconnect.** While A holds a wound, B closes their own minigame, changes tool, or disconnects. A's gesture and consent must remain valid. Rejoin with B and verify current progress/layout. Test the reverse roles. A remote surgeon moving beyond their allowed movement should stop only that surgeon; moving the patient out of bounds can stop all treatment on that patient.
9. **Consent boundaries.** Decline the second surgeon while the first works; the first remains active. Test expired approval, switching the inspected patient, and waking an unconscious patient. Each remote surgeon needs their own permission; someone else's approval is not sufficient.
10. **Latency and interruption.** Under ordinary network delay, hold/release tools, rapidly switch tools, and cancel during a pending extraction. Successful local extraction fades promptly, but a host rejection must restore authoritative presentation. Avoid artificial packet editing. Record repeated input-reset logs, disappearing work, duplicated ratings/healing, or stuck reservations.

## Expected limits

- Up to eight participants per operation; capacity tests beyond available players can remain NOT EXERCISED.
- One local minigame per player. No separate spectator mode or remote hand cursors; shared clinical progress and grip reservations are visible through snapshots.
- Other surgeons' confirmation ratings/sounds are not broadcast to everyone.
- Four-unit treatment reach; three-unit movement allowance from each participant's starting position; obstruction checks remain active.
- Internal bleeding retains patient-wide exclusion against separate procedures. Other procedures can run on different limbs.
- Host migration is not supported. Network timing and native UI behavior need live confirmation despite passing isolated checks.

## Useful report

For each issue: timestamp, host/client role, surgeon/patient, procedure/limb, number of surgeons, action being attempted, expected versus observed result, and measured health/count before and after if relevant. Include both complete logs. Capture screenshots/video for alignment, shared layout, reservation indicators or fading. Mark untested steps rather than assuming they passed.
