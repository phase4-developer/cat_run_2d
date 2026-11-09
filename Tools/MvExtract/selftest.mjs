// 공용 기반 자가 시험: 정본 로드·외부 접속 0·글꼴·결정성(같은 그림 2회 같은 바이트)·AudioContext 대체
import { openCanon, ROOT, sha256 } from './lib/canon.mjs';
import { join } from 'node:path';

const runOnce = async () => {
  const { browser, page, blocked } = await openCanon();
  await page.addScriptTag({ path: join(ROOT, 'inject/harness.js') });
  const r = await page.evaluate(() => {
    const job = { name: 'selftest_star', mode: 'direct', canvas: { w: 32, h: 32, pivotPx: [16, 16] }, expr: "star4(g, X, Y, 13, '#ffffff')" };
    const a = MvH.renderOne(job);
    const env = { now: performance.now(), rnd: Math.random(), raf: window.__mvRafPending(), audio: String(window.AudioContext && window.AudioContext.name), W: MvH.S('W'), H: MvH.S('H') };
    return { url: a.dataURL, alpha: a.alpha, env };
  });
  await browser.close();
  const png = Buffer.from(r.url.split(',')[1], 'base64');
  return { blocked, sha: sha256(png), alpha: r.alpha, env: r.env };
};
const a = await runOnce(), b = await runOnce();
console.log(JSON.stringify({ a, b }, null, 1));
const ok = a.blocked.length === 0 && a.sha === b.sha && !a.alpha.empty && a.env.rnd === b.env.rnd;
console.log(ok ? 'SELFTEST PASS' : 'SELFTEST FAIL');
process.exit(ok ? 0 : 1);
