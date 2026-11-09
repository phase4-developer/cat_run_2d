// 추출 정의 JSON 생성기 (03 §3.2.6, §3.5). node gen_jobs.mjs → jobs/*.json
// 정의는 이 파일의 표에서 만든다. 손으로 jobs/*.json을 고치지 말고 여기를 고친 뒤 다시 돌린다.
import { mkdirSync, writeFileSync } from 'node:fs';
import { join } from 'node:path';
import { ROOT } from './lib/canon.mjs';

const TAU = 6.283185307179586;
const out = {};
const add = (file, j) => (out[file] ||= []).push({ ppu: 128, scaleK: 1, themeSet: 'none', suppress: [], only: [], clock: 0, ...j });

// ── 3.5.1 캐릭터 ── mochiBack: 숨쉬기 sin(now*2.2+pid)=0 인 시각을 정지 그림 시계로 쓴다
const idleClock = (pid, w) => { let k = Math.ceil(pid / Math.PI); return (k * Math.PI - pid) / w; };
const CAT_P0 = { w: 244, h: 408, pivotPx: [122, 344] };
const CAT_F = { w: 196, h: 296, pivotPx: [98, 240] };
for (let t = 1; t <= 8; t++) {
  const base = { atlas: `Atlas_World_Cat_T${t}`, mode: 'direct', pivot: 'Feet', scaleK: 1.05, canvas: CAT_P0, src: 'L3679~3736' };
  const fr = { var: 'ph', from: 0, period: TAU, count: 12, fps: null };
  add('char_cat.json', { ...base, name: `World_Cat_P0_T${t}_Run_A`, frames: fr, args: { t, pat: 0, st: 'run', thr: 0 }, expr: 'mochiBack(g, X, Y, S, {...A, ph: v})', suppress: t === 8 ? [{ fn: 'star4', argIndex: 4, in: ['#fff6a0'] }] : [] });
  add('char_cat.json', { ...base, name: `World_Cat_P0_T${t}_Run_B`, frames: fr, args: { t, pat: 0, st: 'run', thr: 0.2 }, expr: 'mochiBack(g, X, Y, S, {...A, ph: v})', suppress: t === 8 ? [{ fn: 'star4', argIndex: 4, in: ['#fff6a0'] }] : [] });
  add('char_cat.json', { ...base, name: `World_Cat_P0_T${t}_Climb`, frames: fr, args: { t, pat: 0, st: 'climb', thr: 0 }, expr: 'mochiBack(g, X, Y, S, {...A, ph: v})', suppress: t === 8 ? [{ fn: 'star4', argIndex: 4, in: ['#fff6a0'] }] : [] });
  add('char_cat.json', { ...base, name: `World_Cat_P0_T${t}_Idle`, clock: idleClock(0, 2.2), args: { t, pat: 0, st: 'idle', thr: 0 }, expr: 'mochiBack(g, X, Y, S, {...A, ph: 0})' });
}
for (let t = 1; t <= 6; t++) for (let p = 1; p <= 4; p++) {
  const base = { atlas: `Atlas_World_Friend_T${t}`, mode: 'direct', pivot: 'Feet', scaleK: 0.85, canvas: CAT_F, src: 'L3679~3736, L3799' };
  const fr = { var: 'ph', from: 0, period: TAU, count: 12, fps: null };
  add('char_friend.json', { ...base, name: `World_Cat_P${p}_T${t}_Run_A`, frames: fr, args: { t, pat: p, st: 'run' }, expr: 'mochiBack(g, X, Y, S, {...A, ph: v})' });
  add('char_friend.json', { ...base, name: `World_Cat_P${p}_T${t}_Climb`, frames: fr, args: { t, pat: p, st: 'climb' }, expr: 'mochiBack(g, X, Y, S, {...A, ph: v})' });
  add('char_friend.json', { ...base, name: `World_Cat_P${p}_T${t}_Idle`, clock: idleClock(p, 2.2), args: { t, pat: p, st: 'idle' }, expr: 'mochiBack(g, X, Y, S, {...A, ph: 0})' });
}
// 로비 주인공(정면, s*1.25, 로비 160 px/단위). 눈 감음: (now % 3.6) < 0.12
for (let t = 1; t <= 8; t++) {
  const base = { atlas: 'Atlas_World_Lobby', mode: 'direct', pivot: 'Feet', ppu: 160, scaleK: 1.25, canvas: { w: 320, h: 484, pivotPx: [148, 450] }, src: 'L3267~3363, L3532', args: { t, pat: 0, st: 'idle', ph: 0 },
    suppress: t === 8 ? [{ fn: 'star4', argIndex: 4, in: ['#fff6a0'] }] : [] };
  add('char_lobby.json', { ...base, name: `Lobby_Cat_Front_T${t}`, clock: Math.PI / 2.2, expr: 'mochi(g, X, Y, S, {...A})' });
  add('char_lobby.json', { ...base, name: `Lobby_Cat_Front_T${t}_Blink`, clock: 0, expr: 'mochi(g, X, Y, S, {...A})' });
}
for (let p = 1; p <= 4; p++) add('char_lobby.json', { name: `Lobby_Cat_Sleep_P${p}`, atlas: 'Atlas_World_Lobby', mode: 'direct', pivot: 'Feet', ppu: 160, scaleK: 0.62, canvas: { w: 144, h: 132, pivotPx: [72, 114] }, clock: idleClock(p, 1.6), args: { t: 1, pat: p, st: 'sleep', ph: 0 }, expr: 'mochi(g, X, Y, S, {...A})', src: 'L3528' });
// 신 초상(단독 텍스처)
add('char_god.json', { name: 'Tex_God_Thunder', texture: true, atlas: null, mode: 'direct', pivot: 'Center', canvas: { w: 400, h: 468, pivotPx: [200, 234] }, args: { gid: 'thunder' }, expr: 'paintGod(g, A.gid, 400, 468)', allowEdge: true, src: 'L2481' });
add('char_god.json', { name: 'Tex_God_Flame', texture: true, atlas: null, mode: 'direct', pivot: 'Center', canvas: { w: 400, h: 468, pivotPx: [200, 234] }, args: { gid: 'flame' }, expr: 'paintGod(g, A.gid, 400, 468)', allowEdge: true, src: 'L2481' });

