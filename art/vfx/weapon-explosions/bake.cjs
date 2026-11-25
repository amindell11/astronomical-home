const fs = require('node:fs');
const path = require('node:path');
const {pathToFileURL} = require('node:url');
const {chromium} = require('playwright');

async function main() {
  const settingsPath = process.argv[2] || path.join(__dirname, 'settings.json');
  const output = path.resolve(process.argv[3] || path.join(__dirname, 'out'));
  const settings = JSON.parse(fs.readFileSync(settingsPath, 'utf8'));
  for (const key of ['missileFireSeconds', 'missileTailSeconds', 'mineSweepSeconds', 'mineIgnitionSeconds', 'missileSpread', 'smokeOpacity', 'mineFill', 'mineGlow', 'mineMotion', 'mineCorePulse']) {
    if (typeof settings[key] !== 'number' || !Number.isFinite(settings[key]) || settings[key] < 0)
      throw new Error(`Invalid numeric setting: ${key}`);
  }
  for (const key of ['missileFireSeconds', 'missileTailSeconds', 'mineSweepSeconds', 'mineIgnitionSeconds', 'missileSpread']) {
    if (settings[key] === 0) throw new Error(`Setting must be positive: ${key}`);
  }
  for (const key of ['smokeOpacity', 'mineFill', 'mineGlow', 'mineMotion', 'mineCorePulse']) {
    if (settings[key] > 1) throw new Error(`Opacity must not exceed one: ${key}`);
  }
  const sources = Object.fromEntries(['missile', 'mine'].map(name => [name,
    `data:image/png;base64,${fs.readFileSync(path.join(__dirname, 'source', `${name}.png`)).toString('base64')}`]));
  const html = fs.readFileSync(path.join(__dirname, 'tuner.template.html'), 'utf8')
    .replace('/* EFFECTS */', () => fs.readFileSync(path.join(__dirname, 'effects.js'), 'utf8'))
    .replace('/* SETTINGS */', () => JSON.stringify(settings))
    .replace('/* SOURCES */', () => JSON.stringify(sources));
  fs.mkdirSync(output, {recursive:true});
  fs.writeFileSync(path.join(output, 'preview.html'), html);
  const browser = await chromium.launch({channel:'msedge', headless:true});
  try {
    const page = await browser.newPage({viewport:{width:1120, height:1080}});
    const errors = [];
    page.on('pageerror', error => errors.push(error.message));
    await page.goto(pathToFileURL(path.join(output, 'preview.html')).href);
    await page.waitForFunction(() => window.explosionPreview);
    const textures = await page.evaluate(() => Object.fromEntries(Object.entries(explosionPreview.textures())
      .map(([name, canvas]) => [name, canvas.toDataURL('image/png').split(',')[1]])));
    for (const [name, data] of Object.entries(textures)) fs.writeFileSync(path.join(output, `${name}.png`), Buffer.from(data, 'base64'));
    for (const time of [0.04, 0.12, 0.26, 0.48, 0.78]) {
      await page.evaluate(time => explosionPreview.render(time), time);
      await page.locator('#preview').screenshot({path:path.join(output, `preview-${Math.round(time * 1000)}.png`)});
    }
    await page.locator('summary').filter({hasText:'Source layers'}).click();
    await page.locator('#debug').screenshot({path:path.join(output, 'source-layers.png')});
    const frameDir = path.join(output, 'frames');
    fs.mkdirSync(frameDir, {recursive:true});
    for (let frame = 0; frame < 34; frame++) {
      await page.evaluate(time => explosionPreview.render(time), frame * 0.04);
      await page.locator('#preview').screenshot({path:path.join(frameDir, `${String(frame).padStart(3, '0')}.png`)});
    }
    if (errors.length) throw new Error(errors.join('\n'));
    const scales = await page.evaluate(() => WeaponExplosions.atlasScales(explosionPreview.settings));
    fs.writeFileSync(path.join(output, 'settings.json'), JSON.stringify({...settings, ...scales}, null, 2) + '\n');
    process.stdout.write(JSON.stringify({output, textures:Object.keys(textures), frames:34, browserErrors:errors}) + '\n');
  } finally {await browser.close();}
}
main().catch(error => {console.error(error); process.exitCode = 1;});
