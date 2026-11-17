const DATA = /*DATA*/null;

// Knob schema: ["#", heading] starts a sub-group.
const LOOK_KNOBS = [
  ["#", "Look"],
  ["outline", "Outline (0 none, 1 leading tip, 2 full)", 0, 2, 1],
  ["gradient", "Fade toward tail", 0, 1, 0.01],
  ["additive", "Light blend (0 panes, 1 additive)", 0, 1, 1],
  ["fill", "Fill opacity", 0, 1, 0.01],
  ["edge", "Outline opacity", 0, 1, 0.01],
  ["edgePx", "Outline width (screen px)", 0, 4, 0.1],
  ["tint", "Tint strength", 0, 1, 0.01],
  ["flash", "Birth flash", 0, 1, 0.01],
];
const HAND_KNOBS = [
  ["#", "Hand-drawn"],
  ["ink", "Ink contour opacity", 0, 1, 0.01],
  ["inkPx", "Ink width at the head (screen px)", 0, 5, 0.1],
  ["boil", "Line boil (ship lengths)", 0, 0.08, 0.002],
  ["boilFps", "Boil redraws per second", 2, 24, 1],
  ["stepFps", "Animate on (fps, 0 smooth)", 0, 24, 1],
  ["hatch", "Hatching opacity", 0, 1, 0.01],
  ["hatchGap", "Hatch spacing (screen px)", 2, 12, 0.5],
  ["hatchAngle", "Hatch angle (deg)", 0, 90, 1],
];
const UNIQUE_KNOBS = {
  dash: [
    ["#", "Action lines"],
    ["actionLines", "Action lines at launch", 0, 40, 1],
    ["actionLen", "Action line length (ship lengths)", 0.2, 3, 0.05],
    ["actionLife", "Action lines hold (s)", 0.04, 0.5, 0.01],
  ],
  ram: [
    ["#", "Action lines"],
    ["actionLines", "Action lines at launch", 0, 40, 1],
    ["actionLen", "Action line length (ship lengths)", 0.2, 3, 0.05],
    ["actionLife", "Action lines hold (s)", 0.04, 0.5, 0.01],
    ["#", "Ram pressure"],
    ["pressureArcs", "Pressure arcs round the nose", 0, 6, 1],
    ["arcSize", "Arc size (×)", 0.4, 2, 0.01],
    ["impactStar", "Impact star at launch (size)", 0, 2, 0.01],
  ],
  overdrive: [
    ["#", "Overdrive aura"],
    ["aura", "Sketch aura opacity", 0, 1, 0.01],
    ["auraOffset", "Aura distance from hull (ship lengths)", 0, 0.3, 0.005],
    ["auraBreaks", "Aura gaps", 0, 0.8, 0.01],
    ["twinkleRate", "Wingtip twinkles per second", 0, 30, 0.5],
    ["twinkleSize", "Twinkle size (ship lengths)", 0.02, 0.3, 0.005],
  ],
  blink: [
    ["#", "Poof"],
    ["poofLines", "Poof lines at each end", 0, 24, 1],
    ["poofSize", "Poof size (ship lengths)", 0.2, 2, 0.01],
    ["twinkles", "Twinkles at each end", 0, 20, 1],
    ["twinkleSize", "Twinkle size (ship lengths)", 0.02, 0.3, 0.005],
  ],
};
const VARIATION_KNOBS = [
  ["#", "Variation"],
  ["sizeVar", "Size variation", 0, 1, 0.01],
  ["lenVar", "Length variation", 0, 1, 0.01],
  ["colorVar", "Brightness variation", 0, 1, 0.01],
  ["hotCore", "Newest streaks burn whiter", 0, 1, 0.01],
  ["lines", "Speed lines per streak", 0, 3, 0.05],
  ["lineLen", "Speed line length (ship lengths)", 0.2, 3, 0.05],
  ["motes", "Motes per streak", 0, 4, 0.05],
  ["moteSize", "Mote size (ship lengths)", 0.01, 0.12, 0.005],
];
const SHED_KNOBS = [
  ["#", "Shed off the hull"],
  ["rate", "Rate over distance (per ship length)", 0.5, 14, 0.1],
  ["rearY", "Hull region: emit behind this line (−0.5 tail, 0.5 nose)", -0.5, 0.5, 0.01],
  ["lifetime", "Start lifetime (s)", 0.1, 1.5, 0.01],
  ["size", "Start size (× drawn)", 0.2, 1.8, 0.01],
  ["sizeEnd", "Size over lifetime, end (×)", 0, 1.5, 0.01],
  ["inherit", "Inherit ship velocity", 0, 1, 0.01],
  ["drag", "Drag (falls behind faster)", 0, 10, 0.1],
  ["kick", "Outward push off the hull (ship lengths / s)", 0, 4, 0.05],
  ["align", "Point along their slide (0 ship heading, 1 slide)", 0, 1, 0.01],
  ["speedStretch", "Stretch with speed", 0, 1.5, 0.01],
  ["dashBurst", "Push-off burst at launch (count)", 0, 30, 1],
];
const FRONT_KNOBS = [
  ["#", "Ram front"],
  ["frontRate", "Front rate over distance", 0, 14, 0.1],
  ["frontY", "Front region: emit ahead of this line", -0.5, 0.5, 0.01],
  ["frontSize", "Front streak size (×)", 0.2, 2, 0.01],
  ["frontLife", "Front lifetime (s)", 0.1, 1.5, 0.01],
  ["frontInherit", "Front inherit ship velocity", 0, 1.2, 0.01],
  ["frontKick", "Front shove sideways (ship lengths / s)", 0, 6, 0.05],
];
const TAIL_KNOBS = [
  ["#", "Snap in"],
  ["snapTime", "Snap-in time (s)", 0.01, 0.3, 0.005],
  ["overshoot", "Overshoot", 0, 4, 0.05],
  ["stretch", "Birth stretch (× length)", 1, 3, 0.05],
  ["#", "Ending"],
  ["collapse", "Thin to a sliver (last fraction of life)", 0, 1, 0.01],
  ["sparks", "Sparks when a streak dies", 0, 6, 1],
  ["pieces", "Shard fragments when a streak dies", 0, 4, 1],
  ["shatterSpeed", "Spark / shard speed (ship lengths / s)", 0, 4, 0.05],
  ["spin", "Shard spin (deg / s)", 0, 720, 5],
  ["shatterLife", "Spark / shard lifetime (s)", 0.05, 1, 0.01],
  ["endBreak", "At window end, end streaks younger than (s)", 0, 1, 0.01],
  ["#", "Launch beat"],
  ["chevrons", "Launch chevrons", 0, 5, 1],
  ["chevronSize", "Chevron size (× ship width)", 0.2, 2, 0.01],
  ["chevronSpeed", "Chevron kick (ship lengths / s)", 0, 6, 0.05],
  ...LOOK_KNOBS,
  ["#", "Ship"],
  ["boost", "Speed boost during ability (×)", 1, 5, 0.05],
];
const BLINK_KNOBS = [
  ["#", "Wind-up"],
  ["range", "Blink range (ship lengths)", 1, 10, 0.1],
  ["doorScale", "Door size (×)", 0.4, 2, 0.01],
  ["anticip", "Doors swing open first (×)", 0, 1, 0.01],
  ["squeeze", "Door width at pinch (×)", 0.05, 1, 0.01],
  ["pullLines", "Lines pulled in", 0, 24, 1],
  ["pullRadius", "Pull-in radius (ship lengths)", 0.4, 3, 0.05],
  ["markAlpha", "Landing mark opacity", 0, 1, 0.01],
  ["markPulse", "Landing mark pulse", 0, 0.5, 0.01],
  ["#", "Departure"],
  ["pinchFade", "Sliver hold (s)", 0.02, 0.6, 0.01],
  ["flare", "Sliver flare (× height)", 1, 3, 0.05],
  ["pinchShards", "Sliver sparks", 0, 12, 1],
  ["departMotes", "Motes left behind", 0, 30, 1],
  ["#", "Arrival"],
  ["openTime", "Doors open (s)", 0.05, 1.2, 0.01],
  ["overshoot", "Open overshoot", 0, 4, 0.05],
  ["swing", "Swing-out (× offset)", 1, 3, 0.01],
  ["swingDeg", "Door tilt (deg)", 0, 40, 0.5],
  ["burst", "Streaks burst out", 0, 24, 1],
  ["burstSpeed", "Burst speed (ship lengths / s)", 0, 8, 0.05],
  ["burstSize", "Burst streak size (×)", 0.2, 1.5, 0.01],
  ["arriveMotes", "Arrival motes", 0, 30, 1],
  ["hullFlash", "Hull flash", 0, 1, 0.01],
  ["sparks", "Sparks per door at the end", 0, 8, 1],
  ["pieces", "Doors break into shards", 0, 4, 1],
  ["shatterSpeed", "Spark / shard speed (ship lengths / s)", 0, 4, 0.05],
  ["spin", "Shard spin (deg / s)", 0, 720, 5],
  ["shatterLife", "Spark / shard lifetime (s)", 0.05, 1, 0.01],
  ["moteSize", "Mote size (ship lengths)", 0.01, 0.12, 0.005],
  ...LOOK_KNOBS,
];
const WORLD_KNOBS = [
  ["shipPx", "Ship height at game size (px)", 40, 140, 1],
  ["cruise", "Cruise speed (ship lengths / s)", 0.5, 8, 0.1],
  ["weave", "Weave (turn amount)", 0, 1.5, 0.01],
  ["autoEvery", "Auto-fire every (s)", 1, 8, 0.1],
];

const LOOK_BASE = { outline: 1, gradient: 0.85, additive: 1, fill: 0.36, edge: 0.9, edgePx: 1.5, tint: 0.9, flash: 0.7,
  ink: 0.85, inkPx: 2, boil: 0.02, boilFps: 12, stepFps: 12, hatch: 0.35, hatchGap: 4, hatchAngle: 35 };
const UNIQUE_BASE = { actionLines: 14, actionLen: 1.2, actionLife: 0.16, pressureArcs: 0, arcSize: 1, impactStar: 0,
  aura: 0, auraOffset: 0.06, auraBreaks: 0.3, twinkleRate: 0, twinkleSize: 0.09 };
// Vanguard and Bulwark share this body; Ram only adds the front.
const TRAIL_BASE = {
  rate: 7, rearY: -0.05, lifetime: 0.5, size: 0.8, sizeEnd: 0.8,
  inherit: 0.75, drag: 3.5, kick: 0.9, align: 0.8, speedStretch: 0.45, dashBurst: 10,
  sizeVar: 0.4, lenVar: 0.5, colorVar: 0.45, hotCore: 0.35, lines: 0.6, lineLen: 0.9, motes: 0.7, moteSize: 0.035,
  snapTime: 0.07, overshoot: 1.6, stretch: 1.6,
  collapse: 0.45, sparks: 1, pieces: 0, shatterSpeed: 1.2, spin: 260, shatterLife: 0.3, endBreak: 0.2,
  chevrons: 3, chevronSize: 0.9, chevronSpeed: 2.5,
  frontRate: 0, frontY: 0.3, frontSize: 1, frontLife: 0.45, frontInherit: 0.95, frontKick: 2.4,
  ...LOOK_BASE, ...UNIQUE_BASE, boost: 3 };

// Ability timings come from the game; they are not knobs.
const MODES = {
  vanguard_dash: { label: "Vanguard dash", ship: "vanguard", effect: "dash", window: 0.4, windowName: "Guard", tintRGB: [90, 210, 255],
    d: { ...TRAIL_BASE } },
  bulwark_ram: { label: "Bulwark ram", ship: "bulwark", effect: "ram", window: 0.6, windowName: "Ram live", tintRGB: [255, 170, 50],
    d: { ...TRAIL_BASE, tint: 1, boost: 2.5, frontRate: 6, pressureArcs: 3, impactStar: 0.7 } },
  valis_overdrive: { label: "Valis overdrive", ship: "valis", effect: "overdrive", window: 3, windowName: "Overdrive", tintRGB: [255, 120, 205],
    d: { ...TRAIL_BASE, rate: 5.5, size: 0.75, lifetime: 0.65, kick: 0.3, inherit: 0.6, drag: 2.5, dashBurst: 4, chevrons: 0,
      tint: 0.95, boost: 1.35, endBreak: 1, lines: 0.9, motes: 0.5, actionLines: 6, aura: 0.9, twinkleRate: 7 } },
  crimson_blink: { label: "Crimson blink", ship: "crimson", effect: "blink", window: 0.25, windowName: "Wind-up", tintRGB: [255, 60, 80],
    d: { range: 4, doorScale: 1, anticip: 0.25, squeeze: 0.2, pullLines: 12, pullRadius: 1.3, markAlpha: 0.55, markPulse: 0.18,
      pinchFade: 0.12, flare: 1.8, pinchShards: 6, departMotes: 12,
      openTime: 0.35, overshoot: 2.2, swing: 1.5, swingDeg: 12, burst: 10, burstSpeed: 3.2, burstSize: 0.6, arriveMotes: 14, hullFlash: 0.7,
      sparks: 3, pieces: 0, shatterSpeed: 1.4, spin: 300, shatterLife: 0.35, moteSize: 0.04,
      snapTime: 0.06, overshoot2: 1.6, collapse: 0.5, poofLines: 10, poofSize: 0.8, twinkles: 6, twinkleSize: 0.1,
      ...LOOK_BASE, tint: 1, outline: 2, gradient: 0.5 } },
};
/*PICKS*/
const WORLD_DEFAULT = { shipPx: 70, cruise: 2.5, weave: 0.7, autoEvery: 2.6,
  refract: 0, refractPx: 8, localColor: 0, coolAge: 0.7, bands: 0, shipInk: 0, rimLight: 0 };

