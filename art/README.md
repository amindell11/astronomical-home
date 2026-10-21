# art/ — WIP art sources

Work-in-progress Blender files and downloaded asset archives live here, outside
`Assets/`, so Unity never imports them (no reimport churn, no `.meta` files, no
risk of wiring a half-finished model into a prefab).

- `ships/`, `stations/` — WIP models by subject.
- `archives/` — original downloaded pack archives (zips) kept for provenance.
- `third-party/` — whole vendor packs as raw files, no `.meta`. `Assets/` keeps
  only the files something we own uses, moved (same GUID) to a home named for
  that use; the rest lives only here. A file brought back gets a fresh GUID.
- `meshy/` — Meshy (AI model generator) downloads, one folder per download,
  kept whole under their generator names as provenance.

The one editable Vanguard source is
`ships/vanguard/drawn-study/VanguardPainted.blend`.

Graduation path: when a model is ready, export it to FBX into
`src/Asteroids3D/Assets/Visuals/...`; the `.blend` stays here as history.
A `.blend` never goes under `Assets/`:
Unity imports one by launching Blender, which the hosted test runner lacks
(#719). `stations/` holds the shipped stations' `.blend` sources;
`stations/export_unity_fbx.py` regenerates their FBX with the settings Unity's
own Blender importer uses, so the imported hierarchy and fileIDs stay identical.

Everything binary here is LFS-tracked via this directory's `.gitattributes`,
except `third-party/` files that were plain blobs in `Assets/` (`.tif`,
`.bin`), which keep that storage mode.

- `tools/` — art-pipeline generators (`tools/flat_background/` renders the
  procedural starless flat background with Blender; `tools/imagegen/` generates
  and edits images with Google's Nano Banana models; `tools/ship/` scaffolds,
  checks, locks, renders and exports ship sources); scripts, not sources, so
  not LFS.
