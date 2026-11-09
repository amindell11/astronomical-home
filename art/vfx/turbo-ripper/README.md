# Turbolaser and ripper visuals

`draw.js` is the native ripper source. `settings.json` owns its warm palette and sizes, plus the turbo hue and narrower spread. `bake.cjs` also reads the unchanged source and motion in `../laser-impact/`.

The ripper projectile uses the [approved pale gold needle fleck](https://github.com/amindell11/astronomical-home/blob/6e6b69feef17175c38b3225f3748e6c857ae8b10/history/14-ripper-needle-fleck/selected-source.json). Its 22 reference pixels span 1.1 world units. The narrow glow scales with the bake resolution. Ripper keeps its simpler impact. Turbo keeps the first laser's length and timing with a 25 percent narrower fan. Its magenta palette follows a capture of the actual turbo projectile through the game's camera and postprocessing. The decisions and flight evidence are on [the hit-effects issue](https://github.com/amindell11/astronomical-home/issues/792#issuecomment-6704847365).

## Regenerate

With Node and Playwright available, run `node art/vfx/turbo-ripper/bake.cjs <output-directory>`. `CHROMIUM_EXECUTABLE` may select an installed Chromium. Success writes three textures and `bake.json`, then emits one stdout JSON record containing their absolute paths. Failure exits nonzero.

Each impact atlas has twenty 256-pixel frames in five columns and four rows, a centered contact, and its fan along negative local Y. The projectile texture points toward positive local Y. The manifest gives each texture filename, impact layout and lifetime, visible fraction and initial world length. `fadeInTexture` tells the importer whether particle opacity must stay constant.

Run the editor authoring step through the Unity access coordinator:

```text
-executeMethod Combat.Projectiles.Visual.TurboRipperBaker.Bake -turboRipperExport <output-directory>
```

The baker creates impact variants of `LaserImpact`, wires both weapons and replaces the ripper mesh with a visual child. Turbo keeps the base particle's 60 to 280 ms opacity fade; its atlas does not include that fade. Ripper's 180 ms fade is already in its atlas. Its particle opacity stays constant. The slug uses the existing projectile shader with one fixed frame, so the existing distance fade still applies.
