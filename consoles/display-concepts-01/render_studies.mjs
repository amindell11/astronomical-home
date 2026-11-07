/** Browser renders and review-size checks. Run with Node 24; uses an isolated headless Chrome profile. */
import {spawn} from 'node:child_process';
import {readFile,writeFile,mkdtemp,rm} from 'node:fs/promises';
import {dirname,join,resolve,sep} from 'node:path';
import {fileURLToPath,pathToFileURL} from 'node:url';
import {createHash} from 'node:crypto';
const root=dirname(fileURLToPath(import.meta.url));
const profile=await mkdtemp(join(root,'.render-profile-'));
const chrome=spawn('C:/Program Files/Google/Chrome/Application/chrome.exe',['--headless=new','--disable-background-networking','--no-first-run','--no-default-browser-check','--remote-debugging-port=0',`--user-data-dir=${profile}`,'about:blank'],{windowsHide:true,stdio:'ignore'});
const sleep=ms=>new Promise(resolve=>setTimeout(resolve,ms));
async function until(fn,timeout=15000){let last;for(const start=Date.now();Date.now()-start<timeout;){try{const result=await fn();if(result)return result;}catch(error){last=error;}await sleep(80);}throw last||new Error('Timed out waiting for render.');}
let ws,sessionId,id=0;const pending=new Map();
function call(method,params={},session=sessionId){return new Promise((resolve,reject)=>{const key=++id;pending.set(key,{resolve,reject});ws.send(JSON.stringify({id:key,method,params,...(session?{sessionId:session}:{})}));});}
async function evaluate(expression){const result=await call('Runtime.evaluate',{expression,awaitPromise:true,returnByValue:true});if(result.exceptionDetails)throw new Error(JSON.stringify(result.exceptionDetails));return result.result.value;}
const checks={protocol:'file:',studies:[]};
try{
  const portText=await until(()=>readFile(join(profile,'DevToolsActivePort'),'utf8'));const [port,endpoint]=portText.trim().split(/\r?\n/);
  ws=new WebSocket(`ws://127.0.0.1:${port}${endpoint}`);await new Promise((resolve,reject)=>{ws.addEventListener('open',resolve,{once:true});ws.addEventListener('error',reject,{once:true});});
  ws.addEventListener('message',event=>{const message=JSON.parse(event.data);if(message.id&&pending.has(message.id)){const p=pending.get(message.id);pending.delete(message.id);message.error?p.reject(new Error(JSON.stringify(message.error))):p.resolve(message.result);}});
  const {targetId}=await call('Target.createTarget',{url:'about:blank'},null);({sessionId}=await call('Target.attachToTarget',{targetId,flatten:true},null));await call('Page.enable');await call('Runtime.enable');
  await call('Emulation.setDeviceMetricsOverride',{width:850,height:1228,deviceScaleFactor:1,mobile:false});
  for(const letter of 'ABCDEF'){
    await call('Page.navigate',{url:pathToFileURL(join(root,'study.html')).href+'?render='+letter});
    await until(()=>evaluate('window.ScreenStudies?.ready === true'));
    const result=await evaluate(`(()=>{
      const study=document.querySelector('.study'),concept=ScreenStudies.concepts.find(c=>c.id==='${letter}');
      const overflow=[];
      for(const section of study.querySelectorAll('.main-screen,.weapon-strip')){
        const bounds=section.getBoundingClientRect();
        for(const element of section.querySelectorAll('h1,h2,p,.stat-label,.readout,.component-kind,.bar,img')){
          const r=element.getBoundingClientRect();
          if(r.left<bounds.left-.5||r.right>bounds.right+.5||r.top<bounds.top-.5||r.bottom>bounds.bottom+.5)overflow.push({element:element.className||element.tagName,text:element.textContent.slice(0,40),x:r.x,y:r.y,w:r.width,h:r.height});
          if(element.scrollWidth>element.clientWidth+1)overflow.push({textOverflow:element.textContent.slice(0,40),scroll:element.scrollWidth,width:element.clientWidth});
        }
      }
      return {id:'${letter}',file:concept.file,width:study.offsetWidth,height:study.offsetHeight,bars:study.querySelectorAll('.bar').length,weaponCount:study.querySelectorAll('.weapon').length,ship:ScreenStudies.data.ship,keystone:study.querySelector('.keystone-icon').getAttribute('src'),fonts:{label:document.fonts.check('500 28px "HUD Label"'),readout:document.fonts.check('500 28px "HUD Readout"'),heading:document.fonts.check('400 34px "HUD Heading"')},weights:{label:getComputedStyle(study.querySelector('.stat-label')).fontWeight,readout:getComputedStyle(study.querySelector('.readout')).fontWeight,heading:getComputedStyle(study.querySelector('.ship-name')).fontWeight},overflow};
    })()`);
    const shot=await call('Page.captureScreenshot',{format:'png',captureBeyondViewport:true,clip:{x:0,y:0,width:result.width,height:result.height,scale:1}});await writeFile(join(root,result.file),Buffer.from(shot.data,'base64'));checks.studies.push(result);
  }
  await call('Emulation.setDeviceMetricsOverride',{width:1800,height:2400,deviceScaleFactor:1,mobile:false});
  await call('Page.navigate',{url:pathToFileURL(join(root,'index.html')).href+'?contact=1'});
  await until(()=>evaluate("document.readyState==='complete' && Array.from(document.images).filter(image=>image.hasAttribute('src')).every(image=>image.complete&&image.naturalWidth>0)"));
  await evaluate('document.fonts.ready.then(()=>true)');
  const box=await evaluate('(()=>{const r=document.getElementById("contact-sheet").getBoundingClientRect();return {x:r.x,y:r.y,width:r.width,height:r.height};})()');
  const sheet=await call('Page.captureScreenshot',{format:'png',captureBeyondViewport:true,clip:{...box,scale:1}});await writeFile(join(root,'contact-sheet.png'),Buffer.from(sheet.data,'base64'));
  const provenance=JSON.parse(await readFile(join(root,'assets/provenance.json'),'utf8'));checks.assetHashes=[];
  for(const file of provenance.files){const bytes=await readFile(join(root,'assets',file.file));const hash=createHash('sha256').update(bytes).digest('hex');checks.assetHashes.push({file:file.file,verified:hash===file.sha256});}
  checks.contactSheet=box;checks.status=checks.studies.every(s=>s.bars===6&&s.weaponCount===2&&s.overflow.length===0&&Object.values(s.fonts).every(Boolean))&&checks.assetHashes.every(a=>a.verified)?'passed':'needs review';
  await writeFile(join(root,'render-checks.json'),JSON.stringify(checks,null,2)+'\n');console.log(JSON.stringify(checks));
}finally{
  if(ws?.readyState===WebSocket.OPEN){try{await call('Browser.close',{},null);}catch{}ws.close();}
  await until(()=>chrome.exitCode!==null,10000).catch(()=>chrome.kill());
  const absolute=resolve(profile);if(!absolute.startsWith(resolve(root)+sep)||!absolute.split(sep).at(-1).startsWith('.render-profile-'))throw new Error('Invalid render profile cleanup path.');
  await rm(absolute,{recursive:true,force:true,maxRetries:10,retryDelay:150});
}
