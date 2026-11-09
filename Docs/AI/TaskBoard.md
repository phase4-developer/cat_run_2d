# TaskBoard

작업 계약 원장(기획서 11 §11.5). 지금 상태·담당·열린 결정만 둔다. 완료 계약의 검증 세부와 지난 인계는 [TaskBoard_Archive.md](TaskBoard_Archive.md)에 있다(2026-11-10 이전 전문). 계약서는 `C:/Project/MoonVillage.wt/_notes/contracts/`(저장소 밖).

## 계약 상태

| 계약 | 상태 | 결과(dev) | 남은 것 |
|---|---|---|---|
| MV-00 기반 | 완료 | — | 보관 「MV-00 남김」 |
| MV-DATA 표 | 3차 완료 | 2차 `6d946c8`, 3차 `a2be0d6` | 자료 빈칸(정본 값 없음, 보관 참조). VIEW 요청: `view.groundFar` 92→120, `330_climb` 출렁임 열, `304_theme` 6~15행 |
| MV-SAVE 저장 | 완료(G1) | `bf2921c` | D-68 출시 저장 키 |
| MV-TOOL-ART 그림 | 완료(G2) | 3차 `72ba086`, 4차 `ce87826` | `UI_Skin_MapZone_Z4~Z6` 임시 아트, D-ART-01~06 |
| MV-TOOL-SND 소리 굽기 | 완료(2차 tierup_0) | `922cad9` | 효과음 31·`03_sound` 33행, 청취 NOT_RUN. 오디오 Addressables 그룹 없음(33개 미등록) → PLAY-INT 통합 |
| MV-ART-ATLAS 아틀라스 | 부분 완료 | `12f354d` | 실측 패킹·메모리 NOT_RUN, D-MVDATA-PET `Atlas_World_Pet` 규칙 행 |
| MV-QA-HARNESS | 프로토 쪽 완료 | `63c76dc` | Unity 쪽(VIEW 뒤, D-87로 참고 보고서) |
| MV-RUN-SIM / GEN | 완료(G2·G3) | `23cec8a` | — |
| MV-RUN-RULE 확장 지점 | 완료 | `cfe1f46` | 402 표 연결, G7 `IModeRule` |
| MV-RUN-VIEWAPI WY·옹벽 읽기 | 완료 | `a0303d7` | MV.View `RunStateViewSource` Wy·HasCliff·Cliff 교체(VIEW 다음 담당) |
| MV-META 메타 규칙 | 완료 | `d120b2e` | `modePlays` 한도(400_content) 교체 지점 |
| MV-G3-INT Boot 연결 | 완료(G3 끝) | `8de2dc7` | Meta 저장→`RunContext` 도우미 없음 → MV-PLAY-INT. dev 전체 `448a6fc73f04` PASS(compile, EditMode 335/335, PlayMode 2/2) |
| MV-VIEW 월드 렌더 | 2차 완료 | 1차 `64aa5b4`, 2차 `bd7bb3b` | PlayMode·캡처·드로우콜 NOT_RUN. 남음: ScreenRig Vignette, 로비 이름표·말풍선·고양이·방석·전구·난로 빛, 빠진 연출(관문 줄·광륜·별·롤 게이트 줄·벽돌 빠짐), renderScale(폭 1080 상한, URP 통합 Writer). 요청: MV-DATA 105 `viewShape` 열. 결정 후보: 라벨 위치, 꽃 flipX, 우물 잡동사니 배치 |
| MV-SND 소리 재생 | 완료 | `b148dc5` | Boot 연결(G4 통합) |
| MV-FX 이펙트 | 1차 완료 | `79aeb7e` | PLAY-INT로: Addressables `Fx/<이름>` 13건(라벨 Effect), Boot `MvFx.InstallDefault(...)`·매 프레임 `MvFx.OnRunEvents`. 팝업·비행 아이콘·플래시·단계 이름 그림은 MV.UI가 `IMvPopView`·`IMvFlyView`·`IMvHudAnchors`·`IMvFlashView` 구현(총괄 권장, asmdef 변경 없음 — D-FX-1). 요청: MV-DATA 04_effect 자식별 파라미터 열, MV-RUN `RunProjectile.El`. 캡처·손맛 NOT_RUN |
| MV-UI-HUD 1차 | 완료 | `c95e5c9` | PlayMode·화면 NOT_RUN. PLAY-INT로: Boot 씬 UIManagerCreater+EventSystem, GameBootstrap `MvUiBoot.InitAsync(ui)`, Addressables 4건(UIManager는 Local 권장). 결정 후보: 한글 TMP 글꼴, Space 출발 키(MV.UI는 InputSystem 참조 없음), Sorting Layer 「UI」 |
| MV-PLAY-INT 플레이 가능 빌드 | 완료(D-91) | `6837d00` | 전체 확인 `65d66446eb88` PASS(compile, EditMode 419, PlayMode 7). 사람 확인 NOT_RUN(화면·카메라·손맛). 없음: 로딩 화면(`LoadingSceneScreen` 등록 뒤 `Loading()`), 로비 시설 단계, 전면 광고·×3, 선택 UI. 결정 후보: 런 시드(런 순번 결정적 vs 04 §4.17.2-3 MV.Meta 무작위, `SeedFor` 한 곳), Boot 없이 Game 실행 시 Boot로 돌려보냄, MV-SAVE `GetBars(area)` API. Dev_Debug 그룹은 출시 전 IncludeInBuild 끄기. MvFx 머티리얼 리플렉션 우회 → `Fx/Mat_Normal`·`Fx/Mat_Add`로 교체(다음 FX 담당) |
| MV-PLAT-NET / ADS / LOGIN | 완료 | `9c354a6` / `1d3bb2a` / `979b171` | 연결은 G6 통합(LOGIN은 D-90으로 연결 안 함) |
| MV-PLAT-IAP / ANALYTICS / NOTI | 스텁 취소(D-88) → 고정 tgz 연결로 | — | `ai/MV-PLAT-IAP-stub`·`ai/MV-PLAT-ANALYTICS-stub` 병합 안 함 |
| MV-UI-META | 1차 완료 | `fa7bac4` | 화면·PlayMode NOT_RUN. 총괄: Addressables `UIPopup_GodSelect`·`AreaUpgrade`·`Rebirth`·`Map`·`FeatureUnlock`(Remote_UI), `UIPopup_Debug`(개발 전용 그룹) — PLAY-INT 병합 뒤. 요청 MV-META: 로컬 `debug.*` 처리기 8개, `MvMeta.DisarmRebirth()`. 결정 후보: 지도 필터(`05_contentOpen.showOnMap` 빈칸 → conditionType 1 전부), 지도 체크포인트 문구, 05 §5.1.14 클래스 표 소유(테스트 패널·공통 틀을 META가 만듦) |
| MV-UI-LOBBY | 1차 진행(칸 a) | — | D-92 겉 더미 |
| MV-UI-CONTENT | 대기(LOBBY 뒤) | — | D-92 겉 더미 |
| MV-CONTENT-* (G7) | 대기 | — | — |
| MV-QA-ACCEPT (G8) | 대기 | — | D-87 기준 |

