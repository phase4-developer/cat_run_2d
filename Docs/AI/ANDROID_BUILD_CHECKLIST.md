# Android 빌드 전 체크리스트와 실수 기록

이 문서는 Android 빌드에서 나온 실수를 원인·증상·막는 방법으로 남기고, 빌드 전에 볼 항목을 모은 곳이다. AI 작업자와 총괄은 Android 빌드(실행기 `build` 모드, `MV.Editor.Build.MvBuild.BuildAndroidDev`)를 내기 전과 빌드·실행 관련 계약을 쓸 때 이 문서를 읽는다.

근거: 사용자 원문 「이런 문제가 생기는게 반복되면 안돼니까 실수한것들은 다같이 공유해서 실수를 줄이면 좋겠어 안드로이드 빌드전 체크사항같은걸 기록해두는거 어떻까하네 개선사항으로 전달해줘」(2026-11-10, 중계 경유). 같은 날 「앞으로 대체 경로로 읽지말고 Resources 산출물을 읽어」.

## 1. 빌드 전 체크리스트

「자동」은 빌드 메서드나 시험이 이미 막는 항목이다. 「사람·총괄」은 아직 손으로 본다.

| # | 확인 | 어떻게 | 막는 곳 |
|---|---|---|---|
| 1 | 스크립팅 백엔드 IL2CPP, 대상 ARM64 | `ProjectSettings` `scriptingBackend.Android: 1`, `AndroidTargetArchitectures`에 ARM64 | 자동: `MvBuild` 3b(`Exit 5`), 시험 `MvBuildTests.ProjectAndroidSettings_AreDeviceReady` |
| 2 | 모든 Addressables 그룹이 Local 경로 | 그룹 스키마 BuildPath·LoadPath = `Local.*`. 원격 서버가 생기기 전까지 Remote 경로 금지 | 자동: 시험 `MvAtlasSetupTests.AddressableGroups_AreLocalAndPackSeparately`(아트 도구 그룹). 그 밖의 그룹은 사람·총괄 |
| 3 | 그룹을 고치는 편집기 도구가 그 값을 되돌리지 않는지 | 도구가 경로를 강제하는 코드(`MvArtSetup.EnsureGroup` 등)를 같이 고친다 | 사람·총괄 |
| 4 | 표 Resources 산출물이 있고 TSV와 같음 | 빌드 직전 `TableBuilder.Build`(TSV → `Assets/Game/GameData/Table/Resources`, 검증기 포함) | 자동: `MvBuild` 3c(`Exit 6`). 게임은 에디터에서도 산출물만 읽는다(대체 경로 금지) |
| 5 | 패키지 자동 등록(`RuntimeInitializeOnLoadMethod`)이 IL2CPP 코드 제거에서 살아남는지 | `Assets/Game/link.xml`이 Framework·게임 런타임 어셈블리를 `preserve="all"`로 둔다. 새 패키지·새 asmdef를 넣으면 목록에 더한다 | 사람·총괄(시험 후보: link.xml 목록 ⊇ 런타임 asmdef 목록) |
| 6 | 빌드 결과를 APK 안에서 확인 | `unzip -l`로 `lib/arm64-v8a`, `assets/aa/Android/*.bundle` 수, `global-metadata.dat`에 자동 등록 형식 이름(예: `AddressablesClipProvider`)이 있는지 | 사람·총괄 |
| 7 | 빌드 임시 파일이 작업 트리에 남지 않는지 | `Assets/AddressableAssetsData/link.xml`(·`.meta`), `Assets/AddressableAssetsData/Android/` | 자동: `MvBuild`가 빌드 끝에 link.xml을 지운다. `Android/`와 link.xml은 `.gitignore` |
| 8 | 빌드가 새로 쓴 설정 파일을 받아 Commit | 결과의 `project_changes`(AdMob 설정·매니페스트, EDM4U Gradle 템플릿·`AndroidResolverDependencies.xml`, URP 설정, 알림 설정 등)를 보고 Commit. ABI를 바꾸면 EDM4U Gradle 제외 목록도 다시 확인 | 사람·총괄 |
| 9 | 실기 실행 로그 | 설치 뒤 `adb logcat`에서 `E/Unity`·`AndroidRuntime`·`FATAL`이 없는지(사용자가 기기 확인을 허락했을 때) | 사람·총괄. 실행기에는 설치·실행 모드가 없다(중앙 요청 r-73959ba9f77b) |
| 10 | 같은 칸에서 Unity 잡이 도는 중에 파일을 고치지 않는다 | 실행 중인 Unity가 메모리의 옛 값으로 자산을 다시 저장한다 | 사람·총괄 |

## 2. 이번에 나온 실수 (2026-11-10, MV-BUILD-ANDROID)

| 증상 | 원인 | 막는 방법 |
|---|---|---|
| 실행이 안 되거나 「최신 Android와 호환 안 됨」 경고 | 개발 빌드를 Mono·ARMv7(32비트)로 정했다(잠정 결정 D-BUILD-DEV). 최근 기기는 64비트 전용. EDM4U Gradle 템플릿도 arm64 라이브러리를 빼고 있었다 | 체크 1·8. 개발 빌드도 IL2CPP·ARM64 |
| 실행하면 검은 화면 | UI·월드·이펙트가 Remote Addressables 그룹에 있었고 Remote.LoadPath가 비어 APK에 들어가지 않았다. 에디터는 원본 자산을 바로 읽어 드러나지 않았다 | 체크 2·6. 원격 에셋을 쓰지 않으므로 모든 그룹 Local(사용자 「원격 에셋은 안쓸거니 다 로컬로」) |
| 로컬로 바꾼 그룹 3개가 Unity 실행마다 Remote로 돌아감 | 아트 도구 `MvArtSetup.EnsureGroup`이 매번 Remote 경로를 강제했다. 그 사이 확인 잡이 두 번 FAIL | 체크 3 |
| `Audio provider Addressables is not installed` | IL2CPP 코드 제거가 직접 호출 없는 패키지 자동 등록(`AddressablesClipProvider`, `AddressableSceneSource`, `UISceneTransitionPresenter`)을 지웠다 | 체크 5·6 |
| `[TableUtil] Resources table is null (00_config)` | 표 Resources 산출물이 한 번도 만들어지지 않았다. 에디터는 TSV 폴더를 읽는 대체 경로로 돌아서 드러나지 않았다 | 체크 4. 대체 경로 금지(AGENTS.md Unity 절, 기획서 07 §7.1) |
| 확인 잡이 `SOURCE_CANDIDATE_CHANGED`로 여러 번 FAIL | 빌드가 남긴 `Assets/AddressableAssetsData/link.xml`을 다음 Unity 실행이 지웠다. 또 실행 중인 잡이 있는 칸에서 파일을 고쳤다 | 체크 7·10 |
| 커밋 5건이 dev가 아닌 `master`에 들어감 | 원본 폴더가 `master`에 있는 것을 확인하지 않고 그 폴더에서 Commit했다 | Commit 전 `git rev-parse --abbrev-ref HEAD` 확인. 병합은 작업 칸에서 하고 원본 폴더 브랜치는 건드리지 않는다 |
| 첫 APK가 PASS였는데 실기에서 안 됨 | 실행기 PASS는 「APK 파일이 새로 쓰였다」까지만 본다 | 체크 6·9를 거치기 전에는 「빌드 완료」가 아니라 「APK 생성」으로만 보고한다 |
