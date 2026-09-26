# HDR space-skybox authoring

Author in Blender, preview through the game's bloom in Unity, then assign a final
sky to an environment scene. The existing Cycles generator remains the rendering
source for both the panel and command line. Blender 5.1+ is required; Cycles picks
an available GPU backend and otherwise uses the CPU.

## Install the Blender panel

Zip the `skybox` directory so the archive contains `skybox/__init__.py` and its
sibling Python/JSON files. From the repository root, for example:

```powershell
Compress-Archive -Path art/tools/skybox -DestinationPath skybox-addon.zip -Force
```

In Blender: **Edit > Preferences > Add-ons > menu > Install from Disk**, select
that ZIP, then enable **HDR Space Skybox**. In the 3D View, press **N**, choose the
**Skybox** tab, and click **Start with Nebula Glow**. See Blender's
[add-on installation guide](https://docs.blender.org/manual/en/4.4/editors/preferences/addons.html).

Save your `.blend` to keep panel settings, output paths and the selected Unity
project. **Save Preset** writes a portable JSON containing all sky appearance
settings; **Load Preset** restores it. Output/project paths are local workspace
choices and are not part of portable presets. **Load Nebula Glow** restores the
approved appearance without changing those paths.

## Make a sky

1. Start from Nebula Glow and change a few controls.
2. Click **Render Draft**. Choose 512 for rough shape checks, 1K for ordinary
   iteration, or 2K to examine stars more closely. Drafts use 32 samples.
3. Click **View Last Render** to view the persistent result. The adjacent `_preview.png` is AgX-tonemapped
   for shape/color inspection; it does not reproduce Unity bloom.
4. Save a named JSON preset when you like the result.
5. Use **Export 8K HDR** for the final 8192×4096, 64-sample render.

Rendering uses a temporary scene and restores the open scene afterward. Cancel
from Blender's render view with Esc. The sky name uses letters, numbers, hyphens
and underscores. Files default to `Pictures/Skyboxes`; choose any writable output
folder in the panel. Draft and final basenames end in `-draft` and `-8k`, so a new
draft does not replace the final.

| Control | Effect |
|---|---|
| Cloud Variation | 0 keeps the original cloud field. Other integers choose repeatable 3D noise offsets. |
| Cloud Scale | Higher values give smaller, more numerous cloud features. |
| Stretch / Rotation | Shape and orient the nebula independently of the stars. |
| Cloud Coverage | More dense gas versus more empty space. |
| Four palette colors | Nebula emission colors; these also affect sky-derived lighting. |
| Tiny Stars | Brightness of both small procedural-star layers. Zero removes them. |
| Focal Stars | Overall brightness of the five large stars. |
| Core Emission | Selective emission gain, strongest in bright dense gas. |
| Selected focal star | Horizontal/vertical angles relative to the panorama, size, color and brightness. |

The broad glow around focal stars comes from **Unity bloom**, not baked blur.
Keep focal stars enabled to retain that glow. The background wash and noise-detail
parameters remain the established defaults. This version edits the existing five
focal stars; it does not paint clouds or add/remove stars.

## Choose and vary a color scheme

Under **Nebula Colors**, choose a **Color Family** and click **Use Scheme** for its
exact colors: Nebula Glow, Azure & Ice, Teal & Amber, Ember & Violet, or Rose & Gold.
**Randomize** advances the **Palette Seed** and generates a related variation.
**Palette Variation** controls how far its hues/saturation may drift: 0 gives the
exact scheme, while 1 allows the widest variation. A shared hue shift keeps the
family related; small per-swatch changes add variety. Each swatch retains its peak
brightness, preserving the dark base and brighter cloud/accent roles.

To revisit a variation, choose the same family, variation amount and seed, then
click **Apply Seed**. The four swatches remain directly editable. **Refresh Draft**
shows the changed colors in the sky. These actions change nebula colors only.

**Save Preset** stores the exact resulting RGB colors, so loading it reproduces the
sky without needing the generator's family/seed controls. Those controls are saved
with the `.blend` as local authoring state; selecting a family alone does not
replace the current colors until you press one of the palette buttons.

## Look around while editing

Click **Refresh Draft** in the **3D Preview** section to render the selected draft
size and display the completed HDR around you in the 3D viewport with a wide 20 mm
view. The sky stays fixed in world space. Drag the middle
mouse button to look around. Adjust the same sky sliders, then click **Refresh
Draft** again; it keeps your viewing direction. Start with 512 for a quick editing
loop, then switch to 1K or 2K for detail. Slider changes take effect after refresh;
this is a preview of the latest render, not a continuously rendering volume.

**Look Around Last Render** opens an existing result without rendering again.
**End Preview** restores your viewport shading, object visibility, lens and view.
Saving/loading a `.blend` or disabling the add-on also ends the preview. It changes
only viewport display settings; your scene's objects, materials and world stay
intact. **View Last Render** opens the saved 2D preview, which remains available
after the temporary render scene is cleaned up.

512 drafts took about 1.5�3 seconds of rendering on the development machine;
startup, image loading, hardware and higher resolutions affect the total wait.
Blender's preview is for composition/color; evaluate the game's bloom in Unity.

To update from the first ZIP, finish any render, remove the old **HDR Space
Skybox** add-on in Preferences, restart Blender, then install the new ZIP and enable it. Version 1.3.0 adds locked randomization and group color adjustments; it also includes
the persistent render and corrected world-space viewport previews.

Cloud Coverage uses a relative **-100 to +100** scale: **0** keeps the original
coverage, negative values thin the clouds, and positive values fill more sky.
Each one-unit step changes the internal threshold by 0.002; decimal entry allows
finer tuning. Existing JSON presets keep their original coverage units.

## Explore with locks and group colors

**Randomize Nebula** explores cloud shape, core emission and colors within the
selected Color Family and Palette Variation. **Randomize Stars** separately
changes star brightness and the appearance/placement of all five focal stars.
Click the padlock beside a setting to protect it. Stretch/rotation locks cover
all three axes; focal-star locks belong to the selected star. Render size,
output paths and Unity settings are never randomized.

**Hue -/+** rotates all four colors together by 10 degrees; **Saturation -/+**
changes saturation by 5 percentage points; **Brightness -/+** divides/multiplies
color brightness by 1.1, capped at 1. Group color buttons ignore locks.
Locked colors resist Use Scheme, Apply Seed and palette Randomize. Manual edits and explicit preset loads can
still change locked values. Locks live in your `.blend`; portable JSON presets
store the resulting appearance. The new group/randomize buttons require Object
Mode so Blender's Undo restores scene settings reliably; the panel offers a
mode-switch button when needed. Refresh Draft to see changes in the sky.

## Send to Unity and compare

The target Unity project must contain this PR's editor scripts.

1. In Blender, set **Unity Project** to the folder containing `Assets` and
   `ProjectSettings` (for this repository: `src/Asteroids3D`).
2. Click **Send Draft to Unity**. This renders a fresh draft, saves its local
   outputs, then publishes the completed HDR and preset/provenance files into
   `Assets/Visuals/Environment/Sky/Generated` in that project.
3. Return to Unity. With Auto Refresh enabled Unity imports on focus; otherwise
   open **Tools > Skybox Preview** and click **Refresh Imports**.
4. Use **Choose Imported Sky** or the Candidate field. The companion creates a
   `Skybox/Panoramic` material and configures the scene-linear HDR texture with
   an 8K size limit, HDR-capable automatic compression, mipmaps and panorama wrapping.
5. Enter Play Mode in the environment you want to compare. Click **Preview
   Candidate**, then switch **Original / Candidate**. Keep the camera, exposure,
   bloom and lighting fixed, with **High Fidelity** quality. Check the ship as
   well as the background. Allow lighting a few frames to refresh.
6. Re-export the same sky name while iterating. Its texture/material identities
   remain stable, and an active candidate preview refreshes sky-derived ambient
   lighting when the new image imports.
7. **End Preview and Restore**, leaving Play Mode, closing the window, changing
   the active scene, or a script reload restores the original sky.

The window reports the current quality and default reflection texture. It does
not retune bloom/exposure or promise reflection equivalence. If the reflection is
`UnityBlackCube`, that comparison cannot establish sky-reflection behavior. Use
the live Game View for judgment; the CLI screenshot HDR-clipping issue is separate.
The approved HDR does not remove any independent in-game starfield effects.

## Keep a final sky in the game

In Blender, click **Send Final 8K**. In Unity, choose that `-8k` material, leave
Play Mode, select the desired **Environment Scene**, and click **Apply Final to
Environment (Undo)**. The scene is opened additively if needed and marked dirty;
**save that scene normally** to retain the assignment. Undo restores its previous
sky. Drafts cannot be applied through this button.

Each environment has its own skybox. The game's existing locale selection uses
that scene's lighting when entering the corresponding sector. Applying to one
environment does not replace the other environments or their materials.

## Native flat background (opt-in)

Tick **Native Flat Background** to render starless clouds that repeat in both
axes (a 4D-torus noise field) instead of a spherical panorama. Star, focal-star
and 3D-preview controls hide; stars belong to Unity's starfield. Drafts use
**Draft Width**; **Final Size** picks 1K/2K/4K (2K default). Each render writes
`<name>-draft|final.exr`, `_preview.png` and a `.json` sidecar carrying the four
palette roles (base/primary/secondary/accent), provenance and the migration
report for old spherical presets (also printed as `FLATBG_MIGRATION=`).

**Send Draft/Final to Unity** publishes the sidecar, then the EXR, into
`Assets/Visuals/Environment/Flat/Generated` through the same `skybox_unity.publish`
as skies. Unity imports the EXR natively (scene-linear, BC6H, Repeat, mips) and
creates a matching `.mat` once; repeat distance (default 10,000 world units) and
view height in tiles (default 0.1) live on that material. Assign it to the
locale scene's `EnvironmentAuthoring` **Background** field: swap it in Play Mode
to preview (Unity reverts on exit), or assign it in Edit Mode and save the scene
to apply (native Undo).

```bash
blender -b --python-exit-code 1 -P art/tools/skybox/skybox_flat.py --   --preset art/tools/skybox/nebula-glow.json --stage final --out /path/to/clouds-final   --unity-project src/Asteroids3D --name nebula-glow-flat
```

## Reproduce from the command line

The shipped `nebulaCustom0.hdr` used the generator's legacy defaults. The approved
`nebulaGlow0.hdr` is represented by `nebula-glow.json`: 8K, 64 samples, legacy seed
7319, tiny stars 0, focal brightness 1 and core emission 1.5. Both shipped assets
are retained as references.

```bash
blender -b --python-exit-code 1 -P art/tools/skybox/skybox_merged.py -- \
  --preset art/tools/skybox/nebula-glow.json \
  --width 8192 --height 4096 --samples 64 --format HDR --out /path/to/my-sky

# Legacy invocation remains supported; no preset means the original defaults.
blender -b --python-exit-code 1 -P art/tools/skybox/skybox_merged.py -- \
  --star-brightness 0 --anchor-brightness 1 --nebula-core-emission 1.5 \
  --format HDR --out /path/to/my-sky
```

CLI defaults: 2048×1024, 32 samples, EXR. `--width` must be even; optional `--height`
must be half the width. `--format EXR` writes 32-bit float EXR; `HDR` writes Radiance
RGBE. `--seed`, `--star-brightness`, `--anchor-brightness` and
`--nebula-core-emission` override the corresponding preset values.

**Legacy `--seed` is not a whole-sky variation control:** only its fractional part
changes distortion in one tiny-star layer. It has no visible effect with tiny
stars disabled. Use the preset's nebula `variation` instead.

Each render writes the HDR/EXR, `_preview.png`, `_report.txt`, `_preset.json` and
`_render.json`. The last file records Blender version, generator-source hashes,
resolution, samples, exact preset and measured HDR radiance. Match that Blender
version and generator revision for reproduction; normal GPU sampling variation
can prevent byte-identical renders. Schema version 1 presets reject unknown
fields, invalid numbers and unsupported versions before scene construction.

## Handoff contract and checks

`skybox_unity.publish` owns the export layout. It stages all files under the target
project's `Library/SkyboxAuthoring`, then replaces JSON sidecars and atomically
publishes `<name>-draft.hdr` or `<name>-8k.hdr` in the Generated folder. Unity `.meta`
files are retained. `SkyboxImport` owns material creation and the matching material
lookup; it only processes those HDR suffixes under that folder. Failed publishing
reports an error while leaving the local render available to resend.

```bash
python -m unittest discover -s art/tools/skybox/tests -v
blender -b --python-exit-code 1 -P art/tools/skybox/tests/blender_smoke.py -- \
  --out results/skybox-authoring/variants
```

The native preview regression runs in a windowed Blender and writes a JSON
verdict plus a viewport screenshot:

```bash
blender --factory-startup --python art/tools/skybox/tests/blender_preview.py -- \
  --out results/skybox-authoring/preview-regression
```

The pixel-based look-around regression accepts a nonuniform rendered sky and
checks visible movement in Object and Edit Mode:

```bash
blender --factory-startup --python art/tools/skybox/tests/blender_viewport_rotation.py -- \
  --hdr /path/to/my-sky-draft.hdr --out results/skybox-authoring/rotation
```

Unity integration tests: filter `Tests.EditMode.Skyboxes`
(category `Sectors`), through the repository's pooled Unity test runner.
