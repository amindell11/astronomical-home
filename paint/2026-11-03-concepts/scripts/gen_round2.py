import subprocess, sys, os, concurrent.futures as cf
S = sys.argv[1]; REPO = sys.argv[2]
PICKS = {
 "shoal": ("Valis", "shoal/shoal-a-pro", "amber-yellow and black", "acid green (Veil) or cobalt blue (Bastion)",
           "the thin dark stripe panels along its wing edges and tail"),
 "veil": ("Nightshade", "veil/veil-a-pro", "near-black gunmetal and acid green", "amber-yellow (Shoal) or cobalt blue (Bastion)",
          "the thin acid-green line panels along its body and wings"),
 "bastion": ("Bulwark", "bastion/bastion-a-pro", "cobalt blue and white", "amber-yellow (Shoal) or acid green (Veil)",
             "the white shoulder guards and white tail fins"),
}
GLOW = """READABILITY AT GAME DISTANCE COMES FIRST: in play each ship is 60 to 100 pixels across on a dark space background.

Image 1 is this faction's approved livery ({colors}) on the {sig} (left) and the Crimson (right). Keep both ships' shapes and livery exactly. Show the same two ships top-down, nose up, on a near-black space background, each with its engine exhaust flames glowing straight out behind it (downward in the image) and a thin glowing circular shield ring around it. Choose ONE glow colour for the flames and the shield ring that reads as this faction at a glance and pairs with its livery; it must not be red (the player's flame) and must not be confused with {others}. Flat graphic style, no text."""
ELITE = """READABILITY AT GAME DISTANCE COMES FIRST: in play each ship is 60 to 100 pixels across on a dark space background, and an elite enemy must pop out of its pack at a glance.

Image 1 is this faction's livery ({colors}) on the {sig} (left) and the Crimson (right). Make an ELITE version of the {sig} only: keep its shape and its main livery exactly, but repaint {accent} in one bright, hot elite colour, and give it engine exhaust flames glowing behind it in the same elite colour. Show the normal {sig} on the left and the elite {sig} on the right, top-down, nose up, on a near-black space background, with the normal one's flames in the faction colour. The elite colour must stand out from this faction's colours, from the player's red, and from {others}. Flat solid colour panels, no shading, no wear, no text."""
jobs = []
for fac, (sig, pick, colors, others, accent) in PICKS.items():
    for kind, tmpl in (("glow", GLOW), ("elite", ELITE)):
        prompt = tmpl.format(sig=sig, colors=colors, others=others, accent=accent)
        pf = f"{S}/prompts/r2-{fac}-{kind}.txt"; open(pf, "w", encoding="utf-8").write(prompt)
        for model in ("nb2", "pro"):
            out = f"{S}/round2/{fac}/{fac}-{kind}-{model}"; os.makedirs(os.path.dirname(out), exist_ok=True)
            jobs.append(["uv", "run", f"{REPO}/art/tools/imagegen/imagegen.py", "--prompt-file", pf,
                         "--ref", f"{S}/round1/{pick}.jpg", "--model", model, "--size", "2K", "--aspect", "16:9", "--out", out])
def run(cmd):
    r = subprocess.run(cmd, capture_output=True, text=True, encoding="utf-8", errors="replace")
    return cmd[-1], r.returncode, (r.stdout + r.stderr)[-400:]
with cf.ThreadPoolExecutor(6) as ex:
    for out, rc, tail in ex.map(run, jobs):
        print(rc, out, "" if rc == 0 else tail, flush=True)
