# Combat canopy review

The [current joint review](review-04.html) presents the live canopy and console together. The console uses the canopy's shared fonts and CRT renderer. The [preceding review](review-03.html) preserves the earlier colored-hologram stills.

The current review uses the [live personality tuner](tuners/personality.html) and [color tuner](tuners/color.html). Open either downloaded HTML file in a WebGL browser. Each includes its captures, fonts and code, so it runs without a server or network connection. The [tuner notes](tuners/README.md) cover controls, sources and verification.

The revised #1249 brief moved combat review out of imagegen. The pages retain the owner's leaning toward vector A and start the CRT effects visibly strong. No settings are approved yet.

## Earlier concepts

First review for #1249. These image-model studies compare the projection material. They do not settle type, colors, symbols, sizes or instrument details.

- 01-vector.png draws amounts as thin strokes, with open instrument interiors.
- 02-luminous-fields.png adds faint illuminated areas behind separate instruments.
- 03-measured-fills.png reserves illuminated ribbons and hatching for quantities.

The initial session recommendation was the third strategy. The [owner subsequently leaned toward A](https://github.com/amindell11/astronomical-home/issues/684#issuecomment-6508117055) for its subtlety and simplicity, while asking for more CRT character. The leaning is not a final selection.

The [second joint review](review-02.html) compares refinements of A with refinements of the chosen ink-drawn console finish. The [canopy refinement index](refinements-01/README.md) records the files and their limits.

The source is a real Unity gameplay capture of Copper & Turquoise, saved in inputs/locale1-gameplay.png. Source evidence is commit a1f83ad693375be941ffd6bb62ee46fe887c93c3, gameplay-final.png. The image model resynthesized pixels. These studies therefore do not prove additive blending, exact capture fidelity or readability across all locales. The brightest-locale test remains required in the HTML rebuild.

Built-in Codex imagegen produced these files. Prompts and source hashes are in provenance.json. No game code or assets changed.

## Shared files after the first choices

The combat session owns shared/type.css, shared/type-candidates.json, shared/roles.css and shared/personality.js. It also owns tuners/color.html and tuners/personality.html. These files are now delivered. Console HTML consumes the [shared interface](shared/README.md) and supplies its menu and big-moment presets.
