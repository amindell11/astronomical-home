# Asteroid evidence

Per-asset scratch for the drawn asteroid field (the ten `DrawnField` shapes under
`src/Asteroids3D/Assets/Visuals/Environment/Asteroids/`). Nothing here merges to main.

## producers/drawn-field/

Saved 2026-10-03 from pool slot 4's gitignored `scratch/capture/` (issue #869). These
are the only copies of the scripts that generated the field asteroids. They carry
hard-coded `D:/amind/git/agent-4` paths and are reference, not runnable tools.

| File | What it does |
| --- | --- |
| `author-field.py` | Base generator: reads `field-sources/Asteroid<N>_LOD0.json`, builds bowls and ridge strokes, bakes `Asteroid<N>Normal.png`, exports the two FBX per shape. |
| `field-sources/*.json` | Mesh dumps of the ten source asteroids (and LOD0), the generator's input. |
| `hatch-field.py` | Adds the fine cross-hatched scuffs and re-exports `Asteroid<N>Drawing.fbx`. |
| `densify-field.py`, `bolden-field.py` | Later passes over the same drawing meshes (more ridge lines, heavier ink). |
| `author-surface-drawing.py`, `bake-scuff-relief.py` | Single-rock study that preceded the field: surface drawing and scuff relief normal bake. |
| `sculpt-rock.py`, `sculpt-fractured-rock.py`, `repack-stone.py`, `smooth-all.py` | Sculpt and UV helpers from the form and fracture studies. |
| `package-*.py`, `update-page.py`, `update-pr.py` | Review-page packaging for each study round. |
| `inspect-*.py`, `*-probe.py`, `check-contour.py`, `look-locale.py`, `palette-2.py`, `isolate-texture.py` | One-off inspection probes. |
| `notes/` | Briefs, style spec, results and PR drafts for each round, plus the review pages. |
| `unity-helpers/` | Unity-side capture scenarios, material savers and shader probes used with the scripts. |

The commit history of the authored assets themselves is on `task/crimson-drawn`
(kept as tag `archive/task/crimson-drawn`).

## studies/drawn-study/

The Unity side of the single-rock drawn study, removed from main by #921: the study FBX, textures and materials of `Assets/Visuals/Environment/Asteroids/DrawnStudy/`, `.meta` included. Its README has the revive recipe.
