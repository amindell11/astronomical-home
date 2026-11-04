import subprocess, sys, os, concurrent.futures as cf
S = sys.argv[1]; REPO = sys.argv[2]
FACTIONS = {
 "shoal": ("Valis", "valis", "a fast, fragile swarm that comes in packs", {
    "a": "hazard yellow-amber as the dominant hull colour, with black trim and a small charcoal secondary",
    "b": "vivid orange as the dominant hull colour, with slate grey secondary and black trim",
    "c": "acid chartreuse-lime as the dominant hull colour, with charcoal secondary and black trim"}),
 "veil": ("Nightshade", "nightshade", "stealth ambushers that strike from cover", {
    "a": "near-black gunmetal as the dominant hull colour, with thin acid-green accent panels",
    "b": "pale ash grey as the dominant hull colour, with hot magenta accent panels and dark trim",
    "c": "deep blue-black as the dominant hull colour, with ice-white edge panels"}),
 "bastion": ("Bulwark", "bulwark", "a heavy shield wall that advances slowly", {
    "a": "strong cobalt blue as the dominant hull colour, with white secondary and dark navy trim",
    "b": "olive military green as the dominant hull colour, with safety-orange accents and dark trim",
    "c": "steel blue-grey as the dominant hull colour, with gold-yellow accents and dark trim"}),
}
TEMPLATE = """READABILITY AT GAME DISTANCE COMES FIRST. In play each ship is only 60 to 100 pixels across on a dark space background, in fast combat. This faction must be recognisable at a glance at that size: both ships must read instantly as the same faction, clearly unlike the player's ships and unlike other factions. One large dominant hull colour carries the identity; at most three main colours plus a dark trim.

Image 1 is the object reference for the {sig}: keep its exact top-down silhouette, panel boundaries and proportions. Image 2 is the object reference for the Crimson, shown in the player's livery (red and cream-white): keep its exact silhouette and panel boundaries, but its red and cream are the player's colours and must NOT be used as this faction's main colours. The faction must also avoid teal-jade with lavender, violet with electric blue, and bone with plum as its main colours, because those are the other player ships' liveries.

Repaint both ships for the {faction} faction, {trait}: {direction}.

Output: both ships top-down, nose up, side by side on a flat mid-grey background, the {sig} on the left and the Crimson on the right, at the same orthographic framing as the references. Flat solid colour per panel region exactly like the reference renders: no shading, no gradients, no lighting, no wear, damage, grunge, decals, text or logos. Change only the colours and which panels get which colour; never change any shape."""
jobs = []
for fac, (sig, sigfile, trait, dirs) in FACTIONS.items():
    for d, direction in dirs.items():
        prompt = TEMPLATE.format(sig=sig, faction=fac.capitalize(), trait=trait, direction=direction)
        pf = f"{S}/paint/prompts/r1-{fac}-{d}.txt"
        open(pf, "w", encoding="utf-8").write(prompt)
        for model in ("nb2", "pro"):
            out = f"{S}/paint/round1/{fac}/{fac}-{d}-{model}"
            os.makedirs(os.path.dirname(out), exist_ok=True)
            jobs.append(["uv", "run", f"{REPO}/art/tools/imagegen/imagegen.py", "--prompt-file", pf,
                         "--ref", f"{S}/paint/refs/{sigfile}-top-flat.png", "--ref", f"{S}/paint/refs/crimson-top-flat.png",
                         "--model", model, "--size", "2K", "--aspect", "16:9", "--out", out])
def run(cmd):
    r = subprocess.run(cmd, capture_output=True, text=True, encoding="utf-8", errors="replace")
    return cmd[-1], r.returncode, (r.stdout + r.stderr)[-400:]
with cf.ThreadPoolExecutor(6) as ex:
    for out, rc, tail in ex.map(run, jobs):
        print(rc, out, "" if rc == 0 else tail, flush=True)
