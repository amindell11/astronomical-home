import json
from pathlib import Path
p=Path('art/ships/vanguard/drawn-study')
layout=json.loads((p/'service-panels.json').read_text())
layout['palette']={'charcoal':[.38,.39,.41],'gray':[.62,.62,.59],'edge':[.80,.78,.72]}
cover=next(x for x in layout['patches'] if x['name']=='aft pod access cover')['points']
a,b,c,d=cover
lerp=lambda a,b,t:[a[i]+(b[i]-a[i])*t for i in range(2)]
def point(u,v):return lerp(lerp(a,b,v),lerp(d,c,v),u)
for i,v in enumerate([.16,.31,.46,.61,.76]):
 for color,start,end,offset in [('charcoal',v,v+.045,.0013),('edge',v+.046,v+.060,.0014)]:
  layout['patches'].append({'name':f'pod ventilation slat {i+1} {color}','object':'MVP wing_armor','mirror':True,'color':color,'offset':offset,'points':[[round(q,5) for q in point(u,t)] for u,t in [(.19,start),(.81,start),(.81,end),(.19,end)]]})
(p/'service-panels.json').write_text(json.dumps(layout,indent=2)+'\n')
lines=[]
def stroke(name,obj,points,width=.0024):
 lines.append({'name':name,'object':'MVP '+obj,'points':[[round(v,5) for v in xy] for xy in points],'width':width,'offset':.0017})
def hatch(name,obj,origin,step,direction,lengths,cross=False):
 for i,length in enumerate(lengths):
  x,y=origin[0]+i*step[0],origin[1]+i*step[1]
  dx,dy=direction[0]*length,direction[1]*length
  stroke(name+' '+str(i+1),obj,[(x,y),(x+dx*.47+step[0]*.055,y+dy*.47),(x+dx,y+dy)],.0020 if i%3 else .0026)
 if cross:
  x,y=origin
  for i,shift in enumerate([.20,.48,.76]):
   dx,dy=direction[0]*shift,direction[1]*shift
   stroke(name+' cross '+str(i+1),obj,[(x+dx-step[0]*.15,y+dy-step[1]*.15),(x+dx+step[0]*(len(lengths)-.5),y+dy+step[1]*(len(lengths)-.5))],.0018)
# Each bundle is positioned against a particular seam, corner or wear edge.
hatch('starboard pod inner seam','wing_armor',(.258,-.176),(.009,-.008),(.023,.024),[.7,.9,1,.8,.65],True)
hatch('port pod inner seam','wing_armor',(-.258,-.162),(-.009,-.009),(-.022,.024),[.85,1,.8,.65],True)
hatch('starboard forward armor corner','wing_armor',(.290,.057),(.008,-.006),(.012,.014),[.7,.95,1,.75])
hatch('port forward armor corner','wing_armor',(-.294,.049),(-.008,-.006),(-.013,.015),[1,.8,.65])
hatch('starboard cockpit seam','Cockpit.001',(.115,.476),(.007,.012),(.025,-.013),[.7,1,.86,.67],True)
hatch('port cockpit seam','Cockpit.001',(-.115,.469),(-.007,.012),(-.021,-.015),[.9,1,.8,.65],True)
hatch('starboard dorsal joint','Fuselage',(.10,-.065),(.003,.011),(-.020,.013),[.65,.9,1,.8],True)
hatch('port dorsal joint','Fuselage',(-.10,-.038),(-.003,.011),(.022,.011),[1,.85,.6],True)
hatch('starboard pod aft corner','Wing',(.480,-.371),(-.007,-.009),(-.021,.018),[.65,.9,1,.75],True)
hatch('port pod aft corner','Wing',(-.484,-.359),(.007,-.009),(.023,.014),[.8,1,.75],True)
hatch('starboard spar root','Sparrow Tail',(.209,-.278),(.006,-.012),(.022,.011),[.7,.95,.8])
hatch('port spar root','Sparrow Tail',(-.207,-.276),(-.005,-.012),(-.019,.012),[1,.8,.6])
for name,obj,pts,w in [
 ('starboard nose rub','Cockpit.001',[(.065,.892),(.055,.908),(.037,.926)],.0027),
 ('starboard nose broken rub','Cockpit.001',[(.065,.898),(.054,.916)],.0019),
 ('port nose rub','Cockpit.001',[(-.072,.874),(-.064,.889),(-.047,.909)],.0025),
 ('port nose nick','Cockpit.001',[(-.057,.900),(-.043,.906),(-.039,.918)],.0020),
 ('starboard canopy edge scuff','Cockpit.001',[(.118,.742),(.122,.729),(.126,.711)],.0025),
 ('starboard canopy scuff fork','Cockpit.001',[(.121,.738),(.127,.727)],.0018),
 ('port canopy edge scuff','Cockpit.001',[(-.129,.703),(-.125,.724),(-.119,.741)],.0025),
 ('starboard pod edge rub','wing_armor',[(.414,-.130),(.418,-.156),(.417,-.174)],.0028),
 ('starboard pod edge nick','wing_armor',[(.414,-.151),(.407,-.158),(.414,-.166)],.0020),
 ('port pod edge rub','wing_armor',[(-.415,-.183),(-.418,-.164),(-.416,-.145)],.0026),
 ('port pod chipped corner','wing_armor',[(-.410,-.164),(-.417,-.175),(-.411,-.180)],.0021),
 ('rear spine seam rub','Fuselage',[(.026,-.724),(.030,-.705),(.030,-.681)],.0024),
 ('rear spine seam break','Fuselage',[(.026,-.664),(.029,-.646)],.0020),
 ('starboard spar outer scuff','Sparrow Tail',[(.209,-.783),(.212,-.765),(.214,-.752)],.0023),
 ('port spar outer scuff','Sparrow Tail',[(-.213,-.813),(-.214,-.795)],.0023)]:
 stroke(name,obj,pts,w)
(p/'wear-lines.json').write_text(json.dumps({'source':'Authored edge scuffs and seam-local hatch bundles; base finish, not damage states.','lines':lines},indent=2)+'\n')
print('wear strokes:',len(lines),'panel shapes:',sum(2 if x['mirror'] else 1 for x in layout['patches']))
