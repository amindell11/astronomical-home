const fs = require('node:fs');
const path = require('node:path');
const crypto = require('node:crypto');
const { execFileSync } = require('node:child_process');
const { chromium } = require('playwright');

(async () => {
  const root = path.resolve(__dirname, '../..');
  const previous = '682fe0532566dc2fdfbaff46a4b092c6fa50c161';
  const fromCommit = file => execFileSync('git', ['cat-file', '--filters', previous + ':' + file], { cwd:root, maxBuffer:8*1024*1024 });
  const settings = JSON.parse(fs.readFileSync(path.join(root,'art/vfx/turbo-ripper/settings.json'),'utf8'));
  const beforeSettings = JSON.parse(fromCommit('art/vfx/turbo-ripper/settings.json').toString('utf8'));
  const assetPath = 'src/Asteroids3D/Assets/Visuals/Vfx/RipperSlug/Textures/RipperSlug.png';
  const before = fromCommit(assetPath);
  const after = fs.readFileSync(path.join(__dirname,'RipperSlug.png'));
  fs.writeFileSync(path.join(__dirname,'before.png'),before);
  const impactComparisons = ['TurboLaserImpact','RipperImpact'].map(name=>{
    const original = fromCommit('src/Asteroids3D/Assets/Visuals/Vfx/'+name+'/Textures/'+name+'.png');
    const exported = fs.readFileSync(path.join(__dirname,name+'.png'));
    return { name, byteIdentical:original.equals(exported), sha256:crypto.createHash('sha256').update(exported).digest('hex') };
  });
  function shapes(code, config) {
    const drawing = new Function(code+';return RipperDrawing;')();
    return config.slug.variants.map(variant=>{
      const points=[];
      const ctx={ save(){},restore(){},beginPath(){this.points=[];},moveTo(x,y){this.points.push([x,y]);},lineTo(x,y){this.points.push([x,y]);},closePath(){},fill(){points.push(this.points);} };
      drawing.slug(ctx,config.slug.colors,config.slug.shadowBlur,variant);
      return points;
    });
  }
  const geometryUnchanged=JSON.stringify(shapes(fromCommit('art/vfx/turbo-ripper/draw.js').toString('utf8'),beforeSettings))===JSON.stringify(shapes(fs.readFileSync(path.join(root,'art/vfx/turbo-ripper/draw.js'),'utf8'),settings));
  const variantsUnchanged=JSON.stringify(beforeSettings.slug.variants)===JSON.stringify(settings.slug.variants);
  const browser=await chromium.launch({executablePath:process.env.CHROMIUM_EXECUTABLE,headless:true});
  try {
    const page=await browser.newPage();
    const result=await page.evaluate(async({before,after,settings})=>{
      const load=async(source)=>{const img=new Image();img.src=source;await img.decode();return img;};
      const oldImage=await load(before),newImage=await load(after);
      function pixels(image){const c=document.createElement('canvas');c.width=image.width;c.height=image.height;const ctx=c.getContext('2d');ctx.drawImage(image,0,0);return ctx.getImageData(0,0,c.width,c.height).data;}
      const oldPixels=pixels(oldImage),newPixels=pixels(newImage);
      const linear=value=>value<=.04045?value/12.92:((value+.055)/1.055)**2.4;
      const light=(data,index)=>((linear(data[index]/255)*.2126)+(linear(data[index+1]/255)*.7152)+(linear(data[index+2]/255)*.0722))*data[index+3]/255;
      const frames=[];
      for(let frame=0;frame<6;frame++){
        const variant=settings.slug.variants[frame],angle=variant.angleDegrees*Math.PI/180;
        const head={before:0,after:0,count:0},tail={before:0,after:0,count:0};
        let edgeAlpha=0;
        for(let y=0;y<512;y++)for(let x=0;x<256;x++){
          const index=(y*newImage.width+frame*256+x)*4;
          if(!x||!y||x===255||y===511)edgeAlpha=Math.max(edgeAlpha,newPixels[index+3]);
          const u=-(y+.5-256)/(448/22),v=(x+.5-128)/(448/22);
          const sourceX=(Math.cos(angle)*u+Math.sin(angle)*v)/variant.lengthScale-11;
          const sourceY=(-Math.sin(angle)*u+Math.cos(angle)*v)/variant.widthScale;
          let region=null;
          if(sourceX>=-4&&sourceX<=0&&Math.abs(sourceY)<=1.1)region=head;
          if(sourceX>=-20&&sourceX<=-7&&Math.abs(sourceY)<=2.5)region=tail;
          if(region){region.before+=light(oldPixels,index);region.after+=light(newPixels,index);region.count++;}
        }
        const summarize=region=>({before:region.before/region.count,after:region.after/region.count,ratio:region.after/region.before});
        const h=summarize(head),t=summarize(tail);
        frames.push({frame,head:h,tail:t,headToTailBefore:h.before/t.before,headToTailAfter:h.after/t.after,edgeAlpha});
      }
      const sheet=document.createElement('canvas');sheet.width=840;sheet.height=426;
      const ctx=sheet.getContext('2d');ctx.fillStyle='#152945';ctx.fillRect(0,0,840,426);
      ctx.fillStyle='#e7edf5';ctx.font='17px sans-serif';ctx.fillText('Before',24,29);ctx.fillText('Brighter bullet, dimmer trail',444,29);
      const height=settings.slug.worldLength/.875*20;
      function draw(image,frame,x,y,scale){ctx.save();ctx.translate(x,y);ctx.rotate(Math.PI/2);ctx.drawImage(image,frame*256,0,256,512,-height*scale/4,-height*scale/2,height*scale/2,height*scale);ctx.restore();}
      draw(oldImage,0,210,80,10);draw(newImage,0,630,80,10);
      ctx.font='13px sans-serif';ctx.fillStyle='#a8bfdc';ctx.fillText('Enlarged',24,118);ctx.fillText('Enlarged',444,118);
      ctx.fillText('Reference size · 20 px per world unit',24,157);ctx.fillText('Reference size · same size and geometry',444,157);
      for(let i=0;i<6;i++){
        ctx.fillStyle='#a8bfdc';ctx.fillText('Frame '+(i+1),24,190+i*36);
        draw(oldImage,i,210,185+i*36,1);draw(newImage,i,630,185+i*36,1);
      }
      ctx.fillText('Preview of baked textures; game postprocessing is not included.',24,412);
      return {frames,width:newImage.width,height:newImage.height,png:sheet.toDataURL('image/png').split(',')[1]};
    },{before:'data:image/png;base64,'+before.toString('base64'),after:'data:image/png;base64,'+after.toString('base64'),settings});
    fs.writeFileSync(path.join(__dirname,'before-after.png'),Buffer.from(result.png,'base64'));delete result.png;
    const checks={previousCommit:previous,metric:'Mean alpha-weighted linear RGB luminance in fixed source-space head and tail regions',headRegion:{x:[-4,0],y:[-1.1,1.1]},tailRegion:{x:[-20,-7],y:[-2.5,2.5]},geometryUnchanged,variantsUnchanged,worldLengthUnchanged:settings.slug.worldLength===beforeSettings.slug.worldLength,impactComparisons,...result};
    fs.writeFileSync(path.join(__dirname,'verification.json'),JSON.stringify(checks,null,2)+'\n');console.log(JSON.stringify(checks));
    if(!geometryUnchanged||!variantsUnchanged||impactComparisons.some(item=>!item.byteIdentical)||result.frames.some(frame=>frame.tail.ratio>.25||frame.head.ratio<=1||frame.edgeAlpha!==0))throw new Error('V3 comparison failed.');
  }finally{await browser.close();}
})().catch(error=>{console.error(error);process.exitCode=1;});
