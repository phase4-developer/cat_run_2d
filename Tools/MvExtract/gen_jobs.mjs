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

// ── 3.5.3 장애물·관문·우물·계단·아이템 (project: 정본 함수를 가짜 오브젝트로 부르고 pj를 감싼다) ──
const W0 = '{x: 0, y: 0, z: 0}';
const JUNK = [['World_Obj_Junk_FishBox', { idx: 0, lane: 0, rew: 'soul' }], ['World_Obj_Junk_Parcel', { idx: 0, lane: 0 }], ['World_Obj_Junk_Bag_Blue', { idx: 0, lane: 1 }],
  ['World_Obj_Junk_Bag_Dark', { idx: 1, lane: 1 }], ['World_Obj_Junk_Crate_Blue', { idx: 0, lane: 2 }], ['World_Obj_Junk_Crate_Red', { idx: 1, lane: 2 }]];
for (const [nm, a] of JUNK) add('world_obj.json', { name: nm, atlas: 'Atlas_World_Theme', themeSet: 'all', mode: 'project', pivot: 'FrontBottom', canvas: { w: 296, h: 264, pivotPx: [140, 232] }, args: a,
  // jx·sc는 drawJunk(L3608)와 같은 hash 식으로 앵커·norm에서 지운다. 보상 배지(churu/catnipLeaf)는 World_Item_Badge_*로 따로 굽는다
  anchorExpr: '[(hash(A.idx*7.13) - 0.5)*0.35, 0.05, 0.45]', norm: '0.9 + hash(A.idx*7.13 + 3)*0.18',
  expr: `drawJunk({...A, bar: 0, row: 0, shake: 0}, ${W0})`, suppress: a.rew ? [] : [{ fn: 'churu' }],
  v4: a.rew ? null : [{ part: 'World_Item_Badge_Gold', at: [0.7, -0.25], rot: -0.5 }], src: 'L3607~3630' });
add('world_obj.json', { name: 'World_Item_Badge_Gold', atlas: 'Atlas_World_Theme', themeSet: 'all', mode: 'direct', pivot: 'Center', canvas: { w: 80, h: 40, pivotPx: [40, 20] }, expr: 'churu(g, X, Y, 0.55*S, 0, 1)', src: 'L3626(회전 -0.5는 런타임)' });
add('world_obj.json', { name: 'World_Item_Badge_Dia', atlas: 'Atlas_World_Theme', themeSet: 'all', mode: 'direct', pivot: 'Center', canvas: { w: 48, h: 80, pivotPx: [24, 40] }, expr: 'catnipLeaf(g, X, Y, 0.26*S, 0)', src: 'L3626(회전 0.3은 런타임)' });
for (let i = 0; i < 3; i++) add('world_obj.json', { name: `World_Item_Churu_${i}`, atlas: 'Atlas_World_Theme', themeSet: 'all', mode: 'direct', pivot: 'Center', canvas: { w: 96, h: 40, pivotPx: [48, 20] }, args: { i }, expr: 'churu(g, X, Y, 0.62*S, 0, A.i)', src: 'L3108(더미 조립·출렁은 런타임)' });
const FAKE = (k) => `drawObj({k: '${k}', dead: false, body: null, mesh: Object.assign(new THREE.Object3D(), {visible: true})})`;
add('world_obj.json', { name: 'World_Item_CatnipPot', atlas: 'Atlas_World_Theme', themeSet: 'all', mode: 'project', pivot: 'BottomCenter', canvas: { w: 192, h: 212, pivotPx: [96, 204] }, anchor: [0, 0, 0],
  expr: FAKE('gem'), suppress: [{ fn: 'star4' }], src: 'L3112~3116(반짝이는 Fx_Star4 런타임)' });
// 게이트 롤 하나: 롤 i(정본 L3640~3642 순서)를 피벗으로 옮기고 피벗 근처 호출만 그린다. 롤 색 = i % 4, 줄 k = i >> 1, 짝수 왼쪽·홀수 오른쪽, ny = 6
const ROLL_ONLY = [{ fn: 'E', test: 'Math.hypot(a[1] - window.__mvPX, a[2] - window.__mvPY) < 24' }];
add('world_obj.json', { name: 'World_Obj_Gate_RollPaper', atlas: 'Atlas_World_Theme', themeSet: 'all', mode: 'project', pivot: 'Center', canvas: { w: 76, h: 76, pivotPx: [38, 38] }, anchor: [0, 1.4, 0], args: { hw: 1, i: 0 },
  shiftExpr: '[A.hw*1.02*S, -1.4*S]', expr: `drawRollGate({hw: A.hw, flash: 0}, ${W0}, true)`, suppress: [{ fn: 'RR' }, { fn: 'LN' }], only: ROLL_ONLY, src: 'L3638' });
