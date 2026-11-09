# TaskBoard

작업 계약 원장(기획서 11 §11.5). 계약을 시작할 때 계약 ID·담당·작업 브랜치·기준 SHA·쓰기 범위를, 끝날 때 결과 SHA·검증(PASS/FAIL/NOT_RUN)·남은 결정 ID를 적는다.

| 계약 | 상태 | 담당 | 작업 브랜치 | 기준 SHA | 결과 SHA | 검증 | 남은 것 |
|---|---|---|---|---|---|---|---|
| MV-00 프로젝트 생성·기반 연결 | 완료 | 총괄(Claude Code 대화) | `ai/MV-00-project-framework`, `ai/MV-00-boot-skeleton` | `c8c7c3a` | 아래 병합 Commit | compile PASS(잡 `8d04cd506014`, `7f1f90c67dd7`), PlayMode `BootFlowTests` 1/1 PASS(잡 `7f1f90c67dd7`, 후보 `04e675c`), EditMode NOT_RUN(시험 없음), 기기 실행 NOT_RUN | 아래 「MV-00 남김」 |

| MV-DATA 표·검증기·로더 | 1차 병합, 2차 EditMode 194/195(인계 절) | 1번째(앱 세션) 멈춤 → 2번째 닫힘 → 3번째 닫힘(부분, 입력 한도) → 4번째(2번째 총괄의 하위 에이전트, 기준 `6cc292d` = `5c7657c`+dev 병합, 티켓 10 추가) | 1차 `ai/MV-DATA-tables-loader`(병합 뒤 지움), 2차 `ai/MV-DATA-content-tables` @ `.wt/a`, `moonvillage-a` | 1차 `b210aab`, 2차 dev `05b42af` | 1차 `b7c2db5` → dev `05b42af`. 2차 HEAD `5c7657c`(트리 깨끗) | 1차: compile `3d1988cf6179` PASS, EditMode `0263185fe518` 46/46 PASS, PlayMode `c172e9cb5edf` PASS. 2차: `cfd587b` compile `0e02dbd2b992` PASS, `5c7657c` compile `f660a8e6efd9` PASS, EditMode 116개 NOT_RUN(기대 목록 scratchpad `mvdata3-editmode.json`). 티켓 06·07 start, 08·09 미완 | 다음 담당: EditMode 116 → 실패 수정 → 티켓 done → 총괄 dev 병합. 추가 요청(ART-ATLAS 검사기): 07 605 iconAtlas·09 atlas 열(9001001~4, 9002001~3, 9003001~2) → Atlas_UI_Icon, `MvTableAuthoring.Validate`에 `MvResourceColumnChecker` 합치기, 폭풍 흔들림 상수(RUN-SIM). ConsumeType 생성 「적용」은 `ProjectSettings/` 쓰기라 총괄 몫인데 고정본 Framework 0.1.0-pre.7에서 Collision·GeneratedUnmanagedConflict로 막혀 손 enum 유지(Framework 요청 필요). 결정: 802401001 행 보류, 480 statKey 열, 413 `step` 뜻, 701 2차 상품 active 0. D-73·74·78·79 권장안 적용. 기획서 07·08 같은 브랜치 동기화함. 자료 빈칸(icon·banner·SKU 등)은 보고 원문 참조 |
| MV-SAVE 저장·이관 | 완료(dev 병합) | 하위 에이전트 닫힘 | `ai/MV-SAVE-save-migration`(G1 통합 브랜치로 병합, 지움) | `43637e7` | `31b51a3` → dev `bf2921c`(MV-G1-INT와 함께) | compile `291ea15aa1bd` PASS, EditMode `9a7bd185e321` 18/18 PASS, PlayMode NOT_RUN(계약 없음) | D-68 대기: 고정본 save는 평문 불가 → 개발은 공개 고정 키, 출시는 키 공급자 교체 지점(지금 출시 빌드는 저장 못 엶) |
| MV-G1-INT 표+저장 Boot 연결 | 완료(dev 병합) | 하위 에이전트 닫힘 | `ai/MV-G1-INT-boot-save`(병합 뒤 지움) | dev `05b42af` + MV-SAVE `e9d9927` + 총괄 asmdef `ed5083c` | `5a0aa09` → dev `bf2921c` | compile `3121c21cc91d` PASS, EditMode `51d33fefac68` 71/71 PASS, PlayMode `3fbf1b89cb52` BootFlow 1/1 PASS. NOT_RUN: 플레이어 빌드 저장·표(TableBuilder Resources) | Boot 4→6→5 순서(10 §10.7·09 동기화). `mvDaily.modePlays`는 `400_content` 표(MV-DATA 2차) 뒤. G3 통합 때 Boot에 `options.IsComponentImplemented = MV.Run.RunComponents.IsImplemented` 연결(MV-RUN-SIM 요청). D-68 |
| MV-RUN-SIM 런 시뮬레이션 | 완료(dev 병합, G2) | 1·2번째 담당 닫힘 | `ai/MV-RUN-SIM-core`(병합 뒤 지움) | dev `05b42af` + 총괄 asmdef `3155544`·`4576f19` | 담당 `30473b8` + 총괄 GEN 최신 병합 `c0cfe6e` → dev `23cec8a` | 잡 `5c09fa8d07a0` compile + EditMode 34/34 PASS(SIM 23 + GEN 11). 덤프 재생 대조 BASE 3560·RUN-01 3418·RUN-02 5830·RUN-03 4083 프레임 전부 일치. PlayMode NOT_RUN(계약 없음). 유성·폭풍 카드는 덤프에 없어 미검증 | G3 통합 때 Boot에 `options.IsComponentImplemented = MV.Run.RunComponents.IsImplemented`. 04 §4.20 `IRunRule`·`MainRunRule`·동행 사격 본체는 코드 없음 → G7(MV-CONTENT) 전에 후속 계약. 결정 후보: 막힘 종료 사유(정본 `blocked.k` 이름, Unity는 `FinishReason.Wall`만). MV-DATA 요청: 폭풍 착탄 흔들림 상수(±0.3/±2) 표로 |
| MV-RUN-GEN 레벨 생성 | 완료(dev 병합 `23cec8a`, G2) | 하위 에이전트 닫힘 | `ai/MV-RUN-GEN-level`(병합 뒤 지움) | `1bd449d`(MV-RUN-SIM 공용 상태) | `cf76819`(코드 `1a4158b` + 기획서 04 동기화) | compile `1d5d2bd45a6c` PASS(`a680690`), EditMode `67b49640b85e` Gen 11/11 PASS(`1a4158b`). GEN 덤프 900/900×3, FIELD hash 4619 비트 일치×3, 칸 90/90·Baked 90/90·표만 45/45. NOT_RUN: IL2CPP ARM64 실기 FIELD(D-27) | 기반이 RUN-SIM 미완 상태라 RUN-SIM 완료 뒤 같이 병합. Hash는 `JsMath.Sin`(04 §4.14.3 동기화, 총괄 결정). 병합 뒤 정리: gaugeNeed 식 SIM `RunFormulas.GaugeNeed`와 합치기, `RunState.Tpl`=310 id. Unity Mono `double.Parse` 17자리 1ulp 오차 → 덤프 대조 시험은 직접 파서 |
| MV-RUN-RULE 런 확장 지점(04 §4.20) | 완료(dev 병합 `cfe1f46`) | 하위 에이전트 닫힘 | `ai/MV-RUN-RULE-hooks`(병합 뒤 지움) | dev `1ad0b23` | `340f812`(골든 틀 `c2a69fa`) | 잡 `acff14411ab6` compile + EditMode 52/52 PASS(기존 34 + 새 18: 본편 동일 Area1~5×Lv1·8·15 SHA 골든 일치, 모디파이어 7+1, 동행 3, 훅). 골든 기록 잡 `43876e9a0589`. NOT_RUN: §4.20.6 카드 뽑기 직접 대조(RunRng 전체 열 동일로 대신) | 402 표 연결은 MV-DATA 2차 뒤. G7 `IModeRule`·모드. Meta가 능력치·동행을 넘길 때 `RunSim.Create(RunContext, IRunRule, rng, builder)`로 연결. Gen 최소 변경: `RunLevelBuilder.NewState` 분리·`IRunStateFactory`. 결정 후보 D-RULE-01~05(04 §4.20.7 임시안: 동행 우물·게이트 안 채움·즉시 명중·useMetaStats·FireRate 안 곱함, Seconds 대기 시 c.t 0 고정, 대상 제외 105 `chargeGateId`≠0, 배속 시 updRun마다 Tick, Chance 매 명중 CompanionRng·Hits `round(Every)`) |
| MV-META 메타 규칙 | 완료(dev 병합 `d120b2e`) | 2번째 담당 닫힘 | `ai/MV-META-rules`(병합 뒤 지움) | dev `d8f2acf` + 총괄 asmdef `82cf886` | `bfa871c` | 잡 `a96ec0b153fc` compile + EditMode 25/25 PASS(비용 P=0~10 로비3·Area5·부활4, 정산 wall·gate·pass·zoneEnd·×3·중복 runSeq, 부활·광고 한도 UTC, Meta·Save 밖 `ApplyAndSave`·`.Commit(` 0). 기대 목록 `_notes/expected/meta-editmode.json` | G3 통합 Boot: `MvBootSave.Open` 성공 뒤 `MvMeta.Current = new MvMeta(() => MvSave.Current, () => MvBootSave.Resources, MvTables.Current);`. `modePlays` 한도는 400_content(MV-DATA 2차) 뒤 교체 지점. D-META-01~04 권장안 적용. Unity 경고: `Assets/Game/Scripts/MV.meta` 대응 자산 없음(정리 필요) |
| MV-TOOL-ART 그림 추출 | 1차 병합, 3차 ① 분할 끝·EditMode 재시험 대기(인계 절) | 1·2·3번째 담당 닫힘 → 4번째(2번째 총괄의 하위 에이전트, 기준 `4763430` = `2e1178f`+dev 병합, ①만, ②는 MV-DATA, 티켓 10) | 1차 `ai/MV-TOOL-ART-extract`(병합 뒤 지움), 3차 `ai/MV-TOOL-ART-remaining` @ `.wt/c` | `43637e7`, 3차 dev `688ecaf` | 1차 `e7b58c8` → dev `688ecaf`. 3차 담당 `dea2c42` + 총괄 dev 병합 `80b91a9` + Unity 임포트 결과 `2e1178f` | 1차: compile `630d4553c1d9` PASS, V1·V2(ΔE ≤ 3)·V4 PASS, V3 NOT_RUN. 3차(`dea2c42`): 정식 1699→2141장(+442), V1·V1'·V2·V4 PASS, V3 NOT_RUN, compile `a2ac703deba6` PASS. dev 병합 뒤(`2e1178f`): 잡 `6cbf41b8790a` compile PASS, EditMode ATLAS 18/20 — FAIL ① `EveryAtlas_FitsOnePageOfMaxSize`: Atlas_World_Lobby 101장 하한 118 % → `_2` 분할 필요(03 §3.8.1) ② `AllTableResourceColumns_ResolveOrAreReported`: 09 9002002 `UI_Icon_Brick` @Atlas_UI_Hud 없음(실제 Atlas_UI_Icon) | 다음: ①은 로비 아틀라스 분할(ART-ATLAS 규칙·101 atlasPattern이면 MV-DATA) ②는 MV-DATA 요청 목록에 추가. 남은 그림: `Tex_Lobby_Room`, 집 부품, `World_Prop_Pole`, 담장 장식, 빨래 21·전구줄 8, `Fx_Hiss`·`Fx_Paw`·`Fx_FlashEllipse`·`Fx_Stripe`, UI 스킨 전체(dom 방식 준비됨), `World_Label_*`, `Tex_UI_Vignette`. 확인 필요 추정: 관문 기둥 원근·flipX, 금빛 기둥, 계단 피벗, 항구 파랑 생선 상자 조합 |
| MV-QA-HARNESS 캡처 도구(프로토 쪽) | 프로토 쪽 병합 | 하위 에이전트 닫힘 | `ai/MV-QA-HARNESS-proto`(병합 뒤 지움) | `43637e7` | `b66c41f` → dev `63c76dc` | 37/38장면(U-14 protoNA) 118장 2회 픽셀 해시 동일, GEN·FIELD·SIM 덤프 재생성 SHA 동일. 사람 확인 NOT_RUN: U-04·U-12·U-13 연출 포착 | Unity 쪽은 MV-RUN-SIM·MV-VIEW 뒤 별도. `mask[]` 실측은 MV-QA-ACCEPT. 기획서 12 §12.1 동기화. 결정 D-41+D-30·D-71·D-27 |
| MV-TOOL-SND 소리 굽기 | 완료(dev 병합) | 하위 에이전트 닫힘 | `ai/MV-TOOL-SND-bake`(병합 뒤 지움) | `e4e492d` | `cb96a78` → dev `25ae2cb` | 형식 32/32, 재실행 해시 32/32, V6 실시간 녹음 대조 32/32 PASS(상관 최저 0.9954), compile `78fc269bf094` PASS. 청취 확인 NOT_RUN | 03 §3.9.1과 다르게 한 4가지는 총괄 결정으로 03 동기화(앞 무음 1.0014 s, BGM 두 번째 루프, 뒤 무음 정리 끔, GainNode 사슬). D-55 잔재 5종 정의만, D-56 WAV→Vorbis 임포트, D-57 BGM 2파일. `package.json` snd 스크립트 총괄이 추가 |
| MV-ART-ATLAS 아틀라스·임포트 | 부분 완료(dev 병합 `12f354d`) | 하위 에이전트 닫힘 | `ai/MV-ART-ATLAS-import`(병합 뒤 지움) | dev `688ecaf` + 총괄 asmdef `474ad46` | 담당 `64ee754` + 총괄 Sprite Atlas V2 켜기 `7867fb3` | `64ee754`: 잡 `3a020e6a3455` compile + EditMode 20/20 PASS, 생성 산출물 재실행 변화 없음. `7867fb3`: 잡 `5bc1deca8c67` compile + EditMode 20/20 PASS. NOT_RUN: 아틀라스 실제 패킹 크기·Memory Profiler(§3.8.12), ΔE 압축 품질(§3.11.2) | 아틀라스 35개, 런 텍스처 31.9 MB(목표 48). 검사기 오류 11건 = MV-DATA 요청(07 605 iconAtlas→Atlas_UI_Icon, 09 atlas 열 9001001~4·9002001·9002003·9003001·9003002 → Atlas_UI_Icon, 113 portrait 대소문자(2차에서 고침), `MvTableAuthoring.Validate`에 `MvResourceColumnChecker` 합치기). 미추출 20건은 TOOL-ART 3차. 위험: Friend_T1~T6 실제 패킹이 2페이지면 `_2` 분할+101 atlasPattern(MV-DATA). Cat 아틀라스 2048². `Tex_God_*`는 `Art/Source/Texture`에 둔 채 주소만(§3.4.2와 다름, 이동은 TOOL-ART/총괄 결정). Fx 패딩 8(§3.8.1 규칙 우선) |
| MV-VIEW 월드 렌더 | 착수 전 마감(G4, 인계 절) | 2번째 총괄의 하위 에이전트 | `ai/MV-VIEW-world` @ `.wt/d`, `moonvillage-d` | dev `3ad7e78` + 총괄 asmdef `b3ac078`(`MV.View.Tests`, `MV.View` 참조 TMP·Addressables·ResourceManager·Tables) | | | 쓰기 `S/MV.View/`, `S/MV.View.Tests/`, `A/Art/Shaders/`, `A/Art/Materials/`, `A/Addressables/Remote/World/`, 그룹 자산은 World 프리팹 항목만(별도 Commit). 씬 안 고침 → Boot 호출은 G4 통합. 캡처 대조 1차 NOT_RUN(QA-HARNESS Unity 쪽 뒤). 티켓 01~06 |

