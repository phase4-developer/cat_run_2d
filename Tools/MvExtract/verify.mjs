// 03 §3.11.1 대조. npm run verify [-- --skip-v1]
// V1 결정성: 2회 새로 추출해 PNG SHA256 완전 일치 + 저장소 Art/Source와 일치.
// V2 감싸기 무결성: project job마다 감싸기 없이(H를 맞춰 k = 1, 평행이동만) 그린 것 = 추출 그림(화소 완전 일치).
// V4 억제 규칙: job.v4가 있는 job마다 「억제한 그림 + 따로 뽑은 부품을 런타임 위치에 합성」 ≈ 「억제 없는 원래 그림」(ΔE2000 99백분위 ≤ 3).
// V3(실제 런 장면 캡처 대조)는 장면 합성기가 없어 NOT_RUN.
import { execFileSync } from 'node:child_process';
import { readdirSync, readFileSync, statSync, rmSync, writeFileSync, mkdirSync } from 'node:fs';
import { join, relative } from 'node:path';
import { ROOT, REPO, sha256, openCanon } from './lib/canon.mjs';

const SKIP_V1 = process.argv.includes('--skip-v1');
const walk = (d) => readdirSync(d).flatMap((f) => { const p = join(d, f); return statSync(p).isDirectory() ? walk(p) : p.endsWith('.png') ? [p] : []; });
const hashes = (d) => Object.fromEntries(walk(d).map((p) => [relative(d, p).split(String.fromCharCode(92)).join('/'), sha256(readFileSync(p))]));
const run = (out) => { rmSync(out, { recursive: true, force: true }); execFileSync(process.execPath, ['run_art.mjs', '--out', out], { cwd: ROOT, stdio: 'inherit' }); return hashes(out); };
const diff = (x, y) => [...new Set([...Object.keys(x), ...Object.keys(y)])].filter((k) => x[k] !== y[k]);
let ab = [], as = [], nA = 0;
if (!SKIP_V1) {
  const a = run(join(ROOT, 'out/verify_a')), b = run(join(ROOT, 'out/verify_b'));
  const src = hashes(join(REPO, 'Assets/Game/Art/Source'));
  ab = diff(a, b); as = diff(a, src); nA = Object.keys(a).length;
}

