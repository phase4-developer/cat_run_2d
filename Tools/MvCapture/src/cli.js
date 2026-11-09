#!/usr/bin/env node
// MvCapture 명령 입구. 사용법은 Tools/MvCapture/README.md.
const fs = require('fs');
const path = require('path');
const H = require('./harness');
const S = require('./scene');

const args = process.argv.slice(2);
const cmd = args.shift();
const flag = (n, d) => { const i = args.indexOf('--' + n); if (i < 0) return d; const v = args[i + 1]; args.splice(i, 2); return v; };
const CAP_DIR = path.join(H.ROOT, 'QA', 'Captures', 'Proto');

// 작업 목록을 workers개 병렬로
async function pool(items, workers, fn) {
  const out = new Array(items.length); let i = 0;
  await Promise.all(Array.from({ length: Math.min(workers, items.length) }, async () => {
    while (i < items.length) { const k = i++; out[k] = await fn(items[k], k); }
  }));
  return out;
}

async function cmdCapture() {
  const runs = +flag('runs', 2), workers = +flag('workers', 4), outDir = flag('out', CAP_DIR);
  const ids = args.length ? args : S.listScenes();
  const verified = H.verify();
  const scenes = ids.map(S.load).filter(sc => !sc.protoNA);
  const skipped = ids.filter(id => !scenes.find(s => s.id === id));
  const browser = await H.launch();
  const t0 = Date.now();
  const rows = [];
  try {
    const jobs = []; for (const sc of scenes) for (let k = 0; k < runs; k++) jobs.push([sc, k]);
    const res = await pool(jobs, workers, async ([sc, k]) => {
      const t = Date.now(); const shots = await S.capture(browser, sc, { verified });
      process.stderr.write(`${sc.id} run${k + 1} ${((Date.now() - t) / 1000).toFixed(1)}s\n`);
      return shots;
    });
    for (let j = 0; j < scenes.length; j++) {
      const sc = scenes[j], per = res.slice(j * runs, j * runs + runs);
      const dir = path.join(outDir, sc.id); fs.mkdirSync(dir, { recursive: true });
      per[0].forEach((sh, n) => {
        const hs = per.map(p => H.sha256(p[n].png));
        const same = hs.every(h => h === hs[0]);
        const file = `${sc.id}/${String(sh.frame).padStart(5, '0')}.png`;
        fs.writeFileSync(path.join(outDir, file), sh.png);
        const st = sh.state;
        rows.push([sc.id, sh.frame, file, hs[0], same ? 'SAME' : 'DIFF:' + hs.join('/'), st.mode, st.z ?? '', st.x ?? '', st.level, st.area, st.rng]);
      });
    }
  } finally { await browser.close(); }
  const head = ['scene', 'frame', 'file', 'sha256', `repeat${runs}`, 'mode', 'z', 'x', 'level', 'area', 'rngCalls'];
  const tsv = [`# MV-QA-HARNESS 프로토 캡처 해시. 정본 SHA256 ${verified.proto}. 1080x1920 PNG. 같은 장면 ${runs}회 실행 비교.`, head.join('\t'), ...rows.map(r => r.join('\t'))].join('\n') + '\n';
  fs.writeFileSync(path.join(outDir, 'hashes.tsv'), tsv);
  const diff = rows.filter(r => r[4] !== 'SAME');
  console.log(`장면 ${scenes.length}개(제외 ${skipped.join(',') || '없음'}), 캡처 ${rows.length}장, 재현 불일치 ${diff.length}장, ${((Date.now() - t0) / 1000).toFixed(0)}s`);
  if (diff.length) { for (const r of diff) console.log('DIFF', r[0], r[1]); process.exitCode = 2; }
}

// 장면을 돌리며 매 프레임 식을 평가해 출력(장면 작성용). 예: probe W-02 "() => R && R.p.z" --to 600 --every 30
async function cmdProbe() {
  const to = +flag('to', 600), every = +flag('every', 1);
  const [id, expr] = args;
  const sc = S.load(id);
  const browser = await H.launch();
  try {
    const r = await S.start(browser, sc);
    const vals = await r.run(to, `(f) => (f % ${every} === 0) ? [f, (${expr})()] : null`);
    for (const v of vals) if (v) console.log(v[0], JSON.stringify(v[1]));
    if (r.s.errors.length) console.error(r.s.errors);
  } finally { await browser.close(); }
}

(async () => {
  if (cmd === 'verify') { const v = H.verify(); console.log(JSON.stringify({ proto: v.proto, vendor: v.vendor }, null, 1)); }
  else if (cmd === 'capture') await cmdCapture();
  else if (cmd === 'probe') await cmdProbe();
  else if (cmd === 'gen') await require('./gen').main(args, flag);
  else if (cmd === 'sim') await require('./sim').main(args, flag);
  else if (cmd === 'check') { for (const id of S.listScenes()) S.load(id); console.log('장면 정의 검사 통과', S.listScenes().length); }
  else { console.log('명령: verify | check | capture [장면ID...] [--runs 2] [--workers 4] | probe <장면ID> <식> [--to N] [--every N] | gen | sim'); process.exitCode = 1; }
})().catch(e => { console.error(e); process.exit(1); });
