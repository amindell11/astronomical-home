# Combat canopy concepts

First review for #1249. These image-model studies compare the projection material. They do not settle type, colors, symbols, sizes or instrument details.

- 01-vector.png draws amounts as thin strokes, with open instrument interiors.
- 02-luminous-fields.png adds faint illuminated areas behind separate instruments.
- 03-measured-fills.png reserves illuminated ribbons and hatching for quantities.

The provisional recommendation is the third strategy. It keeps the open view while giving charge and remaining capacity a clearer visual area.

The source is a real Unity gameplay capture of Copper & Turquoise, saved in inputs/locale1-gameplay.png. Source evidence is commit a1f83ad693375be941ffd6bb62ee46fe887c93c3, gameplay-final.png. The image model resynthesized pixels. These studies therefore do not prove additive blending, exact capture fidelity or readability across all locales. The brightest-locale test remains required in the HTML rebuild.

Built-in Codex imagegen produced these files. Prompts and source hashes are in provenance.json. No game code or assets changed.

## Shared files after the first choices

The combat session owns shared/type.css, shared/type-candidates.json, shared/roles.css and shared/personality.js. It also owns tuners/color.html and tuners/personality.html. These are agreed paths for the next stage, not delivered files. Console HTML consumes the shared files and supplies its menu and big-moment presets.
