# 프로젝트 문맥

<!-- jjevol-project-settings:start — 설정 화면(「프로젝트 설정」)이 만든 절. 고치려면 그 화면에서 고른다. -->
## 프로젝트 설정

저장: 2026-11-10 13:38 KST, 사용자가 설정 화면에서 고름. 정본은 `Docs/AI/ProjectSettings.json`.

- Framework 연결 방식: 외부
- 프로젝트 기본: 어떤 프로젝트인가 = 새 게임; 누구 프로젝트인가 = 회사
- 게임 형태: 2D / 3D = 2.5D; 렌더 파이프라인 = URP Universal; Unity 버전 = 6000.3.9f1; 출시 플랫폼 = Android, iOS; 화면 방향 = 세로; 입력 = 터치; UI 방식 = uGUI
- 기획과 데이터: 기획서 = 확정 기획서 있음; 기획서 위치 = Docs/MV-SPEC-01/README.md; 바꿀 게임 규칙 정리 위치 = Docs/MV-SPEC-01/04_GameRules.md; 게임 수치를 고치는 곳(정본) = Framework 표(TSV, 표 제작 도구); 지원 언어 = 한국어, 영어; 저장 = 서버 저장; 수익 = 광고, 인앱 결제
- 표·폴더 규칙: 표 규칙 = JJEvol 표 규격 v1; 쓸 보상 표 = Drop, 랜덤박스; 폴더 규칙 = JJEvol 폴더 규격 v1
- Framework: Framework 사용 = 쓴다; Framework 연결 방식 = 외부; 영역: 콘텐츠 로드 = 쓴다; 영역: 씬 전환 = 쓴다; 영역: 화면·팝업 = 쓴다; 영역: 소리·연출 = 쓴다; 영역: 저장 = 쓴다; 영역: 표 읽기·제작 = 쓴다; 영역: 언어 읽기·제작 = 쓴다; 영역: 재화(돈·아이템) = 쓴다; 영역: 플랫폼 기본 = 쓴다; 같은 범위 안 버전 갱신 = AI에게 맡김
- 더할 기능: 광고 = AdMob; 인앱 결제 = Unity 결제; 로그인 = Firebase 계정; 통계 = Firebase; 알림 = 원격 푸시, 로컬 알림; 스토어 리뷰·추적 동의 = iOS 추적 동의(ATT); 서버 통신 = 씀: Firebase 서버; 서버 기능을 지금 처리하는 곳 = 로컬 백엔드(서버 없음); 장르 기능 = 없음
- AI 권한·자동 진행: Commit = 검증 통과하면 묻지 않고 Commit; 병합 = 테스트 브랜치에 병합, main은 내가 직접; 테스트 브랜치 이름 = dev; AI가 Unity로 돌릴 수 있는 것 = EditMode 시험, 컴파일, PlayMode 시험, 빌드와 기기·에뮬레이터 실행; Unity 실행기 등록 = 실행기에 등록; QA 대상 = Android 에뮬레이터; 병렬 담당 = 자동; 외부 패키지·SDK 설치 = 매번 확인; 무인 장기 작업(헤드리스) = 조건을 채우면 헤드리스로 보냄; 같은 문제 자동 수정 횟수 = 2번
- 아트·품질: 아트 공급 = 임시 아트로 먼저; 목표 FPS = 60
- Framework 범위(함께 필요한 패키지 포함): `com.artress.managed-addressables` (고정 1.3.12-pre.10), `com.jjevol.audio` (고정 0.1.0-pre.3), `com.jjevol.audio.addressables` (고정 0.1.0-pre.2), `com.jjevol.data` (고정 0.1.0-pre.1), `com.jjevol.economy` (고정 0.1.0-pre.4), `com.jjevol.economy.drop` (고정 0.1.0-pre.1), `com.jjevol.effects` (고정 0.1.0-pre.2), `com.jjevol.effects.addressables` (고정 0.1.0-pre.2), `com.jjevol.effects.ui` (고정 0.1.0-pre.3), `com.jjevol.localization` (고정 0.1.0-pre.3), `com.jjevol.localization.authoring` (고정 0.1.0-pre.9), `com.jjevol.platform.ads` (고정 0.1.0-pre.1), `com.jjevol.platform.ads.admob` (고정 0.1.0-pre.2), `com.jjevol.platform.analytics` (고정 0.1.0-pre.1), `com.jjevol.platform.analytics.firebase` (고정 0.1.0-pre.1), `com.jjevol.platform.core` (고정 0.1.0-pre.2), `com.jjevol.platform.localnotifications` (고정 0.1.0-pre.1), `com.jjevol.platform.localnotifications.unity` (고정 0.1.0-pre.1), `com.jjevol.platform.login` (고정 0.1.0-pre.1), `com.jjevol.platform.login.firebase` (고정 0.1.0-pre.4), `com.jjevol.platform.network` (고정 0.1.0-pre.1), `com.jjevol.platform.network.data` (고정 0.1.0-pre.1), `com.jjevol.platform.purchase` (고정 0.1.0-pre.1), `com.jjevol.platform.purchase.network` (고정 0.1.0-pre.1), `com.jjevol.platform.purchase.unityiap` (고정 0.1.0-pre.1), `com.jjevol.platform.push` (고정 0.1.0-pre.1), `com.jjevol.platform.push.firebase` (고정 0.1.0-pre.1), `com.jjevol.platform.tracking` (고정 0.1.0-pre.1), `com.jjevol.platform.tracking.apple` (고정 0.1.0-pre.1), `com.jjevol.platform.unity` (고정 0.1.0-pre.2), `com.jjevol.resources.addressables` (고정 0.1.0-pre.3), `com.jjevol.save` (고정 0.1.0-pre.3), `com.jjevol.scene` (고정 0.1.0-pre.2), `com.jjevol.scene.addressables` (고정 0.1.0-pre.2), `com.jjevol.scene.ui` (고정 0.1.0-pre.3), `com.jjevol.tables` (고정 0.1.0-pre.2), `com.jjevol.tables.authoring` (고정 0.1.0-pre.7), `com.jjevol.ui` (고정 0.1.0-pre.2), `com.jjevol.ui.addressables` (고정 0.1.0-pre.4), `com.jjevol.ui.economy` (고정 0.1.0-pre.5), `com.jjevol.ui.urp` (고정 0.1.0-pre.6)

