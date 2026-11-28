// Bakes the engine flame flipbook from tuner.template.html with settings.json.
// usage: node bake.js <outDir>   (needs playwright; see README)
const fs = require('fs'), path = require('path');
const { chromium } = require('playwright');
const FW = 128, FH = 320, COLS = 4;
(async () => {
  const out = path.resolve(process.argv[2] || 'out');
  fs.mkdirSync(out, { recursive: true });
  const S = JSON.parse(fs.readFileSync(path.join(__dirname, 'settings.json'), 'utf8'));
  const html = fs.readFileSync(path.join(__dirname, 'tuner.template.html'), 'utf8');
  const b = await chromium.launch();
  const p = await b.newPage();
  await p.setContent('<!doctype html><html><head><meta charset=utf-8></head><body>' + html + '</body></html>');
  const res = await p.evaluate(({ S, FW, FH, COLS }) => {
    K = { ...DEF, ...S }; W = FW; H = FH;
    const n = K.frames, rows = Math.ceil(n / COLS);
    const bands = document.createElement('canvas'); bands.width = FW * COLS; bands.height = FH * rows;
    const glow = document.createElement('canvas'); glow.width = bands.width; glow.height = bands.height;
    const bx = bands.getContext('2d'), gx = glow.getContext('2d');
    bx.fillStyle = '#000'; bx.fillRect(0, 0, bands.width, bands.height);
    gx.fillStyle = '#000'; gx.fillRect(0, 0, glow.width, glow.height);
    for (let f = 0; f < n; f++) {
      const c = drawFrame(f, '#ffffff', true), x = (f % COLS) * FW, y = Math.floor(f / COLS) * FH;
      bx.drawImage(c, x, y);
      // glow: blurred rim coverage, clipped to its own cell
      const r = document.createElement('canvas'); r.width = FW; r.height = FH;
      const rx = r.getContext('2d'); rx.drawImage(c, 0, 0);
      const id = rx.getImageData(0, 0, FW, FH), d = id.data;
      for (let i = 0; i < d.length; i += 4) { d[i + 1] = d[i]; d[i + 2] = d[i]; }
      rx.putImageData(id, 0, 0);
      gx.save(); gx.beginPath(); gx.rect(x, y, FW, FH); gx.clip();
      gx.filter = `blur(${K.glowRadius * FW / 96}px)`; gx.drawImage(r, x, y); gx.restore();
    }
    return { bands: bands.toDataURL('image/png'), glow: glow.toDataURL('image/png'), rows, K };
  }, { S, FW, FH, COLS });
  const save = (n, u) => fs.writeFileSync(path.join(out, n), Buffer.from(u.split(',')[1], 'base64'));
  save('EngineFlame_Bands.png', res.bands);
  save('EngineFlame_Glow.png', res.glow);
  const K = res.K;
  const values = {
    flipbook: { columns: COLS, rows: res.rows, frames: K.frames },
    material: { _RimShade: K.rimShade, _RimAlpha: K.rimAlpha, _MidAlpha: K.midAlpha, _Glow: K.glow },
    motion: { fps: K.fps, randomOrder: !!K.randomOrder, lengthWobble: K.lengthWobble, widthWobble: K.widthWobble,
      igniteTime: K.igniteTime, cutoffTime: K.cutoffTime, throttleLength: K.throttleLength, pilotLength: S.pilotLength },
  };
  fs.writeFileSync(path.join(out, 'values.json'), JSON.stringify(values, null, 1));
  console.log(JSON.stringify(values, null, 1));
  await b.close();
})();
