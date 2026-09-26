"""Publish completed renders to the Unity companion's dedicated import folders."""

import json
import os
from pathlib import Path
import re
import shutil
import tempfile

ASSET_FOLDER = "Assets/Visuals/Environment/Sky/Generated"
FLAT_FOLDER = "Assets/Visuals/Environment/Flat/Generated"


def project_folder(value):
    project = Path(value).resolve()
    if not (project / "Assets").is_dir() or not (project / "ProjectSettings/ProjectVersion.txt").is_file():
        raise ValueError("Choose the Unity project folder containing Assets and ProjectSettings")
    return project


def sky_name(value):
    if not re.fullmatch(r"[A-Za-z0-9][A-Za-z0-9_-]*", value):
        raise ValueError("Sky Name must use letters, numbers, hyphens or underscores")
    return value


def check_sky(source, final):
    report = json.loads(Path(source + "_render.json").read_text(encoding="utf-8"))
    if report["format"] != "HDR" or report["width"] != 2 * report["height"]:
        raise ValueError("Unity publishing requires a completed 2:1 HDR render")
    if final and (report["width"], report["height"]) != (8192, 4096):
        raise ValueError("Final Unity publishing requires the 8K export")


def publish(out_base, project, name, final=False, folder=ASSET_FOLDER,
            suffixes=("_preset.json", "_render.json", ".hdr"), tags=("-draft", "-8k"), check=check_sky):
    """Replace sidecars, then atomically publish the image (last suffix); preserve Unity .meta identities."""
    project = project_folder(project)
    name = sky_name(name) + tags[final]
    source = str(out_base)
    check(source, final)
    destination = project / folder
    destination.mkdir(parents=True, exist_ok=True)
    staging_root = project / "Library/SkyboxAuthoring"
    staging_root.mkdir(parents=True, exist_ok=True)
    with tempfile.TemporaryDirectory(dir=staging_root) as staging:
        for suffix in suffixes:
            staged = Path(staging) / (name + suffix)
            shutil.copyfile(source + suffix, staged)
        for suffix in suffixes:
            os.replace(Path(staging) / (name + suffix), destination / (name + suffix))
    return destination / (name + suffixes[-1])


def check_flat(source, final):
    stage = json.loads(Path(source + ".json").read_text(encoding="utf-8"))["stage"]
    if stage != ("final" if final else "draft"):
        raise ValueError(f"Sidecar stage '{stage}' does not match the requested publish")


FLAT = dict(folder=FLAT_FOLDER, suffixes=(".json", ".exr"), tags=("-draft", "-final"), check=check_flat)
