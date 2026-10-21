const fs=require('node:fs');
const {chromium}=require('C:/Users/amind/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright');
(async()=>{
 const browser=await chromium.launch({headless:true,channel:'chrome'});
 try {
  const page=await browser.newPage({viewport:{width:1056,height:850},deviceScaleFactor:1});
  const errors=[];page.on('pageerror',error=>errors.push(error.message));
  await page.setContent(fs.readFileSync('D:/amind/git/agent-3/scratch/hangar-trophy-preview.html','utf8'));
  const frame=page.frames().find(item=>item!==page.mainFrame());
  await frame.waitForSelector('.hr-mark');
  const results=[];
  for(const width of [1024,736,320]) {
   await page.setViewportSize({width:width+32,height:850});
   for(const treatment of ['Clean tally','Strike marks']) {
    await frame.locator('[data-variant]').evaluateAll((nodes,name)=>nodes.forEach(node=>node.hidden=node.dataset.variant!==name),treatment);
    const game=frame.locator(`[data-treatment="${treatment}"]`);
    for(const outcome of ['loss','win']) {
     await frame.locator(`[data-outcome="${outcome}"]`).click();
     const title=outcome==='win'?'EXTRACTED':'SHIP LOST';
     if(await game.locator('.hr-outcome').textContent()!==title)throw new Error('Outcome switch failed');
     if(await game.locator('.hr-distance').textContent()!==(outcome==='win'?'3 SECTORS CLEARED':'2 SECTORS CLEARED'))throw new Error('Distance mismatch');
     if(await game.locator('.hr-mark').count()!==17)throw new Error('Wrong trophy count');
     const data=await game.evaluate(node=>{
      const gameRect=node.getBoundingClientRect(),panel=node.querySelector('.hr-panel'),bounds=panel.getBoundingClientRect(),headline=node.querySelector('.hr-outcome');
      const titleSize=parseFloat(getComputedStyle(headline).fontSize);
      const leaves=[...panel.querySelectorAll('*')].filter(item=>!item.children.length&&item.textContent.trim()&&item!==headline);
      const otherSizes=leaves.map(item=>parseFloat(getComputedStyle(item).fontSize));
      const clipped=[...panel.querySelectorAll('.hr-mark,.hr-outcome,.hr-tally-label,.hr-distance,.hr-continue')].filter(item=>{const r=item.getBoundingClientRect();return r.left<bounds.left||r.right>bounds.right||r.top<bounds.top||r.bottom>bounds.bottom;}).map(item=>item.className);
      return {panelWidth:bounds.width,panelHeight:bounds.height,titleSize,maxOtherSize:Math.max(...otherSizes),clipped,inside:bounds.left>=gameRect.left&&bounds.right<=gameRect.right&&bounds.top>=gameRect.top&&bounds.bottom<=gameRect.bottom,overflow:headline.scrollWidth>headline.clientWidth};
     });
     if(!data.inside||data.clipped.length||data.overflow||data.titleSize<=data.maxOtherSize)throw new Error(JSON.stringify({width,treatment,outcome,data}));
     results.push({width,treatment,outcome,...data});
     if(width===1024||width===320)await game.screenshot({path:`D:/amind/git/agent-3/scratch/scoreboard/painted-tally/hangar-${treatment.toLowerCase().replaceAll(' ','-')}-${outcome}-${width}.png`});
    }
    await game.locator('.hr-mark').nth(1).click();
    if(await game.locator('.hr-detail').textContent()!=='Crimson destroyed')throw new Error('Trophy detail failed');
    await game.locator('.hr-mark').nth(1).click();
    await game.locator('.hr-continue').click();
    if(await game.locator('.hr-detail').textContent()!=='Returns to the hangar')throw new Error('Continue preview failed');
   }
  }
  if(errors.length)throw new Error(errors.join('\n'));
  fs.writeFileSync('D:/amind/git/agent-3/scratch/hangar-trophy-checks.json',JSON.stringify({errors,results},null,2));
  console.log(JSON.stringify({layouts:results.length,outcomeLargest:true,interactions:'passed',pageErrors:errors.length}));
 } finally {await browser.close();}
})();
