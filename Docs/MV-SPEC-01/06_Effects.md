# 6. 이펙트·연출

[입구](README.md) · 앞: [05 화면](05_Screens.md) · 다음: [07 표](07_Tables.md)

이 절은 연출 담당(`MV.Fx`, 작업 계약 MV-FX)이 정본의 이펙트·흔들림·히트스톱·숫자 팝업·보상 비행·배너·토스트·플래시·우다다 화면을 같은 모양과 시간으로 재현하도록 정한다. 투영·정렬·안개·시간축 공통 규칙은 [02](02_Visual.md), 표 열의 최종 정의는 [07](07_Tables.md), 이펙트를 부르는 게임 사건(RunEvent)은 [04](04_GameRules.md)가 정본이다.

정본 사실 먼저:
- 이펙트는 three 객체(스프라이트·메쉬)로 만들지만 **화면에는 `drawFx`(L3138~3151)의 2D 해석으로만** 나온다. 3D 모양(원기둥 번개, 구 운석, 8면체 얼음)은 2D 그림으로 바뀐다(§6.2.2).
- 이펙트 수명·이동은 `stepFx(sdt)`(히트스톱·배속 적용). 숫자 팝업·보상 비행·배너·토스트·플래시는 **실시간**이다(§6.10).

## 6.1 구조

| 층 | 정본 | Unity |
|---|---|---|
| 월드 이펙트 | `addFx(obj, life, upd)` → `FX[]`, `stepFx`에서 `k = min(1, t/life)`로 갱신, 끝나면 제거(L1552~1560) | Framework `EffectManager.Play(key, pos, parent)` 핸들. 루트는 `FxRoot`(월드 y = 0, 게임 좌표 그대로). 핸들 루트를 `DepthSorter`에 등록(§6.2.3) |
| 흔들림·히트스톱 | 전역 `shakeA/shakeT`, `hitStop`(최댓값 병합) | `MvFx.Shake(key)`, `MvFx.HitStop(key)` → `CameraRig`·`MvTime`([02 §2.2.4, §2.8](02_Visual.md)) |
| 숫자 팝업 | DOM `.pop` 최대 61개, 월드 점을 화면으로 투영 | UGUI `DamagePopLayer`(HUD 캔버스) TMP 풀 61 |
| 보상 비행 | DOM `.fly` CSS 전환 | UGUI `FlyIconLayer`, 실시간 트윈 |
| 배너·토스트·플래시 | DOM | UGUI(층 순서는 [05](05_Screens.md)) |
| 우다다 화면 | 캔버스 `drawWarp`·`drawWarpOut`·집중선 | `ScreenRig` 아래 `WarpOverlay`([02 §2.1.6](02_Visual.md)) |

호출 경로: `MV.Run`은 그림을 모른다. 사건(`RunEvent`: 적 처치, 벽 파괴, 투사체 적중, 관문 통과…)만 큐에 넣고, `MV.Fx.FxDirector`가 사건 종류 → `04_effect` 그룹 행(§6.4)을 찾아 재생한다. 오브젝트 종류별 맞음·부서짐 이펙트는 `105_objectKind`/`102_enemy`의 `hitEffect`·`breakEffect`(04_effect.key), 소리는 `hitSound`·`breakSound`(03_sound.key) 열에서 읽는다.

## 6.2 정본 2D 해석 규칙과 Unity 공통 방식

### 6.2.1 `drawFx` 해석표 (L3138~3151)

| three 객체 | 2D 그림 | 블렌딩 | 정렬 편향 |
|---|---|---|---|
| 빛 스프라이트(`sprite(color,size,opacity,add)`, `glowTex`) | 원형 그라데이션 `c` → `c+'99'`(알파 0.6)@0.45 → `c+'00'`, 반지름 `scale·0.5·s`. 알파 ≤ 0.01 또는 반지름 < 0.5 px이면 생략 | `add`면 가산(`lighter`), 아니면 일반 | −0.2 |
| 텍스처 없는 메쉬(상자·원기둥·구·8면체) | **회전 없는 사각형** `max(2px, sc.x·s) × max(2px, sc.y·s)`, 채움 = 재질색·불투명도, 1 px OLC 외곽선 | 일반 | −0.1 |
| `G.ring` 링 | 타원 **선** `rx = sc.x·0.9·s`, `ry = rx·0.33`, 굵기 `max(2, sc.x·0.2·s)` | **일반**(3D 재질은 가산이지만 2D는 무시) | −0.1 |
| 에너지 벽돌(`userData.energy`) | `brickBit` 그림, 반지름 `max(3, 0.2·s·sc.y/0.3)` | 일반 | −0.15 |
| 발도장 그룹 | `drawPawFx`(§6.3 표 13) | 일반 | −0.4 |
| 하악 파동 그룹 | `drawHissFx`(§6.3 표 14) | 일반 | −0.4 |

### 6.2.2 2D 해석 때문에 생기는 정본 모양(유지, D-49)

| 3D 의도 | 실제 화면 |
|---|---|
| 번개 원기둥(반지름 0.05) | 길이 × 0.05 m 직사각형이 **세로로 서 있음**(구간 방향 무시) |
| 성광 원기둥(0.35 × 22 m) | 화면 위로 뻗은 세로 막대 |
| 운석 구(0.7) | 주황 사각형 |
| 파편 상자 | 회전 없는 사각형(각도 무시) |
| 충격 링 가산 | 일반 블렌딩 타원선, 납작 비율 0.33 고정 |

### 6.2.3 Unity 공통 방식 (결정 대기 D-45)

권장: **혼합 방식**.

| 종류 | Unity 구현 |
|---|---|
| 점 입자(불꽃 sparks, 파편 debris, 먼지 dust, 꼬리 trail, 얼음 조각) | 프리팹 = `ParticleSystem`(렌더러만, 자체 시뮬 끔) + `FxParticleDriver`. 생성 시 정본 공식으로 입자 초기값을 만들고 **매 프레임 simDt로 C#이 위치·속도·크기·알파를 적분**해 `SetParticles`. 렌더 모드 Billboard(정렬 = 카메라 평면, [02 §2.1.3](02_Visual.md)), 재질 `MV/FxGlow`(방사 그라데이션 텍스처) 또는 `MV/FxRect`(사각형+1px 외곽선) |
| 모양 이펙트(boom 원, 충격 링, 번개, 발도장, 하악 파동, 화염구, 운석, 성광) | 프리팹 = 빌보드 `SpriteRenderer`·절차 메쉬 묶음 + `FxShapeDriver`(k 곡선 적용). 타원 선은 셰이더 `MV/FxEllipseStroke`(반지름·비율·굵기 속성) |
| 정렬 | 핸들 루트 `SortingGroup`(레이어 `World`, rank 3) + `DepthSorter` 매 프레임 갱신, 편향 = `04_effect.sortBias` |
| 안개·틴트 | 안개 없음([02 §2.4](02_Visual.md)). 틴트는 받음(월드 층 안) |
| 시간 | `MvTime.simDt`. `EffectRequest.unscaledTime = false`이고 Framework 기본 시간 대신 드라이버가 simDt로 직접 진행(히트스톱 0.12배 반영) |
| 크기 단위 | 정본 three 크기(m) 그대로. 화면 px 하한(`max(2px, …)`)은 기준 해상도에서 m로 환산해 굽는다 |
| 난수 | 표시 전용 `UnityEngine.Random`. `rnd(a,b) = a + random·(b − a)` |
| 좌표 | 정본 three z는 부호를 바꾼다. 아래 표의 속도·위치는 **이미 게임 좌표(z 전진 +)로 바꾼 값**이다 |

