# Ship art tools

Headless Blender tools that take a ship from a new source file to a role-grouped FBX and its
paint: scaffold, source check, geometry lock, review renders, role export, a UV density check
and the paint masks. Blender 5.1+; no extra Python packages. No tool ever saves a source `.blend` — they read
stored data and evaluate copies in a temporary scene; every report re-hashes the source and
fails loudly if it changed.

## Source conventions

- Nose toward +Y, Z up, mirrored across X. One root empty named `SymmetryOrigin` at the
  centre; parts sit under it (nested editing empties are fine) and Mirror modifiers mirror
  across its X plane. Modifiers stay live; Mirror comes before Solidify.
- The paint UV layer is named `PaintUV`.
- Parts are grouped for export by **role collections** in the ship scene (the file's active
  scene). A part may also sit in any number of editing collections.

| Collection | Exports as |
|---|---|
| `role.hull`, `role.canopy`, `role.cores`, `role.ink` | one joined mesh each: `Hull`, `Canopy`, `Cores`, `Ink` |
| `role.collider` | `Collider`, no materials, no contour (optional) |
| `role.sockets` | empties under `Sockets`, names verbatim |
| `role.move.<id>` | reserved: parsed; the exporter refuses it |
| `role.debris.<id>` | one breakup section; exported by `ship_export --debris` |

Only `role.hull` is required. Any other `role.*` name is an error. Visual roles exclude each
other. Objects in the `ignore` collection are fingerprinted and never exported. Visibility
never matters: hidden and excluded parts are checked, fingerprinted and exported.

## ship.json

```json
{"schema_version": 1, "name": "Crimson", "source": "Crimson.blend", "texture_size": 2048,
 "uv_density_max_ratio": 2.0, "contour_roles": ["hull"],
 "exemptions": [{"rule": "scale", "parts": ["Wing root beam"], "reason": "Why this is accepted."}]}
```

Every field is required and unknown fields are rejected. `source` resolves against the
ship.json folder. `texture_size` is a power of two; `uv_density_max_ratio` is above 1;
`contour_roles` names visual roles. An exemption silences one rule for the listed parts — a real
ship that fails a check records it here; nothing is fixed by applying transforms or modifiers.
Rules: `roles`, `origin`, `mirror`, `modifier_order`, `modifier_type`, `scale`, `ngon`,
`normals`, `name`, `visual_role`, `collider`, `missing_file`, `unit_scale`, `uv_layer`,
`uv_density`.

## Paint settings

`ship_paint` reads two strict files beside the source (every field required, unknown fields
rejected) and the brush tile they name.

`palettes.json`: one colour per **palette region** (a material slot name on the visual role
parts) per palette, linear sRGB, every palette naming the same regions:

```json
{"color_space": "linear-sRGB", "default": "crimson",
 "palettes": {"crimson": {"Silver": [0.86, 0.83, 0.71, 1], "Red": [0.83, 0.003, 0.002, 1]}}}
```

`paint.json`: the knobs, Crimson's values shown:

```json
{"schema_version": 1,
 "brush": {"image": "textures/painted-brush-source.png", "scale": 1.3, "contrast": 3.2, "blend": 0.25},
 "ramp": [[0.10, 0.38], [0.55, 0.64], [0.92, 1.0]],
 "sweep": {"weight_x": 0.65, "dark_below": 0.33, "light_above": 0.66, "jitter": 0.5, "dark": 0.61, "light": 1.0},
 "ink": {"inset": 0.003, "rim": 0.0065, "rim_light": 0.35, "wall_facing": 0.65},
 "plates": {"cap_facing": 0.9, "thin_wall": 0.12},
 "regions": {"Silver": {"plate": true, "sweep": true, "ink_walls": false},
             "Red": {"plate": true, "sweep": false, "ink_walls": true}},
 "accents": [{"part": "Canopy", "target": "light", "where": {"x": [0.025, 0.05]}, "value": 1.0},
             {"part": "Service channel floor", "target": "emission", "where": {"x": [0.068, null]},
              "value": 1.0, "color": [0.01, 0.393, 0.0153]}],
 "material": {"shadow_strength": 1.0, "light_strength": 1.0, "ink_strength": 1.0, "light_lift": 0.25,
              "ink_color": [0.00147, 0.00178, 0.00224], "emission_blend": 1.0,
              "lighting": {"_ShadowThreshold": 0.55}}}
```

