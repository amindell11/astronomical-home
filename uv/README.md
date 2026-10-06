# uv: Crimson UV density rework

Evidence for the PR that replaces #731. Scratch and reports only; nothing here merges.

- `transfer_uvs.py`: Blender, run on main's `Crimson.blend`. Copies each part's UVs from #731's source, renames the layer to `PaintUV`, swaps the packed atlas, and dumps the old and new evaluated UV triangles.
- `remap_debris_uvs.py`: plain Python. Remaps UV0 of the 18 `Breakup/*.asset` meshes barycentrically from the old UV triangles to the new ones. `debris-remap-report.json` is its output from the write run.
- `ship_uv-main.json` / `ship_uv-rework.json`: `ship_uv` reports, 8.84× and 1.04×. The main report measures main's source with the layer renamed in scratch, because the committed `ship.json` exempts every part.
- `debris_still.cs` → `debris-remap-comparison.png`: edit-mode render of the 18 debris pieces three ways, on the hull paint material, so the scorch tint and fade are absent.
- `731-*.png`: #731's Blender before/after comparisons of the same UVs and atlas.
