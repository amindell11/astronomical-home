# Locked armor and fitted joins

This pass starts directly from geometry-15/console.blend. Geometry-16 is not an input.

All original vertex positions are unchanged. The forearm armor faces and fold edges, projector armor, tray, controls, tube paths, camera, and their positions are preserved. Three old connection or backing faces on the forearm mesh and one connection face on the shoulder mesh are replaced by separate planar panels. The obsolete control-intersection Boolean is removed from the forearm mesh after those faces are removed.

Open console.blend, scene `17 - Locked armor with fitted joins`. The new editable panels are in `17 - Planar connecting panels`. Thickness and bevels remain live modifiers. The comparison page starts with the original geometry-15 camera view beside the new version.

build_joins.py loads geometry-15 and accepts --name for a new Blender file and checks JSON. It refuses an existing output and verifies all original vertices and transforms before saving. render_audit.py accepts --prefix and renders without saving. verify.py compares the saved final file against geometry-15, checks retained faces and new panel planarity/closure, writes verification.json, and fails on any mismatch. Visual approval is pending.
