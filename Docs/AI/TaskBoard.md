# TaskBoard

작업 계약 원장(기획서 11 §11.5). 계약을 시작할 때 계약 ID·담당·작업 브랜치·기준 SHA·쓰기 범위를, 끝날 때 결과 SHA·검증(PASS/FAIL/NOT_RUN)·남은 결정 ID를 적는다.

| 계약 | 상태 | 담당 | 작업 브랜치 | 기준 SHA | 결과 SHA | 검증 | 남은 것 |
|---|---|---|---|---|---|---|---|
| MV-00 프로젝트 생성·기반 연결 | 완료 | 총괄(Claude Code 대화) | `ai/MV-00-project-framework`, `ai/MV-00-boot-skeleton` | `c8c7c3a` | 아래 병합 Commit | compile PASS(잡 `8d04cd506014`, `7f1f90c67dd7`), PlayMode `BootFlowTests` 1/1 PASS(잡 `7f1f90c67dd7`, 후보 `04e675c`), EditMode NOT_RUN(시험 없음), 기기 실행 NOT_RUN | 아래 「MV-00 남김」 |

## MV-00 남김

- Managed Addressables Project Config는 만들지 않았다. 만들면 모든 Player 빌드가 BootstrapSplit 경로를 타므로(Framework `NewGame.md` 3단계) 첫 Addressable 콘텐츠가 들어오는 계약(MV-TOOL-ART·MV-UI-*)에서 만든다. 기본 `AddressableAssetSettings`만 있다.
- 10 §10.7 1단계의 앱 포함 A 로더(`ManagedPackagedRevisionLoader`)는 고정본 1.3.12-pre.10에 없다(pre.11부터). Addressables 초기화만 한다.
- 빈 asmdef 11개(`MV.Core`~`MV.Platform`, `MV.Boot`·`MV.Editor` 제외)는 스크립트가 없어 Unity가 「컴파일하지 않음」 경고를 낸다. 각 계약이 코드를 넣으면 사라진다.
- `Game` 씬은 카메라·조명·`GameInitializer`만 있다. `WorldRoot`·`GameCamera`(FOV 52)·`LobbyStage`는 MV-VIEW, 로비 화면은 MV-UI-LOBBY.
- `UIManager` 프리팹과 `ui.StartInitAsync()`(10 §10.7 8단계)는 MV-UI-*. 지금 `UIManagerCreater`는 시작하지 않는다.
- 시험 중 Framework 19개는 고정본이 없어 연결하지 않았다(`PROJECT_CONTEXT.md` 「Framework 기준선」).
- 골격은 `MoonVillage/Setup/Build Project Skeleton`(`MV.Editor.Setup.MvProjectSkeleton`)으로 만들었다. 있는 것은 건드리지 않으므로 다시 실행해도 된다.
