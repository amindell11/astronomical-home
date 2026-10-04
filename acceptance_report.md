# Slice 6b acceptance report

Base `517b6226`, head `4cc432c3`.

**Result: PASS**

## 1. GUIDs kept

- 11 moved files: GUID at the new path equals the original: yes.
- Their `.meta` files as git renames: {'R100': 11}.
- Other `.meta` rename pairings (folder-template noise): ['R077 Assets/Scripts/Editor/Visuals.meta -> Assets/Visuals/Ui/AppIcon.meta'].
- Manifest rows by kind: {'moved': 11, 'deleted': 95, 'art': 33, 'evidence': 53}. Removed GUIDs absent at head: 95/95.

## 2. Every reference still resolves

- Whole-tree scan of `Assets/`, `ProjectSettings/` and `Packages/` (text files; `.meta` folded into its asset; LFS pointers skipped).
- References at head to a GUID this slice removed: 0.
- Resolved references: before 3355, after 3137.
- Arithmetic: 3355 − 136 (internal to the removed study sets) − 82 (removed study files pointing at production assets) = 3137 (after: 3137).
- References from surviving files into the removed set before the change: 0. Every surviving referrer keeps exactly its resolved references (moved owners mapped to their new paths): yes.

## 3. `art/` complete and byte-exact

- 33/33 `art/` copies have their source's blob id and mode; `.meta` files under `art/`: 0.
- Storage mode: 30 LFS pointers (identical pointer blob, so the same oid), 3 raw blobs; the `filter=lfs` attribute agrees with the source for 33/33.
- Per folder: `art/asteroid-study/` 7, `art/concepts/` 1, `art/meshy/AColossalDarkGray0729085551TextureFbx/` 2, `art/meshy/GalacticCruiserTop0705014531TextureFbx/` 2, `art/meshy/StarshipVanguard0606163455TextureObj/` 4, `art/ships/nightshade/concepts/` 7, `art/ships/vanguard/concepts/` 4, `art/ships/vanguard/drawn-study/` 5, `art/vfx/explosion/refs/` 1
- Evidence copies:
  - `evidence/vanguard` at `375a63dd`: 99/99 entries carry their source blob id and mode (57 of them `.meta`).
  - `evidence/asteroids` at `e7747046`: 22/22 entries carry their source blob id and mode (11 of them `.meta`).

## 4. Structural equivalence after a forced reimport

- 24 targets (every prefab and scene outside the removed set reaching a moved GUID directly, through a material, or through prefab nesting); 653 component lines before and after.
- After the moves, the shader renames and a forced reimport of all 11 moved files: byte-identical (`cmp`): yes. Material ids carry their shader GUID (779 lines).

## 5. Shaders compile

- After the renames and the post-delete recompile (`ShaderUtil`, `Shader.Find`):
  - `Astronomical/Drawn/Surface` (DrawnSurface.shader): find=True, hasError=False, errors=0, passes [DrawnSurface,ShadowCaster,DepthNormals,DepthOnly]
  - `Astronomical/Drawn/Contour` (DrawnContour.shader): find=True, hasError=False, errors=0, passes [DrawnContour]
  - `Astronomical/Drawn/Canopy` (DrawnCanopy.shader): find=True, hasError=False, errors=0, passes [DrawnCanopy,ShadowCaster,DepthNormals,DepthOnly]
  - `Astronomical/Vfx/Jagged Explosion Flipbook` (DrawnExplosion.shader): find=True, hasError=False, errors=0, passes [<Unnamed Pass 0>]
  - `Astronomical/Vfx/Traveling Ember` (SparkBurn.shader): find=True, hasError=False, errors=0, passes [<Unnamed Pass 0>]
- `DrawnCanopy` resolves its three `UsePass` passes (ShadowCaster, DepthNormals, DepthOnly), the same pass list as before the rename.
- `Shader.Find("Astronomical/Comparison/Drawn Surface")` returns null after the recompile.

## 6. No stale strings

- Hits outside the exemptions: 0.
- Exempt: 5 material `m_Name` display strings (W4), 0 `assetPath:` provenance lines, 1 README line(s) naming the new evidence location, 1 line(s) inside byte-exact `art/` copies (item 3 forbids editing them; Unity never imports `art/`):
  - `src/Asteroids3D/Assets/Visuals/Ships/Valis/Materials/Contour.mat:10`
  - `src/Asteroids3D/Assets/Visuals/Vfx/LayeredExplosion/Materials/AsteroidContour.mat:10`
  - `src/Asteroids3D/Assets/Visuals/Vfx/LayeredExplosion/Materials/Core.mat:10`
  - `src/Asteroids3D/Assets/Visuals/Vfx/LayeredExplosion/Materials/Smoke.mat:10`
  - `src/Asteroids3D/Assets/Visuals/Vfx/LayeredExplosion/Materials/Spark.mat:10`
  - `art/ships/vanguard/drawn-study/README.md:19`
  - `art/ships/vanguard/drawn-study/ShadowedPlate.shader:1`
- `art/meshy/` is outside the scan: it holds the generator downloads verbatim, names included.

## 7. F4 study-named folder scan

- Folder names under `Assets/` minus `Assets/Scripts/`, case-insensitive substring match on the 12 study words, plus a run of 10+ digits, `chatGptImage` and a `Texture(Fbx|Obj)` suffix.
- Head: 0 hits. Base: 10 hits, all in this slice's removed folders:
  - `Assets/Visuals/Environment/Asteroids/DrawnStudy`
  - `Assets/Visuals/Environment/Models/AColossalDarkGray0729085551TextureFbx`
  - `Assets/Visuals/Environment/Models/AColossalDarkGray0729085551TextureFbx/AColossalDarkGray0729085551TextureFbx`
  - `Assets/Visuals/Shaders/DrawnComparison`
  - `Assets/Visuals/Ships/Nightshade/GalacticCruiserTop0705014531TextureFbx`
  - `Assets/Visuals/Ships/Ship1/StarshipVanguard0606163455TextureObj`
  - `Assets/Visuals/Ships/Vanguard/DrawnStudy`
  - `Assets/Visuals/Ships/_Shared/StarshipVanguard0606163455TextureObj`
  - `Assets/Visuals/Studies`
  - `Assets/Visuals/Vfx/ApprovedPlasma`

## 8. Build profile unchanged

- `Build Profiles/` and `EditorBuildSettings.asset` paths in the diff: 0.

## 9. Full suite green

- `run-tests agent-2 -Mode Both -ScopeType Workspace` on `614da6e4`: `STATUS=passed total=974 passed=972 failed=0 skipped=2` (`test_summary_full.json`). Later commits are docs-only.
