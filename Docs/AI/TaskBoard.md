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
| MV-ART-CHAR 디자이너 리소스 적용 | 0단계 설계 dev `9ed5148b`, 1단계 리소스 등록·2단계 `116_charLook` 표·3단계 캐릭터 구조·4단계 Happy·Stair 완료(2026-11-12), 로비 걷기 보류 | 1단계 `ai/MV-ART-CHAR-1-register` 시험 PASS `c054e84e`; 2단계 `ai/MV-DATA-CHARLOOK-table` 시험 PASS `3372cb9d`; 3단계 `ai/MV-VIEW-CHAR-1-structure` 시험 PASS `ee6f60b2`; 4단계 `ai/MV-VIEW-CHAR-2-happy-stair` 시험 PASS `5fb2b58d` | 1단계: PNG 52장 피벗 발 기준·PPU 161·원본 무압축, 아틀라스 13개(`atlas_player_0~11`, `atlas_clone_siamese`, ASTC 6x6 Tight), `Local_Character` 그룹(라벨 `char`)에 아틀라스·`Char_Cat`·`Char_Clone` 주소. 검증: compile·MV.Editor.Tests EditMode 32/32 PASS(잡 `7d6d777864df`). dev 전체 시험 NOT_RUN(확인 완화). 실측 패킹 크기 NOT_RUN. 2단계: `116_charLook` 13행(플레이어 세트 0~11 fromTier 1~12, 분신 샴 slot 0, scale 0.82), happy·stair는 A 그림 더미 26칸(시트 분홍·`_notes/tools/dummy_rows.tsv`), 300 키 `char.*` 5개(300107001~005), `CharLookResolver`(slot 넘치면 마지막 slot, 등급 이하 최대 fromTier), 검증 `CheckCharLook`, 그림 이름 확인 에디터 시험. 표 산출물 빌드 잡 `8de9cb51a3ce` PASS, compile·MV.Data.Tests+MV.Editor.Tests EditMode 171/171 PASS(잡 `d46deacfc2f9`). 구글 시트 탭 116_charLook·300 행 5개·표목록 줄 추가. 기대 목록 `_notes/expected/dev-full.json`이 dev 이름 변경 3건을 못 따라감(이번 잡은 실행 목록으로 맞춤). 3단계: `CatView` → `CharacterPresenter`(같은 스크립트 GUID라 WorldRoot 참조 유지), `CharStateMachine`(Walk·Stand·Hidden, 공격 C·D 겹침, 칸 내려감, 분신마다 한 칸 어긋남), `CharacterRig`(프리팹 루트), `CharacterPool`(주소별 Addressables + ObjectPool). 프리팹 2개에 Rig 부착, 기본 그림 직접 참조 제거. `101_cat` 옮긴 열 4개 삭제(TSV·산출물·시트·07 §7.7.7 갱신 표시). 빌드 잡 `b2ecd0d07c46` PASS, compile·EditMode(Data·Editor·View) 278/278(잡 `54491a2455b5`), PlayMode 7/7(잡 `bced9b6d6818`). 화면 확인 NOT_RUN(사용자 확인 필요: 걷기 교대·공격 그림·분신 배율). 옛 고양이 아틀라스(`Atlas_World_Cat_T*`·`Friend_T*`)는 안 쓰지만 지우지 않음. 300 `view.playerCatScale`·`view.cloneCatScale`은 코드가 안 읽음(116 scale과 겹침). 정리: 안 쓰게 된 키·아틀라스는 나중에 구글 시트 반영 때 한 번에 제거(사용자 2026-11-12 「안쓰게 된건 나중에 한번에 구글 시트 반영하면서 제거하면 좋겠어」). 4단계: Happy(GateTake·GaugeLevelUp에서 모두, 가로 배율 cos 1바퀴 `char.happySpinSec`), Stair(오르기 단계 플레이어, 흔들림 300 `char.stair*` 6개 300107006~011, HTML `drawClimbCat` 값), 빌보드를 프리팹 루트로. 시트 300 행 6개·표목록 182. 빌드 잡 `188b441d93bc` PASS, compile·EditMode 285/285·PlayMode 7/7(잡 `429bf9a31f8c`). 화면 확인 NOT_RUN. 뒷면 종이 텍스처 없음(앞면이 뒤집혀 보임). 분신 정산 중 숨김·계단 기울기·옆 뒤집기는 5단계. 로비 대기 걷기 보류: MV-SPEC-02 01 「로비와 인게임이 같은 화면」 vs Unity 로비(MV-SPEC-01 아지트 방, 고양이 자리 없음) — 사용자 결정 대기. 5·6단계는 리소스 Git 도착 대기 |
| MV-QA-HARNESS | 프로토 쪽 완료 | `63c76dc` | Unity 쪽(VIEW 뒤, D-87로 참고 보고서) |
| MV-RUN-SIM / GEN | 완료(G2·G3) | `23cec8a` | — |
| MV-RUN-RULE 확장 지점 | 완료 | `cfe1f46` | 402 표 연결, G7 `IModeRule` |
| MV-RUN-VIEWAPI WY·옹벽 읽기 | 완료 | `a0303d7` | MV.View `RunStateViewSource` Wy·HasCliff·Cliff 교체(VIEW 다음 담당) |
| MV-META 메타 규칙 | 3차 완료 | `d120b2e`, 3차 `f678cbd` | 3차: 테스트 패널 로컬 처리기 8개(개발 빌드만), `DisarmRebirth`. 결정 후보 D1 unlockAll은 Area·런 수 올리기, D2 addTime은 저장 안 함, D3 DisarmRebirth는 RebirthPresenter 직접 호출(port에 넣을 자리), `MvDebugRequests`·`MvMetaRequestNames.Debug*` 상수 중복. 요청(LOBBY): `settings.muted` 저장 요청, 로비 구매 결과(`MvLobbyBuyResult`) 화면 입구 |
| MV-G3-INT Boot 연결 | 완료(G3 끝) | `8de2dc7` | Meta 저장→`RunContext` 도우미 없음 → MV-PLAY-INT. dev 전체 `448a6fc73f04` PASS(compile, EditMode 335/335, PlayMode 2/2) |
| MV-VIEW 월드 렌더 | 2차 완료 | 1차 `64aa5b4`, 2차 `bd7bb3b` | PlayMode·캡처·드로우콜 NOT_RUN. 남음: **투사체·창 그림(02 §2.3 World rank 2·§2 표 8행) 미작업** — 지금은 원소 투사체만 MV-FX 꼬리(`fx.trail`)가 보여 「보였다 안 보였다」로 보임(사용자 확인 2026-11-10: 충돌 이상 없음, 미작업이면 무시 — 완료 계약 결함 아님), ScreenRig Vignette, 로비 이름표·말풍선·고양이·방석·전구·난로 빛, 빠진 연출(관문 줄·광륜·별·롤 게이트 줄·벽돌 빠짐), renderScale(폭 1080 상한, URP 통합 Writer). 요청: MV-DATA 105 `viewShape` 열. 결정 후보: 라벨 위치, 꽃 flipX, 우물 잡동사니 배치 |
| MV-SND 소리 재생 | 완료 | `b148dc5` | Boot 연결(G4 통합) |
| MV-FX 이펙트 | 1차 완료 | `79aeb7e` | PLAY-INT로: Addressables `Fx/<이름>` 13건(라벨 Effect), Boot `MvFx.InstallDefault(...)`·매 프레임 `MvFx.OnRunEvents`. 팝업·비행 아이콘·플래시·단계 이름 그림은 MV.UI가 `IMvPopView`·`IMvFlyView`·`IMvHudAnchors`·`IMvFlashView` 구현(총괄 권장, asmdef 변경 없음 — D-FX-1). 요청: MV-DATA 04_effect 자식별 파라미터 열, MV-RUN `RunProjectile.El`. 캡처·손맛 NOT_RUN |
| MV-UI-HUD 1차 | 완료 | `c95e5c9` | PlayMode·화면 NOT_RUN. PLAY-INT로: Boot 씬 UIManagerCreater+EventSystem, GameBootstrap `MvUiBoot.InitAsync(ui)`, Addressables 4건(UIManager는 Local 권장). 결정 후보: 한글 TMP 글꼴, Space 출발 키(MV.UI는 InputSystem 참조 없음), Sorting Layer 「UI」 |
| MV-PLAY-INT 플레이 가능 빌드 | 완료(D-91) | `6837d00` | 전체 확인 `65d66446eb88` PASS(compile, EditMode 419, PlayMode 7). 사람 확인 NOT_RUN(화면·카메라·손맛). 없음: 로딩 화면(`LoadingSceneScreen` 등록 뒤 `Loading()`), 로비 시설 단계, 전면 광고·×3, 선택 UI. 결정 후보: 런 시드(런 순번 결정적 vs 04 §4.17.2-3 MV.Meta 무작위, `SeedFor` 한 곳), Boot 없이 Game 실행 시 Boot로 돌려보냄, MV-SAVE `GetBars(area)` API. Dev_Debug 그룹은 출시 전 IncludeInBuild 끄기. MvFx 머티리얼 리플렉션 우회 → `Fx/Mat_Normal`·`Fx/Mat_Add`로 교체(다음 FX 담당) |
| MV-PLAT-NET / ADS / LOGIN | NET 2차 완료(스텁 → platform.network), ADS·LOGIN 1차 | `9c354a6`, NET 2차 `2a188c9` / `1d3bb2a` / `979b171` | NET 2차: 복제 타입 7개 삭제, 게임 몫 유지, 기본 경로 Local 고정(고정본 기본은 Server), 전체 확인 `4f240cfb605b` PASS. 후속 후보: MvMeta → NetworkManager 전환(MV.Meta asmdef 참조 필요). `MV.Platform.Tests` asmdef에 `Newtonsoft.Json.dll` 없음(필요 시 통합 Writer). ADS·LOGIN 스텁 교체 안 함(사용자 지시는 NET만, D-90) |
| MV-PLAT-IAP / ANALYTICS / NOTI | 스텁 취소(D-88) → 고정 tgz 연결로 | — | `ai/MV-PLAT-IAP-stub`·`ai/MV-PLAT-ANALYTICS-stub` 병합 안 함 |
| MV-00 bundle3·SDK 연결 | 완료(20개 전부, 사용자 허용 2026-11-10) | 17개 `d0491cb`, SDK·남은 3개 `b8fde6f` | `com.unity.purchasing` 5.3.1(+mobile.notifications·ads.ios-support·services.core), Firebase 13.17.0(app·analytics·auth·messaging·functions)·EDM4U 1.2.186은 `Packages/Vendor` tgz(`file:`, app tgz 약 62MB — Push 시 GitHub 50MB 경고 대상), AdMob 10.7.0은 `Assets/GoogleMobileAds`·`Assets/Plugins`(unitypackage 안 EDM4U 사본은 UPM과 겹쳐 뺌), Unity IAP `Assets/Resources/BillingMode.json`. 전체 확인 `e35fd024e251`·`00f8735a204a` PASS. 남음: AdMob App ID·`google-services.json`(사용자 자료), 게임 쪽 어댑터 연결(MV.Platform ADS·LOGIN은 스텁 유지) |
| MV-00 bundle4 Framework 4차(베타) | 완료(2026-11-10, 사용자 「4번 진행」) | `ai/MV-00-bundle4` `3137497` | 13개 갱신(MA 1.3.12-pre.17, audio pre.4, effects pre.3, ui.addressables pre.5, ui.urp pre.7, save pre.4, tables pre.3, tables.authoring pre.10, localization.authoring pre.12, platform.network·network.data·purchase.network·push.firebase pre.2), SHA Delivery.md 대조 PASS, 기준선 `PROJECT_CONTEXT.md`. 게임 코드는 문서 주석 1곳(`SetResponseApplier`→`AddResponseApplier`). 잡 `mv-bundle4-a-01` PASS(`3137497`, compile, EditMode 504/504, PlayMode 7/7), dev 병합 후 `mv-bundle4-g-01` PASS(`2141255`, compile, EditMode 543/543, PlayMode 7/7, 기대 목록 `_notes/expected/dev-full-bundle4.json`; 그 뒤 dev 변경은 문서만). `a-03`은 시험 543/543 통과했으나 실행 중 다른 세션이 칸 a를 옮겨 SOURCE_CANDIDATE_CHANGED. NOT_RUN: Android 실기·Player(사람 확인, 병합 후). 설정 절 「Framework 범위」 버전 표기는 옛 값 — 설정 화면 재저장 대상. MV-NET-SERVER ④⑤는 이제 `LocalTimeout`·`AddResponseApplier`로 우회 없이 가능. 처음 고정된 14개는 범위 밖이라 연결 안 함 |
| MV-UI-META | 1차 완료 | `fa7bac4` | 화면·PlayMode NOT_RUN. 총괄: Addressables `UIPopup_GodSelect`·`AreaUpgrade`·`Rebirth`·`Map`·`FeatureUnlock`(Remote_UI), `UIPopup_Debug`(개발 전용 그룹) — PLAY-INT 병합 뒤. 요청 MV-META: 로컬 `debug.*` 처리기 8개, `MvMeta.DisarmRebirth()`. 결정 후보: 지도 필터(`05_contentOpen.showOnMap` 빈칸 → conditionType 1 전부), 지도 체크포인트 문구, 05 §5.1.14 클래스 표 소유(테스트 패널·공통 틀을 META가 만듦) |
| MV-UI-LOBBY | 1차 완료 | `2367b4c` | 화면 NOT_RUN. PLAY-INT 2차로: `MvGameLoop.EnterLobbyAsync`에 `OnFacilityBump`·`ApplyFacilities()`(1초마다 권장). 요청 MV-META: `settings.muted` 저장, `MvLobbyBuyResult` 화면 입구. MV-UI-META: `WatchAdForReward`도 가짜 광고 팝업 경유. MV-DATA 후보: 아지트 시설 단계 문턱(정본 azStages L3469~3471) 표로. 결정 후보: 가짜 광고 3초 자동(05 §5.12) vs 버튼(지금), 컨텐츠 버튼 「준비 중」 임시. 남음: 시작 무기 덱 표시(§5.5.2), 구역 클리어 문구, 배너 |
| MV-UI-CONTENT | 1차 완료 | `14db621` | 담당 잡 `f3bc8f255c40` PASS, 화면 NOT_RUN. 모드 탭(400_content)·성장 탭(05_contentOpen key 목록)·잠금 토스트·하루 한도·`RecordModePlay`→닫힘→시작 콜백. G7 연결 지점: `ContentMenuData.StartModeHandler`(모드 시작)·`OpenMetaHandler`(성장 탭). 요청 MV-META: 입장권(02_playResource) 보유·충전·차감 API, `ContentEntry.CanEnter/TryEnter` — 교체 지점 `MvContentMenuPort.TicketCount`(지금 「-/3」, 소탕·광고 +1 없음). 결정 후보: 시작 콜백 없으면 메타 기록 안 함, 성장 탭 행 구분 열 없음(MV-DATA 후보) |
| MV-PLAY-INT 2차 | 완료 | `ac38d4d` | 시설 단계·`meta.setMuted`·구매 결과(`MetaResult.LvBefore/After·Tier`)·보상 광고 관문(`WatchAdForReward(key,done,gate)`, 사용 수 1번)·Fx 머티리얼 주소·디버그 상수. 결정 후보 D-PI2-1 가짜 광고 여는 코드 2곳 합치기, D-PI2-2 TMP Essential Resources(`TMP Settings`) 없음 — 배치 PlayMode에서 로비 1초 넘으면 No graphic device 오류, 한글 글꼴 결정과 함께 |
| MV-VIEW 3차 고양이·충돌 연출 | 완료 | `3a2c686` | 전체 확인 `755bab57f29b` PASS(EditMode 489, PlayMode 7). CatView(플레이어·친구 최대 4, 뒷모습, Climb/Run/Idle 프레임, 단계 아틀라스 필요 시 로드), ObjectReaction(벽 흔들림·섬광·롤 관문·벽돌 빠짐·광륜·별·장난감 줄), 막힘 뒤 고양이 정지. 화면·에뮬레이터 NOT_RUN(설정은 빌드·에뮬레이터 허용 `00bfe28`, 실행기에 에뮬레이터 실행 기능 없음, 빌드는 MV-BUILD-ANDROID). 요청 MV-DATA: `106_hitReaction` chargeWell `objShake` 0→1. MV-FX: 막힘 「쾅!」 글자. 결정 후보 D-VIEW-BLOCK |
| MV-VIEW 4차 투사체·창·회전 장난감 | 완료 | `1252823` | 담당 잡 `c89662c378a6` PASS(EditMode 501). 실기 화면에서 털실 투사체 보임. 화면 크기·회전 방향 손맛 NOT_RUN. 요청 MV-RUN: `RunState.SpinA` 올리기(지금 뷰가 게임 시간으로 계산), `RunProjectile.El`. 결정 후보: 풀 상한 64, 등급 8 초과는 8 |
| MV-CONTENT-* (G7) | 진행(보류 해제: Q-01 유지, dev `0fb018f`) | asmdef `14db621` | MV-META-QUEST 1차 재개(칸 c, `ai/MV-META-quest` `99cfff3`에서 이어감). ATTENDANCE·SHOP·G7 모드 계약 다시 배정 예정 |
| MV-META-RESEARCH (G7) | 1차 완료 | `978d3be`, 병합 `7c09c44` | 잡 `1b18270efe96` compile PASS·EditMode 543/543, 화면·PlayMode NOT_RUN. 총괄 연결 완료 `9de637e`(잡 `bcefb090e38e` compile·EditMode PASS 572): `MvGameLoop` 런 시작에 `MvResearch.Modifiers`, `MvBootMeta` 성장 탭 `OpenMetaHandler`(research → 연구 화면), Addressables `Remote_UI`에 `UIPopup_Research`·`UIPopup_Quest`. 화면·PlayMode NOT_RUN. 결정 후보 D-RESEARCH-1: 08 「5계열 동시」 vs 09 저장 `run` 1칸 → 지금 1계열만 |
| MV-META-QUEST (G7) | 1·2차 완료 | `c7bb626`, 병합 `f764632` | 병합본 잡 `0e4eaa2695a3` compile·EditMode PASS(기대 572). 2차 = Framework 퀘스트 비교 사례 B 결함 5건 수정(프로젝트 설정 대화 경유 사용자 「총괄에게 전달」): ① `MvMeta.Applied` 안 재요청 제거 → 로컬 백엔드 보탬(`RegisterContributor`)이 원래 응답 Data에 `mvQuest`를 더해 저장 1번 ② 모든 쓰기 전에 기간 굴림 ③ 월간 전용 칸 `monthly`·`claims`(09 §9.3.5) ④ 화면은 `Store.Quest` 컨테이너만 ⑤ Boot `MvQuest.Install`·정산 뒤 `RecordRun`·로비 퀘스트 버튼·빨간 점, 통합 시험 `Settle_OneRun_AddsDailyProgressInSameSave`·`AnyWrite_RollsStalePeriodBeforeSaving`. NOT_RUN: PlayMode·화면, `RecordModeClear` 호출부(모드 결과 흐름 없음). 총괄: Addressables `UIPopup_Quest` 등록. 결정 후보: 일일 기준 UTC 0시(D-77 A) vs 09 adDayUtcOffsetHour, 런 계단·벽 기록 저장 2번(정산 파라미터로 합치면 `MvRunOutcome` 변경), weaponCraft 수 없음(MV-RUN 요청) |
| MV-QA-ACCEPT (G8) | 보류(2026-11-11 사용자 범위 축소) | — | 디자이너 제작물 뒤 다시 계약 |
| 표 구글 시트 | 2차 완료(2026-11-10, 사용자 「컬럼명과 데이터는 구분되게 색상 정리 … 최상단에 각 컬럼 기능 설명, 별도 시트로 각 테이블 시트 설명 … 참고는 정크버스터 테이블로 … 테이블 사용시 시트 규칙에 추가」) | 시트 [moonVillage_Table](https://docs.google.com/spreadsheets/d/1f5D-B2bEbgKoqO2jHk_Ki5RU3vmlo_JlbU15cOcl4U8/edit) (내 드라이브, 링크 그대로) | 탭 71 = 「시트규칙」·「표목록」(표 69개의 이름·하는 일·정본 절·읽는 코드·열·행 수) + 표 69탭(이름 = `Assets/Game/GameData/Table/TSV/<이름>.txt`). 머리 3줄 색: 설명 회색 기울임 / 형식 하늘색 / 열 이름 노랑 굵게, 데이터 흰색, 위 3줄·첫 열 고정. 열 설명 844/844 채움(기획서 539 + 보강 305, 그중 「(추정)」 13: 410 selectionType·selectionCount, 701 popupType·banner·displayImage·badgeType·badgeValue·startTime·endTime, 702 sectionType·hideIfEmpty, 460 grade, 804 priority). 규칙은 기획서 07 §7.1.1a. 대조: 데이터 14,843칸 차이 0(118칸은 gviz가 비워 미확인). 정본은 저장소 TSV. 다시 만들기 `py -3 -I C:/Project/MoonVillage.wt/_notes/tools/make_table_xlsx.py <저장소> <출력.xlsx>`(입력 `coldesc.json`·`tabledesc.json`·`rules.tsv` 같은 폴더) → 「파일 → 가져오기 → 스프레드시트 바꾸기」 |
| MV-BUILD-ANDROID 빌드 메서드 | 완료, 실기 실행 확인(2026-11-10) | `f2d802e`, 실기 수정 `b631b1e`·병합 (이 커밋 앞) | 갤럭시 S23 Ultra(Android 16)에 설치·실행, 30초 로그 E/Unity 0, 런 화면 캡처 정상(빌드 `b6051036c510`). 고친 것: Mono·ARMv7 → IL2CPP·ARM64(D-BUILD-DEV 변경), Addressables 그룹 전부 Local(사용자 「원격 에셋은 안쓸거니 다 로컬로」, `MvArtSetup`도), `Assets/Game/link.xml`(IL2CPP 코드 제거가 Framework 자동 등록을 지움), 빌드 전 TableBuilder로 Resources 표 산출물 생성·표 대체 경로 제거(사용자 「대체 경로로 읽지말고」). 전체 확인 `e6ba845508ef` PASS(EditMode 504, PlayMode 7). 체크리스트 `Docs/AI/ANDROID_BUILD_CHECKLIST.md`. 남음: 경고 `SpriteAtlasManager.atlasRequested`(Atlas_Fx_Common·테마), 라벨 키 그대로 표시(현지화 미연결), `google-services.json` 없음, 실행기 설치·실행 모드 없음(중앙 r-73959ba9f77b) |
| MV-NET-SERVER 서버 전환 | 보류(서버 붙일 때, 사용자 「응 그렇게 진행해」 2026-11-10) | — | ① network.firebase 연결·Firebase SDK(App·Functions) — 완료(dev `b8fde6f`, 사용자 「SDK 내려받기 허용」) ② 부팅에서 `NetworkManager.SetServerProvider(new FirebaseNetworkProvider(...))` 1번, 로그인 패키지와 같은 FirebaseApp ③ `NetworkManager.DefaultRoute = Server` 1줄(지금 GameBootstrap이 Local 고정), setter가 대기 요청 취소·캐시 비움이라 요청 전 1번만 ④ `DefaultTimeout`을 null로 끄지 않고 경로 바꿀 때 시간 제한도 함께 설정 ⑤ `SetResponseApplier`는 한 슬롯 — `NetworkDataBinding.Connect`와 게임 자체 반영(MvMeta 등) 충돌 먼저 확인 (Framework 평가 2026-11-10, 중계 경유 사용자 「응 전달해」: ④ 로컬 시간 제한 채택 예정 — 지금 우회는 로컬 요청에 `request.Timeout`을 길게, ⑤ 반영기 여러 개 채택 예정 — 지금 우회는 게임 반영기 하나를 `SetResponseApplier`로 등록하고 그 안에서 `NetworkDataBinding.Apply(repository, request, result)`를 직접 불러 Connect와 게임 반영을 함께) ⑥ 로컬·서버 응답 동일성 시험 + DefaultRoute만 바꿔도 요청 클래스가 도는 회귀 시험, 서버 함수 없는 동안 가짜 `INetworkProvider`로 시험 틀 먼저. 출처: mv-process-improve 소스 정리(컴파일 미확인) ⑦ 서버 전환 때 확인: Framework가 로컬 응답의 시간 필드(TimeKey) 누락 결함을 고쳤는지(오류 코드 표는 게임 몫), 로컬·서버 동일성 시험 도구는 Framework가 서버 전환 때로 미룸 |
| MV-RUN-STARTCUR 시작 재화 분리 | 완료(2026-11-11) | `d7140ba3` | PC-15 기획 확인. EditMode 225/232(Q-04 7 건너뜀) |
| MV-00-bundle5 Framework 5차 | 완료(2026-11-11) | `a24de901` | quest·badge 연결만(사용처 없음). EditMode 589/596, PlayMode 7/7 |
| MV-UNLOCK-1·MV-SPEC2-FIX2 | 완료(2026-11-11) | `440ba43f` | 단계별 해금 연출·지도 Level 문구 미구현, 04 745·843행 옛 문장. [창작] 모드 2개 표에서 꺼 둠 |
| MV-RUN-SPLITREWARD 보너스 등분 | 완료(2026-11-11) | `f544040f` | 105 rewardShares(금화 10·나머지 1), hitReward 열 삭제. MainParity 15개 다시 기록 |
| MV-HARDCODE-1a 화면·연출·소리 | 완료(2026-11-11) | `7e2a7c50` | 표 열 없는 그리기 상수 남김(PC-23), 친구 무늬(PC-22) |
| MV-00-bundle5b 외부 도구 | 완료(2026-11-11) | `70aacce6` | Spine b81e5a58·Coffee 4.11.3·DOTween asmdef 3개, 4개 연결(사용처 없음). Spine Editor 라이선스 확인 필요 |
| MV-00-bundle6 프로젝트 설정 맞춤 | 완료(2026-11-12, 사용자 「프로젝트 설정 맞춰서 적용해줄래? 프레임워크」) | `ai/MV-00-bundle6-settings` | 설정 저장 11-12 18:20 범위대로 베타 고정본 갱신 12·추가 2(localization.tmp·ui.contracts)·network.firebase 제거, MV.UI.asmdef에 ui.contracts 참조. compile PASS, EditMode 573 PASS·7 Ignore(Q-04), PlayMode 7/7. 화면·기기 NOT_RUN. 기준선 PROJECT_CONTEXT 6차 |
| MV-HARDCODE-1b 규칙·표 중복 | 완료(2026-11-11) | `13c04949` | 남은 줄 PC-16~21, 115↔330 lap 중복(1a 쪽과 함께) |
| MV-RUN-MIDZONE 평지 감속 제거 | 완료(2026-11-11) | `1fb4a25f` | 310 midFromZ·midToZ 빈칸(걷기 항상 4), 중간 구역 = 계단 오르는 구간(임시, PC-10). 사람 확인: 사용자가 속도 4 확인 |

## 이번 회차 범위 (2026-11-11 재정정: 새 기획 표·규칙으로 기본 모드)

근거: 중계 경유 사용자 원문 「일단 테이블은 새 기획에 맞추어서 수정을해야해 그리고 연결은 되면 좋겠어」, 「디자이너들이 투입되서 리소스 제작을 할거라 기존 프로토 에셋을 고집할 이유가 없어 … 그때 다시 그걸보고 작업하는게 좋을거 같고」, 「작업은 보류야 지금 남은 작업은 id 규칙 병함, 테이블 수정 및 적용 까지라고 보면 될거같아 기본 모드만 돌아가는 상태인거지」, 「새 규칙 코드 넣어야해」, 「유니티 체크를 좀더 느슨하게 하면 안돼?」(문안 보고 「보내」).

**목표: 기본 모드(본편 런)가 새 기획(MV-SPEC-02)의 표와 규칙으로 돌아가는 상태.** 기준 `Docs/MV-SPEC-02/DiffReport.md` 「결정 기록」 1~9차, `TermIdMap.md`, `GodRefill.md`.

| 순 | 작업 | 상태 |
|---|---|---|
| ① | id 규칙(MV-DATA-IDRULE): 시트 id 수식, `02_playResource` 2001001~, 검증기 표 번호 검사 | dev 병합 `46e4921` |
| ② | 표 새 기획 반영(MV-DATA-SPEC2): `ai/MV-DATA-SPEC2-tables` 이어서 완료·병합 | dev 병합 `b48e312` |
| ③ | 코드 연결 + 새 규칙 코드(MV-RUN-SPEC2): 바뀐 key·행·열 읽기, 계단 오르기 공식(`330_climb` 상수 열), 계단 게이트 M, 지팡이 단계·`AddWeapon`, 분신 상한, 결계 막힘, 하수인 위치. ②와 병렬로 시작 | dev 병합 `32b454d` (2026-11-11). 지팡이 12단계(Q-08, 사용자 「행도 키고 이전 성장값 참고해서 추가해서 넣어 q-08 지팡이 기획서에 없는 내용이면 우선 테이블에서 설정만 하게하고 더미값 연결해」): 103 w9~w12 active, 12_weaponLevel 9~12 need 24/28/32/37 더미, 10 weapon max 254, 16_levelPrice Lv150~254 더미, 상한은 `MvTables.WeaponTierMax()`로 표에서 읽음. 검증(de202a1): build PASS, PlayMode 7/7 PASS, EditMode 587 PASS·7 Ignore(Q-04 덤프 대조)·0 FAIL — 실행기는 Ignore를 FAIL로 표시하나 사용자 판정 「통과로 봐」(2026-11-11)로 PASS 취급. APK 빌드 PASS(`32b454d`, run 20261110T200803457460Z-9b98b76a70, 슬롯 f `Builds/Android/MoonVillage-dev.apk`), 기기 설치·실행 NOT_RUN |

공통 완료 기준: 하드코딩 금지(상수·한도·위치는 표 열·변수, 맵 배치는 표·프리팹 정본), 「기획서보다 적은 표값은 추가해서 더미값 넣어둬」(더미는 시트 색 표시), Q-34 key까지 새 기준·저장 이관 없음.

**확인 완화(사용자 「유니티 체크를 좀더 느슨하게」):** 작업 중에는 compile만 돌린다. EditMode·PlayMode·dev 전체·APK 빌드는 ②·③을 모두 넣은 뒤 마지막에 한 번만 돌린다. 중간 병합의 시험은 NOT_RUN으로 적는다. 표 산출물 재생성(빌드 3c)은 표를 바꿀 때 필요하므로 예외로 돌린다.

**보류(디자이너 제작물·다음 지시 뒤 다시 계약):** 출석·상점, 모드 3종(이 중 츄르 털이는 [창작], 기획서 근거 없음 — 대왕쥐 소탕도 같음, 「확정 결정」 2026-11-11), 로비 남은 것(가짜 광고 경로 통합 D-PI2-1 포함, 총괄 배정), 현지화 연결, 아틀라스 경고, 한국어 글꼴, MV-UI-FX2, FX·오디오 Addressables, G8 MV-QA-ACCEPT. 아트·UI·아틀라스·프리팹은 디자이너 제작물이 나온 뒤 다시 계약한다. 이미 병합된 모드·퀘스트·연구 코드는 그대로 둔다.

설정 문서 기획서 위치(`c82ed25`, MV-SPEC-02/Source)는 이번 회차 기준과 맞다.

**시트 → 게임 표 가져오기·자동 적용: 사용자 결정으로 하지 않음(닫음).** 사용자 원문(2026-11-11, 「미확인 알림」 대화 경유): 「시트만 열기로하고 테이블 적용은 그냥 사람이 수동으로 하려했어 당분간은 … 이건 만들지말라고 테이블 프레임워크 쓰는곳에 전달해 과투자야 추후 프레임워크에서 만들었을때나 쓰지 그전에는 그냥 사람이 옮기면돼」. 시트 내용을 TSV로 옮기는 일은 사람이 한다. 참고: 기존 에디터 변환기 `MV.Editor/Tables/MvSheetTsvExporter`(2026-11-09, 내려받은 시트 TSV 텍스트에서 설명 2줄·`#` 열을 빼는 함수)가 있다. 새 기능을 더하지 않고, 자동 가져오기·적용으로 키우지 않는다. `make_table_xlsx.py`(TSV → 시트용 xlsx)는 반대 방향이라 해당 없음.

## 담당표

| 칸 | 실행기 | 지금 계약 | 브랜치 | 쓰기 범위 |
|---|---|---|---|---|
| a | `moonvillage-a` | (비어 있음) | — | — |
| b | `moonvillage-b` | (비어 있음) | — | — |
| c | `moonvillage-c` | (비어 있음) | — | — |
| d | `moonvillage-d` | (비어 있음) | — | — |
| e | `moonvillage-e` | (비어 있음) | — | — |
| f | `moonvillage-f` | (비어 있음) | — | — |
| g | `moonvillage-g` | (비어 있음) | — | — |

- dev 전체 기대 시험 목록: `C:/Project/MoonVillage.wt/_notes/expected/dev-full.json`(EditMode 491, PlayMode 7, `f2d802e` 기준).
- 테스트 폴더 `C:/Project/MoonVillage`는 항상 `dev`. 칸은 브랜치만 바꿔 재사용. 실행기 하위 호스트 위임 만료 2026-11-19.

## 작업 순서 구조 (Unity 병목, 2026-11-10)

PC 한 대에서 Unity 무거운 작업은 한 번에 하나다. 그래서 순서를 이렇게 정한다.
1. **Unity 줄(직렬)**: compile·EditMode·PlayMode. 계약당 원칙 2회(compile 1 + 모듈 EditMode 1, 실패 수정 시 +1). 줄이 길면 새 배정을 늘리지 않는다 — 동시에 Unity를 쓰는 담당은 4명 안팎.
2. **Unity 없는 줄(병렬)**: 문서·표 TSV·Node 추출·순수 C# 작성. Unity 줄이 막힐 때 이쪽 일을 먼저 배정한다.
3. **dev 검증은 묶어서**: 쓰기 범위·asmdef가 겹치지 않는 계약은 계약 브랜치 PASS로 바로 병합하고 그 SHA를 증거로 둔다. dev 전체 EditMode는 그룹 끝, 또는 통합 Writer 파일(asmdef·Scene·Addressables·manifest·ProjectSettings)을 건드렸거나 쓰기 범위가 겹친 병합 뒤에만 한 번 돈다. 시험은 규칙·경계 단위로 두고 줄·필드마다 만들지 않는다(D-93).
4. **Unity 없는 계약을 사이에 섞는다**: Unity 단계가 대기열에 슬롯 수의 두 배쯤 밀려 있으면 Unity가 필요한 새 계약은 띄우지 않고 문서·표 TSV·Node 추출·순수 C# 작성 계약을 먼저 띄운다. 한 계약의 Unity 단계는 끝에 모아 낸다.
5. **담당 운영(D-94)**: 담당은 컨텍스트 크기만 보고 끊지 않는다. 같은 모듈 연속 계약이고 앞 계약이 병합·기록됐고 쓰기 파일이 겹치며 입력이 15만의 절반 이하면 같은 칸 담당이 다음 계약을 이어 받는다. 담당이 Unity 잡만 기다리면 독립 일을 하고, 일이 없고 30분 넘게 기다리면 잡 ID를 원장에 적고 닫는다. 슬롯을 쥔 담당과 쓰기 범위가 겹치는 계약은 그 슬롯이 끝나기 전에 배정하지 않는다. 중앙 `External.md`에 같은 규칙.
6. 우선순위(D-91): 플레이 가능한 빌드 경로(G3-INT → VIEW 2차 → HUD → PLAY-INT)를 Unity 줄 앞에 둔다.

## 잠정 결정

묻지 않고 권장안으로 진행 중인 결정이다. 사용자가 「권장안대로 확정」(전체 또는 번호)이라고 하면 「확정 결정」으로 옮긴다. 경로 `S/`는 `Assets/Game/Scripts/`, 기획서는 `Docs/MV-SPEC-01/`이다.

| 번호 | 요지 | 따르는 권장안 | 바꾸면 달라지는 것 | 되돌리기 | 정본 위치 |
|---|---|---|---|---|---|

## 확정 결정

| 날짜 | 결정 | 원문 또는 출처 | 적용 범위 |
|---|---|---|---|
| 2026-11-11 | D-PI2-1 가짜 광고를 여는 코드 두 곳(로비 `FakeAdPopup.cs`, 메타 `MetaUiPort.cs`)을 한 경로로 합침(권장안 「두 곳 그대로」가 아닌 「바꾸면」 안). 코드 통합은 총괄 몫, 「이번 회차 범위」 보류의 로비·UI 재계약 때 같은 계약으로 넣는다 | 현황판 「다른 안 선택」 2026-11-10 17:46(11-10 stash로 유실, `fa415e69`에서 확인) + 중앙 대화 사용자 「D-PI2-1은 두 가짜 광고 호출 경로를 통합하는 것으로 확정 기록하고, 총괄 작업에 연결해 줘. 코드 통합은 기존 작업 범위에 따라 진행하면 돼」 | 원장 MV-PLAY-INT 2차·MV-UI-LOBBY 행, `S/MV.UI/Lobby/FakeAdPopup.cs`, `S/MV.UI/Meta/Common/MetaUiPort.cs`, D-ADS-2 |
| 2026-11-11 | 기획서에 없는 것은 만들지 않음([창작] 금지): 지시 없는 모드·시스템·규칙·수치를 지어내지 않고, 값이 없거나 「프로토 값」·「원작」이면 출처 값, 없으면 더미 표시 + 결정 후보, 기획서 문장 충돌은 사용자에게 물음(13-2 현재 기준 우선). **[창작], 기획서 근거 없음: 츄르 털이(`churuRush`, 400001003·402·310 rush1·rush2), 대왕쥐 소탕(`bigMouse`, 400001004)** — MV-SPEC-01에서 AI가 만든 모드. 이미 병합된 코드는 이 지시만으로 지우지 않음(보류 모드 3종과 함께 다음 지시 때 처리) | 중계 경유 사용자 원문 「개발 규칙에 추가해줘 명확히 어떤걸 창조해달라는 지시가 없으면 기획서 내용을 준수한다 츄르털이, 대왕쥐 소탕 이런 창작은 위험해」 | AGENTS.md 「경계」, `_notes/CONTRACT_COMMON.md`, 모든 계약 |
| 2026-11-11 | Q-13 결계 막힘 종료 시간: 13-2 현재 기준(프로토 값 = 즉시 종료) `300_runConfig` STUCK_END_SEC 3(더미) → 0. 코드는 그대로(0이면 닿는 프레임에 종료), 3초 전제 시험만 0 기준으로. 구글 시트는 사용자가 직접 고침. MV-RUN-STUCK0 | 중계 경유 사용자 원문 「우선 0초로 다시 수정해줘 저값은 구글 시트는 내가 수정해둘게」, 근거 `Docs/MV-SPEC-02/Source/01_코어게임루프.md` 13-2 | 300 TSV·Resources, `Spec2RuleTests`, `RunSimTests`, 07 STUCK_END_SEC 행, DiffReport Q-13 |
| 2026-11-11 | 화면 방향: 세로 정방향·역방향 자동 회전, 가로 끔(`defaultScreenOrientation` 4, Landscape 자동 회전 0). 프로젝트 생성 때부터 거꾸로 세로(1) 고정이었음. MV-ORIENT dev `c89bd8a`, compile PASS, APK 빌드 PASS(run 20261110T204450539688Z-366a1dcd94), 폰 회전 확인 NOT_RUN | 중계 경유 사용자 원문 「세로 화면 방향도 디폴트 방향 반대로 돌릴수있으면 돌려줘」 | `ProjectSettings/ProjectSettings.asset`(통합 Writer) |
| 2026-11-11 | 기본 모드 남은 수치는 지금 표 값으로 확정, 이후 수치는 기획자가 표에서 직접 고침: 분신 5~30 발사 위상 `300_runConfig` FIRE_PHASE(0.13/0.26/0.07/0.19 반복), 지팡이 9~12단계 강화 need 24·28·32·37(`12_weaponLevel`), 무기 최대 레벨 254(`10_lobbyUpgrade`), `16_levelPrice` Lv150~254(30×1.043^(Lv−1)), 지팡이 스택 상한 WEAPON_STACK_MAX 0(상한 없음), Q-03 게이트 배율 `107_chargeGate` useScale 1(sgate·cgate). 시트 더미 색 표시는 기획자가 값을 고칠 때까지 둠 | 중계 경유 사용자 원문 「나머지는 우선 지금으로 확정하고 기획자가 직접 테이블 수정하도록할게」(문안 보고 「보내」) | MV-RUN-SPEC2·MV-DATA-SPEC2, dev `32b454d` |
| 2026-11-11 | Q-04 프로토 덤프 대조 7건(GenDumpTests Easy·Mid·Orig, SimReplayTests BASE·RUN01~03)은 Q-04로 꺼 둠(Ignore), 새 기준 대조로 교체 예정. 이번 회차 EditMode 587 PASS·7 Ignore는 통과로 봄 | 사용자 「통과로 봐」, 중계 경유 같은 결정 | MV-RUN-SPEC2 시험, 실행기 SKIPPED_TESTS 표시 |
| 2026-11-10 | D-27 닫음: 레벨 칸 sin 해시(`JsMath.Sin`)와 PC 골든 시험은 그대로, 폰 실기 FIELD 골든 대조는 하지 않음(NOT_RUN 아님, 사용자 방침으로 제외). 레벨링을 다시 짤 때 시드 정수 RNG(System.Random·xorshift)로 교체 가능, UnityEngine.Random은 전역 상태라 안 씀 | 중계 경유 「유니티 랜덤으로 대체 못해? 그리고 어차피 느낌 보고 레벨링 시스템은 다시 구성될거야」 | 04 §4.14.3·D-27 행, 13 §13.3.3 |
| 2026-11-10 | AI Unity 실행에 빌드·기기·에뮬레이터 실행 허용(프로젝트 설정 저장 13:38, `00bfe28`). Android 빌드 메서드 계약 MV-BUILD-ANDROID | 중계 경유 카드 「허용 (권장)」 → 사용자가 설정 화면에서 저장, 이어서 「계약 만들기」 | `Docs/AI/PROJECT_CONTEXT.md`, 원장 MV-BUILD-ANDROID 행 |
| 2026-11-10 | D-94 담당 운영: 컨텍스트 크기만 보고 담당을 끊지 않음, 같은 모듈 연속 계약은 같은 칸이 이어 받음, Unity 잡만 기다리면 독립 일·30분 넘으면 잡 ID 적고 닫음 | 「5번도 권고안대로한다해」 | 원장 「작업 순서 구조」 5, 중앙 `External.md` |
| 2026-11-10 | D-93 시험은 규칙·경계 단위로 적당히, 계약 브랜치는 compile 1 + 모듈 EditMode 1, dev 전체는 그룹 끝·통합 Writer 변경 뒤만. 패키지 설정(프로젝트 설정)을 우선 | 「AI한테 작업 맡기면 너무 테스트가 많아.. 확인 하고 하는건 맡긴한데 그게 너무 세분화 되어있어」, 「저거 프로젝트 설정을 우선하도록 규칙 바꿔줘」 | 원장 「작업 순서 구조」 3·5, `AGENTS.md` Framework·Unity 절, 중앙 규칙 |
| 2026-11-10 | D-87 인수 기준: 규칙·손맛·표 구조로 판정. 덤프 완전 일치·캡처 수치는 완료 조건 아님(기존 PASS 시험 유지) | 「완전 일치일 필요 없어 규칙과 룰 느낌 보는게 중요하고 그걸 내가 생각하는 데이터 구조를 가지는지가 관건이고」 | 12 §12.0, 13 §13.3.8, MV-QA-ACCEPT |
| 2026-11-10 | D-88 결제·통계·알림은 패키지만 넣음(스텁 구현 안 함), 고정 tgz를 `Packages/Frozen/`에 연결. IAP·ANALYTICS 스텁 브랜치는 병합 안 함 | 「결제 분석 알림은 일단 패키지 넣어두기만 해도 돼」 | 11 §G6, MV-PLAT-IAP·ANALYTICS·NOTI |
| 2026-11-10 | D-89 백엔드 구조 유지: MV-PLAT-NET(요청 + 로컬 백엔드) | 「백앤드 서버 구조는 그래도 유지하면 좋을거 같은데 이게 큰거겠지?」 | 10, PROJECT_CONTEXT 「AI가 따를 규칙」 |
| 2026-11-10 | D-90 로그인은 병합된 그대로 두고 1차에서 더 연결하지 않음 | 「로그인은 우선 안해도 괜챃아」 | MV-PLAT-LOGIN |
| 2026-11-10 | D-91 플레이 가능한 빌드 우선(G3 통합 → MV-VIEW → Game 씬·최소 HUD) | 같은 대화의 원문에서 정함(13 §13.3.8) | 12 §12.0, 원장 「작업 순서 구조」 6 |
| 2026-11-10 | D-92 1차 UI는 「구조는 실제, 겉은 더미」: HUD는 추출 스킨, 로비·메타·컨텐츠는 uGUI 기본 상자(OnGUI 금지), 연결은 실제, 연출은 2차(MV-UI-FX2) | 「아 UI도 그냥 리소스 없이 OnGUI나 더미로 구성하면 많이 축소 가능해?」 → 권장안 설명 뒤 「응 그렇게 진행하자」 | 05 머리, 11 §G5, 12 §12.0 |
| 2026-11-10 | 패키지: 프로젝트 설정의 Framework 범위 20개 전체(`network.firebase` 포함) + Firebase·AdMob SDK를 설치. bundle3 17개 + `com.unity.purchasing` 5.3.1 연결 완료(`d0491cb`) | 중계 경유 사용자 원문 「총괄한테 프레임워크는 보류하지말고 프로젝트 설정에 맞추어서 설치하라해」, 연결은 사용자 직접 허용 | `Packages/manifest.json`, `Packages/Frozen/`, 원장 MV-00 bundle3 행 |
| 2026-11-10 | NET 스텁을 Framework `platform.network`로 교체(MV-PLAT-NET 2차) | 「NET 스텁교체해줘」 | 칸 f `ai/MV-PLAT-NET-swap`, `S/MV.Platform/Net/` |
| 2026-11-10 | 외부 SDK 내려받기 허용: Firebase 13.17.0, AdMob 10.7.0 SDK(남은 `ads.admob`·`login.firebase`·`network.firebase` 연결용) | 「SDK 내려받기 허용」 | 칸 f `ai/MV-00-sdk`, 원장 MV-00 bundle3 행 |
| 2026-11-10 | MV-NET-SERVER 서버 전환은 서버를 붙일 때까지 보류(①~⑥ 순서 기록) | 「응 그렇게 진행해」 | 원장 MV-NET-SERVER 행 |
| 2026-11-09 / 2026-11-10 | D-08 통신 공급자: 지금은 로컬 백엔드, 서버는 Firebase(`platform.network.firebase`). 전환 시점은 MV-NET-SERVER 보류를 따름 | 프로젝트 설정 「서버 통신 = 씀: Firebase 서버; 서버 기능을 지금 처리하는 곳 = 로컬 백엔드(서버 없음)」 + 위 「응 그렇게 진행해」 (원장 「열린 결정」에 있던 것) | `Docs/AI/PROJECT_CONTEXT.md`, 13 §13.2 D-08, MV-PLAT-NET |
| 2026-11-11 | 츄르 입장권 02 2001003·485 출석 6일차 보상·102 대왕쥐 행은 지금 그대로 둠(출석을 다시 다룰 때 정리) | 중계 경유 「지금은 그대로」 | 원장 MV-UNLOCK-1 행 |
| 2026-11-11 | 외부 도구 설치 허용: Spine 런타임·Coffee UI Particle, 기존 DOTween Pro asmdef 생성, Framework 4개 연결 | 「설치해」, 총괄 대화 「Yes, install」 | 원장 MV-00-bundle5b 행, PROJECT_CONTEXT 기준선 |
| 2026-11-11 | 중간 구역 = 계단 오르는 구간(임시), 평지 감속 없음(걷기 항상 4) | 「중간 구역을 계단 올라가는 동안으로 일단 정의해서 수정해줄수있어?」, 선택 「걷기만 항상 4」 | 원장 MV-RUN-MIDZONE 행, PC-10 |
| 2026-11-11 | 이번 회차 묶음 끝. 다음 작업은 디자이너 리소스·UI 제작물이 오면 다시 시작 | 「작업 다 끝난거 같고 리소스 제작 UI 오면 그때 다시 진행하는게 좋을거같아」 | 원장 「이번 회차 범위」 보류 목록 |
| 2026-11-10 | D-RULE-01 동행 고양이 사격의 기본 성질 → 우물·게이트를 채우지 않음, 즉시 명중(그림만 날아감), 모드 런의 메타 능력치는 402 `useMetaStats`(D-76), 발사 주기에 FireRate를 곱하지 않음 | 현황판 「권장안 확정」 2026-11-10 17:55 | 04 §4.20.7 |
| 2026-11-10 | D-RUN-BLOCK 런이 막혀 끝날 때 끝난 이유를 정본처럼 나눌지 → 막힘은 모두 `FinishReason.Wall` 하나 | 현황판 「권장안 확정」 2026-11-10 17:48 | `S/MV.Run/Core/RunTypes.cs`, 보관 원장 MV-RUN-SIM 행 |
| 2026-11-10 | D-27 레벨 칸을 만드는 sin 해시가 폰(실기)에서도 PC와 똑같이 나오는지 → 실행 중 계산 + 골든 파일 시험(PC 비트 일치 확인). 실기 IL2CPP ARM64 대조는 NOT_RUN | 현황판 「권장안 확정」 2026-11-10 17:48 메모 「레벨 시스템은 추후 다시」 | 04 §4.14.3, 12 §12.3, 13 §13.3.3 |
| 2026-11-10 | D-BUILD-REMOTE 개발 APK에서 Remote Addressables 그룹을 어떻게 읽나 → 개발 빌드 프로필은 Remote 그룹도 APK 안(Local 경로)에 넣음(통합 Writer: Addressables 프로필 1개) | 현황판 「권장안 확정」 2026-11-10 17:48 | Addressables 설정 자산, 원장 MV-BUILD-ANDROID 행 |
| 2026-11-10 | D-BUILD-DEV AI가 만드는 Android 빌드의 형태 → 개발 빌드(Mono·ARMv7·Development·디버그 서명 APK) | 현황판 「권장안 확정」 2026-11-10 17:48 | `S/MV.Editor/Build/MvBuild.cs` |
| 2026-11-10 | D-BUILD-ADMOBID AdMob 앱 ID가 비어 있을 때 빌드 → Google 공개 시험 앱 ID로 채워 빌드(실제 ID가 있으면 그대로) | 현황판 「권장안 확정」 2026-11-10 17:48 | `S/MV.Editor/Build/MvBuild.cs`, GoogleMobileAds 설정 자산 |
| 2026-11-10 | D-BUILD-ID 개발 APK 패키지 이름 → 템플릿 기본값 그대로(`com.UnityTechnologies.com.unity.template.urpblank`) | 현황판 「권장안 확정」 2026-11-10 17:48 | `ProjectSettings/ProjectSettings.asset`, 계약 MV-BUILD-ANDROID |
| 2026-11-10 | D-PLAYINT-BARS 벽 기록을 어떻게 읽나 → 저장 JSON을 거쳐 읽음(MV-SAVE에 `GetBars(area)` API 없음) | 현황판 「권장안 확정」 2026-11-10 17:48 | `S/MV.Boot/MvRunStart.cs`, 원장 MV-PLAY-INT 행 |
| 2026-11-10 | D-PLAYINT-BOOT 에디터에서 Game 씬을 바로 실행하면 → Boot 씬을 먼저 열어 돌려보냄 | 현황판 「권장안 확정」 2026-11-10 17:48 | `S/MV.Boot/GameInitializer.cs` |
| 2026-11-10 | D-PLAYINT-SEED 런 시드를 어떻게 정하나 → 런 순번에서 결정적으로(같은 저장·같은 순번 = 같은 런) | 현황판 「권장안 확정」 2026-11-10 17:48 | `S/MV.Boot/MvRunStart.cs`, 04 §4.17 |
| 2026-11-10 | D-CONTENT-b 성장 탭 목록에 행 구분(묶음) 열을 둘지 → 열 없음. `05_contentOpen` key 순서대로 | 현황판 「권장안 확정」 2026-11-10 17:48 | 원장 MV-UI-CONTENT 행 |
| 2026-11-10 | D-CONTENT-a 모드 시작 연결이 아직 없는 모드를 눌렀을 때 → 「준비 중」 토스트만, 입장 기록(하루 한도) 안 남김 | 현황판 「권장안 확정」 2026-11-10 17:48 | `S/MV.UI/Content/ContentMenuPresenter.cs`, 원장 MV-UI-CONTENT 행 |
| 2026-11-10 | D-LOBBY-AZSTAGE 로비 아지트 시설이 커지는 단계 문턱을 표로 뺄지 → 코드 상수(정본 `azStages` L3469~3471) | 현황판 「권장안 확정」 2026-11-10 17:47 | `S/MV.UI/Lobby/LobbyFacilities.cs`, 원장 MV-UI-LOBBY 행 |
| 2026-11-10 | D-UIMETA-OWN 05 UI 클래스 표의 소유 계약 → 테스트 패널·메타 공통 틀을 MV-UI-META가 만든 대로 둠(표와 다름) | 현황판 「권장안 확정」 2026-11-10 17:47 | 05 §5.1.14 |
| 2026-11-10 | D-UIMETA-CP 지도 체크포인트 칸 문구 → 담당이 넣은 임시 문구 | 현황판 「권장안 확정」 2026-11-10 17:46 | 원장 MV-UI-META 행 |
| 2026-11-10 | D-UIMETA-MAP 지도에 어떤 기능 칸을 보일지 → `05_contentOpen.showOnMap`이 비어 있어 Area 조건(conditionType 1) 행을 모두 보임 | 현황판 「권장안 확정」 2026-11-10 17:46 | 07 §7.8.9, 원장 MV-UI-META 행 |
| 2026-11-10 | D-PI2-2 한글 TMP 글꼴을 넣을지(HUD 한글 글꼴 후보 합침) → TMP 기본 자원은 사용자 「dev에 넣기」로 dev `8bf1ab3`에 들어옴. 한글 글꼴은 넣지 않음, 화면은 uGUI 내장 글꼴 | 현황판 「권장안 확정」 2026-11-10 17:46 | 원장 MV-UI-HUD·MV-PLAY-INT 2차 행, 05 §5.1.7, `S/MV.UI/Common/MvUi.cs` |
| 2026-11-10 | D-HUD-SORT UI용 정렬 레이어 「UI」를 만들지 → 만들지 않음(정렬 레이어는 월드용 10개만) | 현황판 「권장안 확정」 2026-11-10 17:45 | 원장 MV-UI-HUD 행, `ProjectSettings/TagManager.asset` |
| 2026-11-10 | D-LOGIN-2 게스트 id를 어디에 저장하나 → PlayerPrefs(서버 붙일 때 재검토) | 현황판 「권장안 확정」 2026-11-10 17:45 | 보관 원장 MV-PLAT-LOGIN 줄 |
| 2026-11-10 | D-LOGIN-1 로그아웃해도 기기의 게스트 id를 남길지 → 남김 | 현황판 「권장안 확정」 2026-11-10 17:45 | 보관 원장 MV-PLAT-LOGIN 줄 |
| 2026-11-10 | D-ADS-2 (=D-LOBBY-AD) 개발 빌드 가짜 광고를 어떻게 끝내나 → 지금 코드: 「광고 보기(가짜)」·닫기 버튼으로 끝냄. ADS 담당 권장과 정본은 닫기 버튼 없이 3초 뒤 자동 | 현황판 「권장안 확정」 2026-11-10 17:45 | 05 §5.12, `S/MV.UI/Lobby/FakeAdPopup.cs`, `S/MV.Platform/Ads/MvAdTypes.cs` |
| 2026-11-10 | D-NETFW-3 응답 반영기를 여러 개 등록하는 기능을 Framework에 요청할지 → 요청 안 함. `SetResponseApplier` 한 슬롯을 게임이 나눠 씀. Framework 평가: 채택 예정, 그 전 우회는 게임 반영기 안에서 `NetworkDataBinding.Apply(repository, request, result)` 직접 호출 | 현황판 「권장안 확정」 2026-11-10 17:45 | 같은 곳, MV-NET-SERVER 행 ⑤ |
| 2026-11-10 | D-ADS-1 출시 빌드에서 광고를 못 띄울 때(SDK 없음) 처리 → Meta는 「안 봄」으로 처리(보상 없음), 화면은 광고 실패 토스트 | 현황판 「권장안 확정」 2026-11-10 17:45 | 05 §5.12, 보관 원장 MV-PLAT-ADS 줄 |
| 2026-11-10 | D-NETFW-2 로컬 경로별 시간 제한을 Framework에 요청할지 → 요청 안 함. 경로를 바꿀 때 `DefaultTimeout`도 함께 설정. Framework 평가: 채택 예정(사용자가 시작하라고 할 때), 그 전 우회는 로컬 요청 `request.Timeout`을 길게 | 현황판 「권장안 확정」 2026-11-10 17:45 | 같은 곳, MV-NET-SERVER 행 ④ |
| 2026-11-10 | D-NET-2 통신 시험 어셈블리가 JSON 라이브러리(Newtonsoft)를 참조할지 → 참조하지 않음(`MV.Platform.Tests`는 nunit만) | 현황판 「권장안 확정」 2026-11-10 17:44 | `S/MV.Platform.Tests/`의 asmdef, 보관 원장 MV-PLAT-NET 줄 |
| 2026-11-10 | D-NETFW-1 Framework 통신에 「기본 경로」를 설정으로 선언하는 기능을 요청할지 → 요청 안 함. 게임 부팅 코드가 `DefaultRoute`를 1번만 정함(지금 Local). Framework 평가(2026-11-10): 자동 Local은 ＊＊반대＊＊(공급자 등록이 빠지면 출시 빌드가 조용히 로컬로 돔) → 경고 로그로 대체 예정 | 현황판 「권장안 확정」 2026-11-10 17:44 | 원장 「열린 결정」 Framework 요청 후보, MV-NET-SERVER 행 ③ |
| 2026-11-10 | D-NET-1 여러 모듈이 같이 쓰는 시계를 둘지 → 지금은 모듈마다 따로(Meta `IMvClock`, Net `IServerClock`, Sound `IMvSoundClock`). 권장안(MV.Core `IClock` 하나)은 아직 안 함 | 현황판 「권장안 확정」 2026-11-10 17:44 | 보관 원장 MV-PLAT-NET 줄, `S/MV.Meta/MvMetaPorts.cs`, `S/MV.Platform/Net/IServerClock.cs` |
| 2026-11-10 | D-68 저장 파일 암호화 키를 어디에 두나 → 개발 빌드는 공개 고정 키, 출시 빌드는 키 공급자 교체 지점만(지금 출시 빌드는 저장을 못 엶). 권장: 빌드에 키 내장, 키 원본은 저장소 밖 설정 파일 | 현황판 「권장안 확정」 2026-11-10 17:44 | 09 §9.11, 13 §13.3.4 |
| 2026-11-10 | D-SND-2 쥐가 훔칠 때 소리를 쥐 종류별로 낼지 → 종류 없이 한 소리. 종류 id(kindId)는 쥐 종류가 늘 때 MV-RUN에 요청 | 현황판 「권장안 확정」 2026-11-10 17:44 | 보관 원장 MV-SND 줄 |
| 2026-11-10 | D-FX-1 이펙트용 그림 층(팝업·비행 아이콘·플래시·단계 이름)을 누가 그리나 → MV.UI가 `IMvPopView`·`IMvFlyView`·`IMvHudAnchors`·`IMvFlashView`를 구현(asmdef 변경 없음) | 현황판 「권장안 확정」 2026-11-10 17:44 | 06 §6.15, 원장 MV-FX 행 |
| 2026-11-10 | D-49 정본의 2D 부산물(번개 세로 막대·약한 플래시 등, D-44와 짝) → 정본 그대로 유지 | 현황판 「권장안 확정」 2026-11-10 17:44 | 06 §6.x, 02 |
| 2026-11-10 | D-48 숫자 팝업·보상 비행을 그리는 층 → UGUI 오버레이 | 현황판 「권장안 확정」 2026-11-10 17:44 | 06 §6.7.2 |
| 2026-11-10 | D-47 이펙트·팝업의 시간 기준(D-22와 짝) → 정본대로: 이펙트만 히트스톱, 팝업·비행·배너·플래시는 실시간 | 현황판 「권장안 확정」 2026-11-10 17:44 | 06 §6.x, 04 |
| 2026-11-10 | D-46 정본에서 호출되지 않는 잔재 이펙트 → 만들지 않고 키만 예약(frost·flyToWorld 등) | 현황판 「권장안 확정」 2026-11-10 17:44 | 06 §6.x |
| 2026-11-10 | D-45 월드 이펙트 구현 방식 → 혼합: C# 적분 + 이펙트마다 절차 메쉬 1개(`MvFxDriver`), 모양은 스프라이트·메쉬 | 현황판 「권장안 확정」 2026-11-10 17:44 | 06 §6.15, §6.x |
| 2026-11-10 | D-VIEW-WELL 우물 위 잡동사니 배치 → 정본은 매번 무작위, Unity는 결정적 해시로 매 런 같은 모양 | 현황판 「권장안 확정」 2026-11-10 17:44 | `S/MV.View/Math/ObjectLayout.cs` |
| 2026-11-10 | D-VIEW-FLOWER 담장 꽃 장식을 좌우 뒤집을지 → 그림은 왼쪽 담장 기준 ［추정］, 오른쪽 담장은 뒤집기 | 현황판 「권장안 확정」 2026-11-10 17:44 | `S/MV.View/Runtime/PropStreamer.cs`, 원장 MV-VIEW 행 |
| 2026-11-10 | D-VIEW-LABEL 오브젝트 글자판(체력 숫자·이름) 위치 → VIEW 2차 담당이 정한 위치(정본 대조 전) | 현황판 「권장안 확정」 2026-11-10 17:44 | 원장 MV-VIEW 행, `S/MV.View/Runtime/ObjectViewFactory.cs` |
| 2026-11-10 | D-42 색 공간(UI D-37과 짝) → Linear 유지(URP 기본). 기획서 권장은 Gamma, 캡처 대조로 확인 | 현황판 「권장안 확정」 2026-11-10 17:44 | 02 §2.x 의문점·결정 필요, 13 §13.3.3 |
| 2026-11-10 | D-41 긴 폰·태블릿 화면 처리(UI D-30과 짝) → 폭 1080 고정·세로로 늘림. renderScale 폭 1080 상한은 아직(URP 통합 Writer) | 현황판 「권장안 확정」 2026-11-10 17:44 | 02 §2.7.2, 05 §5.1.2, 13 §13.3.2 |
| 2026-11-10 | D-40 2.5D 화면을 Unity에서 어떻게 재현하나 → URP 원근 카메라 + 카메라 평면 빌보드 + 원근 메쉬 | 현황판 「권장안 확정」 2026-11-10 17:44 | 02 §2.1.3 |
| 2026-11-10 | D-ART-06 ［추정］ 크기와 다르게 구운 그림(예: `Fx_Stripe` 기획 16×64, 실제 64×16) → 구운 크기 유지, 03은 나중에 맞춤 | 현황판 「권장안 확정」 2026-11-10 17:44 | 03 §3.5.3(`Fx_Stripe` 16×64 ［추정］), 보관 원장 MV-TOOL-ART 행 |
| 2026-11-10 | D-ART-05 화면 비네트 그림 이름·묶음 → `Tex_UI_Vignette`, 상주 UI 라벨 `ui.core`. 09 9004001은 `Tex_Lobby_Vignette`라 03 §3.5.8과 다름 | 현황판 「권장안 확정」 2026-11-10 17:44 | 03 §3.5.6·§3.5.8, `S/MV.Editor/Import/MvArtRules.cs` |
| 2026-11-10 | D-ART-04 계단 그림 기준점(피벗) → 추정값으로 둠(정본 확인 안 됨) | 현황판 「권장안 확정」 2026-11-10 17:44 | 03 §3.5, 보관 원장 MV-TOOL-ART 행 |
| 2026-11-10 | D-ART-03 정본에 없는 `Crate_Blue_Fish` 그림 → 임시 아트(D-86) | 현황판 「권장안 확정」 2026-11-10 17:44 | 03 §3.13, 보관 원장 MV-TOOL-ART 행 |
| 2026-11-10 | D-ART-02 관문 왼쪽 기둥을 오른쪽 그림 뒤집기로 쓸지 → 뒤집기(flipX). 원근 기울기가 정본과 어긋날 수 있음 | 현황판 「권장안 확정」 2026-11-10 17:44 | 03 §3.5.3, `S/MV.View/Runtime/ObjectViewFactory.cs` |
| 2026-11-10 | D-ART-01 관문 금빛 기둥이 일반 기둥과 같은 그림일 때 합칠지 → 두 이름 그대로 둠(`Arch_Post_Gold` = `Arch_Post` 같은 바이트) | 현황판 「권장안 확정」 2026-11-10 17:44 | 03 §3.5.3, 보관 원장 MV-TOOL-ART 행 |
| 2026-11-10 | D-ATLAS-GODTEX 야옹신 큰 그림(`Tex_God_＊`) 파일 위치 → `Art/Source/Texture`에 둔 채 주소만 붙임 | 현황판 「권장안 확정」 2026-11-10 17:44 | 03 §3.4.2, 보관 원장 MV-ART-ATLAS 행 |
| 2026-11-10 | D-ATLAS-FRIEND2 친구 고양이 아틀라스(Friend_T1~T6)가 한 장에 들어가는지 → 한 장 가정. 실측 패킹 NOT_RUN | 현황판 「권장안 확정」 2026-11-10 17:44 | 보관 원장 MV-ART-ATLAS 행, 03 §3.8.1 |
| 2026-11-10 | D-MVDATA-PET 펫 월드 그림을 어느 아틀라스에 넣나 → 새 아틀라스 `Atlas_World_Pet` | 현황판 「권장안 확정」 2026-11-10 17:44 | 07 §7.10.3, 03 §3.8.1 |
| 2026-11-10 | D-SWAP-3 400 표에 없는 모드 key의 하루 한도 → 한도 없음 | 현황판 「권장안 확정」 2026-11-10 17:44 | `S/MV.Meta/`(`RecordModePlay`), 07 §7.9.1 |
| 2026-11-10 | D-SWAP-2 이펙트 부품의 원소 조건 처리 → 번개(bolt)가 아니면 모두 불(fire). key가 비어 있는 `god.blast`만 예전 색과 다름 | 현황판 「권장안 확정」 2026-11-10 17:44 | 06 §6.15, `04_effect.txt` |
| 2026-11-10 | D-SWAP-1 길가 소품을 무엇으로 고르나 → 정본 `deco` 문턱 우선, `roadsideProps`는 이름 목록 | 현황판 「권장안 확정」 2026-11-10 17:43 | 07 §7.4.4, 원장 「열린 결정」 SWAP 줄 |
| 2026-11-10 | D-400-LIMIT 모드마다 하루 입장 횟수 한도를 따로 둘지 → 두지 않음(400 `dailyLimit` 모두 0, 입장권 하루 3장만) | 현황판 「권장안 확정」 2026-11-10 17:43 | 07 §7.9.1 |
| 2026-11-10 | D-72-LINE Zone 2~6 테마(304 6~15행)의 빨랫줄·전구줄 종류 고르는 규칙 → 정본 규칙(`deco` 값)대로. 밤 테마도 빨랫줄 | 현황판 「권장안 확정」 2026-11-10 17:43 | 07 §7.4.4 |
| 2026-11-10 | D-DATA-450PORT 친구 고양이 초상화 그림 이름 접두 → `Portrait_Friend_＊` | 현황판 「권장안 확정」 2026-11-10 17:43 | 03 §3.4.1, 08 §8.5.4 `450_friend` |
| 2026-11-10 | D-DATA-701OFF 2차 상점 상품을 표에 둘지 → 2차 상품 행은 넣되 `active 0`(지금 20행 꺼짐, 9행 켜짐) | 현황판 「권장안 확정」 2026-11-10 17:43 | 08 §8.5.7·§8.6.2, `701_product.txt` |
| 2026-11-10 | D-DATA-413STEP 반복 퀘스트 표(413)에 단계(step) 열을 둘지 → 열 없음. 순서는 `sort`, 단계별 목표 증가는 `clearValueStep` | 현황판 「권장안 확정」 2026-11-10 17:43 | 08 §8.5.1, `413_repeatQuest.txt` |
| 2026-11-10 | D-DATA-480KEY 축복 표(480)에 능력치 키 열을 둘지 → 열 없음. 장비 축복은 효과 종류 4 = Dmg로 정함 | 현황판 「권장안 확정」 2026-11-10 17:43 | 08 §8.5.9, 04 §4.20.4, `480_blessing.txt` |
| 2026-11-10 | D-DATA-802401 수비전 택배 상자(카드 등급 뽑기) 상자 행을 지금 넣을지 → 행을 넣지 않음(802 표에 없음). ［추정］ 카드 종류 601의 id는 카드 행 id라 「등급」을 적을 방법이 없음 | 현황판 「권장안 확정」 2026-11-10 17:43 | 08 §8.5.12, `Assets/Game/GameData/Table/TSV/802_randomBox.txt` |
| 2026-11-10 | D-DATA-CTYPE 재화 종류 enum을 Framework 생성기로 만들지 → 손으로 쓴 `MV.Data.ConsumeType` 유지. 고정본 0.1.0-pre.7 생성기는 손 enum과 충돌해 옮길 길이 없음 → Framework 요청 | 현황판 「권장안 확정」 2026-11-10 17:43 | `S/MV.Data/Core/ConsumeType.cs`, `S/MV.Editor/Tables/MvConsumeTypeCodegen.cs`, 07 §7.8.12 |
| 2026-11-10 | D-MVDATA-RES 표가 가리키는 그림이 아직 없을 때 오류로 볼지 → 아직 안 뽑은 그림·그림 없음은 경고 목록, 이름 틀림·주소 없음은 오류 | 현황판 「권장안 확정」 2026-11-10 17:43 | 07 §7.10.5 (현재 원장 「열린 결정」에는 없고 보관 원장에만 있음) |
| 2026-11-10 | D-META3-3 테스트 패널의 부활 해제를 어떤 길로 부르나 → `RebirthPresenter`를 직접 부름. 메타 입구(port)에는 자리만 둠 | 현황판 「권장안 확정」 2026-11-10 17:43 | 원장 MV-META 행, `S/MV.UI/Meta/` |
| 2026-11-10 | D-META3-2 테스트 패널 「시간 더하기」를 저장할지(개발 빌드 전용) → 저장하지 않음(앱을 다시 켜면 사라짐) | 현황판 「권장안 확정」 2026-11-10 17:42 | 원장 MV-META 행 |
| 2026-11-10 | D-META3-1 테스트 패널 「모두 열기」가 무엇을 바꾸나(개발 빌드 전용) → Area·런 수를 올려서 열림 조건을 채움 | 현황판 「권장안 확정」 2026-11-10 17:42 | 원장 MV-META 행, `S/MV.Meta/MvMetaRequest.cs` |
| 2026-11-10 | D-META-04 런이 끝나기 전 쌓아 두는 미정산 보상에 무엇을 담나 → 재화와 608(목숨 조각 적립 대기)만 | 현황판 「권장안 확정」 2026-11-10 17:42 | 보관 원장 MV-META, `S/MV.Meta/MvRunOutcome.cs` |
| 2026-11-10 | D-META-03 성장 구매를 Framework 보상 처리로 할지 → Framework `LocalRewardService.Commit` 대신 `ResourceService.Get` + `ApplyAndSave` 한 번(`// Framework 대체:` 표시) | 현황판 「권장안 확정」 2026-11-10 17:42 | 보관 원장 MV-META, `S/MV.Meta/MvMeta.cs` |
| 2026-11-10 | D-META-02 런 정산 때 통과·구역 끝·목숨 조각을 누가 판정하나 → 런이 보낸 값을 그대로 믿지 않고 Meta가 다시 판정 | 현황판 「권장안 확정」 2026-11-10 17:41 | 보관 원장 MV-META, `S/MV.Meta/MvMetaRules.cs` |
| 2026-11-10 | D-META-01 부활할 때 「마지막으로 정산한 런 번호」를 지울지 → 유지(`lastSettledRunSeq`) — 부활 뒤에도 같은 런 중복 정산을 막음 | 현황판 「권장안 확정」 2026-11-10 17:41 | 보관 원장 「칸별 상태」 MV-META, `S/MV.Meta/` |
| 2026-11-10 | D-RULE-05 확률형·명중 횟수형 동행의 난수와 횟수 규칙 → Chance는 명중마다 `CompanionRng`, Hits는 `round(Every)`번째 명중마다 | 현황판 「권장안 확정」 2026-11-10 17:40 | 04 §4.20, 보관 원장 MV-RUN-RULE 행 |
| 2026-11-10 | D-RULE-04 배속(ts > 1)일 때 규칙 훅을 몇 번 부를지 → updRun마다(배속 수만큼) `Tick`·`CheckEnd` | 현황판 「권장안 확정」 2026-11-10 17:40 | 04 §4.20.7 |
| 2026-11-10 | D-RULE-03 동행이 쏘지 않을 대상(우물·게이트)을 고르는 기준 → 105 `chargeGateId` ≠ 0인 오브젝트를 뺌(종류 이름으로 고르지 않음) | 현황판 「권장안 확정」 2026-11-10 17:40 | 04 §4.20.7 |
| 2026-11-10 | D-RULE-02 대상이 없는 동안 「초 단위」 동행이 기다린 만큼 몰아 쏘는지 → 몰아 쏘지 않음. 타이머 `c.t`를 0에 두고 대상이 생기면 1발 | 현황판 「권장안 확정」 2026-11-10 17:39 | 04 §4.20.7 |

## 총괄 인계 (2026-11-10, 3번째 총괄 대화 마감)

- 마감 사유: 압축 2회. 인계 뒤 같은 대화에서 사용자 직접 지시로 패키지 연결·MV-VIEW 3차 배정까지 했다. dev `14db621` 전체 확인 `7b472de116eb` PASS(compile, EditMode 478/478, PlayMode 7/7). 돌고 있는 담당·잡 없음.
- 이 대화 병합: G3-INT(G3 끝), RUN-VIEWAPI, UI-HUD 1차, FX 1차, TOOL-SND 2차, VIEW 2차, PLAY-INT(플레이 가능 빌드, D-91), DATA 3차, UI-META 1차, TABLE-SWAP, META 3차, UI-LOBBY 1차, PLAY-INT 2차, G7 asmdef 준비·UI-CONTENT 1차. Addressables 그룹 Local_UI·Remote_UI·Remote_Effect·Remote_Audio_Sfx·Remote_Audio_Music·Remote_World·Dev_Debug는 총괄이 손으로 썼고 전체 확인을 통과했다.
- 칸: a~e·g 비어 있음(detached, 깨끗). f는 `ai/MV-PLAT-IAP-stub` 체크아웃(깨끗, 병합 안 함 — D-88로 취소). `ai/MV-PLAT-ANALYTICS-stub`도 병합 안 함.
- 2026-11-11 중복 브랜치 제외: `claude/project-setup-52bba1`의 `cd9e928b`(D-PI2-1 확정 행·계약 MV-FAKEAD-MERGE)는 병합하지 않는다. D-PI2-1은 dev 「확정 결정」에 이미 있고, 코드 통합은 별도 계약 없이 「이번 회차 범위」 보류의 로비 재계약에서 처리한다(사용자 「cd9e928b는 병합하지 마. 별도 MV-FAKEAD-MERGE 계약도 추가하지 말고, 기존 로비 재계약에서 처리하도록 유지해」).
- 다음 순서:
  1. G7 메타 시스템: MV-META-RESEARCH(440, 1차 메타 능력치의 유일한 출처) → QUEST(410·413·420) 계약서 있음. 이어서 ATTENDANCE(485), SHOP(700·701·702, 결제 스텁) 계약서를 같은 형식으로 쓴다. 입장권 API(UI-CONTENT 요청)는 MV-META 계약에 넣는다.
  2. G7 모드(`IModeRule`): alleyWar·rapidRush·churuRush(08 §8.4.5~8.4.7, 402·401). 시작은 `ContentMenuData.StartModeHandler`에 연결.
  3. 패키지: 사용자 허용으로 17개 + purchasing 연결 완료(`d0491cb`). 남은 3개는 Firebase 13.17.0·AdMob 10.7.0 SDK 내려받기 사용자 확인 뒤.
  3-1. MV-VIEW 3차(칸 d) 보고를 받아 병합(그룹 끝이 아니어도 `MvGameLoop`·월드 프리팹을 건드리므로 dev 전체 확인 후).
  3-2. 사용자 「dev에 넣기」로 TMP Essential Resources(82파일)와 그림 `.meta` 3,149개 Android 덮어쓰기 블록을 dev `8bf1ab3`에 Commit, 전체 확인 `4f7cc6b6d7c6` PASS(EditMode 489, PlayMode 7). D-PI2-2의 TMP Settings 없음은 해결, 한글 글꼴은 남음.
  4. TMP Essential Resources·한글 글꼴(D-PI2-2), MV-UI-FX2, MV-TOOL-ART-5-1(배치 모드 `MvArtSetup.RequestEnsure` 쓰기 — 계약서만 있음, 그 영역은 mv-process-improve 세션과 겹치는지 먼저 본다).
  5. G8: MV-QA-HARNESS Unity 쪽, MV-QA-ACCEPT(D-87).
- 기대 시험 목록 `C:/Project/MoonVillage.wt/_notes/expected/dev-full.json`은 새 시험이 들어오면 갱신한다(담당 근거 `EditMode.xml`의 fullname을 더함). Addressables 항목 추가는 그룹 파일 `  m_ReadOnly: 0
  m_Settings:` 앞에 넣는다.