`EffectRequest` 사용: `effectType = 04_effect.key`, `parent = FxRoot`, `localPosition = 게임 좌표`, `sortingLayer = "World"`, `sortingOrder`는 초기값(곧 `DepthSorter`가 덮음), `onEnded`로 풀 반환. 그룹 행은 `FxDirector`가 자식 행을 각각 `Play`한다.

## 6.3 이펙트 함수 13종

표 공통: 「k」 = 경과 / 수명(0→1). 「s」 = 그 점의 1 m당 px. 위치 기준은 호출 인자(게임 좌표).

**표 1. `sparks(x,y,z,color,n,spd=4,size=0.35,life=0.6,grav=6)` 불꽃 — L1552~1559** · 키 `fx.sparks` 4001001

| 항목 | 값 |
|---|---|
| 개수 | n. 단, 살아 있는 FX가 700 초과면 `min(n, 4)` |
| 입자 | 빛 스프라이트 가산, 크기 `size·rnd(0.6, 1.2)` |
| 초기 속도 | `(rnd(−1,1), rnd(0.2,1.4), rnd(−1,1)) · spd · rnd(0.4, 1)` |
| 수명 | `life · rnd(0.7, 1.2)` |
| 갱신 | `v.y −= grav·dt`, `pos += v·dt`, 알파 `1 − k` |
| Unity | 점 입자 드라이버, `MV/FxGlow` 가산 |

**표 2. `debris(x,y,z,color,n,size=0.22)` 파편 — L1560~1566** · 키 `fx.debris` 4001002

| 항목 | 값 |
|---|---|
| 입자 | 상자(size, 0.6size, 0.8size) → 2D 사각형 `size × 0.6size`, 불투명, OLC 1 px |
| 초기 위치 | `(x + rnd(−0.6, 0.6), y + rnd(0, 0.6), z)` |
| 초기 속도 | `(rnd(−3,3), rnd(3,7), rnd(−1,3))`(게임 z; 정본 three `rnd(−3,1)`) |
| 수명 | `rnd(0.6, 0.9)` |
| 갱신 | `v.y −= 16·dt`; `y < 0.1`이면 `y = 0.1`, `v = (v.x·0.5, −v.y·0.3, v.z·0.5)` 튐. 크기 `1 − 0.7k` |
| Unity | 점 입자 드라이버, `MV/FxRect` |

**표 3. `boom(x,y,z,color,radius,ring=true)` 폭발 — L1567~1578** · 키 `fx.boom` 4001006 (링 없음은 `flag = noRing`)

| 부품 | 값 |
|---|---|
| 바깥 빛 | 가산 스프라이트 color, 크기 `0.5 + radius·2.4·√k`, 알파 `1 − k`, 수명 0.45 |
| 속 빛 | 흰색 가산, 크기 `0.4 + radius·1.2·k`, 알파 `1 − k`, 수명 0.25 |
| 링(`ring`일 때) | y = 0.15(지면), 크기 `0.3 + radius·1.2·k`, 알파 `0.9·(1 − k)`, 수명 0.5, 2D 일반 블렌딩 타원선 |
| 불꽃 | `sparks(color, 14, radius·3, 0.45, 0.6, 4)` |
| Unity | 모양 프리팹(빛 2 + `MV/FxEllipseStroke`) + 자식 `fx.sparks` |

**표 4. `bolt(a, b)` 번개 — L1579~1592** · 키 `fx.bolt` 4001007

| 항목 | 값 |
|---|---|
| 경로 | a→b를 6구간, 중간 점마다 x·z `±0.35` 흔들기 |
| 구간 | 원기둥 반지름 0.05 `#bfeaff` → 2D 세로 사각형(§6.2.2) |
| 마디 빛 | 구간 시작점마다 `#7fd6ff` 크기 0.9, 알파 0.8 |
| 끝 빛 | b에 `#bfeaff` 크기 2.2 |
| 수명·알파 | 0.22 s, 알파 `(1 − k)·(random < 0.25 ? 0.4 : 1)` 매 프레임 깜빡임 |

**표 5. `frostHit` 얼음 — L1593~1601** · 키 `fx.frost` 4001014 (**호출 없음, 미구현**, D-46)

| 항목 | 값 |
|---|---|
| 조각 | 8개 8면체 `#dff7ff` 0.08×0.24, 속도 `(rnd(−3,3), rnd(1,4), rnd(−2,2))`, 중력 9, 수명 0.55 |
| 빛 | `#aee9ff` 2.4 → `+1.5k`, 알파 0.8, 0.4 s |

**표 6. `impact` 투사체 적중 — L1605~1608** · 키 `fx.impact` 4001005

| 항목 | 일반 | 치명 |
|---|---|---|
| 색·크기 | 흰색 1.0 | `#ffb07a` 1.7 |
| 수명 | 0.08 s | 0.13 s |
| 갱신 | 크기 `sz·(1 + 0.9k)`, 알파 `0.95·(1 − k)`, 위치 z − 0.05(카메라 쪽, 게임 좌표) | 같음 |

**표 7. `dust(x,y,z,n=6)` 먼지 — L1609~1613** · 키 `fx.dust` 4001003

| 항목 | 값 |
|---|---|
| 입자 | 일반 블렌딩 빛 스프라이트 `#d8cbb4`, 알파 `0.55·(1 − k)` |
| 위치 | `(x ± 0.8, y + rnd(−0.3, 0.4), z ± 0.3)` |
| 속도 | `(rnd(−1.4,1.4), rnd(0.4,1.4), rnd(−0.6,0.6))`, 중력 없음 |
| 수명·크기 | `rnd(0.45, 0.7)`, 크기 `(1 + 1.8k)·1.1` |

**표 8. `trail(b)` 투사체 꼬리 — L1614~1619, 생성 간격 L2113** · 키 `fx.trail` 4001004

