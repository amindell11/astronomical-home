# Bulwark — fitted separate parts

The owner wants the kitbash pieces retained and individually refined, with cleaner connections and light shape detail. The prior welded-shell interpretation was rejected. This revision starts from the original kitbash at 6695210794adb7e8e1f78ddfd92a84af3fa3f607.

[Editable Blender file](Bulwark-fitted-parts.blend) · [Before/after review](index.html)

## Parts remain independent

All 14 visible mesh objects remain separate, plus both hidden alternatives. Original transforms, parents, and Mirror/Subdivision modifiers remain live. Small chamfers are additional live Bevel modifiers. No join, union, remesh, or modifier application was used.

| Object | Interpretation |
| --- | --- |
| `01 Ram nose` | Reinforced ram |
| `02 Upper hull.001` | Main deck |
| `02 Upper hull` | Aft bridge |
| `03 Lower hull` | Armored keel |
| `04 Tail booms` | Engine shoulders |
| `05 Tail tips` | Engine shrouds |
| `05 Tail tips.002` | Upper shoulder guards |
| `05 Tail tips.003` | Lower armor skids |
| `09 Side armor` | Side armor belt |
| `09 Side armor.001` | Dorsal spine |
| `09 Side armor.002` | Engine root collars |
| `06 Cockpit dome` | Teardrop canopy |
| `07 Side insets` | Flank modules |
| `08 Nested engines` | Nested engine nozzles |

The four reused armor additions also have editing collections named Upper shoulder guards, Lower armor skids, Dorsal spine, and Engine root collars.

## Fitting and shape refinement

- Upper guards and lower skids receive closed ends and planar inner mounting faces, replacing the reused nozzle cavities. Shallow inset faces establish their armor borders.
- Side armor ends at the guards instead of projecting through them.
- The raised spine and engine collars have trimmed seating faces.
- Ram, deck, keel, bridge, and boom interfaces receive distinct mating edges and narrow seams. Buried keel geometry is lowered beneath the deck; the outer bottom profile remains.
- Round flank modules retain their outer contour and receive a planar recessed center with a protective bezel.
- Cockpit, engine nozzles, hidden alternatives, and editing parent transforms are unchanged.

The exploded view is a review render only; the editable model stays assembled. Gray materials remain.

## Checks and limits

- Source SHA256: 9a3ccb8dfcd23f6a628826bf51336ea526ac8fed67169c46c280fb5257df948e (unchanged).
- Separate candidate SHA256: 03d33653762550073eb62005af20ff3953487b9affb81b6373ac0730a5620915.
- Original mesh object count and names retained. No Boolean modifiers.
- All structural pieces evaluate without boundary or nonmanifold edges; the untouched canopy retains its open underside.
- Source checks: 58 original findings, 51 remaining, no new rule/part findings. Remaining findings concern inherited scales, hierarchy, mirror planes, and hidden alternate geometry; no transforms or owner modifiers were applied to silence them.
- Studio, underside, exploded, orthographic, eight-view, and game-scale render checks completed. Rendering leaves source files unchanged.

This remains a shape review candidate. It has not replaced the owner's tracked source and has not advanced to flight check, UVs, paint, or Unity integration.
