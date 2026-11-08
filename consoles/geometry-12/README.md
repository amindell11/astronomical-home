# Wrapped console housing

Open `index.html` for the reference beside this pass or geometry-11. The hardware crop is the default. Full camera, geometry without the projection cue, and angled views are included.

Open `console.blend`. The active scene is `12 - Wrapped console housing`. This is a copy of geometry-11. The camera, monitor pose, ship, tubes and viewport are unchanged. The original owner scene remains separate.

The earlier slab is replaced by full-depth casing volumes. The base under the screen continues into the cooling bay. The projector has a folded front return and walls that join the control housing and the forearm cheek. The controls sit inside a recessed surround. The Launch housing rises around the button base and joins the forearm return.

`12 | Complete console housing` carries live Boolean unions of the named casing pieces and differences for the recessed seats. Its evaluated mesh is checked as one closed, connected body. The editable operands are hidden in `12 - Editable housing construction` and use wire display. Select one in the Outliner and unhide it to adjust its shape; keep its render visibility off. The housing updates from that operand.

Useful operands include:

- `12 | Under-screen armored housing`
- `12 | Monitor to cooling bay housing`
- `12 | Projector full-depth casing`
- `12 | Wrapped control housing shoulder`
- `12 | Folded armor joining the launch cheek`
- `12 | Launch surrounding armor face`
- `12 | Launch left return beneath folded armor`

The control insert, knobs, buttons, optical cartridge, tube curves and monitor remain separately editable. Replaced construction is hidden. The saved file has the neutral projection cue enabled; structure and angled renders hide it.

`wrap_housing.py` loads geometry-11 and writes a new named scene, three renders and a manifest. It refuses to overwrite a Blender output. Do not rerun it over hand edits. `verify_scene.py` inspects the saved file without saving changes.

The source files are unchanged. Screen artwork, finished materials and visual approval remain pending.
