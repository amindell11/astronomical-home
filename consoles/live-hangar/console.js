(async function(){
'use strict';
const $=id=>document.getElementById(id),embedded=JSON.parse($('embedded').textContent);
try{
  const types=embedded.types,css=getComputedStyle(document.documentElement);
  const roles=Object.fromEntries(['ink','shield','hull','threat','friendly','objective','neutral','backing'].map(key=>[key,css.getPropertyValue(`--ui-${key}`).trim()]));
  const state={settings:{...CanopyCRT.defaults},roles,type:{functional:'plex',heading:'wide'},enabled:true,playing:!matchMedia('(prefers-reduced-motion: reduce)').matches,surface:.12,tint:'cool',preset:'exploration',time:0,age:0,selection:-1};
  for(const item of types.headings)$('heading').add(new Option(item.label,item.id));
  for(const item of types.functional)$('family').add(new Option(item.label,item.id));
  await Promise.all([...new Set([...types.headings.map(t=>t.family),...types.functional.flatMap(t=>[t.sans,t.mono])])].map(async family=>{if(!(await document.fonts.load(`400 24px "${family}"`)).length)throw new Error(`Font did not load: ${family}`);}));
  const background=new Image();background.src=embedded.stage;await background.decode();
  const engine=CanopyCRT.create($('view')),size=engine.setBackground(background);
  const hud=document.createElement('canvas'),glowMask=document.createElement('canvas');
  for(const canvas of [hud,glowMask]){canvas.width=size.width;canvas.height=size.height;}
  const c=hud.getContext('2d'),m=glowMask.getContext('2d'),scale=size.width/2048;
  const outline=new Path2D('M955 310 L1106 317 L1096 209 L1130 214 L1205 280 L1265 304 L1453 303 L1583 329 L1720 380 Q1801 413 1820 439 L1818 454 Q1792 471 1748 468 L1638 452 L1538 420 L1507 451 L1496 477 L1417 546 L1266 546 L1119 531 L1072 508 L957 548 L1004 499 L1073 461 L1050 430 L957 410 L952 398 L1075 408 L1056 385 L948 376 L949 364 L1068 374 L1039 342 L956 330 Z');
  const details=[
    'M1106 317 L1211 327 L1265 304 M1096 209 L1118 288 L1205 280',
    'M1040 342 L1110 346 L1192 421 L1282 416 L1344 446 L1386 456 L1507 451',
    'M1068 374 L1124 377 M1075 408 L1163 417 M1073 461 L1128 462 L1185 511 L1266 515 L1266 546',
    'M1211 327 L1286 339 L1453 303 M1286 339 L1390 353 L1538 420',
    'M1400 355 L1463 345 L1583 329 M1463 345 L1534 383 L1638 396 L1748 444 L1818 454',
    'M1563 372 Q1640 354 1699 387 Q1748 412 1775 432 L1748 444 L1638 419 Z',
    'M1128 446 L1191 450 L1220 483 L1162 481 Z',
    'M1120 490 L1177 528 L1417 546 M1232 468 L1281 459 L1305 478 L1294 512 L1249 512',
    'M1386 456 L1443 491 L1417 523 M1266 515 L1380 517 M1162 455 L1179 477 M1173 456 L1191 479 M1184 456 L1202 479'
  ].map(p=>new Path2D(p));
  const patches=[new Path2D('M957 548 L1004 499 L1073 461 L1090 483 L1072 508 Z'),new Path2D('M1072 508 L1119 531 L1266 546 L1417 546 L1450 519 L1266 515 L1185 511 L1128 462 Z'),new Path2D('M1096 209 L1129 214 L1146 234 L1105 232 Z'),new Path2D('M1125 327 L1208 329 L1262 351 L1209 363 L1145 346 Z')];
  function path(ctx,p,color,width=1){ctx.strokeStyle=color;ctx.lineWidth=width;ctx.stroke(p);}
  function line(ctx,x1,y1,x2,y2,color,width=1){ctx.strokeStyle=color;ctx.lineWidth=width;ctx.beginPath();ctx.moveTo(x1,y1);ctx.lineTo(x2,y2);ctx.stroke();}
  function screen(){
    const family=types.functional.find(t=>t.id===state.type.functional),heading=types.headings.find(t=>t.id===state.type.heading);
    const ink=state.roles.ink;
    c.fillStyle='rgba(218,226,238,.48)';c.fillRect(165,183,548,88);
    c.save();c.globalCompositeOperation='destination-out';c.font=`400 49px "${heading.family}"`;c.fillText('HANGAR',187,246,510);c.restore();
    for(const ctx of [c,m]){line(ctx,165,294,713,294,ink,1.2);line(ctx,365,333,365,747,'rgba(225,233,242,.4)',1);}
    const labels=['SHIP','ENGINE','SHIELD','PRIMARY','SECONDARY'],values=['Vanguard','Default_Engine','Default_Shield','Lasers','Missiles'];
    for(let i=0;i<5;i++){
      const y=351+i*79,tab=new Path2D(`M166 ${y} H335 L350 ${y+15} V${y+44} H166 Z`);
      c.fillStyle='rgba(200,215,235,.065)';c.fill(tab);
      for(const ctx of [c,m]){path(ctx,tab,'rgba(226,234,243,.62)',1.1);line(ctx,166,y+62,713,y+62,'rgba(226,234,243,.2)',.8);}
      c.fillStyle=ink;c.font=`400 25px "${family.mono}"`;c.fillText(labels[i],177,y+31);
      c.font=`400 26px "${family.mono}"`;c.fillText(values[i],390,y+32,237);
      c.font=`400 31px "${family.sans}"`;c.fillText('›',685,y+33);if(i===0||i>2)c.fillText('‹',645,y+33);
      if(state.selection>=0&&state.selection<.8&&i===0){for(const ctx of [c,m])path(ctx,tab,`rgba(245,240,226,${1-state.selection/.8})`,2.4);}
    }
    const launch=new Path2D('M1640 957 L1655 943 H1980 L1992 956 V1027 L1976 1040 H1640 Z');
    for(const ctx of [c,m])path(ctx,launch,'rgba(230,237,248,.74)',1.7);
    c.font=`400 31px "${heading.family}"`;c.fillStyle=ink;c.textAlign='center';c.fillText('LAUNCH',1807,1006,260);c.textAlign='left';
    c.font=`400 38px "${family.sans}"`;c.fillText('›',1950,1008);
  }
  function ship(){
    const cold=state.tint==='cool',edge=cold?'rgba(224,235,255,.93)':state.roles.ink;
    const beam=c.createLinearGradient(0,480,0,920);beam.addColorStop(0,'rgba(160,185,245,0)');beam.addColorStop(1,'rgba(160,185,245,.09)');
    c.fillStyle=beam;c.beginPath();c.moveTo(1150,500);c.lineTo(1580,475);c.lineTo(1340,919);c.lineTo(1265,919);c.closePath();c.fill();
    c.fillStyle=`rgba(${cold?'130,160,225':'200,194,180'},${state.surface*.36})`;c.fill(outline);
    c.fillStyle=`rgba(213,139,120,${state.surface})`;for(const p of patches)c.fill(p);
    for(const ctx of [c,m]){
      path(ctx,outline,edge,2.2);
      for(const p of details)path(ctx,p,cold?'rgba(210,225,255,.58)':'rgba(240,231,216,.58)',1.1);
      ctx.strokeStyle='rgba(150,224,241,.84)';ctx.lineWidth=1.7;ctx.beginPath();ctx.arc(1348,466,28,0,Math.PI*2);ctx.stroke();
      ctx.strokeStyle='rgba(190,214,255,.65)';ctx.beginPath();ctx.ellipse(1301,919,34,7,0,0,Math.PI*2);ctx.stroke();
    }
    c.fillStyle=`rgba(113,200,220,${state.surface*.65})`;c.beginPath();c.arc(1348,466,26,0,Math.PI*2);c.fill();
  }
  function render(delta=0){
    for(const ctx of [c,m]){ctx.setTransform(1,0,0,1,0,0);ctx.clearRect(0,0,size.width,size.height);ctx.setTransform(scale,0,0,scale,0,0);ctx.lineJoin='round';ctx.lineCap='round';}
    screen();ship();
    engine.render({hud,glowMask,settings:state.settings,time:state.time,delta,enabled:state.enabled,appearAge:state.age,transitionAge:state.age,hitAge:-1});
  }
  function exportSettings(){return {schemaVersion:1,status:'review-draft',context:'menus',personalityEnabled:state.enabled,personality:{...state.settings},colors:{...state.roles},typography:{...state.type},console:{surface:state.surface,tint:state.tint}};}
  function refresh(){engine.clear();render();$('settings').value=JSON.stringify(exportSettings(),null,2);}
  $('heading').onchange=e=>{state.type.heading=e.target.value;refresh();};$('family').onchange=e=>{state.type.functional=e.target.value;refresh();};
  $('preset').onchange=e=>{state.preset=e.target.value;state.settings={...CanopyCRT.presets[state.preset]};refresh();};
  $('enabled').onchange=e=>{state.enabled=e.target.checked;refresh();};
  $('surface').oninput=e=>{state.surface=Number(e.target.value);$('surfaceValue').value=state.surface.toFixed(2);refresh();};
  $('tint').onchange=e=>{state.tint=e.target.value;refresh();};
  $('pause').onclick=()=>{state.playing=!state.playing;$('pause').textContent=state.playing?'Pause':'Resume';};
  $('replay').onclick=()=>{state.age=0;state.playing=true;$('pause').textContent='Pause';engine.clear();};
  $('selection').onclick=()=>{state.selection=0;state.playing=true;$('pause').textContent='Pause';};
  $('expand').onclick=() =>document.body.classList.add('focus');$('exitFocus').onclick=()=>document.body.classList.remove('focus');
  $('export').onclick=()=>$('settings').value=JSON.stringify(exportSettings(),null,2);
  $('apply').onclick=()=>{
    try{
      const data=JSON.parse($('settings').value);
      if(data.schemaVersion!==1||typeof data.personalityEnabled!=='boolean')throw new Error('Use a schema version 1 export from the shared tuner.');
      for(const k of CanopyCRT.knobs){const value=data.personality?.[k.key];if(!Number.isFinite(value)||value<k.min||value>k.max)throw new Error(`Invalid shared setting: ${k.label}`);}
      if(!types.functional.some(t=>t.id===data.typography?.functional)||!types.headings.some(t=>t.id===data.typography?.heading))throw new Error('Choose a shared functional and heading candidate.');
      for(const key of Object.keys(roles))if(!/^#[0-9a-f]{6}$/i.test(data.colors?.[key]))throw new Error(`Invalid shared color: ${key}`);
      if(data.console&&(!Number.isFinite(data.console.surface)||data.console.surface<0||data.console.surface>.4||!['cool','neutral'].includes(data.console.tint)))throw new Error('Invalid console surface or tint.');
      state.settings={...data.personality};state.enabled=data.personalityEnabled;state.type={...data.typography};state.roles={...data.colors};state.preset='custom';
      if(data.console){state.surface=data.console.surface;state.tint=data.console.tint;$('surface').value=state.surface;$('surfaceValue').value=state.surface.toFixed(2);$('tint').value=state.tint;}
      $('preset').value='custom';$('enabled').checked=state.enabled;$('family').value=state.type.functional;$('heading').value=state.type.heading;refresh();$('status').textContent='Shared settings applied.';
    }catch(error){$('status').textContent=error.message;}
  };
  $('download').onclick=()=>{const url=URL.createObjectURL(new Blob([JSON.stringify(exportSettings(),null,2)],{type:'application/json'}));const a=document.createElement('a');a.href=url;a.download='console-menu-review.json';a.click();setTimeout(()=>URL.revokeObjectURL(url),1000);};
  if(!state.playing){state.age=1;$('pause').textContent='Resume';}
  refresh();globalThis.consoleReview={ready:true,state,engine,render,exportSettings};
  let last=performance.now();requestAnimationFrame(function frame(now){const delta=Math.min((now-last)/1000,.1);last=now;if(state.playing){state.time+=delta;state.age+=delta;if(state.selection>=0)state.selection+=delta;render(delta);}requestAnimationFrame(frame);});
}catch(error){$('error').hidden=false;$('error').textContent=error.message;console.error(error);}
})();
