# Review round 2 — weapon glyphs

Same brief and criteria as round 1 (repeated at the end). Image 1 is the competitor's sheet the owner loves. Image 2 is my implementation of YOUR round-1 scheme: shared rear corner, no perimeter, 16-grid construction, your primitives for all seven weapons. Three columns: corner at your spec (0,2)→(0,0)→(3.5,0); a larger corner (0,4)→(0,0)→(6,0); marks alone. Sizes 96/32/24/16 px. Below the table: the glyphs under the ship's bars at ortho 7 (true pixels), 12 (shown 2x) and 18 (shown 3x); second strip row applies a 22 px minimum on-screen height.

Deviations from your spec, deliberately: the missile head, the ripper rounds and the charge brackets are built in screen space rotated onto the slash axis (your (u,v) versions sheared into flags/boots). Missile head is now an isosceles 4.2 long × 2.1 wide; rounds are 1.7 wide, 1.6 body + 1.3 nose; charge brackets are vertical screen-space ⟨ ⟩ at 0.75 width.

My own read of image 2: lasers, turbo and railgun finally have the competitor's rhythm. Missile head reads as a sliver; at 24 px missiles and rippers both become "dots on a line". Charge brackets are clunky. The spec-size corner nearly vanishes at 16 px; the larger corner reads as a family cue but starts to compete with the marks.

## Output contract (≤700 words, no preamble)

1. **Score each of the seven glyphs 1–5** against: guessable, distinct silhouette at 24 px, matches the competitor's sharp/aggressive quality. One line each.
2. **Corner:** spec size, larger, or something else (e.g. a longer base only). One sentence of reasoning.
3. **Top 3 fixes**, each as exact primitive changes on the 16-grid (I will implement verbatim). Priorities: missiles vs rippers separation at small size; charge brackets; anything that stops the family reading as one set.
4. **Owner-likeness verdict:** compared with image 1, would the owner say this set is at the competitor's level or above, with the unification they asked for? Give a confidence 0–100 and the single biggest remaining risk.

## Criteria (from round 1)
1 One family via shared construction. 2 Guessable from the weapon list. 3 Sharp, angular, aggressive, minimal; thin confident strokes, cut terminals. 4 Interesting, not procedurally uniform. 5 Readable to ~22 px. 6 Keep: lasers //, turbo ///, concussion burst, missile arrowhead+trail, railgun stripe+two guides, rippers three rounds. Art doc: abstract instrument marks, thin off-white, no literal guns/missiles, no heavy borders.