| 항목 | 값 |
|---|---|
| 조건 | 원소 장난감(`b.el`)만, 0.03 s마다 1개 |
| 색 | fire `#ff8a3a` / bolt `#8fdcff` / 그 외 `#bfefff` |
| 크기·알파 | fire 0.75 / 그 외 0.55, 알파 0.9, 크기 `size·(1 − 0.6k)` |
| 수명 | 0.3 s, fire는 위로 1.5 m/s |

**표 9. `fireball(x,z,size=1.6)` 화염구 — L2152~2156** · 키 `fx.fireball` 4001008

| 항목 | 값 |
|---|---|
| 그림 | 가산 빛 `#ff6a2a` 크기 size + `#ffd27a` size·0.5 |
| 이동 | `(x+2.5, 9, z−3)` → `(x, 0.6, z)` 선형 0.22 s(뒤 위에서 앞으로 떨어짐) |

**표 10. `holyBeam` 성광 — L2190~2201** · 키 `fx.holyBeam` 4001012 (예약, 카드 획득 불가)

| 부품 | 값 |
|---|---|
| 기둥 | 원기둥 `#fff6d0` 지름 0.35 × 길이 22(플레이어 앞 z+0.5부터 전진 방향), 가산, 알파 `1 − k`, x·z 크기 `0.35·(1 + 0.5k)`, 수명 0.45 |
| 바깥 | `#ffc94a` 0.9, 알파 `0.5·(1 − k)`, 크기 `0.9·(1 + k)` |
| 불꽃 | 2 m 간격 10회 `sparks(#ffe08a, 2, 3, 0.45, 0.5, −2)` |
| 섬광 | 시작점 흰 빛 3, 0.3 s + `shake(0.12, 0.2)` |

**표 11. `meteor(tg)` 운석 — L2202~2213** · 키 `fx.meteor` 4001013 (예약)

| 부품 | 값 |
|---|---|
| 이동 | `(tx+6, 22, tz−10)` → `(tx, 0.8, tz)`, 진행 `k²`, 0.6 s |
| 몸 | 구 `#ffb36b` 0.7(2D 주황 사각형) + 가산 `#ff5a1f` 4, 알파 0.9 |
| 꼬리 | 0.02 s마다 `#ff7a2a` 2.2, 알파 0.8, 수명 0.45, 크기 `2.2·(1 − 0.5k)` |
| 착탄 | `boom(#ff5a1f, 3.6)` + `boom(#ffd27a, 2.2, false)` + `debris(#7a5a40, 10, 0.25)` + `shake(0.5, 0.35)` |

**표 12. 우물 벽돌 덩이 `flushBricks` — L1861~1880, 호출 L1471** · 키 `fx.brickChunk` 4001011

| 항목 | 값 |
|---|---|
| 배출 | 대기분 있으면 0.22 s마다(또는 플레이어가 우물을 0.5 m 지나치면 즉시) `n = min(4, 1 + floor(pend/(need·0.1)))` 덩이. 오브젝트가 컬링 범위 안일 때만([02 §2.2.5](02_Visual.md)). 보이는 벽돌이 없으면 `flushOre`(60 ms 간격 비행만) |
| 덩이 | 에너지 벽돌, 색 `ENERGY_C[idx % 4]` = `#c8643c`, `#d9874a`, `#b8543a`, `#e0a060`, 크기 0.48×0.28 m |
| 튀기 | 속도 `(dirX·rnd(1.5,3), rnd(4,6.5), rnd(−1.5,1))`, 중력 14, 0.26 s |
| 비행 | 그 뒤 화면 HUD 게이지(`#gBar`)로 `flyTo`(§6.8). 마지막 덩이 도착 콜백에서 게이지 가산 + 게이지 깜빡임 |
| 덧불꽃 | `sparks(#ffb04a, 4, 3, 0.3, 0.35)` at `(o.x, 1.4, o.z − 0.8)` |

**표 13. `pawStamp(x,z)` 발도장 — 생성 L3745, 그림 `drawPawFx`** · 키 `fx.paw` 4001009

| 항목 | 값 |
|---|---|
| 위치·수명 | y 0.1, 0.55 s |
| 떨어짐 | `drop = k < 0.35 ? 1 − k/0.35 : 0`, 화면 y −= `drop·6s` px(위에서 내려옴), 크기 `sc = 1.25s·(1 + 0.5·drop)` |
| 알파 | `k < 0.7 ? 1 : 1 − (k − 0.7)/0.3` |
| 착지 링 | `drop = 0`부터 흰 타원선 알파 0.6, `rx = 2.4s·(0.6 + k)`, `ry = 0.7s·(0.6 + k)` |
| 발 | 패드 `#f6b65e` 0.85×0.62 + 젤리 `#f4a6b4`, 발가락 4개 `(−0.55,−0.42)`, `(−0.2,−0.62)`, `(0.2,−0.62)`, `(0.55,−0.42)` 크기 0.2×0.17 + 속 0.11×0.09, 외곽선 `max(1.5, 0.06s)` OLC |
| Unity | 빌보드 스프라이트 2장(발 그림 + 링) + `FxShapeDriver` |

**표 14. `hissWave(x,z)` 하악 파동 — 생성 L3746, 그림 `drawHissFx`** · 키 `fx.hiss` 4001010

| 항목 | 값 |
|---|---|
| 위치·수명 | y 0.6, 0.7 s |
| 반지름 | `r = (0.8 + 5k)·s`, 알파 `1 − k` |
| 타원 3겹 | (배수, 색, 굵기 m) = (1, `#c8a8ff`, 0.35), (0.8, `#ffffff`, 0.18), (0.6, `#9a7ad6`, 0.12), 납작 비율 0.55 |
| 빛살 | 10줄, 반지름 0.7r → 1.1r, 흰색, 굵기 `max(1.5, 0.08s)` |

**dead code·잔재**: `kegPop`(L1853, 호출 없음), `nextLevel`의 불꽃(L1829, 도달 불가)은 만들지 않는다(D-46).

## 6.4 `04_effect` 표

열(07에 기대하는 가정): `id, key, type, prefab(주소), poolSize, blend, sortBias, color, count, speed, size, life, grav, radius, flag, children, y, anchor, chance, shake, hitStop, 근거`. 비어 있으면 함수 기본값. 색 토큰: `$tier`(카드 등급색, 115_cardTier), `$elem`(원소색 fire `#ff7a2a` / bolt `#9fe0ff`), `$rew`(보상별 분기, 자식 행에 조건 `flag`). `anchor` = 위치 기준(`hit`: 사건 좌표, `obj`: 오브젝트, `player`, `climbTop`). `y`는 절대 높이(지면 기준, 계단 중이면 + 현재 WY).

### 6.4.1 기본 종류 (그룹 001)

