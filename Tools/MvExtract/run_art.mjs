// 그림 추출 실행기 (03 §3.2). npm run art -- [--only <glob>] [--group <아틀라스>] [--out <폴더>] [--probe] [--accept-canon <sha>]
// jobs/*.json → PNG + manifest.json + sprite_catalog.tsv (+ out/report.md)
import { mkdirSync, readFileSync, readdirSync, writeFileSync, existsSync } from 'node:fs';
import { join, relative, resolve } from 'node:path';
import { openCanon, ROOT, REPO, sha256 } from './lib/canon.mjs';
import { loadThemes } from './lib/tables.mjs';

const argv = process.argv.slice(2);
const opt = (k, d = null) => { const i = argv.indexOf(k); return i >= 0 ? argv[i + 1] : d; };
const flag = (k) => argv.includes(k);
const OUT = resolve(opt('--out', join(REPO, 'Assets/Game/Art/Source')));
const only = opt('--only'), group = opt('--group'), themeOnly = opt('--theme');
const PROBE = flag('--probe');
const glob = (g) => new RegExp('^' + g.split('*').map((s) => s.replace(/[.+?^${}()|[\]\\]/g, '\\$&')).join('.*') + '$');

const jobs = [];
for (const f of readdirSync(join(ROOT, 'jobs')).filter((f) => f.endsWith('.json')).sort()) {
  for (const j of JSON.parse(readFileSync(join(ROOT, 'jobs', f), 'utf8'))) jobs.push({ ...j, _file: f });
}
const pick = jobs.filter((j) => (!only || glob(only).test(j.name)) && (!group || j.atlas === group));
const themes = loadThemes();
const themeList = themeOnly ? themes.filter((t) => t.key === themeOnly) : themes;

const pad2 = (n) => String(n).padStart(2, '0');
const r4 = (n) => Math.ceil(n / 4) * 4;

const { browser, page, blocked } = await openCanon({ acceptSha: opt('--accept-canon') });
await page.addScriptTag({ path: join(ROOT, 'inject/harness.js') });
if (!(await page.evaluate(() => MvH.loadEmoji()))) throw new Error('이모지 글꼴 확인 실패');

const manifest = [], catalog = [], failures = [];
const write = (rel, buf) => { const p = join(OUT, rel); mkdirSync(join(p, '..'), { recursive: true }); writeFileSync(p, buf); return p; };

