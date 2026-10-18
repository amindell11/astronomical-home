# Ship art stage 6: paint

One recipe, one tool. Palette regions start in material slots, with optional authored coverage
on `PaintUV` for livery crossing faces. Three grayscale
paint masks (Shadow, Light, Ink) on `PaintUV` are generated from the locked geometry by
Crimson's rules; one `_PaintLayers` material per palette per region, so palettes stay
swappable (#823). The rules and knobs: `art/tools/ship/README.md` → Paint settings. Valis is
the worked example of the storage (`art/ships/valis/README.md`); Crimson is the look.

## Inputs

- The source under the geometry lock, with passing UVs.
- The approved concept picture, and from it the palette (`palettes.json`: one linear-sRGB
  colour per region) and the region assignment (starting slots and authored coverage).
  Concepts use `art/tools/imagegen/README.md` over the flat render; they decide colours and
  regions and nothing else.

## Commands

1. `ship_lock --mode verify` passes.
2. Regions: give every visual-role part its starting palette-region material in the source
   (a material edit, not a geometry edit; commit first). For livery crossing faces, use
   `paint.json`'s optional `authored_regions` descriptor; see the README schema.
   Write `palettes.json` and `paint.json`
   beside the source with every field required by the README schema, and copy a brush tile in (Crimson's
   `textures/painted-brush-source.png` is the house tile).
3. `ship_paint --ship art/ships/<name>/ship.json --out <dir> [--unity src/Asteroids3D/Assets/Visuals/Ships/<Name>]`
   writes `layers/` beside the source and, under `--out`, `preview/Composite.png` and the
   Unity bundle `unity/<Name>/`.
4. `ship_export`; import the FBX; copy the bundle over `Assets/Visuals/Ships/<Name>/`,
   `.meta` files only where none exist; bind the materials on the chassis prefab; judge the
   ship in the lineup beside the others.
5. Owner edits go onto `layers/<Mask>.png` (Texture Paint on the packed image, or an
   external editor); save and repack. Re-run `ship_paint` whenever a knob or the palette
   changes: the edits survive through `layers/baseline/`.

## The look

- Per plate, a jittered diagonal sweep into a dark third (Shadow) and a light third (Light);
  side walls darken by their facing (Shadow).
- An inset ink line inside every plate boundary (Ink) with a bright rim just inside it
  (Light); thin walls fully inked.
- A quiet world-space brush tile, mirrored across X, in Shadow and Light at low contrast.
- Named `canopy_glass` and `canopy_frames` parts keep only their palette colour;
  reflections are owner mask edits, preserved across re-runs.
- A short list of accents placed by coordinates for indicator lights and emission.
- Strong flat region colours do the rest.

## Done

- The palette and regions approved, then the generated masks approved in the lineup.
- In game, paint only swaps materials on the flight-checked prefab; the materials bind the
  same maps Valis binds.
- `palettes.json`, `paint.json` and `layers/` (baseline included) are committed with the
  source; concept rounds and previews are on `evidence/<name>` under `paint/`.

## Owner's gate

Two approvals in chat: the palette and regions (the concept picture beside a flat render in
region colours), then the masks in the Unity lineup.

## Forbidden moves

- **Masks before an approved palette, or masks left standing after a palette change.**
  Masks derive after the palette is approved and are re-derived on every palette change.
  Nightshade's masks were tuned against a palette that no longer existed; that is how it
  went "too painted".
- **Wear, damage or grunge the owner did not ask for.** Rejected on every ship it appeared
  on (Vanguard, Nightshade, Bulwark).
- **Projecting a painting onto the mesh.** Imagegen is a concept and palette step: no style
  transfer, no marker painting read back per texel. `ship_paint` is the only mask generator.
- **A second UV layout for asymmetric paint.** One layout, Valis's halves: native charts in
  the lower half of the atlas, mirrored charts in the upper.
- **Judging a candidate from a Workbench preview.** Candidates are judged in the Unity
  lineup beside the other ships, at game scale as well as large.
- **Baking colour into one atlas.** That is the Crimson and Vanguard recipe; new paint uses
  masks only. `preview/Composite.png` is a review image, never imported.
- **Overwriting hand edits.** `ship_paint` keeps them through `layers/baseline/`; never
  delete the baseline to tidy up, never regenerate masks with another script.
- **Changing geometry.** `ship_lock --mode verify` opens the stage; a shape change is a
  reopen.