// ── 3.5.3 적 ── drawMouse: t = now/200 → 주기 2π/5 s, 16프레임
add('world_enemy.json', { name: 'World_Enemy_Mouse', atlas: 'Atlas_World_Theme', themeSet: 'all', mode: 'ctx', pivot: 'Feet', canvas: { w: 160, h: 148, pivotPx: [64, 128] }, frames: { var: 't', from: 0, period: TAU / 5, count: 16, fps: +(16 / (TAU / 5)).toFixed(4) }, args: {}, expr: 'drawMouse({}, X, Y, S)', src: 'L3652' });

// ── 3.5.4 투사체 ── 게임 반지름 0.5 m(blades 0.6 m는 같은 그림 축소)
for (let t = 1; t <= 8; t++) add('world_proj.json', { name: `World_Proj_${t}`, atlas: 'Atlas_World_Theme', themeSet: 'all', mode: 'direct', pivot: 'Center', canvas: { w: 160, h: 160, pivotPx: [80, 80] }, args: { t }, expr: '(g.translate(X, Y), drawProjShape(g, A.t, 0.5*S, OLC))', src: 'L3368' });

// ── 3.5.4 이펙트 ──
add('fx.json', { name: 'Fx_Star4', atlas: 'Atlas_Fx_Common', mode: 'direct', pivot: 'Center', canvas: { w: 32, h: 32, pivotPx: [16, 16] }, expr: "star4(g, X, Y, 13, '#ffffff')", src: 'L3261' });
add('fx.json', { name: 'Fx_Star4_Pale', atlas: 'Atlas_Fx_Common', mode: 'direct', pivot: 'Center', canvas: { w: 32, h: 32, pivotPx: [16, 16] }, expr: "star4(g, X, Y, 13, '#fff6a0')", src: 'L3261, L3732' });
for (let i = 0; i < 4; i++) add('fx.json', { name: `Fx_BrickBit_${i}`, atlas: 'Atlas_Fx_Common', mode: 'ctx', pivot: 'Center', canvas: { w: 40, h: 24, pivotPx: [20, 12] }, args: { i },
  pre: 'g.translate(X, Y), g.rotate(-(X + Y)*0.05), g.translate(-X, -Y)', expr: 'brickBit(X, Y, 0.06*S, ENERGY_C[A.i])', src: 'L3605, L3096' });

// ── 3.5.7 UI 아이콘 ──
for (let t = 1; t <= 8; t++) add('ui_icon.json', { name: `UI_Icon_Weapon_${t}`, atlas: 'Atlas_UI_Icon', mode: 'direct', pivot: 'Center', canvas: { w: 208, h: 208, pivotPx: [104, 104] }, args: { t }, expr: 'paintWeapon(g, A.t, 208)', allowEdge: true, src: 'L3388' });
for (const [kind, nm] of [['tower', 'Tower'], ['rack', 'Rack']]) for (let st = 1; st <= 3; st++)
  add('ui_icon.json', { name: `UI_Icon_Fac_${nm}_${st}`, atlas: 'Atlas_UI_Icon', mode: 'svg', pivot: 'Center', canvas: { w: 128, h: 128, pivotPx: [64, 64] }, args: { kind, st }, margin: 0, allowEdge: true, src: 'L3463', srcExpr: `facIconURL('${kind}', ${st})` });
