(async function(){
'use strict';
const $=id=>document.getElementById(id);
try{
  const embedded=JSON.parse($('embedded').textContent);
  const state={settings:{...CanopyCRT.defaults},roles:{...embedded.roles},type:{functional:'plex',heading:'wide',segmented:true},enabled:true,preset:'exploration',capture:'bright',playing:true,sequence:true,time:0,mode:'appear',age:0,showHud:true};
  const sequence=[['appear',2],['reload',4],['recover',5],['hit',2],['lowHull',4],['phase',4],['extraction',4],['steady',3]];
  const names={steady:'Steady HUD',appear:'Element appearing',reload:'Weapon reloading',recover:'Keystone recovering',hit:'Damage shudder',lowHull:'Low hull, informational pulse',phase:'Phase-start banner',extraction:'Extraction-open banner',cleared:'Sector cleared',victory:'Victory',lost:'Ship lost'};
  const sources={bright:'Locale 3 · unchanged Unity sky capture',copper:'Copper & Turquoise · unchanged gameplay capture',blueCombat:'Blue combat · unchanged gameplay capture'};
  const engine=CanopyCRT.create($('view')),hud=CanopyHUD.create();
  let dimensions,dirty=true,cycleIndex=0,last=performance.now(),frames=0,fpsTime=last;
  const images=Object.fromEntries(await Promise.all(Object.entries(embedded.captures).map(async([key,src])=>{const image=new Image();image.src=src;await image.decode();return [key,image];})));
  await Promise.all(['Plex Sans','Plex Mono','Roboto Sans','Roboto Mono','Michroma','DSEG14'].map(async name=>{const loaded=await document.fonts.load(`400 24px "${name}"`);if(loaded.length===0)throw new Error(`The embedded font ${name} did not load.`);}));
  function exportSettings(){
    return {schemaVersion:1,status:'review-draft',context:'combat',preset:state.preset,personalityEnabled:state.enabled,personality:{...state.settings},colors:{...state.roles},typography:{...state.type},capture:state.capture};
  }
  function updateExport(){$('export').value=JSON.stringify(exportSettings(),null,2);dirty=true;}
  function updateKnobs(){for(const k of CanopyCRT.knobs){$(k.key).value=state.settings[k.key];$(`${k.key}-value`).textContent=`${Number(state.settings[k.key]).toFixed(k.step<.1?2:1)}${k.unit}`;}$('preset').value=state.preset;updateExport();}
  function swatchPreview(row,key){
    const canvas=row.querySelector('canvas'),c=canvas.getContext('2d');
    c.clearRect(0,0,180,54);c.drawImage(images[state.capture],0,0,180,54);
    if(key==='backing'){c.fillStyle=state.roles.backing;c.globalAlpha=.85;c.fillRect(30,10,120,34);c.globalAlpha=1;c.fillStyle=state.roles.ink;}
    else{c.globalCompositeOperation='lighter';c.fillStyle=state.roles[key];}
    c.font='400 20px "Plex Mono"';c.textAlign='center';c.fillText('08:41',90,34);c.globalCompositeOperation='source-over';
  }
  function setCapture(id){state.capture=id;$('capture').value=id;dimensions=engine.setBackground(images[id]);$('sourceLabel').textContent=sources[id];document.querySelectorAll('[data-color]').forEach(row=>swatchPreview(row,row.dataset.color));updateExport();}
  function setEvent(mode){state.mode=mode;state.age=0;state.sequence=false;$('sequence').checked=false;state.playing=true;$('play').textContent='Pause';engine.clear();dirty=true;}
  function renderOnce(delta=.016){
    const drawing=hud.render({...dimensions,time:state.time,mode:state.mode,age:state.age,roles:state.roles,type:state.type,capture:state.capture,showHud:state.showHud});
    const lowHullTime=state.age%19,lowHullHits=[0,1.23,3.78,7.1,8.64,13.27,17.9];
    const lastLowHullHit=lowHullHits.filter(start=>start<=lowHullTime).at(-1);
    engine.render({...drawing,settings:state.settings,time:state.time,delta,enabled:state.enabled,appearAge:['appear','phase','extraction','cleared','victory','lost'].includes(state.mode)?state.age:-1,hitAge:state.mode==='hit'?state.age:state.mode==='lowHull'?lowHullTime-lastLowHullHit:-1,transitionAge:['phase','extraction','cleared','victory','lost'].includes(state.mode)?state.age:-1});
    $('eventStatus').textContent=names[state.mode];dirty=false;
  }
  for(const k of CanopyCRT.knobs){
    const label=document.createElement('label');label.className='field';label.innerHTML=`<span>${k.label}<output id="${k.key}-value"></output></span><input id="${k.key}" type="range" min="${k.min}" max="${k.max}" step="${k.step}" aria-label="${k.label}">`;
    $('knobs').appendChild(label);$(k.key).addEventListener('input',event=>{state.settings[k.key]=Number(event.target.value);state.preset='custom';updateKnobs();});
  }
  const roleNames={ink:'Ink',shield:'Shield',hull:'Hull',threat:'Threat',friendly:'You / friendly',objective:'Objective',neutral:'Neutral world',backing:'Console backing'};
  for(const [key,name] of Object.entries(roleNames)){
    const wrap=document.createElement('div');wrap.dataset.color=key;wrap.innerHTML=`<label class="swatch"><span>${name}</span><input type="color" value="${state.roles[key]}" aria-label="${name} color"><code>${state.roles[key]}</code></label><canvas width="180" height="54" aria-label="${name} ink over the selected capture" style="width:100%;height:54px;object-fit:cover"></canvas>`;
    $('colors').appendChild(wrap);wrap.querySelector('input').addEventListener('input',event=>{state.roles[key]=event.target.value;wrap.querySelector('code').textContent=event.target.value;swatchPreview(wrap,key);updateExport();});
  }
  const dim=document.createElement('label');dim.className='field';dim.innerHTML='<span>Dim ink opacity<output id="dim-value">0.55</output></span><input id="dimOpacity" type="range" min="0.15" max="0.8" step="0.01" value="0.55" aria-label="Dim ink opacity">';$('colors').appendChild(dim);
  $('dimOpacity').addEventListener('input',event=>{state.roles.dimOpacity=Number(event.target.value);$('dim-value').textContent=event.target.value;updateExport();});
  $('capture').addEventListener('change',event=>setCapture(event.target.value));
  $('personality').addEventListener('change',event=>{state.enabled=event.target.checked;engine.clear();updateExport();});
  $('preset').addEventListener('change',event=>{state.preset=event.target.value;state.settings={...CanopyCRT.presets[state.preset]};state.enabled=true;$('personality').checked=true;engine.clear();updateKnobs();});
  $('zero').addEventListener('click',()=>{state.settings=Object.fromEntries(CanopyCRT.knobs.map(k=>[k.key,0]));state.preset='custom';engine.clear();updateKnobs();});
  $('play').addEventListener('click',()=>{state.playing=!state.playing;$('play').textContent=state.playing?'Pause':'Resume';dirty=true;});
  $('sequence').addEventListener('change',event=>{state.sequence=event.target.checked;if(state.sequence){cycleIndex=0;state.mode=sequence[0][0];state.age=0;state.playing=true;$('play').textContent='Pause';}dirty=true;});
  document.querySelectorAll('[data-event]').forEach(button=>button.addEventListener('click',()=>setEvent(button.dataset.event)));
  $('family').addEventListener('change',event=>{state.type.functional=event.target.value;updateExport();});
  $('heading').addEventListener('change',event=>{state.type.heading=event.target.value;setEvent('phase');updateExport();});
  $('segmented').addEventListener('change',event=>{state.type.segmented=event.target.checked;updateExport();});
  $('focus').addEventListener('click',()=>{document.body.classList.toggle('focus');$('focus').textContent=document.body.classList.contains('focus')?'Show controls':'Expand preview';});
  $('copy').addEventListener('click',async()=>{try{await navigator.clipboard.writeText($('export').value);$('exportStatus').textContent='Settings copied.';}catch(error){$('export').focus();$('export').select();$('exportStatus').textContent='The browser blocked clipboard access. Copy the selected block.';}});
  $('download').addEventListener('click',()=>{const url=URL.createObjectURL(new Blob([$('export').value],{type:'application/json'}));const a=document.createElement('a');a.href=url;a.download='canopy-settings.json';a.click();setTimeout(()=>URL.revokeObjectURL(url),1000);$('exportStatus').textContent='Settings file downloaded.';});
  $('references').innerHTML='<a href="https://github.com/amindell11/astronomical-home/tree/cd45b620e2b3f7c32f14af03df73a1d6f4bdce9a">Owner mood references: vector CRT, phosphor screens and projected light</a>';
  if(embedded.view==='color'){$('personalitySection').open=false;$('typeSection').open=false;$('colorSection').open=true;}
  setCapture('bright');updateKnobs();
  globalThis.canopyTuner={state,engine,renderOnce,setCapture,setEvent,exportSettings,ready:true};
  function loop(now){
    const delta=Math.min((now-last)/1000,.1);last=now;
    if(state.playing){state.time+=delta;state.age+=delta;if(state.sequence&&state.age>=sequence[cycleIndex][1]){state.age=0;cycleIndex=(cycleIndex+1)%sequence.length;state.mode=sequence[cycleIndex][0];}dirty=true;}
    if(dirty){renderOnce(state.playing?delta:0);frames++;}
    if(now-fpsTime>1000){$('fps').textContent=state.playing?`${Math.round(frames*1000/(now-fpsTime))} fps`:'Paused';frames=0;fpsTime=now;}
    requestAnimationFrame(loop);
  }
  requestAnimationFrame(loop);
}catch(error){$('error').hidden=false;$('error').textContent=error.message;console.error(error);}
})();
