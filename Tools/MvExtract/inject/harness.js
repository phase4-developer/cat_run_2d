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
            const TX = X + sh[0] - A1.x, TY = Y + sh[1] - A1.y; CTX.setTransform(1, 0, 0, 1, TX, TY);
            window.__mvPX = X - TX; window.__mvPY = Y - TY; // test 식은 변환 전 좌표를 본다
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

  // ── dom(03 §3.2.4): 빈 요소에 정본 CSS를 입혀 실행기가 locator.screenshot({omitBackground:true})으로 찍는다 ──
  // job.html: 넣을 마크업. job.hostCss(선택): 호스트에 더할 CSS(부모 선택자 대신 변수 등). --u는 #stage 값(1080 폭 = 10.8px)
  MvH.domPrep = function (job) {
    let st = document.getElementById('mvDomStyle');
    if (!st) { st = document.createElement('style'); st.id = 'mvDomStyle'; st.textContent = 'html,body{background:transparent!important}body>*:not(#mvDomHost),body>*:not(#mvDomHost) *{visibility:hidden!important}'; document.head.appendChild(st); }
    let host = document.getElementById('mvDomHost');
    if (!host) { host = document.createElement('div'); host.id = 'mvDomHost'; document.body.appendChild(host); }
    const u = getComputedStyle(document.getElementById('stage')).getPropertyValue('--u').trim();
    host.style.cssText = `position:fixed;left:0;top:0;width:${job.canvas.w}px;height:${job.canvas.h}px;z-index:2147483647;display:flex;align-items:center;justify-content:center;--u:${u};background:transparent;margin:0;padding:0;border:0;${job.hostCss || ''}`;
    host.innerHTML = job.html;
    return u;
  };
  // ── skin(03 §3.5.8): UI 스킨 1장. job.html 안 [data-t] = 찍을 요소, [data-w] = 선택자용 조상(상자 없이 display:contents).
  // 정본 CSS 연쇄를 그대로 받고, 배치·크기만 인라인 !important로 덮는다. job.size [W, H](바깥 테두리 상자 px) 또는 job.clone(정본 요소의
  // 내용을 잠시 복제해 정본 크기를 잰다). --u는 UI 기준 1080 폭 = 10.8 px(03 §3.3.2). 잘라내기·9-slice 경계는 skinCrop.
  MvH.skinPrep = function (job) {
    MvH.domPrep({ ...job, hostCss: `--u:${job.u || 10.8}px;${job.hostCss || ''}` });
    const host = document.getElementById('mvDomHost');
    for (const w of host.querySelectorAll('[data-w]')) w.setAttribute('style', (w.getAttribute('style') || '') + ';all:unset!important;display:contents!important');
    const t = host.querySelector('[data-t]');
    if (!t) throw new Error('skin: [data-t] 없음');
    const imp = (k, v) => t.style.setProperty(k, v, 'important');
    for (const [k, v] of Object.entries({ position: 'relative', inset: 'auto', transform: 'none', animation: 'none', transition: 'none', margin: '0', flex: 'none', 'min-width': '0', 'min-height': '0', 'max-width': 'none', 'max-height': 'none', 'pointer-events': 'none', 'align-self': 'center' })) imp(k, v);
    let W, H;
    if (job.clone) {
      const src = document.querySelector(job.clone);
      if (!src) throw new Error('skin: 복제 원본 없음 ' + job.clone);
      t.innerHTML = src.innerHTML;
      for (const el of t.querySelectorAll('*')) el.removeAttribute('hidden');
      const r = t.getBoundingClientRect(); W = r.width; H = r.height; t.innerHTML = '';
      if (job.size) W = job.size[0] || W;
    } else [W, H] = job.size;
    W = 2 * Math.round(W / 2); H = 2 * Math.round(H / 2); // 짝수: 호스트 가운데 정수 화소에 놓인다
    imp('box-sizing', 'border-box'); imp('width', W + 'px'); imp('height', H + 'px'); imp('padding', '0');
    if (job.targetCss) for (const d of job.targetCss.split(';').filter((x) => x.trim())) { const i = d.indexOf(':'); imp(d.slice(0, i).trim(), d.slice(i + 1).trim()); }
    const cs = getComputedStyle(t), px = (s) => parseFloat(s) || 0;
    const rad = (k) => { const p = cs[k].split(' '); return [px(p[0]), px(p[1] ?? p[0])]; };
    const R = { tl: rad('borderTopLeftRadius'), tr: rad('borderTopRightRadius'), br: rad('borderBottomRightRadius'), bl: rad('borderBottomLeftRadius') };
    const f = Math.min(1, W / (R.tl[0] + R.tr[0] || 1e9), W / (R.bl[0] + R.br[0] || 1e9), H / (R.tl[1] + R.bl[1] || 1e9), H / (R.tr[1] + R.br[1] || 1e9));
    const b = { l: px(cs.borderLeftWidth), r: px(cs.borderRightWidth), t: px(cs.borderTopWidth), b: px(cs.borderBottomWidth) };
    const corner = { l: Math.max(f * R.tl[0], f * R.bl[0], b.l), r: Math.max(f * R.tr[0], f * R.br[0], b.r), t: Math.max(f * R.tl[1], f * R.tr[1], b.t, 1), b: Math.max(f * R.bl[1], f * R.br[1], b.b) };
    const rc = t.getBoundingClientRect();
    return { x: Math.round(rc.left), y: Math.round(rc.top), W, H, corner };
  };
  // 찍은 호스트 PNG → 요소 가운데 기준 좌우·위아래 대칭으로 자른다(그림자 끝 + 2 px, 4의 배수). 9-slice [L, B, R, T] = 여백 + 모서리 + 1
  MvH.skinCrop = async function (u, info, job) {
    const im = new Image(); im.src = u; await im.decode();
    const c0 = newCanvas(im.width, im.height), g0 = c0.getContext('2d'); g0.drawImage(im, 0, 0);
    if (job.noCrop) return { dataURL: u, w: im.width, h: im.height, border: null, pivotPx: [im.width / 2, im.height / 2], hits: [], zeroRules: [], alpha: checkAlpha(c0) };
    const d = g0.getImageData(0, 0, im.width, im.height).data;
    let x0 = 1e9, y0 = 1e9, x1 = -1, y1 = -1;
    for (let y = 0; y < im.height; y++) for (let x = 0; x < im.width; x++) if (d[(y * im.width + x) * 4 + 3]) { if (x < x0) x0 = x; if (x > x1) x1 = x; if (y < y0) y0 = y; if (y > y1) y1 = y; }
    if (x1 < 0) throw new Error('skin: 빈 그림');
    const { x, y, W, H, corner } = info;
    const mx = Math.max(0, x - x0, x1 + 1 - (x + W)) + 2, my = Math.max(0, y - y0, y1 + 1 - (y + H)) + 2;
    const r4 = (n) => Math.ceil(n / 4) * 4;
    const w = r4(W + 2 * mx), h = r4(H + 2 * my), cx = x - (w - W) / 2, cy = y - (h - H) / 2;
    if (cx < 0 || cy < 0 || cx + w > im.width || cy + h > im.height) throw new Error(`skin: 호스트가 작다(${w}x${h} @ ${cx},${cy} / ${im.width}x${im.height})`);
    const out = newCanvas(w, h); out.getContext('2d').drawImage(im, -cx, -cy);
    const px = (w - W) / 2, py = (h - H) / 2, k = (v) => Math.ceil(v) + 1;
    const border = job.slice === false ? null : [px + k(corner.l), py + k(corner.b), px + k(corner.r), py + k(corner.t)];
    if (border) { if (border[0] + border[2] > w) border[0] = border[2] = Math.floor(w / 2); if (border[1] + border[3] > h) border[1] = border[3] = Math.floor(h / 2); }
    return { dataURL: out.toDataURL('image/png'), w, h, border, pivotPx: [w / 2, h / 2], hits: [], zeroRules: [], alpha: checkAlpha(out) };
  };
  MvH.domClear = function () { const h = document.getElementById('mvDomHost'); if (h) h.remove(); const s = document.getElementById('mvDomStyle'); if (s) s.remove(); };
  MvH.alphaOf = async function (u) { const im = new Image(); im.src = u; await im.decode(); const c = newCanvas(im.width, im.height); c.getContext('2d').drawImage(im, 0, 0); return checkAlpha(c); };

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
