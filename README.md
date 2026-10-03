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

## v02 corrected native preview

The v02 GIF/MP4 and stills show one LayeredAsteroidExplosion per production death. The updated BuildValisBreakup.cs clears the legacy HullVisuals explosion reference reproducibly. The playable prefab snapshot carries that correction. Fragment meshes and animation are unchanged from v01, so their editable files and approved input remain in v01. Focused regressions pass 6/6, including single-effect checks at all three wing poses. Final production head: 512a5509b33154d4af15588c348e07b0e1502111.

Final v02 full Unity validation: 950 passed, 2 skipped, 0 failures (952 total, RequiresGraphics excluded), on 512a5509b33154d4af15588c348e07b0e1502111. Native capture separately ran with graphics enabled.