## 담당표

| 칸 | 실행기 | 지금 계약 | 브랜치 | 쓰기 범위 |
|---|---|---|---|---|
| a | `moonvillage-a` | MV-UI-LOBBY 1차 | `ai/MV-UI-LOBBY-main` | `S/MV.UI/Lobby/`·`Result/`·Common 줄 추가, 각 Tests, `A/Addressables/Remote/UI/Lobby/`·`Result/` |
| b | `moonvillage-b` | (총괄 dev 전체 확인 `c0fcbf8e461f`) | dev 분리 HEAD | — |
| c | `moonvillage-c` | (비어 있음) | — | — |
| d | `moonvillage-d` | (비어 있음) | — | — |
| e | `moonvillage-e` | (비어 있음) | — | — |
| f | `moonvillage-f` | (비어 있음) → 총괄 bundle3 패키지 20개 연결(권한 허용 대기) | — | 총괄 통합 Writer |
| g | `moonvillage-g` | (비어 있음) | — | — |

- dev 전체 기대 시험 목록: `C:/Project/MoonVillage.wt/_notes/expected/dev-full.json`(EditMode 419, PlayMode 7, PLAY-INT 병합 기준).
- 테스트 폴더 `C:/Project/MoonVillage`는 항상 `dev`. 칸은 브랜치만 바꿔 재사용. 실행기 하위 호스트 위임 만료 2026-11-19.

## 작업 순서 구조 (Unity 병목, 2026-11-10)

