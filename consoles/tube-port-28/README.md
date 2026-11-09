# Fitted tube entry

The owner accepted the overall look of nebula-26 and requested brighter window edges, followed by a visible port where the left tube ends enter the console.

This scene includes the brighter etchings from window-edges-27. A new chamfered rim surrounds the existing tube entry, with a recessed twin feedthrough plate, two collars and two captive fasteners. The assembly uses the existing warm grey, graphite and nickel materials. The tube paths and every existing object retain their saved geometry and transforms.

The new parts are grouped under `28 | Tube entry assembly` in `28 - Fitted tube entry`. They remain separate editable meshes. The plate bores follow the existing hose axes. The original armor remains untouched.

Both the camera render and angled inspection are new. The comparison page shows nebula-26 beside this version.

`add_port.py` accepts a loaded window-edges-27 scene and writes console.blend and its manifest, refusing an existing output. `render_review.py` renders both views. `verify.py` checks preservation of the existing scene and closed geometry for the added meshes. Review results and file hashes are recorded in review.json.