## 담당표 (2026-11-09)

| 칸 | 작업 폴더 | 실행기 id | 지금 계약 | 닫는 조건 |
|---|---|---|---|---|
| a | `C:/Project/MoonVillage.wt/a` | `moonvillage-a` | MV-DATA 2차 검증(담당 닫힘, 브랜치 `ai/MV-DATA-content-tables` `72442b4` 유지) | 완료·체크포인트·입력 약 15만 토큰 |
| b | `C:/Project/MoonVillage.wt/b` | `moonvillage-b` | MV-META(담당 닫힘, 브랜치 `ai/MV-META-rules` `8a3a6bc` 유지) | 완료·체크포인트·입력 약 15만 토큰 |
| c | `C:/Project/MoonVillage.wt/c` | `moonvillage-c` | MV-TOOL-ART 3차 FAIL 수정(담당 닫힘, 브랜치 `ai/MV-TOOL-ART-remaining` `99c7614` 유지) | 같음 |
| d | `C:/Project/MoonVillage.wt/d` | `moonvillage-d` | MV-VIEW(담당 닫힘, 브랜치 `ai/MV-VIEW-world` `b3ac078` 유지, 착수 전 마감) | 완료·체크포인트·입력 약 15만 토큰 |
| e | `C:/Project/MoonVillage.wt/e` | `moonvillage-e` | 비어 있음(detached, MV-TOOL-SND 닫힘) | — |
| f | `C:/Project/MoonVillage.wt/f` | `moonvillage-f` | 비어 있음(detached) | — |
| g | `C:/Project/MoonVillage.wt/g` | `moonvillage-g` | 비어 있음(detached) | — |

