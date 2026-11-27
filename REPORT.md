# Region render report

Completed all 10 requested PNGs in `out/`: six 1024×1024 views, three concept/render/swatch sheets, and `all-regions-top.png` (3072×1024). Individual ships fill approximately 85% of the frame's longest dimension. The combined top views use one orthographic scale, **3.0005188549266144**, without resizing ships independently. Three-quarter cameras face the nose from +X,+Y at 20° elevation.

Command run from this folder:

```powershell
python render_regions.py
```

The script launches Blender separately for each source, first to measure evaluated bounds, then to render. Every exact subprocess argument is recorded in `out/run-manifest.json`. Its invocation format is:

```text
"C:/Program Files/Blender Foundation/Blender 5.1/blender.exe" -b --factory-startup --disable-autoexec <source.blend> --python-exit-code 1 -P <this-folder>/render_regions.py -- --ship <minion|rival|elite> --common-scale 3.0005188549266144
```

The measurement pass replaces `--common-scale ...` with `--inspect`.

## Elite accent

**`Main wing joint beds` / `Nightshade Plum`, slot 0, all 65 existing source faces → #e4ffa3**, with live modifiers retained. Top-view ray probes at (330,420) and (330,520) identify this part/material on the broad inner strips, matching the reference. Evidence: `out/elite-face-probes.json`.

`Field array blade` Glass occupies the outer strips; `Rear swept wing pair` Glass occupies the rear strips. Both remain #0b081b, as does all canopy glazing. No geometry was cut or added. The four parts without PaintUV render normally.

## Colour verification

Workbench FLAT/MATERIAL, Standard/sRGB, exposure 0, gamma 1; no lights, shadows, cavity, outlines, specular highlights, compositor or dithering. Material and world colours were converted from sRGB to linear.

All **48 sampled flat interiors across nine rendered views match exactly: maximum channel error 0**, within the required ±2. Samples require a flat 5×5 neighbourhood; antialiased boundaries naturally blend. Full coordinates and values: `out/pixel-checks.json`. Example elite top samples:

| Region | Pixel (x,y) | RGB |
|---|---|---|
| Hull | 542,442 | 97,74,135 |
| Second panels | 592,483 | 63,41,100 |
| Edge | 804,722 | 172,245,73 |
| Glass/recess | 513,270 | 11,8,27 |
| Elite accent | 339,448 | 228,255,163 |
| Background | 564,585 | 122,122,122 |

## Concept differences and source integrity

- The elite reference also makes the `Field array forward emitter`, `Field array aft emitter`, and `Rebuilt main wing blades` tips pale. Their Plum faces retain the prescribed #acf549; the pale exception is limited to the requested inner strips.
- Minion/rival `Rear swept wing pair` Glass strips remain dark, with green Edge borders. No additional unambiguous bright-strip mismatch was identified. Painted purple highlights on these strips and reflections on `Upper canopy glazing` are absent from the flat region renders. Painted lighting and panel highlights do not become new material regions.
- Rendered 32 minion, 43 rival and 54 elite visual meshes, restricted to the requested role collections and excluding `ignore`. Live modifiers were evaluated in temporary scenes.
- Blender 5.1.2 warned that the sources were written by a newer binary (503.31); all render processes completed successfully. All three source SHA-256 hashes are unchanged (manifest). No `.blend` was saved, and all task outputs are inside this folder.