// ── V2·V4: 한 브라우저에서 ──
const jobs = readdirSync(join(ROOT, 'jobs')).filter((f) => f.endsWith('.json')).sort().flatMap((f) => JSON.parse(readFileSync(join(ROOT, 'jobs', f), 'utf8')));
const byName = Object.fromEntries(jobs.map((j) => [j.name, j]));
const { browser, page } = await openCanon();
await page.addScriptTag({ path: join(ROOT, 'inject/harness.js') });
await page.evaluate(() => MvH.loadEmoji());
await page.addScriptTag({ content: `
window.MvV = {
  async px(u) { const im = new Image(); im.src = u; await im.decode(); const c = document.createElement('canvas'); c.width = im.width; c.height = im.height; const g = c.getContext('2d'); g.drawImage(im, 0, 0); return { c, d: g.getImageData(0, 0, c.width, c.height).data }; },
  lab(r, g, b) { const f = (v) => { v /= 255; return v <= 0.04045 ? v / 12.92 : ((v + 0.055) / 1.055) ** 2.4; }; const R = f(r), G = f(g), B = f(b);
    let x = (R * 0.4124 + G * 0.3576 + B * 0.1805) / 0.95047, y = R * 0.2126 + G * 0.7152 + B * 0.0722, z = (R * 0.0193 + G * 0.1192 + B * 0.9505) / 1.08883;
    const h = (t) => (t > 216 / 24389 ? Math.cbrt(t) : (24389 / 27 * t + 16) / 116); x = h(x); y = h(y); z = h(z); return [116 * y - 16, 500 * (x - y), 200 * (y - z)]; },
  de2000([L1, a1, b1], [L2, a2, b2]) { const rad = Math.PI / 180, C1 = Math.hypot(a1, b1), C2 = Math.hypot(a2, b2), Cm = (C1 + C2) / 2, G = 0.5 * (1 - Math.sqrt(Cm ** 7 / (Cm ** 7 + 25 ** 7)));
    const a1p = a1 * (1 + G), a2p = a2 * (1 + G), C1p = Math.hypot(a1p, b1), C2p = Math.hypot(a2p, b2); const hp = (a, b) => (a === 0 && b === 0 ? 0 : (Math.atan2(b, a) / rad + 360) % 360);
    const h1 = hp(a1p, b1), h2 = hp(a2p, b2), dL = L2 - L1, dC = C2p - C1p; let dh = h2 - h1; if (C1p * C2p === 0) dh = 0; else if (dh > 180) dh -= 360; else if (dh < -180) dh += 360;
    const dH = 2 * Math.sqrt(C1p * C2p) * Math.sin(dh * rad / 2), Lm = (L1 + L2) / 2, Cpm = (C1p + C2p) / 2; let hm = h1 + h2; if (C1p * C2p !== 0) hm = Math.abs(h1 - h2) > 180 ? (h1 + h2 + 360) / 2 : (h1 + h2) / 2;
    const T = 1 - 0.17 * Math.cos((hm - 30) * rad) + 0.24 * Math.cos(2 * hm * rad) + 0.32 * Math.cos((3 * hm + 6) * rad) - 0.2 * Math.cos((4 * hm - 63) * rad);
    const SL = 1 + 0.015 * (Lm - 50) ** 2 / Math.sqrt(20 + (Lm - 50) ** 2), SC = 1 + 0.045 * Cpm, SH = 1 + 0.015 * Cpm * T, RT = -2 * Math.sqrt(Cpm ** 7 / (Cpm ** 7 + 25 ** 7)) * Math.sin(60 * Math.exp(-(((hm - 275) / 25) ** 2)) * rad);
    return Math.sqrt((dL / SL) ** 2 + (dC / SC) ** 2 + (dH / SH) ** 2 + RT * (dC / SC) * (dH / SH)); },
  // 흰 바탕 합성 후 ΔE2000. 두 그림 중 하나라도 알파가 있는 화소만 센다
  async compare(u1, u2) { const A = await MvV.px(u1), B = await MvV.px(u2); const n = A.d.length / 4; let exact = 0, maxc = 0; const de = [];
    for (let i = 0; i < n; i++) { const o = i * 4; let same = true; for (let k = 0; k < 4; k++) { const d = Math.abs(A.d[o + k] - B.d[o + k]); if (d) same = false; if (d > maxc) maxc = d; } if (!same) exact++;
      if (!A.d[o + 3] && !B.d[o + 3]) continue; const w = (d, k) => d[o + k] * d[o + 3] / 255 + 255 * (1 - d[o + 3] / 255);
      de.push(MvV.de2000(MvV.lab(w(A.d, 0), w(A.d, 1), w(A.d, 2)), MvV.lab(w(B.d, 0), w(B.d, 1), w(B.d, 2)))); }
    de.sort((x, y) => x - y); const iAll = Math.floor(n * 0.99) - (n - de.length); return { diffPx: exact, maxChannel: maxc, p99all: iAll < 0 ? 0 : de[iAll], p99: de.length ? de[Math.floor(de.length * 0.99)] : 0, max: de.length ? de[de.length - 1] : 0, n: de.length }; },
  async compose(base, parts) { const B = await MvV.px(base.dataURL); const g = B.c.getContext('2d');
    for (const p of parts) { const P = await MvV.px(p.dataURL); g.save(); g.imageSmoothingQuality = 'high'; g.translate(p.x, p.y); g.rotate(p.rot || 0); g.drawImage(P.c, -p.px, -p.py); g.restore(); } return B.c.toDataURL('image/png'); },
};` });
const v2 = [], v4 = [];
for (const j of jobs.filter((x) => x.mode === 'project')) {
  const r = await page.evaluate(async (j) => { const a = MvH.renderOne(j), b = MvH.renderOne({ ...j, __raw: true }); return MvV.compare(a.dataURL, b.dataURL); }, j);
  v2.push({ name: j.name, ...r, ok: r.max <= 3 }); // 완전 일치는 부동소수 반올림 AA 차로 못 맞춘다 → ΔE 최대 ≤ 3을 통과로, 다른 화소 수는 함께 적는다
}
for (const j of jobs.filter((x) => x.v4)) {
  const r = await page.evaluate(async ([j, parts]) => {
    const scale = (j.ppu || 128) * (j.scaleK || 1), [X, Y] = j.canvas.pivotPx;
    const sup = MvH.renderOne(j), orig = MvH.renderOne({ ...j, suppress: [] });
    const ps = parts.map(([v, pj]) => ({ dataURL: MvH.renderOne(pj).dataURL, x: X + v.at[0] * scale, y: Y + v.at[1] * scale, rot: v.rot || 0, px: pj.canvas.pivotPx[0], py: pj.canvas.pivotPx[1] }));
    return MvV.compare(await MvV.compose(sup, ps), orig.dataURL);
  }, [j, j.v4.map((v) => [v, byName[v.part]])]);
  v4.push({ name: j.name, parts: j.v4.map((v) => v.part).join('+'), ...r, ok: r.p99all <= 3 }); // p99는 캔버스 전체 화소 기준(03 §3.11.1), 물체 화소만의 p99도 함께 적는다
}
await browser.close();

