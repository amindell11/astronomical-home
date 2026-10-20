"""Rewrite Nightshade's Breakup/<Region>.mat files from the ship_paint bundle materials.

The breakup copies differ from the hull materials only by m_CustomRenderQueue 3100 and
_SootStrength 0.1 (debris render queue and soot), so each is the bundle material with those two
values applied, keeping its own GUID (the .meta is untouched).

Usage: python -I regen_breakup_materials.py <visuals folder> <palette>
"""
import re
import sys
from pathlib import Path

visuals = Path(sys.argv[1])
palette = sys.argv[2]
breakup = visuals / "Breakup"
source = visuals / "Materials" / palette
changed = []
for mat in sorted(breakup.glob("Nightshade *.mat")):
    text = (source / mat.name).read_text(encoding="utf-8")
    text, n_queue = re.subn(r"^  m_CustomRenderQueue: .*$", "  m_CustomRenderQueue: 3100", text, flags=re.M)
    text, n_soot = re.subn(r"^    - _SootStrength: .*$", "    - _SootStrength: 0.1", text, flags=re.M)
    if n_queue != 1 or n_soot != 1:
        raise SystemExit(f"{mat.name}: expected one render queue and one soot line, got {n_queue}/{n_soot}")
    if mat.read_text(encoding="utf-8") != text:
        mat.write_text(text, encoding="utf-8", newline="\n")
        changed.append(mat.name)
print("rewrote:", ", ".join(changed) or "nothing")
