from pathlib import Path

here = Path(__file__).parent

BASE = """Use your built-in image_gen tool to create ONE NEW 16:9 image, then copy the generated PNG into the current working directory as out.png and print its absolute source path. Do nothing else: no code, no other files. Three images are attached in order: Image 1, Image 2, Image 3.

Reference roles:
- Image 1 is the OBJECT reference: {ship}, seen from directly above, nose up.
- Image 2 is the game's STYLE FRAME: copy its painterly blue-violet nebula background and its hand-painted, ink-outlined illustration style. Do NOT copy its effects.
- Image 3 is the chosen EFFECT LANGUAGE: copy the material of its glass panes exactly: HARD-LIGHT GLASS, flat translucent faceted panels, mostly see-through so the nebula shows through them, a faint tint, a thin crisp pale highlight along their edges, clean straight-edged angular shapes. Ignore its ship and layout.

SHIP FIDELITY (most important rule): wherever the ship appears it must look exactly like Image 1: {fidelity}. Do not repaint, smooth, restyle or redesign it. The effect is a separate layer around the unchanged hull.

STRICT: NO fire, NO flames, NO laser blades, NO white-hot cores, NO neon glow, NO electric sparks, NO smoke. Engines show only their normal small exhaust. All effect shapes are hard-light glass in the tint {tint}.

Layout: four frames left to right, labelled small underneath, the ship pointing straight up and the same size in each. Below, a "Small-size check" strip repeats the two key frames at one fifth size, because in play the ship is only about 70 pixels tall; the glass must still read there, so panes are few and large, never fine slivers.

"""

SHIPS = {
    "vanguard": ("the Vanguard fighter", "white-and-orange hull, grey panels, a long central fuselage, two side pods each with a round cyan lens, orange swept fins", "pale cyan"),
    "bulwark": ("the Bulwark, a heavy armoured brawler", "a blunt tan-and-brown armoured block with a dark cone at the centre of its front plate and two angular legs at the rear", "warm amber"),
    "crimson": ("the Crimson, a spiky assault fighter", "mostly mid-grey armour plates, black recesses, thin red trim lines, a pale tan cockpit, outward-pointing red spikes on both sides", "rose red"),
    "valis": ("the Valis, a swept-wing interceptor", "dark green hull, four long swept wings with pale pink tips, a black cockpit, two small pink lights", "pale pink"),
}