for (let i = 0; i < 4; i++) add('world_obj.json', { name: `World_Obj_Gate_RollYarn_${i}`, atlas: 'Atlas_World_Theme', themeSet: 'all', mode: 'project', pivot: 'Center', canvas: { w: 76, h: 76, pivotPx: [38, 38] }, anchor: [0, 1.4, 0], args: { hw: 1, i },
  shiftExpr: '[(A.i % 2 ? -1 : 1)*A.hw*1.02*S, -1.4*S + (A.i >> 1)*(2.8*S/6)]', expr: `drawRollGate({hw: A.hw, flash: 0}, ${W0}, false)`, suppress: [{ fn: 'RR' }, { fn: 'LN' }],
  only: [...ROLL_ONLY, { ctx: 'arc', test: 'Math.hypot(a[0] - window.__mvPX, a[1] - window.__mvPY) < 24' }], src: 'L3639' });
add('world_obj.json', { name: 'World_Obj_Well_Mouth', atlas: 'Atlas_World_Theme', themeSet: 'all', mode: 'project', pivot: 'WellMouth', canvas: { w: 248, h: 128, pivotPx: [124, 64] }, anchor: [0, 1.25, 0],
  expr: `drawWell({bricks: []}, ${W0})`, src: 'L3645(벽돌 고리는 따로)' });
for (let i = 0; i < 3; i++) add('world_obj.json', { name: `World_Obj_Stair_Step_${i}`, atlas: 'Atlas_World_Theme', themeSet: 'all', mode: 'project', pivot: 'FrontBottom', canvas: [{ w: 1076, h: 104, pivotPx: [518, 52] }, { w: 1048, h: 160, pivotPx: [504, 113] }, { w: 1020, h: 220, pivotPx: [490, 173] }][i], args: { i },
  anchor: [0, 0, -(0.3 + i * 0.8)], expr: `drawBlackHole(${W0})`, only: [{ fn: 'drawBox', test: 'a[2] === 0.3 + window.__mvJ.args.i*0.8' }], suppress: [{ fn: 'PL' }], src: 'L3204~3206' });
add('world_obj.json', { name: 'World_Obj_Stair_Arrow', atlas: 'Atlas_World_Theme', themeSet: 'all', mode: 'project', pivot: 'Center', canvas: { w: 168, h: 176, pivotPx: [84, 96] }, anchor: [0, 2.4, -0.6],
  expr: `drawBlackHole(${W0})`, suppress: [{ fn: 'drawBox' }], src: 'L3208(출렁은 런타임)' });
// 매달린 장난감(World_Obj_Arch_Toy_*)과 그 끈(OLC 선)은 기둥 그림에서 뺀다
const ARCH_TOY = [{ fn: 'E', argIndex: 5, in: ['#ff8fb0', '#7ad0f0', '#ffd84a'] }, { fn: 'LN', argIndex: 2, in: ['#4a3446'] }];
// 관문 기둥: 오른쪽 기둥(x = 4.6)만 들어오게 앵커를 기둥 밑에 둔다. 발판 drawBox는 메쉬(§3.5.9). o.z를 크게 해 지나침 투명(pz)을 끈다
for (const [nm, gate] of [['World_Obj_Arch_Post', false], ['World_Obj_Arch_Post_Gold', true]])
  add('world_obj.json', { name: nm, atlas: 'Atlas_World_Theme', themeSet: 'all', mode: 'project', pivot: 'BottomCenter', canvas: { w: 280, h: 444, pivotPx: [60, 436] }, anchor: [4.6, 0, 0],
    expr: `drawWarpGate({gate: ${gate}, z: 1e4}, ${W0})`, suppress: [{ fn: 'drawBox' }, ...ARCH_TOY, ...(gate ? [{ ctx: 'fillRect', fillIsGradient: true }, { fn: 'star4' }] : [])], src: 'L3216' });

// ── 3.5.2 길가 소품 (drawMoonProp bush/stone 분기 L3570~3578) ── 오른쪽(sd = 1) wp = (1, 0, 0). 소품은 q.s 한 배율로만 그려 원근 기울기가 없다
// 종류는 th.deco와 hh로 정해진다. 바닥 그림자 E(L3571)는 소품 그림에 포함
const PROP = (nm, deco, hh, w, h, py, extra = {}) => add('world_prop.json', { name: nm, atlas: 'Atlas_World_Theme', themeSet: 'all', mode: 'project', pivot: 'BottomCenter', canvas: { w, h, pivotPx: [w / 2, py] },
  args: { deco, hh }, anchorExpr: '[4.22 + (A.hh - 0.5)*0.12, 0, 0]', expr: "drawMoonProp(null, {deco: A.deco, night: 0}, {kind: 'bush'}, {x: 1, y: 0, z: 0}, 0, 0, A.hh)", src: 'L3570~3578', ...extra });
