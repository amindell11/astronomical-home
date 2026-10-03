## What does the ten-shape field pass deliver?

Commit 74844cd2 extends the accepted painted drawing and light-responsive scuff relief to all ten source asteroid shapes, alongside the originals. Each shape has editable Blender art, a fitted drawing mesh and its own normal bake. The native preview includes a generated field still, 9.6-second flight and front/reverse ten-shape inspection views. The flight starts with 74 asteroids, includes every shape, streams new instances and moves the real ship about 31.9 units at full final health. Production spawning/collision and ship art are unchanged.

## What changed during verification?

The initial coordinate conversion mirrored depth; it was corrected and source-coordinate proximity is now asserted for all ten shapes. Tighter ribbon sampling, additional surface offset and more weight removed fragmented strokes on rougher meshes. The quality review found a same-shape pool-reuse bug in the study cache; using SpawnEpoch fixes it at rung 1. The final test explicitly exercises recycled-instance surface restoration and rejects duplicate drawing layers. Follow-up review found no remaining required findings.

## What is verified, and what remains open?

Final field graphics run 20260926-020126: 1 passed, 0 failed. Preceding combined graphics run 20260926-015843: 4 passed, 0 failed. Final native artifacts: results/asteroid-field-study/20260926-090148; the 240-frame 1600×900 MP4 decoded successfully. ReSharper: 0 blockers, 67 report-only findings.

The preview compares the accepted study key with the committed scene lighting. Both use the existing game environment. Editable assets are on PR #691; preview/evidence are copied outside the recyclable slot. Production performance, distant detail, and collision/volume rebakes remain outside this exploration. No merge is requested.
