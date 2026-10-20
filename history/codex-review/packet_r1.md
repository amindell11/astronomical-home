# Design review: weapon glyphs for a space-combat HUD

You are reviewing icon design, not code. Be blunt. The owner looked at my latest set and said "I think they suck", and I agree more than I'd like. Your job is to tell me *why* in construction terms and to give me ONE concrete construction scheme I can implement procedurally (I draw these with a script: lines, polygons, arcs on a unit grid, rendered to PNG at 96 px and at 22–35 px game scale).

## Context

A 2.5D top-down space shooter. Beside every ship's shield/hull bars (thin bars over a blue nebula), a row of 1–2 small glyphs shows which weapons the ship carries. On screen a glyph is ~30 px tall at default zoom and must still be guessable at ~22 px. Seven weapons:

- Lasers (fast straight beam, heat), Turbo Lasers (faster variant), Charge Lasers (hold to charge, release)
- Railgun (charged hitscan, one heavy shot)
- Missiles (locked, guided, slow, big hit)
- Rippers (ballistic autocannon, many small rounds, reload)
- Concussion (a dropped charge / mine with a blast)

The game's art-direction doc says for UI symbols: "flat, minimal and precise: thin off-white strokes... symbols should suggest advanced spacecraft instruments: interrupted arcs, offset strokes and detached geometric marks. They need distinct silhouettes and consistent meanings; literal guns, missiles, heraldic shields and X markers undermine the distant-galaxy feeling. Avoid bulky glowing panels, ornamental circuitry, heavy borders and excessive labels."

## The owner's criteria, as I understand them (from six rounds of feedback)

1. **One family.** A shared construction or bounding logic so all seven read as one set at a glance. Earlier bare marks were rejected as "random lines"; the owner asked for "some type of bounding shape uniting them".
2. **Guessable.** Knowing the weapon list, a player matches each glyph to its weapon without a legend. An earlier abstract set was rejected: "I wouldn't even be able to guess what each one is literally knowing every single weapon."
3. **Sharp, angular, aggressive, minimal.** The owner loved a competitor's sheet (attached, image 1: "Cuts — angular · interrupted · offset") for "the sharp angles and aggressive lines" and called the style "amazing", while saying it was "a bit random" and needed unification.
4. **Interesting and powerful**, not procedurally uniform. "These could be so much more interesting and powerful."
5. **Readable far away.** Must survive to ~22 px with distinct silhouettes.
6. **Approved pieces to keep** (don't redesign these ideas, refine them): Lasers = two parallel slashes; Turbo = three slashes (both "great"). Concussion = centre dot with radiating short ticks (a burst: "looks great"). Missiles = a solid arrowhead on the slash axis with a dashed trail ("good"). Railgun = one big stripe flanked by two thinner full-length edge lines (owner's request). Rippers = three small projectiles streaming up the slash axis, drawn large ("much better but need to be a lot bigger").
7. The owner tried a broken dodecagon frame and then asked for 7/8/9 sides, and asked whether a solid polygon with the icon as negative space would help readability. None of these frames has satisfied them.

## My honest critique of my own latest set (image 2)

- The broken-polygon frame is a uniform dashed ring. It adds visual noise and a generic "sci-fi badge" feel without adding identity; at 24 px it eats the mark.
- Putting marks inside a frame forced them smaller, killing the thin confident line quality the owner liked.
- My strokes are rectangles with square ends. The competitor's strokes have cut terminals (ends sliced at an angle or horizontally), varied lengths, and offset placement that gives rhythm. Mine are all the same length and centred, so they look machine-generated.
- Compositions are centred and symmetric; the competitor's are asymmetric and feel composed.
- The solid negative-space variant is a blob.
- Railgun's three diagonals collide with Turbo at small size. Rippers' triangles look like debris. Concussion in a ring looks like a snowflake. Missile looks like a mouse cursor.

## What I want from you (output contract)

1. **Verdict** on image 2 against the criteria: the three biggest failures, in construction terms.
2. **What image 1 gets right** that image 2 doesn't, stated as rules I can code (stroke terminal shape, stroke length ratios, angle, spacing grid, how enclosures are used, where asymmetry comes from).
3. **ONE unification scheme** that satisfies criterion 1 without a heavy container. Options I've considered: a shared slash axis plus a recurring short "baseline" tick; a partial sheared frame (two edges of a parallelogram, open toward the muzzle); corner cuts; a common baseline bar. Pick one or propose better, and give a concrete spec: grid (e.g. 8×8 units), stroke width, slash angle, terminal rule, and the construction of each of the seven glyphs as a short list of primitives. Keep the approved ideas in 6.
4. **Small-size prediction:** which two glyphs would fail first at 22 px under your scheme and what to change for them.

Keep it under ~900 words. No preamble.
