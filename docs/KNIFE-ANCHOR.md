# Knife visual anchor correction — 0.12.2

The annotation marks the blade tip at approximately (62,1), measured from the top-left of the 210 x 630 exported image. The earlier 256-pixel width referred to the native sprite canvas, not the trimmed exported PNG. The native texture rectangle has a 31.07612-pixel left offset and 209.88097-pixel width; the corrected mapping includes that trim.

Cutting and trail contact now use the original handTransform position again. KnifeTipVisual shifts only rendered mesh vertices so the annotated tip sits at that position. The mesh effect runs after the wrist fade, respects aspect fitting, and moves the tip to the local origin before native scale, mirror and rotation are applied. It does not change handPos, velocity, mouse input, sway or hit detection offsets. The knife follows the normal game hand response, not a raw mouse teleport.

The effect applies only to the inspected handSharp/handSharpalt sprites while our Scalpel tool is selected. Tool switching redraws it; exiting removes it. Other hand sprites and vanilla minigames are unaffected.

Build validation is complete; Unity visual alignment still needs a playtest. Verify the tip follows the usual hand-controlled contact point, the red trail starts at that tip, and incision dots trigger there. Check both hand slots, moving/rotating the knife, reopening surgery and switching back to forceps or gel. No new change to gel, wound rules or resource usage is included.