PC 한 대에서 Unity 무거운 작업은 한 번에 하나다. 그래서 순서를 이렇게 정한다.
1. **Unity 줄(직렬)**: compile·EditMode·PlayMode. 계약당 원칙 2회(compile 1 + 모듈 EditMode 1, 실패 수정 시 +1). 줄이 길면 새 배정을 늘리지 않는다 — 동시에 Unity를 쓰는 담당은 4명 안팎.
2. **Unity 없는 줄(병렬)**: 문서·표 TSV·Node 추출·순수 C# 작성. Unity 줄이 막힐 때 이쪽 일을 먼저 배정한다.
3. **dev 검증은 묶어서**: 쓰기 범위·asmdef가 겹치지 않는 계약은 계약 브랜치 PASS로 바로 병합하고 그 SHA를 증거로 둔다. dev 전체 EditMode는 그룹 끝, 또는 통합 Writer 파일(asmdef·Scene·Addressables·manifest·ProjectSettings)을 건드렸거나 쓰기 범위가 겹친 병합 뒤에만 한 번 돈다. 시험은 규칙·경계 단위로 두고 줄·필드마다 만들지 않는다(D-93).
4. **Unity 없는 계약을 사이에 섞는다**: Unity 단계가 대기열에 슬롯 수의 두 배쯤 밀려 있으면 Unity가 필요한 새 계약은 띄우지 않고 문서·표 TSV·Node 추출·순수 C# 작성 계약을 먼저 띄운다. 한 계약의 Unity 단계는 끝에 모아 낸다.
5. **담당 운영(D-94)**: 담당은 컨텍스트 크기만 보고 끊지 않는다. 같은 모듈 연속 계약이고 앞 계약이 병합·기록됐고 쓰기 파일이 겹치며 입력이 15만의 절반 이하면 같은 칸 담당이 다음 계약을 이어 받는다. 담당이 Unity 잡만 기다리면 독립 일을 하고, 일이 없고 30분 넘게 기다리면 잡 ID를 원장에 적고 닫는다. 슬롯을 쥔 담당과 쓰기 범위가 겹치는 계약은 그 슬롯이 끝나기 전에 배정하지 않는다. 중앙 `External.md`에 같은 규칙.
6. 우선순위(D-91): 플레이 가능한 빌드 경로(G3-INT → VIEW 2차 → HUD → PLAY-INT)를 Unity 줄 앞에 둔다.

## 사용자 결정 (2026-11-10)

- D-94 담당 운영: 「5번도 권고안대로한다해」 → 위 순서 5.
- D-93 시험 적당히·패키지 설정 우선: 「AI한테 작업 맡기면 너무 테스트가 많아.. 확인 하고 하는건 맡긴한데 그게 너무 세분화 되어있어」, 「저거 프로젝트 설정을 우선하도록 규칙 바꿔줘」 → 위 순서 3·5, `AGENTS.md` Framework·Unity 절, 중앙 규칙 반영.
- D-87 인수: 규칙·손맛·표 구조로 판정, 완전 일치·캡처 수치는 완료 조건 아님(기존 PASS 시험 유지). 기획서 12 §12.0.
- D-88 G6: 결제·통계·알림은 패키지만 넣는다. → Framework 3차 묶음 고정본(`Snapshots/20261109-bundle3`)이 나와 있어 `Packages/Frozen/` 연결(총괄).
- 패키지(2026-11-10 중계 경유 사용자 원문 「총괄한테 프레임워크는 보류하지말고 프로젝트 설정에 맞추어서 설치하라해」): 설정 범위 20개 전체(`network.firebase` 포함) + Firebase·AdMob SDK. 총괄 세션 권한 검사에 막혀 사용자 직접 허용 대기, SDK 내려받기는 사용자 확인 뒤.
- D-89 NET 구조 유지. D-90 LOGIN 더 연결 안 함. D-91 플레이 가능 빌드 우선. D-92 UI 1차 「구조는 실제, 겉은 더미」(05 머리·11 §G5).
- 사용자 원문은 기획서 13 §13.3.8과 보관 파일에 있다.

## 열린 결정 (권장안으로 진행, 교체 가능)

D-27 실기 FIELD, D-68 출시 저장 키, D-08 서버 공급자, D-40·41·42(VIEW 권장안, 42는 Linear 유지), D-MVDATA-PET `Atlas_World_Pet`, 450 portrait 접두, 802401001 행·480 statKey·413 step·701 active 0, 막힘 종료 사유, Friend_T 아틀라스 2페이지, `Tex_God_*` 위치, ConsumeType 생성(Framework 요청), D-META-01~04, D-RULE-01~05, D-NET-1(공용 시계 MV.Core `IClock`)·D-NET-2, D-ADS-1·2, D-LOGIN-1·2, D-SND-2(Steal kindId), D-ART-01~06, D-45~49(FX 권장안), D-FX-1(이펙트 그림 층은 MV.UI). D-72 부속(304 6~15 lineKind 정본 deco 규칙 유지), 400 `dailyLimit` 모두 0. 3차 표 교체 MV-TABLE-SWAP 병합(viewShape·304 소품·줄·04 part·dailyLimit). 남음: `WorldViewSource.cs`의 `RunStateViewSource` `_bump = ViewConfig.ClimbBumpOf(tables.Climb)` 1줄(다음 VIEW 담당). 결정 후보(SWAP): 길가 소품은 정본 deco 문턱 우선·목록만으로 고르려면 가중치 열, 원소 when은 bolt 아니면 fire(god.blast key null만 옛 색과 다름), 400에 없는 key는 한도 없음. 후속 계약: MV-UI-FX2(덱 머지·해금 오버레이·화면 전환 연출).
- 미해결(사용자): 없음.
