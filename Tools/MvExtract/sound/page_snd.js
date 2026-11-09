// MV-TOOL-SND 페이지 쪽 코드(03 §3.9.1). page.addScriptTag로 정본 로드 뒤 주입한다.
// 정본 SND는 즉시 실행 함수 안에 갇혀 있으므로 공용 기반의 정본 범위 eval(window.__mvScope)로 SND만 꺼낸다.
// 정본 코드는 고치지 않는다. 바꿔 끼우는 것은 window 전역(AudioContext, setInterval, Math.random)과
// AudioScheduledSourceNode.prototype.stop(정지 시각 기록)뿐이다.
(() => {
  'use strict';
  const W = window;
  const getSND = () => W.__mvScope('SND');

  // SND 전용 난수열(mulberry32, init.js와 같은 식). 페이지의 다른 타이머가 Math.random을 써도
  // 소리 잡음이 흔들리지 않게 SND 호출 동안만 Math.random을 이것으로 바꾼다.
  const mulberry32 = (seed) => {
    let st = (seed >>> 0) || 0x4d56;
    return () => {
      st = (st + 0x6D2B79F5) >>> 0;
      let t = st;
      t = Math.imul(t ^ (t >>> 15), t | 1);
      t ^= t + Math.imul(t ^ (t >>> 7), t | 61);
      return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
    };
  };
  const makeWithRng = (seed) => {
    const rng = mulberry32(seed);
    return (fn) => { const prev = Math.random; Math.random = rng; try { return fn(); } finally { Math.random = prev; } };
  };

  // BGM 스케줄러(setInterval(sched, 50), L1635)를 가로챈다. realtime이면 진짜 타이머로도 등록한다.
  // realtime이면 clock()(실제 오디오 시각)으로 타이머 호출 간격 최대값을 box.maxGap에 기록한다.
  const hookSetInterval = (withRng, realtime, clock) => {
    const realSI = W.setInterval;
    const box = { sched: null, maxGap: 0 };
    W.setInterval = function (fn, ms, ...rest) {
      if (ms === 50 && !box.sched) {
        box.sched = fn;
        W.setInterval = realSI;
        if (realtime) { let prev = clock(); return realSI(() => { const now = clock(); box.maxGap = Math.max(box.maxGap, now - prev); prev = now; withRng(fn); }, 50); }
        return -1;
      }
      return realSI.call(W, fn, ms, ...rest);
    };
    return box;
  };

  // 정본 정지 시각 max(stop t) 기록(길이 L 확인용)
  const hookStop = () => {
    const P = AudioScheduledSourceNode.prototype; const real = P.stop;
    const box = { max: 0, count: 0 };
    P.stop = function (t) { box.max = Math.max(box.max, t || 0); box.count++; return real.call(this, t); };
    return box;
  };

  // 결정적 합산. Chromium은 한 노드 입력에 연결된 여러 출력을 포인터 해시 집합 순서로 더해서, 셋 이상이 모이면
  // 덧셈 순서가 실행마다 달라져 1~2 ULP(≤3e-8) 차이가 난다(16bit로 바꿀 때 가끔 1 LSB가 뒤집혀 재실행 해시가 달라짐).
  // 노드마다 입력을 2개 이하로 만들려고, 두 번째 연결부터는 이득 1 GainNode 사슬로 받는다:
  // T ← {s1, J1}, J1 ← {s2, J2}, J2 ← {s3, J3} … (두 수의 덧셈은 교환법칙이 정확해 순서와 무관). 값은 정본 합과 같다.
  const hookDeterministicSum = () => {
    const P = AudioNode.prototype; const real = P.connect; const tails = new WeakMap();
    P.connect = function (dest, ...rest) {
      if (!(dest instanceof AudioNode) || rest.length) return real.call(this, dest, ...rest);
      const tail = tails.get(dest);
      if (tail === undefined) { tails.set(dest, null); return real.call(this, dest); }
      const j = this.context.createGain();
      real.call(j, tail || dest);
      real.call(this, j);
      tails.set(dest, j);
      return dest;
    };
  };

  const f32ToB64 = (f32) => {
    const u8 = new Uint8Array(f32.buffer, f32.byteOffset, f32.byteLength);
    let s = ''; const CH = 0x8000;
    for (let i = 0; i < u8.length; i += CH) s += String.fromCharCode.apply(null, u8.subarray(i, i + CH));
    return btoa(s);
  };

  const lastOffline = () => {
    const list = W.__mvAudio.contexts;
    const c = list[list.length - 1];
    if (!c) throw new Error('OfflineAudioContext가 만들어지지 않음(SND.init 실패)');
    return c;
  };

  // 효과음 1개: SND.init() → SND.play(lib, arg) → startRendering (03 §3.9.1-3)
  // prerollFrames > 0이면 그 시각(렌더 단위 배수)까지 무음으로 렌더한 뒤 SND.play를 부른다.
  // 정본 컴프레서는 컨텍스트 시작 직후 감쇠 상태에서 풀려 나오므로(Chromium DynamicsCompressor 초기 상태),
  // 시각 0에서 울리면 실제 게임에서 듣는 소리보다 작게 구워진다. 반환 pcm은 preroll을 포함한 전체다.
  async function renderSfx({ lib, arg, seed, prerollFrames }) {
    const SND = getSND();
    const withRng = makeWithRng(seed);
    const si = hookSetInterval(withRng, false);
    const stop = hookStop();
    hookDeterministicSum();
    withRng(() => SND.init());
    const ctx = lastOffline();
    if (ctx.currentTime !== 0) throw new Error('currentTime != 0');
    const play = () => withRng(() => SND.play(lib, arg == null ? undefined : arg));
    let t0 = 0;
    if (prerollFrames > 0) {
      const Off = W.__mvAudio.RealOfflineAudioContext.prototype;
      Off.suspend.call(ctx, prerollFrames / ctx.sampleRate).then(() => { t0 = ctx.currentTime; play(); return Off.resume.call(ctx); });
    } else play();
    const buf = await ctx.startRendering();
    if (stop.count === 0) throw new Error('SND.play가 아무 소리도 예약하지 않음: ' + lib);
    return { sr: buf.sampleRate, ch: buf.numberOfChannels, frames: buf.length, t0, maxStop: stop.max, L: stop.max - t0, sources: stop.count, schedCaptured: !!si.sched, pcm: f32ToB64(buf.getChannelData(0)) };
  }

  // BGM: intensity 설정 → SND.init() → 0.05 s 간격 suspend마다 sched() → 전체 렌더 (03 §3.9.1-4)
  async function renderBgm({ intensity, seed, intervalSec, quantum }) {
    const SND = getSND();
    const withRng = makeWithRng(seed);
    const si = hookSetInterval(withRng, false);
    hookDeterministicSum();
    SND.intensity = intensity;
    withRng(() => SND.init());
    const ctx = lastOffline();
    if (!si.sched) throw new Error('BGM 스케줄러를 가로채지 못함');
    const Off = W.__mvAudio.RealOfflineAudioContext.prototype;
    const sr = ctx.sampleRate, total = ctx.length;
    const step = intervalSec * sr;
    let calls = 0; const seen = new Set();
    for (let k = 1; ; k++) {
      const f = Math.round(k * step / quantum) * quantum;
      if (f >= total) break;
      if (seen.has(f)) continue; seen.add(f);
      Off.suspend.call(ctx, f / sr).then(() => { withRng(si.sched); calls++; return Off.resume.call(ctx); });
    }
    const buf = await ctx.startRendering();
    return { sr: buf.sampleRate, ch: buf.numberOfChannels, frames: buf.length, schedCalls: calls, pcm: f32ToB64(buf.getChannelData(0)) };
  }


  // ── V6: 정본 실시간 녹음(03 §3.11.1) ──
  // 진짜 AudioContext(44.1 kHz)로 정본 SND를 그대로 돌리고, ctx.destination으로 가는 신호를
  // AudioWorklet(오디오 스레드, 128 샘플마다 currentFrame과 함께)으로 PCM 그대로 받는다.
  // MediaRecorder는 Opus 손실 압축이고 ScriptProcessor는 헤드리스에서 128 샘플 블록을 빠뜨려 쓰지 않는다.
  const TAP_SRC = `class MvTap extends AudioWorkletProcessor {
    constructor(){ super(); this.buf = new Float32Array(4096); this.n = 0; this.f0 = 0; this.on = true; this.port.onmessage = () => { this.flush(); this.on = false; this.port.postMessage({ end: true }); }; }
    flush(){ if (this.n) { this.port.postMessage({ f: this.f0, d: this.buf.slice(0, this.n) }); this.n = 0; } }
    process(inputs){
      if (!this.on) return true;
      const ch = inputs[0] && inputs[0][0];
      if (this.n === 0) this.f0 = currentFrame;
      if (currentFrame !== this.f0 + this.n) { this.flush(); this.f0 = currentFrame; }
      for (let i = 0; i < 128; i++) this.buf[this.n + i] = ch ? ch[i] : 0;
      this.n += 128; if (this.n >= 4096) this.flush();
      return true;
    }
  }
  registerProcessor('mv-tap', MvTap);`;

  async function installRecorder() {
    const RA = W.__mvAudio.RealAudioContext;
    const ctx = new RA({ sampleRate: 44100 });
    const url = URL.createObjectURL(new Blob([TAP_SRC], { type: 'application/javascript' }));
    await ctx.audioWorklet.addModule(url);
    const realDest = ctx.destination;
    const tap = ctx.createGain();
    const node = new AudioWorkletNode(ctx, 'mv-tap', { numberOfInputs: 1, numberOfOutputs: 1, outputChannelCount: [1] });
    const rec = { ctx, chunks: [] };
    let endResolve; const ended = new Promise((r) => { endResolve = r; });
    node.port.onmessage = (e) => { if (e.data.end) endResolve(); else rec.chunks.push(e.data); };
    tap.connect(realDest); tap.connect(node); node.connect(realDest);
    Object.defineProperty(ctx, 'destination', { value: tap });
    // Chromium 실시간 컨텍스트는 한 작업(task) 안에서도 currentTime이 오디오 스레드를 따라 128 샘플씩 바뀐다.
    // 정본 tone()·sched()는 currentTime을 여러 번 읽으므로 음 사이 간격이 녹음마다 0~수 블록(최대 11.6 ms 관측) 흔들리고,
    // 이미 지난 시각에 예약된 음은 앞부분이 잘린다. 정본이 뜻한 상대 시각으로 녹음하려고, 녹음 컨텍스트의
    // currentTime을 한 작업 동안 고정(첫 읽기 값)하고 렌더 단위로 올린 뒤 LEAD만큼 앞당겨 보고한다(모든 예약이 미래가 됨).
    const ctDesc = Object.getOwnPropertyDescriptor(BaseAudioContext.prototype, 'currentTime');
    const LEAD = 18 * 128; let snap = null;
    Object.defineProperty(ctx, 'currentTime', { get() {
      if (snap === null) {
        const real = ctDesc.get.call(ctx);
        snap = (Math.ceil(real * ctx.sampleRate / 128) * 128 + LEAD) / ctx.sampleRate;
        queueMicrotask(() => { snap = null; });
      }
      return snap;
    } });
    rec.realNow = () => ctDesc.get.call(ctx);
    rec.finish = async () => { node.port.postMessage('stop'); await ended; };
    // 정본 init의 new AC()가 미리 만든 이 컨텍스트를 받게 한다
    function RecAudioContext() { return ctx; }
    W.AudioContext = RecAudioContext; W.webkitAudioContext = RecAudioContext;
    await ctx.resume();
    return rec;
  }
  const sleep = (ms) => new Promise((r) => setTimeout(r, ms));
  // currentFrame 기준으로 펼친다(빠진 블록은 0, gaps에 기록)
  const placeChunks = (chunks) => {
    let end = 0; for (const c of chunks) end = Math.max(end, c.f + c.d.length);
    const o = new Float32Array(end); let filled = 0;
    for (const c of chunks) { o.set(c.d, c.f); filled += c.d.length; }
    const first = chunks.length ? chunks[0].f : 0;
    return { pcm: o, gaps: end - first - filled };
  };

  async function recordSfx({ lib, arg, seed, prerollSec, tailMs }) {
    const SND = getSND();
    const withRng = makeWithRng(seed);
    const rec = await installRecorder();
    hookSetInterval(withRng, false); // 효과음 녹음에 BGM이 섞이지 않게 스케줄러는 등록만
    const stop = hookStop();
    withRng(() => SND.init());
    const ctx = rec.ctx;
    await sleep(prerollSec * 1000);
    const t0 = ctx.currentTime;
    withRng(() => SND.play(lib, arg == null ? undefined : arg));
    const L = stop.max - t0;
    await sleep(L * 1000 + (tailMs || 600));
    await rec.finish();
    const { pcm, gaps } = placeChunks(rec.chunks);
    await ctx.close();
    return { sr: ctx.sampleRate, t0, maxStop: stop.max, L, gaps, pcm: f32ToB64(pcm) };
  }

  async function recordBgm({ intensity, seed, seconds }) {
    const SND = getSND();
    const withRng = makeWithRng(seed);
    const rec = await installRecorder();
    const si = hookSetInterval(withRng, true, rec.realNow);
    // 정본 스케줄러가 실제로 예약한 시작 시각(루프 길이 대조용)
    const starts = []; const P = AudioScheduledSourceNode.prototype; const realStart = P.start;
    P.start = function (t, ...rest) { starts.push(t || 0); return realStart.call(this, t, ...rest); };
    SND.intensity = intensity;
    const ctx = rec.ctx;
    const tInit = ctx.currentTime;
    withRng(() => SND.init());
    await sleep(seconds * 1000);
    await rec.finish();
    const { pcm, gaps } = placeChunks(rec.chunks);
    await ctx.close();
    // 정본 sched 예약 창은 0.25 s다. 타이머가 0.2 s 넘게 늦으면(페이지 멈춤) 음이 밀려 이번 녹음은 정본 의도와 다르다.
    return { sr: ctx.sampleRate, tInit, gaps, starts, maxTimerGap: si.maxGap, pcm: f32ToB64(pcm) };
  }

  W.MvSnd = { renderSfx, renderBgm, recordSfx, recordBgm };
})();