| id | key | type | prefab | poolSize | blend | sortBias | 기본값 |
|---|---|---|---|---|---|---|---|
| 4001001 | fx.sparks | particle | Fx/Sparks | 24 | add | −0.2 | n 8, spd 4, size 0.35, life 0.6, grav 6 |
| 4001002 | fx.debris | particle | Fx/Debris | 16 | normal | −0.1 | n 6, size 0.22 |
| 4001003 | fx.dust | particle | Fx/Dust | 16 | normal | −0.2 | n 6 |
| 4001004 | fx.trail | particle | Fx/Trail | 64 | add | −0.2 | life 0.3 |
| 4001005 | fx.impact | shape | Fx/Impact | 24 | add | −0.2 | size 1.0, life 0.08 |
| 4001006 | fx.boom | shape | Fx/Boom | 12 | add | −0.2 | radius 1.5, ring 1 |
| 4001007 | fx.bolt | shape | Fx/Bolt | 8 | add | −0.1 | life 0.22 |
| 4001008 | fx.fireball | shape | Fx/Fireball | 8 | add | −0.2 | size 1.6, life 0.22 |
| 4001009 | fx.paw | shape | Fx/Paw | 8 | normal | −0.4 | life 0.55 |
| 4001010 | fx.hiss | shape | Fx/Hiss | 4 | normal | −0.4 | life 0.7 |
| 4001011 | fx.brickChunk | particle | Fx/BrickChunk | 16 | normal | −0.15 | life 0.26, grav 14 |
| 4001012 | fx.holyBeam | shape | Fx/HolyBeam | 0 | add | −0.2 | 예약 |
| 4001013 | fx.meteor | shape | Fx/Meteor | 0 | add | −0.2 | 예약 |
| 4001014 | fx.frost | particle | Fx/Frost | 0 | add | −0.2 | 예약(D-46) |

`fx.boom`의 링 없는 변형은 같은 행에 `flag = noRing`으로 호출한다(자식 행에서 지정). poolSize는 [창작] 초깃값 — §6.14 예산으로 검증 후 조정.

### 6.4.2 런 사건 (그룹 002) — 정본 호출 지점

| id | key | 구성(자식: 종류·색·수·속도·크기·수명·중력 / y) | 흔들림·멈춤 | 줄 |
|---|---|---|---|---|
| 4002001 | run.transform | sparks `#fff3a0` 22·4.5·0.5·0.8 / y 1.2 | — | L1221 |
| 4002002 | run.towerHit | 확률 0.3: sparks `#ffc27a` 3·3·0.25·0.3 / (x±0.6, 1.2, z−1) | — | L1912 |
| 4002003 | run.enemyKill | debris `#5a8a3a` 8·0.2 / 0.8 + sparks `#ffd65a` 12·5·0.4·0.6 / 1 + dust 4 / 0.8 | hitstop.enemyKill | L1925 |
| 4002004 | run.wallHit | debris 1개 색 `$rew`(soul `#5a5490` / 그 외 `#93673f`) 크기 0.16 / 1.4 | — | L1933 |
| 4002005 | run.wallBreak | debris `$rew` 14·0.32 / 1.3 + debris `#5b606c` 4·0.22 / 1.6 + dust 7 / 1.0 + boom `#ffd6a0` 1.5 noRing / 1.2 | shake.wallBreak, hitstop.wallBreak | L1935~1937 |
| 4002006 | run.wallRewardSoul | sparks `#5fd6c9` 16·4·0.5·0.9 / 2 | — | L1938 |
| 4002007 | run.wallRewardDia | sparks `#7feaff` 12·4·0.45·0.8 / 2 | — | L1939 |
| 4002008 | run.coinHit | sparks (gem `#5fd6c9` / coin `#ffd65a`) 2·3·0.3 / 0.8 | — | L1961 |
| 4002009 | run.coinBreak | dust 4 / 0.6 + sparks 금 `#ffd65a` 18·6·0.4·0.8 / 1 · 다이아 `#7feaff` 20·6·0.45·0.9 / 1.2 | hitstop.coinBreak | L1963~1966 |
| 4002010 | run.procBolt | bolt (hx±0.4, 4.5, hz) → (hx, 1, hz) | — | L1987 |
| 4002011 | run.procFire | sparks `$elem` 6·3·0.35·0.5·중력 −2 | — | L1988 |
| 4002012 | run.chainBolt | bolt 이전 대상 → 다음 대상 | — | L1997 |
| 4002013 | run.chainFire | boom `$elem` 반지름 `1.6 + 0.3·연쇄수` | — | L2000 |
| 4002014 | run.statGatePass | sparks `#ffb05a` 16·5·0.5·0.7·중력 2 / 1.5 | — | L2046 |
| 4002015 | run.cloneGatePass | sparks `#8fc0ff` 14·5·0.5·0.7·중력 2 / 1.5 | — | L2051 |
| 4002016 | run.steal | debris `#5a8a3a` 5·0.2 / 0.8 | shake.steal | L2063 |
| 4002017 | run.blocked | debris (벽 `#8a5a34` / 그 외 `#b7b0a4`) 6·0.25 / 1.2 | shake.blocked | L2068 |
| 4002018 | run.projHit | impact(일반/치명) @(b.x, 1, max(b.z, bz)) + sparks (치명 `#ff6a3d` 8·3.4·0.5 / 일반 `#ffffff` 4·3.4·0.3) 수명 0.3 중력 0 | — | L2107~2108 |
| 4002019 | run.burnTick | sparks `#ff7a2a` 3·2·0.4·0.45·중력 −3 / (x±0.5, 1, z−hd) | — | L2137 |
| 4002020 | run.orbHit | sparks `$elem` 3·4·0.3·0.3 | — | L2143 |
| 4002021 | run.spinHit | sparks `#f2c14e` (예약) | — | L2149 |
| 4002022 | run.trail | trail(투사체마다 0.03 s) | — | L2113 |
| 4002023 | run.brickFlush | brickChunk n개 + sparks `#ffb04a` 4·3·0.3·0.35 / 1.4 | — | L1861~1880 |

### 6.4.3 신(God) 연출 (그룹 003)

| id | key | 구성 | 흔들림·기타 | 줄 |
|---|---|---|---|---|
| 4003001 | god.storm | 1.7 s. 불구름(fire 원소만): 일반 빛 6장 `#4a2418` 크기 `rnd(2.6, 3.8)`, 구름 중심 (cx, 7.5, cz+1)에서 x±2·y−0.3~0.4·z±1, 알파 `0.9·min(1, (1−k)·4, k·8)`. 낙하 `due = min(4, floor((k − 0.12)/0.17) + 1)`회(대상 앞면 x±0.3): bolt 원소 → paw + dust 5 @y 0.5, fire 원소 → fireball 1.4. 매 낙하 boom 1.5 @y 0.6, 색 fire `#ff7a2a` / bolt `#9fe0ff` | — | L2157~2174 |
| 4003002 | god.blast | bolt 원소: hiss / fire: fireball 3개 크기 2.4 x·z±1. boom 4.4 @y 1(색 fire `#ff5a1f` / bolt `#9fe0ff`) + boom `#ffffff` 2.2 noRing @y 1.6 + 팝업 문구 | shake.godBlast, 플래시 0.35(60 ms 뒤 0) | L2175~2183 |

