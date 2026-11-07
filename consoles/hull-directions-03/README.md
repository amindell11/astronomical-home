# Wider hull-diagram directions

The owner [selected B on November 7](https://github.com/amindell11/astronomical-home/issues/684#issuecomment-6704567926). The [interactive B pass](../interactive-console-04/index.html) keeps that layout and explores effects, controls, and a detail display.

The owner asked to remove the six right-hand selector buttons and expand the glass into that space. These four concepts keep the hull in the center, connect its stats with leader lines, integrate both weapons into the diagram, and give Guard Dash more space.

Open the [review](index.html) to compare the screens inside the console and at larger sizes. The previous [compact pass](../display-refinement-02/index.html) remains available for comparison.

| Direction | Arrangement | Full scene |
| --- | --- | --- |
| A, radial mounts | Stats surround the hull, weapons sit alongside, and Dash anchors the bottom. | [A](a-full-scene.png) |
| B, bilateral instruments | Stat columns frame the hull, with lower weapon branches and a larger Dash instrument on the right. | [B](b-full-scene.png) |
| C, forward fan | Stats fan above the hull, weapons flank it, and Dash sits on its centerline. | [C](c-full-scene.png) |
| D, Dash ring | An interrupted ring groups the hull and Dash, with weapon callouts above. | [D](d-full-scene.png) |

The ring in D is a layout treatment, not an additional ability or telemetry. Each screen keeps the same six ship values and two weapons.

The [owner ruling](https://github.com/amindell11/astronomical-home/issues/684#issuecomment-6701671760) sets the scope. This is a screen concept pass. The physical Launch button and exterior labels are still awaiting their own refinement.

## Sources

Built-in imagegen edited the approved [viewport glass image](../refinement-04/viewport-glass.png) to create [the widened blank display](wide-display-base.png). The [exact prompt](imagegen-prompt.txt) requests only the inner-aperture change. Imagegen resynthesizes pixels; the surrounding composition is visually preserved, not guaranteed pixel-identical.

The screens are native layouts over that base. They reuse the current HUD fonts, Vanguard silhouette, Lasers and Missiles glyphs, and approved Dash glyph from [the shared source assets](../display-concepts-01/assets/provenance.json). The [ship values](../display-concepts-01/assets/screen-data.json) were verified against merged commit `7834963` in the preceding pass. No new font candidates, icons, or game telemetry were invented.

All work is outside the primary tree. No game code, game assets, or Blender files changed.

## Review checks

The [render checks](render-checks.json) record font loading, all six values, the eight stat and weapon leaders, and text bounds for every option. The [pixel checks](pixel-checks.json) compare the overlays to the widened base, not to the earlier imagegen input. Full-scene renders are 1672 by 941; native screen crops are 370 by 319; close-ups are enlarged three times.