const STORE_KEY = "keystone-trails-v6";
let saved = {};
try { saved = JSON.parse(localStorage.getItem(STORE_KEY) || "{}"); } catch (e) { saved = {}; }
const S = {};
for (const k in MODES) S[k] = Object.assign({}, MODES[k].d, (saved.modes || {})[k] || {});
const W = Object.assign({}, WORLD_DEFAULT, saved.world || {});
function persist() { try { localStorage.setItem(STORE_KEY, JSON.stringify({ modes: S, world: W })); } catch (e) {} }

const ui = { mode: "vanguard_dash", speed: 1, zoom: 1, paused: matchMedia("(prefers-reduced-motion: reduce)").matches, auto: true };

// ---------- geometry ----------
function centroid(pts) { let x = 0, y = 0; for (const p of pts) { x += p[0]; y += p[1]; } return [x / pts.length, y / pts.length]; }
function clipHalf(pts, nx, ny, d) {
  const out = [];
  for (let i = 0; i < pts.length; i++) {
    const a = pts[i], b = pts[(i + 1) % pts.length];
    const da = a[0] * nx + a[1] * ny - d, db = b[0] * nx + b[1] * ny - d;
    if (da >= 0) out.push(a);
    if ((da >= 0) !== (db >= 0)) { const t = da / (da - db); out.push([a[0] + (b[0] - a[0]) * t, a[1] + (b[1] - a[1]) * t]); }
  }
  return out;
}
function area(pts) { let s = 0; for (let i = 0; i < pts.length; i++) { const a = pts[i], b = pts[(i + 1) % pts.length]; s += a[0] * b[1] - b[0] * a[1]; } return Math.abs(s / 2); }
function fracture(pts, n) {
  let pieces = [pts];
  const [cx, cy] = centroid(pts);
  let ext = 0; for (const p of pts) ext = Math.max(ext, Math.hypot(p[0] - cx, p[1] - cy));
  for (let c = 0; c < n - 1; c++) {
    const ang = Math.random() * Math.PI * 0.6 - Math.PI * 0.3;
    const nx = Math.sin(ang), ny = Math.cos(ang);
    const d = cx * nx + cy * ny + (Math.random() - 0.5) * ext * 0.8;
    const next = [];
    for (const pc of pieces) for (const q of [clipHalf(pc, nx, ny, d), clipHalf(pc, -nx, -ny, -d)]) if (q.length >= 3 && area(q) > 1e-5) next.push(q);
    pieces = next;
  }
  return pieces;
}
const META = new Map();
function meta(pts) {
  let m = META.get(pts);
  if (!m) {
    let yMax = -1e9, yMin = 1e9, head = 0;
    pts.forEach(([, y], i) => { if (y > yMax) { yMax = y; head = i; } if (y < yMin) yMin = y; });
    m = { yMax, yMin, head }; META.set(pts, m);
  }
  return m;
}
const CHEVRON = [[-0.5, -0.1], [0, 0.25], [0.5, -0.1], [0.5, -0.3], [0, 0.06], [-0.5, -0.3]];
const DIAMOND = [[0, 1], [0.55, 0], [0, -1], [-0.55, 0]];
const BL = DATA.blink;
const DOOR = { left: { pts: BL.left, c: centroid(BL.left) }, right: { pts: BL.right, c: centroid(BL.right) } };
const DASH_SHAPES = DATA.effects.dash.flatMap((e) => e.shapes.map((s) => s.p));
const RAM_FRONT_SHAPES = DATA.effects.ram.filter((e, i, a) => i !== (a.length - 1) / 2).flatMap((e) => e.shapes.map((s) => s.p));
const OD_GROUPS = DATA.effects.overdrive;

// ---------- images ----------
const SPR = {};
for (const n in DATA.ships) { const im = new Image(); im.src = DATA.ships[n].src; SPR[n] = { im, aspect: DATA.ships[n].aspect, outline: DATA.ships[n].outline }; }
const neb = new Image(); neb.src = DATA.nebula;
let nebPattern = null, NEB_MATRIX = new DOMMatrix(), NEB_SCALE = 1;
neb.onload = () => {
  const c = document.createElement("canvas"); c.width = neb.width * 2; c.height = neb.height * 2;
  const g = c.getContext("2d");
  g.drawImage(neb, 0, 0);
  g.save(); g.translate(c.width, 0); g.scale(-1, 1); g.drawImage(neb, 0, 0); g.restore();
  g.save(); g.translate(0, c.height); g.scale(1, -1); g.drawImage(neb, 0, 0); g.restore();
  g.save(); g.translate(c.width, c.height); g.scale(-1, -1); g.drawImage(neb, 0, 0); g.restore();
  nebPattern = ctx.createPattern(c, "repeat");
  const sc = document.createElement("canvas"); sc.width = NEB_N; sc.height = NEB_N;
  const sg = sc.getContext("2d"); sg.drawImage(c, 0, 0, NEB_N, NEB_N);
  NEB.px = sg.getImageData(0, 0, NEB_N, NEB_N).data; NEB.w = c.width; NEB.h = c.height;
};
// backdrop colour under a world point, value-lifted so an effect in it still reads as light
const NEB_N = 96, NEB = { px: null, w: 1, h: 1 }, NEB_PER_SHIP = 70 / 2.2;
function backdropRGB(x, y) {
  if (!NEB.px) return [120, 150, 255];
  const u = (((x * NEB_PER_SHIP) % NEB.w) + NEB.w) % NEB.w / NEB.w, v = (((-y * NEB_PER_SHIP) % NEB.h) + NEB.h) % NEB.h / NEB.h;
  const i = (Math.floor(v * NEB_N) * NEB_N + Math.floor(u * NEB_N)) * 4;
  const c = [NEB.px[i], NEB.px[i + 1], NEB.px[i + 2]], mx = Math.max(1, ...c);
  return mixRGB(c.map((q) => Math.round(q * 235 / mx)), WHITE, 0.25);
}
// each ship's own line colour (its darkest opaque pixels) and a rear-edge rim mask for the hull light
const SHIP_INK = {}, RIM = {};
function prepShip(n) {
  const im = SPR[n].im, w = im.naturalWidth, h = im.naturalHeight;
  const c = document.createElement("canvas"); c.width = w; c.height = h;
  const g = c.getContext("2d"); g.drawImage(im, 0, 0);
  const d = g.getImageData(0, 0, w, h).data, px = [];
  for (let i = 0; i < d.length; i += 4) if (d[i + 3] > 200) px.push([d[i], d[i + 1], d[i + 2], d[i] + d[i + 1] + d[i + 2]]);
  px.sort((a, b) => a[3] - b[3]);
  const dark = px.slice(0, Math.max(1, Math.floor(px.length * 0.15)));
  SHIP_INK[n] = [0, 1, 2].map((j) => Math.round(dark.reduce((s, p) => s + p[j], 0) / dark.length));
  const r = document.createElement("canvas"); r.width = w; r.height = h;
  const rg = r.getContext("2d");
  rg.drawImage(im, 0, 0); rg.globalCompositeOperation = "source-in"; rg.fillStyle = "#fff"; rg.fillRect(0, 0, w, h);
  rg.globalCompositeOperation = "destination-out"; rg.drawImage(im, 0, -Math.round(h * 0.03));
  RIM[n] = r;
}

// ---------- canvas ----------
const cv = document.getElementById("cv");
const ctx = cv.getContext("2d");
let CW = 0, CH = 0, DPR = 1;
function resize() {
  const r = cv.getBoundingClientRect();
  DPR = Math.min(window.devicePixelRatio || 1, 2);
  CW = r.width; CH = r.height;
  cv.width = Math.round(CW * DPR); cv.height = Math.round(CH * DPR);
}
new ResizeObserver(resize).observe(cv);

// ---------- sim ----------
const ship = { x: 0, y: 0, th: 0, t: 0, speed: 0, vx: 0, vy: 0, flash: 0 };
let T = 0;
const cam = { x: 0, y: 0 };
let parts = [];
let ability = null;
let lastFire = 0;
let regions = [];

const fwd = (th) => [Math.sin(th), Math.cos(th)];
function rot(lx, ly, th) { const c = Math.cos(th), s = Math.sin(th); return [lx * c + ly * s, -lx * s + ly * c]; }
function mixRGB(a, b, t) { return a.map((v, i) => Math.round(v + (b[i] - v) * t)); }
const WHITE = [255, 255, 255];
const easeOut = (u) => 1 - Math.pow(1 - u, 3);
const easeIn = (u) => u * u * u;
const back = (u, k) => 1 + (k + 1) * Math.pow(u - 1, 3) + k * Math.pow(u - 1, 2);
const rand = (a, b) => a + Math.random() * (b - a);
const pick = (a) => a[(Math.random() * a.length) | 0];
function chance(x) { let n = Math.floor(x); if (Math.random() < x - n) n++; return n; }
function angleLerp(a, b, t) { let d = ((b - a + Math.PI) % (2 * Math.PI) + 2 * Math.PI) % (2 * Math.PI) - Math.PI; return a + d * t; }
const STAR4 = [[0, 1], [0.16, 0.16], [1, 0], [0.16, -0.16], [0, -1], [-0.16, -0.16], [-1, 0], [-0.16, 0.16]];
const STAR8 = Array.from({ length: 16 }, (_, i) => { const a = i * Math.PI / 8, r = i % 2 ? 0.38 : 1; return [Math.sin(a) * r, Math.cos(a) * r]; });
function twinkle(mode, x, y, vx, vy, size) {
  const s = size * rand(0.6, 1.3);
  add({ kind: "twinkle", mode, pts: STAR4.map(([a, b]) => [a * s, b * s]), x, y, vx, vy, drag: 3, th: rand(-0.3, 0.3), age: -rand(0, 0.08), life: rand(0.2, 0.35) });
}
function poof(mode, x, y, k) {
  for (let i = 0; i < k.poofLines; i++) {
    const a = (i / k.poofLines) * Math.PI * 2 + rand(-0.2, 0.2), r = 0.25 * k.poofSize, len = k.poofSize * rand(0.18, 0.32), w = 0.014;
    const sp = k.poofSize * rand(3, 5);
    add({ kind: "poof", mode, pts: [[-w, 0], [w, 0], [0, len]], x: x + Math.sin(a) * r, y: y + Math.cos(a) * r, th: a,
      vx: Math.sin(a) * sp + ship.vx * 0.5, vy: Math.cos(a) * sp + ship.vy * 0.5, drag: 7, age: 0, life: rand(0.12, 0.2) });
  }
  for (let i = 0; i < k.twinkles; i++) {
    const a = rand(0, Math.PI * 2), r = k.poofSize * rand(0.2, 0.7);
    twinkle(mode, x + Math.sin(a) * r, y + Math.cos(a) * r, Math.sin(a) * 0.6, Math.cos(a) * 0.6, k.twinkleSize);
  }
}
function add(p) { p.seed = Math.random() * 1000; parts.push(p); if (parts.length > 1800) parts.splice(0, parts.length - 1800); }