`113_god` 행이 원소(`$elem`)를 준다. 위치는 런 사건 좌표.

### 6.4.4 우다다(그룹 004)

| id | key | 구성 | 흔들림·기타 | 줄 |
|---|---|---|---|---|
| 4004001 | climb.lap | boom `$tier` 3 noRing / 꼭대기 y+2 + sparks `$tier` 24·7·0.55·1.1 / y+3 | shake.climbLap | L2276 |
| 4004002 | climb.tier | boom `$tier` 2.2 / y+0.6 + sparks `$tier` 16·5·0.5·0.9 / y+1 | shake.climbTier, 플래시 등급색 0.25(90 ms 뒤 0) | L2282 |
| 4004003 | climb.land | boom `#ffe08a` 2.2 | — | L2286 |
| 4004004 | climb.stage | (화면 연출) | shake.warpStage + 소리 `tierup_{n}`(03 key) | L3165 |

### 6.4.5 룰렛·화면(그룹 005)

| id | key | 구성 | 줄 |
|---|---|---|---|
| 4005001 | roulette.pick | boom `$tier` 1.8 noRing / y 1.4 | L2370 |
| 4005002 | ui.flyIcon | 보상 아이콘 비행(UGUI, prefab = `Ui/FlyIcon`, poolSize 24) | §6.8 |
| 4005003 | ui.flyCard | 카드 비행(prefab `Ui/FlyCard`, poolSize 4) | §6.8 |
| 4005004 | ui.damagePop | 숫자 팝업(prefab `Ui/DamagePop`, poolSize 61) | §6.7 |

## 6.5 히트스톱

| id(300_runConfig) | key | value | unit | 사건 | 줄 |
|---|---|---|---|---|---|
| 300005001 | hitstop.enemyKill | 0.03 | s | 적 처치 | L1925 |
| 300005002 | hitstop.wallBreak | 0.06 | s | 벽 파괴 | L1936 |
| 300005003 | hitstop.coinBreak | 0.03 | s | 동전·보석 파괴 | L1963 |

규칙: `hitStop = max(hitStop, t)`. 실시간 dt로 줄어든다. 런 모드에서만 gameDt·simDt를 `time.hitStopScale`(0.12)배([02 §2.8](02_Visual.md)). 카메라·팝업·비행은 영향 없음.

## 6.6 흔들림

| id(300_runConfig) | key | 진폭 amp(m) | 시간 time(s) | 사건 | 줄 |
|---|---|---|---|---|---|
| 300004001 / 300004002 | shake.wallBreak.amp / .time | 0.16 | 0.14 | 벽 파괴 | L1936 |
| 300004003 / 300004004 | shake.steal.amp / .time | 0.12 | 0.12 | 쥐에게 츄르를 빼앗김 | L2063 |
| 300004005 / 300004006 | shake.blocked.amp / .time | 0.35 | 0.3 | 막힘(벽·바리케이드에 부딪힘) | L2068 |
| 300004007 / 300004008 | shake.godBlast.amp / .time | 0.45 | 0.3 | 신 대폭발 | L2178 |
| 300004009 / 300004010 | shake.climbLap.amp / .time | 0.22 | 0.2 | 우다다 한 바퀴 | L2277 |
| 300004011 / 300004012 | shake.climbTier.amp / .time | 0.12 | 0.12 | 우다다 등급 오름 | L2282 |
| 300004013 / 300004014 | shake.warpStage.amp / .time | 0.18 | 0.2 | 우다다 단계 바뀜 | L3165 |
| 300004015 / 300004016 | shake.holyBeam.amp / .time | 0.12 | 0.2 | 성광(예약) | L2200 |
| 300004017 / 300004018 | shake.meteor.amp / .time | 0.5 | 0.35 | 운석(예약) | L2209 |

규칙: `shakeA = max(shakeA, amp)`, `shakeT = max(shakeT, time)`. 적용은 [02 §2.2.4](02_Visual.md).

`04_effect` 행의 `shake`·`hitStop` 열에는 위 key의 앞부분(`shake.wallBreak`, `hitstop.wallBreak`)을 쓴다.

## 6.7 숫자 팝업

### 6.7.1 정본 (`pop` L1702~1706, `stepPops` L1707~1711)

| 항목 | 값 |
|---|---|
| 개수 | 새로 넣은 뒤 `POPS.length > 60`이면 가장 오래된 것 제거 → **최대 61** |
| 글꼴 | Black Han Sans, `4.2·size` u(u = 스테이지 폭/100), 그림자 `0 2px 0 rgba(0,0,0,.7)` |
| 수명 | 0.8 s, **실시간** dt |
| 위치 | 월드 점(사건 좌표)을 매 프레임 `toScreen`(WY 더함, 표준 투영)으로 화면에 둔다. 월드 y가 초당 1.4 m 오른다. 카메라 뒤면 숨김 |
| 크기 곡선 | `k < 0.15 ? 1 + (0.15 − k)·3 : 1` → 시작 1.45배에서 0.12 s 동안 1로 |
| 알파 곡선 | `1 − max(0, (k − 0.6)/0.4)` → 0.48 s부터 0.32 s 동안 사라짐 |
| 피해 숫자 | 값 `min(99999, max(1, round(v)))`, `fmt`(천 단위 축약), 색 `opts.col`이 없으면 치명 `#ff6a3d` / 일반 `#ffffff` |
| 크기 인자 | 호출마다 `size`(기본 1). 호출 지점별 값·문구는 [05](05_Screens.md) 팝업 목록 |

### 6.7.2 Unity (D-48)

- HUD 캔버스(Screen Space – Camera 또는 Overlay)의 `DamagePopLayer`, TMP 풀 61개(`ui.damagePop`, poolSize 61). 61개 초과 요청 시 가장 오래된 것을 재사용(정본과 같음).
- 매 프레임 realDt로 진행: 월드 점 `(x, y + 1.4·t, z)` → `GameCamera.WorldToScreenPoint` → 캔버스 좌표. 뷰 깊이 ≤ 0이면 숨김.
- 크기 = 기준 글자 크기 `4.2·size` u × 곡선. 그림자는 TMP Underlay(아래 2 px, 알파 0.7).
- 월드 정렬·안개 없음(항상 HUD 아래·월드 위). 층 순서는 [05](05_Screens.md).

