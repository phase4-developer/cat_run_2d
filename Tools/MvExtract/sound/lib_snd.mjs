// MV-TOOL-SND 공용 함수: 정의 읽기, 정본 페이지에서 렌더, WAV 쓰기·읽기, 대조 지표.
import { readFileSync } from 'node:fs';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import { openCanon, loadCanon, injectScopeHook } from '../lib/canon.mjs';

export const SND_DIR = dirname(fileURLToPath(import.meta.url));

export function loadDefs() {
  const d = JSON.parse(readFileSync(join(SND_DIR, 'snd.json'), 'utf8'));
  const sr = d.format.sampleRate;
  const jobs = [];
  for (const s of d.sfx) {
    if (s.enabled === false) continue;
    jobs.push({ ...s, soundType: 'Sfx', kind: 'sfx', frames: framesOf(s.L, sr) });
  }
  for (const b of d.bgm) {
    if (b.enabled === false) continue;
    jobs.push({ ...b, soundType: 'Music', kind: 'bgm', frames: d.bgmLoop.loopFrames, L: d.bgmLoop.loopFrames / sr });
  }
  const outPath = (j) => resolve(SND_DIR, d.outDir[j.soundType], j.soundName + '.wav');
  return { defs: d, sr, jobs, outPath };
}

// 03 §3.9.1-1: length = ceil(L × 44100). 부동소수 오차(0.12×44100 = 5291.9999…)를 지운다.
export const framesOf = (L, sr) => Math.ceil(Math.round(L * sr * 1e6) / 1e6);

// 시드 = 이름 해시(03 §3.2.2, init.js __mvHash와 같은 FNV-1a)
export const fnv1a = (s) => { let h = 0x811c9dc5; for (let i = 0; i < s.length; i++) { h ^= s.charCodeAt(i); h = Math.imul(h, 0x01000193); } return h >>> 0; };

const b64ToF32 = (b64) => { const b = Buffer.from(b64, 'base64'); return new Float32Array(b.buffer, b.byteOffset, b.byteLength / 4).slice(); };

// V6 녹음은 AudioWorklet(보안 컨텍스트 전용)을 쓴다. 정본 출처 http://mv.canon/ 은 보안 컨텍스트가 아니므로
// 녹음할 때만 같은 정본 본문(범위 고리 포함)을 http://localhost/ 로 한 번 더 열어 보안 컨텍스트로 만든다.
// 네트워크 규칙·init.js·글꼴은 openCanon이 단 것을 그대로 쓴다(나중에 단 route가 먼저 맞는다).
const SECURE_URL = 'http://localhost/MoonVillage.html';
async function reopenSecure(context, page) {
  const html = injectScopeHook(loadCanon().bytes.toString('utf8'));
  await context.route(SECURE_URL, (route) => route.fulfill({ status: 200, contentType: 'text/html; charset=utf-8', body: html }));
  const errors = []; page.on('pageerror', (e) => errors.push(String(e)));
  await page.goto(SECURE_URL, { waitUntil: 'load' });
  await page.waitForFunction(() => typeof window.__mvScope === 'function' && window.isSecureContext, null, { timeout: 30000 });
  if (errors.length) throw new Error('정본 실행 오류(localhost): ' + errors.join(' | '));
}

async function withCanonPage(seconds, sr, fn, { secure = false } = {}) {
  // init.js: length = ceil(sr × seconds). frames − 0.5를 넘겨 정확히 frames가 되게 한다.
  const { browser, context, page, blocked } = await openCanon({ initConfig: { audio: { seconds, sampleRate: sr, channels: 1 } } });
  try {
    if (secure) await reopenSecure(context, page);
    await page.addScriptTag({ path: join(SND_DIR, 'page_snd.js') });
    const r = await fn(page);
    if (blocked.length) throw new Error('외부 접속 시도: ' + blocked.join(', '));
    return r;
  } finally { await browser.close(); }
}

