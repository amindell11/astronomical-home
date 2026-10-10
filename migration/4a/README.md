# migration/4a — Valis prefab onto the neutral base

Slice 4a of #868, issue #970. `Valis.prefab` became a depth-1 variant of `ShipBase` with its
meshes unchanged. "Main" below is main @ `099b09cf` plus the harness commit `cb1d27dd`, which adds
the Valis cases and touches no asset.

| Folder / file | What it holds |
| --- | --- |
| `pixel-report.txt` | The pixel proof and why the cross-session golden image wobbles for Valis. Start here. |
| `baseline-main/` | The first harness run on main: Valis at yaw 0/90/135, prefab pose and wings swept, plus the run summary. |
| `pixel-noise/main-run1`, `main-run2`, `branch-run1`, `branch-run2` | Valis renders from four harness sessions (two on main, two on the branch). |
| `pixel-noise/bisect/` | One-session renders: `O-*` main's Valis, `N-*` migrated Valis, `*-clones*` with the hull's materials re-instanced; `bisect.txt` is the pixel counts. |
| `engine/` | Engine stills with particles simulated (seeded): `main-*` (main's Valis), `main-noreactor-*` (main's Valis with only its Reactor left off), `branch-*` (migrated). `engine-sheet.png` is the side-by-side; `engine-compare.txt` the pixel counts. |
| `dumps/before/`, `dumps/after/` | Live-editor dumps: every component's serialized properties, world matrices, PhysX inertia at mass 215 and 800, fileIDs, stat lines, the two references and the whole-tree GUID scan. `before/ShipBase.*` is the base the variant inherits. |
| `dumps/world-compare.txt` | Root-relative world matrices of the hull renderer, its five bones, both colliders, the hardpoints, the exhaust emitters and the minimap marker, old vs new. |
| `dumps/prop-compare.txt` | Every serialized difference between old and new Valis, paths mapped onto the base rig. |
| `reference-report.txt` | The two references to Valis's `Ship` component: before, mid-build, rewritten and after. |
| `logs/` | Editor scripts run over `unity command run_script` (`Migrate.cs`, `Dump.cs`, `EngineStill.cs`), the scratch bisection test (never committed), the comparison scripts, and the build and remap logs. |