| id | key | value | unit | 근거 |
|---|---|---|---|---|
| 300007001 | pop.max | 61 | 개 | L1703 |
| 300007002 | pop.life | 0.8 | s | L1705 |
| 300007003 | pop.rise | 1.4 | m/s | L1708 |
| 300007004 | pop.scaleIn | 0.15 | k | L1709 |
| 300007005 | pop.scaleBoost | 3 | 배 | L1709 |
| 300007006 | pop.fadeFrom | 0.6 | k | L1709 |
| 300007007 | pop.fontU | 4.2 | u | L1704 |
| 300007008 | pop.critColor | #ff6a3d | color | 호출부 `hit()` |

## 6.8 HUD 보상 비행

### 6.8.1 정본

| 함수 | 시작 | 끝 | 시간·곡선 | 크기·알파 | 줄 |
|---|---|---|---|---|---|
| `flyTo(world, target, html, cb)` | 월드 점의 화면 위치 | 대상 HUD 요소 가운데 | 0.55 s, `cubic-bezier(.5,0,.8,.6)`(transform·opacity 함께) | 크기 1.3 → 0.6, 알파 1 → 0.4 | L1735~1741 |
| `flyTo2(from, target, html, cb)` | 요소 위 `top + 0.35h` | 대상 `top + min(h, 30px)` | 같음 | 1.2 → 0.7, 알파 유지 | L1719~1725 |
| 끝 | 560 ms(실시간 `setTimeout`)에 제거 + 콜백 | | | | |
| `flycard` | 룰렛 카드 → 손패 칸 | | 0.6 s, `cubic-bezier(.55,0,.75,.4)`, 620 ms에 제거 | | L563, L2384~2389 |
| `flyToWorld` | HUD 요소 | 월드 점 | **호출 없음**, 미구현(D-46) | 1.1 → 0.55 | L1712~1718 |

곡선 표본(시간 x → 진행 y) — 구현 검산용:

| x | 0.1 | 0.2 | 0.3 | 0.4 | 0.5 | 0.6 | 0.7 | 0.8 | 0.9 |
|---|---|---|---|---|---|---|---|---|---|
| fly `(.5,0,.8,.6)` | 0.008 | 0.034 | 0.078 | 0.141 | 0.227 | 0.335 | 0.467 | 0.624 | 0.804 |
| flycard `(.55,0,.75,.4)` | — | 0.033(@.25) | — | — | 0.164 | — | 0.471(@.75) | — | — |

느리게 시작해 끝에서 빨라지는 「빨려 들어가는」 곡선이다. Unity는 같은 4점 베지어를 x→t 뉴턴 풀이로 계산한다(DOTween 기본 Ease로 대체하지 않는다).

아이콘별 크기(u): 금화 4, 영혼 4×5, 다이아 4.4×5, 계단 4.4 + 글자 `+n`(6u, 색 `--stair`), 동료 관문 `min(n, 5)`개를 70 ms 간격. 대상: 금화·영혼·다이아 = 상단 재화 칸, 계단 = 계단 칸, 우물 벽돌 = 게이지 `#gBar`. 호출 지점·대상 요소는 [05](05_Screens.md)에 기대.

### 6.8.2 도착 시점과 규칙 값

정본은 **도착 콜백에서** 게이지·재화를 더하는 경우가 있다(우물 벽돌 게이지, 일부 재화). 표시만이 아니라 값 반영 시점(0.56 s 실시간 지연)이 규칙과 얽힌다. [04](04_GameRules.md)에 기대하는 가정: `MV.Run`은 즉시 값을 확정하고, HUD 숫자 표시만 도착 때 올린다. 단 우물 게이지처럼 정본이 도착 때 가산해 다음 판정에 영향을 주는 것은 04가 정본 순서를 따로 정한다.

| id | key | value | unit | 근거 |
|---|---|---|---|---|
| 300007009 | fly.time | 0.55 | s | L144 |
| 300007010 | fly.removeMs | 560 | ms | L1740 |
| 300007011 | fly.bezier | .5,0,.8,.6 | 곡선 | L144 |
| 300007012 | fly.startScale | 1.3 | 배 | L1738 |
| 300007013 | fly.endScale | 0.6 | 배 | L1739 |
| 300007014 | fly.endAlpha | 0.4 | 0~1 | L1739 |
| 300007015 | fly.cloneGapMs | 70 | ms | L2052 |
| 300007016 | flyCard.time | 0.6 | s | L563 |
| 300007017 | flyCard.removeMs | 620 | ms | L2389 |
| 300007018 | flyCard.bezier | .55,0,.75,.4 | 곡선 | L563 |

## 6.9 배너·토스트·플래시

| 연출 | 정본 | Unity | 키 |
|---|---|---|---|
| 배너(`banner(big, small)`) | 키프레임 `bn` 1.5 s ease-out: 0% 알파 0·크기 1.4 → 15% 알파 1·크기 1 → 75% 알파 1 → 100% 알파 0. 큰 글자 9.4u, 작은 글자 4.4u 금색. 1500 ms 뒤 숨김. 새 배너는 애니메이션·타이머 재시작(L2450~2452) | UGUI 패널 + 실시간 키프레임 트윈. CSS `ease-out`은 키프레임 **구간마다** 따로 적용되므로 0~15%, 15~75%, 75~100% 각 구간에 `cubic-bezier(0,0,.58,1)`을 쓴다 | `banner.time` 1.5 |
| 토스트(`toast(msg)`) | 화면 높이 58% 위치, 4u 글자, 패널 배경, 알파 전환 0.2 s, 실시간 1.4 s 뒤 사라짐. 새 토스트는 타이머 재시작 | UGUI, 문구 `toastMessage` 컬렉션 | `toast.time` 1.4, `toast.fade` 0.2 |
| 플래시 `#flash` | 흰 전체 화면, `opacity` 전환 0.25 s(CSS 기본 `ease`). 신 대폭발: 0.35로 놓고 60 ms 뒤 0. 등급 오름: 배경색을 등급색으로, 0.25로 놓고 90 ms 뒤 0, 배경은 그때 `''`로 돌아가 **흰색으로 사라짐** | UGUI 전체 화면 Image, 실시간 | 아래 |

**플래시 실제 최고값** [추정: 전환 도중 반대 방향 전환은 CSS 「reversing shortening」으로 남은 시간이 줄어드는 규칙]: 0→0.35 전환이 60 ms(0.24 진행, ease 0.386) 지점에서 꺾이므로 화면 최고 알파 ≈ **0.135**, 등급 오름은 90 ms(0.36 진행, ease 0.621) → ≈ **0.155**. 그 뒤 0으로 가는 시간은 짧아진 전환(올라간 만큼)이다. Unity는 「목표값 + ease 0.25 s 전환, 도중 반전」을 그대로 구현하면 자동으로 같다(D-49).

