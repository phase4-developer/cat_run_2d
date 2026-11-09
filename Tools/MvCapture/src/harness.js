// 정본 MoonVillage.html을 Playwright Chromium에서 같은 상태로 띄우는 공용 하네스.
const fs = require('fs');
const path = require('path');
const crypto = require('crypto');
const { chromium } = require('playwright');
const inject = require('./inject');

const ROOT = path.resolve(__dirname, '..', '..', '..');          // 저장소 루트
const TOOL = path.resolve(__dirname, '..');
const PROTO = path.join(ROOT, 'Docs', 'Reference', 'MoonVillage.html');
const PROTO_SHA = 'fe573cf2f8ed9b90030c993e8127ecd479f4f2d47195d3fbfa19962694926dfa'; // 01 §1.2, Docs/Reference/SHA256.txt
const ORIGIN = 'http://mv.local';
const SAVE_KEY = 'catescape_moon_v1';   // 정본 L843
const RND_LINE = 854;                   // 정본 L854 rnd()
const VIEW = { width: 1112, height: 1920 }; // 스테이지 1080×1920 + body 좌우 여백 16px×2(정본 L26)
const STAGE = { width: 1080, height: 1920 };
const DEFAULT_EPOCH = Date.UTC(2026, 0, 1, 3, 0, 0); // 2026-01-01 12:00 KST

const sha256 = buf => crypto.createHash('sha256').update(buf).digest('hex');

// vendor 파일과 SHA256 (vendor/SOURCES.md와 같은 값)
const VENDOR = {
  'three.min.js': '9274bbcec8d96168626c732b5d31c775aa8cfb7eaa0599bec0c175908a2c1ce2',
  'fonts/BlackHanSans-Regular.ttf': '31960809284026681774a8e52dc19ebcad26cf69b0ad9d560f288296fbb52739',
  'fonts/GothicA1-Regular.ttf': '211151bea98098c579610ab1114bb1bfe909057207db561f357b896d076c21ac',
  'fonts/GothicA1-Bold.ttf': '2e883fa0ae548000996258b4101f2bb732a28b197264f49621a2cb16d6e9f126',
  'fonts/GothicA1-ExtraBold.ttf': '37a65353a646fea5a1ac90585d1bb8f9b997ecae0b79c4f1f187566f910e3f9d',
  'fonts/NotoColorEmoji-Regular.ttf': '4d82a18d8d95f60ba883ce242bbadbf84a576e987745dd7ba38d71c67cff2d73',
};

function verify() {
  const out = { proto: null, vendor: {} };
  const html = fs.readFileSync(PROTO);
  out.proto = sha256(html);
  if (out.proto !== PROTO_SHA) throw new Error(`정본 SHA256 불일치: ${out.proto}`);
  for (const [f, want] of Object.entries(VENDOR)) {
    const p = path.join(TOOL, 'vendor', f);
    if (!fs.existsSync(p)) throw new Error(`vendor 파일 없음: ${p} (vendor/SOURCES.md의 출처에서 받아 둔다)`);
    const got = sha256(fs.readFileSync(p));
    if (got !== want) throw new Error(`vendor SHA256 불일치: ${f} ${got}`);
    out.vendor[f] = got;
  }
  return { html, ...out };
}

// Google Fonts css2 요청을 대신하는 글꼴 CSS. 이모지는 같은 글꼴 이름에 unicode-range로 Noto Color Emoji를
// 낮은 우선순위로 붙인다(정본 font-family는 그대로, 시스템 이모지 글꼴 차이를 없앤다).
const EMOJI_RANGE = 'U+00A9,U+00AE,U+203C,U+2049,U+20E3,U+2122,U+2139,U+2194-21AA,U+231A-23FF,U+24C2,U+25AA-25FE,U+2600-27BF,U+2934-2935,U+2B05-2B55,U+3030,U+303D,U+3297,U+3299,U+FE0F,U+200D,U+1F000-1FAFF,U+E0020-E007F';
const FONT_URL = 'https://fonts.gstatic.com/mvq/';
function fontCss() {
  const face = (fam, file, w, range) => `@font-face{font-family:'${fam}';font-style:normal;font-weight:${w};font-display:block;src:url(${FONT_URL}${file}) format('truetype');${range ? `unicode-range:${range};` : ''}}`;
  const out = [];
  for (const [fam, faces] of [['Black Han Sans', [[400, 'BlackHanSans-Regular.ttf']]],
    ['Gothic A1', [[400, 'GothicA1-Regular.ttf'], [700, 'GothicA1-Bold.ttf'], [800, 'GothicA1-ExtraBold.ttf']]]]) {
    for (const [w] of faces) out.push(face(fam, 'NotoColorEmoji-Regular.ttf', w, EMOJI_RANGE)); // 먼저 = 낮은 우선순위
    for (const [w, f] of faces) out.push(face(fam, f, w));
  }
  return out.join('\n');
}

