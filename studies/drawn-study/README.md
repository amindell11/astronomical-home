# Drawn asteroid study (Unity side)

Removed from main by slice 6b of #868 (issue #921). Source commit `517b6226`. The ten study
files of `src/Asteroids3D/Assets/Visuals/Environment/Asteroids/DrawnStudy/` with their `.meta`
files (and the folder's), byte-identical (same blob ids), at their original paths: 3 FBX
(`AsteroidFractureStudy`, `AsteroidPaintStudy`, `AsteroidSurfaceDrawing`), 4 textures
(`AsteroidFracturePaint`, `AsteroidPaint`, `AsteroidScuffNormal`, `AsteroidStoneAlbedo`) and
3 materials. The nested `.gitattributes` is `src/Asteroids3D/.gitattributes`, so a checkout
reproduces the source bytes.

The folder's fifth texture, `AsteroidBrushAlbedo.png`, is production: it moved (same GUID)
to `Assets/Visuals/Environment/Asteroids/DrawnField/`. The raw FBX and textures also stay on
main, without `.meta`, in `art/asteroid-study/` beside their blends.

## Revive

From a checkout of this branch:

    cp -r studies/drawn-study/Assets/. <repo>/src/Asteroids3D/Assets/

Copy each `.meta` with its asset: it carries the GUID. The three materials use the drawn
surface shader by GUID, now `Assets/Visuals/Shaders/DrawnSurface.shader`
(`Astronomical/Drawn/Surface`); they revive only while it exists.
