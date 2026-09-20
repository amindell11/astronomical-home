# Validation candidate freeze

Frozen before consuming final validation seeds, 2026-09-09: terminal weight **0.03**, horizon 1.7 s, resolution 48, minimum spacing 4 m, bake interval 0.4 s. The committed-candidate asset SHA256 is `C4C9ECDB1B0173E747553E81E4CB001EAA1F5115A8B765CDDE57CC8648758197` (file bytes; formatting-only changes affect this hash).

Occupancy uses cell-center membership in discs inflated by unbanked hull radius plus the existing collision safety margin. The initial implementation conservatively included intersecting cell squares, effectively adding a cell-sized footprint. The center representation uses the configured clearance directly; its explicit unit cases distinguish zero clearance from an added 1 m clearance. Dijkstra, seeding, sampling, scheduling and all benchmark conditions are unchanged. Both rasterization experiments and their limitations are retained under `center-raster-experiment`.

Selection evidence: 4/5 original development transit seeds and 20/20 additional development seeds 3601–3620. All six movement-error development comparisons pass, with improved dummy closeout time. Cover and fire-lane collision-step development comparisons fail. The candidate is frozen for an honest final measurement, not accepted for shipping.

Additional development terrain gave the field-off baseline 20/20 successes. This suggests the paired-benefit requirement may meet a ceiling on the declared validation seeds. The requirement remains unchanged: >=18/20 arrivals and at least four more than field-off, separately in three fresh processes. No validation-driven weight retuning or seed replacement.

Final transit seeds: 1234, 7, 99, 2001, 2002, 3101–3115. Final session seeds remain 3201–3215. Reserved 1001–1020 remain unused.
