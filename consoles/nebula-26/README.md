# Brighter nebula

This copies console-finish-25 and raises the visible nebula from 0.078 to 0.45, about 5.8 times its previous intensity. Its contribution to console illumination remains at the previous level. The console lights, materials, glass, lettering, geometry and camera are otherwise unchanged.

Adjust `Visible nebula brightness` in the `19 | Distant nebula` material to change this amount.

The camera render is new. The angled inspection is reused from pass 25 because it hides the nebula and is unchanged. The comparison page shows pass 25 beside this version.

`brighten_nebula.py` accepts a loaded console-finish-25 scene and writes console.blend and its manifest, refusing an existing output. `render_camera.py` renders only the changed camera view. `verify.py` checks the original objects, geometry, lights, camera and packed dependencies against pass 25. Review results and final file hashes are in review.json.
