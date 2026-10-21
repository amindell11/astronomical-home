const {chromium}=require('playwright');
const fs=require('node:fs');
const path=require('node:path');
(async()=>{
 const browser=await chromium.launch({executablePath:process.env.CHROMIUM_EXECUTABLE||undefined,headless:true});
 const page=await browser.newPage({viewport:{width:1320,height:1200},deviceScaleFactor:1});
 const errors=[];page.on('pageerror',e=>errors.push(e.message));
 await page.goto('file:///'+path.join(__dirname,'index.html').replaceAll('\\','/'));
 await page.waitForFunction(()=>window.prototype.ready());
 fs.mkdirSync(path.join(__dirname,'frames'),{recursive:true});
 await page.evaluate(()=>prototype.render(.055));
 await page.screenshot({path:path.join(__dirname,'comparison.png'),fullPage:true});
 await page.evaluate(()=>prototype.render(.16));
 await page.screenshot({path:path.join(__dirname,'follow-through.png'),fullPage:true});
 await page.locator('#speed').selectOption('0.25');
 for(let i=0;i<55;i++){
   await page.evaluate(t=>prototype.render(t),-.18+i*.02);
   await page.screenshot({path:path.join(__dirname,'frames',String(i).padStart(3,'0')+'.png'),fullPage:true});
 }
 await page.locator('#repeat').check();
 await page.locator('#time').fill('220');
 if(await page.locator('#clock').innerText()!=='220 ms')throw Error('Scrub control did not update.');
 await page.screenshot({path:path.join(__dirname,'rapid-hits.png'),fullPage:true});
 await page.setViewportSize({width:390,height:900});
 await page.locator('#repeat').uncheck();
 await page.evaluate(()=>prototype.render(.06));
 await page.screenshot({path:path.join(__dirname,'mobile.png'),fullPage:true});
 const overflow=await page.evaluate(()=>document.documentElement.scrollWidth>innerWidth);
 await browser.close();
 process.stdout.write(JSON.stringify({pageErrors:errors,mobileOverflow:overflow,frames:55})+'\n');
 if(errors.length||overflow)process.exitCode=1;
})();
