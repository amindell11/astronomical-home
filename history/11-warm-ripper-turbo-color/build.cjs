const fs=require('node:fs'),path=require('node:path');
const root=path.resolve(process.argv[2]||path.join(__dirname,'../..'));
const source=fs.readFileSync(path.join(root,'art/vfx/laser-impact/source.png'));
const engine=fs.readFileSync(path.join(root,'art/vfx/laser-impact/motion.js'),'utf8');
const template=fs.readFileSync(path.join(__dirname,'study.template.html'),'utf8');
fs.writeFileSync(path.join(__dirname,'study.html'),template.replace('__SOURCE__',source.toString('base64')).replace('__ENGINE__',engine));
const selected=fs.readFileSync(path.join(__dirname,'selected.js'),'utf8');
fs.writeFileSync(path.join(__dirname,'selected-pass.html'),template.replace('__SOURCE__',source.toString('base64')).replace('__ENGINE__',engine).replace('window.renderAt=',selected+'\nwindow.renderAt=').replace('mode?drawMotion(time):drawStudy();','drawSelected(time,mode);'));
console.log(JSON.stringify({html:path.join(__dirname,'study.html')}));
