import glob
import json
import os
import re
import subprocess

REPO = "D:/amind/git/agent-8"
SHIPS = REPO + "/src/Asteroids3D/Assets/Visuals/Ships"
HERE = os.path.dirname(os.path.abspath(__file__))
M = json.load(open(os.path.join(HERE, "manifest.json"), encoding="utf-8"))
FAIL = []


def fail(msg):
    FAIL.append(msg)
    print("  FAIL:", msg)


def srgb(v):
    return 12.92 * v if v <= 0.0031308 else 1.055 * v ** (1 / 2.4) - 0.055


def lin(v):
    return v / 12.92 if v <= 0.04045 else ((v + 0.055) / 1.055) ** 2.4


def hexrgb(h):
    h = h.lstrip("#")
    return [int(h[i:i + 2], 16) / 255 for i in (0, 2, 4)]


def lumY(c):
    return 0.2126 * c[0] + 0.7152 * c[1] + 0.0722 * c[2]


def read(p):
    with open(p, encoding="utf-8", newline="") as f:
        return f.read()


COLOR = re.compile(r"^    - (\w+): \{r: ([^,]+), g: ([^,]+), b: ([^,]+), a: ([^}]+)\}\r?$")


def colors(text):
    out = {}
    for line in text.split("\n"):
        m = COLOR.match(line)
        if m:
            out[m.group(1)] = [float(m.group(i)) for i in (2, 3, 4, 5)]
    return out


def tex_guids(text):
    return re.findall(r"m_Texture: \{fileID: \d+, guid: ([0-9a-f]{32})", text)


def close(a, b, tol=2e-6):
    return all(abs(x - y) <= tol for x, y in zip(a, b))


def expected_from_pick(desc, src_colors, prop):
    hexes = re.findall(r"#[0-9a-f]{6}", desc)
    h = hexes[0]
    if "(hex/255" in desc:
        return hexrgb(h)
    if "srgb(lift(linear6," in desc:
        L = float(re.search(r"linear6, ([0-9.]+)\)", desc).group(1))
        return [srgb(round(lin(c), 6) + (1 - round(lin(c), 6)) * L) for c in hexrgb(h)]
    if "srgb(linear6)" in desc:
        return [srgb(round(lin(c), 6)) for c in hexrgb(h)]
    if desc.startswith("glow"):
        today = src_colors[prop]
        ty = lumY([lin(c) for c in today[:3]])
        g = [lin(c) for c in hexrgb(h)]
        k = ty / lumY(g)
        return [srgb(c * k) for c in g]
    raise ValueError(desc)


print("== Materials (%d)" % len(M["materials"]))
all_guids = {}
for p in glob.glob(REPO + "/src/Asteroids3D/Assets/**/*.meta", recursive=True):
    m = re.search(r"^guid: ([0-9a-f]{32})", read(p), re.M)
    if m:
        all_guids.setdefault(m.group(1), []).append(p)
dups = {g: ps for g, ps in all_guids.items() if len(ps) > 1}

