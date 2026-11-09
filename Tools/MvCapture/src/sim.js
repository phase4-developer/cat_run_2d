// SIM 덤프(12 §12.3.1~12.3.2): 같은 시드·입력 기록으로 정본을 돌린 프레임별 결과 + 로직 난수 기록.
// 형식은 QA/Dumps/README.md.
const fs = require('fs');
const path = require('path');
const H = require('./harness');
const S = require('./scene');

const OUT = path.join(H.ROOT, 'QA', 'Dumps', 'sim');
const RUN_DIR = path.join(OUT, 'inputs');
// 기본 런: 장면 W-02~W-10(같은 save·seed·입력, 프레임만 다름)이 공유하는 런. W-10까지 덮도록 결과 화면까지 돈다.
const BASE_SCENE = 'W-02', BASE_FRAMES = 3560;

// 04 §4.17.1 「규칙」 줄(정본 줄 번호). L1323은 rnd(1.1,1.6)(쥐 속도)만 규칙이고 같은 줄의 Math.random()*6(ph)은 그림.
const LOGIC_LINES = { 1323: 'rnd', 1927: '', 1986: '', 2004: '', 2005: '', 2081: '', 2090: '', 2167: '', 2168: '', 2301: '', 2302: '' };

const PAGE_PROBE = `(() => {
  const ids = new WeakMap(); let next = 1; const last = new Map();
  let pm = null, pl = null, pf = null, pst = null, pcl = null, pc = '', pfin = false;
  const LOGIC = ${JSON.stringify(LOGIC_LINES)};
  const id = o => { let v = ids.get(o); if (!v) { v = next++; ids.set(o, v); } return v; };
  return (f) => {
    const rec = window.__mvq.rec, rng = [];
    for (const r of rec) { const need = LOGIC[r[3]];
      if (need !== undefined && (need === '' || r[6] === need)) rng.push([r[0], r[3], r[2]]); }
    rec.length = 0;
    const ev = [];
    if (mode !== pm) { ev.push(['mode', pm, mode]); pm = mode; }
    if (!R) return { f, mode, rng, ev };
    const P = R.p;
    const s = [P.x, P.z, P.tx, R.gold, R.souls, R.dia, R.gauge.lv, R.gauge.fill, R.projs.length, R.stairs, R.climbed, save.level, R.floor, R.time];
    if (save.level !== pl) { if (pl !== null) ev.push(['level', pl, save.level]); pl = save.level; }
    if (R.floor !== pf) { if (pf !== null) ev.push(['floor', pf, R.floor]); pf = R.floor; }
    if (R.stairs !== pst) { if (pst !== null) ev.push(['stairs', pst, R.stairs]); pst = R.stairs; }
    if (R.climbed !== pcl) { if (pcl !== null) ev.push(['climbed', pcl, R.climbed]); pcl = R.climbed; }
    const cs = JSON.stringify(R.cards); if (cs !== pc) { ev.push(['cards', JSON.parse(cs)]); pc = cs; }
    if (R.fin && !pfin) { ev.push(['finish', R.reason || '', !!R.pass]); pfin = true; }
    const add = [], chg = [], seen = new Set();
    for (const o of R.objs) {
      const k = id(o); seen.add(k);
      const cur = [o.x, o.z, o.hp, o.dead ? 1 : 0];
      const p = last.get(k);
      if (!p) add.push([k, o.k, ...cur, o.max ?? null]);
      else if (p[0] !== cur[0] || p[1] !== cur[1] || p[2] !== cur[2] || p[3] !== cur[3]) { chg.push([k, ...cur]); if (cur[3] && !p[3]) ev.push(['dead', k, o.k]); }
      last.set(k, cur);
    }
    const rm = []; for (const k of last.keys()) if (!seen.has(k)) { rm.push(k); last.delete(k); }
    return { f, mode, s, add, chg, rm, ev, rng };
  };
})()`;

const S_COLS = ['p.x', 'p.z', 'p.tx', 'R.gold', 'R.souls', 'R.dia', 'gauge.lv', 'gauge.fill', 'projs', 'R.stairs', 'R.climbed', 'save.level', 'R.floor', 'R.time'];

async function dumpOne(browser, sc, frames, verified, srcFile) {
  const r = await S.start(browser, sc, { verified });
  await r.s.page.evaluate(src => { window.__mvq.rec = []; window.__mvq.simProbe = window.__mvq.ev(src); }, PAGE_PROBE);
  // 결과 화면(R.fin) 뒤 60프레임에서 멈춘다(frames가 상한).
  const rows = [];
  let endAt = 0;
  for (let f = 0; f < frames; f += 500) {
    const part = await r.run(Math.min(frames, f + 500), 'window.__mvq.simProbe');
    for (const x of part) { if (!endAt && x.ev.some(e => e[0] === 'finish')) endAt = x.f; if (endAt && x.f > endAt + 60) break; rows.push(x); }
    if (endAt && rows[rows.length - 1].f >= endAt + 60) break;
  }
  frames = rows.length;
  const totalRng = await r.s.page.evaluate(() => window.__mvq.rngN);
  if (r.s.errors.length) throw new Error(`${sc.id}: ${r.s.errors[0]}`);
  await r.s.ctx.close();
  const head = { kind: 'SIM', version: 1, protoSha256: verified.proto, id: sc.id, source: srcFile, frames,
    command: 'node Tools/MvCapture/src/cli.js sim', scene: sc, sCols: S_COLS, addCols: ['id', 'k', 'x', 'z', 'hp', 'dead', 'max'], chgCols: ['id', 'x', 'z', 'hp', 'dead'],
    rngCols: ['seq', 'line', 'value'], logicLines: LOGIC_LINES, rngTotal: totalRng,
    logicRngTotal: rows.reduce((a, x) => a + x.rng.length, 0) };
  return JSON.stringify(head).replace(/}$/, ',"rows":[\n') + rows.map(x => JSON.stringify(x)).join(',\n') + '\n]}\n';
}

async function main(args, flag) {
  const verified = H.verify();
  fs.mkdirSync(OUT, { recursive: true });
  const jobs = [[S.load(BASE_SCENE), BASE_FRAMES, `QA/Scenes/${BASE_SCENE}.json`, 'SIM_BASE']];
  for (const f of fs.readdirSync(RUN_DIR).filter(f => /^RUN-\d+\.json$/.test(f)).sort()) {
    const sc = JSON.parse(fs.readFileSync(path.join(RUN_DIR, f), 'utf8')); S.validate(sc, f.replace(/\.json$/, ''));
    jobs.push([sc, sc.frames, `QA/Dumps/sim/inputs/${f}`, 'SIM_' + sc.id]);
  }
  const only = args.length ? new Set(args) : null;
  const browser = await H.launch();
  try {
    await Promise.all(jobs.filter(j => !only || only.has(j[3])).map(async ([sc, frames, src, name]) => {
      const t = Date.now();
      const text = await dumpOne(browser, sc, frames, verified, src);
      const file = path.join(OUT, name + '.json');
      fs.writeFileSync(file, text);
      console.log(`${H.sha256(Buffer.from(text))} QA/Dumps/sim/${name}.json ${(text.length / 1e6).toFixed(2)}MB ${((Date.now() - t) / 1000).toFixed(0)}s`);
    }));
  } finally { await browser.close(); }
}

module.exports = { main, LOGIC_LINES };
