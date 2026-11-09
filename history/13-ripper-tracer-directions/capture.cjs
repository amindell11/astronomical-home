const { chromium } = require('playwright');
const path = require('node:path');
const fs = require('node:fs');

(async () => {
  const browser = await chromium.launch({
    executablePath: 'C:/Users/amind/AppData/Local/ms-playwright/chromium-1223/chrome-win64/chrome.exe',
    headless: true
  });
  try {
    const page = await browser.newPage({ viewport: { width: 768, height: 1400 }, deviceScaleFactor: 1 });
    const errors = [];
    page.on('pageerror', error => errors.push(error.message));
    await page.goto('file:///' + path.join(__dirname, 'preview.html').replaceAll('\\', '/'));
    const frame = page.frames().find(candidate => candidate !== page.mainFrame());
    await frame.waitForFunction(() => Boolean(window.ripperStudy));
    const initialTime = await frame.evaluate(() => window.ripperStudy.getState().elapsed);
    await page.waitForTimeout(120);
    const automaticTime = await frame.evaluate(() => window.ripperStudy.getState().elapsed);
    await frame.locator('[data-control="play"]').click();
    const pauseTime = await frame.evaluate(() => window.ripperStudy.getState().elapsed);
    await page.waitForTimeout(120);
    const pausedTime = await frame.evaluate(() => window.ripperStudy.getState().elapsed);
    await frame.locator('[data-control="play"]').click();
    await page.waitForTimeout(120);
    const resumedTime = await frame.evaluate(() => window.ripperStudy.getState().elapsed);
    await frame.evaluate(() => { window.ripperStudy.stop(); window.ripperStudy.renderAt(.18); });
    await page.waitForTimeout(300);
    const visual = frame.locator('#ripper-directions-1108');
    await visual.screenshot({ path: path.join(__dirname, 'directions.png') });
    console.log('First render: ' + path.join(__dirname, 'directions.png'));
    const beforePalette = await frame.locator('[data-scene="B"]').evaluate(canvas => canvas.toDataURL());
    await frame.locator('[data-control="palette"]').selectOption('amber');
    const afterPalette = await frame.locator('[data-scene="B"]').evaluate(canvas => canvas.toDataURL());
    await frame.locator('[data-direction="F"]').click();
    const selected = await frame.evaluate(() => window.ripperStudy.getState());
    await frame.locator('[data-control="rate"]').selectOption('0.25');
    await frame.locator('[data-control="play"]').click();
    await page.waitForTimeout(150);
    const playing = await frame.evaluate(() => window.ripperStudy.getState());
    await frame.locator('[data-control="play"]').click();
    await frame.locator('[data-control="rate"]').selectOption('1');
    await frame.evaluate(() => { window.ripperStudy.stop(); window.ripperStudy.setPalette('pale'); window.ripperStudy.setSelected('B'); window.ripperStudy.renderAt(.18); });
    const sizes = [];
    for (const width of [736, 510, 320]) {
      await page.setViewportSize({ width: width + 32, height: 1700 });
      await page.waitForTimeout(100);
      sizes.push(await frame.evaluate(() => ({
        width: innerWidth,
        scrollWidth: document.documentElement.scrollWidth,
        panels: Array.from(document.querySelectorAll('[data-scene]')).map(canvas => ({ width: canvas.getBoundingClientRect().width, height: canvas.getBoundingClientRect().height }))
      })));
      if (width === 320) await visual.screenshot({ path: path.join(__dirname, 'directions-mobile.png') });
    }
    await page.setViewportSize({ width: 768, height: 1400 });
    await page.waitForTimeout(100);
    await frame.evaluate(() => window.ripperStudy.renderAt(.18));
    await visual.screenshot({ path: path.join(__dirname, 'directions.png') });
    if (process.argv.includes('--frames')) {
      await frame.locator('[data-control="play"]').click();
      await frame.evaluate(() => window.ripperStudy.freeze());
      const frameDir = path.join(__dirname, 'frames');
      fs.mkdirSync(frameDir, { recursive: true });
      for (let i = 0; i < 72; i++) {
        await frame.evaluate(time => window.ripperStudy.renderAt(time), i / 24);
        await visual.screenshot({ path: path.join(frameDir, String(i).padStart(3, '0') + '.png') });
      }
    }
    const reducedContext = await browser.newContext({ reducedMotion: 'reduce', viewport: { width: 768, height: 1400 } });
    const reducedPage = await reducedContext.newPage();
    await reducedPage.goto('file:///' + path.join(__dirname, 'preview.html').replaceAll('\\', '/'));
    const reducedFrame = reducedPage.frames().find(candidate => candidate !== reducedPage.mainFrame());
    await reducedFrame.waitForFunction(() => Boolean(window.ripperStudy));
    const reducedState = await reducedFrame.evaluate(() => window.ripperStudy.getState());
    const reducedMatches = await reducedFrame.evaluate(() => matchMedia('(prefers-reduced-motion: reduce)').matches);
    await reducedContext.close();
    const motion = { automatic: automaticTime > initialTime, pause: pausedTime === pauseTime, resume: resumedTime > pausedTime, reducedMotionMediaMatches: reducedMatches, reducedMotionPaused: !reducedState.playing };
    const checks = { errors, motion, paletteChangesDrawing: beforePalette !== afterPalette, selected, playing, sizes };
    fs.writeFileSync(path.join(__dirname, 'checks.json'), JSON.stringify(checks, null, 2));
    console.log(JSON.stringify(checks));
    if (errors.length || Object.values(motion).some(value => !value) || beforePalette === afterPalette || selected.selected !== 'F' || playing.rate !== .25 || !playing.playing || playing.elapsed <= .18 || sizes.some(size => size.scrollWidth > size.width)) {
      throw new Error('Visual interaction or layout check failed.');
    }
  } finally {
    await browser.close();
  }
})().catch(error => { console.error(error); process.exitCode = 1; });
