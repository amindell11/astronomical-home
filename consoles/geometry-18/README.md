# Fitted connector housings

This pass uses geometry-17. The individual connector sheets remain available but hidden. Their shared vertices are welded into a closed casing with a recessed top, continuous sidewalls, and a bottom fitted within the Launch tray profile. The lower flange is a second closed mounting return. Only the exterior casing edges receive a bevel.

All existing mesh vertices, faces, object transforms, controls, and tube paths are unchanged. The geometry-15 armor and fold edges remain intact.

Open console.blend, scene `18 - Fitted connector housings`. New objects are in `18 - Recessed connector housings`. The comparison page opens on an angled close view, with camera, full-frame, and side inspections available.

build_housings.py accepts a loaded geometry-17 file and --name; it writes a new Blender file and checks JSON, refusing overwrite. render_audit.py accepts --prefix and renders without saving. verify.py compares against geometry-17 and the geometry-15 armor and checks the closed housing meshes. It writes verification.json and fails on mismatch. Visual approval is pending.
