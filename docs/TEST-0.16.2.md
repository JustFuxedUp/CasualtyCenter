# Casualty Center 0.16.2 tester patch

Close the game and replace the existing CasualtiesSurgery.dll. Keep one copy under BepInEx/plugins, retain the existing config and dependencies, and keep Audio beside the DLL. Sprites are embedded. For multiplayer, enable Development.EnableAuthorityViews and confirm both logs say 0.16.2 loaded.

1. Check rotated fragment sites. The wide end of the shard should be exposed and overlap halfway into the wound at the seam. Grab above and below its center, pull it clear, and release. As client, also open the wheel immediately after release and check that it scores once.
2. Grip the side of a laceration from slightly outside the sprite. Drag inward to pinch, let it reopen once, then suture it and watch it fade.
3. Check the larger, lighter green gel marks, larger bruises, darker surgery backdrop, and more transparent amber blood pools. Clear a pool and check its completion sound.
4. Heal a bruise with gel and watch it fade. Try making a bruise the last repair before closure to check that the fade still plays. Internal bleeding should have wound labels and action feedback, with no DEBUG usage counter.

Repeat the relevant interactions in the solo and Together views. Preserve both full BepInEx logs if reporting a multiplayer issue. Include a screenshot or clip for alignment and contrast issues.
