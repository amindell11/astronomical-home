# Ship art stage 4: flight check

The unpainted ship flies in the game with flat region colours, its collider and its
mounts. All Unity wiring happens here, so paint later only swaps materials.

## Inputs

- The approved detail model.

## Commands

- `ship_export` (`art/tools/ship/README.md`) writes `<Name>.fbx` and its sidecar
  `<Name>.export.json`. The FBX becomes
  `src/Asteroids3D/Assets/Visuals/Ships/<Name>/<Name>.fbx`, overwriting an existing one in
  place; the sidecar goes to `evidence/<name>`.
- Unity wiring is a coding task in a pool slot (`agent-worktree-pr-loop`):
  - the chassis prefab `Assets/Prefabs/Ships/<Name>.prefab`, a depth-1 variant of
    `ShipBase.prefab`;
  - the hull under the `ShipBaseRig/Hull` anatomy slot, referencing the FBX's sub-assets;
  - sockets, the collider from the `Collider` role, flat region colours and authored
    inertia;
  - an `ItemCatalog` entry.
- The owner flies it.
- On approval: `ship_lock --mode lock`; commit `lock.json` beside the source.

## Done

- `ShipAnatomyEditModeTests` passes with the name on no `ShipLegacyList` list, and import
  validation accepts the FBX.
- The owner flew the ship and approved its shape and handling.
- `lock.json` is committed; the name mirror holds (`doc/agents/art-pipeline.md`).

## Owner's gate

The owner flies the unpainted ship and approves it in chat. That approval writes the
geometry lock, the only approval a tool enforces.

## Forbidden moves

- **Writing the geometry lock before the owner has flown it.** Valis's geometry was
  approved as final before it flew; the revision after flying rebuilt its UVs, hull and
  rig.
- **Painting or texturing before this stage passes.**
- **Inertia derived from the collider.** Inertia is authored, so an art change cannot
  alter balance.
- **Overrides outside the declared anatomy slots, or a variant of a variant.** A
  one-ship feature goes under `Hull`; anything else needs a new anatomy slot on the base,
  in a reviewed base change.
- **A saved `Mesh` asset for the hull or collider, or the model prefab nested.** Both
  meshes come from the FBX's roles, and prefabs reference its sub-assets.
