# Monitor rake, panels and tubes

Open `index.html` to compare the approved reference with this pass or geometry-09. The angled view shows the same editable parts. Neutral reference is a browser grayscale filter; the source image is unchanged.

Open `console.blend`. The active scene is `10 - Raked monitor and formed panels`. This is a copy of geometry-09. The owner's saved scene remains separate and the original source files are unchanged.

The monitor now holds a 34-degree backward rake. Placement and dimensions were fitted with its angles fixed, so the fit cannot straighten it. Both display faces remain rectangles. Move or rotate `07 | Monitor assembly` to adjust the whole unit.

The forearm shell has a shallow upper face and a steeper folded nose. The reference outlines were measured against those physical planes, then given thickness and backing. The tube assembly has rounded entry bends, a paired straight run, a retaining strap and a low inline connector. The cooling shoulder and control panel have broader chamfers and structural backing.

New parts are in `10 - Editable form refinement`:

- `10 | Folded forearm assembly`
- `10 | Complete tube assembly`
- `10 | Cooling bank armor surround`
- `10 | Projection panel single enclosure`
- `10 | Projection panel chamfered face`

The tube curves and panel bevels remain live. New part origins are centered for editing. Earlier replaced objects are hidden. Camera, ship and lights are unchanged. Screen artwork and finished materials remain outside this pass.

`refine_forms.py` takes geometry-09 as input and writes a new named Blender file, camera render, angled render and manifest. It refuses to overwrite an existing Blender output. Do not rerun it over hand edits. `verify_scene.py` checks the saved file without saving changes.

Visual approval is pending.
