# Cockpit D — round 11: the console

## A — the sketch's desk

**Idea.** One low desk spans the pods, with a chamfered front rail and a raised central launch island. The island's rear shoulder grows into the faceted projector cowl, while the pod feet fold down into the same desk.

**Controls.** LAUNCH is on the centre line in a 23-degree raked island; the two ship keys occupy their own recessed well immediately to its left. The selector has a separate bezelled well immediately to the right, and four switches sit farther right under the weapon pod; the left outer ledge carries a plain inset access cover.

**Cost.** This stays closest to the picked sketch and leaves the clearest low silhouette, but spreads the switch row away from the ship controls. Its shallower desk is less enclosing than B or C.

## B — a centre spine

**Idea.** A single tapering tunnel descends from the projector cap into the launch nose, making the longitudinal centre form dominant. Two 31-degree raked control flanks and the descending pod ledges feed into its shoulders.

**Controls.** LAUNCH is at the viewer's end of the tunnel. The left flank holds previous/next ship keys over an inset lower service strip; the right flank holds the selector above a row of four switches, all inside one large recessed group.

**Cost.** This gives the controls the strongest face-on presentation and the clearest centre hierarchy. The tall flanks consume more of the lower window area and feel heavier and more divided than the sketch's desk.

## C — a wraparound control band

**Idea.** One folded, raked horseshoe runs from pod to pod, with its centre pulled forward toward the pilot and its wings falling outward. Behind its raised rear edge, the deck drops to the sill and the projector rises from a compact cowl.

**Controls.** LAUNCH occupies the forward centre of the band. Previous/next ship keys are grouped along its left inner segment, the selector occupies the right inner segment, and four switches continue outward along the right segment; the left outer segment has an inset access cover.

**Cost.** The band gives the foreground the most continuous enclosure and the clearest lateral sweep. Its forward centre uses more foreground depth and leaves less bottom-frame margin around LAUNCH than A or B.

## What is held fixed

Nothing outside the console changed. The builder starts with the round 10 scene, records signatures for 57 retained objects, and replaces only the console surfaces, pedestal cowl, launch housing, pod control ledges and their lower transitions, ship keys, selector and switches.

The two pod shells, full face carriers, all four screens and their chamfers, four weapon step keys, struts, stepped header, outer pillars, open sides, hologram and its clear-volume guide remain identical. Their geometry, transforms, material assignments and the fixed camera are checked against the generated round 10 baseline; the 57 signatures also match between A, B and C. The three-quarter camera is retained at the same round 10 transform and scale for all three inspection renders.

The projector lens remains centred at **(0, 0, 2.48)** with its original rim, radius and depth; its top is **2.4925**, below the hologram volume's **2.50** floor. All new pedestal geometry stays below that volume.

## Scale, surface treatment and verification

- Ship keys are **0.54 × 0.49**, versus round 10's **1.08 × 0.97**. Switch faces are **0.34 × 0.49**, versus **0.65 × 0.97**. The selector radius is **0.34**.
- Control groups have explicit faceted bezels, reveal walls and recessed floors. Structural deck polygons are cut away beneath these openings; the wells do not merely cover an intersecting surface.
- One inherited key light, flat colours, slightly different adjacent-plane values, no textures, no text objects and no modifiers. Plain inset covers divide the unused outer ledges without introducing additional controls.
- All requested console controls are wholly in the 1920 × 1080 frame. Camera visibility checks pass for the control centres and near-edge samples; all four screens pass camera and nine-position key-light visibility checks.
- Fixed and three-quarter views were visually reviewed. The first passes were revised to remove deck/well intersections, align C's bezels to its band, and increase C's forward sweep.
- Per-variant `D11A-checks.json`, `D11B-checks.json` and `D11C-checks.json` contain the retained-object signatures, screen sample counts, control bounds, margins and empty error lists.

## Files and rebuilding

Each variant has a 1920 × 1080 fixed-camera PNG, a 1920 × 1080 three-quarter PNG, and a `.blend` saved with the fixed game camera active. `D11-sheet.png` is a labelled **2400 × 1440**, 2 × 2 comparison; `D11-console-crops.png` stacks the exact bottom **45%** of R10, A, B and C at original pixel scale, with labels, at **1920 × 2136**.

`build_D11.py` is self-contained and uses local Blender geometry only. From this workspace:

```powershell
$env:BLENDER_USER_RESOURCES = Join-Path (Get-Location) 'out/.blender'
& 'C:/Program Files/Blender Foundation/Blender 5.1/blender.exe' -b --factory-startup --python out/build_D11.py -- A
& 'C:/Program Files/Blender Foundation/Blender 5.1/blender.exe' -b --factory-startup --python out/build_D11.py -- B
& 'C:/Program Files/Blender Foundation/Blender 5.1/blender.exe' -b --factory-startup --python out/build_D11.py -- C
python out/assemble_D11.py
```

Append `--preview` for the fixed view at 16 samples, or `--check` for verification without rendering. Full builds use 48 Cycles samples and render both requested views. All work was local to this folder; no network was used, and `round10/` and `refs/` were not edited.
