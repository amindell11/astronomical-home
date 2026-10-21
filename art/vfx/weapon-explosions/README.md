# Weapon explosions

The missile uses a compact warm burst and short smoke curls. The mine uses a blue
ignition and a filled disc whose edge follows the existing concussion wave.

## Source and controls

`source/missile.png` and `source/mine.png` are the approved drawings, retained
unchanged. The earlier sketches and reference roles live in the
[design history](https://github.com/amindell11/astronomical-home/tree/c163dc955058029bedcb45b03cafc04cf4fe8a39/history/refinement-01).
The approval and original-speed ruling are on
[the effects issue](https://github.com/amindell11/astronomical-home/issues/789#issuecomment-6323145029).

`effects.js` extracts the painted layers, separates warm fire from neutral smoke,
and fits the mine's overhead rim to a circle. The source-layer view exposes that
parse. The same renderer drives the preview and texture export. Canvas resampling
interpolates premultiplied color; exported PNGs use straight alpha.

`settings.json` holds the shared timing and visual controls. The tuner exposes
missile spread, smoke opacity, mine fill and mine edge. Its playback-speed control
is for inspection and does not change exported timing. Copy its exported settings
back to `settings.json` before rebaking.

## Regenerate

The baker needs Node, the `playwright` package and Microsoft Edge. Use the configured
workspace Node dependencies, or make `playwright` available on `NODE_PATH`.

```text
node art/vfx/weapon-explosions/bake.cjs [settings.json] [output-directory]
```

Both arguments are optional and resolve to this folder's settings and `out/`.
The script writes a standalone `preview.html`, texture PNGs, source-layer and
timeline captures, animation frames and the settings used. Its single stdout JSON
record names the output directory, textures and frame count. Failure exits nonzero.
The renderer requires no network requests.

After exporting, run **Tools > Art > Bake weapon explosions** in a coordinated
Unity editor for this worktree. `WeaponExplosionBaker.Bake` imports the textures,
updates the effect materials and prefabs, and assigns the two projectile effects.
It reads the exported settings alongside the textures.

The mine mesh is in the XY game plane, with its painted rim at 90% of the quad's
half-width. Its child scale accounts for that padding; the existing wave visual
scales it with `ConcussionWave.Radius`. The wave's gameplay values stay in its prefab.
