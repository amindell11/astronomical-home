# Hangar hologram

The hologram the hangar console projects: the selected ship as painted glass, over a projector cone.
The ship keeps its own paint, thinned and see-through, under thin cream lines. One resolve pass on
the hologram stage's camera draws it (`HologramResolve.shader`, run by `StageResolvePass`).
The three takes the owner chose between, the mock-ups and the in-game stills live on the
`evidence/hangar-hologram-look` branch: [design history](HISTORY_URL).
The PR that shipped it holds the rejected options.

## Files

| File | Role |
|---|---|
| `settings.json` | The tuned knobs: the tuner page's starting values. |
| `tuner.template.html` | The tuner page. It redraws the hologram from the layers with the game shaders' maths. |
| `hologram.py` | Turns a capture into `layers/`, and bakes the tuner page. |
| `capture/HologramBuffersProbe.cs` | The capture scenario: the hangar plate, and each offered ship's stage buffers in its own paint. |

`layers/` and `out/` are generated and ignored.

## Regenerate

```bash
cp art/ui/hangar-hologram/capture/HologramBuffersProbe.cs scratch/capture/
./scripts/agent_worktree_pool.sh run-tests <slot> -Mode PlayMode -TestFilter Tests.PlayMode.Recording.CaptureScenarioPlayModeTests -WithGraphics -Windowed -CaptureScenario HologramBuffersProbe
cd art/ui/hangar-hologram
uv run hologram.py --layers <slot path>/results/hologram-probe
uv run hologram.py --tuner out/tuner.html      # publish it with the Artifact tool
```

The capture needs a windowed editor (`game-capture` skill). The tuner's Copy settings block pastes over
`settings.json`. A page opened as `out/tuner.html#shot-Vanguard` shows only the 1920×1080 view of that
ship, for stills from a headless browser.

## In Unity

The knobs are material and profile values. The preview panel's hangar hologram target
(`Window/Preview Host`) draws the same materials live in the game.

| Knob | Lives in |
|---|---|
| `saturation`, `lift`, `fillLight`, `fillBody`, `pigmentKeep`, `rimLight`, `rimPower`, `lineLight`, `lineBody`, `creaseLight`, `paintEdgeLight`, `tint`, `lineColor` | `Hologram Resolve.mat`, under the same names |
| `lineWidth` | `Hologram Contour.mat`, `_ContourPixels`. The stage scales ink by 1200/768 at 1080p, so divide by 1.5625. |
| `glassLight` | `Hologram Glass.mat`, `_GlassLight`. The glass passes through the fill, so multiply by 1.6. |
| `edgeFeather` | `Hologram Layer.mat`, `_EdgeFeather` |
| `layerOpacity`, `flickerDepth`, `flickerRate` | `Assets/Settings/UI/HangarHologram.asset` |
| `scanlines`, `glow`, `haloStrength`, `haloReach`, `halation`, `textGlow`, `aberration`, `beamJitter`, `wear`, `signalBleed`, `bleedLength`, `warmth`, `coolTint` | the same asset's canopy settings |
| `coneLight`, `coneBase`, `coneTop`, `coneReach`, `coneFalloff`, `coneSoft`, `coneRays`, `lensGlow`, `coneColor` | `Projector Cone.mat`: `_ConeLight`, `_BaseWidth`, `_TopWidth`, `_Reach`, `_Falloff`, `_EdgeSoftness`, `_Rays`, `_LensGlow`, `_ConeColor` |

The page differs from the game in two places. Its inner lines come from buffers read offline, so they
sit a little differently from the pass's own edge finding. Its afterglow and ship-switch glitch are
not drawn, because both need motion.
