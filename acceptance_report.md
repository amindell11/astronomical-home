# Slice 6a acceptance report

Base `4b7622fe`, head `db371e53`.

**Result: PASS**

## 1. GUIDs kept

- 70 kept files: GUID at the new path equals the original: yes
- Moved files' `.meta` as git renames (from -> planned to): {'R100': 70}
- The 12 new folder `.meta`s have fresh GUIDs; git also pairs 10 of them with deleted vendor folder `.meta`s ({'R077': 10}), a rename-detection artefact of near-identical folder templates.
- Manifest rows: 70 kept + 498 out = 568; base pack files: 568

## 2. Every reference still resolves

- Whole-tree scan of `Assets/` + `ProjectSettings/` (text files; `.meta` folded into its asset).
- Resolved references from files outside the packs: before 3447, after 3355 (after includes the moved files' own 65 internal refs).
- Unresolved (package/builtin GUIDs, not project assets): before 956, after 583.
- Arithmetic: 3447 before - 157 EditScene drops + 65 moved-file refs = 3355 (after: 3355).
- References to a GUID this slice removed: 0.
- The only referrer-side drops are in `EditScene` (157 refs):
  - Assets/Visuals/Environment/Station_Dildo/source/p_154_CylinderAnimation_003.fbx ×34
  - Assets/Visuals/Environment/Station_Spindle/source/p_887_spaceStation.fbx ×53
  - Assets/Visuals/Environment/Station_Spindle/textures/Station Material.mat ×37
  - Assets/Visuals/Environment/Station_Wheel/source/p_96_spacestation_v3.fbx ×25
  - Assets/Visuals/Environment/Station_Wheel/textures/Station_Lights.mat ×3
  - Assets/Visuals/Environment/Station_Wheel/textures/Station_Main.mat ×2
  - Assets/Visuals/Environment/Station_Wheel/textures/Station_Main2.mat ×3

## 3. `art/third-party/` complete and byte-exact

- File set equals the original packs minus `.meta`: 568 files, yes; `.meta` files: 0.
- Same blob id and mode as the source: 568/568.
- Storage mode: 203 LFS pointers (same oid, since the pointer blob is identical), 365 raw blobs; `filter=lfs` attribute agrees with the source for 568/568.
- Per pack: HD_Asteroids 82, Junk_Ships 8, ParticlePack 397, Platform 7, Radio 12, Station_Dildo 9, Station_Fork 14, Station_Hug 10, Station_Spindle 7, Station_Wheel 22

## 6. No stale path strings

- Hits outside `art/third-party/`: 0. (40 vendor `assetPath:` provenance lines inside moved `.meta` files are left untouched: editing them would break `.meta` R100.)

## 7. Build profile unchanged

- `Build Profiles/` and `EditorBuildSettings.asset` changed paths: 0.
