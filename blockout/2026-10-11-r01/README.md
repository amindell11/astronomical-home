# Bulwark — blockout round 1

## What was built?

The owner approved the round 4 turnaround and authorized blockout on October 11, 2026. The source was scaffolded with ship_new, then shaped in an interactive Blender session against aligned top and side reference images. Four meshes hold the hull, mirrored tail, canopy and mirrored side inset; their X Mirror modifiers remain live. The source points +Y with Z up and uses the ship role collections.

[Hero](hero.png) · [Review](index.html) · [Approved concept](approved-concept.png) · [Source check](ship_check.json)

## What needs the owner's judgment?

The concept's side view raises the tail tips above the hull, but its front drawing omits them. The blockout follows the top and side geometry; the raised tips are visible in the front render. Their height remains a review question. Color regions are broad placeholders; there are no panel seams, vents, trim, mechanical details, paint UV work, or game integration.

The 10-unit overall length is a modeling reference scale, not a gameplay balance decision. The owner approves proportions before the detail-model stage.

## What was checked?

- ship_check passed with no findings or exemptions.
- ship_render ortho and scale passed and verified that the source file did not change.
- The main hull and tail were visually checked over the top and side reference planes in Blender.
- The hero is a temporary studio-lit Workbench review render; orthographic and scale images use the pipeline's flat review preset.
- The ReSharper ratchet skipped because no C# changed. No Unity assets or gameplay code changed.

The source and approved concept are committed on the Bulwark work branch; review images belong only to evidence/bulwark. Proportions await owner approval.
