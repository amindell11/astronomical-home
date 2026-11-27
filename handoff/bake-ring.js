// Bakes the tuner's "Shock ring" start cue as an Overdrive flipbook sheet, drawn by the page's own ring().
// usage: node bake-ring.js <tuner.html> <picked-settings.json> <out-dir>
const {chromium}=require('playwright');const fs=require('fs');const path=require('path');
const [,, htmlPath, settingsPath, outDir]=process.argv;
(async()=>{
  const b=await chromium.launch();const p=await b.newPage({viewport:{width:1200,height:900}});
  await p.setContent('<!doctype html><html><head><meta charset=utf-8></head><body>'+fs.readFileSync(htmlPath,'utf8')+'</body></html>');
  await p.waitForTimeout(500);
  const picked=JSON.parse(fs.readFileSync(settingsPath,'utf8'));
  const r=await p.evaluate((picked)=>{
    applySettings(picked); paused=true;
    const S=3,L=61,c=PAL[st.pal],g=st.glow,N=6,COLS=6,ROWS=1,W=132,H=96;  // ship px; ring reaches 100 x 64 plus glow
    const engineY=(ENGINE.y-SPR.cy)*K;
    const cv=document.createElement('canvas');cv.width=COLS*W*S;cv.height=ROWS*H*S;const ctx=cv.getContext('2d');
    for(let i=0;i<N;i++){const cx=i*W*S;ctx.save();ctx.beginPath();ctx.rect(cx,0,W*S,H*S);ctx.clip();
      ctx.setTransform(S,0,0,S,cx+W*S/2,H*S/2);SC=S;ring(ctx,i,c,g);ctx.restore();ctx.setTransform(1,0,0,1,0,0);}
    const im=ctx.getImageData(0,0,cv.width,cv.height),d=im.data;   // premultiply: shader blends One OneMinusSrcAlpha
    for(let k=0;k<d.length;k+=4){const al=d[k+3]/255;d[k]=Math.round(d[k]*al);d[k+1]=Math.round(d[k+1]*al);d[k+2]=Math.round(d[k+2]*al);}
    ctx.putImageData(im,0,0);
    let edge=0;for(let i=0;i<N;i++)for(let k=0;k<W*S;k++){edge=Math.max(edge,d[((0)*cv.width+i*W*S+k)*4+3],d[((H*S-1)*cv.width+i*W*S+k)*4+3]);}
    for(let i=0;i<N;i++)for(let k=0;k<H*S;k++){edge=Math.max(edge,d[(k*cv.width+i*W*S)*4+3],d[(k*cv.width+i*W*S+W*S-1)*4+3]);}
    const q=x=>Math.round(x*1e4)/1e4;
    return {png:cv.toDataURL('image/png'),edge,meta:{name:'Ring',file:'ValisOverdrive_Ring.png',columns:COLS,rows:ROWS,frames:N,
      width:q(W/L),length:q(H/L),centreAhead:q(-engineY/L),speed:0.25,startsOn:'Press',loops:false,followsShip:true,under:true,premultiplied:true,pixels:[cv.width,cv.height]}};
  },picked);
  fs.mkdirSync(outDir,{recursive:true});
  fs.writeFileSync(path.join(outDir,r.meta.file),Buffer.from(r.png.split(',')[1],'base64'));
  fs.writeFileSync(path.join(outDir,'meta-ring.json'),JSON.stringify(r.meta,null,2));
  console.log('edge alpha',r.edge,JSON.stringify(r.meta));await b.close();
})();
