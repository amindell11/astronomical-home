## What does the recording show?

The 12-second recording stages events in the real Combat Playtest at commit `280df20a0b07019fc4d422447c1c149ec9e7e387`. It uses the owner's saved base settings and the profile's BigMoment overrides. World simulation is paused while decorative clocks continue on unscaled time. Film grain is disabled in runtime copies so the HUD changes are easier to compare. One enemy is placed near the player for the reticle demonstration.

| Approximate time | Staged event |
| --- | --- |
| 0 seconds | Base settings |
| 1 second | Applied laser hit and decaying shudder |
| 2.4 seconds | Hull set to 20 percent, activating LowHull and irregular shudder |
| 5.1 seconds | Hull restored and the real wave director advanced into phase 2 |
| 7.2 seconds | Wave schedule completed, allowing the objective service to open extraction |
| 9.3 seconds | Enemy lock progress raised, revealing the HUD-owned reticle |
| 10.8 seconds | Saved profile switch disabled, leaving the functional HUD visible |

`frames.csv` records the event stage, resolved state, hit and transition ages, hull fraction and effective off switch for every acquired frame. `capture-check.json` confirms the expected states and clocks, paused world time and nonuniform image content. The capture fixture passed. Selected frames were inspected before encoding.

## How were the clips verified?

The shared encoder verified the GIF's playback duration and subject colors. Its MP4 duration check reproduced #1185, which compares FFmpeg's progress clock with the presentation timeline. Independent decoding recovered all 152 acquired frames plus the terminal repeat. The maximum timestamp difference from the source was 0.497 milliseconds. `mp4-verification.json` records that result. No capture-tool code was changed in this PR.

## What are the limits?

These are staged event demonstrations on the Performant quality level, not combat difficulty or performance measurements. Focused tests separately check the appearance dip depth, repeat limit, ownership, visibility and fade multiplication. The recording does not establish visual parity with different instrument artwork or typography.

The Editor was closed through its coordinator, and its temporary quality setting was restored. The capture scenario was removed from Assets. Its source is preserved here for reproduction.
