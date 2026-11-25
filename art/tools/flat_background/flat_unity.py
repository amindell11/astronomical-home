"""Publish completed renders to the Unity companion's dedicated import folder."""

import json
import os
from pathlib import Path
import re
import shutil
import tempfile

FLAT_FOLDER = "Assets/Visuals/Locales/Flat/Generated"
SUFFIXES = (".json", ".exr")


def project_folder(value):
    project = Path(value).resolve()
    if not (project / "Assets").is_dir() or not (project / "ProjectSettings/ProjectVersion.txt").is_file():
        raise ValueError("Choose the Unity project folder containing Assets and ProjectSettings")
    return project


def sky_name(value):
    if not re.fullmatch(r"[A-Za-z0-9][A-Za-z0-9_-]*", value):
        raise ValueError("Sky Name must use letters, numbers, hyphens or underscores")
    return value


def publish(out_base, project, name, final):
    """Replace the sidecar, then atomically publish the EXR; preserve Unity .meta identities."""
    project = project_folder(project)
    stage = "final" if final else "draft"
    name = f"{sky_name(name)}-{stage}"
    source = str(out_base)
    sidecar_stage = json.loads(Path(source + ".json").read_text(encoding="utf-8"))["stage"]
    if sidecar_stage != stage:
        raise ValueError(f"Sidecar stage '{sidecar_stage}' does not match the requested publish")
    destination = project / FLAT_FOLDER
    destination.mkdir(parents=True, exist_ok=True)
    staging_root = project / "Library/FlatBackgroundAuthoring"
    staging_root.mkdir(parents=True, exist_ok=True)
    with tempfile.TemporaryDirectory(dir=staging_root) as staging:
        for suffix in SUFFIXES:
            staged = Path(staging) / (name + suffix)
            shutil.copyfile(source + suffix, staged)
        for suffix in SUFFIXES:
            os.replace(Path(staging) / (name + suffix), destination / (name + suffix))
    return destination / (name + ".exr")
