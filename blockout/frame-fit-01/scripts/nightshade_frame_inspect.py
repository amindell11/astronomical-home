from pathlib import Path
import bpy,json,subprocess,sys
root=Path('D:/amind/git/agent-1');scratch=Path('C:/Users/amind/.codex/visualizations/2026/10/06/01a11013-205d-7b23-b294-06d76bc3b8ba')
oldsource=scratch/'frame-prior-read.blend'
with oldsource.open('wb') as f:
    subprocess.run(['git','cat-file','--filters','262eee9f:art/ships/nightshade/Nightshade.blend'],cwd=root,stdout=f,check=True)
bpy.ops.wm.open_mainfile(filepath=str(oldsource))
ob=bpy.data.objects['Rebuilt pitched hull']
(scratch/'frame-prior-hull.json').write_text(json.dumps({'vertices':[list(v.co) for v in ob.data.vertices],
    'faces':[list(p.vertices) for p in ob.data.polygons],'matrix':[list(r) for r in ob.matrix_world]}))
print('PRIOR_HULL_READ_ONLY_EXPORT')
oldsource.unlink()