- 테스트 폴더 `C:/Project/MoonVillage`는 항상 `dev`다. 작업 폴더는 칸을 재사용하고 브랜치만 바꾼다(Unity Library 재생성 절약).
- 실행기 하위 호스트 위임: `moonvillage --hosts`, compile·EditMode·PlayMode, 근거 「작업 폴더를 실행기에 등록해」, 만료 2026-11-19.
- 담당 공통 규칙: `C:/Project/MoonVillage.wt/_notes/CONTRACT_COMMON.md`(저장소 밖, 이 PC).
- 시험 어셈블리는 계약마다 `S/MV.<모듈>.Tests/`(기획서 11의 `MV.Run.Tests` 방식)로 둬서 브랜치끼리 asmdef가 겹치지 않게 했다.

## 3번째 총괄 진행 (2026-11-10 시작)

- 2번째 총괄 인계를 이어 받음(dev `28e66ea`). 계약서 `C:/Project/MoonVillage.wt/_notes/contracts/`(저장소 밖).
- 배정(하위 에이전트, 계약마다 새 담당, 입력 약 15만 토큰 또는 완료에 닫음):

| 칸 | 계약 | 담당 | 브랜치·기준 SHA | 쓰기 범위 |
|---|---|---|---|---|
| a | MV-DATA 2차 마감 | 5번째(`MV-DATA-2-5.md`) | `ai/MV-DATA-content-tables` `72442b4` | `S/MV.Data/`, `A/GameData/Table/TSV/`, `S/MV.Editor/Tables/`, 이번만 리소스 열 검사기·시험(`MV.Editor/Import`·`MV.Editor.Tests/Import`), 기획서 07 |
| b | MV-META | 2번째(`MV-META-2.md`) | `ai/MV-META-rules` `8a3a6bc` | `S/MV.Meta/`, `S/MV.Meta.Tests/`, `QA/Dumps/meta/` |
| c | MV-TOOL-ART 3차 검증 | 5번째(`MV-TOOL-ART-3-5.md`) | `ai/MV-TOOL-ART-remaining` `99c7614` | `Tools/MvExtract/`, `A/Art/Source/`, 아틀라스, `MvArtRules`, 그룹 로비 항목만 |
| d | MV-VIEW 1차 | 2번째(`MV-VIEW-2.md`) | `ai/MV-VIEW-world` `b28b038`(총괄 정렬 레이어 10개) | `S/MV.View/`, `S/MV.View.Tests/`, `A/Art/Shaders/`, `A/Art/Materials/`, `A/Addressables/Remote/World/`(그룹 등록 없음) |
| e | MV-RUN-RULE 런 확장 지점(MV-RUN-SIM 후속, 04 §4.20) | 1번째(`MV-RUN-RULE-1.md`) | `ai/MV-RUN-RULE-hooks` dev `1ad0b23` | `S/MV.Run/`(Gen 제외), `S/MV.Run.Tests/Rule/`, 기획서 04 §4.20 |
| c | MV-TOOL-ART 4차 남은 그림 추출 | 1번째(`MV-TOOL-ART-4-1.md`) | `ai/MV-TOOL-ART-ui-lobby` (3차 `99c7614` 위) | `Tools/MvExtract/`, `A/Art/Source/`, `A/Art/Temp/`, 새 아틀라스 파일, `MvArtRules` |

