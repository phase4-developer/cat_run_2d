# TaskBoard

작업 계약 원장(기획서 11 §11.5). 계약을 시작할 때 계약 ID·담당·작업 브랜치·기준 SHA·쓰기 범위를, 끝날 때 결과 SHA·검증(PASS/FAIL/NOT_RUN)·남은 결정 ID를 적는다.

| 계약 | 상태 | 담당 | 작업 브랜치 | 기준 SHA | 결과 SHA | 검증 | 남은 것 |
|---|---|---|---|---|---|---|---|
| MV-00 프로젝트 생성·기반 연결 | 완료 | 총괄(Claude Code 대화) | `ai/MV-00-project-framework`, `ai/MV-00-boot-skeleton` | `c8c7c3a` | 아래 병합 Commit | compile PASS(잡 `8d04cd506014`, `7f1f90c67dd7`), PlayMode `BootFlowTests` 1/1 PASS(잡 `7f1f90c67dd7`, 후보 `04e675c`), EditMode NOT_RUN(시험 없음), 기기 실행 NOT_RUN | 아래 「MV-00 남김」 |

| MV-DATA 표·검증기·로더 | 1차 병합, 2차 진행 | 1번째(앱 세션) 멈춤 → 2번째(하위 에이전트) 닫힘 → 3번째 진행 | 1차 `ai/MV-DATA-tables-loader`(병합 뒤 지움), 2차 `ai/MV-DATA-content-tables` @ `.wt/a`, `moonvillage-a` | 1차 `b210aab`, 2차 dev `05b42af` | 1차 `b7c2db5` → dev 병합 `05b42af` | 1차: compile `3d1988cf6179` PASS, EditMode `0263185fe518` 46/46 PASS, PlayMode `c172e9cb5edf` BootFlow PASS. NOT_RUN: TableBuilder Resources 산출물(플레이어 빌드), 문구 키 누락, 리소스 열 검사 | 2차: 08 4xx·7xx 표 24개, 자료 빈칸, 소리 `_n` 검사. 결정 D-71·D-03·D-63. 기획서 07 §7.13.2 630 허용 동기화함 |
| MV-SAVE 저장·이관 | 완료(G1 통합 대기) | 하위 에이전트 닫힘 | `ai/MV-SAVE-save-migration`(G1 통합 브랜치로 병합) | `43637e7` | `31b51a3` | compile `291ea15aa1bd` PASS, EditMode `9a7bd185e321` 18/18 PASS, PlayMode NOT_RUN(계약 없음) | D-68 대기: 고정본 save는 평문 불가 → 개발은 공개 고정 키, 출시는 키 공급자 교체 지점(지금 출시 빌드는 저장 못 엶). 참조 정리·Boot 6단계는 MV-G1-INT |
| MV-G1-INT 표+저장 Boot 연결 | 진행 | 하위 에이전트 | `ai/MV-G1-INT-boot-save` @ `.wt/b`, `moonvillage-b` | dev `05b42af` + MV-SAVE 병합 `e9d9927` + 총괄 asmdef `ed5083c` | | | 쓰기: `S/MV.Boot/`, `S/MV.Save/`, `S/MV.Save.Tests/`, `Tests/PlayMode/`, 기획서 09 동기화. 통과하면 이 브랜치를 dev에 병합(G1 끝) |
| MV-RUN-SIM 런 시뮬레이션 | 진행 | 하위 에이전트 | `ai/MV-RUN-SIM-core` @ `.wt/f`, `moonvillage-f` | dev `05b42af` + 총괄 asmdef `3155544`(MV.Run.Tests) | | | 쓰기: `S/MV.Run/`, `S/MV.Run.Tests/`(Gen 제외). 공용 상태 Commit 뒤 MV-RUN-GEN 시작 |
| MV-RUN-GEN 레벨 생성 | 대기 | — | — | | | | MV-RUN-SIM 공용 상태(RunObject) Commit 위에서 시작 |
| MV-META 메타 규칙 | 대기 | — | — | | | | 선행: MV-SAVE가 dev에 들어간 뒤(MV-G1-INT) |
| MV-TOOL-ART 그림 추출 | 1차 병합, 3차 진행 | 1·2번째 닫힘 → 3번째 진행 | 1차 `ai/MV-TOOL-ART-extract`(병합 뒤 지움), 3차 `ai/MV-TOOL-ART-remaining` @ `.wt/c` | `43637e7`, 3차 dev `688ecaf` | `e7b58c8` → dev `688ecaf` | compile `630d4553c1d9` PASS, V1 결정성 PASS, V2 PASS(ΔE 최대 ≤ 3 — 총괄 결정, 03 §3.11 동기화), V4 PASS, V3 NOT_RUN | 정식 1699·임시 275. 남은 것: 표가 쓰는 미추출 14이름, 로비·이펙트·UI 스킨. MV-DATA 요청: 113 portrait 대소문자(2차 담당에 전달) |
| MV-QA-HARNESS 캡처 도구(프로토 쪽) | 프로토 쪽 병합 | 하위 에이전트 닫힘 | `ai/MV-QA-HARNESS-proto`(병합 뒤 지움) | `43637e7` | `b66c41f` → dev `63c76dc` | 37/38장면(U-14 protoNA) 118장 2회 픽셀 해시 동일, GEN·FIELD·SIM 덤프 재생성 SHA 동일. 사람 확인 NOT_RUN: U-04·U-12·U-13 연출 포착 | Unity 쪽은 MV-RUN-SIM·MV-VIEW 뒤 별도. `mask[]` 실측은 MV-QA-ACCEPT. 기획서 12 §12.1 동기화. 결정 D-41+D-30·D-71·D-27 |
| MV-TOOL-SND 소리 굽기 | 진행 | 하위 에이전트 | `ai/MV-TOOL-SND-bake` @ `.wt/e`, `moonvillage-e` | `e4e492d`(MV-TOOL-ART 공용 기반, dev `43637e7` 위) | | | 쓰기: `Tools/MvExtract/sound/`, `A/Addressables/Remote/Audio/`. 공용 파일 변경은 요청으로 |
| MV-ART-ATLAS 아틀라스·임포트 | 진행 | 하위 에이전트 | `ai/MV-ART-ATLAS-import` @ `.wt/g`, `moonvillage-g` | dev `688ecaf` + 총괄 asmdef `474ad46`(MV.Editor 참조, MV.Editor.Tests) | | | 쓰기: UIAtlas·WorldAtlas·Texture, `S/MV.Editor/Import/`, `S/MV.Editor.Tests/`, Art `.meta` 임포트 설정. Addressables 그룹 자산을 이 계약에 위임(전역 설정·Default 그룹·Managed Config 제외) |

## 담당표 (2026-11-09)

| 칸 | 작업 폴더 | 실행기 id | 지금 계약 | 닫는 조건 |
|---|---|---|---|---|
| a | `C:/Project/MoonVillage.wt/a` | `moonvillage-a` | MV-DATA 2차 | 완료·체크포인트·입력 약 15만 토큰 |
| b | `C:/Project/MoonVillage.wt/b` | `moonvillage-b` | MV-G1-INT(MV-SAVE 끝) | 같음 |
| c | `C:/Project/MoonVillage.wt/c` | `moonvillage-c` | MV-TOOL-ART 3차 | 같음 |
| d | `C:/Project/MoonVillage.wt/d` | `moonvillage-d` | 비어 있음(dev 분리 HEAD, 다음 계약에 재사용) | — |
| e | `C:/Project/MoonVillage.wt/e` | `moonvillage-e` | MV-TOOL-SND | 같음 |
| f | `C:/Project/MoonVillage.wt/f` | `moonvillage-f` | MV-RUN-SIM | 같음 |
| g | `C:/Project/MoonVillage.wt/g` | `moonvillage-g` | MV-ART-ATLAS | 같음 |

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