// hull points a region sheds from, in ship units with outward normals
function buildRegions(m, k) {
  const ol = SPR[m.ship].outline;
  const rear = { pts: ol.filter((q) => q[1] < k.rearY), shapes: DASH_SHAPES, rate: k.rate, size: k.size, life: k.lifetime, inherit: k.inherit, kick: k.kick, n: 0, acc: 0 };
  if (m.effect === "dash") return [rear];
  if (m.effect === "ram") return [rear, { pts: ol.filter((q) => q[1] > k.frontY), shapes: RAM_FRONT_SHAPES, rate: k.frontRate, size: k.frontSize, life: k.frontLife,
    inherit: k.frontInherit, kick: k.frontKick, n: 0, acc: 0, front: true }];
  // overdrive: one region per wingtip, the hull points nearest each drawn trail head
  return OD_GROUPS.map((g) => {
    const near = ol.map((q) => [q, Math.hypot(q[0] - g.x, q[1] - g.y)]).sort((a, b) => a[1] - b[1]).slice(0, 4).map((a) => a[0]);
    return { pts: near, shapes: g.shapes.map((s) => s.p), rate: k.rate, size: k.size, life: k.lifetime, inherit: k.inherit, kick: k.kick, n: 0, acc: 0 };
  });
}

function startAbility() {
  const m = MODES[ui.mode], k = S[ui.mode];
  ability = { t: 0, mode: ui.mode, ended: false };
  if (m.effect === "blink") { ability.pulled = 0; return; }
  regions = buildRegions(m, k);
  const aspect = SPR[m.ship].aspect;
  for (let i = 0; i < k.chevrons; i++) {
    const sz = k.chevronSize * aspect * (1 - i * 0.12);
    const [px, py] = shipPoint(0, -0.55 - i * 0.16 * k.chevronSize);
    const [bx, by] = fwd(ship.th);
    add({ kind: "flick", mode: ui.mode, pts: CHEVRON.map(([x, y]) => [x * sz, y * sz]), x: px, y: py, th: ship.th,
      vx: -bx * k.chevronSpeed * (1 + i * 0.35), vy: -by * k.chevronSpeed * (1 + i * 0.35), age: -i * 0.035, life: 0.32, bright: 1 });
  }
  // manga action lines riding with the ship for a couple of drawn frames
  for (let i = 0; i < k.actionLines; i++) {
    let lo = rand(-1, 1); if (Math.abs(lo) < 0.3) lo = Math.sign(lo || 1) * rand(0.3, 1);
    const [px, py] = shipPoint(lo * (aspect * 0.5 + 0.5), rand(-1.4, 0.5));
    const L = k.actionLen * rand(0.5, 1.2), w = 0.012;
    add({ kind: "action", mode: ui.mode, pts: [[-w, 0], [w, 0], [0, -L]], x: px, y: py, th: ship.th, vx: ship.vx * 0.85, vy: ship.vy * 0.85,
      age: -rand(0, 0.06), life: k.actionLife * rand(0.7, 1.3) });
  }
  if (k.impactStar > 0) {
    const s = k.impactStar * aspect * 0.5;
    const [px, py] = shipPoint(0, 0.58);
    add({ kind: "star", mode: ui.mode, pts: STAR8.map(([a, b]) => [a * s, b * s]), x: px, y: py, th: ship.th, vx: ship.vx, vy: ship.vy, age: 0, life: 0.16 });
  }
  ability.twAcc = 0;
  // the lunge shoves streaks hard off the hull's sides
  const ol = SPR[m.ship].outline.filter((q) => Math.abs(q[2]) > 0.5);
  for (let i = 0; i < k.dashBurst; i++) shed(m, k, { pts: ol, shapes: DASH_SHAPES, size: k.size * 0.7, life: k.lifetime * 0.7, inherit: k.inherit * 0.6, kick: k.kick * 2.2, n: (Math.random() * 99) | 0 });
}
function shipPoint(lx, ly) { const [wx, wy] = rot(lx, ly, ship.th); return [ship.x + wx, ship.y + wy]; }

function shed(m, k, r) {
  if (!r.pts.length) return;
  const q = pick(r.pts);
  const [x, y] = shipPoint(q[0], q[1]);
  const [nx, ny] = rot(q[2], q[3], ship.th);
  const kick = r.kick * rand(0.5, 1.3);
  const vx = ship.vx * r.inherit + nx * kick, vy = ship.vy * r.inherit + ny * kick;
  const sh = r.shapes[r.n++ % r.shapes.length];
  const sv = 1 + k.sizeVar * rand(-0.5, 0.5);
  add({ kind: "trail", mode: ui.mode, x, y, vx, vy, drag: k.drag, th: ship.th, th0: ship.th, pts: sh, age: 0, life: r.life * rand(0.85, 1.15),
    sv: sv * r.size / k.size, lv: 1 + k.lenVar * rand(-0.5, 0.6), bright: 1 + k.colorVar * rand(-0.6, 0.6) });
  for (let i = 0, n = chance(k.lines); i < n; i++) {
    const L = k.lineLen * rand(0.6, 1.3), w = 0.008;
    add({ kind: "line", mode: ui.mode, pts: [[-w, 0], [w, 0], [w * 0.2, -L], [-w * 0.2, -L]], x, y,
      vx: ship.vx * r.inherit * 1.05 + nx * kick * 0.4, vy: ship.vy * r.inherit * 1.05 + ny * kick * 0.4, drag: k.drag * 0.8,
      th: ship.th, th0: ship.th, age: 0, life: r.life * rand(0.4, 0.8), bright: 1 + k.colorVar * rand(0, 0.6), aligned: true });
  }
  for (let i = 0, n = chance(k.motes); i < n; i++) mote(ui.mode, x, y, vx * 0.8 + nx * rand(0, 0.6), vy * 0.8 + ny * rand(0, 0.6), k.moteSize, r.life * rand(1, 1.8), k.colorVar);
}
function mote(mode, x, y, vx, vy, size, life, cvar) {
  const s = size * rand(0.5, 1.4);
  add({ kind: "mote", mode, pts: DIAMOND.map(([a, b]) => [a * s, b * s]), x, y, th: rand(-0.4, 0.4), vx, vy, drag: 2.2,
    age: 0, life, ph: rand(0, 6.28), bright: 1 + (cvar || 0.3) * rand(-0.3, 0.8) });
}

function sparksFrom(mode, x, y, th, n, spreadX, k, len = 0.09) {
  for (let i = 0; i < n; i++) {
    const ang = th + Math.PI + rand(-1.1, 1.1);
    const sp = k.shatterSpeed * rand(0.6, 1.4);
    const [ox, oy] = rot(rand(-spreadX, spreadX), 0, th);
    add({ kind: "spark", mode, pts: [[0, len * rand(0.7, 1.3)], [0.012, 0], [0, -len * 0.5], [-0.012, 0]], x: x + ox, y: y + oy,
      th: ang + Math.PI, vx: Math.sin(ang) * sp, vy: Math.cos(ang) * sp, drag: 2, age: 0, life: k.shatterLife * rand(0.6, 1.2), bright: 1.4 });
  }
}

function dieTrail(p) {
  const k = S[p.mode];
  pose(p);
  const n = Math.round(k.pieces || 0);
  if (n > 0) {
    const pts = p.pts.map(([x, y]) => [x * p.sx, y * p.sy]);
    const [pcx, pcy] = centroid(pts);
    for (const fr of fracture(pts, n)) {
      const [fcx, fcy] = centroid(fr);
      let dx = fcx - pcx, dy = fcy - pcy; const dl = Math.hypot(dx, dy) || 1;
      const [wx, wy] = rot(fcx, fcy, p.th), [vx, vy] = rot(dx / dl, dy / dl, p.th);
      const sp = k.shatterSpeed * rand(0.6, 1.4);
      add({ kind: "frag", mode: p.mode, pts: fr.map(([x, y]) => [x - fcx, y - fcy]), x: p.x + wx, y: p.y + wy, th: p.th,
        vx: vx * sp, vy: vy * sp, spin: (Math.random() < 0.5 ? -1 : 1) * k.spin * Math.PI / 180 * rand(0.5, 1.5), age: 0, life: k.shatterLife * rand(0.7, 1.3), bright: p.bright });
    }
  }
  const mt = meta(p.pts);
  const [hx, hy] = rot(0, mt.yMax * p.sy * 0.5, p.th);
  sparksFrom(p.mode, p.x + hx, p.y + hy, p.th, p.burst ? 1 : Math.round(k.sparks || 0), 0.05, k);
}

function blinkTarget(k) { const [fx, fy] = fwd(ship.th); return [ship.x + fx * k.range, ship.y + fy * k.range]; }
function doorPts(side, offsetK, squeeze, tiltRad, k, yScale = 1) {
  const d = DOOR[side];
  const [cx, cy] = d.c, sgn = side === "left" ? -1 : 1, ct = Math.cos(tiltRad * sgn), st = Math.sin(tiltRad * sgn);
  return d.pts.map(([x, y]) => {
    const lx = (x - cx) * squeeze * k.doorScale, ly = (y - cy) * k.doorScale * yScale;
    return [lx * ct + ly * st + cx * offsetK * k.doorScale, -lx * st + ly * ct + cy];
  });
}

function blinkTick(m, k, dt) {
  // wind-up: lines get pulled in from a ring toward the ship
  const due = Math.floor(k.pullLines * Math.min(1, ability.t / (m.window * 0.85)));
  while (ability.pulled < due) {
    ability.pulled++;
    const a = rand(0, Math.PI * 2), r = k.pullRadius * rand(0.8, 1.2);
    const [ox, oy] = [Math.sin(a) * r, Math.cos(a) * r];
    const sp = r / (m.window * rand(0.5, 0.8));
    const L = rand(0.25, 0.5), w = 0.01;
    add({ kind: "pull", mode: ui.mode, pts: [[-w, 0], [w, 0], [w * 0.2, -L], [-w * 0.2, -L]], ox, oy, th: Math.atan2(-ox, -oy),
      sp, r, age: 0, life: r / sp, bright: 1.2 });
  }
  if (ability.t >= m.window && !ability.done) {
    ability.done = true;
    add({ kind: "pinch", mode: ui.mode, x: ship.x, y: ship.y, th: ship.th, age: 0, life: k.pinchFade });
    for (let i = 0; i < k.departMotes; i++) {
      const a = rand(0, Math.PI * 2), sp = rand(0.3, 1.4);
      mote(ui.mode, ship.x + rand(-0.05, 0.05), ship.y + rand(-0.4, 0.4), Math.sin(a) * sp, Math.cos(a) * sp, k.moteSize, rand(0.4, 0.9), 0.5);
    }
    poof(ui.mode, ship.x, ship.y, k);
    const [tx, ty] = blinkTarget(k);
    ship.x = tx; ship.y = ty;
    ship.flash = 1;
    poof(ui.mode, ship.x, ship.y, k);
    add({ kind: "open", mode: ui.mode, x: ship.x, y: ship.y, th: ship.th, age: 0, life: k.openTime });
    for (let i = 0; i < k.burst; i++) {
      const side = i % 2 ? 1 : -1;
      const a = ship.th + side * rand(0.5, 2.4);
      const sp = k.burstSpeed * rand(0.6, 1.2);
      add({ kind: "trail", mode: ui.mode, x: ship.x, y: ship.y + rand(-0.2, 0.2), vx: Math.sin(a) * sp + ship.vx * 0.5, vy: Math.cos(a) * sp + ship.vy * 0.5,
        drag: 4, th: a, th0: a, pts: pick(DASH_SHAPES), age: 0, life: rand(0.3, 0.5), sv: k.burstSize * rand(0.7, 1.2), lv: rand(0.8, 1.4), bright: rand(0.9, 1.4), burst: true });
    }
    for (let i = 0; i < k.arriveMotes; i++) {
      const a = rand(0, Math.PI * 2), sp = rand(0.5, 2);
      mote(ui.mode, ship.x, ship.y, Math.sin(a) * sp + ship.vx * 0.6, Math.cos(a) * sp + ship.vy * 0.6, k.moteSize, rand(0.4, 0.9), 0.5);
    }
  }
  if (ability.t > m.window + 1.5) ability = null;
}

