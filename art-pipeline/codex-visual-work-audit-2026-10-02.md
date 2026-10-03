# Codex visual work — organization and hygiene audit (2026-10-02)

Scope: the illustrated-ship arc and its neighbours, 2026-09-22 → 2026-10-02. PRs #649, #658, #691, #724, #725, #730, #738, #740, #767, #814, #821, #843; the `art/` tree; `Assets/Prefabs/Ships`; `Assets/Visuals/Ships`, `Visuals/Studies`, `Visuals/Shaders`; the `codex/*` and `evidence/*` branches; related open issues (#504, #684, #729, #774, #789, #790, #823).

Method: prefab hierarchies were parsed from the serialized YAML (GameObjects, components, nested-prefab sources, override counts); every ship asset's GUID was searched across all serialized assets to find referencers; `git status`, `git ls-files`, branch diffs and PR bodies supplied the process view.

---

## 1. What is working (keep this)

The design output is strong and the *process* around each PR is better than the tree suggests:

- **Production-only landings.** Every art PR since #740 ships only runtime assets. Builders, one-off tests and capture scenarios go to a non-merge `codex/<asset>-authoring` branch, pinned by commit in the PR body.
- **Evidence off main.** Imagegen rounds, prompts, sidecars, turntables and validation JSON live on orphan `evidence/<lease>` branches (`evidence/valis-geometry`, `evidence/vanguard-breakup`, `evidence/laser-bolt`, …), linked by commit-pinned URL.
- **Provenance manifests.** Source/FBX/texture hashes recorded (`asset-validation.json`, `capture-manifest.json`, `mesh-approval.json`, `palettes.json`).
- **PR bodies** name rejected alternatives, fix-ladder rungs, and scope checks. They are the best design records in the repo.
- **Intact-hull consolidation happened.** Crimson went from 78 renderers to 1; Vanguard from 23 to 5. The "tens of parts" problem is real but narrower than it looks (see §3).
- **Some invariants are already tested.** `IllustratedShipPrefabEditModeTests` asserts one rig per ship, saved (not generated) meshes, collider bounds enclosing the hull.

The hygiene failures below are mostly *consistency* and *ownership* failures: each ship was integrated by a fresh one-off recipe, so each one made different structural choices, and the tools that produced the assets live off-tree.

---

## 2. Prefab structure: four ships, four shapes

