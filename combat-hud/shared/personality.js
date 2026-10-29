(function(global){
'use strict';
const knobs = [
  ['glow','Beam bloom',0,2,.01,1.35,''],
  ['aberration','Chromatic aberration',0,12,.1,7,' px'],
  ['curvature','CRT curvature',0,1,.01,.65,''],
  ['beamJitter','Beam jitter',0,4,.1,2.2,' px'],
  ['afterglow','Phosphor afterglow',0,1.5,.01,.65,' s'],
  ['scanlines','Scanline strength',0,1,.01,.72,''],
  ['flicker','Appearance flicker',0,1,.01,.8,''],
  ['warmth','Phosphor warmth',0,1,.01,.6,''],
  ['wear','Beam wear',0,1,.01,.7,''],
  ['glassDistortion','Glass distortion',0,1,.01,.7,''],
  ['damageShudder','Damage shudder',0,18,.1,12,' px'],
  ['transitionGlitch','Transition glitch',0,1,.01,.75,'']
].map(([key,label,min,max,step,value,unit])=>({key,label,min,max,step,value,unit}));
const defaults=Object.fromEntries(knobs.map(k=>[k.key,k.value]));
const presets={
  exploration:{...defaults},
  combat:{glow:.45,aberration:1.3,curvature:.15,beamJitter:.35,afterglow:.12,scanlines:.22,flicker:.3,warmth:.3,wear:.18,glassDistortion:.15,damageShudder:3,transitionGlitch:.2},
  menus:{glow:.75,aberration:2,curvature:.35,beamJitter:.6,afterglow:.3,scanlines:.45,flicker:.4,warmth:.4,wear:.25,glassDistortion:.1,damageShudder:0,transitionGlitch:.45},
  bigMoments:{glow:1.1,aberration:4,curvature:.4,beamJitter:1.3,afterglow:.5,scanlines:.5,flicker:.6,warmth:.4,wear:.25,glassDistortion:.35,damageShudder:5,transitionGlitch:.7}
};
const vertex=`attribute vec2 position; varying vec2 uv; void main(){uv=position*.5+.5;gl_Position=vec4(position,0.,1.);}`;
const historyFragment=`precision mediump float;varying vec2 uv;uniform sampler2D currentImage,previousImage;uniform float decay;void main(){vec3 now=texture2D(currentImage,uv).rgb;vec3 old=texture2D(previousImage,uv).rgb*decay;gl_FragColor=vec4(max(now,old),1.);}`;
const finalFragment=`
precision highp float;
varying vec2 uv;
uniform sampler2D inkImage,glowImage,sceneImage;
uniform vec2 resolution,shudder;
uniform float time,glow,aberration,curvature,beamJitter,scanlines,flicker,warmth,wear,glassDistortion,transitionGlitch,appearAge,transitionAge,enabled;
float hash(vec2 p){return fract(sin(dot(p,vec2(127.1,311.7)))*43758.5453);}
vec3 ink(vec2 p){if(p.x<0.||p.x>1.||p.y<0.||p.y>1.)return vec3(0.);return texture2D(inkImage,p).rgb;}
vec3 bloom(vec2 p){if(p.x<0.||p.x>1.||p.y<0.||p.y>1.)return vec3(0.);return texture2D(glowImage,p).rgb;}
void main(){
  vec3 scene=texture2D(sceneImage,uv).rgb;
  vec2 p=uv;
  vec2 center=p-.5;
  float edge=smoothstep(.025,.45,dot(center,center));
  if(enabled>.5){
    p=.5+center*(1.+curvature*.16*dot(center,center));
    p.x+=glassDistortion*.006*sin(p.y*13.+p.x*4.)*edge;
    p.y+=glassDistortion*.003*sin(p.x*10.)*edge;
    float band=floor(p.y*resolution.y/3.);
    p.x+=(hash(vec2(band,floor(time*11.)))-.5)*beamJitter/resolution.x;
    p-=shudder/resolution;
    if(transitionAge>=0.&&transitionAge<.35){
      float tear=step(.75,hash(vec2(floor(p.y*18.),floor(transitionAge*9.))));
      p.x+=tear*sin(transitionAge*39.)*transitionGlitch*.025*(1.-transitionAge/.35);
    }
  }
  vec3 light=ink(p);
  if(enabled>.5){
    vec2 separation=normalize(center+vec2(.0001))*aberration*edge/resolution;
    vec3 split=vec3(ink(p+separation).r,light.g,ink(p-separation).b);
    light=mix(light,split,.88);
    float grain=hash(floor(p*resolution*.8));
    light*=1.-wear*.62*(1.-grain);
    light*=1.-scanlines*.48*(.5+.5*cos(p.y*resolution.y*3.14159265));
    float radius=1.5+glow*4.;
    vec2 d=radius/resolution;
    vec3 halo=bloom(p)*.16;
    halo+=(bloom(p+vec2(d.x,0.))+bloom(p-vec2(d.x,0.))+bloom(p+vec2(0.,d.y))+bloom(p-vec2(0.,d.y)))*.12;
    halo+=(bloom(p+d)+bloom(p-d)+bloom(p+vec2(d.x,-d.y))+bloom(p+vec2(-d.x,d.y)))*.09;
    light+=halo*glow*.65;
    float chroma=max(light.r,max(light.g,light.b))-min(light.r,min(light.g,light.b));
    light=mix(light,light*vec3(1.08,1.,.67),warmth*(1.-smoothstep(.06,.3,chroma)));
    if(appearAge>=0.&&appearAge<.8){
      float dip=(step(.09,appearAge)-step(.19,appearAge))+(step(.39,appearAge)-step(.49,appearAge));
      light*=1.-dip*flicker*.17;
    }
  }
  gl_FragColor=vec4(min(scene+max(light,vec3(0.)),vec3(1.)),1.);
}`;
function create(canvas){
  const gl=canvas.getContext('webgl',{alpha:false,antialias:false,preserveDrawingBuffer:true});
  if(!gl)throw new Error('WebGL is unavailable. This tuner requires a working WebGL renderer.');
  function program(fragment){
    const p=gl.createProgram();
    for(const [type,source] of [[gl.VERTEX_SHADER,vertex],[gl.FRAGMENT_SHADER,fragment]]){
      const shader=gl.createShader(type);gl.shaderSource(shader,source);gl.compileShader(shader);
      if(!gl.getShaderParameter(shader,gl.COMPILE_STATUS))throw new Error(gl.getShaderInfoLog(shader));
      gl.attachShader(p,shader);
    }
    gl.linkProgram(p);if(!gl.getProgramParameter(p,gl.LINK_STATUS))throw new Error(gl.getProgramInfoLog(p));
    return p;
  }
  const historyProgram=program(historyFragment),finalProgram=program(finalFragment);
  const buffer=gl.createBuffer();gl.bindBuffer(gl.ARRAY_BUFFER,buffer);gl.bufferData(gl.ARRAY_BUFFER,new Float32Array([-1,-1,1,-1,-1,1,1,1]),gl.STATIC_DRAW);
  function texture(){const t=gl.createTexture();gl.bindTexture(gl.TEXTURE_2D,t);gl.texParameteri(gl.TEXTURE_2D,gl.TEXTURE_MIN_FILTER,gl.LINEAR);gl.texParameteri(gl.TEXTURE_2D,gl.TEXTURE_MAG_FILTER,gl.LINEAR);gl.texParameteri(gl.TEXTURE_2D,gl.TEXTURE_WRAP_S,gl.CLAMP_TO_EDGE);gl.texParameteri(gl.TEXTURE_2D,gl.TEXTURE_WRAP_T,gl.CLAMP_TO_EDGE);return t;}
  const source=texture(),mask=texture(),background=texture();
  const history=[texture(),texture()],frames=[gl.createFramebuffer(),gl.createFramebuffer()];
  let width=0,height=0,index=0;
  function select(p){gl.useProgram(p);gl.bindBuffer(gl.ARRAY_BUFFER,buffer);const a=gl.getAttribLocation(p,'position');gl.enableVertexAttribArray(a);gl.vertexAttribPointer(a,2,gl.FLOAT,false,0,0);}
  function bind(p,name,unit,t){gl.activeTexture(gl.TEXTURE0+unit);gl.bindTexture(gl.TEXTURE_2D,t);gl.uniform1i(gl.getUniformLocation(p,name),unit);}
  function scalar(p,name,value){gl.uniform1f(gl.getUniformLocation(p,name),value);}
  function upload(t,image){gl.bindTexture(gl.TEXTURE_2D,t);gl.pixelStorei(gl.UNPACK_FLIP_Y_WEBGL,true);gl.pixelStorei(gl.UNPACK_PREMULTIPLY_ALPHA_WEBGL,true);gl.texImage2D(gl.TEXTURE_2D,0,gl.RGBA,gl.RGBA,gl.UNSIGNED_BYTE,image);}
  function clear(){for(const f of frames){gl.bindFramebuffer(gl.FRAMEBUFFER,f);gl.clearColor(0,0,0,1);gl.clear(gl.COLOR_BUFFER_BIT);}gl.bindFramebuffer(gl.FRAMEBUFFER,null);}
  function setBackground(image){
    width=Math.min(image.naturalWidth,1600);height=Math.round(width*image.naturalHeight/image.naturalWidth);
    canvas.width=width;canvas.height=height;gl.viewport(0,0,width,height);
    upload(background,image);
    for(let i=0;i<2;i++){gl.bindTexture(gl.TEXTURE_2D,history[i]);gl.texImage2D(gl.TEXTURE_2D,0,gl.RGBA,width,height,0,gl.RGBA,gl.UNSIGNED_BYTE,null);gl.bindFramebuffer(gl.FRAMEBUFFER,frames[i]);gl.framebufferTexture2D(gl.FRAMEBUFFER,gl.COLOR_ATTACHMENT0,gl.TEXTURE_2D,history[i],0);if(gl.checkFramebufferStatus(gl.FRAMEBUFFER)!==gl.FRAMEBUFFER_COMPLETE)throw new Error('CRT history framebuffer is incomplete.');}
    clear();return {width,height};
  }
  function render({hud,glowMask,settings,time,delta,enabled=true,appearAge=-1,transitionAge=-1,hitAge=-1}){
    gl.activeTexture(gl.TEXTURE0);upload(source,hud);upload(mask,glowMask);
    select(historyProgram);const next=1-index;gl.bindFramebuffer(gl.FRAMEBUFFER,frames[next]);
    bind(historyProgram,'currentImage',0,source);bind(historyProgram,'previousImage',1,history[index]);
    scalar(historyProgram,'decay',enabled&&settings.afterglow>0?Math.exp(-delta/Math.max(.001,settings.afterglow)):0);
    gl.drawArrays(gl.TRIANGLE_STRIP,0,4);index=next;
    gl.bindFramebuffer(gl.FRAMEBUFFER,null);select(finalProgram);
    bind(finalProgram,'inkImage',0,history[index]);bind(finalProgram,'glowImage',1,mask);bind(finalProgram,'sceneImage',2,background);
    for(const key of ['glow','aberration','curvature','beamJitter','scanlines','flicker','warmth','wear','glassDistortion','transitionGlitch'])scalar(finalProgram,key,settings[key]);
    scalar(finalProgram,'time',time);scalar(finalProgram,'enabled',enabled?1:0);scalar(finalProgram,'appearAge',appearAge);scalar(finalProgram,'transitionAge',transitionAge);
    gl.uniform2f(gl.getUniformLocation(finalProgram,'resolution'),width,height);
    const kick=enabled&&hitAge>=0&&hitAge<.7?settings.damageShudder*Math.exp(-hitAge*7):0;
    gl.uniform2f(gl.getUniformLocation(finalProgram,'shudder'),Math.sin(hitAge*71)*kick,Math.sin(hitAge*53)*kick*.65);
    gl.drawArrays(gl.TRIANGLE_STRIP,0,4);
  }
  return {setBackground,render,clear,gl};
}
global.CanopyCRT={schemaVersion:1,knobs,defaults,presets,create};
})(window);