// 오프라인 렌더(굽기). 반환 Float32Array(모노)와 정보.
export async function renderJob(job, defs) {
  const sr = defs.format.sampleRate;
  const seed = fnv1a(job.soundName);
  if (job.kind === 'sfx') {
    const frames = job.frames, pre = defs.sfxPreroll.frames, total = pre + frames;
    const r = await withCanonPage((total - 0.5) / sr, sr, (page) => page.evaluate((a) => window.MvSnd.renderSfx(a), { lib: job.lib, arg: job.arg, seed, prerollFrames: pre }));
    if (r.frames !== total || r.sr !== sr || r.ch !== 1) throw new Error(`${job.soundName}: 렌더 형식 불일치 ${JSON.stringify({ frames: r.frames, sr: r.sr, ch: r.ch })}`);
    if (Math.round(r.t0 * sr) !== pre) throw new Error(`${job.soundName}: 재생 시각 ${r.t0} ≠ preroll ${pre / sr}`);
    // 03 표 L = 정본 정지 시각 max(at + d + 0.05). 정본이 실제로 예약한 정지 시각과 대조한다.
    if (Math.abs(r.L - job.L) > 1e-6) throw new Error(`${job.soundName}: 정본 정지 시각 ${r.L} ≠ 표 L ${job.L}`);
    const pcm = b64ToF32(r.pcm).slice(pre, total);
    return { pcm, info: { stopL: +r.L.toFixed(9), sources: r.sources } };
  }
  const lp = defs.bgmLoop;
  const start = Math.round(lp.startSec * sr) + lp.cutLoop * lp.loopFrames;
  const total = start + lp.loopFrames;
  const r = await withCanonPage((total - 0.5) / sr, sr, (page) => page.evaluate((a) => window.MvSnd.renderBgm(a), { intensity: job.intensity, seed, intervalSec: lp.schedIntervalSec, quantum: lp.renderQuantum }));
  if (r.frames !== total || r.sr !== sr || r.ch !== 1) throw new Error(`${job.soundName}: 렌더 형식 불일치`);
  const all = b64ToF32(r.pcm);
  return { pcm: all.slice(start, total), info: { schedCalls: r.schedCalls, cutStart: start } };
}

// 03 §3.9.1-7: 마지막 −90 dBFS 이상 샘플 뒤 10 ms만 남기고 자른다(원래 길이를 넘지 않음).
export function trimTail(pcm, sr, dbfs, keepMs) {
  const th = Math.pow(10, dbfs / 20);
  let last = -1;
  for (let i = pcm.length - 1; i >= 0; i--) if (Math.abs(pcm[i]) >= th) { last = i; break; }
  const n = Math.min(pcm.length, last + 1 + Math.round(keepMs * sr / 1000));
  return pcm.subarray(0, Math.max(1, n));
}

export function toInt16(pcm) {
  const o = new Int16Array(pcm.length);
  for (let i = 0; i < pcm.length; i++) { const x = Math.max(-1, Math.min(1, pcm[i])); o[i] = Math.round(x < 0 ? x * 32768 : x * 32767); }
  return o;
}

// WAV 16bit PCM. 헤더 44바이트, 부가 청크 없음(시각 등 비결정 정보 없음).
export function encodeWav(i16, sr, ch = 1) {
  const data = i16.length * 2; const b = Buffer.alloc(44 + data);
  b.write('RIFF', 0, 'ascii'); b.writeUInt32LE(36 + data, 4); b.write('WAVE', 8, 'ascii');
  b.write('fmt ', 12, 'ascii'); b.writeUInt32LE(16, 16); b.writeUInt16LE(1, 20); b.writeUInt16LE(ch, 22);
  b.writeUInt32LE(sr, 24); b.writeUInt32LE(sr * ch * 2, 28); b.writeUInt16LE(ch * 2, 32); b.writeUInt16LE(16, 34);
  b.write('data', 36, 'ascii'); b.writeUInt32LE(data, 40);
  Buffer.from(i16.buffer, i16.byteOffset, data).copy(b, 44);
  return b;
}