| Ship | Root prefab | Visual rig | Hull placement | Shared UI prefabs | Thrusters |
|---|---|---|---|---|---|
| Ship_1 (Vanguard) | base `Ship_1` | nested `Ship_1_IllustratedRig` = variant of `Ship_1_VisualRig` (35 overrides, 3 removed components) | `Vanguard/Authored mesh/` → 5 renderers (painted hull, contour, structural ink, canopy, cores) | nested (inherited) | 2 main + 2 small, inherited from legacy hull positions |
| Ship_2 (Crimson) | variant of `Ship_1` (97 overrides) | nested `Ship_2_IllustratedRig` = variant of `Ship_1_VisualRig` (92 overrides); dead override still points at `Ship_2_VisualRig` | `Crimson` → 1 renderer, 2 submeshes | nested (inherited) | 2 + 2, all four overridden to one point (#790) |
| Ship_3 (legacy) | variant of `Ship_1` (86 overrides) | nested `Ship_3_VisualRig` = variant of `Ship_1_VisualRig` (85 overrides) | `Model` legacy FBX | nested | 1 main, pair removed |
| Ship_1_Vanguard (July) | variant of `Ship_1` | nested `Ship_1_Vanguard_VisualRig` = variant of `Ship_1_VisualRig` | `Vanguard.fbx` + 6 `VNG_*` materials | nested | inherited |
| Valis | standalone `Valis` — **0 nested prefabs, 0 overrides** | in-prefab child `Valis VisualRig` (not a prefab) | `Valis` → 1 renderer, 8 submeshes (7 palette regions + contour) | **flattened copies** of `UILockOnIndicator` and `SmokeTrail` (SmokeTrail inactive) | 1 main + 1 small |

Findings:

1. **Two integration idioms coexist.** Ship_1/2/3 are variant chains (ship variant → rig variant → base rig, each leg carrying ~90 overrides). Valis is a flat copy. Neither is stated anywhere; #504 (prefab vs runtime-composition boundary) is the open ruling that would settle it.
2. **Variant-of-variant chains are the root cause of #790.** Thruster emitter positions are inherited from the legacy Vanguard hull and patched per rig by overrides; a new hull silently inherits the old layout. The 90-override legs also make every prefab diff unreadable.
3. **Valis detached from shared prefabs.** Edits to `Prefabs/UI/UILockOnIndicator.prefab` or `Prefabs/Particles/SmokeTrail.prefab` no longer reach Valis. Its `MinimapMarker` renders the *collider* mesh (`Valis collider.asset`); the others render the hull mesh.
4. **Hull node naming differs per ship** (`Vanguard/Authored mesh/…`, `Crimson`, `Valis`) and the rig prefab naming differs (`_IllustratedRig` vs `_VisualRig` vs in-prefab `Valis VisualRig`).
5. **Collider mesh homes differ**: `Visuals/Ships/Shared/Illustrated/Ship_1 collider.asset`, `…/Ship_2 collider.asset` vs `Visuals/Ships/Valis/Meshes/Valis collider.asset`.
6. **Stale roster entry.** `Ship_1_Vanguard` + `Ship_1_Vanguard_VisualRig` (2026-07-10) are still in `ItemCatalog.asset` and are the *only* consumers of `Visuals/Ships/Vanguard/Vanguard.fbx` and the six `VNG_*` materials. The illustrated rig superseded them on 2026-09-28.
7. **Test coverage is ship-number-shaped.** `IllustratedShipPrefabEditModeTests` is parametrized on `Ship_{1,2}.prefab`; Valis is not covered, and its own tests were archived off-tree (`ValisPrefabEditModeTests.cs` on `codex/valis-paint-authoring-archive`). Each ship got a bespoke test file instead of extending the shared one.

---

## 3. "Tens of small Blender parts": where the smell actually lives

Intact hulls are consolidated. The many-parts pattern survives in three places:

| Asset | Renderers / mesh files | Layout |
|---|---|---|
| `Visuals/Ships/Crimson/Breakup/CrimsonBreakup.prefab` | 18 GameObjects, 18 `.asset` meshes | flat in `Breakup/` next to the prefab, anim and 2 materials |
| `Visuals/Ships/Vanguard/Breakup/VanguardBreakup.prefab` | 14 GameObjects, 14 `.asset` meshes | grouped `Pieces/{Armor,Nacelles,Tails,Wings}/`, materials in `Materials/` |
| `Visuals/Studies/DrawnArt/Prefabs/VanguardPreview.prefab` | 25 renderers (14 FBX submeshes + 11 outline `.asset` meshes) | study content, in production `Assets/` |

The two breakup prefabs were built a day apart and already disagree on folder layout and material naming.

**The mesh pipeline is the deeper issue.** Every live illustrated mesh is a **serialized Unity `Mesh` asset generated in-editor** (CombineMeshes + PrefabUtility) by a per-ship builder, not an FBX import:

- `Valis hull.asset` (1.5 MB) — **no FBX exists in `Assets/` at all**; the Blender→Unity interchange for Valis is not in the tree.
- `Crimson.asset` (2.9 MB) sits beside `Crimson.fbx`, which nothing references.
- `Vanguard painted hull.asset` + `Vanguard contour.asset` (5.6 MB) were combined from `VanguardStructure.fbx` submeshes.
- Roughly 17 MB of mesh YAML across the ship folders. `.asset` is not in LFS (only binary extensions are), so every regeneration is a multi-MB text diff.

The generators that produced them are on non-merge branches: `CrimsonBreakupBuilder.cs`, `VanguardBreakupBuilder.cs`, `ApplyValisPaint.cs`, the consolidation helper. Regeneration = "restore the archived builder into a disposable checkout, run the menu item, promote outputs" (six manual steps per ship, three different builders). This inverts AGENTS.md wiring rule 6's corollary: the production tree holds outputs whose producer is not in the tree. It is reproducible on paper and fragile in practice.

The Blender side is fine as artist source (Crimson 39 parts / 33 mirrors; Valis 36 parts; Vanguard ~20 MVP parts). What is missing is the **export contract**: which parts are hull, canopy, cores, debris groups, overlays-to-exclude, and what names the builder requires ("requires the original part names" is the only statement, in a PR body).

---

## 4. Duplicates, orphans and misplaced assets in `Assets/`

**Orphans (zero references):**
- `Prefabs/Ships/Meshes/Ship1.prefab`, `Ship2.prefab`, `Ship3.prefab` (2025)
- `Visuals/Ships/Crimson/Crimson.fbx`
- `Visuals/Ships/Vanguard/DrawnStudy/VanguardStudy.fbx`
- `Visuals/Ships/Vanguard/DrawnStudy/HangarBackground-v1.png`
- `Visuals/Ships/Ship1/StarshipVanguard0606163455TextureObj/starshipVanguard0606163455Texture.obj`
- `Visuals/Ships/Ship3/GalacticCruiserTop0705014531TextureFbx/galacticCruiserTop0705014531Texture.fbx`
- `Visuals/Shaders/DrawnComparison/DrawnScreenInk.shader`, `Visuals/Ships/Shared/ShipShader.shader`
- Valis palette folders `ivory-lavender/` and `jade-gray/` (only `jade-iris/` is wired). Of the 21 palette materials, 5 of 7 are byte-identical between ivory-lavender and jade-gray, and `Ivory.mat` is identical between jade-gray and jade-iris. ~9 distinct materials stored as 21.

**Superseded but live:**
- `Ship_1_Vanguard` pair + `Vanguard.fbx` + `VNG_*` materials (see §2.6).
- `Ship_2_VisualRig.prefab` — reachable only through Ship_2's dead `visualRigPrefab` override.

**Misnamed / misplaced:**
- `Visuals/Ships/Vanguard/DrawnStudy/` holds the **live production** Vanguard: `Vanguard painted hull.asset`, `Vanguard contour.asset`, `Vanguard vivid paint.mat`, `VanguardStructure.fbx`, `VanguardBaseColor.png`. It also holds study-only backgrounds (`HangarBackground-*`, `PlanetBackground-*`, `NebulaBackground-v2`) and `ShadowedPlate.shader`. Meanwhile `Visuals/Ships/Vanguard/Vanguard.fbx`, which `art/README.md` calls "the Unity export", is the superseded July model.
- `Visuals/Shaders/DrawnComparison/` ("Comparison" is study-era naming) is the home of the production ship and asteroid shaders (`Drawn Surface`, `Drawn Contour`, `Drawn Canopy`).
- `Visuals/Studies/DrawnArt/` — 3 scenes, 16 materials, 11 outline meshes, 2 prefabs, a bloom profile — study content in production `Assets/` (already inventoried on #774).
- Shader homes are split four ways: `Visuals/Shaders/DrawnComparison/`, `Ships/Vanguard/DrawnStudy/`, `Ships/Shared/`, `Vfx/LaserBolt/Shaders/`.
- Naming styles collide: `VNG_Hull_White` vs `Hull paint` vs `Debris blue core`; `ivory-lavender` (kebab) beside `Materials` (Pascal); `Ship_1_IllustratedRig` vs `Valis VisualRig`.

---

## 5. `art/` tree hygiene

Numbers: 100 tracked files, **271 untracked** (206 MB), 474 MB on disk. `art/ships/vanguard/experiments/` alone is 138 MB untracked.

1. **A three-week-old uncommitted reorganization sits on the primary tree.** `git status` shows `art/ships/starship_scratch_model_3.blend` deleted and `art/ships/vanguard/{vanguard.blend, README.md, textures/, reference/, guides/, experiments/}` untracked, with `art/README.md` modified to point at the new path. `git log --all` has no `vanguard.blend` on any branch. The vanguard README (also untracked) says the reorg "did not update Unity assets" and that the only backup is a gitignored `scratch/art-reorganization-20260909-225001/`. If the working tree is lost, the active Vanguard source goes with it. This also violates "build in a pooled worktree, never the primary tree".
2. **Imagegen experiment folders never left main's working tree.** `art/gameplay/experiments/2026-09-23-*`, `art/gameplay/experiments/2026-09-28-laser-bolt/`, `art/hangar/experiments/2026-09-22-*`, `art/hangar/experiments/2026-09-23-*`, `art/ships/ship3/experiments/2026-09-23-*`, `art/ships/vanguard/experiments/2026-09-*`. The `aesthetic-authoring` skill already says these belong on `evidence/<lease>` under `history/`; the laser-bolt set *was* copied to `evidence/laser-bolt` but the local copy was left behind. `art/gameplay/experiments/2026-09-28-laser-bolt/crackle/crackle.py` is a stale draft of the shipped `art/vfx/laser-bolt/crackle/crackle.py`.
3. **Scratch generator scripts.** Tracked and superseded: `art/ships/vanguard/drawn-study/build_structure.py`, `export_study.py` (the README itself says export_study "is not the selected paint pipeline"); neither has a `uv` script header or a README row. Untracked: `capture_views.py`, `build_texture_mvp.py` (405 lines), `build_texture_mvp_v2.py` (461 lines), `finish_preview.py`.
4. **Backup files.** `vanguard.blend1`, `vanguard_uv_work.blend1`, `vanguard-textured-mvp.blend1`, … are present and not gitignored.
5. **Partial tracking.** `art/ships/valis/concepts/` (3 files) untracked while the rest of `valis/` landed in #843.
6. **Stale README claims.** `art/README.md`: "The Unity export remains at `Visuals/Ships/Vanguard/Vanguard.fbx`" (superseded). `art/ships/vanguard/drawn-study/README.md` points regeneration at a `pipeline/README.md` that exists only on `evidence/vanguard-retexture`.
7. **Root clutter from the same pattern** (not art, same habit): `reports/`, `research_notes/`, `sync.json` untracked at repo root.

---

## 6. Root causes

1. **No written anatomy.** Nothing in `doc/agents/` says what a ship's prefab, folder, mesh and export shape is. `unity-conventions.md` covers scripts (namespaces, folders) and `aesthetic-authoring` covers VFX folder shape. Codex re-infers the ship shape on every ticket and, being good at design, invents a slightly better one each time.
2. **Producer off-tree.** Three bespoke builders on three archive branches. Nothing shared, nothing on main, so nothing to extend and nothing to test.
3. **Experiments default to "leave it".** The evidence-branch rule exists but has no mechanical enforcement; untracked files are invisible to the PR diff, so they accumulate.
4. **Primary-tree work.** The Sep 9 reorg and the Vanguard experiments were done in the main checkout, not a pool slot.
5. **#504 unresolved.** The prefab-vs-runtime boundary is the question underneath §2; until ruled, every hull re-decides it.

---

## 7. Improvement plan

Principle: let codex keep owning *what the asset looks like*; move *where it goes and what shape it takes* out of its judgement and into (a) one doc it must read, (b) one repo-owned tool it must drive, (c) tests and a hygiene check that fail loudly.

### P0 — Rescue and sweep (small, this week)

- **Commit the Vanguard reorg** as the rename it is: `starship_scratch_model_3.blend` → `vanguard.blend`, plus README, `textures/`, `reference/`, `guides/`. Move `experiments/` (138 MB) to an orphan `evidence/vanguard-history` branch and delete it locally.
- **Sweep the imagegen experiment folders** (`art/gameplay`, `art/hangar`, `art/ships/ship3`, `art/ships/valis/concepts`) to their evidence branches; delete local copies; delete the stale `crackle.py` draft.
- **Ignore policy**: add `*.blend1` to `art/.gitignore`; decide `reports/`, `research_notes/`, `sync.json`.
- Fix the two stale README claims.

### P1 — Write the contract (one doc, one skill pointer)

`doc/agents/art-pipeline.md`, branch-triggered from AGENTS.md ("Adding or changing an asset's visuals → read this"):

- **Folder shape.** `art/<category>/<name>/{<Name>.blend, README.md, textures/, reference/, *.json manifests}`; experiments never on main. `Assets/Visuals/Ships/<Name>/{<Name>.fbx, Meshes/, Materials/, Paint/, Breakup/}`. `Studies/`, `DrawnStudy/`, `Comparison/` are not production names.
- **Export contract.** Blender part-naming grammar the builder consumes (hull / canopy / core.L|R / debris.<group>.<side> / overlay.* excluded); axes; the FBX is always present in `Assets/` as the interchange even when the rig renders a combined mesh.
- **Ship anatomy.** Root sim components; `Mesh` (collider); `Hardpoints`; `ShipBody`; exactly one nested `<Name>_VisualRig` prefab with `Hull`, `Thruster` (emitters on named sockets, the structural fork of #790), `Shield`, `MinimapMarker`, shared UI as nested prefabs, `Breakup`. New ship = new base + its own rig; no variant-of-variant chains.
- **What lands where**: main / `codex/*-authoring` / `evidence/*` / nowhere.
- **Naming**: one material and folder style.

Point the `aesthetic-authoring` skill at it; add an "asset handoff" checklist to the brief template so the target paths and part map are fixed on the issue *before* codex starts.

### P2 — One builder, one test (one PR, pr-prep first)

- Promote the three archived builders into a single in-tree `ShipAssetBuilder` editor tool driven by a per-ship manifest (`art/ships/<name>/unity-manifest.json`: FBX, part→role map, palette regions, breakup groups, motion constants). Outputs become regenerable from main; codex fills a manifest instead of writing a new builder.
- Grow `IllustratedShipPrefabEditModeTests` into a catalog-driven **ship anatomy test**: for every prefab in `ItemCatalog`, assert the anatomy (one rig, nested shared UI, named sockets, saved hull mesh with FBX sibling, collider encloses hull, no overrides on emitter transforms, no variant-of-variant). This is the guardrail codex cannot talk its way around (fix ladder rung 2).

### P3 — Hygiene PRs (dedicated, each a chip, after P1)

- **Prefabs:** retire the `Ship_1_Vanguard` pair, `VNG_*`, `Vanguard.fbx`, `Prefabs/Ships/Meshes/*`, `Ship_2_VisualRig`; rename `Ship_N_IllustratedRig` → `<Name>_VisualRig`; rebuild Valis as base + nested rig and re-nest its UI prefabs; collapse the 21 palette materials to the distinct set (or material variants) and wire palette selection via #823.
- **Assets folders:** `Vanguard/DrawnStudy` → production meshes to `Vanguard/Meshes`, paint to `Vanguard/Paint`, plates and `ShadowedPlate.shader` to `Studies/`; `Shaders/DrawnComparison` → `Shaders/Illustrated`; fold into or sibling of #774 (GUID-preserving `AssetDatabase.MoveAsset`, build-profile path sweep as #774 notes).
- **art/:** remove or archive `build_structure.py` / `export_study.py`; `uv` headers on kept scripts; README rows for every file.

### P4 — Process guardrails aimed at codex

- Codex reads `AGENTS.md` and `.agents/skills` → `.claude/skills`. Every rule must live there, not in Claude memory. One AGENTS.md line under Default workflow: asset handoffs follow `doc/agents/art-pipeline.md`; a tree that deviates from the anatomy is a hygiene failure, not a style choice.
- `scripts/art_hygiene.sh`, run by the pool verify step for any PR touching `art/` or `Assets/Visuals|Prefabs`: fail on untracked files under `art/` or `Assets/`; new `.py` under `art/` outside `tools/` or `<asset>/generator/`; non-hygiene PRs touching `Studies|DrawnStudy|Experiments` paths; a `Mesh` `.asset` with no FBX sibling. Run the anatomy test for asset PRs (relates to #589, the reduced-suite tier for asset-only diffs).
- Codex runs in a pool slot (`codex exec --cd <slot>`), never the primary tree; add to the codex section of `doc/agents/environment.md`.
- Rule #504 before the next hull: write the prefab-vs-runtime boundary down, even as the current implicit line.

### Suggested tracker shape

One arc issue ("Ship asset pipeline: one anatomy, one builder, hygiene sweep") with P0 as an immediate child, P1 and P2 as design children (pr-prep), and P3 as three hygiene children. #504, #774, #790, #823 become dependencies or siblings rather than duplicates.
