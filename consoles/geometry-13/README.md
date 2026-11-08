# Folded console armor

This pass starts again from geometry-11 after the owner rejected geometry-12. Open `index.html` for the reference comparison, the geometry-11 baseline, and the camera and angled renders.

The cooling deck now folds down behind the tubes and extends beneath the monitor. The projector retains its earlier ramp, with a shorter front lip and a recessed dark support. The control housing continues into the Launch surround, with a folded outer shoulder. Camera, monitor angle, ship, controls and tube transforms are unchanged.

Open `console.blend`, scene `13 - Folded console skins`. The new objects are in `13 - Editable folded skins`. Each skin is a mesh with editable thickness and bevel modifiers. The control and Launch chassis is a separate mesh. There are no casing unions. The original owner scene remains separate; earlier construction is hidden where replaced.

The saved file opens at the final camera with the neutral projection cue enabled. The structure and angled renders hide that cue. Screen artwork and finished materials remain pending.

`fold_console.py` loads geometry-11 and writes a named Blender file, renders and manifest. It refuses to overwrite an existing Blender output. `verify_scene.py` checks the saved file without saving changes.

Visual approval is pending.
