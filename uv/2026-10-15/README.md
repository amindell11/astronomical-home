# Vanguard paint UV layout — 2026-10-15

The owner approved the detail model and explicitly waived the flight check. `ship_lock` then recorded that approved geometry. This UV layout is ready for review; paint has not started.

![PaintUV guide](PaintUV-guide.png)

Negative-X charts occupy the lower half; positive-X charts occupy the upper half. All 15 visual parts have their own charts, including the canopy and the three new detail assemblies. The 300 projected charts have eight-pixel bounding-box padding at the 4096 texture size. Original atlas UVs remain available.

`ship_uv/ship_uv.json` passes with density ratio **1.0000017294450878**, below the allowed **2.0**, with no exemptions. `ship_lock/ship_lock.json` verifies the approved geometry unchanged. `ship_check/ship_check.json` also passes without findings or exemptions.

`source/` holds the source, ship.json and geometry lock. The scratch UV authoring producer is archived under `producers/`; it projects temporary meshes and transfers only UV coordinates, preserving the source geometry. No new shared ship tool was added.

The next stage needs the owner's layout approval, then a palette/region concept approved before the canonical painter generates masks. See [#1016](https://github.com/amindell11/astronomical-home/issues/1016).