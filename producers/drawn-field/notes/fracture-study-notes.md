# Asteroid mesh and paint study

The current candidate uses a new Blender mesh with an uneven taper, nine depressions and raised fracture lips. A generated slate/olive albedo carries heavier branching fissures and dark pockets. The existing surface shader supplies lighting and the existing thicker outer shell supplies the silhouette. No new shader or automatic internal screen-edge effect was added.

## Comparison

The two 768×768 turntables render 144 matching orientations at 24 fps with identical camera and fixed lighting. The previous version uses the original asteroid and drawn surface material; the candidate uses the new mesh and painting. They are raw Unity renders, with no image retouching. The page enlarges the framing for inspection; the original PNGs remain linked. Each movie was decoded after encoding to verify its frame count.

The actual-scene clip uses `DrawnAsteroidPaintScenario`, retaining real rotation, ship banking and damage from the existing capture fixture. The ship treatment is unchanged. The prototype replaces visual geometry only: collision and volume still use the original asteroid configuration. It is not ready for a production gameplay rollout.

## What improved, and what still needs work?

The surface marks now stay attached to the mesh and the shadow faces are broader. The fine geometry no longer drives a scratchy internal ink detector. The new reference drives stronger contrast: cool blue-grey faces, darker fractures and amber light-facing planes.

The reference still has more deliberate brush marks and shape transitions. Some crack placement remains driven by the texture wrap rather than each modeled fracture. Spherical UV projection also stretches paint near the poles. Further authored texture placement and asymmetrical sculpting remain worthwhile. No finished art-match or performance claim is made.

## Asset provenance

Editable source: `art/asteroid-study/AsteroidFractureStudy.blend` with packed texture. Unity export, albedo and material: `Assets/Visuals/Environment/Asteroids/DrawnStudy/`.

The texture used the built-in ImageGen tool, with the user-provided fractured-rock reference as style input. The full prompt is saved in `art/asteroid-study/README.md`. Mesh construction and editing were performed in Blender. Production spawn settings and ship assets are unchanged.

## Capture correction

The magenta overlay was traced to the asteroid's `LowLODMESH` child: a minimap-only mesh with no regular material. The capture camera incorrectly included the Minimap layers, which the production world camera excludes. Excluding the three minimap layers prevents that proxy from rendering over the world asteroid. The camera-construction regression failed with Minimap bit 2048 present and passes with the exclusion. Matching gameplay frame 100 changed from 7,853 magenta pixels to zero. This corrects the capture setup; it is unrelated to the painted material.

The correction also removes a ship minimap proxy that contaminated the earlier footage. Ship assets and material treatment are unchanged, but the corrected scene reveals the real ship's darker appearance. Earlier gameplay captures are therefore superseded; no conclusion about the ship's current art match should rely on them.

PR: https://github.com/amindell11/astronomical-home/pull/691