- **Geometry.** Evaluated parts stay in signed symmetry-origin space. Boundary distances never
  fold X; brush and sweep sampling use |x| so mirrored shared UVs receive the same paint.
  Lengths are source units. The Cycles seed is fixed at zero.
- **Plate.** Each connected group of faces with |n*z| >= `plates.cap_facing` has its own
  boundary. Only those faces receive that boundary's inset ink (`ink.inset`) and inner rim
  (`ink.rim`, Shadow 0, Light `ink.rim_light`) when their region enables `plate`.
  `sweep` adds diagonal thirds on those cap faces: `s = weight_x*sx + (1-weight_x)*sy +
  jitter*(brush - 0.65)`; `s < dark_below` adds Shadow `1 - dark`, and `s > light_above`
  sets Light `light`. `ink_walls` inks faces with |n*z| < `ink.wall_facing` on parts
  thinner than `plates.thin_wall` (each connected piece's smallest axis extent is below it).
- **Ramp.** Shadow from facing: `1 − ramp(|n·z|)`, ease-interpolated through the stops.
- **Brush.** The tile, box-projected at `scale` with `blend`, reads as `factor = 1 +
  contrast·(brush − 0.625)`: below 1 it adds Shadow, above 1 it adds Light.
- **Accents.** `where` bounds (folded x, y, z; `null` is open) on one part. `light` and `ink`
  set that mask to `value`; `emission` writes `Emission.png` and gives the part's regions the
  accent's linear `color` as painted indicators (`_PaintEmissionBlend` =
  `material.emission_blend`). One emission colour per region.
- **Material.** The Unity strengths, the per-region light colour (`_PaintLightColor` is the
  region colour lifted toward white by `light_lift`), the ink colour, and `lighting`: any of
  `_ShadowThreshold`, `_ShadowSoftness`, `_CastShadowStrength`, `_AmbientStrength`,
  `_PaletteLighting`, `_SpecularStrength`, `_PaintSaturation`, `_ShadowColor` (Valis's values
  are the defaults).

**Outputs.** Beside the source: `layers/Shadow.png`, `Light.png`, `Ink.png`, `Emission.png`
(only when an accent emits), 8-bit grayscale at `texture_size`, and `layers/baseline/`, the
tool's own last output with `baseline.json`. On a re-run the owner's edits (each mask minus
its baseline) ride onto the new generation, so hand paint survives new knobs and palettes.
The tool refuses masks that have no baseline (move hand-made masks aside first) and a baseline
whose UVs or texture size no longer match (delete it to start over; edits are lost). Under
`--out`: the report, `preview/Composite.png` (the flat colour composite, for a
`ship_render --textured` look; review only, never imported) and `unity/<Name>/`: `Paint/`
textures with `.meta` (linear, clamped, mipmapped) and `Materials/<palette>/<Region>.mat` with
`.meta`, one drawn-surface material per palette per region binding the masks. GUIDs derive
from the ship name and path; `--unity <visuals folder>` reuses the GUIDs of files already
there. Copy the bundle over the visuals folder, `.meta` files only where none exist.

## Commands

```bash
blender -b --factory-startup --python-exit-code 1 -P art/tools/ship/ship_X.py -- \
  --ship art/ships/<name>/ship.json --out <dir> [tool options]
```

| Tool | Options | Does |
|---|---|---|
| `ship_new` | `--top IMG`, `--side IMG` | Creates the source: `SymmetryOrigin`, role and `ignore` collections, optional reference images (in `ignore`), ortho cameras, the flat review render preset. Refuses an existing file. |
| `ship_check` | | Findings for the rules above plus per-part fingerprints. |
| `ship_lock` | `--mode lock\|verify\|reopen` | Writes `lock.json` beside the source; `verify` fails on changed geometry; `reopen` reports changed parts and the stages they invalidate (`uv`, `masks`, `breakup`) and rewrites the lock. |
| `ship_render` | `--set ortho\|turnaround\|sheet\|scale\|compare`, `--size N`, `--before BLEND`, `--textured` | Workbench review renders of the visual roles; `compare` renders `--before` beside the source and counts changed pixels; `--textured` shows each material's active image texture instead of its flat colour. |
| `ship_export` | `--debris` | Intact: `<Name>.fbx` and `<Name>.export.json`. Debris: `<Name>Breakup.fbx` and `<Name>Breakup.export.json`. |
| `ship_uv` | | Texel density per part on `PaintUV`; fails when max/min exceeds `uv_density_max_ratio`. |
| `ship_paint` | `--palette NAME`, `--unity DIR` | Bakes the paint masks beside the source from `paint.json` and `palettes.json` (Paint settings above), keeps owner edits through `layers/baseline/`, and writes `preview/Composite.png` and the Unity bundle `unity/<Name>/` under `--out`. Needs the geometry lock. |

