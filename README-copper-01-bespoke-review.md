# Copper 01, bespoke art review

One individually authored Copper/turquoise review candidate. The planned set is five asteroids per locale, twenty-five total; that set is not completed. The accepted Violet source is unchanged. No companions, runtime changes or production exports are included.

The model has a thick left shear slab, lower forward right blade, high rear shoulder and low left toe. Their overlapping volumes form one connected rock host. Copper sections and near-flush turquoise deposits remain separate editable meshes. The hidden `Editable rock construction` collection retains the source volumes and fracture cutters.

- `copper-painted-comparison.png`: original concept, clay and painted model, plus small-scale views.
- `copper-views.png`: current hero, reverse, side and fracture detail.
- `copper-painted.blend`: current mapped model, packed images and useful hero camera.
- `copper-clay.blend`: geometry and plain material regions before the paint pass.
- `reference-copper.png`: unretouched crop of the original approved locale sheet.
- `paint/`: unmodified Imagegen swatches and their generation provenance. Slate is the approved Violet swatch. Node grading changes the rendered tone without altering source pixels.
- `source-check.json`: a fresh Blender reopen inspection of connected components, surface topology, UV layers and packed images. Drawing ribbons are intentionally open surfaces.

`build_wedges.py` creates the current geometry and clay views. `paint_copper.py` maps the actual component surfaces and adds fitted graphite strokes. `inspect_and_views.py` reopens the saved painted source, reports its structure and renders the additional views. The two `make_*` scripts compose ordinary contact sheets from those renders. Earlier construction attempts are excluded from the curated review folder.

The outer drawn contour uses Blender Freestyle and is render-only. Selected internal graphite strokes are actual mesh ribbons. Neither treatment has been exported or integrated into Unity for this candidate. The native small preview is rendered on a 150 by 110 pixel canvas; the asteroid occupies less than the full canvas. This is an art proof, not a runtime-ready asset.

The large planes and mineral boundaries remain cleaner and simpler than the original concept. The direct comparison is included so visual acceptance can be judged from the model rather than inferred from source checks.
