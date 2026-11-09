// 페이지 로드 전에 주입되는 스크립트(정본 HTML은 바꾸지 않는다).
// 이 파일의 함수 본문이 그대로 page.addInitScript로 들어간다. Node 쪽 값은 cfg 하나로만 받는다.
//
// 하는 일
//  1. 시간: performance.now / Date.now / new Date() 를 가짜 시계로, requestAnimationFrame·setTimeout·
//     setInterval 을 가짜 큐로 바꾼다. 하네스가 __mvq.step()으로 1/60초씩 진행한다.
//     CSS 애니메이션·트랜지션도 같은 시계로 맞춘다(document.getAnimations()의 currentTime 설정).
//  2. 난수: Math.random 을 mulberry32(seed) 또는 상수(c)로 바꾸고, 기록을 켜면 (순번, 값, 호출 줄)을 남긴다.
//  3. 저장: localStorage['catescape_moon_v1'] 에 장면 정의의 save JSON(정본 형식)을 넣는다.
//  4. 부팅: 정본은 window.claude.hot.ready(boot)가 있으면 그 콜백으로 부팅한다(L3888). 그 고리로
//     부팅 시점을 잡고, debugger 문에서 멈춰 하네스(CDP)가 IIFE 안쪽 변수에 닿는 __mvq.ev 를 만든다.
module.exports = function mvqInject(cfg) {
  'use strict';
  const realDate = Date;
  const T0 = 1000;                       // performance.now() 시작 값(ms)
  let now = T0;                          // 가짜 performance.now()
  const epoch = cfg.epochMs;             // Date.now() = epoch + (now - T0)
  const FRAME_MS = 1000 / 60;

  // ── 시간 ──
  const perfNow = () => now;
  try { Object.defineProperty(performance, 'now', { value: perfNow, configurable: true, writable: true }); } catch (_) { performance.now = perfNow; }
  function FakeDate(...a) {
    if (!new.target) return new realDate(epoch + (now - T0)).toString();
    return a.length ? new realDate(...a) : new realDate(epoch + (now - T0));
  }
  FakeDate.prototype = realDate.prototype;
  FakeDate.now = () => epoch + (now - T0);
  FakeDate.UTC = realDate.UTC; FakeDate.parse = realDate.parse;
  window.Date = FakeDate;

  // 정본 소리(SND, L1630~1690)는 ctx.currentTime으로 재생 간격을 거르고 잡음 버퍼에 Math.random을 쓴다.
  // 실제 오디오 시계면 난수 소비 횟수가 실행마다 달라지므로 오디오 시계도 가짜 시계에 묶는다.
  for (const C of [window.BaseAudioContext, window.AudioContext]) {
    if (C && Object.getOwnPropertyDescriptor(C.prototype, 'currentTime'))
      Object.defineProperty(C.prototype, 'currentTime', { get() { return (now - T0) / 1000; }, configurable: true });
  }

  let tid = 1, raf = [], timers = new Map();
  window.requestAnimationFrame = cb => { const id = tid++; raf.push([id, cb]); return id; };
  window.cancelAnimationFrame = id => { raf = raf.filter(r => r[0] !== id); };
  const addTimer = (cb, ms, args, rep) => {
    const id = tid++; const d = Math.max(0, +ms || 0);
    timers.set(id, { id, cb, args, at: now + d, ms: d, rep }); return id;
  };
  window.setTimeout = (cb, ms, ...args) => addTimer(cb, ms, args, false);
  window.setInterval = (cb, ms, ...args) => addTimer(cb, ms, args, true);
  window.clearTimeout = window.clearInterval = id => { timers.delete(id); };

  function runTimers() {
    for (let guard = 0; guard < 10000; guard++) {
      let best = null;
      for (const t of timers.values()) if (t.at <= now && (!best || t.at < best.at || (t.at === best.at && t.id < best.id))) best = t;
      if (!best) return;
      if (best.rep) best.at += Math.max(1, best.ms); else timers.delete(best.id);
      if (typeof best.cb === 'function') best.cb(...best.args);
    }
  }
  function syncAnimations() {
    for (const a of document.getAnimations()) {
      if (a.__mvq0 === undefined) { a.__mvq0 = now; try { a.pause(); } catch (_) {} }
      try { a.currentTime = now - a.__mvq0; } catch (_) {}
    }
  }
  // 한 프레임: 시간 +1/60초 → 만기 타이머 → rAF 콜백(정본 frame) → CSS 애니메이션 맞춤
  function step() {
    now += FRAME_MS;
    runTimers();
    const q = raf; raf = [];
    for (const [, cb] of q) cb(now);
    syncAnimations();
    mvq.frame++;
  }

  // ── 난수 ──
  let seedState = cfg.seed >>> 0;
  const mulberry32 = () => {
    seedState = (seedState + 0x6D2B79F5) >>> 0; let t = seedState;
    t = Math.imul(t ^ (t >>> 15), t | 1); t ^= t + Math.imul(t ^ (t >>> 7), t | 61);
    return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
  };
  const constFn = () => cfg.rngConst;
  const gen = cfg.rngMode === 'const' ? constFn : mulberry32;
  const pageFile = /MoonVillage\.html:(\d+):(\d+)/;
  function caller() {
    // 첫 게임 프레임(줄·열·함수). rnd()(L854) 거쳐 오면 via='rnd'로 표시하고 그 바깥 호출 줄을 쓴다.
    const lines = new Error().stack.split('\n').slice(2);
    let via = '';
    for (const l of lines) {
      const m = pageFile.exec(l); if (!m) continue;
      const fn = (/at ([^\s(]+) \(/.exec(l) || [, '?'])[1];
      if (+m[1] === cfg.rndLine && !via) { via = 'rnd'; continue; }
      return { line: +m[1], col: +m[2], fn, via };
    }
    return { line: 0, col: 0, fn: '?', via };
  }
  Math.random = function () {
    const v = gen();
    mvq.rngN++;
    if (mvq.rec) { const c = caller(); mvq.rec.push([mvq.rngN, mvq.frame, v, c.line, c.col, c.fn, c.via]); }
    return v;
  };

  // ── 저장 ──
  try {
    if (cfg.save === null) localStorage.removeItem(cfg.saveKey);
    else if (cfg.save !== undefined) localStorage.setItem(cfg.saveKey, JSON.stringify(cfg.save));
  } catch (_) {}

  // ── 부팅 고리 ──
  const mvq = window.__mvq = {
    frame: 0, rngN: 0, rec: null, booted: false, ev: null,
    step, steps(n) { for (let i = 0; i < n; i++) step(); },
    get now() { return now; },
    boot: null, snap: null,
  };
  window.claude = {
    hot: {
      snapshot(fn) { mvq.snap = fn; },
      ready(boot) {
        mvq.boot = boot;
        // CDP가 여기서 멈춘 뒤 호출 프레임(정본 IIFE)에서 __mvq.ev = s => eval(s) 를 만든다.
        debugger; // eslint-disable-line no-debugger
      },
    },
  };
};
