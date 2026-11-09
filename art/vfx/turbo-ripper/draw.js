const RipperDrawing = (() => {
  function polygon(ctx, points, color) {
    ctx.fillStyle=color;ctx.beginPath();
    points.forEach(([x,y],i)=>i?ctx.lineTo(x,y):ctx.moveTo(x,y));ctx.closePath();ctx.fill();
  }
  function slug(ctx, colors, shadowBlur, variant) {
    ctx.save();ctx.shadowColor=colors.tail;ctx.shadowBlur=shadowBlur;
    ctx.globalAlpha=variant.brightness;
    polygon(ctx,[[0,0],[-3.2,-.9],[-5.1,-.55],[-4.1,.55],[-2.4,.7]],colors.body);
    polygon(ctx,[[-.1,0],[-3.6,-.5],[-2.6,.25]],colors.core);
    ctx.globalAlpha=.85*variant.brightness;
    polygon(ctx,[[-4.4,-.4],[-22,-.08],[-13.2,.35],[-5.5,.22]],colors.tail);
    ctx.globalAlpha=.8*variant.brightness;
    polygon(ctx,[[-7.1,-.2],[-18,-.1],[-9.2,.05]],colors.core);
    ctx.globalAlpha=.7*variant.brightness;
    polygon(ctx,variant.fleck,colors.rim);ctx.restore();
  }
  function impact(ctx, time, duration, colors) {
    const u=time/duration;if(u<0||u>=1)return;
    ctx.save();ctx.globalAlpha=Math.max(0,1-u*1.15);
    const spread=1+Math.sin(u*Math.PI)*.12;ctx.scale(spread,spread);
    if(u<.62){
      polygon(ctx,[[0,-2],[3,-5],[11,-8],[17,-14],[14,-6],[24,-8],[19,-2],[31,0],[18,3],[23,11],[12,6],[8,9],[3,4],[0,2]],colors.rim);
      polygon(ctx,[[1,-1],[4,-3],[11,-5],[9,-1],[20,0],[10,2],[13,6],[5,3],[1,1]],colors.inner);
      polygon(ctx,[[0,-1],[2,-3],[5,-2],[6,1],[3,3],[0,1]],colors.core);
    }
    const d=4+u*15;ctx.globalAlpha=Math.max(0,1-u);
    polygon(ctx,[[22+d,-12],[26+d,-16],[24+d,-11]],colors.sparkLight);
    polygon(ctx,[[27+d,6],[32+d,8],[27+d,8]],colors.sparkGold);
    polygon(ctx,[[13+d,14],[16+d,17],[13+d,16]],colors.sparkLight);ctx.restore();
  }
  return {slug,impact};
})();
