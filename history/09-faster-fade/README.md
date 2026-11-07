# Faster fade after the first flight

On 2026-11-06 the owner flew the first standard laser and asked for a slightly faster fade. Implementation commit `916a9f13d` adds a particle-opacity fade that holds through 60 ms and reaches zero at 280 ms. Source motion, lifetime, drawing, and scale stay as in stage 08.

Five focused checks passed together in Unity 6000.1.8f1: the three production hit checks and two temporary rendered-hit probes. Actual ship and asteroid collisions still spawn the visible flash. At 300 ms, hiding the effect changes zero pixels on either target, proving that the visible tail is gone.

The `*-impact.png` frames show the animation at 50 ms. The `*-300ms.png` frames show the shortened tail's endpoint, and the matching `*-without.png` frames hide only the effect renderer. The temporary probe source is preserved here. It uses zero projectile damage to keep targets intact and the game camera's culling mask, as described in stage 08.

The shape animation remains 340 ms long; shortening opacity instead of speeding the animation preserves the opening movement. These images verify rendering and fade timing, not final owner approval of the new feel.
