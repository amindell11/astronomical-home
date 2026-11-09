"""Render the loaded material study to console-camera.png and console-inspection.png, without saving scene changes."""
import bpy
from pathlib import Path
from mathutils import Vector
root=Path(__file__).resolve().parent;s=bpy.context.scene;s.cycles.samples=48
s.render.filepath=str(root/'console-camera.png');bpy.ops.render.render(write_still=True)
for o in s.objects:
 if o.name.startswith('07 reuse | Vanguard') or (o.name.startswith('19 | Vanguard') and o.type=='CURVE') or o.name in ['19 | Distant space','11 | Neutral projector frustum','25 | Viewport glass'] or o.name.startswith(('11 | Long chamfered','11 | Left beveled','11 | Right beveled','11 | Inset viewport')):o.hide_render=True
 if o.type=='LIGHT':o.data.energy=0
s.camera=bpy.data.objects['07 Camera - construction'];s.world.node_tree.nodes['Background'].inputs[0].default_value=(.22,.22,.22,1);s.world.node_tree.nodes['Background'].inputs[1].default_value=.5
for label,loc,power,size in [('Inspection key',(-5,-8,13),1200,10),('Inspection fill',(10,-3,8),700,8)]:
 d=bpy.data.lights.new(label,'AREA');d.energy=power;d.size=size;o=bpy.data.objects.new(label,d);s.collection.objects.link(o);o.location=loc;o.rotation_euler=(Vector((0,-1,1))-o.location).to_track_quat('-Z','Y').to_euler()
s.render.filepath=str(root/'console-inspection.png');bpy.ops.render.render(write_still=True)
