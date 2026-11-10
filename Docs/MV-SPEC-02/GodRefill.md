# 별자리냥 3~12번 다시 채우기 — 데이터 가안

작업 `MV-SPEC-02` 결정 Q-20 A(사용자 2026-11-10 「네, 새로 채움」): `113_god` 3~12번의 **id는 유지**하고, 새 기준 순서(원작 신 Poseidon … Shiva)대로 이름·테마·카드를 다시 채운다. 이 문서는 표에 넣기 전 가안이다. 아직 TSV·Resources·코드·MV-SPEC-01 본문을 고치지 않았다.

## 근거와 한계

- 새 기준 7-1 표는 별자리냥마다 **테마와 대표 스킬 2~3개**만 준다. 카드 7장의 효과·수치는 03 탭(별자리냥·대마법 주문카드 상세, 작성 예정)이 정할 자리다.
- 그래서 아래 카드 7장은 **[컨셉 가안]**: 대표 스킬을 지금 있는 효과 type(`112_cardEffect`)에 맞추고, 나머지 칸은 지금 묶음 틀(초록 orb · 파랑 proc·chain · 보라 storm · 주황 blast·frate·spear)을 그대로 쓴다. 맞는 type이 없는 스킬은 **신규 type 필요**로 표시하고 가장 가까운 type으로 임시로 둔다.
- 3~12번은 모두 `active` 0을 유지한다(새 기준: 1번만 오픈). 03 탭이 나오면 이 가안을 그 기준으로 고친다.

## 1. `113_god` 행

| id | 지금 key → 새 key | 새 이름(문구) | 원작 신 | element(새) | 메뉴(새 기준) | 바꿀 열 |
|---|---|---|---|---|---|---|
| 113001001 | thunder (유지) | 물병자리냥 | Zeus | bolt | 오픈 | 이름 문구만 |
| 113001002 | flame (유지) | 사자자리냥 | Ra | fire | 잠금 | 이름 문구, active·해금은 Q-21 |
| 113001003 | wind → `pisces` | 물고기자리냥 | Poseidon | water | 잠금 | key·element·color·portrait·passive |
| 113001004 | snow → `sagittarius` | 사수자리냥 | Artemis | arrow | 잠금 | 〃 |
| 113001005 | cloud → `scorpio` | 전갈자리냥 | Hades | poison | 잠금 | 〃 |
| 113001006 | sea → `aries` | 양자리냥 | Ares | war | 잠금 | 〃 |
| 113001007 | star → `virgo` | 처녀자리냥 | Aphrodite | light | 잠금 | 〃 |
| 113001008 | forest → `taurus` | 황소자리냥 | Atlas | earth | 잠금 | 〃 |
| 113001009 | shadow → `libra` | 천칭자리냥 | Kronos | time | 잠금 | 〃 |
| 113001010 | lightning → `capricorn` | 염소자리냥 | Thanatos | soul | 잠금 | 〃 |
| 113001011 | earth → `gemini` | 쌍둥이자리냥 | Odin | spear | 데이터만 | 〃 + 메뉴 숨김(Q-21) |
| 113001012 | fish → `cancer` | 게자리냥 | Shiva | cosmic | 데이터만 | 〃 + 메뉴 숨김(Q-21) |

- key를 바꾸는 이유: key가 테마와 어긋나면 이번 같은 혼선이 다시 생긴다. 바꾸지 않고 이름만 바꾸는 쪽도 가능하다(그러면 아래 「key를 쓰는 곳」 수정이 없다).
- color·passiveStat·portrait: color는 아트 시안이 나오면 정한다(그때까지 지금 값 유지). portrait는 `Tex_God_<Key>`로 이름만 바꾼다(그림은 디자이너 제작물). passive는 새 기준에 없어 지금 값을 유지한다.

## 2. `110_card` 묶음 3~12 (카드 7장, [컨셉 가안])

칸: 초록 = orb(tier 1), 파랑 = proc·chain(tier 2), 보라 = storm(tier 3), 주황 = blast·frate·spear(tier 4). ★ = 새 기준 대표 스킬.

