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
| `experiments/2026-09-04-ai-livery/` | Baseline and AI review variants, plus their external paint files. |
| `experiments/2026-07-26-pre-livery/` | Original pre-livery checkpoint. |
| `experiments/packed-textures/` | Exact packed image snapshots with no matching external file; hash names distinguish their contents. |

The SVG and active PNG have different edit dates. Both were preserved; the
PNG was not regenerated from the SVG during organization.

Use relative image paths. Save edits to the external livery PNG; when working
on an experiment with packed images, deliberately update or unpack the packed
copy so it does not silently override the edited file. Packed snapshots in the
experiments are retained for historical comparison.

The Unity export remains at
`src/Asteroids3D/Assets/Visuals/Ships/Vanguard/Vanguard.fbx` from the repository
root. This reorganization did not update Unity assets or their `.meta` files.

## Recovery and versioning

All 27 original files were hash-verified before moving. An exact pre-move ZIP,
the source hashes, and the move manifest are retained locally at
`scratch/art-reorganization-20260909-225001/` from the repository root.
That scratch folder is ignored by Git and is not a remote backup.

The `.blend1` recovery files retain their original bytes and may contain old
paths. The seven `.blend` projects have repaired relative image paths.

No tracking policy was changed during the move. Binary assets still use the
existing LFS rules; SVGs and documentation use normal Git. Commit working
source and its required textures together. Do not bulk-add experimental
checkpoints merely to clear the untracked-file list.
