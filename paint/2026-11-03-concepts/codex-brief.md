# Codex brief: faction palette swaps (factions slice 2, issue #1329)

You are working in the git worktree `D:/amind/git/agent-8` (repo astronomical-home, a Unity game; project at `src/Asteroids3D`). Do the palette and material work below and nothing else. Do not commit; leave the changes in the working tree. Finish with a short report: every file created or edited, and any value you could not set as specified and why.

## Hard limits

- Create new material files and edit only the four `palettes.json` files named below. Edit no existing `.mat`, `.prefab`, `.fbx`, `.png`, `.asset`, `.unity` or `.cs` file. In particular never touch `TrialSector*.prefab`, `WaveDirector`, `PhaseSpineModule`, `GameHost`, any chassis prefab, or `ShipBaseRig.prefab`.
- No geometry, UV, mask or texture change. Do not run Blender, `ship_paint` or any art tool; do not touch anything under `art/ships/*/layers/`. New materials bind exactly the same textures (same GUIDs) as the material they are copied from.
- Each new material is a byte-for-byte copy of its source `.mat` except: `m_Name` (= the new file's base name, which stays the same as the source's) and the colour properties listed below. Each new `.mat` gets a `.mat.meta` copied from the source's meta with a fresh random 32-hex-digit `guid`. Each new folder gets a folder `.meta` (`fileFormatVersion: 2`, fresh guid, `folderAsset: yes`, `DefaultImporter` block like sibling folder metas in the repo).
- Unity stores material colours in sRGB (gamma) space. `palettes.json` stores linear sRGB. Convert with the standard sRGB transfer function (`srgb(v) = 12.92*v if v <= 0.0031308 else 1.055*v**(1/2.4) - 0.055`; inverse for hex -> linear). Write linear values rounded to 6 decimals.

## The picks (sRGB hex, from the concept the build session picked; "keep" = today's value)

Elite colour (shared by all elites): `#fc12d8`.
Glow colours: Shoal `#fe861a`, Veil `#a8f63b`, Bastion `#2bd2f7`.

### Crimson (`art/ships/crimson/palettes.json`; Unity `src/Asteroids3D/Assets/Visuals/Ships/Crimson/Hull paint.mat`)

| Region | shoal | veil | bastion |
|---|---|---|---|
| Silver | #f2a71a | #2d2e36 | #eeeeee |
| SilverStructure | #f2a71a | #2c2e35 | #234eb3 |
| Red | #171614 | #9fe535 | #234eb3 |
| RedStructure | #171614 | #9fe535 | #234eb3 |
| Frame | #171614 | #9fe535 | #234eb3 |
| Charcoal | #3c3e43 | #1d1e23 | #1f2441 |
| Rim | #3a3a3e | #1d1e23 | #ededed |
| Crimson canopy, FloorIndicator, YokeIndicator, NozzleRing | keep | keep | keep |

