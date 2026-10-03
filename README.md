# Valis authored breakup archive

Native Unity preview, reproducible authoring helper, editable output assets, and validation proofs for issue #857. Production branch: task/valis-breakup. The preview runs actual production deaths at rest, intermediate wing sweep, and full wing sweep with inherited velocity.

- `v01/previews/valis-breakup.gif` and `.mp4`: native capture, 519 fixed steps at 50 fps; GIF samples every second frame.
- `v01/editable-assets/`: sixteen fragment meshes, the authored burst animation, debris prefab, and playable Valis prefab snapshot.
- `v01/authoring/BuildValisBreakup.cs`: Unity asset builder; `approved-export.json` and `unity-build.json` retain its approved geometry input and normalization.
- `v01/authoring/ValisBreakupCapture.cs`: temporary native preview scenario, excluded from production.
- Remaining authoring scripts and JSON preserve pose/animation investigation and editor import helpers. Raw capture frames and console logs are excluded.
- `v01/proofs/`: geometry accounting, native capture manifest, focused six-test proof, and capture-run proof. Full-suite and ReSharper proof are added after completion.

The existing concept, model, material, paint, wing-motion and approved-source history remains pinned at [ce7e26719515715f334fad5b6fa60deb41ef47ce](https://github.com/amindell11/astronomical-home/tree/ce7e26719515715f334fad5b6fa60deb41ef47ce/history). Separate paint-authoring tools/tests remain at [2d433ca2e6790ab6f767198a59f7d6d3657405ad](https://github.com/amindell11/astronomical-home/tree/2d433ca2e6790ab6f767198a59f7d6d3657405ad).

The clips demonstrate controlled death presentation, not combat balance or whole-match performance.

## v01 visual correction

The user rejected v01's orange explosion. Investigation found an additional legacy ExplosionVFX from Valis HullVisuals overlaying the authored breakup's LayeredAsteroidExplosion. v01 is preserved as an intermediate revision; the corrected v02 clears that legacy explosion reference while retaining damage flash and smoke.
