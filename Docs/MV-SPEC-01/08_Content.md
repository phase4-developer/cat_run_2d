# 8. 콘텐츠 — 30 Area, 야옹신·카드, 추가 모드, 메타 시스템
[입구](README.md) · 앞: [07 표 설계](07_Tables.md) · 다음: [09 저장](09_Save.md)

이 문서는 「무엇을 얼마나 넣는가」를 정한다. 표 구조 공통 규칙(정수 id `{표}{그룹3}{순번3}`, consumeData 3열, 리소스 열, 문구 키 = 행 id, `16_levelPrice` 방식)은 [07](07_Tables.md)을 따른다. 여기서는 **세계(301~304)·콘텐츠(400~499)·상점(700~702)·확률(801~802) 표의 열과 행**을 확정한다.

사용자 원문(2026-11-09): 「컨텐츠 볼륨은 현재 게임 컨텐츠도 임의로 추가해서 만들어줘」, 「메뉴는 로비에서 컨텐츠 버튼 누르면 추가 컨텐츠 메뉴 등장해서 누르면 시작하는 형태로」, 「테이블 구조는 정크버스터즈 참고 많이해줘 … 보상같은건 consumeData형태로 테이블에서 수정 가능해야하고 리소스도 테이블에서 설정가능해야해」. 제외: 레이스, Guild Raid, 길드, 팀 이벤트. 결제·서버·광고 SDK는 스텁.

---

## 8.1 원칙

### 8.1.1 정본 범위와 이 문서의 범위

| 구분 | 정본(`MoonVillage.html`) | 이 문서 |
|---|---|---|
| 플레이 가능 Area | 1~5 (Zone 1). `PROTO_AREAS = 5` L757, 마지막 판정 `finish` L2394~2418 | 표에 30행 전부. 실제로 여는 수는 `300_runConfig.PLAYABLE_AREAS`(결정 대기 D-73, 권장 15) |
| 런칭 범위 | 15. `LAUNCH_AREAS = 15` L757, `FINAL` 15행 L761~767, `AREA_NAMES` 15개 L2579 | 1차 출시 권장 범위 |
| 전체 | 30. `AREA_HP`·`AREA_NEED` 30개 L755~756, 주석 「Area는 1~30 통번호」 L759 | 30행 수치 확정(16~30 Floor는 원작 값) |
| Zone | 6개 이름 `ZONES` L754. 지도는 Zone 1~3만 그림 L2587~2609 | 6행 |
| 신 | `GOD_MENU` 12개 L803~805. 열린 것은 `thunder` 하나, `GODS.flame`은 데이터만 L783~800 | 12행. 1차 공개 2종(thunder, flame) 권장 |
| 추가 모드·메타 | 없음. 지도 `MAP_FEATS`에 「장비·도전·야옹신 추가·동료·레이스·펫」이 「업데이트 예정」 표시만 L2581~2586 | 모드 4종(400_content), 메타 시스템 12종 |

**정본 동등 판정**([12](12_Acceptance.md))은 `PLAYABLE_AREAS = 5`, 추가 콘텐츠 꺼짐(메타 능력치 합 0) 상태에서 한다. 이 문서가 더한 것은 모두 **값 0 또는 `active = 0`일 때 정본과 같은 결과**가 나오게 설계했다.

### 8.1.2 값 태그 규칙

| 태그 | 뜻 | 이 문서에서 쓰는 곳 |
|---|---|---|
| **[프로토]** | 정본 HTML 원값. 줄 번호 함께 | Area 1~15 수치, 이름 15개, 테마 5종, 신 메뉴 12개, thunder·flame 카드, 지도 기능 표시 |
| **[원작]** | HellEscape 원작 값(분석 노트 G가 「원작」으로 표기한 값만) | Area 16~30 Floor 수치, Rapid Rush 층·HP 증가율·보상, Hell War 캣닢 보상, 펫 레벨 비용, 연구 비용·시간, 부활 쿨타임 |
| **[창작]** | 이 기획서 임시값. 플레이 시험으로 조정 | 나머지 |
| **[창작·HE임시]** | HellEscape 저장소가 「임시」로 둔 값을 빌림. 원작 값이 아니므로 창작으로 본다 | 펫·친구 고양이·패스·상점·장비 수치 대부분 |
| **[추정]** | 근거에서 복원한 해석 | Rapid Rush 일부 보상 행 |

표 머리에 태그를 단 열은 그 열 전체가 같은 태그다. 칸마다 다르면 칸에 단다. id·group·sort·리소스 이름은 구조 값이라 태그를 달지 않는다.

원작 고유명(Area·펫·챔피언·신·스킬·무기 이름)은 하나도 쓰지 않는다. 모두 달동네 이름으로 바꿨다.

### 8.1.3 단위 환산(원작 → 정본)

| 항목 | 원작 | 정본 | 환산 |
|---|---|---|---|
| Floor 칸 HP | `FinalValues` | `h × BAL.scale` (fieldSpec L1773) | Floor 값은 그대로 넣는다. 프리셋 배율은 런이 곱한다 |
| 무기 피해 | 1단계 1 | `wDmg(1) = 10` L772 | ×10 |
| 무기 단계 | 12단계 | 8단계 L769 | 원작 k → `round(k × 8/12)` |
| 적·상자 HP | 원작 수치 | 적 `hp × 0.4`, 상자 `hp × 0.6` L1321, L1328 | 모드 표에는 정본 단위로 직접 적는다 |
| 영혼 / 다이아 / 골드 | Soul / Diamond / Gold | 생선 `souls`(01_wealth 3) / 캣닢 `dia`(2) / 츄르 `gold`(1) | 같은 수 |

### 8.1.4 창작값 산정법

창작값은 아래 공식으로 만들었다. 행을 늘릴 때도 같은 공식을 쓴다. 공식은 문서에만 남고 런타임은 행만 읽는다([07](07_Tables.md) 규칙 8).

| 이름 | 공식 | 쓰는 곳 |
|---|---|---|
| R1 반올림 | 값 < 100 → 정수, 100~999 → 5 단위, ≥ 1000 → 유효숫자 2자리 | 모든 창작 수치 |
| C1 선형 보간 | `v(r) = h0 + (h1 − h0) × r / max(1, rows − 1)` (정본 fieldSpec L1773) | Floor 줄별 HP |
| C2 기하 성장 | `v(d) = R1(base × g^d)` | 모드 단계 HP |
| C3 원작 배율 | `v(d) = base × (1 + p × max(0, d − d0))` | Rapid Rush HP(p 0.3, d0 1) [원작] |
| C4 계단 증가 | `v(d) = base + step × ceil(d / 2)` | 수비전 캣닢(20 + 5×ceil(d/2)) [원작] |
| C5 외삽(Area 31 이상) | `hp(n) = R1(hp(30) × 1.15^(n−30))`, Floor h0·h1 = 30번 × `hp(n)/hp(30)`, rows·souls 30번과 같음, `needSouls(n) = needSouls(n−1) + 60` | 31번 이상을 더할 때만. 지금 쓰지 않음 |
| C6 생선 여유 | `needSouls(a) = needSouls(a−1) + min(원값 증가분, Area 생선 합 − 10)` | Area 30 보정(D-70) |
| C7 단위 환산 | 8.1.3 | 원작 → 정본 |

### 8.1.5 이 문서의 표 번호 확정

| 표 | 키·id 형식 | 절 | 1차/2차 |
|---|---|---|---|
| `301_zone` | 301001001~006 | 8.2 | 1차 |
| `302_area` | 302001001~030 | 8.2 | 1차 |
| `303_areaFloor` | `303{Area 3자리}{Floor 3자리}`, 모드용 Area 자리 901~ | 8.2, 8.4 | 1차 |
| `304_theme` | 304001001~015 + `key` | 8.2 | 1차 |
| `113_god`·`110_card`·`111_cardPool` 행 | [07](07_Tables.md) 열 + 8.3 요청 열 | 8.3 | 1차(2종)/2차 |
| `400_content` | 400000001(본편), 400001001~004 | 8.4 | 1차/2차 |
| `401_modeStage` | `401{모드 순번 3}{단계+1 3}` | 8.4 | 1차/2차 |
| `402_modeRule` | 402001001~004 | 8.4 | 1차/2차 |
| `410_questGroup`·`413_repeatQuest`·`414_passQuest`·`420_achievement` | 정크버스터즈 열 구조 | 8.5.1, 8.5.6 | 1차(퀘스트·업적)/2차(패스) |
| `430_pet` · `431_petBuild` | 430001001~, 431001001~ | 8.5.2 | 2차 |
| `440_research` | `440{계열 3}{단계 3}` | 8.5.3 | 1차 |
| `450_friend` · `451_friendPower` | 450001001~, 451001001~ | 8.5.4 | 2차 |
| `460_rune` | 460001001~004 | 8.5.5 | 2차 |
| `470_gearPart` · `471_gearRarity` | 470001001~007, 471001001~008 | 8.5.8 | 2차 |
| `480_blessing` | 480001001~004 | 8.5.9 | 2차 |
| `485_attendance` | 485001001~007 | 8.5.10 | 1차 |
| `490_roadMap` | 490001001~006 | 8.5.11 | 2차(기본 꺼짐) |
| `495_godLevel` | 495001001~005 | 8.3.5 | 2차(1차는 Lv1 고정, D-74) |
| `499_contentConfig` | 세로 키/값 | 8.5.13 | 1차 |
| `700_productTab`·`701_product`·`702_shopSection` | 정크버스터즈 구조 | 8.5.7 | 1차(스텁) |
| `801_drop`·`802_randomBox` | 정크버스터즈 구조 | 8.5.12 | 1차/2차 |
| `16_levelPrice` 행(그룹 301 펫, 302 친구, 304 신, 306~313 장비) | `16{그룹 3}{목표 레벨 3}` | 8.3.5, 8.5 각 절 | 2차 |
| `02_playResource` 행 2001001~2001005, `05_contentOpen` 행, `08_adPlacement` 행, `01_wealth` 행 5~17 | 07 열 | 8.6, 8.7 | 1차/2차 |

문구: 이 문서 표 행의 이름·설명은 Localization `name`·`description` 컬렉션에서 **행 id를 키로** 찾는다([07](07_Tables.md) 규칙 7). 아래 표의 「이름」 열이 `name` 컬렉션 ko 값이다.

리소스: 각 행의 `icon`·`bg`·`bgm`·`sprite` 열 값은 **스프라이트 이름 또는 Addressables 주소**다. 이름은 [03](03_ResourcePipeline.md) 명명 규칙을 따르며, 여기 적은 이름은 권장 이름이다. 아틀라스는 행의 `atlas` 열 → `07_consumeType.iconAtlas` → 표 기본 아틀라스 순으로 찾는다.

---

## 8.2 Zone 6개와 Area 30개

### 8.2.1 301_zone

