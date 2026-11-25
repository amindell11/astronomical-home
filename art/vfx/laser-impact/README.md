# Standard laser impact

`source.png` and `settings.json` are the approved drawing and 340 ms motion settings.
`motion.js` renders that animation. The source approval and motion approval are on
[the hit-effects issue](https://github.com/amindell11/astronomical-home/issues/792#issuecomment-6328585947).

## Bake

With Node and `playwright` available, run:

```text
node art/vfx/laser-impact/bake.cjs <output-directory>
```

`CHROMIUM_EXECUTABLE` may select an installed Chromium binary. The required output
directory receives `LaserImpact.png` and `bake.json`. A single stdout JSON record
names both absolute paths and the atlas layout; failure exits nonzero.
The atlas has 20 frames in five columns and four rows, with the contact at each
cell's center and the fan pointing along negative local Y.

In a coordinated Unity editor, choose **Tools > Art > Bake laser impact** and select
that output directory. For coordinated batch execution, pass the output directory
returned by `bake.cjs`:

```text
-executeMethod Combat.Projectiles.Visual.LaserImpactBaker.Bake -laserImpactExport <output>
```

The baker imports the texture, creates the pooled particle
prefab and material, and wires the standard laser. Turbo and ripper authoring lives in
`../turbo-ripper/`.
The initial visible fan length is 1.2 world units. Tune the effect prefab's particle
size in flight; the browser's 48-pixel preview did not establish a world scale.
The Unity prefab keeps the source motion timing and fades its opacity from 60 ms
to zero at 280 ms, following the first flight feedback.
