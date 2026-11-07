(function(){
'use strict';
const data=window.SCREEN_STUDY_DATA;
const concepts=[
  {id:'A',name:'Flight card',file:'a-flight-card.png',description:'A compact identity row and evenly spaced groups make the whole loadout easy to scan.'},
  {id:'B',name:'Hull diagram',file:'b-hull-diagram.png',description:'The ship silhouette anchors the screen, trading a single reading order for spatial callouts.'},
  {id:'C',name:'Ship dossier',file:'c-ship-dossier.png',description:'A generous name and description lead into a calm ledger with aligned values and bars.'},
  {id:'D',name:'Component stack',file:'d-component-stack.png',description:'Distinct fitted-system blocks pair with a persistent icon rail, making each group feel self-contained.'},
  {id:'E',name:'Open instruments',file:'e-open-instruments.png',description:'Loose clusters and a prominent keystone glyph create the most open, HUD-like reading.'},
  {id:'F',name:'Vertical instruments',file:'f-vertical-instruments.png',description:'Six upright segmented tracks show the ship as grouped instruments, with values read below each track.'}
];
const stats=Object.fromEntries(data.stats.map(stat=>[stat.key,stat]));
const labels={hull:'Hull',shieldCapacity:'Capacity',shieldRecharge:'Recharge',topSpeed:'Top speed',acceleration:'Acceleration',turn:'Turn'};
const esc=text=>String(text).replace(/[&<>"']/g,value=>({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[value]));
const asset=file=>'assets/'+file;
const icon=(file,className,alt)=>`<img class="${className}" src="${asset(file)}" alt="${esc(alt)}">`;
const shipIcon=className=>icon(data.shipIcon,'ship-icon '+(className||''),data.ship+' silhouette');
const dashIcon=className=>icon(data.keystone.icon,'keystone-icon '+(className||''),data.keystone.name+' icon');
const description=`<span class="placeholder">${esc(data.description||'[Ship description placeholder]')}</span>`;
const number=value=>Number.isInteger(value)?String(value):String(Math.round(value*100)/100);
const unit=value=>value==='deg/s'?'°/s':value;
const weaponReadouts=text=>esc(text).replace(/\d+(?:\.\d+)?/g,value=>`<span class="weapon-number">${value}</span>`);
function value(key){const stat=stats[key];return `<span class="readout" data-value="${key}">${number(stat.value)}${stat.unit?`<small>${esc(unit(stat.unit))}</small>`:''}</span>`;}
function bar(key,vertical=false){
  const stat=stats[key],fraction=Math.max(.05,stat.value/stat.barMaximum),count=20;
  const segments=Array.from({length:count},(_,index)=>{const fill=Math.max(0,Math.min(1,fraction*count-index))*100;return `<i style="--fill:${fill}%"></i>`;}).join('');
  return `<div class="bar${vertical?' vertical':''}" data-stat="${key}" role="img" aria-label="${esc(stat.label)} ${number(stat.value)} ${esc(unit(stat.unit))}; relative to fleet maximum ${stat.barMaximum}">${segments}</div>`;
}
function stat(key,className=''){return `<div class="stat ${className}"><div class="stat-line"><span class="stat-label">${labels[key]}</span>${value(key)}</div>${bar(key)}</div>`;}
function group(title,type,keys,className=''){return `<section class="stat-group ${className}"><div class="group-heading"><h2>${esc(title)}</h2><span class="component-kind">${type}</span></div>${keys.map(key=>stat(key)).join('')}</section>`;}
function ability(className=''){return `<div class="ability ${className}">${dashIcon()}<div class="ability-copy"><span class="component-kind">Keystone</span><h2>${esc(data.keystone.name)}</h2><div class="cooldown"><span class="readout">${number(data.keystone.cooldownSeconds)}<small>s</small></span><span class="cooldown-label">cooldown</span></div></div></div>`;}
function identity(className=''){return `<div class="identity ${className}">${shipIcon()}<div><h1 class="ship-name">${esc(data.ship)}</h1><p class="ship-description">${description}</p></div></div>`;}
const footer='<div class="scale-note">Bars relative to the fleet</div>';
function weapons(){return `<section class="weapon-strip" aria-label="Companion hardpoint display">${data.weapons.map(weapon=>`<article class="weapon"><header>${icon(weapon.icon,'weapon-icon',weapon.name+' glyph')}<div><span class="component-kind">${esc(weapon.slot)}</span><h2>${esc(weapon.name)}</h2></div></header><p class="weapon-stats">${weaponReadouts(weapon.statLine)}</p><p class="weapon-detail">${weaponReadouts(weapon.detailLine)}</p><p class="weapon-description placeholder">[Weapon description placeholder]</p></article>`).join('')}</section>`;}
const layouts={
  A:()=>`<div class="a-header"><div class="identity">${shipIcon()}<h1 class="ship-name">${esc(data.ship)}</h1></div>${ability('compact')}</div><p class="a-description">${description}</p><div class="a-specs">${group('Hull','', ['hull'],'hull-group')}${group(data.shield,'Shield',['shieldCapacity','shieldRecharge'])}${group(data.engine,'Engine',['topSpeed','acceleration','turn'])}</div>${footer}`,
  B:()=>`<div class="b-header"><h1 class="ship-name">${esc(data.ship)}</h1><p class="ship-description">${description}</p></div><svg class="b-leaders" viewBox="0 0 850 1000" aria-hidden="true"><path d="M 258 336 H 299 L 341 375 M 575 361 H 553 L 516 402 M 425 643 V 685"/></svg>${shipIcon('b-ship')}<section class="b-hull">${group('Hull','',['hull'])}</section><section class="b-shield">${group(data.shield,'Shield',['shieldCapacity','shieldRecharge'])}</section>${ability('b-ability')}<section class="b-engine"><div class="group-heading"><h2>${esc(data.engine)}</h2><span class="component-kind">Engine</span></div><div class="b-engine-stats">${['topSpeed','acceleration','turn'].map(key=>stat(key)).join('')}</div></section>${footer}`,
  C:()=>`<div class="c-header"><div><h1 class="ship-name">${esc(data.ship)}</h1><p class="ship-description">${description}</p></div>${shipIcon()}</div><div class="c-ledger"><div class="c-ledger-row">${stat('hull')}</div><div class="group-heading"><h2>${esc(data.shield)}</h2><span class="component-kind">Shield</span></div>${['shieldCapacity','shieldRecharge'].map(key=>`<div class="c-ledger-row">${stat(key)}</div>`).join('')}<div class="group-heading"><h2>${esc(data.engine)}</h2><span class="component-kind">Engine</span></div>${['topSpeed','acceleration','turn'].map(key=>`<div class="c-ledger-row">${stat(key)}</div>`).join('')}</div>${ability('c-ability')}${footer}`,
  D:()=>`<div class="d-header"><h1 class="ship-name">${esc(data.ship)}</h1><p class="ship-description">${description}</p></div><div class="d-components">${group('Hull','',['hull'],'d-block')}${group(data.shield,'Shield',['shieldCapacity','shieldRecharge'],'d-block')}${group(data.engine,'Engine',['topSpeed','acceleration','turn'],'d-block')}</div><aside class="d-rail">${shipIcon()}${ability('d-ability')}</aside>${footer}`,
  E:()=>`<div class="e-identity"><h1 class="ship-name">${esc(data.ship)}</h1><p class="ship-description">${description}</p></div>${shipIcon('e-ship')}<section class="e-hull">${group('Hull','',['hull'])}</section><section class="e-shield">${group(data.shield,'Shield',['shieldCapacity','shieldRecharge'])}</section>${ability('e-ability')}<section class="e-engine">${group(data.engine,'Engine',['topSpeed','acceleration','turn'])}</section>${footer}`,
  F:()=>`${identity('f-identity')}${ability('f-ability')}<div class="f-group-headings"><h2>Hull</h2><h2>${esc(data.shield)}</h2><h2>${esc(data.engine)}</h2></div><div class="f-instruments">${data.stats.map(stat=>`<div class="vertical-instrument">${bar(stat.key,true)}${value(stat.key)}<span class="stat-label">${labels[stat.key]}</span></div>`).join('')}</div>${footer}`
};
function render(id){if(!layouts[id])throw new Error('Unknown screen direction.');return `<div class="study" data-concept="${id}"><section class="main-screen screen-${id.toLowerCase()}" aria-label="${esc(concepts.find(c=>c.id===id).name)} main specification display">${layouts[id]()}</section>${weapons()}</div>`;}
window.ScreenStudies={concepts,data,render,ready:false};
const params=new URLSearchParams(location.search),id=params.get('render')||params.get('concept')||'A';
const concept=concepts.find(item=>item.id===id);
if(!concept)throw new Error('Unknown concept '+id);
document.getElementById('study-title').textContent=`${id} · ${concept.name}`;
document.getElementById('study-description').textContent=concept.description;
document.getElementById('study-root').innerHTML=render(id);
if(params.has('render'))document.body.classList.add('render-only');
Promise.all([document.fonts.load('500 28px "HUD Label"'),document.fonts.load('500 28px "HUD Readout"'),document.fonts.load('400 34px "HUD Heading"'),...Array.from(document.images,image=>image.decode())]).then(results=>{if(results.slice(0,3).some(fonts=>fonts.length===0))throw new Error('A current HUD font did not load.');window.ScreenStudies.ready=true;}).catch(error=>{window.ScreenStudies.error=error.message;console.error(error);});
})();