## Contract

Exit 0 pass, 3 findings or refusal, 1 error (bad arguments, invalid ship.json, crash).
stdout carries only trailers, written last: `SHIP_VERDICT=pass|fail`, `SHIP_REPORT=<abs json>`
on exits 0 and 3, plus `SHIP_FBX=` (export), `SHIP_LOCK=` (lock) and `SHIP_MASKS=` (paint).
Prose goes to stderr.
Lines Blender logs before the script starts are outside the tools' control: a child of
another Blender inherits its `OCIO` variable and logs it on stdout, so launch children
without it. Every output JSON carries `schema_version`, `recipe` (`ship-fp-1`),
`blender_version` and `source_sha256`; reports also carry `tool` and `verdict`.

**Fingerprint `ship-fp-1`.** sha256 over stored data only, for every MESH and EMPTY in the ship
scene sorted by name, hidden and ignored ones included: float32 vertex coordinates, corner and
face indices, shape keys, `matrix_basis`, parent and `matrix_parent_inverse`, and the modifier
stack (type, order, viewport toggle and whitelisted geometry parameters of Mirror, Solidify,
Bevel and Subdivision Surface). Components `geometry`, `uv`, `materials`, per part and
combined; the lock compares `geometry`. No rounding, no evaluation, no world space.

**Export.** The FBX root `<Name>` is symmetry-origin space, -Z forward and Y up. Each role
is triangulated on temporary copies (corner normals carried across exactly) and joined; material
slots are sorted by name. A role in `contour_roles` gets a contour: per part, in part-local
space, each exact float32 position takes the normalized, unweighted sum of the normals of its
distinct split vertices (position, normal and UVs), as Unity splits them on import. The
contour is a separate vertex range after the surface, in a last slot named `Contour`. The
sidecar records each role's parts, slots, UV layers, per-part vertex and triangle ranges for
surface and contour, socket matrices and the fingerprint. Unity's importer welds vertices with
identical attributes by default, which can merge contour and surface vertices; the importing
side decides that setting.

**Debris export.** Requires the current geometry lock. Each visual mesh belongs to one
whole `role.debris.<id>` or a matching `role.debris.<id>.L` / `.R` pair. A pair contains
the same source objects: evaluated triangles with negative symmetry-space X centroids go
left, the rest right. This partitions every triangle exactly once without cutting faces
or applying source modifiers. Each group becomes one mesh, retaining its source material
slots, UVs, surface normals and contours. Mixed hull/canopy groups retain contours only
on the parts whose visual role requests them. Missing, overlapping, empty or nonvisual
groups fail with exit 3. Intact export accepts debris annotations and ignores them.
The sidecar's `set` is `intact` or `debris`; `roles` keys are section IDs for debris.

## Tests

```bash
python -m unittest discover -s art/tools/ship/tests -v
blender -b --factory-startup --python-exit-code 1 -P art/tools/ship/tests/blender_ship_smoke.py -- --out /path/to/scratch
```

The unit tests cover ship.json, paint.json and palettes.json rejection, role grammar,
exemptions and the exit contract. The smoke generates a fixture ship with
`tests/fixture_ship.py` (no committed binaries; its paint settings and brush tile are
generated too), runs every tool's pass case and one failure case each (for paint: non-uniform
masks, a hand edit that survives a re-run, one material per palette per region; a palette
missing a region refuses), re-imports the FBX, and prints `SHIP_SMOKE_PASS`. No gate runs
these tests.