function step(dt) {
  const m = MODES[ui.mode], k = S[ui.mode];
  ship.t += dt; T += dt;
  const active = ability && ability.mode === ui.mode && ability.t < m.window;
  const agile = active && m.effect === "overdrive" ? 1.5 : 1;
  ship.th = Math.sin(ship.t * 0.55) * W.weave * agile + Math.sin(ship.t * 0.23) * 0.6;
  let boost = 1;
  if (ability && m.effect !== "blink") {
    if (m.effect === "overdrive") boost = active ? k.boost : 1;
    else boost = 1 + (k.boost - 1) * Math.exp(-ability.t / (m.window * 0.6));
  }
  ship.speed = W.cruise * boost;
  const [fx, fy] = fwd(ship.th);
  ship.vx = fx * ship.speed; ship.vy = fy * ship.speed;
  ship.x += ship.vx * dt; ship.y += ship.vy * dt;
  ship.flash = Math.max(0, ship.flash - dt * 4);

  if (ability) {
    ability.t += dt;
    if (m.effect === "blink") blinkTick(m, k, dt);
    else if (active) {
      for (const r of regions) {
        r.acc += ship.speed * dt * r.rate;
        const n = Math.floor(r.acc); r.acc -= n;
        for (let i = 0; i < n; i++) shed(m, k, r);
      }
      ability.twAcc += k.twinkleRate * dt;
      while (ability.twAcc >= 1) {
        ability.twAcc -= 1;
        const r = pick(regions), q = pick(r.pts);
        const [x, y] = shipPoint(q[0], q[1]);
        twinkle(ui.mode, x, y, ship.vx * 0.5, ship.vy * 0.5, k.twinkleSize);
      }
    } else {
      if (!ability.ended) {
        ability.ended = true;
        for (const p of parts) if (p.kind === "trail" && p.age < k.endBreak) p.age = p.life;
      }
      if (ability.t > m.window + 2) ability = null;
    }
  }

  const dying = [];
  for (const p of parts) {
    p.age += dt;
    if (p.age < 0) continue;
    if (p.vx !== undefined) {
      if (p.drag) { const f = Math.exp(-p.drag * dt); p.vx *= f; p.vy *= f; }
      p.x += p.vx * dt; p.y += p.vy * dt;
    }
    if ((p.kind === "trail" || p.aligned) && !p.burst) {
      const rx = p.vx - ship.vx, ry = p.vy - ship.vy;
      p.rel = Math.hypot(rx, ry);
      if (p.rel > 0.05) p.th = angleLerp(p.th0, Math.atan2(-rx, -ry), S[p.mode].align);
    }
    if (p.spin) p.th += p.spin * dt;
    if (p.age >= p.life && !p.dead) { p.dead = true; dying.push(p); }
  }
  for (const p of dying) {
    const k2 = S[p.mode];
    if (p.kind === "trail") dieTrail(p);
    else if (p.kind === "pinch") sparksFrom(p.mode, p.x, p.y, p.th, Math.round(k2.pinchShards), 0.04, k2, 0.14);
    else if (p.kind === "open") openEnd(p);
  }
  parts = parts.filter((p) => p.age < p.life);

  cam.x += (ship.x - cam.x) * Math.min(1, dt * 10);
  cam.y += (ship.y - cam.y) * Math.min(1, dt * 10);
  if (ui.auto && ship.t - lastFire > W.autoEvery && !ability) fire();
}

function openEnd(p) {
  const k = S[p.mode], n = Math.round(k.pieces);
  for (const side of ["left", "right"]) {
    const pts = doorPts(side, k.swing, 1, k.swingDeg * Math.PI / 180, k);
    const [pcx, pcy] = centroid(pts);
    const [wx, wy] = rot(pcx, pcy, p.th);
    sparksFrom(p.mode, p.x + wx, p.y + wy, p.th + (side === "left" ? 0.5 : -0.5), Math.round(k.sparks), 0.03, k);
    if (n < 1) continue;
    for (const fr of fracture(pts, n)) {
      const [fcx, fcy] = centroid(fr);
      let dx = fcx - pcx + (side === "left" ? -0.3 : 0.3), dy = fcy - pcy; const dl = Math.hypot(dx, dy) || 1;
      const [fx2, fy2] = rot(fcx, fcy, p.th), [vx, vy] = rot(dx / dl, dy / dl, p.th);
      const sp = k.shatterSpeed * rand(0.6, 1.4);
      add({ kind: "frag", mode: p.mode, pts: fr.map(([x, y]) => [x - fcx, y - fcy]), x: p.x + fx2, y: p.y + fy2, th: p.th,
        vx: vx * sp, vy: vy * sp, spin: (Math.random() < 0.5 ? -1 : 1) * k.spin * Math.PI / 180, age: 0, life: k.shatterLife, bright: 1 });
    }
  }
}

// trail streak pose: snap-in pop and stretch, speed stretch, then the collapse to a sliver
function pose(p, age = p.age) {
  const k = S[p.mode];
  const snapTime = k.snapTime || 0.06, over = k.overshoot2 !== undefined ? k.overshoot2 : k.overshoot;
  const u = Math.min(1, age / p.life);
  const us = Math.min(1, age / snapTime);
  const pop = back(us, over);
  const grow = (k.size || 1) * p.sv * (1 + ((k.sizeEnd !== undefined ? k.sizeEnd : 0.8) - 1) * u);
  const col = k.collapse || 0;
  const cs = col > 0 ? Math.max(0, (u - (1 - col)) / col) : 0;
  const speed = 1 + (k.speedStretch || 0) * Math.min(3, (p.rel || 0) / W.cruise);
  p.sx = grow * pop * (1 - 0.9 * easeIn(cs));
  p.sy = grow * pop * p.lv * speed * (1 + ((k.stretch || 1.5) - 1) * (1 - easeOut(us))) * (1 + 0.3 * cs);
}

// ---------- draw ----------
function scale() { return W.shipPx * ui.zoom; }
function toScreen(x, y) { const s = scale(); return [CW / 2 + (x - cam.x) * s, CH / 2 - (y - cam.y) * s]; }
function hash(a, b, c) { const x = Math.sin(a * 12.9898 + b * 78.233 + c * 37.719) * 43758.5453; return x - Math.floor(x); }
function stepAge(age, k) { return k.stepFps > 0 ? Math.floor(age * k.stepFps) / k.stepFps : age; }
function boilFrame(k) { return Math.floor(T * (k.boilFps || 12)); }

let items = [];
// one drawable in screen space; ys carries each vertex's position head (1) to tail (0) for the ink taper
function shapeItem(mode, pts, x, y, th, sx, sy, alpha, flashU, bright, seed, opt = {}) {
  const m = MODES[mode], k = S[mode];
  if (alpha <= 0.002) return;
  const f = (k.flash || 0) * Math.max(0, 1 - flashU);
  const b = Math.max(0, bright - 1), dim = Math.min(1, bright);
  const u = opt.u !== undefined ? opt.u : 0.5;
  if (W.shipInk > 0 && !SHIP_INK[m.ship] && SPR[m.ship].im.complete) prepShip(m.ship);
  const local = W.localColor > 0 ? backdropRGB(x, y) : WHITE;
  const take = W.localColor * (1 - W.coolAge + W.coolAge * u);
  const base = mixRGB(mixRGB(WHITE, m.tintRGB, k.tint * dim), local, take);
  const fillRGB = mixRGB(base, WHITE, Math.min(1, f * 0.6 + b * 0.6));
  const tailRGB = mixRGB(fillRGB, local, W.localColor * W.coolAge);
  const edgeRGB = mixRGB(mixRGB(mixRGB(WHITE, m.tintRGB, k.tint * 0.35), local, take), WHITE, Math.min(1, f + b));
  const s = scale(), c = Math.cos(th), sn = Math.sin(th);
  const bp = (k.boil || 0) * s, fr = boilFrame(k);
  const toS = (lx0, ly0) => { const lx = lx0 * sx, ly = ly0 * sy; return [CW / 2 + (x + lx * c + ly * sn - cam.x) * s, CH / 2 - (y - lx * sn + ly * c - cam.y) * s]; };
  const sp = pts.map(([lx, ly], i) => { const [px, py] = toS(lx, ly); return [px + (hash(seed, i, fr) - 0.5) * 2 * bp, py + (hash(seed, i + 50, fr) - 0.5) * 2 * bp]; });
  let ys, h, t, head = 0;
  if (opt.open) {
    const n = pts.length;
    ys = pts.map((_, i) => 1 - Math.abs((2 * i) / Math.max(1, n - 1) - 1));
    h = sp[0]; t = sp[n - 1];
  } else {
    const mt = meta(pts);
    ys = pts.map(([, yy]) => (yy - mt.yMin) / Math.max(1e-6, mt.yMax - mt.yMin));
    h = toS(0, mt.yMax); t = toS(0, mt.yMin); head = mt.head;
  }
  items.push({ sp, ys, head, h, t, open: !!opt.open, refr: opt.open ? 0 : W.refract * alpha,
    fillRGB, tailRGB, fa: opt.open ? 0 : Math.min(1, k.fill * alpha * (1 + f) * (0.6 + 0.4 * dim)) * (opt.fillMul || 1),
    edgeRGB, ea: Math.min(1, k.edge * alpha * (opt.edgeMul || 1)), edgePx: k.edgePx * (1 + f) * (opt.edgeW || 1),
    outline: opt.outline !== undefined ? opt.outline : k.outline,
    gradient: opt.gradient !== undefined ? opt.gradient : k.gradient,
    inkA: (k.ink || 0) * alpha * (opt.inkMul !== undefined ? opt.inkMul : 1), inkPx: (k.inkPx || 0) * (opt.inkW || 1),
    inkRGB: mixRGB(mixRGB([12, 10, 28], m.tintRGB, 0.18), SHIP_INK[m.ship] || [12, 10, 28], W.shipInk),
    hatchA: opt.hatch === false || opt.open ? 0 : (k.hatch || 0) * alpha, hatchGap: k.hatchGap || 4, hatchAngle: (k.hatchAngle || 0) * Math.PI / 180 });
}

function renderItems(additive) {
  ctx.globalCompositeOperation = "source-over";
  // refraction: the backdrop redrawn inside each streak, dragged along it
  if (nebPattern && W.refract > 0) for (const it of items) {
    if (it.refr < 0.01) continue;
    const L = Math.hypot(it.t[0] - it.h[0], it.t[1] - it.h[1]); if (L < 1) continue;
    const ox = (it.t[0] - it.h[0]) / L * W.refractPx, oy = (it.t[1] - it.h[1]) / L * W.refractPx;
    ctx.save();
    ctx.beginPath(); it.sp.forEach(([px, py], i) => (i ? ctx.lineTo(px, py) : ctx.moveTo(px, py))); ctx.closePath(); ctx.clip();
    nebPattern.setTransform(NEB_MATRIX.translate(-ox / NEB_SCALE, -oy / NEB_SCALE));
    ctx.globalAlpha = Math.min(1, it.refr); ctx.fillStyle = nebPattern; ctx.fillRect(0, 0, CW, CH);
    ctx.globalCompositeOperation = "lighter"; ctx.globalAlpha = Math.min(1, it.refr) * 0.12; ctx.fillStyle = "#fff"; ctx.fillRect(0, 0, CW, CH);
    ctx.restore();
  }
  // ink first, normally blended: dark ink adds nothing under a light blend
  ctx.globalCompositeOperation = "source-over";
  ctx.lineCap = "round";
  for (const it of items) {
    if (it.inkA < 0.01 || it.inkPx <= 0) continue;
    ctx.strokeStyle = `rgba(${it.inkRGB},${Math.min(1, it.inkA)})`;
    const n = it.sp.length, segs = it.open ? n - 1 : n;
    for (let i = 0; i < segs; i++) {
      const a = it.sp[i], b = it.sp[(i + 1) % n], yy = (it.ys[i] + it.ys[(i + 1) % n]) / 2;
      ctx.lineWidth = it.inkPx * (0.3 + 0.9 * yy);
      ctx.beginPath(); ctx.moveTo(a[0], a[1]); ctx.lineTo(b[0], b[1]); ctx.stroke();
    }
  }
  ctx.globalCompositeOperation = additive ? "lighter" : "source-over";
  ctx.lineCap = "butt";
  for (const it of items) {
    const path = () => { ctx.beginPath(); it.sp.forEach(([px, py], i) => (i ? ctx.lineTo(px, py) : ctx.moveTo(px, py))); if (!it.open) ctx.closePath(); };
    if (it.fa > 0) {
      path();
      const long = Math.hypot(it.h[0] - it.t[0], it.h[1] - it.t[1]) > 1;
      if (long && W.bands > 0) {
        // flat cel bands, head to tail, like the shipped laser bolt
        const g = ctx.createLinearGradient(it.h[0], it.h[1], it.t[0], it.t[1]), n = W.bands + 1;
        for (let i = 0; i < n; i++) {
          const q = n > 1 ? i / (n - 1) : 0, c = mixRGB(it.fillRGB, it.tailRGB, q), a = it.fa * (1 - it.gradient * q);
          g.addColorStop(i / n, `rgba(${c},${a})`); g.addColorStop((i + 1) / n - 1e-4, `rgba(${c},${a})`);
        }
        ctx.fillStyle = g;
      } else if ((it.gradient > 0 || W.localColor > 0) && long) {
        const g = ctx.createLinearGradient(it.h[0], it.h[1], it.t[0], it.t[1]);
        g.addColorStop(0, `rgba(${it.fillRGB},${it.fa})`);
        g.addColorStop(1, `rgba(${it.tailRGB},${it.fa * (1 - it.gradient)})`);
        ctx.fillStyle = g;
      } else ctx.fillStyle = `rgba(${it.fillRGB},${it.fa})`;
      ctx.fill();
    }
    if (it.hatchA > 0.01) {
      let x0 = 1e9, y0 = 1e9, x1 = -1e9, y1 = -1e9;
      for (const [px, py] of it.sp) { x0 = Math.min(x0, px); y0 = Math.min(y0, py); x1 = Math.max(x1, px); y1 = Math.max(y1, py); }
      const R = Math.hypot(x1 - x0, y1 - y0) / 2, cx = (x0 + x1) / 2, cy = (y0 + y1) / 2;
      if (R > it.hatchGap) {
        ctx.save(); path(); ctx.clip();
        const ang = Math.atan2(it.t[1] - it.h[1], it.t[0] - it.h[0]) + it.hatchAngle;
        const dx = Math.cos(ang), dy = Math.sin(ang), nx = -dy, ny = dx;
        const g = ctx.createLinearGradient(it.h[0], it.h[1], it.t[0], it.t[1]);
        g.addColorStop(0, `rgba(${it.edgeRGB},${Math.min(1, it.hatchA)})`);
        g.addColorStop(1, `rgba(${it.edgeRGB},0)`);
        ctx.strokeStyle = g; ctx.lineWidth = 1;
        ctx.beginPath();
        for (let o = -R; o <= R; o += it.hatchGap) { const px = cx + nx * o, py = cy + ny * o; ctx.moveTo(px - dx * R, py - dy * R); ctx.lineTo(px + dx * R, py + dy * R); }
        ctx.stroke(); ctx.restore();
      }
    }
    if (it.ea > 0.01 && it.edgePx > 0 && (it.outline > 0 || it.open)) {
      ctx.lineJoin = "miter"; ctx.lineWidth = it.edgePx; ctx.strokeStyle = `rgba(${it.edgeRGB},${it.ea})`;
      if (it.open || it.outline >= 2) { path(); ctx.stroke(); }
      else {
        const n = it.sp.length, hh = it.head, a = it.sp[(hh - 1 + n) % n], c = it.sp[(hh + 1) % n];
        ctx.beginPath(); ctx.moveTo(a[0], a[1]); ctx.lineTo(it.sp[hh][0], it.sp[hh][1]); ctx.lineTo(c[0], c[1]); ctx.stroke();
      }
    }
  }
  ctx.globalCompositeOperation = "source-over";
  items = [];
}

