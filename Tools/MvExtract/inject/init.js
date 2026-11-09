// MvExtract 결정성 장치 (03 §3.2.2). page.addInitScript로 정본보다 먼저 실행된다.
// 시계·난수·rAF·AudioContext를 고정한다. 그림(MV-TOOL-ART)과 소리(MV-TOOL-SND)가 같이 쓴다.
(() => {
  'use strict';
  const W = window;
  const cfg = W.__mvInitConfig || {};

  // ── 시계: performance.now / Date.now / new Date()는 window.__clock(초)를 따른다 ──
  W.__clock = typeof cfg.clock === 'number' ? cfg.clock : 0;
  const EPOCH = Date.UTC(2026, 0, 1, 0, 0, 0);
  const nowMs = () => W.__clock * 1000;
  performance.now = nowMs;
  const RealDate = Date;
  class MvDate extends RealDate {
    constructor(...a) { if (a.length === 0) super(EPOCH + nowMs()); else super(...a); }
    static now() { return EPOCH + nowMs(); }
  }
  W.Date = MvDate;

  // ── 난수: mulberry32. __mvSeed(n)로 다시 시드 ──
  let st = (cfg.seed >>> 0) || 0x4d56;
  const rnd = () => {
    st = (st + 0x6D2B79F5) >>> 0;
    let t = st;
    t = Math.imul(t ^ (t >>> 15), t | 1);
    t ^= t + Math.imul(t ^ (t >>> 7), t | 61);
    return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
  };
  Math.random = rnd;
  W.__mvSeed = (n) => { st = (n >>> 0) || 0x4d56; };
  // 문자열 → 32bit 해시(FNV-1a). job 이름 시드에 쓴다.
  W.__mvHash = (s) => { let h = 0x811c9dc5; for (let i = 0; i < s.length; i++) { h ^= s.charCodeAt(i); h = Math.imul(h, 0x01000193); } return h >>> 0; };

  // ── rAF: 저장만 하고 부르지 않는다(메인 루프 frame 정지). 필요하면 __mvStepRaf로 한 번 돌린다 ──
  const raf = new Map(); let rafId = 0;
  W.requestAnimationFrame = (cb) => { raf.set(++rafId, cb); return rafId; };
  W.cancelAnimationFrame = (id) => { raf.delete(id); };
  W.__mvRafPending = () => raf.size;
  W.__mvStepRaf = () => { const list = [...raf.values()]; raf.clear(); for (const cb of list) cb(nowMs()); return list.length; };

  // ── AudioContext: 장치 출력 없는 OfflineAudioContext로 바꾼다 ──
  // currentTime은 렌더 전까지 0이라 예약 시각이 결정적이다. MV-TOOL-SND는
  // __mvInitConfig.audio = { seconds, sampleRate, channels }를 주고 __mvAudio.contexts에서 꺼내 startRendering 한다.
  const Off = W.OfflineAudioContext;
  const ac = cfg.audio || {};
  W.__mvAudio = { contexts: [], RealAudioContext: W.AudioContext, RealOfflineAudioContext: Off };
  if (Off) {
    class MvAudioContext extends Off {
      constructor(opts) {
        const sr = (opts && opts.sampleRate) || ac.sampleRate || 44100;
        super({ numberOfChannels: ac.channels || 2, length: Math.max(1, Math.ceil(sr * (ac.seconds || 1))), sampleRate: sr });
        W.__mvAudio.contexts.push(this);
      }
      resume() { return Promise.resolve(); }
      suspend() { return Promise.resolve(); }
      close() { return Promise.resolve(); }
    }
    W.AudioContext = MvAudioContext;
    W.webkitAudioContext = MvAudioContext;
  }

  // 외부 호출 고리(정본은 window.claude?.hot 이 없으면 바로 boot)
  W.__mvInitDone = true;
})();
