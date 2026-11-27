// Bakes the Overdrive start ring: a sonic-boom ring that opens from the ship on the press.
// usage: node bake-ring.js <out-dir>
const {chromium}=require('playwright');const fs=require('fs');const path=require('path');
const [,, outDir]=process.argv;
(async()=>{
  const b=await chromium.launch();const p=await b.newPage();
  const r=await p.evaluate(()=>{
    const S=3,L=61,N=10,COLS=5,ROWS=2,BOX=200;      // ship px; L px is one ship length
    const rim='#14b4e8',mid='#8ff6ff',core='#ffffff';
    const cv=document.createElement('canvas');cv.width=COLS*BOX*S;cv.height=ROWS*BOX*S;const ctx=cv.getContext('2d');
    function arcBand(r,t,a0,a1,col){ctx.beginPath();ctx.arc(0,0,r+t/2,a0,a1);ctx.arc(0,0,Math.max(0,r-t/2),a1,a0,true);ctx.closePath();ctx.fillStyle=col;ctx.fill();}
    for(let i=0;i<N;i++){const cx=(i%COLS)*BOX*S,cy=Math.floor(i/COLS)*BOX*S;
      ctx.save();ctx.beginPath();ctx.rect(cx,cy,BOX*S,BOX*S);ctx.clip();ctx.setTransform(S,0,0,S,cx+BOX*S/2,cy+BOX*S/2);
      const u=(i+1)/N, e=1-Math.pow(1-u,2.4);        // fast out, easing
      const R=10+74*e, t=11*(1-u)+1.6;               // radius and band thickness
      const gaps=i<3?0:6, gap=i<3?0:(0.05+0.5*Math.pow((i-2)/(N-2),1.5))*(Math.PI*2/6); // ring breaks into arcs as it thins
      ctx.globalAlpha=i>=N-2?[0.6,0.32][i-(N-2)]:1;
      ctx.scale(1.12,0.9);                           // wider across the ship than along it
      const segs=gaps||1;
      for(let k=0;k<segs;k++){const a0=k*Math.PI*2/segs+gap/2+0.26,a1=(k+1)*Math.PI*2/segs-gap/2+0.26;
        ctx.shadowColor='rgba(60,210,255,0.9)';ctx.shadowBlur=10*S;arcBand(R,t,a0,gaps?a1:a0+Math.PI*2,rim);ctx.shadowBlur=0;
        arcBand(R,t*0.62,a0,gaps?a1:a0+Math.PI*2,mid);
        if(i<6)arcBand(R+t*0.08,t*0.3,a0,gaps?a1:a0+Math.PI*2,core);}
      ctx.restore();ctx.setTransform(1,0,0,1,0,0);}
    const im=ctx.getImageData(0,0,cv.width,cv.height),d=im.data;   // premultiply: shader blends One OneMinusSrcAlpha
    for(let k=0;k<d.length;k+=4){const al=d[k+3]/255;d[k]=Math.round(d[k]*al);d[k+1]=Math.round(d[k+1]*al);d[k+2]=Math.round(d[k+2]*al);}
    ctx.putImageData(im,0,0);
    const q=x=>Math.round(x*1e4)/1e4;
    return {png:cv.toDataURL('image/png'),meta:{name:'Ring',file:'ValisOverdrive_Ring.png',columns:COLS,rows:ROWS,frames:N,
      width:q(BOX/L),length:q(BOX/L),centreAhead:0,speed:0.5,startsOn:'Press',loops:false,followsShip:true,under:true,premultiplied:true,pixels:[cv.width,cv.height]}};
  });
  fs.mkdirSync(outDir,{recursive:true});
  fs.writeFileSync(path.join(outDir,r.meta.file),Buffer.from(r.png.split(',')[1],'base64'));
  fs.writeFileSync(path.join(outDir,'meta-ring.json'),JSON.stringify(r.meta,null,2));
  console.log(JSON.stringify(r.meta));await b.close();
})();
