# Capture preset acceptance

Source and capture commit: c2b5c2c9234d5be8b93624ae6260618e14c9c520. Base: recorder PR #1165 at 8a9a025ceccae736a021e9cdc5afc2e4e112ad0a, stacked on tooling PR #1178. All three named preparation paths ran through the coordinated resident editor and the existing capture lane. No merge is authorized.

## Ownership

The recorder request and writer are unchanged by C. GameViewEpisodeCapture composes an owned named-view scope with the existing recording scope. Named preparation owns scene, lighting and overlay policy. The shared diagnostic transaction and existing recovery journal retain and restore those settings; recording cleanup runs before view cleanup. Callers still own simulation and preview time.

`PrepareGameTopdown(observer)` uses the supplied authored ObserverCam and its normal framing and smoothing. `PrepareLineupNeutral(subjects)` accepts explicit visual-backed ship prefabs at their prefab root scale and identity pose. `PrepareHud1280x720()` sets the Game View size and hides debug overlays while preserving the host's canvas and opacity ownership.

## Warm results

All three focused routed summaries passed 1/1, with no skips. These are resident-editor graphics acceptance checks, not cold headless merge proof.

| Preparation | Retained output | Evidence |
| --- | --- | --- |
| lineup-neutral | 6 PNG/row pairs at 2048x512 and 6 at 1024x256 | Every selected hull occupies pixels; neutral key/ambient, quality, fog and shader settings checked; recording preserves prepared settings; view disposal restores prior presentation and Game View configuration after repaint. |
| hud-1280x720 | 23 PNG/row pairs at 1280x720 | Actual PreviewHost Victory banner changes in the center region, host transport advances from span 1 to 2, simulation stays at 0.908060579648526, and unscaled time advances from 10.231936798469388 to 11.05538540249433. A caller-owned disabled canvas and alpha 0.37 stay unchanged. Presentation and Game View settings restore after disposal. |
| game-topdown | 8 PNG/row pairs at 1280x720 | Actual authored ObserverCam orientation and subject framing checked, decoded center-region scene pixels checked, camera enabled state and presentation/Game View settings restored. The middle frame shows the player, world bars, game HUD and star background without the debug overlay. |

The neutral row is Vanguard, Crimson, Nightshade, Valis and Bulwark, left to right. It uses white directional light at Euler(20,-25,0), intensity 1.6, soft shadows and flat ambient 0.24. Only hull renderers are enabled; canvases and particles are disabled. No art assets were changed.

HUD MP4 and GIF were assembled from the matching unscaled timestamps without an FPS override. MP4 passed frame-count and duration read-back; GIF passed duration and decoded subject-colour read-back. Playback lasts 0.866841 seconds before GIF precision quantization. The final still uses the declared repeat-last-interval convention; its hold is inferred, not measured. Raw PNGs are preserved. The recorded banner follows the current RecapFeel asset and real PreviewHost transport.

Offline checks confirmed contiguous frame IDs, exact filename correspondence, frame counts, decoded dimensions, monotonically increasing unscaled timestamps, completed manifests and the source commit for every retained output. `capture-c-pair-check.json` carries the counts and times.

## Negative result and correction

The first warm neutral test at be19e9418 failed before recording. ItemCatalog's legacy Junker_1 entry has a valid Ship but no serialized VisualRig or child ShipVisualRig. Its separate Mesh/MapMesh hierarchy is outside the current visual-rig presentation contract. Catalog membership did not prove representation support. Preparation now accepts an explicit caller-selected list and rejects an unsupported ship by name before changing settings, fix-ladder rung 2. The test preserves Junker_1 as that observed rejection case. It is not silently skipped, and no all-catalog capability or legacy asset conversion is claimed.

The original issue requires neutral lighting/pose, not legacy cargo conversion. The historical Nightshade renderer also selected a named fleet. This correction stays inside caller-owned presentation, without a new registry, wrapper type or legacy rendering policy.

## Files and cleanup

`capture-c-complete-outputs.tar.gz` contains every PNG, frames.csv, provenance.json and completed manifest from the four output folders. The standalone images and HUD videos are unchanged copies for review. `capture-c-test-records.tar.gz` contains the initial failed summary, the three passing warm summaries, the editor logs and the two read-only asset probes.

Passing summaries: 20261024-220231, 20261024-220258 and 20261024-220341. Initial negative summary: 20261024-215459. The final resident-editor lane restored Enter Play Mode Options, then the coordinator closed PID 44060. Only verified generated analytics and quality-profile noise was restored afterward. No additional RL work was scheduled, per the user's priority ruling.

## Final review

The independent combined review cleared exact c2b5c2c92 against B 8a9a025ce with no actionable correctness, scope, comment or Unity-convention finding. The reviewer inspected all three retained views. Local ReSharper passed at c2b5c2c92 against origin/main with 0 blocking findings; the two report-only HarnessHost findings are inherited from B. No source changed after these captures. C changes no scripts and schedules no additional RL work. Hosted checks remain separate from the warm evidence and start when the draft PR is opened.