PROP('World_Prop_Pot', 'roof', 0.2, 100, 152, 136);
PROP('World_Prop_Bag', 'roof', 0.8, 100, 124, 108);
// 과일 색 ['#f0a070','#ffd84a'][(hh*10|0) % 2]: A = 0번, B = 1번. 파랑 hh < 0.25, 빨강 0.25 ≤ hh < 0.5(market)
PROP('World_Prop_Crate_Blue_A', 'market', 0.05, 108, 100, 84);
PROP('World_Prop_Crate_Blue_B', 'market', 0.15, 108, 100, 84);
PROP('World_Prop_Crate_Red_A', 'market', 0.45, 108, 100, 84);
PROP('World_Prop_Crate_Red_B', 'market', 0.35, 108, 100, 84);
PROP('World_Prop_Crate_Red_Fish', 'harbor', 0.5, 108, 112, 96);
// 항구 상자는 hh ≥ 0.4라 정본에서는 늘 빨강이다. 표가 쓰는 파랑 생선 상자는 종류 판정 때만 market, 장식 판정 때 harbor로 읽히게 해 조합한다
PROP('World_Prop_Crate_Blue_Fish', 'harbor', 0.05, 108, 112, 96, { expr: "(() => { let n = 0; const th = { get deco() { return n++ ? 'harbor' : 'market'; }, night: 0 }; drawMoonProp(null, th, {kind: 'bush'}, {x: 1, y: 0, z: 0}, 0, 0, A.hh); })()", src: 'L3576(파랑 hh·항구 생선 조합)' });
PROP('World_Prop_Buoy', 'harbor', 0.2, 96, 104, 88);
PROP('World_Prop_Snowman', 'snow', 0.2, 96, 136, 120);
// 장독: snow 테마만 눈 뚜껑(L3574). each로 테마마다 그린다(market은 정본에 장독이 없지만 같은 장독)
PROP('World_Prop_Jar', null, 0.5, 100, 132, 116, { themeSet: 'each', args: { hh: 0.5 }, expr: "drawMoonProp(null, {deco: TH && TH.deco === 'snow' ? 'snow' : 'roof', night: 0}, {kind: 'bush'}, {x: 1, y: 0, z: 0}, 0, 0, A.hh)" });

// ── 3.5.6 로비 비네트(L3537): 화면 W×H 위 방사 그라데이션을 540×960으로 ──
add('fx.json', { name: 'Tex_Lobby_Vignette', texture: true, atlas: null, mode: 'direct', pivot: 'Center', canvas: { w: 540, h: 960, pivotPx: [270, 480] }, allowEdge: true, src: 'L3475(cx, cy), L3537',
  expr: "(() => { const W = 540, H = 960, cx = W/2, cy = H*0.548; const vg = g.createRadialGradient(cx, cy, H*0.2, cx, cy, H*0.75); vg.addColorStop(0, 'rgba(0,0,0,0)'); vg.addColorStop(1, 'rgba(20,8,20,.45)'); g.fillStyle = vg; g.fillRect(0, 0, W, H); })()" });
// ── 3.5.7 .ic-brick(L42): dom. --u = 10.8 px(1080 폭 무대) ──
add('ui_icon.json', { name: 'UI_Icon_Brick', atlas: 'Atlas_UI_Icon', mode: 'dom', pivot: 'Center', canvas: { w: 104, h: 84, pivotPx: [52, 42] }, html: '<i class="ic-brick"></i>', src: 'CSS .ic-brick L42' });
// ── 3.5.8 #fgMask 사선 무늬(L530): repeating-linear-gradient(135deg, 6px+6px). 45° 주기 12·√2 = 16.97 px는 정수가 아니라
// 타일이 이어지게 x 주기 17 px(직각 주기 12.02 px)로 68×68 타일을 그린다. 화소마다 4×4 표본 덮임으로 안티에일리어싱
add('ui_skin.json', { name: 'UI_Skin_HatchTile', atlas: 'Atlas_UI_Hud', mode: 'direct', pivot: 'Center', canvas: { w: 68, h: 68, pivotPx: [34, 34] }, allowEdge: true, src: 'CSS #fgMask L530(x 주기 17 px로 맞춤)',
  expr: "(() => { const N = 68, P = 17, im = g.createImageData(N, N), A = [8, 6, 14], B = [24, 20, 36]; for (let y = 0; y < N; y++) for (let x = 0; x < N; x++) { let a = 0; for (let sy = 0; sy < 4; sy++) for (let sx = 0; sx < 4; sx++) { const u = (((x + (sx + 0.5)/4) + (y + (sy + 0.5)/4)) % P + P) % P; if (u < P/2) a++; } const k = a/16, o = (y*N + x)*4; for (let c = 0; c < 3; c++) im.data[o + c] = Math.round(A[c]*k + B[c]*(1 - k)); im.data[o + 3] = 230; } g.putImageData(im, 0, 0); })()" });

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
  // facIconURL(L3463)은 96 px 캔버스에 drawFac을 부른다. 128 px로 늘리지 않고 같은 호출을 4/3배 좌표·크기로 다시 그린다
  add('ui_icon.json', { name: `UI_Icon_Fac_${nm}_${st}`, atlas: 'Atlas_UI_Icon', mode: 'direct', pivot: 'Center', canvas: { w: 128, h: 128, pivotPx: [64, 64] }, args: { kind, st }, allowEdge: true, src: 'L3463',
    expr: `drawFac(g, A.kind, X, Y - 64 + (A.kind === 'tower' && A.st >= 2 ? 90 : 78)*4/3, (A.kind === 'tower' && A.st >= 2 ? 30 : 38)*4/3, A.st, 0.5, 4)` });
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
