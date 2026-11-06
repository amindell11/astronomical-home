# Vanguard hologram input

Append the `HologramVanguard` collection from `hologram-vanguard.blend`. Its root is
`HologramVanguardRoot`, centered locally with identity rotation and scale. The nose
points along +X. Set the root location to `(3.65, 0.4, 5.5)` and bank it 34 degrees
around X for the console camera. Natural bounds are 6.0 by 3.894 by 1.421 units.
The banked bounds are 6.0 by 3.228 by 2.558. Scale uniformly to preserve the ship.

The collection contains 15 editable mesh parts and separate editable contour
curves. The review camera and lights are in `Review only`, outside that collection.
The palette image is packed. The asset needs no external textures.

## Controls

In material `HOLO / muted red and grey panels`:

| Node | Input | Default | Effect |
| --- | --- | --- | --- |
| `Front opacity / back opacity` | `To Min` | 0.83 | Front surface opacity. Lower values expose more hidden contours. |
| `Front opacity / back opacity` | `To Max` | 0.04 | Back surface opacity. |
| `Scanline contrast` | `To Min`, `To Max` | 0.94, 1.0 | Opacity modulation. Closer values soften scanlines. |
| `Rim gain` | second input | 0.04 | Extra surface opacity near the rim. |
| `Muted painted projection` | `Emission Strength` | 0.65 | Surface brightness. |
| `Understated horizontal scanlines` | `Scale` | 42 | Scanline spacing in ship coordinates. |

Material `HOLO / bright contour`, node `Contour brightness`, uses emission strength
2.2. The contour curve objects end in `/ bright contours`; their bevel depth is
0.0048. Lower brightness or bevel depth together if the creases overwhelm the
silhouette. Canopy, cores, structural seams, and canopy frame have separate named
materials. The canopy remains more transparent than the hull.

## Provenance

`build_hologram_vanguard.py` reads the production source at
`D:/amind/git/astronomical-home/art/ships/vanguard/Vanguard.blend` and its
`layers/Region_*.png` paint masks. It copies the evaluated meshes from the source's
visual role collections, retains their UVs, converts the nose axis, and centers
and uniformly scales the ship. Source modifiers are evaluated into the copied
editable meshes. The production file is untouched.

The builder remaps the authored paint regions to muted red and grey, packs that
palette, adds translucent shaders, and generates crease and boundary curves from
the copied mesh edges. No ship silhouette was redrawn and no billboard is used.
`hologram-vanguard-manifest.json` records the source hash and mesh counts.
`hologram-vanguard-review.png` is the isolated material review.

Rebuild with Blender 5.1 using `--background --factory-startup --python-exit-code 1
--python build_hologram_vanguard.py`. The builder saves into its own folder.
