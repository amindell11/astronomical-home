# B with physical selection and screen effects

This pass keeps the selected B hull diagram and explores the [2026-11-07 owner direction](https://github.com/amindell11/astronomical-home/issues/684#issuecomment-6704567926): canopy-style display effects, physical selection controls, a small detail display, lighter hardware labels, and a revised physical Launch button.

The [interactive review](index.html) is the main deliverable. The previous [four-way comparison](../hull-directions-03/index.html) preserves the layout decision.

## Interaction to review

Hovering an item temporarily previews its details. Clicking selects it and rotates the existing knob to its corresponding position. Previous and next cycle through hull, Lasers, Missiles, and Guard Dash. Moving away from a preview returns the detail display to the selected item.

The detail display uses real values and fitted-part names from the [existing source data](../display-concepts-01/assets/screen-data.json). It does not add invented descriptions or status telemetry. The Launch control is a visual interaction prototype and does not start a game.

Use the side buttons or arrow keys to cycle items. Enter and Space activate a focused control. Clicking the knob also advances the selection. The review page has a Screen effects toggle and a Native size view. Reduced-motion preferences keep the static glow and scan lines while disabling temporal effects and knob animation.

## Sources

The display uses the current HUD fonts and the actual Vanguard, weapon, and Dash glyphs recorded in [asset provenance](../display-concepts-01/assets/provenance.json).

Screen effects consume [the shared canopy renderer](../../combat-hud/shared/personality.js) directly. Its menu preset supplies the effect family, including temporal phosphor history and a brief transition glitch. The console does not introduce another shared tuner or fork the renderer.

Built-in imagegen produced [the hardware base](hardware-base.png) from the previous [widened monitor image](../hull-directions-03/wide-display-base.png). The [exact edit prompt](imagegen-prompt.txt) is preserved. This adds the lower display and side buttons, removes the old bold lettering, and simplifies the Launch button for native typography. Imagegen resynthesizes pixels; the surrounding art is visually preserved, not guaranteed pixel-identical.

The hologram remains the accepted concept bitmap with a live effect layer. This pass does not rebuild the ship or console in 3D.

All files are in the evidence checkout outside the primary tree. No game code, game assets, shared canopy files, or Blender files changed. No merge.

## Review evidence

- [Full scene](preview.png) and [console crop](console-native.png).
- [Effects on](effects-on.png) and [effects off](effects-off.png).
- [Missiles selected](selected-missiles.png).
- [Transition at 100 ms](transition-100ms.png), [phosphor at 400 ms](afterglow-400ms.png), and [settled after two seconds](afterglow-2000ms.png).
- [Interaction checks](interaction-checks.json) cover hover restoration, persistent selection, knob angle, physical buttons, arrow keys, native Enter/Space activation, transient effects, phosphor persistence, and reduced-motion feedback.
- [Effects comparison checks](effect-pixel-checks.json) found no changed pixels outside the two displays and hologram region. Browser-error and text-fit checks passed.

The shared menu settings are reduced locally for this smaller display. The renderer itself is unchanged. Glitches are triggered by information changes; they are not a continuous flicker sequence.
