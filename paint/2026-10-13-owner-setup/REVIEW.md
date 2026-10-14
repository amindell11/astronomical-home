# Bulwark owner paint setup

Removed generated edge highlights, chips and edge ink from the saved owner source. Broad shading, deliberate panel lines and canopy reflections remain. BaseRegion now controls solid coat coverage; existing livery boundaries remain the starting point for owner painting.

The live Blender file is in Texture Paint with BaseRegion as the explicit image canvas. Paint Hard uses a constant falloff, strength 1, size 40 px, white primary and black secondary. White adds the existing bone coat; X reveals the underlying color. Alt+Q transfers paint mode to the part under the cursor. Plum armor now accepts the same white coat as Bone, Flap and EdgeCoat.

Geometry and UV fingerprints matched immediately before and after the setup. Owner Shadow pixels matched exactly. The owner geometry differs from the earlier geometry lock; this editing handoff does not approve a new geometry lock or update gameplay assets.

Validation: live image hashes, canvas/brush settings, material connections, saved source, and Blender viewport inspection. `setup-result.json` records the checks. No Unity/code tests: this change prepares the editable source and masks.

Save edited images through Blender's Image menu, then save the blend before handing rough paint back for line cleanup.
