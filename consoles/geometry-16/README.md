# Three annotated joins

This pass starts from geometry-15/console.blend. It addresses only the three regions marked on the angled render.

The control corner now has a closing face. The projector/control transition uses a shared crease with a planar upper fold instead of overlapping tips. The Launch tray meets the front rail, and its clearance follows that fitted edge instead of leaving a triangular notch.

Open console.blend, scene `16 - Annotated join repairs`. The existing panel meshes remain editable; the base clearance remains a live Boolean. Object positions, controls, tube paths, camera, and the rest of the geometry are unchanged.

index.html compares the repaired joins with geometry-15. It opens on a close view of the marked region, with full-frame and inspection views available. verification.json records the untouched-data comparison and closure checks. Visual approval is pending.

repair_joins.py takes a loaded geometry-15 file and --name to write a new Blender file and checks JSON. Existing Blender output is rejected. render_audit.py takes --prefix and renders five views without saving. verify.py reads the final and preceding files, writes verification.json, and fails if preservation or mesh closure checks fail.
