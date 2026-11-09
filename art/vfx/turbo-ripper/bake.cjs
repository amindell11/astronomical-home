// Usage: node bake.cjs <output-directory>. Requires Playwright; CHROMIUM_EXECUTABLE may select its browser.
// Writes three textures and bake.json; stdout is one JSON manifest with absolute output paths.
// Failure exits nonzero.
const fs=require('node:fs'),path=require('node:path'),crypto=require('node:crypto'),{chromium}=require('playwright');
(async()=>{
 if(!process.argv[2])throw Error('Pass an output directory.');
 const output=path.resolve(process.argv[2]),laser=path.join(__dirname,'../laser-impact');fs.mkdirSync(output,{recursive:true});
 const source=fs.readFileSync(path.join(laser,'source.png')),sourceHash=crypto.createHash('sha256').update(source).digest('hex');
 if(sourceHash!=='0eb51ba237edc0a04427f49863250b4d234705e9a07e1de12a1da3061c9e6108')throw Error('The approved laser source changed.');
 const settings=JSON.parse(fs.readFileSync(path.join(__dirname,'settings.json'),'utf8'));
 const baseline=JSON.parse(fs.readFileSync(path.join(laser,'settings.json'),'utf8'));
 const browser=await chromium.launch({executablePath:process.env.CHROMIUM_EXECUTABLE,headless:true});
 try{
  const page=await browser.newPage();await page.addScriptTag({path:path.join(laser,'motion.js')});await page.addScriptTag({path:path.join(__dirname,'draw.js')});
  const images=await page.evaluate(async({source,settings,baseline})=>{
   const image=new Image();image.src=source;await image.decode();const engine=ImpactMotion.create(ImpactMotion.parse(image));
   const canvas=(w,h)=>{const c=document.createElement('canvas');c.width=w;c.height=h;return c;};
   const png=c=>c.toDataURL('image/png').split(',')[1],result={};
   for(const kind of ['turbo','ripper']){
    const atlas=canvas(1280,1024),ac=atlas.getContext('2d'),frame=canvas(512,512),fc=frame.getContext('2d');
    const duration=kind==='turbo'?baseline.durationMs:settings.ripper.durationMs;
    for(let i=0;i<20;i++){
     fc.setTransform(1,0,0,1,0,0);fc.clearRect(0,0,512,512);fc.save();fc.translate(256,256);fc.scale(2,2);fc.rotate(Math.PI/2);
     if(kind==='turbo'){
      fc.filter=`hue-rotate(${settings.turbo.hueDegrees}deg) saturate(${settings.turbo.saturation})`;fc.scale(1,settings.turbo.spread);
      engine.draw(fc,i*duration/20,baseline,[0,0],96);
     }else{fc.scale(76/34,76/34);RipperDrawing.impact(fc,i*duration/20,duration,settings.ripper.colors);}
     fc.restore();ac.drawImage(frame,i%5*256,Math.floor(i/5)*256,256,256);
    }
    result[kind]=png(atlas);
   }
   const slug=canvas(256,512),sc=slug.getContext('2d'),slugScale=448/22;
   sc.translate(128,256);sc.rotate(-Math.PI/2);sc.scale(slugScale,slugScale);sc.translate(11,0);
   RipperDrawing.slug(sc,settings.slug.colors,settings.slug.shadowBlur*slugScale);result.slug=png(slug);
   return result;
  },{source:'data:image/png;base64,'+source.toString('base64'),settings,baseline});
  const names={turbo:'TurboLaserImpact.png',ripper:'RipperImpact.png',slug:'RipperSlug.png'};
  for(const [key,name] of Object.entries(names))fs.writeFileSync(path.join(output,name),Buffer.from(images[key],'base64'));
  const effect=(kind,duration,fraction,fadeInTexture)=>({texture:names[kind],columns:5,rows:4,durationSeconds:duration/1000,solidWidthFraction:fraction,worldLength:settings[kind].worldLength,fadeInTexture});
  const manifest={sourceHash,turbo:effect('turbo',baseline.durationMs,96/256,false),ripper:effect('ripper',settings.ripper.durationMs,76*31/34/256,true),slug:{texture:names.slug,solidLengthFraction:448/512,worldLength:settings.slug.worldLength}};
  const manifestPath=path.join(output,'bake.json');fs.writeFileSync(manifestPath,JSON.stringify(manifest,null,2)+'\n');
  process.stdout.write(JSON.stringify({manifest:manifestPath,textures:Object.values(names).map(name=>path.join(output,name)),...manifest})+'\n');
 }finally{await browser.close();}
})().catch(error=>{console.error(error);process.exitCode=1;});