| b | MV-PLAT-NET 통신 로컬 스텁 | 1번째(`MV-PLAT-NET-1.md`) | `ai/MV-PLAT-NET-stub` dev `d120b2e`+총괄 asmdef `555a996` | `S/MV.Platform/Net/`, `S/MV.Platform.Tests/Net/` |
| f | MV-PLAT-ADS 광고 스텁 | 1번째(`MV-PLAT-ADS-1.md`) | `ai/MV-PLAT-ADS-stub` 같은 기준 | `S/MV.Platform/Ads/`, `S/MV.Platform.Tests/Ads/` |
| g | MV-PLAT-LOGIN 게스트 로그인 스텁 | 1번째(`MV-PLAT-LOGIN-1.md`) | `ai/MV-PLAT-LOGIN-stub` 같은 기준 | `S/MV.Platform/Login/`, `S/MV.Platform.Tests/Login/` |
- MV-TOOL-ART 3차 검증 끝(담당 닫힘): `99c7614` 새 Commit 없음, EditMode 잡 `a6c9597f2d67` 19/20 PASS(`EveryAtlas_FitsOnePageOfMaxSize` PASS, 로비 하한 71.6 %·_2 46.5 %, 사각 추정 107 %는 실측 위험). FAIL `AllTableResourceColumns_ResolveOrAreReported` = 09[9002002] `UI_Icon_Brick` atlas(MV-DATA, 칸 a에 전달). dev 병합은 MV-DATA 2차 뒤 재시험하고 한다.

