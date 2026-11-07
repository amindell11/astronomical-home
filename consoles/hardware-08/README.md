# Hardware geometry review

[Compare the new hardware, previous geometry, and approved reference](index.html). [Open the editable Blender scene](hardware.blend).

The owner considered the perspective pass an improvement but rejected its armored panels, tubes, details, and geometry. This pass keeps that camera and reworks the hardware against the accepted reference.

The armor uses thick sections with broad shoulder faces. The monitor has a separate control column, raised brow, and large captive bolts. The right side has a faceted projector housing, dark cooling fins, an overlapping forearm cover with structural support, and stepped control housings. Two service hoses enter sockets, pass through a wraparound clamp, and terminate in a coupler. Switches and knobs have larger, faceted silhouettes.

The resumed Claude UI session reviewed the geometry against the reference. Its feedback prioritized thick major forms and identified the disconnected right-hand assemblies in the first draft. The revised support joins those housings while retaining their separate seams. This review does not establish owner approval.

Camera, screen transforms, ship placement, and lights are held from `perspective-07`. Grey material contrast separates armor, structure, and rubber. Screen artwork, color, electronic glow, and final surface finish are outside this pass.

## Editing

Open `hardware.blend` in Blender 5.1. The active scene is `08 - Hardware forms with locked camera`. It opens at `07 Camera - perspective`; `07 Camera - construction` supplies the angled render.

New geometry is in `08 - Editable hardware`. Replaced meshes are hidden, not deleted. The original owner scene remains available as `SOURCE - owner saved scene`. Both the previous source file and the original saved owner file remain unchanged.

- `07 | Monitor assembly`, `07 | Selector`, `07 | Previous`, and `07 | Next` retain the monitor and control assembly transforms.
- `08 | Projector housing` moves the cartridge, armored housing, and aperture cutout together.
- `08 | Overlapping forearm armor` contains the forearm armor and supporting step.
- `07 | Projection control assembly` and `07 | Launch assembly` retain the control grouping.
- `08 | Complete hose loom` groups the hoses, sockets, clamp, coupler, and termination.
- Individual geometry origins sit near their parts. Hoses remain Bezier curves. Bevels and the projector Boolean remain editable.

Once you edit this file, keep it as the working source. Do not rerun the builder over your edits.

`refine_hardware.py` documents its Blender invocation and refuses an existing output filename. It starts from the saved `perspective-07` file. `hardware-manifest.json` records the held transforms, source hash, and replaced objects. `verify_hardware.py` checks the final saved file in a fresh Blender process and writes `verification.json` without saving changes.
