#!/usr/bin/env bash
set -euo pipefail

ROOT="src/Asteroids3D/Assets/Art"

if [[ ! -d "$ROOT" ]]; then
  echo "Art root not found: $ROOT" >&2
  exit 1
fi

python - <<'PY'
import os
import re
import sys

root = "src/Asteroids3D/Assets/Art"
name_pattern = re.compile(r'^[a-z0-9]+(?:_[a-z0-9]+)*$')

file_violations = []
dir_violations = []
ableton_violations = []

for dirpath, dirnames, filenames in os.walk(root):
    rel_dir = os.path.relpath(dirpath, root)
    if rel_dir != ".":
        dirname = os.path.basename(dirpath)
        if not name_pattern.match(dirname):
            dir_violations.append(dirpath)

    for filename in filenames:
        if filename.endswith('.meta'):
            continue

        full_path = os.path.join(dirpath, filename)
        base, ext = os.path.splitext(filename)

        if ext.lower() in {'.als', '.asd'}:
            ableton_violations.append(full_path)

        if not name_pattern.match(base) or ext != ext.lower():
            file_violations.append(full_path)

errors = False
if ableton_violations:
    errors = True
    print('Ableton source files found (should be deleted):')
    for path in ableton_violations:
        print(f'  {path}')

if dir_violations:
    errors = True
    print('Non snake_case directory names found:')
    for path in dir_violations:
        print(f'  {path}')

if file_violations:
    errors = True
    print('Non snake_case file names found:')
    for path in file_violations:
        print(f'  {path}')

if errors:
    sys.exit(1)

print('PASS: Art directories/files are snake_case and no Ableton source files remain.')
PY