## 총괄 인계 (2026-11-09, 1번째 총괄 대화 마감 — 압축 2회)

- dev `086c1f9` 기준. dev에 들어온 계약: MV-00, MV-DATA 1차, MV-SAVE, MV-G1-INT, MV-TOOL-ART 1차, MV-QA-HARNESS(프로토), MV-TOOL-SND, MV-RUN-SIM, MV-RUN-GEN, MV-ART-ATLAS(부분). G1은 MV-DATA 2차, G2는 끝.
- 열린 하위 에이전트 없음(모두 보고 후 닫힘). 실행기에 이 프로젝트 잡 없음. 사용자 테스트 폴더 `C:/Project/MoonVillage`는 `dev`, 미커밋 없음.
- 브랜치가 남은 칸: a `ai/MV-DATA-content-tables`(`5c7657c`, compile PASS, EditMode 116 NOT_RUN — 기대 목록 scratchpad `mvdata3-editmode.json`, scratchpad는 1번째 총괄 대화 것이라 새 총괄은 목록을 다시 만든다), c `ai/MV-TOOL-ART-remaining`(`2e1178f`, FAIL 2건 위 행). b·d·e·f·g는 detached, 재사용 가능.
- 다음 순서(권장):
  1. MV-DATA 2차 검증 담당(칸 a): EditMode → 실패 수정 → 위 MV-DATA 행의 추가 요청 반영 → dev 병합(G1 끝).
  2. MV-TOOL-ART 3차 FAIL 2건 담당(칸 c): 로비 아틀라스 분할(①), ②는 MV-DATA 요청 반영 뒤 재시험 → dev 병합. 남은 그림은 다음 회차.
  3. MV-META(칸 b): 총괄이 `MV.Meta.Tests` asmdef를 먼저 만든다.
  4. G3 통합: Boot에 `options.IsComponentImplemented = MV.Run.RunComponents.IsImplemented`. 이후 기획서 11 순서대로 G3(MV-VIEW 등) → G4 …
  5. MV-QA-HARNESS Unity 쪽은 MV-VIEW 뒤.
