# Console geometry comparison

Open `index.html` for the reference beside the new render. The previous render is available in the same comparison. Neutral reference applies a browser grayscale filter only. The source image is unchanged.

Open `console.blend` to edit. The active scene is `09 - Reference geometry fit`. The hero camera, ship and lights match hardware-08. The owner's saved scene remains separate as `SOURCE - owner saved scene`.

The monitor is narrower and more upright. Its controls and both rectangular screens move with `07 | Monitor assembly`. The lower screen remains separate. The source screen meshes were not reshaped.

The projector now has a broad, shallow ramp and an inclined optical cartridge. The fins are shorter and supported by a deck plate. The forearm panel slopes toward the deck. The hose run is higher and shorter, with smaller fittings. The projection controls and Launch use fitted rigid poses at their original mounting depth.

Useful objects:

- `07 | Monitor assembly`
- `09 | Projector optical assembly`
- `09 | Broad low projector ramp`
- `07 | Angled cooling bank`
- `08 | Overlapping forearm armor`
- `07 | Projection control assembly`
- `07 | Launch assembly`
- `08 | Complete hose loom`

New meshes are in `09 - Reference fitted hardware`. Earlier replaced pieces are hidden. Armor bevels, the projector aperture Boolean and the Bezier hose curves remain editable. The monitor has pedestal supports below the camera crop.

`fit_geometry.py` takes hardware-08 as its input and writes a new named scene and two renders. It refuses to overwrite a Blender file. Do not rerun it over hand edits. `console-fit.json` records the selected reference corner measurements. Those measurements guide proportion changes; they are not a visual acceptance test.

The camera and angled renders were inspected. `verify_scene.py` fresh-loads the saved scene to check source preservation, rectangular display faces, packed images and editable assemblies. The artwork still needs owner review. Screen artwork, paint and electronic glow are outside this geometry pass.