| id | key | value | unit | 근거 |
|---|---|---|---|---|
| 300008001 | banner.time | 1.5 | s | L248~250, L2452 |
| 300008002 | toast.time | 1.4 | s | L2449, L3828 |
| 300008003 | toast.fade | 0.2 | s | L251 |
| 300008004 | flash.fade | 0.25 | s | L146 |
| 300008005 | flash.godBlast | 0.35 | 0~1 | L2179 |
| 300008006 | flash.godBlastMs | 60 | ms | L2179 |
| 300008007 | flash.climbTier | 0.25 | 0~1 | L2283 |
| 300008008 | flash.climbTierMs | 90 | ms | L2283 |

층: 플래시는 토스트 위, 결과 화면 아래, z-index 4~6 HUD 요소 아래([05](05_Screens.md)에 기대).

## 6.10 시간축 정리

| 대상 | 시간 | 히트스톱 영향 |
|---|---|---|
| 월드 이펙트(§6.3), 신 연출, 우물 벽돌 덩이 | simDt | 0.12배(런만) |
| 흔들림 감소, 히트스톱 감소, 카메라 | realDt | 없음 |
| 숫자 팝업 | realDt | 없음 |
| 보상 비행·카드 비행·배너·토스트·플래시 | 실시간 ms | 없음 |
| 우다다 화면: 단계 진행 `C.t`, 지붕·계단 흐름 `T` | 계단 오르기 simDt | 해당 없음(런 아님) |
| 우다다 화면: 별 깜빡임, 단계 이름 1.8 s, 종료 섬광 700 ms, 집중선 | 실시간 ms | 없음 |

## 6.11 우다다 화면 (`WARP_ST` L3155, `drawWarp` L3163~3196)

계단 오르기 중 월드를 덮는 화면 공간 연출. `ScreenRig` `Warp` 층([02 §2.1.6](02_Visual.md)). 좌표는 실제 뷰포트 W·H 비례, px 상수는 기준 px.

### 6.11.1 단계 `330_climb` (정본 `WARP_ST`)

| id | at(칸) | 이름(`name` 컬렉션) | accent | sky1 | sky2 | sky3 | night |
|---|---|---|---|---|---|---|---|
| 330001001 | 0 | 우다다 시작 | #ffd84a | #8ac8f0 | #c8e4f4 | #f4ead8 | 0 |
| 330001002 | 25 | 지붕 위 질주 | #ff9a5a | #e8806a | #f8b880 | #ffe0b0 | 0.2 |
| 330001003 | 75 | 달빛 계단 | #9ab8ff | #1c1e4e | #3a3a7a | #6a5a9a | 0.65 |
| 330001004 | 150 | 보름달 점프 | #fff0a0 | #0e1032 | #2a2a6a | #4a4a8a | 0.8 |

단계 = 표시 칸 수 `C.shown`이 넘은 가장 큰 `at`. 바뀌면 `climb.stage`(흔들림 0.18·0.2 + 소리 `tierup_{n}`) + 단계 이름 표시.

### 6.11.2 그림 순서와 식

| 순서 | 부품 | 식 |
|---|---|---|
| 0 | 전체 알파 | `a = min(1, C.t/0.35)`(들어올 때 0.35 s 페이드) |
| 1 | 속도 | `sp = 1 + st·0.35 + min(1.5, T·0.08)`, 소실점 `VX = W/2`, `VY = H·0.2` |
| 2 | 하늘 | 세로 그라데이션 `sky1`(0) → `sky2`(0.5) → `sky3`(1), 높이 `H·0.6`, 그 아래는 `sky3` |
| 3 | 별 | `night > 0.5`일 때 50개 2 px, 알파 `0.5 + 0.5·sin(now·3 + i)`, 위치 해시, `y < 0.45H` |
| 4 | 해·달 | 정본 식(L3170~3174): `night < 0.5` 해(accent 원 + 빛), 아니면 달(원 + 그늘 원) |
| 5 | 지붕 | 양쪽 9채, `raw = i/9 + T·sp·0.22 + (sd > 0 ? 0.05 : 0)`, `k = frac(raw)`, `kk = k²`. `y = VY + (H·1.1 − VY)·kk`, `hw = W·0.07 + W·0.62·kk`, `x = VX + sd·(hw + W·0.04 + W·0.25·kk)`, `bw = W·(0.06 + 0.3kk)`, `bh = H·(0.03 + 0.2kk)`. 색은 지붕·벽 기본색을 `night`만큼 남색으로 섞음. 지붕 평행사변형 + 창 |
| 6 | 계단 | `N = 16`, `ph0 = (T·sp·1.5) % 1`, 칸 `k = (j + ph0)/N`, `yk = VY + (H·1.08 − VY)·k^1.9`, `hk = W·0.06 + W·0.58·k^1.9`. 7칸마다 굴림, 4칸마다 띠 색 `tc` |
| 7 | 빛 | `ha(accent, 0.7)` 반지름 `W·0.25` 가산 @소실점 |
| 8 | 눈 | 단계 ≥ 2: 30/70개, 속도 1.6 |
| 9 | 단계 이름 | 1.8 s 실시간, 알파 `a·min(1, dtx·4)·min(1, (1.8 − dtx)·2)`. 글자 900, 크기 `min(W,H)·0.068·(1 + max(0, 0.25 − dtx))`, OLC 테두리 `fs·0.22`, y `H·0.262` |
| 10 | 고양이 | 플레이어·친구를 `WarpCat` 층으로 다시 그림(정본 `shipF`) |

### 6.11.3 종료·집중선

| 연출 | 식 |
|---|---|
| `drawWarpOut` | 700 ms 실시간. `wo = 경과/700`. 흰 화면 알파 `(1 − wo)·0.85`. 링 accent 색 굵기 `14·(1 − wo) + 2` 기준 px, 중심 `(W/2, H·0.62)`, 반지름 `hypot(W, H)·wo·0.7` |
| 집중선 | 우다다 중 18줄. `t = floor(perf_ms/40)`, 각 `hash(i·1.7 + t·0.13)·2π`, 시작 반지름 `min(W,H)·(0.42 + hash(i + t)·0.1)`, 길이 `H·0.2`, 굵기 `1 + hash(i·3 + t)·3` 기준 px, `rgba(255,255,255,.6)`, 중심 화면 가운데 |

Unity: `WarpOverlay` = 하늘 그라데이션 쿼드 + 별·지붕·계단·집중선은 C#이 매 프레임 정점 생성하는 동적 메쉬 1~2개(`MV/ScreenVertexColor`), 해·달·빛은 스프라이트, 단계 이름 TMP. 크기 계산은 C# `double` 해시.

| id | key | value | unit | 근거 |
|---|---|---|---|---|
| 300008009 | warp.fadeIn | 0.35 | s | L3166 |
| 300008010 | warp.vanishY | 0.2 | H 비율 | L3166 |
| 300008011 | warp.roofCount | 9 | 채 | L3177 |
| 300008012 | warp.stepCount | 16 | 칸 | L3183 |
| 300008013 | warp.stageNameMs | 1800 | ms | L3193 |
| 300008014 | warp.outMs | 700 | ms | L3197~3209 |
| 300008015 | warp.speedLines | 18 | 줄 | L3816 |