### AI가 따를 규칙

사용자가 설정 화면에서 직접 고른 값이다. 이 저장소의 규칙 파일(AGENTS.md 등)이 같은 항목을 다르게 적었으면 이 절이 먼저다. 회사 내용 반출 금지·Push와 `main` 반영·통합 Writer 같은 경계는 그 파일대로 지킨다.

- 위 「Framework 범위」에 적힌 패키지는 사용자가 범위로 승인한 것이다. 연결할 때 설치 확인과 저장소 규칙의 「고정본만」 제한보다 이 범위가 먼저다. 「고정」 표시는 그 고정 tgz를, 「베타」 표시는 Framework 최신 후보(외부 연결은 후보 tgz, 내부 연결은 폴더)를 연결하고 기준선에 「베타」와 SHA를 적는다. 고정본이 있다고 베타 표시 패키지를 고정본으로 바꾸거나 빼지 않는다.
- Framework 연결이 먼저다: 범위에 아직 연결하지 않은 패키지가 있으면 기능 계약보다 먼저, 담당 한 명이 범위 전체를 한 번에 연결하고 compile·EditMode·PlayMode 한 바퀴로 확인한다. 하나씩 나눠 연결·검증하지 않는다. 범위가 바뀌거나 고정본이 새로 나오면 모아서 다시 한 번에 한다. 연결을 막는 도구 오류는 바로 막힘으로 신고한다. 단, 게임에 없는 외부 SDK가 있어야 컴파일되는 패키지 (`com.jjevol.platform.ads.admob`: GoogleMobileAds.Common.dll, GoogleMobileAds.Core.dll, GoogleMobileAds.Ump.dll, GoogleMobileAds.dll; `com.jjevol.platform.login.firebase`: Firebase.App.dll, Firebase.Auth.dll)는 그 SDK가 게임에 들어올 때까지 첫 묶음에서 빼고 나머지를 먼저 연결한다. 뺀 패키지는 SDK 설치 결정(외부 설치 승인)으로 원장에 남기고, 그 패키지가 필요한 계약만 기다린다. SDK가 들어오면 뺀 것을 한 번에 더 연결한다.
- 검증을 통과한 자기 작업 브랜치 변경은 묻지 않고 Commit한다.
- AI 결과는 `dev`에 병합한다. `main` 반영과 Push는 사용자가 그 회차에 직접 말할 때만 한다.
- AI가 Unity로 돌리는 배치 실행은 EditMode, compile, PlayMode뿐이다(실행기가 그 밖은 거절한다).
- AI는 빌드를 만들고 기기·에뮬레이터에서 실행해 확인한다.
- 병렬 담당: 쓰기 파일이 겹치지 않는 작업마다 담당 1명.
- 외부 패키지·SDK 설치는 매번 확인한다. 단 위 「Framework 범위」에 적힌 패키지와 그 의존은 범위 승인이 이미 확인이다. 묻지 않고 연결한다(범위 밖 서드파티 SDK·유료만 확인).
- 같은 문제의 자동 수정은 2번까지. 넘으면 원인과 선택지를 보고하고 멈춘다.
- 조건(완료 조건·검증 명령, 남은 결정 없음, 긴 구현)을 채운 무인 장기 작업은 헤드리스로 보낸다. 병합 대상(테스트 브랜치 또는 main)의 최신 Commit에서 만든 `codex/<항목 ID>-<내용>`·`claude/…` 작업 브랜치에 결과가 남고, 테스트 브랜치 병합은 총괄이 한다. Push·main 반영은 하지 않는다.
- 회사 프로젝트의 헤드리스는 회사 조건으로 돈다: 커밋 작성자는 이 저장소의 사용자 Git 이름, 중앙 알림에는 작업 ID·상태·경로만 나간다.
- 재화 변경·구매·보상·우편·서버 시간처럼 서버가 판정할 기능은 게임 코드가 상태를 직접 바꾸지 않는다. `NetworkManager` 요청(`NetworkRequest` 하위 클래스)으로 보내고, 응답이 오면 응답 반영 함수가 저장소를 바꾼 뒤 콜백이 화면을 고친다. 재화 저장소 반영은 `com.jjevol.platform.network.data`가 한다.
- 지금은 서버가 없다. `NetworkManager.DefaultRoute = NetworkRoute.Local`로 두고, 이 프로젝트의 `INetworkLocalService` 구현(로컬 백엔드)이 서버와 같은 요청 이름·파라미터·응답 JSON으로 처리한다. 규칙에 어긋난 요청은 `NetworkRejection`(오류 코드)으로 거절한다.
- 서버 전환은 서버 공급자 패키지 연결, `SetServerProvider`, `DefaultRoute = NetworkRoute.Server`만으로 한다. 요청 클래스와 그것을 부르는 게임 코드는 고치지 않는다.

