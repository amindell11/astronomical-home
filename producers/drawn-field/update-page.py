from pathlib import Path
p=Path('C:/Users/amind/.codex/visualizations/2026/09/24/01a0d4d9-91e6-7060-b2f0-69cb7ec7c752/drawn-motion/comparison.html')
s=p.read_text(encoding='utf-8-sig')
s=s.replace('<label>View <select', '<label>B contours <select id="width"><option value="contour-thick">Thicker · 3.2 px</option><option value="contour-original">Original · 1.6 px</option></select></label>\n<label>View <select')
s=s.replace('src="contour.mp4" poster="contour-flight.png"','src="contour-thick.mp4" poster="contour-thick-flight.png"')
s=s.replace('The same surface plus light-weighted geometry contours.','The same surface plus light-weighted geometry contours. Thickness is selectable above.')
s=s.replace('href="contour-flight.png"','href="contour-thick-flight.png"').replace('src="contour-flight.png"','src="contour-thick-flight.png"')
s=s.replace("document.getElementById('still').onchange=e=>document.querySelectorAll('#pictures a').forEach((a,i)=>{const path=['control','surface','contour'][i]+'-'+e.target.value+'.png';a.href=path;a.firstElementChild.src=path});", """function updateStills(){document.querySelectorAll('#pictures a').forEach((a,i)=>{const path=['control','surface',document.getElementById('width').value][i]+'-'+document.getElementById('still').value+'.png';a.href=path;a.firstElementChild.src=path})}
document.getElementById('still').onchange=updateStills;
document.getElementById('width').onchange=e=>{videos.forEach(v=>v.pause());const v=videos[2],t=Number(seek.value);v.src=e.target.value+'.mp4';v.poster=e.target.value+'-flight.png';v.addEventListener('loadedmetadata',()=>{v.currentTime=t},{once:true});v.load();updateStills()};""")
p.write_text(s,encoding='utf-8')
print('Added original/thicker B control')