function drawShip() {
  const m = MODES[ui.mode], sp = SPR[m.ship]; if (!sp.im.complete) return;
  const s = scale(), h = s, w = s * sp.aspect;
  const [sx, sy] = toScreen(ship.x, ship.y);
  ctx.save(); ctx.translate(sx, sy); ctx.rotate(ship.th); ctx.drawImage(sp.im, -w / 2, -h / 2, w, h);
  const fl = ship.flash * (S[ui.mode].hullFlash || 0);
  if (fl > 0.01) { ctx.globalCompositeOperation = "lighter"; ctx.globalAlpha = fl; ctx.drawImage(sp.im, -w / 2, -h / 2, w, h); ctx.drawImage(sp.im, -w / 2, -h / 2, w, h); }
  if (W.rimLight > 0) {
    if (!RIM[m.ship]) prepShip(m.ship);
    let env = ship.flash;
    if (ability && ability.mode === ui.mode) env = Math.max(env, ability.t < m.window ? Math.min(1, ability.t / 0.05) : Math.max(0, 1 - (ability.t - m.window) / 0.3));
    if (env > 0.01) {
      const c = mixRGB(mixRGB(WHITE, m.tintRGB, S[ui.mode].tint), backdropRGB(ship.x, ship.y), W.localColor * 0.5);
      const t = document.createElement("canvas"); t.width = RIM[m.ship].width; t.height = RIM[m.ship].height;
      const tg = t.getContext("2d"); tg.drawImage(RIM[m.ship], 0, 0); tg.globalCompositeOperation = "source-in"; tg.fillStyle = `rgb(${c})`; tg.fillRect(0, 0, t.width, t.height);
      ctx.globalCompositeOperation = "lighter"; ctx.globalAlpha = W.rimLight * env * 0.45;
      ctx.drawImage(t, -w / 2, -h / 2, w, h);
      ctx.globalAlpha = W.rimLight * env; ctx.filter = `blur(${Math.max(2, s * 0.04)}px)`; ctx.drawImage(t, -w / 2, -h / 2, w, h); ctx.drawImage(t, -w / 2, -h / 2, w, h); ctx.filter = "none";
    }
  }
  ctx.restore();
}

function collectParts() {
  for (const p of parts) {
    if (p.age < 0) continue;
    const k = S[p.mode], a = stepAge(p.age, k), u = Math.min(1, a / p.life);
    if (p.kind === "trail") {
      pose(p, a);
      const hot = 1 + (k.hotCore || 0) * Math.max(0, 1 - a / (p.life * 0.4)) * 0.8;
      shapeItem(p.mode, p.pts, p.x, p.y, p.th, p.sx, p.sy, 1 - easeIn(u) * 0.5, a / Math.max(0.02, (k.snapTime || 0.06) * 1.5), p.bright * hot, p.seed, { u });
    } else if (p.kind === "line") {
      const st = 1 + (k.speedStretch || 0) * Math.min(3, (p.rel || 0) / W.cruise);
      shapeItem(p.mode, p.pts, p.x, p.y, p.th, 1, st, (1 - u) * 0.9, 1, p.bright, p.seed, { outline: 0, hatch: false, inkMul: 0.6, inkW: 0.6 });
    } else if (p.kind === "action") {
      shapeItem(p.mode, p.pts, p.x, p.y, p.th, 1, 1, 1 - u * 0.4, 0.5, 1.3, p.seed, { outline: 0, hatch: false, inkW: 0.9 });
    } else if (p.kind === "poof") {
      shapeItem(p.mode, p.pts, p.x, p.y, p.th, 1, 1 - u * 0.5, 1 - u * 0.6, 0.5, 1.3, p.seed, { outline: 0, hatch: false, inkW: 0.9 });
    } else if (p.kind === "star" || p.kind === "twinkle") {
      const pop = Math.sin(Math.PI * Math.min(1, u)) * (p.kind === "star" ? 1.15 : 1);
      shapeItem(p.mode, p.pts, p.x, p.y, p.th, pop, pop, 1, 0, 1.5, p.seed, { outline: 2, hatch: false, gradient: 0, fillMul: 1.6, inkW: 0.7 });
    } else if (p.kind === "pull") {
      const r = Math.max(0, p.r - p.sp * p.age);
      shapeItem(p.mode, p.pts, ship.x + p.ox * r / p.r, ship.y + p.oy * r / p.r, p.th + Math.PI, 1, 0.5 + u, Math.min(1, u * 4) * (1 - u * 0.5), 1, p.bright, p.seed, { outline: 0, hatch: false, inkMul: 0.6, inkW: 0.6 });
    } else if (p.kind === "mote") {
      const tw = 0.55 + 0.45 * Math.sin(a * 26 + p.ph);
      shapeItem(p.mode, p.pts, p.x, p.y, p.th, 1, 1, (1 - easeIn(u)) * tw * 1.6, 1, p.bright + 0.3, p.seed, { outline: 0, hatch: false, inkMul: 0.7, inkW: 0.5 });
    } else if (p.kind === "spark") {
      shapeItem(p.mode, p.pts, p.x, p.y, p.th, 1, 1 + u, (1 - u) * 1.6, 1, p.bright, p.seed, { outline: 0, hatch: false, inkMul: 0.5, inkW: 0.5 });
    } else if (p.kind === "frag" || p.kind === "flick") {
      const sc = p.kind === "flick" ? back(Math.min(1, a / 0.06), 2) * (1 - 0.3 * u) : 1 - 0.4 * u;
      shapeItem(p.mode, p.pts, p.x, p.y, p.th, sc, sc, 1 - easeIn(u), p.kind === "flick" ? a / 0.08 : 1, p.bright, p.seed);
    }
  }
  const k = S[ui.mode];
  for (const p of parts) {
    const a = stepAge(Math.max(0, p.age), k), u = Math.min(1, a / p.life);
    if (p.kind === "pinch") {
      const fl = 1 + (k.flare - 1) * easeOut(Math.min(1, u * 2));
      ["left", "right"].forEach((side, i) => shapeItem(p.mode, doorPts(side, 0, k.squeeze * (1 - 0.6 * u), 0, k, fl), p.x, p.y, p.th, 1, 1, 1 - u * 0.5, 0, 1.6, p.seed + i));
    } else if (p.kind === "open") {
      const e = back(Math.min(1, u * 1.4), k.overshoot);
      ["left", "right"].forEach((side, i) => shapeItem(p.mode, doorPts(side, k.swing * e, k.squeeze + (1 - k.squeeze) * Math.min(1, e), k.swingDeg * Math.PI / 180 * e, k), p.x, p.y, p.th, 1, 1, 1 - u * u, u * 3, 1, p.seed + i));
    }
  }
}

// layers that ride on the ship, drawn over it
function collectOverlays() {
  const m = MODES[ui.mode], k = S[ui.mode];
  if (!ability || ability.mode !== ui.mode) return;
  const at = stepAge(ability.t, k);
  const aspect = SPR[m.ship].aspect, w = aspect / 2;
  if (m.effect === "ram" && k.pressureArcs > 0 && at < m.window + 0.1) {
    const env = Math.min(1, at / 0.06) * (at > m.window ? 1 - (at - m.window) / 0.1 : 1);
    for (let i = 0; i < k.pressureArcs; i++) for (const side of [-1, 1]) {
      const z = k.arcSize, y0 = 0.5 + 0.05 + i * 0.07 * z;
      const P0 = [side * 0.04, y0], P1 = [side * (w * 0.95 + 0.12 * i) * z, y0 - 0.02], P2 = [side * (w * 1.05 + 0.2 * i) * z, y0 - (0.5 + 0.18 * i) * z];
      const pts = [];
      for (let j = 0; j <= 8; j++) { const t = j / 8, q = 1 - t; pts.push([q * q * P0[0] + 2 * q * t * P1[0] + t * t * P2[0], q * q * P0[1] + 2 * q * t * P1[1] + t * t * P2[1]]); }
      const flick = 0.7 + 0.3 * hash(i, side, boilFrame(k));
      shapeItem(ui.mode, pts, ship.x, ship.y, ship.th, 1, 1, env * flick * (1 - i * 0.18), 1, 1.3, 900 + i * 2 + side, { open: true, edgeW: 1.2, inkW: 1 });
    }
  }
  if (m.effect === "overdrive" && k.aura > 0 && at < m.window + 0.25) {
    const ending = at > m.window ? (at - m.window) / 0.25 : 0;
    const env = Math.min(1, at / 0.1) * (1 - ending);
    const ol = SPR[m.ship].outline, off = k.auraOffset, fr = boilFrame(k), seg = 8;
    const pts = ol.map((q) => [q[0] + q[2] * off, q[1] + q[3] * off]);
    for (let s0 = 0, si = 0; s0 < pts.length; s0 += seg - 2, si++) {
      if (hash(si, 7, fr) < k.auraBreaks + ending * 0.6) continue;
      const run = []; for (let j = 0; j < seg; j++) run.push(pts[(s0 + j) % pts.length]);
      shapeItem(ui.mode, run, ship.x, ship.y, ship.th, 1, 1, k.aura * env, 1, 1.4, 700 + si, { open: true, edgeW: 0.9, inkW: 0.8 });
    }
  }
  if (m.effect === "blink" && !ability.done) {
    const u = Math.min(1, at / m.window), a = 0.3;
    const offsetK = u < a ? 1 + k.anticip * easeOut(u / a) : (1 + k.anticip) * (1 - easeIn((u - a) / (1 - a)));
    const sq = u < a ? 1 : 1 + (k.squeeze - 1) * easeIn((u - a) / (1 - a));
    ["left", "right"].forEach((side, i) => shapeItem(ui.mode, doorPts(side, offsetK, sq, 0, k), ship.x, ship.y, ship.th, 1, 1, 1, 1 - u, 1 + u * 0.4, 600 + i));
    const [tx, ty] = blinkTarget(k);
    const pulse = 1 + k.markPulse * Math.sin(T * 40) * (1 - u) + k.markPulse * u;
    const lens = BL.lens.map(([x, y]) => [x * k.doorScale, y * k.doorScale]);
    shapeItem(ui.mode, lens, tx, ty, ship.th, pulse, pulse, k.markAlpha * Math.min(1, u * 4), 1 - u, 1, 650);
  }
}

