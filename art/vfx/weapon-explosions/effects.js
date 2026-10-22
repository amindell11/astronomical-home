(() => {
  const clamp = value => Math.max(0, Math.min(1, value));
  const fade = (time, begin, end) => 1 - clamp((time - begin) / (end - begin));
  const canvas = (width, height = width) => Object.assign(document.createElement('canvas'), {width, height});
  const layers = {};
  const mineIgnitionExpansionSeconds = 0.047;

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
      const alpha = clamp(Math.max(...rgb.map((v, c) => (v - background[c]) / (255 - background[c]))))
        * clamp((signal - 3) / 6);
      const warm = clamp((rgb[0] - rgb[2] - 25) / 30) * clamp((rgb[0] - rgb[1] * 0.96 + 12) / 24);
      const heat = Math.max(warm, clamp((Math.min(...rgb) - 165) / 30));
      const selection = channel === 'fire' ? heat
        : channel === 'smoke' ? (1 - heat) * clamp((Math.max(...rgb) - 55) / 20) : 1;
      for (let c = 0; c < 3; c++) {
        pixels.data[i + c] = alpha ? Math.max(0, (rgb[c] - background[c] * (1 - alpha)) / alpha) : 0;
      }
      pixels.data[i + 3] = Math.round(alpha * selection * 255);
    }
    if (channel === 'smoke') pruneSmoke(pixels, width, height);
    ctx.putImageData(pixels, 0, 0);
    return result;
  }

  function ignitionCore(flash) {
    const size = flash.width, result = canvas(size), ctx = result.getContext('2d');
    ctx.translate(size / 2, size / 2);
    stampCore(ctx, flash, size, 1, (size - 96) / (flash.height - 96));
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
        const brightness = (original[i + 1] + original[i + 2]) * original[i + 3];
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
      const rgba = [0, 0, 0, 0];
      for (let by = 0; by < 2; by++) for (let bx = 0; bx < 2; bx++) {
        const px = ix + bx, py = iy + by;
        if (px < 0 || py < 0 || px >= width || py >= width) continue;
        const i = (py * width + px) * 4;
        const weight = (bx ? fx : 1 - fx) * (by ? fy : 1 - fy) * original[i + 3] / 255;
        for (let c = 0; c < 3; c++) rgba[c] += original[i + c] * weight;
        rgba[3] += weight;
      }
      const target = (y * 512 + x) * 4;
      for (let c = 0; c < 3; c++) pixels.data[target + c] = rgba[3] ? rgba[c] / rgba[3] : 0;
      pixels.data[target + 3] = rgba[3] * 255 * fade(radius, 1.005, 1.04);
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
    layers.flash = extract(mine, [68, 144, 180, 264]);
    layers.core = ignitionCore(layers.flash);
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
    ctx.imageSmoothingQuality = 'high';
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

  function stampCore(ctx, source, size, opacity, tipScale) {
    const width = source.width, centerHeight = 96;
    const sourceTipHeight = (source.height - centerHeight) / 2;
    const tipHeight = sourceTipHeight * tipScale;
    ctx.save();
    ctx.globalAlpha = clamp(opacity);
    ctx.scale(size / width, size / width);
    ctx.imageSmoothingQuality = 'high';
    ctx.drawImage(source, 0, 0, width, sourceTipHeight,
      -width / 2, -centerHeight / 2 - tipHeight, width, tipHeight);
    ctx.drawImage(source, 0, sourceTipHeight, width, centerHeight,
      -width / 2, -centerHeight / 2, width, centerHeight);
    ctx.drawImage(source, 0, sourceTipHeight + centerHeight, width, sourceTipHeight,
      -width / 2, centerHeight / 2, width, tipHeight);
    ctx.restore();
  }

  function ignition(ctx, time, size, settings) {
    const progress = time / settings.mineIgnitionSeconds;
    if (progress < 0 || progress > 1) return;
    const pulse = 1 + settings.mineCorePulse * 0.16 * Math.sin(progress * Math.PI * 3);
    const expansion = clamp(time / mineIgnitionExpansionSeconds);
    const coreSize = size * 1.2;
    const spikeGrowth = clamp((time - 0.02) / 0.16);
    const spikeLength = 0.2 + 0.8 * spikeGrowth * spikeGrowth * (3 - 2 * spikeGrowth);
    stampCore(ctx, layers.core, coreSize * (0.55 + expansion * 0.5) * pulse,
      0.65 * fade(progress, 0.3, 1), spikeLength);
    stampCore(ctx, layers.core, coreSize * (0.36 + expansion * 0.26) / pulse,
      fade(progress, 0.4, 1), spikeLength);
    stampCore(ctx, layers.flash, size * (0.62 + expansion * 0.25), fade(progress, 0.5, 1), spikeLength);
  }

  function ribbon(ctx, radius, start, span, width, phase, opacity, color) {
    if (opacity <= 0) return;
    const count = 28;
    const point = (step, side) => {
      const u = step / count, angle = start + span * u;
      const ripple = 1 + 0.025 * Math.sin(angle * 13 + phase) + 0.014 * Math.sin(angle * 29 - phase);
      const r = radius * ripple - side * width * Math.sin(Math.PI * u);
      return [Math.cos(angle) * r, Math.sin(angle) * r];
    };
    ctx.save(); ctx.globalAlpha = clamp(opacity); ctx.fillStyle = color;
    ctx.beginPath(); ctx.moveTo(...point(0, 0));
    for (let step = 1; step <= count; step++) ctx.lineTo(...point(step, 0));
    for (let step = count; step >= 0; step--) ctx.lineTo(...point(step, 1));
    ctx.closePath(); ctx.fill(); ctx.restore();
  }

  function disc(ctx, time, size, settings) {
    const progress = time / settings.mineSweepSeconds;
    if (progress < 0 || progress >= 1) return;
    const radius = size * 0.45;
    const dissolve = fade(progress, 0.55, 1);
    stamp(ctx, layers.discFill, size, settings.mineFill * (0.8 - progress * 0.3) * dissolve);
    stamp(ctx, layers.discFill, size * (0.68 + progress * 0.25),
      settings.mineFill * 0.5 * dissolve, -0.5 + progress * 0.7);
    for (let i = 0; i < 3; i++) {
      const age = clamp((progress - i * 0.11) / (1 - i * 0.11));
      const r = radius * (0.12 + 0.81 * Math.sqrt(age));
      const alpha = Math.sin(age * Math.PI) * settings.mineMotion;
      for (let arc = 0; arc < 3; arc++) {
        const start = arc * 2.09 + i * 0.7 + Math.sin(age * 5 + i) * 0.13;
        const span = 0.8 + 0.35 * Math.sin(arc * 2 + i);
        ribbon(ctx, r, start, span, radius * 0.1 * (1 - age), age * 8 + i, alpha * 0.42, '#168dde');
        ribbon(ctx, r, start, span * 0.86, radius * 0.018, age * 8 + i, alpha * 0.65, '#7ae8ff');
      }
    }
    stamp(ctx, layers.discEdge, size,
      settings.mineGlow * (0.88 + 0.12 * Math.sin(progress * 43)) * fade(progress, 0.92, 1));
  }

  function mine(ctx, time, size, settings, pitch = settings.mineDiscAspect) {
    if (time < 0 || time >= settings.mineSweepSeconds) return;
    const progress = clamp(time / settings.mineSweepSeconds);
    ctx.save();
    ctx.globalAlpha = settings.mineRangeOpacity;
    ctx.strokeStyle = '#99e6ff';
    ctx.lineWidth = Math.max(0.6, size / 600);
    ctx.beginPath();
    ctx.arc(0, 0, size * 0.45 * progress, 0, Math.PI * 2);
    ctx.stroke();
    ctx.restore();
    ctx.save();
    ctx.scale(1, pitch);
    disc(ctx, time, size * progress, settings);
    ctx.restore();
    ignition(ctx, time, size * 0.18, settings);
  }

  function atlas(painter, seconds, columns, rows, cell, scale, settings, padding = 0.88) {
    const result = canvas(columns * cell, rows * cell);
    const highResolution = canvas(result.width * 2, result.height * 2);
    const ctx = highResolution.getContext('2d');
    ctx.scale(2, 2);
    for (let frame = 0; frame < columns * rows; frame++) {
      ctx.save();
      ctx.translate((frame % columns + 0.5) * cell, (Math.floor(frame / columns) + 0.5) * cell);
      painter(ctx, seconds * frame / (columns * rows - 1), cell * padding / scale, settings);
      ctx.restore();
    }
    const output = result.getContext('2d');
    output.imageSmoothingQuality = 'high';
    output.drawImage(highResolution, 0, 0, result.width, result.height);
    return result;
  }

  function atlasScales(settings) {
    return {missileAtlasScale: Math.max(1, settings.missileSpread), mineIgnitionAtlasScale: layers.flash.height / layers.flash.width};
  }

  function textures(settings) {
    const scale = atlasScales(settings);
    return {
      Missile: atlas(missile, settings.missileTailSeconds, 4, 8, 256, scale.missileAtlasScale, settings),
      MineIgnition: atlas(ignition, settings.mineIgnitionSeconds, 4, 4, 256, scale.mineIgnitionAtlasScale, settings),
      MineDisc: atlas(disc, settings.mineSweepSeconds, 4, 8, 256, 1, settings, 1)
    };
  }

  window.WeaponExplosions = {load, layers, missile, mine, ignition, disc, textures, atlasScales};
})();
