# Vanguard drawn-art study (Unity side)

Removed from main by slice 6b of #868 (issue #921). Source commit `517b6226`. Every file is
byte-identical to its source (same blob id), `.meta` files included, at its original path
under `src/Asteroids3D/`. The nested `.gitattributes` is `src/Asteroids3D/.gitattributes`, so
a checkout reproduces the source bytes (LFS files smudge, Unity YAML keeps LF).

| Original path | Contents |
| --- | --- |
| `Assets/Visuals/Studies/DrawnArt/` | 3 study scenes (`AsteroidField`, `HangarHero`, `SpaceHero`), 2 prefabs (`AsteroidContext`, `VanguardPreview`), 13 materials, 11 contour mesh assets, `Lighting/PodBloom.asset`, the study README |
| `Assets/Visuals/Ships/Vanguard/DrawnStudy/` | 4 backdrop plates and `ShadowedPlate.shader` (`Astronomical/Comparison/Shadowed Plate`) |
| `Assets/Scripts/Editor/Visuals/Studies/` | `ArtPreviewAuthoring.cs`, `ArtPreviewCapture.cs`, `Visuals.Studies.Editor.asmdef` |

The backdrop plates and `ShadowedPlate.shader` also stay on main as raw files, without
`.meta`, in `art/ships/vanguard/drawn-study/`. The Blender producers that exported the study meshes are in
`producers/drawn-study/` on this branch.

## Revive

From a checkout of this branch:

    cp -r studies/drawn-art/Assets/. <repo>/src/Asteroids3D/Assets/

Copy each `.meta` with its asset: it carries the GUID. The study points at production GUIDs,
so it revives only while those exist: Vanguard's `VanguardStructure.fbx`,
`VanguardBaseColor.png` and `Canopy`, `Engine glow` and `Structural ink` materials; the ten
`DrawnField` shapes (`Asteroid<N>.fbx`, `Asteroid<N>Drawing.fbx`, `Asteroid<N>Paint.mat`) and
`FieldGraphite.mat`; and the drawn shaders, now `Assets/Visuals/Shaders/DrawnSurface.shader`
and `DrawnContour.shader`, named `Astronomical/Drawn/Surface` and `Astronomical/Drawn/Contour`.
`ArtPreviewAuthoring.cs` finds those shaders by their old names (`Astronomical/Comparison/*`);
update the strings when reviving it.
