const ImpactMotion = (() => {
  const WORK_SIZE = 320; // Raster size for interactive warping.
  const PALETTE = [[8,104,250],[0,214,250],[184,250,244],[250,252,252]]; // Approved energy bands.
  const BAND_NAMES = ['Blue rim','Cyan','Pale mint','White core'];
  const BODY_END = 0.66; // Body disappears before the last sparks.
  const BAND_END = [0.66,0.60,0.50,0.38]; // Hotter bands vanish first.
  const SPARK_START = 0.10; // Sparks leave after the initial contact flash.
  const SPARK_LENGTH_PX = 6; // Starting spark length at the default display scale.
  const SPARK_WIDTH_PX = 1.4; // Keeps sparks legible without dominating the fan.
  const GLOW_RADIUS_PX = 18; // Glow stays near contact.
  const WARP_CYCLES = 2.5; // Uneven edge motion across one body lifetime.
  const RAY_WINDOWS = [[-1.35,-.70],[-.70,-.23],[-.23,.23],[.23,.78],[.78,1.35]]; // One source-derived ray per lobe.
  const clamp = (x,a=0,b=1) => Math.max(a,Math.min(b,x));
  const smooth = x => {x=clamp(x);return x*x*(3-2*x);};
  const canvas = (w,h=w) => {const c=document.createElement('canvas');c.width=w;c.height=h;return c;};

  function parse(source) {
    const native=canvas(source.naturalWidth,source.naturalHeight),nc=native.getContext('2d',{willReadFrequently:true});
    nc.drawImage(source,0,0);
    const original=nc.getImageData(0,0,native.width,native.height);
    const labels=new Uint8Array(native.width*native.height),counts=[0,0,0,0];
    let minX=native.width,maxX=0,minY=native.height,maxY=0,whiteY=0,whiteWeight=0;
    for(let p=0;p<labels.length;p++) {
      const i=p*4,a=original.data[i+3];if(!a)continue;
      let band=0,best=Infinity;
      PALETTE.forEach((color,b)=>{let distance=0;for(let k=0;k<3;k++)distance+=(original.data[i+k]-color[k])**2;if(distance<best){best=distance;band=b;}});
      labels[p]=band+1;counts[band]++;
      if(a>240){const x=p%native.width,y=Math.floor(p/native.width);minX=Math.min(minX,x);maxX=Math.max(maxX,x);minY=Math.min(minY,y);maxY=Math.max(maxY,y);if(band===3){whiteY+=y;whiteWeight++;}}
    }
    if(!whiteWeight||maxX<=minX)throw Error('The impact source has no usable contact core.');
    const anchor=[minX,whiteY/whiteWeight],bounds=[minX,minY,maxX+1,maxY+1];
    const work=canvas(WORK_SIZE),wc=work.getContext('2d',{willReadFrequently:true});wc.drawImage(source,0,0,WORK_SIZE,WORK_SIZE);
    const pixels=wc.getImageData(0,0,WORK_SIZE,WORK_SIZE),bands=new Uint8Array(WORK_SIZE*WORK_SIZE);
    for(let p=0;p<bands.length;p++)bands[p]=labels[Math.min(native.height-1,Math.floor(p/WORK_SIZE*native.height/WORK_SIZE))*native.width+Math.min(native.width-1,Math.floor((p%WORK_SIZE)*native.width/WORK_SIZE))]-1;
    const rays=RAY_WINDOWS.map(([lo,hi])=>{
      let bestAngle=(lo+hi)/2,bestReach=0;
      for(let angle=lo;angle<=hi;angle+=.012){let reach=0;for(let r=10;r<native.width;r+=3){const x=Math.round(anchor[0]+Math.cos(angle)*r),y=Math.round(anchor[1]+Math.sin(angle)*r);if(x<0||y<0||x>=native.width||y>=native.height)break;if(original.data[(y*native.width+x)*4+3]>240)reach=r;}if(reach>bestReach){bestReach=reach;bestAngle=angle;}}
      return bestAngle;
    });
    return {source,native,original,labels,counts,anchor,bounds,solidWidth:maxX-minX+1,pixels,bands,rays};
  }

  function create(parsed) {
    const frame=canvas(WORK_SIZE),fc=frame.getContext('2d'),output=fc.createImageData(WORK_SIZE,WORK_SIZE);
    const k=WORK_SIZE/parsed.native.width,ax=parsed.anchor[0]*k,ay=parsed.anchor[1]*k,span=parsed.solidWidth*k;
    let cachedKey='';
    function raster(time,settings) {
      if(time===0)return parsed.source;
      const key=[time,settings.durationMs,settings.fanMotion].join('|');if(key===cachedKey)return frame;cachedKey=key;
      output.data.fill(0);
      const u=time/settings.durationMs,q=clamp(u/BODY_END);
      if(time<0||u>=BODY_END){fc.putImageData(output,0,0);return frame;}
      const expansion=Math.sin(Math.PI*q),stretch=1+settings.fanMotion*expansion,spread=1+settings.fanMotion*.75*expansion;
      const src=parsed.pixels.data;
      for(let y=0;y<WORK_SIZE;y++)for(let x=0;x<WORK_SIZE;x++){
        const dx=x-ax,dy=y-ay,sx=ax+dx/stretch;
        const phase=dx/40+q*WARP_CYCLES,tri=2*Math.abs(2*(phase-Math.floor(phase))-1)-1;
        const edge=1+settings.fanMotion*.35*tri*clamp(dx/span)*expansion;
        const sy=ay+dy/(spread*edge);
        if(sx<0||sy<0||sx>=WORK_SIZE-1||sy>=WORK_SIZE-1)continue;
        const ix=Math.floor(sx),iy=Math.floor(sy),fx=sx-ix,fy=sy-iy;
        const locations=[iy*WORK_SIZE+ix,iy*WORK_SIZE+ix+1,(iy+1)*WORK_SIZE+ix,(iy+1)*WORK_SIZE+ix+1];
        const weights=[(1-fx)*(1-fy),fx*(1-fy),(1-fx)*fy,fx*fy];
        let alpha=0,r=0,g=0,b=0;
        for(let n=0;n<4;n++){const p=locations[n],i=p*4,band=parsed.bands[p];if(band>3)continue;const end=BAND_END[band],fade=1-smooth((u-end*.38)/(end*.62));const a=src[i+3]/255*weights[n]*fade;alpha+=a;r+=src[i]*a;g+=src[i+1]*a;b+=src[i+2]*a;}
        if(alpha<=0)continue;const o=(y*WORK_SIZE+x)*4;output.data[o]=r/alpha;output.data[o+1]=g/alpha;output.data[o+2]=b/alpha;output.data[o+3]=alpha*255;
      }
      fc.putImageData(output,0,0);return frame;
    }

    function draw(ctx,time,settings,contact,width=settings.widthPx) {
      const u=time/settings.durationMs;if(time<0||u>=1)return;
      const scale=width/parsed.solidWidth,relative=width/settings.widthPx;
      ctx.save();ctx.translate(contact[0],contact[1]);
      if(time>0&&u<BODY_END&&settings.glow>0){
        const radius=GLOW_RADIUS_PX*relative,alpha=settings.glow*(1-smooth(u/BODY_END));
        const g=ctx.createRadialGradient(1,0,0,1,0,radius);g.addColorStop(0,`rgba(70,185,255,${alpha})`);g.addColorStop(.4,`rgba(8,104,250,${alpha*.4})`);g.addColorStop(1,'rgba(8,104,250,0)');ctx.fillStyle=g;ctx.fillRect(-radius,-radius,radius*2,radius*2);
      }
      if(u<BODY_END){const body=raster(time,settings);ctx.drawImage(body,-parsed.anchor[0]*scale,-parsed.anchor[1]*scale,parsed.native.width*scale,parsed.native.height*scale);}
      if(u>SPARK_START&&settings.sparkReachPx>0){
        const progress=(u-SPARK_START)/(1-SPARK_START),fade=1-smooth(progress),reach=settings.sparkReachPx*relative;
        parsed.rays.forEach((angle,i)=>{
          const distance=(width*.18+reach*(1-(1-progress)**2))*(.82+i*.045);
          const length=SPARK_LENGTH_PX*relative*(1-progress),thickness=SPARK_WIDTH_PX*relative*(1-progress);
          ctx.save();ctx.rotate(angle);ctx.translate(distance,0);ctx.globalAlpha=fade;
          ctx.fillStyle=i%2?'#b8faf4':'#00d6fa';ctx.beginPath();ctx.moveTo(0,0);ctx.lineTo(-length,-thickness*.5);ctx.lineTo(-length*.65,0);ctx.lineTo(-length,thickness*.5);ctx.closePath();ctx.fill();ctx.restore();
        });
      }
      ctx.restore();
    }

    function debug(index) {
      const c=canvas(parsed.native.width,parsed.native.height),ctx=c.getContext('2d'),data=ctx.createImageData(c.width,c.height);
      for(let p=0;p<parsed.labels.length;p++){if(parsed.labels[p]!==index+1)continue;for(let channel=0;channel<4;channel++)data.data[p*4+channel]=parsed.original.data[p*4+channel];}
      ctx.putImageData(data,0,0);return c;
    }
    return {draw,raster,debug,parsed};
  }
  return {parse,create,PALETTE,BAND_NAMES,WORK_SIZE};
})();
