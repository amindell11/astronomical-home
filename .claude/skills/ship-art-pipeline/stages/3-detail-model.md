# Ship art stage 3: detail model

## Inputs

- The approved blockout.

The owner shapes the ship by hand; the agent does mechanical cleanup after each pass.
From the owner's first hand edit the `.blend` is theirs (`doc/agents/art-pipeline.md` →
Who may change geometry).

## Commands

From `art/tools/ship/README.md`:

- Link every part into a role collection; place `role.sockets` empties where hull
  geometry positions engine emitters and hardpoints.
- Moving parts are optional and decided here: which assemblies move and their end poses.
  Author them in the source; `role.move.<id>` is reserved and the exporter refuses it in
  this version.
- `ship_check` before and after every agent edit. Its per-part fingerprints, hidden
  parts included, must show only the parts the edit claims to touch.
- Risky edit: commit the saved source, edit in place, then
  `ship_render --set compare --before <committed .blend>` for the owner.
- Review: `ship_render --set turnaround` and `--set sheet`.

## Done

- The owner has approved the shape.
- `ship_check` passes, or each accepted finding is a `ship.json` exemption with its
  reason.
- The source is committed; renders are on `evidence/<name>` under `detail-model/`.

## Owner's gate

The owner approves the detail model in chat, including the moving parts and their poses.

## Forbidden moves

The standing list in `SKILL.md` applies in full; this is the stage where it bit hardest.

- **An edit whose fingerprints touch parts it did not claim.** Revert from the commit
  and redo it narrowly.
- **Fixing a check finding by applying scale or modifiers.** Record an exemption and let
  the owner decide.