for (const job of pick) {
  if (job.canvas.w % 4 || job.canvas.h % 4) { failures.push(`${job.name}: 캔버스가 4의 배수가 아님`); continue; }
  const variants = job.themeSet === 'each' ? themeList.map((t) => ({ theme: t })) : [{ theme: null }];
  for (const vv of variants) {
    // each: AREA_TH[0] 내용을 그 테마 행 색으로 바꾸고 job.theme = 0
    let j = { ...job };
    if (vv.theme) {
      await page.evaluate((th) => { const T = MvH.S('AREA_TH[0]'); window.__mvThemeBak = window.__mvThemeBak || JSON.stringify(T); Object.assign(T, JSON.parse(window.__mvThemeBak), th); }, vv.theme.areaTh);
      j.theme = 0;
    }
    const frames = j.frames ? Array.from({ length: j.frames.count }, (_, i) => j.frames.from + (i * j.frames.period) / j.frames.count) : [null];
    const outFrames = [];
    for (let i = 0; i < frames.length; i++) {
      const name = j.frames ? `${j.name}_${pad2(i)}` : j.name;
      let r;
      try {
        if (PROBE) j = { ...j, canvas: { w: j.canvas.w * 3, h: j.canvas.h * 3, pivotPx: [j.canvas.pivotPx[0] + j.canvas.w, j.canvas.pivotPx[1] + j.canvas.h] } };
        r = j.mode === 'svg' ? await page.evaluate((jj) => MvH.renderSvg(jj), j) : await page.evaluate(([jj, v]) => MvH.renderOne(jj, v), [j, frames[i]]);
      } catch (e) { failures.push(`${name}: 예외 ${String(e.message || e).split('\n')[0]}`); continue; }
      if (PROBE) { const bb = await page.evaluate((u) => new Promise((res) => { const im = new Image(); im.onload = () => { const c = document.createElement('canvas'); c.width = im.width; c.height = im.height; const g = c.getContext('2d'); g.drawImage(im, 0, 0); const d = g.getImageData(0, 0, c.width, c.height).data; let x0 = 1e9, y0 = 1e9, x1 = -1, y1 = -1; for (let y = 0; y < c.height; y++) for (let x = 0; x < c.width; x++) if (d[(y * c.width + x) * 4 + 3]) { x0 = Math.min(x0, x); y0 = Math.min(y0, y); x1 = Math.max(x1, x); y1 = Math.max(y1, y); } res([x0, y0, x1, y1]); }; im.src = u; }), r.dataURL);
        const [px, py] = j.canvas.pivotPx; console.log(`PROBE ${name} left ${px - bb[0]} right ${bb[2] - px + 1} up ${py - bb[1]} down ${bb[3] - py + 1}`); j = { ...job }; continue; }
      const errs = [];
      if (r.alpha.empty) errs.push('빈 그림');
      if (r.alpha.edgeHit > 0 && !job.allowEdge) errs.push(`가장자리 2px 안 알파 ${r.alpha.edgeHit}`);
      if (r.zeroRules.length) errs.push(`억제 규칙 0건 일치 #${r.zeroRules.join(',')}`);
      if (errs.length) { failures.push(`${name}${vv.theme ? '@' + vv.theme.key : ''}: ${errs.join(', ')}`); continue; }
      const png = Buffer.from(r.dataURL.split(',')[1], 'base64');
      const folder = j.texture ? 'Texture' : `Sprite/${vv.theme ? `Atlas_World_Theme_${vv.theme.key}` : j.atlas}`;
      const targets = j.themeSet === 'all' ? themeList.map((t) => `Sprite/Atlas_World_Theme_${t.key}`) : [folder];
      for (const fo of targets) {
        const rel = `${fo}/${name}.png`;
        write(rel, png);
        if (!j.frames) {
          manifest.push({ name, atlas: fo.replace(/^Sprite\//, ''), theme: vv.theme ? vv.theme.key : (j.themeSet === 'all' ? fo.split('Atlas_World_Theme_')[1] : null), file: rel, w: r.w, h: r.h,
            pivot: [+(j.canvas.pivotPx[0] / r.w).toFixed(5), +(1 - j.canvas.pivotPx[1] / r.h).toFixed(5)], pivotName: j.pivot, ppu: j.ppu || 128, border: j.border || null, src: j.src, args: j.args || null, sha256: sha256(png) });
          catalog.push([fo.replace(/^Sprite\//, ''), name, 0]);
        } else outFrames.push({ fo, rel, name, t: frames[i], sha256: sha256(png), w: r.w, h: r.h });
      }
    }
    if (j.frames && outFrames.length) {
      const byFolder = {};
      for (const f of outFrames) (byFolder[f.fo] ||= []).push(f);
      for (const [fo, fl] of Object.entries(byFolder)) {
        manifest.push({ name: j.name, atlas: fo.replace(/^Sprite\//, ''), theme: vv.theme ? vv.theme.key : null, file: null, w: fl[0].w, h: fl[0].h,
          pivot: [+(j.canvas.pivotPx[0] / fl[0].w).toFixed(5), +(1 - j.canvas.pivotPx[1] / fl[0].h).toFixed(5)], pivotName: j.pivot, ppu: j.ppu || 128, border: null,
          frames: fl.map((f) => ({ file: f.rel, name: f.name, [j.frames.var]: +f.t.toFixed(6), sha256: f.sha256 })), fps: j.frames.fps ?? null, drive: j.frames.var === 'ph' ? 'phase' : 'time', src: j.src, args: j.args || null });
        for (const f of fl) catalog.push([fo.replace(/^Sprite\//, ''), f.name, 0]);
      }
    }
  }
}
await browser.close();
if (PROBE) process.exit(0);

manifest.sort((a, b) => (a.atlas + a.name).localeCompare(b.atlas + b.name));
catalog.sort((a, b) => (a[0] + '\t' + a[1]).localeCompare(b[0] + '\t' + b[1]));
const partial = !!(only || group || themeOnly);
const mPath = join(OUT, 'manifest.json');
let merged = manifest;
if (partial && existsSync(mPath)) { // 일부 실행이면 기존 항목과 합친다
  const keep = JSON.parse(readFileSync(mPath, 'utf8')).items.filter((m) => !manifest.some((n) => n.name === m.name && n.atlas === m.atlas));
  merged = keep.concat(manifest).sort((a, b) => (a.atlas + a.name).localeCompare(b.atlas + b.name));
}
writeFileSync(mPath, JSON.stringify({ canon: 'fe573cf2f8ed9b90030c993e8127ecd479f4f2d47195d3fbfa19962694926dfa', tool: 'Tools/MvExtract run_art.mjs', items: merged }, null, 1) + '\n');
const cat = merged.flatMap((m) => (m.frames ? m.frames.map((f) => [m.atlas, f.name, 0]) : [[m.atlas, m.name, 0]])).sort((a, b) => (a[0] + '\t' + a[1]).localeCompare(b[0] + '\t' + b[1]));
writeFileSync(join(OUT, 'sprite_catalog.tsv'), 'atlas\tname\ttemp\n' + cat.map((r) => r.join('\t')).join('\n') + '\n');

const pngCount = cat.length;
const rep = [`# MvExtract 그림 추출 보고`, ``, `- job ${pick.length}, 카탈로그 행 ${pngCount}, 실패 ${failures.length}, 차단된 외부 요청 ${blocked.length}`, ``, ...failures.map((f) => `- 실패: ${f}`)].join('\n');
mkdirSync(join(ROOT, 'out'), { recursive: true }); writeFileSync(join(ROOT, 'out/report.md'), rep + '\n');
console.log(rep);
process.exit(failures.length || blocked.length ? 1 : 0);
