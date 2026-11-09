// 장면 정의(QA/Scenes/*.json) 읽기·검사·실행. 형식은 QA/Scenes/README.md.
const fs = require('fs');
const path = require('path');
const H = require('./harness');

const SCENE_DIR = path.join(H.ROOT, 'QA', 'Scenes');
const PRESETS = ['easy', 'mid', 'orig'];
const OPS = ['card', 'deliver', 'debug', 'speed'];
const DEBUG_IDS = ['addGold', 'addSoul', 'lvUp', 'rbOpen', 'rbCdClear', 'addDia', 'addTok'];

function listScenes() {
  return fs.readdirSync(SCENE_DIR).filter(f => /^[A-Z]+-\d+[A-Za-z0-9-]*\.json$/.test(f)).sort().map(f => f.replace(/\.json$/, ''));
}

function load(id) {
  const s = JSON.parse(fs.readFileSync(path.join(SCENE_DIR, id + '.json'), 'utf8'));
  validate(s, id);
  return s;
}

function validate(s, id) {
  const bad = m => { throw new Error(`${id}: ${m}`); };
  if (s.id !== id) bad('id가 파일 이름과 다름');
  if (!Number.isInteger(s.seed) || s.seed < 0 || s.seed > 0xFFFFFFFF) bad('seed는 0~2^32-1 정수');
  if (s.rng && !['mulberry32', 'const'].includes(s.rng.mode)) bad('rng.mode');
  if (!PRESETS.includes(s.preset)) bad('preset');
  if (s.save !== null && (typeof s.save !== 'object' || !s.save.up)) bad('save는 null 또는 정본 save 객체(up 필수)');
  if (!Number.isInteger(s.frames) || s.frames < 1) bad('frames');
  if (!Array.isArray(s.captureAt) || !s.captureAt.length) bad('captureAt');
  for (const f of s.captureAt) if (!Number.isInteger(f) || f < 1 || f > s.frames) bad(`captureAt ${f}`);
  for (const e of s.inputs || []) {
    if (!Number.isInteger(e.frame) || e.frame < 0 || e.frame >= s.frames) bad(`inputs.frame ${e.frame}`);
    if (!['down', 'move', 'up', 'tap', 'key'].includes(e.type)) bad(`inputs.type ${e.type}`);
    if (e.type === 'key' ? !['ArrowLeft', 'ArrowRight'].includes(e.key) || !['down', 'up'].includes(e.state) : !(e.x >= 0 && e.x <= 1080 && e.y >= 0 && e.y <= 1920)) bad('inputs 좌표/키');
    if (e.op !== undefined) bad('inputs에 op 금지(ops에 적는다)');
  }
  for (const o of s.ops || []) {
    if (!Number.isInteger(o.frame) || o.frame < 0 || o.frame >= s.frames) bad(`ops.frame ${o.frame}`);
    if (!OPS.includes(o.op)) bad(`ops.op ${o.op}`);
    if (o.op === 'debug' && !DEBUG_IDS.includes(o.id)) bad(`debug id ${o.id}`);
  }
  for (const m of s.mask || []) if (!(m.w > 0 && m.h > 0 && m.x >= 0 && m.y >= 0)) bad('mask');
}

// 페이지 안에서 도는 프레임 진행기. 정본 IIFE 스코프(__mvq.ev)에서 만든다.
// 프레임 f(0부터)에서: ops → inputs 적용 → __mvq.step() → (probe 있으면) 기록.
const PAGE_RUNNER = `(() => {
  const st = document.querySelector('#stage');
  let downEl = null;
  const pe = (type, el, x, y) => el.dispatchEvent(new PointerEvent(type, { clientX: x, clientY: y, pointerId: 1, pointerType: 'touch', isPrimary: true, bubbles: true, cancelable: true, composed: true, button: 0, buttons: type === 'pointerup' ? 0 : 1 }));
  const input = e => {
    if (e.type === 'key') { dispatchEvent(new KeyboardEvent(e.state === 'down' ? 'keydown' : 'keyup', { key: e.key, bubbles: true })); return; }
    const r = st.getBoundingClientRect(), x = r.left + e.x, y = r.top + e.y;
    if (e.type === 'down' || e.type === 'tap') { downEl = document.elementFromPoint(x, y); if (downEl) pe('pointerdown', downEl, x, y); }
    if (e.type === 'move' && downEl) pe('pointermove', downEl, x, y);
    if (e.type === 'up' || e.type === 'tap') {
      const el = downEl; downEl = null; if (!el) return; pe('pointerup', el, x, y);
      const at = document.elementFromPoint(x, y); if (at && (at === el || el.contains(at) || at.contains(el))) at.click();
    }
  };
  const op = o => {
    if (o.op === 'card') window.__ce.applyCard(o.id);
    else if (o.op === 'deliver') window.__ce.deliver(o.n);
    else if (o.op === 'debug') document.getElementById(o.id).click();
    else if (o.op === 'speed') DBG.ts = o.v;
  };
  return {
    preset(p) { Object.assign(BAL, BALS[p]); DBG.rate = BAL.rate; },
    // from 프레임부터 to 프레임 직전까지 진행. probe(f)가 있으면 매 프레임 끝에 호출해 모은다.
    run(sc, from, to, probe) {
      const out = [];
      for (let f = from; f < to; f++) {
        for (const o of sc.ops || []) if (o.frame === f) op(o);
        for (const e of sc.inputs || []) if (e.frame === f) input(e);
        window.__mvq.step();
        if (probe) out.push(probe(f + 1));
      }
      return out;
    },
  };
})()`;

// 장면을 연다(부팅 + 프리셋). 반환: { s, run(to, probeSrc) }
async function start(browser, sc, opts = {}) {
  const s = await H.open(browser, {
    seed: sc.seed, rngMode: sc.rng && sc.rng.mode, rngConst: sc.rng && sc.rng.c,
    save: sc.save, epochMs: sc.epochMs,
  }, opts);
  await s.page.evaluate(src => { window.__mvq.runner = window.__mvq.ev(src); }, PAGE_RUNNER);
  await s.page.evaluate(p => window.__mvq.runner.preset(p), sc.preset);
  let at = 0;
  const run = async (to, probeSrc) => {
    const r = await s.page.evaluate(([sc2, a, b, p]) => window.__mvq.runner.run(sc2, a, b, p ? window.__mvq.ev('(' + p + ')') : null),
      [{ ops: sc.ops, inputs: sc.inputs }, at, to, probeSrc || null]);
    at = to; return r;
  };
  return { s, run, get frame() { return at; } };
}

// 장면 1회 캡처: captureAt 프레임마다 PNG. 반환 [{frame, png, state}]
const STATE_PROBE = `() => ({ mode, frame: window.__mvq.frame, rng: window.__mvq.rngN,
  z: R ? +R.p.z.toFixed(4) : null, x: R ? +R.p.x.toFixed(4) : null, level: save.level, area: save.area })`;
async function capture(browser, sc, opts = {}) {
  const r = await start(browser, sc, opts);
  const shots = [];
  try {
    for (const f of [...sc.captureAt].sort((a, b) => a - b)) {
      await r.run(f);
      const png = await H.shot(r.s.page);
      const state = await r.s.ev(STATE_PROBE);
      shots.push({ frame: f, png, state });
    }
    if (r.s.errors.length) throw new Error(`${sc.id} 페이지 오류: ${r.s.errors[0]}`);
  } finally { await r.s.ctx.close(); }
  return shots;
}

module.exports = { SCENE_DIR, listScenes, load, validate, start, capture, STATE_PROBE };
