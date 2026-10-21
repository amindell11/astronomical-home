const {chromium}=require('playwright');
const path=require('node:path');
(async()=>{
 const browser=await chromium.launch({executablePath:process.env.CHROMIUM_EXECUTABLE,headless:true});
 const page=await browser.newPage({viewport:{width:1080,height:1250},deviceScaleFactor:1});
 await page.goto('file:///'+path.join(__dirname,'index.html').replaceAll('\\','/'));
 await page.waitForFunction(()=>Array.from(document.images).every(img=>img.complete&&img.naturalWidth));
 await page.screenshot({path:path.join(__dirname,'comparison.png'),fullPage:true});
 await browser.close();
})();
