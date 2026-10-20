# Crimson

`Crimson.blend` is the editable source. Its 39 mesh parts and 33 live Mirror
modifiers retain the approved geometry and `PaintUV` layout. X runs across the
wings, +Y points toward the nose, and +Z points up. Mirrored parts share paint.

## Paint editing

`palettes.json` holds the original colors in linear sRGB. Source material slots
name the starting regions; `regions.json` places the existing green indicators
and nozzle bands. `paint.json` supplies the shared painter settings.

`layers/Shadow.png`, `Light.png`, and `Ink.png` are grayscale masks.
`layers/Region_*.png` holds editable palette coverage. These images are packed
into the source; the shared `Crimson paint layers` node group previews them in
Material Preview. Its labeled palette nodes mirror `palettes.json`.

Edit the external layers, then reload and repack them before saving the source.
Keep `layers/baseline/`: the shared `ship_paint` tool preserves edits against that
baseline when regenerating. Commands and settings are in `art/tools/ship/README.md`.
Regeneration writes a Unity bundle and a review composite; the composite is not
a runtime texture. Repack changed source images after regeneration.

The Unity hull and debris materials retain their identities and lighting settings.
They share the three paint masks and three packed region-weight maps. No FBX or
prefab change is needed: palette coverage follows the existing UVs. Import the
bundle's `Paint/` files in place and transfer its palette/paint properties to the
existing materials, preserving the debris soot and rendering settings.

The canopy uses its separate approved material and reflection artwork. The
retained `textures/Crimson_BaseColor.png` is the prior combined atlas for comparison;
it is no longer the hull material's input. Original references remain packed and
have external copies. `reference/approved-texture-concept.png` records the silver,
red and charcoal direction; `textures/painted-brush-source.png` is the shared brush.

## Evidence

The `evidence/crimson` branch holds review renders and comparisons by stage:
original paint at `producers/codex-blender/crimson-texturing-20260928-010254/`,
UV density under `uv/`, and this grayscale conversion under
`paint/2026-10-19-grayscale/`.

The approved source retains 14 non-manifold edges across `Cube` and `Structural
center web`; no faces have zero area. These authored parts are kept, not repaired.
