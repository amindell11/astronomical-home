# Bulwark shared paint workflow and drawn cockpit

Source commit `4358664c62a8cb7ee8e9c7924ba829f4362d71aa`. Shared ship_paint used read-only from primary checkout commit `4dbafabe9685272c67976cb00923da1a17d8e0f2`. Owner authorized the new workflow with the approved base coat retained, then explicitly authorized refreshing the geometry fingerprint for texture generation while keeping flight and UV checks pending.

The generated Shadow, Light and Ink masks now come from ship_paint, with plate boundaries and broad tonal shapes. Brush contrast is zero: no grain, edge wear or chips. Canopy parts are excluded from generated effects. The authored canopy has dark glass, a lower dark wedge and paired reflection strokes; these edits sit above the saved tool baseline and survive regeneration exactly at the decoded pixel level (rerun-verification.json). Blender now displays a flat drawn composite instead of physical glass/metal reflections.

BaseRegion pixels and packed bytes are unchanged. Fresh-reload verification compares every object's geometry, transforms, modifiers, UVs and face material indices. Only duplicate material labels changed from .001 to Secondary to satisfy the strict region schema. Original images remain packed. The refreshed lock verifies; this is a texture fingerprint, not flight approval. UV density remains deferred.

Final views are renders of the saved Blender source, not in-game approvals. The source was reloaded in the open Blender window only after checking it had no unsaved scene or image changes.

The generic Unity bundle is not ready to import: it omits the BaseRegion coat, and the earlier emission workaround blends white after shading. This integration gap is tracked in #1018. No gameplay assets or shaders changed; no Unity tests or editor captures were run. Flight, UV-density correction, in-game lineup review and breakup remain pending.

Reproduction inputs are the committed paint.json, palettes.json, shared brush tile and baseline. prepare.py captures palette colors; prepare_names.py prepares the naming map; paint_canopy.py authors only canopy edits; drawn_materials.py builds the Blender composite. apply.py verifies preservation and saved-file reload. bake/ and rebake/ hold shared-tool reports. The scratch generation copy protected original masks while the new baseline was established.
