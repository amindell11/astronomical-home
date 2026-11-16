from pathlib import Path

here = Path(__file__).parent

BASE = """Use your built-in image_gen tool to create ONE NEW 16:9 image, then copy the generated PNG into the current working directory as out.png and print its absolute source path. Do nothing else: no code, no other files. Three images are attached in order: Image 1, Image 2, Image 3.

Reference roles:
- Image 1 is the OBJECT reference: {ship}, seen from directly above, nose up.
- Image 2 is the game's STYLE FRAME: copy its painterly blue-violet nebula background and its hand-painted, ink-outlined illustration style. Do NOT copy its effects.
- Image 3 is the chosen EFFECT LOOK: copy the look of the trail behind the ship in its middle frame: a TRAIL of flat, angular, translucent streaks with a faint tint and a thin crisp pale edge, overlapping and staggered along the path, mostly see-through so the nebula shows through. Ignore its ship and layout.

The effect is a MOTION TRAIL, never an object. It only ever exists behind the ship or streaming backward along its path: no shells, cages, shields, walls, prows, wings, doorways or any structure around or in front of the ship. Do not draw it as glass or crystal; it is a streak of light-like translucent shapes, like the reference trail.

SHIP FIDELITY (most important rule): wherever the ship appears it must look exactly like Image 1: {fidelity}. Do not repaint, smooth, restyle or redesign it. The trail is a separate layer behind the unchanged hull.

STRICT: NO fire, NO flames, NO laser beams, NO white-hot cores, NO neon glow, NO electric sparks, NO smoke. Engines show only their normal small exhaust. All trail shapes are tinted {tint}.

Layout: four frames left to right, labelled small underneath, the ship pointing straight up and the same size in each. Below, a "Small-size check" strip repeats the two key frames at one fifth size, because in play the ship is only about 70 pixels tall; the trail must still read there, so its streaks are few and broad, never fine slivers.

"""

SHIPS = {
    "vanguard": ("the Vanguard fighter", "white-and-orange hull, grey panels, a long central fuselage, two side pods each with a round cyan lens, orange swept fins", "pale cyan"),
    "bulwark": ("the Bulwark, a heavy armoured brawler", "a blunt tan-and-brown armoured block with a dark cone at the centre of its front plate and two angular legs at the rear", "warm amber"),
    "crimson": ("the Crimson, a spiky assault fighter", "mostly mid-grey armour plates, black recesses, thin red trim lines, a pale tan cockpit, outward-pointing red spikes on both sides", "strong rose red, clearly red and not purple"),
    "valis": ("the Valis, a swept-wing interceptor", "dark green hull, four long swept wings with pale pink tips, a black cockpit, two small pink lights", "pale pink"),
}

