# Ship art stage 2: blockout

## Inputs

- The approved top view and side profile in `art/ships/<name>/concepts/`.

## Commands

All from `art/tools/ship/README.md`:

- Write `art/ships/<name>/ship.json` (its fields are in the README), then `ship_new` with
  `--top` and `--side` set to the approved views. It creates the source with the
  conventions, role collections, reference planes, cameras and review preset.
- Block out proportions in Blender over the reference planes.
- `ship_check` for the source rules; `ship_render --set ortho` and `--set scale` for
  review.

## Done

- Proportions match the approved top and side views.
- `ship_check` passes, or each finding the owner accepts is an exemption in `ship.json`
  with its reason.
- The source and `ship.json` are committed in the slot; renders are on `evidence/<name>`
  under `blockout/`.

## Owner's gate

The owner approves the proportions in chat, from the renders or their own viewport.

## Forbidden moves

- **Blocking out anywhere but Blender.** Generated meshes and scripted builds missed the
  references on past ships.
- **Detail before approved proportions.** Panels, greebles and cutouts wait for the
  detail model.
- **Applying Mirror modifiers.** They stay live so the owner can edit one side.
- **A geometry script under `art/ships/`.** Scratch builders go to the evidence branch.
