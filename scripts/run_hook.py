"""Launch a repo hook file without CLAUDE_PROJECT_DIR.

The hook shell can expand that variable to empty before the hook environment
is applied. Python then looks for `<drive>/scripts/...` and exits 2, which
refuses every command. Locate the file from the cwd and the project-dir
variables, then run it with the caller's stdin.
"""
import os
import pathlib
import runpy
import subprocess
import sys


def locate(rel):
    posix = pathlib.PurePosixPath(rel.replace("\\", "/"))
    if posix.is_absolute() or ".." in posix.parts or ":" in rel or not posix.parts:
        return None
    rel_path = pathlib.Path(*posix.parts)
    roots = []
    for key in ("CURSOR_PROJECT_DIR", "CLAUDE_PROJECT_DIR"):
        val = os.environ.get(key)
        if val:
            roots.append(pathlib.Path(val))
    here = pathlib.Path.cwd()
    roots.append(here)
    roots.extend(here.parents)
    for root in roots:
        found = root / rel_path
        if found.is_file():
            return found
    return None


def main():
    if len(sys.argv) != 2:
        sys.stderr.write("usage: run_hook.py <repo-relative path>\n")
        return 2
    found = locate(sys.argv[1])
    if found is None:
        sys.stderr.write(f"hook file not found: {sys.argv[1]}\n")
        return 2
    if found.suffix == ".py":
        sys.argv = [str(found)]
        runpy.run_path(str(found), run_name="__main__")
        return 0
    if found.suffix == ".sh":
        return subprocess.run(["bash", str(found)]).returncode
    sys.stdout.write(found.read_text(encoding="utf-8"))
    return 0


if __name__ == "__main__":
    sys.exit(main())
