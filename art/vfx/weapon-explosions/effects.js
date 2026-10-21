(() => {
  const clamp = value => Math.max(0, Math.min(1, value));
  const fade = (time, begin, end) => 1 - clamp((time - begin) / (end - begin));
  const canvas = (width, height = width) => Object.assign(document.createElement('canvas'), {width, height});
  const layers = {};

  function pruneSmoke(pixels, width, height) {
    const visited = new Uint8Array(width * height);
    for (let start = 0; start < visited.length; start++) {
      if (visited[start] || pixels.data[start * 4 + 3] < 20) continue;
      const component = [start];
      visited[start] = 1;
      for (let j = 0; j < component.length; j++) {
        const p = component[j];
        for (const n of [p - width, p + width, p % width ? p - 1 : -1, p % width < width - 1 ? p + 1 : -1]) {
          if (n < 0 || n >= visited.length || visited[n] || pixels.data[n * 4 + 3] < 20) continue;
          visited[n] = 1; component.push(n);
        }
      }
      if (component.length < 120) for (const p of component) pixels.data[p * 4 + 3] = 0;
    }
    for (let p = 0; p < visited.length; p++) if (!visited[p]) pixels.data[p * 4 + 3] = 0;
  }

  function extract(image, bounds, channel = 'all') {
    const [x, y, width, height] = bounds;
    const result = canvas(width, height);
    const ctx = result.getContext('2d', {willReadFrequently: true});
    ctx.drawImage(image, x, y, width, height, 0, 0, width, height);
    const pixels = ctx.getImageData(0, 0, width, height);
    const background = [0, 1, 2].map(c => {
      const samples = [];
      for (let i = 0; i < width; i++) samples.push(pixels.data[i * 4 + c]);
      return samples.sort((a, b) => a - b)[Math.floor(samples.length / 2)];
    });
    for (let i = 0; i < pixels.data.length; i += 4) {
      const rgb = Array.from(pixels.data.slice(i, i + 3));
      const signal = Math.max(...rgb.map((v, c) => v - background[c]));
      let alpha = clamp((signal - 9) / 28);
      const warm = (rgb[0] - rgb[2] > 40 && rgb[0] >= rgb[1] * 0.96) || Math.min(...rgb) > 180;
      if ((channel === 'fire' && !warm) || (channel === 'smoke' && (warm || Math.max(...rgb) < 65))) alpha = 0;
      for (let c = 0; c < 3; c++) {
        pixels.data[i + c] = alpha ? Math.max(0, (rgb[c] - background[c] * (1 - alpha)) / alpha) : 0;
      }
      pixels.data[i + 3] = Math.round(alpha * 255);
    }
    if (channel === 'smoke') pruneSmoke(pixels, width, height);
    ctx.putImageData(pixels, 0, 0);
    return result;
  }

  function circularDisc(source) {
    const width = source.width, center = width / 2;
    const original = source.getContext('2d').getImageData(0, 0, width, width).data;
    const radii = Array.from({length:360}, (_, angle) => {
      const theta = angle * Math.PI / 180;
      let brightest = 0, edge = center * 0.9;
      for (let radius = center * 0.7; radius < center * 0.98; radius += 0.5) {
        const x = Math.round(center + Math.cos(theta) * radius), y = Math.round(center + Math.sin(theta) * radius);
        const i = (y * width + x) * 4;
        const brightness = original[i + 1] + original[i + 2];
        if (brightness > brightest) {brightest = brightness; edge = radius;}
      }
      return edge;
    });
    const smoothedRadii = radii.map((_, angle) => {
      let sum = 0;
      for (let offset = -4; offset <= 4; offset++) sum += radii[(angle + offset + 360) % 360];
      return sum / 9;
    });
    const result = canvas(512), ctx = result.getContext('2d'), pixels = ctx.createImageData(512, 512);
    for (let y = 0; y < 512; y++) for (let x = 0; x < 512; x++) {
      const dx = x - 255.5, dy = y - 255.5, radius = Math.hypot(dx, dy) / 230.4;
      if (radius > 1.04) continue;
      const angle = (Math.atan2(dy, dx) * 180 / Math.PI + 360) % 360;
      const a = Math.floor(angle), edge = smoothedRadii[a] * (1 - (angle - a)) + smoothedRadii[(a + 1) % 360] * (angle - a);
      const sx = center + dx / 230.4 * edge, sy = center + dy / 230.4 * edge;
      const ix = Math.floor(sx), iy = Math.floor(sy), fx = sx - ix, fy = sy - iy;
      for (let c = 0; c < 4; c++) {
        let value = 0;
        for (let by = 0; by < 2; by++) for (let bx = 0; bx < 2; bx++) {
          const px = ix + bx, py = iy + by;
          if (px >= 0 && py >= 0 && px < width && py < width) value += original[(py * width + px) * 4 + c] * (bx ? fx : 1 - fx) * (by ? fy : 1 - fy);
        }
        pixels.data[(y * 512 + x) * 4 + c] = value * (c === 3 ? fade(radius, 1.005, 1.04) : 1);
      }
    }
    ctx.putImageData(pixels, 0, 0);
    return result;
  }

  function discLayer(source, edge) {
    const result = canvas(512), ctx = result.getContext('2d');
    ctx.drawImage(source, 0, 0);
    const pixels = ctx.getImageData(0, 0, 512, 512);
    for (let y = 0; y < 512; y++) for (let x = 0; x < 512; x++) {
      const radius = Math.hypot(x - 255.5, y - 255.5) / 230.4;
      const t = clamp((radius - 0.92) / 0.06), mix = t * t * (3 - 2 * t);
      pixels.data[(y * 512 + x) * 4 + 3] *= edge ? mix : 1 - mix;
    }
    ctx.putImageData(pixels, 0, 0);
    return result;
  }

  async function load(sources) {
    const images = await Promise.all(Object.values(sources).map(src => new Promise((resolve, reject) => {
      const image = new Image();
      image.onload = () => resolve(image);
      image.onerror = reject;
      image.src = src;
    })));
    const [missile, mine] = images;
    layers.ignition = extract(missile, [130, 187, 90, 90]);
    layers.burst = extract(missile, [350, 90, 340, 280]);
    layers.breakup = extract(missile, [735, 105, 380, 265], 'fire');
    layers.smoke = extract(missile, [735, 105, 380, 265], 'smoke');
    layers.embers = extract(missile, [1140, 135, 335, 230], 'fire');
    layers.lateSmoke = extract(missile, [1140, 135, 335, 230], 'smoke');
    layers.flash = extract(mine, [70, 140, 180, 250]);
    layers.disc = circularDisc(extract(mine, [964, 536, 348, 348]));
    layers.discFill = discLayer(layers.disc, false);
    layers.discEdge = discLayer(layers.disc, true);
    return layers;
  }

  function stamp(ctx, source, size, opacity, angle = 0, y = 0) {
    if (opacity <= 0 || size <= 0) return;
    ctx.save();
    ctx.globalAlpha = clamp(opacity);
    ctx.translate(0, y);
    ctx.rotate(angle);
    const h = size * source.height / source.width;
    ctx.drawImage(source, -size / 2, -h / 2, size, h);
    ctx.restore();
  }

  function missile(ctx, time, size, settings) {
    if (time < 0 || time > settings.missileTailSeconds) return;
    const fire = time / settings.missileFireSeconds;
    const tail = time / settings.missileTailSeconds;
    const spread = settings.missileSpread;
    stamp(ctx, layers.smoke, size * (0.72 + tail * 0.22) * spread,
      clamp((time - 0.08) / 0.12) * fade(tail, 0.55, 1) * settings.smokeOpacity, 0, -size * tail * 0.04);
    stamp(ctx, layers.lateSmoke, size * (0.82 + tail * 0.16) * spread,
      clamp((time - 0.22) / 0.15) * fade(tail, 0.65, 1) * settings.smokeOpacity, 0, -size * tail * 0.04);
    stamp(ctx, layers.ignition, size * (0.14 + clamp(time / 0.04) * 0.16), fade(time, 0.015, 0.055));
    stamp(ctx, layers.burst, size * (0.45 + clamp(time / 0.075) * 0.42) * spread,
      clamp(time / 0.035) * fade(time, 0.055, 0.105));
    stamp(ctx, layers.breakup, size * (0.8 + clamp(fire) * 0.2) * spread,
      clamp((time - 0.07) / 0.05) * fade(fire, 0.58, 1));
    stamp(ctx, layers.embers, size * (0.85 + tail * 0.16) * spread,
      clamp((time - 0.20) / 0.08) * fade(tail, 0.55, 0.93));
  }

  function ignition(ctx, time, size, settings) {
    const progress = time / settings.mineIgnitionSeconds;
    if (progress < 0 || progress > 1) return;
    stamp(ctx, layers.flash, size * (0.62 + clamp(progress * 3) * 0.25), fade(progress, 0.24, 1));
  }

  function disc(ctx, size, settings) {
    stamp(ctx, layers.discFill, size, settings.mineFill);
    stamp(ctx, layers.discEdge, size, settings.mineGlow);
  }

  function mine(ctx, time, size, settings, pitch = 0.28) {
    if (time < 0 || time > settings.mineSweepSeconds) return;
    ctx.save();
    ctx.scale(1, pitch);
    disc(ctx, size * clamp(time / settings.mineSweepSeconds), settings);
    ctx.restore();
    ignition(ctx, time, size * 0.18, settings);
  }

  function atlas(painter, seconds, columns, rows, cell, settings) {
    const result = canvas(columns * cell, rows * cell);
    const ctx = result.getContext('2d');
    for (let frame = 0; frame < columns * rows; frame++) {
      ctx.save();
      ctx.translate((frame % columns + 0.5) * cell, (Math.floor(frame / columns) + 0.5) * cell);
      painter(ctx, seconds * frame / (columns * rows - 1), cell * 0.88, settings);
      ctx.restore();
    }
    return result;
  }

  function textures(settings) {
    return {
      Missile: atlas(missile, settings.missileTailSeconds, 4, 8, 256, settings),
      MineIgnition: atlas(ignition, settings.mineIgnitionSeconds, 4, 4, 256, settings),
      MineDisc: layers.disc
    };
  }

  window.WeaponExplosions = {load, layers, missile, mine, ignition, disc, textures};
})();