function draw() {
  ctx.setTransform(DPR, 0, 0, DPR, 0, 0);
  ctx.globalCompositeOperation = "source-over";
  ctx.fillStyle = "#070912"; ctx.fillRect(0, 0, CW, CH);
  if (nebPattern) {
    const s = 2.2 * ui.zoom * W.shipPx / 70;
    NEB_SCALE = s;
    NEB_MATRIX = new DOMMatrix().translate(CW / 2 - cam.x * W.shipPx * ui.zoom, CH / 2 + cam.y * W.shipPx * ui.zoom).scale(s);
    nebPattern.setTransform(NEB_MATRIX);
    ctx.fillStyle = nebPattern; ctx.fillRect(0, 0, CW, CH);
  }
  const m = MODES[ui.mode], k = S[ui.mode];
  collectParts();
  renderItems(k.additive);
  drawShip();
  collectOverlays();
  renderItems(k.additive);

  let state = "idle";
  if (ability) {
    if (ability.t < m.window) state = `<b>${m.windowName} ${Math.max(0, m.window - ability.t).toFixed(2)} s</b>`;
    else state = m.effect === "blink" ? "arrived" : `${m.windowName} ended`;
  }
  document.getElementById("hud").innerHTML = `${m.label} · ${state}${ui.speed < 1 ? " · ¼ speed" : ""}${ui.paused ? " · paused" : ""}`;
}

let last = performance.now();
function frame(now) {
  const dt = Math.min(0.05, (now - last) / 1000); last = now;
  if (!ui.paused) { const sub = 3, h = dt * ui.speed / sub; for (let i = 0; i < sub; i++) step(h); }
  if (CW > 0) draw();
  requestAnimationFrame(frame);
}
function fire() { if (ability && ability.t < MODES[ui.mode].window) return; lastFire = ship.t; startAbility(); }

// ---------- authored starting points ----------
// Direction: dramatic but believable. Thin stable ink, light glow, no anime beats (action lines, chevrons, overshoot pops).
const REAL = { outline: 1, gradient: 0.9, additive: 0.7, fill: 0.26, edge: 0.7, edgePx: 1.1, tint: 0.8, flash: 0.5,
  ink: 0.55, inkPx: 1.3, boil: 0.012, boilFps: 12, stepFps: 0, hatch: 0.12, hatchGap: 5, hatchAngle: 40 };
const CALM = { snapTime: 0.1, overshoot: 0.5, stretch: 1.3, collapse: 0.5, chevrons: 0, actionLines: 0, pieces: 0, spin: 0,
  sparks: 0, pressureArcs: 0, impactStar: 0, aura: 0, twinkleRate: 0 };
const abc = (blurb, name, a, b, c) => ({ name, blurb, v: [{ ...b, ...a }, b, { ...b, ...c }] });

const BODY = {
  slipstream: { ...REAL, ...CALM, rearY: -0.1, rate: 9, lifetime: 0.6, size: 0.6, sizeEnd: 0.5, inherit: 0.85, drag: 3, kick: 0.3,
    align: 1, speedStretch: 0.9, dashBurst: 4, sizeVar: 0.4, lenVar: 0.6, colorVar: 0.4, hotCore: 0.3, lines: 0.8, lineLen: 1.4,
    motes: 0.4, moteSize: 0.025 },
  wash: { ...REAL, ...CALM, rearY: -0.3, rate: 10, lifetime: 0.7, size: 0.7, sizeEnd: 1.4, inherit: 0.5, drag: 4.5, kick: 0.6,
    align: 0.9, speedStretch: 0.6, dashBurst: 12, sizeVar: 0.5, lenVar: 0.5, colorVar: 0.5, hotCore: 0.6, lines: 0.4, lineLen: 1,
    motes: 1, moteSize: 0.025, gradient: 0.95 },
  shear: { ...REAL, ...CALM, rearY: 0.3, rate: 12, lifetime: 0.35, size: 0.5, sizeEnd: 0.6, inherit: 0.8, drag: 4, kick: 1,
    align: 0.8, speedStretch: 0.8, dashBurst: 6, sizeVar: 0.45, lenVar: 0.7, colorVar: 0.4, hotCore: 0.25, lines: 0.6, lineLen: 1.1,
    motes: 0.3, moteSize: 0.02, hatch: 0.3, hatchGap: 4, ink: 0.65 },
  // From the owner's own Vanguard export, settled: steadier lines, fewer motes.
  inked: { ...REAL, ...CALM, rearY: -0.05, rate: 7, lifetime: 0.85, size: 0.8, sizeEnd: 1.1, inherit: 0.55, drag: 1.8, kick: 0.5,
    align: 0.6, speedStretch: 0.45, dashBurst: 8, sizeVar: 0.6, lenVar: 0.75, colorVar: 0.6, hotCore: 0.35, lines: 0.8, lineLen: 0.9,
    motes: 0.8, moteSize: 0.03, snapTime: 0.12, overshoot: 0.6, outline: 2, gradient: 0.75, additive: 0.5, fill: 0.24, edge: 0.6,
    edgePx: 0.9, ink: 0.75, inkPx: 1.4, boil: 0.02, stepFps: 12, hatch: 0.25, hatchGap: 6, hatchAngle: 53, sparks: 1 },
};
const QUIET = { rate: 6, fill: 0.18, additive: 0.5, lines: 0.4, motes: 0.2, flash: 0.35, dashBurst: 2 };
const LOUD = { rate: 12, lifetime: 0.75, flash: 0.75, hotCore: 0.55, dashBurst: 10, lines: 1.2, fill: 0.34 };

const VANGUARD = [
  abc("Air peels off the hull edges and streams back in long thin lines, fading as the ship pulls away.",
    "Slipstream", QUIET, BODY.slipstream, LOUD),
  abc("Thrust washes out behind the engines: streaks spread and slow, with a heavier push at launch.",
    "Thruster wash", { ...QUIET, dashBurst: 6, sizeEnd: 1.1 }, BODY.wash, { ...LOUD, dashBurst: 18, sizeEnd: 1.7, lifetime: 0.8 }),
  abc("Short crisp shards of air shear off the whole outline: a fast, tight, high-speed feel.",
    "Hull shear", { rate: 8, lifetime: 0.28, hatch: 0.15, lines: 0.3 }, BODY.shear, { rate: 15, lifetime: 0.42, kick: 1.4, flash: 0.7, lines: 1 }),
  abc("Your Vanguard pick, steadied: hand-inked and hatched, on twos, with less boil and dust.",
    "Inked (yours)", { boil: 0.012, motes: 0.4, hatch: 0.15, sizeVar: 0.45 }, BODY.inked, { boil: 0.03, motes: 1.2, rate: 9, flash: 0.7, sparks: 2 }),
];
const FRONT = {
  bowShock: { frontRate: 5, frontY: 0.3, frontSize: 0.9, frontLife: 0.35, frontInherit: 1, frontKick: 1.2, pressureArcs: 1, arcSize: 0.8, impactStar: 0.25 },
  plough: { frontRate: 7, frontY: 0.15, frontSize: 1.1, frontLife: 0.5, frontInherit: 0.85, frontKick: 2.2, impactStar: 0.15 },
  sheath: { frontRate: 9, frontY: 0.35, frontSize: 0.6, frontLife: 0.22, frontInherit: 1.05, frontKick: 0.5, pressureArcs: 1, arcSize: 0.6,
    hotCore: 0.8, flash: 0.7, tint: 1 },
};
const BULWARK = [
  abc("Vanguard's slipstream, plus a shock cone the nose pushes ahead of it, sliding off the sides.",
    "Bow shock", { frontRate: 3, pressureArcs: 0, impactStar: 0 }, { ...BODY.slipstream, ...FRONT.bowShock },
    { frontRate: 8, pressureArcs: 2, impactStar: 0.45, frontKick: 1.8, flash: 0.7 }),
  abc("The nose ploughs through: streaks get shoved wide at the front and fall back along the flanks.",
    "Plough", { frontRate: 4, frontKick: 1.4 }, { ...BODY.wash, ...FRONT.plough }, { frontRate: 10, frontKick: 3, frontLife: 0.6, impactStar: 0.35 }),
  abc("A hot glowing sheath hugs the nose like re-entry, short-lived and bright.",
    "Heat sheath", { frontRate: 6, hotCore: 0.5, pressureArcs: 0 }, { ...BODY.slipstream, ...FRONT.sheath },
    { frontRate: 12, frontSize: 0.75, flash: 0.85, pressureArcs: 2 }),
  abc("Your inked Vanguard body, with a modest inked front.",
    "Inked + front", { frontRate: 3, impactStar: 0 }, { ...BODY.inked, ...FRONT.bowShock }, { frontRate: 8, frontKick: 2, impactStar: 0.4 }),
];

const VALIS_BASE = { ...REAL, ...CALM, endBreak: 1, chevrons: 0, actionLines: 0 };
const VALIS = [
  abc("A long, thin, smooth trail that grows and lingers, with a faint halo on the hull.",
    "Ion trail", { rate: 7, aura: 0.15, fill: 0.2 },
    { ...VALIS_BASE, rearY: -0.2, rate: 10, lifetime: 1, size: 0.35, sizeEnd: 1.2, inherit: 0.8, drag: 0.6, kick: 0.1, align: 0.9,
      speedStretch: 0.8, dashBurst: 6, sizeVar: 0.4, lenVar: 0.6, colorVar: 0.4, lines: 0.3, lineLen: 2, motes: 0.2, moteSize: 0.03,
      outline: 0, ink: 0.2, aura: 0.3, auraOffset: 0.045, auraBreaks: 0.4, twinkleRate: 2, twinkleSize: 0.08, sparks: 1 },
    { rate: 13, lifetime: 1.2, aura: 0.45, twinkleRate: 4, hotCore: 0.5, flash: 0.75 }),
  abc("The air around the hull wavers like heat, a soft aura and short faint streaks.",
    "Heat shimmer", { aura: 0.4, rate: 4 },
    { ...VALIS_BASE, rearY: 0, rate: 6, lifetime: 0.6, size: 0.5, sizeEnd: 1, inherit: 0.75, drag: 1.5, kick: 0.2, align: 0.9,
      speedStretch: 0.5, dashBurst: 3, sizeVar: 0.5, lenVar: 0.5, colorVar: 0.5, lines: 0.2, motes: 0.3, moteSize: 0.02,
      gradient: 0.95, fill: 0.15, edge: 0.5, aura: 0.6, auraOffset: 0.04, auraBreaks: 0.5, boil: 0.025 },
    { aura: 0.8, auraOffset: 0.06, rate: 8, boil: 0.035, twinkleRate: 2 }),
  abc("Engines run hot: a dense bright exhaust that spreads quickly, with a few embers.",
    "Afterburn", { rate: 9, hotCore: 0.5, motes: 0.3 },
    { ...VALIS_BASE, rearY: -0.35, rate: 14, lifetime: 0.45, size: 0.45, sizeEnd: 1.3, inherit: 0.6, drag: 2.5, kick: 0.2, align: 0.9,
      speedStretch: 0.7, dashBurst: 8, sizeVar: 0.4, lenVar: 0.5, colorVar: 0.5, hotCore: 0.8, flash: 0.8, lines: 0.4, motes: 0.6,
      moteSize: 0.025, aura: 0.25, twinkleRate: 4, twinkleSize: 0.08 },
    { rate: 18, lifetime: 0.55, aura: 0.35, motes: 1, sparks: 2 }),
  abc("Your Valis pick, settled: no snap overshoot, fewer sparks, the same growing trail.",
    "Yours, settled", { aura: 0.3, sparks: 1, rate: 10 },
    { ...VALIS_BASE, rearY: 0.31, rate: 14, lifetime: 1.06, size: 0.2, sizeEnd: 1.5, inherit: 0.82, drag: 0, kick: 0, align: 0.32,
      speedStretch: 0.79, dashBurst: 10, sizeVar: 0.46, lenVar: 0.81, colorVar: 0.52, hotCore: 0, lines: 0, motes: 0,
      snapTime: 0.15, overshoot: 1, stretch: 1.8, collapse: 0.53, sparks: 2, endBreak: 0.75,
      outline: 0, gradient: 0.88, additive: 1, fill: 0.44, edge: 0.96, edgePx: 1.5, tint: 1, flash: 0.8, ink: 0.18, inkPx: 2.9,
      boil: 0, hatch: 0, aura: 0.46, auraOffset: 0.045, auraBreaks: 0.33, twinkleSize: 0.12 },
    { aura: 0.6, sparks: 3, twinkleRate: 3, flash: 0.95 }),
];