| 열 | 타입 | 뜻 |
|---|---|---|
| id | int | 301001001~ |
| sort | int | 지도 순서(아래→위, 정본 `column-reverse` L2587~2609) |
| active | int | 1/0 |
| areaFrom / areaTo | int | `302_area.areaNo` 범위 |
| mapIcon | string | 지도 Zone 머리 아이콘 스프라이트 |
| mapCol1 / mapCol2 | string(#rrggbb) | 지도 섹션 색. 정본 `ZONE_COL` L2580 (배경 `color-mix(c1 55%, #141020)`) |
| bgm | string | `03_sound.key`. 비우면 Area 값 사용 |

| id | 이름 [프로토] L754 | sort | areaFrom | areaTo | mapIcon | mapCol1 | mapCol2 | bgm |
|---|---|---|---|---|---|---|---|---|
| 301001001 | 달동네 | 1 | 1 | 5 | UI_Icon_Map_Zone1 | #a85a4a [프로토] | #e0a070 [프로토] | bgm_run |
| 301001002 | 눈 내린 도시 | 2 | 6 | 10 | UI_Icon_Map_Zone2 | #4a6aa8 [프로토] | #8ab0e0 [프로토] | bgm_run |
| 301001003 | 고양이 마을 | 3 | 11 | 15 | UI_Icon_Map_Zone3 | #8a5aa8 [프로토] | #c08ad8 [프로토] | bgm_run |
| 301001004 | 항구 시장 | 4 | 16 | 20 | UI_Icon_Map_Zone4 | #3a8a8a [창작] | #7ad0c8 [창작] | bgm_run |
| 301001005 | 벚꽃 언덕 | 5 | 21 | 25 | UI_Icon_Map_Zone5 | #c85a8a [창작] | #f0a8c8 [창작] | bgm_run |
| 301001006 | 달의 계단 | 6 | 26 | 30 | UI_Icon_Map_Zone6 | #3a3a8a [창작] | #9a9ae8 [창작] | bgm_run |

- 정본 BGM은 하나(합성 BGM, [06](06_Effects.md)·[03](03_ResourcePipeline.md))다. `bgm_run`은 그 키의 가칭이다. Zone별로 바꾸려면 이 열만 고친다.

### 8.2.2 302_area·303_areaFloor 열

| 표 | 열 | 타입 | 뜻 |
|---|---|---|---|
| 302_area | id | int | 302001001~030 |
| | areaNo | int | 1~30. `save.area + 1` |
| | zoneId | int | 301 id |
| | sort / active | int | 순서 / 1·0 (active 0이면 지도에 「업데이트 예정」) |
| | themeId | int | 304 id |
| | hp | int | `AREA_HP[a]` L755. 런의 `R.Z.hp`(게이지·게이트·제작·적·상자 HP의 `hp/100` 배율) |
| | needSouls | int | `AREA_NEED[a]` L756. **이 Area에서 다음 Area를 여는 누적 생선**. 30번은 최종 탈출 조건 |
| | floorGroup | int | 303_areaFloor의 group. 기본 = areaNo |
| | bgm | string | `03_sound.key`. 비우면 301 값 |
| | mapIcon | string | 지도 Area 배너 아이콘(비우면 공용) |
| | clearRewardType / Id / Value | consumeData | Area 개방 순간 보상. **정본에는 없다 → 전부 빈칸** [프로토]. 늘리고 싶으면 여기에 넣는다 |
| 303_areaFloor | id | int | `303{group 3}{floor 3}` |
| | group | int | 302.floorGroup (모드용 901~) |
| | floor | int | 1~3 |
| | h0 / h1 | int | 첫 줄 / 마지막 줄 칸 HP(프리셋 scale 곱하기 전) |
| | rows | int | 줄 수(한 줄 = 3칸) |
| | souls | int | 그 Floor 생선 합 |

### 8.2.3 Area 30행

- 이름: 1~15 [프로토] `AREA_NAMES` L2579, 16~30 [창작].
- 테마: 1~5 [프로토] `AREA_TH[area % 5]` L760, 6~30 [창작](D-72). 테마 key는 8.2.5.
- Floor `[h0, h1, rows, souls]`: Area 1~15 [프로토] `FINAL` L761~767(원작 값과 같음). 16~30은 정본이 `FINAL[min(A,14)]`(Area 15 재사용, L1767)이지만 **원작 `Finals` 값**을 넣는다 [원작](D-71).
- 「합」 = Floor 생선 합, 「증가」 = `needSouls(a) − needSouls(a−1)`, 「여유」 = 합 − 증가.

| areaNo | Zone | 이름 | 테마 key | hp [프로토] | needSouls [프로토] | F1 h0,h1,rows,souls | F2 | F3 | 합 | 증가 | 여유 |
|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | 1 | 시장 골목 | market | 100 | 50 | 1,16,8,10 | 16,60,10,17 | 60,240,12,23 | 50 | 50 | 0 |
| 2 | 1 | 달동네 계단길 | stairs | 200 | 100 | 2,70,8,10 | 70,510,10,17 | 510,1105,12,23 | 50 | 50 | 0 |
| 3 | 1 | 항구 마을 | harbor | 500 | 160 | 5,175,10,14 | 175,1500,12,20 | 1500,3400,14,26 | 60 | 60 | 0 |
| 4 | 1 | 옥상 마을 | roof | 700 | 220 | 7,300,10,14 | 300,3000,12,20 | 3000,6000,14,26 | 60 | 60 | 0 |
| 5 | 1 | 급수탑 꼭대기 | snow | 950 | 280 | 12,500,12,18 | 500,5000,14,23 | 5000,12000,16,29 | 70 | 60 | 10 |
| 6 | 2 | 편의점 앞 | snowday | 1100 | 340 | 15,750,12,18 | 750,9000,14,23 | 9000,18000,16,29 | 70 | 60 | 10 |
| 7 | 2 | 포장마차 거리 | stallnight | 1300 | 400 | 25,1320,14,18 | 1320,13200,16,23 | 13200,28800,18,29 | 70 | 60 | 10 |
| 8 | 2 | 공원 놀이터 | park | 1600 | 460 | 30,2000,14,18 | 2000,20400,16,23 | 20400,41000,18,29 | 70 | 60 | 10 |
| 9 | 2 | 지하철 입구 | subway | 1700 | 520 | 40,3000,14,18 | 3000,29500,16,23 | 29500,62500,18,29 | 70 | 60 | 10 |
| 10 | 2 | 빌딩 옥상 | skyline | 2000 | 580 | 60,4300,14,18 | 4300,43000,16,23 | 43000,90000,18,29 | 70 | 60 | 10 |
| 11 | 3 | 벚꽃길 | blossom | 2200 | 640 | 70,6000,14,18 | 6000,60000,16,23 | 60000,120000,18,29 | 70 | 60 | 10 |
| 12 | 3 | 고양이 카페 | cafe | 2500 | 700 | 80,8400,14,18 | 8400,84000,16,23 | 84000,165000,18,29 | 70 | 60 | 10 |
| 13 | 3 | 온천 골목 | onsen | 2800 | 760 | 90,11000,14,18 | 11000,110000,16,23 | 110000,230000,18,29 | 70 | 60 | 10 |
| 14 | 3 | 등대 언덕 | lighthouse | 3200 | 820 | 135,20625,14,18 | 20625,190000,16,23 | 190000,375000,18,29 | 70 | 60 | 10 |
| 15 | 3 | 달의 계단 | moonstair | 4200 | 870 | 155,27000,14,18 | 27000,270000,16,23 | 270000,550000,18,29 | 70 | 50 | 20 |
| 16 | 4 | 어시장 골목 | harbor | 5000 | 930 | 200,36000,14,18 [원작] | 36000,350000,16,23 [원작] | 350000,750000,18,29 [원작] | 70 | 60 | 10 |
| 17 | 4 | 부두 창고 | market | 6000 | 990 | 225,45000,14,18 [원작] | 45000,455000,16,23 [원작] | 455000,1000000,18,29 [원작] | 70 | 60 | 10 |
| 18 | 4 | 수산 경매장 | stallnight | 8000 | 1050 | 250,59000,14,18 [원작] | 59000,590000,16,23 [원작] | 590000,1300000,18,29 [원작] | 70 | 60 | 10 |
| 19 | 4 | 건어물 거리 | cafe | 10000 | 1110 | 300,76500,14,18 [원작] | 76500,765000,16,23 [원작] | 765000,1700000,18,29 [원작] | 70 | 60 | 10 |
| 20 | 4 | 갈매기 방파제 | lighthouse | 12500 | 1155 | 400,92000,14,18 [원작] | 92000,920000,16,23 [원작] | 920000,2250000,18,29 [원작] | 70 | 45 | 25 |
| 21 | 5 | 벚꽃 터널 | blossom | 15000 | 1215 | 500,110000,14,18 [원작] | 110000,1100000,16,23 [원작] | 1100000,3000000,18,29 [원작] | 70 | 60 | 10 |
| 22 | 5 | 꽃잎 계단 | stairs | 17500 | 1275 | 600,132000,14,18 [원작] | 132000,1320000,16,23 [원작] | 1320000,3500000,18,29 [원작] | 70 | 60 | 10 |
| 23 | 5 | 언덕 위 정자 | park | 20000 | 1335 | 700,146000,14,18 [원작] | 146000,1460000,16,23 [원작] | 1460000,4500000,18,29 [원작] | 70 | 60 | 10 |
| 24 | 5 | 연못 돌다리 | onsen | 25000 | 1395 | 1000,200000,14,18 [원작] | 200000,2000000,16,23 [원작] | 2000000,6000000,18,29 [원작] | 70 | 60 | 10 |
| 25 | 5 | 꽃구름 전망대 | roof | 30000 | 1455 | 1500,500000,14,18 [원작] | 500000,5000000,16,23 [원작] | 5000000,10000000,18,29 [원작] | 70 | 60 | 10 |
| 26 | 6 | 별빛 사다리 | skyline | 35000 | 1500 | 1500,500000,14,18 [원작] | 500000,5000000,16,23 [원작] | 5000000,10000000,18,29 [원작] | 70 | 45 | 25 |
| 27 | 6 | 구름 다리 | snow | 40000 | 1545 | 1800,600000,14,18 [원작] | 600000,6000000,16,23 [원작] | 6000000,12000000,18,29 [원작] | 70 | 45 | 25 |
| 28 | 6 | 달맞이 고개 | subway | 45000 | 1590 | 2160,720000,14,18 [원작] | 720000,7200000,16,23 [원작] | 7200000,14000000,18,29 [원작] | 70 | 45 | 25 |
| 29 | 6 | 은하 골목 | snowday | 50000 | 1635 | 2600,864000,14,18 [원작] | 864000,8640000,16,23 [원작] | 8640000,17000000,18,29 [원작] | 70 | 45 | 25 |
| 30 | 6 | 달의 문 | moonstair | 60000 | **1695 [창작]** (원값 1755) | 3200,1000000,14,18 [원작] | 1000000,10500000,16,23 [원작] | 10500000,21000000,18,29 [원작] | 70 | 60 | 10 |

TSV 예(302·303 첫 행):
```
id	areaNo	zoneId	sort	active	themeId	hp	needSouls	floorGroup	bgm	mapIcon	clearRewardType	clearRewardId	clearRewardValue
302001001	1	301001001	1	1	304001001	100	50	1				
id	group	floor	h0	h1	rows	souls
303001001	1	1	1	16	8	10
303001002	1	2	16	60	10	17
303001003	1	3	60	240	12	23
```

- `active` 기본값: areaNo ≤ `PLAYABLE_AREAS`이면 1, 아니면 0. 열지 않은 Area는 지도에서 정본처럼 「업데이트 예정」(L2589~2590).
- 원작 줄별 HP 목록은 Area 1~30 모두 C1 선형 보간과 같다(노트 G 부록 A 대조). 그래서 `[h0, h1]` 두 값으로 충분하다. 정본 식 `Math.round((h0 + (h1−h0)×r/(rows−1)) × BAL.scale)`을 쓰며 원작 목록과 반올림 1 차이는 허용한다.
- Area 25와 26의 Floor 값은 원작에서도 같다 [원작]. 그대로 둔다(D-71).
- Area 15 이름과 Zone 6 이름이 둘 다 「달의 계단」이다 [프로토]. 정본을 따른다. Zone 6 마지막 Area는 「달의 문」.
- 생선 칸·다이아 칸 위치와 생선 분배는 정본 `fieldSpec` L1766~1779를 Area 16~30에도 그대로 쓴다(시드 `(A+1)×97 + f×131 + 7`). 해시 결정성은 [04](04_GameRules.md)·[07](07_Tables.md)의 처리를 따른다.

### 8.2.4 생선 합과 개방 요구치가 안 맞는 문제 — 결정 대기(D-70)

정본 주석 L1762는 「Area 3다리 합 = 누적 요구치 증가분」이라 했지만 Area 5부터 Floor 생선 합(70)이 증가분(60·50·45)보다 많다(위 표 「여유」).

영향:
- 개방은 **포털 도달 때 누적 생선 ≥ needSouls면 즉시**다(`finish` L2408~2410, `collect` L2430). 개방이 그때 바로 일어나므로 넘치는 양은 마지막 런에서 더 부순 만큼뿐이다. 결과적으로 Area마다 「생선 합 − 10~25개만 모으면 열린다」는 **여유분**으로 작동한다. 막히지 않는다.
- 체크포인트(Floor 생선 전부 파괴, L2402)는 여유와 상관없이 Floor별로 동작한다.
- **Area 30만 막힌다.** 원값 증가분 120 > 생선 합 70. Area 30에 들어올 때 누적이 1635 근처라 1755를 못 채우면 영원히 탈출할 수 없다.

권장 A: 정본·원작 값 유지, Area 30만 C6으로 `needSouls = 1695`. 선택지 비교는 8.9 D-70.

검증기(`TableAuthoringOptions.ValidateTables`) 규칙: **모든 Area에서 `Floor 생선 합 ≥ needSouls(a) − needSouls(a−1)`** (Area 1은 `needSouls ≤ 합`). 어기면 로드 실패로 막는다.

### 8.2.5 304_theme — Zone 2~6 테마 — 결정 대기(D-72)

정본은 5테마를 `area % 5`로 반복한다 L760. 그러면 Area 6은 Area 1과 똑같이 보인다. HUD·지도에는 「눈 내린 도시」라 나오는데 화면은 낮 시장이다.

권장 B: **색 테마 10종 추가**(Area 6~15 각 1종, Area 16~30은 15종을 다시 배정). 소품 묶음(`deco`)은 정본 5종을 재사용하고 색만 바꾼다. 정본 집과 지면 그림은 코드가 팔레트 색으로 그리므로, [03](03_ResourcePipeline.md)의 그림 추출 도구(MV-TOOL-ART)가 **팔레트만 바꿔 같은 그림을 다시 구워** 테마별 월드 아틀라스(`worldAtlas`)를 만든다. 새로 그리는 그림은 없다. 선택지 비교는 8.9 D-72.

**304_theme 열** — 색 열 이름은 정본 `AREA_TH` 필드 그대로 L743~753. 리스트 열(`leaf`, `walls`, `roofs`)은 `/` 구분.

| 열 | 타입 | 뜻 |
|---|---|---|
| id / key | int / string | 304001001~ / market 등 |
| skyTop … door | string(#rrggbb) | 정본 필드 |
| prop | string | 전부 `moon` [프로토] |
| deco | string | 소품 묶음(market/stairs/harbor/roof/snow) |
| tint / night | string / float | 정본 필드(tint 없음은 빈칸) |
| worldAtlas | string | 이 테마의 월드 아틀라스 주소. 스프라이트 이름은 테마와 무관하게 같다 |

정본 5종(id 304001001~005 = market, stairs, harbor, roof, snow)의 값은 [07](07_Tables.md)·노트 B §1.5 그대로다 [프로토]. 새 10종 [창작]:

| 열 | snowday | stallnight | park | subway | skyline |
|---|---|---|---|---|---|
| id | 304001006 | 304001007 | 304001008 | 304001009 | 304001010 |
| 쓰는 Area | 6, 29 | 7, 18 | 8, 23 | 9, 28 | 10, 26 |
| 분위기 | 눈 온 낮 | 포장마차 저녁 | 놀이터 오후 | 해질녘 역 | 도시 밤 |
| skyTop | #a8c8e8 | #2a2050 | #9ad0f0 | #f0a070 | #0e1238 |
| skyBot | #eef4fa | #c8607a | #fff0d8 | #ffe0b0 | #2a3a78 |
| fog | #e8eef4 | #6a4a6a | #e8e0d0 | #e0c8b0 | #3a4a7a |
| grass | #eef2f8 | #b8a49c | #d8e4d0 | #c4b8ac | #9aa0b8 |
| grass2 | #e4eaf2 | #b09a92 | #ccdcc4 | #bcb0a4 | #9298b0 |
| road | #f4f6fa | #cdbcb0 | #ece4d4 | #d4ccc4 | #b8bcd0 |
| road2 | #e8ecf4 | #c2b0a4 | #e2d8c6 | #cac2ba | #aeb2c6 |
| stone | #b8c2d0 | #8a7880 | #a8a090 | #8a8a90 | #7a7e98 |
| hillFar | #b8c8d8 | #4a3a6a | #a8c0b8 | #b08a90 | #1e2450 |
| hillNear | #d4dce6 | #6a4a6a | #c8d4c0 | #c8a898 | #2e3664 |
| leaf | #9ab8a8/#8aa898/#aac8b8 | #7ab86a/#6aa85a/#8ac87a | #7ab86a/#6aa85a/#8ac87a | #7ab86a/#6aa85a/#8ac87a | #6a8a9a/#5a7a8a/#7a9aaa |
| walls | #f4ece0/#e0e8f0/#f0e0dc/#e8f0e4 | #f2dcc8/#ead0d0/#dcd8e8/#f6e2c0 | #f6efe0/#e4ecf2/#f2e4d8/#e8f2e0 | #e8e4e0/#d8e0e8/#f0dcd0/#e4e8dc | #d0d8ec/#c8d0e4/#e0d8ec |
| wallTop | #ffffff | #8a5a4a | #6a8a7a | #4a6a8a | #6a7a9a |
| roofs | #f4f8ff/#d0704a/#5a7ab8 | #e8604a/#f0a040/#5a7ab8/#c84a5a | #e8a040/#5a9ad0/#d05a5a/#6aaa6a | #3a7ab8/#e0a030/#7a7a8a/#4aa07a | #4a5a98/#6a6a8a/#3a7a9a |
| door | #3a8a7a | #f0a040 | #d05a5a | #3a7ab8 | #ffd84a |
| deco | snow | market | stairs | roof | roof |
| tint | #f0f4ff | #ffc8a8 | #fff6e8 | #ffe0c0 | #8a90d0 |
| night | 0 | 0.6 | 0 | 0.15 | 1 |
| worldAtlas | Atlas_World_Theme_snowday | Atlas_World_Theme_stallnight | Atlas_World_Theme_park | Atlas_World_Theme_subway | Atlas_World_Theme_skyline |

| 열 | blossom | cafe | onsen | lighthouse | moonstair |
|---|---|---|---|---|---|
| id | 304001011 | 304001012 | 304001013 | 304001014 | 304001015 |
| 쓰는 Area | 11, 21 | 12, 19 | 13, 24 | 14, 20 | 15, 30 |
| 분위기 | 봄 낮 | 오후 카페 | 노을 온천 김 | 밤 바다 | 깊은 밤 달빛 |
| skyTop | #a8d4f4 | #a0c4e8 | #d88a7a | #1a2450 | #0a0c30 |
| skyBot | #fde8ee | #f8e4cc | #f8d0b8 | #4a5a8a | #2a2a6a |
| fog | #f4e0e4 | #e8d8c4 | #f0dcd0 | #4a5878 | #3a3a6a |
| grass | #e8d4d0 | #d0bca4 | #c8b4a8 | #a4aac0 | #c8cce8 |
| grass2 | #e0cac6 | #c8b49c | #c0aca0 | #9ca2b8 | #c0c4e0 |
| road | #f2e6e0 | #e8dcc8 | #dccabc | #c0c4d6 | #dcdff0 |
| road2 | #eadcd4 | #dccfba | #d2c0b2 | #b6bacc | #d2d6ea |
| stone | #b8a09a | #a08a72 | #8a7a70 | #848aa0 | #9a9ec0 |
| hillFar | #d8b8c8 | #b0a8b8 | #a88a98 | #2a3460 | #1a1a48 |
| hillNear | #ecc8d0 | #ccb8a4 | #c8a0a0 | #3a4670 | #2a2a5a |
| leaf | #f4b8c8/#f8c8d4/#eaa4b8 | #7ab86a/#6aa85a/#8ac87a | #8aa86a/#7a985a/#9ab87a | #6a8a9a/#5a7a8a/#7a9aaa | #8a9ad0/#7a8ac0/#9aaae0 |
| walls | #fff0e8/#f4e4ec/#e8ecf4/#fcf0dc | #f6e8d4/#f0dccc/#e4e0d0/#fff2e0 | #f0e0d0/#e8d8d8/#dce0e8/#f4e4cc | #e8e4f0/#dce4f0/#f0e4e4 | #e8e8f8/#dcdcf2/#f0eaf8 |
| wallTop | #c87a8a | #8a5a3a | #6a4a3a | #c84a4a | #ffe8a0 |
| roofs | #d0605a/#e88aa0/#6a8ab8 | #a0603a/#d08a4a/#6a8a6a/#c8a06a | #5a4a4a/#8a5a4a/#4a6a7a | #c84a4a/#4a6ab8/#e8e8f0 | #6a6ab8/#8a7ac8/#4a5aa8 |
| door | #c8506a | #8a5a3a | #c84a3a | #ffd84a | #ffd84a |
| deco | stairs | market | harbor | harbor | stairs |
| tint | #fff0f4 | #fff2e0 | #ffe0d0 | #9aa4d8 | #a8a8e8 |
| night | 0 | 0 | 0.25 | 0.9 | 1 |
| worldAtlas | Atlas_World_Theme_blossom | Atlas_World_Theme_cafe | Atlas_World_Theme_onsen | Atlas_World_Theme_lighthouse | Atlas_World_Theme_moonstair |

- deco별 연출(예: `snow`의 흰 지면·눈)은 정본 5종과 같다([02](02_Visual.md)). 새 테마는 deco를 재사용하므로 연출 코드 추가가 없다.
- 선택지 A(정본 반복)로 가면 302의 `themeId`만 304001001~005 반복으로 바꾸면 된다. 다른 열은 영향 없다.

---
## 8.3 야옹신 12종과 카드

### 8.3.1 정본 상태

- `GOD_MENU` 12개 L803~805 [프로토]. `id`가 있는 것은 thunder(달빛 야옹신) 하나라 나머지 11개는 잠김 표시(`🔒 잠김`, 토스트 「… — 업데이트에서 해금 예정」 L2500).
- `GODS` L783~800은 thunder·flame 2종의 고유 카드 7장씩. flame은 메뉴 2번에 id가 없어 저장값 `god='flame'`일 때만 쓰인다.
- 덱 = 공용 4장 + 출전 신 고유 7장 = 11장(`setGodCards` L808). 신 패널 안내 「공용 카드(회색) + 야옹신 고유 카드 7장(초록 1 · 파랑 2 · 보라 1 · 주황 3)」 L666~669.
- 슬롯 구조: 모든 신은 같은 카드 key 7개(orb·proc·chain·storm·blast·frate·spear)에 다른 이름·효과를 붙인다. tier = 1, 2, 2, 3, 4, 4, 4 [프로토].
- 지도 `MAP_FEATS` Area 4(a:3) 「야옹신 추가 · 2번째 야옹신 해금」 표시 L2581~2586 [프로토]. 해금 로직은 없다.
- 원작 사양은 「신 메뉴 10종 표시, 1번만 선택」이지만 정본은 12개를 표시하므로 **12개를 따른다**.

### 8.3.2 113_god 12행

[07](07_Tables.md)의 113_god 열(이름·속성·색·부제·순서·사용 여부)에 아래 **열 추가를 요청**한다: `unlockType`(0 무료 / 1 contentOpen / 2 구매), `openId`(05 id), `unlockPriceType/Id/Value`(consumeData), `passiveStat`·`passivePerLv`(8.3.5), `cardGroup`(110_card group), `gearSet`(1이면 장비 세트, 8.5.8), `portrait`(초상 스프라이트), `projTint`(투사체 색).

| id | key | 이름 [프로토] | 부제 [프로토] | color [프로토] | element | unlockType | 해금 조건 | passiveStat / perLv [창작·HE임시] | gearSet | active(1차) |
|---|---|---|---|---|---|---|---|---|---|---|
| 113001001 | thunder | 달빛 야옹신 | 털실 · 정전기 · 냥냥펀치 | #9ab8ff | bolt [프로토] | 0 | 처음부터 | CritChance / 1 | 1 | 1 |
| 113001002 | flame | 햇살 야옹신 | 햇살 · 범위 · 폭발 (메뉴 부제 「햇살 · 폭발」) | #ff8a3d | fire [프로토] | 1 | Area 4 도달(05 행 5001004) [창작, 지도 표시 근거] | Dmg / 2 | 1 | 1 |
| 113001003 | wind | 바람 야옹신 | 회오리 돌진 | #9fe8c0 | wind [창작] | 2 | 방울 50 | FireRate / 2 | 1 | 0 |
| 113001004 | snow | 눈꽃 야옹신 | 얼음 · 둔화 | #aee9ff | ice [창작] | 2 | 방울 50 | CritDmg / 5 | 1 | 0 |
| 113001005 | cloud | 구름 야옹신 | 친구 강화 | #d6c0ff | cloud [창작] | 2 | 방울 80 | DoubleChance / 1 | 1 | 0 |
| 113001006 | sea | 바다 야옹신 | 물결 방어막 | #5fa8f0 | water [창작] | 2 | 방울 80 | Range / 2 | 0 | 0 |
| 113001007 | star | 별빛 야옹신 | 별똥별 | #ffe08a | star [창작] | 2 | 방울 120 | Gold / 5 | 0 | 0 |
| 113001008 | forest | 숲 야옹신 | 덩굴 · 독 | #8ad06a | leaf [창작] | 2 | 방울 120 | Dmg / 3 | 0 | 0 |
| 113001009 | shadow | 그림자 야옹신 | 관통 | #9a7ad6 | shadow [창작] | 2 | 방울 150 | CritDmg / 5 | 0 | 0 |
| 113001010 | lightning | 번개 야옹신 | 유도 번개 | #ffd84a | spark [창작] | 2 | 방울 150 | FireRate / 3 | 0 | 0 |
| 113001011 | earth | 흙 야옹신 | 바위 낙하 | #c49a62 | rock [창작] | 2 | 방울 200 | Dmg / 3 | 0 | 0 |
| 113001012 | fish | 생선 야옹신 | 생선 보상 | #6ad0e8 | fish [창작] | 2 | 방울 200 | Gold / 8 | 0 | 0 |

- 「방울」 = `01_wealth` 8 godCoin(야옹신 방울). `unlockPriceType/Id/Value` = `1 / 8 / 50` 형식. 가격 [창작·HE임시](원작 저장소 임시 가격 50~200 곡선).
- thunder 메뉴 항목에는 정본상 색·부제가 없다(L803). 색은 `GODS.thunder.c`, 부제는 `GODS.thunder.sub` [프로토].
- 새 element(wind~fish)는 투사체 색(`projTint` = 신 color)과 카드 아이콘만 다르다. 피해 계산에는 영향이 없다(정본도 el은 연출만, C 노트 §3.8).
- active 0인 신은 정본처럼 잠김 칸으로 보이고 토스트 「{이름} · {부제} — 업데이트에서 해금 예정」을 띄운다. 1차 공개 범위는 결정 대기 D-74.

### 8.3.3 카드 효과 type (112_cardEffect)

정본 카드 효과를 type으로 나눈다. **type 이름·파라미터 열은 [07](07_Tables.md) 112_cardEffect가 정본**이며, 이름이 다르면 07을 따른다. 이 표는 「어떤 카드가 어떤 동작인가」를 정한다.

| type | 동작 | 파라미터(정본 값) | 근거 | 상태 |
|---|---|---|---|---|
| DmgMul | 1발 피해 ×(1 + p1×장수) | p1 0.2 | baseDmg L1751 | 정본 사용 |
| FireRateMul | 연사 ×(1 + p1×장수) | rate 0.2, frate 0.5 | iv L2074 | 정본 사용 |
| CloneAdd | 친구 +p1, 상한 `MAX_CLONES` 4 | p1 1 | L2313, L830 | 정본 사용 |
| GoldMul | 츄르 ×(1 + p1×장수) | p1 0.5 | goldMul L1746 | 정본 사용 |
| Orbit | 위성 p1개 반경 p2, 0.3초마다 `baseDmg×p3×n` | 3, 1.5, 1.2 | L1842, L2140~2144 | 정본 사용 |
| ProcExtraHit | 명중 p1 확률 추가타 `dmg×p2×n` | 0.5, 0.6 | L1986~1989(bolt) | 정본 사용 |
| ProcBurn | 명중 p1 확률 화상 p2초, 0.5초 틱 `dmg×p3×n×0.5` | 0.5, 3, 0.3 | L1986~1989(fire), L2136~2138 | 정본 사용 |
| ChainJump | `1+n`회 연쇄, 거리 < p1, 피해 `dmg×p2` | 9, 0.5 | L1990~2003(bolt) | 정본 사용 |
| ChainSplash | 반경 `p1+0.3n`, 상자 `p2+hw`, 피해 `dmg×p3×n` | 1.6, 2.4, 0.4 | L1990~2003(fire) | 정본 사용 |
| Storm | 확률 p1, 쿨 p2초, 4발, `baseDmg×p3×n` | 0.25, 2.6, 1.5 | L2004, L2157~2174 | 정본 사용 |
| Blast | 확률 `p1 + 0.04(n−1)`, 쿨 p2, 범위 p3, `baseDmg×4n` | 0.1, 1.4, 3.6 | L2005, L2175~2182 | 정본 사용 |
| PierceEveryN | 본체 p1번째 볼리마다 관통 99, 피해 ×(1+n) | 2 | L2082~2086 | 정본 사용 |
| Beam | 5초마다 전방 22m 광선 `baseDmg×3n` | 5, 22, 3 | L2119~2125 | 정본 코드만 있음(카드 없음) → 재사용 |
| Meteor | 쿨 `max(2.5, 6−(n−1))`, 반경 3.2, `baseDmg×5n` | 6, 2.5, 3.2, 5 | L2127~2133, L2202~2212 | 코드만 있음 → 재사용 |
| Spin | 0.3초마다 `|dx|<hw+1.4`, `baseDmg×1.5n` | 0.3, 1.4, 1.5 | L2146~2150 | 코드만 있음 → 재사용 |
| CritChance | 치명 확률 +p1/장 | 0.15 | L2075 | 코드만 있음 → 재사용 |
| DoubleChance | 2연발 +p1/장(상한 1) | 0.2 | L2075 | 코드만 있음 → 재사용 |
| Pierce | 관통 +1/장 | 1 | L2075 | 코드만 있음 → 재사용 |
| **ProcChill** | 명중 p1 확률로 p2초 얼림: 받는 피해 ×(1+p3), 적 이동 ×p4 | 0.4, 2, 0.25, 0.5 | 정본은 `o.chill` 읽기만 L1904 | **새 type [창작]** |
| **Shield** | 벽 접촉 때 런 종료 대신 그 벽을 즉시 부수고(보상 없음) 1회 소모. 장당 p1회 | 1 | — | **새 type [창작]** |
| **CloneDmgMul** | 친구 고양이 발사 피해 ×(1 + p1×n) | 0.5 | — | **새 type [창작]** |
| **DropBonus** | 벽·상자 파괴 때 p1 확률로 캣닢 `ceil(diaCap/2)`, 츄르 ×(1 + p2×n) | 0.08, 0.3 | 적 8% 캣닢 규칙 L1925~1927 응용 | **새 type [창작]** |

### 8.3.4 고유 카드 12종 × 7장 (110_card 행)

110_card의 신 카드 행은 `group` = 신 순번(1 thunder … 12 fish), `key` = 슬롯 key(orb…spear), id = `110{신 순번 3}{슬롯 순번 3}`(예 thunder orb 110001001). 공용 카드는 group 0. id 규칙이 07과 다르면 07을 따르고 이 표는 이름·tier·type을 정한다. `icon` = `Card_{신 key}_{슬롯 key}`(예 `Card_thunder_orb`, [03 §3.5.7](03_ResourcePipeline.md) `Card_` 접두, 아틀라스 `Atlas_UI_Card`).

**thunder·flame [프로토]** L783~800 — 원문 그대로.

| 슬롯(tier) | thunder 이름 / 설명 / type | flame 이름 / 설명 / type |
|---|---|---|
| orb (1) | 털실 위성 / 주위를 도는 털실뭉치 3개 / Orbit | 햇살 털실 / 주위를 도는 햇살 털실 3개 / Orbit |
| proc (2) | 찌릿 정전기 / 공격 50% 확률로 정전기, 피해 +60% / ProcExtraHit | 따끈 화상 / 공격 50% 확률로 3초간 화상 / ProcBurn |
| chain (2) | 정전기 연쇄 / 명중 시 정전기가 주변 2곳으로 튐 / ChainJump | 불씨 파편 / 명중 시 불씨가 주변 폭발, 피해 40% / ChainSplash |
| storm (3) | 냥냥펀치 소나기 / 명중 시 하늘에서 거대 냥냥펀치 4발 / Storm | 생선 소나기 / 명중 시 생선 4마리가 쏟아짐 / Storm |
| blast (4) | 하악질 / 명중 시 확률로 주변을 날려버리는 하악질 / Blast | 햇살 폭발 / 명중 시 확률로 거대 햇살 폭발 / Blast |
| frate (4) | 우다다 연사 / 연사 +50% / FireRateMul | 햇살 연사 / 연사 +50% / FireRateMul |
| spear (4) | 레이저 포인터 / 2번째 공격마다 관통 레이저 포인터 / PierceEveryN | 돋보기 햇빛 / 2번째 공격마다 관통 돋보기 햇빛 / PierceEveryN |

**나머지 10종 [창작]** — 이름·설명·type 전부 창작. 파라미터는 8.3.3 정본 값(다르면 아래 주석).

| 신 | orb (1) | proc (2) | chain (2) | storm (3) | blast (4) | frate (4) | spear (4) |
|---|---|---|---|---|---|---|---|
| wind | 회오리 털실 / 주위를 도는 회오리 3개 / Orbit | 돌풍 밀치기 / 공격 50% 확률로 돌풍, 피해 +60% / ProcExtraHit | 바람 연쇄 / 명중 시 바람이 주변 2곳으로 튐 / ChainJump | 회오리 돌진 / 몸 주위 회오리가 계속 피해 / Spin | 태풍의 눈 / 명중 시 확률로 주변을 날리는 태풍 / Blast | 바람 연사 / 연사 +50% / FireRateMul | 바람 칼날 / 2번째 공격마다 관통 바람 칼날 / PierceEveryN |
| snow | 눈송이 위성 / 주위를 도는 눈송이 3개 / Orbit | 꽁꽁 얼음 / 공격 40% 확률로 2초 얼림, 받는 피해 +25% / ProcChill | 얼음 파편 / 명중 시 얼음이 튀어 주변 피해 40% / ChainSplash | 우박 소나기 / 명중 시 우박 4개 / Storm | 눈사태 / 명중 시 확률로 주변을 덮는 눈사태 / Blast | 눈뭉치 연사 / 연사 +50% / FireRateMul | 고드름 창 / 2번째 공격마다 관통 고드름 / PierceEveryN |
| cloud | 솜구름 위성 / 주위를 도는 솜구름 3개 / Orbit | 구름 친구 / 친구 고양이 +1 / CloneAdd | 합동 냥펀치 / 친구 고양이 피해 +50% / CloneDmgMul | 비구름 / 명중 시 빗방울 4개 / Storm | 천둥 구름 / 명중 시 확률로 주변 천둥 / Blast | 구름 연사 / 연사 +50% / FireRateMul | 무지개 다리 / 2번째 공격마다 관통 무지개 / PierceEveryN |
| sea | 물방울 위성 / 주위를 도는 물방울 3개 / Orbit | 물결 튀김 / 공격 50% 확률로 물결, 피해 +60% / ProcExtraHit | 파도 연쇄 / 명중 시 파도가 튀어 주변 피해 40% / ChainSplash | 물결 방어막 / 장애물에 막혀도 1번 부수고 지나감 / Shield | 해일 / 명중 시 확률로 주변을 덮는 해일 / Blast | 물총 연사 / 연사 +50% / FireRateMul | 물대포 / 2번째 공격마다 관통 물대포 / PierceEveryN |
| star | 별 위성 / 주위를 도는 별 3개 / Orbit | 반짝 별가루 / 공격 50% 확률로 별가루, 피해 +60% / ProcExtraHit | 별자리 잇기 / 명중 시 별빛이 주변 2곳으로 튐 / ChainJump | 별똥별 / 몇 초마다 앞쪽에 별똥별 / Meteor | 초신성 / 명중 시 확률로 빛 폭발 / Blast | 별빛 연사 / 연사 +50% / FireRateMul | 별빛 광선 / 5초마다 앞쪽 일직선 광선 / Beam |
| forest | 도토리 위성 / 주위를 도는 도토리 3개 / Orbit | 독버섯 포자 / 공격 50% 확률로 3초간 독 / ProcBurn | 덩굴 휘감기 / 명중 시 주변 덩굴 피해 40% / ChainSplash | 도토리 비 / 명중 시 도토리 4개 / Storm | 쓰러지는 고목 / 명중 시 확률로 주변을 덮치는 고목 / Blast | 솔방울 연사 / 연사 +50% / FireRateMul | 가시 덩굴 / 2번째 공격마다 관통 가시 / PierceEveryN |
| shadow | 그림자 위성 / 주위를 도는 그림자 3개 / Orbit | 급소 노리기 / 치명타 확률 +15% / CritChance | 그림자 꿰뚫기 / 공격이 대상 1개를 더 관통 / Pierce | 어둠 발톱 / 명중 시 발톱 그림자 4번 / Storm | 그림자 폭발 / 명중 시 확률로 주변 폭발 / Blast | 그림자 연사 / 연사 +50% / FireRateMul | 그림자 창 / 2번째 공격마다 관통 그림자 창 / PierceEveryN |
| lightning | 번개 구슬 / 주위를 도는 번개 구슬 3개 / Orbit | 찌릿찌릿 / 공격 50% 확률로 번개, 피해 +60% / ProcExtraHit | 유도 번개 / 명중 시 번개가 주변 3곳으로 튐 / ChainJump(연쇄 `2+n`) | 번개 소나기 / 명중 시 번개 4줄기 / Storm | 벼락 / 명중 시 확률로 큰 벼락 / Blast | 번쩍 연사 / 연사 +50% / FireRateMul | 번개 창 / 2번째 공격마다 관통 번개 창 / PierceEveryN |
| earth | 조약돌 위성 / 주위를 도는 조약돌 3개 / Orbit | 단단 냥펀치 / 2연발 확률 +20% / DoubleChance | 돌 부스러기 / 명중 시 돌이 튀어 주변 피해 40% / ChainSplash | 바위 낙하 / 몇 초마다 앞쪽에 바위 / Meteor | 땅울림 / 명중 시 확률로 지진 / Blast | 자갈 연사 / 연사 +50% / FireRateMul | 돌기둥 / 2번째 공격마다 관통 돌기둥 / PierceEveryN |
| fish | 멸치 위성 / 주위를 도는 멸치 3마리 / Orbit | 생선 냄새 / 부술 때 8% 확률로 캣닢, 츄르 +30% / DropBonus | 꽁치 연쇄 / 명중 시 꽁치가 주변 2곳으로 튐 / ChainJump | 고등어 소나기 / 명중 시 고등어 4마리 / Storm | 참치 폭발 / 명중 시 확률로 거대 참치 폭발 / Blast | 멸치 연사 / 연사 +50% / FireRateMul | 황새치 찌르기 / 2번째 공격마다 관통 황새치 / PierceEveryN |

- **생선 야옹신은 생선(souls)을 늘리지 않는다.** 생선은 Area 개방 축이라 늘리면 8.2.4 균형이 깨진다. 츄르·캣닢만 늘린다.
- 새 type 4개(ProcChill, Shield, CloneDmgMul, DropBonus)와 재사용 6개(Beam, Meteor, Spin, CritChance, DoubleChance, Pierce)는 [04](04_GameRules.md)·[07](07_Tables.md)에 구현 요청이다. 1차에 thunder·flame만 열면 1차 구현 대상은 정본 12개 type뿐이다.
- 70장 카드 아이콘·10종 초상은 새 아트다(2차, MV-ART). 정본 `paintGod` L2481~2491은 해/달 두 가지 배경만 그린다.

### 8.3.5 신 레벨(495_godLevel)과 조각

원작 구조(신마다 레벨·조각, 공용 조각이 부족분을 채움)를 옮긴다. 값 [창작·HE임시].

| 열 | 타입 | 뜻 |
|---|---|---|
| id | int | 495001001~005 |
| level | int | 1~5 |
| dmgPct | int | 신 레벨 공격 보너스 %(메타 Dmg에 더함) |
| passiveMul | int | 113_god `passivePerLv` × 이 값 = 패시브 수치 |
| levelPriceGroup | int | 16_levelPrice 그룹(304) |
| icon | string | 레벨 별 아이콘 |

| id | level | dmgPct | passiveMul | 다음 레벨 비용 |
|---|---|---|---|---|
| 495001001 | 1 | 0 | 1 | 조각 5 |
| 495001002 | 2 | 10 | 2 | 조각 10 |
| 495001003 | 3 | 20 | 3 | 조각 20 |
| 495001004 | 4 | 35 | 4 | 조각 40 |
| 495001005 | 5 | 50 | 5 | MAX |

`16_levelPrice` 그룹 304 행: id `16304002`(targetLevel 2) `priceType 622 / priceId 0 / priceValue 5`, `16304003` 10, `16304004` 20, `16304005` 40. consumeType 622 GodShard는 「priceId 0 = 지금 올리는 신」이며 부족분은 공용 조각(01_wealth 9 godShard)으로 1:1 채운다(MV.Meta 규칙).

- 조각 획득: 본편 런이 Area 돌파 아치 도달(`finish('gate')` L2029)로 끝나면 출전 신 조각 +1 [창작·HE임시].
- **1차는 신 레벨 Lv1 고정, passive 0 권장**(D-74). 정본 동등이 유지된다. 2차에 113 `passivePerLv`를 채우고 레벨업을 연다.

### 8.3.6 공용 카드 추가 여부 — 결정 대기(D-75)

정본 공용 4장(atk 발톱 갈기, rate 냥냥펀치 연타, clone1 친구 부르기, gold 츄르 파티) L776~782 [프로토]. 정본 코드에는 얻을 수 없는 카드 경로가 4개 더 있다: `crit`, `dbl`, `pierce`, `clone2`(아이콘도 있음 L901~906).

권장: 110_card group 0에 아래 4행을 **`active 0`(본편 풀 제외)**으로 넣고, 모드 규칙(402 `cardPoolId`)이 가리킬 때만 쓴다. 본편 카드 확률이 정본과 같게 유지된다.

| id | key | 이름 [창작] | 설명 | tier | type | active |
|---|---|---|---|---|---|---|
| 110000005 | crit | 급소 할퀴기 | 치명타 확률 +15% | 1 | CritChance | 0 |
| 110000006 | dbl | 양발 펀치 | 2연발 확률 +20% | 1 | DoubleChance | 0 |
| 110000007 | pierce | 꿰뚫는 발톱 | 공격이 대상 1개를 더 관통 | 2 | Pierce | 0 |
| 110000008 | clone2 | 친구 둘 부르기 | 친구 고양이 +2 | 2 | CloneAdd(p1 2) | 0 |

`111_cardPool`(07) 행 요청: `main`(공용 4 + 출전 신 7, 정본), `defence`(공용 4에서 gold 빼고 + 위 4장 + 출전 신 7), `rush`(main과 같음).

---
## 8.4 추가 콘텐츠 모드 — 로비 「컨텐츠」 메뉴

### 8.4.1 메뉴 흐름

1. 로비에 「컨텐츠」 버튼이 있다(위치·모양은 [05](05_Screens.md)). 잠금 조건 `05_contentOpen` 5001003 = **Area 3 도달**. 정본 지도 `MAP_FEATS` Area 3(a:2) 「⚔️ 도전 · 특수 규칙 코스」 L2581~2586 [프로토]가 이 메뉴다.
2. 버튼을 누르면 `UIPopup_ContentMenu`가 열리고 `400_content`에서 `contentType ≠ 0`·`active = 1` 행을 `sort` 순으로 보여 준다.
3. 항목 카드: `icon`, `bg`, 이름(name 컬렉션 = 행 id), 한 줄 설명(description 컬렉션 = 행 id), 입장권 `보유/chargeMax`, 다음 도전 단계, 최고 클리어 단계. 잠긴 항목은 회색 + 05의 조건 문구.
4. 버튼 3개: **[도전 단계 N]** = 입장 비용(`spendResourceType/Id/Value`)을 내고 **바로 시작**(확인 창 없음). **[소탕]** = 최고 클리어 단계 보상을 런 없이 받음(입장권 1). **[▶ +1]** = 보상 광고로 입장권 +1(`08_adPlacement` modeTicket).
5. 런은 `Game` 씬 안에서 그대로 시작한다([10](10_Architecture.md): 로비→런 즉시 전환). 별도 `Mode_*` 씬은 쓰지 않는다(권장). 모든 모드가 정본 월드 렌더를 재사용하기 때문이다.
6. 끝나면 `UIPopup_ModeResult`(클리어/실패, 보상 목록) → [받기] → 로비로 돌아가 컨텐츠 메뉴를 다시 연다. 모드 결과에는 정본의 ×3 광고가 없다 [창작].
7. 시작 처리 중 오류로 런이 열리지 않으면 입장권을 돌려준다 [창작·HE임시].

### 8.4.2 400_content (메뉴 항목)

정크버스터즈 `400_content` 구조(contentType, spendResource)를 따른다.

| 열 | 타입 | 뜻 |
|---|---|---|
| id | int | 400000001(본편), 400001001~ |
| key | string | 코드가 찾는 이름 |
| contentType | int | 0 본편 / 1 Defence / 2 FixedLoadout / 3 Course / 4 Boss. `IModeRule` 구현 선택 |
| active / sort | int | 1·0 / 메뉴 순서 |
| icon / bg / banner | string | 메뉴 아이콘 / 카드 배경 텍스처 / 결과 머리 그림 |
| bgm | string | `03_sound.key` |
| modeRuleId | int | 402 id |
| stageGroup | int | 401 group |
| openId | int | 05_contentOpen id(메뉴 항목 잠금) |
| spendResourceType / Id / Value | consumeData | 입장 비용. 보통 `3 / 입장권 id / 1` |
| sweepActive | int | 1이면 소탕 가능 |
| hudId | string | 런 HUD 이름. 빈칸 = `Run`(본편 `UIHud_Run`). alleyWar = `Defence`([05 §5.17.2](05_Screens.md)) |

| id | key | 이름 [창작] | 한 줄 설명 [창작] | contentType | active | sort | icon | bg | bgm | modeRuleId | stageGroup | openId | spend Type/Id/Value | sweep |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 400000001 | main | 달동네 탈출 | 본편 | 0 | 1 | 0 | — | — | bgm_run | — | — | — | 빈칸(스태미나 켜면 `3/105/1`) | 0 |
| 400001001 | alleyWar | 골목 수비전 | 몰려오는 쥐 떼에게서 생선 가게를 지켜라 | 1 | 1 | 1 | UI_Icon_Mode_AlleyWar | Tex_Mode_AlleyWar | bgm_run | 402001001 | 1 | 5001003 | 3 / 2001001 / 1 | 1 |
| 400001002 | rapidRush | 우다다 질주 | 정해진 무기 하나로 끝까지 달려라 | 2 | 1 | 2 | UI_Icon_Mode_RapidRush | Tex_Mode_RapidRush | bgm_run | 402001002 | 2 | 5001006 | 3 / 2001002 / 1 | 1 |
| 400001003 | churuRush | 츄르 털이 | 츄르가 가득한 골목을 털어라 | 3 | 1 | 3 | UI_Icon_Mode_ChuruRush | Tex_Mode_ChuruRush | bgm_run | 402001003 | 3 | 5001007 | 3 / 2001003 / 1 | 1 |
| 400001004 | bigMouse | 대왕쥐 소탕 | 거대한 대왕쥐를 시간 안에 쓰러뜨려라 | 4 | **0 (2차)** | 4 | UI_Icon_Mode_BigMouse | Tex_Mode_BigMouse | bgm_run | 402001004 | 4 | 5001008 | 3 / 2001004 / 1 | 1 |

BGM: 정본 BGM은 로비·런 2개뿐이라([03 §3.9](03_ResourcePipeline.md)) 1차는 모든 모드가 `bgm_run`을 쓴다. 모드 전용 BGM은 2차에 `03_sound` 행을 추가하고 이 열 값만 바꾼다(코드 수정 없음) [창작].

원작 대응: alleyWar = Hell War(원작 Raid1 Defence), rapidRush = Rapid Rush(Raid2). churuRush·bigMouse는 [창작] 추가 모드다. 레이스·Guild Raid 행은 만들지 않는다.

### 8.4.3 입장권·소탕·단계 진행 공통 규칙

| 규칙 | 내용 | 값 |
|---|---|---|
| 입장권 | `02_playResource` 골목 수비전 입장권(2001001)~대왕쥐 소탕 입장권(2001004). `chargeType 2` = 날짜가 바뀌면 `chargeMax`로 **덮어쓰기**(누적 아님) | 하루 3장 [창작·HE임시] |
| 광고 충전 | `08_adPlacement` modeTicket. 광고 1회 = 고른 모드 입장권 +1. 횟수는 **모든 모드 공용** | 하루 3회 [창작·HE임시] |
| 야옹신 축복 | 켜져 있으면 날짜 변경 때 각 입장권 +1 | 8.5.9 |
| 단계 진행 | 클리어하면 `clears[contentId] = max(기존, stage+1)`. 다음 도전 = `min(clears, 마지막 단계)` | 원작 규칙 [창작·HE임시] |
| 소탕 | 조건 `clears ≥ 1`. 입장권 1장으로 **최고 클리어 단계(clears−1)**의 `clearReward`를 런 없이 지급. churuRush는 8.4.7 예외 | 원작 규칙 |
| 실패 | 보상 없음. 입장권은 소모됨 | |
| 카운터 | 클리어 시 퀘스트 카운터 `modeClear`(clearCondition = contentType) +1 | 8.5.1 |
| 날짜 기준 | 정본 `adDay()` L2423과 같은 UTC 날짜(D-77) | |

`02_playResource` 행 [창작]:

| id | key | type | chargeType | chargeSec | chargeMax | sort | active | icon |
|---|---|---|---|---|---|---|---|---|
| 골목 수비전 입장권(2001001) | ticket.alleyWar | modeTicket | 2 | 0 | 3 | 1 | 1 | UI_Icon_Ticket_AlleyWar |
| 우다다 질주 입장권(2001002) | ticket.rapidRush | modeTicket | 2 | 0 | 3 | 2 | 1 | UI_Icon_Ticket_RapidRush |
| 츄르 털이 입장권(2001003) | ticket.churuRush | modeTicket | 2 | 0 | 3 | 3 | 1 | UI_Icon_Ticket_ChuruRush |
| 대왕쥐 소탕 입장권(2001004) | ticket.bigMouse | modeTicket | 2 | 0 | 3 | 4 | 0 | UI_Icon_Ticket_BigMouse |
| 기운(2001005) | energy | stamina | 1 | 600 | 30 | 5 | 0 | UI_Icon_Energy |

기운(2001005)은 원작 스태미나 [창작·HE임시: 600초 1회복, 상한 30]. 정본에 없으므로 `active 0`(D-79).

### 8.4.4 402_modeRule (모드 규칙)

| 열 | 타입 | 뜻(정본 대응) |
|---|---|---|
| id / key | int / string | 402001001~ |
| ruleClass | string | `IModeRule` 구현 클래스 이름 |
| layout | int | 0 본편 구조(템플릿 → 보너스 상자 → Floor → 포털, `buildLevel` L1783~1807) / 1 아레나(전진 없음, 스폰) / 2 템플릿 + 보너스 상자 + 포털(Floor 없음) |
| forwardSpeed | float | 전진 m/s. 정본 4 L2030 |
| failOnWall | int | 1이면 벽 접촉 = 실패(정본 L2066~2070) |
| useLobbyDeck | int | 1 = 저장 시작 덱·무기 Level(`save.deck`, baseTier) 사용 |
| fixedWeaponTier | int | 0 = 없음. 1~8이면 시작 덱을 그 등급 1장으로, 제작 게이트 무기 등급도 그 등급 |
| craftPerMade | int | 제작 게이트가 1개 만들 때 주는 무기 수. 정본 1(L2057) |
| useLobbyUpgrade / useAreaUpgrade / useRebirthPerk / useMetaStats | int | 각 강화의 런 적용 여부(0이면 레벨 0으로 계산) |
| cardPoolId | string | 111_cardPool 행 |
| cycleGroup | int | `312_templateCycle.cycleGroup`. 0 = 본편 순환. 기본 0 |
| fieldGenKey | string | `313_fieldGen.key`. 기본 `default` |
| climbKey | string | `330_climb` 설정 행 key. 기본 `default` |
| hpSource | int | 0 = 현재 Area `hp` / 1 = 단계 행 `baseHp × hpMul` |
| runRewardToAccount | int | 0 없음 / 1 츄르·캣닢만 / 2 정본처럼 전부 |
| keepBars / useCheckpoint | int | 벽 HP 기록 / 체크포인트(정본 1/1) |
| themeId | int | 304 id. 0이면 현재 Area 테마 |
| arenaSpawnZ / arenaLaneX / arenaJitter / arenaLineZ | float / string / float / float | 아레나 스폰 z, 레인 x 목록(`/`), x 흔들림, 방어선 z |
| baseHpMax / xpPerKill / xpBase / xpStep / cardChoices / cardTierWeight | int / int / int / int / int / string | 수비전 전용(8.4.5) |

| 열 | alleyWar 402001001 | rapidRush 402001002 | churuRush 402001003 | bigMouse 402001004 |
|---|---|---|---|---|
| ruleClass | DefenceRule | FixedLoadoutRule | CourseRule | BossRule |
| layout | 1 | 0 | 2 | 1 |
| forwardSpeed | 0 [창작] | 4 [프로토] | 4 [프로토] | 0 [창작] |
| failOnWall | 0 | 1 [프로토] | 1 [프로토] | 0 |
| useLobbyDeck | 1 [창작] | 0 [원작] | 1 [창작] | 1 [창작] |
| fixedWeaponTier | 0 | 4 [창작: 원작 6/12단계 → C7 round(6×8/12)=4, 깡통 피해 58] | 0 | 0 |
| craftPerMade | 1 | 3 [원작 문당 무기 3] | 1 | 1 |
| useLobbyUpgrade | 1 | 0 [원작] | 1 | 1 |
| useAreaUpgrade | 1 | 0 [원작] | 1 | 1 |
| useRebirthPerk | 1 | 1 [원작 영구 능력치 적용] | 1 | 1 |
| useMetaStats | 1 | 1 [원작] | 1 | 1 |
| cardPoolId | defence | rush | main | defence |
| hpSource | 1 | 1 | 1 | 1 |
| runRewardToAccount | 0 [원작] | 0 [원작] | 1 [창작] | 0 |
| keepBars / useCheckpoint | 0 / 0 | 0 / 0 [원작] | 0 / 0 | 0 / 0 |
| themeId | 304001007 stallnight | 0 | 304001012 cafe | 304001003 harbor |
| arenaSpawnZ | 26 [창작] | — | — | 24 [창작] |
| arenaLaneX | -2.4/0/2.4 [프로토 벽 레인 L1793] | — | — | 0 |
| arenaJitter | 0.4 [창작] | — | — | 0 |
| arenaLineZ | 0.5 [창작] | — | — | 1.0 [창작] |
| baseHpMax | 10 [창작·HE임시] | — | — | — |
| xpPerKill / xpBase / xpStep | 1 / 4 / 2 [창작·HE임시] | — | — | — |
| cardChoices | 3 [창작·HE임시] | — | — | — |
| cardTierWeight | 50/25/15/8/2 [창작·HE임시] (tier 0~4) | — | — | — |

### 8.4.5 골목 수비전 (alleyWar) — 원작 Hell War 달동네판

**한 줄**: 몰려오는 쥐 떼에게서 생선 가게를 지켜라.

**규칙**

| 항목 | 정본 런 재사용 | 바뀌는 점 |
|---|---|---|
| 이동 | 드래그·방향키 좌우 이동, x ∈ [−3.2, 3.2], 추종 계수 14 (L2024~2025) | 전진 속도 0. 플레이어 z = 0 고정 |
| 사격 | 자동 사격·연사·치명·2연발·관통·분신·투사체 속도 20·사거리 22 전부 (L2071~2115) | 없음 |
| 카드 효과 | 8.3.3 전부(공용·신 카드) | 카드 획득 방식이 룰렛 → **레벨업 3택** |
| 적 | 정본 쥐(`drawMouse`) 그림·피격·사망 연출 | 이동 AI `Advance`: z가 매초 `enemySpeed`만큼 줄고 x는 고정. 플레이어 추격·츄르 훔치기 없음 |
| 실패 | — | 쥐가 `arenaLineZ`(0.5)에 닿으면 사라지고 가게 HP −1. HP 10 → 0이면 실패 |
| 레벨업 | — | 처치 1마리 = XP 1. 다음 레벨 필요 XP = `xpBase + xpStep × (레벨 − 1)` = 4, 6, 8 … 도달하면 **일시정지 후 카드 3장 중 1장 선택**. 등급은 가중치 50/25/15/8/2로 tier 0~4를 굴리고 그 tier 카드 중 무작위(없으면 한 단계 아래). 여러 번 오르면 대기열 |
| 택배 상자 | 정본 상자 그림 재사용 | `boxMs`마다 1개 스폰, 쥐처럼 전진하지만 가게 HP는 깎지 않음. 부수면 카드 1장 즉시(3택 없음, `cardPoolId`에서 가중치 굴림) |
| 승리 | — | 경과 ≥ `survivalSec` **그리고** 처치 ≥ `minKills`이면 스폰 중단 → 남은 쥐를 모두 처치하면 클리어. 처치가 모자라면 시간이 지나도 계속 스폰 |
| 계단·룰렛·게이트·Floor | — | 없음 |
| 런 재화 | — | 츄르·캣닢·생선 런 획득 없음. 보상은 클리어 보상만 |
| 스폰 | — | `spawnMs`마다 1마리, 동시 생존 `maxAlive` 넘으면 대기. x = `arenaLaneX` 중 무작위 + `±arenaJitter`, z = `arenaSpawnZ` |

**사용 오브젝트**(07 요청): `102_enemy` 행 `mouseDefence`(정본 쥐 그림, `moveAiId` = Advance, HP = 단계 `enemyHp`, 보상 없음), `105_objectKind` key `cardbox`(breakable, 보상 consumeType 601 Card, `108_receiveCondition` OnBreak), `109_moveAI` type **Advance 신규**(speed 단계 값, 방어선 도달 시 `ModeEvent.LineReached`).

**단계 표 (401_modeStage group 1, 17단계)**

- 생존·처치·생존 상한·스폰 간격(0~4) [창작·HE임시], 5~16은 4단계 값 유지(원작 저장소 규칙).
- `enemyHp` [창작] = C2 `R1(20 × 1.45^d)`. 근거: Area 1 무기 1등급 피해 10·연사 2/초에서 0단계 쥐를 2발에 잡아 스폰(0.83/초)보다 빨리 처치. 16단계 7,600은 8등급 무기(612)+카드로 12발 수준.
- `enemySpeed` [창작] 3.0/3.0/3.5/3.5/4.0, 5단계 이후 4.0 m/s(스폰 26m → 6.5~8.7초 도착).
- `boxMs` 9000 [창작·HE임시], `boxHp` = 2 × enemyHp [창작·HE임시 배율].
- 보상: 캣닢 C4 `20 + 5×ceil(d/2)` [원작], 츄르 `200 × (d+1)` [창작](원작 에너지 보상을 대체), 도전 메달 1 [원작 ChallengeToken 1].

| id | stage | survivalSec | minKills | maxAlive | spawnMs | enemyHp | enemySpeed | boxMs | boxHp | clearReward Type / Id / Value |
|---|---|---|---|---|---|---|---|---|---|---|
| 401001001 | 0 | 30 | 20 | 12 | 1200 | 20 | 3.0 | 9000 | 40 | 1;1;1 / 2;1;5 / 20;200;1 |
| 401001002 | 1 | 40 | 30 | 14 | 1000 | 29 | 3.0 | 9000 | 58 | 1;1;1 / 2;1;5 / 25;400;1 |
| 401001003 | 2 | 50 | 40 | 16 | 900 | 42 | 3.5 | 9000 | 84 | 1;1;1 / 2;1;5 / 25;600;1 |
| 401001004 | 3 | 60 | 50 | 18 | 800 | 61 | 3.5 | 9000 | 122 | 1;1;1 / 2;1;5 / 30;800;1 |
| 401001005 | 4 | 75 | 65 | 20 | 700 | 88 | 4.0 | 9000 | 176 | 1;1;1 / 2;1;5 / 30;1000;1 |
| 401001006 | 5 | 75 | 65 | 20 | 700 | 130 | 4.0 | 9000 | 260 | 1;1;1 / 2;1;5 / 35;1200;1 |
| 401001007 | 6 | 75 | 65 | 20 | 700 | 185 | 4.0 | 9000 | 370 | 1;1;1 / 2;1;5 / 35;1400;1 |
| 401001008 | 7 | 75 | 65 | 20 | 700 | 270 | 4.0 | 9000 | 540 | 1;1;1 / 2;1;5 / 40;1600;1 |
| 401001009 | 8 | 75 | 65 | 20 | 700 | 390 | 4.0 | 9000 | 780 | 1;1;1 / 2;1;5 / 40;1800;1 |
| 401001010 | 9 | 75 | 65 | 20 | 700 | 565 | 4.0 | 9000 | 1130 | 1;1;1 / 2;1;5 / 45;2000;1 |
| 401001011 | 10 | 75 | 65 | 20 | 700 | 820 | 4.0 | 9000 | 1640 | 1;1;1 / 2;1;5 / 45;2200;1 |
| 401001012 | 11 | 75 | 65 | 20 | 700 | 1200 | 4.0 | 9000 | 2400 | 1;1;1 / 2;1;5 / 50;2400;1 |
| 401001013 | 12 | 75 | 65 | 20 | 700 | 1700 | 4.0 | 9000 | 3400 | 1;1;1 / 2;1;5 / 50;2600;1 |
| 401001014 | 13 | 75 | 65 | 20 | 700 | 2500 | 4.0 | 9000 | 5000 | 1;1;1 / 2;1;5 / 55;2800;1 |
| 401001015 | 14 | 75 | 65 | 20 | 700 | 3600 | 4.0 | 9000 | 7200 | 1;1;1 / 2;1;5 / 55;3000;1 |
| 401001016 | 15 | 75 | 65 | 20 | 700 | 5300 | 4.0 | 9000 | 10600 | 1;1;1 / 2;1;5 / 60;3200;1 |
| 401001017 | 16 | 75 | 65 | 20 | 700 | 7600 | 4.0 | 9000 | 15200 | 1;1;1 / 2;1;5 / 60;3400;1 |

(보상 consumeData: type 1 Wealth, id 2 캣닢 · 1 츄르 · 5 도전 메달.)

**HUD 추가**([05](05_Screens.md)): 가게 HP 막대(10칸), 남은 시간, 처치 `n/minKills`, XP 막대·레벨, 카드 3택 팝업 `UIPopup_CardPick`(정본 카드 그림·등급색 재사용).

### 8.4.6 우다다 질주 (rapidRush) — 원작 Rapid Rush 달동네판

**한 줄**: 정해진 무기 하나로 끝까지 달려라.

**규칙**

| 항목 | 정본 런 재사용 | 바뀌는 점 |
|---|---|---|
| 코스 | `buildLevel` L1783~1807 그대로: 템플릿 → 보너스 상자 2줄 → Floor(아치 + 벽) → 포털 | 템플릿은 단계 행 `templateId`, Floor는 `floorGroup`(303 group 901·902) |
| 무기 | 사격·합성(3장 머지) 그대로 | 시작 덱 = 4등급(깡통) 1장. 제작 게이트 무기 등급도 4등급(`R.bt = 4`). 제작 1회 = 무기 3개 [원작] |
| 강화 | 부활 특전·메타 능력치 적용 [원작] | 로비 강화 3종·Area 강화 5종은 0으로 계산 [원작] |
| HP | 벽 HP = `h × BAL.scale × hpMul`(정본 fieldSpec에 배율 하나 추가). 적·상자 HP = `baseHp(100) × hpMul × 0.4·0.6·0.25 …`(정본 `Z.hp` 자리에 `100 × hpMul`) | Area와 무관 |
| 실패 / 클리어 | 벽 접촉 = 실패(L2066~2070), 포털 통과 = 클리어(L2029) | 체크포인트·벽 기록 없음(한 판 승부) |
| 런 재화 | — | 츄르·캣닢·생선 계정 반영 없음 [원작]. 보상은 클리어 보상만 |
| 계단·룰렛·카드 | 정본 그대로(`rush` 풀 = 본편 풀) | 없음 |

**모드용 Floor (303_areaFloor group 901·902)** [원작] — Area 1 Final과 같은 값.

| id | group | floor | h0 | h1 | rows | souls |
|---|---|---|---|---|---|---|
| 303901001 | 901 | 1 | 1 | 16 | 5 | 10 |
| 303902001 | 902 | 1 | 1 | 16 | 8 | 10 |
| 303902002 | 902 | 2 | 16 | 60 | 10 | 17 |
| 303902003 | 902 | 3 | 60 | 240 | 12 | 23 |

(souls는 생선 칸 배치·그림에만 쓰고 계정에 넣지 않는다.)

**단계 표 (401 group 2, 16단계)** — `hpMul` C3 `1 + 0.3 × max(0, d−1)` [원작 HP +30%/단계]. 보상: 종이 상자(01_wealth 14) / 황금 상자(16) / 도전 메달(5). 0·3·15단계 [원작], 나머지 [추정](원작 규칙성 「0~1 종이 2/3, 이후 황금 1~4개를 4단계씩 유지하며 종이 0/1/2/3 추가, 메달 1 고정」에서 복원).

| id | stage | templateId | floorGroup | hpMul | clearReward Type / Id / Value | 태그 |
|---|---|---|---|---|---|---|
| 401002001 | 0 | 310000001 | 901 | 1.0 | 1;1 / 14;5 / 2;1 | [원작] |
| 401002002 | 1 | 310000002 | 902 | 1.0 | 1;1 / 14;5 / 3;1 | [원작] |
| 401002003 | 2 | 310000002 | 902 | 1.3 | 1;1 / 16;5 / 1;1 | [추정] |
| 401002004 | 3 | 310000002 | 902 | 1.6 | 1;1;1 / 16;14;5 / 1;1;1 | [원작] |
| 401002005 | 4 | 310000002 | 902 | 1.9 | 1;1;1 / 16;14;5 / 1;2;1 | [추정] |
| 401002006 | 5 | 310000002 | 902 | 2.2 | 1;1;1 / 16;14;5 / 1;3;1 | [추정] |
| 401002007 | 6 | 310000002 | 902 | 2.5 | 1;1 / 16;5 / 2;1 | [추정] |
| 401002008 | 7 | 310000002 | 902 | 2.8 | 1;1;1 / 16;14;5 / 2;1;1 | [추정] |
| 401002009 | 8 | 310000002 | 902 | 3.1 | 1;1;1 / 16;14;5 / 2;2;1 | [추정] |
| 401002010 | 9 | 310000002 | 902 | 3.4 | 1;1;1 / 16;14;5 / 2;3;1 | [추정] |
| 401002011 | 10 | 310000002 | 902 | 3.7 | 1;1 / 16;5 / 3;1 | [추정] |
| 401002012 | 11 | 310000002 | 902 | 4.0 | 1;1;1 / 16;14;5 / 3;1;1 | [추정] |
| 401002013 | 12 | 310000002 | 902 | 4.3 | 1;1;1 / 16;14;5 / 3;2;1 | [추정] |
| 401002014 | 13 | 310000002 | 902 | 4.6 | 1;1;1 / 16;14;5 / 3;3;1 | [추정] |
| 401002015 | 14 | 310000002 | 902 | 4.9 | 1;1;1 / 16;14;5 / 4;1;1 | [추정] |
| 401002016 | 15 | 310000002 | 902 | 5.2 | 1;1;1 / 16;14;5 / 4;2;1 | [원작] |

`templateId` 310000001·310000002 = 정본 템플릿 1·2(07의 310_template id 규칙을 따름). 장비(2차)가 꺼져 있는 동안 상자는 **열면 캣닢으로 바뀐다**: 종이 20, 나무 60, 황금 150 [창작](`499_contentConfig` box.fallbackDia.*).

### 8.4.7 츄르 털이 (churuRush) — [창작] 추가 모드

**한 줄**: 츄르가 가득한 골목을 털어라. 츄르 수급용 모드.

**규칙**

| 항목 | 정본 런 재사용 | 바뀌는 점 |
|---|---|---|
| 코스 | 템플릿 + 보너스 상자 2줄 + 포털(`layout 2`, Floor 없음) | 전용 템플릿 310000101·310000102(아래) |
| 오브젝트 | 츄르 상자(coin)·캣닢 화분(gem)·벽돌 우물(tower)·계단 게이트·제작 게이트·계단·쥐 무리 전부 정본 | 상자 보상 = `20 + 10 × stage`(정본 `20 + 10 × zone` L1328의 zone 자리에 단계) |
| 쥐 | 정본 추격·츄르 훔치기 그대로(L2059~2064) | 훔치는 양 `5 + 4 × stage` |
| 실패 | 벽이 없어 실패하지 않는다 | 포털 통과 = 클리어 |
| 런 재화 | 런 츄르·캣닢 정본대로 쌓임 | 클리어 시 **런 츄르 × rewardMul**, 런 캣닢(정본 `diaCap` 상한 규칙 동일)을 계정에 넣는다. 생선 없음 |
| 소탕 | — | 그 단계에서 기록한 최고 계정 지급 츄르의 80% + clearReward [창작]. 기록이 없으면 소탕 불가 |

**전용 템플릿 (310_template·311_templateObject 행 요청)** [창작] — 정본 PLACE 형식(kind, x, z). 무리 오프셋은 정본 적 무리 그룹(314_spawnGroup) 재사용.

| 템플릿 | endZ | seq: kind (x, z) |
|---|---|---|
| 310000101 | 190 | 1 tower (0, 15) · 2 coin (−2.2, 30) · 3 coin (0, 30) · 4 coin (2.2, 30) · 5 sgate (−2.25, 45) · 6 enemy 무리 (0, 57.42) · 7 stair (0, 66.5) · 8 coin (−2.2, 88) · 9 gem (0, 90) · 10 coin (2.2, 88) · 11 cgate (2.25, 105) · 12 coin (−2.2, 122) · 13 coin (0, 124) · 14 coin (2.2, 122) · 15 sgate (2.25, 140) · 16 stair (0, 157.5) · 17 gem (−2.2, 172) · 18 coin (2.2, 175) |
| 310000102 | 230 | 1 tower (−2.17, 17) · 2 tower (2.2, 17) · 3 coin (−2.2, 34) · 4 gem (0, 36) · 5 coin (2.2, 34) · 6 sgate (0, 50) · 7 enemy 무리 (0, 57.42) · 8 stair (0, 73.5) · 9 coin (−2.2, 95) · 10 coin (0, 95) · 11 coin (2.2, 95) · 12 cgate (−2.25, 110) · 13 tower (0, 125) · 14 sgate (2.25, 140) · 15 coin (−2.2, 152) · 16 gem (2.2, 154) · 17 stair (0, 171.5) · 18 coin (−2.2, 195) · 19 coin (0, 197) · 20 coin (2.2, 195) · 21 gem (0, 215) |

**단계 표 (401 group 3, 10단계)** — `hpMul` [창작] 1,2,4,7,11,16,23,32,45,60(앞 단계 × 약 1.4~2, 상자 HP = `100 × hpMul × 0.6`). `rewardMul` [창작] `1 + 0.25 × d`.

| id | stage | templateId | hpMul | rewardMul | clearReward Type / Id / Value |
|---|---|---|---|---|---|
| 401003001 | 0 | 310000101 | 1 | 1.00 | 1 / 5 / 1 |
| 401003002 | 1 | 310000101 | 2 | 1.25 | 1 / 5 / 1 |
| 401003003 | 2 | 310000101 | 4 | 1.50 | 1 / 5 / 1 |
| 401003004 | 3 | 310000101 | 7 | 1.75 | 1 / 5 / 1 |
| 401003005 | 4 | 310000101 | 11 | 2.00 | 1 / 5 / 1 |
| 401003006 | 5 | 310000102 | 16 | 2.25 | 1;1 / 5;2 / 1;10 |
| 401003007 | 6 | 310000102 | 23 | 2.50 | 1;1 / 5;2 / 1;15 |
| 401003008 | 7 | 310000102 | 32 | 2.75 | 1;1 / 5;2 / 1;20 |
| 401003009 | 8 | 310000102 | 45 | 3.00 | 1;1 / 5;2 / 1;25 |
| 401003010 | 9 | 310000102 | 60 | 3.25 | 1;1 / 5;2 / 1;30 |

### 8.4.8 대왕쥐 소탕 (bigMouse) — [창작] 추가 모드, 2차

**한 줄**: 거대한 대왕쥐를 쓰러뜨려라. 피해량 시험용 보스 모드.

| 항목 | 정본 재사용 | 바뀌는 점 |
|---|---|---|
| 이동·사격·카드 | 수비전과 같음(전진 0, 레벨업 3택 없음 → 택배 상자만 카드 지급) | — |
| 보스 | 쥐 그림 2.2배 크기(새 스프라이트 `Enemy_BigMouse` 권장) | `102_enemy` 행 `bossMouse`(hw 2.2, hd 1.2), `arenaSpawnZ` 24에서 `bossSpeed`로 전진. `arenaLineZ` 1.0 도달 = 실패 |
| 졸개 | 정본 쥐 | `summonMs`마다 보스 앞에서 `summonCount`마리, HP = bossHp × 0.02, 이동 Advance 3.0 m/s, 방어선 도달 시 보스 HP +2% 회복 |
| 클리어 | — | 보스 HP 0 |

| id | stage | bossHp [창작 C2 5000×1.6^d, R1] | bossSpeed | summonMs | summonCount | boxMs | clearReward Type / Id / Value [창작] |
|---|---|---|---|---|---|---|---|
| 401004001 | 0 | 5000 | 0.45 | 8000 | 3 | 10000 | 1;1;1 / 2;1;5 / 30;1000;2 |
| 401004002 | 1 | 8000 | 0.45 | 8000 | 3 | 10000 | 1;1;1 / 2;1;5 / 40;2000;2 |
| 401004003 | 2 | 13000 | 0.45 | 7500 | 3 | 10000 | 1;1;1 / 2;1;5 / 50;3000;2 |
| 401004004 | 3 | 20000 | 0.5 | 7500 | 4 | 10000 | 1;1;1 / 2;1;5 / 60;4000;2 |
| 401004005 | 4 | 33000 | 0.5 | 7000 | 4 | 10000 | 1;1;1 / 2;1;5 / 70;5000;2 |
| 401004006 | 5 | 52000 | 0.5 | 7000 | 4 | 10000 | 1;1;1;1 / 2;1;5;16 / 80;6000;2;1 |
| 401004007 | 6 | 84000 | 0.55 | 6500 | 5 | 10000 | 1;1;1;1 / 2;1;5;16 / 90;7000;2;1 |
| 401004008 | 7 | 130000 | 0.55 | 6500 | 5 | 10000 | 1;1;1;1 / 2;1;5;16 / 100;8000;2;1 |
| 401004009 | 8 | 210000 | 0.6 | 6000 | 5 | 10000 | 1;1;1;1 / 2;1;5;16 / 110;9000;2;1 |
| 401004010 | 9 | 340000 | 0.6 | 6000 | 6 | 10000 | 1;1;1;1 / 2;1;5;16 / 120;10000;2;1 |

### 8.4.9 401_modeStage 열 정의

| 열 | 타입 | 쓰는 모드 | 뜻 |
|---|---|---|---|
| id | int | 전부 | `401{group 3}{stage+1 3}` |
| group / stage / active | int | 전부 | 400.stageGroup / 0부터 / 1·0 |
| templateId | int | rapidRush, churuRush | 310 id |
| floorGroup | int | rapidRush | 303 group |
| baseHp | int | rapidRush, churuRush | 정본 `Z.hp` 자리 값. 기본 100 [창작: Area 1 hp] |
| hpMul | float | rapidRush, churuRush | 8.4.6·8.4.7 |
| rewardMul | float | churuRush | 런 츄르 배율 |
| survivalSec / minKills / maxAlive / spawnMs / enemyHp / enemySpeed / boxMs / boxHp | int·float | alleyWar | 8.4.5 |
| bossHp / bossSpeed / summonMs / summonCount / boxMs | int·float | bigMouse | 8.4.8 |
| enemyId | int | alleyWar, bigMouse | 102 id(mouseDefence / bossMouse) |
| clearRewardType / Id / Value | consumeData | 전부 | 클리어·소탕 보상 |

검증기: `ruleClass`별 필수 열이 비어 있으면 로드 실패(예 DefenceRule 행에 `spawnMs` 0).

### 8.4.10 IModeRule 구현 범위

`MV.Run`은 Unity를 모르는 순수 C#이고 `MV.Content`가 `MV.Run`을 참조한다([10](10_Architecture.md)). 그래서 **런 쪽 확장점은 `MV.Run`이 정의하고, 모드 규칙은 `MV.Content`가 구현**한다. 이 문서가 가정하는 확장점(이름은 [04](04_GameRules.md)·[10](10_Architecture.md)이 정본):

```csharp
// MV.Run — 정본 규칙의 기본 구현은 MainRunRule
public interface IRunRule {
    RunSetup Build(RunContext ctx);               // 덱·무기 등급·강화 적용 여부·코스(템플릿·Floor·아레나)
    void Tick(RunState s, float dt);              // 스폰·타이머 같은 모드 전용 진행
    RunEnd CheckEnd(RunState s);                  // None / Clear / Fail
    void OnEvent(RunState s, in RunEvent e);      // Kill, Break, LineReached …
}
// MV.Content
public interface IModeRule : IRunRule {
    int ContentType { get; }
    ModeResult Settle(RunState s, ModeStageRow stage); // 보상 계산만. 지급은 MV.Meta
}
```

| 구현 | 모드 | 하는 일 | 정본에서 바꾸지 않는 것 |
|---|---|---|---|
| `MainRunRule`(MV.Run) | 본편 | 정본 규칙 전부 | — |
| `DefenceRule` | alleyWar | 아레나 생성(지면·벽 없음), 쥐·택배 상자 스폰 타이머, 가게 HP, XP·레벨업 대기열과 3택 요청 이벤트(`ModeEvent.CardPick`), 승리 판정 | 사격·카드 효과·피격·연출 |
| `FixedLoadoutRule` | rapidRush | 덱·제작 등급 고정, `craftPerMade`, 강화 0 처리, HP 배율, 체크포인트·벽 기록 끔 | 코스 생성·계단·룰렛 |
| `CourseRule` | churuRush | Floor 없는 코스, 상자 보상·훔치기 양에 단계 대입, 결과 츄르 배율, 최고 기록 저장 요청 | 나머지 전부 |
| `BossRule` | bigMouse | 보스·졸개 스폰, 회복, 실패선 | 사격·카드 |

모드 중 앱이 꺼지면 그 판은 버린다(입장권 환불 없음) [창작]. 정본도 런 중 상태를 저장하지 않는다.

---
## 8.5 메타 시스템 달동네판

### 8.5.0 공통 — 우선순위와 메타 능력치

| 시스템 | 원작 대응 | 표 | 우선순위 | 이유 |
|---|---|---|---|---|
| 퀘스트(일일·월간)·일일 종합·업적 | 퀘스트 | 410·413·420 | **1차** | 런 수치에 영향 없음. 정본 동등 유지 |
| 출석 | 일일 보상 | 485 | **1차** | 같음 |
| 연구 | 연구 | 440 | **1차** | 연구 0이면 정본과 같음. 단순 타이머 |
| 상점(결제 스텁) | 상점·오퍼 | 700·701·702 | **1차**(캣닢·광고 제거·패키지·메달 교환) | 결제 스텁만 |
| 야옹신 2번째(flame)·신 레벨 | 신 시스템 | 113·495 | 1차(flame 해금만), 레벨은 2차 | 8.3 |
| 펫 | 펫 | 430·431 | 2차 | 동행 그림·자동 사격 연출 필요 |
| 친구 고양이(동료) | 챔피언 | 450·451 | 2차 | 지도 표시 Area 6 [프로토], 스킬 연출 필요 |
| 룬 | 룬 | 460 | 2차 | 패스와 같이 |
| 패스 | 패스 | 410·414 | 2차 | BM 미정 |
| 장비·상자 | 장비 | 470·471·802 | 2차 | 지도 표시 Area 3 [프로토], 인벤토리 UI 큼 |
| 축복 | 축복 | 480 | 2차 | 부활·모드와 같이 시험 |
| 지도 추격(장마 구름) | 로드맵·호드 | 490 | 2차, **기본 꺼짐** | 정본 진행 감각을 바꿈(D-78) |
| 부활 추가분 | 부활 부가 기능 | 499 | 2차 | 8.5.14 |
| 스태미나(기운) | 스태미나 | 02_playResource 기운(2001005) | 꺼짐 | 정본에 없음(D-79) |

**메타 능력치(statKey) 7종** — 펫·연구·친구 고양이·신 패시브·신 레벨·장비·축복이 모두 이 7개만 쓴다(원작 공통 효과 키 7종 구조). 각 시스템 값을 % 단위로 **합산**한 뒤, 런 시작 때 `RunModifiers`로 `MV.Run`에 넘긴다. 정본 식에는 **곱 인자 하나**로 들어간다. 합이 0이면 정본과 같다.

| statKey | 정본 식에 붙는 자리 | 근거 |
|---|---|---|
| Dmg | `baseDmg() × (1 + Dmg/100)` | L1751 |
| FireRate | 발사 간격 분모 `× (1 + FireRate/100)` | L2074 |
| Range | 사거리 `22 × (1 + 0.1·aup.range) × (1 + Range/100)` | L2094 |
| CritDmg | `critMul() + CritDmg/100` | L1752 |
| Gold | `goldMul() × (1 + Gold/100)` | L1746 |
| CritChance | `crC + CritChance/100` | L2075 |
| DoubleChance | `dbC = min(1, dbC + DoubleChance/100)` | L2075 |

`statKey` 값은 표에 문자열로 적는다(`Dmg` 등). [04](04_GameRules.md)에 `RunModifiers` 7필드 요청이다.

### 8.5.1 퀘스트·일일 종합·업적 (410_questGroup, 413_repeatQuest, 420_achievement) — 1차

**clearType (퀘스트 카운터)** — `QuestClearType` 열거. 카운터는 평생 누적과 기간 누적에 동시에 더한다(원작 구조). 본편 런에서만 오르는 것은 「본편」 표시.

| clearType | key | 오르는 때(정본 근거) | clearCondition |
|---|---|---|---|
| 1 | runComplete | 본편 결과 수령 `collect` L2424 +1 | 0 |
| 2 | stairClimb | 계단 오르기 끝 `endClimb`, 오른 칸 수(본편) | 0 |
| 3 | wallBreak | Floor 벽 파괴 L1936 +1(본편) | 0 |
| 4 | goldEarn | `collect` 때 계정 츄르 증가분 | 0 |
| 5 | soulEarn | `collect` 때 `R.souls` | 0 |
| 6 | weaponCraft | 제작 게이트 수령 무기 수 L2057(본편) | 0 |
| 7 | areaUpgrade | 장난감 바구니 구매 L2530 | 0 |
| 8 | lobbyUpgrade | 로비 강화 구매 L2641 | 0 전체 / 1 캣타워 / 2 스크래쳐 / 3 생선 건조대 |
| 9 | areaOpen | Area 개방(`pass`) L2430 | 0 |
| 10 | modeClear | 모드 클리어 | 0 전체 / contentType |
| 11 | adWatch | 보상 광고 완료 | 0 |
| 12 | rebirth | 부활 `doRebirth` L2559 | 0 |
| 13 | petSummon | 펫 소환 | 0 |
| 14 | friendUpgrade | 친구 고양이 레벨업 | 0 |
| 15 | boxOpen | 상자 열기 | 0 |
| 16 | godLevelUp | 신 레벨업 | 0 |
| 17 | researchDone | 연구 완료 | 0 |
| 18 | loginDay | 출석 수령 | 0 |
| 20 | dailyPoint | 일일 퀘스트 포인트(consumeType 300) 누적 | 0 |

**410_questGroup** (정크버스터즈 열): `id, group, name(작성용), periodType, category, groupType, progressScope, selectionType, selectionCount`.
- periodType: 0 없음 / 2 일일(UTC 날짜) / 3 월간(UTC `yyyyMM`) / 5 이벤트 주기(8.5.13 `event.*`).
- category: 1 일일 / 2 월간 / 3 업적 / 100 패스. groupType: 1 목록형 / 2 단계형(업적) / 3 포인트·레벨형(일일 종합·패스). progressScope: 1 평생 누적 / 2 기간 누적.

| id | group | name | periodType | category | groupType | progressScope | 우선 |
|---|---|---|---|---|---|---|---|
| 410001001 | 1 | 일일 퀘스트 | 2 | 1 | 1 | 2 | 1차 |
| 410001002 | 2 | 일일 종합 | 2 | 1 | 3 | 2 | 1차 |
| 410001003 | 3 | 월간 퀘스트 | 3 | 2 | 1 | 2 | 1차 |
| 410001101~106 | 101~106 | 패스 6종(8.5.6) | 3/3/3/0/0/5 | 100 | 3 | 2 | 2차 |
| 410001300~304 | 300~304 | 업적(런·Area·벽·모드·부활) | 0 | 3 | 2 | 1 | 1차 |

**413_repeatQuest** (정크버스터즈 열 + `clearValueStep`): `id, questGroup, step, sort, periodType, clearType, clearCondition, clearValue, clearValueStep, rewardType, rewardId, rewardValue`.
- 일일: 한 번 받으면 그날 끝. 월간: 받을 때마다 목표 = `clearValue + clearValueStep × 받은 횟수`로 반복(원작 구조). 목표·보상 [창작·HE임시](원작 저장소 9행), 모드·광고·부활 행 [창작].
- 보상의 `300` = DailyQuestPoint(07_consumeType 300), id 0.

| id | group | sort | clearType / cond | clearValue | step | rewardType / Id / Value |
|---|---|---|---|---|---|---|
| 413001001 | 1 | 1 | 1 runComplete / 0 | 3 | 0 | 1;300 / 2;0 / 10;20 |
| 413001002 | 1 | 2 | 2 stairClimb / 0 | 10 | 0 | 1;300 / 2;0 / 10;20 |
| 413001003 | 1 | 3 | 3 wallBreak / 0 | 20 | 0 | 1;300 / 2;0 / 10;20 |
| 413001004 | 1 | 4 | 4 goldEarn / 0 | 500 | 0 | 1;300 / 2;0 / 10;20 |
| 413001005 | 1 | 5 | 10 modeClear / 0 | 1 | 0 | 1;300 / 5;0 / 1;20 |
| 413001006 | 1 | 6 | 11 adWatch / 0 | 2 | 0 | 1;300 / 2;0 / 10;20 |
| 413002001 | 2 | 1 | 20 dailyPoint / 0 | 40 | 0 | 1 / 1 / 300 |
| 413002002 | 2 | 2 | 20 dailyPoint / 0 | 80 | 0 | 1 / 2 / 20 |
| 413002003 | 2 | 3 | 20 dailyPoint / 0 | 120 | 0 | 1;1 / 2;5 / 30;1 |
| 413003001 | 3 | 1 | 5 soulEarn / 0 | 50 | 50 | 1 / 2 / 30 |
| 413003002 | 3 | 2 | 6 weaponCraft / 0 | 30 | 30 | 1 / 2 / 30 |
| 413003003 | 3 | 3 | 7 areaUpgrade / 0 | 5 | 5 | 1 / 2 / 30 |
| 413003004 | 3 | 4 | 8 lobbyUpgrade / 3 | 5 | 5 | 1 / 2 / 30 |
| 413003005 | 3 | 5 | 9 areaOpen / 0 | 1 | 1 | 1 / 2 / 50 |
| 413003006 | 3 | 6 | 10 modeClear / 0 | 5 | 5 | 1 / 2 / 30 |
| 413003007 | 3 | 7 | 12 rebirth / 0 | 1 | 1 | 1 / 2 / 50 |

- 일일 포인트 합 120 = 6개 전부 완료. 리롤은 원작 저장소 코드에 없어 넣지 않는다.
- 모드 보상 문구·아이콘은 `07_consumeType`이 정한다(코드 하드코딩 금지).

**420_achievement** (정크버스터즈 열): `id, questGroup, progress, clearType, clearCondition, prevClearValue, clearValue, rewardType, rewardId, rewardValue`. 값 [창작]. 단계를 차례로 깬다(`progress` 순).

| group | clearType | clearValue 단계(prev → value) | 보상 캣닢(단계별) |
|---|---|---|---|
| 300 | 1 runComplete | 10, 50, 100, 300, 1000 | 10, 20, 30, 50, 100 |
| 301 | 9 areaOpen | 1, 5, 10, 15, 30 | 20, 40, 60, 80, 200 |
| 302 | 3 wallBreak | 100, 1000, 10000 | 10, 30, 60 |
| 303 | 10 modeClear | 10, 50, 200 | 20, 40, 80 |
| 304 | 12 rebirth | 1, 5, 10 | 30, 60, 100 |

행 생성: id = `420{group}{progress 3}`, `prevClearValue` = 앞 단계 값(첫 단계 0), reward = `1 / 2 / 캣닢`. 예 `420300001	300	1	1	0	0	10	1	2	10`.

### 8.5.2 펫 (430_pet, 431_petBuild, 16_levelPrice 그룹 301, 802 소환) — 2차

**규칙** [창작·HE임시, 레벨 비용만 원작]:
- 소환: 캣닢 100 → `802_randomBox` group 802430001 굴림 → 펫 카드 1장(consumeType 620 PetCard) + 사료 10.
- 카드 2장 모이면 해금(Lv1, 카드 2장 차감). 레벨업: `16_levelPrice` 그룹 301 행의 `needPiece`(카드)·`price`(사료) 소모. 최대 Lv40.
- 사료: 본편 Area 돌파 아치 도달 시 30.
- 장착 2칸. 장착 펫 보너스 = `value × (1 + 0.2 × (레벨 − 1))`, statKey 합산.
- 자동 공격: 장착 펫이 `skillEveryMs`마다 앞쪽 가장 가까운 대상 `skillTargets`개에 `baseDmg × (skillDmgPct + skillLevelPct × (레벨−1)) / 100`. 새 런 동작 `CompanionShot`([04](04_GameRules.md) 요청). 펫 그림은 플레이어 뒤를 따라다닌다(`sprite` 열).
- 빌드: 열린 칸 = `min(3, 1 + floor(레벨/10))`. 잠그지 않은 칸 전부 다시 굴림, 비용 털실(01_wealth 7) 10/20/40(잠금 0/1/2개). 칸마다 잠금 토글.

**430_pet 열**: `id, key, rarity(0~4), statKey, value(Lv1 %), skillEveryMs, skillDmgPct, skillTargets, skillLevelPct, levelPriceGroup, icon, sprite, active`. 스킬 값은 등급별 [창작·HE임시]: 0 → 2000ms·20%, 1 → 1800·30, 2 → 1600·45, 3 → 1400·70, 4 → 1200·100, 대상 1, 레벨당 +3%p.

| id | key | 이름 [창작] | rarity | statKey | value | 소환 weight(802) |
|---|---|---|---|---|---|---|
| 430001001 | sparrow | 참새 | 0 | Dmg | 5 [창작·HE임시] | 1098 |
| 430001002 | pigeon | 비둘기 | 0 | Gold | 8 | 1099 |
| 430001003 | hamster | 햄스터 | 0 | FireRate | 4 | 1099 |
| 430001004 | goldfish | 금붕어 | 0 | Range | 4 | 1099 |
| 430001005 | snail | 달팽이 | 0 | CritChance | 2 | 1099 |
| 430001006 | squirrel | 다람쥐 | 1 | FireRate | 6 | 659 |
| 430001007 | hedgehog | 고슴도치 | 1 | CritDmg | 15 | 659 |
| 430001008 | duck | 오리 | 1 | Gold | 12 | 659 |
| 430001009 | rabbit | 토끼 | 1 | Dmg | 8 | 659 |
| 430001010 | raccoon | 너구리 | 2 | Dmg | 12 | 440 |
| 430001011 | owl | 부엉이 | 2 | CritChance | 5 [창작·HE임시] | 440 |
| 430001012 | otter | 수달 | 2 | DoubleChance | 4 | 440 |
| 430001013 | fox | 여우 | 3 | CritDmg | 40 | 220 |
| 430001014 | crane | 두루미 | 3 | FireRate | 12 | 220 |
| 430001015 | moonRabbit | 달토끼 | 4 | CritDmg | 60 [창작·HE임시] | 55 |
| 430001016 | goldKoi | 황금 잉어 | 4 | Gold | 40 | 55 |

rate는 원작 저장소 가중치 20/12/8/4/1(합 182)을 10000으로 환산(합이 10000이 되도록 첫 행 −1). icon `UI_Icon_Pet_{key}`, sprite `Pet_{key}`.

**16_levelPrice 그룹 301 (39행)** [원작] — `targetLevel` = 현재 레벨 + 1(2~40). `needPiece` = 카드, `priceType 1 / priceId 6(사료) / priceValue`.
- 카드: 현재 Lv 1~3 → 2, 4~8 → 3, 9~12 → 4, 13~17 → 5, 18~22 → 6, 23~27 → 7, 28~31 → 8, 32~36 → 9, 37~39 → 10.
- 사료: 현재 Lv 1~25 → 10×Lv, 26~35 → 275 + 25×(Lv−26), 36 → 550, 37 → 600, 38 → 650, 39 → 750.
- 예: `16301002	301	2	1	6	10	2`, `16301026	301	26	1	6	250	7`, `16301040	301	40	1	6	750	10`.

**431_petBuild 열**: `id, tier(0~3), tierRate, statKey, valueMin, valueMax`. 굴림: tier를 `tierRate`(50/30/15/5 %, 10000 기준 5000/3000/1500/500)로 고르고, 그 tier의 4행 중 균등, 값은 min~max 정수 균등 [창작·HE임시].

| statKey | tier 0 | tier 1 | tier 2 | tier 3 |
|---|---|---|---|---|
| Dmg | 2~5 | 4~10 | 6~15 | 10~20 |
| CritChance | 1~2 | 2~4 | 3~6 | 5~8 |
| CritDmg | 5~10 | 10~20 | 15~30 | 25~50 |
| DoubleChance | 1~2 | 2~3 | 3~5 | 4~8 |

id = `431{tier+1 3}{순번 3}`(순번 Dmg 1, CritChance 2, CritDmg 3, DoubleChance 4).

### 8.5.3 연구 (440_research) — 1차

정크버스터즈 `17_labo` 구조(group, statType, statValue, price)에 시간 열을 더한다. 시설 이름 「고양이 공부방」 [창작].

**규칙**: 시작할 때 비용을 먼저 내고 완료 시각을 저장한다. 앱이 꺼져도 시각이 지나면 완료다. 계열(group)마다 타이머가 따로라 5계열 동시 진행. 즉시 완료 없음. 완료된 가장 높은 단계의 `statValue`(누적값)를 메타 능력치에 더한다. 해금 Area 5(05 행 5001009) [창작].

**열**: `id, group, level, icon, statType, statValue, priceType, priceId, priceValue, seconds, active`.

| group | 계열 이름 [창작] | statType | statValue Lv1~5(누적 %) [창작·HE임시] |
|---|---|---|---|
| 1 | 발톱 다듬기 | Dmg | 2 / 4 / 6 / 8 / 10 |
| 2 | 냥펀치 연습 | FireRate | 2 / 4 / 6 / 8 / 10 |
| 3 | 눈썰미 | Range | 2 / 4 / 6 / 8 / 10 |
| 4 | 급소 공부 | CritDmg | 5 / 10 / 15 / 20 / 25 |
| 5 | 츄르 냄새 | Gold | 5 / 10 / 15 / 20 / 25 |

모든 계열 공통: 비용 캣닢(`1 / 2 / v`) v = 10 / 20 / 30 / 40 / 50 [원작 값, 재화는 창작], 시간 50 / 120 / 900 / 2400 / 4800초 [원작]. id = `440{group 3}{level 3}`. 예 `440001001	1	1	UI_Icon_Research_claw	Dmg	2	1	2	10	50	1`. 계열 이름은 name 컬렉션 키 `440{group}000`(계열 머리 행)을 쓴다 — 07에 「묶음 이름은 group 000 키」 규칙 요청.

### 8.5.4 친구 고양이 (450_friend, 451_friendPower, 16_levelPrice 그룹 302) — 2차

원작 챔피언을 정본 지도 「🐾 동료 · 함께 다니는 친구 고양이」(Area 6, L2581~2586) [프로토]로 옮긴다. 정본 분신(친구 부르기 카드)과는 별개다.

**규칙** [창작·HE임시]:
- 영입: `unlockPrice`(동료 간식 01_wealth 10) → Lv1.
- 레벨업(최대 30): `16_levelPrice` 그룹 302 — 공용 간식(01_wealth 11) `20 + 10 × 현재Lv` + 그 친구 전용 코인(consumeType 621, id 0 = 이 친구) `2 + 현재Lv`. 전용 코인이 모자라면 만능 간식(01_wealth 12)으로 1:1 채운다.
- 본편 Area 돌파 아치 도달 시 장착 친구마다 전용 코인 +2.
- 우정 단계(451): 해금 레벨 도달 후 공용 간식으로 1회 구매, 효과 영구.
- 장착 슬롯 기본 1, 캣닢 `200 × 현재 슬롯 수`로 최대 3.
- 보너스 = `baseValue + perLevel × (레벨−1) + 보유 우정 단계 값`.
- 스킬: `skillTrigger` 1 Hits(본체 n번째 볼리마다) / 2 Chance(명중당 n% 확률) / 3 Seconds(n ms마다), 피해 `baseDmg × (skillDmgPct + 5 × (레벨−1)) / 100`, 대상 `skillTargets`개(앞쪽 가까운 순). 런 동작 `CompanionShot` 재사용.
- 그림: 장착 친구는 HUD 초상만 1차 표시. 월드 동행은 2차 결정(정본 분신 자리 `CLONE_OFF` L831과 겹침).

**450_friend 열**: `id, key, rarity, statKey, baseValue, perLevel, maxLevel, unlockPriceType, unlockPriceId, unlockPriceValue, skillTrigger, skillEvery, skillDmgPct, skillTargets, levelPriceGroup, icon, portrait, active`.

| id | key | 이름 [창작] | rarity | statKey | base + perLv [창작·HE임시] | 영입(간식) | 스킬 trigger / every / dmg% / 대상 [창작] |
|---|---|---|---|---|---|---|---|
| 450001001 | cheese | 치즈 | 0 | Dmg | 5 + 1 | 20 | 1 / 10 / 80 / 1 |
| 450001002 | mackerel | 고등어 | 0 | Gold | 8 + 2 | 20 | 1 / 10 / 80 / 1 |
| 450001003 | calico | 삼색이 | 1 | Range | 5 + 1 | 40 | 2 / 10 / 120 / 1 |
| 450001004 | tuxedo | 턱시도 | 1 | CritChance | 3 + 1 | 40 | 2 / 10 / 120 / 1 |
| 450001005 | cow | 젖소 | 1 | FireRate | 5 + 1 | 40 | 2 / 10 / 120 / 1 |
| 450001006 | blackie | 까망이 | 2 | CritDmg | 15 + 3 | 80 | 3 / 5000 / 180 / 2 |
| 450001007 | gray | 회색이 | 2 | Dmg | 10 + 2 | 80 | 3 / 5000 / 180 / 2 |
| 450001008 | boss | 대장 노랑이 | 3 | Dmg | 15 + 3 | 150 | 3 / 6000 / 250 / 3 [창작·HE임시] |
| 450001009 | siamese | 꼬마 샴 | 3 | DoubleChance | 5 + 1 | 150 | 3 / 6000 / 250 / 3 |
| 450001010 | persian | 구름 페르시안 | 3 | FireRate | 12 + 2 | 150 | 3 / 6000 / 250 / 3 |

maxLevel 30 전부. icon `UI_Icon_Friend_{key}`, portrait `Portrait_Friend_{key}`.

**451_friendPower** [창작·HE임시]: `id, unlockLevel, priceType, priceId, priceValue, statKey, value, icon`.

| id | 이름 | unlockLevel | price | statKey | value |
|---|---|---|---|---|---|
| 451001001 | 단짝 | 5 | 1 / 11 / 100 | CritChance | 3 |
| 451001002 | 베프 | 10 | 1 / 11 / 300 | Dmg | 10 |
| 451001003 | 평생 친구 | 20 | 1 / 11 / 800 | CritDmg | 30 |

**16_levelPrice 그룹 302 (29행)**: targetLevel 2~30, `priceType 1;621 / priceId 11;0 / priceValue (20+10c);(2+c)`, c = targetLevel − 1. 예 `16302002	302	2	1;621	11;0	30;3`. (정본 원작 식 「공용 20 + 10×현재Lv, 고유 2 + 1×현재Lv」에서 현재Lv = c.)

### 8.5.5 룬 — 별사탕 (460_rune, 802 group 802460001) — 2차

**규칙** [창작·HE임시]: 별사탕(01_wealth 13) 1개를 고른 패스에 쓰면 등급을 굴려 그 패스 XP를 더한다. 굴림은 `802_randomBox` group 802460001(rewardType 500 Pass, rewardId 0 = 고른 패스).

| 460 id | 등급 이름 [창작] | color | weight | 패스 XP |
|---|---|---|---|---|
| 460001001 | 하얀 별사탕 | #e8e8e8 | 6000 | 20 |
| 460001002 | 초록 별사탕 | #46c35a | 2500 | 40 |
| 460001003 | 파란 별사탕 | #3a8ff0 | 1200 | 60 |
| 460001004 | 금빛 별사탕 | #ffd23a | 300 | 100 |

460 열: `id, grade, color, icon, randomBoxRowId`(결과 표시용). 확률·XP는 802 행이 정본이다.

### 8.5.6 패스 (410 group 101~106, 414_passQuest) — 2차

**규칙**: 패스 XP = 해당 카운터의 기간 누적 + 별사탕 보너스. 레벨 L 도달 = XP ≥ 그 행 `clearValue`(누적). 수령은 도달한 모든 단계의 무료(+프리미엄 구매 시 프리미엄)를 한 번에. 기간이 바뀌면 진행·수령·프리미엄 초기화. periodType 0(펀드)은 초기화 없음. 프리미엄 구매 = 701 상품 보상 `500 / 패스의 410 행 id(410001101~106) / 1`(07 §7.8.12 idKind row).

**414_passQuest 열**(정크버스터즈): `id, questGroup, banner, step, sort, periodType, clearType, clearCondition, clearValue, rewardType, rewardId, rewardValue, premiumRewardType, premiumRewardId, premiumRewardValue`.

| group | 이름 [창작] | periodType | clearType | 구간 XP(L) | clearValue(누적) | 무료 보상 | 프리미엄 보상 | 상품 |
|---|---|---|---|---|---|---|---|---|
| 101 | 냥냥 패스 | 3 | 1 runComplete | 5L | 2.5·L(L+1) | 츄르 500L `1/1/500L` | 캣닢 50 `1/2/50` | 701004001 |
| 102 | 동료 패스 | 3 | 14 friendUpgrade | 2L | L(L+1) | 공용 간식 20 `1/11/20` | 만능 간식 20 `1/12/20` | 701004002 |
| 103 | 펫 패스 | 3 | 13 petSummon | 3L | 1.5·L(L+1) | 사료 50L `1/6/50L` | 털실 10 `1/7/10` | 701004003 |
| 104 | 생선 펀드 | 0 | 5 soulEarn | 50L | 25·L(L+1) | 캣닢 20 | 캣닢 100 | 701004004 |
| 105 | 도전 펀드 | 0 | 10 modeClear | 1L | 0.5·L(L+1) | 별사탕 1 `1/13/1` | 별사탕 3 | 701004005 |
| 106 | 기간 펀드 | 5 | 3 wallBreak | 30L | 15·L(L+1) | 츄르 1000L | 나무 상자 1 `1/15/1` | 701004006 |

값 전부 [창작·HE임시](원작 저장소 「구간 XP 차감식」을 누적으로 바꿈). L = 1~10. id = `414{group}{L 3}`. 예(냥냥 패스 Lv1·Lv10):
```
414101001	101	Banner_Pass_Battle	1		3	1	0	5	1	1	500	1	2	50
414101010	101	Banner_Pass_Battle	10		3	1	0	275	1	1	5000	1	2	50
```

### 8.5.7 상점 (700_productTab, 701_product, 702_shopSection) — 1차(결제 스텁)

**규칙**: `priceType 101 Inapp`은 `PurchaseManager` 스텁이 즉시 성공을 돌려준다(가격은 원화 표시용). `100 AD`는 보상 광고(`08_adPlacement`). `1 Wealth`는 재화 차감. 노출 조건 `minArea`·`minLevel`, 구매 제한 `chargeType`(0 무제한 / 1 평생 `chargedQuantity`회 / 2 일일 `chargedQuantity`회), `cooldownHours`, `perArea`(Area마다 1회) [창작·HE임시 규칙]. 기록 `purchases[id] = {count, unix, area}`.

**701_product 열**: 정크버스터즈 열(`id, tabId, active, sort, popupType, banner, icon, displayImage, badgeType, badgeValue, chargeType, chargedQuantity, rewardType/Id/Value, priceType/Id/Value, googleProductId, appleProductId, startTime, endTime`) + `minArea, minLevel, cooldownHours, perArea`.

**700_productTab**: 700001001 무료 · 700001002 캣닢 · 700001003 상자 · 700001004 패스 · 700001005 패키지 · 700001006 메달 교환. **702_shopSection** 열 `id, active, sort, sectionType, hideIfEmpty, tabIds(/)`: 702001001 상점(무료/캣닢/패키지), 702001002 상자(상자), 702001003 패스(패스), 702001004 메달 교환(메달 교환).

| id | 이름 [창작] | tab | reward Type / Id / Value | price Type / Id / Value | 제한 | 조건 | 우선 |
|---|---|---|---|---|---|---|---|
| 701001001 | 오늘의 츄르 | 무료 | 1 / 1 / 500 | 100 / 0 / 1 (광고 shopFree) | 일일 2 | — | 1차 |
| 701002001 | 캣닢 한 줌 | 캣닢 | 1 / 2 / 100 | 101 / 0 / 1200 | 무제한 | — | 1차 |
| 701002002 | 캣닢 한 봉지 | 캣닢 | 1 / 2 / 600 | 101 / 0 / 6000 | 무제한 | — | 1차 |
| 701002003 | 캣닢 한 상자 | 캣닢 | 1 / 2 / 3500 | 101 / 0 / 29000 | 무제한 | — | 1차 |
| 701002004 | 광고 제거 | 캣닢 | 1 / 17 / 1 | 101 / 0 / 5900 | 평생 1 | — | 1차 |
| 701003001 | 종이 상자 | 상자 | 1 / 14 / 1 | 1 / 1 / 500 | 무제한 | — | 2차 |
| 701003002 | 나무 상자 | 상자 | 1 / 15 / 1 | 1 / 2 / 50 | 무제한 | — | 2차 |
| 701003003 | 황금 상자 | 상자 | 1 / 16 / 1 | 1 / 2 / 300 | 무제한 | — | 2차 |
| 701003004 | 나무 상자 묶음 | 상자 | 1 / 15 / 3 | 101 / 0 / 2900 | 무제한 | — | 2차 |
| 701004001~006 | 패스·펀드 6종 | 패스 | 500 / 410001101~106 / 1 | 101 / 0 / 12000·12000·12000·6000·6000·6000 | 펀드 104·105 평생 1 | — | 2차 |
| 701005001 | 새내기 꾸러미 | 패키지 | 1;1;1 / 2;1;6 / 300;3000;100 | 101 / 0 / 3300 | 평생 1 | — | 1차 |
| 701005002 | Area 응원 꾸러미 | 패키지 | 1;1;1 / 1;2;4 / 5000;200;5 | 101 / 0 / 4900 | perArea, 24시간 | minArea 3 | 1차 |
| 701005003 | 털실 묶음 | 패키지 | 1 / 7 / 50 | 101 / 0 / 2900 | 무제한 | — | 2차 |
| 701005004 | 야옹신 방울 묶음 | 패키지 | 1;1 / 8;9 / 100;20 | 101 / 0 / 4900 | 무제한 | — | 2차 |
| 701005005 | 동료 간식 묶음 | 패키지 | 1;1;1 / 10;11;12 / 100;100;50 | 101 / 0 / 4900 | 무제한 | — | 2차 |
| 701005006~008 | 축복 카드 3종(목숨·야옹신·장비) | 패키지 | 630 / 480001001·002·004 / 30 | 101 / 0 / 5900 | 무제한 | — | 2차 |
| 701005009 | 야옹신 세트 꾸러미 | 패키지 | 1;1 / 16;1 / 5;10000 | 101 / 0 / 9900 | 24시간 | minArea 6 | 2차 |
| 701005010 | 별사탕 팩 | 패키지 | 1 / 13 / 10 | 101 / 0 / 2900 | 무제한 | — | 2차 |
| 701006001 | 캣닢 교환 | 메달 교환 | 1 / 2 / 50 | 1 / 5 / 5 | 일일 3 | — | 1차 |
| 701006002 | 츄르 교환 | 메달 교환 | 1 / 1 / 3000 | 1 / 5 / 10 | 일일 3 | — | 1차 |
| 701006003 | 방울 교환 | 메달 교환 | 1 / 8 / 10 | 1 / 5 / 20 | 일일 1 | — | 2차 |
| 701006004 | 황금 상자 교환 | 메달 교환 | 1 / 16 / 1 | 1 / 5 / 30 | 일일 1 | — | 2차 |

가격·구성 [창작·HE임시](메달 교환·오늘의 츄르는 [창작]). `googleProductId`·`appleProductId`는 `mv_` 접두 SKU 가칭(예 `mv_dia_100`). 원작 「Area 오퍼 Lv15」는 정본에 Level 조건 체계가 약해 `minArea 3`으로 바꿨다. 광고 제거(01_wealth 17)는 **강제 전면 광고만** 끈다(보상형 유지). 정본 강제 광고 조건은 `mul === 1 && save.area >= 3` L2434.

### 8.5.8 장비·상자 (470_gearPart, 471_gearRarity, 802 상자, 16_levelPrice 그룹 306~313) — 2차

정본 지도 「🛡️ 장비 · 투구 · 갑옷 · 망토 · 신발」(Area 3, L2581~2586) [프로토].

**규칙** [창작·HE임시]:
- 상자 열기: 상자(01_wealth 14 종이 / 15 나무 / 16 황금) 1개 → `802_randomBox` group 802471001·802472001·802473001로 희귀도 결정(consumeType 610 Gear, id = 471 id). 부위는 7종 균등, 세트는 `113_god.gearSet = 1`인 신 5종 균등(코드 규칙). 상자가 없으면 701 상자 상품으로 산다.
- 장착: 부위마다 1개. 효과 = 희귀도 `value` + 강화 레벨 × 1 (%) → 부위 statKey에 더함. 같은 세트 4개 착용 시 Dmg +10%(499 `gear.setPieces`·`gear.setDmg`).
- 강화: 츄르, 16_levelPrice 그룹 `306 + 희귀도`(목표 레벨 1~30, 값 = `upgradeGold × 목표레벨`). 상한 30 [창작](원작 저장소는 상한 없음).
- 판매: `sellGold` 츄르(착용 중 불가). 합성: 같은 부위·같은 희귀도 미착용 3개 → 다음 희귀도 1개.
- 장비가 꺼진 동안(1차) 상자는 캣닢으로 바뀐다(8.4.6).

**470_gearPart** 열 `id, key, statKey, icon, sort`:

| id | key | 이름 [창작] | statKey |
|---|---|---|---|
| 470001001 | claw | 발톱 장식 | Dmg |
| 470001002 | vest | 조끼 | Range |
| 470001003 | hat | 털모자 | CritChance |
| 470001004 | mitten | 발토시 | FireRate |
| 470001005 | boots | 장화 | Gold |
| 470001006 | collar | 방울 목걸이 | CritDmg |
| 470001007 | cape | 망토 | DoubleChance |

**471_gearRarity** 열 `id, rarity, color, value, sellPriceType/Id/Value, levelPriceGroup, frame`:

| id | rarity | 이름 [창작] | color [프로토 WGRADE L771] | value % [창작·HE임시] | sell 츄르 | upgrade 츄르/레벨 | levelPriceGroup |
|---|---|---|---|---|---|---|---|
| 471001001 | 0 | 평범 | #9ea4ae | 2 | 50 | 100 | 306 |
| 471001002 | 1 | 고급 | #46c35a | 4 | 100 | 200 | 307 |
| 471001003 | 2 | 희귀 | #3a8ff0 | 7 | 250 | 400 | 308 |
| 471001004 | 3 | 영웅 | #a65be8 | 12 | 600 | 800 | 309 |
| 471001005 | 4 | 전설 | #f08a24 | 20 | 1500 | 1600 | 310 |
| 471001006 | 5 | 신화 | #e8443a | 32 | 4000 | 3200 | 311 |
| 471001007 | 6 | 신성 | #1fc8c8 | 50 | 10000 | 6400 | 312 |
| 471001008 | 7 | 영원 | #ffd23a | 80 | 25000 | 12800 | 313 |

### 8.5.9 축복 (480_blessing) — 2차

**규칙** [창작·HE임시]: 광고 1회 → 10런, 상점 카드 → 30런(consumeType 630, value = 런 수). 남은 런은 **본편** `collect`마다 −1(모드 런은 안 깎음). 켜져 있으면 다시 켤 수 없다.

열 `id, key, effectType, value1, value2, adRuns, icon, active`.

| id | key | 이름 [창작] | effectType | value1 / value2 | adRuns | active |
|---|---|---|---|---|---|---|
| 480001001 | rebirth | 목숨 축복 | 1 부활 | 목숨 조각 +20% / 쿨타임 −20% | 10 | 1 |
| 480001002 | god | 야옹신 축복 | 2 일일 | 모드 입장권 +1 / 매일 캣닢 +20(켤 때 첫날분 즉시) | 10 | 1 |
| 480001003 | energy | 기운 축복 | 3 스태미나 | 기운 상한 +10 / — | 10 | 0(스태미나 꺼짐) |
| 480001004 | gear | 장비 축복 | 4 능력치 | Dmg +10 / — | 10 | 1 |

광고는 `08_adPlacement` blessing(하루 4회).

### 8.5.10 출석 (485_attendance) — 1차

**규칙** [창작]: UTC 날짜마다 1회 수령, 놓친 날은 건너뛰지 않고 다음 칸을 받는다. 7일째 뒤 1일째로 돌아간다. 해금 본편 런 1회(05 행 5001002).

열 `id, day, rewardType, rewardId, rewardValue, icon`.

| id | day | 보상 | Type / Id / Value |
|---|---|---|---|
| 485001001 | 1 | 츄르 300 | 1 / 1 / 300 |
| 485001002 | 2 | 캣닢 20 | 1 / 2 / 20 |
| 485001003 | 3 | 도전 메달 2 | 1 / 5 / 2 |
| 485001004 | 4 | 츄르 800 | 1 / 1 / 800 |
| 485001005 | 5 | 캣닢 40 | 1 / 2 / 40 |
| 485001006 | 6 | 모드 입장권 각 1 | 3;3;3 / 2001001;2001002;2001003 / 1;1;1 |
| 485001007 | 7 | 캣닢 100 + 나무 상자 1 | 1;1 / 2;15 / 100;1 |

### 8.5.11 지도 추격 — 장마 구름 (490_roadMap) — 2차, 기본 꺼짐

원작 호드를 「장마 구름」으로 옮긴다: 생선 축 위를 따라오는 비구름. 잡히면 생선이 젖는다.

**규칙** [창작·HE임시]:
- 구름 값 `horde`(생선 단위). 본편 런 정산마다 `+gainPerRun`. 내 생선과 차이가 `maxRange`보다 크면 대신 `+catchUpGain`.
- Area 시작 시 `horde = Area 시작 생선 − startGap`. 새 Area가 열리면 `horde = needSouls(a−1) − startGap`.
- 잡힘: 이번 런에 Area를 못 열었고, 생선 > Area 시작 생선, `horde ≥ 생선`이면 생선을 Area 시작값으로 되돌리고 그 Area 벽 기록(`save.bars`)을 지운다(원작 저장소 임시 처리, 생선만 잃으면 다시 못 벌어 막히기 때문).
- 결과 전 지도에 구름 위치 표시, 문구 「장마 구름에 젖었다!」.

열 `id, zoneId, gainPerRun, maxRange, catchUpGain, startGap, icon, active`. 6행(Zone 1~6) 모두 `5, 100, 15, 30`, **active 0** [창작·HE임시]. 켜는 판단은 D-78.

### 8.5.12 확률 표 (801_drop, 802_randomBox) 행

JJEvol 표 규격 v1 §6~7 모양(열 정의 정본 [07 §7.9.3](07_Tables.md)). 802 열 `id, group, subGroup, rewardType, rewardId, rewardValue_min, rewardValue_max, weight, icon, nameKey`. 한 번 열면 **칸(subGroup)마다 한 행씩** weight로 뽑아 모두 준다. 묶음 ID = 묶음 첫 행 id. 아래 표의 「칸 n」 행 값은 `rewardType / rewardId / rewardValue`, weight는 같은 칸 안 비중(합 10000 권장).

| group | 쓰임 | 칸·행 | 우선 |
|---|---|---|---|
| 802430001 | 펫 소환 | 칸 1: 16행 `620 / 430 id / 1`, weight 8.5.2 표 · 칸 2: 1행 `1 / 6(사료) / 10`, weight 1 | 2차 |
| 802460001 | 별사탕 굴림 | 칸 1: 4행 `500 / 0 / 20·40·60·100`, weight 6000·2500·1200·300 | 2차 |
| 802471001 | 종이 상자 | 칸 1: 3행 `610 / 471001001·002·003 / 1`, weight 7000·2500·500 | 2차 |
| 802472001 | 나무 상자 | 칸 1: 희귀도 0~4, weight 3000·4000·2200·700·100 | 2차 |
| 802473001 | 황금 상자 | 칸 1: 희귀도 1~5, weight 1000·4000·3500·1200·300 | 2차 |
| 802401001 | 수비전 택배 상자 카드 tier | 칸 1: 5행 `601 / tier 0~4 / 1`, weight 5000·2500·1500·800·200 | 1차 |

상자 확률 [창작·HE임시](원작 저장소 가중치 70/25/5, 30/40/22/7/1, 0/10/40/35/12/3을 10000 환산). 장비 상자 group은 이전 판의 `802470001~003`에서 `802471001`·`802472001`·`802473001`로 바꿨다(묶음 ID = 첫 행 id라서 종이 상자의 2·3번째 행 id와 겹쳤다). 천장(803·804)은 1차·2차 모두 행 없음. 801은 1차에 적 캣닢 묶음 `801001001` 하나뿐(07 §7.9.3). 상자 등급 강화·자동 상자(원작 저장소 기능)는 넣지 않는다(3차 후보).

### 8.5.13 499_contentConfig (콘텐츠 상수)

`300_runConfig`와 같은 세로 키/값 형식: `id, key, value, unit, 근거`. id `499001001~`.

| key | value | unit | 태그 |
|---|---|---|---|
| mode.adTicketDaily | 3 | 회 | [창작·HE임시] |
| mode.sweepChuruRatio | 0.8 | 배 | [창작] |
| box.fallbackDia.paper / wood / gold | 20 / 60 / 150 | 캣닢 | [창작] |
| pet.summonPrice | 1/2/100 | consumeData | [창작·HE임시] |
| pet.summonFood | 10 | 사료 | [창작·HE임시] |
| pet.foodPerClear | 30 | 사료 | [창작·HE임시] |
| pet.cardsToUnlock | 2 | 장 | [창작·HE임시] |
| pet.slots | 2 | 칸 | [창작·HE임시] |
| pet.levelPct | 20 | % | [창작·HE임시] |
| pet.buildSlotsMax / buildSlotEvery | 3 / 10 | 칸 / 레벨 | [창작·HE임시] |
| pet.buildCost0 / 1 / 2 | 10 / 20 / 40 | 털실 | [창작·HE임시] |
| friend.slotsMax / slotPriceDia | 3 / 200 | 칸 / 캣닢×현재 슬롯 | [창작·HE임시] |
| friend.coinPerClear | 2 | 개 | [창작·HE임시] |
| god.shardPerClear | 1 | 개 | [창작·HE임시] |
| gear.setPieces / setDmg | 4 / 10 | 개 / % | [창작·HE임시] |
| gear.maxUpgrade | 30 | 레벨 | [창작] |
| blessing.shopRuns | 30 | 런 | [창작·HE임시] |
| event.cycleDays / activeDays | 14 / 7 | 일(UTC 1970-01-01 기준) | [창작·HE임시] |
| rebirth.fastTravelPerRebirth | 1 | 회 | [창작·HE임시] |
| rebirth.skipDiaPer10Min | 5 | 캣닢 | [창작] |
| energy.enabled | 0 | 0/1 | [창작](D-79) |
| horde.enabled | 0 | 0/1 | [창작](D-78) |

### 8.5.14 부활 추가분 — 2차

정본 부활(아홉 목숨): Area 2 개방 시 해금(`rbOpen` L2516), 쿨타임 6시간 `RB_CD` L2515 [프로토, 원작 6시간과 같음], 광고 70분 스킵 하루 4회(`ads.skip` 4, L2423, L2575) [프로토], 특전 4종. 원작의 「Level 15 이상」 조건은 정본이 Area 2로 바꿨으므로 정본을 따른다.

| 추가분 | 규칙 | 값 |
|---|---|---|
| 캣닢 스킵 | 정본 `#rbSkipDia` 버튼(항상 비활성, 문구 「유료 다이아는 향후」 L705)을 켠다. 남은 시간 전체 스킵, 비용 = `ceil(남은 분 / 10) × 5` 캣닢(최대 6시간 = 180) | [창작] |
| 빠른 이동 | 부활 직후 1회: 목적지 = `max(1, 최고 도달 Area − 1)`, 생선 = 그 Area 시작값(`needSouls(목적지−1)`), 벽 기록 없음. 저장에 `bestArea` 필요([09](09_Save.md)) | [창작·HE임시] |
| 목숨 축복 | 8.5.9 | [창작·HE임시] |
| 레벨 스킵 | 넣지 않음(정본은 Level 조건이 없다) | — |

---
## 8.6 해금 순서 (05_contentOpen)

### 8.6.1 열과 조건 종류

정크버스터즈 열 `id, contentId, sort, icon, conditionType, conditionValue`에 `key`(시스템 키)를 더한다. 모드 행은 `contentId` = 400_content id, 메타 시스템 행은 `contentId 0` + `key`.

| conditionType | 뜻 | 판정 |
|---|---|---|
| 1 | Area 도달 | `save.area ≥ conditionValue` (정본 지도 `MAP_FEATS` 기준 L2581~2586) |
| 2 | 본편 런 수 | 본편 `collect` 누적 ≥ conditionValue |
| 0 | 항상 열림 | — |

잠긴 항목의 버튼은 보이되 회색 + 자물쇠, 누르면 토스트 「Area {n}에서 열려요」(문구 키 `toastMessage.contentLocked`). 열리는 순간 1회 연출 「새 콘텐츠가 열렸어요!」 + 로비 「컨텐츠」 버튼 빨간 점. 해금 판정은 `MV.Meta`가 Area 개방·런 정산 직후 한 번 한다.

### 8.6.2 행 (18행)

| id | key | contentId | 조건 | 근거 | 우선 |
|---|---|---|---|---|---|
| 5001001 | rebirth | 0 | 1 / 2 | [프로토] `rbOpen` L2516, 지도 Area 2 「아홉 목숨」 | 1차(정본) |
| 5001002 | attendance | 0 | 2 / 1 | [창작] | 1차 |
| 5001003 | contentMenu | 0 | 1 / 3 | [프로토] 지도 Area 3 「도전」 | 1차 |
| 5001004 | god.flame | 0 | 1 / 4 | [프로토] 지도 Area 4 「야옹신 추가」 | 1차 |
| 5001005 | quest | 0 | 2 / 1 | [창작] | 1차 |
| 5001006 | mode.rapidRush | 400001002 | 1 / 4 | [창작] | 1차 |
| 5001007 | mode.churuRush | 400001003 | 1 / 5 | [창작] | 1차 |
| 5001008 | mode.bigMouse | 400001004 | 1 / 6 | [창작] | 2차 |
| 5001009 | research | 0 | 1 / 5 | [창작] | 1차 |
| 5001010 | gear | 0 | 1 / 3 | [프로토] 지도 Area 3 「장비」 | 2차 |
| 5001011 | friend | 0 | 1 / 6 | [프로토] 지도 Area 6 「동료」 | 2차 |
| 5001012 | pet | 0 | 1 / 7 | [프로토] 지도 Area 7 「펫」 | 2차 |
| 5001013 | shop | 0 | 0 / 0 | [창작] | 1차 |
| 5001014 | achievement | 0 | 2 / 1 | [창작] | 1차 |
| 5001015 | pass | 0 | 1 / 4 | [창작] | 2차 |
| 5001016 | blessing | 0 | 1 / 3 | [창작] 강제 광고 시작 Area와 같게(L2434) | 2차 |
| 5001017 | godLevel | 0 | 1 / 6 | [창작] | 2차 |
| 5001018 | roadMap | 0 | 1 / 2 | [창작] `horde.enabled 0`이면 무시 | 2차(꺼짐) |

- 골목 수비전(400001001)은 메뉴 자체 행 5001003을 `openId`로 쓴다(첫 모드, 메뉴와 동시에 열림).
- 정본 지도의 Area 7 「레이스」 표시는 **지운다**(레이스 제외). 지도 칸 문구는 `MAP_FEATS`를 이 표 `key`·조건으로 다시 만든다 — 05 화면 요청(D-78).
- 2차 행은 `active 0`(07 공통 열)으로 둔다. 지도에는 「업데이트 예정」으로 표시.

### 8.6.3 컨텐츠 메뉴 잠금

`UIPopup_ContentMenu`는 `400_content`에서 `contentType ≥ 1`이고 `active 1`인 행을 `sort` 순으로 그린다. 항목마다 `openId`의 05 행을 판정해 잠금/열림을 정한다. 메뉴 버튼(5001003)이 잠긴 동안 로비 「컨텐츠」 버튼은 자물쇠다.

| 항목 id | 이름 | 열림 | 1차 |
|---|---|---|---|
| 400001001 | 골목 수비전 | Area 3 | ○ |
| 400001002 | 우다다 질주 | Area 4 | ○ |
| 400001003 | 츄르 털이 | Area 5 | ○ |
| 400001004 | 대왕쥐 소탕 | Area 6 | ✕(목록에서 숨김) |

## 8.7 재화 (01_wealth 5~17, 02_playResource, 07_consumeType 요청)

### 8.7.1 01_wealth 행

1~4는 정본 재화로 [07](07_Tables.md)이 정한다: 1 gold 츄르, 2 dia 캣닢, 3 souls 생선, 4 rbToken 목숨 조각. 이 문서는 5~17을 더한다. 열은 07의 01_wealth 열(`id, type, icon, maxValue, chargeShopSectionId` 등)을 따른다(아래 표의 key = `type` 열). 아이콘 이름 [창작], 그림은 [03](03_ResourcePipeline.md) 신규 목록.

| id | key | 이름 [창작] | 얻는 곳 | 쓰는 곳 | chargeShopSectionId | 우선 |
|---|---|---|---|---|---|---|
| 5 | medal | 도전 메달 | 모드 클리어 | 메달 교환(701006) | 702001004 | 1차 |
| 6 | petFood | 사료 | Area 돌파, 펫 소환, 패스 | 펫 레벨 | 0 | 2차 |
| 7 | yarn | 털실 | 패스, 상점 | 펫 빌드 | 702001001 | 2차 |
| 8 | godCoin | 야옹신 방울 | 상점, 메달 교환 | 신 해금(3~12번째) | 702001001 | 2차 |
| 9 | godShard | 공용 신 조각 | 상점 | 신 레벨 부족분 | 702001001 | 2차 |
| 10 | friendCoin | 동료 간식 | 상점 | 친구 영입 | 702001001 | 2차 |
| 11 | friendUp | 공용 간식 | 상점, 패스 | 친구 레벨, 우정 단계 | 702001001 | 2차 |
| 12 | friendAny | 만능 간식 | 상점, 패스 | 전용 코인 대체 | 702001001 | 2차 |
| 13 | starCandy | 별사탕 | 상점, 도전 펀드 | 룬(패스 XP) | 702001001 | 2차 |
| 14 | boxPaper | 종이 상자 | 우다다 질주 | 장비 상자 | 702001002 | 2차(1차는 캣닢 대체) |
| 15 | boxWood | 나무 상자 | 출석 7일, 상점 | 장비 상자 | 702001002 | 2차(1차는 캣닢 대체) |
| 16 | boxGold | 황금 상자 | 우다다 질주, 메달 교환 | 장비 상자 | 702001002 | 2차(1차는 캣닢 대체) |
| 17 | noAds | 광고 제거 | 상점(701002004) | 강제 광고 끔(값 1이면) | 0 | 1차 |

- 1차에 상자 재화를 받으면 `499 box.fallbackDia.*` 캣닢으로 바꿔 준다(8.4.6). 출석 7일의 나무 상자도 같다.
- 아이콘 이름: `UI_Icon_{Key}`(첫 글자 대문자, 1~4번 `UI_Icon_Gold`·`UI_Icon_Dia`·`UI_Icon_Soul`과 같은 형식. 예 `UI_Icon_BoxWood`, `UI_Icon_NoAds`). 실제 이름은 각 행의 `01_wealth.icon` 열이 정본이다.

### 8.7.2 02_playResource 행(8.4.3 재게시)

| id | key | chargeType | 회복량 `chargeAmount` / 상한 `chargeMax` (energy는 `chargeSec`=600) | 우선 |
|---|---|---|---|---|
| 골목 수비전 입장권(2001001) | ticket.alleyWar | 2 일일 초기화 | 3 / 3 | 1차 |
| 우다다 질주 입장권(2001002) | ticket.rapidRush | 2 | 3 / 3 | 1차 |
| 츄르 털이 입장권(2001003) | ticket.churuRush | 2 | 3 / 3 | 1차 |
| 대왕쥐 소탕 입장권(2001004) | ticket.bigMouse | 2 | 3 / 3 | 2차 |
| 기운(2001005) | energy | 1 시간 회복 | 600초마다 1 / 30 | 꺼짐(D-79) |

### 8.7.3 07_consumeType 신규 요청

07이 정한 600 StairLv, 601 Card, 602 RunGold, 603 RunSouls에 더한다. 번호는 07이 최종 확정한다.

| consumeType | key | id 뜻 | value 뜻 | 쓰는 곳 |
|---|---|---|---|---|
| 610 | Gear | 471 희귀도 id(부위·세트는 코드 굴림) | 개수 | 상자 802 |
| 620 | PetCard | 430 id | 장 | 소환 802 |
| 621 | FriendCoin | 450 id, 0 = 지금 올리는 친구 | 개 | 16_levelPrice 302 |
| 622 | GodShard | 113 id, 0 = 지금 올리는 신 | 개 | 16_levelPrice 304 |
| 630 | Blessing | 480 id | 런 수 | 상점 축복 카드 |

### 8.7.4 08_adPlacement 행 추가

열은 07(`id, key, placement, dailyLimit, adRewardType/Id/Value`). id는 07이 붙인다. 정본 광고 dia(하루 3, 캣닢 100)·skip(하루 4) L2423은 07 행이다.

| key | placement | dailyLimit | adReward | 태그 |
|---|---|---|---|---|
| modeTicket | 컨텐츠 메뉴 [▶ +1] | 3(모드 공용) | `3 / 0 / 1` (id 0 = 고른 모드 입장권) | [창작·HE임시] |
| shopFree | 상점 「오늘의 츄르」 | 2 | 701 상품 보상을 씀(`0 / 0 / 0`) | [창작] |
| blessing | 축복 화면 | 4 | `630 / 0 / 10` (id 0 = 고른 축복) | [창작·HE임시] |

광고 제거(01_wealth 17 = 1)는 이 표의 보상형 광고를 끄지 않는다.

## 8.8 1차 출시 범위

| 묶음 | 1차 | 2차 | 근거 |
|---|---|---|---|
| 본편 | Area 1~`PLAYABLE_AREAS`(권장 15), 정본 규칙 그대로 | Area 16~30 | D-71, D-73 |
| 테마 | 304 15종 | — | D-72 |
| 야옹신 | thunder(정본) + flame(Area 4 해금), Lv1 고정 | 나머지 10종, 신 레벨, 방울·조각 | D-74 |
| 공용 카드 | 정본 12종 | 휴면 카드 4종 부활 | D-75 |
| 컨텐츠 메뉴 | 골목 수비전·우다다 질주·츄르 털이 | 대왕쥐 소탕 | D-76 |
| 퀘스트 | 일일·일일 종합·월간·업적 | 패스 6종 | — |
| 출석 | 7일 | — | — |
| 연구 | 5계열 × 5단계 | — | — |
| 상점 | 무료(광고)·캣닢·광고 제거·새내기·Area 응원·메달 교환 2종, 결제 스텁 | 상자·패스·축복·재화 묶음 | — |
| 펫·친구 고양이·장비·룬·축복 | — | 전부 | 8.5.0 |
| 지도 추격 | — | 표만, 기본 꺼짐 | D-78 |
| 기운(스태미나) | — | 꺼짐 | D-79 |
| 부활 추가분 | — | 캣닢 스킵·빠른 이동 | 8.5.14 |
| 광고 | 정본 2종 + modeTicket + shopFree | blessing | 8.7.4 |

1차 메타 능력치(8.5.0) 출처는 **연구뿐**이다. 연구 0이면 정본과 같은 손맛이 유지된다(검증 기준: 연구·모드 없이 Area 1~5 시간·수치가 정본과 같아야 함, [12](12_Acceptance.md) 요청).

## 8.9 의문점·결정 필요

| 결정 ID | 질문 | 선택지 | 권장안 | 영향 |
|---|---|---|---|---|
| **D-70** | Area 생선 합이 개방 요구치보다 작은 Area(특히 Area 30)를 어떻게 하나 | A 정본·원작 유지 + Area 30만 C6(1695) / B 모든 Area C6 / C 그대로 | **A**. 검사기 규칙 「Area 생선 합 − 10 ≥ 증가분」을 경고로 둔다 | 302 Area 30 행 1개, 검사기 1개 |
| **D-71** | Area 16~30 Floor·HP 값 | A 원작 `Finals` 값 / B 정본처럼 Area 15 재사용 / C 새 곡선 | **A**. Zone 4 열기 전 시뮬([12](12_Acceptance.md))로 후반 HP(2100만) 확인 | 302·303 행 75개, 밸런스 |
| **D-72** | Zone 2~6 테마 | A 색 테마 10종 추가 + 재배정 / B 정본 5종 반복 / C 새 그림 | **A**. MV-TOOL-ART가 팔레트만 바꿔 다시 굽기 | 304 10행, 03 아틀라스 10개 |
| **D-73** | 첫 출시에 여는 Area 수 `PLAYABLE_AREAS` | 5 / 15 / 30 | **15**(Zone 1~3). 검증 판정은 정본 범위 5 | 300_runConfig 1값, 결말 연출 위치 |
| **D-74** | 1차 공개 야옹신과 신 레벨 | A thunder+flame, Lv1 고정 / B 12종 전부 / C thunder만 | **A** | 113 active 2행, 495 미사용 |
| **D-75** | 정본 휴면 카드 경로(crit·dbl·pierce·clone2 등) 공용 카드로 부활 | A 2차에 4종 / B 1차 / C 안 함 | **A**(1차는 정본 동등) | 110 4행 active, 112 type |
| **D-76** | 모드 런에서 로비 강화·Area 강화·메타 능력치를 적용하나 | A 모드별 402 열로 정함(수비전 적용, 우다다 고정 무기 tier 4·강화 0) / B 전부 적용 / C 전부 끔 | **A**(8.4.4) | 402 열, `RunModifiers` 생성 경로 |
| **D-77** | 일일 초기화 시각 | A UTC 0시(정본 `adDay`) / B 한국 시간 0시 | **A**(정본 동등, 한국 9시) | 퀘스트·입장권·출석·광고 |
| **D-78** | 2차 범위와 지도 정리 | A 8.8 표대로, 지도 추격 기본 꺼짐, 지도 「레이스」 칸 삭제 / B 지도 추격 켬 | **A** | 499 `horde.enabled`, 05 지도 문구 |
| **D-79** | 기운(스태미나) | A 넣지 않음(행만 active 0) / B 본편 런마다 소모 | **A**. 정본에 없고 손맛을 바꿈 | 02 행 기운(2001005), 480 행 3 |

### 8.9.1 다른 문서 작성자에게 기대한 가정

- **[07](07_Tables.md)**: 이 문서의 표 번호·id 형식(8.1.5), consumeType 610·620·621·622·630(8.7.3), `113_god` 추가 열(8.3.2), `301_zone`·`302_area` 열(8.2.2), `05_contentOpen.key` 열, `802_randomBox` 보상 3열 직접 지정, `102_enemy` 행 mouseDefence·bossMouse, `105_objectKind` key cardbox, `109_moveAI` type Advance, `111_cardPool` 그룹 main·defence·rush, `310_template` id 310000101·102, `499_contentConfig` 세로 형식, 「묶음 이름은 group 000 키」 규칙.
- **[05](05_Screens.md)**: `UIPopup_ContentMenu`, `UIPopup_ModeResult`(×3 없음), 수비전 HUD(방어선·남은 쥐), 카드 고르기 재사용, 메타 화면(퀘스트·출석·연구·상점; 2차 펫·친구·장비·패스·축복), 지도 칸 문구를 05_contentOpen으로 생성, 잠금 토스트.
- **[04](04_GameRules.md)**: `RunModifiers` 7필드(8.5.0), `IModeRule`(8.4.10), 런 동작 `CompanionShot`(2차).
- **[09](09_Save.md)**: 모드 최고 단계·입장권·광고 횟수(UTC 날짜), 퀘스트 기간 카운터, 출석 칸, 연구 완료 시각, 상점 구매 기록, `bestArea`, 2차 시스템 저장 칸 예약.
- **[03](03_ResourcePipeline.md)**: 아이콘 이름 규칙 `UI_Icon_{표}_{key}`, 테마 아틀라스 10개, 모드 배경·BGM 행 값.
