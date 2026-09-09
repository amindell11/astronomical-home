# art/ — WIP art sources

Work-in-progress Blender files and downloaded asset archives live here, outside
`Assets/`, so Unity never imports them (no reimport churn, no `.meta` files, no
risk of wiring a half-finished model into a prefab).

- `ships/`, `stations/` — WIP models by subject.
- `archives/` — original downloaded asset packs kept for provenance.

Graduation path: when a model is ready, export/copy it into
`src/Asteroids3D/Assets/Visuals/...`; the WIP source stays here as history.
Finished source for already-shipped assets remains in the per-asset `source/`
folders under `Assets/Visuals/` — several of those files are live scene/prefab
dependencies, so do not move them out.

Everything binary here is LFS-tracked via this directory's `.gitattributes`.

- `tools/` — art-pipeline generators (`tools/skybox/` renders the procedural
  HDR space skybox with Blender); scripts, not sources, so not LFS.
