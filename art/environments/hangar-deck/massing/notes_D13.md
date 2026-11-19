# Cockpit D — round 13

All four console fixes are implemented. The fixed camera, the three-quarter view and the orthographic views were reviewed. Geometry starts from `round12/build_D12.py`; all work and rendering were local, with no network access.

**1. The canopy struts now plant into the band.** Each unchanged strut enters a chamfered socket shoulder at X approximately ±3.836. The socket cap is 0.88 × 1.12 units, centred at Y 2.43, Z 1.32. The original strut ends remain at (±3.836, 2.65, 1.20), penetrating the shoulder by 0.12 units. Neither the strut line nor its 0.38 × 0.30 section changed; the original short foot returns are retained inside the new structure.

Each shoulder widens to 1.90 units and shifts 0.30 outward as its facets descend into the existing band and toward the pod heel. Its footprint runs from Y −0.35 to 3.14, with a short closed return behind the band's sill. The two raised caps are visible above the control panels, and the sides remain open.

The final **camera PNG contains zero sky pixels directly below either strut's lower end**:

| Camera region, zero-based pixels | Pixels checked | D12 sky pixels | D13 sky pixels |
|---|---:|---:|---:|
| Left, beneath original foot: X 617–658, Y 838–861 | 1,008 | 192 | **0** |
| Right, beneath original foot: X 1262–1303, Y 838–861 | 1,008 | 206 | **0** |
| Left, immediately below visible socket contact: X 620–653, Y 803–818 | 544 | 11 | **0** |
| Right, immediately below visible socket contact: X 1267–1300, Y 803–818 | 544 | 21 | **0** |

The PNG check compares every pixel in those rectangles with the rendered sky colour, RGB (9, 23, 40), allowing ±2 per channel. An independent camera-ray check confirms that **all 544 pixels under each visible contact hit the shoulder itself**. Thus the landing is real structure, not a gap concealed by a control panel. Evidence: `D13-pixel-checks.json` and `D13-audit.json`.

**2. The two flank panels are taller and more steeply raked.** They now have identical mirrored outlines, at matching band stations X ±3.60. Their folds grow directly from the band, spreading 0.27 units. Both faces move 0.16 units down the band toward the viewer to keep the pod heels and strut landings clear. The continuous front apron is retained beneath the recessed wells.

| Measurement, Blender units unless noted | D12 ship keys | D12 knob | D13, both panels |
|---|---:|---:|---:|
| Width × face length | 1.93 × 1.12 | 1.55 × 1.24 | **1.85 × 1.60** |
| Vertical face extent, including outward bank | 0.883 | 0.901 | **1.316** |
| Vertical height relative to D12 | — | — | **1.49× left / 1.46× right** |
| Rake above horizontal | 37° | 37° | **45°** |
| Centre lift along band normal | 0.19 | 0.19 | 0.30 |
| Highest outer rim Z | 1.829 | 1.884 | 2.122 |

Their 1.60 face length sits between D12's 1.12/1.24 and round 11 B's 2.52. Width is unified for the mirrored outline and clearances. The ship keys and knob retain their individual sizes. All three pedestal risers and slots remain visible; no console geometry enters the unchanged hologram volume, whose floor is Z 2.50.

**3. LAUNCH is smaller.** The button and housing are reduced by exactly 20% in both face dimensions:

| Part | D12 | D13 |
|---|---:|---:|
| Orange button | 2.60 × 1.08 | **2.08 × 0.864** |
| Housing | 3.15 × 1.56 | **2.52 × 1.248** |

LAUNCH remains the largest individual control, centred at (0, −3.44, 1.50), with its original 27° rake. The spine's front station is fitted to the smaller housing so their join stays closed. The remainder of the spine and the exact three-step pedestal are retained.

**4. The left end balances the right.** The two tiny lights are replaced by four switches matching the right bank: 0.34 × 0.49 controls at 0.52 pitch, in a 2.63 × 1.20 recessed bezel. The banks occupy mirrored stations at X ±5.85. Their bezel meshes are exact reflections; the right bank's dimensions and location are unchanged.

**Preserved and checked.** Nothing outside the console changed. Comparing against the actual round 12 generator gives **57/57 identical retained object signatures**, covering the pods and screens, weapon step keys, struts and original feet, header, side pillars, open sides, hologram, clearance guide, lens, cameras and key light. Pedestal geometry also matches separately. The horseshoe path and band cross-section retain their forward and outward fall. The console continues to use the pods' dark material datablocks, with flat colours, one key light, no textures, no text objects and no modifiers.

The camera audit found zero newly hidden samples across 19,060 pod surface samples, 5,670 weapon-key samples and 9,638 pedestal samples. All 12 console controls are in frame and unobscured. All four screens pass 1,362 camera-visibility samples and 4,104 light-visibility samples with zero blockers. The continuous apron passes another 1,911 samples with no new sky gaps. Both pairs of bezel outlines pass exact mirror checks. See `D13-checks.json` and `D13-audit.json`.

**Files and rebuild.** The five renders are 1920 × 1080. `D13-compare.png` is 2880 × 592, ordered picked sketch / D12 / D13. `D13-console-crop.png` is 1920 × 1068: the original bottom 486 pixels (45%) of D12 and D13, stacked without rescaling, with separate label bars. Labels occur only on these comparison sheets. `D13.blend` opens with the fixed camera active, the dark background restored, and 48 render samples.

Main dimensions and angles are named constants at the top of `build_D13.py`. From the workspace root:

```powershell
$env:BLENDER_USER_RESOURCES = Join-Path (Get-Location) 'out/.blender'
& 'C:/Program Files/Blender Foundation/Blender 5.1/blender.exe' -b --factory-startup --python out/build_D13.py
& 'C:/Program Files/Blender Foundation/Blender 5.1/blender.exe' -b --factory-startup --python out/validate_D13.py
python out/assemble_D13.py
```

Use `-- --preview` for the fixed camera at 16 samples, or `-- --check` for the builder's geometry, visibility and lighting checks without rendering. The audit rebuilds both versions in memory and writes only into `out/`; `round12/` and `refs/` are untouched.
