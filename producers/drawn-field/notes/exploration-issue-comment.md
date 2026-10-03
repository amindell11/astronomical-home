## What changed after the upside-down capture report?

The gameplay capture camera was on the opposite side of the game plane from the normal camera, exposing the underside and mirroring horizontal directions. PR #691 now reuses the canonical game-plane basis on the normal viewing side. A regression failed on the original framing, then passed; the native hangar camera was checked independently and already views the top. Earlier gameplay footage is superseded.

The new local comparison contains corrected existing-material, B and exploration clips. All three PlayMode runs pass banking, asteroid rotation and hull-damage checks; all 900 motion rows match exactly, with bank spanning approximately -37 to +37 degrees. Fourteen capture tests and three actual-mesh contour graphics cases pass. The latter require a visible contour and zero fragmented interior ink.

## How close is the exploration to the reference?

The new candidate has cream/orange paint, quieter highlights/emission, violet shadows and darker 4.5 px contours. Surface stencil exclusion removes shell fragments inside the asteroid; the native hangar render texture now supplies stencil. A 282-triangle asteroid with flat visual normals gives broader planes. Smoothed fighter normals reduce noisy shading islands without changing flight or collision geometry.

The fighter still has lumpy geometry where the reference has clean panels. Texture suppression alone did not solve that; generated geometric crease marks produced broken detail and were rejected. The visual asset shape and deliberate panel boundaries remain the next art problem. The PR description records these negative results. This remains an exploration, not a production rollout or a final art approval.
