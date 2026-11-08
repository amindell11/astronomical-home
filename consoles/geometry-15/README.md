# Owner scene cleanup

This pass uses the owner's saved `D:/amind/Downloads/console (1).blend`, preserved as `inputs/owner-saved.blend`. It does not rebuild the scene from an earlier generator.

Open `index.html` for before/after camera, angled, side and rear views. The left image is the owner's saved scene. The right image is the cleanup.

The cleanup adds fitted dark rear/end closures beneath the projector and control pod, trims the base overlap beneath Launch, and seats seals where the saved tube curves cross the fascia. One obsolete floating shoulder behind the current casing is hidden. An internal protruding return is removed, and the twisted join under the controls is split into two defined faces. Existing object transforms, original mesh vertices, and tube curves are preserved. The visible apron shape remains intact.

Open `console.blend`, scene `15 - Owner geometry cleanup`. Added parts are in `15 - Local geometry repairs`. The base clearance is a live Boolean with a hidden wireframe cutter. The existing panel objects and original owner scene remain available. Completely concealed construction was left alone.

`clean_console.py` loads the preserved owner file and writes a named Blender file and manifest. It refuses to overwrite an existing Blender output. `render_audit.py` renders without saving the scene. `verify_scene.py` compares the cleanup against `owner-baseline.json` without changing the file.

The side and rear inspection views use studio lighting and temporarily hide the ship and viewport header. The camera and angled renders retain the scene lighting. Visual approval is pending.