for e in M["materials"]:
    src, dst = REPO + "/" + e["src"], REPO + "/" + e["dst"]
    st, dt = read(src), read(dst)
    print(f"-- {e['dst']}  (from {e['src']})")
    if tex_guids(st) != tex_guids(dt):
        fail("texture guids differ")
    else:
        print(f"  texture guids identical ({len(tex_guids(st))})")
    sl, dl = st.split("\n"), dt.split("\n")
    if len(sl) != len(dl):
        fail("line count differs")
        continue
    changed = {}
    name = os.path.splitext(os.path.basename(dst))[0]
    for a, b in zip(sl, dl):
        if a == b:
            continue
        if a.startswith("  m_Name: ") and b.rstrip("\r") == f"  m_Name: {name}":
            continue
        ma, mb = COLOR.match(a), COLOR.match(b)
        if ma and mb and ma.group(1) == mb.group(1):
            changed[mb.group(1)] = [float(mb.group(i)) for i in (2, 3, 4, 5)]
            continue
        fail(f"non-colour line differs: {a!r} -> {b!r}")
    if not re.search(rf"^  m_Name: {re.escape(name)}\r?$", dt, re.M):
        fail("m_Name not set to file base name")
    if set(changed) != set(e["picks"]):
        fail(f"changed props {sorted(changed)} != planned {sorted(e['picks'])}")
    sc = colors(st)
    for prop in sorted(e["picks"]):
        desc = e["picks"][prop]
        got = changed.get(prop) or colors(dt)[prop]
        exp = expected_from_pick(desc, sc, prop)
        ok = close(got[:3], exp)
        alpha_note = ""
        if prop == "_EmissionColor" and desc.startswith("glow"):
            if got[3] != sc[prop][3]:
                fail("emission alpha changed")
            yt = lumY([lin(c) for c in sc[prop][:3]])
            yg = lumY([lin(c) for c in got[:3]])
            alpha_note = f" lumY today {yt:.5f} new {yg:.5f} alpha {got[3]:g}"
            if abs(yt - yg) > 1e-5:
                fail("emission luminance mismatch")
        print(f"  {prop:20s} = ({got[0]:.6f}, {got[1]:.6f}, {got[2]:.6f}, a {got[3]:g})  <- {desc}{alpha_note}  {'OK' if ok else 'MISMATCH'}")
        if not ok:
            fail(f"{prop} expected {exp}")
    sm, dm = read(src + ".meta"), read(dst + ".meta")
    smr = re.sub(r"^guid: [0-9a-f]{32}", "guid: X", sm, flags=re.M)
    dmr = re.sub(r"^guid: [0-9a-f]{32}", "guid: X", dm, flags=re.M)
    g = re.search(r"^guid: ([0-9a-f]{32})", dm, re.M).group(1)
    if smr != dmr:
        fail("meta differs beyond guid")
    if g in dups:
        fail(f"guid {g} duplicated: {dups[g]}")

print("\n== Folder metas")
tmpl = re.sub(r"^guid: [0-9a-f]{32}", "guid: X", read(SHIPS + "/Valis/Materials/jade-iris.meta"), flags=re.M)
for f in M["folders"]:
    t = read(REPO + "/" + f + ".meta")
    g = re.search(r"^guid: ([0-9a-f]{32})", t, re.M).group(1)
    ok = re.sub(r"^guid: [0-9a-f]{32}", "guid: X", t, flags=re.M) == tmpl and "folderAsset: yes" in t and g not in dups
    print(f"  {f}.meta guid {g} {'OK' if ok else 'FAIL'}")
    if not ok:
        fail(f"folder meta {f}")
print(f"  duplicate guids anywhere under Assets: {len(dups)}")

print("\n== Light-lift checks (today's source materials)")
ns_L = json.load(open(REPO + "/art/ships/nightshade/paint.json"))["material"]["light_lift"]
worst = 0
for p in glob.glob(SHIPS + "/Nightshade/Materials/nightshade/*.mat"):
    c = colors(read(p))
    exp = [srgb(lin(x) + (1 - lin(x)) * ns_L) for x in c["_PaperColor"][:3]]
    worst = max(worst, max(abs(a - b) for a, b in zip(c["_PaintLightColor"][:3], exp)))
print(f"  Nightshade: paint.json light_lift L={ns_L}; lift rule vs today's nightshade/ max diff {worst:.2e} -> {'reproduces (<=0.002)' if worst <= 0.002 else 'DOES NOT reproduce'}")
Ls = []
for p in glob.glob(SHIPS + "/Bulwark/Materials/plum-bone/*.mat"):
    c = colors(read(p))
    if "_PaintLightColor" not in c:
        continue
    for a, b in zip(c["_PaperColor"][:3], c["_PaintLightColor"][:3]):
        Ls.append((lin(b) - lin(a)) / (1 - lin(a)))
