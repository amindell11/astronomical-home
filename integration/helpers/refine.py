from pathlib import Path
p=Path('D:/amind/git/agent-2/results/valis-geometry/build_valis.py')
s=p.read_text(encoding='utf-8-sig')
start=s.index("plate('03 canopy side cheeks'")
end=s.index("plate('04 dorsal armor'",start)
s=s[:start]+'''cheek_vertices=[]
for y,w,top,bottom in HULL_PROFILE[1:5]:
    for t in [.08,.92]:
        cheek_vertices.append((w*(.70+.30*t),y,top-.035+t*((top+bottom)*.5-top+.035)+.03))
mesh('03 canopy side cheeks',cheek_vertices,[(i,i+1,i+3,i+2) for i in range(0,6,2)],'Lavender',solid=.045)
'''+s[end:]
start=s.index("plate('08 ventral keel'")
end=s.index('\n',start)
s=s[:start]+'''belly_vertices=[]
for y,w,top,bottom in HULL_PROFILE:
    belly_vertices.extend([(0,y,bottom-.014),(.70*w,y,bottom-.014)])
mesh('08 ventral armor',belly_vertices,[(i,i+1,i+3,i+2) for i in range(0,len(belly_vertices)-2,2)],'Ivory',solid=.025)'''+s[end:]
start=s.index("plate('63 ventral engine stripe'")
end=s.index('\n',start)
s=s[:start]+'''stripe_vertices=[]
for y in [1.28,1.3,2.0,2.21,2.5,2.86]:
    h=hull_profile(y)[2]
    if y>=2.21:h=min(h,-.30)
    stripe_vertices.extend([(0,y,h-.023),(.16,y,h-.023)])
mesh('63 ventral engine stripe',stripe_vertices,[(i,i+1,i+3,i+2) for i in range(0,len(stripe_vertices)-2,2)],'Lavender',solid=.025)'''+s[end:]
p.write_text(s,encoding='utf-8')
