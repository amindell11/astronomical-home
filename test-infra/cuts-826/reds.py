"""Print non-passing test cases (name, result, first message line) from the newest run's XML in a slot."""
import glob, os, re, sys, html
import xml.etree.ElementTree as ET

d = sys.argv[1] if len(sys.argv) > 1 else "D:/amind/git/agent-3/results/unity-tests-agent"
stamp = sys.argv[2] if len(sys.argv) > 2 else None
if not stamp:
    newest = max(glob.glob(os.path.join(d, "*-summary.json")), key=os.path.getmtime)
    newest = [p for p in glob.glob(os.path.join(d, "*-summary.json")) if "latest" not in p]
    stamp = os.path.basename(max(newest, key=os.path.getmtime)).replace("-summary.json", "")
print("run", stamp)
for f in sorted(glob.glob(os.path.join(d, stamp + "-*.xml"))):
    root = ET.parse(f).getroot()
    cases = list(root.iter("test-case"))
    bad = [c for c in cases if c.get("result") != "Passed"]
    print(os.path.basename(f), "cases", len(cases), "non-passing", len(bad))
    for c in bad:
        msg = c.findtext("failure/message") or c.findtext("reason/message") or ""
        print("  ", c.get("result"), c.get("fullname"))
        for line in msg.strip().splitlines()[:4]:
            print("      ", line.strip()[:220])
