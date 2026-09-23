"""Publish completed renders to the Unity companion's dedicated import folder."""

import json
import os
from pathlib import Path
import re
import shutil
import tempfile

ASSET_FOLDER = "Assets/Visuals/Environment/Sky/Generated"


def project_folder(value):
    project = Path(value).resolve()
    if not (project / "Assets").is_dir() or not (project / "ProjectSettings/ProjectVersion.txt").is_file():
        raise ValueError("Choose the Unity project folder containing Assets and ProjectSettings")
    return project


def sky_name(value):
    if not re.fullmatch(r"[A-Za-z0-9][A-Za-z0-9_-]*", value):
        raise ValueError("Sky Name must use letters, numbers, hyphens or underscores")
    return value


def publish(out_base, project, name, final=False):
    """Replace JSON sidecars, then atomically publish the HDR; preserve Unity .meta identities."""
    project = project_folder(project)
    name = sky_name(name) + ("-8k" if final else "-draft")
    source = str(out_base)
    report = json.loads(Path(source + "_render.json").read_text(encoding="utf-8"))
    if report["format"] != "HDR" or report["width"] != 2 * report["height"]:
        raise ValueError("Unity publishing requires a completed 2:1 HDR render")
    if final and (report["width"], report["height"]) != (8192, 4096):
        raise ValueError("Final Unity publishing requires the 8K export")
    destination = project / ASSET_FOLDER
    destination.mkdir(parents=True, exist_ok=True)
    staging_root = project / "Library/SkyboxAuthoring"
    staging_root.mkdir(parents=True, exist_ok=True)
    with tempfile.TemporaryDirectory(dir=staging_root) as staging:
        for suffix in ("_preset.json", "_render.json", ".hdr"):
            staged = Path(staging) / (name + suffix)
            shutil.copyfile(source + suffix, staged)
        for suffix in ("_preset.json", "_render.json", ".hdr"):
            os.replace(Path(staging) / (name + suffix), destination / (name + suffix))
    return destination / (name + ".hdr")
