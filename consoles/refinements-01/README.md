# Ink-drawn housing refinements

The owner [rejected these three refinements](https://github.com/amindell11/astronomical-home/issues/684#issuecomment-6508943698). The request was for better detail, not less. The light housing crowds the ship in R1 and R2, and R3 floats too freely. The recommendation below records the session's earlier interpretation, which the owner corrected.

The owner [selected the first batch's B finish](https://github.com/amindell11/astronomical-home/issues/684#issuecomment-6508133052), while leaving shape and detail open. These three concepts keep that finish and compare housing construction. They continue the concept review before the HTML rebuild.

## What changed in these options?

All three keep the same current hangar content, ship and source scene. The prompts replace the first B image's cables, vents and stacked equipment with broad drawn planes and fewer seams. This reduction is a proposed response to the open detail feedback, not a separate owner ruling.

| Option | Image | Construction | Trade-off |
| --- | --- | --- | --- |
| R1 | [Compact bezel](01-compact-bezel.png) | A simple recessed monitor with a low sill and a separate Launch housing. | Keeps the installed-console feel and removes most surrounding machinery. Recommended. |
| R2 | [Continuous console face](02-continuous-face.png) | One broad equipment face joins the left screen to the lower Launch control. | The strongest sense of one physical console, with more material across the view. |
| R3 | [Corner blocks and rails](03-corner-and-rail.png) | Distinct corner pieces joined by long straight edges. | The clearest reusable frame construction, but the generated boxes feel detached from the cockpit. |

R1 is the session recommendation for the next joint review. It retains a believable console housing while letting the broad shading carry the detail. None of these refinements is selected yet. The approved finish remains the ink-drawn treatment from the first B image.

## What is still provisional?

The image model resynthesized the source capture. These are whole-screen style studies, not pixel-exact screenshots or production hardware. They do not settle dimensions, the exact house angle, modular stretching, type, palette or motion. Screen typography and loadout content are deliberately unchanged between options.

The more generic screen row styling is not a new direction decision. The shared type and personality work belongs to the combat HUD session and will be consumed in the HTML stage.

## How were they made?

Built-in Codex imagegen used the unchanged real [hangar capture](../source/hangar-vanguard.png) as the scene input and [the first B concept](../concepts/02-ink-drawn.png) as the finish reference. Exact prompts are in [prompts/](prompts/), and hashes and reference details are in [provenance.json](provenance.json).

The combat HUD session hosts the joint review with its revised canopy concepts. No game code or assets changed.
