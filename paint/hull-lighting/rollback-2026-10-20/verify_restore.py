import json
import subprocess
from pathlib import Path

root = Path(__file__).resolve().parents[2]
target = "c51bfd8f05b726911e9e019f1329713a3d322320"
merged = "e3d2c47528610eca53ea97993d56475c7fedc0e3"


def git(*args):
    return subprocess.check_output(["git", *args], cwd=root).decode().strip()


def entry(ref, path):
    return git("ls-tree", ref, "--", path)


paths = git("diff-tree", "--no-commit-id", "--name-only", "-r", merged).splitlines()
rows = []
for path in paths:
    expected = entry(target, path)
    actual = entry("HEAD", path)
    assert actual == expected, path
    rows.append({"path": path, "entry": actual, "matches_original": True})

changed = set(git("diff", "--name-only", "origin/main", "HEAD").splitlines())
assert changed.issubset(paths), changed.difference(paths)
assert not git("status", "--porcelain")
report = {
    "target": target,
    "source": git("rev-parse", "HEAD"),
    "base": git("rev-parse", "origin/main"),
    "status": "passed",
    "files": rows,
    "unrelated_main_changes_preserved": True,
}
Path(__file__).with_name("restoration.json").write_text(
    json.dumps(report, indent=2), encoding="utf-8"
)
print(json.dumps(report))
