"""Usage: python gen.py <this folder> <repo root holding art/tools/imagegen>. Needs GEMINI_API_KEY."""
import subprocess, sys, os, concurrent.futures as cf
S, REPO = sys.argv[1], sys.argv[2]
COMMON = """READABILITY AT GAME DISTANCE COMES FIRST. In play this ship is only 60 to 100 pixels across on a dark blue space background, in fast combat. The big colour shapes of image 1 must survive untouched at that size; detail is a second read that only shows up close.

Image 1 is the object and colour reference: the Bastion {rank}, top-down, nose up, in flat colour blocks. Keep its exact silhouette, proportions and panel boundaries, and keep every region in the colour image 1 gives it. Where images disagree about a colour, image 1 wins. Images 2, 3 and 4 show the same ship from the side, three-quarter front and three-quarter rear. They are only there so you understand its 3D form; do not draw those views.

The faction: Bastion, "the wall". Disciplined clan warships that advance slowly behind heavy armour plate and ram. Proud, maintained, heraldic, not scrappy. The palette is fixed:
- armour plates: deep oxblood red #8a2a3c
- second plates and recessed panels: darker oxblood #561a26
- frame, side skirts and undercarriage: warm near-black slate #241f22
- trim, side guards, tail fins and clan markings: bone #ebcb9e
- engine deck and nozzles: warm steel grey #8c7f7c
- canopy: dark glass #1c2a35, no glow
There is no cyan, teal, blue, orange, yellow, green or magenta anywhere on this ship, and no glowing lights.

{rank_text}

Turn the flat blocks into a finished paint concept in a hand-painted illustrated style: solid colour regions, confident dark ink panel lines, one lighter brushed highlight pass and one darker shadow pass, lit softly from the top of the image. Add believable surface structure: plate seams, bolts, vents, hatches, armour overlaps, edge chipping and brushed wear.

HARD RULE FOR ALL ADDED DETAIL: it is value-only. Every line, scuff, chip, shadow and highlight is a darker or lighter tone of the colour already underneath it. Detail never brings a new hue: no rust orange, no brown grime, no green or blue staining, no coloured decals. The same detail must still work if the red were swapped for navy blue. Any marking that needs its own colour is a clean flat bone shape with hard edges, like the chevrons and bands in image 1. No text, numbers, letters or logos.

Output: {output}"""
ONE = "this one ship only, top-down, nose up, centred, same orthographic framing as image 1, on a flat mid-grey background. No engine flames, no shield, no background objects."
SHIPS = {
 "minion": ("minion", "This is the lowest rank: a plain line trooper, smaller and simpler than its officers. It carries NO chevrons and NO fin bands; do not add any. Its flat blocking is too plain, so give it structure through paint: split the big red top plate into a few large armour panels with ink seams and a slightly darker centre spine, a thin bone trim line along the leading edges of the top plate, a darker inset engine deck with visible nozzles, and one small solid bone clan block on the left shoulder. Keep it clearly plainer than an officer."),
 "rival": ("rival (a clan officer)", "This is the middle rank. Its rank marks are exactly ONE bone chevron across the top plate and exactly ONE oxblood band on each bone tail fin, as in image 1. Keep that count exactly. The bone side guards with oxblood inset panels stay as shown."),
 "champion": ("champion (the clan's elite)", "This is the top rank. Its rank marks are exactly TWO bone chevrons across the top plate and exactly TWO oxblood bands on each bone tail fin, as in image 1. Keep that count exactly. The overlapping near-black armour scales down each flank have bone edges; make them read as heavy layered plate. It should feel like the most decorated and most armoured ship of the three, through livery and plate, not through glow."),
}
os.makedirs(f"{S}/prompts", exist_ok=True); os.makedirs(f"{S}/out", exist_ok=True)
jobs = []
for key, (rank, text) in SHIPS.items():
    pf = f"{S}/prompts/{key}.txt"
    open(pf, "w", encoding="utf-8").write(COMMON.format(rank=rank, rank_text=text, output=ONE))
    refs = [x for v in ("blocking-top", "side", "turn045", "turn225") for x in ("--ref", f"{S}/refs/{key}-{v}.png")]
    for model in ("nb2", "pro"):
        for n in (1, 2):
            jobs.append(["uv", "run", f"{REPO}/art/tools/imagegen/imagegen.py", "--prompt-file", pf, *refs,
                         "--model", model, "--size", "2K", "--aspect", "1:1", "--out", f"{S}/out/{key}-{model}-{n}"])
LINEUP = """READABILITY AT GAME DISTANCE COMES FIRST. In play each ship is only 60 to 100 pixels across on a dark blue space background.

Images 1, 2 and 3 are the object and colour references for the three ranks of the Bastion faction, top-down, nose up, in flat colour blocks: image 1 the minion, image 2 the rival, image 3 the champion. Keep each ship's exact silhouette, proportions, panel boundaries and region colours.

Bastion is "the wall": disciplined, proud clan warships in heavy armour. Fixed palette: plates deep oxblood red #8a2a3c, second plates #561a26, frame and side skirts near-black slate #241f22, trim, guards, fins and clan markings bone #ebcb9e, engines warm steel grey #8c7f7c, canopy dark glass #1c2a35. No cyan, blue, orange, green or magenta, and no glowing lights.

Rank marks, keep the counts exactly: the minion has no chevron and no fin bands; the rival has one bone chevron and one oxblood band per tail fin; the champion has two chevrons and two bands per fin.

Paint all three as one family in a hand-painted illustrated style: solid colour regions, dark ink panel lines, one lighter brushed highlight pass and one darker shadow pass, lit softly from the top. Add plate seams, bolts, vents, armour overlaps and edge wear. All added detail is value-only: darker or lighter tones of the colour underneath, never a new hue, no rust, grime colour or decals. No text or logos.

Output: the three ships side by side, top-down, nose up, minion left, rival centre, champion right, at their relative sizes from the references, on a flat mid-grey background. No flames, no shields."""
pf = f"{S}/prompts/lineup.txt"; open(pf, "w", encoding="utf-8").write(LINEUP)
refs = [x for k in SHIPS for x in ("--ref", f"{S}/refs/{k}-blocking-top.png")]
for model in ("nb2", "pro"):
    jobs.append(["uv", "run", f"{REPO}/art/tools/imagegen/imagegen.py", "--prompt-file", pf, *refs,
                 "--model", model, "--size", "2K", "--aspect", "16:9", "--out", f"{S}/out/lineup-{model}"])
def run(cmd):
    r = subprocess.run(cmd, capture_output=True, text=True, encoding="utf-8", errors="replace")
    return cmd[-1], r.returncode, (r.stdout + r.stderr)[-400:]
with cf.ThreadPoolExecutor(6) as ex:
    for out, rc, tail in ex.map(run, jobs):
        print(rc, out, "" if rc == 0 else tail, flush=True)