### 적용 상태

- Commit·병합: 대화가 따름 — 「AI가 따를 규칙」으로 PROJECT_CONTEXT.md에 들어가 대화가 시작할 때 읽는다
- AI의 Unity 실행: 도구가 따름 — 실행기(`unity_job.py submit`)가 고르지 않은 compile·EditMode·PlayMode를 거절한다
- Framework 범위: 도구가 따름 — 고른 패키지가 모두 manifest에 있다
- 담당 수·설치·수정 횟수: 대화가 따름 — 「AI가 따를 규칙」으로 대화가 따른다
- 서버 기능 처리: 대화가 따름 — 로컬 백엔드. 「AI가 따를 규칙」으로 대화가 따른다. `INetworkLocalService` 구현은 그 게임의 작업이다. 서버로 바꿀 때 Firebase 공급자 패키지와 Firebase SDK를 넣는다.
- 헤드리스: 도구가 따름 — 헤드리스 도구(`headless_work.py`)가 매 턴 이 값을 읽어 이 저장소의 쓰기 작업을 받는다. 「쓰지 않음」으로 바꾸면 다음 턴에 멈춘다. 회사 조건(사용자 Git 이름으로 커밋, 알림은 ID·상태·경로만)으로 돈다.

<!-- jjevol-project-settings:end -->

## 설정 절 보정 (2026-11-10, 사용자 지시)

위 설정 절은 2026-11-09 저장본이라 3차 묶음(`Snapshots/20261109-bundle3`) 고정 전의 「시험 중」·「연결 보류」 표기가 남아 있다. 사용자 원문 「결제·분석·알림 패키지는 넣어두기만 해도 돼」, 「저거 프로젝트 설정을 우선하도록 규칙 바꿔줘」에 따라 Framework 범위에 적힌 패키지는 설치 확인 없이 연결한다(고정본이 있으면 그것, 없으면 후보 tgz와 「시험 중」 기록). `AGENTS.md`는 이 절보다 낮다. 서버 통신 = Firebase 서버(`fw_network`)이므로 `com.jjevol.platform.network.firebase`도 범위다(위 저장본 목록에는 빠져 있음). 설정 화면에서 다시 저장하면 위 표기도 고쳐진다.

## Framework 기준선

연결: 2026-11-09, 계약 MV-00(작업 브랜치 `ai/MV-00-project-framework`), 사용자 지시 「만들고 연결해」·「넣어」. 방식은 「외부」라 Framework `Docs/Guide/Delivery.md`(2026-11-09, master `48868b1`) 「고정」 tgz만 `Packages/Frozen/`에 넣고 `file:Frozen/...`으로 연결했다. 복사본 SHA-256은 모두 목록과 같다. `audio`·`ui.urp`는 `scene.ui`·`audio.addressables`가 요구하는 2차 묶음 버전을 쓴다.

