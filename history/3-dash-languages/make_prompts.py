from pathlib import Path

here = Path(__file__).parent

BASE = """Use your built-in image_gen tool to create ONE NEW 16:9 image, then copy the generated PNG into the current working directory as out.png and print its absolute source path. Do nothing else: no code, no other files. Two images are attached in order: Image 1, Image 2.

Reference roles: Image 1 is the OBJECT reference: the Vanguard fighter seen from directly above, nose up. Keep its exact silhouette, white-and-orange paint, grey panels and two round cyan lenses; do not redesign or repaint it. Image 2 is the game's STYLE FRAME: copy its painterly blue-violet nebula background and its hand-painted, ink-outlined illustration style for the whole picture. Do NOT copy the effects in Image 2.

Subject: three frames, left to right, of the Vanguard performing DASH, a sudden short forward lunge, in a top-down space game. The ship points straight up and is the same size in each frame. Labels small underneath: "Launch", "Dashing", "Fading". Below, a "Small-size check" strip repeats Dashing at one fifth size, because in play the ship is only about 70 pixels tall; the effect must still read there.

STRICT: NO fire, NO flames, NO glowing laser blades, NO white-hot cores, NO neon glow, NO electric sparks. The engines show only their normal small exhaust. The effect is described below and nothing else:

"""

VARIANTS = {
    "ink": "INK SPEED LINES, like a manga panel: crisp tapered brush strokes in off-white and pale blue-grey ink, the ink colour of the ship outlines, streaming back from the ship's edges along its path, thick at the root and tapering to points; a few parallel speed lines further out. Launch: short strokes bunch tightly behind the ship. Dashing: long strokes, the ship slightly blurred into them at its tail. Fading: strokes break into short dashes and thin out.",
    "smear": "ANIMATION SMEAR, the classic hand-drawn animation technique: during the dash the ship's own painted silhouette is stretched backward along its path into a long smear, its colours (white, orange, grey) dragged out into streaky painted bands, plus one or two partial overlapping repeats of the ship's outline behind it, all in the ship's own paint colours, no added light. Launch: the tail starts to stretch. Dashing: the full smear, about twice the ship's length. Fading: the smear snaps back, leaving two thin painted streaks.",
    "vapour": "VAPOUR, like a jet breaking the sound barrier, drawn in a cel-shaded animated-film style: Launch: a puffy cone of soft white vapour bursts around the ship's rear in round cartoon cloud puffs with two-tone pale grey shading. Dashing: the ship has left the puffy ring behind; two thin wavy white contrails stream from the wingtips. Fading: the cloud ring drifts apart into a few round puffs.",
    "ripple": "SPACE DISTORTION: the dash bends space itself. Launch: a clear, colourless lens-like ripple ring forms at the ship's tail, visible only because it warps and magnifies the nebula and stars behind it, with a thin pale highlight on its edge like glass. Dashing: the ship has punched forward; behind it a stretched teardrop of warped space trails out, the background stars inside smeared into short streaks. Fading: the warp relaxes, a last faint ring of displacement.",
    "stardust": "STARDUST: the dash scatters glittering dust like a comet. Launch: a small bloom of tiny pale gold and white sparkle motes and a few four-pointed twinkle stars bursts at the tail. Dashing: a long tapering comet-tail sweep of glittering motes, densest near the ship, sparser further back, a handful of larger twinkles among them. Fading: the dust drifts and twinkles out, a few last motes.",
    "panes": "HARD-LIGHT PANES: translucent, faintly cyan-tinted flat glass-like geometric panels. Launch: three thin angular glass chevrons flick into being around the ship's rear, edges catching a thin pale highlight, mostly see-through. Dashing: a short trail of overlapping translucent faceted panes outlines the path behind the ship like broken glass sheets. Fading: the panes crack into a few clean angular glass fragments that fade.",
}

for key, body in VARIANTS.items():
    d = here / f"run_{key}"
    d.mkdir(exist_ok=True)
    (d / "prompt.txt").write_text(BASE + body, encoding="utf-8")
print("ok")
