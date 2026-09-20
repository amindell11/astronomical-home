Independent terminal-field chase evidence. This is an evidence-only prerelease, not a software release. The tag remains at baseline main commit 27c356bff9aaae376fbcbd3d6b0f28d0d89b87cb; it does not identify the experimental implementation.

## Revised candidate — not accepted; session validation incomplete

Paired warm captures: 40 seconds each, 400 frames at 10 fps, density 2, seed 3301, with native debug gizmos. Both captures passed their test, and middle/late frames were inspected. Frozen source: 676e02f6; settings LF SHA256: A3411E06D3BFBEC484503DFB0C91E0F3B6F40FD9B01E89175C899AFAF8058984.

- [Revised field on, weight 3](https://github.com/amindell11/astronomical-home/releases/download/codex/terminal-field-evidence-20260909/20260909-182124-terminal-field-dense-chase-on.mp4)
- [Revised field off, weight 0](https://github.com/amindell11/astronomical-home/releases/download/codex/terminal-field-evidence-20260909/20260909-181842-terminal-field-dense-chase-off.mp4)

| Measurement | Field on | Field off |
|---|---:|---:|
| Final gap (m) | 9.944542 | 25.87489 |
| Closing progress (m) | 40.055458 | 24.12511 |
| Path length (m) | 431.1915 | 464.7068 |
| Path length / closing progress | 10.76486 | 19.26237 |
| Collision steps | 7 | 13 |
| Stalled 2-second windows | 9 | 8 |

All three transit repeats passed: 20/20 arrivals with the field versus 16/20 without it, with zero collisions. The candidate is not accepted: the first session matrix recorded 164 field-on collision steps versus 156 field-off in kite, violating the strict no-increase requirement. Kite collision-bearing episodes were 10 versus 13, and movement/success remained within tolerance. Orbit and cover comparisons passed. The full matrix and two further repeats remain incomplete. The chase comparison is behavioral evidence, not a replacement for the acceptance tests.

## Failed candidate 1 — preserved historical evidence

Candidate 1 failed acceptance: its frozen transit benchmark achieved only +2 arrivals over baseline instead of the required +4, and chase collision steps increased. These original videos remain unchanged.

Both original clips show a 40-second comparison with the same dense fleeing-chase setup and seed 3301, with native debug gizmos enabled.

- [Candidate 1 field on](https://github.com/amindell11/astronomical-home/releases/download/codex/terminal-field-evidence-20260909/20260909-041526-terminal-field-dense-chase-on.mp4)
- [Candidate 1 field off](https://github.com/amindell11/astronomical-home/releases/download/codex/terminal-field-evidence-20260909/20260909-041658-terminal-field-dense-chase-off.mp4)

Only video artifacts are attached. Implementation details and subsequent debugging belong in the eventual PR.

