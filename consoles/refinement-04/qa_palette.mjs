/** Run with Node 24. Uses an isolated headless Chrome profile and removes it afterward. */
import {spawn} from 'node:child_process';
import {readFile,writeFile,mkdtemp,rm,readdir} from 'node:fs/promises';
import {dirname,join,resolve,sep} from 'node:path';
import {fileURLToPath,pathToFileURL} from 'node:url';
import {createHash} from 'node:crypto';

const root=dirname(fileURLToPath(import.meta.url));
const profile=await mkdtemp(join(root,'.palette-qa-'));
const downloads=await mkdtemp(join(root,'.palette-downloads-'));
const sourcePath=join(root,'neutral-materials.png');
const sourceBefore=createHash('sha256').update(await readFile(sourcePath)).digest('hex');
const chrome=spawn('C:/Program Files/Google/Chrome/Application/chrome.exe',[
  '--headless=new','--disable-background-networking','--disable-component-update','--no-first-run',
  '--no-default-browser-check','--remote-debugging-port=0',`--user-data-dir=${profile}`,'about:blank'
],{windowsHide:true,stdio:'ignore'});
const sleep=ms=>new Promise(resolve=>setTimeout(resolve,ms));
async function until(fn,timeout=15000){const started=Date.now();let last;while(Date.now()-started<timeout){try{const result=await fn();if(result)return result;}catch(error){last=error;}await sleep(80);}throw last||new Error('Timed out waiting for browser.');}
let ws,requestId=0,sessionId;
const pending=new Map();
function call(method,params={},session=sessionId){return new Promise((resolve,reject)=>{const id=++requestId;pending.set(id,{resolve,reject});ws.send(JSON.stringify({id,method,params,...(session?{sessionId:session}:{})}));});}
async function evaluate(expression){const result=await call('Runtime.evaluate',{expression,awaitPromise:true,returnByValue:true});if(result.exceptionDetails)throw new Error(JSON.stringify(result.exceptionDetails));return result.result.value;}
const checks={};
try{
  const portText=await until(async()=>await readFile(join(profile,'DevToolsActivePort'),'utf8'));
  const [port,endpoint]=portText.trim().split(/\r?\n/);
  ws=new WebSocket(`ws://127.0.0.1:${port}${endpoint}`);
  await new Promise((resolve,reject)=>{ws.addEventListener('open',resolve,{once:true});ws.addEventListener('error',reject,{once:true});});
  ws.addEventListener('message',event=>{const message=JSON.parse(event.data);if(message.id&&pending.has(message.id)){const item=pending.get(message.id);pending.delete(message.id);message.error?item.reject(new Error(JSON.stringify(message.error))):item.resolve(message.result);}});
  const {targetId}=await call('Target.createTarget',{url:'about:blank'},null);
  ({sessionId}=await call('Target.attachToTarget',{targetId,flatten:true},null));
  await call('Page.enable');await call('Runtime.enable');
  await call('Emulation.setDeviceMetricsOverride',{width:1600,height:1000,deviceScaleFactor:1,mobile:false});
  await call('Browser.setDownloadBehavior',{behavior:'allow',downloadPath:downloads},null);
  await call('Page.navigate',{url:pathToFileURL(join(root,'palette.html')).href});
  await until(()=>evaluate('window.consolePalette?.renderReady === true'));
  await evaluate('document.fonts.ready.then(()=>true)');
  checks.fileProtocol=await evaluate('location.protocol');
  if(checks.fileProtocol!=='file:')throw new Error('The test did not use file://.');
  checks.initial=await evaluate(`(async()=>{
    const assets=window.CONSOLE_PALETTE_ASSETS;
    async function pixels(src){const image=new Image();image.src=src;await image.decode();const canvas=document.createElement('canvas');canvas.width=image.width;canvas.height=image.height;const context=canvas.getContext('2d');context.drawImage(image,0,0);return context.getImageData(0,0,image.width,image.height).data;}
    const source=await pixels(assets.image),mask=await pixels(assets.mask);
    window.__paletteQA={source,mask};
    const rendered=document.getElementById('view').getContext('2d').getImageData(0,0,1670,941).data;
    let changed=0,outside=0;const perRegion={};
    for(let i=0;i<source.length;i+=4){let differs=false;for(let c=0;c<4;c++)if(source[i+c]!==rendered[i+c])differs=true;if(differs){changed++;if(mask[i]===0||mask[i+1]===0)outside++;perRegion[mask[i]]=(perRegion[mask[i]]||0)+1;}}
    return {changedPixels:changed,changedOutsideMask:outside,changedByRegion:perRegion,roleCount:Object.keys(consolePalette.state.roles).length};
  })()`);
  if(checks.initial.changedOutsideMask!==0||checks.initial.changedPixels<10000)throw new Error('Default recoloring mask check failed.');
  const compareExpression=`(()=>{const current=document.getElementById('view').getContext('2d').getImageData(0,0,1670,941).data;let differentBytes=0;for(let i=0;i<current.length;i++)if(current[i]!==__paletteQA.source[i])differentBytes++;return differentBytes;})()`;
  await evaluate('consolePalette.setOriginal(true)');
  checks.originalComparisonDifferentBytes=await evaluate(compareExpression);
  await evaluate("consolePalette.reset('original')");
  checks.restoredSourceDifferentBytes=await evaluate(compareExpression);
  if(checks.originalComparisonDifferentBytes!==0||checks.restoredSourceDifferentBytes!==0)throw new Error('Source comparison or restore was not pixel-identical.');
  const firstRole=await evaluate('Object.keys(consolePalette.state.roles)[0]');
  await evaluate(`consolePalette.setRole(${JSON.stringify(firstRole)},{color:'#5f665b',brightness:.74})`);
  checks.exported=await evaluate('consolePalette.export()');
  const saved=await evaluate('JSON.parse(localStorage.getItem(consolePalette.storageKey))');
  if(saved.colors[firstRole]!=='#5f665b'||saved.brightness[firstRole]!==.74)throw new Error('Persistent palette values did not match the edit.');
  await call('Page.reload');
  await until(()=>evaluate(`window.consolePalette?.renderReady === true && consolePalette.state.roles[${JSON.stringify(firstRole)}] === '#5f665b'`));
  checks.persistedAfterReload=await evaluate(`consolePalette.export().colors[${JSON.stringify(firstRole)}] === '#5f665b' && consolePalette.export().brightness[${JSON.stringify(firstRole)}] === .74`);
  if(!checks.persistedAfterReload)throw new Error('Saved palette did not survive reload.');
  checks.invalidHexRejected=await evaluate(`(()=>{const input=document.getElementById('material-'+${JSON.stringify(firstRole)}+'-hex');input.value='garbage';input.dispatchEvent(new Event('change'));return input.getAttribute('aria-invalid')==='true'&&consolePalette.state.roles[${JSON.stringify(firstRole)}]==='#5f665b';})()`);
  if(!checks.invalidHexRejected)throw new Error('Invalid hex input changed the palette.');
  await evaluate("consolePalette.reset('working')");
  await evaluate("document.getElementById('download-palette').click()");
  await until(async()=>{try{return JSON.parse(await readFile(join(downloads,'console-materials.palette.json'),'utf8'));}catch{return false;}});
  const downloaded=JSON.parse(await readFile(join(downloads,'console-materials.palette.json'),'utf8'));
  const exported=await evaluate('consolePalette.export()');
  checks.downloadedJSONMatches=JSON.stringify(downloaded)===JSON.stringify(exported);
  if(!checks.downloadedJSONMatches)throw new Error('Downloaded palette JSON differs from live state.');
  const defaultPNG=await evaluate("document.getElementById('view').toDataURL('image/png').split(',')[1]");
  await writeFile(join(root,'palette-default-preview.png'),Buffer.from(defaultPNG,'base64'));
  await evaluate("document.getElementById('download-image').click()");
  await until(async()=>{const files=await readdir(downloads);return files.includes('console-materials.png')&&!files.some(file=>file.endsWith('.crdownload'));});
  checks.downloadedPNGMatches=(await readFile(join(downloads,'console-materials.png'))).equals(Buffer.from(defaultPNG,'base64'));
  if(!checks.downloadedPNGMatches)throw new Error('Downloaded PNG differs from displayed canvas.');
  checks.layouts=[];
  for(const width of [1600,840,390]){
    await call('Emulation.setDeviceMetricsOverride',{width,height:1000,deviceScaleFactor:1,mobile:width===390});
    checks.layouts.push(await evaluate('({width:innerWidth,contentWidth:document.documentElement.scrollWidth,canvasWidth:document.getElementById("view").getBoundingClientRect().width})'));
  }
  if(checks.layouts.some(layout=>layout.contentWidth>layout.width+1))throw new Error('The controls overflow on a narrow screen.');
  await call('Emulation.setDeviceMetricsOverride',{width:1600,height:1000,deviceScaleFactor:1,mobile:false});
  await evaluate('document.fonts.ready.then(()=>true)');
  const layout=await call('Page.getLayoutMetrics');
  const shot=await call('Page.captureScreenshot',{format:'png',captureBeyondViewport:true,clip:{x:0,y:0,width:1600,height:layout.cssContentSize.height,scale:1}});
  await writeFile(join(root,'palette-editor-preview.png'),Buffer.from(shot.data,'base64'));
  const sourceAfter=createHash('sha256').update(await readFile(sourcePath)).digest('hex');
  checks.sourceSHA256=sourceAfter;checks.sourceUnchanged=sourceBefore===sourceAfter;
  if(!checks.sourceUnchanged)throw new Error('The source image changed.');
  checks.status='passed';
  await writeFile(join(root,'palette-qa.json'),JSON.stringify(checks,null,2)+'\n');
  console.log(JSON.stringify(checks));
}finally{
  if(ws?.readyState===WebSocket.OPEN){try{await call('Browser.close',{},null);}catch{}ws.close();}
  await until(()=>chrome.exitCode!==null,10000).catch(()=>chrome.kill());
  for(const directory of [profile,downloads]){
    const absolute=resolve(directory);
    if(!absolute.startsWith(resolve(root)+sep)||!absolute.split(sep).at(-1).startsWith('.palette-'))throw new Error('Refusing cleanup outside the palette workspace.');
    await rm(absolute,{recursive:true,force:true,maxRetries:10,retryDelay:150});
  }
}