| 패키지 | 버전 | 저장소 안 tgz | Framework 스냅샷 | SHA-256 |
|---|---|---|---|---|
| `com.artress.managed-addressables` | 1.3.12-pre.10 | `Packages/Frozen/com.artress.managed-addressables-1.3.12-pre.10.tgz` | `20261107-managed-addressables-pre10` | `4190EDE4CAA6670CD42CE5F84E69226F5A738DF0EE3EA21D1E00460DE88B56A4` |
| `com.jjevol.audio` | 0.1.0-pre.3 | `Packages/Frozen/com.jjevol.audio-0.1.0-pre.3.tgz` | `20261109-bundle2` | `D68F43F9712A2C5FB19D4C929B3BEE6D33163C8555B2275AC79A6B18E87FB65A` |
| `com.jjevol.audio.addressables` | 0.1.0-pre.2 | `Packages/Frozen/com.jjevol.audio.addressables-0.1.0-pre.2.tgz` | `20261109-bundle2` | `CD9AE59072985A68029D11CA5A25E3C94292C24CA34E484AE32049B5309CC090` |
| `com.jjevol.data` | 0.1.0-pre.1 | `Packages/Frozen/com.jjevol.data-0.1.0-pre.1.tgz` | `20261108-bundle1` | `8D584655A65D77F290ABAB59AA3231A30AD0F0BE48EF891A90261192C06D20DC` |
| `com.jjevol.economy` | 0.1.0-pre.4 | `Packages/Frozen/com.jjevol.economy-0.1.0-pre.4.tgz` | `20261108-bundle1` | `EAC0A90BFE46E2751431BE41B5E006EE6FA63BFFDDA850628317FD55C7FEFE28` |
| `com.jjevol.effects` | 0.1.0-pre.2 | `Packages/Frozen/com.jjevol.effects-0.1.0-pre.2.tgz` | `20261108-bundle1` | `94975F2E324034262508D431267AB15B7DCB3ACCC5F46AF5521DD5C6CBA1AD05` |
| `com.jjevol.effects.addressables` | 0.1.0-pre.2 | `Packages/Frozen/com.jjevol.effects.addressables-0.1.0-pre.2.tgz` | `20261109-bundle2` | `540FB07D38E8CF8BB0A64B5F3331103E3D3796D9EDFE8C2EE80A8A2AD3155570` |
| `com.jjevol.localization` | 0.1.0-pre.3 | `Packages/Frozen/com.jjevol.localization-0.1.0-pre.3.tgz` | `20261108-bundle1` | `FFC2EB16164B7FB937663C32DFC210DCAC2ABCCE79CDD5EC401D5DE5B7CACC06` |
| `com.jjevol.localization.authoring` | 0.1.0-pre.9 | `Packages/Frozen/com.jjevol.localization.authoring-0.1.0-pre.9.tgz` | `20261109-bundle2` | `A6540FDBA86B1D3BA79B7B56B7CD531068076E7D1F417DDBBA6DF645A4AD2D36` |
| `com.jjevol.platform.core` | 0.1.0-pre.2 | `Packages/Frozen/com.jjevol.platform.core-0.1.0-pre.2.tgz` | `20261108-bundle1` | `E6D79CF622F137FA623C9BEACB5E9149EC2747F941041BFE0D0112D7837B971B` |
| `com.jjevol.platform.unity` | 0.1.0-pre.2 | `Packages/Frozen/com.jjevol.platform.unity-0.1.0-pre.2.tgz` | `20261108-bundle1` | `A690C195CAC783DD55EA9C8DBF7C98C590708E7E7F79A55AE193139744A044AD` |
| `com.jjevol.resources.addressables` | 0.1.0-pre.3 | `Packages/Frozen/com.jjevol.resources.addressables-0.1.0-pre.3.tgz` | `20261108-bundle1` | `8FB6D140F0E1D7EF987F1AD369BC9C9436ED4F9BF869EB01E718D06EA0708540` |
| `com.jjevol.save` | 0.1.0-pre.3 | `Packages/Frozen/com.jjevol.save-0.1.0-pre.3.tgz` | `20261104-ae2-tables-candidate1` | `5BAEC760A19A6EEB3DF54E9E98840E7395454CB07084BAEDFF808C66A77478B0` |
| `com.jjevol.scene` | 0.1.0-pre.2 | `Packages/Frozen/com.jjevol.scene-0.1.0-pre.2.tgz` | `20261109-bundle2` | `8D98B251CF3DF979D81B5C563B7D3A762682F133DC29DA20FF6D6DD31DBC0AA5` |
| `com.jjevol.scene.addressables` | 0.1.0-pre.2 | `Packages/Frozen/com.jjevol.scene.addressables-0.1.0-pre.2.tgz` | `20261109-bundle2` | `B9FC0C1E73AD831D8F20204C1E615F79918111D69DFDA97B1B3533CC35A32738` |
| `com.jjevol.scene.ui` | 0.1.0-pre.3 | `Packages/Frozen/com.jjevol.scene.ui-0.1.0-pre.3.tgz` | `20261109-bundle2` | `7FDED5CC767F98FF31CE0BDD8C83FDEC0C3499FCFBD318556B1CD5118A213094` |
| `com.jjevol.tables` | 0.1.0-pre.2 | `Packages/Frozen/com.jjevol.tables-0.1.0-pre.2.tgz` | `20261108-bundle1` | `76F156FDE1BF8F85B5CE07907AE42FB35BD0869EC5EE3252EA95F515ABCC3A49` |
| `com.jjevol.tables.authoring` | 0.1.0-pre.7 | `Packages/Frozen/com.jjevol.tables.authoring-0.1.0-pre.7.tgz` | `20261109-bundle2` | `A5C050317A92772562E74BC9DE676052A7C24A864B5CE96F4A87BCDB54BDA8B3` |
| `com.jjevol.ui` | 0.1.0-pre.2 | `Packages/Frozen/com.jjevol.ui-0.1.0-pre.2.tgz` | `20261108-bundle1` | `18C30EA3731A47A1AB374546ABE9D9BB4C0AFD02F26E994A0DC5F8E84F3D8270` |
| `com.jjevol.ui.addressables` | 0.1.0-pre.4 | `Packages/Frozen/com.jjevol.ui.addressables-0.1.0-pre.4.tgz` | `20261108-bundle1` | `169B5421A5CA0CF883DA133D71041B14F85E799CA82C4306C822AEC9BD31BFAF` |
| `com.jjevol.ui.economy` | 0.1.0-pre.5 | `Packages/Frozen/com.jjevol.ui.economy-0.1.0-pre.5.tgz` | `20261108-bundle1` | `93B39DEB04C7251B77525BFE5F0AA54EF22CB86B3BA5B79B8F9D9479F9A23E89` |
| `com.jjevol.ui.urp` | 0.1.0-pre.6 | `Packages/Frozen/com.jjevol.ui.urp-0.1.0-pre.6.tgz` | `20261109-bundle2` | `888168B6FDEACE1113DD3BD9D03E6D10EDA8B273E93B07B3BA8546ED675B3E40` |

