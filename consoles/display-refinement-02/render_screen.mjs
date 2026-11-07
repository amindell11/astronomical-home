/** Render the screen replacement in an isolated headless Chrome profile. */
import {spawn} from 'node:child_process';
import {readFile,writeFile,mkdtemp,rm} from 'node:fs/promises';
import {dirname,join,resolve,sep} from 'node:path';
import {fileURLToPath,pathToFileURL} from 'node:url';
const root=dirname(fileURLToPath(import.meta.url));const profile=await mkdtemp(join(root,'.render-profile-'));
const chrome=spawn('C:/Program Files/Google/Chrome/Application/chrome.exe',['--headless=new','--disable-background-networking','--no-first-run','--no-default-browser-check','--remote-debugging-port=0',`--user-data-dir=${profile}`,'about:blank'],{windowsHide:true,stdio:'ignore'});
const sleep=ms=>new Promise(resolve=>setTimeout(resolve,ms));async function until(fn,timeout=15000){let last;for(const start=Date.now();Date.now()-start<timeout;){try{const result=await fn();if(result)return result;}catch(error){last=error;}await sleep(80);}throw last||new Error('Timed out waiting for render.');}
let ws,sessionId,id=0;const pending=new Map();let checks;
function call(method,params={},session=sessionId){return new Promise((resolve,reject)=>{const key=++id;pending.set(key,{resolve,reject});ws.send(JSON.stringify({id:key,method,params,...(session?{sessionId:session}:{})}));});}
async function evaluate(expression){const result=await call('Runtime.evaluate',{expression,awaitPromise:true,returnByValue:true});if(result.exceptionDetails)throw new Error(JSON.stringify(result.exceptionDetails));return result.result.value;}
try{
  const portText=await until(()=>readFile(join(profile,'DevToolsActivePort'),'utf8'));const [port,endpoint]=portText.trim().split(/\r?\n/);ws=new WebSocket(`ws://127.0.0.1:${port}${endpoint}`);await new Promise((resolve,reject)=>{ws.addEventListener('open',resolve,{once:true});ws.addEventListener('error',reject,{once:true});});
  ws.addEventListener('message',event=>{const message=JSON.parse(event.data);if(message.id&&pending.has(message.id)){const p=pending.get(message.id);pending.delete(message.id);message.error?p.reject(new Error(JSON.stringify(message.error))):p.resolve(message.result);}});
  const {targetId}=await call('Target.createTarget',{url:'about:blank'},null);({sessionId}=await call('Target.attachToTarget',{targetId,flatten:true},null));await call('Page.enable');await call('Runtime.enable');await call('Emulation.setDeviceMetricsOverride',{width:1672,height:941,deviceScaleFactor:1,mobile:false});await call('Page.navigate',{url:pathToFileURL(join(root,'scene.html')).href});await until(()=>evaluate('window.CompactHullStudy?.ready === true'));
  checks=await evaluate(`(()=>{const screen=document.getElementById('screen'),r=screen.getBoundingClientRect();const overflow=[];for(const e of screen.querySelectorAll('h1,img,.metric-label,.metric-value,.weapon-name,.keystone-value')){const b=e.getBoundingClientRect();if(b.left<r.left||b.right>r.right||b.top<r.top||b.bottom>r.bottom)overflow.push(e.className);}return {source:'../refinement-04/viewport-glass.png',dimensions:{width:1672,height:941},screenBounds:CompactHullStudy.screenBounds,maskPolygon:CompactHullStudy.maskPolygon,stats:Array.from(screen.querySelectorAll('.metric')).map(e=>({key:e.dataset.stat,label:e.querySelector('.metric-label').textContent,value:e.querySelector('.metric-value').textContent,segments:e.querySelectorAll('.bar i').length})),fonts:{labels:document.fonts.check('500 12px "HUD Label"'),numbers:document.fonts.check('500 16px "HUD Readout"'),heading:document.fonts.check('400 17px "HUD Heading"')},glyphs:Array.from(screen.querySelectorAll('img')).map(e=>e.getAttribute('src')),overflow};})()`);
  let image=await call('Page.captureScreenshot',{format:'png',clip:{x:0,y:0,width:1672,height:941,scale:1}});await writeFile(join(root,'full-scene.png'),Buffer.from(image.data,'base64'));
  image=await call('Page.captureScreenshot',{format:'png',clip:{...checks.screenBounds,scale:1}});await writeFile(join(root,'screen-native.png'),Buffer.from(image.data,'base64'));
  await call('Emulation.setDeviceMetricsOverride',{width:1672,height:941,deviceScaleFactor:3,mobile:false});image=await call('Page.captureScreenshot',{format:'png',clip:{...checks.screenBounds,scale:1}});await writeFile(join(root,'screen-closeup.png'),Buffer.from(image.data,'base64'));
  await writeFile(join(root,'render-checks.json'),JSON.stringify(checks,null,2)+'\n');console.log(JSON.stringify(checks));
}finally{
  if(ws?.readyState===WebSocket.OPEN){await Promise.race([call('Browser.close',{},null).catch(()=>{}),sleep(500)]);ws.close();}
  await until(()=>chrome.exitCode!==null,2000).catch(()=>chrome.kill());const absolute=resolve(profile);if(!absolute.startsWith(resolve(root)+sep)||!absolute.split(sep).at(-1).startsWith('.render-profile-'))throw new Error('Invalid cleanup path.');await rm(absolute,{recursive:true,force:true,maxRetries:10,retryDelay:150});
}
process.exit(checks?.overflow.length===0?0:1);
