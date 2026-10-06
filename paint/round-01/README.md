# Nightshade paint concept — round 01

Owner review pending. The owner approved starting this paint-concept pass after the detail-model review. The render source is the approved `c1fdb2746421408761f4202dfdef32422c554fdc` Nightshade model.

![Paint concept](concept.png)

![Game-scale concept thumbnails](game-scale.png)

Dark slate panels, charcoal recesses, restrained cool edge light, blue-violet glass and magenta tips follow the approved concepts. The illustration proposes surface paint over four fresh model renders. The source model, material assignments, UVs, modifiers and scales were not edited. The generated illustration is a paint reference, not geometric validation or finished masks.

- [Exact rendered edit target](flat-contact-sheet.png)
- [Prompt](prompt.txt) and [generation provenance](concept.json)
- [Source audit](render-audit.json)
- [Existing source check for the identical source hash](source-check.json): zero findings, nine active owner-approved scale exemptions.

The game-scale image reduces the concept's top view to 32, 48, 64 and 96 pixels of ship length; actual-size thumbnails sit above nearest-neighbour enlargements. These are concept thumbnails on the reference background, not in-game captures.

The detail model is approved. Owner flight approval, a formal geometry lock, UV approval and paint-concept approval remain outstanding. No final masks, Unity integration, legacy hull changes, ShipLegacyList changes, breakup work or PR are included.

The shared mask-projection and palette-material generators described by arc #868 slice 8 are absent from current main (`25785b3`). Valis's grayscale masks, palette/settings files, layered ORA and archived authoring helpers were inspected as references; no generic tool completion is claimed.
