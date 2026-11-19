# Cockpit D — round 12

C supplies the continuous wraparound band: its horseshoe path, forward centre, folded front edge, outward fall and low pod landings are retained. The two inner control wells now grow out of that skin through short, faceted shoulders. The outer sections stay low. The empty left outer bezel is gone; two small status inserts occupy that area.

B supplies the raised, viewer-facing control groups and the centre connection. The ship-key well and selector well rise locally from the band, while the four switches remain on its lower right section. A short tapering spine runs from the rear of the LAUNCH housing into the pedestal's lowest tier. It is most legible in the top view; from the fixed camera the launch housing and tower meet as one centre assembly.

The raised faces are **37° above horizontal**, compared with the band's approximately **27°**. Their centres lift **0.19 units along the band normal**; their surrounding folds spread **0.27 units** into the band. Dimensions below are in Blender units, measured in each face's local plane.

| Part | Width × face length | Placement and height |
|---|---:|---|
| Ship-key raised well | 1.93 × 1.12 | Left of LAUNCH, centre approximately (−4.033, −2.092, 1.387); highest rim corner Z 1.829 |
| Selector raised well | 1.55 × 1.24 | Right of LAUNCH, centre approximately (3.683, −2.135, 1.434); highest rim corner Z 1.884 |
| B's original flank, for comparison | 2.92 × 2.52 | D12 face lengths are 44% and 49% of B's; no tall combined knob-and-switch panel |
| LAUNCH face | 2.60 × 1.08 | C's centre-line location (0, −3.44, 1.50), at 27° |
| Previous / next ship keys | 0.54 × 0.49 each | Side by side in the left raised well, with 0.74 centre spacing |
| Selector knob | Radius 0.34; height 0.24 | Recessed collar inside the right raised well |
| Four switches | 0.34 × 0.49 each | Low right-hand well, 0.52 pitch; band station X 5.85 |

All control sizes are inherited from round 11 C. Both raised wells have a 0.16-wide faceted bezel and a floor recessed 0.075 below the face. Their vertical face extents, including the inherited outward bank, are 0.883 and 0.901 units. Their shoulders meet the band directly; they have no separate mounting blocks.

The pedestal restores round 10's exact seven perimeter loops forming three stepped, faceted risers, including the two horizontal ledges at Z 1.69 and 2.08 and all three dark front slots. The lens remains at **(0, 0, 2.48)**; its top is **2.4925**, below the unchanged hologram volume's **2.50** floor.

The console now uses the pod structure's actual material datablocks: charcoal blue `(0.040, 0.057, 0.078)`, nearby return blue `(0.037, 0.055, 0.078)`, and ink blue `(0.019, 0.029, 0.045)` for the lower apron. These are linear material RGB values. Adjacent working planes have only slight value differences. The original small control colours, orange launch face and blue screens are retained.

**Nothing outside the console changed.** All **57 retained object signatures match the original round 11 C builder**, including mesh geometry, transforms, material assignments, cameras and light. This covers both pod housings, carriers, four screens, weapon step keys, struts and feet, stepped header, outer pillars, open sides, hologram, lens and clearance guide. The inspection cameras also retain their inherited transforms and framing.

The fixed camera and all four inspection views were visually reviewed. The final saved-scene audit confirms all three pedestal slots and both strut feet are visible from the fixed camera. Every requested control is wholly in frame and unobscured; the smallest control-to-frame margin is **63.5 pixels**, at LAUNCH. Screen tests pass **1,362 camera samples** and **4,104 samples from nine positions across the single key light**, with zero blocked samples. No console geometry enters the hologram volume. There are no textures, text objects or modifiers. Labels exist only on the requested comparison sheets.

`D12-camera.png` and the four inspection renders are 1920 × 1080. `D12-compare.png` is 2880 × 592, ordered B / C / D12. `D12-console-crop.png` is 1920 × 1614, stacking the exact bottom 486 pixels (45%) of those three original camera renders, with separate label bars and no rescaling or retouching. `D12.blend` opens with the fixed game camera active and the original dark world restored.

`build_D12.py` is self-contained; the D12 design constants are at its top. Rebuild from this workspace:

```powershell
$env:BLENDER_USER_RESOURCES = Join-Path (Get-Location) 'out/.blender'
& 'C:/Program Files/Blender Foundation/Blender 5.1/blender.exe' -b --factory-startup --python out/build_D12.py
python out/assemble_D12.py
```

Append `-- --preview` for the fixed camera at 16 samples, or `-- --check` for geometry checks only. Full renders use 48 Cycles samples. `D12-checks.json` records the final validation; `verify_D11_baseline.py` and `validate_D12.py` reproduce the additional saved-scene comparison. All work was local to this folder, without network access. The round 10, round 11 and reference files were not edited.
