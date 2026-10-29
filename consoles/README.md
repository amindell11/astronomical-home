# Console panel concepts

These four image-model studies support the first joint review for #1249. The two owner rulings on #684 dated 2026-10-29 remain the direction. None of these studies is an owner pick.

## What should the owner compare?

Each image restyles the same hangar capture and shows the same ship and current loadout. Compare the panel and hardware strategy. Type, sizes, component geometry and effects values remain provisional.

| Option | Image | Strategy | Trade-off |
| --- | --- | --- | --- |
| C1 | [Physical housing](concepts/01-physical.png) | A deep screen recess, heavy matte housing and physical edge wear. | The strongest retro monitor presence also takes the most attention. |
| C2 | [Ink-drawn housing](concepts/02-ink-drawn.png) | Broad light and shadow planes, weighted contours and drawn surrounding equipment. | The closest match to the ship's illustration adds more hardware around the view. |
| C3 | [Blended housing](concepts/03-blended.png) | A recessed screen with simple rails, selective ink contours and broad shaded material. | Keeps physical depth and the drawn finish with less surrounding equipment. Recommended. |
| C4 | [Unboxed projection](concepts/04-unboxed.png) | Screen content projected directly over the scene. | Leaves the widest open view but loses the physical distinction between canopy and console. Required comparison only. |

C3 is the console session's recommendation, not a recorded decision. The combat HUD session hosts the joint numbered review and records owner choices on #684.

## Where did the images come from?

Built-in Codex imagegen generated all four. Exact prompts are in [prompts/](prompts/). The first input is the real [Vanguard hangar capture](https://media.githubusercontent.com/media/amindell11/astronomical-home/9c72e3e6ee0f745291dc692a726632adf261bde5/hangar-stars/Vanguard_Preview.png), copied unchanged to [source/hangar-vanguard.png](source/hangar-vanguard.png). Its source README identifies the current stars-only hangar and distinguishes it from the cancelled illustrated-floor study.

Hardware references are the [beige schematic console](https://raw.githubusercontent.com/amindell11/astronomical-home/cd45b620e2b3f7c32f14af03df73a1d6f4bdce9a/03-beige-console-schematic.png) and [wireframe console](https://raw.githubusercontent.com/amindell11/astronomical-home/cd45b620e2b3f7c32f14af03df73a1d6f4bdce9a/09-beige-console-wireframe.png) from the owner's reference branch.

The image model resynthesized pixels. These are style studies based on a real capture, not pixel-exact captures, implemented UI, modular production art or game assets. The selected strategy will be rebuilt over unchanged captures in HTML.

## What did the review check?

The four images show the existing Hangar, Ship, Engine, Shield, Primary, Secondary and Launch content. The current values are Vanguard, Default_Engine, Default_Shield, Lasers and Missiles. There is no additional telemetry. Hardware uses amber and dim green lamps. Screen content uses warm off-white ink.

The generated frames illustrate reusable corners and straight edge rails, but do not prove modular stretching or the exact 40-degree house angle. Still images do not prove flicker, warm-up, motion limits or reduced-motion behavior. Those belong to the HTML stage after the strategy pick.

## What does the combat session depend on?

The console session owns these files under `consoles/`. It does not create type candidates or tuners. The agreed future shared files are:

- `combat-hud/shared/type.css`
- `combat-hud/shared/type-candidates.json`
- `combat-hud/shared/roles.css`
- `combat-hud/shared/personality.js`
- `combat-hud/tuners/color.html`
- `combat-hud/tuners/personality.html`

Console pages will consume those files. Menu and big-moment presets, the remaining console screens and the three transitions follow the joint strategy review.

No game code or assets changed. All generation and evidence work happened outside the primary tree.
