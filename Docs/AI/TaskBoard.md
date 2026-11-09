# TaskBoard

작업 계약 원장(기획서 11 §11.5). 계약을 시작할 때 계약 ID·담당·작업 브랜치·기준 SHA·쓰기 범위를, 끝날 때 결과 SHA·검증(PASS/FAIL/NOT_RUN)·남은 결정 ID를 적는다.

| 계약 | 상태 | 담당 | 작업 브랜치 | 기준 SHA | 결과 SHA | 검증 | 남은 것 |
|---|---|---|---|---|---|---|---|
| MV-00 프로젝트 생성·기반 연결 | 완료 | 총괄(Claude Code 대화) | `ai/MV-00-project-framework`, `ai/MV-00-boot-skeleton` | `c8c7c3a` | 아래 병합 Commit | compile PASS(잡 `8d04cd506014`, `7f1f90c67dd7`), PlayMode `BootFlowTests` 1/1 PASS(잡 `7f1f90c67dd7`, 후보 `04e675c`), EditMode NOT_RUN(시험 없음), 기기 실행 NOT_RUN | 아래 「MV-00 남김」 |

| MV-DATA 표·검증기·로더 | 진행 | 하위 에이전트(이 총괄 대화) — 2번째 담당. 1번째 담당(앱 세션)은 사용자 지시로 멈추고 「작업 중」 Commit `d397e9d`로 넘김 | `ai/MV-DATA-tables-loader` @ `C:/Project/MoonVillage.wt/a`, 실행기 `moonvillage-a` | `b210aab`(작업 중 Commit 기준), dev `43637e7` | | | 쓰기: `S/MV.Data/`, `S/MV.Data.Tests/`, TSV·Resources, `S/MV.Editor/Tables/`, `GameBootstrap.cs` 4단계. 총괄 asmdef `464f8b8` |
| MV-SAVE 저장·이관 | 진행 | 하위 에이전트 | `ai/MV-SAVE-save-migration` @ `.wt/b`, `moonvillage-b` | `43637e7` | | | 쓰기: `S/MV.Save/`, `S/MV.Save.Tests/`. Boot 6단계 연결은 G1 통합 때 총괄. D-68 대기(키 위치) → 교체 지점. 총괄 asmdef `f0214ef` |
| MV-TOOL-ART 그림 추출 | 진행 | 하위 에이전트 | `ai/MV-TOOL-ART-extract` @ `.wt/c`, `moonvillage-c` | `43637e7` | | | 쓰기: `Tools/MvExtract/`(sound 제외), `A/Art/Source/`, `A/Art/Temp/`. D-50 승인(2026-11-09 사용자 「전부 승인」) |
| MV-QA-HARNESS 캡처 도구(프로토 쪽) | 진행 | 하위 에이전트 | `ai/MV-QA-HARNESS-proto` @ `.wt/d`, `moonvillage-d` | `43637e7` | | | 쓰기: `Tools/MvCapture/`, `QA/Scenes/`, `QA/Dumps/`, `QA/Captures/Proto/`. Unity 쪽은 MV-RUN-SIM·MV-VIEW 뒤 별도 계약 |
| MV-TOOL-SND 소리 굽기 | 대기 | — | — | | | | MV-TOOL-ART 공용 기반 Commit 위에서 시작(같은 `Tools/MvExtract/` 기반) |
| MV-ART-ATLAS 아틀라스·임포트 | 대기 | — | — | | | | 선행: MV-TOOL-ART 출력, MV-DATA 표 클래스 |

## 담당표 (2026-11-09)

| 칸 | 작업 폴더 | 실행기 id | 지금 계약 | 닫는 조건 |
|---|---|---|---|---|
| a | `C:/Project/MoonVillage.wt/a` | `moonvillage-a` | MV-DATA | 완료·체크포인트·입력 약 15만 토큰 |
| b | `C:/Project/MoonVillage.wt/b` | `moonvillage-b` | MV-SAVE | 같음 |
| c | `C:/Project/MoonVillage.wt/c` | `moonvillage-c` | MV-TOOL-ART | 같음 |
| d | `C:/Project/MoonVillage.wt/d` | `moonvillage-d` | MV-QA-HARNESS(프로토) | 같음 |

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
