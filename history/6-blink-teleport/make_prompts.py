from pathlib import Path

here = Path(__file__).parent

BASE = """Use your built-in image_gen tool to create ONE NEW 16:9 image, then copy the generated PNG into the current working directory as out.png and print its absolute source path. Do nothing else: no code, no other files. Three images are attached in order: Image 1, Image 2, Image 3.

Reference roles:
- Image 1 is the OBJECT reference: the Crimson, a spiky assault fighter, seen from directly above, nose up.
- Image 2 is the game's STYLE FRAME: copy its painterly blue-violet nebula background and its hand-painted, ink-outlined illustration style. Do NOT copy its effects.
- Image 3 shows the chosen EFFECT MATERIAL: copy only the look of the streaks behind the ship in its middle frame: flat, angular, translucent streaks with a faint tint and a thin crisp pale edge, mostly see-through so the nebula shows through. Ignore its ship and layout, and do NOT copy its trail arrangement.

Ability: BLINK, a TELEPORT, not a dash. After a quarter-second wind-up the ship vanishes and instantly reappears a short distance ahead, keeping its speed. Nothing travels between the two points: NO trail, NO streak or line connecting departure and arrival, NO motion blur. The effect happens only AT the departure point and AT the arrival point. The wind-up must be a clear warning that other players can read in a quarter of a second.

SHIP FIDELITY (most important rule): wherever the ship appears it must look exactly like Image 1: mostly mid-grey armour plates, black recesses, thin red trim lines, a pale tan cockpit, outward-pointing red spikes on both sides. Do not repaint, smooth, restyle or redesign it. The effect is a separate layer.

STRICT: NO fire, NO flames, NO laser beams, NO white-hot cores, NO neon glow, NO electric sparks, NO smoke, NO glass objects. All effect shapes are the translucent angular streaks of Image 3, tinted strong rose red, clearly red and not purple.

Layout: four frames left to right, labelled small underneath: "Wind-up", "Vanish", "Arrive", "After". The ship points straight up and is the same size wherever it appears; in Vanish, Arrive and After show both the departure point (lower) and the arrival point (higher) in the same frame. Below, a "Small-size check" strip repeats Wind-up and Arrive at one fifth size, because in play the ship is only about 70 pixels tall; the effect must still read there, so its streaks are few and broad, never fine slivers.

"""

VARIANTS = {
    "converge": """Concept: IMPLODE AND BURST.
Wind-up: a ring of eight broad angular streaks surrounds the ship a little way out, all pointing inward at it and closing in.
Vanish: the ship is gone; at the departure point the inward streaks have collapsed to a small tight knot of crossed streaks.
Arrive: at the arrival point the ship has reappeared at the centre of a burst of eight broad angular streaks pointing outward from it, flying away radially.
After: the outward burst at the arrival point fades as the ship flies on; the knot at the departure point has gone.""",
    "pinch": """Concept: PINCH SHUT AND SPLIT OPEN.
Wind-up: two tall angular streaks close in on the ship from its left and right sides like closing doors, squeezing it.
Vanish: at the departure point, only a single thin vertical sliver remains where the ship was, the two streaks pressed together.
Arrive: at the arrival point a vertical sliver splits open sideways, two tall angular streaks swinging outward left and right, revealing the ship between them.
After: the ship flies on; the two streaks at the arrival point fall away to each side and fade.""",
    "slats": """Concept: SHUTTER SLATS, like a blink of an eye.
Wind-up: four or five horizontal angular streaks, each wider than the ship, appear stacked across the ship like the slats of a closing shutter, starting to cover it.
Vanish: at the departure point the slats have slid shut over where the ship was, then slide apart sideways, revealing empty space.
Arrive: at the arrival point the same horizontal slats slide open sideways, revealing the ship behind them.
After: the ship flies on; the slats at the arrival point fade out.""",
    "marker": """Concept: DESTINATION MARK.
Wind-up: while the ship is still at the departure point, a few broad angular streaks form a hollow outline of the ship's silhouette at the arrival point ahead, a clear marker showing where it will appear; the ship itself starts shedding a few short streaks.
Vanish: the ship is gone from the departure point, which shows only a fading cluster of angular streaks in the ship's outline.
Arrive: the ship appears exactly inside the marker outline, which snaps tight around it and breaks into a few streaks flicking outward.
After: the ship flies on; a few streaks fade at both points.""",
}

for key, body in VARIANTS.items():
    d = here / f"run_{key}"
    d.mkdir(parents=True, exist_ok=True)
    (d / "prompt.txt").write_text(BASE + body, encoding="utf-8")
print("ok")