## 6.12 잔재 이펙트 판정

| 이름 | 정본 상태 | 판정 |
|---|---|---|
| `frostHit` | 함수만 있고 호출 없음 | 미구현, 키 `fx.frost` 예약(poolSize 0) |
| `flyToWorld` | 함수만 있고 호출 없음 | 미구현 |
| `kegPop` | 호출 없음 | 미구현, 키 없음 |
| `nextLevel` 불꽃(L1829) | 도달 불가 경로 | 미구현 |
| `holyBeam`, `meteor` | 카드 획득 경로 없음 | 키·예산만 예약, 프리팹은 카드 해금 때 |
| 회전 장난감 불꽃(L2149) | 카드 획득 경로 없음 | `run.spinHit` 예약 |
| 적 피격 깜빡임(updObjMesh) | 3D 재질만 바뀌어 2D 화면에 안 보임 | 미구현 |

## 6.13 오브젝트 뷰 반응 (참고, `updObjMesh` L1464~1539)

이펙트는 아니지만 MV-FX가 함께 만드는 뷰 반응. 오브젝트 뷰 프리팹 스크립트가 처리.

| 반응 | 정본 | 화면에 보이는 것 |
|---|---|---|
| 맞음 펀치 | `punch` dt·9로 감소, 크기 `(1 + 0.07k, 1 − 0.08k)` | 2D에선 **글자판만** 커짐(월드 스케일 반영) |
| 벽 흔들림 | 0.12 s, x += `sin(shake·90)·0.05` + 흰 타원 1.1×0.8 알파 0.35 | 그대로 |
| 탑 흔들림 | 벽돌 더미 x `sin(shake·80)·0.06` | 그대로 |
| 관문 번쩍 | 0.1 s, 그라데이션 알파 `aa`(0.67) ↔ 평소 `55`(0.33) | 그대로 |
| 관문 가라앉음 | 통과 뒤 `sink += dt`, 안쪽 y = −sink·6, 0.5 넘으면 숨김 | 그대로 |
| 동전·보석 크기 | `0.55 + 0.45·hp/max` | 그대로 |
| 우물 벽돌 | 다 빠지면 2.2 s 뒤 dt·5로 다시 자람 | 그대로 |

## 6.14 성능 예산

| 항목 | 예산 | 근거 |
|---|---|---|
| 동시 월드 이펙트 핸들 | 120 | [창작] 정본 상한 장치는 「FX 700개 초과 시 불꽃 4개」뿐 |
| 동시 입자(점 입자 전체) | 700 — 넘으면 sparks 개수 `min(n, 4)`(정본 규칙) | L1563 |
| `SetParticles` 호출 | 프레임당 활성 입자 시스템 수(≤ 40 목표) | [창작] |
| 숫자 팝업 | 61 | 정본 |
| 비행 아이콘 | 24 동시 | [창작] 동료 관문 5 + 재화 연속 |
| 드로콜(월드 FX) | ≤ 30: 같은 재질(`MV/FxGlow` 가산·일반, `MV/FxRect`, 타원선) 4종으로 묶음 | [창작] |
| 과그리기 | 빛 스프라이트 화면 면적 합 ≤ 화면 3장분 | [창작] boom 반경 4.4가 가장 큼 |
| GC | 재생 중 할당 0(풀, 고정 배열) | Framework 풀 |
| 측정 | 개발 빌드 HUD에 활성 핸들·입자 수 표시. 기기 측정은 MV-QA 단계 | |

| id | key | value | unit | 근거 |
|---|---|---|---|---|
| 300006001 | fx.sparkCapThreshold | 700 | 개 | L1563 |
| 300006002 | fx.sparkCapCount | 4 | 개 | L1563 |
| 300006003 | fx.maxHandles | 120 | 개 | [창작] |
| 300006004 | fx.trailInterval | 0.03 | s | L2113 |
| 300006005 | fx.brickFlushInterval | 0.22 | s | L1471 |
| 300006006 | fx.towerHitChance | 0.3 | 확률 | L1912 |

## 6.x 의문점·결정 필요

| ID | 질문 | 선택지 | 권장안 | 영향 |
|---|---|---|---|---|
| D-45 | 월드 이펙트 구현 방식 | (가) 혼합: 점 입자는 렌더 전용 ParticleSystem + C# 적분, 모양은 스프라이트·절차 메쉬 / (나) 전부 Shuriken 모듈 설정으로 근사 / (다) 전부 SpriteRenderer 오브젝트 풀 | **(가)** | 정본 공식(속도 분포·튐·알파 곡선)을 그대로 옮기고 히트스톱 0.12배·배속을 정확히 따른다. (나)는 아트가 편집하기 쉽지만 튐·`k` 곡선이 근사가 되고 simDt 제어가 어렵다. (다)는 입자 수백 개에서 Transform 비용이 크다 |
| D-46 | 잔재 이펙트 | frostHit·flyToWorld·kegPop·nextLevel 불꽃 미구현, holyBeam·meteor·spinHit 키 예약 / 전부 구현 | **미구현 + 키 예약** | 작업량이 줄고 예약 키로 나중에 카드 해금 시 프리팹만 추가한다. 01 범위의 「도달 불가 코드 제외」와 같은 기준 |
| D-47 | 시간축 | 정본대로(이펙트만 히트스톱, 팝업·비행·배너·플래시 실시간) / 전부 게임 시간 | **정본대로** | 손맛(멈칫하는 동안 숫자는 계속 떠오름)이 같다. 일시정지 화면에서는 실시간 연출도 함께 멈춘다 — 이 부분은 [05](05_Screens.md)의 일시정지 정의를 따른다 |
| D-48 | 숫자 팝업·보상 비행 구현 층 | UGUI 오버레이 TMP 풀 / 월드 TMP(카메라 평면) | **UGUI 오버레이** | 정본처럼 HUD 글꼴 크기 고정(거리와 무관), 월드 정렬·틴트 영향 없음, 비행 끝점이 HUD 요소라 같은 캔버스가 쉽다. 월드 TMP로 하면 먼 피해 숫자가 작아지고 틴트로 어두워진다 |
| D-49 | 2D 해석 부산물 | 번개 세로 막대·링 비율 0.33 일반 블렌딩·파편 무회전·플래시 실제 최고값(0.135/0.155) 그대로 / 의도대로(구간 방향 번개, 가산 링, 플래시 0.35/0.25 최고) | **정본 그대로** | 「같은 화면」 판정([12](12_Acceptance.md))과 맞는다. 의도대로 고치면 번개·폭발·플래시가 정본보다 강해지므로 [창작]으로 표시하고 QA 기준 화면을 다시 찍는다 |
