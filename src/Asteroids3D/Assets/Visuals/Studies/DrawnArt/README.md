# Drawn art preview scenes

Open a scene in `Scenes/` and use a **16:9 Game view** with **High Fidelity** quality.
These are opt-in art studies, excluded from the production build scene list.

| Scene | Use |
| --- | --- |
| HangarHero | Ship materials, silhouette, bloom and cast shadow against the hangar reference UI. |
| SpaceHero | The same hero pose against the planet reference UI. |
| AsteroidField | All ten drawn asteroids and the ship at gameplay distance against the nebula. |

The UI is baked into the background images. It is a composition reference, not an interactive interface.

## Reuse

Duplicate a scene before making a new study. Replace the prefab under `Subject — replace child asset`
to compare another ship with the same camera, pose and lighting. The child prefab uses the XY game
plane, with the ship's nose toward -Y and depth along Z; its visible hull is centered and six world
units long. Keep that convention when comparing equivalent framing. Use layer 30 on new renderers
and particles: the preview camera and key light intentionally render that layer.

`Prefabs/VanguardPreview.prefab` includes the approved native hull, structural lines, integrated
panels, wear and saved outline meshes. `Prefabs/AsteroidContext.prefab` holds the ten field assets.
Materials and the bloom profile are shared assets: duplicate them before changing only one study.
The hero outline is 7.5 pixels; AsteroidField overrides the ship's outline with the 3.25-pixel
gameplay material. The saved emission mask keeps the blue engine pods emissive across scene reloads.

Use the key light for lighting experiments and the Subject parent for pose experiments. The hero
camera half-height is 3.5; the field half-height is 26. To inspect the field closer, set it to 14
and resize the background quad to (49.7778, 28, 1), using the hero outline for the ship. Background
quads are composed for 16:9. The hangar quad receives a real shadow; space quads are unlit.

For hangar shadow tuning, select `Fixed key` and rotate its Transform. The hangar light starts
at (5, -8, 0) degrees; space retains (25, -35, 0). Bringing X/Y rotation closer to zero shortens
the projection onto the flat background plate. Moving a directional light does not change it.
The hangar architecture is painted into that plate, so it does not provide a modeled 3D floor.

The canopy uses `Drawn Canopy`: a tapered blue-gray reflection and an interrupted glint,
projected in the canopy mesh's local XY coordinates. Its material exposes reflection strength
and both colors. The reflection shifts with the light/view direction, narrows at grazing angles,
and fades in shadow. A subtle blue edge response suggests glass without emission. This is an
art-directed approximation, not a reflection of scene objects. Other ship surfaces keep the
`Drawn Surface` shader.

## Reproduce

`Astronomical > Art previews > Capture saved study scenes` reopens the three saved scenes,
checks persistent mesh/material references and captures them at 1600x900. Images and a verification
receipt land in the repository's `results/art-previews/` folder. It restores the open scene setup
and quality level afterward.

`Astronomical > Art previews > Rebuild approved study scenes` resets these three scenes, shared
materials, outline meshes and prefabs to the approved study recipe. It preserves asset GUIDs.
Keep custom studies in duplicates so a rebuild does not overwrite their edits.

The recipe is `Visuals.Studies.ArtPreviewAuthoring`; the batch entry point
`Visuals.Studies.ArtPreviewCapture.BuildAndCapture` builds, reopens, validates and renders the scenes,
then exits Unity. Run batch authoring through the repository's Unity access coordinator.
The source ship and background inputs remain in `Assets/Visuals/Ships/Vanguard/DrawnStudy/`;
asteroid inputs remain in `Assets/Visuals/Environment/Asteroids/DrawnField/`.
