const RipperDrawing = (() => {
  function polygon(ctx, points, color) {
    ctx.fillStyle=color;ctx.beginPath();
    points.forEach(([x,y],i)=>i?ctx.lineTo(x,y):ctx.moveTo(x,y));ctx.closePath();ctx.fill();
  }
  function slug(ctx, colors) {
    polygon(ctx,[[0,-1.5],[3,-4.3],[15,-3.4],[19,-1.4],[19,2.4],[14,4.1],[3,3.4],[0,1]],colors.shadow);
    polygon(ctx,[[0,-1.5],[3,-4.3],[15,-3.4],[19,-1.4],[4,-1],[1,1.2]],colors.top);
    polygon(ctx,[[1,1.2],[4,-1],[18,-1.4],[16,1.3],[4,2.9]],colors.side);
    polygon(ctx,[[0,-1.5],[3,-4.3],[4,-2],[2,1.5],[0,1]],colors.nose);
    polygon(ctx,[[19,-.7],[28,0],[19,1]],colors.tail);
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
