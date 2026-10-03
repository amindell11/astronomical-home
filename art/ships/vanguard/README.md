# Vanguard

Open `vanguard.blend` to continue modeling. This is the September 9 working
copy formerly named `starship_scratch_model_3.blend`, not an AI review variant.

| Location | Purpose |
| --- | --- |
| `textures/livery.svg` | Editable Inkscape artwork, formerly the master UV SVG. |
| `textures/livery_mask.png` | Active paint mask, last edited September 7 before reorganization. |
| `reference/` | Top and side images extracted from the packed references. |
| `textures/stamp_atlas.png` | Existing stamp texture extracted from the blend. |
| `textures/donor_albedo.png` | Existing imported model texture, copied from Unity's Shared ship assets to repair the missing legacy path. |
| `guides/` | Historical UV outlines and previews. Check against the working model before reusing them. |
| `drawn-study/` | The painted production source (`VanguardPainted.blend`) and its study scenes; see its README. |

The SVG and active PNG have different edit dates. Both were preserved; the
PNG was not regenerated from the SVG during organization.

Use relative image paths. Save edits to the external livery PNG.

`src/Asteroids3D/Assets/Visuals/Ships/Vanguard/Vanguard.fbx` is the July export
of this model and is referenced only by the legacy
`Ship_1_Vanguard_VisualRig.prefab`. The Vanguard the game shows comes from
`drawn-study/VanguardPainted.blend`, as the saved meshes under
`Assets/Visuals/Ships/Vanguard/DrawnStudy/Meshes/`.

Dated livery and texture experiments, with their `.blend1` backups, are on the
`evidence/vanguard` branch under `paint/experiments/`, not on main.

Binary assets use the LFS rules in `art/.gitattributes`; SVGs and documentation
use normal Git. Commit working source and its required textures together.
