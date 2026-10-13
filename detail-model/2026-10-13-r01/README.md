# Bulwark — separate kitbash refinement

The owner approved their hand-built silhouette and requested a separate version that interprets and cleans up the assembled pieces. The original remains at art/ships/bulwark/Bulwark.blend, checkpoint 6695210794adb7e8e1f78ddfd92a84af3fa3f607. It has not been replaced by this candidate.

## Editable file

[Bulwark-refined.blend](Bulwark-refined.blend)

Four editable mesh objects, each with a live Mirror:
- Hull: one connected, closed structural surface.
- Cockpit: the existing teardrop cage with live subdivision.
- Round flank modules.
- Protected twin engine nozzles.

The hull has eleven named vertex groups for selecting its functional regions in Edit Mode: Ram bulkhead, Lower hull, Main deck, Aft bridge, Engine shoulders, Engine shrouds, Flank armor, Dorsal spine, Engine root collars, Upper shoulder guards, Lower shoulder skids.

## Interpretation

- Reused tail shells on the flanks become solid upper shoulder guards and lower armor skids. Their old nozzle cavities are closed.
- Raised central strips form the dorsal spine.
- The raised pieces on the booms become engine-root armor collars.
- The main tail shells remain open shrouds around recessed engines.
- Overlapping body, ram, armor and shoulder surfaces are joined, buried geometry removed, normals corrected, and nearly coplanar topology simplified. An enclosed 17-vertex sliver at the nose/deck seam was removed.

No panel/greeble pass, UV work, paint, flight check or Unity export.

## Validation

- Original SHA256: 9a3ccb8dfcd23f6a628826bf51336ea526ac8fed67169c46c280fb5257df948e.
- Refined SHA256: 7b2cfa3a5b633f1e8787c1433d6157911c03c2c2cdbaf4f5b89f7511adbc1096.
- Original file hash unchanged after cleanup and all renders.
- Source check passes, with no findings or exemptions, for this separate refined file.
- Original kitbash has 58 source-check findings; its structure is intentionally preserved.
- Evaluated hull: 782 vertices, 594 faces, one connected component, zero boundary or non-manifold edges, positive volume.
- Silhouette overlap: top 99.8017%, side 99.9953%; minimum across six views 99.8017%. Tiny filled regions reflect closing reused armor cavities.
- Orthographic/eight-view sheet and 32/48/64/96-pixel render checks passed. Studio views inspected.
- Source-only art: no Unity tests apply.

[Interactive before/after](index.html) · [Source check](ship_check.json) · [Topology audit](cleanup-audit.json) · [Silhouette comparison](silhouette-comparison.json)

Shape cleanup candidate; owner review is still required before it replaces the kitbash source.
