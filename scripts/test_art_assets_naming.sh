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

snake_violations = []
ableton_violations = []

for dirpath, _, filenames in os.walk(root):
    for filename in filenames:
        if filename.endswith('.meta'):
            continue

        full_path = os.path.join(dirpath, filename)
        base, ext = os.path.splitext(filename)

        if ext.lower() in {'.als', '.asd'}:
            ableton_violations.append(full_path)

        if not name_pattern.match(base) or ext != ext.lower():
            snake_violations.append(full_path)

if ableton_violations or snake_violations:
    if ableton_violations:
        print('Ableton source files found (should be deleted):')
        for path in ableton_violations:
            print(f'  {path}')
    if snake_violations:
        print('Non snake_case file names found:')
        for path in snake_violations:
            print(f'  {path}')
    sys.exit(1)

print('PASS: Art filenames are snake_case and no Ableton source files remain.')
PY
