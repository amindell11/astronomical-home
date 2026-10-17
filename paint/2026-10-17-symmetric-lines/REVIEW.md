# Symmetrical paint and parallel hood stripe

The owner explicitly replaced the earlier asymmetry preference: paint lines must now be symmetrical, and the white stripe must be parallel to the white hood boundary. The owner closed Blender without saving; this revision starts from the last committed source, 4c42d10b.

BaseRegion uses identical 1024px half-atlases matching the existing Mirror modifier UV offset of 0.5. The forward main-deck hood boundary and stripe are evaluated in world XY, avoiding UV stretch differences. Both stripe edges are constant perpendicular offsets of the same hood polyline: 0.35 units of plum gap, then 0.45 units of white stripe. The centerline is horizontal; the side segments share a slope of 1.1. Geometry and UV coordinates are unchanged.

Scripts and definition.json record the construction. Validation checks all scene objects and other images unchanged, exact half-atlas equality, and a fresh saved-file reload. Existing source findings remain outside this paint-only scope. No in-game integration or merge is included.
