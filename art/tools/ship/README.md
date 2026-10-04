# Ship art tools

Headless Blender tools that take a ship from a new source file to a role-grouped FBX:
scaffold, source check, geometry lock, review renders, role export and a UV density check.
Blender 5.1+; no extra Python packages. No tool ever saves a source `.blend` — they read
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
| `role.move.<id>`, `role.debris.<id>` | reserved: parsed; the exporter refuses them in this version |

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
| `ship_render` | `--set ortho\|turnaround\|sheet\|scale\|compare`, `--size N`, `--before BLEND` | Workbench review renders of the visual roles; `compare` renders `--before` beside the source and counts changed pixels. |
| `ship_export` | | `<Name>.fbx` and `<Name>.export.json` in `--out`. |
| `ship_uv` | | Texel density per part on `PaintUV`; fails when max/min exceeds `uv_density_max_ratio`. |

## Contract

Exit 0 pass, 3 findings or refusal, 1 error (bad arguments, invalid ship.json, crash).
stdout carries only trailers, written last: `SHIP_VERDICT=pass|fail`, `SHIP_REPORT=<abs json>`
on exits 0 and 3, plus `SHIP_FBX=` (export) and `SHIP_LOCK=` (lock). Prose goes to stderr.
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

## Tests

```bash
python -m unittest discover -s art/tools/ship/tests -v
blender -b --factory-startup --python-exit-code 1 -P art/tools/ship/tests/blender_ship_smoke.py -- --out /path/to/scratch
```

The unit tests cover ship.json rejection, role grammar, exemptions and the exit contract.
The smoke generates a fixture ship with `tests/fixture_ship.py` (no committed binaries), runs
every tool's pass case and one failure case each, re-imports the FBX, and prints
`SHIP_SMOKE_PASS`. No gate runs these tests.
