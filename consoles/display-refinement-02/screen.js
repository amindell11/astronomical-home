(function(){
'use strict';
const data=window.SCREEN_STUDY_DATA;
const base='../display-concepts-01/assets/';
const labels={hull:'Hull',shieldCapacity:'Shield',shieldRecharge:'Regen',topSpeed:'Speed',acceleration:'Accel',turn:'Turn'};
const stats=Object.fromEntries(data.stats.map(stat=>[stat.key,stat]));
const escape=value=>String(value).replace(/[&<>"']/g,char=>({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[char]));
function metric(key){
  const stat=stats[key],fraction=Math.max(.05,stat.value/stat.barMaximum);
  const unit=stat.unit==='deg/s'?'°/s':stat.unit;
  const segments=Array.from({length:7},(_,index)=>`<i style="--fill:${Math.max(0,Math.min(1,fraction*7-index))*100}%"></i>`).join('');
  return `<div class="metric" data-stat="${key}"><span class="metric-label">${labels[key]}</span><span class="metric-value">${stat.value}${unit?`<small>${escape(unit)}</small>`:''}</span><div class="bar" aria-hidden="true">${segments}</div></div>`;
}
document.getElementById('screen').innerHTML=`<h1 class="ship-title">${escape(data.ship)}</h1><img class="ship-icon" src="${base+data.shipIcon}" alt="${escape(data.ship)} silhouette"><div class="metrics left">${['hull','shieldCapacity','shieldRecharge'].map(metric).join('')}</div><div class="metrics right">${['topSpeed','acceleration','turn'].map(metric).join('')}</div><div class="keystone"><img src="${base+data.keystone.icon}" alt="${escape(data.keystone.name)}"><span class="keystone-value">${data.keystone.cooldownSeconds}<small>s</small></span></div>${data.weapons.map((weapon,index)=>`<div class="weapon ${index?'secondary':'primary'}"><img src="${base+weapon.icon}" alt="${escape(weapon.name)} glyph"><span class="weapon-name">${escape(weapon.name)}</span></div>`).join('')}`;
window.CompactHullStudy={ready:false,data,screenBounds:{x:151,y:497,width:286,height:314},maskPolygon:[[162,497],[414,497],[422,505],[435,803],[429,811],[159,811],[151,803],[151,507]]};
Promise.all([document.fonts.load('500 16px "HUD Label"'),document.fonts.load('500 16px "HUD Readout"'),document.fonts.load('400 17px "HUD Heading"'),...Array.from(document.images,image=>image.decode())]).then(result=>{if(result.slice(0,3).some(fonts=>fonts.length===0))throw new Error('A current HUD font failed to load.');window.CompactHullStudy.ready=true;}).catch(error=>{window.CompactHullStudy.error=error.message;console.error(error);});
})();
