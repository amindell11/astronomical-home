// Captures preview evidence and verifies source fidelity, expiry, and tuner controls.
// Stdout is one JSON result. Exit 1 means an acceptance check failed.
const {chromium}=require('playwright');
const fs=require('node:fs');
const path=require('node:path');
(async()=>{
 const browser=await chromium.launch({executablePath:process.env.CHROMIUM_EXECUTABLE,headless:true});
 const page=await browser.newPage({viewport:{width:780,height:1300},deviceScaleFactor:1,reducedMotion:'reduce'});
 const errors=[];page.on('pageerror',e=>errors.push(e.message));
 await page.goto('file:///'+path.join(__dirname,'preview.html').replaceAll('\\','/'));
 const handle=await page.waitForSelector('iframe'),frame=await handle.contentFrame();
 await frame.waitForFunction(()=>window.impactPreview?.ready);
 const evidence=await frame.evaluate(()=>{
   const p=impactPreview,e=p.engine,s=e.parsed,settings=p.settings;
   const c=document.createElement('canvas');c.width=s.native.width;c.height=s.native.height;const ctx=c.getContext('2d');
   e.draw(ctx,0,settings,s.anchor,s.solidWidth);
   const actual=ctx.getImageData(0,0,c.width,c.height).data;
   const reference=document.createElement('canvas');reference.width=c.width;reference.height=c.height;
   const referenceContext=reference.getContext('2d');referenceContext.drawImage(s.source,0,0);
   const expected=referenceContext.getImageData(0,0,c.width,c.height).data;
   let different=0;for(let i=0;i<actual.length;i++)if(actual[i]!==expected[i])different++;
   ctx.clearRect(0,0,c.width,c.height);e.draw(ctx,settings.durationMs,settings,s.anchor,s.solidWidth);
   const ended=ctx.getImageData(0,0,c.width,c.height).data;let remainingAlpha=0;for(let i=3;i<ended.length;i+=4)remainingAlpha+=ended[i];
   const debug=document.createElement('canvas');debug.width=1200;debug.height=310;const dc=debug.getContext('2d');dc.fillStyle='#131c2a';dc.fillRect(0,0,1200,310);dc.font='18px system-ui';dc.fillStyle='#e4eaf1';
   ['Approved source',...ImpactMotion.BAND_NAMES].forEach((name,i)=>{dc.fillText(name,i*240+15,28);dc.drawImage(i===0?s.source:e.debug(i-1),i*240,45,240,240);});
   return {frameZeroDifferentChannels:different,alphaAtExpiry:remainingAlpha,bandCounts:s.counts,anchor:s.anchor,bounds:s.bounds,rays:s.rays,debug:debug.toDataURL('image/png').split(',')[1]};
 });
 fs.writeFileSync(path.join(__dirname,'parsed-bands.png'),Buffer.from(evidence.debug,'base64'));delete evidence.debug;
 await frame.evaluate(()=>impactPreview.render(40));
 await frame.locator('#laser-impact-tuner').screenshot({path:path.join(__dirname,'tuner.png')});
 for(const ms of [0,100,200,340]){await frame.evaluate(t=>impactPreview.render(t),ms);await frame.locator('.motion-scenes').screenshot({path:path.join(__dirname,`motion-${ms}ms.png`)});}
 await frame.locator('[data-setting="durationMs"]').fill('500');
 const durationChanged=await frame.locator('[data-control="time"]').getAttribute('max')==='500';
 await frame.locator('[data-setting="fanMotion"]').fill('0.24');
 const exportSettings=JSON.parse(await frame.locator('[data-export]').inputValue());
 await frame.evaluate(()=>{window.openai={...window.openai,setWidgetState:async()=>{},sendFollowUpMessage:async message=>{window.capturedSettingsMessage=message;}};});
 await frame.locator('[data-action="use"]').click();
 const settingsMessage=await frame.evaluate(()=>window.capturedSettingsMessage);
 await frame.evaluate(()=>impactPreview.setSettings({durationMs:340,widthPx:48,fanMotion:.12,sparkReachPx:20,glow:.25}));
 await page.setViewportSize({width:320,height:1400});
 await frame.evaluate(()=>impactPreview.render(40));
 const mobileOverflow=await frame.evaluate(()=>document.documentElement.scrollWidth>innerWidth);
 const mobileHeight=await frame.locator('#laser-impact-tuner').evaluate(el=>el.scrollHeight);
 await page.locator('iframe').evaluate((el,height)=>el.style.height=height+'px',mobileHeight+32);
 await frame.locator('#laser-impact-tuner').screenshot({path:path.join(__dirname,'mobile.png')});
 await page.setViewportSize({width:780,height:1300});
 const frames=path.join(__dirname,'frames');fs.mkdirSync(frames,{recursive:true});
 for(let i=0;i<42;i++){await frame.evaluate(ms=>impactPreview.render(ms),-100+i*20);await frame.locator('.motion-scenes').screenshot({path:path.join(frames,String(i).padStart(3,'0')+'.png')});}
 const result={...evidence,pageErrors:errors,durationChanged,exportChanged:exportSettings.durationMs===500&&exportSettings.fanMotion===.24,settingsActionWorks:settingsMessage.prompt.includes(JSON.stringify(exportSettings)),mobileOverflow,frames:42};
 fs.writeFileSync(path.join(__dirname,'checks.json'),JSON.stringify(result,null,2)+'\n');
 process.stdout.write(JSON.stringify(result)+'\n');await browser.close();
 if(result.frameZeroDifferentChannels||result.alphaAtExpiry||errors.length||!durationChanged||!result.exportChanged||!result.settingsActionWorks||mobileOverflow)process.exitCode=1;
})();
