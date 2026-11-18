This is a rough camera-led massing of picked sketch D, with the larger SHIP-screen hierarchy from E. All dimensions are Blender units, Z up. There are no textures, model text, bevel modifiers, or finished ship details. The labelled image is an overlay on the unlabelled render.

The game camera remains at (0, -18, 6.8), points along the normalized (0, 0.9936, -0.1132) direction, and has a vertical field of view of 34.11 degrees. Its render is 1920 by 1080.

The SHIP screen is 4.25 wide by 3.50 high in its own plane, before its 0.20-unit corner cuts. It retains 99.99% of its true width relative to the camera's local sightline, a width loss of 0.01%. Its projected centreline width is 387.1 pixels. All 63 interior visibility rays reach the screen without obstruction.

The DETAIL screen is 3.76 wide by 0.92 high in its own plane, before its 0.20-unit corner cuts. It retains 99.99% of its true width relative to the camera's local sightline, a width loss of 0.01%. Its projected centreline width is 353.3 pixels. All 63 interior visibility rays reach the screen without obstruction.

The PRIMARY screen is 4.25 wide by 1.60 high in its own plane, before its 0.20-unit corner cuts. It retains 99.95% of its true width relative to the camera's local sightline, a width loss of 0.05%. Its projected centreline width is 392.1 pixels. All 63 interior visibility rays reach the screen without obstruction.

The ORDNANCE screen is 4.25 wide by 1.60 high in its own plane, before its 0.20-unit corner cuts. It retains 99.95% of its true width relative to the camera's local sightline, a width loss of 0.05%. Its projected centreline width is 401.9 pixels. All 63 interior visibility rays reach the screen without obstruction.

The SHIP plane contains an unobstructed 3.40 by 3.00 rectangle. Its bounding area is 14.875 square units, compared with 6.80 for each matched weapon screen and 3.4592 for DETAIL. PRIMARY and ORDNANCE are each larger than the required 4.13 by 1.45. All four screens are recessed 0.09 behind their frames.

Width retention is the length of the unit screen-width vector projected perpendicular to the sightline from that screen centre to the camera: sqrt(1 - dot(width_axis, sightline)^2). This separates foreshortening from perspective scale. The separate horizontal image-space ratios in measurements.json are slightly over 100% because the off-axis pods turn toward the eye; that does not mean the physical screens grew. Both measures exceed 70%.

The pods cant inward by 17 degrees, tilt back by 16 degrees and bank outward by 10 degrees. Left pod width is 5.25; right pod width is 5.70. The centre desk is 4.50 deep. The projector lens is centred at (0, 0, 1.25).

A hidden wire guide reserves x = -2.5 to 2.5, y = -2 to 2, z = 2.5 to 6.5 for the hologram. The translucent fighter placeholder fits inside that box, in its upper portion. Both main canopy struts remain wholly outside the box in X and sit behind it in Y. Their landing centres are (-2.90, 3.05, 0.78) and (2.90, 3.05, 0.78). The header and outer returns complete three window openings.

The pods seat into rear saddles and the continuous sill. The sill meets the strut feet and the centre deck bridge, and the pedestal overlaps the centre deck. Keys, knob, launch socket and projector meet their supports. These are editable overlapping masses, not a final watertight production mesh. The hologram and its light fans are the only deliberately suspended display elements.

The main departure from D is the taller left pod and nearly square SHIP area, required by the usable 3.40 by 3.00 rectangle and the owner’s instruction to make that screen bigger. Its shallow DETAIL strip preserves the hierarchy shown in E. The right pod is lower, keeping its two equal weapon screens rather than stretching them to match the taller left pod.

The specified camera and lens position place the projector at about 81% of image height, lower than in the drawing. The centre desk consequently sits near the bottom edge and is cropped in front. The camera and lens were not moved to force a traced silhouette. The canopy and sill sit farther behind the pods to prevent struts or dash faces covering the screen rectangles. The two navigation keys on each weapon screen take a small inboard rail absent from D’s blank screen study.

The background is deliberately uniform dark blue, the glazing is represented by open panes, and the projected fighter is only a translucent angular placeholder. Star fields, surface texture, UI content and glow polish are omitted to keep the review about mass and framing.

I would ask the owner whether the taller left pod gives SHIP enough priority; whether the right pod should rise to align its top with SHIP despite leaving more space around its smaller screens; and whether the launch button feels sufficiently prominent at the fixed camera’s low foreground position.

Rebuild all deliverables by running the supplied Blender executable with -b --python out/build_D.py from the project folder. The script clears the scene, rebuilds and saves D.blend, renders all four views, writes these notes and measurements, then uses the installed local Miniconda Python with Pillow to create the labels and reference comparison. It reads refs/picked-sketch-D.png for the comparison only and uses no network. Add -- --preview for just the game-camera render.
