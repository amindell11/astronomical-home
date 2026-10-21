// Reads preview.html and writes stills, frames, and checks.json in this directory.
// Stdout is JSON with evidence paths and check results. Exit 1 means a failed check.
const {chromium}=require('playwright'),fs=require('node:fs'),path=require('node:path');
(async()=>{
 const browser=await chromium.launch({executablePath:process.env.CHROMIUM_EXECUTABLE,headless:true});
 try{
 const page=await browser.newPage({viewport:{width:780,height:1700},deviceScaleFactor:1,reducedMotion:'reduce'}),errors=[];
 page.on('pageerror',e=>errors.push(e.message));
 await page.goto('file:///'+path.join(__dirname,'preview.html').replaceAll('\\','/'));
 const frame=await (await page.waitForSelector('iframe')).contentFrame();await frame.waitForFunction(()=>window.energyPreview?.ready);
 const root=frame.locator('#energy-hit-family'),scenes=root.locator('[data-scenes]');
 const checks=await frame.evaluate(()=>{
  const p=energyPreview,s=p.settings;
  const alpha=(engine,settings,t)=>{const c=document.createElement('canvas');c.width=400;c.height=300;const ctx=c.getContext('2d');engine.draw(ctx,t,settings,[80,150]);return ctx.getImageData(0,0,400,300).data.reduce((sum,v,i)=>sum+(i%4===3?v:0),0);};
  const expiry=[['laser',p.baseline],['turbo',s.turbo],['charge',p.charged(.3)],['charge',p.charged(.65)],['charge',p.charged(1)]].map(([k,v])=>alpha(p.engines[k],v,v.durationMs));
  return {baseline:p.baseline,chargeSamples:[.3,.65,1].map(x=>({charge:x,...p.charged(x)})),expiryAlpha:expiry,turboVisibleBeforeExpiry:alpha(p.engines.turbo,s.turbo,75)>0};
 });
 await frame.evaluate(()=>energyPreview.render(25));await scenes.screenshot({path:path.join(__dirname,'family-25ms.png')});
 const ladder=await frame.evaluate(()=>{
  const p=energyPreview,c=document.createElement('canvas');c.width=780;c.height=220;const ctx=c.getContext('2d');ctx.fillStyle='#131c2a';ctx.fillRect(0,0,780,220);ctx.fillStyle='#e4eaf1';ctx.font='18px system-ui';
  [.3,.65,1].forEach((q,i)=>{const s=p.charged(q);ctx.fillText(`${Math.round(q*100)}% charge`,i*260+16,28);ctx.font='14px system-ui';ctx.fillText(`${Math.round(s.widthPx)} px · ${Math.round(s.durationMs)} ms`,i*260+16,52);ctx.font='18px system-ui';p.engines.charge.draw(ctx,25,s,[i*260+28,140],s.widthPx*2);});return c.toDataURL('image/png').split(',')[1];
 });fs.writeFileSync(path.join(__dirname,'charge-strengths.png'),Buffer.from(ladder,'base64'));
 await frame.locator('[data-charge]').fill('30');checks.chargeControlWorks=(await frame.locator('[data-spec="charge"]').innerText()).includes('42 px');
 await frame.locator('[data-burst]').selectOption('1');checks.burstControlWorks=(await frame.locator('[data-spec="turbo"]').innerText()).includes('six hits');
 await frame.locator('details').first().evaluate(el=>el.open=true);await frame.locator('[data-setting="turbo.durationMs"]').fill('200');
 checks.tuningUpdatesExport=JSON.parse(await frame.locator('[data-export]').inputValue()).turbo.durationMs===200;
 await frame.evaluate(()=>{window.openai={...window.openai,sendFollowUpMessage:async msg=>{window.settingsMessage=msg;}};});await frame.locator('[data-use]').click();
 checks.settingsActionWorks=await frame.evaluate(()=>settingsMessage.prompt.includes('"durationMs":200'));
 await frame.locator('[data-setting="turbo.durationMs"]').fill('160');await frame.locator('details').first().evaluate(el=>el.open=false);await frame.evaluate(()=>energyPreview.setView({charge:1,burst:false}));
 await root.screenshot({path:path.join(__dirname,'tuner.png')});
 const frameDirs={};
 for(const burst of [false,true]){
  const name=burst?'burst':'single',dir=path.join(__dirname,'frames',name);fs.mkdirSync(dir,{recursive:true});frameDirs[name]=dir;
  await frame.evaluate(b=>energyPreview.setView({burst:b,charge:1}),burst);
  for(let i=0;i<44;i++){await frame.evaluate(ms=>energyPreview.render(ms),-100+i*25);await (burst?frame.locator('[data-weapon="turbo"]'):scenes).screenshot({path:path.join(dir,String(i).padStart(3,'0')+'.png')});}
 }
 await frame.evaluate(()=>energyPreview.setView({burst:true,charge:1}));await frame.evaluate(()=>energyPreview.render(100));await frame.locator('[data-weapon="turbo"]').screenshot({path:path.join(__dirname,'turbo-overlap.png')});
 await page.setViewportSize({width:320,height:2400});await frame.evaluate(()=>energyPreview.render(25));
 checks.mobileOverflow=await frame.evaluate(()=>document.documentElement.scrollWidth>innerWidth);
 const height=await root.evaluate(el=>el.scrollHeight);await page.locator('iframe').evaluate((el,h)=>el.style.height=h+'px',height+32);await root.screenshot({path:path.join(__dirname,'mobile.png')});
 await page.setViewportSize({width:780,height:1700});await page.emulateMedia({colorScheme:'dark'});await frame.evaluate(()=>{energyPreview.setView({burst:false,charge:1});energyPreview.render(25);});await root.screenshot({path:path.join(__dirname,'tuner-dark.png')});
 checks.pageErrors=errors;
 fs.writeFileSync(path.join(__dirname,'checks.json'),JSON.stringify(checks,null,2)+'\n');
 const ok=checks.expiryAlpha.every(x=>x===0)&&checks.turboVisibleBeforeExpiry&&checks.chargeControlWorks&&checks.burstControlWorks&&checks.tuningUpdatesExport&&checks.settingsActionWorks&&!checks.mobileOverflow&&!errors.length;
 process.stdout.write(JSON.stringify({ok,checks,frameDirs,frameCount:44,stepMs:25})+'\n');if(!ok)process.exitCode=1;
 }finally{await browser.close();}
})().catch(e=>{console.error(e);process.exitCode=1;});
