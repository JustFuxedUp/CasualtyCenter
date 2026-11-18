# Remote surgeon visuals and release-order retest (unreleased)

These changes are on the development branch after the distributed 0.16.0 build. They are not in the existing ZIP or installed DLL. Use matching newly packaged builds on both peers when this pass is released.

1. As client, pull a shard fully loose, release it, and open the tool wheel immediately. Repeat with left release/right press on the same frame and with a short delay. The extraction must remain committed and score once; it must not reset to the wound. Repeat as host. Opening the wheel while still holding an unfinished drag should continue to cancel that drag.
2. Two surgeons join the same operation. Each should see the other's faded native hand/tool, tinted using the other player's configured Together color. Check both hand slots, tool changes and scalpel tip alignment. Ghosts must not intercept clicks or change tool selection.
3. Draw sutures, cut, apply gel and suction while the other surgeon watches. Expect translucent attempted tool effects; sutures clear after release/cancellation and network confirmation of the visual update. Remote effects produce no extra audio, doses, damage, score or healing.
4. Change Together player color if possible and verify the tint updates. Move smoothly across the field under ordinary latency. Capture video for any offset, jump, wrong tint or lingering trail.
5. Work on different limbs or patients: hands/effects must not cross operation boundaries. In a paged fragment board, remote visuals should appear only on the same page. Check phase transitions, tool wheel open/close, cancellation and departure. Stale hands fade and disappear within roughly 1.2 seconds plus delivery delay; a received leave snapshot removes them immediately.
6. Retain the successful 0.16.0 ownership/progress checks. Consent boundaries remain pending per the user's report; the known heal-command bug is outside this change.

Preserve both logs and note role, procedure, limb, tool, hand slot, wheel timing and approximate timestamp. Visual appearance and actual socket behavior require live testing; isolated tests only verify routing, validation, ordering and clinical isolation.
