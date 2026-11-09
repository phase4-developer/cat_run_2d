// 정본 로드 공용 기반 (03 §3.2.1~3.2.3). 그림·소리 도구가 같이 쓴다.
// - 정본 SHA256 확인(다르면 중단)
// - 고정 Chromium + 결정성 인자, 새 프로필
// - 네트워크: 정본·three.js·글꼴만 로컬 파일로 응답, 나머지는 모두 막는다(외부 접속 0)
// - 정본은 'use strict' IIFE 안에 있어 전역에서 함수에 닿을 수 없다. 디스크 파일은 그대로 두고,
//   응답 본문 끝(IIFE 닫기 직전)에 inject/scope_hook.js 한 줄을 붙여 window.__mvScope(code)로 정본 범위 eval을 연다.
import { chromium } from 'playwright';
import { createHash } from 'node:crypto';
import { readFileSync } from 'node:fs';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

export const ROOT = resolve(dirname(fileURLToPath(import.meta.url)), '..');
export const REPO = resolve(ROOT, '..', '..');
const read = (p) => readFileSync(join(ROOT, p));
export const sha256 = (buf) => createHash('sha256').update(buf).digest('hex');

export function loadCanon({ acceptSha } = {}) {
  const canon = JSON.parse(read('canon.json'));
  const file = resolve(ROOT, canon.path);
  const bytes = readFileSync(file);
  const sha = sha256(bytes);
  if (sha !== canon.sha256 && sha !== acceptSha) {
    throw new Error(`정본 SHA256 불일치: ${sha} (canon.json ${canon.sha256}). 바뀐 정본을 받아들이려면 --accept-canon ${sha}`);
  }
  return { ...canon, file, bytes, sha };
}

// 정본 메인 IIFE의 마지막 닫기 `})();` 앞에 범위 고리를 붙인다(메모리 안에서만).
export function injectScopeHook(html) {
  const hook = read('inject/scope_hook.js').toString('utf8').trim();
  const close = '\n})();\n</script>';
  const at = html.lastIndexOf(close);
  if (at < 0) throw new Error('정본 IIFE 끝을 찾지 못함(정본 구조 변경?)');
  return html.slice(0, at) + '\n' + hook + html.slice(at);
}

const FONT_CSS = () => {
  const face = (fam, w, file) => `@font-face{font-family:'${fam}';font-style:normal;font-weight:${w};font-display:block;src:url(https://fonts.gstatic.com/mv/${file}) format('truetype');}`;
  return [
    face('Black Han Sans', 400, 'BlackHanSans-Regular.ttf'),
    face('Gothic A1', 400, 'GothicA1-Regular.ttf'),
    face('Gothic A1', 700, 'GothicA1-Bold.ttf'),
    face('Gothic A1', 800, 'GothicA1-ExtraBold.ttf'),
  ].join('\n');
};

export const CHROMIUM_ARGS = ['--disable-gpu', '--force-color-profile=srgb', '--font-render-hinting=none', '--disable-lcd-text', '--disable-font-subpixel-positioning'];

/**
 * 정본을 연다. 반환: { browser, context, page, canon, blocked }
 * opts.initConfig: window.__mvInitConfig (clock, seed, audio)
 * opts.viewport: 기본 1080×1920
 */
export async function openCanon(opts = {}) {
  const canon = loadCanon(opts);
  const html = injectScopeHook(canon.bytes.toString('utf8'));
  const browser = await chromium.launch({ headless: true, args: CHROMIUM_ARGS });
  const vp = opts.viewport || { width: 1080, height: 1920 };
  const context = await browser.newContext({ viewport: vp, deviceScaleFactor: 1, locale: 'ko-KR', timezoneId: 'UTC', colorScheme: 'dark', reducedMotion: 'no-preference' });
  const blocked = [];
  await context.route('**/*', async (route) => {
    const url = route.request().url();
    if (url === canon.origin || url === canon.origin + 'MoonVillage.html') return route.fulfill({ status: 200, contentType: 'text/html; charset=utf-8', body: html });
    if (url.startsWith('https://cdnjs.cloudflare.com/ajax/libs/three.js/r128/three.min.js')) return route.fulfill({ status: 200, contentType: 'application/javascript', body: read('vendor/three.min.js') });
    if (url.startsWith('https://fonts.googleapis.com/css2')) return route.fulfill({ status: 200, contentType: 'text/css', body: FONT_CSS() });
    if (url.startsWith('https://fonts.gstatic.com/mv/')) {
      const f = url.slice('https://fonts.gstatic.com/mv/'.length).replace(/[^A-Za-z0-9_.-]/g, '');
      return route.fulfill({ status: 200, contentType: 'font/ttf', headers: { 'access-control-allow-origin': '*' }, body: read('vendor/fonts/' + f) });
    }
    if (url.startsWith('data:') || url.startsWith('blob:')) return route.continue();
    blocked.push(url);
    return route.abort();
  });
  const init = read('inject/init.js').toString('utf8');
  await context.addInitScript({ content: `window.__mvInitConfig = ${JSON.stringify(opts.initConfig || {})};\n${init}` });
  const page = await context.newPage();
  const errors = [];
  page.on('pageerror', (e) => errors.push(String(e && e.stack || e)));
  await page.goto(canon.origin + 'MoonVillage.html', { waitUntil: 'load' });
  await page.waitForFunction(() => typeof window.__mvScope === 'function', null, { timeout: 30000 });
  if (errors.length) throw new Error('정본 실행 오류: ' + errors.join('\n'));
  const fontsOk = await page.evaluate(async () => {
    await document.fonts.load('20px "Black Han Sans"'); await document.fonts.load('400 20px "Gothic A1"');
    await document.fonts.load('700 20px "Gothic A1"'); await document.fonts.load('800 20px "Gothic A1"');
    await document.fonts.ready;
    return ['20px "Black Han Sans"', '400 20px "Gothic A1"', '700 20px "Gothic A1"', '900 20px "Gothic A1"'].map((f) => [f, document.fonts.check(f)]);
  });
  const bad = fontsOk.filter(([, ok]) => !ok);
  if (bad.length) throw new Error('글꼴 확인 실패: ' + JSON.stringify(bad));
  return { browser, context, page, canon, blocked, errors };
}

// 화면 그림 도구용: 정본 범위 안 eval
export const scopeEval = (page, code) => page.evaluate((c) => window.__mvScope(c), code);
