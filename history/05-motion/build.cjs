// Writes laser-hit-tuner.html. Stdout is one JSON result; failures exit nonzero.
const fs=require('node:fs');
const path=require('node:path');
const crypto=require('node:crypto');
const here=__dirname;
const source=fs.readFileSync(path.join(here,'source.png'));
const sourceHash=crypto.createHash('sha256').update(source).digest('hex');
if(sourceHash!=='0eb51ba237edc0a04427f49863250b4d234705e9a07e1de12a1da3061c9e6108')throw Error('The approved source changed. Preserve the approved image before rebuilding.');
const template=fs.readFileSync(path.join(here,'tuner.template.html'),'utf8');
const settings=JSON.parse(fs.readFileSync(path.join(here,'settings.json'),'utf8'));
const output=template.replace('__MOTION_ENGINE__',fs.readFileSync(path.join(here,'motion.js'),'utf8')).replace('__SETTINGS__',JSON.stringify(settings)).replace('__SOURCE_IMAGE__','data:image/png;base64,'+source.toString('base64')).replace('__BACKDROP_IMAGE__','data:image/webp;base64,'+fs.readFileSync(path.join(here,'../../style-frame.webp')).toString('base64'));
if(Buffer.byteLength(output)>1000000)throw Error('The inline tuner exceeds 1 MB.');
const destination=path.join(here,'laser-hit-tuner.html');fs.writeFileSync(destination,output);
process.stdout.write(JSON.stringify({output:destination,bytes:Buffer.byteLength(output),sourceHash,settings})+'\n');
