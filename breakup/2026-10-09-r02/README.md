# Nightshade breakup — retained paint colors

Actual Unity Game View capture at code commit `9f754af303231ec043cd267233e24f04c3c5262b`.
The owner rejected round 1 because pieces became gray instantly and requested retention of the ship palette.
Motion approval remains pending.

![Breakup](Nightshade-breakup.gif)

[MP4](Nightshade-breakup.mp4)

## Color correction

The live imported-mesh probe found varied red vertex-color values on Crimson's debris (roughly 0.02–0.97),
but no vertex colors on any of Nightshade's 13 debris meshes. DrawnSurface receives white where the
vertex color channel is absent, making Crimson's 0.9 soot strength cover all Nightshade surfaces.
This retained about 17% of the painted albedo and caused the observed gray shift.

Nightshade overrides soot to 0.1 in breakup.json and all six debris paint materials. This retains
about 90% of the painted albedo, with subtle darkening. It does not change geometry, textures,
intact materials, animation, fade or runtime code. The source remains editable and locked.

## Verification

The exact same native capture scenario passed (1/1), now visibly retaining the blue armor, pale hull,
purple accents and drawn swaths through burst and drift. Intact, burst and drift frames were inspected.
Both GIF and MP4 encodes were decoded and their duration/frame count verified.
The quality review found no issue with the parameter-only correction.

[Round 1 build scripts, geometry/texture continuity and test evidence](../2026-10-09-r01/README.md)
remain applicable. Rebuild with the numeric settings in this folder, or use RefineBreakupColor.cs
in the stopped editor to update the six existing materials. SootProbe.cs documents the live-mesh probe.

![Intact](intact.png)
![Burst](burst.png)
![Drift](drift.png)
