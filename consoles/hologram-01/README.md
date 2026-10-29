# Hologram console studies

## What is the current owner preference?

The owner chose the mounted terminal from the preceding batch, then attached the surface hologram and said, "This looks really nice, but would love to have the ship color visible in the hologram." The [owner record on #684](https://github.com/amindell11/astronomical-home/issues/684#issuecomment-6509444335) makes that surface treatment the working base. Exact effect values and generator defects are not approved.

The latest [review gallery](review.html) opens with [the Pro color edit](05-color-hologram-pro.jpg). It restores Vanguard's creamy hull, orange accents, graphite areas and cyan circular detail while retaining luminous outlines, scan strata and projection from the bench. The color treatment is the console session's recommendation for review, not a new owner pick. These are the ship's own colors, not new UI color roles.

## What does the comparison show?

| Image | Result |
| --- | --- |
| [Color hologram, Pro](05-color-hologram-pro.jpg) | Recommended. Keeps the mounted terminal and the ship high above the bench. Restores the ship colors and removes the earlier duplicate Launch rectangle. Colored facets read more solid than the monochrome base; the beam is broader. |
| [Color hologram, nb2](05-color-hologram-nb2.jpg) | Restores colors, but lowers the ship and moves the projector into the foreground. Retains the duplicate Launch rectangle. |
| [Surface hologram, nb2](01-surface-nb2.jpg) | Owner-preferred working base before the color edit. Clear translucent volume and a small bench projector. An extra Launch rectangle is a generator defect. |
| [Contour correction, Pro](04-wireframe-correction-pro.jpg) | Retains the chosen console and a more open ship interior. Broader beam. This was the console session's recommendation before the owner selected the surface base. |
| [Surface hologram, Pro](01-surface-pro.jpg) | Replaces the chosen hardware with a different desk and returns to the source capture's overlay menu. |
| [Wireframe hologram, nb2](02-wireframe-nb2.jpg) | Loses the chosen terminal and moves the menu onto the scene. |
| [Wireframe hologram, Pro](02-wireframe-pro.jpg) | Invents a different ship, camera and terminal layout. |
| [Surface correction, nb2](03-surface-correction-nb2.jpg) | Keeps ship identity but replaces the selected terminal and equipment. |
| [Surface correction, Pro](03-surface-correction-pro.jpg) | Changes the ship, hardware, perspective and labels. |
| [Wireframe correction, nb2](04-wireframe-correction-nb2.jpg) | Keeps ship identity but replaces the terminal with a pale panel and the old overlay rows. |

## How were the images made?

All ten outputs came from the repo's `art/tools/imagegen/imagegen.py`, with nb2 and pro run in parallel for each prompt. The five `.txt` files contain the prompts, and each output has the generator's JSON sidecar. No image was manually composited or retouched.

The exact owner attachments are in `inputs/preferred-mounted-terminal.jpg` and `inputs/preferred-surface-hologram.jpg`. The latest attachment becomes the edit target for its round. The original mounted terminal supplies the ship's color pattern for the color edit. `inputs/real-hangar-capture.png` supplies real ship identity. The six mood images remain inputs with their roles stated in each prompt: 03 and 09 for hardware, 04 for electronics and lamps, and 01, 06 and 07 for terminal screens.

The model resynthesizes pixels. These are still concepts, not game captures, production assets or a working console implementation. The gallery uses ordinary document styling around the images. It does not create another type family or effects tuner.

## What does the combat HUD session depend on?

The mounted terminal and surface hologram are the working direction. The color edit is ready for the joint review. No shared type, UI role color or effects setting changed here. The combat session's published `combat-hud/shared/` and tuner files are present in this evidence checkout and remain the source for the later HTML rebuild. The console session still owns the menu and big-moment presets.

All files stay under `consoles/` on the shared evidence branch. No game code or assets changed.
