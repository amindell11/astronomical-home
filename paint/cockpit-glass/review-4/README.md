# Bulwark smooth cockpit reflections

Code commit: 101a5cdb54a4d9c372d0fcdc2b329967fce2a682. Follow-up to [PR #1088](https://github.com/amindell11/astronomical-home/pull/1088).

The earlier texture crop exposed staircase-shaped outlines in the original 1024×512 reflection map. Each half was 512 pixels wide, and Bulwark's horizontal crop stretched about 154 of those pixels across the glass. The steps also appear in the raw Unity frames, so preview compression was not the cause.

The broad reflection and fine glint now have editable Bézier outlines in `art/ships/bulwark/canopy/Bulwark glints.svg`. Supersampled rasterization produces the 2048×1024 packed map, and Unity imports it at that resolution. The strokes extend through the entire reflection field; Bulwark uses the full vertical sampling range to avoid bends where the earlier tight clamp repeated interior rows. Shadow and Ink are exactly a bilinear resample of the previous fixed channels. Geometry, UVs, palette and original hull paint masks are unchanged.

## Inspect the edges

This lossless comparison uses native pixels from matched 1280×1280 Unity captures. It is not enlarged.

![Before and after outlines](resolution-edge-comparison.png)

![Neutral cockpit](resolution-neutral.png)

![Banked cockpit](resolution-bank.png)

## Inspect the motion

[Full-resolution MP4](bulwark-hd-preview.mp4): 1280×1376 including the caption, with the native 1280×1280 render underneath. It lasts 18.4 seconds and pauses at six views. The first pass rolls from -55 to +55 degrees; the second pitches from -35 to +35 degrees.

The GIF is a smaller 768-pixel preview. Use the MP4 or lossless PNGs to judge edge quality.

![Motion preview](bulwark-hd-preview.gif)

These are controlled Unity asset renders without bloom; they do not establish gameplay flight approval.

## Verification

- Unity graphics run: 62/62 passed, including the fixed-highlight, ±65-degree pitch, intact/debris projection, reflection motion and damage/debris checks. The temporary capture assertion verifies the imported texture is 2048×1024.
- The source contour check fails on the prior texture and passes on the new one. On a normalized 512-pixel scale, the largest adjacent edge step falls from 2.736 to 0.0867 pixels; the longest stationary run falls from 48 rows to zero. See the two edge logs and `check_reflection_edges.py`.
- Source and runtime textures match. Shadow/Ink equal the stated bilinear resample, and the reflection drawing field has full coverage. See `resolution-proof.json`.
- Reflections-disabled captures have small resampling differences around the rim: mean absolute channel difference 0.0213/255. They are not pixel-identical. No fixed glint was added.
- Whole-ship differences greater than one channel value occupy 7,017 pixels, bounded by x575–704 and y239–519 inside the cockpit.
- Geometry-lock verification passes. Independent review of five matched views, whole-ship and reflections-disabled captures found no new highlight clipping or scope findings.

## Reproduction record

The saved capture test, Unity XML, geometry-lock report, contour check and fitting/encoding scripts preserve the local verification. `resolution-source-packed.png` is the exact prior texture used to fit the curves and resample the fixed channels. Scripts retain this session's Windows paths and expect the capture frames in the scratch directory. The temporary capture test was removed from Unity's Assets after the run.

`matched-before/` and `matched-after/` preserve frames 000, 020, 060, 100 and 140. Whole-ship and reflections-disabled PNGs are included separately. The full motion is preserved in the MP4; rerunning the capture creates the complete frame sequence.
