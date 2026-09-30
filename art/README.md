# art/ — WIP art sources

Work-in-progress Blender files and downloaded asset archives live here, outside
`Assets/`, so Unity never imports them (no reimport churn, no `.meta` files, no
risk of wiring a half-finished model into a prefab).

- `ships/`, `stations/` — WIP models by subject.
- `archives/` — original downloaded asset packs kept for provenance.

Graduation path: when a model is ready, export it to FBX into
`src/Asteroids3D/Assets/Visuals/...`; the `.blend` stays here as history.
Finished FBX sources for already-shipped assets remain in the per-asset
`source/` folders under `Assets/Visuals/` — several are live scene/prefab
dependencies, so do not move them out. A `.blend` never goes under `Assets/`:
Unity imports one by launching Blender, which the hosted test runner lacks
(#719). `stations/` holds the shipped stations' `.blend` sources;
`stations/export_unity_fbx.py` regenerates their FBX with the settings Unity's
own Blender importer uses, so the imported hierarchy and fileIDs stay identical.

Everything binary here is LFS-tracked via this directory's `.gitattributes`.

- `tools/` — art-pipeline generators (`tools/flat_background/` renders the
  procedural starless flat background with Blender; `tools/imagegen/` generates
  and edits images with Google's Nano Banana models); scripts, not sources, so
  not LFS.
