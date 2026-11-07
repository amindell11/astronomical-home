/* Run: node export_ui.mjs. Exports native canvas PNGs and manifest.json beside this script.
   Exit 0 reports one JSON verification result; failures exit nonzero. Source files stay read-only. */
import {spawn} from 'node:child_process';
import {readFile,writeFile,mkdtemp,rm} from 'node:fs/promises';
import {createHash} from 'node:crypto';
import {dirname,join,resolve,sep} from 'node:path';
import {fileURLToPath,pathToFileURL} from 'node:url';

const outputRoot=dirname(fileURLToPath(import.meta.url));
const artifactRoot=resolve(outputRoot,'../../../..');
const sourceRoot=join(artifactRoot,'consoles/interactive-console-05');
const assetRoot=join(artifactRoot,'consoles/display-concepts-01/assets');
const sha=bytes=>createHash('sha256').update(bytes).digest('hex');
const sleep=ms=>new Promise(resolve=>setTimeout(resolve,ms));
async function until(fn,timeout=20000){let last;for(const start=Date.now();Date.now()-start<timeout;){try{const result=await fn();if(result)return result;}catch(error){last=error;}await sleep(80);}throw last||new Error('UI export timed out.');}
const fontDefinitions=[
  {role:'labels',family:'HUD Label',name:'Rajdhani Medium',weight:500,file:'Rajdhani-Medium.ttf'},
  {role:'numbers',family:'HUD Readout',name:'Orbitron Medium',weight:500,file:'Orbitron-Medium.ttf'},
  {role:'headings',family:'HUD Heading',name:'Michroma',weight:400,file:'Michroma.ttf'}
];
const sources=[
  ...['index.html','console.js','console.css','embedded-assets.js'].map(file=>join(sourceRoot,file)),
  join(artifactRoot,'consoles/display-concepts-01/screen-data.js'),
  join(assetRoot,'screen-data.json'),join(assetRoot,'provenance.json'),
  ...['type.css','roles.css','personality.js'].map(file=>join(artifactRoot,'combat-hud/shared',file)),
  ...fontDefinitions.map(font=>join(assetRoot,'fonts',font.file))
];
const sourceRecords=await Promise.all(sources.map(async path=>{const bytes=await readFile(path);return {path,bytes:bytes.length,sha256:sha(bytes)};}));
const profile=await mkdtemp(join(outputRoot,'.export-profile-'));
const chrome=spawn('C:/Program Files/Google/Chrome/Application/chrome.exe',['--headless=new','--enable-unsafe-swiftshader','--disable-background-networking','--no-first-run','--no-default-browser-check','--remote-debugging-port=0',`--user-data-dir=${profile}`,'about:blank'],{windowsHide:true,stdio:'ignore'});
let ws,sessionId,id=0;
const pending=new Map(),browserErrors=[];
function call(method,params={},session=sessionId){return new Promise((resolve,reject)=>{const key=++id;pending.set(key,{resolve,reject});ws.send(JSON.stringify({id:key,method,params,...(session?{sessionId:session}:{})}));});}
async function evaluate(expression){const result=await call('Runtime.evaluate',{expression,awaitPromise:true,returnByValue:true});if(result.exceptionDetails)throw new Error(JSON.stringify(result.exceptionDetails));return result.result.value;}
const exports=[];
async function exportCanvas(surface,state,file,expected){
  const result=await evaluate(`(()=>{const p=consolePrototype;p.select(${JSON.stringify(state)});const canvas=p.inputs.${surface},pixels=canvas.getContext('2d').getImageData(0,0,canvas.width,canvas.height).data;let transparent=0,partial=0,opaque=0,minX=canvas.width,minY=canvas.height,maxX=-1,maxY=-1;for(let i=0;i<pixels.length;i+=4){const alpha=pixels[i+3];if(alpha===0)transparent++;else{if(alpha===255)opaque++;else partial++;const n=i/4,x=n%canvas.width,y=Math.floor(n/canvas.width);minX=Math.min(minX,x);minY=Math.min(minY,y);maxX=Math.max(maxX,x);maxY=Math.max(maxY,y);}}return {png:canvas.toDataURL('image/png'),width:canvas.width,height:canvas.height,transparentPixels:transparent,partialAlphaPixels:partial,opaquePixels:opaque,visibleBounds:{minX,minY,maxX,maxY},snapshot:p.snapshot()};})()`);
  if(result.width!==expected[0]||result.height!==expected[1])throw new Error('Unexpected canvas dimensions: '+file);
  if(!result.transparentPixels||!result.partialAlphaPixels||!result.opaquePixels)throw new Error('Missing native ink/alpha coverage: '+file);
  if(result.snapshot.selected!==state||result.snapshot.detail!==state)throw new Error('Unexpected exported state: '+file);
  const bytes=Buffer.from(result.png.split(',')[1],'base64');
  if(bytes.readUInt32BE(16)!==result.width||bytes.readUInt32BE(20)!==result.height)throw new Error('PNG dimensions do not match source canvas: '+file);
  await writeFile(join(outputRoot,file),bytes);
  const {png,...metadata}=result;
  exports.push({file,surface,source:`window.consolePrototype.inputs.${surface}`,state,bytes:bytes.length,sha256:sha(bytes),...metadata});
}
try{
  const [port,endpoint]=(await until(()=>readFile(join(profile,'DevToolsActivePort'),'utf8'))).trim().split(/\r?\n/);
  ws=new WebSocket(`ws://127.0.0.1:${port}${endpoint}`);
  await new Promise((resolve,reject)=>{ws.addEventListener('open',resolve,{once:true});ws.addEventListener('error',reject,{once:true});});
  ws.addEventListener('message',event=>{const message=JSON.parse(event.data);if(message.method==='Runtime.exceptionThrown')browserErrors.push(message.params.exceptionDetails);if(message.method==='Runtime.consoleAPICalled'&&message.params.type==='error')browserErrors.push(message.params.args);if(message.id&&pending.has(message.id)){const p=pending.get(message.id);pending.delete(message.id);message.error?p.reject(new Error(JSON.stringify(message.error))):p.resolve(message.result);}});
  const {targetId}=await call('Target.createTarget',{url:'about:blank'},null);
  ({sessionId}=await call('Target.attachToTarget',{targetId,flatten:true},null));
  await call('Page.enable');await call('Runtime.enable');
  await call('Emulation.setDeviceMetricsOverride',{width:1672,height:941,deviceScaleFactor:1,mobile:false});
  const sourceUrl=pathToFileURL(join(sourceRoot,'index.html')).href+'?capture=1&test=1';
  await call('Page.navigate',{url:sourceUrl});
  await until(()=>evaluate('window.consolePrototype?.ready===true'));
  const runtime=await evaluate(`(async()=>{await document.fonts.ready;return {fonts:[...document.fonts].filter(font=>font.family.startsWith('HUD ')).map(font=>({family:font.family,weight:font.weight,status:font.status,style:font.style})),loadedFamilies:consolePrototype.fonts,content:SCREEN_STUDY_DATA,iconSources:CONSOLE_ASSETS.icons,devicePixelRatio,canvasColorSpace:consolePrototype.inputs.main.getContext('2d').getContextAttributes().colorSpace};})()`);
  for(const font of fontDefinitions){if(!runtime.fonts.some(face=>face.family===font.family&&face.weight===String(font.weight)&&face.status==='loaded'))throw new Error('HUD font unavailable: '+font.name);}
  const iconRecords=[];
  for(const [relative,dataUrl] of Object.entries(runtime.iconSources)){
    const path=join(assetRoot,relative),bytes=await readFile(path),embedded=Buffer.from(dataUrl.split(',')[1],'base64');
    if(!bytes.equals(embedded))throw new Error('Embedded icon differs from asset source: '+relative);
    iconRecords.push({path,sha256:sha(bytes),bytes:bytes.length,embeddedBytesMatch:true});
  }
  delete runtime.iconSources;
  await exportCanvas('main','hull','main-hud.png',[370,319]);
  await exportCanvas('detail','hull','detail-hud.png',[364,70]);
  for(const state of ['lasers','missiles','dash'])await exportCanvas('detail',state,`detail-${state}.png`,[364,70]);
  if(browserErrors.length)throw new Error('Browser errors: '+JSON.stringify(browserErrors));
  for(const record of sourceRecords){if(sha(await readFile(record.path))!==record.sha256)throw new Error('Source changed during export: '+record.path);}
  const manifest={schemaVersion:1,exportedAt:new Date().toISOString(),sourceUrl,sourceHtml:join(sourceRoot,'index.html'),acceptedReference:join(sourceRoot,'preview.png'),method:'Direct native canvas.toDataURL(image/png); serialized bytes written unchanged.',pixelTreatment:'Transparent sRGB RGBA ink. No resizing, background, CRT effects, glass, scanlines, bloom, emission, or raster processing applied.',defaultSelection:'hull',detailStateFiles:{hull:'detail-hud.png',lasers:'detail-lasers.png',missiles:'detail-missiles.png',dash:'detail-dash.png'},blenderUse:{colorSpace:'sRGB',alpha:'STRAIGHT',placement:'Separate screen planes. PNG top-left is canvas top-left; preserve aspect ratio.',effects:'Apply emission and physical glass in Blender.'},fonts:fontDefinitions.map(font=>({...font,path:join(assetRoot,'fonts',font.file),sha256:sourceRecords.find(record=>record.path===join(assetRoot,'fonts',font.file)).sha256})),runtime,sources:sourceRecords,icons:iconRecords,exporter:{path:fileURLToPath(import.meta.url),sha256:sha(await readFile(fileURLToPath(import.meta.url)))},exports,verification:{browserErrors,sourceFilesUnchanged:true,nativeDimensionsVerified:true,allFontsLoaded:true,embeddedIconsMatchSources:true}};
  await writeFile(join(outputRoot,'manifest.json'),JSON.stringify(manifest,null,2)+'\n');
  console.log(JSON.stringify({status:'verified',outputRoot,manifest:'manifest.json',exports:exports.map(({file,width,height,sha256})=>({file,width,height,sha256})),fontsLoaded:true,browserErrors:0}));
}finally{
  if(ws?.readyState===WebSocket.OPEN){await Promise.race([call('Browser.close',{},null).catch(()=>{}),sleep(500)]);ws.close();}
  await until(()=>chrome.exitCode!==null,2000).catch(()=>chrome.kill());
  const absolute=resolve(profile);
  if(!absolute.startsWith(resolve(outputRoot)+sep)||!absolute.split(sep).at(-1).startsWith('.export-profile-'))throw new Error('Invalid cleanup path');
  await rm(absolute,{recursive:true,force:true,maxRetries:10,retryDelay:150});
}
