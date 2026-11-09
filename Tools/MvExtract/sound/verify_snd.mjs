// MV-TOOL-SND 검수(03 §3.11.1 V6, 11 MV-TOOL-SND 완료·검증).
// 사용: node sound/verify_snd.mjs [--skip-rerun] [--skip-v6] [--only <key>[,…]]
//  0) 03_sound 표(--table-ref, 기본 dev) id·key·soundType·soundName = snd.json
//  1) 형식: 03_sound 32행 파일이 모두 있고 모노·44100 Hz·16bit, 길이 = 03 표(효과음 ceil(L×44100), BGM 729,931 샘플)
//  2) 재실행 해시: 같은 정의로 다시 구워 sound/out/rerun/ 에 쓰고 sha256이 저장된 파일과 같은지
//  3) V6: 정본 실시간 녹음(같은 시드) vs 구운 파일 — 길이 ±1 ms, RMS 차 ≤ 0.5 dB, 스펙트럼 상관 ≥ 0.98
// 결과: sound/out/verify.json, 종료 코드 0 = 모두 PASS
import { existsSync, mkdirSync, readFileSync, writeFileSync, rmSync } from 'node:fs';
import { join } from 'node:path';
import { createHash } from 'node:crypto';
import { execFileSync } from 'node:child_process';
import { loadDefs, decodeWav, recordJob, compare, SND_DIR } from './lib_snd.mjs';

const argv = process.argv.slice(2);
const has = (k) => argv.includes(k);
const opt = (k) => { const i = argv.indexOf(k); return i >= 0 ? argv[i + 1] : null; };
const only = opt('--only') ? opt('--only').split(',') : null;
const { defs, sr, jobs, outPath } = loadDefs();
const sel = jobs.filter((j) => !only || only.includes(j.key) || only.includes(j.soundName));
const sha = (b) => createHash('sha256').update(b).digest('hex');
const OUT = join(SND_DIR, 'out'); mkdirSync(OUT, { recursive: true });
const rep = { expectedRows: 32, jobs: jobs.length, format: [], rerun: null, v6: [] };
let bad = 0;

// 0) 03_sound 표 대조: id·key·soundType·soundName이 정의와 같은지(표는 git 참조에서 읽음, 기본 dev)
{
  const ref = opt('--table-ref') || 'dev';
  try {
    const tsv = execFileSync('git', ['show', `${ref}:Assets/Game/GameData/Table/TSV/03_sound.txt`], { cwd: SND_DIR, encoding: 'utf8' });
    const rows = tsv.replace(/\r/g, '').trim().split('\n').slice(1).map((l) => l.split('\t'));
    const want = jobs.map((j) => [String(j.id), j.key, j.soundType, j.soundName].join('\t'));
    const got = rows.map((r) => r.slice(0, 4).join('\t'));
    const diff = want.filter((w) => !got.includes(w)).concat(got.filter((g) => !want.includes(g)).map((g) => '표에만: ' + g));
    rep.table = { ref, rows: rows.length, ok: diff.length === 0 && rows.length === 32, diff };
  } catch (e) { rep.table = { ref, ok: false, reason: e.message.split('\n')[0] }; }
  if (!rep.table.ok) bad++;
  console.log(`03_sound(${rep.table.ref}): ${rep.table.ok ? '32행 일치' : 'FAIL ' + JSON.stringify(rep.table)}`);
}

// 1) 형식
for (const j of sel) {
  const f = outPath(j);
  if (!existsSync(f)) { rep.format.push({ soundName: j.soundName, ok: false, reason: '파일 없음' }); bad++; continue; }
  const w = decodeWav(readFileSync(f));
  const ok = w.format === 1 && w.channels === defs.format.channels && w.sampleRate === sr && w.bits === defs.format.bitsPerSample && w.frames === j.frames;
  rep.format.push({ soundName: j.soundName, ok, channels: w.channels, sampleRate: w.sampleRate, bits: w.bits, frames: w.frames, expectFrames: j.frames, seconds: +w.seconds.toFixed(6), tableL: j.L });
  if (!ok) bad++;
}
if (!only && jobs.length !== rep.expectedRows) { bad++; rep.countError = `정의 ${jobs.length} ≠ 32`; }
console.log(`형식: ${rep.format.filter((x) => x.ok).length}/${sel.length} PASS`);

// 2) 재실행 해시
if (!has('--skip-rerun')) {
  const dir = join(OUT, 'rerun'); rmSync(dir, { recursive: true, force: true });
  const args = [join(SND_DIR, 'run_snd.mjs'), '--out', dir]; if (only) args.push('--only', only.join(','));
  execFileSync(process.execPath, args, { stdio: 'inherit' });
  const diff = [];
  for (const j of sel) {
    const a = existsSync(outPath(j)) ? sha(readFileSync(outPath(j))) : null;
    const b = sha(readFileSync(join(dir, j.soundType, j.soundName + '.wav')));
    if (a !== b) diff.push({ soundName: j.soundName, saved: a, rerun: b });
  }
  rep.rerun = { ok: diff.length === 0, compared: sel.length, diff };
  if (diff.length) bad++;
  console.log(`재실행 해시: ${sel.length - diff.length}/${sel.length} 같음`);
}

// 3) V6 정본 실시간 녹음 대조(실시간이라 하나씩 차례로)
if (!has('--skip-v6')) {
  for (const j of sel) {
    const baked = decodeWav(readFileSync(outPath(j))).pcm;
    // 실시간 녹음은 페이지 타이머가 늦으면(BGM sched 창 0.25 s) 정본 의도와 다른 소리가 녹음된다.
    // 타이머 간격이 0.2 s를 넘긴 녹음, 블록이 빠진 녹음은 무효로 보고 최대 3번까지 다시 녹음한다(시도 수 기록).
    let r, attempts = 0;
    for (;;) {
      attempts++;
      try {
        const rec = await recordJob(j, defs);
        if (rec.sr !== sr) throw new Error('녹음 샘플레이트 ' + rec.sr);
        r = compare(baked, rec.pcm, rec.L, sr, { hint: rec.hint });
        r.recL = +rec.L.toFixed(7); r.recGaps = rec.gaps;
        if (rec.maxTimerGap != null) r.maxTimerGapMs = +(rec.maxTimerGap * 1000).toFixed(1);
        const invalid = rec.gaps > 0 || (rec.maxTimerGap != null && rec.maxTimerGap > 0.2);
        if (invalid) { r.ok = false; r.reason = '녹음 무효(블록 빠짐 또는 타이머 지연)'; if (attempts < 3) continue; }
      } catch (e) { r = { ok: false, reason: e.message }; }
      break;
    }
    r.attempts = attempts;
    rep.v6.push({ soundName: j.soundName, ...r });
    if (!r.ok) bad++;
    console.log(`V6 ${j.soundName}: ${r.ok ? 'PASS' : 'FAIL'} ${JSON.stringify(r)}`);
  }
}

rep.pass = bad === 0;
writeFileSync(join(OUT, 'verify.json'), JSON.stringify(rep, null, 2) + '\n');
console.log(rep.pass ? 'SND VERIFY PASS' : `SND VERIFY FAIL (${bad})`);
process.exit(rep.pass ? 0 : 1);
