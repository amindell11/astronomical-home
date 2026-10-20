Scores: **guessable / distinct silhouette at 24 px / competitor’s sharpness**, judging the spec-corner column.

- **Lasers: 5 / 4 / 4** — immediate identification; good stagger and cut terminals.
- **Turbo Lasers: 5 / 3 / 4** — convincing escalation, but approaches railgun’s footprint.
- **Charge Lasers: 3 / 3 / 2** — inward brackets pinch the slash into a cramped hourglass.
- **Railgun: 3 / 3 / 4** — strong stripe, though equal-length guides produce a barcode silhouette.
- **Missiles: 4 / 2 / 3** — understandable enlarged; narrow head loses its identity at 24 px.
- **Rippers: 2 / 2 / 3** — pointed rounds are plausible, but their single-file arrangement duplicates missiles.
- **Concussion: 4 / 4 / 2** — recognizable burst; circular center and short, chunky ticks feel softer than the competitor.

**Corner:** Use `[(0,2),(0,0),(4.5,0)]`, width `0.8`: extending only the base strengthens the shared cue without making every glyph sit on the larger corner’s conspicuous pedestal.

**Top 3 fixes**

Coordinates use your existing `B`, `P`, `along`, and `round_` conventions; screen offsets below are measured in unscaled 16-grid units.

1. **Separate missiles and rippers through arrangement, not finer internal detail.**

   **Missiles:** Replace the filled triangle with an **open arrowhead**, one continuous polyline through:
   `along(4.8,8,0,-2.2)` → `along(4.8,8,3.6,0)` → `along(4.8,8,0,2.2)`.
   Use perpendicular stroke width `1.0`, a sharp miter at the apex, and butt-cut free ends. Replace both trail dashes with **one** `B(4.8,2.8,6.8,1.0)`. This gives a broad directional head and a readable trail.

   **Rippers:** Replace the inline rounds with three `round_` primitives anchored at **`(1,8)`, `(6,8)`, `(5.5,3)`**. For all three use `halfw=0.9`, `body=1.2`, `nose=1.8`. Preserve their common slash-axis orientation. Two forward rounds plus one rear round give a triangular footprint that remains different from missiles even when the noses blur.

2. **Open the charge brackets outward and stagger them.**

   Keep `B(4.5,4.4,8.6)`. Delete both existing brackets. Around screen-space center `C=P(4.5,6.5)`, draw these polylines using screen offsets:
   
   - Left: `(-2.2,-3.8)` → `(-3.6,-0.6)` → `(-2.7,2.6)`.
   - Right: `(2.7,-2.6)` → `(3.6,0.6)` → `(2.2,3.8)`.

   Both use width `0.8`, sharp miter joins, butt-cut terminals. These are outward **〈 〉**, with the left bracket raised: more breathing room, longer cuts, less mechanical symmetry.

3. **Make concussion share the set’s angular, linear construction.**

   Keep its center at `P(4,6)` and its five existing ray angles. Replace the circular center with a filled screen-space diamond having offsets `(0,-0.9)`, `(0.9,0)`, `(0,0.9)`, `(-0.9,0)`. Change every ray to `r0=2.1`, `r1=4.6`, width `0.8`, butt ends. Longer, thinner rays should read as an energetic burst instead of scattered blocks.

**Owner-likeness verdict:** **Not yet; 85/100 confidence in that assessment.** The slash family is close, and the corner supplies recognizable unity. The biggest remaining risk is that this unity reads as a repeated attachment while the individual weapon silhouettes still lack the competitor’s distinctive, confident shapes.