bw_json = json.load(open(REPO + "/art/ships/bulwark/paint.json"))["material"]["light_lift"]
print(f"  Bulwark: L derived from plum-bone paper/light pairs: mean {sum(Ls)/len(Ls):.6f} range [{min(Ls):.6f}, {max(Ls):.6f}] (n={len(Ls)}); paint.json light_lift {bw_json}; used {M['notes']['bulwark_L_used']}")

print("\n== Crimson Hull paint region order (sorted names -> _PaintRegionColorN vs srgb(crimson linear))")
cr = json.loads(subprocess.run(["git", "-C", REPO, "show", "HEAD:art/ships/crimson/palettes.json"], capture_output=True, text=True, check=True).stdout)["palettes"]["crimson"]
hc = colors(read(SHIPS + "/Crimson/Hull paint.mat"))
order_ok = True
for i, n in enumerate(sorted(cr)):
    exp = [srgb(x) for x in cr[n][:3]]
    got = hc[f"_PaintRegionColor{i}"][:3]
    d = max(abs(a - b) for a, b in zip(got, exp))
    order_ok &= d < 0.005
    print(f"  {i:2d} {n:16s} today ({got[0]:.3f}, {got[1]:.3f}, {got[2]:.3f}) srgb(lin) ({exp[0]:.3f}, {exp[1]:.3f}, {exp[2]:.3f}) diff {d:.1e}")
print(f"  order {'CONFIRMED' if order_ok else 'NOT confirmed'}")
if not order_ok:
    fail("crimson order")

print("\n== palettes.json")
for ship, newp in M["palettes"].items():
    rel = f"art/ships/{ship}/palettes.json"
    head = json.loads(subprocess.run(["git", "-C", REPO, "show", f"HEAD:{rel}"], capture_output=True, text=True, encoding="utf-8", check=True).stdout)
    cur = json.loads(read(REPO + "/" + rel))
    print(f"-- {rel}: default {cur['default']!r} (HEAD {head['default']!r}); palettes {list(cur['palettes'])}")
    if cur["default"] != head["default"] or cur["color_space"] != head["color_space"]:
        fail("default/color_space changed")
    for p, v in head["palettes"].items():
        if cur["palettes"].get(p) != v:
            fail(f"existing palette {p} changed")
    defkeys = list(head["palettes"][head["default"]].keys())
    raw_head = subprocess.run(["git", "-C", REPO, "show", f"HEAD:{rel}"], capture_output=True, text=True, encoding="utf-8", check=True).stdout
    raw_cur = read(REPO + "/" + rel).replace("\r\n", "\n")
    # head text must be a prefix-wise subset: every HEAD line survives in order
    hl, cl = raw_head.rstrip("\n").split("\n"), raw_cur.rstrip("\n").split("\n")
    print(f"  formatting: HEAD lines {len(hl)}, now {len(cl)}, HEAD first {len(hl)-3} lines unchanged: {cl[:len(hl)-3] == hl[:len(hl)-3]}")
    for p in newp:
        regs = cur["palettes"][p]
        if list(regs) != defkeys:
            fail(f"{p} key order/set differs from default")
        print(f"  [{p}] {len(regs)} regions, keys match default order: {list(regs) == defkeys}")
        for n, val in regs.items():
            dv = head["palettes"][head["default"]][n]
            if val == dv:
                print(f"    {n:30s} keep {dv[:3]}")
            else:
                print(f"    {n:30s} {val}")
for ship, newp in M["palettes"].items():
    pass

