// Writes LaserImpact.png and bake.json into this directory from the approved stage-05 inputs.
// Stdout is JSON with paths, layout, lifetime, and source hash; failures exit nonzero.
const fs=require('node:fs'),path=require('node:path'),crypto=require('node:crypto'),{chromium}=require('playwright');
const TILE=256,COLUMNS=5,ROWS=4,SUPERSAMPLE=2;
const IMPACT_WIDTH=96; // Centers contact while leaving room for the fan and sparks.
(async()=>{
 const input=path.join(__dirname,'../05-motion'),source=fs.readFileSync(path.join(input,'source.png'));
 const sourceHash=crypto.createHash('sha256').update(source).digest('hex');
 if(sourceHash!=='0eb51ba237edc0a04427f49863250b4d234705e9a07e1de12a1da3061c9e6108')throw Error('The approved impact source changed.');
 const settings=JSON.parse(fs.readFileSync(path.join(input,'settings.json'),'utf8'));
 const browser=await chromium.launch({executablePath:process.env.CHROMIUM_EXECUTABLE,headless:true});
 try{
  const page=await browser.newPage();await page.addScriptTag({path:path.join(input,'motion.js')});
  const baked=await page.evaluate(async ({source,settings,tile,columns,rows,supersample,width})=>{
   const image=new Image();image.src=source;await image.decode();const engine=ImpactMotion.create(ImpactMotion.parse(image));
   const atlas=document.createElement('canvas');atlas.width=tile*columns;atlas.height=tile*rows;const ctx=atlas.getContext('2d');
   const frame=document.createElement('canvas');frame.width=frame.height=tile*supersample;const fc=frame.getContext('2d');
   const frames=columns*rows,alphaCounts=[];
   for(let i=0;i<frames;i++){
    fc.setTransform(1,0,0,1,0,0);fc.clearRect(0,0,frame.width,frame.height);fc.translate(frame.width/2,frame.height/2);fc.scale(supersample,supersample);fc.rotate(Math.PI/2);
    engine.draw(fc,i*settings.durationMs/frames,settings,[0,0],width);
    const rgba=fc.getImageData(0,0,frame.width,frame.height).data;
    let count=0;for(let j=3;j<rgba.length;j+=4)if(rgba[j])count++;alphaCounts.push(count);
    ctx.drawImage(frame,i%columns*tile,Math.floor(i/columns)*tile,tile,tile);
   }
   return {png:atlas.toDataURL('image/png').split(',')[1],alphaCounts};
  },{source:'data:image/png;base64,'+source.toString('base64'),settings,tile:TILE,columns:COLUMNS,rows:ROWS,supersample:SUPERSAMPLE,width:IMPACT_WIDTH});
  if(!baked.alphaCounts[0]||baked.alphaCounts[0]<=baked.alphaCounts.at(-1))throw Error('The atlas must begin with a visible impact and diminish.');
  const output=path.join(__dirname,'LaserImpact.png'),manifest=path.join(__dirname,'bake.json');fs.writeFileSync(output,Buffer.from(baked.png,'base64'));
  const result={texture:'LaserImpact.png',sourceHash,tilePx:TILE,columns:COLUMNS,rows:ROWS,frames:COLUMNS*ROWS,durationSeconds:settings.durationMs/1000,contactUv:[.5,.5],fanDirection:'negative local Y',solidWidthFraction:IMPACT_WIDTH/TILE,alphaCounts:baked.alphaCounts};
  fs.writeFileSync(manifest,JSON.stringify(result,null,2)+'\n');process.stdout.write(JSON.stringify({...result,output,manifest})+'\n');
 }finally{await browser.close();}
})().catch(error=>{console.error(error);process.exitCode=1;});
