// GEN 덤프(12 §12.3.1): 정본 buildLevel 결과 + fieldSpec 골든(D-27). 형식은 QA/Dumps/README.md.
const fs = require('fs');
const path = require('path');
const H = require('./harness');

const OUT = path.join(H.ROOT, 'QA', 'Dumps', 'gen');
const PRESETS = ['easy', 'mid', 'orig'];
const LEVELS = 20, AREAS = 15, FLOORS = 3, FIELD_AREAS = 30;
const RNG = { mode: 'const', c: 0.5 }; // 04 §4.17.2-5: 상수 난수로 대조

// 정본 IIFE 안에서 도는 함수(문자열로 넘긴다). 오브젝트의 원시값 속성만 남긴다(mesh 등 그림 참조 제외).
const PAGE_GEN = `({ preset, levels, areas, floors }) => {
  Object.assign(BAL, BALS[preset]); DBG.rate = BAL.rate;
  const prim = v => v === null || ['number', 'string', 'boolean'].includes(typeof v);
  const pick = o => { const r = {}; for (const k of Object.keys(o)) { const v = o[k];
    if (prim(v)) r[k] = v; else if (Array.isArray(v) && v.every(prim)) r[k] = v.slice(); } return r; };
  const base = JSON.parse(JSON.stringify(DEF()));
  const entries = [];
  for (let L = 1; L <= levels; L++) for (let A = 0; A < areas; A++) for (let cp = 0; cp < floors; cp++) {
    save = Object.assign(DEF(), { level: L, area: A, zone: zoneOf(A), cp, bars: {} });
    newRun();
    entries.push({ level: L, area: A + 1, floor: cp + 1, cp, tpl: R.tpl, base: R.base, gz: R.gz,
      Z: pick(R.Z), gates: R.gates.map(pick), objs: R.objs.map(pick) });
  }
  return { bal: Object.assign({}, BAL), saveBase: base, entries };
}`;

const PAGE_FIELD = `({ preset, areas }) => {
  Object.assign(BAL, BALS[preset]); DBG.rate = BAL.rate;
  save = DEF();
  const out = [];
  for (let A = 0; A < areas; A++) for (let f = 0; f < 3; f++) {
    const [h0, h1, rows, souls] = FINAL[Math.min(A, FINAL.length - 1)][f], n = rows * 3;
    const seed = (A + 1) * 97 + f * 131 + 7, sN = Math.max(1, Math.ceil(rows / 3));
    // 정본 fieldSpec(L1766~1778)이 hash()에 넣는 입력과 결과(골든). 결과 칸은 정본 fieldSpec 그대로.
    const soulHash = [], have = [];
    for (let k = 0; have.length < sN && k < 400; k++) { const x = seed + k * 7.31, h = hash(x), i = 3 + Math.floor(h * (n - 3)); soulHash.push([x, h]); if (!have.includes(i)) have.push(i); }
    const diaHash = []; for (let idx = 0; idx < n; idx++) { const x = seed + idx * 3.7 + 11; diaHash.push([x, hash(x)]); }
    out.push({ area: A + 1, floor: f + 1, final: [h0, h1, rows, souls], finalRow: Math.min(A, FINAL.length - 1) + 1, seed, soulHash, diaHash, cells: fieldSpec(A, f) });
  }
  return out;
}`;

function stable(v) { return JSON.stringify(v); }

async function main(args, flag) {
  const only = flag('preset', null);
  const verified = H.verify();
  fs.mkdirSync(OUT, { recursive: true });
  const browser = await H.launch();
  const files = [];
  try {
    for (const preset of only ? [only] : PRESETS) {
      const s = await H.open(browser, { seed: 0, rngMode: RNG.mode, rngConst: RNG.c, save: null }, { verified });
      const t = Date.now();
      const g = await s.ev(PAGE_GEN, { preset, levels: LEVELS, areas: AREAS, floors: FLOORS });
      const field = await s.ev(PAGE_FIELD, { preset, areas: FIELD_AREAS });
      if (s.errors.length) throw new Error(s.errors[0]);
      await s.ctx.close();
      // 오브젝트를 종류(k)별 키 표 + 행 배열로 줄인다. 행 = [k, ...objKeys[k] 순서의 값(없으면 null)].
      const keys = {};
      for (const e of g.entries) for (const o of e.objs) { const ks = keys[o.k] || (keys[o.k] = new Set()); for (const k of Object.keys(o)) if (k !== 'k') ks.add(k); }
      for (const k of Object.keys(keys)) keys[k] = [...keys[k]].sort();
      const head = { kind: 'GEN', version: 1, protoSha256: verified.proto, preset, bal: g.bal, rng: RNG,
        command: `node Tools/MvCapture/src/cli.js gen --preset ${preset}`,
        range: { level: [1, LEVELS], area: [1, AREAS], floor: [1, FLOORS] }, saveBase: g.saveBase, objKeys: keys };
      const lines = g.entries.map(e => stable({ level: e.level, area: e.area, floor: e.floor, cp: e.cp, tpl: e.tpl, base: e.base, gz: e.gz, Z: e.Z, gates: e.gates,
        objs: e.objs.map(o => [o.k, ...keys[o.k].map(k => (k in o ? o[k] : null))]) }));
      const genText = stable(head).replace(/}$/, ',"entries":[\n') + lines.join(',\n') + '\n]}\n';
      const fGen = path.join(OUT, `GEN_${preset}.json`);
      fs.writeFileSync(fGen, genText);
      const fieldText = stable({ kind: 'FIELD', version: 1, protoSha256: verified.proto, preset, bal: g.bal, hash: 'x => { s = Math.sin(x*127.1 + 311.7)*43758.5453; return s - Math.floor(s); } (정본 L858)',
        command: `node Tools/MvCapture/src/cli.js gen --preset ${preset}`, range: { area: [1, FIELD_AREAS], floor: [1, 3] } }).replace(/}$/, ',"fields":[\n') + field.map(stable).join(',\n') + '\n]}\n';
      const fField = path.join(OUT, `FIELD_${preset}.json`);
      fs.writeFileSync(fField, fieldText);
      files.push(fGen, fField);
      console.log(`${preset}: GEN ${g.entries.length}칸 ${(genText.length / 1e6).toFixed(1)}MB, FIELD ${field.length}칸, ${((Date.now() - t) / 1000).toFixed(0)}s`);
    }
  } finally { await browser.close(); }
  for (const f of files) console.log(H.sha256(fs.readFileSync(f)), path.relative(H.ROOT, f).replace(/\\/g, '/'));
}

module.exports = { main };
