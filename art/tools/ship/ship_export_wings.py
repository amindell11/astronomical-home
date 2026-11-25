"""Export a Valis-family ship, whose wings swing, through the unmodified ship_export.

CLI: python ship_export_wings.py --ship SHIP_JSON --out DIR [--blender EXE]
ship_export refuses role.move.*, so this builds temporary copies of the source, one per output,
and exports each with the standard tools:
  <Name>.fbx         intact export with every role.move part left out of the drawn roles: Hull,
                     Sockets, and a Collider whose outline spans the wings in both thrust and idle pose.
  <Name>Wings.fbx    one mesh per role.move.<id> group, exported through debris mode (paint and
                     contour kept), in the source pose, which is the thrust pose.
  <Name>Breakup.fbx  the source's own role.debris groups, when it has them.
Each move group <id> has a socket Hinge.<id>: the pivot the group turns about. Its rotation is
the idle pose, so a wing at thrust sits as modelled and idles at the socket's rotation.
The source is never saved; copies live under <out>/work.
"""

import argparse
import json
import os
from pathlib import Path
import shutil
import subprocess
import sys

TOOLS = Path(__file__).resolve().parent
DEFAULT_BLENDER = r"C:\Program Files\Blender Foundation\Blender 5.1\blender.exe"


def prepare(mode, target, wings_name):
    """Inside Blender: reshape the opened source's role collections for one output, save as target."""
    import bmesh
    import bpy
    from mathutils import Matrix
    moves = {c.name[len("role.move."):]: c for c in bpy.data.collections if c.name.startswith("role.move.")}
    if not moves:
        raise SystemExit("No role.move collections: nothing swings")
    for gid in moves:
        if f"Hinge.{gid}" not in bpy.data.objects:
            raise SystemExit(f"role.move.{gid} has no Hinge.{gid} socket")
    moving = {o for c in moves.values() for o in c.objects}
    visual = [c for c in bpy.data.collections if c.name in ("role.hull", "role.canopy", "role.cores", "role.ink")]
    debris = [c for c in bpy.data.collections if c.name.startswith("role.debris.")]
    if mode == "intact":
        for c in visual:
            for o in [o for o in c.objects if o in moving]:
                c.objects.unlink(o)
        collider = bpy.data.collections.get("role.collider")
        if collider is None:
            collider = bpy.data.collections.new("role.collider")
            bpy.context.scene.collection.children.link(collider)
        origin = bpy.data.objects["SymmetryOrigin"].matrix_world.inverted()
        depsgraph = bpy.context.evaluated_depsgraph_get()
        for gid, c in moves.items():
            hinge = bpy.data.objects[f"Hinge.{gid}"]
            unturned = Matrix.LocRotScale(hinge.location, None, hinge.scale)
            rest = hinge.matrix_world @ hinge.matrix_basis.inverted() @ unturned
            idle = hinge.matrix_world @ rest.inverted()
            side = 1 if (origin @ hinge.matrix_world.translation).x >= 0 else -1
            for o in c.objects:
                if o.name not in collider.objects:
                    collider.objects.link(o)
                # A mirrored part spans both hinge groups; only this group's half swings about this hinge.
                mesh = bpy.data.meshes.new_from_object(o.evaluated_get(depsgraph), depsgraph=depsgraph)
                mesh.transform(origin @ idle @ o.matrix_world)
                work = bmesh.new()
                work.from_mesh(mesh)
                bmesh.ops.delete(work, geom=[v for v in work.verts if v.co.x * side < 0], context="VERTS")
                work.to_mesh(mesh)
                work.free()
                pose = bpy.data.objects.new(f"{o.name} idle {gid}", mesh)
                pose.matrix_world = origin.inverted()
                collider.objects.link(pose)
    elif mode == "wings":
        for c in visual:
            for o in [o for o in c.objects if o not in moving]:
                c.objects.unlink(o)
        for c in debris:
            bpy.data.collections.remove(c)
        for gid, c in moves.items():
            group = bpy.data.collections.new("role.debris." + gid)
            bpy.context.scene.collection.children.link(group)
            for o in c.objects:
                group.objects.link(o)
    elif mode == "breakup":
        if not debris:
            raise SystemExit("No role.debris collections")
    for c in moves.values():
        bpy.data.collections.remove(c)
    bpy.context.preferences.filepaths.save_version = 0
    bpy.ops.wm.save_as_mainfile(filepath=target, relative_remap=True)


def blender(exe, *args):
    env = {k: v for k, v in os.environ.items() if k != "OCIO"}
    run = subprocess.run([exe, "-b", "--factory-startup", *args], env=env, capture_output=True, text=True)
    if run.returncode != 0:
        sys.stderr.write(run.stdout[-4000:] + run.stderr[-4000:])
        raise SystemExit(f"Blender failed ({run.returncode}): {' '.join(args[:4])}")
    return run.stdout + run.stderr


def main():
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument("--ship", required=True)
    parser.add_argument("--out", required=True)
    parser.add_argument("--blender", default=os.environ.get("BLENDER", DEFAULT_BLENDER))
    args = parser.parse_args()
    ship_path = Path(args.ship).resolve()
    ship = json.loads(ship_path.read_text(encoding="utf-8"))
    source = ship_path.parent / ship["source"]
    out = Path(args.out).resolve()
    work = out / "work"
    shutil.rmtree(work, ignore_errors=True)
    out.mkdir(parents=True, exist_ok=True)
    name = ship["name"]
    outputs = {}
    for mode, export_name, debris in (("intact", name, False), ("wings", name + "Wings", True), ("breakup", name, True)):
        folder = work / mode
        folder.mkdir(parents=True)
        copy = folder / f"{export_name}.blend"
        try:
            blender(args.blender, str(source), "--python-exit-code", "1", "-P", __file__, "--", "--prepare", mode, str(copy), export_name)
        except SystemExit as missing:
            if mode == "breakup":
                print(f"BREAKUP skipped: {missing}")
                continue
            raise
        copied = dict(ship, name=export_name, source=copy.name)
        (folder / "ship.json").write_text(json.dumps(copied, indent=2) + "\n", encoding="utf-8")
        if debris:
            blender(args.blender, "--python-exit-code", "1", "-P", str(TOOLS / "ship_lock.py"), "--",
                    "--ship", str(folder / "ship.json"), "--mode", "lock", "--out", str(folder / "lock"))
        flags = ["--debris"] if debris else []
        log = blender(args.blender, "--python-exit-code", "1", "-P", str(TOOLS / "ship_export.py"), "--",
                      "--ship", str(folder / "ship.json"), "--out", str(folder / "export"), *flags)
        produced = export_name + ("Breakup" if debris else "")
        final = name + {"intact": "", "wings": "Wings", "breakup": "Breakup"}[mode]
        fbx = out / f"{final}.fbx"
        shutil.copyfile(folder / "export" / f"{produced}.fbx", fbx)
        sidecar = json.loads((folder / "export" / f"{produced}.export.json").read_text(encoding="utf-8"))
        sidecar.update(name=name, fbx=fbx.name, set=mode)
        (out / f"{final}.export.json").write_text(json.dumps(sidecar, indent=2) + "\n", encoding="utf-8")
        outputs[mode] = str(fbx)
        print(f"{mode.upper()} {fbx}  ({log.count('SHIP_VERDICT=pass')} pass)")
    print("VALIS_EXPORT=" + json.dumps(outputs))


if __name__ == "__main__":
    if "--prepare" in sys.argv:
        argv = sys.argv[sys.argv.index("--prepare") + 1:]
        prepare(*argv)
    else:
        main()
