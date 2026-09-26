# Painted asteroid field study

The ten numbered Blender files correspond to the ten original meshes in `SpawnSettings`. Runtime assets are grouped under `Assets/Visuals/Environment/Asteroids/DrawnField/Shape01` through `Shape10`. Each source packs the shared brush albedo and its own tangent-space relief bake, with the high-resolution scuff sculpt hidden for editing.

The original game-space meshes supplied the silhouettes. Four shallow impact bowls per shape add local recesses. Geometry-guided tapered strokes follow 22 selected, separated ridge sections per mesh, with broken impact rims and paired nicks. Scrape fans follow nearby ridge directions; shallow flakes sit next to impact rims. The ten impact layouts are individually placed. Detail is deterministic and fitted to the surface; it is not scattered black texture noise.

The palette, brush albedo, lighting material and 5.5-pixel outline come from the accepted single-rock study. Each shape has its own 1024px normal map at strength 0.85. Large dark regions remain light-dependent. Normal detail does not alter cast-shadow geometry; the shallow bowls do modify the visual mesh slightly.

`DrawnFieldPlayModeTests` creates the field using the existing generator, spawn meshes, scale distribution, drift and collision assets. Its local settings clone limits field/load radius for capture. The editor-only study substitutes the converted surface and adds drawing/outline meshes to the live asteroids, including newly streamed or reused instances. Original production assets, spawning and ship art are unchanged.

The field captures use the existing environment and canonical game-plane camera framing. The main art study uses a single directional light matching the accepted contrast; a separate still retains the existing scene lights. The flight uses the real ship movement and simulation. Batch captures read the native Unity camera directly, allowing the user's other editor to remain open. They contain no composited asteroid imagery.

Validation checks all ten forms occur in the field, source-coordinate proximity, visible rendered detail and actual ship displacement. Production performance, distant-detail handling and a collision/volume rebake remain outside this art exploration.
