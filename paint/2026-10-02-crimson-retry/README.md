# Valis — asymmetric Crimson paint study

Open `Valis-Crimson-study.blend`. This is the review candidate, not the gameplay export.
The 36 approved mesh parts and 36 live Mirror modifiers are unchanged. The exact
approval fingerprint is in `validation.json`. Geometry still mirrors across X;
paint does not. Mirror UV offset V = -0.5 selects independent left-side paint.

## Hand editing

`layers/Shadow.png`, `Light.png`, and `Ink.png` are the authoritative 4096px
paint masks. White adds the named effect; black removes it. Open the matching image
in Blender's Texture Paint workspace. Paint, save the external image, repack it,
and save your blend under a new revision filename. The saved source packs all three
images and opens with Shadow as the active paint target.

`Valis-Paint-Layers.ora` opens the same three grayscale layers in Krita. Export each
changed mask back to its named PNG, reload those images in Blender, repack, and save.
`UV-guide.png` labels the islands. The upper half of the atlas paints the original
right half; the lower half paints its mirrored left half. Chart shapes and order
match between halves. Side and underside islands are separate. Wire lines in the
UV guide are editing aids; they are absent from the paint layers.

The material's `BASE COAT — recolor here` RGB node controls its palette region.
`PAINT — independent layers` exposes Shadow strength, Light strength, and Ink
strength. The masks and original base coat remain separate. All three approved
palettes are stored in the scene and unchanged in `palettes.json`.

## Live controls

`valis-paint-review.html` is the self-contained conversation review source.
Top and quarter views use paint layers rendered from the actual Blender model.
The 60/100/150px comparisons share nose-to-tail height with Crimson. Large PNG
comparisons retain the full-resolution model evidence. Preview controls export
one settings block; they do not modify native files automatically.

Save exported settings as a JSON file, then run Blender in background with:

    blender -b --python apply_settings.py -- --source YOUR_EDITED.blend --settings SETTINGS.json --output NEW_REVISION.blend

The helper changes controls and base colors in the supplied artist-edited blend.
It preserves its texture pixels and refuses to overwrite an existing output.

## Source and preservation

`paint-source.png` was made with the built-in imagegen tool from the flat approved
model and Crimson top/quarter references. `paint-provenance.json` retains the prompt.
Its painted tones were transferred into separate grayscale masks, with restrained
panel ink; the geometry and palette were not taken from the generated image.
The asymmetric pass follows the user's preference on 2026-10-02.

The masks and native blend are the source for all further art edits. The extraction
and construction helpers are historical processing evidence. `ARTIST_SOURCE_LOCK`
prevents them from overwriting this candidate. Any fresh processing experiment must
use a new revision directory and retain the edited layers and blend here.

Evidence: Blender unlit painted-model renders. Unity's existing flat-color Valis
integration, handling, weapons and roster remain untouched. No PR or merge has been
performed for this study. This evidence checkout is preserved locally; it is not
claimed as published remotely.
