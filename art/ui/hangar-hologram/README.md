# Hangar hologram

The hologram the hangar console projects: the selected ship as painted glass, over a projector cone.
The ship keeps its own paint, thinned and see-through, under thin cream lines. One resolve pass on
the hologram stage's camera draws it (`HologramResolve.shader`, run by `StageResolvePass`).
The three takes the owner chose between, the mock-ups and the in-game stills live on the
`evidence/hangar-hologram-look` branch: [design history](https://github.com/amindell11/astronomical-home/tree/38d31e3af7e6671db0c80893df3aceca7c921a62).
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
| `beamSourceRadius`, `beamHalfAngle`, `beamLength`, `beamLight` | `Assets/Settings/UI/HangarHologram.asset` |
| `streaks`, `sourceGlow`, `haze`, `drift` | `Projector Cone.mat`: `_Streaks`, `_SourceGlow`, `_Haze`, `_Drift` |
| `beamPixelsPerUnit` | Capture calibration only. Pixels per world unit at the lens, 96 for the supplied 1920x1080 plate. |

The beam uses the resolve material's `_LineColor`, shared with the hologram. Its light follows the
projector's flicker level. A finite source inside the lens feeds a narrow spread of fine streaks,
with a rounded fade at the lens and a soft end beneath the ship. The quad includes the glow below
the lens and rotates toward the settled frame's centre. Its dimensions depend only on the visible
beam settings; the ship's reach and cut-off stay separate.

Apply these values in the Editor to the existing material and profile. Changing source defaults
does not replace values already serialized in those assets.

| Profile field | Value |
|---|---|
| `beamSourceRadius` | 0.32 world units |
| `beamHalfAngle` | 16 degrees |
| `beamLength` | 1.45 world units |
| `beamLight` | 1.05 |

| Projector Cone material property | Value |
|---|---|
| `_Streaks` | 0.55 |
| `_SourceGlow` | 0.45 |
| `_Haze` | 0.16 |
| `_Drift` | 0.18 |

The existing quad prefab and material shader binding need no changes. The projector supplies
`_ConeColor`, `_ConeLight`, `_Level`, `_Apex`, `_QuadUnits`, `_SourceRadius`, `_Spread` and `_Length`
per renderer. The console's existing lens glow stays unchanged and is already part of the captured
plate. The tuner therefore adds only the beam. Changing `lineColor` in the tuner recolours the beam
and ship lines, but cannot recolour that baked lens glow. A fresh plate is needed to judge that change.

The page's inner lines come from buffers read offline, so they
sit a little differently from the pass's own edge finding. Its afterglow and ship-switch glitch are
not drawn, because both need motion.
