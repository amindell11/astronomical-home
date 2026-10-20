"""Copy a ship_paint Unity bundle over the visuals folder: every file, except that a .meta file is
copied only where the visuals folder has none at that path (existing GUIDs survive).

Usage: python -I copy_bundle.py <bundle root (unity/<Name>)> <visuals folder>
"""
import shutil
import sys
from pathlib import Path

bundle = Path(sys.argv[1])
visuals = Path(sys.argv[2])
copied, kept = [], []
for src in sorted(p for p in bundle.rglob("*") if p.is_file()):
    rel = src.relative_to(bundle)
    dst = visuals / rel
    if src.suffix == ".meta" and dst.exists():
        kept.append(rel.as_posix())
        continue
    dst.parent.mkdir(parents=True, exist_ok=True)
    shutil.copyfile(src, dst)
    copied.append(rel.as_posix())
print("copied:", *copied, sep="\n  ")
print("kept existing .meta:", *kept, sep="\n  ")