export function decodeWav(buf) {
  if (buf.toString('ascii', 0, 4) !== 'RIFF' || buf.toString('ascii', 8, 12) !== 'WAVE') throw new Error('WAV 아님');
  let p = 12, fmt = null, data = null;
  while (p + 8 <= buf.length) {
    const id = buf.toString('ascii', p, p + 4), n = buf.readUInt32LE(p + 4);
    if (id === 'fmt ') fmt = { format: buf.readUInt16LE(p + 8), channels: buf.readUInt16LE(p + 10), sampleRate: buf.readUInt32LE(p + 12), bits: buf.readUInt16LE(p + 22) };
    if (id === 'data') data = buf.subarray(p + 8, p + 8 + n);
    p += 8 + n + (n & 1);
  }
  if (!fmt || !data) throw new Error('WAV 청크 없음');
  const frames = data.length / (fmt.channels * fmt.bits / 8);
  const f32 = new Float32Array(frames);
  for (let i = 0; i < frames; i++) { const v = data.readInt16LE(i * 2 * fmt.channels); f32[i] = v < 0 ? v / 32768 : v / 32767; }
  return { ...fmt, frames, seconds: frames / fmt.sampleRate, pcm: f32 };
}

// ── V6 실시간 녹음 ──
export async function recordJob(job, defs) {
  const sr = defs.format.sampleRate;
  const seed = fnv1a(job.soundName);
  // 녹음 pcm의 첨자 = 실시간 컨텍스트 프레임. hint = 구운 파일 0번 샘플이 있어야 할 녹음 첨자.
  if (job.kind === 'sfx') {
    const r = await withCanonPage(1, sr, (page) => page.evaluate((a) => window.MvSnd.recordSfx(a), { lib: job.lib, arg: job.arg, seed, prerollSec: defs.sfxPreroll.frames / sr, tailMs: 600 }), { secure: true });
    return { pcm: b64ToF32(r.pcm), L: r.L, sr: r.sr, gaps: r.gaps, hint: Math.round(r.t0 * sr) };
  }
  const lp = defs.bgmLoop;
  const r = await withCanonPage(1, sr, (page) => page.evaluate((a) => window.MvSnd.recordBgm(a), { intensity: job.intensity, seed, seconds: lp.startSec + (lp.cutLoop + 1) * lp.loopFrames / sr + 0.8 }), { secure: true });
  const hint = Math.round((r.tInit + lp.startSec) * sr) + lp.cutLoop * lp.loopFrames;
  // 루프 길이 = 정본이 실시간으로 예약한 첫 음 시각과 64 step 뒤 같은 자리 음 시각의 차(가장 가까운 시작 시각)
  const s0 = r.tInit + lp.startSec, want = s0 + lp.loopFrames / sr;
  let near = null; for (const t of r.starts) if (near === null || Math.abs(t - want) < Math.abs(near - want)) near = t;
  return { pcm: b64ToF32(r.pcm), L: near - s0, sr: r.sr, gaps: r.gaps, hint, maxTimerGap: r.maxTimerGap };
}

// ── 대조 지표 ──
const rms = (a, o = 0, n = a.length) => { let s = 0; for (let i = 0; i < n; i++) { const x = a[o + i] || 0; s += x * x; } return Math.sqrt(s / Math.max(1, n)); };
const firstAbove = (a, th) => { for (let i = 0; i < a.length; i++) if (Math.abs(a[i]) >= th) return i; return -1; };

// 녹음에서 구운 파형이 시작하는 위치: 첫 소리 위치로 대략 맞추고 ±256 샘플에서 상관 최대로 다듬는다.
export function align(rec, ref, hint = null, off = 0) {
  const th = 1e-3;
  const a = firstAbove(ref, th), b = firstAbove(rec, th);
  if (a < 0 || b < 0) return -1;
  const guess = hint ?? (b - a); let best = -1, bestV = -Infinity;
  const n = Math.min(ref.length - off, 8192);
  for (let lag = guess - 256; lag <= guess + 256; lag++) {
    if (lag < 0 || lag + off + n > rec.length) continue;
    let s = 0; for (let i = 0; i < n; i++) s += ref[off + i] * rec[lag + off + i];
    if (s > bestV) { bestV = s; best = lag; }
  }
  return best;
}

