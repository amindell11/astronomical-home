"""Render the loaded scene to console-camera.png without saving changes."""
import bpy
from pathlib import Path
s=bpy.context.scene;s.cycles.samples=48
s.render.filepath=str(Path(__file__).resolve().parent/'console-camera.png')
bpy.ops.render.render(write_still=True)
