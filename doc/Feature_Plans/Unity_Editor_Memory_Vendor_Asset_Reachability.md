# Unity Editor memory: vendor-asset reachability

Research artifact for [Inventory vendor-asset dependency closure and future-use candidates](https://github.com/amindell11/astronomical-home/issues/436). This report records reachability facts; [Classify authoring-only assets by future use](https://github.com/amindell11/astronomical-home/issues/435) owns the human decision to retain, archive, or delete anything.

## Method and limits

Snapshot: `origin/main` at `6e038afb` on 2026-08-26.

The audit mapped every `Assets/**/*.meta` GUID to its asset, parsed serialized GUID references from project assets, then computed transitive closures from:

- the three enabled production scenes in `ProjectSettings/EditorBuildSettings.asset`: `InitScene`, `Environment_1`, and `Environment_2`;
- `Assets/Scenes/RLTraining.unity`, the sole scene named by `RLTrainingPlayerBuild`.

Counts and MiB below describe source assets, not imported Library size or resident Editor memory. GUID reachability detects normal scene, prefab, material, and ScriptableObject dependencies. It cannot prove that an asset is disposable: editor-only authoring scenes, future content, string-based loads, custom importers, or human workflows may use an asset without placing it in either build closure.

## Dependency closure

| Folder | Source | Production closure | RL closure | Reading |
|---|---:|---:|---:|---|
| `Assets/Visuals/Environment/Asteroids` | 97 assets / 590.2 MiB | 17 / 56.3 MiB | 17 / 56.3 MiB | Both closures use the ten meshes, LightA material and textures, tiled normal, and physics material. |
| `Assets/Visuals/ParticlePack` | 460 / 171.3 MiB | 13 / 6.8 MiB | 13 / 6.8 MiB | Both closures use a narrow fire/explosion subset through game-owned particle prefabs. |
| `Assets/Visuals/Environment/Sky` | 12 / 184.2 MiB | 6 / 178.4 MiB | 0 | Production uses both HDR skies plus their materials/shader. |
| `Assets/Visuals/Environment/Station_Hug` | 11 / 62.1 MiB | 7 / 59.6 MiB | 0 | Production station/extraction prefabs bind the mesh, material, and five textures. |
| `Assets/Visuals/Environment/Radio` | 18 / 32.2 MiB | 6 / 8.9 MiB | 0 | Production radio prefabs bind one mesh/material set. |
| `Assets/Visuals/Environment/Station_Fork` | 17 / 98.7 MiB | 0 | 0 | Bound by `Station 3.prefab`, which is outside both build closures. |
| `Assets/Visuals/Environment/Station_Spindle` | 9 / 80.6 MiB | 0 | 0 | Referenced from the authoring-only `EditScene`. |
| `Assets/Visuals/Environment/Station_Wheel` | 24 / 102.6 MiB | 0 | 0 | Referenced from the authoring-only `EditScene`. |

The production build roots are explicit at [`ProjectSettings/EditorBuildSettings.asset`](../../src/Asteroids3D/ProjectSettings/EditorBuildSettings.asset#L7-L16). The RL build bypasses Build Profile scene lists and names `RLTraining` directly at [`RLTrainingPlayerBuild.cs`](../../src/Asteroids3D/Assets/Scripts/RLHarness/Hosts/RLTrainingPlayerBuild.cs#L10-L30).

### Confirmed live bindings

- [`SpawnSettings.asset`](../../src/Asteroids3D/Assets/Settings/Asteroids/SpawnSettings.asset#L1170-L1187) binds the HD Asteroids mesh variants used by procedural fields. [`Asteroid3D.prefab`](../../src/Asteroids3D/Assets/Prefabs/Asteroid/Asteroid3D.prefab#L37-L44) also binds vendor mesh content.
- [`ExplosionVFX.prefab`](../../src/Asteroids3D/Assets/Prefabs/Particles/ExplosionVFX.prefab#L4851-L4853), `SmallExplosion`, `SmokeTrail`, `SparksVFX`, and `Embers (1)` bind the retained ParticlePack subset.
- [`Station.prefab`](../../src/Asteroids3D/Assets/Prefabs/Structures/Station.prefab#L36-L67) and `Station Extraction Zone.prefab` bind Station_Hug content.
- `Key Radio.prefab` and `Radio.prefab` bind the retained Radio mesh/material set.

## Candidates exposed by the audit

These are classification inputs, not deletion instructions:

- HD Asteroids has 80 assets and about 533.9 MiB outside both current build closures. The live subset is real, so whole-folder removal is invalid; a later implementation would need a retained dependency subset.
- ParticlePack has 447 assets and about 164.5 MiB outside both current build closures. Its demo scenes, example categories, bundled TextMesh Pro copy, tutorial material, and most Shared content are not build-reachable. The game-owned explosion prefabs still depend on 13 assets, so whole-folder removal is invalid.
- Station_Wheel and Station_Spindle are absent from both build closures but remain visible in `Assets/Scenes/EditScene.unity`.
- Station_Fork is absent from both build closures but remains bound by `Assets/Prefabs/Structures/Station 3.prefab`.
- Radio, Sky, and Station_Hug are partially live. Their unreachable tails are small enough that future-use classification should precede any surgical cleanup.

The legacy Post Processing package has eight serialized project GUID references, all confined to ParticlePack's demo `Shared/Prefabs/Player.prefab` and `Scenes/Profiles/DefaultProfile.asset`. Timeline-like GUID hits are likewise confined to copied ParticlePack demo/sample content. That coupling matters to package cleanup, but this research does not decide either package removal or demo deletion.

## Memory relevance

The project contains 323 source textures totaling about 1,338 MiB. Thirty-eight sources larger than 8 MiB total about 903 MiB; 286 textures have mipmaps, four enable mip streaming, and none of those 38 large sources stream. Global streaming is disabled in all three quality tiers at [`ProjectSettings/QualitySettings.asset`](../../src/Asteroids3D/ProjectSettings/QualitySettings.asset#L39-L44).

Removing unreachable source assets should reduce import, Asset Database, and per-worktree Library pressure. This audit does not establish a resident-memory saving. The map's later dry-editor and representative-scene measurements must quantify that result.

## Boundary for the next decision

Issue #435 decides whether authoring-only and future-use candidates stay in the main project, move to recoverable external/archive storage, or are deleted. A chosen cleanup should preserve the reported live closure, reimport from a clean Library, scan for missing scripts/references, build both production and RL scenes, and run the normal Unity gate. No asset has been classified or changed by this research.
