## Root cause found in the asteroid art study

The magenta surface is the asteroid's `LowLODMESH` minimap proxy, not its world material. The prefab puts that child on layer 11 (`Minimap`) with a null regular material; `UI.MeshMirror` copies the collider mesh into it. The capture camera was created with the default all-layer mask, whereas the world-camera prefab excludes minimap layers. Replacing the visible asteroid mesh made the overlapping old proxy especially clear.

Live inspection identified the proxy, and disabling its renderer isolated it. The fix on PR #691 excludes `Minimap`, `Minimap_Ship` and `Minimap_Enemy` in `GameViewEpisodeCapture.CreateRig` (fix ladder rung 1: those proxies cannot enter the world capture). The regression exercises the actual camera creation: red on the old code with `Minimap Expected: 0 But was: 2048`, green after the mask correction. In matching frame 100 of the same new-asset capture, magenta pixels dropped from 7,853 to zero.

This also means earlier gameplay ship captures contained a minimap proxy. Those captures are superseded; correcting the camera exposes the real ship's darker appearance without changing ship assets. MSAA and the painted asteroid material were not the cause. The branch remains an unmerged exploration; this comment records the cause and evidence without closing the issue.
