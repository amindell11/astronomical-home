const fs=require('node:fs');
const {chromium}=require('C:/Users/amind/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright');
(async()=>{
 const browser=await chromium.launch({headless:true,channel:'chrome'});
 try {
  const page=await browser.newPage({viewport:{width:768,height:700},deviceScaleFactor:1});
  const errors=[];page.on('pageerror',error=>errors.push(error.message));
  await page.setContent(fs.readFileSync('D:/amind/git/agent-3/scratch/strike-mark-preview.html','utf8'));
  const frame=page.frames().find(item=>item!==page.mainFrame());
  await frame.waitForSelector('.ss-icon');
  const variants=await frame.locator('[data-variant]').evaluateAll(nodes=>nodes.map(node=>node.dataset.variant));
  const results=[];
  for(const width of [736,320]){
   await page.setViewportSize({width:width+32,height:700});
   for(const variant of variants){
    await frame.locator('[data-variant]').evaluateAll((nodes,name)=>nodes.forEach(node=>node.hidden=node.dataset.variant!==name),variant);
    const stage=frame.locator(`[data-variant="${variant}"] .ss-stage`);
    const result=await stage.evaluate(node=>{
     const r=node.getBoundingClientRect();
     const clipped=[...node.querySelectorAll('.ss-specimen,.ss-icon,.ss-caption')].filter(el=>{const b=el.getBoundingClientRect();return b.left<r.left||b.right>r.right||b.top<r.top||b.bottom>r.bottom;}).map(el=>el.className);
     return {width:r.width,height:r.height,clipped,mask:getComputedStyle(node.querySelector('.ss-ship')).maskImage.startsWith('url("data:image/png;base64,'),icons:node.querySelectorAll('.ss-icon').length};
    });
    if(result.clipped.length||!result.mask||result.icons!==2)throw new Error(JSON.stringify({width,variant,result}));
    results.push({width,variant,...result});
    await stage.screenshot({path:`D:/amind/git/agent-3/scratch/scoreboard/painted-tally/strike-${variant.toLowerCase().replaceAll(' ','-')}-${width}.png`});
   }
  }
  if(errors.length)throw new Error(errors.join('\n'));
  fs.writeFileSync('D:/amind/git/agent-3/scratch/strike-mark-checks.json',JSON.stringify({errors,results},null,2));
  await page.setViewportSize({width:1350,height:1500});
  await frame.locator('.viz-carousel').evaluate(node=>{node.style.display='grid';node.style.gridTemplateColumns='1fr 1fr';node.style.gap='20px';node.style.padding='0';[...node.querySelectorAll('[data-variant]')].forEach(variant=>{variant.hidden=false;const title=document.createElement('div');title.textContent=variant.dataset.variant;title.style.padding='6px 0';variant.prepend(title);});});
  await frame.locator('#strike-mark-studies').screenshot({path:'D:/amind/git/agent-3/scratch/scoreboard/painted-tally/strike-mark-contact-sheet.png'});
  console.log(JSON.stringify({layouts:results.length,pageErrors:errors.length}));
 } finally {await browser.close();}
})();
