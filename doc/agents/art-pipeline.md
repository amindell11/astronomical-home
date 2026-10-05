# Ship art law

> STATUS: living — branch-triggered reference, read before touching ship art; pointed at from `AGENTS.md`.

These rules hold at every stage of making a ship. The stage-by-stage workflow is the
`ship-art-pipeline` skill; tool commands and source conventions are
`art/tools/ship/README.md`. Agents follow this page by hand: no script checks it. The
anatomy test (`ShipAnatomyEditModeTests`) and import validation
(`ShipRoleImportPostprocessor`) are the only automated checks on ships.

## Layout

One name per ship, used verbatim for every path and file prefix:

| Path | Holds |
|---|---|
| `src/Asteroids3D/Assets/Prefabs/Ships/<Name>.prefab` | the **chassis prefab**: a depth-1 variant of `ShipBase.prefab`; what the catalog, sectors and tests reference |
| `src/Asteroids3D/Assets/Visuals/Ships/<Name>/` | the **visuals folder**: `<Name>.fbx`, materials, paint, breakup |
| `src/Asteroids3D/Assets/Visuals/Ships/_Shared/` | `ShipBaseRig.prefab`, rig parts, shared materials |
| `art/ships/<name>/` | the **art folder**, lowercase: `ship.json`, the `.blend` source, approved concept views |

- `art/` is the full asset base. `Assets/` holds only what something we own uses, named
  for that use.
- A file reaches `Assets/` by `AssetDatabase.MoveAsset` or by overwriting the existing
  file in place, so its GUID survives. A copy gets a new GUID and orphans every reference.
- `art/third-party/` and `art/meshy/` are verbatim provenance; generator names stay there.

## Name mirror

Every chassis in `ItemCatalog` has exactly one same-named visuals folder and exactly one
art folder (its name lowercased). Unless the name is on `ShipLegacyList.HullModels`, the
art folder holds a `ship.json` whose `name` is the chassis name. The anatomy test covers
the FBX; the folders and `ship.json` are yours to keep true.

## Legacy list

`ShipLegacyList` names the ships not yet migrated. Each array exempts only from its own
check: `Chassis` from the anatomy test, `HullModels` from import validation and the
hull-mesh checks, `SavedColliderMeshes` from the collider-from-FBX check. A name on
`Chassis` or `HullModels` is also exempt from every rule on this page. The list only
shrinks: each migration PR deletes its name.

## Tree hygiene

For every ship not on the legacy list:

- **Commit, never copy.** Before a risky edit, commit the file and edit it in place; a
  rejection restores it from that commit. No file under `art/` or
  `src/Asteroids3D/Assets/` has `-before-` or `-candidate` in its name.
- **Build-tree folders are named for their use.** No folder under
  `src/Asteroids3D/Assets/` outside `Scripts/` matches, case-insensitively, a study word
  (`study`, `studies`, `comparison`, `approved`, `candidate`, `experiment`, `prototype`,
  `scratch`, `wip`, `demo`, `sample`, `evidence`) or a generator pattern (a run of 10+
  digits, `chatGptImage`, a `Texture(Fbx|Obj)` suffix). Studies and downloads live under
  `art/`.
- **Ship tooling lives in `art/tools/ship/`.** `art/ships/` holds no scripts. A per-ship
  geometry script is scratch and goes to the ship's evidence branch. Generator folders
  elsewhere under `art/` (`aesthetic-authoring`'s `art/<category>/<asset>/`) are outside
  this rule.

## Who may change geometry

- The agent builds the blockout and does mechanical cleanup.
- From the owner's first hand edit, the `.blend` is the source of record; the
  `ship-art-pipeline` skill lists the moves that would overwrite it. A risky agent edit
  commits first (above), and the owner judges the result in the viewport.

## Where the work happens

- Art agents work in a pool slot (`agent-worktree-pr-loop`), never the primary tree.
- Evidence lives on one branch per ship, `evidence/<name>`, with a folder per stage and a
  README indexing them (model: `evidence/valis`). Publish with the orphan-branch recipe in
  `agent-worktree-pr-loop` → Step 4, Visual evidence, with that branch name and the
  stage folder. Only approved sources and tools reach main.