- 남은 결정(총괄 권장안으로 진행 중, 교체 가능): D-27(실기 FIELD), D-68(출시 저장 키), 802401001 행, 480 statKey, 413 step, 701 2차 active 0, 막힘 종료 사유, Friend_T 아틀라스 2페이지 여부(Sprite Atlas V2 켠 뒤 실측), `Tex_God_*` 위치, ConsumeType 생성(고정본 Framework 0.1.0-pre.7 Collision — Framework 요청).
- 실행기: `RESOURCE_BUSY: OTHER_OWNER_DEVICE_WORK`는 다시 낸다. Unity가 실행 중 만든 `.meta`·아틀라스·그룹 변경은 `PROJECT_EXECUTION_CANDIDATE_CHANGED`/FAIL로 나오니 범위 확인 뒤 Commit하고 다시 낸다. 덤프 대조 시험은 Mono `double.Parse` 1ulp 오차 때문에 GEN의 직접 파서(`MiniJson`)를 쓴다.
- 승인 경계: 하위 호스트 위임 만료 2026-11-19. D-50 승인됨. 그 밖 승인은 새 총괄이 현행 문서로 다시 확인한다.
## 총괄 인계 (2026-11-09, 2번째 총괄 대화 마감 — 사용자 요청으로 중지)

