from pathlib import Path
from PIL import Image

here = Path(__file__).parent
lineup = here.parent / "lineup"
for n in ("bulwark", "crimson", "valis"):
    im = Image.open(lineup / f"{n}-top.png").convert("RGBA")
    bg = Image.new("RGBA", im.size, (13, 18, 32, 255))
    bg.alpha_composite(im)
    bg.convert("RGB").save(here / f"{n}.png")

STYLE = """Use your built-in image_gen tool to create ONE NEW 16:9 image, then copy the generated PNG into the current working directory as out.png and print its absolute source path. Do nothing else: no code, no other files. Three images are attached in order: Image 1, Image 2, Image 3.

Reference roles: Image 1 is the OBJECT reference: {ship} seen from directly above, nose pointing up. Keep its exact silhouette, paint and proportions; do not redesign it. Image 2 and Image 3 are STYLE references for the effects ONLY: flat cel-shaded 2D game VFX, three to four hard-edged colour bands from a white-hot core out to a saturated rim, sharp angular jagged silhouettes, no outlines on effects, no soft airbrush gradients, no photoreal smoke, no lens flares. Do not copy their colours or shapes literally.

Layout: a storyboard sheet for one ship ability in a top-down space shooter. Dark navy space background (#0d1220), no planets, no text except small labels. The ship points straight up and is the same size in every frame. Four frames left to right in one row, each labelled small underneath. Below the row, a "Small-size check" strip repeating the two most important frames at one fifth size, because in play the ship is only about 70 pixels tall: every effect shape must still read at that size, so no fine detail and no lines thinner than the ship's own trim stripes.

"""

RAM = "Ability: RAM. The heavy armoured ship lunges forward and for a moment its nose becomes a weapon that smashes whatever it hits. Effect palette: white-hot core, pale yellow, hot orange, deep red rim.\nFrames: \"Brace\", \"Charge\", \"Impact\", \"Spent\".\n"
BLINK = "Ability: BLINK. After a quarter-second wind-up the ship vanishes and reappears a short distance ahead, keeping its speed. Effect palette: white core, hot pink, crimson red, dark wine rim.\nFrames: \"Wind-up\", \"Vanish\", \"Arrive\", \"After\".\n"
OD = "Ability: OVERDRIVE. For three seconds the ship is faster and more agile; players must be able to tell at a glance that it is still active, and see when it ends. Effect palette: white core, pale pink, magenta, deep violet rim, matching the ship's pale pink trim.\nFrames: \"Engage\", \"Overdrive (cruise)\", \"Overdrive (turning)\", \"Expire\".\n"

VARIANTS = {
    "ram_a": ("bulwark", "the Bulwark, a heavy armoured brawler", RAM + """Brace: the engines at the rear flare into two short white-hot cel-banded spikes; a thin arc of orange begins to form just ahead of the blunt nose.
Charge: a solid, sharp, cel-banded WEDGE of force sits just ahead of the ship's nose like a battering-ram prow: a wide arrowhead chevron hugging the front of the hull, white-hot at its leading point, banding out to deep red along its swept-back edges, which stream backward past the ship's sides as a few short jagged blades. Big engine spike behind. This is the "danger, the ram is live" read and must be unmistakable.
Impact: the ram meets a grey asteroid fragment directly ahead: a large jagged cel-banded impact star bursts at the contact point in front of the nose, with sharp shards thrown outward and forward.
Spent: the wedge is gone; a few cooling orange shards fall away behind; normal small engine flames."""),
    "ram_b": ("bulwark", "the Bulwark, a heavy armoured brawler", RAM + """Brace: the engines flare white-hot; the ship's front armour edge glows a hard orange band.
Charge: the space in front of the ship compresses into a BOW SHOCK: two or three nested, flat, cel-banded crescent arcs curving around the front of the hull like a shield being pushed ahead, white at the centre of the innermost arc, orange then deep red outward, their tips flicking backward along the ship's flanks as torn flame tongues. Long engine spike behind.
Impact: the bow shock slams into a grey asteroid fragment directly ahead and bursts into a large jagged cel-banded impact star at the contact point, the crescents flattening and splashing sideways into sharp shards.
Spent: the arcs are gone; a few cooling orange shards fall away behind; normal small engine flames."""),
    "blink_a": ("crimson", "the Crimson, a spiky assault fighter", BLINK + """Wind-up: sharp crimson cel-banded crackle slivers converge inward on the ship from all sides, as if the ship is being pulled into a point; its outline glows a hard pink band.
Vanish: the ship is gone; where it was, a tall narrow jagged vertical TEAR in space, cel-banded white core to crimson to dark wine edges, pinching closed, a few sharp slivers flicking outward.
Arrive: further up the frame, a matching jagged tear is splitting open and the ship bursts out of it nose first, with sharp crimson shards spraying outward from the opening.
After: the ship flies on; the arrival tear has shrunk to a few fading shards behind it; at the very bottom of the frame a last small crimson scar marks the departure point."""),
    "blink_b": ("crimson", "the Crimson, a spiky assault fighter", BLINK + """Wind-up: the ship is covered in a hard flat crimson silhouette glow, and three sharp cel-banded crimson chevrons appear ahead of its nose pointing to where it will arrive.
Vanish: the ship shatters into a cloud of sharp flat crimson and pink angular shards in its own silhouette shape, the shards already streaking upward toward the destination as straight jagged blades.
Arrive: the blades converge further up the frame and snap together into the ship, with a sharp white-pink cel-banded star flash behind it.
After: the ship flies on; a straight dotted line of a few fading crimson shards connects the departure point to the ship."""),
    "od_a": ("valis", "the Valis, a swept-wing interceptor", OD + """Engage: the engine flame jumps to three times its length in a sharp cel-banded white-pink-magenta spike, with a short white-pink burst star at the tail.
Overdrive (cruise): long, smooth, flat cel-banded SLIPSTREAM RIBBONS flow from the wingtips and swept fins backward past the ship, each a tapering ribbon with a pale pink core and magenta edge, three on each side, and the long engine spike stays lit. This sustained look must read for three seconds.
Overdrive (turning): the same ship banking right in a curve; the ribbons curve to trace the path behind it like brush strokes.
Expire: the ribbons have broken into short sharp falling segments and the engine flame has snapped back to normal size."""),
    "od_b": ("valis", "the Valis, a swept-wing interceptor", OD + """Engage: a sharp cel-banded white-pink ring of jagged spikes bursts outward behind the ship, and the engine flame triples in length.
Overdrive (cruise): the ship's pale pink trim lines glow hard hot pink, and a sleek cel-banded AURA hugs the whole ship's outline a little way out, sharp at the nose and drawn out backward into a long pointed tail of pink and magenta bands behind the ship, as if the ship is wrapped in a speed sheath.
Overdrive (turning): the same ship banking right; the sheath's tail sweeps out and curves along the path.
Expire: the sheath cracks into a few sharp pink shards that fall away behind; trim stops glowing; normal engine flame."""),
}

for key, (img, ship, body) in VARIANTS.items():
    d = here / f"run_{key}"
    d.mkdir(exist_ok=True)
    (d / "prompt.txt").write_text(STYLE.format(ship=ship) + body, encoding="utf-8")
    (d / "ship.txt").write_text(img, encoding="utf-8")
print("ok")
