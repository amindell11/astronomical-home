Exploration for #685 (map #684): compare the existing fighter and asteroid with drawn surface shading, selective contours, and a cleaner cream/orange/violet candidate during real banking, spin, hull damage and native hangar inspection. The user selected B, requested heavier contours, then authorized further exploration on this branch. Production materials remain unchanged; the art decision is still open.

## Why the latest candidate differs

The original textured treatment kept too much baked grey shading and fine geometry detail. The exploration separates saturated paint from neutral surfaces, bounds the main-light palette, quiets highlights/emission, smooths duplicate fighter vertices' visual normals, and uses the asteroid's existing 282-triangle LOD with flat face normals. Darker contours reach 4.5 px on the shadow side; B remains available at 3.2 px. All meshes/materials are temporary, editor-only comparison clones; collision geometry and flight state are unchanged.

Three observed rendering/capture failures also needed fixes:

- The gameplay capture camera filmed the underside and mirrored horizontal directions. `CaptureFraming` now reuses the canonical game-plane basis on the normal game's viewing side (fix ladder rung 1). A regression failed on the original camera and passes on the corrected one. All final comparison clips were regenerated; the native hangar camera was checked separately and already viewed the top.
- Inverted contour shells painted fragmented lines across asteroid interiors. The surface now marks URP user stencil bit 0 and the contour excludes that surface. The native hangar texture needs 24-bit depth/stencil instead of its previous 16-bit depth. Three actual-mesh poses reproduce interior ink without the exclusion, then pass with zero interior ink while requiring a visible contour (rung 1).
- A no-gizmo profile still enabled GameView/collider guides. Capture preparation now requires the selected profile's visibility value (rung 1); its graphics regression demonstrated red then green in the initial round.

## Validation and evidence

- Corrected control, B and exploration PlayMode captures pass real bank, asteroid rotation and damage assertions. The packaged review checks all 900 motion rows for exact equality, including position, bank, asteroid quaternion and health.
- Fourteen capture EditMode tests and three graphics contour regressions pass. Four native gizmo recovery tests passed in the initial round.
- Final clips are 1920×1080 at the existing 50 Hz simulation cadence; the encoder decodes each output to verify its frame count. The local comparison page includes original PNG stills, motion traces and rendering settings outside the recyclable worktree.
- ReSharper changed-line ratchet passed with zero blocking findings (47 report-only/touched-file findings). The combined quality review found no required changes; no review edits were made.
- These scoped results are not full-suite merge proof. No merge is requested.

Reproduce with the warm capture lane: queue `DrawnControlScenario`, `DrawnContourScenario` or `DrawnExplorationScenario`, then route `CaptureScenarioPlayModeTests`. `DrawnSurfaceScenario` remains available. Assemble each producer-emitted frame directory with `scripts/capture/assemble.py --keep-frames`.

## Alternatives and remaining limits

Frontal shell attenuation and a world-space extrusion probe did not remove interior contour fragments; surface stencil exclusion did. A generated UV crease mask from geometric dihedrals added small, broken marks rather than the reference's deliberate panel boundaries, so it was removed. Texture suppression alone left jagged lighting islands; shared-position normals reduce those, at the cost of rounding some panel edges. Read-only mesh data is required because the committed fighter is not CPU-readable in PlayMode.

The existing fighter still has lumpy, detailed geometry instead of the reference's clean panels; this is a closer rendering study, not a finished asset match. Native inspection textures are 768×768. Magenta patches remain in the existing-material control (#619); the exploration is not proof that the underlying defect is fixed. The 1080p/60 fps target and additional-light/quality-tier coverage are unverified; frame dumping and another live editor preclude a solo timing conclusion. Background/lighting work belongs to arc #678; recheck against #681 when available.

Scope conservation: prototype shaders, temporary comparison scenarios, test assembly references, regressions, and the shared capture/hangar corrections required by observed footage. Generated editor settings are excluded. This PR does not close #685 or roll the style out across the roster.

Vocab: none

