# Ship art stage 7: breakup

Every ship has a breakup. Pieces are declared in the source; motion comes from house
defaults (Crimson's approved timing, soot, and the centre vanishing in the flash) with
per-ship number overrides in a settings file.

## Inputs

- The painted ship, its geometry lock verifying.

## Commands

None yet. Debris roles (`role.debris.<id>`) are reserved and the exporter refuses them in
this version; the breakup build arrives with the arc's second-cut tools (#868, slice 8),
which add the commands here.

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