| 묶음 | 별자리냥 | 초록 orb | 파랑 proc | 파랑 chain | 보라 storm | 주황 blast | 주황 frate | 주황 spear |
|---|---|---|---|---|---|---|---|---|
| 3 | 물고기자리냥 | 물방울 위성 Orbit | ★물 분신 CloneAdd | 물보라 ChainSplash | ★해일 Storm | 물 폭발 Blast | 물살 연사 FireRateMul | ★물 창 PierceEveryN |
| 4 | 사수자리냥 | 화살 위성 Orbit | ★다중 화살 DoubleChance (신규 MultiShot 필요) | 튕기는 화살 ChainJump | ★화살비 Meteor (신규 ArrowRain 권장) | ★폭발 화살 Blast | 속사 FireRateMul | 관통 화살 PierceEveryN |
| 5 | 전갈자리냥 | ★독 오라 Orbit (신규 Aura 필요) | ★독 베기 ProcBurn(독 지속 피해) | 독 튐 ChainSplash | ★독 운석 Meteor | 독 폭발 Blast | 독침 연사 FireRateMul | 독침 창 PierceEveryN |
| 6 | 양자리냥 | 투지 위성 Orbit | ★전쟁 버프 DmgMul | 돌격 연쇄 ChainJump | 돌격 소나기 Storm | 돌진 폭발 Blast | ★분노 버프 FireRateMul | ★연속 창 PierceEveryN |
| 7 | 처녀자리냥 | 빛 구슬 Orbit | ★매혹 ProcChill (신규 Charm 필요) | 빛 연쇄 ChainJump | 빛줄기 Beam | 빛 폭발 Blast | ★축복 FireRateMul | ★보호막 Shield (치유·버프) |
| 8 | 황소자리냥 | 바위 위성 Orbit | ★힘 DmgMul | 돌 파편 ChainSplash | 낙석 Meteor | ★지면 강타 Blast | 연타 FireRateMul | 바위 창 PierceEveryN |
| 9 | 천칭자리냥 | 시계 위성 Orbit | ★둔화 ProcChill | 시간 연쇄 ChainJump | ★시간 정지 ProcChill (신규 TimeStop 필요) | 시간 폭발 Blast | 가속 FireRateMul | 시침 창 PierceEveryN |
| 10 | 염소자리냥 | 영혼 위성 Orbit | ★처치 시 추가 효과 DropBonus (신규 OnKill 필요) | 영혼 연쇄 ChainJump | 영혼 소나기 Storm | 영혼 폭발 Blast | 영혼 연사 FireRateMul | 영혼 창 PierceEveryN |
| 11 | 쌍둥이자리냥 | 창 위성 Orbit | 창 찌르기 ProcExtraHit | ★분열 창 ChainSplash | 창 소나기 Storm | ★창 폭발 Blast | 연속 투척 FireRateMul | 관통 창 PierceEveryN |
| 12 | 게자리냥 | 별 위성 Orbit | 우주 섬광 ProcExtraHit | 성운 연쇄 ChainJump | ★우주 폭탄 Meteor | 초신성 Blast | 유성 연사 FireRateMul | ★관통 투사체 Pierce |

- 효과 수치(p1~p4)는 지금 같은 type 행 값을 그대로 쓴다. 효과 행이 묶음끼리 공유되므로 새 `112` 행은 신규 type이나 수치가 다른 경우에만 만든다.
- **신규 효과 type 6개**(MultiShot·ArrowRain·Aura·Charm·TimeStop·OnKill)는 코드(`MV.Run` 카드 효과 분기)가 필요하다. 가안에서는 가까운 type으로 두고, 신규 type은 03 탭 확정 뒤 별도 계약으로 만든다. 3~12번이 꺼져 있으니 지금 플레이에는 영향이 없다.

## 3. 표에 넣을 때 바뀌는 곳 (반영 계약 범위)

**반영 담당과 순서**: 이 가안의 표·Resources·시험 반영은 이 문서 작업(MV-SPEC-02)에서 하지 않는다. MoonVillage 총괄이 별도 반영 계약으로 잡고, 표 69개에 `name` 열을 넣는 계약 MV-DATA-NAME이 끝난 뒤에 한다(표 쓰기 겹침 방지, 2026-11-10 총괄 요청). key는 1절 가안대로 바꾼다(사용자 3차 결정, 2026-11-10). 메뉴는 10종·1번만 공개(Q-21), 11·12번은 메뉴에서 숨긴다.

| 대상 | 내용 |
|---|---|
| `TSV/113_god.txt` | 3~12행 key·element·portrait(필요하면 color) |
| `TSV/110_card.txt` | group 3~12 각 7행의 effectId(위 표). key는 칸 이름(orb…spear) 그대로 |
| `TSV/112_cardEffect.txt` | 필요한 경우만 행 추가(수치 다른 변형) |
| 문구 | `name:{id}`·카드 이름·설명 문구(현지화 미연결이라 지금은 08 기획서 표와 시트) |
| Resources 산출물 | TableBuilder로 다시 만듦(`Assets/Game/GameData/Table/Resources`, Unity 필요 — 실행기 또는 빌드 메서드) |
| 시험 | `MV.Data.Tests/MvContentDataTests.cs` `ByKey("wind")` 등 key 단언 고침. `MV.Editor.Tests/Import/MvResourceColumnTests.cs` portrait 이름 확인 |
| key를 쓰는 다른 곳 | 확인함: 3~12번 key(wind·snow·cloud·sea·star·forest·shadow·lightning·earth·fish)를 표·코드가 god key로 쓰는 곳은 시험뿐이다. `04_effect`의 `god.storm`·`god.blast`는 효과 이름, `05_contentOpen`의 `god.flame`은 2번(유지), 코드의 `"snow"`는 테마 deco 값이라 무관 |
| 기획서 | MV-SPEC-01 08 §8.3.2(113 12행)·§8.3.4(110 고유 카드)를 새 행으로 고침, 08 §8.3.1 출처 문구 |
| 검증 | 실행기 compile 1회 + MV.Data EditMode 1회 |
