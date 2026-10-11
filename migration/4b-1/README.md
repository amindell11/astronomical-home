# Slice 4b-1: Valis source onto the ship conventions

Evidence for #983 (slice 4b-1 of #868). The source edits are in `art/ships/valis/Valis.blend`
on branch `task/valis-source`, as one commit per edit. Each edit is scripted here and was run
headless in Blender 5.1.2 against the commit before it. The pre-edit source is main at
`b511a4d2` (LFS oid `0db0adda…`).

| Edit | Script | What it does |
|---|---|---|
| 1 | `scripts/edit_1_wrap_turn.py` | Adds a new identity root, `SymmetryOrigin`. `Valis symmetry origin` goes under it, keeps its X scale and tilt, and gains the 180° turn about Z. All 30 Mirrors retarget to the root. |
| 2 | `scripts/edit_2_uv_swap.py` | Moves native charts V − 0.5 and flips each Mirror's `offset_v` from −0.5 to +0.5. Renames `ValisPaintUV` to `PaintUV`. |
| 3 | `scripts/edit_3_roles.py` | Adds `role.hull`, the `role.move.{upper,lower}_wing.L/.R` pairs, 14 `role.debris.*` pieces and `role.collider` (today's `Valis collider.asset`, inverse-mapped). Puts the joined previews under `ignore`. |

## Checks

| File | Shows |
|---|---|
| `checks/shape-check.json` | Final source against pre-edit. Every part's evaluated world vertices, plus every empty's frame, match after undoing the turn: 36 meshes and 15 empties, max 2.07e-6 against a 1e-5 tolerance. The only additions are `SymmetryOrigin` and `Valis collider`. `shape-check-edit1/2.json` are the same check after edits 1 and 2. |
| `checks/uv-check.json` | Each part is evaluated up to its Mirror. Every mirrored corner is the exact V − 0.5 copy of its native corner before the edit, and the exact V + 0.5 copy after. With the full modifier stack, the swap is exact loop for loop (0 mismatches on 30 parts). It also records `full_stack_symmetry_gap`: 05 and 61 are 0.0025 and 0.027 units off world-mirror symmetry after Solidify. That gap is in the approved shape and unchanged by this slice. |
| `checks/collider-check.json` | The source collider, mapped back to Unity hull space, matches the asset's stored AABB within 6e-8. Today's hull bounds sit inside it. |
| `checks/ship_check.json` | 19 findings exempted in `ship.json`: scale ×15, modifier_order ×4. One open finding: a non-planar pentagon (deviation 0.0066) on `01 central fuselage`, awaiting the owner's ruling. |
| `checks/ship_uv.json` | Pass, density ratio 1.934, with 61 and 62 exempted. |
| `checks/paint-side-score.txt` | High-passed luminance correlation against today's swept game capture (`../4a/baseline-main/Valis-0-swept.png`) and against its mirror image. Pre-edit source: 0.784 same / 0.870 mirrored. Edited source: 0.870 same / 0.786 mirrored. The paint now sits on the game's sides. |

## Renders

- `renders/paint-compare-top.png`: today's game, the pre-edit source and the edited source, as EEVEE top views with nose up.
  `before-top.png` and `after-top.png` are the single tiles.
- `renders/paint-zoom.png`: the most side-specific paint window, at 3×. `paint-zoom-where.png` marks where it is.
- `renders/sheet.png`, `top.png`, `scale.png`: `ship_render` review renders (Workbench flat material colours, so no masks).

The Blender renders are previews, not in-engine. The game look changes only in 4b-3.