const BLINK_BASE = { ...REAL, outline: 2, gradient: 0.6, sparks: 1, pieces: 0, spin: 0, shatterSpeed: 1, shatterLife: 0.3,
  moteSize: 0.025, snapTime: 0.06, collapse: 0.5, twinkleSize: 0.06 };
const CRIMSON = [
  abc("Space snaps shut on the ship: small doors, a quick pinch, a short puff where it lands.",
    "Air snap", { doorScale: 0.5, burst: 3, poofLines: 3, hullFlash: 0.3 },
    { ...BLINK_BASE, doorScale: 0.6, anticip: 0.1, squeeze: 0.15, pullLines: 8, pullRadius: 1, markAlpha: 0.25, markPulse: 0.1,
      pinchFade: 0.1, flare: 1.5, pinchShards: 3, departMotes: 10, openTime: 0.25, overshoot: 0.8, swing: 1.2, swingDeg: 6,
      burst: 6, burstSpeed: 1.8, burstSize: 0.5, arriveMotes: 8, hullFlash: 0.5, poofLines: 6, poofSize: 0.6, twinkles: 2 },
    { doorScale: 0.75, burst: 9, burstSpeed: 2.4, poofLines: 9, hullFlash: 0.7, sparks: 2 }),
  abc("A ghost of the ship hangs where it was and fades; the arrival is soft.",
    "Afterimage", { pinchFade: 0.22, departMotes: 8 },
    { ...BLINK_BASE, doorScale: 0.8, anticip: 0.05, squeeze: 0.4, pullLines: 4, pullRadius: 0.9, markAlpha: 0.4, markPulse: 0.08,
      pinchFade: 0.35, flare: 1.2, pinchShards: 2, departMotes: 16, openTime: 0.4, overshoot: 0.4, swing: 1.1, swingDeg: 4,
      burst: 3, burstSpeed: 1.2, burstSize: 0.45, arriveMotes: 6, hullFlash: 0.35, poofLines: 3, poofSize: 0.5, twinkles: 2,
      additive: 1, ink: 0.3, moteSize: 0.02 },
    { pinchFade: 0.5, departMotes: 24, hullFlash: 0.55, markAlpha: 0.55 }),
  abc("Air rushes in to fill the gap: lines pulled hard to the ship, then a sharp release at the landing.",
    "Implosion", { pullLines: 10, burst: 6, poofLines: 6 },
    { ...BLINK_BASE, doorScale: 0.7, anticip: 0, squeeze: 0.08, pullLines: 16, pullRadius: 1.6, markAlpha: 0.3, markPulse: 0.15,
      pinchFade: 0.14, flare: 2, pinchShards: 6, departMotes: 20, openTime: 0.2, overshoot: 0.7, swing: 1.25, swingDeg: 8,
      burst: 10, burstSpeed: 2.5, burstSize: 0.55, arriveMotes: 12, hullFlash: 0.8, poofLines: 10, poofSize: 0.9, twinkles: 3, sparks: 2 },
    { pullLines: 22, pullRadius: 2, burst: 14, poofLines: 14, hullFlash: 1, flare: 2.4 }),
  abc("Your doors, toned down: smaller, steadier lines, no shards, a slower open.",
    "Your doors, tamed", { doorScale: 0.5, poofLines: 6, inkPx: 1.6 },
    { ...BLINK_BASE, doorScale: 0.6, anticip: 0.05, squeeze: 0.08, pullLines: 10, pullRadius: 0.95, markAlpha: 0.15, markPulse: 0.15,
      pinchFade: 0.3, flare: 2, pinchShards: 5, departMotes: 18, openTime: 0.5, overshoot: 0.6, swing: 1, swingDeg: 10,
      burst: 8, burstSpeed: 1.2, burstSize: 0.8, arriveMotes: 12, hullFlash: 0.7, sparks: 2, shatterSpeed: 1.5, shatterLife: 0.2,
      poofLines: 10, poofSize: 0.9, twinkles: 4, twinkleSize: 0.05, gradient: 0.4, additive: 1, fill: 0.34, edge: 0.65, edgePx: 1.5,
      tint: 0.7, flash: 0.8, ink: 0.65, inkPx: 2, boil: 0.02, boilFps: 12, hatch: 0 },
    { doorScale: 0.8, poofLines: 14, burst: 12, inkPx: 2.6, hullFlash: 0.9 }),
];
const PRESETS = { vanguard_dash: VANGUARD, bulwark_ram: BULWARK, valis_overdrive: VALIS, crimson_blink: CRIMSON };
const VERSIONS = ["A", "B", "C"];

function applyPreset(s) {
  const m = MODES[ui.mode], locks = UIS.locks[ui.mode], cur = S[ui.mode];
  pushHistory();
  const next = Object.assign({}, m.d);
  for (const k in s) if (k in m.d) next[k] = s[k];
  for (const k in locks) if (locks[k] && k in cur) next[k] = cur[k];
  S[ui.mode] = next; buildEffectKnobs(); changed(); restartAbility();
}

// ---------- blend into the game ----------
const BLEND_KNOBS = [
  ["refract", "Bend the backdrop inside streaks", 0, 1, 0.01],
  ["refractPx", "Bend distance (screen px)", 0, 24, 0.5],
  ["localColor", "Take on the backdrop's colour", 0, 1, 0.01],
  ["coolAge", "…as it ages (0 at once, 1 only by the end)", 0, 1, 0.01],
  ["bands", "Flat tone bands (0 smooth gradient)", 0, 5, 1],
  ["shipInk", "Ink in the ship's own line colour", 0, 1, 0.01],
  ["rimLight", "Effect lights the hull's rear edge", 0, 1, 0.01],
];
const BLEND_OFF = { refract: 0, refractPx: 8, localColor: 0, coolAge: 0.7, bands: 0, shipInk: 0, rimLight: 0 };
const BLENDS = [
  abc("The streaks bend the space behind them like heat haze, so they're made of the scene, not painted over it.",
    "Bent space", { refract: 0.35, refractPx: 5, localColor: 0.15 }, { refract: 0.6, refractPx: 9, localColor: 0.25 }, { refract: 0.85, refractPx: 14, localColor: 0.35 }),
  abc("Born in the ship's colour, they cool into the colour of the nebula behind them as they age.",
    "Cools into space", { localColor: 0.35, coolAge: 0.85 }, { localColor: 0.6, coolAge: 0.8 }, { localColor: 0.85, coolAge: 0.7 }),
  abc("Drawn like the game's own effects: flat cel bands like the laser bolt, inked in the ship's line colour.",
    "House style", { bands: 2, shipInk: 0.5 }, { bands: 3, shipInk: 0.8 }, { bands: 3, shipInk: 1, localColor: 0.2 }),
  abc("The effect throws light back on the hull's rear edge, so it's physically there with the ship.",
    "Lit hull", { rimLight: 0.35, shipInk: 0.3 }, { rimLight: 0.6, shipInk: 0.5 }, { rimLight: 0.9, shipInk: 0.7 }),
  abc("All of it at once, kept in proportion.",
    "Everything", { refract: 0.3, refractPx: 6, localColor: 0.3, bands: 2, shipInk: 0.6, rimLight: 0.3 },
    { refract: 0.5, refractPx: 8, localColor: 0.5, bands: 3, shipInk: 0.8, rimLight: 0.5 },
    { refract: 0.7, refractPx: 11, localColor: 0.7, bands: 3, shipInk: 1, rimLight: 0.7 }),
];
function buildBlend() {
  const box = document.getElementById("blendKnobs");
  box.innerHTML = `<h2>Blend into the game</h2><p class="note">Shared by all four ships. Changes how the effect sits in the scene, not how it moves.</p>`;
  const pre = document.createElement("div"); pre.className = "presets";
  pre.innerHTML = `<div class="ptitle">Blends <span class="count">A quiet · B mid · C strong</span></div><div class="plist"></div><p class="pblurb note"></p>`;
  const plist = pre.querySelector(".plist"), pblurb = pre.querySelector(".pblurb");
  const sel = typeof UIS.blend === "string" ? UIS.blend : null;
  const off = document.createElement("div"); off.className = "prow";
  off.innerHTML = `<span class="pname">Off</span>`;
  const ob = document.createElement("button"); ob.type = "button"; ob.textContent = "–"; ob.setAttribute("aria-label", "Blend off");
  ob.setAttribute("aria-pressed", !sel);
  ob.addEventListener("click", () => { UIS.blend = null; saveUI(); Object.assign(W, BLEND_OFF); changed(); buildBlend(); });
  off.appendChild(ob); plist.appendChild(off);
  BLENDS.forEach((bl, i) => {
    const row = document.createElement("div"); row.className = "prow";
    row.innerHTML = `<span class="pname">${bl.name}</span>`;
    row.addEventListener("mouseenter", () => { pblurb.textContent = bl.blurb; });
    VERSIONS.forEach((vn, v) => {
      const b = document.createElement("button"); b.type = "button"; b.textContent = vn;
      b.setAttribute("aria-label", `${bl.name} ${vn}`); b.setAttribute("aria-pressed", sel === `${i}:${v}`);
      b.addEventListener("focus", () => { pblurb.textContent = bl.blurb; });
      b.addEventListener("click", () => { UIS.blend = `${i}:${v}`; saveUI(); Object.assign(W, BLEND_OFF, bl.v[v]); changed(); buildBlend(); });
      row.appendChild(b);
    });
    plist.appendChild(row);
  });
  pblurb.textContent = sel ? BLENDS[+sel.split(":")[0]].blurb : "Try each at B first, then A or C.";
  box.appendChild(pre);
  const body = document.createElement("div"); body.className = "gbody";
  box.appendChild(body);
  for (const r of BLEND_KNOBS) knobRow(body, r, W, changed, false);
}

// ---------- UI ----------
const UI_KEY = "keystone-trails-ui-v1";
let UIS = {};
try { UIS = JSON.parse(localStorage.getItem(UI_KEY) || "{}"); } catch (e) { UIS = {}; }
UIS.locks = UIS.locks || {}; UIS.open = UIS.open || {}; UIS.snaps = UIS.snaps || {}; UIS.amount = UIS.amount ?? 0.3;
const DEFAULT_LOCKS = ["boost", "range"];
for (const m in MODES) if (!UIS.locks[m]) UIS.locks[m] = Object.fromEntries(DEFAULT_LOCKS.map((k) => [k, true]));
function saveUI() { try { localStorage.setItem(UI_KEY, JSON.stringify(UIS)); } catch (e) {} }
const HIST = {};
const hist = () => (HIST[ui.mode] = HIST[ui.mode] || { back: [], fwd: [] });
function pushHistory() { const h = hist(); h.back.push(JSON.stringify(S[ui.mode])); if (h.back.length > 60) h.back.shift(); h.fwd = []; }

const LOCK_SVG = (on) => `<svg viewBox="0 0 16 16" width="13" height="13" aria-hidden="true"><rect x="3" y="7" width="10" height="7" rx="1.5" fill="currentColor"/><path d="${on ? "M5 7V5a3 3 0 0 1 6 0v2" : "M5 7V5a3 3 0 0 1 6 0"}" fill="none" stroke="currentColor" stroke-width="1.6"/></svg>`;

function groupsOf(list) {
  const groups = []; let g = null;
  for (const row of list) { if (row[0] === "#") { g = { name: row[1], rows: [] }; groups.push(g); } else g.rows.push(row); }
  return groups;
}
const decOf = (step) => (String(step).includes(".") ? String(step).split(".")[1].length : 0);

