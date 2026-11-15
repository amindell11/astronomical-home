# Planet clouds and glow (#1481): evidence and tuner sources

Pictures

- `planet-before-after.png`: both shader-planet locales in engine, today's values against the owner's settings. Batch render of the locale prefab with the game's Main Camera prefab, so the grade and bloom are the game's.
- `planet-motion-4x.gif`: 16 in-engine frames about a second apart, played four times faster.
- `planet-motion-closeups.png`: the same sequence at 1:1, at about 1 s, 8 s and 16 s.
- `planet-looks-today-A.png`, `planet-looks-B-C.png`: the first pass shown to the owner on 2026-11-14. Offline port over in-game frames from #1359, not in engine.

Tuner (the page the owner set the values on)

- `planet_body.glsl`: the planet shader's maths as one GLSL fragment body, shared by the stills renderer and the page. It carries the drifting second layer (look B), which did not ship.
- `settings.json`: planets, knobs and looks. `owner` is the block the owner sent back.
- `render.py`: offline stills with moderngl. `bake.py` + `tuner.template.html`: builds the single-file tuner page.
- The port applies the game's grade (post exposure +0.52 stops, then ACES). It does not model bloom.

Batch capture

- `PlanetLookCapture.cs` + `planet_look_batch.ps1`: dropped into the slot for one coordinated batch run. It rendered the before stills, wrote the owner's settings into both `Planet.mat` files, then rendered the after stills and the sequence. `capture-log.txt` is its log; its `shaderTime` column reads 0 because the global is not readable after `Camera.Render()`.
