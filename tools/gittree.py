"""Git tree helpers: ls-tree and batched blob reads, plus a GUID scan of a Unity project subtree."""
import re, subprocess
from collections import Counter

SRC = "src/Asteroids3D/"
META_GUID = re.compile(rb"^guid:\s*([0-9a-f]{32})", re.M)
REF = re.compile(rb'guid["\']?\s*[:=]\s*["\']?([0-9a-fA-F]{32})', re.I)
ASMDEF = re.compile(rb"GUID:([0-9a-fA-F]{32})")


def git(repo, *a, inp=None):
    return subprocess.run(["git", "-C", repo, *a], input=inp, capture_output=True, check=True).stdout


def ls_tree(repo, rev, *paths):
    out = {}
    for line in git(repo, "ls-tree", "-r", "-z", rev, "--", *paths).split(b"\0"):
        if not line:
            continue
        meta, path = line.split(b"\t", 1)
        mode, _, oid = meta.split()
        out[path.decode()] = (mode.decode(), oid.decode())
    return out


def read_blobs(repo, oids):
    uniq = sorted(set(oids))
    p = git(repo, "cat-file", "--batch", inp="".join(o + "\n" for o in uniq).encode())
    res, i = {}, 0
    for o in uniq:
        nl = p.index(b"\n", i)
        size = int(p[i:nl].split()[2])
        res[o] = p[nl + 1:nl + 1 + size]
        i = nl + 1 + size + 1
    return res


def scan(repo, rev, tops=("Assets", "ProjectSettings", "Packages")):
    """-> tree, guid->project-relative path, owner->Counter(guid) (meta folded into its asset, self refs dropped)."""
    tree = ls_tree(repo, rev, *(SRC + t for t in tops))
    blobs = read_blobs(repo, (o for _, o in tree.values()))
    g2p, refs = {}, {}
    for path, (_, oid) in tree.items():
        data = blobs[oid]
        rel = path[len(SRC):]
        if rel.endswith(".meta"):
            m = META_GUID.search(data)
            if m:
                g2p[m.group(1).decode()] = rel[:-5]
        if b"\0" in data[:8000] or data.startswith(b"version https://git-lfs"):
            continue
        owner = rel[:-5] if rel.endswith(".meta") else rel
        c = refs.setdefault(owner, Counter())
        c.update(m.group(1).decode().lower() for m in REF.finditer(data))
        c.update(m.group(1).decode().lower() for m in ASMDEF.finditer(data))
    for owner, gs in refs.items():
        for g in [g for g in gs if g2p.get(g) == owner]:
            del gs[g]
    return tree, g2p, refs
