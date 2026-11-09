// 표 TSV 읽기. 작업 트리에 표가 있으면 그것을, 없으면 MV_TABLE_REF(기본 ai/MV-DATA-tables-loader)의 커밋에서 읽는다(읽기 전용).
import { execFileSync } from 'node:child_process';
import { existsSync, readFileSync, readdirSync } from 'node:fs';
import { join } from 'node:path';
import { REPO } from './canon.mjs';

const TSV_DIR = 'Assets/Game/GameData/Table/TSV';
const REF = process.env.MV_TABLE_REF || 'ai/MV-DATA-tables-loader';

export function tableSource() {
  return existsSync(join(REPO, TSV_DIR, '304_theme.txt')) ? `worktree:${TSV_DIR}` : `git:${REF}:${TSV_DIR}`;
}
export function listTables() {
  if (existsSync(join(REPO, TSV_DIR, '304_theme.txt'))) return readdirSync(join(REPO, TSV_DIR)).filter((f) => f.endsWith('.txt'));
  return execFileSync('git', ['-C', REPO, 'ls-tree', '--name-only', `${REF}:${TSV_DIR}`], { encoding: 'utf8' }).split('\n').filter((f) => f.endsWith('.txt'));
}
export function readTable(file) {
  const text = existsSync(join(REPO, TSV_DIR, '304_theme.txt'))
    ? readFileSync(join(REPO, TSV_DIR, file), 'utf8')
    : execFileSync('git', ['-C', REPO, 'show', `${REF}:${TSV_DIR}/${file}`], { encoding: 'utf8', maxBuffer: 64 << 20 });
  const lines = text.replace(/^\uFEFF/, '').split(/\r?\n/).filter((l) => l.length && !l.startsWith('#'));
  const head = lines[0].split('\t');
  return { head, rows: lines.slice(1).map((l) => { const c = l.split('\t'); return Object.fromEntries(head.map((h, i) => [h, c[i] ?? ''])); }) };
}
// 304_theme → [{key, atlas, areaTh}] (active 1만). areaTh는 정본 AREA_TH 항목 모양
export function loadThemes() {
  const { rows } = readTable('304_theme.txt');
  const L = (s) => s.split('/').filter(Boolean);
  return rows.filter((r) => r.active !== '0').map((r) => ({
    key: r.key, atlas: r.worldAtlas,
    areaTh: { skyTop: r.skyTop, skyBot: r.skyBot, fog: r.fog, grass: r.grass, grass2: r.grass2, road: r.road, road2: r.road2, stone: r.stone, hillFar: r.hillFar, hillNear: r.hillNear,
      walls: L(r.walls), roofs: L(r.roofs), wallTop: r.wallTop, door: r.door, prop: r.prop || 'moon', deco: r.deco, tint: r.tint || null, night: +r.night || 0 },
  }));
}
