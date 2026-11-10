const fs=require('node:fs'),path=require('node:path'),{chromium}=require('playwright');
(async()=>{
 const output='C:/Users/amind/.codex/visualizations/2026/10/21/01a15fa8-130c-7a20-af5e-62d067bb20db/needle-fleck-v3-gif';
 const frames=path.join(output,'frames');fs.mkdirSync(frames,{recursive:true});
 const source='D:/amind/git/agent-4/results/needle-fleck-v3-export';
 const textures=['before.png','RipperSlug.png'].map(name=>'data:image/png;base64,'+fs.readFileSync(path.join(source,name)).toString('base64'));
 const browser=await chromium.launch({executablePath:process.env.CHROMIUM_EXECUTABLE,headless:true});
 try{
  const page=await browser.newPage();
  const images=await page.evaluate(async textures=>{
   const sprites=await Promise.all(textures.map(async src=>{const image=new Image();image.src=src;await image.decode();return image;}));
   const canvas=document.createElement('canvas');canvas.width=840;canvas.height=330;
   const ctx=canvas.getContext('2d'),result=[],order=[0,4,1,5,2,3];
   for(let f=0;f<240;f++){
    const time=f/50;ctx.fillStyle='#0b1b2d';ctx.fillRect(0,0,840,330);
    ctx.fillStyle='#eff5ff';ctx.font='600 22px Arial';ctx.fillText('Ripper • brighter bullet, faint trail',24,36);
    ctx.fillStyle='#9eb4cf';ctx.font='14px Arial';ctx.fillText('Enlarged 4× · slowed to 1/8 speed · artwork preview',24,62);
    for(let row=0;row<2;row++){
     const y=134+row*133;ctx.fillStyle=row?'#f8e5ae':'#aec0d8';ctx.font='600 15px Arial';ctx.fillText(row?'UPDATED':'BEFORE',24,y-35);
     ctx.strokeStyle='#193047';ctx.beginPath();ctx.moveTo(24,y+32);ctx.lineTo(816,y+32);ctx.stroke();
     for(let i=-4;i<16;i++){
      const x=24+(time-i*.4)*600;if(x < -50 || x > 890)continue;
      const index=order[((i%6)+6)%6],height=.75/.875*80;
      ctx.save();ctx.translate(x,y);ctx.rotate(Math.PI/2);
      ctx.drawImage(sprites[row],index*256,0,256,512,-height/4,-height/2,height/2,height);ctx.restore();
     }
    }
    result.push(canvas.toDataURL('image/png').split(',')[1]);
   }
   return result;
  },textures);
  images.forEach((data,index)=>fs.writeFileSync(path.join(frames,`f_${String(index).padStart(4,'0')}.png`),Buffer.from(data,'base64')));
  fs.writeFileSync(path.join(output,'preview.json'),JSON.stringify({sourceCommit:'cb20e0cf55261f3c6414478289014496dda25c3f',frames:240,fps:50,durationSeconds:4.8,enlargement:4,speedFraction:0.125,inGameCapture:false},null,2)+'\n');
  process.stdout.write(JSON.stringify({output,frames:images.length})+'\n');
 }finally{await browser.close();}
})().catch(error=>{console.error(error);process.exitCode=1;});
