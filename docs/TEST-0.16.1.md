# Casualty Center 0.16.1 client test

Close the game, replace the existing CasualtiesSurgery.dll, and keep one copy under BepInEx/plugins. Keep your existing config and dependencies. The eight WAVs in Audio belong beside the DLL. Artwork is embedded.

Keep Development.EnableAuthorityViews=true for Together testing. Confirm both host and client logs say “Casualty Center 0.16.1 loaded” before testing. The tutorial map is suitable for this checklist.

1. Check all five tool wheel icons and the player tint. In the same operation, check that the other surgeon's faded hand uses their player color and their tool effects are visible. Leave the operation and confirm the overlay disappears.
2. Pull a shard loose as client and open the wheel immediately after release. The shard should fade and score once. Check the shard tip, grip alignment, and mask as it moves. Repeat as host.
3. Check the amber blood pools and green gel effects. Blood pools should remain distinct over the orange open-surgery backdrop.
4. Align both fracture pieces. Check the generic bone's fit and pivot, including how the outer ends clip at the window.
5. Pinch an internal laceration and allow it to reopen. The sprite should ease back to full width. Pinch and suture it, then check that it fades after confirmation, including the final wound before closure.
6. Check the client scalpel alignment in both hand slots if practical.

Save both complete BepInEx/LogOutput.log files before relaunching. For visual issues, include a screenshot or clip with player role, tool, and procedure.