async function launch() {
  return chromium.launch({
    headless: true,
    args: ['--disable-gpu', '--force-color-profile=srgb', '--disable-lcd-text', '--hide-scrollbars',
      '--disable-features=FontAccessChooser', '--autoplay-policy=no-user-gesture-required'],
  });
}

// scene: { seed, rngMode, rngConst, save, epochMs } → 부팅까지 마친 page와 eval 함수
async function open(browser, scene, opts = {}) {
  const { html } = opts.verified || verify();
  const ctx = await browser.newContext({
    viewport: VIEW, deviceScaleFactor: 1, locale: 'ko-KR', timezoneId: 'Asia/Seoul',
    reducedMotion: 'no-preference', colorScheme: 'light', serviceWorkers: 'block',
  });
  const blocked = [];
  await ctx.route('**/*', async route => {
    const u = route.request().url();
    if (u === `${ORIGIN}/MoonVillage.html`) return route.fulfill({ body: html, contentType: 'text/html; charset=utf-8' });
    if (u === 'https://cdnjs.cloudflare.com/ajax/libs/three.js/r128/three.min.js')
      return route.fulfill({ path: path.join(TOOL, 'vendor', 'three.min.js'), contentType: 'application/javascript' });
    if (u.startsWith('https://fonts.googleapis.com/css2')) return route.fulfill({ body: fontCss(), contentType: 'text/css' });
    if (u.startsWith(FONT_URL)) return route.fulfill({ path: path.join(TOOL, 'vendor', 'fonts', path.basename(new URL(u).pathname)), contentType: 'font/ttf' });
    blocked.push(u); return route.abort();
  });
  const page = await ctx.newPage();
  const errors = [];
  page.on('pageerror', e => errors.push(String(e && e.stack || e)));
  const cfg = {
    seed: scene.seed >>> 0, rngMode: scene.rngMode || 'mulberry32', rngConst: scene.rngConst ?? 0.5,
    save: scene.save, saveKey: SAVE_KEY, epochMs: scene.epochMs ?? DEFAULT_EPOCH, rndLine: RND_LINE,
  };
  await page.addInitScript(inject, cfg);

  // debugger 문(window.claude.hot.ready)에서 멈추면 바로 바깥 프레임(정본 IIFE)에서 eval 고리를 만든다.
  const cdp = await ctx.newCDPSession(page);
  let hooked = false;
  cdp.on('Debugger.paused', async ev => {
    try {
      if (!hooked && ev.callFrames.length > 1) {
        const fr = ev.callFrames[1];
        const r = await cdp.send('Debugger.evaluateOnCallFrame', { callFrameId: fr.callFrameId, expression: 'window.__mvq.ev = (s) => eval(s), 1' });
        if (r.exceptionDetails) errors.push('hook: ' + JSON.stringify(r.exceptionDetails));
        hooked = true;
      }
    } finally { await cdp.send('Debugger.resume').catch(() => {}); }
  });
  await cdp.send('Debugger.enable');
  await page.goto(`${ORIGIN}/MoonVillage.html`, { waitUntil: 'load' });
  await cdp.send('Debugger.disable');
  if (!hooked) throw new Error('정본 내부 고리 실패: ' + errors.join('\n'));

  // 글꼴 전부 로드 → 정본 boot({}) 호출(정본 L3888이 하는 일과 같다)
  await page.evaluate(async () => {
    const fams = ['400 20px "Black Han Sans"', '400 20px "Gothic A1"', '700 20px "Gothic A1"', '800 20px "Gothic A1"'];
    for (const f of fams) { await document.fonts.load(f, '가A1'); await document.fonts.load(f, '\u{1F431}'); }
    await document.fonts.ready;
    window.__mvq.boot({});
    window.__mvq.booted = true;
  });
  const ev = (src, arg) => page.evaluate(([s, a]) => window.__mvq.ev(`(${s})`)(a), [typeof src === 'function' ? src.toString() : src, arg]);
  return { ctx, page, cdp, ev, errors, blocked };
}

// 스테이지(#stage) 영역 PNG
async function shot(page) {
  const box = await page.evaluate(() => { const r = document.querySelector('#stage').getBoundingClientRect(); return { x: r.x, y: r.y, width: r.width, height: r.height }; });
  if (Math.round(box.width) !== STAGE.width || Math.round(box.height) !== STAGE.height) throw new Error(`스테이지 크기 ${box.width}×${box.height}`);
  return page.screenshot({ clip: box, animations: 'allow', caret: 'hide', type: 'png' });
}

module.exports = { ROOT, TOOL, PROTO, PROTO_SHA, SAVE_KEY, VIEW, STAGE, sha256, verify, launch, open, shot };
