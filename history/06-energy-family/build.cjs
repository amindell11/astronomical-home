// Writes energy-hit-family.html. Stdout reports its path, size, source hash, and settings as JSON.
// Requires the approved stage-05 source, settings, and engine. Failures exit nonzero.
const fs=require('node:fs'),path=require('node:path'),crypto=require('node:crypto');
const read=(name)=>fs.readFileSync(path.join(__dirname,name));
const source=read('../05-motion/source.png'),sourceHash=crypto.createHash('sha256').update(source).digest('hex');
if(sourceHash!=='0eb51ba237edc0a04427f49863250b4d234705e9a07e1de12a1da3061c9e6108')throw Error('The approved source changed.');
const settings=JSON.parse(read('settings.json')),baseline=JSON.parse(read('../05-motion/settings.json'));
const replacements={__ENGINE__:read('../05-motion/motion.js').toString(),__SETTINGS__:JSON.stringify(settings),__BASELINE__:JSON.stringify(baseline),__SOURCE__:'data:image/png;base64,'+source.toString('base64'),__BACKDROP__:'data:image/webp;base64,'+read('../../style-frame.webp').toString('base64')};
let html=read('tuner.template.html').toString();for(const [key,value] of Object.entries(replacements))html=html.replace(key,value);
if(Buffer.byteLength(html)>1000000)throw Error('The inline comparison exceeds 1 MB.');
const output=path.join(__dirname,'energy-hit-family.html');fs.writeFileSync(output,html);
process.stdout.write(JSON.stringify({output,bytes:Buffer.byteLength(html),sourceHash,baseline,settings})+'\n');
