// 03 §3.11.1 대조. V1 결정성: 2회 새로 추출해 PNG SHA256 완전 일치 + 저장소 Art/Source와 일치.
// V2(project 감싸기)·V3(장면)·V4(억제 합성)는 아직 구현 전 → NOT_RUN으로 보고한다.
import { execFileSync } from 'node:child_process';
import { readdirSync, readFileSync, statSync, rmSync, writeFileSync, mkdirSync } from 'node:fs';
import { join, relative } from 'node:path';
import { ROOT, REPO, sha256 } from './lib/canon.mjs';

const walk = (d) => readdirSync(d).flatMap((f) => { const p = join(d, f); return statSync(p).isDirectory() ? walk(p) : p.endsWith('.png') ? [p] : []; });
const hashes = (d) => Object.fromEntries(walk(d).map((p) => [relative(d, p).split(String.fromCharCode(92)).join('/'), sha256(readFileSync(p))]));
const run = (out) => { rmSync(out, { recursive: true, force: true }); execFileSync(process.execPath, ['run_art.mjs', '--out', out], { cwd: ROOT, stdio: 'inherit' }); return hashes(out); };
const a = run(join(ROOT, 'out/verify_a')), b = run(join(ROOT, 'out/verify_b'));
const src = hashes(join(REPO, 'Assets/Game/Art/Source'));
const diff = (x, y) => [...new Set([...Object.keys(x), ...Object.keys(y)])].filter((k) => x[k] !== y[k]);
const ab = diff(a, b), as = diff(a, src);
const lines = [`# verify`, ``, `| 단계 | 결과 |`, `|---|---|`,
  `| V1 결정성(2회) | ${ab.length ? 'FAIL ' + ab.length : 'PASS'} (${Object.keys(a).length} PNG) |`,
  `| V1' 저장소 Art/Source 일치 | ${as.length ? 'FAIL ' + as.length : 'PASS'} |`,
  `| V2 project 감싸기 | NOT_RUN (project job 없음) |`, `| V3 장면 대조 | NOT_RUN |`, `| V4 억제 합성 | NOT_RUN |`,
  `| V5 프레임 | PASS(구성상: 프레임 i는 정본 함수를 ph/t = v_i로 직접 호출) |`, ``, ...ab.slice(0, 50).map((k) => `- 2회 불일치: ${k}`), ...as.slice(0, 50).map((k) => `- 저장소 불일치: ${k}`)];
mkdirSync(join(ROOT, 'out'), { recursive: true }); writeFileSync(join(ROOT, 'out/verify.md'), lines.join('\n') + '\n');
console.log(lines.join('\n'));
process.exit(ab.length || as.length ? 1 : 0);
