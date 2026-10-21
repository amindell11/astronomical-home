## What guided these layouts?

The user likes the actual ship silhouettes and requested alternatives to the Unity mockup's panel layout and style, using the hangar and design docs for inspiration.

The art guide's Interface section asks for flat, minimal, precise instruments: thin off-white strokes, translucent navy backing, subtle dividers, generous spacing, narrow clean typography and modest outlined actions. Its hangar reference uses a left-side equipment panel, interrupted corners and an outlined Launch action. The current HangarScreen prefab likewise uses dark navy surfaces with pale text and blue-gray secondary labels.

References at source baseline a9eb769063224d9bd07e92861abda6f1722e7121:
- doc/design/Art/Style.md, Interface; selected Hangar reference
- doc/design/assets/Pasted image 20260923023842.png
- src/Asteroids3D/Assets/Prefabs/UI/HangarScreen.prefab
- doc/design/Design/Gameplay/Combat.md, High-level intent
- doc/design/Design/World/Sectors.md, Sector win condition

The combat and sector notes distinguish defeating ships from completing sector objectives and extracting. The mockups therefore keep trophies and cleared-sector progress separate.

## What differs between the options?

- Quiet glass: centered navy panel with fine dividers and an outlined action.
- Open frame: centered marks over a soft backing, with little enclosing chrome.
- Low strip: a wide tally near the bottom, leaving the center of the game clear.
- Hangar side panel: a narrow panel at the left, borrowing the hangar's equipment layout.
- Sector chapters: kills grouped by the sector in which they occurred; sector status sits underneath each group.
- Floating bands: separate backing for trophies and sector progress, with the action outside both.

Each uses the same 17 example kills and the five silhouettes captured from the actual ship models. Numbered sectors remain; sector pictures are deferred in #1071. These are interactive layout mockups over a schematic game backdrop, not new Unity captures or integrated gameplay.

## What was verified?

Six variants rendered at 1024, 736 and 320 pixels. All 18 layouts kept the controls inside the game area with no horizontal overflow. Every variant showed 17 trophies. Selecting a trophy showed its ship name; Continue displayed its proposed destination. No JavaScript page errors occurred. Desktop renders and a narrow render were inspected. The source is a 32 KB self-contained fragment with embedded ship silhouettes.
