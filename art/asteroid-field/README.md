# Painted asteroid field study

The ten numbered Blender files correspond to the ten original meshes in `SpawnSettings`. Runtime assets are grouped under `Assets/Visuals/Environment/Asteroids/DrawnField/Shape01` through `Shape10`. Each source packs the shared brush albedo and its own tangent-space relief bake, with the high-resolution scuff sculpt hidden for editing.

The original game-space meshes supplied the silhouettes. Four shallow impact bowls per shape add local recesses. Geometry-guided tapered strokes follow 38 selected ridge sections per mesh, with broken impact rims and paired nicks. Scrape fans follow nearby ridge directions; shallow flakes sit next to impact rims. The ten impact layouts are individually placed. Detail is deterministic and fitted to the surface; it is not scattered black texture noise.

The brush albedo and relief technique come from the accepted single-rock study. The field uses wider fitted ribbons and its own near-black graphite material. Its contour is 7.5 pixels with a stronger lit-side width. The field surface tint reduces the earlier yellow-orange cast while preserving cool shadow planes. Each shape has its own 1024px normal map at strength 0.85. Large dark regions remain light-dependent. Normal detail does not alter cast-shadow geometry; the shallow bowls do modify the visual mesh slightly.

A finer drawing layer adds short hatch bundles, crossed strokes and chisel ticks in 14 separated patches per shape. Marks follow the local surface beside ridges and avoid existing heavy lines. Stroke counts vary with available surface space. The `Fine crosshatching` vertex group isolates these additions in each Blender drawing mesh; the runtime drawing export includes them in the existing mesh and material.

Production spawns these assets: `SpawnSettings` names `FragmentingDrawnAsteroid.prefab`, whose `DrawnAsteroidAppearance` binds each spawn shape to its converted surface, drawing mesh and paint. `IllustratedSceneWiringEditModeTests` checks that every bound surface stays within 2.5% of its source mesh's bounds diagonal.

The saved `AsteroidField` study scene shows all ten shapes with the ship at gameplay distance; `src/Asteroids3D/Assets/Visuals/Studies/DrawnArt/README.md` covers opening and capturing it.

Production performance, distant-detail handling and a collision/volume rebake remain outside this art exploration.
