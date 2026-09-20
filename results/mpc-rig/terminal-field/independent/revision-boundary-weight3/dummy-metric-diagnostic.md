# Dummy metric diagnosis — no contract change applied

The frozen benchmark still fails. This diagnostic does not replace its episode-average movement-error test.

At development weight 3, every paired seed reaches 10 m sooner, wins sooner, and has no contacts. Post-warmup accumulated absolute ring error is lower on every seed. The average error nevertheless increases on two seeds because the successful episodes end sooner.

| Seed | Win seconds on/off | Closeout seconds on/off | Average error on/off (m) | Integrated error on/off (m·s) | Contacts on/off |
|---|---:|---:|---:|---:|---:|
| 3401 | 23.48 / 31.72 | 16.40 / 16.58 | 25.20 / 18.36 | 541.36 / 545.71 | 0 / 0 |
| 3402 | 15.38 / 18.58 | 5.84 / 6.44 | 6.61 / 6.81 | 88.38 / 112.95 | 0 / 0 |
| 3403 | 18.46 / 36.14 | 10.84 / 23.78 | 19.23 / 15.60 | 316.45 / 532.68 | 0 / 17 |

Integrated error is the recorded mean multiplied by the recorded post-warmup sample count and the fixed 0.02 s timestep. No simulation rerun or changed seed is used to compute it. The identity `mean = integral / sampled duration` explains why a shorter successful episode can have a worse average while accumulating less error. This does not claim that its trajectory is better at every instant.

The original average-error comparison remains in all evidence. A possible contract revision, requiring explicit user agreement before validation, is to assess dummy-closeout movement using the median integrated ring error, allowing at most `max(10% of baseline, 1 m·s)` increase. Keep its success, closeout-time and collision requirements unchanged; keep the other five rows' movement metrics unchanged. This is a proposed change of metric, not a claim that the current contract passed.

Acceptance seeds 3201–3215 have not been consumed. The remaining five development rows are being checked once at weight 3 to determine whether this metric issue is the only remaining development failure. No proposal should be presented as sufficient if other behavioral requirements fail.
