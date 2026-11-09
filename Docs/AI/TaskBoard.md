# TaskBoard

작업 계약 원장(기획서 11 §11.5). 계약을 시작할 때 계약 ID·담당·작업 브랜치·기준 SHA·쓰기 범위를, 끝날 때 결과 SHA·검증(PASS/FAIL/NOT_RUN)·남은 결정 ID를 적는다.

| 계약 | 상태 | 담당 | 작업 브랜치 | 기준 SHA | 결과 SHA | 검증 | 남은 것 |
|---|---|---|---|---|---|---|---|
| MV-00 프로젝트 생성·기반 연결 | 완료 | 총괄(Claude Code 대화) | `ai/MV-00-project-framework`, `ai/MV-00-boot-skeleton` | `c8c7c3a` | 아래 병합 Commit | compile PASS(잡 `8d04cd506014`, `7f1f90c67dd7`), PlayMode `BootFlowTests` 1/1 PASS(잡 `7f1f90c67dd7`, 후보 `04e675c`), EditMode NOT_RUN(시험 없음), 기기 실행 NOT_RUN | 아래 「MV-00 남김」 |

| MV-DATA 표·검증기·로더 | 1차 병합, 2차 구현 끝·검증 대기 | 1번째(앱 세션) 멈춤 → 2번째 닫힘 → 3번째 닫힘(부분, 입력 한도) → **다음 담당 필요(검증만)** | 1차 `ai/MV-DATA-tables-loader`(병합 뒤 지움), 2차 `ai/MV-DATA-content-tables` @ `.wt/a`, `moonvillage-a` | 1차 `b210aab`, 2차 dev `05b42af` | 1차 `b7c2db5` → dev `05b42af`. 2차 HEAD `5c7657c`(트리 깨끗) | 1차: compile `3d1988cf6179` PASS, EditMode `0263185fe518` 46/46 PASS, PlayMode `c172e9cb5edf` PASS. 2차: `cfd587b` compile `0e02dbd2b992` PASS, `5c7657c` compile `f660a8e6efd9` 줄 서 있음(결과 확인), EditMode 116개 NOT_RUN(기대 목록 scratchpad `mvdata3-editmode.json`). 티켓 06·07 start, 08·09 미완 | 다음 담당: f660 결과 → EditMode 116 → 실패 수정 → 티켓 done → 총괄 dev 병합. ConsumeType 생성 「적용」은 `ProjectSettings/` 쓰기라 총괄 몫인데 고정본 Framework 0.1.0-pre.7에서 Collision·GeneratedUnmanagedConflict로 막혀 손 enum 유지(Framework 요청 필요). 결정: 802401001 행 보류, 480 statKey 열, 413 `step` 뜻, 701 2차 상품 active 0. D-73·74·78·79 권장안 적용. 기획서 07·08 같은 브랜치 동기화함. 자료 빈칸(icon·banner·SKU 등)은 보고 원문 참조 |
| MV-SAVE 저장·이관 | 완료(dev 병합) | 하위 에이전트 닫힘 | `ai/MV-SAVE-save-migration`(G1 통합 브랜치로 병합, 지움) | `43637e7` | `31b51a3` → dev `bf2921c`(MV-G1-INT와 함께) | compile `291ea15aa1bd` PASS, EditMode `9a7bd185e321` 18/18 PASS, PlayMode NOT_RUN(계약 없음) | D-68 대기: 고정본 save는 평문 불가 → 개발은 공개 고정 키, 출시는 키 공급자 교체 지점(지금 출시 빌드는 저장 못 엶) |
| MV-G1-INT 표+저장 Boot 연결 | 완료(dev 병합) | 하위 에이전트 닫힘 | `ai/MV-G1-INT-boot-save`(병합 뒤 지움) | dev `05b42af` + MV-SAVE `e9d9927` + 총괄 asmdef `ed5083c` | `5a0aa09` → dev `bf2921c` | compile `3121c21cc91d` PASS, EditMode `51d33fefac68` 71/71 PASS, PlayMode `3fbf1b89cb52` BootFlow 1/1 PASS. NOT_RUN: 플레이어 빌드 저장·표(TableBuilder Resources) | Boot 4→6→5 순서(10 §10.7·09 동기화). `mvDaily.modePlays`는 `400_content` 표(MV-DATA 2차) 뒤. G3 통합 때 Boot에 `options.IsComponentImplemented = MV.Run.RunComponents.IsImplemented` 연결(MV-RUN-SIM 요청). D-68 |
| MV-RUN-SIM 런 시뮬레이션 | 진행 | 하위 에이전트 2번째(1번째 닫힘: 입력 한도, `841bcf0` — 공용 상태 `1bd449d` compile `a787f247aaec` PASS, 규칙 1차 compile 대기 `b584d62dab29`, 실행기 RESOURCE_BUSY 거절 2회) | `ai/MV-RUN-SIM-core` @ `.wt/f`, `moonvillage-f` | dev `05b42af` + 총괄 asmdef `3155544`(MV.Run.Tests) | | | 쓰기: `S/MV.Run/`, `S/MV.Run.Tests/`(Gen 제외). 공용 상태 Commit 뒤 MV-RUN-GEN 시작 |
| MV-RUN-GEN 레벨 생성 | 완료(dev 병합 대기: MV-RUN-SIM과 함께) | 하위 에이전트 닫힘 | `ai/MV-RUN-GEN-level` @ `.wt/d` | `1bd449d`(MV-RUN-SIM 공용 상태) | `cf76819`(코드 `1a4158b` + 기획서 04 동기화) | compile `1d5d2bd45a6c` PASS(`a680690`), EditMode `67b49640b85e` Gen 11/11 PASS(`1a4158b`). GEN 덤프 900/900×3, FIELD hash 4619 비트 일치×3, 칸 90/90·Baked 90/90·표만 45/45. NOT_RUN: IL2CPP ARM64 실기 FIELD(D-27) | 기반이 RUN-SIM 미완 상태라 RUN-SIM 완료 뒤 같이 병합. Hash는 `JsMath.Sin`(04 §4.14.3 동기화, 총괄 결정). 병합 뒤 정리: gaugeNeed 식 SIM `RunFormulas.GaugeNeed`와 합치기, `RunState.Tpl`=310 id. Unity Mono `double.Parse` 17자리 1ulp 오차 → 덤프 대조 시험은 직접 파서 |
| MV-META 메타 규칙 | 시작 가능 | — | (칸 b 또는 e) | | | | 선행 MV-SAVE·MV-G1-INT dev 들어옴(`bf2921c`). 총괄 asmdef: `MV.Meta.Tests` 필요 |
| MV-TOOL-ART 그림 추출 | 1차 병합, 3차 진행 | 1·2번째 닫힘 → 3번째 진행 | 1차 `ai/MV-TOOL-ART-extract`(병합 뒤 지움), 3차 `ai/MV-TOOL-ART-remaining` @ `.wt/c` | `43637e7`, 3차 dev `688ecaf` | `e7b58c8` → dev `688ecaf` | compile `630d4553c1d9` PASS, V1 결정성 PASS, V2 PASS(ΔE 최대 ≤ 3 — 총괄 결정, 03 §3.11 동기화), V4 PASS, V3 NOT_RUN | 정식 1699·임시 275. 남은 것: 표가 쓰는 미추출 14이름, 로비·이펙트·UI 스킨. MV-DATA 요청: 113 portrait 대소문자(2차 담당에 전달) |
| MV-QA-HARNESS 캡처 도구(프로토 쪽) | 프로토 쪽 병합 | 하위 에이전트 닫힘 | `ai/MV-QA-HARNESS-proto`(병합 뒤 지움) | `43637e7` | `b66c41f` → dev `63c76dc` | 37/38장면(U-14 protoNA) 118장 2회 픽셀 해시 동일, GEN·FIELD·SIM 덤프 재생성 SHA 동일. 사람 확인 NOT_RUN: U-04·U-12·U-13 연출 포착 | Unity 쪽은 MV-RUN-SIM·MV-VIEW 뒤 별도. `mask[]` 실측은 MV-QA-ACCEPT. 기획서 12 §12.1 동기화. 결정 D-41+D-30·D-71·D-27 |
| MV-TOOL-SND 소리 굽기 | 완료(dev 병합) | 하위 에이전트 닫힘 | `ai/MV-TOOL-SND-bake`(병합 뒤 지움) | `e4e492d` | `cb96a78` → dev `25ae2cb` | 형식 32/32, 재실행 해시 32/32, V6 실시간 녹음 대조 32/32 PASS(상관 최저 0.9954), compile `78fc269bf094` PASS. 청취 확인 NOT_RUN | 03 §3.9.1과 다르게 한 4가지는 총괄 결정으로 03 동기화(앞 무음 1.0014 s, BGM 두 번째 루프, 뒤 무음 정리 끔, GainNode 사슬). D-55 잔재 5종 정의만, D-56 WAV→Vorbis 임포트, D-57 BGM 2파일. `package.json` snd 스크립트 총괄이 추가 |
| MV-ART-ATLAS 아틀라스·임포트 | 부분 완료(총괄 검증 잡 대기 → dev 병합) | 하위 에이전트 닫힘 | `ai/MV-ART-ATLAS-import` @ `.wt/g` | dev `688ecaf` + 총괄 asmdef `474ad46` | 담당 `64ee754` + 총괄 Sprite Atlas V2 켜기 `7867fb3` | `64ee754`: 잡 `3a020e6a3455` compile + EditMode 20/20 PASS, 생성 산출물 재실행 변화 없음. `7867fb3`: 잡 `5bc1deca8c67` 대기. NOT_RUN: 아틀라스 실제 패킹 크기·Memory Profiler(§3.8.12), ΔE 압축 품질(§3.11.2) | 아틀라스 35개, 런 텍스처 31.9 MB(목표 48). 검사기 오류 11건 = MV-DATA 요청(07 605 iconAtlas→Atlas_UI_Icon, 09 atlas 열 9001001~4·9002001·9002003·9003001·9003002 → Atlas_UI_Icon, 113 portrait 대소문자(2차에서 고침), `MvTableAuthoring.Validate`에 `MvResourceColumnChecker` 합치기). 미추출 20건은 TOOL-ART 3차. 위험: Friend_T1~T6 실제 패킹이 2페이지면 `_2` 분할+101 atlasPattern(MV-DATA). Cat 아틀라스 2048². `Tex_God_*`는 `Art/Source/Texture`에 둔 채 주소만(§3.4.2와 다름, 이동은 TOOL-ART/총괄 결정). Fx 패딩 8(§3.8.1 규칙 우선) |

