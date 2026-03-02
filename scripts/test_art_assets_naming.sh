#!/usr/bin/env bash
set -euo pipefail

python - <<'PY'
import os
import re
import sys

assets_root = "src/Asteroids3D/Assets"
visuals_root = os.path.join(assets_root, "Visuals")
audio_sfx_root = os.path.join(assets_root, "Audio", "Sfx")

if not os.path.isdir(visuals_root):
    print(f"Missing expected folder: {visuals_root}")
    sys.exit(1)
if not os.path.isdir(audio_sfx_root):
    print(f"Missing expected folder: {audio_sfx_root}")
    sys.exit(1)

# Root Art folder should no longer exist after top-level organization.
legacy_art_root = os.path.join(assets_root, "Art")
if os.path.isdir(legacy_art_root):
    print(f"Legacy Art root still exists: {legacy_art_root}")
    sys.exit(1)

folder_pattern = re.compile(r"^[A-Z][A-Za-z0-9]*$")
file_pattern = re.compile(r"^[a-z][A-Za-z0-9]*$")

folder_violations = []
file_violations = []
ableton_violations = []
visuals_root_file_violations = []

for root in (visuals_root, audio_sfx_root):
    for dirpath, _, filenames in os.walk(root):
        if dirpath != root:
            folder_name = os.path.basename(dirpath)
            if not folder_pattern.match(folder_name):
                folder_violations.append(dirpath)

        for filename in filenames:
            if filename.endswith('.meta'):
                continue

            full_path = os.path.join(dirpath, filename)
            base, ext = os.path.splitext(filename)

            if ext.lower() in {'.als', '.asd'}:
                ableton_violations.append(full_path)

            if not file_pattern.match(base) or ext != ext.lower():
                file_violations.append(full_path)

            if dirpath == visuals_root:
                visuals_root_file_violations.append(full_path)

errors = False

if ableton_violations:
    errors = True
    print("Ableton source files found:")
    for path in ableton_violations:
        print(f"  {path}")

if folder_violations:
    errors = True
    print("Folder names must be UpperCamelCase:")
    for path in folder_violations:
        print(f"  {path}")

if file_violations:
    errors = True
    print("File names must be lowerCamelCase with lowercase extension:")
    for path in file_violations:
        print(f"  {path}")

if visuals_root_file_violations:
    errors = True
    print("Loose files found in Visuals root (place in section folders):")
    for path in visuals_root_file_violations:
        print(f"  {path}")

if errors:
    sys.exit(1)

print("PASS: Assets are top-level organized with CamelCase naming rules.")
PY