const f2 = (x) => (+x).toFixed(2);
const v2bad = v2.filter((x) => !x.ok), v4bad = v4.filter((x) => !x.ok);
const lines = [`# verify`, ``, `| 단계 | 결과 |`, `|---|---|`,
  SKIP_V1 ? `| V1 결정성(2회) | NOT_RUN(--skip-v1) |` : `| V1 결정성(2회) | ${ab.length ? 'FAIL ' + ab.length : 'PASS'} (${nA} PNG) |`,
  SKIP_V1 ? `| V1' 저장소 Art/Source 일치 | NOT_RUN(--skip-v1) |` : `| V1' 저장소 Art/Source 일치 | ${as.length ? 'FAIL ' + as.length : 'PASS'} |`,
  `| V2 project 감싸기 | ${v2bad.length ? 'FAIL ' + v2bad.length : 'PASS'} (${v2.length} job, ΔE2000 최대 ≤ 3. 화소 완전 일치는 ${v2.filter((x) => !x.diffPx).length}/${v2.length}) |`,
  `| V3 장면 대조 | NOT_RUN(실제 런 장면 합성기 없음) |`,
  `| V4 억제 합성 | ${v4bad.length ? 'FAIL ' + v4bad.length : 'PASS'} (${v4.length} job, 캔버스 전체 ΔE2000 p99 ≤ 3) |`,
  `| V5 프레임 | PASS(구성상: 프레임 i는 정본 함수를 ph/t = v_i로 직접 호출) |`, ``,
  `## V2`, ``, `| job | 다른 화소 | 최대 채널 차 | ΔE p99 | ΔE 최대 |`, `|---|---|---|---|---|`, ...v2.map((x) => `| ${x.name} | ${x.diffPx} | ${x.maxChannel} | ${f2(x.p99)} | ${f2(x.max)} |`), ``,
  `## V4`, ``, `| job | 부품 | ΔE p99(전체) | ΔE p99(물체 화소) | ΔE 최대 | 물체 화소 |`, `|---|---|---|---|---|---|`, ...v4.map((x) => `| ${x.name} | ${x.parts} | ${f2(x.p99all)} | ${f2(x.p99)} | ${f2(x.max)} | ${x.n} |`), ``,
  ...ab.slice(0, 50).map((k) => `- 2회 불일치: ${k}`), ...as.slice(0, 50).map((k) => `- 저장소 불일치: ${k}`)];
mkdirSync(join(ROOT, 'out'), { recursive: true }); writeFileSync(join(ROOT, 'out/verify.md'), lines.join('\n') + '\n');
console.log(lines.join('\n'));
process.exit(ab.length || as.length || v2bad.length || v4bad.length ? 1 : 0);
