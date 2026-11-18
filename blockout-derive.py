"""Run ship_new --base for one minion with the omit list from its cuts file. Usage: python derive.py <base> <out>"""
import json, os, subprocess, sys
from pathlib import Path

REPO = Path("D:/amind/git/agent-1")
BLENDER = r"C:/Program Files/Blender Foundation/Blender 5.1/blender.exe"
base, out = sys.argv[1], sys.argv[2]
minion = REPO / "art/ships" / f"{base}minion"
omit = json.loads((minion / "concepts" / f"{base}minion-cuts.json").read_text())["omit"]
env = {k: v for k, v in os.environ.items() if k != "OCIO"}
result = subprocess.run([BLENDER, "-b", "--factory-startup", "--python-exit-code", "1", "-P",
                         str(REPO / "art/tools/ship/ship_new.py"), "--", "--ship", str(minion / "ship.json"),
                         "--out", out, "--base", str(REPO / "art/ships" / base / "ship.json"), "--omit", *omit],
                        capture_output=True, text=True, env=env)
sys.stderr.write(result.stderr[-2000:])
print(result.stdout, end="")
sys.exit(result.returncode)
