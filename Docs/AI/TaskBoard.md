# TaskBoard

작업 계약 원장(기획서 11 §11.5). 계약을 시작할 때 계약 ID·담당·작업 브랜치·기준 SHA·쓰기 범위를, 끝날 때 결과 SHA·검증(PASS/FAIL/NOT_RUN)·남은 결정 ID를 적는다.

| 계약 | 상태 | 담당 | 작업 브랜치 | 기준 SHA | 결과 SHA | 검증 | 남은 것 |
|---|---|---|---|---|---|---|---|
| MV-00 프로젝트 생성·기반 연결 | 완료 | 총괄(Claude Code 대화) | `ai/MV-00-project-framework`, `ai/MV-00-boot-skeleton` | `c8c7c3a` | 아래 병합 Commit | compile PASS(잡 `8d04cd506014`, `7f1f90c67dd7`), PlayMode `BootFlowTests` 1/1 PASS(잡 `7f1f90c67dd7`, 후보 `04e675c`), EditMode NOT_RUN(시험 없음), 기기 실행 NOT_RUN | 아래 「MV-00 남김」 |

| MV-DATA 표·검증기·로더 | 1차 병합, 2차 진행 | 1번째(앱 세션) 멈춤 → 2번째(하위 에이전트) 닫힘 → 3번째 진행 | 1차 `ai/MV-DATA-tables-loader`(병합 뒤 지움), 2차 `ai/MV-DATA-content-tables` @ `.wt/a`, `moonvillage-a` | 1차 `b210aab`, 2차 dev `05b42af` | 1차 `b7c2db5` → dev 병합 `05b42af` | 1차: compile `3d1988cf6179` PASS, EditMode `0263185fe518` 46/46 PASS, PlayMode `c172e9cb5edf` BootFlow PASS. NOT_RUN: TableBuilder Resources 산출물(플레이어 빌드), 문구 키 누락, 리소스 열 검사 | 2차: 08 4xx·7xx 표 24개, 자료 빈칸, 소리 `_n` 검사. 결정 D-71·D-03·D-63. 기획서 07 §7.13.2 630 허용 동기화함 |
| MV-SAVE 저장·이관 | 완료(dev 병합) | 하위 에이전트 닫힘 | `ai/MV-SAVE-save-migration`(G1 통합 브랜치로 병합, 지움) | `43637e7` | `31b51a3` → dev `bf2921c`(MV-G1-INT와 함께) | compile `291ea15aa1bd` PASS, EditMode `9a7bd185e321` 18/18 PASS, PlayMode NOT_RUN(계약 없음) | D-68 대기: 고정본 save는 평문 불가 → 개발은 공개 고정 키, 출시는 키 공급자 교체 지점(지금 출시 빌드는 저장 못 엶) |
| MV-G1-INT 표+저장 Boot 연결 | 완료(dev 병합) | 하위 에이전트 닫힘 | `ai/MV-G1-INT-boot-save`(병합 뒤 지움) | dev `05b42af` + MV-SAVE `e9d9927` + 총괄 asmdef `ed5083c` | `5a0aa09` → dev `bf2921c` | compile `3121c21cc91d` PASS, EditMode `51d33fefac68` 71/71 PASS, PlayMode `3fbf1b89cb52` BootFlow 1/1 PASS. NOT_RUN: 플레이어 빌드 저장·표(TableBuilder Resources) | Boot 4→6→5 순서(10 §10.7·09 동기화). `mvDaily.modePlays`는 `400_content` 표(MV-DATA 2차) 뒤. G3 통합 때 Boot에 `options.IsComponentImplemented = MV.Run.RunComponents.IsImplemented` 연결(MV-RUN-SIM 요청). D-68 |
| MV-RUN-SIM 런 시뮬레이션 | 진행 | 하위 에이전트 2번째(1번째 닫힘: 입력 한도, `841bcf0` — 공용 상태 `1bd449d` compile `a787f247aaec` PASS, 규칙 1차 compile 대기 `b584d62dab29`, 실행기 RESOURCE_BUSY 거절 2회) | `ai/MV-RUN-SIM-core` @ `.wt/f`, `moonvillage-f` | dev `05b42af` + 총괄 asmdef `3155544`(MV.Run.Tests) | | | 쓰기: `S/MV.Run/`, `S/MV.Run.Tests/`(Gen 제외). 공용 상태 Commit 뒤 MV-RUN-GEN 시작 |
| MV-RUN-GEN 레벨 생성 | 진행 | 하위 에이전트 | `ai/MV-RUN-GEN-level` @ `.wt/d`, `moonvillage-d` | `1bd449d`(MV-RUN-SIM 공용 상태: dev `d9fcddc` + 총괄 asmdef `4576f19` MV.Run→JJEvol.Tables.Runtime) | | | 쓰기: `S/MV.Run/Gen/`, `S/MV.Run.Tests/Gen/`. 대조: `QA/Dumps/gen/` |
| MV-META 메타 규칙 | 시작 가능 | — | (칸 b 또는 e) | | | | 선행 MV-SAVE·MV-G1-INT dev 들어옴(`bf2921c`). 총괄 asmdef: `MV.Meta.Tests` 필요 |
| MV-TOOL-ART 그림 추출 | 1차 병합, 3차 진행 | 1·2번째 닫힘 → 3번째 진행 | 1차 `ai/MV-TOOL-ART-extract`(병합 뒤 지움), 3차 `ai/MV-TOOL-ART-remaining` @ `.wt/c` | `43637e7`, 3차 dev `688ecaf` | `e7b58c8` → dev `688ecaf` | compile `630d4553c1d9` PASS, V1 결정성 PASS, V2 PASS(ΔE 최대 ≤ 3 — 총괄 결정, 03 §3.11 동기화), V4 PASS, V3 NOT_RUN | 정식 1699·임시 275. 남은 것: 표가 쓰는 미추출 14이름, 로비·이펙트·UI 스킨. MV-DATA 요청: 113 portrait 대소문자(2차 담당에 전달) |
| MV-QA-HARNESS 캡처 도구(프로토 쪽) | 프로토 쪽 병합 | 하위 에이전트 닫힘 | `ai/MV-QA-HARNESS-proto`(병합 뒤 지움) | `43637e7` | `b66c41f` → dev `63c76dc` | 37/38장면(U-14 protoNA) 118장 2회 픽셀 해시 동일, GEN·FIELD·SIM 덤프 재생성 SHA 동일. 사람 확인 NOT_RUN: U-04·U-12·U-13 연출 포착 | Unity 쪽은 MV-RUN-SIM·MV-VIEW 뒤 별도. `mask[]` 실측은 MV-QA-ACCEPT. 기획서 12 §12.1 동기화. 결정 D-41+D-30·D-71·D-27 |
| MV-TOOL-SND 소리 굽기 | 완료(dev 병합) | 하위 에이전트 닫힘 | `ai/MV-TOOL-SND-bake`(병합 뒤 지움) | `e4e492d` | `cb96a78` → dev `25ae2cb` | 형식 32/32, 재실행 해시 32/32, V6 실시간 녹음 대조 32/32 PASS(상관 최저 0.9954), compile `78fc269bf094` PASS. 청취 확인 NOT_RUN | 03 §3.9.1과 다르게 한 4가지는 총괄 결정으로 03 동기화(앞 무음 1.0014 s, BGM 두 번째 루프, 뒤 무음 정리 끔, GainNode 사슬). D-55 잔재 5종 정의만, D-56 WAV→Vorbis 임포트, D-57 BGM 2파일. `package.json` snd 스크립트 총괄이 추가 |
| MV-ART-ATLAS 아틀라스·임포트 | 진행 | 하위 에이전트 | `ai/MV-ART-ATLAS-import` @ `.wt/g`, `moonvillage-g` | dev `688ecaf` + 총괄 asmdef `474ad46`(MV.Editor 참조, MV.Editor.Tests) | | | 쓰기: UIAtlas·WorldAtlas·Texture, `S/MV.Editor/Import/`, `S/MV.Editor.Tests/`, Art `.meta` 임포트 설정. Addressables 그룹 자산을 이 계약에 위임(전역 설정·Default 그룹·Managed Config 제외) |

## 담당표 (2026-11-09)

| 칸 | 작업 폴더 | 실행기 id | 지금 계약 | 닫는 조건 |
|---|---|---|---|---|
| a | `C:/Project/MoonVillage.wt/a` | `moonvillage-a` | MV-DATA 2차 | 완료·체크포인트·입력 약 15만 토큰 |
| b | `C:/Project/MoonVillage.wt/b` | `moonvillage-b` | 비어 있음(detached, MV-G1-INT 닫힘) | — |
| c | `C:/Project/MoonVillage.wt/c` | `moonvillage-c` | MV-TOOL-ART 3차 | 같음 |
| d | `C:/Project/MoonVillage.wt/d` | `moonvillage-d` | MV-RUN-GEN | 완료·체크포인트·입력 약 15만 토큰 |
| e | `C:/Project/MoonVillage.wt/e` | `moonvillage-e` | 비어 있음(detached, MV-TOOL-SND 닫힘) | — |
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
