# Screen concept directions

Open the [six-concept review](index.html) or the [contact sheet](contact-sheet.png). This batch explores the screen itself after the owner approved the viewport glass. The surrounding console, exterior labels and physical Launch button are not changed.

Each study uses the same Vanguard data, actual icons and current HUD fonts. A main spec readout and a narrow companion weapon readout stay visible together, following the hangar spec-sheet rulings. The display sizes here are for comparing concepts, not a component-size decision.

- A, Flight card, gives the ship identity and grouped stats a balanced hierarchy.
- B, Hull diagram, places the ship silhouette at the center of grouped stat callouts.
- C, Ship dossier, emphasizes the name and description above a quiet ledger.
- D, Component stack, organizes the fitted systems into blocks beside an icon rail.
- E, Open instruments, separates the data into sparse HUD-like clusters.
- F, Vertical instruments, compares the six quantities as grouped vertical tracks.

The console session recommends A as the clearest balanced starting point, or E for a stronger resemblance to the open combat HUD. B makes the ship diagram dominant; C prioritizes an orderly reading sequence; D makes the fitted parts explicit; F gives the quantities an instrument-bank arrangement. No direction is selected yet.

These are layouts rendered with real font and image files, not image-model lettering. Rajdhani Medium supplies labels, Orbitron Medium supplies values, and Michroma supplies headings. The source Medium faces use their existing weight without synthetic bold. The gallery reuses the combat session's shared type and color-role files, which remain unchanged.

The Vanguard silhouette, Lasers and Missiles glyphs come from existing game assets. The Dash glyph comes from the merged keystone-icon change in [PR #1379](https://github.com/amindell11/astronomical-home/pull/1379). The [source records](assets/provenance.json) identify the original assets and font licenses. [Screen data](assets/screen-data.json) records the verified values and bar bounds. No authored Vanguard description exists in that source, so the concepts mark its copy as pending rather than introducing a new ship description.

The two HUD captures under `assets/reference/` are real October 30 Unity captures used for visual reference. They predate the newly merged Dash glyph. This is a screen study only; no Blender or game files changed.