VARIANTS = {
    "dash_a": ("vanguard", """Ability: DASH, a short violent forward lunge. Frames: "Launch", "Dashing", "Trail", "Fading".
Launch: three large thin glass chevrons flick into being stacked behind the ship's tail, pointing forward.
Dashing: the ship has lunged ahead; behind it a trail of four or five large overlapping glass panes, each a flat angular sheet roughly the ship's width, staggered along the path like the ship punched through a row of glass.
Trail: the panes nearest the ship persist, the farthest ones begin to crack along clean straight lines.
Fading: the panes have broken into a few large clean angular fragments drifting apart and fading."""),
    "dash_b": ("vanguard", """Ability: GUARD DASH, a short forward lunge during which the ship cannot be hurt for under half a second. Frames: "Launch", "Guarded", "Shell breaks", "Fading".
Launch: a faceted glass SHELL snaps shut around the whole ship, a close-fitting low-poly cage of a dozen flat translucent facets hugging the hull's outline with crisp pale edges; two glass chevrons flick in behind the tail.
Guarded: the ship lunges forward inside its glass shell; behind it a short trail of three overlapping glass panes.
Shell breaks: the protection ends: the shell bursts outward into eight to ten large clean angular glass shards flying away from the hull.
Fading: the shards and the trailing panes fade out; the ship flies on unprotected."""),
    "ram_a": ("bulwark", """Ability: RAM. The heavy ship lunges forward and for a moment its front becomes a weapon that smashes whatever it hits. Frames: "Brace", "Ram live", "Impact", "Spent".
Brace: a thick faceted glass PROW starts forming ahead of the blunt front plate, two or three facets.
Ram live: a solid, thick, multi-faceted hard-light glass WEDGE sits just ahead of the ship's front like a battering-ram prow: a wide pointed arrowhead of a dozen chunky facets, noticeably thicker and heavier-looking than the other abilities' glass, its swept-back edges running past the ship's front corners. This is the "danger, the ram is live" read.
Impact: the wedge slams into a grey rock directly ahead and the glass shatters forward into a spray of large chunky angular shards; the rock cracks.
Spent: the prow is gone; a few amber glass chunks tumble away behind."""),
    "ram_b": ("bulwark", """Ability: RAM. The heavy ship lunges forward and for a moment its front becomes a weapon that smashes whatever it hits. Frames: "Brace", "Ram live", "Impact", "Spent".
Brace: two flat glass plates slide into place in front of the ship.
Ram live: a curved WALL of five or six thick overlapping glass plates stands ahead of the ship's front like a riot shield or snow-plough blade, wider than the ship, its outer plates angled back along the flanks; a short trail of two panes behind.
Impact: the plate wall hits a grey rock directly ahead; the plates crack and burst forward and outward into large chunky angular shards.
Spent: the wall is gone; a few amber glass chunks tumble away behind."""),
    "blink_a": ("crimson", """Ability: BLINK. After a quarter-second wind-up the ship vanishes and reappears a short distance ahead, keeping its speed. Frames: "Wind-up", "Vanish", "Arrive", "After".
Wind-up: the ship's outline is traced by a thin faceted glass skin, a low-poly glass copy of its silhouette laid exactly over the hull.
Vanish: the ship is gone; its glass silhouette, now empty, shatters into a dozen large clean angular shards, the shards all streaking upward toward the destination.
Arrive: further up, the shards fly together and lock into the ship's glass silhouette, and the real ship is inside it.
After: the ship flies on; its glass skin cracks off in a few large shards behind; a faint row of shards marks the departure point at the bottom."""),
    "blink_b": ("crimson", """Ability: BLINK. After a quarter-second wind-up the ship vanishes and reappears a short distance ahead, keeping its speed. Frames: "Wind-up", "Vanish", "Arrive", "After".
Wind-up: a tall flat rectangular glass PANE, a doorway wider than the ship, materialises flat in space just ahead of the nose; a matching pane appears far ahead where the ship will arrive.
Vanish: the ship passes into the near pane and disappears; the pane ripples like a window and cracks across its middle.
Arrive: the ship bursts out through the far pane, which shatters into large clean angular shards thrown forward and outward around the ship.
After: the ship flies on; both panes have fallen into a few drifting fragments."""),
    "od_a": ("valis", """Ability: OVERDRIVE. For three seconds the ship is faster and more agile; players must see at a glance that it is still active, and see when it ends. Frames: "Engage", "Overdrive (cruise)", "Overdrive (turning)", "Expire".
Engage: thin glass blades snap out from each wingtip.
Overdrive (cruise): from each of the four wingtips a long ribbon of glass trails backward, made of a chain of narrow overlapping glass panes, each pane a long flat angular sliver as wide as the wingtip, the chain stretching two ship-lengths behind: a glass slipstream. Held steady for three seconds.
Overdrive (turning): the ship banking right; the glass pane chains curve along its path behind it.
Expire: the chains break apart into separate panes that crack and fall away; the wingtips are bare again."""),
    "od_b": ("valis", """Ability: OVERDRIVE. For three seconds the ship is faster and more agile; players must see at a glance that it is still active, and see when it ends. Frames: "Engage", "Overdrive (cruise)", "Overdrive (turning)", "Expire".
Engage: hard-light glass extensions unfold from the hull.
Overdrive (cruise): the ship wears large faceted glass WING EXTENSIONS: each of its four swept wings is extended by a big translucent glass fin about as long again as the wing, plus a glass blade extending the nose, so the ship's silhouette is visibly larger and sharper while overdrive lasts. A short trail of two panes behind.
Overdrive (turning): the ship banking right with the glass extensions; a short curved trail of panes.
Expire: the glass extensions crack off into large clean shards that fall away; the ship is back to its normal silhouette."""),
}

for key, (ship, body) in VARIANTS.items():
    name, fidelity, tint = SHIPS[ship]
    d = here / f"run_{key}"
    d.mkdir(exist_ok=True)
    (d / "prompt.txt").write_text(BASE.format(ship=name, fidelity=fidelity, tint=tint) + body, encoding="utf-8")
    (d / "ship.txt").write_text(ship, encoding="utf-8")
print("ok")
