# Weapon glyphs: design history

The intermediate steps behind the weapon glyphs shown beside each ship's status bars
(arc amindell11/astronomical-home#994), kept off main. Parked 2026-10-19 with the owner's
picks still open; the issue carries the current state and what is left to decide.

Order of events, because the lesson is in the order:

1. `procedural/` — nine rounds of glyphs drawn by rule (PIL, a unit grid, cut-terminal
   strokes). `glyphs.py` … `glyphs5.py` are the first session's rounds (bare marks, disc and
   ring frames, broken polygons, solid negative space); `glyphs6.py` … `glyphs7.py` and
   `cutstroke.py` / `sheetkit.py` / `present.py` are the second session's (7/8/9-gon frames, far-
   readability study, then the codex "shared rear corner" scheme). Every `out/` sheet is
   named by round. The owner rejected these as mechanical: "random lines", then "they suck".
2. `codex-review/` — four review rounds with the codex CLI as reviewer: `packet_r*.md` is
   what it was sent, `review_r*.md` what it said. Its round-1 construction scheme is what
   `glyphs7.py` implements; by round 4 it scored the set 82/100 and recommended shipping the
   marks without a corner device. The owner disagreed with the whole direction.
3. `imagegen/` — five rounds through `art/tools/imagegen/` (Nano Banana), each image with its
   provenance sidecar and the prompt it was given. This is where the owner's favourites came
   from. `r1_*` used reference images (the hangar mockup leaked its whole composition into
   `r1_B_nb2` and `r1_C_nb2`); `r2_*` used words only; `r3_*` put "readable at a distance" first
   (owner: "too tribal"); `r4_*` asked for avionics symbology with constant-width strokes
   (owner: the sci-fi feel came back); `r5_*` fed the owner's four favourite crops back as style
   references and added the charge-bar language, the storm rippers, a missile study and a
   charge/railgun study. The `*_gamescale.png` files are true downscales to ~30 and ~17 px.
4. `refs/` — the competitor sheet the owner loved (`competitor_cuts.png`, from another AI) and
   the four crops the owner picked as favourites (`fav_*.png`). The design-vault mockups used as
   mood references live on main under `doc/design/assets/`.

What the history says about method: generating by rule and judging by model produced
coherent but characterless sets; generating with the image model and letting the owner judge
produced the sets the owner liked. The procedural generator's remaining job is cleanup:
tracing the picks onto one grid, one stroke weight, one terminal rule.
