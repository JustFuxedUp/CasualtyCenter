# Casualty Center 0.16.3 tester patch

With the game closed, replace the existing CasualtiesSurgery.dll and keep Audio beside it. Keep one plugin copy, the existing config, and the existing dependencies. For Together testing, enable Development.EnableAuthorityViews and confirm both logs say 0.16.3 loaded.

1. Inspect a healthy limb, a fragment wound, a fracture, and a chest with internal bleeding. Each clinically available surgery should light up on its own. Check hover and disabled contrast. Repeat on a remote patient after the host overview arrives.
2. Check that bruise edges blend into the exposed flesh and that internal wounds have no labels beneath them. Confirm the guide and completion feedback still appear.
3. Apply gel repeatedly and watch for varied splash angles. In a shared operation, existing remote gel marks should hold their angle between updates.
4. Clear both sizes of rotated blood pool, including suction near the visible edges. Check that one completion sound plays per pool.

Save both full BepInEx logs for multiplayer issues. Include a screenshot or clip for visual or menu feedback.
