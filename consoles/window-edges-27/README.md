# Brighter window edges

This copies nebula-26 and raises the window etching visibility from 0.075 to 0.12. Clouding, the bright nebula, console lighting, materials, geometry and camera retain the previous settings.

Adjust `Etching visibility` in the `25 | Etched glass perimeter` material to tune the amount. The glass remains a separate editable surface.

The camera render is new. The angled inspection is reused from pass 26 because that inspection hides the glass. The comparison page shows pass 26 beside this version.

`brighten_edges.py` accepts a loaded nebula-26 scene and writes console.blend and its manifest, refusing an existing output. `render_camera.py` renders the camera view. `verify.py` checks the original objects, geometry, lights, camera and packed dependencies against pass 26. Review results and final file hashes are in review.json.
