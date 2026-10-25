# Sector jump preview migration

Source commit: f2a5eae33cb9cb88402faf56e4b8408b467b833d

The shared PreviewHost runs the jump presentation over the real player hull, live Locale_3 and Locale_1 skies, and actual HUD. The shared prepared Game View recorder captured 365 PNG frames at 1280x720 with the High Fidelity quality setting.

The scenario scrubs the presentation in 0.02-second steps. The clip is encoded at 50 fps to reproduce that presentation timeline; the GIF samples every second frame at 25 fps. Recorder wall-clock intervals include capture overhead and do not measure real-time rendering performance. The MP4 is the color reference; the GIF uses a limited palette.

## Checks

- SectorJumpSequenceEditModeTests: 5 passed. Deterministic scrubbing, saved speed overshoot, speed-driven blur, integrated distance, endpoint restoration.
- PreviewHostBootstrapEditModeTests: 1 passed.
- SectorJumpPreviewScenario through CaptureScenarioPlayModeTests: 1 passed on the source commit above. Visible source/destination backdrop difference, HUD endpoint, repeatable scrubbing, invariant hull framing at 0 and 45 degrees, backdrop unload, jump after staged defeat, and teardown clock restoration.

The runs used the coordinated resident Editor and are scoped development checks. They are not merge-grade full-suite proof. Hangar Launch and sector Continue integration remain outside this preview migration.

## Preserved prior version

The old implementation and saved tuning are preserved at evidence/sector-jump commit 5d22cfb21ae43b4d50c1818a8b33638a1c71d8ad under tuned-before-migration/. The migrated baseline and working tuning are separate SectorJumpProfile assets in this PR.