// 짧은 시간 푸리에 변환 크기(Hann 1024, hop 512) — 간단한 radix-2 FFT
function fftMag(frame) {
  const n = frame.length; const re = Float64Array.from(frame), im = new Float64Array(n);
  for (let i = 1, j = 0; i < n; i++) { let bit = n >> 1; for (; j & bit; bit >>= 1) j ^= bit; j ^= bit; if (i < j) { [re[i], re[j]] = [re[j], re[i]]; } }
  for (let len = 2; len <= n; len <<= 1) {
    const ang = -2 * Math.PI / len, wr = Math.cos(ang), wi = Math.sin(ang);
    for (let i = 0; i < n; i += len) {
      let cr = 1, ci = 0;
      for (let k = 0; k < len / 2; k++) {
        const ar = re[i + k], ai = im[i + k], br = re[i + k + len / 2] * cr - im[i + k + len / 2] * ci, bi = re[i + k + len / 2] * ci + im[i + k + len / 2] * cr;
        re[i + k] = ar + br; im[i + k] = ai + bi; re[i + k + len / 2] = ar - br; im[i + k + len / 2] = ai - bi;
        const t = cr * wr - ci * wi; ci = cr * wi + ci * wr; cr = t;
      }
    }
  }
  const m = new Float64Array(n / 2 + 1); for (let i = 0; i <= n / 2; i++) m[i] = Math.hypot(re[i], im[i]); return m;
}
function spectrogram(a, o, n) {
  const N = 1024, H = 512, win = new Float64Array(N); for (let i = 0; i < N; i++) win[i] = 0.5 - 0.5 * Math.cos(2 * Math.PI * i / N);
  const out = []; const fr = new Float64Array(N);
  for (let s = 0; s + N <= n || s === 0; s += H) { for (let i = 0; i < N; i++) fr[i] = (s + i < n ? (a[o + s + i] || 0) : 0) * win[i]; out.push(...fftMag(fr)); if (s + N > n) break; }
  return out;
}
const pearson = (x, y) => { const n = Math.min(x.length, y.length); let mx = 0, my = 0; for (let i = 0; i < n; i++) { mx += x[i]; my += y[i]; } mx /= n; my /= n; let sxy = 0, sxx = 0, syy = 0; for (let i = 0; i < n; i++) { const a = x[i] - mx, b = y[i] - my; sxy += a * b; sxx += a * a; syy += b * b; } return sxy / Math.sqrt(sxx * syy); };

// V6 지표: 길이 차(ms) = |구운 길이 − 정본 실시간 정지 시각 L|, RMS 차(dB), 스펙트로그램 상관, 최대 표본 차
export function compare(baked, rec, recL, sr, { hint = null } = {}) {
  const lag = align(rec, baked, hint);
  if (lag < 0) return { ok: false, reason: '녹음 또는 구운 파일이 무음' };
  const n = baked.length;
  const r1 = rms(baked), r2 = rms(rec, lag, n);
  const rmsDb = 20 * Math.log10(r1 / r2);
  const corr = pearson(spectrogram(baked, 0, n), spectrogram(rec, lag, n));
  let maxDiff = 0; for (let i = 0; i < n; i++) maxDiff = Math.max(maxDiff, Math.abs(baked[i] - (rec[lag + i] || 0)));
  // 길이 차: 구운 길이 vs 정본 실시간 정지 시각 L(효과음) 또는 실시간 예약 루프 길이(BGM)
  const lenMs = Math.abs(n / sr - recL) * 1000;
  const ok = lenMs <= 1 && Math.abs(rmsDb) <= 0.5 && corr >= 0.98;
  return { ok, lenMs: +lenMs.toFixed(3), rmsDb: +rmsDb.toFixed(4), corr: +corr.toFixed(6), maxDiff: +maxDiff.toFixed(6), lag, lagFromHint: hint == null ? null : lag - hint };
}