print("\n== Palette values vs picks (linear6 of hex)")
PICKS = {
    ("crimson", "shoal"): {"Silver": "#f2a71a", "SilverStructure": "#f2a71a", "Red": "#171614", "RedStructure": "#171614", "Frame": "#171614", "Charcoal": "#3c3e43", "Rim": "#3a3a3e"},
    ("crimson", "veil"): {"Silver": "#2d2e36", "SilverStructure": "#2c2e35", "Red": "#9fe535", "RedStructure": "#9fe535", "Frame": "#9fe535", "Charcoal": "#1d1e23", "Rim": "#1d1e23"},
    ("crimson", "bastion"): {"Silver": "#eeeeee", "SilverStructure": "#234eb3", "Red": "#234eb3", "RedStructure": "#234eb3", "Frame": "#234eb3", "Charcoal": "#1f2441", "Rim": "#ededed"},
    ("valis", "shoal"): {"Ivory": "#f1a619", "Cool gray": "#d28c0c", "Edge": "#d78c08", "Graphite": "#171412", "Lavender": "#363737", "Engine": "#39393d"},
    ("valis", "shoal-elite"): {"Ivory": "#f1a619", "Cool gray": "#d28c0c", "Edge": "#d78c08", "Graphite": "#171412", "Lavender": "#fc12d8", "Engine": "#39393d"},
    ("nightshade", "veil"): {"Nightshade Midnight": "#1c1d22", "Nightshade Violet": "#2c2e36", "Nightshade Plum": "#9ee636", "Nightshade Glass": "#a1e535", "Nightshade Edge": "#1e1f24"},
    ("nightshade", "veil-elite"): {"Nightshade Midnight": "#1c1d22", "Nightshade Violet": "#2c2e36", "Nightshade Plum": "#fc12d8", "Nightshade Glass": "#a1e535", "Nightshade Edge": "#1e1f24"},
}
for flap, pal in (("#eeeeee", "bastion"), ("#fc12d8", "bastion-elite")):
    d = {n: "#1c2547" for n in ["Bulwark Plum", "Bulwark Bone", "Bulwark Plum Secondary", "Bulwark Bone Secondary", "Bulwark Structure", "Bulwark Structure Secondary", "Bulwark EdgeCoat", "Bulwark Armor Charcoal"]}
    d.update({"Bulwark Flap": flap, "Bulwark Flap Secondary": flap, "Bulwark Guard Plum": flap, "Bulwark Guard Bone": "#2450b8", "Bulwark Metal": "#e6e6e6"})
    PICKS[("bulwark", pal)] = d
for (ship, pal), picks in PICKS.items():
    cur = json.loads(read(f"{REPO}/art/ships/{ship}/palettes.json"))
    regs = cur["palettes"][pal]
    dflt = cur["palettes"][cur["default"]]
    bad = 0
    for n, val in regs.items():
        if n in picks:
            exp = [round(lin(c), 6) for c in hexrgb(picks[n])]
            if val[:3] != exp or val[3] != 1:
                bad += 1
                fail(f"{ship}/{pal}/{n} {val} != {exp}")
        elif val != dflt[n]:
            bad += 1
            fail(f"{ship}/{pal}/{n} keep mismatch")
    kept = [n for n in regs if n not in picks]
    print(f"  {ship}/{pal}: {len(picks)} picks match linear6(hex), kept {kept}: {'OK' if not bad else 'FAIL'}")

print("\n== git status --short")
st = subprocess.run(["git", "-C", REPO, "status", "--short", "-uall"], capture_output=True, text=True, check=True).stdout
mod = [l for l in st.splitlines() if not l.startswith("??")]
new = [l[3:].strip('"') for l in st.splitlines() if l.startswith("??")]
for l in mod:
    print("  " + l)
expected_mod = {f" M art/ships/{s}/palettes.json" for s in ("bulwark", "crimson", "nightshade", "valis")}
print(f"  modified: {len(mod)} (expected exactly the 4 palettes.json: {set(mod) == expected_mod})")
if set(mod) != expected_mod:
    fail("unexpected modified files")
exp_new = set()
for e in M["materials"]:
    exp_new |= {e["dst"], e["dst"] + ".meta"}
for f in M["folders"]:
    exp_new.add(f + ".meta")
print(f"  untracked files: {len(new)} (expected {len(exp_new)}; set equal: {set(new) == exp_new})")
if set(new) != exp_new:
    fail(f"untracked set mismatch: extra {sorted(set(new) - exp_new)} missing {sorted(exp_new - set(new))}")

print("\nRESULT:", "PASS" if not FAIL else f"FAIL ({len(FAIL)})")
