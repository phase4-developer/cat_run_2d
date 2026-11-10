# 4. 게임 규칙 (런·메타 공식)

[입구](README.md) · 앞: [03 리소스 파이프라인](03_ResourcePipeline.md) · 다음: [05 화면](05_Screens.md)

이 문서는 `MV.Run`(Unity 비참조 순수 C#, 결정적 시뮬레이션)과 `MV.Meta`(영속 상태 변경) 담당이 **이 문서만 보고** 프로토와 같은 결과를 내도록 쓴 규칙서다. 공식은 정본 HTML 원문을 그대로 인용하고, 모든 상수가 어느 표·열로 가는지 「→ 표.열」로 적는다. 표 구조 자체는 [07](07_Tables.md)이 정한다. 이 문서가 07에 기대하는 열은 각 절의 상수 표와 §4.19 끝의 「07에 기대한 가정」에 모았다.

- 줄 번호 `L123`은 정본 `MoonVillage.html`(SHA256 `fe573cf2…26dfa`) 기준이다.
- 모든 값은 기준 프리셋 **easy**(결정 대기 [D-03](13_Decisions.md)) 기준이다.
- 값 태그: **[프로토]** 정본 원값 / **[원작]** / **[창작]** 이 기획서 임시값 / **[추정]** 코드만으로 확정 못 한 해석.
- 「결정 대기(D-2x)」 표시는 §4.19의 권장안으로 본문을 쓴 것이다.
- 표 표기(정크버스터즈 방식 표 규약): `표[key]`는 그 표의 `key` 열(string) 값으로 찾은 행이다(id는 정수 `{표번호}{그룹3}{순번3}`). `300_runConfig.NAME`은 런 상수 표에서 `key = NAME`인 행의 `value`, `00_config.name`은 앱 전역 1행의 열이다.
- 보상은 consumeData 3열(`rewardType`·`rewardId`·`rewardValue`), 비용은 `priceType`·`priceId`·`priceValue`로 적는다. consumeType 번호: 1 Wealth(`01_wealth` id: 1 츄르, 2 캣닢, 3 생선, 4 목숨 조각), 50 Drop(`801_drop`), 600 StairLv, 601 Card, 602 RunGold(런 안 츄르), 603 RunSouls(런 안 생선), **604 RunDia(런 안 캣닢) [가정, 07 확정]**. 런 안 보상의 Zone 비례분은 행의 **`rewardValuePerZone` 열**(값 = `rewardValue + rewardValuePerZone × zone`, zone은 0기반)로 적는다 [가정, 07 확정].
- 성장 비용은 공식 대신 `16_levelPrice` 행이다. 프로토 공식은 「행 생성 규칙」으로 남긴다(§4.12).

---

## 4.0 공통 전제

### 4.0.1 좌표·단위

| 항목 | 규칙 | 근거 |
|---|---|---|
| 게임 좌표 | x = 오른쪽(+), z = 전진(+), y = 위. 단위 m | L940 |
| three 변환 | `V(x,y,z) => (x, y, -z)` — `MV.Run`은 게임 좌표만 쓴다. 변환은 `MV.View` 몫 | L940 |
| 레인 | 오브젝트는 x ≈ -2.4 / 0 / 2.4 (장애물), -2.25 / 0 / 2.25 (템플릿), 플레이어는 [-3.2, 3.2] 연속값 | L738, L1793 |
| 지면 높이 `WY` | 계단을 오를 때마다 `dH`만큼 올라간다. 규칙 판정에는 쓰지 않고 표시용. `MV.Run`이 `WorldY`·`Cliff`로 보관해 View에 넘긴다 | L937, L2227, L2292 |
| 오브젝트 순서 | `R.objs`의 배열 순서가 판정 순서다(투사체 최근접 동률, 연쇄 대상, 폭풍 후보 인덱스, 게이트 통과 처리). **생성 순서를 §4.14 그대로 지킨다** | L2099, L1995, L2166 |

### 4.0.2 공통 스탯 함수 `L1744~1755`

```js
const cardN = id => (R && R.cards[id]) || 0;
const eqTier = () => { for (let t=8;t>=1;t--) if (R.w[t]>0) return t; return 1; };
const goldMul = () => (1 + 0.05*save.up.income) * (1 + 0.5*cardN('gold'));
const gaugeNeed = lv => 150 * Math.pow(1.6, lv-1) * (R.Z.hp/100) * BAL.gauge;
const GAUGE_MAX = 8;
const gateMax = lv => (lv <= 8 ? GATE_MAX[lv-1] : 15 + 2.4*(lv-8)) * BAL.scale * (R.Z.hp/100);
const craftT = k => CRAFT_T(k) * BAL.scale * (R.Z.hp/100);
const baseDmg = () => wDmg(eqTier()) * (1 + 0.2*cardN('atk')) * (1 + 0.15*save.rb.perks.power);
const critMul = () => 2.2 + 0.2*save.rb.perks.crit;
const obstMul = () => Math.max(0.5, 1 - 0.05*save.rb.perks.obst);
const diaCap = () => 10 + 2*save.rb.perks.dcap;
function mainElem(){ return R && Object.keys(R.cards).some(id=>GODCARD.has(id)) ? R.el : null; }
```
보조: `wDmg = t => Math.round(10*Math.pow(1.8, t-1))` `L772`, `GATE_MAX = [1.9,3.5,…]` `L836`, `CRAFT_T = k => 1.9 + 3.8*k` `L837`, `CRAFT_CAP = 20` `L838`.

| 상수 | 값 [프로토] | → 표.열 |
|---|---|---|
| income 1단계 츄르 배율 0.05 | 0.05 | `300_runConfig.INCOME_PER_LV` |
| gold 카드 0.5 | 0.5 | `112_cardEffect[gold].p1` |
| atk 카드 0.2 | 0.2 | `112_cardEffect[atk].p1` |
| 게이지 150, 1.6, 상한 8 | 150 / 1.6 / 8 | `107_chargeGate[well].base / step / cap` |
| `R.Z.hp` | Area 체력 | `302_area.hp` |
| `BAL.gauge`, `BAL.scale`, `BAL.diff` | 1 / 3 / 0.02 | `06_balancePreset.gauge / scale / diff` |
| `GATE_MAX[0]` 1.9 (실제 쓰는 값은 이것 하나, §4.16) | 1.9 | `107_chargeGate[sgate].base` |
| `CRAFT_T` 1.9 + 3.8k, 상한 20 | 1.9 / 3.8 / 20 | `107_chargeGate[cgate].base / step / cap` |
| `wDmg(t)` | 10,18,32,58,105,189,340,612 | `103_item.dmg` (반올림 결과를 그대로 저장) |
| 부활 power 0.15 | 0.15 | `14_rebirthPerk[power].perLv` |
| 치명 배수 2.2 + 0.2×lv | 2.2 / 0.2 | `14_rebirthPerk[crit].base / perLv` |
| 장애물 배수 max(0.5, 1 − 0.05×lv) | 1 / −0.05 / 0.5 | `14_rebirthPerk[obst].base / perLv / floor` |
| 캣닢 블록 10 + 2×lv | 10 / 2 | `14_rebirthPerk[dcap].base / perLv` |
| 신 고유 카드 집합 `GODCARD` | orb, proc, chain, storm, blast, frate, spear | `110_card.group` ≠ 0(신 카드) |

수치형 규칙(구현 필수):
- `R.gold`는 **실수(double)**로 누적한다(goldMul이 소수). 표시·정산 때만 내림한다 `L2425`, `L855`.
- `hit()`에 들어간 피해는 `Math.min(99999, Math.max(1, Math.round(dmg)))` 정수다 `L1905`. 투사체 피해 `d`, 게이트 충전 `F`, 게이지 `fill`, 화상 `dps`는 실수다.
- JS `Math.round`는 .5에서 +∞ 방향이다. C# `Math.Round`(짝수 반올림) 대신 `Math.Floor(x + 0.5)`를 쓴다(음수 없음).

---

## 4.1 입력·이동

### 4.1.1 원문 `L3843~3852`, `L3833`, `L2025`

```js
cv.addEventListener('pointerdown', e=>{ … if (mode==='lobby') startRun(); drag = {sx:e.clientX, px: R ? R.p.tx : 0}; });
cv.addEventListener('pointermove', e=>{ if (!drag || !R) return; R.p.tx = clamp(drag.px + (e.clientX - drag.sx) / (W/7.2), -3.2, 3.2); });
if (keys.l || keys.r){ if (R) R.p.tx = clamp(R.p.tx + (keys.r - keys.l)*dt*9, -3.2, 3.2); }   // frame(), 실시간 dt
P.x += (P.tx - P.x) * Math.min(1, dt*14); P.x = clamp(P.x, -3.2, 3.2);                     // updRun, 게임 dt
```

### 4.1.2 규칙

| 항목 | 규칙 | → 표.열 |
|---|---|---|
| 드래그 | 누른 순간 `sx`(화면 x)와 `px`(그때의 목표 x)를 기억. 움직이면 `tx = clamp(px + (x − sx) / (W/7.2), −3.2, 3.2)`. `W` = 9:16 게임 영역 폭(px). 즉 **게임 영역 폭 전체 드래그 = 7.2m** | `300_runConfig.DRAG_SCREEN_M`=7.2, `LANE_X_MAX`=3.2 |
| 로비 첫 터치 | 같은 터치로 런 시작 + 드래그 시작(`px`=새 런의 tx=0) | — |
| 키 | ←/→를 누르는 동안 `tx += (r − l) × dt × 9`, clamp ±3.2. dt는 **실시간 dt(0.05 상한)**, 히트스톱·배속 영향 없음. R이 있으면 모든 모드(run/cannon/result)에서 tx가 바뀐다 | `300_runConfig.KEY_MOVE_SPEED`=9 |
| 추종 | `x += (tx − x) × min(1, dt×14)`, clamp ±3.2. **run 모드 updRun 안에서만**(게임 dt) | `300_runConfig.MOVE_FOLLOW_K`=14 |
| 전진 | `spd = 4` m/s 고정, 막히면 앞면에서 멈춤 | `300_runConfig.RUN_SPEED`=4 |
| 달리기 위상 | `P.phase += dt × (blocked ? 4 : 13)` (그림용) | `300_runConfig.PHASE_RUN`=13, `PHASE_BLOCKED`=4 |
| 계단 중 | `P.x += (0 − P.x) × min(1, dt×6); P.tx = P.x; P.phase += dt×30` `L2264` | `300_runConfig.CLIMB_X_FOLLOW_K`=6, `PHASE_CLIMB`=30 |
| 런 시작 키 | Space/Enter(로비, 다른 입력 칸에 포커스 없음) `L3850` | — |

Unity 구현: `MV.Run`은 `RunInput { float? dragTargetX; int keyAxis; }`만 받는다. 화면 px → m 변환(`/ (W/7.2)`)은 `MV.UI`(Input System) 몫이다. 키 축은 `MV.Run.Tick`이 실시간 dt로 tx에 더한다(프로토와 같은 시간축, §4.13).

---

## 4.2 사격·발사 간격·분신·투사체

### 4.2.1 원문 `L2071~2093`

```js
const units = [{x:P.x, z:P.z}];
for (let i=0;i<R.clones;i++){ const [ox,oz2] = CLONE_OFF[i]; units.push({x:clamp(P.x+ox,-3.4,3.4), z:P.z+oz2}); }
const iv = 1 / (DBG.rate * (1 + 0.2*cardN('rate')) * (1 + 0.5*cardN('frate')) * (1 + 0.1*save.aup.rate));
const t = eqTier(), crC = 0.15*cardN('crit') + 0.1*save.aup.crit, dbC = Math.min(1, 0.2*cardN('dbl') + 0.1*save.aup.dbl), pierce = cardN('pierce'), el = mainElem();
P.throwT = Math.max(0, P.throwT - dt);
units.forEach((u,i)=>{
  R.fireT[i] -= dt;
  if (R.fireT[i] <= 0){
    R.fireT[i] += iv;
    const crit = Math.random() < crC, d = baseDmg() * (crit ? critMul() : 1);
    if (i===0) R.vol++;
    if (i===0 && cardN('spear') && R.vol % 2 === 0){
      … R.projs.push({x:u.x, z:u.z+0.4, sz:u.z+0.4, d:d*(1 + cardN('spear')), crit, t, pierce:99, hit:null, …, spin:false, spear:true});
      P.throwT = 0.12; return; }
    const mk = ox => { … R.projs.push({x:u.x+ox, z:u.z+0.4, sz:u.z+0.4, d, crit, t, pierce, hit:null, m, el, tr:0, spin: !(t===4||t===7)}); };
    if (Math.random() < dbC){ mk(-0.2); mk(0.2); } else mk(0);
    if (i===0){ P.throwT = 0.12; SND.play('throw'); }
  }
});
```

### 4.2.2 규칙

| 항목 | 규칙 | → 표.열 |
|---|---|---|
| 유닛 | 본체 1 + 분신 `R.clones`(0~4). 분신 i 위치 = (clamp(P.x+dx, −3.4, 3.4), P.z+dz) | 오프셋 `314_spawnGroup[clone]` idx 0~3: (−0.95,−0.9) (0.95,−0.9) (−1.9,−1.8) (1.9,−1.8); `300_runConfig.CLONE_X_MAX`=3.4, `MAX_CLONES`=4 |
| 발사 간격 | `iv = 1 / (rate × (1+0.2·rate카드) × (1+0.5·frate카드) × (1+0.1·aup.rate))`. easy 무강화 0.5초 | `06_balancePreset.rate`, `112_cardEffect[rate].p1`=0.2, `[frate].p1`=0.5, `13_areaUpgrade[rate].perLv`=0.1 |
| 위상 | 유닛별 독립 타이머 초기값 `[0, 0.13, 0.26, 0.07, 0.19]` `L1811`. `fireT -= dt; ≤0이면 += iv`(한 프레임 1발까지) | `300_runConfig.FIRE_PHASE`="0/0.13/0.26/0.07/0.19" |
| 치명 | `crit = rng < crC`, `crC = 0.15·crit카드 + 0.1·aup.crit`(상한 없음). **유닛 1회 발사(볼리)마다 1회 판정**. 피해 × critMul | `112_cardEffect[crit].p1`=0.15(잔재), `13_areaUpgrade[crit].perLv`=0.1 |
| 2연발 | 창이 아닐 때만 `rng < dbC`, `dbC = min(1, 0.2·dbl카드 + 0.1·aup.dbl)`. 성공 시 x−0.2, x+0.2에 **같은 피해·같은 치명**으로 2발 | `112_cardEffect[dbl].p1`=0.2(잔재), `13_areaUpgrade[dbl].perLv`=0.1, `300_runConfig.DBL_SHOT_DX`=0.2 |
| 관통 | 투사체 `pierce = pierce카드 수`(잔재라 항상 0). 명중 후 `--pierce < 0`이면 소멸 → p+1 대상 | `112_cardEffect[pierce].p1`=1 |
| 창(spear) | 본체(i=0)만 `R.vol`(본체 볼리 수) 증가. spear 카드가 있고 `vol % 2 == 0`이면 일반탄 대신 창 1발: 피해 `d × (1 + spear카드 수)`, pierce 99, 2연발 판정 없음, throw 소리 없음 | `112_cardEffect[spear].p1`=2(주기), `p2`=1(장당 배율), `p3`=99 |
| 피해 | `d = baseDmg() × (crit ? critMul() : 1)` (실수) | §4.0.2 |
| 출발점 | (u.x [+ox], u.z + 0.4), `sz` = 출발 z | `300_runConfig.PROJ_SPAWN_DZ`=0.4 |
| 던지기 | 본체 발사 시 `throwT = 0.12`(그림), 매 프레임 −dt | `300_runConfig.THROW_T`=0.12 |
| 속성 `el` | 신 고유 카드 1장 이상이면 `R.el`(bolt/fire), 아니면 null. **피해에 영향 없음**, 탄 그림·꼬리 색만 | `113_god.element` |
| 회전 그림 | 등급 4·7은 눕힘, 그 외 회전 `spin` | `103_item` (02/03 문서 소관) |

**난수 소비 순서(유닛마다, 발사할 때)**: ① 치명 1회(확률 0이어도 소비) ② 창이 아니면 2연발 1회. §4.17.

### 4.2.3 투사체 이동·명중 `L2094~2117`

```js
const PSPD = 20, RANGE = 22 * (1 + 0.1*save.aup.range);
for (const b of R.projs){
  if (b.dead) continue;
  const nzb = b.z + PSPD*dt;
  let best = null, bz = 1e9;
  for (const o of R.objs){
    if (!hittable(o) || Math.abs(b.x - o.x) > o.hw) continue;
    if (b.hit && b.hit.includes(o)) continue;
    const f = o.z - o.hd;
    if (f <= nzb && o.z + o.hd >= b.z && f < bz){ bz = f; best = o; }
  }
  if (best){ hit(best, b.d, b.crit, {proc:true}); impact(…); SND.play(b.crit ? 'crit' : 'hit'); sparks(…);
    (b.hit ||= []).push(best); if (--b.pierce < 0){ b.dead = true; continue; } }
  b.z = nzb; … if (b.z - b.sz > RANGE) b.dead = true;
}
```

| 항목 | 규칙 | → 표.열 |
|---|---|---|
| 속도 | 20 m/s | `300_runConfig.PROJ_SPEED`=20 |
| 사거리 | 22m × (1 + 0.1·aup.range). 출발 z 기준 | `300_runConfig.PROJ_RANGE`=22, `13_areaUpgrade[range].perLv`=0.1 |
| 가로 판정 | `|b.x − o.x| ≤ o.hw` (탄 반경 0) | `105_objectKind.hw` |
| 세로 판정 | 이번 프레임 이동 구간 [b.z, nzb]와 대상 [z−hd, z+hd]가 겹치는 대상 중 **앞면(z−hd)이 가장 작은 것 1개**. 동률이면 `R.objs` 앞쪽 | `105_objectKind.hd` |
| 1프레임 1대상 | 관통탄도 프레임당 1대상. 맞힌 대상은 `b.hit`에 기록해 다시 안 맞힘 | — |
| 명중 시 | `hit(best, b.d, b.crit, {proc:true})` → 신 가호 발동 가능(§4.9.2) | — |
| 계단 진입 | 모든 투사체 제거 `L2245` | — |

---

## 4.3 오브젝트 판정·피격 분기

### 4.3.1 판정 함수 `L1896~1902`

```js
function hittable(o){
  if (o.dead || o.k==='stair' || o.k==='arch') return false;
  if ((o.k==='sgate'||o.k==='cgate') && (o.done || o.z < R.p.z)) return false;
  return true;
}
const breakable = o => !o.dead && (o.k==='tower'||o.k==='coin'||o.k==='gem'||o.k==='cell'||o.k==='wall'||o.k==='enemy');
const blocks = o => !o.dead && (o.k==='cell' || o.k==='wall');
```

| kind (`105_objectKind.key`) | protoType | hw / hd | hittable | breakable | blocks | 체력 규칙 `hpRuleId` | 반응 `hitReactionId` |
|---|---|---|---|---|---|---|---|
| tower (벽돌 우물) | 7 | 1.25 / 1.1 | Always | O | X | 없음(무한) | ChargeWell |
| enemy (쥐) | 5 | 0.6 / 0.5 | Always | O | X | AreaHpMul 0.4 | Damage |
| sgate (계단 게이트) | 14 | 1.25 / 0.12 | BeforePass | X | X | 없음 | ChargeStairGate |
| cgate (장난감 공방) | 13 | 1.25 / 0.12 | BeforePass | X | X | 없음 | ChargeCraftGate |
| stair (우다다 계단) | 15 | 1.6 / 1.0 | Never | X | X | 없음 | None |
| coin (츄르 상자) | 2 | 0.9 / 0.7 | Always | O | X | AreaHpMul 0.6 | Damage(+타격당 츄르) |
| gem (캣닢 화분) | 3 | 0.9 / 0.7 | Always | O | X | AreaHpMul 0.6 | Damage |
| coinBonus / gemBonus | (보너스 줄) | 0.9 / 0.7 | Always | O | X | AreaHpMul 0.25 / 0.4 | Damage |
| wall (골목 장애물) | (필드) | 1.15 / 0.55 | Always | O | **O** | FieldLerp | Damage |
| arch (골목·Area 아치) | (필드) | — | Never | X | X | 없음 | None |

- `hittable` 열 값: `Always` / `BeforePass`(= `!done && o.z ≥ P.z`) / `Never` → `105_objectKind.hittable`(07에 enum 열로 기대).
- **범위 스킬(breakable 기반: 폭풍·하악질·위성·빛·유성·칼날)은 게이트를 충전하지 않지만 우물(tower)은 채운다.** 게이트 충전은 투사체·정전기 연쇄·불씨(hittable 기반)로만 된다.
- coinBonus/gemBonus는 프로토에선 `k:'coin'/'gem'` 그대로다 `L1788`. 표에서 체력·보상만 다른 별도 행으로 둔다(`sprite`·라벨은 coin/gem과 같은 값) [창작 정리].
- enemy 행은 `105_objectKind[enemy]`(판정 플래그·컴포넌트)와 `102_enemy[mouse]`(sprite, hpRuleId, moveAiId, 보상 consumeData, hitSound, breakSound, hitEffect, breakEffect)로 나뉜다. `105_objectKind[enemy]`가 `enemyId` 열로 102 행을 가리킨다 [가정, 07 확정].

### 4.3.2 hit(o, dmg, crit, opts) 공통 `L1903~1907`

```js
if (o.chill > 0) dmg *= 1 + 0.25*cardN('frost');          // o.chill을 세우는 코드 없음 → 미동작(§4.16)
dmg = Math.min(99999, Math.max(1, Math.round(dmg)));
const gm = goldMul(), hx = o.x, hz = o.z - o.hd, q = opts.quiet;
const dcol = opts.col || (crit ? '#ff6a3d' : '#ffffff');
```
- `opts.quiet`면 피해 숫자 팝업만 생략(보상 팝업은 나옴). `opts.col`이 숫자 색을 덮는다(가호 `#bfeaff`/`#ffb36b`, 화상 `#ff9a4a`, 빛 `#ffe08a`, 유성 `#ffb36b`).
- → `300_runConfig.DMG_MIN`=1, `DMG_MAX`=99999.

### 4.3.3 kind별 분기 `L1909~1980`

| kind | 상태 변화 | 보상(즉시 R에 가산) | 파괴 시 | 이벤트(§4.18) |
|---|---|---|---|---|
| tower | `o.pend += dmg`, shake 0.12(그림) | 게이지(§4.5) | 없음 | `Hit`(sfx clank, 30% 불꽃은 Fx 난수) |
| enemy | `o.hp -= dmg`, flash 0.08 | 사망 시 `R.gold += reward × goldMul()`; **8% 확률** 캣닢 `ceil(diaCap()/2)` | `dead`, **hitstop 0.03** | `Hit`, `Break`, `Gain`×1~2, `Hitstop` |
| wall | `o.hp -= dmg` | 파괴 시 `rew`별: soul → `R.souls += amt` / dia → `R.dia += amt` / gold → `R.gold += amt × goldMul()` | `dead`, `R.walls++`, **shake(0.16, 0.14)**, **hitstop 0.06** | `Hit`, `Break`, `Gain`, `Shake`, `Hitstop` |
| sgate | §4.4 공식 | 계단(통과 시) | — | `GateFill` |
| cgate | §4.4 공식 | 무기(통과 시) | — | `GateFill` |
| coin / gem | `o.hp -= dmg`, **맞을 때마다 `R.gold += 1 × gm`** | 파괴 시 `o.dia > 0`이면 `R.dia += o.dia`, 아니면 `R.gold += reward × gm` | `dead`, **hitstop 0.03** | `Hit`, `Gain`, `Break`, `Hitstop` |
| stair / cell | 도달 불가(§4.16) | — | — | — |

| 상수 | 값 | → 표.열 |
|---|---|---|
| 쥐 처치 츄르 6 + 4×zone (× goldMul) | 6 / 4 | `102_enemy[mouse]` `rewardType`=602 RunGold, `rewardValue`=6, `rewardValuePerZone`=4 (zone = 0기반 `save.zone`) |
| 쥐 캣닢 8%, 양 ceil(diaCap/2) | 0.08 | `102_enemy[mouse]` `dropId`=`801_drop.dropGroup`(consumeType 50 Drop. 묶음 801001001 = 보상 행 weight 800(rewardType 604 RunDia) + 꽝 행 weight 9200, 양 = `801_drop.rewardValueRule=HalfDiaCapCeil`, 07 §7.9.3 확정. 균등 난수 1개로 판정해 정본 `Math.random() < 0.08`과 같음) |
| 상자 보상 20 + 10×zone (× goldMul) | 20 / 10 | `105_objectKind[coin]` rewardType 602, rewardValue 20, rewardValuePerZone 10 |
| 캣닢 화분 diaCap() | 10 + 2·dcap | `105_objectKind[gem]` rewardType 604, rewardValue 10(= diaCap 기본, 부활 dcap 가산은 `14_rebirthPerk[dcap]`) |
| 상자 타격당 츄르 1 × goldMul | 1 | `105_objectKind[coin/gem]` `hitRewardType`=602, `hitRewardValue`=1 (07 §7.5.2 확정) |
| 보너스 상자 12 + 6×zone / 캣닢 1 | 12 / 6 / 1 | `105_objectKind[coinBonus]` 602·12·perZone 6, `105_objectKind[gemBonus]` 604·1 |
| goldMul 적용 | RunGold 보상만 | 코드 규칙: consumeType 602 보상은 지급 시 × goldMul(). 604·603은 배수 없음 |
| 히트스톱 0.03 / 0.06 / 0.03 | | `300_runConfig.hitstop.enemyKill / hitstop.wallBreak / hitstop.coinBreak` |
| 장애물 파괴 흔들림 | 0.16 / 0.14 | `300_runConfig.shake.wallBreak.amp`=0.16 / `.time`=0.14 |

- 보상 아이콘이 HUD로 날아가는 연출(flyTo 560ms)은 **표시만**이다. 재화는 `hit()` 안에서 즉시 가산된다 `L1735~1741`.
- wall 라벨에 보이는 츄르는 `fmt(o.amt*goldMul())`(현재 goldMul) `L1497`.

---

## 4.4 계단 게이트·장난감 공방 충전과 통과 수령

### 4.4.1 충전 `L1945~1956`

```js
case 'sgate': {
  const M = gateMax(1) * 2;
  o.F += dmg / (1 + o.n*BAL.diff); o.flash = 0.1; o.punch = 0.5;
  let got = 0; while (o.F >= M){ o.F -= M; o.n += R.gauge.lv; got += R.gauge.lv; }
  if (got && !q) pop(…, `+${got}`, '#6fe08f', 1.1);
}
case 'cgate': {
  o.F += dmg; …
  while (o.made < CRAFT_CAP && o.F >= craftT(o.made)){ o.F -= craftT(o.made); o.made++; pop(…'+1'…); }
  if (o.made >= CRAFT_CAP) o.F = 0;
}
```

| 게이트 | 문턱 | 1회 넘을 때 | 감쇠 | Area 1 easy 값 | → 표.열 |
|---|---|---|---|---|---|
| sgate | `M = 1.9 × 2 × scale × hp/100` (항상 lv 1 기준) | `n += 게이지 Lv`, `got += 게이지 Lv` | 피해 ÷ `(1 + n × diff)`, n은 이미 모은 계단 수 | M = 11.4 | `107_chargeGate[sgate].base`=1.9, `.mul`=2, `.diffMul`=1(× `06_balancePreset.diff`) |
| cgate | k번째 무기 `(1.9 + 3.8k) × scale × hp/100` | `made++`(무기 1장) | 없음. 20장이면 F=0 | 5.7, 17.1, 28.5, 39.9 … | `107_chargeGate[cgate].base`=1.9, `.step`=3.8, `.cap`=20 |

- 라벨 진행도: sgate `clamp(F/(gateMax(1)*2), 0, 1)`, cgate `made ≥ 20 ? 1 : clamp(F/craftT(made), 0, 1)`, 20단계 양자화 `L1505~1514` (View 소관, [02](02_Visual.md)).
- sgate 라벨 `칸당 +${lv}`의 lv = `R.gauge.lv`.

### 4.4.2 통과 판정 `L2041~2056`

```js
for (const o of R.objs){
  if ((o.k!=='sgate' && o.k!=='cgate') || o.done || !(oz < o.z && P.z >= o.z)) continue;
  o.done = true;
  const inGate = Math.abs(P.x - o.x) < o.hw + 0.2;
  …
}
```

| 경우 | sgate | cgate |
|---|---|---|
| 판정 시점 | 이번 프레임에 `oz < o.z ≤ P.z` (oz = 이동 전 z). 1회만, 이후 `done` → 더 이상 hittable 아님 | 같음 |
| 안(`|P.x − o.x| < 1.45`), n>0 | 소리 gate, 아이콘이 `#hStair`로 560ms 비행 → **도착 시 `R.stairs += n`** | 소리 forge, `R.pendingW.push({t:0.5, n:made})`, 아이콘 `min(made,5)`개를 70ms 간격 비행(표시만) |
| 안, 0 | 팝 `계단 0` | 팝 `무기 0` |
| 밖, >0 | 팝 `계단 ${n} 놓침` | 팝 `무기 ${made}개 놓침` |
| 밖, 0 | 없음 | 없음 |
| 가라앉기 | 안이면 `sink = 0.001`, 매 프레임 `sink += dt`(1까지, 그림) | 같음 |

| 상수 | 값 | → 표.열 |
|---|---|---|
| 통과 여유 0.2 | 0.2 | `108_receiveCondition[sgatePass].margin`, `[cgatePass].margin` (type OnPass) |
| 계단 수령 지연 560ms (실시간) | 560 | `108_receiveCondition[sgatePass].delayMs`, `clock=Real` — 결정 대기(D-21, D-22) |
| 무기 지급 지연 0.5초 (게임 시간) | 500 | `108_receiveCondition[cgatePass].delayMs`, `clock=Game` |

### 4.4.3 무기 지급 `L2057~2058`, `L1847~1851`

```js
for (const w of R.pendingW){ w.t -= dt; if (w.t <= 0 && !w.done){ w.done = true; for (let i=0;i<w.n;i++) addWeapon(R.bt, true); handDeliver(w.n, R.bt); } }
function addWeapon(t, quiet){ R.w[t]++; let merged = false;
  while (t < 8 && R.w[t] >= 3){ R.w[t] -= 3; t++; R.w[t]++; merged = true; }
  if (merged && cardN('spin')) rebuildUnits(); }
```
- 지급 등급은 `R.bt = baseTier(save.up.weapon)`(로비 「스크래쳐」 무기 Level, §4.12) — **1등급이 아니다**. 비행 아이콘은 `weaponURL(1)` 고정이라 그림과 다르다 [프로토 불일치, 그림만].
- `R.w[1..8]`은 즉시 3장 → 다음 등급 1장 머지(최대 8). 피해는 `eqTier()`(R.w 최고 등급)로 즉시 반영. 손패 연출은 §4.10.
- `pendingW`는 updRun 안에서만 줄어든다(계단 중 멈춤).

---

## 4.5 벽돌 우물(tower)·계단 게이지

### 4.5.1 흐름

| 단계 | 규칙 | 시간축 | 근거 |
|---|---|---|---|
| ① 피격 | `o.pend += dmg` (체력 없음) | — | L1910 |
| ② 묶음 | `o.ft += dt`; `pend > 0` 이고 (`ft > 0.22` 또는 우물이 플레이어 뒤 0.5m를 지남 `o.z < P.z − 0.5`)이면 `ft = 0`, 배출 | 실시간 dt (syncScene) | L1470~1471 |
| ③ 조각 수 | `n = min(4, 1 + floor(pend / (gaugeNeed(lv) × 0.1)))` (그림용 1~4개) | — | L1471 |
| ④ 튐 | 조각 수명 0.26초 | 게임 시간 sdt (stepFx) | L1872 |
| ⑤ 비행 | 수명 끝에 `!R || R.fin`이면 중단, 아니면 게이지 막대로 560ms 비행 | 실시간 | L1873 |
| ⑥ 수령 | **마지막 조각 도착 시 1회 `gaugeAdd(amt)`** (amt = 배출한 pend 전부) | — | L1873 |
| (대체) | 보이는 벽돌이 없으면 `flushOre`: 아이콘 n개를 60ms 간격 비행, 마지막 도착 시 `gaugeAdd` | 실시간 | L1864, L1878~1886 |

Unity 규칙(결정 대기 D-21): `MV.Run`이 위 ②~⑥을 **표시와 분리된 결정적 타이머**로 가진다. 배출 시 `WellFlush{objId, amount, bricks}` 이벤트를 내고, 0.26초(게임) + 0.56초(실시간) 뒤 `GaugeReceive`를 처리한다. `flushOre` 경로(벽돌 44개가 다 빠진 경우, 차이 ≤ 0.2초)는 따로 모델링하지 않는다 [창작 단순화].

| 상수 | 값 | → 표.열 |
|---|---|---|
| 묶음 간격 0.22 | 0.22 | `300_runConfig.fx.brickFlushInterval` |
| 지나침 판정 0.5 | 0.5 | `300_runConfig.WELL_FLUSH_BEHIND` |
| 조각 최대 4, 기준 0.1 | 4 / 0.1 | `300_runConfig.WELL_BRICK_MAX / WELL_BRICK_DIV` |
| 튐 0.26 / 비행 560ms | 0.26 / 560 | `108_receiveCondition[wellGauge].delayMs`=820 내역(게임 260 + 실시간 560), 그림 값은 `300_runConfig.fly.removeMs`(560) |

### 4.5.2 gaugeAdd `L1887~1893`

```js
const g = R.gauge; if (g.lv >= GAUGE_MAX){ g.fill = 0; return; }
g.fill += d;
while (g.lv < GAUGE_MAX && g.fill >= gaugeNeed(g.lv)){ g.fill -= gaugeNeed(g.lv); g.lv++; pop(…`계단 Lv ${g.lv}! 게이트 +${g.lv}`…); SND.play('lvup'); … }
if (g.lv >= GAUGE_MAX) g.fill = 0;
```
- 넘친 양은 이월(한 번에 여러 Lv). Lv 8이면 fill 0 고정, 이후 수령분은 버림.
- 게이지 Lv의 의미: sgate 문턱 1회당 계단 +Lv(§4.4).
- 필요량(easy, Area 1): Lv1→2 150, 240, 384, 614.4, 983.04, 1572.86, 2516.58 (Lv7→8). Area 체력 비례(Area 2 ×2, Area 3 ×5 …).
- 시작값 `newRun` `L1816`: `k = kegStart(save.up.stair); R.gauge.lv = k.lv; R.gauge.fill = k.lv < 8 ? (k.step/k.need)*gaugeNeed(k.lv) : 0;` → 로비 진행률을 런 게이지 비율로 환산.

---

## 4.6 적(쥐) AI·상자

### 4.6.1 쥐 `L2059~2064`

```js
for (const o of R.objs){ if (o.k !== 'enemy' || o.dead) continue;
  if (o.z - P.z < 24){ o.z -= o.spd*dt; o.x += clamp(P.x - o.x, -1, 1)*0.5*dt; }
  if (Math.abs(o.x - P.x) < 0.8 && Math.abs(o.z - P.z) < 0.7){ o.dead = true; const st = Math.min(Math.floor(R.gold), 5 + 4*save.zone);
    R.gold -= st; …; pop(P.x, 2.4, P.z + 0.6, st ? `츄르 -${st}` : '휙!', '#ff9a7a', 1.1); shake(0.12, 0.12); }
  if (o.z < P.z - 4) o.dead = true; }
```

| 항목 | 값 [프로토] | → 표.열 (`109_moveAI[mouse]`, type Chase+Steal) |
|---|---|---|
| 감지 거리(앞) | 24 m | `aggro` |
| 전진 속도 | `spd` = 생성 시 균등 난수 [1.1, 1.6) m/s, −z 방향 | `speedMin` / `speedMax` |
| 가로 추적 | `clamp(dx, −1, 1) × 0.5` m/s | `xFollow`=0.5 |
| 접촉 | `|dx| < 0.8 && |dz| < 0.7` | `touchX` / `touchZ` |
| 훔침 | `min(floor(R.gold), 5 + 4×zone)`, 쥐 소멸(보상 없음, 플레이어 사망 아님), shake(0.12, 0.12) | `stealBase`=5, `stealPerZone`=4; `300_runConfig.shake.steal` |
| 이탈 | 플레이어 뒤 4m → 보상 없이 소멸 | `despawnBehind`=4 |
| 무리 | 5마리, `314_spawnGroup[mouse5]` | §4.14 |

- 쥐 체력 `round(Z.hp × 0.4)`, 보상 `6 + 4×zone`(§4.3, `102_enemy` consumeData). **BAL 미적용**.
- AI는 updRun 안에서만(계단 중 정지).

### 4.6.2 상자(coin/gem)

| 종류 | 생성 | 체력 | 보상 | 근거 |
|---|---|---|---|---|
| coin (템플릿 type 2) | 템플릿 좌표 | `round(Z.hp × 0.6)` | 타격당 `1×gm` + 파괴 `(20+10·zone)×gm` | L1328, L1959~1967 |
| gem (type 3) | 템플릿 좌표 | `round(Z.hp × 0.6)` | 타격당 `1×gm` + 파괴 캣닢 `diaCap()` | L1328 |
| 보너스 coin | 구간 끝 2줄×3레인, 가운데 2번째 줄 제외 | `round(Z.hp × 0.25)` | 타격당 `1×gm` + `(12+6·zone)×gm` | L1787~1788 |
| 보너스 gem | 2번째 줄 가운데 | `round(Z.hp × 0.4)` | 타격당 `1×gm` + 캣닢 1 | L1787~1788 |

→ `114_hpRule[box06]` AreaHpMul 0.6, `[bonus025]` 0.25, `[bonus04]` 0.4, `[enemy04]` 0.4 (round = JS 반올림). 보상은 §4.3 표.

---

## 4.7 골목 장애물 필드·아치·Floor 3단·체크포인트·HP 지속

### 4.7.1 구조

런 1회 = 플레이 구간(템플릿) → 보너스 상자 2줄 → 아치 「골목 1」 → 필드 1 → 「골목 2」 → 필드 2 → 「골목 3」 → 필드 3 → 아치 「Area N 돌파」(gz). 체크포인트 cp가 있으면 골목 cp+1부터 깐다(§4.14 buildLevel). 필드는 가로 3레인 × 세로 rows줄, **한 레인만 끝까지 뚫으면 통과**(레인 사이 빈틈 없음).

### 4.7.2 벽 막힘 `L2033~2038`, `L2066~2070`

```js
for (const o of R.objs){
  if (!blocks(o) || Math.abs(P.x - o.x) >= o.hw + 0.32) continue;
  const face = o.z - o.hd - 0.35;
  if (P.z <= face + 0.02 && nz > face){ nz = face; blocked = o; }
}
const oz = P.z; P.z = Math.max(P.z, nz);
…
if (blocked){ shake(0.35, 0.3); SND.play('wood'); …; pop(P.x, 2.4, P.z+0.5, '쾅!', '#ffb08a', 1.6);
  R.hitWall = blocked; return finish(blocked.k); }
```
- 막힘 x 범위 = 레인 중심 ± (1.15 + 0.32) = ±1.47m, 레인 간격 2.4m → 빈틈 없음.
- 막히면 **그 프레임의 게이트 통과·무기 지급·쥐 처리까지 한 뒤** `finish('wall')`. 사격은 하지 않는다.
- → `300_runConfig.PLAYER_HALF_W`=0.32, `WALL_FACE_GAP`=0.35, `WALL_FACE_EPS`=0.02, `shake.blocked.amp`=0.35 / `.time`=0.3.
- 경고: 같은 레인 폭(`|dx| < hw + 0.32`) 안 살아 있는 wall이 앞 9m 안에 있으면 HUD `전방 장애물!`, 처음 켜질 때 warn 소리 `L2738~2741` → `300_runConfig.WARN_DIST`=9. `MV.Run`은 `NearestBlockerAhead()` 조회만 제공하고 켜짐 판단은 `MV.UI`.

### 4.7.3 아치 통과 배너 `L2026~2028`

```js
for (const g of R.gates) if (!g.passed && P.z > g.z){ g.passed = true; R.floor = g.floor;
  const prev = g.floor - 2, ok = prev < 0 || R.wl.every(o=>o.bar!==prev || o.rew!=='soul' || o.dead);
  SND.play('floor'); banner(`골목 ${g.floor}`, g.floor === 1 ? '장애물 골목 · 한 줄만 뚫으면 통과' : ok ? `골목 ${g.floor-1} 생선 전부 획득 · 체크포인트` : `골목 ${g.floor-1} 생선 미획득`); }
if (P.z > R.gz){ return finish('gate'); }
```
- 아치는 표시·배너만. 체크포인트 저장은 `finish`에서 한다.
- `R.gates`는 아치 z 목록(`{z, floor}`), `R.floor`는 HUD용 현재 골목(0 = 플레이 구간).
- Area 돌파 판정은 **이동 전 P.z**로 프레임 처음에 한다.

### 4.7.4 체크포인트·HP 지속 `L2396~2403`, `L2429~2435`, `L1780~1782`

```js
const walls = R.objs.filter(o=>o.k==='wall' && o.lvl===R.level);
const rec = JSON.parse(JSON.stringify(R.savedBars || {}));
for (const f of [0,1,2]) rec[f] = rec[f] || [];
for (const o of walls) rec[o.bar][o.idx] = Math.max(0, o.hp);
let cpN = 0; while (cpN < 2 && floorSouls(save.area, rec, cpN)[0] >= floorSouls(save.area, rec, cpN)[1]) cpN++;
R.cpRun = Math.max(R.cp, cpN);
function floorSouls(A, rec, f){ let have = 0, tot = 0;
  for (const c of fieldSpec(A, f)) if (c.rew === 'soul'){ tot += c.amt; if (rec && rec[f] && rec[f][c.idx] != null && rec[f][c.idx] <= 0) have += c.amt; }
  return [have, tot]; }
```
- 기록 `rec[floor][idx] = 남은 HP(0 이상)`. 이번 런에 깔지 않은 골목(cp 이전)은 저장된 값을 그대로 복사한다.
- 체크포인트 = 골목 1부터 **연속으로** 생선 장애물을 전부 부순 골목 수(최대 2 → 골목 3은 항상 플레이). `save.cp = max(save.cp, cpRun)` (내려가지 않음).
- 다음 런 buildLevel: `hp = min(저장값, max)`, 0 이하면 `dead + pre`(이미 부숨, 보상 없음).
- 저장 키 `save.bars['A'+area]`, **Area가 바뀌면 비움**.
- → `300_runConfig.CP_MAX`=2, `FLOOR_COUNT`=3. 저장 형식은 [09](09_Save.md).

---

## 4.8 계단 오르기(climb)·카드 룰렛

### 4.8.1 진입 `L2032`, `L2242~2261`

```js
for (const o of R.objs) if (o.k==='stair' && !o.used && nz >= o.z){ o.used = true; if (startClimb(o)){ P.z = o.z; return; } }
function startClimb(o){
  const n = R.stairs;
  if (n <= 0){ toast('모은 계단이 없습니다'); return false; }
  for (const b of R.projs) projG.remove(b.m); R.projs = []; R.stairs = 0;
  mode = 'cannon'; warpSt = -1;
  const k = n <= 60 ? n : Math.min(150, 60 + Math.ceil((n - 60)/3)), pw = n / k, z0 = o.z + 1.4, zEnd = z0 + k*STEP_D, dH = k*STEP_H;
  shiftWorld(o.z + 0.5, k*STEP_D + 8, zEnd, dH);
  … CN = {o, n, k, pw, z0, zEnd, dH, steps, t:0, pos:0, phase:'fly', shown:0, tier:-1, lap:0, cards:[], rem:0, T: Math.min(9, 1.6 + k*0.05)};
  SND.play('gate'); return true;
}
```
- 계단 판정은 벽 막힘보다 **먼저**, 한 번만(`used`). 계단 0이면 토스트 후 계속 달린다(재시도 없음).
- 성공하면 `P.z = o.z`, 이 프레임 나머지(게이트·적·사격) 생략.
- 칸 압축: n ≤ 60 → k = n, 초과분 3계단당 1칸, 최대 150칸(n ≥ 327). `pw = n/k`.
- **shiftWorld** `L2226~2240`: `z ≤ o.z + 0.5` 오브젝트는 `R.objs`·`R.wl`에서 **제거**(계단 자신 포함). 나머지·아치 목록·`R.gz`는 `L = 0.72k + 8`만큼 +z. 결과적으로 계단 뒤 d만큼 앞에 있던 오브젝트는 오른 뒤 플레이어 앞 `d + 6.4`m에 있다(`zEnd + 0.2` 착지).
- `WY += dH`(= 0.42k), `R.cliff = {z:zEnd, low, high}`는 View용.

### 4.8.2 진행 updClimb `L2262~2288`

```js
const p = Math.min(1, C.t / C.T), e = p < 0.12 ? p*p/0.24 : p - 0.06;
C.pos = Math.min(C.k, e/0.94 * C.k);
C.rise = (C.rise || 0) + dt*Math.max(30, C.k*1.6);
for (let i=0;i<Math.min(C.k, Math.ceil(C.pos + 6 + C.rise));i++){ … 처음 보이면 st.t = 0, i % 4 === 0이면 tick 소리 }
C.shown = Math.min(C.n, Math.floor(C.pos * C.pw + 0.0001)); if (p >= 1) C.shown = C.n;
while (C.lap < MAX_CLIMB_CARDS - 1 && C.shown >= (C.lap + 1)*LAP){ C.lap++; C.cards.push(4); C.tier = -1; … SND.play('card', 4); shake(0.22, 0.2); }
C.rem = Math.min(LAP, C.shown - C.lap*LAP);
const t = tierFor(C.rem);
if (t > C.tier){ C.tier = t; SND.play('tierup', t); … shake(0.12, 0.12); 화면 플래시 }
if (p >= 1){ … pop(`${C.n}칸 우다다!`); SND.play('land'); endClimb(); }
```
- 오르는 시간 `T = min(9, 1.6 + 0.05k)`초. 처음 12%는 2차 가속, 이후 등속.
- 75계단마다 LEGENDARY(4) 1장 확정, 최대 2장. 마지막 랩 나머지 `rem`(최대 75)으로 등급 1장: 5/10/20/40/75 → C/U/R/E/L, 5 미만이면 없음.
- 결과 예: n=4 → 없음, 5 → [C], 74 → [E], 75 → [L], 80 → [L, C], 150 → [L, L], 200 → [L, L, E], 225 이상 → [L, L, L].
- 워프 단계(그림·소리·흔들림) `L3162~3165`: `C.shown` ≥ 0/25/75/150 → 단계 0~3, 단계가 바뀌고 >0이면 shake(0.18, 0.2) + tierup(min(4, st+1)). 프로토는 draw2D 안에서 판정하지만 Unity는 `MV.Run`이 `ClimbWarpStage` 이벤트로 낸다.
- 시간: `updClimb(sdt)` — 히트스톱 없음(run 모드만), 배속 적용.

### 4.8.3 endClimb `L2289~2296`

```js
R.climbed += C.n; R.lvClimbed += C.n; R.climbs.push(C.n);
R.p.z = C.zEnd + 0.2; R.p.tx = R.p.x;
WY += C.dH; …
mode = 'run'; CN = null;
const tiers = C.cards.slice(); if (C.tier >= 0) tiers.push(C.tier);
startRoulette(tiers, C.n);
```

| 상수 | 값 | → 표.열 (`330_climb[default]`) |
|---|---|---|
| 랩 75, 카드 최대 3, 랩 카드 등급 4 | 75 / 3 / 4 | `lap`, `maxCards`, `lapTier` |
| 압축 60 / 3 / 150 | | `kDirectMax`, `kCompress`, `kMax` |
| 칸 0.42 × 0.72 × 8.6 | | `stepH`, `stepD`, `stepW` |
| z0 간격 1.4, 제거 기준 0.5, 여분 8, 착지 0.2 | | `z0Gap`, `shiftFrom`, `shiftExtra`, `landGap` |
| T = min(9, 1.6 + 0.05k), 가속 0.12 | | `tBase`, `tPerStep`, `tMax`, `accelP` |
| 칸 등장 30 / 1.6 / 6, 낙하 0.2초·높이 5 | | `riseMin`, `risePerK`, `riseAhead`, `dropSec`, `dropH` |
| 등급 문턱 5/10/20/40/75, 색 | | `115_cardTier.need`, `.color` |
| 워프 단계 0/25/75/150 | | `330_climb` 워프 단계 행 `at`(0/25/75/150, 이름·색은 06/07) |
| 흔들림 랩 / 등급 / 워프 | 0.22,0.2 / 0.12,0.12 / 0.18,0.2 | `300_runConfig.shake.climbLap / shake.climbTier / shake.warpStage` |

### 4.8.4 카드 뽑기·룰렛 `L2299~2303`, `L2358~2391`

```js
function validCard(c){ return !((c.id==='clone1'||c.id==='clone2') && R.clones >= MAX_CLONES); }
function drawCard(tier){
  for (let k=tier; k>=0; k--){ const pool = CARDS.filter(c=>c.t===k && validCard(c)); if (pool.length) return pool[Math.floor(Math.random()*pool.length)]; }
  const any = CARDS.filter(validCard); return any[Math.floor(Math.random()*any.length)];
}
function startRoulette(tiers, m){
  if (!tiers.length){ toast(`우다다 ${m}칸 · 5칸 미만이라 가호 없음`); return; }
  RL = {tiers, m, t:0, cards: tiers.map((tier, i)=>{ const pick = drawCard(tier); … pool 섞기(그림) …
    return {tier, pick, pool, step:i, next:0, done:false, endAt: 1.0 + i*0.45}; }), endT:0};
}
// updRoulette: RL.t >= c.next → 얼굴 넘김(0번만 tick), c.next = RL.t + 0.06 + (rem < 0.4 ? (0.4 - rem)*0.35 : 0)
//              RL.t >= c.endAt → 확정(card 소리, 팝 `${이름} · ${설명}`), flyAt = RL.t + 1.5
//              RL.t >= flyAt → flyCard → 620ms(실시간) 뒤 applyCard + buy 소리
//              모두 도착 + 0.3초 → endRoulette
```
- 결과는 **startRoulette 때 확정**(tier 순서대로 drawCard). 같은 등급 풀에서 균등, 비면 한 단계씩 아래로. 분신 4명이면 clone 카드 제외. 중복 획득 가능.
- 풀(신 1명): t0 atk/rate/clone1/gold, t1 orb, t2 proc/chain, t3 storm, t4 blast/frate/spear.
- 카드 i 확정 = 1.0 + 0.45i초(게임 시간 sdt, run에선 히트스톱 영향), 적용 ≈ 확정 + 1.5 + 0.62(실시간).
- 룰렛 중에도 mode='run'(계속 달리고 싸움). `RL`은 run/cannon/result에서 갱신(`mode !== 'lobby'`).
- 이전 룰렛이 끝나기 전 다음 계단을 오르면 `RL`이 덮어써져 미적용 카드가 사라진다 — 결정 대기(D-23).

| 상수 | 값 | → 표.열 |
|---|---|---|
| 확정 1.0 + 0.45i | 1.0 / 0.45 | `330_climb[default].rlEndBase / rlEndStep` |
| 넘김 0.06, 감속 구간 0.4, 계수 0.35 | | `330_climb[default].rlFlipSec`, `rlSlowWin`, `rlSlowK` |
| 비행 대기 1.5, 비행 620ms, 종료 여유 0.3 | | `330_climb[default].rlFlyDelay`, `rlFlyMs`, `rlEndPad` |
| 카드 등급·풀 | | `110_card.tier`, `.group`, `.active`; 뽑기 풀은 `111_cardPool` |

### 4.8.5 applyCard `L2311~2316`

```js
R.cards[c.id] = cardN(c.id) + 1;
if (c.id==='clone1') R.clones = Math.min(MAX_CLONES, R.clones+1);
if (c.id==='clone2') R.clones = Math.min(MAX_CLONES, R.clones+2);
rebuildUnits();
```
모든 효과는 사용처에서 `cardN(id)`를 읽는다. 분신만 즉시 `R.clones` 증가, 위성(orb)은 rebuildUnits로 3개 생성(그림).

---

## 4.9 카드 효과·신 효과 전부

### 4.9.1 카드 효과 표 (`112_cardEffect`)

| 카드 id | 획득 | type | 공식(n = 장수) | 근거 | p1 | p2 | p3 | p4 |
|---|---|---|---|---|---|---|---|---|
| atk | O t0 | DmgMul | baseDmg × (1 + 0.2n) | L1751 | 0.2 | | | |
| rate | O t0 | FireRateMul | iv 분모 × (1 + 0.2n) | L2074 | 0.2 | | | |
| clone1 | O t0 | CloneAdd | clones = min(4, clones + 1) | L2313 | 1 | | | |
| gold | O t0 | GoldMul | goldMul × (1 + 0.5n) | L1746 | 0.5 | | | |
| orb | O t1 | Orbit | 0.3초마다 범위 breakable에 baseDmg × 1.2n | L2140~2144 | 0.3 | 1.2 | 1.5 | 1.7 |
| proc | O t2 | ProcExtraHit(bolt)·ProcBurn(fire) | 명중 50%: bolt 추가타 / fire 화상 | L1986~1989 | 0.5 | 0.6 | 0.3 | 3 |
| chain | O t2 | ChainJump(bolt)·ChainSplash(fire) | bolt 연쇄 / fire 폭발 | L1990~2003 | 1 | 0.5 | 9 | 0.4 |
| storm | O t3 | Storm | 명중 25%, 쿨 2.6 | L2004 | 0.25 | 2.6 | 1.5 | 4 |
| blast | O t4 | Blast | 명중 10%+4%(n−1), 쿨 1.4 | L2005 | 0.1 | 0.04 | 1.4 | 4 |
| frate | O t4 | FireRateMul | iv 분모 × (1 + 0.5n) | L2074 | 0.5 | | | |
| spear | O t4 | PierceEveryN | 본체 2번째 볼리마다 창, × (1 + n), 관통 99 | L2083~2086 | 2 | 1 | 99 | |
| crit | X | CritChance | 0.15n | L2075 | 0.15 | | | |
| dbl | X | DoubleChance | min(1, 0.2n + …) | L2075 | 0.2 | | | |
| pierce | X | Pierce | 관통 +n | L2075 | 1 | | | |
| spin | X | Spin | 0.3초마다 baseDmg × 1.5n | L2146~2150 | 0.3 | 1.5 | 1.4 | 1.6 |
| beam | X | Beam | 5초마다 22m 빛, baseDmg × 3n | L2119~2125 | 5 | 3 | 22 | 0.7 |
| meteor | X | Meteor | max(2.5, 6 − (n−1))초, baseDmg × 5n | L2127~2133, L2210 | 6 | 1 | 2.5 | 5 |
| frost | X | ProcChill | `o.chill > 0`이면 × (1 + 0.25n) — chill 설정 코드 없음 | L1904 | | | 0.25 | |
| clone2 | X | CloneAdd | clones + 2 | L2314 | 2 | | | |

type 이름과 p1~p4 배치의 정본은 [07 §7.7.5](07_Tables.md)다. proc·chain은 07에서 `procBolt`/`procFire`, `chainBolt`/`chainFire` 두 행으로 나뉜다(위 proc·chain 줄의 p 값은 두 행을 합쳐 적은 것).

「획득 X」는 `CARDS`에 없어 프로토에서 절대 얻지 못한다. 처리 방침은 §4.16·결정 대기(D-20). 범위 상수:

| 효과 | 범위·주기 상수 | → `300_runConfig` |
|---|---|---|
| orb | 위성 3개, 반경 1.5, 각속도 4 rad/s(그림), 판정 `|dx| < hw + 1.5`, `z − hd < P.z + 1.7 && z + hd > P.z − 1.4` | `ORB_COUNT`=3, `ORB_RADIUS`=1.5, `ORB_ANG_SPEED`=4, `ORB_Z_BACK`=1.4 (앞 1.7, 가로 1.5는 p3·p4) |
| proc fire 화상 | `o.burn = {t:3, dps: dmg×0.3×pr, tick:0}` 덮어씀, 0.5초 틱마다 `dps×0.5` (= 원 피해 × 0.15 × pr), 3초 = 6틱, quiet, 색 `#ff9a4a` | `BURN_TICK`=0.5 (07 `procFire` 행: 배율 p2, 지속 p3) |
| chain fire | `|dx| < 2.4 + t.hw && |dz| < 2.4 + t.hd`, 원 대상 제외 hittable, 피해 dmg × 0.4 × ch, 폭발 그림 반경 1.6 + 0.3ch | `CHAIN_FIRE_BOX`=2.4 |
| storm | 지속 1.7초, 타격 k = 0.12 + 0.17·j (j=0~3) → 0.204/0.493/0.782/1.071초, 후보 `breakable && |t.z − cz| < 7 && t.z > P.z`, 피해 상자 1.3 | `STORM_LIFE`=1.7, `STORM_K0`=0.12, `STORM_DK`=0.17, `STORM_PICK_DZ`=7, `STORM_BOX`=1.3 |
| blast | 상자 3.6, 숫자 표시 | `BLAST_BOX`=3.6 |
| beam | `breakable && P.z < z < P.z + 22 && |dx| < hw + 0.7`, 색 `#ffe08a` | (p3, p4) |
| meteor | 대상 = breakable 중 `P.z+2 < z < P.z+20`에서 가장 가까운 것, 낙하 0.6초(게임), 착탄 상자 3.2. 대상이 없으면 타이머 유지 | `METEOR_MIN_DZ`=2, `METEOR_MAX_DZ`=20, `METEOR_FALL`=0.6, `METEOR_BOX`=3.2 |
| spin | 판정 `|dx| < hw + 1.4`, `[P.z − 1.4, P.z + 1.6]`, 각속도 5 | `BLADE_ANG_SPEED`=5 |

### 4.9.2 신의 고유 가호(명중 시) `L1982~2006`

```js
if (opts.proc && R){
  const el = R.el, …; const pr = cardN('proc'), ch = cardN('chain'), st = cardN('storm'), bl = cardN('blast');
  if (pr && Math.random() < 0.5){
    if (el==='bolt'){ bolt(…); if (!o.dead) hit(o, dmg*0.6*pr, false, {col:tcol, quiet:true}); }
    else { o.burn = {t:3, dps:dmg*0.3*pr, tick:0}; … }
  }
  if (ch){
    if (el==='bolt'){
      let from = V(hx, 1.2, hz); const used = new Set([o]);
      for (let i=0;i<1+ch;i++){
        let best = null, bd = 1e9;
        for (const t of R.objs){ if (used.has(t) || !hittable(t)) continue; const d = Math.hypot(t.x-hx, t.z-hz); if (d < 9 && d < bd){ bd = d; best = t; } }
        if (!best) break; used.add(best); … hit(best, dmg*0.5, false, {col:tcol, quiet:true}); from = to;
      }
    } else {
      boom(hx, 1.2, hz, col, 1.6 + 0.3*ch);
      for (const t of R.objs) if (t !== o && hittable(t) && Math.abs(t.x-hx) < 2.4 + t.hw && Math.abs(t.z - hz) < 2.4 + t.hd) hit(t, dmg*0.4*ch, false, {quiet:true});
    }
  }
  if (st && R.stormCd <= 0 && Math.random() < 0.25){ R.stormCd = 2.6; godStorm(hx, hz, st); }
  if (bl && R.blastCd <= 0 && Math.random() < 0.1 + 0.04*(bl-1)){ R.blastCd = 1.4; godBlast(hx, hz, bl); }
}
```
- `proc:true`는 **투사체 명중일 때만**. 가호에서 파생된 hit에는 proc가 없어 재귀 없음.
- `dmg`는 이미 반올림된 원 피해. `hx, hz` = 원 대상의 `(x, z − hd)`.
- bolt 연쇄: `1 + ch`회, 매회 **최초 명중점(hx,hz)** 기준 대상 중심까지 거리 < 9인 가장 가까운 미사용 hittable(게이트 포함 → 게이트도 충전된다). 피해 dmg × 0.5(ch 배율 없음).
- 쿨다운 `R.stormCd`, `R.blastCd`는 **updRun에서만** `−= dt`(계단 중 멈춤) `L2135`.
- 신별 차이는 `el`(bolt/fire) 하나뿐이다. → `113_god.element`. 같은 카드 id가 신마다 이름·설명만 다르다 → `111_cardPool`.

### 4.9.3 godStorm `L2157~2174`

```js
addFx(cloud, 1.7, (f,dt,k)=>{
  const due = Math.min(4, Math.floor((k - 0.12)/0.17) + 1);
  while (done < due && R){ done++;
    const cands = R.objs.filter(t=>breakable(t) && Math.abs(t.z - cz) < 7 && t.z > R.p.z);
    const t = cands.length ? cands[Math.floor(Math.random()*cands.length)] : null;
    const x = t ? t.x + rnd(-.3,.3) : cx + rnd(-2,2), z = t ? t.z - t.hd : cz + rnd(-2,2);
    …
    for (const u of R.objs) if (breakable(u) && Math.abs(u.x-x) < 1.3+u.hw && Math.abs(u.z-z) < 1.3+u.hd) hit(u, baseDmg()*1.5*n, false, {col:tcol, quiet:true});
  }
});
```
- **피해가 FX 타이머(stepFx, sdt) 안에서 난다.** `MV.Run`은 이것을 `StormJob{cx, cz, n, t, done}`으로 들고 매 프레임 sdt로 진행한다. stepFx는 모든 모드에서 돌므로 Job도 run·cannon·result에서 진행한다(R이 있을 때).
- 피해는 착탄 즉시(fireball 낙하 0.22초를 기다리지 않음).
- 난수(게임 규칙): 후보 인덱스 1회(후보 있을 때) → x 흔들림 1회 → (후보 없으면) z 흔들림 1회. 흔들림이 피해 상자를 움직이므로 **RunRng**다.
- 피해 `baseDmg()`는 **타격 시점** 값.

### 4.9.4 godBlast `L2175~2182`

```js
… boom; shake(0.45, 0.3); 화면 플래시 0.35 → 60ms 뒤 0; pop(el==='fire' ? '햇살 폭발!' : '하악!');
for (const u of R.objs) if (breakable(u) && Math.abs(u.x-x) < 3.6+u.hw && Math.abs(u.z-z) < 3.6+u.hd) hit(u, baseDmg()*4*n, false, {col: …});
```
즉시 피해, 숫자 표시. fire의 fireball 3개 위치 난수는 그림(FxRng). → `300_runConfig.shake.godBlast.amp`=0.45 / `.time`=0.3.

### 4.9.5 쿨다운·주기 요약

| 효과 | 주기/쿨 | 감소 시간축 | 계단 중 |
|---|---|---|---|
| storm | 2.6초(발동 시) | updRun dt | 멈춤 |
| blast | 1.4초 | updRun dt | 멈춤 |
| orb / spin | 0.3초 틱(`R.spinT` **공유**) | updRun dt | 멈춤 |
| 화상 | 0.5초 틱, 3초 | updRun dt | 멈춤 |
| beam | 5초 | updRun dt | 멈춤 |
| meteor | max(2.5, 6 − (n−1))초, 대상 생길 때까지 유지 | updRun dt; 착탄은 FX 0.6초(sdt) | 타이머 멈춤, 낙하 진행 |

- orb와 spin이 `R.spinT`/`R.spinA`를 공유해 둘 다 있으면 spin이 사실상 안 나간다. spin을 살리면 타이머를 분리한다(D-20).

---

## 4.10 HAND(손패 3장 머지) 규칙

**논리와 표시가 분리돼 있다.** 논리 `R.w[1..8]`은 §4.4.3 addWeapon에서 즉시 머지되고 피해(`eqTier`)에 즉시 반영된다. HAND는 그 결과를 시간차로 보여 주는 `MV.UI` 위젯이다(`MV.Run`은 `WeaponDeliver{n, tier}` 이벤트만 낸다).

| 함수 | 규칙 | 근거 |
|---|---|---|
| handReset | 런 시작 시 `R.w[t]`를 t=8→1 순으로 카드 생성 | L1159 |
| handDeliver(n, t) | 큐에 `{deal, n, t}` 다음 `{cascade}` | L1160 |
| handSort | 등급 내림차순, 같은 등급은 id 오름차순(먼저 받은 것이 위) | L1158 |
| handTier | 손패 최고 등급(비면 1) — **고양이 외형·`#wDmg` 문구·「변신!」 팝업은 이 값** | L1161 |
| handWork | 손패 + 큐 deal을 합산, k=1..7에서 `m = floor(c[k]/3)`, `c[k+1] += m`, `c[k] −= 3m` → {deals, merges} | L1163~1169 |
| handUnit | `left = max(0.4, 3 − (clock − t0))`, `U = merges + deals×0.12`, `u = clamp(left/U, 0.07, 0.54)` (U=0이면 0.54) | L1170~1173 |
| stepHand(dt) | **실시간 dt**. 대기 중이면 `t0 = clock`. deal: 1장 추가 후 `busy = min(0.05, u×0.12)`. cascade: 3장 이상인 **가장 낮은 등급**의 앞 3장을 가운데 카드로 모음 `md = u×0.92`, 끝나면 3장 제거·다음 등급 1장 추가·merge 소리·`busy = min(0.04, u×0.08)`. 더 없으면 큐에서 제거 | L1174~1197 |
| 등급 상승 표시 | 장착 카드 등급이 오르면 런 중 `pop("변신! ${LOOKS[t-1]}", WGRADE[t-1], 1.15)`, 불꽃, lvup 소리 | L1218~1221 |

| 상수 | 값 | → `300_runConfig` |
|---|---|---|
| 예산 3초, 최소 남은 시간 0.4 | 3 / 0.4 | `ui.hand.budget`, `ui.hand.leftMin` |
| 전달 1장 = 0.12단위 | 0.12 | `ui.hand.dealCost` |
| u 범위 0.07~0.54 | | `ui.hand.unitMin`, `ui.hand.unitMax` |
| 전달 간격 상한 0.05 | | `ui.hand.dealGapMax`, `ui.hand.dealGapMul` |
| 머지 시간 u×0.92, 후속 min(0.04, u×0.08) | | `ui.hand.mergeMul`, `ui.hand.afterMax`, `ui.hand.afterMul` |
| 3장 → 1장 | 3 | `MERGE_N`(그룹 104, 논리 머지 3과 같은 값; `addWeapon`·`deckAdd`도 이 값) |

배치·CSS 수치(카드 한 변 `min(박스폭, W×0.105)`, 최대 높이 화면 34% 등)는 [05](05_Screens.md). 표시 지연을 유지할지는 결정 대기(D-28).

---

## 4.11 종료·정산·Area 개방·Zone 이동

### 4.11.1 finish(reason) `L2394~2418`

```js
if (R.fin) return; R.fin = true; mode = 'result'; R.reason = reason;
… (§4.7.4 rec·cpRun)
const have = save.souls + R.souls, need = AREA_NEED[save.area], last = save.area >= PROTO_AREAS - 1;
R.zoneEnd = reason === 'gate' && have >= need && last;
R.pass = reason === 'gate' && have >= need && !last;
R.tok = 1 + Math.floor(R.souls/4) + (reason === 'gate' ? 2 : 0) + (R.pass || R.zoneEnd ? 3 : 0);
setTimeout(()=>SND.play(R && (R.pass || R.zoneEnd || R.reason === 'gate') ? 'win' : 'fail'), reason === 'gate' ? 0 : 350);
… $('#x3Btn').disabled = !(R.gold >= 1 || R.dia > 0);
```

| 항목 | 규칙 | → 표.열 |
|---|---|---|
| 종료 사유 | `gate`(Area 돌파 아치 통과), `wall`(장애물 막힘). `cell`/`area`는 도달 불가 | — |
| 개방 조건 | `gate` && 누적 생선(`save.souls + R.souls`) ≥ `AREA_NEED[area]` | `302_area.needSouls` |
| 마지막 Area | `area ≥ PROTO_AREAS − 1`(=4, Area 5) → `zoneEnd`(더 진행 없음) | `300_runConfig.PLAYABLE_AREAS`=5 — 결정 대기(D-26) |
| 목숨 조각 | `1 + floor(R.souls/4) + (gate ? 2 : 0) + (개방 ? 3 : 0)` | `300_runConfig.TOK_BASE`=1, `TOK_SOUL_DIV`=4, `TOK_GATE`=2, `TOK_OPEN`=3 |
| 결과 소리 | gate면 즉시 win, 아니면 350ms 뒤 fail | `300_runConfig.FAIL_SFX_DELAY_MS`=350 |
| ×3 버튼 활성 | `R.gold ≥ 1 || R.dia > 0` | — |
| 결과 제목 | zoneEnd `Zone 1 클리어!` / pass `Area ${area+2} 개방!` / gate `LEVEL COMPLETED!` / 실패 `아지트로 귀환` | 문구 키 [07 §7.12](07_Tables.md) |

`MV.Run`은 `RunResult{reason, gold(double), souls, dia, tok, pass, zoneEnd, cpRun, barRec, climbed, walls, gaugeLv, eqTier, clones, cardCount, hitWallBar}`를 만들고 끝난다. 영속 반영은 `MV.Meta.Collect`만 한다.

### 4.11.2 collect(mul) `L2424~2443` — MV.Meta

```js
save.gold += Math.floor(R.gold*mul); save.souls += R.souls; save.dia = (save.dia||0) + R.dia*mul; save.runs++;
save.rb.pending += R.tok;
save.level++;
save.bars = {}; save.cp = Math.max(save.cp || 0, R.cpRun || 0);
if (pass){ save.cp = 0; save.area++; save.zone = zoneOf(save.area); if (save.area >= 1 && !save.rb.open){ save.rb.open = true; unlockRb = true; }
  save.up = {stair:0, weapon:0, income:0}; save.deck = DEF().deck; save.aup = DEF().aup; }
else if (zoneEnd){ save.zoneClear = true; save.bars[barKey()] = R.barRec; }
else save.bars[barKey()] = R.barRec;
const interstitial = mul === 1 && save.area >= 3;
```

| 항목 | 규칙 | → 표.열 |
|---|---|---|
| 정산 지급 | RunGold(602) → Wealth 1 `floor(gold × mul)`, RunSouls(603) → Wealth 3, RunDia(604) → Wealth 2 `× mul`, 조각 → `rb.pending`(Wealth 4가 아니라 대기분) | `07_consumeType` 600번대 → `01_wealth` 대응 [가정] |
| ×3 | 츄르·캣닢만 ×mul. 생선·조각·Level은 배수 없음. **일일 한도 없음** | `00_config.collectAdMul`=3, `08_adPlacement[collect3].dailyLimit`=0(무제한) |
| Level | 성공·실패 무관 +1 | — |
| 개방(pass) | cp 0, area+1, zone = `302_area.zoneId`, 부활 최초 해금(`rb.open`), **로비 강화 3종·시작 덱·Area 강화 초기화**, 재화 유지, bars 비움 | `05_contentOpen[rebirth]` conditionType 1(Area 도달), conditionValue 2, sticky 1 |
| Area 첫 개방 보상 | 프로토에 없음 | `302_area.clearReward*` 열은 비워 둔다(08이 쓰면 pass 때 지급) |
| 그 외 | 이번 Area 장애물 기록 저장 | — |
| 강제 전면 광고 | `mul == 1 && area ≥ 3`(pass 반영 **후** area) → 전면 광고 후 로비 | `00_config.interstitialMinArea`=3, `08_adPlacement[interstitial]` — 결정 대기(D-24) |
| 로비 배너 | pass: Zone이 바뀌면 `Zone ${zone+1} · ${이름}` / `Area ${area+1} 시작`, 아니면 `Area ${area+1}` / `${Area 이름} · 강화 초기화`. zoneEnd: `Zone 1 클리어` / `Zone 2는 업데이트 예정` | 문구 키 |
| 부활 해금 안내 | 900ms 뒤 신기능 팝업(아홉 목숨) | `05_contentOpen[rebirth]` |

### 4.11.3 Area·Zone 공식

| 공식 | 원문 | → 표.열 |
|---|---|---|
| Zone | `zoneOf = a => Math.floor(a/5)` `L758` | `302_area.zoneId` (표가 정본, 공식은 검증기) |
| 런 Area 정보 | `curZ()` = `{hp: AREA_HP[area], zi, th: AREA_TH[area % 5]}` `L760` | `302_area.hp`, `.themeId` |
| 필드 데이터 | `FINAL[Math.min(A, 14)][f]` `L1767` | `302_area.floorGroup` → `303_areaFloor` (정본 재현이면 Area 16~30은 floorGroup 15, 08 권장은 areaNo — D-71) |
| 개방 요구 | `AREA_NEED[area]` = 이 Area에서 **다음 Area**를 열 누적 생선 | `302_area.needSouls` |

---

## 4.12 메타 공식(MV.Meta)

### 4.12.1 로비 강화 3종 `L809~829`, `L2641~2650`

```js
const KEG_STEP = lv => 4 + 3*lv;  const LOBBY_CANNON_MAX = 4;
function kegStart(P){ let lv = 1, p = P; while (lv < LOBBY_CANNON_MAX && p >= KEG_STEP(lv)){ p -= KEG_STEP(lv); lv++; } return {lv, step:p, need:KEG_STEP(lv)}; }
const WLV_NEED = lv => Math.round(8*Math.pow(1.15, lv-1));
function wLevel(P){ let lv = 1, p = P; while (lv < 8 && p >= WLV_NEED(lv)){ p -= WLV_NEED(lv); lv++; } return {lv, step:p, need:WLV_NEED(lv)}; }
const W_MAXP = [1,2,3,4,5,6,7].reduce((a,l)=>a+WLV_NEED(l), 0) + 60;
function deckAdd(d, t){ d[t]++; let merged = 0; while (t < 8 && d[t] >= 3){ d[t] -= 3; t++; d[t]++; merged = t; } return merged; }
stair:  cost:P=>Math.round(25*Math.pow(1.18,P)), max:KEG_MAXP(=30)
weapon: cost:P=>Math.round(30*Math.pow(1.043,P)), max:W_MAXP(=149)
income: cost:P=>20+8*(P+1), max:50
```

| 키 | 효과 | 비용(츄르 `save.gold`) | max | → 표.열 |
|---|---|---|---|---|
| stair 캣타워 | 구매 수 P → `kegStart(P)` = 런 시작 게이지 Lv(1~4)와 진행률 | round(25 × 1.18^P) | 30 (= 7+10+13) | `10_lobbyUpgrade[stair]` max 30, levelPriceGroup 1; 단계 필요 `11_stairLevel.need` = 7/10/13/16; `300_runConfig.LOBBY_STAIR_MAX_LV`=4 |
| weapon 스크래쳐 | 구매 시 **현재 무기 Level 등급 카드 1장을 시작 덱에 추가**(`deckAdd`, 3장 머지). 무기 Level `wLevel(P).lv` = 런 `R.bt`(공방 제작 등급) | round(30 × 1.043^P) | 149 (= 89 + 60) | `10_lobbyUpgrade[weapon]` max 149, levelPriceGroup 2; `12_weaponLevel.need` = 8/9/11/12/14/16/19/21 (Lv 8 뒤 추가 구매 60회는 max에 포함) |
| income 생선 건조대 | goldMul × (1 + 0.05P) | 20 + 8(P+1) | 50 | `10_lobbyUpgrade[income]` max 50, levelPriceGroup 3; 효과 `300_runConfig.INCOME_PER_LV`=0.05 |

**`16_levelPrice` 행 생성 규칙**(런타임은 행만 읽는다. P = 현재 구매 수, `targetLevel = P + 1`, `priceType 1`(Wealth), `priceId 1`(츄르)):

| group | 대상 | targetLevel 범위 | priceValue | 검증 예(P=0,1,2,10) |
|---|---|---|---|---|
| 1 | 캣타워 | 1~30 | `round(25 × 1.18^(targetLevel−1))` | 25, 30, 35, 131 |
| 2 | 스크래쳐 | 1~149 | `round(30 × 1.043^(targetLevel−1))` | 30, 31, 33, 46 |
| 3 | 생선 건조대 | 1~50 | `20 + 8 × targetLevel` | 28, 36, 44, 108 |

`round`는 JS 반올림(§4.0.2). 행 생성 도구는 MV-DATA 작업이 만들고, 검증기가 `10_lobbyUpgrade.max` = 그 group 행 수를 확인한다.

구매 절차(원문 순서 그대로): `gold ≥ c && !max` 확인 → `before`(stair: kegStart(P).lv, weapon: baseTier(P)) → weapon이면 `deckAdd(save.deck, before)` → `gold −= c; up[k]++` → 저장 → (Lv 상승 시 배너 `캣타워 Lv N`/`계단 게이트 1칸당 계단 +N` 또는 `무기 Level N`/`이제 ${무기} 카드 · 장난감 공방도 ${무기}`). 연출 순서는 [05](05_Screens.md).

- 덱 `save.deck`은 길이 9 배열(인덱스 1~8), 기본 털뭉치 1장. 런 시작 시 `R.w = deck.slice()`.

### 4.12.2 Area 강화 5종(장난감 바구니) `L2515~2532`

```js
const AUP = [{k:'rate', base:40, max:10}, {k:'range', base:40, max:10}, {k:'clone', base:100, max:4}, {k:'crit', base:80, max:10}, {k:'dbl', base:80, max:10}];
const aupCost = (a, l) => Math.round(a.base * Math.pow(1.6, l));
```

| 키 | 효과(단계당) | 사용처 | 비용(캣닢) | max | → `13_areaUpgrade` |
|---|---|---|---|---|---|
| rate | 발사 간격 분모 × (1 + 0.1L) | L2074 | round(40 × 1.6^L) | 10 | perLv 0.1, max 10, levelPriceGroup 11 |
| range | 사거리 × (1 + 0.1L) | L2094 | round(40 × 1.6^L) | 10 | perLv 0.1, levelPriceGroup 12 |
| clone | 시작 분신 min(4, L) | L1818 | round(100 × 1.6^L) | 4 | perLv 1, levelPriceGroup 13 |
| crit | 치명 확률 + 0.1L (**실제 동작**) | L2075 | round(80 × 1.6^L) | 10 | perLv 0.1, levelPriceGroup 14 |
| dbl | 2연발 확률 + 0.1L (**실제 동작**) | L2075 | round(80 × 1.6^L) | 10 | perLv 0.1, levelPriceGroup 15 |

`16_levelPrice` 행 생성 규칙: group 11~15, targetLevel = L+1 (1~max), `priceType 1`, `priceId 2`(캣닢), `priceValue = round(base × 1.6^(targetLevel−1))`, base = 40/40/100/80/80. 검증 예: rate 40, 64, 102, 164; clone 100, 160, 256, 410.

- Area 개방 시 전부 0으로 초기화(§4.11.2). 구매 실패 문구 없음(버튼 비활성).

### 4.12.3 부활 4종(아홉 목숨) `L2533~2576`

```js
const RBP = [{k:'power', base:50, max:20}, {k:'crit', base:50, max:20}, {k:'obst', base:60, max:10}, {k:'dcap', base:60, max:20}];
const rbCost = (p, l) => Math.round(p.base * Math.pow(1.5, l));
const RB_CD = 6*3600*1000;
const rbOpen = () => !!(save.rb.open || save.area >= 1);
function rbLeft(){ return Math.max(0, (save.rb.cdUntil||0) - Date.now()); }
// doRebirth
keep = {level:save.level, dia:save.dia, god:save.god, rb, ads:save.ads, runs:save.runs};
rb.tokens += rb.pending; got = rb.pending; rb.pending = 0; rb.lv++; rb.cdUntil = Date.now() + RB_CD;
save = Object.assign(DEF(), keep);
```

| 퍽 | 효과 | 비용(목숨 조각 `rb.tokens`) | max | → `14_rebirthPerk` |
|---|---|---|---|---|
| power | baseDmg × (1 + 0.15L) | round(50 × 1.5^L) | 20 | base 1, perLv 0.15, max 20, levelPriceGroup 21 |
| crit | critMul = 2.2 + 0.2L | round(50 × 1.5^L) | 20 | base 2.2, perLv 0.2, levelPriceGroup 22 |
| obst | 장애물 max HP × max(0.5, 1 − 0.05L) | round(60 × 1.5^L) | 10 | base 1, perLv −0.05, floor 0.5, levelPriceGroup 23 |
| dcap | diaCap = 10 + 2L (캣닢 블록·화분·쥐 캣닢) | round(60 × 1.5^L) | 20 | base 10, perLv 2, levelPriceGroup 24 |

`16_levelPrice` 행 생성 규칙: group 21~24, targetLevel = L+1, `priceType 1`, `priceId 4`(목숨 조각), `priceValue = round(base × 1.5^(targetLevel−1))`, base = 50/50/60/60. 검증 예: power 50, 75, 113, 169.

| 규칙 | 값 | → 표.열 |
|---|---|---|
| 해금 | `rb.open || area ≥ 1`(Area 2 진입) | `05_contentOpen[rebirth]` conditionType 1(Area 도달), conditionValue 2, sticky 1 |
| 부활 조건 | 해금 && 쿨 없음 && **2.5초 안에 두 번 누름** | `00_config.rebirthArmSec`=2.5 |
| 결과 | `tokens += pending`, `pending = 0`, `lv++`, `cdUntil = now + 6h`. **유지**: level, 캣닢, 신, rb 전체(open 포함), 광고 횟수, runs. 나머지 DEF | `00_config.rebirthCooldownSec`=21600 — Level 유지는 결정 대기(D-25) |
| 쿨 스킵 광고 | `cdUntil −= 70분`, `ads.skip−−`, 쿨이 남아 있을 때만 | `08_adPlacement[skip]` dailyLimit 4, 보상 아래 표 |
| 조각 적립 | 결과마다 `pending += R.tok`(×3 무관) | §4.11 |

### 4.12.4 광고 한도 `L2423`, `L2574~2575`

```js
function adDay(){ const d = new Date().toISOString().slice(0,10); if (save.ads.day !== d){ save.ads = {day:d, dia:3, skip:4}; } }
```

| placement | 한도/일 | 보상 | 호출 | → `08_adPlacement` |
|---|---|---|---|---|
| dia (바구니 무료) | 3 | 캣닢 +100 | `#aupFree` | dailyLimit 3, adRewardType 1, adRewardId 2, adRewardValue 100 |
| skip (부활 쿨 스킵) | 4 | 쿨 −70분 | `#rbSkipAd` | dailyLimit 4, adRewardType 609 RebirthCooldownSec, adRewardValue 4200 |
| collect3 (결과 ×3) | 무제한 | 츄르·캣닢 ×3 | `#x3Btn` | dailyLimit 0, 보상 없음(배수는 `00_config.collectAdMul`=3) |
| interstitial (전면) | 무제한 | — | collect | dailyLimit 0, 조건 `00_config.interstitialMinArea`=3 |

- 날짜 기준은 **UTC 날짜**(`toISOString`) → 한국 시간 09:00에 초기화. `00_config.adDayUtcOffsetHour`=0. 결정 대기(D-29).
- `adDay()`는 바구니 패널을 그릴 때·광고 버튼을 누를 때만 호출된다. Unity는 `MV.Meta.AdsToday()` 조회 시마다 검사한다(결과 같음).
- 광고 연결은 `AdManager.ShowRewardAd(new AdRequest(placement), cb)`. 개발 빌드 대체 3초 가짜 광고는 [10](10_Architecture.md).

### 4.12.5 신 선택 `L2513`

`save.god = gpSel` (비용 없음). 런 시작 시 `CARDS = 공용 4장 + 신 고유 7장`(`setGodCards`), `R.el = GODS[god].el`. 열린 신은 thunder 하나(GOD_MENU에 id가 있는 항목만). → `113_god.unlockType`·`openId`, `.active`. 10종 확장은 [08](08_Content.md).

---

## 4.13 상태 기계·시간

### 4.13.1 상태

| Unity `RunPhase` | 프로토 `mode` | 매 프레임 | 진입 | 근거 |
|---|---|---|---|---|
| Lobby | `lobby` | 로비 애니만 | 시작, `toLobby()` | L1311, L2472 |
| Run | `run` | `updRun(gdt)` × 배속 | `startRun()`, `endClimb()` | L2661, L2293 |
| Climb | `cannon` | `updClimb(sdt)` | 계단 도달 && `R.stairs > 0` | L2246 |
| Result | `result` | (룰렛만) | `finish()` | L2395 |
| (겹침) Roulette | `RL != null` | `updRoulette(sdt)` — Run/Climb/Result 모두 | `endClimb()` → tiers 있음 | L3831 |

| from | 조건 | to |
|---|---|---|
| Lobby | 게임 영역 터치 또는 Space/Enter → `newRun()` | Run |
| Run | 계단 z 도달 && 계단 > 0 | Climb |
| Run | 계단 z 도달 && 계단 = 0 | Run(토스트, 그 계단은 소모) |
| Climb | `t ≥ T` → `endClimb()` | Run (+ Roulette) |
| Run | 이동 전 `P.z > gz` | Result(`gate`) |
| Run | 벽 앞면 접촉 | Result(`wall`) |
| Result | 「그냥 받기」 → `collect(1)` (조건부 전면 광고) | Lobby |
| Result | 「광고 보고 ×3」 → 보상 광고 → `collect(3)` | Lobby |

`toLobby()`는 룰렛 종료, CN = null, 런 오브젝트 정리 후 로비 배경(`makeObjs(tplFor(level))` 중 z < 70)을 깐다 `L2472~2479`. 이 배경 생성은 **FxRng**를 쓴다(RunRng 순서에 끼지 않게).

### 4.13.2 시간 `L3823~3835`

```js
const dt = Math.min(0.05, (now-last)/1000); last = now;
const hs = hitStop > 0 && mode==='run'; if (hitStop > 0) hitStop -= dt;
const gdt = hs ? dt*0.12 : dt, sdt = gdt * DBG.ts;
if (mode==='run'){ for (let k=0;k<DBG.ts;k++) if (mode==='run') updRun(gdt); }
else if (mode==='cannon') updClimb(sdt);
if (RL && mode !== 'lobby') updRoulette(sdt);
if (R && mode !== 'lobby') stepHand(dt);
if (keys.l || keys.r){ … dt*9 … }
stepFx(sdt); syncScene(dt); stepPops(dt);
```

| 시계 | 값 | 쓰는 것 | → 표.열 |
|---|---|---|---|
| `dt` 실시간 | min(0.05, 프레임 간격) | 키 이동, 히트스톱 감소, 토스트, HAND, 카메라, 팝업, **우물 묶음 0.22·벽돌 재생** | `300_runConfig.time.dtCap`=0.05 |
| `gdt` 게임 | 히트스톱 중(run만) dt × 0.12 | updRun(배속만큼 반복 호출) | `300_runConfig.time.hitStopScale`=0.12 |
| `sdt` 게임×배속 | gdt × ts | updClimb, updRoulette, stepFx(**폭풍 타격·유성 착탄·벽돌 튐 0.26**) | `DBG.ts` 기본 1 (디버그 ×1/×2/×3) |
| setTimeout 실시간 ms | — | flyTo 560(계단·게이지 수령), flyCard 620(카드 적용), 공방 아이콘 70 간격, 광고 3초, 배너 1.5초, 실패음 350, 해금 안내 900 | 각 절 |

- 히트스톱: `hitstop(t) = hitStop = max(hitStop, t)` (누적 아님). 흔들림: `shake(a,t) = shakeA = max(shakeA, a); shakeT = max(shakeT, t)` `L1603~1604`. 흔들림 적용은 View([06](06_Effects.md)).
- Unity 계약(결정 대기 D-22): `RunSim.Tick(float realDt, RunInput input)` 하나로 받는다. 안에서 `dt = min(0.05, realDt)`, gdt, sdt를 위 규칙대로 만들고, 프로토의 setTimeout 지연은 **실시간 dt로 줄어드는 내부 타이머**로 대체한다(프레임 상한 0.05가 걸리는 점만 다름, 결정성을 위해 의도). 배속 `ts`는 디버그 전용 `RunSetup.timeScale`.

### 4.13.3 updRun 한 프레임 순서 `L2023~2151` (구현 순서 고정)

1. `R.time += dt`; x 추종·clamp(§4.1).
2. 아치 통과 배너(§4.7.3) → `P.z > gz`면 `finish('gate')` 후 종료.
3. `nz = P.z + 4dt`. 계단 진입 검사(§4.8.1) — 성공 시 종료.
4. 벽 막힘 검사, `oz = P.z`, `P.z = max(P.z, nz)`, phase.
5. 게이트 통과(§4.4.2) — `R.objs` 순서대로.
6. `pendingW` 진행·무기 지급(§4.4.3).
7. 쥐 AI·훔침·이탈(§4.6).
8. `sink` 진행(그림).
9. 막혔으면 `finish('wall')` 후 종료.
10. 사격(§4.2) — 유닛 순서 본체 → 분신 1~4.
11. 투사체 이동·명중(§4.2.3) — `R.projs` 생성 순서대로, 각 명중은 hit() 안에서 가호까지 끝냄.
12. 죽은 투사체 제거.
13. beam → meteor 타이머(잔재).
14. `stormCd −= dt; blastCd −= dt`; 화상 틱.
15. orb → spin 틱.

---

## 4.14 레벨 생성(tplFor → makeObjs → fieldSpec → buildLevel → newRun)

### 4.14.1 tplFor `L857`

```js
const tplFor = lv => lv<=8 ? lv : 3 + ((lv-9) % 6);
```
→ `312_templateCycle` 2행: `{levelFrom 1, levelTo 8, templates "1/2/3/4/5/6/7/8", mode Direct}`, `{levelFrom 9, levelTo (빈칸=끝없음), templates "3/4/5/6/7/8", mode Loop}`.

```text
TplFor(level):
  row = 312_templateCycle에서 levelFrom ≤ level ≤ levelTo(빈칸=∞)인 첫 행
  i = level - row.levelFrom
  Direct: return row.templates[i]          // 범위 밖이면 검증기 오류
  Loop:   return row.templates[i % row.templates.Count]
```
검증: Level 1~8 → 1~8, 9 → 3, 14 → 8, 15 → 3.

### 4.14.2 makeObjs `L1314~1331`

```text
MakeObjs(tpl, area, baseZ, rng):            // 쓰는 표: 311_templateObject, 105_objectKind, 314_spawnGroup, 114_hpRule, 102_enemy, 109_moveAI, 801_drop
  out = []
  for row in 311_templateObject[tpl] (seq 오름차순):
    kind = 105_objectKind[row.kindId]; z = row.z + baseZ
    if row.groupId 비어 있지 않음:            // 쥐 무리(proto type 5)
      for g in 314_spawnGroup[row.groupId] (idx 오름차순):
        x = Clamp(row.x + g.dx, -3, 3)        // 300_runConfig.ENEMY_X_MAX = 3
        o = New(kind, x, z + g.dz)            // dz에 +6이 이미 들어 있음(아래)
        o.spd = rng.Range(109_moveAI[kind.moveAiId].speedMin, speedMax)   // RunRng 1회
        o.ph  = fxRng.Range(0, 6)             // 그림 위상, FxRng
        out.Add(o)
    else:
      out.Add(New(kind, (kind.id=="stair" ? 0 : row.x), z))
  return out
New(kind,x,z): hw/hd = kind.hw/hd, hp = HpRule(kind.hpRuleId, area) (없으면 무한), reward = ConsumeData(kind 행의 rewardType/rewardId/rewardValue + rewardValuePerZone × zone)
```

| 원문 | 값 | → 표.열 |
|---|---|---|
| tower `{hw:1.25, hd:1.1, pend:0, ft:0}` | | `105_objectKind[tower]` |
| enemy 오프셋 `[-1.6,0],[0,0.9],[1.6,0],[-0.8,2.2],[0.8,2.2]`, z +6 | dz 저장값 6, 6.9, 6, 8.2, 8.2 | `314_spawnGroup[mouse5]` idx 0~4 (dz에 +6을 합쳐 저장) [창작 정리] |
| enemy `hp = round(Z.hp*0.4)`, `reward 6+4*zone`, `spd rnd(1.1,1.6)` | | `102_enemy[mouse]`(hpRuleId → `114_hpRule[enemy04]`, 보상 consumeData, moveAiId → `109_moveAI[mouse]`) |
| sgate/cgate `{hw:1.25, hd:0.12, F:0, n/made:0, done:false, sink:0}` | | `105_objectKind[sgate/cgate]` |
| stair `{x:0, hw:1.6, hd:1.0, used:false}` | x 강제 0 | `105_objectKind[stair]` |
| coin/gem `{hw:0.9, hd:0.7, hp:round(Z.hp*0.6), reward:20+10*zone, dia: gem ? diaCap() : 0}` | | `105_objectKind[coin/gem]`(hpRuleId → `114_hpRule[box06]`, 보상 consumeData §4.3) |
| `PLACE` 84행 `[템플릿, type, x, z]` | | `311_templateObject` (type → kindId: 7 tower, 5 enemy+groupId mouse5, 14 sgate, 13 cgate, 15 stair, 2 coin, 3 gem) |
| `TPL_END = [0,189,203,224,224,231,224,231,238]` | | `310_template.endZ` |

- PLACE 전체 값은 [07](07_Tables.md) `311_templateObject` 초기 행(B 노트 §1.1 원문 그대로). 모든 템플릿은 stair 2개, 쥐 무리 1개(x 0, z 57.42).
- 죽은 분기 `if (false)`(쥐 대신 coin 4개)는 옮기지 않는다(§4.16).

### 4.14.3 hash·fieldSpec `L858`, `L1766~1779`

```js
const hash = n => { const x = Math.sin(n*127.1 + 311.7)*43758.5453; return x - Math.floor(x); };
function fieldSpec(A, f){
  const [h0, h1, rows, souls] = FINAL[Math.min(A, FINAL.length-1)][f], n = rows*3, zi = zoneOf(A);
  const sN = Math.max(1, Math.ceil(rows/3)), seed = (A+1)*97 + f*131 + 7, soulIdx = [];
  for (let k=0; soulIdx.length < sN && k < 400; k++){ const i = 3 + Math.floor(hash(seed + k*7.31)*(n-3)); if (!soulIdx.includes(i)) soulIdx.push(i); }
  soulIdx.sort((a,b)=>a-b);
  const base = Math.floor(souls/sN), extra = souls - base*sN, out = [];
  for (let idx=0; idx<n; idx++){
    const r = Math.floor(idx/3), l = idx % 3, hp = Math.round((h0 + (h1-h0)*r/Math.max(1, rows-1))*BAL.scale);
    let rew = 'gold', amt = Math.round((10 + 6*zi)*(1 + 0.2*r)); const si = soulIdx.indexOf(idx);
    if (si >= 0){ rew = 'soul'; amt = base + (si >= sN-extra ? 1 : 0); } else if (hash(seed + idx*3.7 + 11) < 0.14){ rew = 'dia'; amt = diaCap(); }
    out.push({idx, r, l, hp, rew, amt});
  }
  return out;
}
```

C# 구현 규칙:
```csharp
static double Hash(double n) {            // double 필수. float Mathf.Sin 금지
    double a = n * 127.1;                 // 곱·합을 나눠 써서 FMA 융합을 막는다(IL2CPP ARM64)
    double b = a + 311.7;
    double x = JsMath.Sin(b) * 43758.5453; // Math.Sin 아님(아래)
    return x - Math.Floor(x);
}
```

`Math.Sin`은 쓰지 않는다. 정본 V8의 sin은 올바르게 반올림된 값인데, Windows CRT sin은 골든 4619개 중 118개가 마지막 비트에서 달랐다(칸 결과 변화 0). 그래서 `MV.Run/Gen/JsMath.Sin`(FMA 없는 double-double, 올바른 반올림)을 쓴다. 실기 대조는 하지 않는다(D-27 닫음, 사용자 방침). Baked(303 `cellMode`)는 필요해지면 쓰는 대체 경로로 남긴다.

| 원문 상수 | 값 | → `313_fieldGen[default]` 열 |
|---|---|---|
| 레인 3 | 3 | `lanes` |
| 생선 블록 수 max(1, ceil(rows/3)) | 3 | `soulPerRows` |
| 시드 (A+1)×97 + f×131 + 7 | 97 / 131 / 7 | `seedA`, `seedF`, `seedC` |
| 생선 해시 간격 7.31, 시도 400, 첫 줄 제외 3 | | `soulHashStep`, `soulTryMax`, `soulMinIdx` |
| 캣닢 해시 3.7·+11, 확률 0.14 | | `diaHashStep`, `diaHashAdd`, `diaChance` |
| 츄르 (10 + 6zi)(1 + 0.2r) | | `goldBase`, `goldPerZone`, `goldPerRow` |
| h0, h1, rows, souls | FINAL | `303_areaFloor.h0 / h1 / rows / souls` |
| hash 상수 127.1, 311.7, 43758.5453 | | 코드 상수(표에 두지 않음 — 바꾸면 배치가 전부 바뀜) |

- **seed는 클램프 전 A를 쓴다.** Area 16~30은 FINAL 15번 행 값을 쓰지만 생선·캣닢 칸 위치는 Area마다 다르다.
- HP는 `BAL.scale`에 비례, 위치는 BAL과 무관. 실제 블록 max = `max(1, round(hp × obstMul()))`(buildLevel).

**검증 표(골든)** — 프로토 공식을 node로 재현한 값(B 노트 §6.3). `MV.Run` EditMode 시험이 이 값과 같아야 한다. idx는 0기반(`r = idx/3`, `l = idx%3`).

| Area | Floor | sN | 생선 칸 idx=양 | 캣닢 칸 idx | 줄별 HP(easy) |
|---|---|---|---|---|---|
| 1 | 1 | 3 | 4=3, 8=3, 15=4 | 9, 16, 18 | 3,9,16,22,29,35,42,48 |
| 1 | 2 | 4 | 3=4, 8=4, 21=4, 22=5 | 1, 2, 6, 23, 27, 29 | 48,63,77,92,107,121,136,151,165,180 |
| 1 | 3 | 4 | 10=5, 16=6, 21=6, 35=6 | 1, 20, 28, 32, 34 | 180,229,278,327,376,425,475,524,573,622,671,720 |
| 2 | 1 | 3 | 6=3, 9=3, 15=4 | 0, 1, 22, 23 | 6,35,64,93,123,152,181,210 |
| 2 | 2 | 4 | 5=4, 13=4, 21=4, 29=5 | 14, 16, 17 | 210,357,503,650,797,943,1090,1237,1383,1530 |
| 2 | 3 | 4 | 10=5, 22=6, 24=6, 29=6 | 2, 5, 19, 26 | 1530 … 3315 |
| 3 | 1 | 4 | 6=3, 25=3, 26=4, 28=4 | 2, 12 | 15 … 525 |
| 3 | 2 | 4 | 11=5, 12=5, 18=5, 33=5 | 10, 17, 21, 24, 27, 29 | 525 … 4500 |
| 3 | 3 | 5 | 6=5, 9=5, 11=5, 23=5, 41=6 | 2, 19, 27, 35 | 4500 … 10200 |
| 4 | 1 | 4 | 4=3, 5=3, 25=4, 29=4 | (없음) | 21 … 900 |
| 4 | 2 | 4 | 15=5, 21=5, 22=5, 28=5 | 6, 8, 12, 13, 20, 35 | 900 … 9000 |
| 4 | 3 | 5 | 7=5, 12=5, 24=5, 34=5, 36=6 | 15, 35 | 9000 … 18000 |
| 5 | 1 | 4 | 8=4, 16=4, 27=5, 30=5 | 21 | 36 … 1500 |
| 5 | 2 | 5 | 4=4, 13=4, 16=5, 24=5, 26=5 | 2, 22, 32 | 1500 … 15000 |
| 5 | 3 | 6 | 11=4, 18=5, 23=5, 25=5, 35=5, 47=5 | 2, 3, 9, 12, 13, 26, 33, 44, 46 | 15000 … 36000 |

D-27 닫음(2026-11-10, 사용자 방침): 지금 동작하는 `JsMath.Sin` 해시와 PC 골든 시험은 그대로 둔다. 실기(IL2CPP ARM64)에서 골든을 대조하는 일은 하지 않는다(사용자 방침으로 제외). 레벨링 시스템을 다시 짤 때는 시드를 받는 정수 RNG(`System.Random` 또는 xorshift)로 바꿀 수 있다. `UnityEngine.Random`은 전역 상태라 쓰지 않는다. 사용자 원문 「유니티 랜덤으로 대체 못해? 그리고 어차피 느낌 보고 레벨링 시스템은 다시 구성될거야」.

### 4.14.4 buildLevel `L1783~1807`

```text
BuildLevel(baseZ, level, saved):   // 쓰는 표: 312, 310, 311, 314, 313, 302, 303, 114, 105(consumeData)
  A = save.area; cp = min(2, save.cp); tpl = TplFor(level)
  objs = MakeObjs(tpl, A, baseZ, rng)                               // ① 템플릿
  z = baseZ + 310_template[tpl].endZ
  for r in 0..1, for li in 0..2 (x = -2.2, 0, 2.2):                 // ② 보너스 상자 2줄 (313_fieldGen.bonus*)
    gem = (r==1 && li==1)
    objs.Add(gem ? gemBonus : coinBonus, x, z + 3 + r*4.5)          // hp round(hp×0.4 | ×0.25), reward 0+dia1 | 12+6zone
  z += 14
  gates = []
  for f in cp..2:                                                   // ③ 골목
    objs.Add(arch{x:0, z, label: "골목 f+1"}); gates.Add({z, floor:f+1})
    z0 = z + 6; zEnd = z0
    for c in FieldSpec(A, f):                                       // idx 오름차순
      mx = max(1, round(c.hp * obstMul()))
      hp = (saved?[f]?[c.idx] != null) ? min(saved[f][c.idx], mx) : mx
      o = wall{bar:f, row:c.r, lane:c.l, x:-2.4 + c.l*2.4, z:z0 + c.r*1.7, hw:1.15, hd:0.55, hp, max:mx, rew:c.rew, amt:c.amt, idx:c.idx}
      if hp <= 0: o.dead = true; o.pre = true
      objs.Add(o); zEnd = max(zEnd, o.z)
    z = zEnd + 6
  gz = z; objs.Add(arch{x:0, z:gz, label:"Area A+1 돌파", gate:true})   // ④ Area 아치
  all o.lvl = level
  R.{level, tpl, base, gates, gz, floor:0, cp, cpRun:cp, lvClimbed:0, savedBars: saved}
  R.objs.AddRange(objs); R.wl = objs where k=="wall"
```

| 원문 상수 | 값 | → `313_fieldGen[default]` 열 |
|---|---|---|
| 보너스 줄 2, x −2.2/0/2.2, z +3 + 4.5r, 가운데 2번째 줄 gem | | `bonusRows`, `bonusLaneX`="-2.2/0/2.2", `bonusZ0`=3, `bonusRowStep`=4.5, `bonusGemRow`=1, `bonusGemLane`=1 |
| 보너스 뒤 아치까지 14 | 14 | `bonusToArch` |
| 아치 → 필드 6, 필드 → 다음 아치 6 | 6 / 6 | `archToField`, `fieldToNext` |
| 장애물 레인 x −2.4 + 2.4l, 줄 1.7, hw 1.15, hd 0.55 | | `laneX0`, `laneStep`, `rowStep`, `wallHw`, `wallHd` |
| 아치 문구 `FLOOR_N`, `Area ${A+1} 돌파` | | 문구 키 `ui.run.floorN`, `ui.run.areaGate` [07 §7.12] |

Area 1, cp 0, base 0 위치 검증(B 노트 §6.5): 템플릿 1 → 보너스 192/196.5, 아치 203/226.9/254.2, gz 284.9. 템플릿 8 → gz 333.9.

### 4.14.5 newRun `L1808~1822`

```js
R = {objs:[], Z:curZ(), area:save.area, p:{x:0, z:0, tx:0, phase:0, throwT:0}, startLevel:save.level, cleared:0,
  gauge:{lv:1, fill:0}, bt:baseTier(save.up.weapon), stairs:0, climbed:0, climbs:[],
  w:[0,0,0,0,0,0,0,0,0], cards:{}, clones:0, fireT:[0,0.13,0.26,0.07,0.19], projs:[], …,
  gold:0, souls:0, dia:0, pendingW:[], beamT:0, meteorT:0, spinA:0, spinT:0, time:0, fin:false, walls:0,
  god:save.god, el:(GODS[save.god]||GODS.thunder).el, stormCd:0, blastCd:0, vol:0};
setGodCards(save.god);
{ const k = kegStart(save.up.stair); R.gauge.lv = k.lv; R.gauge.fill = k.lv < 8 ? (k.step/k.need)*gaugeNeed(k.lv) : 0; }
R.w = save.deck.slice(); handReset();
R.clones = Math.min(MAX_CLONES, save.aup.clone);
buildLevel(0, save.level, (save.bars || {})[barKey()]);
rebuildUnits();
```

```text
RunSim.Create(RunSetup s, MvTables t, IRng rng):
  // RunSetup = 런 동안 바뀌지 않는 save 스냅숏: area, zone, level, souls(누적), cp, bars['A'+area],
  //            up.{stair,weapon,income}, deck[9], aup.{rate,range,clone,crit,dbl}, rb.perks.{power,crit,obst,dcap},
  //            god, preset(06_balancePreset 행), rateOverride(디버그), timeScale(디버그)
  1. 상태 초기화(위 R 필드 그대로). el = 113_god[god].element
  2. 카드 풀 = 111_cardPool[main] (CommonActive: 110_card group 0·active 1 + GodCards: 출전 신 cardGroup·active 1)
  3. 게이지 = kegStart(up.stair) (11_stairLevel)
  4. w = deck 복사; HAND는 UI가 WeaponHandReset 이벤트로 받음
  5. clones = min(4, aup.clone)
  6. BuildLevel(0, level, bars)
  7. 이벤트 RunStarted{area, level, tpl, areaNameKey}
```
- 런 중에 save를 읽는 곳(goldMul의 income, obstMul, diaCap, critMul, zone, souls)은 모두 RunSetup 스냅숏을 쓴다(프로토도 런 중엔 save가 안 바뀜).

---

## 4.15 밸런스 프리셋

### 4.15.1 원값 `L832~834`

| 키 | orig(원작 근사) | mid(임시 기본) | **easy(쉬움, 프로토 기본)** | → `06_balancePreset` 열 |
|---|---|---|---|---|
| rate | 0.95 | 1.4 | 2.0 | `rate` |
| scale | 10 | 5 | 3 | `scale` |
| diff | 0 | 0.01 | 0.02 | `diff` |
| gauge | 2 | 1.4 | 1 | `gauge` |
| 기본 | | | O (`BAL = Object.assign({}, BALS.easy)`) | `isDefault` |

- 프로토 실제 연사는 `DBG.rate`(디버그 슬라이더 0.95~4, 프리셋 전환 시 `BAL.rate` 복사) `L3860~3861`. Unity는 `preset.rate`를 쓰고, 디버그 패널만 `RunSetup.rateOverride`로 덮는다.
- 기준 프리셋 선택은 결정 대기 [D-03](13_Decisions.md)(권장 easy).

### 4.15.2 적용되는 곳·안 되는 곳

| 키 | 적용 | 공식 |
|---|---|---|
| rate | 발사 간격 | `iv = 1 / (rate × …)` |
| scale | 계단 게이트 문턱, 공방 비용, **장애물 HP** | `gateMax`, `craftT`, `fieldSpec.hp` |
| diff | 계단 게이트 감쇠 | `dmg / (1 + n × diff)` |
| gauge | 우물 게이지 필요량 | `gaugeNeed` |
| **미적용** | 쥐·상자·보너스 상자 HP(Z.hp × 0.4/0.6/0.25/0.4), 모든 보상, 계단 압축·오르기·카드 등급, 카드·신 효과 수치, 메타 비용 | — |

### 4.15.3 Area 1 예시 (Z.hp 100, 강화·카드 0)

| 항목 | easy | mid | orig |
|---|---|---|---|
| 발사 간격 / 초당 피해(털뭉치 10) | 0.5초 / 20 | 0.714초 / 14 | 1.053초 / 9.5 |
| 계단 게이트 첫 칸 M | 11.4 | 19 | 38 |
| n=10일 때 칸당 필요 | 13.68 | 20.9 | 38 |
| 공방 k=0/1/2 | 5.7 / 17.1 / 28.5 | 9.5 / 28.5 / 47.5 | 19 / 57 / 95 |
| 게이지 Lv1→2 / Lv7→8 | 150 / 2516.58 | 210 / 3523.22 | 300 / 5033.16 |
| 골목 1 줄별 HP | 3 → 48 | 5 → 80 | 10 → 160 |
| 골목 2 / 3 HP 범위 | 48→180 / 180→720 | 80→300 / 300→1200 | 160→600 / 600→2400 |
| 쥐 / 상자 / 보너스 HP | 40 / 60 / 25·40 | 같음 | 같음 |

Area 2는 게이트·공방·게이지가 2배, Area 3은 5배, Area 4는 7배, Area 5는 9.5배(`302_area.hp / 100`).

---

## 4.16 잔재 판정

「정본 화면에 나오는가」는 easy·신 thunder·정상 플레이 기준. 「실제 동작」 경로와 「정의만」 경로를 나눴다.

### 4.16.1 카드 키

| 항목 | 화면에 나오나 | 실제 경로 | 권장 | 이유 |
|---|---|---|---|---|
| `crit` 카드 | X (CARDS에 없음) | 공식은 살아 있음(crC) | 효과 타입 구현, `110_card.active=0` — D-20 | `aup.crit`과 같은 공식을 공유하므로 비용 0. 신 확장 때 켤 수 있음 |
| `aup.crit` (Area 강화) | **O** | **실제 동작** | 포함 | 바구니 강화로 crC += 0.1L |
| `dbl` 카드 | X | 공식 살아 있음 | crit와 같음 — D-20 | |
| `aup.dbl` | **O** | **실제 동작** | 포함 | dbC += 0.1L |
| `pierce` | X | 투사체 pierce 경로 살아 있음(spear가 같은 경로 사용) | 효과 타입 구현, active=0 — D-20 | 창(spear)이 pierce 99로 같은 코드를 씀 |
| `spin` | X | 주기 피해·rebuildUnits 분기 | 구현하면 **spinT 분리**, active=0 — D-20 | orb와 타이머 공유 버그 |
| `beam` | X | 주기 피해 + holyBeam 연출 | 구현, active=0 — D-20 | 08의 신 확장(빛) 후보 |
| `meteor` | X | 주기 피해 + 유성 연출 | 구현, active=0 — D-20 | 08의 신 확장(별빛) 후보 |
| `frost` | X | `o.chill`을 세우는 코드 없음(이중 불가) | **제외**(효과 타입도 만들지 않음) | 규칙이 미완성. 눈꽃 신은 08이 새로 설계 |
| `clone2` | X | applyCard·validCard 경로 | CloneAdd p1=2 행만, active=0 | 같은 효과 타입의 매개변수 차이 |
| `fire`/`bolt` 아이콘 | 신 속성 표시(아이콘) | 카드 효과 아님 | 아이콘만(03) | |

### 4.16.2 함수·상수

| 항목 | 화면 | 권장 | 이유 |
|---|---|---|---|
| `GATE_MAX[1..7]`, `gateMax(lv>8)` 공식 | X (항상 lv 1) | 제외. `107_chargeGate[sgate].base`=1.9 하나만 | 호출이 `gateMax(1)`뿐 `L1507`, `L1946` |
| `compressK`, `STAIR_C` | X | 제외 | 호출 없음. 계단 압축은 `startClimb`의 60/3/150 |
| `nextLevel()`, `finish('area')`, `LAUNCH_AREAS` | X | 제외 | 호출 없음. `finish('area')`는 `R.hitWall.bar` 접근 오류 경로 |
| `buildCells`, `cell`, `BAND_H`, `R.band`, `R.fz`, `cellsDone`, `stuckT`, `blocks()`의 cell | X | 제외(105_objectKind에 cell 행 없음) | 호출 없음, `R.fz` 미정의 |
| `hit()`의 `stair` 분기, `kegPop` | X | 제외 | stair는 hittable이 아니라 도달 불가 |
| `stamp()` (`#mulFx`), `showCard()` (`#card`) | X | 제외 | 호출 없음 |
| `MS`, `P0`, makeObjs `t7`, `if (false)` coin 무리, addWeapon `quiet` | X | 제외 | 미사용 |
| `flushOre` | 드물게(벽돌 44개 소진 시) | 규칙상 무시(§4.5, 차이 ≤ 0.2초) | 연출 대체 경로 |
| `frostHit`, `flyToWorld` | X | 제외 | 호출 없음 |
| SND `load`, `fuse`, `boom`, `stamp`, `whoosh` | X | 제외(03이 굽지 않음) | 호출 없음 |
| `CN.phase` 'land'/'load'/'jump'/'fire', `CN.hudLeft`, `CN.fed`, `CN.rx` | X | 제외. Climb은 단일 단계 | 대포 사이클 잔재 |
| `#tierUp`(등급 게이지) | X (`display:none!important`) | 제외 | 연료 게이지가 대체 [추정] |
| `#unlockBtn`, `ZONES[].unlock`, `escaped`, 「겨울나기 완료」 | X | 제외(저장 필드 `escaped`는 09 판단) | 항상 숨김 |
| `GOD_IDS` | X | 제외 | 미사용 |
| `GODS.flame`(햇살 신) | X (메뉴 잠김) | 데이터 포함, 잠금 — 08 신 확장 | 저장값이 flame이면 동작하는 실데이터 |
| `ELEM`의 frost/meteor/beam 색 | X | 08 신 확장용 색으로만 | |
| `rb.lv` | O (부활 레벨 표시) | 포함 | 표시용 카운터 |
| `R.time`, `R.cleared`, `R.startLevel` | X | 상태 필드는 둬도 되나 규칙 없음 | 미사용 |

---

## 4.17 난수 — RunRng

### 4.17.1 프로토 `Math.random` 사용처 전부 (L729 이후)

`rnd(a,b) = a + Math.random()*(b-a)` `L854`.

| 줄 | 용도 | 분류 | Unity |
|---|---|---|---|
| L1323 | 쥐 `spd = rnd(1.1,1.6)` | **규칙** | RunRng, 생성 시 쥐 순서대로 |
| L1323 | 쥐 `ph = Math.random()*6` | 그림 | FxRng |
| L1927 | 쥐 처치 8% 캣닢 | **규칙** | RunRng |
| L1986 | proc 50% (proc 카드 있을 때만 판정) | **규칙** | RunRng |
| L2004 | storm 25% (`st && stormCd ≤ 0`일 때만 판정) | **규칙** | RunRng |
| L2005 | blast 10%+4%(n−1) (`bl && blastCd ≤ 0`일 때만 판정) | **규칙** | RunRng |
| L2081 | 치명(유닛 볼리마다, 확률 0이어도) | **규칙** | RunRng |
| L2090 | 2연발(창이 아닌 볼리마다) | **규칙** | RunRng |
| L2167 | 폭풍 대상 인덱스(후보 있을 때) | **규칙** | RunRng |
| L2168 | 폭풍 착탄 x 흔들림, (후보 없으면) z 흔들림 | **규칙**(피해 상자 위치) | RunRng |
| L2301~2302 | drawCard 풀 인덱스 | **규칙** | RunRng, tiers 순서대로 |
| L2361 | 룰렛 회전 풀 섞기 | 그림 | FxRng |
| L1911~1972 팝 위치 `rnd`, L1912 tower 불꽃 30%, L1933 wall 파편 30% | 그림 | FxRng |
| L1555~1612 sparks/debris/dust, L1583 bolt, L1591 bolt 깜빡임, L1597 frostHit | 그림 | FxRng |
| L1854~1856 kegPop, L1867 벽돌 고르기, L1871 벽돌 속도, L1883 flushOre | 그림 | FxRng |
| L2052 공방 아이콘 위치 | 그림 | FxRng |
| L2137 화상 불꽃 위치 | 그림 | FxRng |
| L2160 폭풍 구름, L2177 하악질·햇살 폭발 fireball 위치 | 그림 | FxRng |
| L2333 카드 별 | 그림 | FxRng(UI) |
| L2778 카메라 흔들림 | 그림 | View 난수 |
| L994 노이즈 텍스처, L1072 고양이 애니, L1347 우물 잡동사니, L2969 그림 | 그림 | 리소스 굽기·View |
| L1634, L1646 소리 잡음 | 소리 | 굽기([03](03_ResourcePipeline.md)) |

### 4.17.2 규칙

1. `MV.Run`은 **규칙 난수 전용 `RunRng`(IRng) 하나**만 쓴다. 그림 난수는 이벤트를 받은 `MV.Fx`/`MV.View`/`MV.UI`가 자기 `FxRng`로 만든다. `MV.Run`은 그림 난수를 한 번도 뽑지 않는다.
2. `IRng`: `double NextDouble()` ∈ [0,1). 파생 `Range(a,b) = a + NextDouble()*(b−a)`, `Chance(p) = NextDouble() < p`, `Index(n) = (int)Math.Floor(NextDouble()*n)`. 알고리즘은 PCG32 또는 xoshiro256** [창작], 시드 `ulong`.
3. 시드: 런마다 `RunSetup.seed`. 기본값은 `MV.Meta`가 만든 무작위 값이고 결과 로그(통계)에 남긴다. QA·재현은 시드를 주입한다.
4. **호출 순서 고정 원칙**: 위 표의 규칙 난수는 프로토 코드와 같은 위치·같은 조건에서만 뽑는다. 특히
   - 치명은 확률이 0이어도 뽑는다(L2081은 항상 `Math.random()` 호출).
   - proc/storm/blast는 단락 평가 조건(카드 있음·쿨 끝남)이 참일 때만 뽑는다.
   - 2연발은 창 볼리면 뽑지 않는다.
   - hit() 안 순서: (enemy 처치 캣닢) → proc → [bolt 추가타의 중첩 hit] → chain(중첩 hit들) → storm → blast.
   - 처리 순서는 §4.13.3 프레임 순서, `R.objs`·`R.projs` 배열 순서를 따른다.
5. 프로토와의 대조(MV-QA-HARNESS): 프로토는 그림·규칙 난수가 한 줄기로 섞여 있어 **시드 대조가 불가능하다.** 대신 프로토의 `Math.random`을 상수 함수(`() => c`, c ∈ {0, 0.5, 0.999})로 바꾸고 Unity는 같은 상수를 내는 `ConstRng(c)`를 넣어 비교한다(상수면 뽑는 횟수·순서와 무관하게 같은 값). 프레임은 고정 dt로 직접 구동하고(`window.__ce`로 `R`·`DBG` 접근 가능 `L3885`), setTimeout은 가짜 타이머로 같은 시계에 묶는다.
6. Unity 내부 재현 시험: 같은 `RunSetup`+시드+입력 기록이면 `RunResult`와 이벤트 열이 바이트 단위로 같아야 한다.

---

## 4.18 RunEvent

`MV.Run`이 한 Tick 동안 쌓고 `DrainEvents()`로 넘긴다. 이름은 [10 §10.4](10_Architecture.md)의 `Hit`, `Break`, `GateFill`, `GateTake`, `GateMiss`, `Steal`, `ClimbStart`를 따른다(요청서의 GateCharge = `GateFill`, GateReceive = `GateTake` + `StairsReceived`). 좌표는 게임 좌표, 그림 흔들림(팝 위치 지터 등)은 구독자가 FxRng로 더한다. 소리 키는 프로토 `SND` 키 이름 그대로(`03_sound.key`) [가정].

### 4.18.1 이벤트 목록

| 이벤트 | 필드 | 구독 |
|---|---|---|
| `RunStarted` | area, level, tpl, areaNameKey | UI 배너 `Area N` / `${이름} · Level N`, Sound(intensity 1) |
| `FloorEnter` | floor(1~3), prevSoulsOk(bool) | UI 배너, Sound `floor` |
| `Fire` | unit(0~4), projId, x, z, tier, el, spear, pairIndex(−1/0/1) | View 탄 생성, Sound `throw`(unit 0, 창 아님) |
| `ProjImpact` | projId, x, z, crit | Fx impact·sparks, Sound `hit`/`crit` |
| `ProjRemoved` | projId | View |
| `Hit` | objId, kind, x, z(=z−hd), dmg(int), crit, colorKey(Normal/Crit/GodBolt/GodFire/Burn/Beam/Meteor/Blast), quiet | Fx 피해 숫자(quiet면 생략), kind별 반응(tower clank·불꽃, wall 파편, coin 불꽃), View punch/shake |
| `Break` | objId, kind, x, z, rew(Gold/Dia/Soul/None) | Fx debris·dust·boom, Sound(wood/coin/gem/soul) |
| `Gain` | currency(Gold/Dia/Soul), amount(표시값), x, y, z, popSize, fly(bool) | UI 보상 팝·HUD 비행(560ms) |
| `Hitstop` | sec | (MV.Run 내부 적용, 구독자는 FX 시간 배율 참고) |
| `Shake` | amp, sec | View 카메라 |
| `GateFill` | objId, kind(sgate/cgate), got(sgate +n / cgate +1), progress(0~1) | UI 라벨·팝 `+n`, View flash |
| `GateTake` | objId, kind, amount(n 또는 made) | sgate: Sound `gate`, sparks, `#hStair` 비행 / cgate: Sound `forge`, 아이콘 min(n,5)개 70ms 간격 / amount 0이면 팝 `계단 0`·`무기 0` |
| `GateMiss` | objId, kind, amount | 팝 `계단 n 놓침` / `무기 n개 놓침` |
| `StairsReceived` | n, total | UI `#stairBox` pulse |
| `WeaponDeliver` | n, tier | UI HAND `handDeliver` |
| `WeaponHandReset` | w[9] | UI HAND `handReset` |
| `WellFlush` | objId, amount, bricks(1~4) | View 벽돌 빼기·튐, Fx 비행 |
| `GaugeReceive` | amount, lv, fill | UI `gpulse` |
| `GaugeLevelUp` | lv | 팝 `계단 Lv N! 게이트 +N`, Sound `lvup`, UI `glvup` |
| `Steal` | objId, amount, x, z | Fx debris, Sound `steal`, 팝 `츄르 -N`/`휙!` |
| `Blocked` | objId, x, z | Sound `wood`, debris, 팝 `쾅!` |
| `GodProc` | targetId, el, from, to | Fx bolt 번개 / 화상 불꽃 |
| `GodChain` | el, points[] 또는 (x, z, radius) | Fx bolt 연쇄선 / 불씨 폭발 |
| `BurnTick` | objId | Fx 불꽃(피해는 `Hit`) |
| `GodStormStart` | x, z, el, life | Fx 구름(fire) |
| `GodStormStrike` | x, z, el | Fx 냥냥펀치 도장(bolt)·fireball(fire)·boom |
| `GodBlast` | x, z, el | Fx 하악질/햇살 폭발, 화면 플래시 0.35(60ms), 팝 |
| `OrbTick` | objIds[] | Fx sparks |
| `Beam` / `MeteorLaunch` / `MeteorImpact` | x, z | Fx (잔재, D-20) |
| `ClimbStart` | n, k, pw, z0, zEnd, dH, T | View 계단 생성, Sound `gate`, UI 연료 게이지 |
| `ClimbStepShown` | i | View 칸 등장, Sound `tick`(i%4==0) |
| `ClimbLap` | lap, stepIndex | Fx boom·sparks, Sound `card`(4), UI hcBurst |
| `ClimbTierUp` | tier, stepIndex | Sound `tierup`(tier), Fx, UI 플래시 0.25(90ms) |
| `ClimbWarpStage` | stage | View 워프 배경, Sound `tierup`(min(4, st+1)) |
| `ClimbEnd` | n, tier | 팝 `N칸 우다다!`, Sound `land`, Fx |
| `RouletteStart` | tiers[], picks[] | UI 머리 위 카드 |
| `RouletteFlip` | i | UI 얼굴 넘김(FxRng 풀), Sound `tick`(i==0) |
| `RouletteChosen` | i, cardId, tier | UI 확정·hcBurst, Sound `card`(tier), Fx boom, 팝 `이름 · 설명` |
| `RouletteFly` | i, cardId | UI flyCard(620ms) |
| `CardApplied` | cardId, count | UI 버프 arrive, Sound `buy` |
| `RouletteEnd` | — | UI 정리 |
| `Toast` | key, args | UI 토스트(1.4초) |
| `Banner` | key, subKey, args | UI 배너(1.5초) |
| `RunFinished` | RunResult | UI 결과 화면, Sound `win`/`fail`(350ms) |

### 4.18.2 프로토 호출 → 이벤트 대응

| 프로토 호출 (줄) | 이벤트 |
|---|---|
| `banner('Area …')` startRun L2663 | `RunStarted` |
| `SND.play('floor')`, `banner('골목 …')` L2028 | `FloorEnter` |
| `SND.play('throw')` L2091, `weaponMesh`/`spearMesh` L2084~2089 | `Fire` |
| `impact`, `SND.play('hit'/'crit')`, `sparks` L2107~2108 | `ProjImpact` |
| `pop(fmt(dmg))` L1911/1924/1932/1960, `SND.play('clank')` L1912, `o.punch/flash/shake` | `Hit` |
| `debris/dust/boom` + `SND('wood'/'coin'/'gem'/'soul')` L1925, L1934~1942, L1962~1967 | `Break` + `Gain` |
| `pop('+N')`, `pop('생선 +N')`, `pop('캣닢 +N')`, `flyTo('#hGold'/'#hSoul'/'#hDia')` | `Gain` |
| `hitstop(0.03/0.06)` L1925/1936/1963 | `Hitstop` |
| `shake(...)` L1936, L2063, L2068, L2178, L2200, L2209, L2277, L2282, L3165 | `Shake` |
| `pop('+got')` L1949, `pop('+1')` L1954 | `GateFill` |
| `SND('gate')`+`flyTo('#hStair')` L2046~2047, `SND('forge')`+`flyTo('#wHand')` L2051~2052, `pop('계단 0'/'무기 0')` | `GateTake` |
| `pop('계단 n 놓침'/'무기 n개 놓침')` L2049, L2054 | `GateMiss` |
| flyTo 콜백 `R.stairs += n`, `#stairBox pulse` L2047 | `StairsReceived` |
| `handDeliver(n, R.bt)` L2057 | `WeaponDeliver` |
| `flushBricks` L1861~1876 | `WellFlush` |
| `gaugeAdd` + `gpulse` L1873~1874 | `GaugeReceive` |
| `pop('계단 Lv N!…')`, `SND('lvup')`, `glvup` L1890~1891 | `GaugeLevelUp` |
| `SND('steal')`, `pop('츄르 -N')` L2063 | `Steal` + `Shake` |
| `shake(0.35,0.3)`, `SND('wood')`, `pop('쾅!')` L2068 | `Blocked` + `Shake` |
| `bolt()` L1987, L1997; 화상 sparks L1988; `boom` L2000 | `GodProc`, `GodChain` |
| `godStorm` 구름 L2159~2160 / 타격 L2169~2170 | `GodStormStart` / `GodStormStrike` |
| `godBlast` L2177~2180 | `GodBlast` + `Shake` |
| orb sparks L2143 | `OrbTick` |
| `startClimb` `SND('gate')` L2260 | `ClimbStart` |
| `SND('tick')` L2270 | `ClimbStepShown` |
| 랩 L2274~2277 | `ClimbLap` + `Shake` |
| 등급 L2281~2283 | `ClimbTierUp` + `Shake` |
| `drawWarp` 단계 L3165 | `ClimbWarpStage` + `Shake` |
| `pop('N칸 우다다!')`, `SND('land')` L2285~2286 | `ClimbEnd` |
| `startRoulette` L2358~2362 | `RouletteStart` (tiers 비면 `Toast`) |
| `hcSet` + `SND('tick')` L2368 | `RouletteFlip` |
| 확정 L2369~2371 | `RouletteChosen` |
| `flyCard` L2375 | `RouletteFly` |
| `applyCard` + `SND('buy')` L2377 | `CardApplied` |
| `endRoulette` L2391 | `RouletteEnd` |
| `toast(...)` L2244, L2359 | `Toast` |
| `finish` L2394~2418 | `RunFinished` |
| 「변신!」 팝·lvup L1221 | (UI HAND 내부, Run 이벤트 아님) |
| `#warn` + `SND('warn')` L2738~2741 | (UI가 `NearestBlockerAhead` 조회로 판단) |

---

## 4.19 의문점·결정 필요

### 4.19.1 결정 항목

| ID | 질문 | 선택지 | 권장안 | 영향 |
|---|---|---|---|---|
| D-20 | 획득 불가 카드 효과(crit, dbl, pierce, spin, beam, meteor, clone2)를 Unity에 구현하나 | A 효과 타입 구현 + `110_card.active=0` / B 전부 삭제 / C 획득 가능하게 켜기 | **A**. frost만 제외. spin을 구현하면 orb와 타이머 분리 | A: 화면은 정본과 같고 08의 신 확장 때 표만 바꿔 켬. 구현·시험 비용 증가(효과 7종). B: 가볍지만 신 확장 때 다시 구현. C: 정본과 플레이 감각이 달라짐 |
| D-21 | 수령 지연(계단 0.56초, 게이지 ≈0.82초, 카드 적용 0.62초)을 규칙에 남기나 | A 프로토와 같은 지연을 `MV.Run` 결정적 타이머로 / B 즉시 수령, 연출만 지연 | **A** | A: 「게이트 통과 직후 0.56초 안에 계단에 닿으면 그 계단은 이번에 못 씀」 같은 프로토 결과까지 같음. B: 단순하지만 정본과 판정 결과가 달라질 수 있음 |
| D-22 | 시간축: 프로토는 실시간(setTimeout·syncScene)과 게임 시간(히트스톱·배속)이 섞여 있음 | A 프로토와 같은 이원 시계(§4.13.2 표대로) / B 전부 게임 시간 | **A**. 단 실시간 타이머도 dt 상한 0.05를 적용 | A: 히트스톱·배속 중 수령 타이밍이 정본과 같음. B: 디버그 배속에서 결과가 달라짐 |
| D-23 | 룰렛이 끝나기 전 다음 계단을 오르면 미적용 카드가 사라지는 동작 | A 프로토 그대로 덮어쓰기 / B 이전 룰렛의 확정 카드를 즉시 적용 후 새 룰렛 / C 대기열로 이어 붙임 | **B** | A: 정본과 같으나 카드 손실(버그로 보임). B: 손실 없음, 연출 일부 생략. 정상 배치(계단 간격 ≥ 약 90m)에서는 거의 안 생김 [추정] |
| D-24 | 강제 전면 광고 조건 `×1 수령 && area ≥ 3`(개방 반영 후) — Area 3→4 개방 판에서도 뜸 | A 프로토 그대로 / B 개방 반영 전 area로 판정 | **A** | A: 정본과 같음. B: 첫 전면 광고가 한 판 늦어짐 |
| D-25 | 부활해도 Level(판 번호)이 유지됨(문구는 「처음부터 다시」) | A 유지 / B 1로 초기화 | **A** | A: 정본과 같고 tplFor 진행(템플릿 순환)이 이어짐. B: 부활 후 템플릿 1부터 |
| D-26 | 마지막 Area 판정 `PROTO_AREAS = 5`(Area 5에서 zoneEnd, 진행 정지)와 30 Area 콘텐츠 | A `300_runConfig.PLAYABLE_AREAS`를 공개된 Area 수로 두고 그 끝에서만 zoneEnd / B 프로토 5 고정 | **A** (초기값 5, 08이 Area를 열 때 올림). Zone 경계 통과는 pass + Zone 변경 배너(collect L2439가 이미 지원) | A: 콘텐츠 추가가 표 수정만으로 됨. 결과 제목 `Zone 1 클리어!` 문구는 Zone 번호 인자로 바꿔야 함 |
| D-27 | Floor 생성의 sin 해시 결정성 | A 런타임 계산 + 30×3 골든 파일 시험(실기 포함) / B 생성 결과를 표 데이터로 미리 구움 | **닫음(2026-11-10 사용자 방침)**: A를 PC 골든 시험까지만 유지, 실기 대조 제외. 레벨링을 다시 짤 때 시드 정수 RNG(System.Random·xorshift)로 교체 가능, UnityEngine.Random 금지 | A: 표가 규칙(313_fieldGen)만 가짐, 수치 조정 즉시 반영. B: 플랫폼 안전하나 FINAL·규칙을 바꿀 때마다 다시 구워야 함 |
| D-28 | 손패 표시 지연: 피해는 `eqTier`(즉시), 고양이 외형·`#wDmg` 문구는 `handTier`(최대 약 3초 늦음) | A 유지 / B 외형도 즉시 | **A** | A: 정본 감각(머지 연출 끝에 변신). B: 연출과 외형이 어긋남 |
| D-29 | 광고 일일 초기화 기준 | A UTC 0시(= KST 09:00, 프로토) / B KST 0시 / C 서버 시각 | **A**(서버 연결 전), 서버 연결 시 C | A: 정본과 같음. 한국 사용자에게 오전 9시 초기화로 보임 |

### 4.19.2 프로토 그대로 따르는 항목(결정 불필요, 확인용)

| 항목 | 이 문서의 처리 |
|---|---|
| 치명·2연발을 볼리 단위로 1회 판정 (C 노트 의문 6) | 프로토 그대로 |
| 계단 게이트 감쇠 `1/(1 + n × diff)`의 n이 모은 계단 수(게이지 Lv 배수 포함) (C 의문 7) | 프로토 그대로, `06_balancePreset.diff`로 조정 |
| bolt 연쇄 거리 기준이 최초 명중점 (C 의문 8) | 프로토 그대로 |
| godStorm 피해가 fireball 낙하 전에 들어감 (C 의문 9) | 프로토 그대로(View가 0.22초 연출만) |
| wall 츄르의 goldMul 시점, 상자 타격당 츄르 (C 의문 11) | 프로토 그대로 |
| shiftWorld가 계단 뒤 오브젝트를 지움 (C 의문 12) | 프로토 그대로. 템플릿 검증기가 「골목 wall은 마지막 stair보다 앞」을 확인([07](07_Tables.md)) |
| 범위 스킬이 우물은 채우고 게이트는 못 채움 | 프로토 그대로(§4.3.1) |
| ×3 보상 광고에 일일 한도 없음 | 프로토 그대로(`dailyLimit 0`) |
| 공방 비행 아이콘이 항상 1등급 그림 | 그림만 `R.bt` 등급으로 고치기를 [06](06_Effects.md)에 권고 [창작] |

### 4.19.3 노트 간 모순·정정

| 내용 | 정정 |
|---|---|
| C 노트 §2.1 「범위 스킬은 게이트를 충전하지 않음」만 적고 우물은 언급 안 함 | breakable에 tower가 있어 범위 스킬도 우물 게이지를 채운다(§4.3.1) |
| C 노트 §1.2 「flushBricks는 updObjMesh(카메라 범위)에서」 — 시간축 언급 없음 | updObjMesh는 syncScene에서 **실시간 dt**로 돈다(L3835). 벽돌 튐 0.26초는 stepFx **sdt**. 두 시계가 섞임(§4.5) |
| C 노트 §7.7 「카드 적용 ≈ 3.12 + 0.45i초」 | 확정 1.0 + 0.45i(게임) + 1.5(게임) + 0.62(실시간). 히트스톱이 끼면 더 늦다 |
| B 노트 §6.3 「영혼·다이아 위치는 BAL 무관」은 맞지만 Area 16~30의 위치가 Area 15와 같다는 오해 소지 | seed가 클램프 전 A를 써서 Area마다 다르다(§4.14.3) |
| 요청서의 이벤트 이름(GateCharge/GateReceive)과 10 문서의 이름(GateFill/GateTake) | 10 문서 이름으로 통일(§4.18) |
| B 노트 FINAL 생선 합(Area 5~15는 70)과 AREA_NEED 증가분(60 또는 50) 불일치 | 규칙에는 영향 없음(개방은 누적 비교). 수치 정리는 [08](08_Content.md)·[07](07_Tables.md) 몫 |

### 4.19.4 다른 문서에 기대한 가정

- [07](07_Tables.md): `300_runConfig` 키 이름(이 문서의 UPPER_SNAKE 이름), `105_objectKind.hittable` enum(Always/BeforePass/Never)과 coinBonus·gemBonus 행, `107_chargeGate`의 `well` 행과 `mul`·`useScale` 열, `108_receiveCondition.clock`(Real/Game) 열, `105_objectKind`의 consumeData 3열 + `rewardValuePerZone`·`hitRewardType/Value` 열과 `enemyId`(→`102_enemy`) 열, `07_consumeType` 600번대에 604 RunDia와 609 RebirthCooldownSec 추가, `801_drop`의 적 캣닢 드롭 묶음(보상 weight 800 + 꽝 9200)과 `rewardValueRule`(HalfDiaCapCeil) 열, `109_moveAI`의 xFollow·touchX/Z·despawnBehind·steal 열, `313_fieldGen`의 bonus*·seed*·hash* 열, `314_spawnGroup`의 mouse5(dz에 +6 포함)·clone 그룹, `16_levelPrice` group 1~3(로비)·11~15(Area 강화)·21~24(부활 특성)과 각 표의 `levelPriceGroup` 열, `13_areaUpgrade.perLv`, `14_rebirthPerk.base/perLv/floor`, `330_climb[default]`의 rl* 열(§4.8.3), `00_config`의 collectAdMul·interstitialMinArea·rebirthArmSec·rebirthCooldownSec·adDayUtcOffsetHour, `08_adPlacement`의 adRewardType/Id/Value·dailyLimit, `05_contentOpen[rebirth]`, 효과음은 `03_sound.key`, `303_areaFloor`의 묶음 키 = `302_area.floorGroup`.
- [09](09_Save.md): `save.bars['A'+area][floor][idx]`, `save.cp`, `save.rb.open`(DEF에 없는 필드), `save.ads.day` 형식.
- [06](06_Effects.md): §4.18 이벤트를 구독해 프로토 FX·팝 위치 지터를 재현.
- [05](05_Screens.md): HAND 배치 수치, `#warn` 판단, 부활 버튼 대기 문구 버그(rbArm 미해제) 처리.
- [08](08_Content.md): 30 Area 수치, 신 10종의 카드 활성(D-20과 연동), `PLAYABLE_AREAS` 증가 시점(D-26).

---

## 4.20 모드 확장 지점

[08 §8.4.10](08_Content.md)의 모드 4종(골목 수비전·우다다 질주·츄르 털이·대왕쥐 소탕)과 [08 §8.5.0](08_Content.md)의 메타 능력치·펫·친구 고양이가 `MV.Run`에 들어오는 자리를 정한다. 이 절의 이름이 정본이다(08은 이 절을 따른다). 이 절에서 새로 만든 규칙은 모두 [창작]이다.

### 4.20.1 원칙과 본편 동일 조건

- **확장점 정의는 `MV.Run`, 모드 구현은 `MV.Content`**다. `MV.Run`은 `MV.Content`를 모른다([10](10_Architecture.md)). 정본 규칙은 `MainRunRule`(MV.Run)이 구현한다.
- **본편 동일 조건(필수)**: 본편 `400_content[400000001]`(contentType 0) + `MainRunRule` + `RunModifiers` 7필드 모두 0 + 동행(`companions`) 없음 + §4.20.3의 모드 필드 모두 기본값이면, 런 결과는 §4.0~§4.18 정본과 **같아야 한다**. 같다는 것은 다음 세 가지가 바이트 단위로 같다는 뜻이다.
  1. `RunResult`(§4.11.1)
  2. `RunEvent` 열(§4.18)
  3. `RunRng` 소비 횟수와 순서(§4.17)
- 기본값이 정본과 같은 값을 내는 근거: 모디파이어는 `× (1 + m/100)` 또는 `+ m/100` 꼴로만 붙인다. m = 0이면 IEEE 754에서 `x × 1.0 = x`, `x + 0.0 = x`가 정확히 성립한다. 그래서 **곱·합을 붙이는 위치와 순서는 아래 표 그대로 고정**한다. 다른 인자와 미리 합쳐 하나의 배율로 만들지 않는다.
- `MainRunRule`의 훅은 정본 동작을 그대로 하거나 아무것도 하지 않는다. 훅 호출 자체가 `RunRng`를 뽑거나 이벤트를 만들지 않는다.
- 모드 규칙이 쓰는 난수는 `ModeRng`, 동행 사격은 `CompanionRng`다. 둘 다 `RunSetup.seed`에서 파생한 **별도 줄기**다(파생식은 구현 자유, 고정). 구현: 같은 시드의 PCG32를 줄기 번호만 달리 만든다 — `ModeRng` = `0x6d6f6465`, `CompanionRng` = `0x636f6d70`(`RunRngStreams`). `RunState.ModeRng`·`RunState.CompanionRng`로 쓴다. 모드·동행이 생겨도 `RunRng` 순서는 정본과 같다. §4.17.2의 「`RunRng` 하나만」 규칙은 정본 경로에 대한 것이고, 이 두 줄기는 정본 경로에서 한 번도 뽑지 않는다.

### 4.20.2 훅 (`IRunRule`, MV.Run)

```csharp
namespace MV.Run {
public interface IRunRule {
    RunSetup Build(RunContext ctx);                    // ① 시작
    void BuildCourse(RunState s, CourseRequest req);   // ② Floor 생성
    void OnHit(RunState s, in HitInfo h);              // ③ 피격
    void Tick(RunState s, double dt);                  // 모드 진행(스폰·타이머)
    RunEnd CheckEnd(RunState s);                       // ④ 종료 판정: None / Clear / Fail
    RunResult Settle(RunState s, FinishReason reason); // ⑤ 보상 정산(계산만)
    void OnEvent(RunState s, in RunEvent e);           // 이벤트 관찰(Kill, Break, FloorEnter …)
}
}
namespace MV.Content {
public interface IModeRule : MV.Run.IRunRule {
    int ContentType { get; }                                  // 400_content.contentType
    ModeResult SettleMode(RunResult r, ModeStageRow stage);   // 모드 보상 계산. 지급은 MV.Meta
}
}
```

08 §8.4.10 초안과 다른 점: `BuildCourse`·`OnHit`·`Settle`(RunResult)을 `IRunRule`에 넣었다. 08의 `IModeRule.Settle`은 이름을 `SettleMode`로 바꿨다.

| 훅 | 호출 위치 | `MainRunRule` 동작 = 정본 | 모드가 할 수 있는 것 | 못 하는 것 |
|---|---|---|---|---|
| ① `Build` | `RunSim.Create` 첫 단계(§4.14.5 앞) | save 스냅숏으로 §4.14.5 `RunSetup`을 만든다. 모디파이어는 `ctx.metaModifiers`, 동행은 `ctx.companions`를 그대로 넣는다 | §4.20.3 모드 필드 설정. `402` 열의 `use*` = 0이면 그 강화 레벨을 0으로 둔 스냅숏을 만든다. `useMetaStats` = 0이면 `RunModifiers.Zero` | 런 중 save 읽기 |
| ② `BuildCourse` | §4.14.5 6단계(`BuildLevel(0, level, bars)`) 자리. 런마다 1회. Floor 3단은 이 한 번에 다 만든다(§4.14.4) | `LevelBuilder.BuildLevel(0, level, saved)`(§4.14.4) 그대로 | 정본 `BuildLevel`을 부른 뒤 오브젝트를 더하거나(layout 0·2), 직접 아레나를 만든다(layout 1). `s.AddObj`는 `R.objs` 끝에 붙인다 | 정본 경로의 `R.objs` 순서 바꾸기 |
| (Floor 진입) | §4.7.3 골목 아치 통과로 `R.floor++`가 될 때 `OnEvent(FloorEnter)` | 없음 | Floor별 스폰·HP 조정 | — |
| ③ `OnHit` | `hit()` 한 번이 **가호(proc·chain·storm·blast)까지 다 끝난 뒤**. 중첩 hit도 각각 호출된다(안쪽부터) | 없음 | XP·보스 HP·처치 수 같은 모드 상태 갱신, `ModeRng` 사용 | `hit()` 다시 부르기, `RunRng` 뽑기, 정본 보상 바꾸기 |
| `Tick` | §4.13.3의 15단계 뒤, 동행 사격(§4.20.5) 다음. Run 상태에서만 | 없음 | 스폰 타이머, 방어선 판정 준비 | — |
| ④ `CheckEnd` | 프레임 마지막(`Tick` 다음). 정본 종료(§4.13.3의 2·9단계 `finish('gate'/'wall')`)는 그대로 먼저 처리한다 | 항상 `None` | `Clear` → `finish('modeClear')`, `Fail` → `finish('modeFail')` | 정본 종료 막기(막으려면 `failOnWall` = 0 같은 Setup 필드로) |
| ⑤ `Settle` | `finish(reason)` 안(§4.11.1) | §4.11.1 계산 그대로 `RunResult` | 결과를 그대로 넘기고 `SettleMode`에서 모드 보상을 계산한다 | 계정 재화 직접 지급(지급은 `MV.Meta`만 한다) |

- 정산 분기(`MV.Meta`): contentType 0이면 §4.11.2 `collect`, 아니면 `CollectMode(ModeResult)`다. `402.runRewardToAccount`(0 없음 / 1 츄르·캣닢만 / 2 정본처럼)는 `SettleMode`가 반영한다. 모드에서는 Level +1, Area 개방, 장애물 기록 저장을 하지 않는다(08 §8.4).
- `FinishReason`은 정본 `gate`·`wall`에 `modeClear`·`modeFail`을 더한다. `RunResult.pass`는 정본 계산 그대로 둔다(모드에선 무시).
- `HitInfo` = `{objId, kind, dmg(정수, §4.3.2 반올림 뒤), crit, killed, source}`. `source`: Projectile / Spear / GodProc / Chain / Storm / Blast / Burn / Orb / Companion / Mode. 구현은 휴면 카드 출처 Spin / Beam / Meteor도 따로 둔다. `kind` = 105_objectKind.id, `killed` = 이 hit의 반응으로 죽었는가(가호 전 판정).

**구현 메모(MV-RUN-RULE)**
- 진입점: `RunSim.Create(RunContext, IRunRule, rng, builder)`가 ① `Build`를 부른다. `RunContext` = `{Tables, Snapshot(save로 만든 정본 RunSetup), MetaModifiers, Companions}`이고 스냅숏은 호출자(MV.Meta/Boot)가 만든다(MV.Run은 save를 모른다). `MainRunRule.Build`는 스냅숏 복사본에 모디파이어·동행을 넣는다. 기존 `RunSim.Create(RunSetup, …)`는 Build를 마친 setup으로 보고 `MainRunRule(builder)`를 쓴다.
- ② `BuildCourse`: GEN이 `IRunStateFactory.NewState`(§4.14.5 1~5단계)로 상태를 만들고 SIM이 6단계 자리에서 `BuildCourse(s, CourseRequest(0, level, keepBars ? bars : null))`를 부른다. `NewRun` = `NewState` + `BuildLevel`(정본 그대로). `NewState`가 없는 생성기(시험 대역)는 `NewRun`이 코스까지 만들고 BuildCourse를 건너뛴다.
- `OnEvent`: `RunState.Emit`마다 이벤트를 쌓은 뒤 부른다.
- `Tick`·`CheckEnd`: updRun 끝(15단계 뒤 = 동행 사격 다음)에 updRun마다 부른다(배속이면 한 프레임에 여러 번). Run 상태가 아니거나 이미 끝났으면 CheckEnd를 부르지 않는다.
- ⑤ `Settle`: 정본 계산은 `RunSettlement.Main(s, reason, builder)`(공개)다. 모드 규칙도 이것을 불러 그대로 넘긴다.

### 4.20.3 RunSetup 모드 필드 (기본값 = 정본)

`402_modeRule` 열(08 §8.4.4)이 이 필드로 들어온다. 본편은 모두 기본값이다.

| 필드 | 기본값(정본) | 적용 위치 | 402 열 |
|---|---|---|---|
| `forwardSpeed` | 4 | §4.13.3 3단계 `nz = P.z + forwardSpeed × dt` | `forwardSpeed` |
| `failOnWall` | 1 | 0이면 §4.13.3 9단계 `finish('wall')`를 건너뛴다(전진 막힘은 그대로) | `failOnWall` |
| `fixedWeaponTier` | 0(없음) | 1~8이면 `R.bt` = 그 값, 시작 덱 = 그 등급 1장(§4.14.5 4단계) | `fixedWeaponTier` |
| `craftPerMade` | 1 | §4.4.3 공방 1개 완성 때 지급 무기 수 | `craftPerMade` |
| `zoneHp` | `302_area.hp`(현재 Area) | `R.Z.hp` 자리 전부: `gaugeNeed`·`gateMax`·`craftT`(§4.0.2), 쥐·상자 HP(§4.6) | `hpSource` = 1이면 `100 × 401.hpMul` |
| `wallHpMul` | 1 | §4.14.4 `mx = max(1, round(c.hp × obstMul() × wallHpMul))` — **obstMul 뒤에 곱한다** | `hpSource` = 1이면 `401.hpMul` |
| `useCheckpoint` / `keepBars` | 1 / 1 | 0이면 `cp = 0` / `saved = null`이고 `RunResult.barRec`는 버린다 | 같은 이름 |
| `cardPoolId` | 신 풀(§4.14.5 2단계) | §4.8.4 카드 뽑기 풀 | `cardPoolId` |
| `layout` | 0 | `BuildCourse`가 읽는다(0 정본 / 1 아레나 / 2 Floor 없음) | `layout` |
| `modifiers` | `RunModifiers.Zero` | §4.20.4 | `useMetaStats` |
| `companions` | 빈 목록 | §4.20.5 | — (Meta가 장착 펫·친구로 만든다) |

### 4.20.4 RunModifiers 7필드

`MV.Meta`가 펫·연구·친구 고양이·신 패시브·신 레벨·장비·축복의 statKey 값을 **% 단위로 합산**해 만든다(08 §8.5.0). 런 동안 바뀌지 않는다. 형은 `double`, 단위는 %다(10 = 10%).

| 필드(statKey) | 기본값 | 붙는 공식(정본 식 끝에 붙인다) | 영향 받는 곳 | 검증 예(Area 1 easy, 강화·카드 0) |
|---|---|---|---|---|
| `Dmg` | 0 | `BaseDmg() = wDmg(eqTier) × (1+0.2·atk) × (1+0.15·power) × (1 + Dmg/100)` §4.0.2 | `baseDmg()`를 쓰는 곳 전부: 투사체·창·가호·연쇄·폭풍·하악질·화상·위성, 동행 사격 | 등급 1, Dmg 10 → 10 × 1.1 = 11 |
| `FireRate` | 0 | `iv = 1 / (rate × (1+0.2·rate) × (1+0.5·frate) × (1+0.1·aup.rate) × (1 + FireRate/100))` §4.2.2 | 본체·분신 발사 간격. 위상 초기값(`FIRE_PHASE`)과 동행 주기는 그대로 | rate 2, FireRate 20 → iv = 1/2.4 ≈ 0.4167 |
| `Range` | 0 | `RANGE = 22 × (1 + 0.1·aup.range) × (1 + Range/100)` §4.2.3 | 투사체 사거리, 동행 대상 거리 | Range 10 → 24.2 |
| `CritDmg` | 0 | `critMul() = 2.2 + 0.2·perk.crit + CritDmg/100` §4.0.2 | 치명 피해 배수 | CritDmg 50 → 2.7 |
| `Gold` | 0 | `goldMul() = (1+0.05·income) × (1+0.5·gold) × (1 + Gold/100)` §4.0.2 | 런 안 RunGold(602) 보상 전부, 장애물 라벨 표시 | Gold 10 → 1.1 |
| `CritChance` | 0 | `crC = 0.15·crit + 0.1·aup.crit + CritChance/100` §4.2.2 | 볼리마다 치명 판정. **난수는 정본처럼 확률과 무관하게 1회 뽑는다** | CritChance 5 → 0.05 |
| `DoubleChance` | 0 | `dbC = min(1, 0.2·dbl + 0.1·aup.dbl + DoubleChance/100)` §4.2.2 | 2연발 판정. 창 볼리는 판정 없음(정본 그대로) | DoubleChance 10 → 0.1 |

- 검증기: `FireRate > −100`, 나머지 ≥ 0. 음수 합은 로드 실패로 처리한다(감소 효과는 아직 없다). 구현: `RunModifiers.Validate()`, `RunSetup.Validate()`(동행 포함)에 걸리면 `RunSim.Create`가 `ArgumentException`을 낸다.
- 표 열 대응: statKey 문자열(`Dmg` …)은 08의 430·440·450·460·470·480·495 표 `statKey` 열 값이다. 07은 이 7개 이름을 enum으로 둔다.

### 4.20.5 CompanionShot (동행 자동 사격)

펫(08 §8.5.2)과 친구 고양이(08 §8.5.4)가 같은 런 동작을 쓴다. `RunSetup.companions`는 `CompanionSpec` 목록이다. 순서는 펫 슬롯 순서, 그다음 친구 슬롯 순서이고, 이 순서가 처리 순서다. 최대 5개(펫 2 + 친구 3).

```csharp
public readonly struct CompanionSpec {
    public readonly int SourceId;     // 430_pet.id 또는 450_friend.id (이벤트·그림용)
    public readonly CompanionTrigger Trigger; // 1 Hits / 2 Chance / 3 Seconds (08의 skillTrigger 번호)
    public readonly double Every;     // Hits: n볼리, Chance: 확률 0~1, Seconds: 초(ms ÷ 1000)
    public readonly double DmgPct;    // 레벨 반영 끝난 값(Meta 계산)
    public readonly int Targets;      // 1 이상
}
```

- `DmgPct` 계산(Meta): 펫 = `skillDmgPct + skillLevelPct × (레벨 − 1)`, 친구 = `skillDmgPct + 5 × (레벨 − 1)`. 펫은 항상 Seconds(`skillEveryMs`)다.

**발사 시점** — Run 상태에서만 판정한다(계단 오르기·룰렛·종료 뒤에는 멈춤). 시간은 `updRun`의 `dt`(게임 시간, 히트스톱·배속 반영)다. 처리 위치는 §4.13.3의 12단계(죽은 투사체 제거)와 13단계 사이다.

| Trigger | 규칙 |
|---|---|
| Seconds | 타이머 `c.t`의 초기값은 `Every`다. 매 프레임 `c.t −= dt`, `c.t ≤ 0`이고 대상이 있으면 발사 후 `c.t += Every`. 대상이 없으면 `c.t = 0`에 멈춰 두었다가(시간 빚을 쌓지 않아 몰아 쏘지 않는다) 대상이 생기는 프레임에 쏜다. 프레임당 최대 1발 |
| Hits | 본체 볼리 수 `R.vol`(§4.2.2, 창 볼리 포함)이 이번 프레임에 `n = round(Every)`의 배수가 되었으면 1발 예약 → 같은 프레임 동행 단계에서 발사. 대상이 없으면 버린다 |
| Chance | §4.2.3에서 **플레이어 투사체(본체·분신·창)가 명중할 때마다** `CompanionRng.Chance(Every)`(이미 예약돼 있어도 뽑는다). 성공하면 1발 예약(프레임당 동행 하나에 최대 1발). 대상이 없으면 버린다 |

**대상 선택**
- 후보: `hittable(o) && breakable(o) && o.k ≠ 'tower'`. 우물·게이트는 동행이 채우지 않아 정본 경제를 건드리지 않는다. 구현은 종류 이름 대신 105 열로 고른다: `chargeGateId` = 0(107 충전 게이트 없음). 지금 표에서 우물(tower)·계단 게이트·공방 게이트가 빠져 같은 결과다.
- 위치 조건: `o.z − o.hd ≥ P.z`(앞쪽)이고 `(o.z − o.hd) − P.z ≤ RANGE`(§4.20.4의 Range 반영).
- 정렬 ① `o.z − o.hd` 오름차순 ② `|o.x − P.x|` 오름차순 ③ `R.objs` 순서. 앞에서 `Targets`개를 고른다.

**피해** — 대상마다 `hit(o, baseDmg() × DmgPct / 100, false, {proc:false, source:Companion})`를 정렬 순서대로 부른다.
- 치명·2연발 판정이 없고, 신 가호도 발동하지 않는다(`proc:false`).
- 반올림·최소 1, 보상·goldMul·히트스톱은 §4.3 그대로다.
- 투사체를 만들지 않는 **즉시 명중**이다. 날아가는 그림은 View가 이벤트로 그린다.

**이벤트** — `CompanionShot{sourceId, fromX, fromZ, targetIds[], dmgEach}`. 대상들의 `hit()`보다 먼저 쌓는다. 이 이벤트는 §4.18 목록에 더하는 항목이다. 구현: from = 플레이어 위치(`P.x`, `P.z`), `dmgEach` = `baseDmg() × DmgPct / 100`(반올림 전). 검증: 동행 최대 5, `Every` > 0, `DmgPct` ≥ 0, `Targets` ≥ 1.

### 4.20.6 검증

1. **본편 동일 시험(필수, MV-QA)**:
   - 같은 시드·같은 입력 기록으로 「확장 지점 없는 기준 구현」(또는 확장 지점을 넣기 전 커밋의 골든 기록)과 「`MainRunRule` + 기본값」을 돌린다.
   - Area 1~5 × Level 1·8·15에서 `RunResult`·이벤트 열·`RunRng` 소비 횟수가 같아야 한다.
   - 프로토 대조(§4.17.2의 5번 ConstRng)도 이 경로로 돈다.
2. 모디파이어 단위 시험: §4.20.4 검증 예 7개. 0일 때 결과가 정본 함수와 비트 단위로 같은지도 확인한다.
3. 동행 시험:
   - 대상 없을 때 Seconds 보류
   - Hits 주기(창 볼리 포함)
   - Chance가 `RunRng` 순서를 바꾸지 않는지: 동행 있음·없음에서 쥐 속도와 카드 뽑기 결과가 같아야 한다

### 4.20.7 남은 질문 (결정 ID 미부여, 13에 올릴 후보)

| 질문 | 이 절의 임시안 [창작] |
|---|---|
| 동행이 우물·게이트를 채우나 | 안 채운다. 정본 계단·무기 경제를 지키기 위해서다 |
| 동행 사격이 즉시 명중인가, 투사체인가 | 즉시 명중이다. 그림만 날아간다 |
| 모드 런에 메타 능력치를 적용하나 | 08 D-76 A안(402 `useMetaStats` 열)을 따른다 |
| 동행 주기에 FireRate를 곱하나 | 곱하지 않는다 |
| Seconds 동행이 대상 없이 기다린 시간을 몰아 쏘나 | 몰아 쏘지 않는다. `c.t`를 0에 멈춰 두고 대상이 생기면 1발, 다음은 `Every` 뒤 |
| 동행 대상 제외(우물·게이트)를 무엇으로 고르나 | 105 `chargeGateId` ≠ 0(충전 게이트가 있는 오브젝트)을 뺀다. 종류 이름(`tower`)으로 고르지 않는다 |
| 배속(ts > 1)에서 `Tick`·`CheckEnd`를 몇 번 부르나 | updRun마다(배속 수만큼) 부른다 |
