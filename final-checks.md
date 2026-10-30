## What changed after the capture?

Commit `9dfda2e7f68f6acf08a4a2606e8deaa7947118b5` adds `RequiresGraphics` to the three CanopyEventsPlayModeTests. Their common setup creates a real projection render texture. A cold headless run reproduced RenderTexture.Create failures before that label was added. This is an early test-selection correction, not a runtime guard or suppressed log.

After the correction, five selected headless tests passed and all three graphics tests passed in a cold run. ReSharper again passed with zero blocking findings and 60 report-only findings. Review of the one-line delta found no issues. Runtime code is unchanged from the recorded commit `280df20a0b07019fc4d422447c1c149ec9e7e387`.

The final proof is in `headless-summary.json`, `graphics-summary.json` and `resharper-summary.json`. Hosted checks run separately on the final PR head.
