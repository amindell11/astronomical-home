const {chromium}=require('playwright');
const fs=require('node:fs');
const path=require('node:path');
const {pathToFileURL}=require('node:url');
(async()=>{
  const out=path.join(__dirname,'verification');fs.mkdirSync(out,{recursive:true});
  const browser=await chromium.launch({channel:'chrome',headless:true,args:['--enable-webgl','--enable-unsafe-swiftshader','--use-angle=swiftshader']});
  try{
    const page=await browser.newPage({viewport:{width:1600,height:1100}}),errors=[],requests=[];
    page.on('pageerror',e=>errors.push(e.message));page.on('request',r=>{if(/^https?:/.test(r.url()))requests.push(r.url());});
    await page.goto(pathToFileURL(path.join(__dirname,'index.html')).href);
    await page.waitForFunction(()=>globalThis.consoleReview?.ready,{timeout:15000});
    await page.evaluate(()=>{const r=consoleReview;r.state.playing=false;r.state.time=2;r.state.age=2;r.engine.clear();r.render();});
    await page.locator('#view').screenshot({path:path.join(out,'console-strong.png')});
    await page.screenshot({path:path.join(out,'page.png'),fullPage:true});
    const results=await page.evaluate(()=>{
      const r=consoleReview,canvas=document.getElementById('view');
      const pixels=()=>{r.engine.clear();r.render();return canvas.toDataURL();};
      const strong=pixels();r.state.enabled=false;const baseline=pixels();r.state.type.heading='terminal';const terminal=pixels();r.state.type.heading='wide';r.state.surface=0;const empty=pixels();r.state.surface=.12;r.state.tint='neutral';const neutral=pixels();r.state.tint='cool';
      r.state.enabled=true;r.state.age=.12;const startup=pixels();r.state.age=2;r.state.enabled=false;pixels();
      return {ready:r.ready,strongDiffersFromBaseline:strong!==baseline,headingChangesPixels:terminal!==baseline,surfaceChangesPixels:empty!==baseline,tintChangesPixels:neutral!==baseline,startupChangesPixels:startup!==strong,fontCount:document.fonts.size,canvas:[canvas.width,canvas.height]};
    });
    await page.locator('#view').screenshot({path:path.join(out,'console-baseline.png')});
    const roundtrip=await page.evaluate(()=>{
      const r=consoleReview,expected=r.exportSettings();expected.personality.glow=.82;expected.typography.functional='roboto';expected.typography.heading='terminal';expected.console.surface=.08;expected.console.tint='neutral';
      document.getElementById('settings').value=JSON.stringify(expected);document.getElementById('apply').click();
      const result=JSON.stringify(r.exportSettings())===JSON.stringify(expected);
      const before=JSON.stringify(r.exportSettings());expected.personality.glow='bad';document.getElementById('settings').value=JSON.stringify(expected);document.getElementById('apply').click();
      return {roundtrip:result,invalidImportKeepsState:JSON.stringify(r.exportSettings())===before};
    });
    const overflow=[];for(const width of [375,768]){await page.setViewportSize({width,height:1000});overflow.push(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth));}
    const report={...results,...roundtrip,mobileFits:overflow.every(Boolean),errors,externalRequests:requests};
    fs.writeFileSync(path.join(out,'checks.json'),JSON.stringify(report,null,2)+'\n');console.log(JSON.stringify(report));
    if(errors.length||requests.length||Object.entries(report).some(([k,v])=>typeof v==='boolean'&&!v))process.exitCode=1;
  }finally{await browser.close();}
})();