function knobRow(container, row, obj, onChange, lockable) {
  const [key, label, min, max, step] = row;
  const id = `k-${container.closest("[id]").id}-${key}`;
  const el = document.createElement("div"); el.className = "knob";
  const locked = lockable && UIS.locks[ui.mode][key];
  el.innerHTML = `<label for="${id}">${label}</label><output for="${id}"></output>` +
    (lockable ? `<button class="lock" type="button" aria-pressed="${!!locked}" title="Lock: randomize leaves it alone" aria-label="Lock ${label}">${LOCK_SVG(locked)}</button>` : "") +
    `<input id="${id}" type="range" min="${min}" max="${max}" step="${step}">`;
  if (locked) el.classList.add("locked");
  const inp = el.querySelector("input"), out = el.querySelector("output"), dec = decOf(step);
  inp.value = obj[key]; out.textContent = Number(obj[key]).toFixed(dec);
  let pushed = false;
  inp.addEventListener("pointerdown", () => { if (!pushed && lockable) { pushHistory(); pushed = true; } });
  inp.addEventListener("change", () => { pushed = false; });
  inp.addEventListener("input", () => { obj[key] = Number(inp.value); out.textContent = Number(inp.value).toFixed(dec); onChange(); });
  if (lockable) el.querySelector(".lock").addEventListener("click", (e) => {
    const b = e.currentTarget, on = !UIS.locks[ui.mode][key];
    UIS.locks[ui.mode][key] = on; b.setAttribute("aria-pressed", on); b.innerHTML = LOCK_SVG(on); el.classList.toggle("locked", on); saveUI(); refreshCounts();
  });
  container.appendChild(el);
}

function knobsFor(m) {
  if (m.effect === "blink") return [...UNIQUE_KNOBS.blink, ...HAND_KNOBS, ...BLINK_KNOBS];
  return [...UNIQUE_KNOBS[m.effect], ...HAND_KNOBS, ...SHED_KNOBS, ...(m.effect === "ram" ? FRONT_KNOBS : []), ...VARIATION_KNOBS, ...TAIL_KNOBS];
}

function randomize(rows) {
  const k = S[ui.mode], amt = UIS.amount, locks = UIS.locks[ui.mode];
  pushHistory();
  for (const [key, , min, max, step] of rows) {
    if (locks[key]) continue;
    let v = amt >= 0.99 ? min + Math.random() * (max - min) : k[key] + (Math.random() * 2 - 1) * amt * (max - min);
    v = Math.min(max, Math.max(min, v));
    v = min + Math.round((v - min) / step) * step;
    k[key] = Number(v.toFixed(decOf(step)));
  }
  buildEffectKnobs(); changed(); restartAbility();
}
function restartAbility() { ability = null; parts = parts.filter((p) => p.mode !== ui.mode || p.kind === "mote"); fire(); }
function restore(json) { S[ui.mode] = JSON.parse(json); buildEffectKnobs(); changed(); restartAbility(); }

function refreshCounts() {
  document.querySelectorAll("#effectKnobs details").forEach((d) => {
    const keys = JSON.parse(d.dataset.keys), n = keys.filter((x) => UIS.locks[ui.mode][x]).length;
    d.querySelector(".count").textContent = n ? `${n} locked` : "";
    d.querySelector(".glock").setAttribute("aria-pressed", n === keys.length);
  });
}

function buildEffectKnobs() {
  const box = document.getElementById("effectKnobs");
  const m = MODES[ui.mode], obj = S[ui.mode], rows = knobsFor(m);
  const flat = rows.filter((r) => r[0] !== "#");
  box.innerHTML = `<h2>${m.label}</h2>`;
  const note = document.createElement("p"); note.className = "note";
  note.textContent = `${m.windowName} window ${m.window} s, fixed by the game.` + (m.effect === "ram" ? " Same body as Vanguard's dash; only the front differs." : "");
  box.appendChild(note);

  const pre = document.createElement("div"); pre.className = "presets";
  pre.innerHTML = `<div class="ptitle">Starting points <span class="count">A quiet · B mid · C loud · P cycles</span></div><div class="plist"></div><p class="pblurb note"></p>`;
  const plist = pre.querySelector(".plist"), pblurb = pre.querySelector(".pblurb");
  const sel = typeof UIS.preset?.[ui.mode] === "string" ? UIS.preset[ui.mode] : null;
  PRESETS[ui.mode].forEach((pr, i) => {
    const row = document.createElement("div"); row.className = "prow";
    row.innerHTML = `<span class="pname">${pr.name}</span>`;
    row.addEventListener("mouseenter", () => { pblurb.textContent = pr.blurb; });
    VERSIONS.forEach((vn, v) => {
      const b = document.createElement("button"); b.type = "button"; b.textContent = vn;
      b.setAttribute("aria-label", `${pr.name} ${vn}`);
      b.setAttribute("aria-pressed", sel === `${i}:${v}`);
      b.addEventListener("focus", () => { pblurb.textContent = pr.blurb; });
      b.addEventListener("click", () => { (UIS.preset = UIS.preset || {})[ui.mode] = `${i}:${v}`; saveUI(); applyPreset(pr.v[v]); });
      row.appendChild(b);
    });
    plist.appendChild(row);
  });
  pblurb.textContent = sel ? PRESETS[ui.mode][+sel.split(":")[0]].blurb : "Pick one, then Randomize at a nudge to explore around it. Locks still hold.";
  box.appendChild(pre);

  const tools = document.createElement("div"); tools.className = "tools";
  const h = hist();
  tools.innerHTML = `<div class="row"><button type="button" class="fire" id="rnd">Randomize (R)</button>
      <button type="button" id="undo" ${h.back.length ? "" : "disabled"}>Undo</button><button type="button" id="redo" ${h.fwd.length ? "" : "disabled"}>Redo</button></div>
    <div class="knob amount"><label for="amt">Randomize by</label><output for="amt"></output><input id="amt" type="range" min="0.05" max="1" step="0.05"></div>
    <div class="row snaps"><button type="button" id="keep">Keep this</button><span class="chips"></span></div>`;
  box.appendChild(tools);
  const amt = tools.querySelector("#amt"), amtOut = tools.querySelector(".amount output");
  const amtLabel = (v) => (v >= 0.99 ? "anything" : v <= 0.15 ? "a nudge" : `±${Math.round(v * 100)}%`);
  amt.value = UIS.amount; amtOut.textContent = amtLabel(UIS.amount);
  amt.addEventListener("input", () => { UIS.amount = Number(amt.value); amtOut.textContent = amtLabel(UIS.amount); saveUI(); });
  tools.querySelector("#rnd").addEventListener("click", () => randomize(flat));
  tools.querySelector("#undo").addEventListener("click", () => { const hh = hist(); if (!hh.back.length) return; hh.fwd.push(JSON.stringify(S[ui.mode])); restore(hh.back.pop()); });
  tools.querySelector("#redo").addEventListener("click", () => { const hh = hist(); if (!hh.fwd.length) return; hh.back.push(JSON.stringify(S[ui.mode])); restore(hh.fwd.pop()); });
  const snaps = (UIS.snaps[ui.mode] = UIS.snaps[ui.mode] || []);
  const chips = tools.querySelector(".chips");
  snaps.forEach((sn, i) => {
    const c = document.createElement("span"); c.className = "chip";
    c.innerHTML = `<button type="button" class="load" title="Load kept settings ${sn.name}">${sn.name}</button><button type="button" class="del" aria-label="Forget ${sn.name}">×</button>`;
    c.querySelector(".load").addEventListener("click", () => { pushHistory(); restore(JSON.stringify(sn.s)); });
    c.querySelector(".del").addEventListener("click", () => { snaps.splice(i, 1); saveUI(); buildEffectKnobs(); });
    chips.appendChild(c);
  });
  tools.querySelector("#keep").addEventListener("click", () => {
    const used = new Set(snaps.map((s) => s.name));
    const name = "ABCDEFGHIJKLMNOPQRSTUVWXYZ".split("").find((c) => !used.has(c)) || String(snaps.length + 1);
    snaps.push({ name, s: JSON.parse(JSON.stringify(obj)) }); if (snaps.length > 12) snaps.shift();
    saveUI(); buildEffectKnobs();
  });

  const open = (UIS.open[ui.mode] = UIS.open[ui.mode] || {});
  groupsOf(rows).forEach((g, gi) => {
    const d = document.createElement("details");
    d.open = open[g.name] ?? gi < 2;
    d.dataset.keys = JSON.stringify(g.rows.map((r) => r[0]));
    d.innerHTML = `<summary><span class="gname">${g.name}</span><span class="count"></span>
      <button type="button" class="glock" title="Lock or unlock the whole group" aria-label="Lock group ${g.name}">${LOCK_SVG(true)}</button>
      <button type="button" class="gshuffle" title="Randomize just this group">Shuffle</button></summary>`;
    d.addEventListener("toggle", () => { open[g.name] = d.open; saveUI(); });
    d.querySelector(".gshuffle").addEventListener("click", (e) => { e.preventDefault(); randomize(g.rows); });
    d.querySelector(".glock").addEventListener("click", (e) => {
      e.preventDefault();
      const all = g.rows.every((r) => UIS.locks[ui.mode][r[0]]);
      for (const r of g.rows) UIS.locks[ui.mode][r[0]] = !all;
      saveUI(); buildEffectKnobs();
    });
    const body = document.createElement("div"); body.className = "gbody";
    d.appendChild(body);
    box.appendChild(d);
    for (const r of g.rows) knobRow(body, r, obj, changed, true);
  });
  refreshCounts();
}
function buildWorldKnobs() { const box = document.getElementById("worldKnobs"); for (const r of WORLD_KNOBS) knobRow(box, r, W, changed, false); }
function changed() { persist(); exportText(); }
function exportText() { document.getElementById("export").value = JSON.stringify({ world: W, modes: S }, null, 1); }
addEventListener("keydown", (e) => {
  if ((e.key === "r" || e.key === "R") && !e.ctrlKey && !e.metaKey && e.target.tagName !== "TEXTAREA" && e.target.tagName !== "INPUT") {
    randomize(knobsFor(MODES[ui.mode]).filter((r) => r[0] !== "#"));
  }
  if ((e.key === "p" || e.key === "P") && !e.ctrlKey && !e.metaKey && e.target.tagName !== "TEXTAREA" && e.target.tagName !== "INPUT") {
    const list = PRESETS[ui.mode], flat = list.flatMap((pr, i) => VERSIONS.map((_, v) => `${i}:${v}`));
    const next = flat[(flat.indexOf(UIS.preset?.[ui.mode]) + 1) % flat.length], [i, v] = next.split(":").map(Number);
    (UIS.preset = UIS.preset || {})[ui.mode] = next; saveUI(); applyPreset(list[i].v[v]);
  }
});

const modesBox = document.getElementById("modes");
for (const k in MODES) {
  const b = document.createElement("button"); b.textContent = MODES[k].label; b.dataset.mode = k; b.setAttribute("aria-pressed", k === ui.mode);
  b.addEventListener("click", () => { ui.mode = k; ability = null; parts = []; lastFire = ship.t; modesBox.querySelectorAll("button").forEach((x) => x.setAttribute("aria-pressed", x.dataset.mode === k)); buildEffectKnobs(); });
  modesBox.appendChild(b);
}
document.getElementById("fire").addEventListener("click", fire);
addEventListener("keydown", (e) => { if (e.code === "Space" && e.target.tagName !== "TEXTAREA") { e.preventDefault(); fire(); } });
const autoB = document.getElementById("auto");
autoB.addEventListener("click", () => { ui.auto = !ui.auto; autoB.setAttribute("aria-pressed", ui.auto); });
const pauseB = document.getElementById("pause");
pauseB.setAttribute("aria-pressed", ui.paused);
pauseB.addEventListener("click", () => { ui.paused = !ui.paused; pauseB.setAttribute("aria-pressed", ui.paused); });
document.querySelectorAll("[data-speed]").forEach((b) => b.addEventListener("click", () => { ui.speed = Number(b.dataset.speed); document.querySelectorAll("[data-speed]").forEach((x) => x.setAttribute("aria-pressed", x === b)); }));
document.querySelectorAll("[data-zoom]").forEach((b) => b.addEventListener("click", () => { ui.zoom = Number(b.dataset.zoom); document.querySelectorAll("[data-zoom]").forEach((x) => x.setAttribute("aria-pressed", x === b)); }));
document.getElementById("copy").addEventListener("click", async () => {
  const ta = document.getElementById("export"), btn = document.getElementById("copy");
  try { await navigator.clipboard.writeText(ta.value); btn.textContent = "Copied"; }
  catch (e) { ta.focus(); ta.select(); btn.textContent = "Selected, press Ctrl+C"; }
  setTimeout(() => (btn.textContent = "Copy settings"), 1800);
});
document.getElementById("reset").addEventListener("click", () => { pushHistory(); S[ui.mode] = Object.assign({}, MODES[ui.mode].d); buildEffectKnobs(); changed(); });

buildEffectKnobs(); buildBlend(); buildWorldKnobs(); exportText(); resize();
requestAnimationFrame(frame);
