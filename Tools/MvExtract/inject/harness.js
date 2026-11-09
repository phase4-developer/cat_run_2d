// MvExtract 작업 실행기 (03 §3.2.4~3.2.5). page.addScriptTag로 넣는 고전 스크립트.
// 정본 함수는 IIFE 안에 있으므로 window.__mvScope(code)(scope_hook.js)로 닿는다.
// job.expr는 정본 범위에서 평가되는 JS 식이며 다음 이름을 쓸 수 있다:
//   g(그릴 컨텍스트) X Y(피벗 화소) S(px/단위) t(__clock 초) v(프레임 값) J(job 객체) A(job.args) TH(현재 테마 객체)
(() => {
  'use strict';
  const S = (code) => window.__mvScope(code);
  const MvH = (window.MvH = {});
  MvH.S = S;

  // ── 바꿔 끼우기(억제·한정) ──
  const C2D = CanvasRenderingContext2D.prototype;
  function matchArgs(rule, args, self) {
    if (rule.argIndex != null) {
      let v = args[rule.argIndex];
      if (typeof v === 'string') v = v.toLowerCase();
      if (rule.in && !rule.in.map((x) => (typeof x === 'string' ? x.toLowerCase() : x)).includes(v)) return false;
      if (rule.eq !== undefined && v !== (typeof rule.eq === 'string' ? rule.eq.toLowerCase() : rule.eq)) return false;
    }
    if (rule.rMax != null && !(Math.abs(args[3]) <= rule.rMax && Math.abs(args[4] ?? args[3]) <= rule.rMax)) return false;
    if (rule.fillIsGradient && !(self && self.fillStyle instanceof CanvasGradient)) return false;
    if (rule.full && self) { const c = self.canvas; if (!(args[0] <= 0 && args[1] <= 0 && args[0] + args[2] >= c.width && args[1] + args[3] >= c.height)) return false; }
    if (rule.test) { const f = MvH._tests[rule.test] || (MvH._tests[rule.test] = S(`(function(a, self){ return (${rule.test}); })`)); if (!f(args, self)) return false; }
    return true;
  }
  MvH._tests = {};

  // rules: [{fn|ctx, ...조건, mode:'suppress'|'only'}]. 반환: 되돌리기 함수, 일치 수 배열
  function install(rules) {
    const undo = []; const hits = rules.map(() => 0);
    const onlyRules = rules.filter((r) => r.mode === 'only');
    rules.forEach((rule, i) => {
      if (rule.fn) {
        const orig = S(rule.fn);
        if (typeof orig !== 'function') throw new Error('억제 대상 함수 없음: ' + rule.fn);
        const wrap = function (...a) {
          const m = matchArgs(rule, a, a[0] instanceof CanvasRenderingContext2D ? a[0] : null);
          if (m) hits[i]++;
          if (rule.mode === 'only' ? !m : m) return undefined;
          return orig.apply(this, a);
        };
        window.__mvTmp = wrap; S(`${rule.fn} = window.__mvTmp`); window.__mvTmp = null;
        undo.push(() => { window.__mvTmp = orig; S(`${rule.fn} = window.__mvTmp`); window.__mvTmp = null; });
      } else if (rule.ctx) {
        const orig = C2D[rule.ctx];
        C2D[rule.ctx] = function (...a) {
          const m = matchArgs(rule, a, this);
          if (m) hits[i]++;
          if (rule.mode === 'only' ? !m : m) return undefined;
          return orig.apply(this, a);
        };
        undo.push(() => { C2D[rule.ctx] = orig; });
      }
    });
    return { undo: () => { for (const u of undo.reverse()) u(); }, hits, onlyRules };
  }

  // ── 캔버스 ──
  function newCanvas(w, h) { const c = document.createElement('canvas'); c.width = w; c.height = h; return c; }
  function checkAlpha(c, edge = 2) {
    const g = c.getContext('2d'); const { data } = g.getImageData(0, 0, c.width, c.height);
    let any = false, edgeHit = 0;
    for (let y = 0; y < c.height; y++) for (let x = 0; x < c.width; x++) {
      const a = data[(y * c.width + x) * 4 + 3]; if (!a) continue; any = true;
      if (x < edge || y < edge || x >= c.width - edge || y >= c.height - edge) edgeHit++;
    }
    return { empty: !any, edgeHit };
  }
  MvH.checkAlpha = checkAlpha;

  // ── 투영 감싸기(project) ──
  function setRunCamera(Wpx, Hpx) {
    S(`W = ${Wpx}; H = ${Hpx}; camera.aspect = W/H; camera.updateProjectionMatrix();
       camera.position.set(0, 13, 9); camera.lookAt(0, 0.5, -5.5); camera.updateMatrixWorld(true);`);
  }
  MvH.setRunCamera = setRunCamera;

  // 앵커(three 좌표) → 출력 피벗 화소로 옮기는 pj 감싸기. k = S/(A.s*norm)
  function wrapPj(anchor, X, Y, scale, norm) {
    const pj0 = S('pj');
    const A = pj0(anchor[0], anchor[1], anchor[2]);
    if (!A) throw new Error('앵커가 카메라 뒤');
    const k = scale / (A.s * (norm || 1));
    window.__mvTmp = (x, y, z) => { const p = pj0(x, y, z); if (!p) return null; return { x: X + (p.x - A.x) * k, y: Y + (p.y - A.y) * k, d: p.d, s: p.s * k }; };
    S('pj = window.__mvTmp'); window.__mvTmp = null;
    return { k, A, undo: () => { window.__mvTmp = pj0; S('pj = window.__mvTmp'); window.__mvTmp = null; } };
  }

  const exprCache = new Map();
  function compile(expr) {
    if (!exprCache.has(expr)) exprCache.set(expr, S(`(function(g, X, Y, S, t, v, J, A, TH){ ${expr.includes(';') ? expr : 'return (' + expr + ');'} })`));
    return exprCache.get(expr);
  }
  MvH.compile = compile;

  // ── 한 장 그리기 ── 반환 { dataURL, w, h, hits, alpha }
  MvH.renderOne = function (job, frameVal) {
    const w = job.canvas.w, h = job.canvas.h, [X, Y] = job.canvas.pivotPx;
    const scale = (job.ppu || 128) * (job.scaleK || 1);
    const t = frameVal != null && job.frames && job.frames.var === 't' ? frameVal : (job.clock ?? 0);
    window.__clock = t;
    window.__mvJ = job; window.__mvPX = X; window.__mvPY = Y; // 억제 규칙 test 식에서 쓴다
    if (window.__mvSeed && window.__mvHash) window.__mvSeed(window.__mvHash(job.name));
    const out = newCanvas(w, h);
    const TH = job.theme != null ? S(`AREA_TH[${job.theme | 0}]`) : null;
    const pre = job.pre ? compile(job.pre) : null;
    const fn = compile(job.expr);
    const rules = install((job.suppress || []).map((r) => ({ ...r, mode: 'suppress' })).concat((job.only || []).map((r) => ({ ...r, mode: 'only' }))));
    let proj = null; let saved = null;
    try {
      if (job.mode === 'direct' || job.mode === 'svg') {
        const g = out.getContext('2d');
        if (pre) pre(g, X, Y, scale, t, frameVal, job, job.args || {}, TH);
        fn(g, X, Y, scale, t, frameVal, job, job.args || {}, TH);
      } else if (job.mode === 'ctx' || job.mode === 'project') {
        saved = S('({W, H, cw: cv.width, ch: cv.height})');
        const cv = S('cv'), CTX = S('CTX');
        cv.width = w; cv.height = h;
        CTX.setTransform(1, 0, 0, 1, 0, 0); CTX.clearRect(0, 0, w, h);
        CTX.globalAlpha = 1; CTX.globalCompositeOperation = 'source-over';
        if (job.mode === 'project') {
          setRunCamera(job.view ? job.view[0] : 1080, job.view ? job.view[1] : 1920);
          const nrm = job.norm ? compile(job.norm)(CTX, X, Y, scale, t, frameVal, job, job.args || {}, TH) : 1;
          // anchorExpr(선택): 함수가 앵커에 더하는 무작위 흔들림(drawJunk의 jx 등)을 정본 hash로 계산할 때
          const an = job.anchorExpr ? compile(job.anchorExpr)(CTX, X, Y, scale, t, frameVal, job, job.args || {}, TH) : job.anchor;
          // shiftExpr(선택): 그림 안 한 부품(게이트 롤 하나 등)을 피벗에 오게 옮기는 화소 오프셋
          const sh = job.shiftExpr ? compile(job.shiftExpr)(CTX, X, Y, scale, t, frameVal, job, job.args || {}, TH) : [0, 0];
          if (job.__raw) { // V2(03 §3.11.1): 감싸지 않고 k = 1이 되게 H = 2·S·d/(P5·norm)로 맞춘 뒤 평행이동만
            const A0 = S('pj')(an[0], an[1], an[2]); const P5 = S('camera.projectionMatrix.elements[5]');
            const H2 = (2 * scale * A0.d) / (P5 * nrm); S(`W = ${(H2 * 9) / 16}; H = ${H2};`);
            const A1 = S('pj')(an[0], an[1], an[2]);
            CTX.setTransform(1, 0, 0, 1, X + sh[0] - A1.x, Y + sh[1] - A1.y);
          } else proj = wrapPj([an[0], an[1], an[2]], X + sh[0], Y + sh[1], scale, nrm);
        } else if (job.wh) {
          S(`W = ${job.wh[0]}; H = ${job.wh[1]};`);
        }
        if (pre) pre(CTX, X, Y, scale, t, frameVal, job, job.args || {}, TH);
        fn(CTX, X, Y, scale, t, frameVal, job, job.args || {}, TH);
        out.getContext('2d').drawImage(cv, 0, 0);
      } else throw new Error('모르는 mode: ' + job.mode);
    } finally {
      if (proj) proj.undo();
      rules.undo();
      if (saved) { S(`W = ${saved.W}; H = ${saved.H}; camera.aspect = W/H; camera.updateProjectionMatrix();`); }
    }
    const zero = rules.hits.map((n, i) => (n === 0 ? i : -1)).filter((i) => i >= 0);
    return { dataURL: out.toDataURL('image/png'), w, h, hits: rules.hits, zeroRules: zero, alpha: checkAlpha(out), k: proj ? proj.k : null };
  };

  // svg data URI 그림(이미지 로드가 비동기라 따로)
  // 이모지 글꼴(D-52). 정본 CSS에는 없으므로 실행기가 따로 등록한다
  MvH.loadEmoji = async function () { if (MvH._emoji) return true; const f = new FontFace('MvEmoji', 'url(https://fonts.gstatic.com/mv/NotoColorEmoji-Regular.ttf)'); await f.load(); document.fonts.add(f); MvH._emoji = true; return document.fonts.check('76px MvEmoji', '🐾'); };

  MvH.renderSvg = async function (job) {
    const w = job.canvas.w, h = job.canvas.h; const out = newCanvas(w, h);
    let src = job.src;
    if (job.srcExpr) src = S(job.srcExpr);
    if (job.selector) { for (const sh of document.styleSheets) { let rs; try { rs = sh.cssRules; } catch (_) { continue; } for (const r of rs) if (r.selectorText && r.selectorText.split(',').map((x) => x.trim()).includes(job.selector) && r.style.backgroundImage) src = r.style.backgroundImage.replace(/^url\(["']?/, '').replace(/["']?\)$/, ''); } }
    if (!src) throw new Error('SVG 원본 없음: ' + job.selector);
    const img = new Image(); img.src = src; await img.decode();
    const m = job.margin ?? 4, sc = Math.min((w - 2 * m) / img.width, (h - 2 * m) / img.height);
    const [bx, by, bw, bh] = job.box || [(w - img.width * sc) / 2, (h - img.height * sc) / 2, img.width * sc, img.height * sc];
    out.getContext('2d').drawImage(img, bx, by, bw, bh);
    return { dataURL: out.toDataURL('image/png'), w, h, hits: [], zeroRules: [], alpha: checkAlpha(out), natural: [img.width, img.height] };
  };
})();
