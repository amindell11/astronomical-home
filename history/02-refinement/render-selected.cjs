const {chromium}=require('playwright');
const fs=require('node:fs');
const path=require('node:path');
(async()=>{
  const browser=await chromium.launch({executablePath:process.env.CHROMIUM_EXECUTABLE,headless:true});
  const page=await browser.newPage();
  await page.goto('file:///'+path.resolve(__dirname,'../../index.html').replaceAll('\\','/'));
  await page.waitForFunction(()=>window.prototype.ready());
  const png=await page.evaluate(()=>{
    const canvas=document.createElement('canvas');canvas.width=1024;canvas.height=1024;
    const c=canvas.getContext('2d');c.fillStyle='#131a26';c.fillRect(0,0,1024,1024);
    c.translate(280,512);c.scale(16,16);flash(c,1,.055,11);
    return canvas.toDataURL('image/png').split(',')[1];
  });
  fs.writeFileSync(path.join(__dirname,'selected-b.png'),Buffer.from(png,'base64'));
  await browser.close();
})();
