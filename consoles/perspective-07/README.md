# Perspective and form review

[Review the camera, angled view, and approved composition](index.html). [Open the editable scene](perspective.blend).

The owner rejected `blender-console-06`, including its perspective. The rejected builder reverse-projected image coordinates onto tilted surfaces. That produced trapezoidal faces in the actual model: the main display's opposite edges diverged by up to 3.8 degrees. Fitting image bounds had concealed the construction problem.

This pass constructs parts in local 3D coordinates and uses one perspective camera. The monitor rotation comes from the owner's saved scene. Its main and detail screens have parallel opposite edges. Equipment is seated in a continuous console body; the projector faces upward through an opening in the sloping apron, with the ship above it. This corrects the construction method. The camera and visual match still need the owner's judgment.

The grey pass deliberately leaves out screen artwork, electronic glow, and final finish. Blank screens expose their proportions and orientation. Simple controls show their scale and depth.

The resumed Claude UI session reviewed the first grey attempt and identified a remaining tabletop read, detached assemblies, and boxlike side profiles. This revision lowers and brings the camera closer, connects the console body to the viewport frame, adds monitor pivot supports, recesses the projector into an apron, and slopes the vent profiles. The foreground body continues beyond the camera crop.

The follow-up review found no major camera issue, while identifying the projector's perched appearance and concealed monitor supports. The final revision recesses the lens through a live Boolean opening and moves the pivots beyond the bezel sides. These are review findings, not owner approval.

## Editing

- Active scene: `07 - Perspective and form review`.
- Camera: `07 Camera - perspective`; alternate: `07 Camera - construction`.
- Move the `07 |` assembly empties to adjust the monitor, projector, projection controls, Launch control, ship, cables, and viewport.
- Parts have live bevel modifiers. Cables remain Bezier curves. Screens are separate surfaces.
- The camera has a disabled packed reference image for alignment.
- `SOURCE - owner saved scene` retains the loaded original scene, including its camera and objects. The original file was not written.

Source: `../blender-console-06/inputs/owner-source.blend`, a byte-identical copy of `../refinement-04/inputs/owner-edited.blend`. Vanguard and projector geometry are copied from that scene. Materials in the new scene are neutral inspection materials.

`build_perspective.py` documents its Blender invocation. It refuses an existing blend output and writes two renders plus measurements under the chosen name. Do not rebuild over subsequent hand edits. `diagnose_faces.py` records the rejected display's edge divergence without saving its scene. Geometric checks establish construction facts; they do not establish visual approval.