- 위 「1번째 총괄 대화」 인계를 대신한다. dev 이 Commit 기준. 열린 하위 에이전트 없음(4명 모두 마감 보고 후 닫힘). 실행기에 이 대화가 낸 잡 없음. 테스트 폴더 `dev` 미커밋 없음. 칸 a·b·c·d 작업 트리 깨끗.
- 도구: 이전 scratchpad 도구를 `C:/Project/MoonVillage.wt/_notes/tools/`(make_metas.py, Framework 고정본 `fw/`)로 옮기고 CONTRACT_COMMON 경로를 바꿨다. 기대 시험 목록은 `_notes/expected/`, 만들다 만 파일은 `_notes/wip/`.
- 방침: 압축 2회 또는 그룹 끝이면 이 원장에 인계를 적고 새 총괄 세션으로 넘긴다. 담당은 계약마다 새 하위 에이전트, 입력 약 15만 토큰에서 마감.

### 칸별 상태와 다음 순서
1. **a MV-DATA 2차** `ai/MV-DATA-content-tables` `72442b4`(dev `d8f2acf` 병합 포함). 잡 `ee0b04176ff9`: compile PASS, EditMode 194/195 — FAIL `MV.Editor.Tests.MvResourceColumnTests.AllTableResourceColumns_ResolveOrAreReported`(2차 4xx 표 그림 없음 299건: 430_pet sprite 240·icon 16, 113 `Tex_God_*` 10, 450 icon 10·portrait 10, 440 icon 5, 400 icon 4·bg 4). 반영함: 07 605·09 atlas 열 → `Atlas_UI_Icon`, `MvTableAuthoring.Validate`에 `MvResourceColumnChecker` 합침(Error=오류), `300_runConfig` `STORM_JIT_TARGET`/`STORM_JIT_FREE`(리플레이 4건 PASS), 07 동기화. 기대 목록 `_notes/expected/mvdata4-editmode.json`(195). 티켓 06·08 done, 07·09·10 진행.
   - 다음: (권장안 D-MVDATA-RES) 「그림 자체 없음」은 경고로 내리고 시험 요청 목록 갱신(시험은 `S/MV.Editor.Tests/Import/` — ART-ATLAS 몫이라 총괄이 같은 담당에 범위 허락), 430_pet sprite 아틀라스(`@theme` → 펫 전용?) 03 §3.4 대조 → 재시험 PASS → dev 병합(G1 끝).
2. **c MV-TOOL-ART 3차** `ai/MV-TOOL-ART-remaining` `99c7614`. `Atlas_World_Lobby` → `_2` 분할(시설 Tower 25·Rack 12장, GUID 유지, `gen_jobs.mjs` LOB2), `MvArtRules.IsLobbyAtlas`, 그룹 `Remote_WorldAtlas`에 `_2` 항목만(`99c7614`, 총괄 허락 범위). compile `586babb79b4c` PASS, EditMode NOT_RUN. 사각 추정 Lobby ≈107 %라 실측 필요.
   - 다음: EditMode(`_notes/expected/atlas-editmode.json` 20) → ① 포함 19 PASS 목표, ②는 MV-DATA 병합 뒤 dev 재병합해 재시험 → dev 병합. 참고: Tower·Rack `facSprite` 채울 때 `10_lobbyUpgrade`에 `atlas` 열 필요(MV-DATA).
3. **b MV-META** `ai/MV-META-rules` `8a3a6bc`(총괄 asmdef `82cf886` 위). `S/MV.Meta/` 소스 6개(+meta, `MV.Meta.meta` 새로) Commit, compile·EditMode NOT_RUN, 시험 없음. 티켓 01 진행, 02~05 미착수(코드 일부 있음).
   - 다음: compile(의심: `jjevol.ConsumeData`↔`MV.Data.ConsumeData` 충돌, 별칭 `MvConsume`) → `S/MV.Meta.Tests/` EditMode(07 §7.8.6 비용 P=0~10, 정산 wall·gate·pass·zoneEnd·×3·중복 runSeq, 부활, 광고 한도·UTC 날짜 넘김, Meta·Save 밖 `ApplyAndSave`·`Commit` 호출 0) → dev 병합. Boot 연결 한 줄(G3 통합): `MvMeta.Current = new MvMeta(() => MvSave.Current, () => MvBootSave.Resources, MvTables.Current)`. `modePlays` 한도는 400_content 들어온 뒤.
   - 결정 후보(권장안 적용): D-META-01 부활 때 `lastSettledRunSeq` 유지, D-META-02 정산 pass·zoneEnd·조각은 Meta가 재판정, D-META-03 성장 구매는 `LocalRewardService.Commit` 대신 `ResourceService.Get`+`ApplyAndSave` 한 번(`// Framework 대체:`), D-META-04 `PendingSettle`은 재화·608만.
