// MV-TOOL-SND 굽기 진입점(03 §3.9). 사용: node sound/run_snd.mjs [--only <key|soundName>[,…]] [--out <폴더>] [-j <동시 수>]
// --out이 없으면 snd.json outDir(Addressables/Remote/Audio/Sfx|Music)에 쓰고 sound/manifest.json을 갱신한다.
// --out이 있으면 <폴더>/<Sfx|Music>/<soundName>.wav로만 쓴다(재실행 해시 대조용).
import { mkdirSync, writeFileSync } from 'node:fs';
import { dirname, join, relative, resolve } from 'node:path';
import { createHash } from 'node:crypto';
import { loadDefs, renderJob, trimTail, toInt16, encodeWav, SND_DIR } from './lib_snd.mjs';
import { REPO } from '../lib/canon.mjs';

const argv = process.argv.slice(2);
const opt = (k) => { const i = argv.indexOf(k); return i >= 0 ? argv[i + 1] : null; };
const only = opt('--only') ? opt('--only').split(',') : null;
const outRoot = opt('--out') ? resolve(opt('--out')) : null;
const conc = Math.max(1, +(opt('-j') || 4));

const { defs, sr, jobs, outPath } = loadDefs();
const sel = jobs.filter((j) => !only || only.includes(j.key) || only.includes(j.soundName));
if (!sel.length) { console.error('대상 없음'); process.exit(1); }

const sha = (b) => createHash('sha256').update(b).digest('hex');
const results = []; let failed = 0;

async function bake(job) {
  const t = Date.now();
  const { pcm, info } = await renderJob(job, defs);
  const cut = job.kind === 'sfx' && defs.trim.enabled ? trimTail(pcm, sr, defs.trim.dbfs, defs.trim.keepMs) : pcm;
  const wav = encodeWav(toInt16(cut), sr, 1);
  const file = outRoot ? join(outRoot, job.soundType, job.soundName + '.wav') : outPath(job);
  mkdirSync(dirname(file), { recursive: true });
  writeFileSync(file, wav);
  let peak = 0; for (const x of cut) peak = Math.max(peak, Math.abs(x));
  const r = { id: job.id, key: job.key, soundType: job.soundType, soundName: job.soundName, file: relative(REPO, file).replace(/\\/g, '/'),
    sampleRate: sr, channels: 1, bitsPerSample: 16, frames: cut.length, seconds: +(cut.length / sr).toFixed(6), tableL: job.L, tableFrames: job.frames,
    peakDbfs: +(20 * Math.log10(peak || 1e-12)).toFixed(2), sha256: sha(wav), src: job.src, ...info };
  console.log(`${job.soundName}: ${r.frames} 샘플 (${r.seconds}s) peak ${r.peakDbfs} dBFS sha ${r.sha256.slice(0, 12)} — ${Date.now() - t} ms`);
  return r;
}

const queue = [...sel];
await Promise.all(Array.from({ length: Math.min(conc, queue.length) }, async () => {
  while (queue.length) {
    const job = queue.shift();
    try { results.push(await bake(job)); } catch (e) { failed++; console.error(`실패 ${job.soundName}: ${e.message}`); }
  }
}));

results.sort((a, b) => a.id - b.id);
if (!outRoot && !only && !failed) {
  const manifest = { canonSha256: JSON.parse((await import('node:fs')).readFileSync(join(SND_DIR, '..', 'canon.json'), 'utf8')).sha256,
    format: defs.format, count: results.length, sounds: results.map(({ schedCalls, cutStart, sources, maxStop, ...r }) => r) };
  writeFileSync(join(SND_DIR, 'manifest.json'), JSON.stringify(manifest, null, 2) + '\n');
}
console.log(`완료 ${results.length}/${sel.length}${failed ? `, 실패 ${failed}` : ''}`);
process.exit(failed ? 1 : 0);