- `palettes.json`: add palettes `shoal`, `veil`, `bastion` with all 11 regions (keep = copy the `crimson` palette's value). `default` stays `crimson`.
- Unity: `Crimson/Hull paint.mat` holds all regions in one material: `_PaintRegionColor0..10` are the regions in sorted-name order (Charcoal, Crimson canopy, FloorIndicator, Frame, NozzleRing, Red, RedStructure, Rim, Silver, SilverStructure, YokeIndicator). Confirm that order against today's values (each is srgb of the `crimson` linear value, approximately; Frame is the one hand-tuned outlier) before relying on it. Write `Crimson/Materials/<palette>/Hull paint.mat` for each of the three palettes: set `_PaintRegionColorN` = the pick in sRGB for changed regions; keep today's `_PaintRegionColorN` value exactly for "keep" regions. Leave every other property as in the source.

### Valis (`art/ships/valis/palettes.json`; Unity source folder `Valis/Materials/jade-iris/`)

| Region | shoal | shoal-elite |
|---|---|---|
| Ivory | #f1a619 | #f1a619 |
| Cool gray | #d28c0c | #d28c0c |
| Edge | #d78c08 | #d78c08 |
| Graphite | #171412 | #171412 |
| Lavender | #363737 | #fc12d8 |
| Engine | #39393d | #39393d |
| Canopy | keep | keep |

- `palettes.json`: add `shoal` and `shoal-elite` (Canopy = the `jade-iris` value). Match the file's existing formatting. `default` stays `jade-iris`.
- Unity: write `Valis/Materials/shoal/` and `Valis/Materials/shoal-elite/`, each with the six region materials copied from `jade-iris/` (`Ivory`, `Cool gray`, `Lavender`, `Graphite`, `Edge`, `Engine`). Set `_PaperColor` = the pick (sRGB). `_PaintLightColor` is one constant across today's Valis materials: keep it. `Lavender` carries the accent emission (`_EmissionColor`, HDR, alpha 2.2): in `shoal` set it to the Shoal glow `#fe861a`, in `shoal-elite` to the elite `#fc12d8`, each scaled so its luminance matches today's Lavender `_EmissionColor` luminance; keep alpha 2.2. Leave the canopy materials (`Valis/Canopy/`) alone.

### Nightshade (`art/ships/nightshade/palettes.json`; Unity source folder `Nightshade/Materials/nightshade/`)

| Region | veil | veil-elite |
|---|---|---|
| Nightshade Midnight | #1c1d22 | #1c1d22 |
| Nightshade Violet | #2c2e36 | #2c2e36 |
| Nightshade Plum | #9ee636 | #fc12d8 |
| Nightshade Glass | #a1e535 | #a1e535 |
| Nightshade Edge | #1e1f24 | #1e1f24 |
| Nightshade Recess | keep | keep |

- `palettes.json`: add `veil` and `veil-elite`; `default` stays `nightshade`.
- Unity: write `Nightshade/Materials/veil/` and `Nightshade/Materials/veil-elite/`, each with the six materials copied from `nightshade/`. Set `_PaperColor` = srgb(linear pick) and `_PaintLightColor` = srgb(lift(linear pick, L)) with `lift(c, L) = c + (1 - c) * L`, where L is `material.light_lift` from `art/ships/nightshade/paint.json`. First check that this rule reproduces today's `nightshade/` materials (within 0.002); if it does not, report it and keep today's `_PaintLightColor` instead. Keep `_EmissionColor` as today (green indicator emission on Edge and Recess). Leave `Nightshade/Canopy/` and `Nightshade/Breakup/` alone.

### Bulwark (`art/ships/bulwark/palettes.json`; Unity source folder `Bulwark/Materials/plum-bone/`)

Bulwark's Unity materials are not one-per-palette-region. `Bulwark Bone`, `Bulwark EdgeCoat` and `Bulwark Flap` are masked: `_PaintRegionColor0` = substrate, `_PaintRegionColor1` = coat (bone today), and `_PaperColor` equals the substrate. The others are solid `_PaperColor`.

| Unity material | bastion | bastion-elite |
|---|---|---|
| Bulwark Bone (masked) | substrate #1c2547, coat #2450b8 | same |
| Bulwark EdgeCoat (masked) | substrate #1c2547, coat #2450b8 | same |
| Bulwark Flap (masked) | substrate #eeeeee, coat #eeeeee | substrate #fc12d8, coat #fc12d8 |
| Bulwark Guard Plum | #eeeeee | #fc12d8 |
| Bulwark Armor Charcoal | #1c2547 | #1c2547 |
| Bulwark Structure | #1c2547 | #1c2547 |
| Bulwark Metal | #e6e6e6 | #e6e6e6 |
| Bulwark Glass | not copied (canopy keeps `plum-bone/Bulwark Glass.mat`) | same |

- Unity: write `Bulwark/Materials/bastion/` and `Bulwark/Materials/bastion-elite/` with the seven materials above copied from `plum-bone/`. Set `_PaperColor` (and region colours for the masked three) as above. `_PaintLightColor` = srgb(lift(linear paper, L)): derive L from today's plum-bone materials (it should come out near 0.2; check `art/ships/bulwark/paint.json` `material.light_lift` too) and report the L you used. `_EmissionColor` is today a lilac HDR colour on every Bulwark material (the owner-authored DetailEmission inserts): in `bastion` set it to the Bastion glow `#2bd2f7` scaled to today's luminance; in `bastion-elite` use the Bastion glow on all materials except `Bulwark Guard Plum` and `Bulwark Flap`, which get the elite `#fc12d8` at the same luminance.
- `palettes.json`: add `bastion` and `bastion-elite` with all 15 regions. Map by today's role: every region whose `plum-bone` value is today's plum (Plum, Bone, Plum Secondary, Bone Secondary) = substrate, except Flap/Flap Secondary = the flap colour and Guard Plum = the guard colour; Guard Bone (today's bone, the coat) = coat; Structure, Structure Secondary, EdgeCoat = structure; Armor Charcoal = armor; Metal = metal; Glass, Glass Secondary = keep. Match the file's formatting; `default` stays `plum-bone`.

### Engine flames (`src/Asteroids3D/Assets/Visuals/Ships/_Shared/`)

Write `ThrusFlame_Shoal.mat`, `ThrusFlame_Veil.mat`, `ThrusFlame_Bastion.mat` and `ThrusFlame_Elite.mat`, each a copy of `ThrusFlame_Blue.mat` (URP Particles/Unlit). Set `_BaseColor` and `_Color` to the glow colour (sRGB, alpha 1): Shoal `#fe861a`, Veil `#a8f63b`, Bastion `#2bd2f7`, Elite `#fc12d8`. Set `_EmissionColor` to the same colour (it is unused while `_EmissionEnabled` is 0; keep that 0). The particle system's start colour multiplies `_BaseColor`; the skin applier will set it to white, so do not compensate for it.

## Self-check before reporting

Write a throwaway Python check (keep it outside the repo, e.g. in your temp dir) that parses every new `.mat`, confirms its texture GUIDs equal its source's, confirms every non-colour line is identical to the source, and prints each changed colour property beside the pick it came from. Run `git -C D:/amind/git/agent-8 status --short` and confirm only the four `palettes.json` files are modified and everything else is new.