4. **d MV-VIEW** `ai/MV-VIEW-world` `b3ac078`(총괄 asmdef만). 담당 Commit 없음, 티켓 01~06 미착수. 초안 3개(`MvColor`·`ViewConfig`·`CameraModel`, compile 전) `_notes/wip/MV-VIEW/` → 원래 `S/MV.View/Math/`.
   - 다음 담당: 초안 복귀+meta → compile → 카메라 시험(02 §2.2.3 검산 40.76°/42.18°, d 14.85/17.38, s 44.2/37.7, 안개 시작 z +24.7) → 순수 계산(DepthMath·GroundStripBuilder·PropLayout·BoxBillboardBuilder) → 실행 구성 요소·셰이더 4개. 정한 방식: 안개 알파는 CPU(DepthSorter)가 `SpriteRenderer.color`에(03 §3.8.9 MPB 금지), 전봇대는 빌보드 스프라이트, `FieldGen.Hash` 재사용.
   - 총괄 몫(통합 Writer): TagManager 정렬 레이어 10개(Ground·World·Tint·Light·Snow·Warp·WarpCat·WarpOut·SpeedLine·Vignette, 02 §2.1.6), Addressables `Remote_World` 그룹 새로 만들기, D-42 색 공간(지금 Linear) 확인.
   - 요청: MV-DATA — `view.groundFar` 92→120(02 §2.7.3), `330_climb` 출렁임 0.12 열, `304_theme` 6~15행 `roadsideProps`·`lineKind` 빈칸. MV-RUN — 옹벽 {z, low, high}(정본 `R.cliff`) 읽기 인터페이스.
5. 그 뒤: G3 통합(Boot `options.IsComponentImplemented = MV.Run.RunComponents.IsImplemented` + MvMeta 연결, EditMode 전체) → MV-SND(MV-DATA 뒤, `03_sound` 겹침) → MV-FX(VIEW API 뒤) → G5 → G6(MV-META 뒤) → G7 → G8. MV-RUN-SIM 후속(04 §4.20 `IRunRule`·`MainRunRule`·동행 사격)은 G7 전.

### 남은 결정(권장안으로 진행, 교체 가능)
D-27, D-68, 802401001 행, 480 statKey, 413 step, 701 2차 active 0, 막힘 종료 사유, Friend_T 아틀라스 2페이지, `Tex_God_*` 위치, ConsumeType 생성(Framework 요청), D-40·D-41(VIEW 권장안), D-42 색 공간, D-MVDATA-RES, D-META-01~04, 430_pet 아틀라스.
- 미해결(사용자): 없음.

## MV-00 남김

- Managed Addressables Project Config는 만들지 않았다. 만들면 모든 Player 빌드가 BootstrapSplit 경로를 타므로(Framework `NewGame.md` 3단계) 첫 Addressable 콘텐츠가 들어오는 계약(MV-TOOL-ART·MV-UI-*)에서 만든다. 기본 `AddressableAssetSettings`만 있다.
- 10 §10.7 1단계의 앱 포함 A 로더(`ManagedPackagedRevisionLoader`)는 고정본 1.3.12-pre.10에 없다(pre.11부터). Addressables 초기화만 한다.
- 빈 asmdef 11개(`MV.Core`~`MV.Platform`, `MV.Boot`·`MV.Editor` 제외)는 스크립트가 없어 Unity가 「컴파일하지 않음」 경고를 낸다. 각 계약이 코드를 넣으면 사라진다.
- `Game` 씬은 카메라·조명·`GameInitializer`만 있다. `WorldRoot`·`GameCamera`(FOV 52)·`LobbyStage`는 MV-VIEW, 로비 화면은 MV-UI-LOBBY.
- `UIManager` 프리팹과 `ui.StartInitAsync()`(10 §10.7 8단계)는 MV-UI-*. 지금 `UIManagerCreater`는 시작하지 않는다.
- 시험 중 Framework 19개는 고정본이 없어 연결하지 않았다(`PROJECT_CONTEXT.md` 「Framework 기준선」).
- 골격은 `MoonVillage/Setup/Build Project Skeleton`(`MV.Editor.Setup.MvProjectSkeleton`)으로 만들었다. 있는 것은 건드리지 않으므로 다시 실행해도 된다.
