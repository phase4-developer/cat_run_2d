// 임시 아트 (03 §3.13). npm run temp — npm run art 다음에 돌린다.
// 모든 표 리소스 열 값(07 §7.10)을 모아 Art/Source/sprite_catalog.tsv(A·B 결과)에 없는 이름을 찾고,
//   - 03 §3.5 대응표에 있는 이름 → 갈래 A(아직 추출 안 함, MV-TOOL-ART 남은 일). PNG를 만들지 않는다
//   - 대소문자만 다른 이름 → NAME_CASE(표 값 고칠 것, MV-DATA 변경 요청). PNG를 만들지 않는다
//   - 그 밖 → 갈래 C: §3.13.3 순서(1 정본 그림 조합 → 2 이모지 → 3 글자 판)로 Art/Temp/에 임시 PNG
// 결과: Art/Temp/temp_art_list.tsv, Art/Temp/manifest.json, Art/Temp/**.png, sprite_catalog.tsv의 temp 1 행
import { existsSync, mkdirSync, readFileSync, readdirSync, rmSync, writeFileSync } from 'node:fs';
import { join } from 'node:path';
import { openCanon, ROOT, REPO, sha256 } from './lib/canon.mjs';
import { listTables, readTable, loadThemes, tableSource } from './lib/tables.mjs';

// --out <폴더>: 시험용. Temp/와 sprite_catalog.tsv를 저장소 대신 그 폴더에 쓴다
const oi = process.argv.indexOf('--out'), OUTD = oi > 0 ? process.argv[oi + 1] : null;
const SRC = join(REPO, 'Assets/Game/Art/Source'), TMP = OUTD ? join(OUTD, 'Temp') : join(REPO, 'Assets/Game/Art/Temp'), CAT_OUT = OUTD ? join(OUTD, 'sprite_catalog.tsv') : join(SRC, 'sprite_catalog.tsv');

// ── 1. 카탈로그(정식) ──
const catRows = readFileSync(join(SRC, 'sprite_catalog.tsv'), 'utf8').split('\n').slice(1).filter(Boolean).map((l) => l.split('\t'));
const formal = catRows.filter((r) => r[2] !== '1');
const names = new Set(formal.map((r) => r[1]));
const lower = new Map([...names].map((n) => [n.toLowerCase(), n]));

// ── 2. 03 §3.5 대응표의 이름 패턴(갈래 A) ──
const spec = readFileSync(join(REPO, 'Docs/MV-SPEC-01/03_ResourcePipeline.md'), 'utf8');
const sec = spec.slice(spec.indexOf('## 3.5 '), spec.indexOf('## 3.6 '));
const aPats = [];
for (const line of sec.split('\n').filter((l) => l.startsWith('| '))) {
  const col = line.split('|')[2] || '';
  let prev = null; // 「`World_Obj_Junk_Bag_Dark`, `_Blue`」처럼 앞 이름의 끝 조각만 바꾼 줄임 표기
  for (const m of col.matchAll(/`(_?[A-Z][A-Za-z0-9_{}.,|]+|_[A-Za-z0-9{}.,|]+)`/g)) {
    const nm = m[1].startsWith('_') && prev ? prev.replace(/_[^_]+$/, m[1]) : m[1]; if (!m[1].startsWith('_')) prev = m[1];
    const rx = nm.replace(/\{(\d+)\.\.(\d+)\}/g, '\\d+').replace(/\{([A-Za-z0-9_,|]+)\}/g, (_, x) => (/,|\|/.test(x) ? `(${x.split(/[,|]/).join('|')})` : '[A-Za-z0-9]+'));
    aPats.push(new RegExp('^' + rx + '(_\\d\\d)?$'));
  }
}
const isA = (n) => aPats.some((r) => r.test(n));

// ── 3. 표 리소스 열 → 이름·쓰는 곳·아틀라스 ──
const SPRITE_COLS = new Set(['icon', 'largeIcon', 'mapIcon', 'frameSprite', 'sprite', 'projSprite', 'spriteSoul', 'worldBadge', 'roadsideProps', 'lobbySprite', 'facSprite', 'portrait', 'illust', 'bg', 'address']);
const DEF_ATLAS = { mapIcon: 'Atlas_UI_Lobby', frameSprite: 'Atlas_UI_Card', worldBadge: '@theme', sprite: '@theme', projSprite: '@theme', spriteSoul: '@theme', roadsideProps: '@theme', lobbySprite: 'Atlas_World_Lobby', facSprite: 'Atlas_World_Lobby' };
const atlasOf = (file, col, row) => {
  if (/^(portrait|illust|bg|address)$/.test(col)) return 'Texture';
  if (row.atlas && /^Atlas_/.test(row.atlas)) return row.atlas;
  if (row.iconAtlas && col === 'iconColumn') return row.iconAtlas;
  if (file.startsWith('110_') && col === 'icon') return 'Atlas_UI_Card';
  return DEF_ATLAS[col] || 'Atlas_UI_Icon';
};
const want = new Map(); // name → {uses:Set, atlas, prefix}
for (const f of listTables()) {
  const t = readTable(f);
  for (const row of t.rows) {
    for (const col of t.head) {
      const v = row[col] || '';
      let vals = [];
      if (SPRITE_COLS.has(col)) vals = v.split('/').filter(Boolean);
      else if (col === 'iconColumn' && v.startsWith('=')) vals = [v.slice(1)];
      else if (col === 'spritePrefix' && v) vals = [v];
      for (const n of vals) {
        if (!/^[A-Za-z][A-Za-z0-9_{}]*$/.test(n)) continue;
        const w = want.get(n) || { uses: new Set(), atlas: atlasOf(f, col, row), prefix: col === 'spritePrefix', color: row.color || row.col || '' };
        w.uses.add(`${f.replace(/\.txt$/, '')}.${col}${row.key ? '[' + row.key + ']' : row.id ? '[' + row.id + ']' : ''}`);
        want.set(n, w);
      }
    }
  }
}
const has = (n, w) => {
  if (n.includes('{')) { const rx = new RegExp('^' + n.replace(/\{[a-z]+\}/g, '\\d+') + '_'); return [...names].some((x) => rx.test(x)); }
  if (w.prefix) return [...names].some((x) => x.startsWith(n + '_'));
  return names.has(n) || names.has(n + '_00');
};

