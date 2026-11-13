// Nav icon geometry, shared by the tuner page and the export (navicons.py runs it under node).
// Icons draw in a 64-unit box; settings come from settings.json.
const NAV_KINDS = [["mod", "find", "star"], ["repair", "find", "plus"], ["armory", "find", "target"], ["exit", "exit", "arch"], ["lull", "lull", "beacon"], ["enemy", "enemy", "bang"]];

function NavIcons(S) {
  const KINDS = NAV_KINDS;
  const f = n => +n.toFixed(2);
  function poly(pts) { return pts.map(p => f(p[0]) + "," + f(p[1])).join(" "); }
  function containerPts(role) {
    if (role === "find") { const d = S.find_diamond_half; return [[32, 32 - d], [32 + d, 32], [32, 32 + d], [32 - d, 32]]; }
    if (role === "lull") { const r = S.lull_hex_r; return [0, 1, 2, 3, 4, 5].map(i => [32 + r * Math.cos(i * Math.PI / 3), 32 + r * Math.sin(i * Math.PI / 3)]); }
    if (role === "enemy") { const t = S.enemy_tri_half; return [[32, 32 - t * 1.0], [32 + t, 32 + t * .76], [32 - t, 32 + t * .76]]; }
    return null;
  }
  function reach(role) { return role === "find" ? S.find_diamond_half : role === "exit" ? S.exit_circle_r : role === "lull" ? S.lull_hex_r : S.enemy_tri_half; }
  function container(role, colour) {
    const rc = S.corner_round, rim = S.rim, ink = S.ink;
    const pts = containerPts(role);
    const shape = (fill, sw, stroke) => pts
      ? `<polygon points="${poly(pts)}" fill="${fill}" stroke="${stroke}" stroke-width="${f(sw)}" stroke-linejoin="round"/>`
      : `<circle cx="32" cy="32" r="${S.exit_circle_r}" fill="${fill}" stroke="${stroke}" stroke-width="${f(sw)}"/>`;
    let out = "";
    if (rim > 0) out += shape(ink, 2 * (rc + rim), ink);
    out += shape(colour, 2 * rc, colour);
    return out;
  }
  function glyph(kind, role) {
    const w = S.glyph_stroke, s = S.glyph_scale, ink = S.ink, cx = 32, cy = role === "enemy" ? 32 + S.enemy_tri_half * .2 : 32;
    const st = `fill="none" stroke="${ink}" stroke-width="${f(w)}" stroke-linecap="${S.glyph_cap}" stroke-linejoin="${S.glyph_cap === "round" ? "round" : "miter"}"`;
    switch (kind) {
      case "star": { const t = 16 * s, c = S.star_waist * s;
        return `<path d="M${cx} ${f(cy - t)} L${f(cx + c)} ${f(cy - c)} L${f(cx + t)} ${cy} L${f(cx + c)} ${f(cy + c)} L${cx} ${f(cy + t)} L${f(cx - c)} ${f(cy + c)} L${f(cx - t)} ${cy} L${f(cx - c)} ${f(cy - c)}Z" fill="${ink}"/>`; }
      case "plus": { const a = 12.5 * s; return `<path d="M${cx} ${f(cy - a)} V${f(cy + a)} M${f(cx - a)} ${cy} H${f(cx + a)}" ${st}/>`; }
      case "target": return `<circle cx="${cx}" cy="${cy}" r="${f(11 * s)}" ${st}/><circle cx="${cx}" cy="${cy}" r="${f(w * .6)}" fill="${ink}"/>`;
      case "arch": { const x = 9 * s, top = cy - 1 * s, bot = cy + 11 * s;
        return `<path d="M${f(cx - x)} ${f(bot)} V${f(top)} A${f(x)} ${f(x)} 0 0 1 ${f(cx + x)} ${f(top)} V${f(bot)}" ${st}/>`; }
      case "beacon": { const r = 14 * s, a = Math.PI / 4, ex = r * Math.cos(a), ey = r * Math.sin(a);
        return `<circle cx="${cx}" cy="${cy}" r="${f(w * .68)}" fill="${ink}"/><path d="M${f(cx - ex)} ${f(cy - ey)} A${f(r)} ${f(r)} 0 0 0 ${f(cx - ex)} ${f(cy + ey)} M${f(cx + ex)} ${f(cy - ey)} A${f(r)} ${f(r)} 0 0 1 ${f(cx + ex)} ${f(cy + ey)}" ${st}/>`; }
      case "chip": { const h = 7 * s, p = 12 * s, o = 3.5 * s, pw = f(w * .55);
        return `<rect x="${f(cx - h)}" y="${f(cy - h)}" width="${f(2 * h)}" height="${f(2 * h)}" fill="${ink}"/><path d="M${f(cx - o)} ${f(cy - p)} V${f(cy + p)} M${f(cx + o)} ${f(cy - p)} V${f(cy + p)} M${f(cx - p)} ${f(cy - o)} H${f(cx + p)} M${f(cx - p)} ${f(cy + o)} H${f(cx + p)}" fill="none" stroke="${ink}" stroke-width="${pw}" stroke-linecap="butt"/>`; }
      case "upgrade": return `<path d="M${f(cx - 10 * s)} ${f(cy + 1 * s)} L${cx} ${f(cy - 9 * s)} L${f(cx + 10 * s)} ${f(cy + 1 * s)} M${f(cx - 10 * s)} ${f(cy + 11 * s)} L${cx} ${f(cy + 1 * s)} L${f(cx + 10 * s)} ${f(cy + 11 * s)}" ${st}/>`;
      case "bolt": return `<path d="M${f(cx + 3 * s)} ${f(cy - 15 * s)} L${f(cx - 8 * s)} ${f(cy + 2 * s)} L${f(cx - 1 * s)} ${f(cy + 2 * s)} L${f(cx - 3 * s)} ${f(cy + 15 * s)} L${f(cx + 8 * s)} ${f(cy - 2 * s)} L${f(cx + 1 * s)} ${f(cy - 2 * s)}Z" fill="${ink}"/>`;
      case "gem": { const g = 12 * s; return `<path d="M${cx} ${f(cy - g)} L${f(cx + g)} ${cy} L${cx} ${f(cy + g)} L${f(cx - g)} ${cy}Z" ${st}/><rect x="${f(cx - w * .55)}" y="${f(cy - w * .55)}" width="${f(w * 1.1)}" height="${f(w * 1.1)}" transform="rotate(45 ${cx} ${cy})" fill="${ink}"/>`; }
      case "reticle": { const a = 5 * s, b = 13 * s;
        return `<path d="M${cx} ${f(cy - b)} V${f(cy - a)} M${cx} ${f(cy + a)} V${f(cy + b)} M${f(cx - b)} ${cy} H${f(cx - a)} M${f(cx + a)} ${cy} H${f(cx + b)}" ${st}/>`; }
      case "ship": return `<path d="M${cx} ${f(cy - 13 * s)} L${f(cx + 10 * s)} ${f(cy + 10 * s)} L${cx} ${f(cy + 4 * s)} L${f(cx - 10 * s)} ${f(cy + 10 * s)}Z" fill="${ink}"/>`;
      case "chevrons": return `<path d="M${f(cx - 10 * s)} ${f(cy - 9 * s)} L${cx} ${f(cy - 1 * s)} L${f(cx + 10 * s)} ${f(cy - 9 * s)} M${f(cx - 10 * s)} ${f(cy + 2 * s)} L${cx} ${f(cy + 10 * s)} L${f(cx + 10 * s)} ${f(cy + 2 * s)}" ${st}/>`;
      case "claws": return `<path d="M${f(cx - 11 * s)} ${f(cy + 9 * s)} L${f(cx - 4 * s)} ${f(cy - 9 * s)} M${f(cx - 3.5 * s)} ${f(cy + 9 * s)} L${f(cx + 3.5 * s)} ${f(cy - 9 * s)} M${f(cx + 4 * s)} ${f(cy + 9 * s)} L${f(cx + 11 * s)} ${f(cy - 9 * s)}" ${st}/>`;
      case "bang": return `<path d="M${cx} ${f(cy - 13 * s)} V${f(cy + 2 * s)}" ${st}/><circle cx="${cx}" cy="${f(cy + 10 * s)}" r="${f(w * .58)}" fill="${ink}"/>`;
    }
  }
  function iconInner(kindIdx, fill) { const [k, role, g0] = KINDS[kindIdx]; const g = role === "enemy" ? S.enemy_glyph : k === "mod" ? S.mod_glyph : g0; return container(role, fill || S[k]) + glyph(g, role); }
  function iconSVG(kindIdx) { return `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 64 64">${iconInner(kindIdx)}</svg>`; }
  // Icon plus arrow tab facing angle (radians, 0 = right, CCW up). Drawn in a 128-unit box centred on the icon.
  function pointerInner(kindIdx, angle) {
    const [k, role] = KINDS[kindIdx];
    const d = reach(role) + S.tab_gap, len = S.tab_size * 64, half = len * .62;
    const ux = Math.cos(angle), uy = -Math.sin(angle), px = -uy, py = ux;
    const bx = 64 + ux * d, by = 64 + uy * d;
    const tab = [[bx + ux * len, by + uy * len], [bx + px * half, by + py * half], [bx - px * half, by - py * half]];
    const rim = S.rim > 0 ? `<polygon points="${poly(tab)}" fill="${S.ink}" stroke="${S.ink}" stroke-width="${f(2 * (S.corner_round + S.rim))}" stroke-linejoin="round"/>` : "";
    return `${rim}<polygon points="${poly(tab)}" fill="${S[k]}" stroke="${S[k]}" stroke-width="${f(2 * S.corner_round)}" stroke-linejoin="round"/><g transform="translate(32 32)">${iconInner(kindIdx)}</g>`;
  }
  return { iconInner, iconSVG, pointerInner, reach };
}

if (typeof module !== "undefined" && require.main === module) {
  const S = JSON.parse(require("fs").readFileSync(process.argv[2], "utf8"));
  const N = NavIcons(S), out = {};
  NAV_KINDS.forEach(([k], i) => {
    out[k] = N.iconSVG(i);
    out[k + "_white"] = `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 64 64">${N.iconInner(i, "#FFFFFF")}</svg>`;
  });
  process.stdout.write(JSON.stringify(out));
}
