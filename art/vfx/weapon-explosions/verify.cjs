const fs = require('node:fs');
const path = require('node:path');
const assert = require('node:assert/strict');
const {chromium} = require('playwright');

async function main() {
  const renderer = fs.readFileSync(process.argv[2] || path.join(__dirname, 'effects.js'), 'utf8');
  const sources = Object.fromEntries(['missile', 'mine'].map(name => [name,
    `data:image/png;base64,${fs.readFileSync(path.join(__dirname, 'source', `${name}.png`)).toString('base64')}`]));
  const settings = JSON.parse(fs.readFileSync(path.join(__dirname, 'settings.json'), 'utf8'));
  const browser = await chromium.launch({channel:'msedge', headless:true});
  try {
    const page = await browser.newPage();
    await page.addScriptTag({content:renderer});
    const result = await page.evaluate(async ({sources, settings}) => {
      await WeaponExplosions.load(sources);
      const layers = Object.fromEntries(Object.entries(WeaponExplosions.layers).map(([name, layer]) => {
        const {width, height} = layer;
        const pixels = layer.getContext('2d').getImageData(0, 0, width, height).data;
        let borderPaint = 0, strongPaint = 0, extremeJumps = 0;
        for (let y = 0; y < height; y++) for (let x = 0; x < width; x++) {
          const i = (y * width + x) * 4, alpha = pixels[i + 3];
          if (alpha > 8 && (x === 0 || y === 0 || x === width - 1 || y === height - 1)) borderPaint++;
          if (alpha > 220) strongPaint++;
          if (x + 1 < width && Math.abs(alpha - pixels[i + 7]) > 230) extremeJumps++;
          if (y + 1 < height && Math.abs(alpha - pixels[i + width * 4 + 3]) > 230) extremeJumps++;
        }
        return [name, {borderPaint, strongPaint, extremeJumps}];
      }));
      const core = WeaponExplosions.layers.core;
      const corePixels = core.getContext('2d').getImageData(0, 0, core.width, core.height).data;
      const tipRows = Array.from({length:core.height}, (_, y) => {
        let width = 0;
        for (let x = 0; x < core.width; x++) if (corePixels[(y * core.width + x) * 4 + 3] > 32) width++;
        return width;
      });
      const tipStart = tipRows.findIndex(width => width > 0);
      const tipEnd = tipRows.findLastIndex(width => width > 0);
      const fireEdge = Array.from(WeaponExplosions.layers.breakup.getContext('2d').getImageData(225, 121, 1, 1).data);
      const disc = WeaponExplosions.layers.disc;
      const pixels = disc.getContext('2d').getImageData(0, 0, 512, 512).data;
      const rimRadii = Array.from({length:36}, (_, angle) => {
        const theta = angle * Math.PI / 18;
        let peak = 0, edge = 0;
        for (let radius = 200; radius < 245; radius += 0.5) {
          const x = Math.round(255.5 + Math.cos(theta) * radius), y = Math.round(255.5 + Math.sin(theta) * radius);
          const i = (y * 512 + x) * 4, brightness = (pixels[i + 1] + pixels[i + 2]) * pixels[i + 3];
          if (brightness > peak) {peak = brightness; edge = radius;}
        }
        return edge;
      });
      const atlases = [];
      for (const missileSpread of [0.7, 1, 1.3]) {
        for (const [name, sheet] of Object.entries(WeaponExplosions.textures({...settings, missileSpread}))) {
          const pixels = sheet.getContext('2d').getImageData(0, 0, sheet.width, sheet.height).data;
          let borderAlpha = 0, finalAlpha = 0;
          for (let y = 0; y < sheet.height; y++) for (let x = 0; x < sheet.width; x++) {
            const alpha = pixels[(y * sheet.width + x) * 4 + 3];
            if (x % 256 === 0 || x % 256 === 255 || y % 256 === 0 || y % 256 === 255) borderAlpha += alpha;
            if (x >= sheet.width - 256 && y >= sheet.height - 256) finalAlpha += alpha;
          }
          atlases.push({name, missileSpread, borderAlpha, finalAlpha});
        }
      }
      return {layers, coreTips:{start:tipStart, end:tipEnd, widths:[tipRows[tipStart], tipRows[tipEnd]]}, fireEdge, rimRadii, atlases};
    }, {sources, settings});
    console.log(JSON.stringify(result));
    for (const [name, layer] of Object.entries(result.layers)) assert.equal(layer.borderPaint, 0, `${name} is cut at extraction bounds`);
    assert.ok(result.layers.core.strongPaint > 100, 'Core must retain its bright center');
    assert.ok(result.coreTips.end - result.coreTips.start >= 150, 'Core must retain the complete outer tips');
    assert.ok(result.coreTips.widths.every(width => width <= 3), 'Core tips must end in points');
    assert.ok(result.fireEdge[0] > result.fireEdge[1] * 1.8 && result.fireEdge[3] > 0, 'Partial fire selection must retain its red hue');
    assert.equal(result.layers.flash.extremeJumps, 0, 'Flash alpha has opaque-to-transparent steps');
    assert.ok(result.rimRadii.every(radius => Math.abs(radius - 230.4) < 4), 'Disc rim must follow the circular wave');
    for (const atlas of result.atlases) assert.equal(atlas.borderAlpha + atlas.finalAlpha, 0, `${atlas.name} atlas has clipped or unfinished frames`);
  } finally {await browser.close();}
}
main().catch(error => {console.error(error.message); process.exitCode = 1;});
