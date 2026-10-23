# Death transition correction

Production commit: 6c58d1a7d7904ca186d821b169546c242fefc1e3
PR: https://github.com/amindell11/astronomical-home/pull/1146

The recording uses the actual game scene with scripted player death and automated Continue. It shows the approved banner and breakdown over the game. Capture uses fixed simulation steps while the UI uses unscaled time, so the clip is not a real-time pacing or audio assessment. The development FMOD overlay is visible.

`recap-tests.json` records the nine passing focused graphics tests, including camera framing, surviving ship movement, timing, input and next-run restoration. `before-fix-tests.json` records the two reproduced failures before the correction. Hosted CI separately passed all 1,146 tests and ReSharper: https://github.com/amindell11/astronomical-home/actions/runs/39897613062

`capture-summary.json` reports an infrastructure error: the capture assertion passed and wrote 79 frames, but Unity failed to exit and the runner terminated it at its timeout. This is not a clean capture-run pass. `capture-timing.csv` contains the recorded phase telemetry; `RecapResponseScenario.cs` preserves the scratch reproduction.
