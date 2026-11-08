# Joined armor and viewport

Open `index.html` for the full-frame side-by-side comparison. Uncheck Full frame to inspect the hardware crop. The previous pass, a camera render without the projection cue, and an angled construction view are included.

Open `console.blend`. The active scene is `11 - Joined console and viewport`. This is a copy of geometry-10. The camera, monitor pose, ship and light objects are unchanged. The original owner scene remains separate and the source files are unchanged.

The armor now sits on a continuous console body. The cooling fins use that body directly, and the Launch control is seated in its sloping face. An armored shoulder joins the projector to the controls. The projector's wraparound edges have broader facets, and the control-panel rim is thicker. The forearm and other plates remain separate editable pieces with adjoining or overlapping edges.

The viewport header has beveled corner volumes and an inset lip. Its side posts remain in the scene but sit outside the final camera. A separate neutral projection volume restores the visual connection between the projector and ship. It is a composition cue, not the finished lighting pass.

New objects are in `11 - Joined console structure`:

- `11 | Continuous faceted console body`
- `11 | Projector to controls armored knuckle`
- `11 | Projector wraparound armor`
- `11 | Thick projection panel armor`
- `11 | Beveled viewport header`
- `11 | Projection volume`

To hide the projection cue, turn off rendering for `11 | Neutral projector frustum` under `11 | Projection volume`. The saved scene has it enabled; the structure and angled renders hide it. New part origins are centered. Bevels, the projector Boolean and the earlier tube curves remain editable. Replaced objects are hidden.

`join_console.py` takes geometry-10 as input and writes a new named Blender file, three renders and a manifest. It refuses to overwrite a Blender file. Do not rerun it over hand edits. `verify_scene.py` checks the saved file without saving changes.

Screen artwork, final materials and visual approval remain pending.
