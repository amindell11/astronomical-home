# Round 2 region-plan report

Completed all **23 requested PNGs** in `out2/`: four sets of grey and transparent top/three-quarter views plus concept sheets, `all-top.png`, `game-scale.png`, and `round1-vs-round2.png`. The comparison includes both rival variants. Extended `render_regions.py`; round 1 remains available.

Command: `python render_regions.py --round 2`. Exact Blender subprocess arguments are in `out2/run-manifest.json`. Workbench FLAT/MATERIAL, Standard/sRGB, linear-converted colours, no shadows/cavity/outlines/specular/dither. Views are 1024 x 1024; transparent views use RGBA and `film_transparent`. The common top orthographic scale is **3.0005188549266144** (Blender stores 3.000518798828125). Individual framing remains approximately 85%; three-quarter cameras are front-right at 20 degrees elevation.

## Per-part plan

Only each listed part's original **Nightshade Midnight** faces are affected, across every populated Midnight slot. **H = hull #614a87; S = second violet #3f2964**. Counts are original source polygons before live Mirror/Solidify evaluation, in **minion/rival/elite** order; `-` means absent. Both rivals share this table, except rival-b's mark below.

| Part | Faces M/R/E | Before | After | Positional basis |
|---|---:|---|---|---|
| Aft engine collar | 8/8/8 | H | S | Central aft attachment |
| Aft spine armor | -/54/54 | H | S | Central spine / body cover |
| Cockpit forward latch | 5/5/5 | H | H | Cockpit / nose shell fitting |
| Cockpit inset secondary rail | 192/192/192 | H | H | Cockpit / nose shell fitting |
| Cockpit lower cradle plating | 58/58/58 | H | H | Cockpit / nose shell fitting |
| Cockpit lower retaining rail | 64/64/64 | H | H | Cockpit / nose shell fitting |
| Cockpit rear retaining collar | 8/8/8 | H | S | Rear / shoulder body attachment |
| Cockpit shoulder latch | 5/5/5 | H | S | Rear / shoulder body attachment |
| Cockpit upper cradle plating | 58/58/58 | H | H | Cockpit / nose shell fitting |
| Cockpit upper retaining rail | 64/64/64 | H | H | Cockpit / nose shell fitting |
| Dorsal bridge service cover | 5/5/5 | H | S | Central spine / body cover |
| Dorsal central blade | 82/82/82 | H | S | Central spine / body cover |
| Dorsal shield cap | 47/47/47 | H | S | Central spine / body cover |
| Dorsal shield inset | 4/4/4 | H | S | Central spine / body cover |
| Field array aft strut | -/-/6 | H | S | Inboard array support |
| Field array blade | -/-/370 | H | H | Outer wing shell |
| Field array forward strut | -/-/6 | H | S | Inboard array support |
| Lower small swept fins | -/122/122 | H | S | Inboard shoulder fins |
| Rear swept wing pair | 370/370/370 | H | H | Outer wing shell |
| Rebuilt main wing blades | 228/217/217 | H | H | Outer wing shell |
| Rebuilt pitched hull | 290/342/342 | H | S | Central body (includes nose rim) |
| Spine shoulder armor | 6/6/6 | H | S | Central spine / body cover |
| Tail mount service panel | -/5/5 | H | S | Inner tail attachment |
| Tail root mounting blocks | 28/28/28 | H | S | Inner tail attachment |
| Upper small swept fins.001 | -/114/114 | H | S | Inboard shoulder fins |
| Ventral central blade | 82/82/82 | H | S | Central spine / body cover |
| Ventral shield cap | 47/47/47 | H | S | Central spine / body cover |
| Ventral shield inset | 4/4/4 | H | S | Central spine / body cover |
| Wing outer armor | 16/16/16 | H | H | Outer wing shell |

Totals moved H -> S: **616 minion, 963 rival, 975 elite** source faces. Existing Violet faces stay S; Glass/Recess and all canopy glazing stay #0b081b. No geometry was cut, added, or baked.

## Green and rank marks

- **No Edge/Plum face sets darkened.** Inspection of all 1024 top views found edge bands, wing/fin tips, small exhaust ends, and short emitter/latch strips, rather than broad acid-green plates. Main wing Plum triangles and small-fin Plum ends are tips; the elite's two emitter bars are short strips. Sidewall edge bands look wider in the low three-quarter view but remain edge lines in the specified top-view assessment.
- Elite: `Main wing joint beds`, original `Nightshade Plum`, slot 0, **all 65 source faces -> #e4ffa3**, retaining the established pale inner-wing strips. No magenta. The four parts without PaintUV render normally.
- Rival-a has no added mark. Rival-b assigns **Dorsal central blade source polygons 37 and 44 (zero-based)**, both originally Midnight slot 1, to #acf549. Their live X mirror gives **four visible polygons forming two narrow ticks**, one on each dorsal shoulder. Selection in SymmetryOrigin space: transformed normal Z > 0.85, 0.115 < centroid X < 0.13, 0.05 < centroid Y < 0.15. The copied mesh receives only material-index changes. Polygon details are in `out2/rival-b-render.json`; the A/B top difference is **1,037 pixels**, bounded by x=[435,589), y=[462,519).

## Verification

**97 flat-interior samples across 20 rendered views: maximum RGB channel error 0** (required tolerance +/-2). Each sample has a flat 5 x 5 neighbourhood; transparent samples are fully opaque. `out2/pixel-checks.json` records every coordinate and value. Elite top examples:

| Region | Pixel x,y | RGB |
|---|---|---|
| Hull | 260,468 | 97,74,135 |
| Second violet | 299,412 | 63,41,100 |
| Acid green | 217,723 | 172,245,73 |
| Glass/recess | 514,270 | 11,8,27 |
| Pale lime | 339,448 | 228,255,163 |
| Grey | 558,585 | 122,122,122 |

All eight alpha views are RGBA, with transparent corners, opaque ship interiors, and intermediate alpha on antialiased edges. `game-scale.png` crops each alpha silhouette, reduces its longest side to **80 pixels with Lanczos**, composites over **#05040c**, and displays the same result at 1x and nearest-neighbour 4x. Both violets remain visible; narrow green borders and rival-b ticks soften at 1x, while tips and elite pale strips remain prominent. Pixel counts within +/-2 of each violet after reduction:

| Variant | Size | Hull pixels | Second-violet pixels |
|---|---|---:|---:|
| minion | 80 x 74 | 874 | 630 |
| rival-a | 76 x 80 | 513 | 188 |
| rival-b | 76 x 80 | 513 | 166 |
| elite | 68 x 80 | 218 | 151 |

## Limits and integrity

`Rebuilt pitched hull` combines the inner body and a small nose rim in one part. To respect whole-part Midnight reassignment, the rim follows the darker body; separate cockpit cradle/retaining-rail shells stay hull violet. A separately light rim would require a face-subset exception to that rule. Existing painted highlights/reflections and dark Glass strips remain as in round 1; no new painted subdivisions were invented.

All requested files were produced. Blender 5.1.2 repeated the source-version warning (files written by binary 503.31); every subprocess completed successfully. Rendered only the eligible 32/43/54 visual meshes, with live modifiers. Source SHA-256 hashes and all 28 existing files in `out/` are unchanged (manifest). No `.blend` save operation or bake was performed; all task files are in this folder.
