# Flat background authoring

Render starless clouds that repeat in both axes (a 4D-torus noise field) as a
native flat background for Unity's sky layers. Stars belong to Unity's
starfield, not to this image. Blender 5.1+ is required; Cycles picks an
available GPU backend and otherwise uses the CPU.

## Install the Blender panel

Zip the `flat_background` directory so the archive contains
`flat_background/__init__.py` and its sibling Python/JSON files. From the
repository root, for example:

```powershell
Compress-Archive -Path art/tools/flat_background -DestinationPath flat-background-addon.zip -Force
```

In Blender: **Edit > Preferences > Add-ons > menu > Install from Disk**, select
that ZIP, then enable **Flat Background**. In the 3D View, press **N** and choose
the **Flat Background** tab. To update, finish any render, remove the old add-on
in Preferences, restart Blender, then install the new ZIP.

Save your `.blend` to keep panel settings, output paths and the selected Unity
project. **Save Preset** writes a portable JSON containing all appearance
settings; **Load Preset** restores it. Output/project paths are local choices
and are not part of presets. **Load Nebula Glow** restores the default
appearance without changing those paths.

## Make a background

1. Load a preset (for example `illustrated-blue.json`) or start from the
   defaults, then adjust a few controls.
2. Click **Render Draft** (**Draft Size** 512/1K/2K, 8 samples).
3. **View Last Render** shows the AgX-tonemapped `_preview.png`; it does not
   reproduce Unity bloom.
4. **Export Final** renders at **Final Size** (1K/2K/4K, 2K default).

Rendering uses a temporary scene and restores the open scene afterward. Cancel
from Blender's render view with Esc. Each render writes
`<name>-draft|final.exr` (scene-linear half float), `_preview.png` and a `.json`
sidecar: `schema_version`, `stage`, dimensions, the four palette roles
(base/primary/secondary/accent) and provenance (Blender version, generator,
samples, the exact preset and its migration report).

| Control | Effect |
|---|---|
| Cloud Variation | Repeatable offsets into the 4D noise field. |
| Cloud Scale | Higher values give smaller, more numerous cloud features. |
| Stretch | X/Y set the torus radii; Z sets cloud detail. |
| Rotation | X/Y shift the periodic phases; Z offsets the 4D field. |
| Cloud Coverage | Relative -100 to +100; 0 is neutral, negative is sparse, positive dense. |
| Core Emission | Emission gain in bright dense cloud. |
| Four palette colors | Cloud emission colors, also exported as the sidecar palette roles. |

**Nebula Colors** offers color families (**Use Scheme**), seeded variations
(**Apply Seed**, **Randomize**, **Palette Variation**) and group **Hue /
Saturation / Brightness -/+** buttons. **Randomize Nebula** explores the
unlocked cloud, color and emission settings; a padlock protects a setting from
randomization and palette generation (group color buttons ignore locks). These
buttons need Object Mode so Undo restores the settings. Presets store the
resulting RGB colors, not the family/seed.

## Command line

```bash
blender -b --python-exit-code 1 -P art/tools/flat_background/flat_render.py -- \
  --preset art/tools/flat_background/illustrated-blue.json --stage final \
  --out /path/to/illustrated-blue-final --unity-project src/Asteroids3D --name illustrated-blue
```

`--stage draft|final` (default draft), `--width`/`--height` (defaults 1024 draft,
2048 final, square), `--samples` (default 8). Without `--preset` the defaults
are used. `--unity-project` and `--name` publish the result (below). The run
prints `FLATBG_MIGRATION=` and `FLATBG_PATH=`.

## Presets

Presets are `schema_version` 2 JSON: a `nebula` object with `variation`,
`scale`, `stretch`, `rotation`, `coverage`, `core_emission` and a four-color
`palette`. Unknown fields, invalid numbers and unsupported versions are rejected
before scene construction.

A `schema_version` 1 preset still loads: its `tiny_stars`, `anchor_brightness`
and `anchors` are dropped and a migration report names what was dropped and how
the remaining fields are interpreted. The report is printed as
`FLATBG_MIGRATION=`, shown as a warning by **Load Preset**, and stored in the
sidecar's `provenance.migration`; a version 2 preset reports nothing. Saving
always writes version 2.

## Illustrated blue (Environment_3)

`illustrated-blue.json` supplies the cool palette and subdued generated texture
used by `Environment_3`. Its Background material adds a Solid Base and reduces
Cloud Texture Strength; the main visible cloud banks are the locale's FarNebula
material. Cloud Bank Opacity enables shaded, partially opaque banks; zero keeps
the additive wisps treatment. Coverage, Direction and Stretch shape the banks.
Cloud Interior Glow and Cloud Glow Spread light the interiors and surrounding
haze independently of star occlusion; Cool/Violet Interior Light are HDR colours.
Compare with bloom disabled when tuning
the material, then restore bloom to judge the final glow.

That locale draws Background → DistantStars → FarNebula → StarField → CloseNebula.
Cloud banks obscure the distant stars, while sparse nearer stars remain in front.
Both star populations use the existing starfield shader; Small Star Bias keeps
large stars rare, and Four Point Star Share adds pointed accents. Tune these
materials together on the LocaleSky root, then compare during camera travel and
zoom. Background and sky-layer motion are deliberately slower than gameplay.
The Atmosphere child carries this locale's chromatic-aberration override so tiny
stars remain distinct; it leaves other locales and the shared bloom profile alone.

## Handoff contract

Set **Unity Project** to the folder containing `Assets` and `ProjectSettings`
(for this repository: `src/Asteroids3D`), then **Send Draft to Unity** or
**Send Final**.

`flat_unity.publish` owns the export layout. It refuses a sidecar whose `stage`
does not match the requested publish, stages both files under the project's
`Library/FlatBackgroundAuthoring`, then replaces the `.json` sidecar before the
`.exr` in `Assets/Visuals/Environment/Flat/Generated` as `<name>-draft` or
`<name>-final`. Unity `.meta` files are retained, so re-sending keeps texture and
material identities. A failed publish reports an error and leaves the local
render available to resend.

`FlatBackgroundImport` owns the Unity side: it imports the EXR (scene-linear,
BC6H, Repeat, mips), creates the matching `.mat` once, and on each sidecar
import writes the palette parents beside it (`<name>-final.FarNebula.mat`,
`….CloseNebula.mat`), Material Variants of the shared nebula materials with
palette-mapped colours. A locale's nebula materials vary these, so re-sending
refreshes inherited colours while Inspector overrides survive. Assign the
background material to a `Background` sky layer under the locale's `LocaleSky`
root; swap it in Play Mode to preview, or assign it in Edit Mode and save the
scene to apply.

## Tests

```bash
python -m unittest discover -s art/tools/flat_background/tests -v
blender -b --python-exit-code 1 -P art/tools/flat_background/tests/blender_flat_smoke.py -- --out /path/to/scratch
blender --factory-startup --python art/tools/flat_background/tests/blender_controls.py -- --out /path/to/scratch
```

The unit tests cover preset validation, v1 migration and publishing. The smoke
test renders a small draft of `illustrated-blue.json`, checks the EXR, the
sidecar and that no star-like peaks appear, confirms the open scene is restored,
and publishes into a temporary Unity-shaped project; it prints
`FLATBG_SMOKE_PASS`. The windowed controls regression writes `result.json`
covering locks, palette tools and Undo.

Unity integration tests: filter `Tests.EditMode.FlatBackground`
(category `Sectors`), through the repository's pooled Unity test runner.
