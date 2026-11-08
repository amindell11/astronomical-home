import bpy,json
from pathlib import Path
s=bpy.context.scene
print('COMPOSITOR',[(p.identifier,p.type)for p in s.bl_rna.properties if 'compos' in p.identifier or 'node' in p.identifier])
for n in ['HOLO / muted red and grey panels','HOLO / bright contour','11 | Neutral projection volume']:
 m=bpy.data.materials[n];print(n)
 for node in m.node_tree.nodes: print(node.name,node.type,[(i.name,str(i.default_value))for i in node.inputs if hasattr(i,'default_value')and not i.is_linked][:6])
for n in ['07 | Main rectangular display','07 | Detail rectangular display','08 | Launch broad beveled button']:
 o=bpy.data.objects[n];print(n, 'verts',[(v.index,tuple(v.co))for v in o.data.vertices][:20], 'faces',[(p.index,len(p.vertices),tuple(p.normal))for p in o.data.polygons])
