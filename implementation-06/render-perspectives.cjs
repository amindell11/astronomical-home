// Writes preview files; stdout names their directory and frame count. Errors exit nonzero.
const fs = require('node:fs');
const path = require('node:path');
const {pathToFileURL} = require('node:url');
const {chromium} = require('playwright');

(async () => {
  const output = path.resolve('results/weapon-explosions/perspective');
  fs.mkdirSync(path.join(output, 'frames'), {recursive:true});
  const source = fs.readFileSync('art/vfx/weapon-explosions/out/preview.html', 'utf8');
  const start = source.indexOf('  function render(time) {');
  const end = source.indexOf('  const debug =', start);
  const render = `  function render(time) {
    ctx.fillStyle = '#07121f'; ctx.fillRect(0, 0, 1024, 500);
    const titles = ['Overhead', 'Oblique', 'Oblique + range ripple'];
    const progress = Math.max(0, Math.min(1, time / settings.mineSweepSeconds));
    for (let column = 0; column < 3; column++) {
      const x = 172 + column * 340;
      label(titles[column], x, 38);
      for (const [y, size] of [[205, 330], [403, 130]]) {
        ctx.save(); ctx.translate(x, y);
        if (column === 2 && progress < 1) {
          ctx.beginPath(); ctx.arc(0, 0, size * 0.45 * progress, 0, Math.PI * 2);
          ctx.strokeStyle = 'rgba(105,199,239,' + (0.28 * Math.min(1, (1 - progress) / 0.18)) + ')';
          ctx.lineWidth = 1; ctx.stroke();
        }
        WeaponExplosions.mine(ctx, time, size, {...settings, mineRangeOpacity:0}, column ? 0.28 : 1);
        ctx.restore();
      }
      label('Game scale', x, 340, 14);
    }
    label(Math.round(time * 1000) + ' ms', 970, 475, 14);
    scrub.value = Math.min(time, 0.8) * 1000;
    document.getElementById('time').textContent = Math.round(time * 1000) + ' ms';
  }
`;
  const html = (source.slice(0, start) + render + source.slice(end))
    .replace('<h1>Missile and mine explosions</h1>', '<h1>Mine perspective comparison</h1>');
  const preview = path.join(output, 'preview.html');
  fs.writeFileSync(preview, html);
  const browser = await chromium.launch({channel:'msedge', headless:true});
  try {
    const page = await browser.newPage({viewport:{width:1120, height:1080}});
    const errors = [];
    page.on('pageerror', error => errors.push(error.message));
    await page.goto(pathToFileURL(preview).href);
    await page.waitForFunction(() => window.explosionPreview);
    for (const time of [0.04, 0.10, 0.18, 0.30, 0.50]) {
      await page.evaluate(time => explosionPreview.render(time), time);
      await page.locator('#preview').screenshot({path:path.join(output, 'preview-' + Math.round(time * 1000) + '.png')});
    }
    for (let frame = 0; frame < 30; frame++) {
      await page.evaluate(time => explosionPreview.render(time), frame * 0.04);
      await page.locator('#preview').screenshot({path:path.join(output, 'frames', String(frame).padStart(3, '0') + '.png')});
    }
    if (errors.length) throw new Error(errors.join('\n'));
    console.log(JSON.stringify({output, frames:30}));
  } finally {await browser.close();}
})().catch(error => {console.error(error); process.exitCode = 1;});
