# Laser bolt: design history

The intermediate steps behind `art/vfx/laser-bolt/` on main, kept off main.
Shipped in amindell11/astronomical-home#767.

- `refs/`: `base_spark_pick.png` is the spark layer the user picked from the concept sketch; `explosion_sheet.png` is the drawn explosion used as the style reference. The main inspiration, a Star Wars blaster-bolt still, is not stored (third-party).
- `svg-concept/`: `gen.py` draws band, fringe, glow, muzzle, impact and spark layers in two colourways (writes next to itself). The long thin streak is a candidate for the railgun beam.
- `imagegen/`: `bolt_refine*` rounds with their provenance sidecars and prompts. Round 1 asked for a long streak; round 2 (short bolt, torn band edges, no floating filaments) gave `bolt_refine_r2_pro.jpg`, which the user flattened into `bolt_flat.webp` on main. Sidecar paths point at the files' original experiment folder.
