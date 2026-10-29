# Transparent hologram revision

## What did the owner ask to change?

Continue from the latest colored hologram in the HTML review, `hologram-01/05-color-hologram-pro.jpg`. The owner asked for [less vivid colors, more prominent outlines and a possible tint](https://github.com/amindell11/astronomical-home/issues/684#issuecomment-6510193481), then [more transparency](https://github.com/amindell11/astronomical-home/issues/684#issuecomment-6510331300). They also asked to bring the design closer to the combat HUD prototype they like. The [reference clarification](https://github.com/amindell11/astronomical-home/issues/1249#issuecomment-6510253761) identifies actual Unity captures separately from the browser CRT tuner.

## What should the next review show?

The [gallery](review.html) opens with [the transparent hull, nb2](04-transparent-hull-nb2.jpg). Recommended for review. The pale outer contour carries the silhouette, internal lines are fainter, and subdued peach and cyan retain the ship's identity. More of the nebula shows through the hull. A cool tint and small color fringes relate the projection to the browser prototype. The chosen hardware and dark terminal screen remain recognizable.

The actual game HUD informs the clean line hierarchy and open interiors. The browser prototype informs luminous texture. These stills do not prove additive rendering or animate afterglow and flicker. The rebuild will use the combat session's shared renderer, type and tuners. No new shared setting or UI color role is selected here.

| Attempt | Result |
| --- | --- |
| [Transparent hull, nb2](04-transparent-hull-nb2.jpg) | Recommended. Best balance of recognizable color, transparent interior and clear contour in the chosen layout. |
| [Transparent hull, Pro](04-transparent-hull-pro.jpg) | Similar translucency, with rougher contours and more visible scan texture. |
| [White contours, nb2](01-white-contours-nb2.jpg) | Preserves layout, but retains too much bright surface fill. |
| [Cool tint, nb2](02-cool-tint-nb2.jpg) | Preserves layout and mutes colors; still has more fill than the latest pass. |
| [Game HUD contours, nb2](03-game-hud-contours-nb2.jpg) | Opens the ship interior but brightens the entire terminal screen too much. |
| [White contours, Pro](01-white-contours-pro.jpg) | Changes the ship and console composition. |
| [Cool tint, Pro](02-cool-tint-pro.jpg) | Changes composition, invents labels and adds a holographic chair. |
| [Game HUD contours, Pro](03-game-hud-contours-pro.jpg) | Changes ship, scene, menu and hardware. |

## Where did the references come from?

`inputs/latest-html-hologram.jpg` is the prior recommended Pro color image. The actual Unity frames were copied unchanged from the crosshair session associated with [PR #1194](https://github.com/amindell11/astronomical-home/pull/1194):

- `unity-grenade-hud.png`: `agent-1/scratch/crosshair/concussion-shared/frames/20261029-205128-450-weapon-grenades/f_00144.png`. Its [preserved animation](https://media.githubusercontent.com/media/amindell11/astronomical-home/bcbebf22df1647a499245d8f29306df9afb75045/concussion-shared.gif) supplies the source context.
- `unity-laser-hud.png`: `agent-1/scratch/crosshair/weapon-gifs/frames/20261029-191335-192-weapon-lasers/f_00049.png`.

The two `combat-hud-*-copper.png` files are browser tuner screenshots from `combat-hud/tuners/verification/` at `ce9780d`. Both Unity frames and both browser views were inspected. The strong browser view is a generation input for all four prompts; the actual grenade frame is also an input for prompts 03 and 04. The six original mood references retain their named hardware, electronics and screen roles.

All four prompts ran through the repo image tool with nb2 and pro in parallel. Prompts, inputs and output sidecars are saved here. Numbers in prompts describe visual intent, not measured or approved opacity values. Outputs are model-generated stills without manual retouching. All work stayed outside the primary tree, and no game code or assets changed.
