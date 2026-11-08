# Console folds and tube entry

Open `index.html` for the reference side-by-side, geometry-13 baseline, tube-entry close-up, and camera and angled views.

The projector apron now meets the diagonal forearm fold. The forearm and Launch surround share mesh edges, with a flush gasket beneath the raised Launch key. A narrow shoulder meets the control panel. The control face is thinner, and the folded armor uses a thickness of 0.12 scene units. Recessed sidewalls support the folds.

The paired tubes now enter through the front fascia. The shared socket has shallow glands aligned with that face. The curves turn downward into the existing horizontal run; the retaining strap and inline connector are unchanged.

Open `console.blend`, scene `14 - Joined folds and front hose entry`. New parts are in `14 - Editable armor and hose entry`. Edit mesh vertices and live Solidify/Bevel modifiers to adjust the folds. The optical opening remains a live Boolean. The socket fittings are grouped under `14 | Recessed front hose socket`; the two hose curves remain editable.

This is a copy of geometry-13. Camera, monitor angle, ship and control poses are preserved. The original owner scene is separate. The saved file opens at the final camera with the projection cue enabled; structure and angled renders hide it.

`join_folds.py` writes a named Blender file, renders and manifest, refusing to overwrite an existing Blender output. `verify_scene.py` checks the currently loaded file against its manifest and writes a verification report without saving scene changes.

Screen artwork, final materials and visual approval remain pending.