외부 의존(2026-11-09 사용자 확인 「넣어」): `com.cysharp.unitask` Git `2e993ff18f28c931602a07292df0b0804eebef99`(2.5.11, Framework 검증 Host와 같음), `com.unity.addressables` 4.0.1, `com.unity.localization` 1.5.12, `com.unity.inputsystem` 1.18.0, `com.unity.nuget.newtonsoft-json` 3.2.2, `com.unity.render-pipelines.universal` 17.3.0, `com.unity.ugui` 2.0.0, `com.unity.test-framework` 1.6.0, IDE 연동 2개, 기본 모듈.

연결 보류(고정본 없음, 범위에는 있음): `economy.drop`, `effects.ui`, `platform.ads`·`.admob`, `platform.analytics`·`.firebase`, `platform.localnotifications`·`.unity`, `platform.login`·`.firebase`, `platform.network`·`.data`·`.firebase`, `platform.purchase`·`.network`·`.unityiap`, `platform.push`·`.firebase`, `platform.tracking`·`.apple` (20개, 모두 `com.jjevol.`). 고정본이 나오면 같은 방식으로 더한다.

검증: tgz SHA-256 대조 PASS, manifest JSON·`file:` 경로 확인 PASS. 사용자가 Unity 6000.3.9f1로 한 번 열어 `packages-lock.json` 생성(Editor.log 컴파일 오류 0). 실행기 compile 잡 `8d04cd506014` PASS(후보 `873e204`, 2026-11-09). EditMode·PlayMode·Boot 실행은 NOT_RUN(MV-00의 씬·asmdef 작업 뒤).

실행기: 2026-11-09 등록(`moonvillage`, compile·EditMode·PlayMode, 원문 「실행기에 등록해」), 위임 10일(원문 「10일 위임해」, 2026-11-19 만료).