for (const [sel, nm, w, h] of [['.ic-gold', 'Gold', 96, 96], ['.ic-dia', 'Dia', 96, 96], ['.ic-soul', 'Soul', 96, 68], ['.ic-stair', 'Stair', 96, 96]])
  add('ui_icon.json', { name: `UI_Icon_${nm}`, atlas: 'Atlas_UI_Icon', mode: 'svg', pivot: 'Center', canvas: { w, h, pivotPx: [w / 2, h / 2] }, selector: sel, src: `CSS ${sel}` });
// 카드 그림 16장: paintCard(g, id, s, el)
const CARD = [['atk', 'Card_Atk', null], ['rate', 'Card_Rate', null], ['gold', 'Card_Gold', null], ['storm', 'Card_Storm', null], ['blast', 'Card_Blast', null], ['clone1', 'Card_Clone1', null],
  ['orb', 'Card_Orb_Bolt', 'bolt'], ['orb', 'Card_Orb_Fire', 'fire'], ['spear', 'Card_Spear_Bolt', 'bolt'], ['spear', 'Card_Spear_Fire', 'fire'],
  ['proc', 'Card_Proc_Bolt', 'bolt'], ['proc', 'Card_Proc_Fire', 'fire'], ['chain', 'Card_Chain_Bolt', 'bolt'], ['chain', 'Card_Chain_Fire', 'fire'], ['frate', 'Card_Frate_Bolt', 'bolt'], ['frate', 'Card_Frate_Fire', 'fire']];
for (const [id, nm, el] of CARD) add('ui_card.json', { name: nm, atlas: 'Atlas_UI_Card', mode: 'direct', pivot: 'Center', canvas: { w: 192, h: 192, pivotPx: [96, 96] }, args: { id, el: el || 'bolt' }, expr: 'paintCard(g, A.id, 192, A.el)', allowEdge: true, src: 'L873~912, L3758' });
// 이모지(D-52 Noto Color Emoji)
const EMO = [['Basket', '🧺'], ['Map', '🗺️'], ['Fire', '🔥'], ['Herb', '🌿'], ['Paw', '🐾'], ['Close', '✕'], ['Sound', '♪'], ['Play', '▶'],
  ['Lock', '🔒'], ['Target', '🎯'], ['Cat', '🐱'], ['Boom', '💥'], ['Swords', '⚔️'], ['Dagger', '🗡️'], ['Brick', '🧱'], ['Shield', '🛡️'], ['Moon', '🌙'], ['Chick', '🐣']];
for (const [nm, ch] of EMO) add('ui_emoji.json', { name: `UI_Emoji_${nm}`, atlas: 'Atlas_UI_Icon', mode: 'direct', pivot: 'Center', canvas: { w: 96, h: 96, pivotPx: [48, 48] }, args: { ch },
  expr: "(g.font = '72px MvEmoji, \"Gothic A1\"', g.textAlign = 'center', g.textBaseline = 'middle', g.fillStyle = '#ffffff', g.fillText(A.ch, X, Y + 4))", src: 'D-52 마크업 L658~713, L2499~2586' });
for (const [nm, ch, c] of [['Note', '♪', '#7a5ac8'], ['Heart', '♥', '#e8504a'], ['Sparkle', '✦', '#f2b33d']])
  add('ui_emoji.json', { name: `UI_Emoji_${nm}`, atlas: 'Atlas_UI_Icon', mode: 'direct', pivot: 'Center', canvas: { w: 64, h: 64, pivotPx: [32, 32] }, args: { ch, c },
    expr: "(g.font = '900 44px \"Gothic A1\"', g.textAlign = 'center', g.textBaseline = 'middle', g.fillStyle = A.c, g.fillText(A.ch, X, Y + 2))", src: 'L3534' });

mkdirSync(join(ROOT, 'jobs'), { recursive: true });
let n = 0;
for (const [f, list] of Object.entries(out)) { writeFileSync(join(ROOT, 'jobs', f), JSON.stringify(list, null, 1) + '\n'); n += list.length; }
console.log(`jobs ${n} in ${Object.keys(out).length} files`);