// ── 4. 판정 ──
const list = [];
for (const [n, w] of [...want].sort((a, b) => a[0].localeCompare(b[0]))) {
  if (has(n, w)) continue;
  const uses = [...w.uses].sort().join(', ');
  if (lower.has(n.toLowerCase())) { list.push({ name: n, uses, kind: 'NAME_CASE', note: `카탈로그 이름 ${lower.get(n.toLowerCase())}` }); continue; }
  if (isA(n)) { list.push({ name: n, uses, kind: 'A', note: '03 §3.5 대응표에 있음, 아직 추출 안 함' }); continue; }
  list.push({ name: n, uses, kind: 'C', atlas: w.atlas, color: w.color });
}

// ── 5. 임시 PNG (§3.13.3) ──
const EMO = [[/Ticket/, '🎟️'], [/Energy/, '⚡'], [/RbToken/, '🪙'], [/Medal/, '🏅'], [/PetFood/, '🍖'], [/Pet/, '🐶'], [/Yarn/, '🧶'], [/GodCoin/, '🪙'], [/GodShard/, '💎'], [/FriendCoin/, '🐾'],
  [/FriendUp/, '⬆️'], [/FriendAny/, '🐱'], [/StarCandy/, '🍬'], [/BoxPaper|BoxWood/, '📦'], [/BoxGold/, '🎁'], [/NoAds/, '🚫'], [/Drop/, '🎲'], [/_Ad$/, '📺'], [/Inapp/, '💳'],
  [/QuestDaily/, '📅'], [/QuestWeekly/, '🗓️'], [/PassXp/, '⭐'], [/StairLv/, '🪜'], [/Gauge/, '📊'], [/Clock/, '⏰'], [/Map_Zone/, '🗺️'], [/Content/, '📜'], [/Research/, '🔬'], [/Gear|Equip/, '🛡️'], [/Rune/, '🔮'], [/Bless/, '✨'], [/Attend/, '📆'], [/Roadmap/, '🧭']];