## 담당표 (2026-11-09)

| 칸 | 작업 폴더 | 실행기 id | 지금 계약 | 닫는 조건 |
|---|---|---|---|---|
| a | `C:/Project/MoonVillage.wt/a` | `moonvillage-a` | MV-DATA 2차 | 완료·체크포인트·입력 약 15만 토큰 |
| b | `C:/Project/MoonVillage.wt/b` | `moonvillage-b` | 비어 있음(detached, MV-G1-INT 닫힘) | — |
| c | `C:/Project/MoonVillage.wt/c` | `moonvillage-c` | MV-TOOL-ART 3차 | 같음 |
| d | `C:/Project/MoonVillage.wt/d` | `moonvillage-d` | MV-RUN-GEN 끝(브랜치 유지, 담당 닫힘) | — |
| e | `C:/Project/MoonVillage.wt/e` | `moonvillage-e` | 비어 있음(detached, MV-TOOL-SND 닫힘) | — |
| f | `C:/Project/MoonVillage.wt/f` | `moonvillage-f` | MV-RUN-SIM | 같음 |
| g | `C:/Project/MoonVillage.wt/g` | `moonvillage-g` | MV-ART-ATLAS 끝(담당 닫힘, 총괄 검증 중) | — |

- 테스트 폴더 `C:/Project/MoonVillage`는 항상 `dev`다. 작업 폴더는 칸을 재사용하고 브랜치만 바꾼다(Unity Library 재생성 절약).
- 실행기 하위 호스트 위임: `moonvillage --hosts`, compile·EditMode·PlayMode, 근거 「작업 폴더를 실행기에 등록해」, 만료 2026-11-19.
- 담당 공통 규칙: `C:/Project/MoonVillage.wt/_notes/CONTRACT_COMMON.md`(저장소 밖, 이 PC).
- 시험 어셈블리는 계약마다 `S/MV.<모듈>.Tests/`(기획서 11의 `MV.Run.Tests` 방식)로 둬서 브랜치끼리 asmdef가 겹치지 않게 했다.

