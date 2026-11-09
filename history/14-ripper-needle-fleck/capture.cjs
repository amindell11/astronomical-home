const { chromium } = require('playwright');
const fs = require('node:fs');
const path = require('node:path');

(async () => {
  const browser = await chromium.launch({ executablePath: 'C:/Users/amind/AppData/Local/ms-playwright/chromium-1223/chrome-win64/chrome.exe', headless: true });
  try {
    const page = await browser.newPage({ viewport: { width: 768, height: 640 }, deviceScaleFactor: 1 });
    const errors = [];
    page.on('pageerror', error => errors.push(error.message));
    await page.goto('file:///' + path.join(__dirname, 'preview.html').replaceAll('\\', '/'));
    const frame = page.frames().find(candidate => candidate !== page.mainFrame());
    await frame.waitForFunction(() => Boolean(window.needleFleckStudy));
    const readTime = () => frame.evaluate(() => window.needleFleckStudy.getState().elapsed);
    const first = await readTime();
    await page.waitForTimeout(100);
    const playing = await readTime();
    await frame.locator('[data-play]').click();
    const stopped = await readTime();
    await page.waitForTimeout(100);
    const paused = await readTime();
    await frame.locator('[data-play]').click();
    await page.waitForTimeout(100);
    const resumed = await readTime();
    await frame.locator('[data-rate]').selectOption('0.25');
    const quarterRate = await frame.evaluate(() => window.needleFleckStudy.getState().rate);
    await frame.locator('[data-rate]').selectOption('1');
    await frame.evaluate(() => { window.needleFleckStudy.stop(); window.needleFleckStudy.renderAt(.18); });
    const visual = frame.locator('#ripper-needle-fleck-1108');
    await visual.screenshot({ path: path.join(__dirname, 'selected-preview.png') });
    console.log('PNG ready: ' + path.join(__dirname, 'selected-preview.png'));
    const source = await frame.evaluate(() => window.needleFleckStudy.getSource());
    fs.writeFileSync(path.join(__dirname, 'selected-source.json'), JSON.stringify(source, null, 2) + '\n');
    const previousSource = fs.readFileSync(path.join(__dirname, '../13-ripper-tracer-directions/ripper-tracer-directions.html'), 'utf8');
    const paletteText = previousSource.match(/const palettes = (\{[\s\S]*?\n  \});/)[1];
    const pale = new Function('return (' + paletteText + ').pale;')();
    const layers = [];
    const ctx = { save() {}, restore() {} };
    const start = previousSource.indexOf('  function drawShape(');
    const end = previousSource.indexOf('  function fitCanvas(', start);
    const originalDraw = new Function('polygon', previousSource.slice(start, end) + '; return drawShape;')((canvas, points, color, alpha = 1) => layers.push({ color, alpha, points }));
    originalDraw(ctx, 'A', pale, .18);
    const exactGeometry = JSON.stringify(layers) === JSON.stringify(source.layers.map(layer => ({ color: source.palette[layer.color], alpha: layer.alpha, points: layer.points })));
    const exactGlow = ctx.shadowBlur === source.shadowBlur && ctx.shadowColor === source.palette[source.shadowColor];
    const sizes = [];
    for (const width of [736, 320]) {
      await page.setViewportSize({ width: width + 32, height: 640 });
      await page.waitForTimeout(100);
      sizes.push(await frame.evaluate(() => ({ width: innerWidth, scrollWidth: document.documentElement.scrollWidth, height: document.getElementById('ripper-needle-fleck-1108').getBoundingClientRect().height })));
    }
    await page.setViewportSize({ width: 768, height: 640 });
    await page.waitForTimeout(100);
    await frame.evaluate(() => window.needleFleckStudy.renderAt(.18));
    await visual.screenshot({ path: path.join(__dirname, 'selected-preview.png') });
    const selectedState = await frame.evaluate(() => {
      dispatchEvent(new CustomEvent('openai:set_globals', { detail: { globals: { widgetState: { modelContent: { study: 'ripper-tracer-directions-2026-11-08', direction: 'A', palette: 'yellow' }, privateContent: { rate: .25 } } } } }));
      return window.needleFleckStudy.getState();
    });
    if (process.argv.includes('--frames')) {
      await frame.locator('[data-play]').click();
      await frame.evaluate(() => window.needleFleckStudy.freeze());
      fs.mkdirSync(path.join(__dirname, 'frames'), { recursive: true });
      for (let i = 0; i < 72; i++) {
        await frame.evaluate(time => window.needleFleckStudy.renderAt(time), i / 24);
        await visual.screenshot({ path: path.join(__dirname, 'frames', String(i).padStart(3, '0') + '.png') });
      }
    }
    const reducedContext = await browser.newContext({ reducedMotion: 'reduce', viewport: { width: 768, height: 640 } });
    const reducedPage = await reducedContext.newPage();
    await reducedPage.goto('file:///' + path.join(__dirname, 'preview.html').replaceAll('\\', '/'));
    const reducedFrame = reducedPage.frames().find(candidate => candidate !== reducedPage.mainFrame());
    await reducedFrame.waitForFunction(() => Boolean(window.needleFleckStudy));
    const reducedMotionState = await reducedFrame.evaluate(() => ({ mediaMatches: matchMedia('(prefers-reduced-motion: reduce)').matches, state: window.needleFleckStudy.getState() }));
    const reducedMotionPaused = reducedMotionState.mediaMatches && !reducedMotionState.state.playing;
    await reducedContext.close();
    const checks = {
      errors,
      autoplay: playing > first,
      pause: paused === stopped,
      resume: resumed > paused,
      quarterSpeed: quarterRate === .25,
      reducedMotionPaused,
      selectedPaletteFixed: selectedState.palette === 'pale' && selectedState.direction === 'A' && selectedState.rate === 1,
      exactGeometry,
      exactGlow,
      sizes
    };
    if (!reducedMotionPaused) console.log(JSON.stringify({ reducedMotionState }));
    fs.writeFileSync(path.join(__dirname, 'checks.json'), JSON.stringify(checks, null, 2) + '\n');
    console.log(JSON.stringify(checks));
    if (errors.length || Object.entries(checks).some(([key, value]) => key !== 'errors' && key !== 'sizes' && value !== true) || sizes.some(size => size.scrollWidth > size.width || size.height > 500)) throw new Error('Selected preview check failed.');
  } finally {
    await browser.close();
  }
})().catch(error => { console.error(error); process.exitCode = 1; });
