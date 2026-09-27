# Vanguard native rendering study

`VanguardStudy.blend` preserves the September 23 textured working scene and its
original editable source scene. Required images are packed. `source-manifest.json`
records the original file hash and image provenance; the primary-tree originals
remain untouched. This is separate from the committed production Vanguard export.
`VanguardStructure.blend` adds selected tapered panel seams; their editable paths
are in `structural-lines.json`. The original paint remains unchanged.

Run `export_study.py` with Blender in background mode to export the packed study
to Unity. An optional path after `--` selects a different source. Export reads the
`Vanguard - Texture MVP` scene and retains its paint UV as the exported UV channel.
Blender's evaluated triangles and corner normals are frozen into the export:
the source contains nonplanar panels whose alternative triangulations intersect
the blue disks. The original editable polygons remain in the baseline source.
The export manifest describes that run; it does not replace source provenance.

`VanguardStudyPlayModeTests` produces matched native Unity captures under
`results/vanguard-study` through the graphics-enabled test runner. The conventional
lighting baseline uses the same textured mesh as the drawn candidate. The separate
production captures use the committed Vanguard rig's mesh and materials, staged
with the hull top facing the canonical camera and normalized to the same length.
The context captures combine that ship with the ten drawn asteroid assets and
`NebulaBackground-v2.png`, an AI background plate with foreground objects removed.
The ship and asteroids are native Unity meshes; the backdrop is a static image.
`background-prompt.txt` records the built-in image-generation edit request.
The hero captures use clean hangar and green-planet plates derived from the two
user-supplied concept references; `hero-background-prompts.txt` records those edits.
The hangar is a flat shadow-receiving plate, not a modeled environment.

The rendering brief and visual decisions live on
[#685](https://github.com/amindell11/astronomical-home/issues/685#issuecomment-5853114008).
