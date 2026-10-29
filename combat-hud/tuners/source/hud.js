(function(global){
'use strict';
function create(){
  const hud=document.createElement('canvas'),glowMask=document.createElement('canvas');
  const ink=hud.getContext('2d'),halo=glowMask.getContext('2d');
  let roles;
  function geometry(draw,color=roles.ink,alpha=1,bloom=true){
    for(const c of bloom?[ink,halo]:[ink]){c.save();c.globalAlpha=alpha;c.strokeStyle=color;c.fillStyle=color;c.lineWidth=1.8;c.lineCap='round';c.lineJoin='round';draw(c);c.restore();}
  }
  function line(x1,y1,x2,y2,color,alpha=1,width=1.8){geometry(c=>{c.lineWidth=width;c.beginPath();c.moveTo(x1,y1);c.lineTo(x2,y2);c.stroke();},color,alpha);}
  function arc(x,y,r,start,end,color,alpha=1,width=1.8){geometry(c=>{c.lineWidth=width;c.beginPath();c.arc(x,y,r,start,end);c.stroke();},color,alpha);}
  function poly(points,color,alpha=1){geometry(c=>{c.beginPath();points.forEach(([x,y],i)=>i?c.lineTo(x,y):c.moveTo(x,y));c.closePath();c.fill();},color,alpha);}
  function text(value,x,y,size,family,color=roles.ink,alpha=1,align='center',spacing=0){ink.save();ink.fillStyle=color;ink.globalAlpha=alpha;ink.font=`400 ${size}px "${family}"`;ink.textAlign=align;ink.textBaseline='middle';ink.letterSpacing=`${spacing}px`;ink.fillText(value,x,y);ink.restore();}
  function objective(x,y,s=1,alpha=1){poly([[x-12*s,y-12*s],[x-3*s,y-12*s],[x-3*s,y+6*s],[x-12*s,y+13*s]],roles.objective,alpha);poly([[x+12*s,y-12*s],[x+3*s,y-12*s],[x+3*s,y+6*s],[x+12*s,y+13*s]],roles.objective,alpha);line(x-12*s,y+13*s,x+12*s,y+13*s,roles.objective,alpha,3*s);}
  function render({width,height,time,mode,age,roles:colors,type,capture,showHud=true}){
    roles=colors;
    if(hud.width!==width||hud.height!==height){hud.width=glowMask.width=width;hud.height=glowMask.height=height;}
    for(const c of [ink,halo]){c.setTransform(1,0,0,1,0,0);c.clearRect(0,0,width,height);c.scale(width/1600,height/900);}
    if(!showHud)return {hud,glowMask};
    const sans=type.functional==='roboto'?'Roboto Sans':'Plex Sans';
    const mono=type.functional==='roboto'?'Roboto Mono':'Plex Mono';
    const heading={wide:'Michroma',terminal:'Plex Mono',segmented:'DSEG14'}[type.heading];
    const reload=mode==='reload'?Math.min(age/3,1):1;
    const recover=mode==='recover'?Math.min(age/4,1):.72;
    const low=mode==='lowHull';
    const hull=low?.19:.78;
    const appear=mode==='appear'?Math.min(age/.65,1):1;
    const counter=19-Math.floor((time%12)/3);
    text(`0:${String(counter).padStart(2,'0')}`,800,62,45,type.segmented?'DSEG14':mono);
    const segments=[[745,772,1],[784,832,1],[844,871,.32]];
    segments.forEach(([a,b,alpha])=>line(a,108,b,108,roles.ink,alpha,alpha===1?3:1.5));
    text('NEXT PHASE',800,132,10,sans,roles.ink,roles.dimOpacity,'center',2);

    const mx=1460,my=132,mr=79;
    arc(mx,my,mr,0,Math.PI*2,roles.ink,appear,1.5);
    for(let i=0;i<4;i++){const a=i*Math.PI/2;line(mx+Math.cos(a)*(mr-4),my+Math.sin(a)*(mr-4),mx+Math.cos(a)*(mr+5),my+Math.sin(a)*(mr+5),roles.ink,appear*.6,1);}
    [[-33,-41],[31,-27],[-49,13],[28,41],[-17,50]].forEach(([x,y])=>arc(mx+x,my+y,2.8,0,Math.PI*2,roles.neutral,appear*.8,1.2));
    poly([[mx,my-8],[mx+6,my+6],[mx,my+3],[mx-6,my+6]],roles.friendly,appear);
    poly([[mx+37,my-4],[mx+45,my+8],[mx+31,my+8]],roles.threat,appear);
    poly([[mx-25,my+20],[mx-18,my+31],[mx-32,my+31]],roles.threat,appear);
    objective(mx+18,my-44,.33,appear);

    const cy=750;
    arc(800,cy,47,-Math.PI*.5,Math.PI*1.5,roles.ink,.23,1.4);
    arc(800,cy,47,-Math.PI*.5,-Math.PI*.5+Math.PI*2*recover,roles.ink,1,2.3);
    poly([[800,cy-23],[817,cy],[800,cy+23],[783,cy]],roles.ink);
    text(recover>=1?'READY':'',800,cy+66,10,sans,roles.ink,.75,'center',2);
    arc(688,cy,41,Math.PI*.63,Math.PI*1.37,roles.ink,.35);
    arc(688,cy,41,Math.PI*.63,Math.PI*.63+Math.PI*.74*reload,roles.ink,1,2);
    arc(912,cy,41,-Math.PI*.37,Math.PI*.37,roles.ink,.35);
    arc(912,cy,41,-Math.PI*.37,Math.PI*.2,roles.ink,1,2);
    for(let n=0;n<3;n++){const x=672+n*9;poly([[x,cy+3],[x+22,cy-15],[x+27,cy-15],[x+5,cy+3]],roles.ink);}
    poly([[912,cy-21],[917,cy-10],[917,cy+10],[922,cy+17],[913,cy+12],[903,cy+17],[907,cy+10],[907,cy-10]],roles.ink);
    text(reload<1?'RELOADING':'18',688,cy+30,reload<1?9:21,reload<1?sans:mono,roles.ink,1,'center',reload<1?1.1:0);
    text('6',912,cy+30,21,mono);
    if(reload<1){for(let i=0;i<18*reload;i++){const x=653+i*4;line(x,cy+47,x+7,cy+41,roles.ink,.9,1);}}
    const tracks=[[650,780,roles.shield,.9],[820,950,low?roles.threat:roles.hull,hull]];
    tracks.forEach(([x1,x2,color,value])=>{line(x1,843,x2,843,roles.ink,.25,1);for(let x=x1;x<x1+(x2-x1)*value;x+=7)line(x,831,x,840,color,1,2.4);});
    text('SHIELD',715,862,9,sans,roles.ink,roles.dimOpacity,'center',1.4);text('HULL',885,862,9,sans,roles.ink,roles.dimOpacity,'center',1.4);
    if(low){const pulse=.4+.6*(.5+.5*Math.sin(time*Math.PI*2));arc(800,cy,54,0,Math.PI*2,roles.threat,pulse,1.8);text('HULL LOW',800,680,13,sans,roles.threat,1,'center',2);}

    const aimX=961,aimY=470;
    [[-15,0,-6,0],[6,0,15,0],[0,-15,0,-6],[0,6,0,15]].forEach(v=>line(aimX+v[0],aimY+v[1],aimX+v[2],aimY+v[3],roles.ink,.9,1.3));
    if(reload<1)line(aimX-4,aimY+21,aimX+4,aimY+21,roles.ink,.6,1.3);
    const ships=capture==='copper'?[[515,168],[1110,574]]:capture==='blueCombat'?[[615,490],[800,342]]:[];
    ships.forEach(([x,y],i)=>{line(x-20,y,x+20,y,roles.ink,.23,1);line(x-20,y,x+8+i*8,y,roles.hull,.65,1.5);arc(x+26,y-1,3,0,Math.PI*2,roles.ink,.5,1);});
    objective(1510,452,.85);
    poly([[1543,452],[1534,444],[1538,452],[1534,460]],roles.objective,.9);
    if(capture==='blueCombat')objective(1052,353,.65);

    const banners={phase:['CONTACTS INBOUND','Try to keep the paint.'],extraction:['EXTRACTION OPEN','Your ride is here.'],cleared:['SECTOR CLEAR','That could have gone worse.'],victory:['RUN COMPLETE','Still in one piece. Mostly.'],lost:['SHIP LOST','That will need more than polish.']};
    if(banners[mode]&&age<4){const alpha=Math.min(age/.25,1)*Math.min((4-age)/.5,1);const color=mode==='lost'?roles.threat:roles.ink;text(banners[mode][0],800,253,type.heading==='wide'?25:30,heading,color,alpha,'center',type.heading==='segmented'?1:3);text(banners[mode][1],800,292,15,sans,roles.ink,alpha*.72);line(701,320,899,320,roles.ink,alpha*.45,1);}
    return {hud,glowMask};
  }
  return {render,hud,glowMask};
}
global.CanopyHUD={create};
})(window);
