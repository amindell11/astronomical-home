(async function(){
'use strict';
const $=id=>document.getElementById(id);
const api=window.consolePalette={renderReady:false};
const STORAGE_KEY='astronomical.console-materials.neutral-materials.v1';
const clone=value=>JSON.parse(JSON.stringify(value));
const normalizeColor=value=>{
  const cleaned=String(value).trim().replace(/^#/,'');
  if(/^[\da-f]{3}$/i.test(cleaned))return '#'+[...cleaned].map(c=>c+c).join('').toLowerCase();
  if(/^[\da-f]{6}$/i.test(cleaned))return '#'+cleaned.toLowerCase();
  throw new Error('Use six hex digits, such as #8f8272.');
};
try{
  const embedded=window.CONSOLE_PALETTE_ASSETS;
  if(!embedded)throw new Error('The embedded console images are missing. Rebuild palette-assets.js.');
  const regions=embedded.metadata.regions;
  if(!Array.isArray(regions)||regions.length===0)throw new Error('The console material regions are missing.');
  const sourceImage=embedded.metadata.sourceImage;
  const imageInfo=typeof sourceImage==='string'?{file:sourceImage}:sourceImage;
  const imageFile=imageInfo.file||'neutral-materials.png';
  const loadImage=async src=>{const image=new Image();image.src=src;await image.decode();return image;};
  const [source,mask]=await Promise.all([loadImage(embedded.image),loadImage(embedded.mask)]);
  if(source.width!==mask.width||source.height!==mask.height)throw new Error('The material mask does not match the source image.');
  const canvas=$('view'),context=canvas.getContext('2d',{willReadFrequently:true});
  canvas.width=source.width;canvas.height=source.height;
  const scratch=document.createElement('canvas');scratch.width=source.width;scratch.height=source.height;
  const scratchContext=scratch.getContext('2d',{willReadFrequently:true});
  scratchContext.drawImage(source,0,0);
  const original=scratchContext.getImageData(0,0,source.width,source.height);
  scratchContext.clearRect(0,0,source.width,source.height);scratchContext.drawImage(mask,0,0);
  const membership=scratchContext.getImageData(0,0,mask.width,mask.height).data;
  const indices=new Map(regions.map(region=>[region.id,[]]));
  for(let index=0;index<membership.length;index+=4){
    if(membership[index]===0||membership[index+1]===0)continue;
    if(!indices.has(membership[index]))throw new Error('The mask contains an unknown material region.');
    indices.get(membership[index]).push(index);
  }
  for(const [id,list] of indices)indices.set(id,new Uint32Array(list));
  const output=context.createImageData(source.width,source.height);
  const srgbToLinear=value=>value<=.04045?value/12.92:((value+.055)/1.055)**2.4;
  const linearToByte=value=>Math.round(255*(value<=.0031308?12.92*value:1.055*value**(1/2.4)-.055));
  const linearBytes=Float64Array.from({length:256},(_,value)=>srgbToLinear(value/255));
  const linearColor=hex=>[1,3,5].map(start=>linearBytes[parseInt(hex.slice(start,start+2),16)]);
  const referenceColors=Object.fromEntries(regions.map(region=>[region.key,linearColor(normalizeColor(region.referenceColor||region.originalColor))]));
  if(Object.values(referenceColors).some(rgb=>rgb.some(channel=>channel===0)))throw new Error('Reference material colors must have nonzero RGB channels.');
  const defaults=()=>({context:'console-materials',sourceImage:imageFile,preset:'darker-warm-neutral',
    roles:Object.fromEntries(regions.map(region=>[region.key,normalizeColor(region.defaultColor)])),
    brightness:Object.fromEntries(regions.map(region=>[region.key,1])),
    enabled:Object.fromEntries(regions.map(region=>[region.key,true]))});
  let state=defaults(),showOriginal=false,queued=false,storageAvailable=true;
  let initialStorageNotice='Your choices save in this browser.';
  function exportSettings(){return {schemaVersion:1,status:'review-draft',context:state.context,preset:state.preset,colors:{...state.roles},brightness:{...state.brightness},enabled:{...state.enabled},sourceImage:state.sourceImage,sourceSHA256:embedded.sourceSHA256};}
  function parseSaved(text){
    const saved=JSON.parse(text);
    if(saved.schemaVersion!==1||saved.context!=='console-materials'||saved.sourceImage!==imageFile||saved.sourceSHA256!==embedded.sourceSHA256)throw new Error('Saved palette belongs to a different source.');
    const parsed=defaults();parsed.preset=saved.preset==='darker-warm-neutral'?'darker-warm-neutral':saved.preset==='original'?'original':'custom';
    for(const region of regions){
      parsed.roles[region.key]=normalizeColor(saved.colors[region.key]);
      const value=saved.brightness[region.key];
      if(!Number.isFinite(value)||value<.35||value>1.6)throw new Error('Saved brightness is out of range.');
      parsed.brightness[region.key]=value;
      if(typeof saved.enabled[region.key]!=='boolean')throw new Error('Saved material enable state is invalid.');
      parsed.enabled[region.key]=saved.enabled[region.key];
    }
    return parsed;
  }
  try{const saved=localStorage.getItem(STORAGE_KEY);if(saved){state=parseSaved(saved);initialStorageNotice='Restored your saved palette.';}}
  catch(error){if(error.name==='SecurityError'){storageAvailable=false;initialStorageNotice='Browser storage is unavailable. Download the palette to keep it.';}else initialStorageNotice='The saved palette could not be read. Started with darker warm neutrals.';}
  function persist(){
    if(!storageAvailable)return;
    try{localStorage.setItem(STORAGE_KEY,JSON.stringify(exportSettings()));$('save-status').textContent='Palette saved in this browser.';}
    catch(error){storageAvailable=false;$('save-status').textContent='Browser storage is unavailable. Download the palette to keep it.';}
  }
  function updateExport(){
    $('export').value=JSON.stringify(exportSettings(),null,2);
    $('palette-name').textContent=state.preset==='darker-warm-neutral'?'Darker warm neutrals':state.preset==='original'?'Source material colors':'Custom palette';
  }
  function draw(){
    queued=false;
    if(showOriginal){context.putImageData(original,0,0);return;}
    output.data.set(original.data);
    for(const region of regions){
      if(!state.enabled[region.key])continue;
      const target=linearColor(state.roles[region.key]);
      const gains=target.map((value,channel)=>value/referenceColors[region.key][channel]*state.brightness[region.key]);
      if(gains.every(gain=>gain===1))continue;
      const lookup=gains.map(gain=>Uint8Array.from(linearBytes,value=>linearToByte(Math.min(1,value*gain))));
      for(const index of indices.get(region.id)){
        const coverage=membership[index+1]/255;
        const isLight=region.key==='projectionLights';
        const reference=referenceColors[region.key];
        const lightShade=isLight?(linearBytes[original.data[index]]*.2126+linearBytes[original.data[index+1]]*.7152+linearBytes[original.data[index+2]]*.0722)/(reference[0]*.2126+reference[1]*.7152+reference[2]*.0722)*state.brightness[region.key]:0;
        for(let channel=0;channel<3;channel++){
          const sourceValue=original.data[index+channel];
          const recolored=isLight?linearToByte(Math.min(1,target[channel]*lightShade)):lookup[channel][sourceValue];
          output.data[index+channel]=Math.round(sourceValue+(recolored-sourceValue)*coverage);
        }
      }
    }
    context.putImageData(output,0,0);
  }
  function scheduleDraw(){if(!queued){queued=true;requestAnimationFrame(draw);}}
  function setOriginal(value){
    showOriginal=Boolean(value);$('compare').setAttribute('aria-pressed',String(showOriginal));
    $('compare').textContent=showOriginal?'Return to palette':'Show original';
    $('view-status').textContent=showOriginal?'Original image · downloads match this view':'Palette preview';
    canvas.setAttribute('aria-label',showOriginal?'Original console image':'Console with the current material colors');
    draw();
  }
  const controls=new Map();
  function updateControl(key){
    const fields=controls.get(key);if(!fields)return;
    fields.color.value=state.roles[key];fields.hex.value=state.roles[key];fields.hex.setAttribute('aria-invalid','false');fields.error.hidden=true;
    fields.range.value=Math.round(state.brightness[key]*100);fields.output.value=Math.round(state.brightness[key]*100)+'%';
    if(fields.enabled){fields.enabled.checked=state.enabled[key];fields.fieldset.classList.toggle('disabled',!state.enabled[key]);for(const input of [fields.color,fields.hex,fields.range])input.disabled=!state.enabled[key];}
  }
  function changed(key){state.preset='custom';if(showOriginal)setOriginal(false);updateControl(key);updateExport();persist();scheduleDraw();}
  function setRole(key,values){
    if(!controls.has(key))throw new Error('Unknown material role: '+key);
    if(values.color!==undefined)state.roles[key]=normalizeColor(values.color);
    if(values.brightness!==undefined){if(!Number.isFinite(values.brightness)||values.brightness<.35||values.brightness>1.6)throw new Error('Brightness must be between 0.35 and 1.6.');state.brightness[key]=values.brightness;}
    if(values.enabled!==undefined){if(typeof values.enabled!=='boolean')throw new Error('Enabled must be a boolean.');state.enabled[key]=values.enabled;}
    changed(key);draw();return exportSettings();
  }
  for(const region of regions){
    const optional=region.key==='projectionLights'||region.optional===true;
    if(optional){const heading=document.createElement('div');heading.className='light-heading';heading.innerHTML='<h2>Optional light color</h2><p>Only the cyan controls and cable light. The projection beam stays amber.</p>';$('light-controls').appendChild(heading);}
    const fieldset=document.createElement('fieldset');const prefix='material-'+region.key;
    const legend=document.createElement('legend');legend.textContent=region.label;fieldset.appendChild(legend);
    if(optional){const label=document.createElement('label');label.className='enabled-label';label.innerHTML=`<input id="${prefix}-enabled" type="checkbox"> Tune these accents`;fieldset.appendChild(label);}
    const row=document.createElement('div');row.className='color-row';
    row.innerHTML=`<input id="${prefix}-color" type="color" aria-label="${region.label} color"><input id="${prefix}-hex" class="hex" type="text" maxlength="7" spellcheck="false" autocomplete="off" aria-label="${region.label} hex color" aria-describedby="${prefix}-error">`;
    fieldset.appendChild(row);
    const brightness=document.createElement('label');brightness.className='brightness-label';brightness.htmlFor=prefix+'-brightness';brightness.innerHTML=`<span>Brightness</span><output id="${prefix}-value" for="${prefix}-brightness"></output>`;fieldset.appendChild(brightness);
    const range=document.createElement('input');range.id=prefix+'-brightness';range.type='range';range.min='35';range.max='160';range.step='1';range.setAttribute('aria-label',region.label+' brightness');fieldset.appendChild(range);
    const error=document.createElement('p');error.id=prefix+'-error';error.className='field-error';error.hidden=true;error.textContent='Use six hex digits, such as #8f8272.';fieldset.appendChild(error);
    $(optional?'light-controls':'material-controls').appendChild(fieldset);
    const fields={fieldset,color:$(prefix+'-color'),hex:$(prefix+'-hex'),range,output:$(prefix+'-value'),error,enabled:$(prefix+'-enabled')};controls.set(region.key,fields);
    fields.color.addEventListener('input',event=>{state.roles[region.key]=event.target.value;changed(region.key);});
    fields.hex.addEventListener('change',event=>{try{state.roles[region.key]=normalizeColor(event.target.value);changed(region.key);}catch(error){fields.hex.setAttribute('aria-invalid','true');fields.error.hidden=false;}});
    fields.hex.addEventListener('keydown',event=>{if(event.key==='Enter')fields.hex.blur();});
    fields.range.addEventListener('input',event=>{state.brightness[region.key]=Number(event.target.value)/100;changed(region.key);});
    if(fields.enabled)fields.enabled.addEventListener('change',event=>{state.enabled[region.key]=event.target.checked;changed(region.key);});
    updateControl(region.key);
  }
  function reset(kind='working'){
    if(!['working','original'].includes(kind))throw new Error('Reset must select working or original.');
    state=defaults();
    if(kind==='original'){state.preset='original';for(const region of regions)state.roles[region.key]=normalizeColor(region.referenceColor||region.originalColor);}
    for(const region of regions)updateControl(region.key);
    setOriginal(false);updateExport();persist();draw();return exportSettings();
  }
  function download(blob,filename){const url=URL.createObjectURL(blob);const anchor=document.createElement('a');anchor.href=url;anchor.download=filename;anchor.click();setTimeout(()=>URL.revokeObjectURL(url),1000);}
  $('compare').addEventListener('click',()=>setOriginal(!showOriginal));
  $('reset').addEventListener('click',()=>reset());
  $('original').addEventListener('click',()=>reset('original'));
  $('download-palette').addEventListener('click',()=>{download(new Blob([JSON.stringify(exportSettings(),null,2)],{type:'application/json'}),'console-materials.palette.json');$('save-status').textContent='Palette JSON downloaded.';});
  $('download-image').addEventListener('click',()=>{draw();canvas.toBlob(blob=>{if(!blob){$('error').hidden=false;$('error').textContent='The browser could not export the image.';return;}download(blob,'console-materials.png');$('save-status').textContent='Image PNG downloaded.';},'image/png');});
  updateExport();draw();$('loading').hidden=true;$('source-label').textContent=`${imageFile} · ${source.width} × ${source.height}`;
  $('save-status').textContent=initialStorageNotice;
  for(const id of ['compare','reset','original','download-image','download-palette'])$(id).disabled=false;
  Object.defineProperty(api,'state',{get:()=>clone({...state,showOriginal})});
  Object.assign(api,{export:exportSettings,reset,setRole,setOriginal,render:draw,renderReady:true,storageKey:STORAGE_KEY});
}catch(error){$('loading').hidden=true;$('error').hidden=false;$('error').textContent=error.message;console.error(error);api.error=error.message;}
})();
