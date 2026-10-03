# Laser bolt concept (parked 2026-09-28)

Goal: replace the laser projectile's flat quad with a layered particle that
matches the drawn explosion style. Main inspiration: `refs/starwars_bolt.png`.

## Where it stands

- **Favourite: `bolt_refine_r2_pro.jpg`.** A short compact bolt (lasers are
  bolts, not beams) with torn, chunky cel-band edges, a white core about half
  the width, and bloom only outside the body. Not yet approved as final.
- `refs/base_spark_pick.png` is the shape language that was picked: the
  "impact spark" layer from the SVG concept.
- Round 1 (`bolt_refine_*.jpg`, prompt `bolt_refine.prompt.txt`) asked for a
  long streak and added floating lightning squiggles; both were rejected.
  Round 2 (`*_r2_*`) fixed the proportions and banned the squiggles.
- `svg-concept/`: the first procedural pass (`gen.py` regenerates it). The
  long, thin streak body is **worth revisiting for the railgun beam**, not the
  laser. `contact_sheet.html` builds its animated/in-context sections with
  JS, so open it in a browser; the in-app file viewer blocks the script.

- **`bolt_flat.webp`** is the user's flattened cleanup of the r2 pro bolt, the
  current base.
- **`crackle/`**: `crackle.py` animates `bolt_flat.webp` into a 6-frame
  shape-change flipbook (no lightning filaments; they won't read at bolt
  size). Frame 0 is the drawing. Other frames warp each band's edge with a
  backward-swept sawtooth and flicker the tail length, while the core and the
  nose hold. Outputs in `crackle/out/`: per-frame bodies, a separate glow,
  `body_sheet_1x6.png`, GIF previews and `ingame_frames_3x.png`. Reads well at
  ~150 px on screen; at ~60 px mostly the tail flicker reads.
  Tunables are at the top of the script. `SMOOTH = "tween"` bakes `TWEEN`
  in-between shapes per key (interpolated warp numbers, crisp edges);
  `"crossfade"` keeps only the keys, for Unity's flipbook frame blending.
- **`crackle/tuner.html`** is the same algorithm ported to the browser, with
  live knobs; published as an artifact
  (https://claude.ai/artifact/3vJKehFQhymyHgZWJu72nm). Its "Copy settings"
  block pastes over the tunables in `crackle.py`. Its seed doesn't match the
  script's (different random generators).

## Next steps when resumed

1. Review the crackle flipbook (frame count, amplitude, whether frame 0, the
   calmer drawn frame, stays in the loop).
2. Trace the variants into clean SVG band layers (core / hot / mid / rim) so
   the colour can come from a ramp shader, which allows side colours. Blue
   reads weaker than red against the blue nebula.
3. Build the Unity particle on `Prefabs/Weapons/Laser.prefab`. `ChargeBolt`
   and `RipperSlug` share `LaserVisual`; decide whether they get variants.
4. Only then: muzzle flash and impact (drafts in `svg-concept/`).

Open unknowns: whether the laser raises a hit event an impact effect can hook
into; SVG→PNG rasterizer (none installed; Unity Vector Graphics or cairosvg).
