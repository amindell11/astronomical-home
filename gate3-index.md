# Outpost kit, gate 3: colour and light, 2026-11-14

The palette, the lights and the three states for the blockouts the owner accepted, judged over game captures at the size the game shows them.

| File | What it is |
|---|---|
| `gate3-colour-backdrops-sheet.png` | Every station on four locales, at the camera's widest zoom |
| `gate3-colour-states-sheet.png` | Open, in use and dark for each station |
| `gate3-colour-screen-gimbal-copper.png`, `gate3-colour-screen-kite-ice.png`, `gate3-colour-screen-drum-verdant.png` | A full 1280x720 screen at the closest zoom, one per station |
| `gate3-colour-window-size-compare.png` | The repair dock with the blockout's windows above and half-size windows below |
| `gate3-colour.json` | The settings block: hull colours, shading, lights, glow |
| `gate3-compose.py` | Colours the pass renders from the settings block and lays them over the captures |
| `gate3-stations.py` | The build script, now with pass renders and the small window style |
| `gate3-tuner-template.html`, `gate3-build_tuner.py` | The live tuner page and the script that bakes the renders into it |

## The size correction

Gates 1 and 2 showed a "game size" row that put a 29 u station at about 260 px. That figure came from a staged wide capture in PR #1344 and is wrong for play.

`Settings/Cameras/FlightCamera.asset` gives the flight camera an orthographic size of 15 at its closest and 25 at its widest. The view is 30 to 50 u tall. On a 720 px tall screen that is 24 to 14.4 px per unit, so a 29 u station is about 700 px across with the ship parked at it and about 420 px with the camera pulled back. The sheets here use those two sizes.

## How the colouring works

`gate3-stations.py ... passes` renders seven mask images per view: how much of each pixel is each of the six hull colours, how much of that is lit and how much half-lit, and how much is window, service light or prize. `gate3-compose.py` and the tuner page both rebuild the picture from those masks and `gate3-colour.json`, so changing a colour needs no Blender run. Outline is whatever a pixel's coverage leaves over.

```
blender -b --factory-startup --python gate3-stations.py -- <passes dir> passes gimbal kite drum
blender -b --factory-startup --python gate3-stations.py -- <passes dir> passes smallwin gimbal
python gate3-compose.py <passes dir> <captures dir> <sheets dir>
python gate3-build_tuner.py <passes dir> <captures dir> gate3-colour.json <out.html>
```

The captures are the four `asteroids-current20-game-*-flight.png` files on `evidence/asteroid-locale-concepts-20261106` at `e06e538731ff4570f133be6585b82cb5ba903e06`, saved as `<locale>-flight.png`.

## What the pictures are and are not

- The backdrops are real game captures at the closest zoom, with their asteroids and ship. On the widest-zoom sheets the capture is scaled down with the station.
- The stations are Blender previews laid over those captures. Shading bands, outline and bloom are stand-ins for `DrawnSurface`, `DrawnContour` and the game's post-processing. Nothing has been in Unity.
- "In use" is a proposal: the service lights flare. No code drives it yet.