VARIANTS = {
    "dash_a": ("vanguard", """Ability: GUARD DASH, a short forward lunge; the ship cannot be hurt for under half a second. Frames: "Launch", "Guarded", "Guard ends", "Fading".
Launch: three short angular chevron streaks flick out just behind the tail.
Guarded: the ship has lunged ahead; a dense, bright trail of overlapping angular streaks pours back from the whole outline of the hull, as if the ship's silhouette is being dragged out behind it, about two ship-lengths long.
Guard ends: the trail has cut off sharply a short way behind the ship, leaving a clean gap between the ship and the trail; the ship's outline sheds nothing now.
Fading: the detached trail breaks into a few large angular streaks that fade."""),
    "dash_b": ("vanguard", """Ability: DASH, a sudden short forward lunge. Frames: "Launch", "Dashing", "Trail", "Fading".
Launch: a short burst of angular streaks behind the tail.
Dashing: a long trail of tall, narrow, tapered angular streaks stretched along the path, each streak the width of an engine pod and up to two ship-lengths long, overlapping and staggered like speed lines made of the reference streaks.
Trail: the trail lengthens and thins behind the ship.
Fading: the streaks shorten into a few broken angular dashes and fade."""),
    "ram_a": ("bulwark", """Ability: RAM. The heavy ship lunges forward and for a moment its front smashes whatever it hits. Frames: "Brace", "Ram live", "Impact", "Spent".
Brace: a short burst of streaks behind the tail.
Ram live: broad, heavy angular streaks peel off the ship's two FRONT CORNERS and sweep backward along both flanks like a bow wake, plus a wide trail of thick, broad streaks behind, noticeably heavier and wider than a normal dash trail. This is the "danger, the ram is live" read.
Impact: the ship hits a grey rock directly ahead; the bow-wake streaks splash outward sideways from the contact point; the rock cracks.
Spent: the streaks fall away behind and fade."""),
    "ram_b": ("bulwark", """Ability: RAM. The heavy ship lunges forward and for a moment its front smashes whatever it hits. Frames: "Brace", "Ram live", "Impact", "Spent".
Brace: two wide chevron streaks appear just behind the tail, pointing forward.
Ram live: behind the ship a stack of four or five WIDE CHEVRON streaks, each wider than the ship and pointing forward, spaced along the path like a ramming charge, the nearest ones brightest.
Impact: the ship hits a grey rock directly ahead; the chevron stack compresses up against the ship's rear and the rock cracks.
Spent: the chevrons fall away behind and fade."""),
    "blink_a": ("crimson", """Ability: BLINK. After a quarter-second wind-up the ship vanishes and reappears a short distance ahead, keeping its speed. Frames: "Wind-up", "Vanish", "Arrive", "After".
Wind-up: a few short streaks start to pull backward off the ship's outline.
Vanish: the ship is gone; where it was, a short stack of angular streaks in the shape of its outline.
Arrive: further up the frame the ship has reappeared; a single straight trail of overlapping angular streaks connects the departure point at the bottom to the ship's tail, like the trail of a very fast dash across the gap.
After: the ship flies on; the connecting trail fades from the departure end first."""),
    "blink_b": ("crimson", """Ability: BLINK. After a quarter-second wind-up the ship vanishes and reappears a short distance ahead, keeping its speed. Frames: "Wind-up", "Vanish", "Arrive", "After".
Wind-up: short streaks gather behind the ship.
Vanish: the ship is gone; a short trail of streaks remains at the departure point, ending abruptly where the ship was.
Arrive: further up the frame the ship has reappeared with a short trail of streaks already behind it; between the two trails is an empty GAP of plain space, showing the ship skipped the distance.
After: the ship flies on; both short trails fade."""),
    "od_a": ("valis", """Ability: OVERDRIVE. For three seconds the ship is faster and more agile; players must see at a glance that it is still active, and see when it ends. Frames: "Engage", "Overdrive (cruise)", "Overdrive (turning)", "Expire".
Engage: short streaks flick back from each of the four wingtips.
Overdrive (cruise): from each of the four wingtips a long trail of overlapping angular streaks streams backward about two ship-lengths: a slipstream. Held steady for three seconds.
Overdrive (turning): the ship banking right; the four wingtip trails curve along its path behind it.
Expire: the wingtip trails detach from the wingtips and break into fading streaks; the wingtips are bare again."""),
    "od_b": ("valis", """Ability: OVERDRIVE. For three seconds the ship is faster and more agile; players must see at a glance that it is still active, and see when it ends. Frames: "Engage", "Overdrive (cruise)", "Overdrive (turning)", "Expire".
Engage: a short burst of streaks behind the tail.
Overdrive (cruise): one broad, continuous trail of overlapping angular streaks streams behind the whole ship, as wide as its wingspan and about three ship-lengths long, new streaks continually forming at the tail. Held steady for three seconds.
Overdrive (turning): the ship banking right; the broad trail sweeps along its curved path.
Expire: the trail stops forming at the tail, leaving a gap behind the ship, and the detached trail fades."""),
}

for key, (ship, body) in VARIANTS.items():
    name, fidelity, tint = SHIPS[ship]
    d = here / f"run_{key}"
    d.mkdir(parents=True, exist_ok=True)
    (d / "prompt.txt").write_text(BASE.format(ship=name, fidelity=fidelity, tint=tint) + body, encoding="utf-8")
    (d / "ship.txt").write_text(ship, encoding="utf-8")
print("ok")
