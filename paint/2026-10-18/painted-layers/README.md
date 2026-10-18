# Painted light and shadow restoration

Follow-up to the authored-region checkpoint on #1016. The owner approved the softer hatch lines, then requested the painted and shadow layers from the procedure and the left-hand reference.

The flat review had disabled facing shadows and diagonal plate sweeps. This pass restores the house recipe's facing ramp and cream-panel light/shadow sweeps, and raises the brush contrast from 1.2 to the procedure's 3.2. Palette placement, authored artwork, geometry, UVs, and the approved line selection stay unchanged. Generated boundary ink remains disabled because the retained artwork already supplies those lines.

Shadow and Light are independent grayscale maps. Colors remain in the palette and neutral region masks, so recoloring does not bake in the lighting treatment.

- `preview/top.png`, `preview/hero.png`: Blender source previews of the restored layers.
- `preview/owner-reference.png`: the owner's selected visual reference.
- `preview/previous-top.png`: the accepted line treatment before this restoration.
- `source/paint.json`, `source/layers/`: canonical recipe and generated masks, including the owner-edit baseline.
- `runtime-bundle/`: generated materials and texture maps for both palettes.
- `checks/`: geometry-lock and mask-preservation reports, canonical bake report and log.
- `producers/`: read-only preview and verification scripts.

Use these paint settings and masks with the source and shared tool implementation in `../authored-regions/implementation/`. Only `Shadow` and `Light` and their baselines change in the layer set. The source SHA-256 remains `e4c3dbf9552873ddf0a461f361d59245da13edf76208f27b0f0e56b3920fa031`.

These are Blender source previews. Unity shader compilation, import, material binding, and lineup approval remain deferred at the owner's request; no in-game approval is claimed.