## MV-00 남김

- Managed Addressables Project Config는 만들지 않았다. 만들면 모든 Player 빌드가 BootstrapSplit 경로를 타므로(Framework `NewGame.md` 3단계) 첫 Addressable 콘텐츠가 들어오는 계약(MV-TOOL-ART·MV-UI-*)에서 만든다. 기본 `AddressableAssetSettings`만 있다.
- 10 §10.7 1단계의 앱 포함 A 로더(`ManagedPackagedRevisionLoader`)는 고정본 1.3.12-pre.10에 없다(pre.11부터). Addressables 초기화만 한다.
- 빈 asmdef 11개(`MV.Core`~`MV.Platform`, `MV.Boot`·`MV.Editor` 제외)는 스크립트가 없어 Unity가 「컴파일하지 않음」 경고를 낸다. 각 계약이 코드를 넣으면 사라진다.
- `Game` 씬은 카메라·조명·`GameInitializer`만 있다. `WorldRoot`·`GameCamera`(FOV 52)·`LobbyStage`는 MV-VIEW, 로비 화면은 MV-UI-LOBBY.
- `UIManager` 프리팹과 `ui.StartInitAsync()`(10 §10.7 8단계)는 MV-UI-*. 지금 `UIManagerCreater`는 시작하지 않는다.
- 시험 중 Framework 19개는 고정본이 없어 연결하지 않았다(`PROJECT_CONTEXT.md` 「Framework 기준선」).
- 골격은 `MoonVillage/Setup/Build Project Skeleton`(`MV.Editor.Setup.MvProjectSkeleton`)으로 만들었다. 있는 것은 건드리지 않으므로 다시 실행해도 된다.
