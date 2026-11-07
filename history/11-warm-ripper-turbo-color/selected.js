function drawSelected(time,moving){
 if(moving){drawSelectedMotion(time);return;}
 board.width=1200;board.height=730;ctx.fillStyle='#080e18';ctx.fillRect(0,0,1200,730);
 text('TURBOLASER + RIPPER',28,37,13,'#7c9caf',700);text('Warmer ripper, tighter turbo fan',28,78,33,ink,700);text('Compact slug, short tracer, simpler impact.',28,108,17,muted);
 panel(28,138,558,530);panel(612,138,560,530);
 text('TURBOLASER',52,170,12,'#eb89ea',700);text('A tighter fan',52,205,24,ink,700);
 contact(92,338,100);impact('turbo',96,338,175,0);text('Enlarged',92,455,13,muted);line(354,245,354,466);contact(411,341,65);impact('turbo',415,341,48,0);text('48 px reference',388,424,13,muted);
 text('Same length, 25% tighter spread',52,530,22,ink,700);text('Keeps the faster fade you flew.',52,563,17,muted);text('Color pending the in-game check.',52,596,17,muted);
 text('RIPPER',636,170,12,cyan,700);text('Compact slug + short tracer',636,205,24,ink,700);
 slug('A',666,272,196);slug('A',1024,272,28);text('Enlarged',666,319,13,muted);text('28 px proposal',991,319,13,muted);line(636,349,1148,349);
 text('A smaller, simpler hit',636,391,24,ink,700);contact(671,492,86);ripperHit(675,492,132,0);contact(1024,494,64);ripperHit(1028,494,34,0);text('Enlarged',675,580,13,muted);text('34 px proposal',992,580,13,muted);text('180 ms proposal. A few broad shards and sparks.',636,629,16,muted);
 text('Concept preview. Reference sizes are proposed; the flight check comes next.',28,707,13,'#7d97a9');
}
function drawSelectedMotion(t){
 board.width=1050;board.height=510;ctx.fillStyle='#080e18';ctx.fillRect(0,0,1050,510);text('TURBOLASER + RIPPER',32,34,12,'#7c9caf',700);text('A short tracer into a tight hit',32,72,28,ink,700);text('Enlarged concept preview',32,100,15,muted);
 const phase=t%1500,hitTime=phase-550;
 ['turbo','ripper'].forEach((kind,i)=>{const top=124+i*169,y=top+82;panel(24,top,1002,153);text(i?'Ripper':'Turbolaser',45,top+29,18,ink,700);text(i?'Proposed 180 ms':'Same laser fade: clear by 280 ms',45,top+54,12,muted);contact(397,y,90);
 if(hitTime<0){const x=397+(-hitTime/550)*395;ctx.save();ctx.globalAlpha=clamp((phase-70)/80);if(i)slug('A',x,y,62);else{polygon([[x,y-2],[x+98,y-5],[x+114,y],[x+98,y+5],[x,y+2]],'#e66fe0');polygon([[x,y-1],[x+99,y-2],[x+106,y],[x+99,y+2],[x,y+1]],'#f7fcfa');}ctx.restore();}
 else if(i)ripperHit(401,y,100,hitTime);else impact(kind,401,y,148,hitTime);
 text(hitTime<0?'In flight':hitTime<340?`${Math.floor(hitTime)} ms after contact`:'Clear',803,top+110,13,muted);});text('Concept motion. Flight readability still needs an in-game check.',32,493,12,'#6e899e');
}
