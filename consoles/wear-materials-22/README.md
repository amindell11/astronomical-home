# Handled paint and drawn scuffs

This copies ink-materials-21/console.blend. Geometry, fold lines, modifiers, object positions, camera, lighting and the established ink treatment remain unchanged. The geometry remains provisional.

The new marks are placed around handling areas on the monitor frame and corners, control plate, forearm, projector edge, cable clamp, connector and Launch button. Short dark cuts and smaller pale chipped edges have varied lengths, directions and spacing. Screens, labels and the hologram retain their previous materials.

Materials prefixed `22 |` are per-part overrides. Their nodes named `Handled dark scuffs strength` and `Exposed paint edges strength` control wear intensity. Each part's `22 | Wear on ...` node group contains the authored marks. The masks use local surface coordinates, so marks follow the part when it moves. There is no bump, displacement, decal geometry or geometry change. Existing palette and painted shading nodes remain editable.

The review page keeps the previous/current side by side, the illustrated reference comparison, and the camera/angled inspection views. All file images and fonts remain packed.

apply_wear.py accepts a loaded ink-materials-21 file and --name, refusing existing output. It projects authored mark placements onto their named parts once, then saves local material masks and a placement manifest. Missing placements fail before saving. render_review.py writes camera and neutral inspection images without saving temporary inspection changes. verify.py checks the saved scene against ink-materials-21 for unchanged geometry, smoothing, modifier settings, transforms, visibility, cameras and object inventory.

Sol cleared the camera and angled views with no remaining actionable wear feedback. The delivered scene is byte-for-byte identical to the reviewed candidate. Review rounds and file hashes are in review.json. User approval remains pending.
