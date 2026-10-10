# Ship art stage 7: breakup

Every ship has a breakup. Pieces are declared in the source; motion comes from house
defaults (Crimson's approved timing, soot, and the centre vanishing in the flash) with
per-ship number overrides in a settings file.

## Inputs

- The painted ship, its geometry lock verifying.

## Commands

Verify the geometry lock, then run `ship_export --debris` with the stage's `--ship`
and `--out` paths. Group ownership and paired mirrored sections follow
`art/tools/ship/README.md` → Debris export. Import the emitted FBX under the ship's
`Breakup/` folder. Generate the legacy motion clip and prefab through the Editor from
`art/ships/<name>/breakup.json`; preserve the build script with the capture evidence.
The shared motion/prefab builder remains deferred to #868, slice 8.

## Done

- Every piece is a debris role in the source.
- Motion is the house defaults plus this ship's overrides in its settings file.
- The owner has approved the breakup in game; captures are on `evidence/<name>` under
  `breakup/`.

## Owner's gate

The owner reviews the breakup in game and approves it in chat. Piece count is theirs.

## Forbidden moves

- **Hand animation.** Motion is defaults plus number overrides.
- **Grouping pieces by object names in C#.** Grouping lives in the source's debris roles.
- **A per-ship breakup builder.** Scratch goes to the evidence branch.
- **Changing geometry.** `ship_lock --mode verify` opens the stage; a shape change is a
  reopen.
