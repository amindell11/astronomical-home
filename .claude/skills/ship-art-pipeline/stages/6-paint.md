# Ship art stage 6: paint

One recipe: grayscale shadow, light and ink masks over recolourable base regions, so
palettes stay swappable (#823). Valis is the worked example (`art/ships/valis/README.md`).

## Inputs

- The source under the geometry lock, with passing UVs.
- The approved concept views; a flat render of the model (`ship_render`).

## Commands

None yet. Mask projection and palette material generation arrive with the arc's
second-cut tools (#868, slice 8), which add the commands here. Paint concepts use
`art/tools/imagegen/README.md`, painting over the flat render.

## Done

- An approved paint concept picture, then masks that reproduce it on the model.
- In game, paint only swaps materials on the flight-checked prefab.
- Concept rounds and mask previews are on `evidence/<name>` under `paint/`.

## Owner's gate

Two approvals in chat: the paint concept picture, then the masks in game.

## Forbidden moves

- **Masks before an approved concept picture.**
- **Baking colour into one atlas.** That is the Crimson and Vanguard recipe; new paint
  uses masks only.
- **Changing geometry.** `ship_lock --mode verify` opens the stage; a shape change is a
  reopen.