const PLATE = ['#6a8ad8', '#d87a5a', '#5ab88a', '#b87ad8', '#d8b04a', '#5aa8c8', '#c8607a', '#7a8a9a'];
const hashN = (s) => { let h = 0x811c9dc5; for (const ch of s) { h ^= ch.charCodeAt(0); h = Math.imul(h, 0x01000193); } return h >>> 0; };
const themes = loadThemes();
const C = list.filter((x) => x.kind === 'C');
const outItems = [];
if (C.length) {
  rmSync(TMP, { recursive: true, force: true }); // 이전 임시 PNG는 다시 만든다(정식이 생긴 이름은 사라진다)
  const { browser, page } = await openCanon({});
  await page.addScriptTag({ path: join(ROOT, 'inject/harness.js') });
  if (!(await page.evaluate(() => MvH.loadEmoji()))) throw new Error('이모지 글꼴 확인 실패');
  const toURL = (p) => 'data:image/png;base64,' + readFileSync(p).toString('base64');
  for (const it of C) {
    const n = it.name; const plate = it.color && /^#[0-9a-f]{6}$/i.test(it.color) ? it.color : PLATE[hashN(n) % PLATE.length];
    const shots = []; // [{suffix, dataURL, w, h, pivotPx}]
    // 1. 정본 그림 조합: World_Enemy_<X>Mouse → 쥐 프레임 색 바꾸기
    const base = /^World_Enemy_\w*Mouse$/.test(n) ? 'World_Enemy_Mouse' : null;
    if (base) {
      const dir = join(SRC, 'Sprite', `Atlas_World_Theme_${themes[0].key}`);
      const fr = readdirSync(dir).filter((f) => f.startsWith(base + '_') && /_\d\d\.png$/.test(f)).sort();
      const m = JSON.parse(readFileSync(join(SRC, 'manifest.json'), 'utf8')).items.find((x) => x.name === base);
      for (const f of fr) {
        const r = await page.evaluate(async (u) => { const im = new Image(); im.src = u; await im.decode(); const c = document.createElement('canvas'); c.width = im.width; c.height = im.height; const g = c.getContext('2d');
          g.filter = 'hue-rotate(150deg) saturate(1.4) brightness(0.9)'; g.drawImage(im, 0, 0); return { dataURL: c.toDataURL('image/png'), w: c.width, h: c.height }; }, toURL(join(dir, f)));
        shots.push({ suffix: f.slice(base.length, -4), ...r, pivot: m.pivot });
      }
      it.method = '1 정본 조합(쥐 프레임 색 바꿈)';
    } else {
      const tex = it.atlas === 'Texture';
      const w = tex ? 540 : n.startsWith('World_') ? 128 : n.startsWith('Card_') ? 192 : 96, h = tex ? 960 : w;
      const emo = EMO.find(([rx]) => rx.test(n));
      const label = n.replace(/^(UI_Icon_|UI_|World_|Tex_|Card_)/, '').replace(/_/g, '').slice(0, 2);
      const r = await page.evaluate(([w, h, plate, ch, label, tex]) => {
        const c = document.createElement('canvas'); c.width = w; c.height = h; const g = c.getContext('2d');
        const rr = (x, y, ww, hh, r) => { g.beginPath(); g.roundRect(x, y, ww, hh, r); };
        if (tex) { const gr = g.createLinearGradient(0, 0, 0, h); gr.addColorStop(0, plate); gr.addColorStop(1, '#1e2436'); g.fillStyle = gr; g.fillRect(0, 0, w, h); }
        else { rr(4, 4, w - 8, h - 8, w * 0.22); g.fillStyle = plate; g.fill(); g.lineWidth = Math.max(2, w * 0.04); g.strokeStyle = '#4a3446'; g.stroke(); }
        g.textAlign = 'center'; g.textBaseline = 'middle';
        if (ch) { g.font = `${Math.round(w * 0.6)}px MvEmoji`; g.fillStyle = '#ffffff'; g.fillText(ch, w / 2, h / 2 + w * 0.04); }
        else { g.font = `900 ${Math.round(Math.min(w, h) * 0.36)}px "Gothic A1"`; g.fillStyle = '#ffffff'; g.lineWidth = Math.max(2, w * 0.05); g.strokeStyle = '#4a3446'; g.strokeText(label, w / 2, h / 2); g.fillText(label, w / 2, h / 2); }
        return { dataURL: c.toDataURL('image/png'), w, h };
      }, [w, h, plate, emo ? emo[1] : null, label, tex]);
      shots.push({ suffix: '', ...r, pivot: n.startsWith('World_') ? [0.5, 0] : [0.5, 0.5] });
      it.method = emo ? `2 이모지 ${emo[1]}` : '3 글자 판';
    }
    const folders = it.atlas === '@theme' ? themes.map((t) => `Sprite/Atlas_World_Theme_${t.key}`) : [it.atlas === 'Texture' ? 'Texture' : `Sprite/${it.atlas}`];
    it.size = `${shots[0].w}x${shots[0].h}`; it.files = [];
    for (const fo of folders) for (const s of shots) {
      const rel = `${fo}/${n}${s.suffix}.png`; const p = join(TMP, rel); mkdirSync(join(p, '..'), { recursive: true });
      const png = Buffer.from(s.dataURL.split(',')[1], 'base64'); writeFileSync(p, png); it.files.push(rel);
      outItems.push({ name: n + s.suffix, atlas: fo.replace(/^Sprite\//, ''), file: rel, w: s.w, h: s.h, pivot: s.pivot, ppu: 128, temp: 1, method: it.method, sha256: sha256(png) });
    }
  }
  await browser.close();
}

// ── 6. 기록 ──
mkdirSync(TMP, { recursive: true });
const tsv = ['name\tuses\tkind\tsize\tmethod\tatlas\tnote', ...list.map((x) => [x.name, x.uses, x.kind, x.size || '', x.method || '', x.atlas || '', x.note || ''].join('\t'))];
writeFileSync(join(TMP, 'temp_art_list.tsv'), tsv.join('\n') + '\n');
outItems.sort((a, b) => (a.atlas + a.name).localeCompare(b.atlas + b.name));
writeFileSync(join(TMP, 'manifest.json'), JSON.stringify({ tool: 'Tools/MvExtract temp_art.mjs', tables: tableSource().replace(/^git:([^:]+):.*/, 'git:$1'), items: outItems }, null, 1) + '\n');
const cat = formal.concat(outItems.map((m) => [m.atlas, m.name, '1'])).sort((a, b) => (a[0] + '\t' + a[1]).localeCompare(b[0] + '\t' + b[1]));
writeFileSync(CAT_OUT, 'atlas\tname\ttemp\n' + cat.map((r) => r.join('\t')).join('\n') + '\n');
const cnt = (k) => list.filter((x) => x.kind === k).length;
console.log(`temp: 표 이름 ${want.size}, 없음 ${list.length} (C ${cnt('C')} → PNG ${outItems.length}, A 미추출 ${cnt('A')}, 대소문자 ${cnt('NAME_CASE')})`);
