# Locale asteroid art, revision 3

This is a Blender art proof for the five approved locale directions. It responds to the owner's feedback that revision 2 still differed from the concepts and had lost the hand-drawn outlines.

## What changed?

The main rock forms were rebuilt against the approved silhouettes and fracture boundaries. Each has a closed rear volume and a new fitted UV painting. Copper has slanted mineral exposures, Chalk has broken strata, Ice has a blunt fractured body, Violet has a larger connected crystal exposure, and Verdant has an off-center banded exposure interrupted by thick cream rind.

The earlier slab-deformation and separate-wedge attempts did not reproduce the intended forms. The history archive preserves those rejected constructions. The selected meshes use reference-led faces with actual depth rather than projected concept artwork.

## Where are the outlines?

Each source and export includes a separate fitted graphite drawing mesh for selected fractures and overlaps. The outer contour is a Blender Freestyle render treatment in the BLEND source. Small previews use their own contour width so the line remains visible at game size.

FBX and GLB do not carry the Freestyle outer contour. Unity integration must apply the project's existing DrawnContour treatment to the surface mesh. The packaged images show Blender's contour setup, not a completed Unity integration.

## What is included?

Each family folder contains an editable BLEND source, FBX and GLB exports, the selected albedo, its color guide and UV layout, preview renders and the exact Imagegen prompt chain. The BLEND sources pack their albedos. The separate history archive contains authoring scripts, generated paintings and rejected geometry.

The comparison sheet places the approved concept beside revision 2 and revision 3. The other views and game-size previews make the simpler rear geometry and remaining differences visible. This is an art iteration for owner review, not a claim of concept parity or acceptance.

## Production work

The proof changes no spawning, density, damage, loot or fragmentation behavior. New silhouettes still require their own collision, volume and covering-sphere bakes during integration. Non-asteroid debris remains a separate deferred arc.

The design and review record live on [the locale asteroid arc](https://github.com/amindell11/astronomical-home/issues/1377).

The `*-guide-uv-layout.png` files preserve the painting inputs. The `*-uv-layout.png` files show final UVs; Chalk’s new ledge wall reuses a slate paint region. Native small renders use 160 × 128 and 120 × 96 canvases, with separately sized outer ink.
