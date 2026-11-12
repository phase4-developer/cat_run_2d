# 프로젝트 문맥

<!-- jjevol-project-settings:start — 설정 화면(「프로젝트 설정」)이 만든 절. 고치려면 그 화면에서 고른다. -->
## 프로젝트 설정

저장: 2026-11-12 18:20 KST, 사용자가 설정 화면에서 고름. 정본은 `Docs/AI/ProjectSettings.json`.

- Framework 연결 방식: 외부
- 프로젝트 기본: 어떤 프로젝트인가 = 새 게임; 누구 프로젝트인가 = 회사
- 게임 형태: 2D / 3D = 2.5D; 렌더 파이프라인 = URP Universal; Unity 버전 = 6000.3.9f1; 출시 플랫폼 = Android, iOS; 화면 방향 = 세로; 입력 = 터치; UI 방식 = uGUI
- 기획과 데이터: 기획서 = 확정 기획서 있음; 기획서 위치 = Docs/MV-SPEC-02/Source/README.md; 외부 원본 문서(URL) = https://docs.google.com/document/d/1R1DI7ckuu3zzKtwmAJ-7PD9MArLRYvfeO8MNY1n7RWo/edit?tab=t.0; 바꿀 게임 규칙 정리 위치 = Docs/MV-SPEC-02/DiffReport.md; 게임 수치를 고치는 곳(정본) = Framework 표(TSV, 표 제작 도구); 지원 언어 = 한국어, 영어; 저장 = 서버 저장; 수익 = 광고, 인앱 결제
- 표·폴더 규칙: 표 규칙 = JJEvol 표 규격 v1; 쓸 보상 표 = Drop, 랜덤박스; 폴더 규칙 = JJEvol 폴더 규격 v1
- Framework 기능: Framework 사용 = 쓴다; Framework 연결 방식 = 외부; 콘텐츠 로드 = 쓴다; 씬 전환 = 쓴다; 화면·팝업 = 쓴다; 소리·연출 = 쓴다; 저장 = 쓴다; 표 읽기·제작 = 쓴다; 언어 읽기·제작 = 쓴다; 재화(돈·아이템) = 쓴다; 플랫폼 기본 = 쓴다; 같은 범위 안 버전 갱신 = AI에게 맡김; 베타로 쓸 패키지 = com.jjevol.data, com.jjevol.economy, com.jjevol.platform.core, com.jjevol.platform.tracking, com.jjevol.platform.tracking.apple, com.jjevol.platform.unity, com.jjevol.scene, com.jjevol.scene.addressables, com.jjevol.scene.ui, com.jjevol.ui.economy; 광고 = AdMob; 인앱 결제 = Unity 결제; 로그인 = Firebase 계정; 통계 = Firebase; 알림 = 원격 푸시, 로컬 알림; 스토어 리뷰·추적 동의 = iOS 추적 동의(ATT); 게임 시스템 = 퀘스트·업적, 알림 배지(빨간 점); 외부 도구 연결 = DOTween Pro, Spine, Coffee UI Particle; 서버 통신 = 씀: Firebase 서버; 서버 기능을 지금 처리하는 곳 = 로컬 백엔드(서버 없음); 장르 기능 = 없음
- AI 권한·자동 진행: Commit = 검증 통과하면 묻지 않고 Commit; 병합 = 테스트 브랜치에 병합, main은 내가 직접; 테스트 브랜치 이름 = dev; AI가 Unity로 돌릴 수 있는 것 = EditMode 시험, 컴파일, PlayMode 시험, 빌드와 기기·에뮬레이터 실행; Unity 실행기 등록 = 실행기에 등록; QA 대상 = Android 에뮬레이터; 병렬 담당 = 자동; 외부 패키지·SDK 설치 = 매번 확인; 무인 장기 작업(헤드리스) = 조건을 채우면 헤드리스로 보냄; 같은 문제 자동 수정 횟수 = 2번
- 아트·품질: 아트 공급 = 임시 아트로 먼저; 목표 FPS = 60
- Framework 범위(함께 필요한 패키지 포함): `com.artress.managed-addressables` (고정 1.3.12-pre.19(베타 고정본: Unity 시험만 통과)), `com.jjevol.audio` (고정 0.1.0-pre.4(베타 고정본: Unity 시험만 통과)), `com.jjevol.audio.addressables` (고정 0.1.0-pre.2), `com.jjevol.badge` (고정 0.1.0-pre.1(베타 고정본: Unity 시험만 통과)), `com.jjevol.data` (베타 — 고정 0.1.0-pre.1 대신), `com.jjevol.economy` (베타 — 고정 0.1.0-pre.4 대신), `com.jjevol.economy.drop` (고정 0.1.0-pre.1), `com.jjevol.effects` (고정 0.1.0-pre.4(베타 고정본: Unity 시험만 통과)), `com.jjevol.effects.addressables` (고정 0.1.0-pre.2), `com.jjevol.effects.coffee` (고정 0.1.0-pre.2(베타 고정본: Unity 시험만 통과)), `com.jjevol.effects.dotween-pro` (고정 0.1.0-pre.2(베타 고정본: Unity 시험만 통과)), `com.jjevol.effects.spine` (고정 0.1.0-pre.3(베타 고정본: Unity 시험만 통과)), `com.jjevol.effects.ui` (고정 0.1.0-pre.4(베타 고정본: Unity 시험만 통과)), `com.jjevol.localization` (고정 0.1.0-pre.5(베타 고정본: Unity 시험만 통과)), `com.jjevol.localization.authoring` (고정 0.1.0-pre.14(베타 고정본: Unity 시험만 통과)), `com.jjevol.localization.tmp` (고정 0.1.0-pre.1(베타 고정본: Unity 시험만 통과)), `com.jjevol.platform.ads` (고정 0.1.0-pre.1), `com.jjevol.platform.ads.admob` (고정 0.1.0-pre.2), `com.jjevol.platform.analytics` (고정 0.1.0-pre.1), `com.jjevol.platform.analytics.firebase` (고정 0.1.0-pre.1), `com.jjevol.platform.core` (베타 — 고정 0.1.0-pre.2 대신), `com.jjevol.platform.localnotifications` (고정 0.1.0-pre.1), `com.jjevol.platform.localnotifications.unity` (고정 0.1.0-pre.1), `com.jjevol.platform.login` (고정 0.1.0-pre.1), `com.jjevol.platform.login.firebase` (고정 0.1.0-pre.4), `com.jjevol.platform.network` (고정 0.1.0-pre.2(베타 고정본: Unity 시험만 통과)), `com.jjevol.platform.network.data` (고정 0.1.0-pre.2(베타 고정본: Unity 시험만 통과)), `com.jjevol.platform.purchase` (고정 0.1.0-pre.1), `com.jjevol.platform.purchase.network` (고정 0.1.0-pre.2(베타 고정본: Unity 시험만 통과)), `com.jjevol.platform.purchase.unityiap` (고정 0.1.0-pre.1), `com.jjevol.platform.push` (고정 0.1.0-pre.1), `com.jjevol.platform.push.firebase` (고정 0.1.0-pre.2(베타 고정본: Unity 시험만 통과)), `com.jjevol.platform.tracking` (베타 — 고정 0.1.0-pre.1 대신), `com.jjevol.platform.tracking.apple` (베타 — 고정 0.1.0-pre.1 대신), `com.jjevol.platform.unity` (베타 — 고정 0.1.0-pre.2 대신), `com.jjevol.quest` (고정 0.1.0-pre.2(베타 고정본: Unity 시험만 통과)), `com.jjevol.resources.addressables` (고정 0.1.0-pre.3), `com.jjevol.save` (고정 0.1.0-pre.4(베타 고정본: Unity 시험만 통과)), `com.jjevol.scene` (베타 — 고정 0.1.0-pre.2 대신), `com.jjevol.scene.addressables` (베타 — 고정 0.1.0-pre.2 대신), `com.jjevol.scene.ui` (베타 — 고정 0.1.0-pre.4 대신), `com.jjevol.tables` (고정 0.1.0-pre.3(베타 고정본: Unity 시험만 통과)), `com.jjevol.tables.authoring` (고정 0.1.0-pre.11(베타 고정본: Unity 시험만 통과)), `com.jjevol.ui` (고정 0.1.0-pre.3(베타 고정본: Unity 시험만 통과)), `com.jjevol.ui.addressables` (고정 0.1.0-pre.6(베타 고정본: Unity 시험만 통과)), `com.jjevol.ui.contracts` (고정 0.1.0-pre.1(베타 고정본: Unity 시험만 통과)), `com.jjevol.ui.dotween-pro` (고정 0.1.0-pre.3(베타 고정본: Unity 시험만 통과)), `com.jjevol.ui.economy` (베타 — 고정 0.1.0-pre.6 대신), `com.jjevol.ui.urp` (고정 0.1.0-pre.8(베타 고정본: Unity 시험만 통과))

### AI가 따를 규칙

사용자가 설정 화면에서 직접 고른 값이다. 이 저장소의 규칙 파일(AGENTS.md 등)이 같은 항목을 다르게 적었으면 이 절이 먼저다. 회사 내용 반출 금지·Push와 `main` 반영·통합 Writer 같은 경계는 그 파일대로 지킨다.

- 위 「Framework 범위」는 사용자가 승인한 연결 범위다. 설치 확인과 저장소 규칙의 고정본 제한보다 먼저다. 「고정」(베타 고정본 포함)은 그 고정 tgz, 「베타」는 Framework 최신 후보(외부 연결은 후보 tgz, 내부 연결은 폴더)로 연결하고 기준선에 표시와 SHA를 적는다. 고정본이 생겨도 베타 표시를 바꾸거나 빼지 않는다.
- Framework 연결이 먼저다: 기능 계약보다 먼저 담당 한 명이 범위 전체를 한 번에 연결하고 compile·EditMode·PlayMode로 확인한다(절차는 `Docs/AI/Policy/Common.md` 「Framework 연결 먼저」). 외부 SDK가 있어야 컴파일되는 패키지(`com.jjevol.effects.dotween-pro`: DOTween.Modules, DOTween.dll, DOTweenPro.Scripts, DOTweenPro.dll; `com.jjevol.platform.ads.admob`: GoogleMobileAds.Common.dll, GoogleMobileAds.Core.dll, GoogleMobileAds.Ump.dll, GoogleMobileAds.dll; `com.jjevol.platform.login.firebase`: Firebase.App.dll, Firebase.Auth.dll; `com.jjevol.ui.dotween-pro`: DOTween.Modules, DOTween.dll, DOTweenPro.Scripts, DOTweenPro.dll)는 그 SDK가 들어올 때까지 첫 묶음에서 빼고 나머지를 먼저 연결한다. SDK 설치 결정은 원장에 남긴다.
- Android·iOS Player 빌드에 Firebase SDK 필요: `com.jjevol.platform.analytics.firebase`(Firebase Analytics), `com.jjevol.platform.push.firebase`(Firebase Messaging). Editor에서는 SDK 없이 컴파일되므로 첫 묶음에서 빼지 않는다. Player 빌드 전에 SDK 설치 결정(외부 설치 승인)을 원장에 남긴다.
- 기획서 원본은 외부 문서(<https://docs.google.com/document/d/1R1DI7ckuu3zzKtwmAJ-7PD9MArLRYvfeO8MNY1n7RWo/edit?tab=t.0>)다. AI는 읽기만 하고 원본을 고치지 않는다. 변경 추적·비교는 저장소 사본 `Docs/MV-SPEC-02/Source/README.md` 기준이다. 사본은 원본에서 다시 가져와 덮으므로 직접 고치지 않고, 기획서와 다른 결정은 원장에 남겨 원본 수정을 사용자에게 알린다.
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

실제 반영 상태(적용됨·미적용·불일치·확인 불가)는 이 절에 적지 않는다. `py -3 C:/Project/JJEvolAutomation/tools/project_settings.py check`가 매번 실제 파일에서 읽는다.

<!-- jjevol-project-settings:end -->

## 설정 절 보정 (2026-11-10, 사용자 지시)

위 설정 절은 2026-11-09 저장본이라 3차 묶음(`Snapshots/20261109-bundle3`) 고정 전의 「시험 중」·「연결 보류」 표기가 남아 있다. 사용자 원문 「결제·분석·알림 패키지는 넣어두기만 해도 돼」, 「저거 프로젝트 설정을 우선하도록 규칙 바꿔줘」에 따라 Framework 범위에 적힌 패키지는 설치 확인 없이 연결한다(고정본이 있으면 그것, 없으면 후보 tgz와 「시험 중」 기록). `AGENTS.md`는 이 절보다 낮다. 서버 통신 = Firebase 서버(`fw_network`)이므로 `com.jjevol.platform.network.firebase`도 범위다(위 저장본 목록에는 빠져 있음). 설정 화면에서 다시 저장하면 위 표기도 고쳐진다.

## Framework 기준선

연결: 2026-11-09, 계약 MV-00(작업 브랜치 `ai/MV-00-project-framework`), 사용자 지시 「만들고 연결해」·「넣어」. 방식은 「외부」라 Framework `Docs/Guide/Delivery.md`(2026-11-09, master `48868b1`) 「고정」 tgz만 `Packages/Frozen/`에 넣고 `file:Frozen/...`으로 연결했다. 복사본 SHA-256은 모두 목록과 같다. `audio`·`ui.urp`는 `scene.ui`·`audio.addressables`가 요구하는 2차 묶음 버전을 쓴다.

| 패키지 | 버전 | 저장소 안 tgz | Framework 스냅샷 | SHA-256 |
|---|---|---|---|---|
| `com.artress.managed-addressables` | 1.3.12-pre.19 | `Packages/Frozen/com.artress.managed-addressables-1.3.12-pre.19.tgz` | `20261112-uitoolkit-beta` **베타** | `29821A6762A75DC5740FD2382C100526F8C2C1EE85E5D5D74B989775C3E4E6B8` |
| `com.jjevol.audio` | 0.1.0-pre.4 | `Packages/Frozen/com.jjevol.audio-0.1.0-pre.4.tgz` | `20261110-bundle4` **베타** | `823B407CDECB6E9212E69A0158F7D069F6A56DECF1043A6FBE60A5A1663AB606` |
| `com.jjevol.audio.addressables` | 0.1.0-pre.2 | `Packages/Frozen/com.jjevol.audio.addressables-0.1.0-pre.2.tgz` | `20261109-bundle2` | `CD9AE59072985A68029D11CA5A25E3C94292C24CA34E484AE32049B5309CC090` |
| `com.jjevol.data` | 0.1.0-pre.1 | `Packages/Frozen/com.jjevol.data-0.1.0-pre.1.tgz` | `20261108-bundle1` → master `f39bd53` **베타**(후보 바이트 같음) | `8D584655A65D77F290ABAB59AA3231A30AD0F0BE48EF891A90261192C06D20DC` |
| `com.jjevol.economy` | 0.1.0-pre.4 | `Packages/Frozen/com.jjevol.economy-0.1.0-pre.4-beta.f39bd53.tgz` | master `f39bd53` **베타** | `02F0662BC5C9818D90BC7F1C91CD9F5F336656B189B70C47AA083BD4D8EDEC44` |
| `com.jjevol.effects` | 0.1.0-pre.4 | `Packages/Frozen/com.jjevol.effects-0.1.0-pre.4.tgz` | `20261112-uitoolkit-beta` **베타** | `E7D119726C4607AC7EB6E6A7A6BC6023CDE05680C9E0327916A5577ACD805260` |
| `com.jjevol.effects.addressables` | 0.1.0-pre.2 | `Packages/Frozen/com.jjevol.effects.addressables-0.1.0-pre.2.tgz` | `20261109-bundle2` | `540FB07D38E8CF8BB0A64B5F3331103E3D3796D9EDFE8C2EE80A8A2AD3155570` |
| `com.jjevol.localization` | 0.1.0-pre.5 | `Packages/Frozen/com.jjevol.localization-0.1.0-pre.5.tgz` | `20261112-uitoolkit-beta` **베타** | `3FA30F14C336B14FEBDCED834FD335BFEA218610CA8A1262C27E7F59943ED4AF` |
| `com.jjevol.localization.authoring` | 0.1.0-pre.14 | `Packages/Frozen/com.jjevol.localization.authoring-0.1.0-pre.14.tgz` | `20261112-uitoolkit-beta` **베타** | `ECBC292950B0139C4B05BF57DF0014BC3F8F6A5F66E7FD8655C8CBC0279028FC` |
| `com.jjevol.platform.core` | 0.1.0-pre.2 | `Packages/Frozen/com.jjevol.platform.core-0.1.0-pre.2-beta.f39bd53.tgz` | master `f39bd53` **베타** | `271F6DEFD2C74E910BEF4FB57699FB2D2B734681E7D0693252F5C10F5FAD5D0A` |
| `com.jjevol.platform.unity` | 0.1.0-pre.2 | `Packages/Frozen/com.jjevol.platform.unity-0.1.0-pre.2-beta.f39bd53.tgz` | master `f39bd53` **베타** | `06F235426F7F1DA375AF38CAC40A2498F90A39EF9AF0E4520B2921CEF0AB2F25` |
| `com.jjevol.resources.addressables` | 0.1.0-pre.3 | `Packages/Frozen/com.jjevol.resources.addressables-0.1.0-pre.3.tgz` | `20261108-bundle1` | `8FB6D140F0E1D7EF987F1AD369BC9C9436ED4F9BF869EB01E718D06EA0708540` |
| `com.jjevol.save` | 0.1.0-pre.4 | `Packages/Frozen/com.jjevol.save-0.1.0-pre.4.tgz` | `20261110-bundle4` **베타** | `07E4C459ED61D62E0BDD8AB9F2B1F588AB1BEC32A514BE713F579E335DFB2007` |
| `com.jjevol.scene` | 0.1.0-pre.2 | `Packages/Frozen/com.jjevol.scene-0.1.0-pre.2.tgz` | `20261109-bundle2` → master `f39bd53` **베타**(후보 바이트 같음) | `8D98B251CF3DF979D81B5C563B7D3A762682F133DC29DA20FF6D6DD31DBC0AA5` |
| `com.jjevol.scene.addressables` | 0.1.0-pre.2 | `Packages/Frozen/com.jjevol.scene.addressables-0.1.0-pre.2.tgz` | `20261109-bundle2` → master `f39bd53` **베타**(후보 바이트 같음) | `B9FC0C1E73AD831D8F20204C1E615F79918111D69DFDA97B1B3533CC35A32738` |
| `com.jjevol.scene.ui` | 0.1.0-pre.4 | `Packages/Frozen/com.jjevol.scene.ui-0.1.0-pre.4.tgz` | `20261112-uitoolkit-beta` **베타**(설정 범위 「베타」, 같은 버전 고정 tgz) | `18F336FF5EF05DDE399332C6FE1D1E307B60AFA4761934B6ABD175EB1D3EDE75` |
| `com.jjevol.tables` | 0.1.0-pre.3 | `Packages/Frozen/com.jjevol.tables-0.1.0-pre.3.tgz` | `20261110-bundle4` **베타** | `16B7BD7F4703B9F4B7832B5AF20BEDC68EA9A81D98BBF6EB7BE875EE880809D9` |
| `com.jjevol.tables.authoring` | 0.1.0-pre.11 | `Packages/Frozen/com.jjevol.tables.authoring-0.1.0-pre.11.tgz` | `20261110-tables-authoring-pre11` 고정(베타) | `365F1CFDF4F200AED88F521E47052DF8D7AA0C8A3CA44189A374B442345A08F8` |
| `com.jjevol.ui` | 0.1.0-pre.3 | `Packages/Frozen/com.jjevol.ui-0.1.0-pre.3.tgz` | `20261112-uitoolkit-beta` **베타** | `26F08DFD29ADF96E4D0ADD55242F6EBDBB545F45F1A6F63CC02A8ADAA728EFBA` |
| `com.jjevol.ui.addressables` | 0.1.0-pre.6 | `Packages/Frozen/com.jjevol.ui.addressables-0.1.0-pre.6.tgz` | `20261112-uitoolkit-beta` **베타** | `35BCB9A76DEE0D87F527B1A6A2774623B0017DD15AE33F05372BBD1F77010676` |
| `com.jjevol.ui.economy` | 0.1.0-pre.6 | `Packages/Frozen/com.jjevol.ui.economy-0.1.0-pre.6.tgz` | `20261112-uitoolkit-beta` **베타**(설정 범위 「베타」, 같은 버전 고정 tgz) | `7AB1200422F3074A6AB70D876282CBB2F6B22988E5EBD5BC84AE07D0D63EC371` |
| `com.jjevol.ui.urp` | 0.1.0-pre.8 | `Packages/Frozen/com.jjevol.ui.urp-0.1.0-pre.8.tgz` | `20261112-uitoolkit-beta` **베타** | `FD6A0DDB0E983DC2FB0C3D4EB7FB9CC518D876F7EE7B3154B6C099CA68863B18` |
| `com.jjevol.platform.network` | 0.1.0-pre.2 | `Packages/Frozen/com.jjevol.platform.network-0.1.0-pre.2.tgz` | `20261110-bundle4` **베타** | `4672CCA4278125D69442E2257BBE905E997BB94981BEABB01BDA511513C8BAD5` |
| `com.jjevol.platform.network.data` | 0.1.0-pre.2 | `Packages/Frozen/com.jjevol.platform.network.data-0.1.0-pre.2.tgz` | `20261110-bundle4` **베타** | `92C8B4AC9FF8B053C8DABDB080DEE42DB3AD00953BF8E91AADE01495A186654E` |
| `com.jjevol.platform.purchase.network` | 0.1.0-pre.2 | `Packages/Frozen/com.jjevol.platform.purchase.network-0.1.0-pre.2.tgz` | `20261110-bundle4` **베타** | `36262D9EBCC4A0FB2D0321F75B5D4F53CB742CE6E2FE19FB19615E1B193C7C44` |
| `com.jjevol.platform.push.firebase` | 0.1.0-pre.2 | `Packages/Frozen/com.jjevol.platform.push.firebase-0.1.0-pre.2.tgz` | `20261110-bundle4` **베타** | `EF8949D9D1DC9C13AF89ADCE9DAB468CB56C8F5E095195594D1DB753B5D0FB30` |
| `com.jjevol.platform.tracking` | 0.1.0-pre.1 | `Packages/Frozen/com.jjevol.platform.tracking-0.1.0-pre.1.tgz` | `20261109-bundle3` → master `f39bd53` **베타**(후보 바이트 같음) | `18933C391326CEB0BF15BA89DB9DCE7AED8236D1CBE37235C9CA8E6DE68CDACF` |
| `com.jjevol.platform.tracking.apple` | 0.1.0-pre.1 | `Packages/Frozen/com.jjevol.platform.tracking.apple-0.1.0-pre.1.tgz` | `20261109-bundle3` → master `f39bd53` **베타**(후보 바이트 같음) | `2F0AADA89F86390E22C4DDB38B69B312E58F98694B6CA46E14FF9C59EA13B7A7` |
| `com.jjevol.quest` | 0.1.0-pre.2 | `Packages/Frozen/com.jjevol.quest-0.1.0-pre.2.tgz` | `20261111-quest-pre2` 고정(베타) | `10002F7FBC64AED823D825CA8FCD951D1D42002F397749A9E99E6B9BD175FE74` |
| `com.jjevol.badge` | 0.1.0-pre.1 | `Packages/Frozen/com.jjevol.badge-0.1.0-pre.1.tgz` | `20261110-badge-pre1` 고정(베타) | `95D069769B3051564B9DDEB43F51C96EEA9BEB9ED59C79095C6010B710F8DAF9` |
| `com.jjevol.effects.dotween-pro` | 0.1.0-pre.2 | `Packages/Frozen/com.jjevol.effects.dotween-pro-0.1.0-pre.2.tgz` | master `f39bd53` = `20261111-addons-beta` **베타**(바이트 같음) | `6E01905B8D473C0BDECEDDF629F1A168F92B08EEFE3538E2BD9721DFF9440C14` |
| `com.jjevol.ui.dotween-pro` | 0.1.0-pre.3 | `Packages/Frozen/com.jjevol.ui.dotween-pro-0.1.0-pre.3.tgz` | `20261112-uitoolkit-beta` **베타** | `1F30D3E04B1AF6995EAAABBC7DFA48725925EC1C3E043BE224D92B69DEA73B08` |
| `com.jjevol.effects.spine` | 0.1.0-pre.3 | `Packages/Frozen/com.jjevol.effects.spine-0.1.0-pre.3.tgz` | master `f39bd53` = `20261110-bundle4` **베타**(바이트 같음) | `01E02A19CB3108B35EC81155CB6F7D789C27D6DF4FCDA76A6309DE7CCDEBB982` |
| `com.jjevol.effects.coffee` | 0.1.0-pre.2 | `Packages/Frozen/com.jjevol.effects.coffee-0.1.0-pre.2.tgz` | `20261112-uitoolkit-beta` **베타** | `7492A8A5335F3336796621FE1571247F5CE0FAA9FC8A33EC0A86A8DA9B05E329` |
| `com.jjevol.effects.ui` | 0.1.0-pre.4 | `Packages/Frozen/com.jjevol.effects.ui-0.1.0-pre.4.tgz` | `20261112-uitoolkit-beta` **베타** | `9B41DACCFDD60657BBB036B6A3458265327216EDD5235C4208A0BDE365E6A82F` |
| `com.jjevol.localization.tmp` | 0.1.0-pre.1 | `Packages/Frozen/com.jjevol.localization.tmp-0.1.0-pre.1.tgz` | `20261112-uitoolkit-beta` **베타** | `050A9C138BBB0BD85A03AA7C1BDE3C3ECDED4367656B6CF70AEB195E80348544` |
| `com.jjevol.ui.contracts` | 0.1.0-pre.1 | `Packages/Frozen/com.jjevol.ui.contracts-0.1.0-pre.1.tgz` | `20261112-uitoolkit-beta` **베타** | `8410DF1FBD5132EDEE9DEEAE6EC4D047F08E86B73C68096385E94AD4F525C7B9` |

4차 묶음(베타) 갱신: 2026-11-10, 계약 MV-00(작업 브랜치 `ai/MV-00-bundle4`), 사용자 원문(Framework 대화) 「유니티 내부 테스트만 진행하면 우선 게임에 적용가능하게 해주면 좋겠어 베타로는 내부테스트 끝난건 다 가져가서 쓰게 해주면 좋겠어」·「4번 진행」. Framework `Docs/Guide/Delivery.md` 「고정」 표(master `b6d846f`, 묶음 고정 `b3e6672`) `Snapshots/20261110-bundle4/`에서 위 표의 **베타** 13개를 바꿨다. 복사본 SHA-256은 모두 목록과 같다. 옛 tgz 13개는 쓰는 곳이 없어 지웠다. 위 표에 없던 `platform.network`·`.network.data`·`.purchase.network`·`.push.firebase`는 3차 묶음으로 연결한 것(원장 MV-00 bundle3 행)의 새 버전이다. 3차 묶음의 나머지 패키지는 그대로다. 4차 묶음에 처음 고정된 14개(`effects.spine`, `platform.network.http`, `platform.purchase.onestore`, `platform.login.google`·`.apple`, `platform.steam`, `platform.ads.adx`, `platform.analytics.appsflyer`·`.facebook`, `platform.review`·`.googleplay`, `genre.runner`·`.editor`·`.editor.addressables`)는 Framework 범위 밖이라 연결하지 않았다. 게임 코드 영향: network `SetResponseApplier`·`DefaultTimeout`, save `OriginalVersion`을 게임이 쓰지 않아 문서 주석 1곳만 고쳤다. 표 TSV 140개는 머리 행 중복·이름 없는 열·빈 행·짧은 행이 없다.

5차 묶음 갱신: 2026-11-11, 계약 MV-00-bundle5(작업 브랜치 `ai/MV-00-bundle5`), 설정 화면 저장(2026-11-11 10:59)과 사용자 원문 「프로젝트 설정으로 프래임 워크 추가된게 있을텐데 이것도 적용하면 좋겠어 추가된것만이고 제거는 없으니 연결이 빠진게 있으면 이야기해줘 그리고 아직 사용처가 없는건 설치만 하고 구현까지 진행하지는 마」. ① 새 범위 `quest` pre.2·`badge` pre.1과 고정본 갱신 `com.artress.managed-addressables` pre.18·`tables.authoring` pre.11은 Framework `Delivery.md` 「고정」 표의 베타 고정 tgz를 그대로 넣었다(SHA-256 목록과 같음). `quest`·`badge`는 게임 코드에서 아직 쓰지 않는다(연결만). ② 설정의 「베타로 쓸 패키지」 10개는 Framework master `f39bd53`(2026-11-11 03:35)에서 `Tools/Pack-FrozenPackage.py`로 후보 tgz를 포장했다. `data`·`platform.tracking`·`.tracking.apple`·`scene`·`scene.addressables`·`scene.ui` 6개는 후보가 기존 고정 tgz와 바이트가 같아 파일을 그대로 두고 표시만 **베타**로 바꿨다. `economy`·`platform.core`·`platform.unity`·`ui.economy` 4개는 버전 문자열은 같고 문서·컴파일하지 않는 `~` 폴더만 다르다(README·CHANGELOG·NOTICE, `Tests/Authoring~`·`Tests/DotNet~`·`Tests/Integration~`). 고정본과 구분하려고 `-beta.f39bd53.tgz` 이름으로 넣었다. 옛 tgz 6개(`managed-addressables` pre.17, `tables.authoring` pre.10, `economy`·`platform.core`·`platform.unity`·`ui.economy` 고정본)는 쓰는 곳이 없어 지웠다. ③ 빼고 둔 범위 패키지(외부 도구가 있어야 컴파일, 설치는 사용자 확인 대상): `effects.dotween-pro` pre.2·`ui.dotween-pro` pre.2(DOTween Pro: asmdef `precompiledReferences` `DOTween.dll`·`DOTweenPro.dll`), `effects.spine` pre.3(Spine 런타임 `spine-csharp` 4.2.37·`spine-unity` 4.2.120), `effects.coffee` pre.1(`com.coffee.ui-particle` 4.11.3). 도구가 게임에 들어오면 한 번에 더 연결한다. `tables.authoring` pre.11부터 선언하지 않은 `Name` 머리 열은 다음 표 빌드에서 `name`으로 바뀐다(Runtime 무변경).

5차 묶음 외부 도구 연결: 2026-11-11, 계약 MV-00-bundle5b(작업 브랜치 `ai/MV-00-bundle5b-external`), 사용자 원문 「외부도구 설치가능한것들 아니야? DotTweenPro는 설치되어있고 나머지는 공개된 외부툴인데」·「설치해」·「Yes, install」. 5차 묶음 ③에서 빼 둔 4개를 같은 방식(Framework master `f39bd53`에서 `Tools/Pack-FrozenPackage.py`로 포장)으로 넣었다. 포장 결과는 Framework `Delivery.md` 「고정」 표의 베타 고정 tgz(`effects.spine`은 `Snapshots/20261110-bundle4/`, 나머지 셋은 `Snapshots/20261111-addons-beta/`)와 SHA-256이 같다. 게임 코드에서는 아직 쓰지 않는다(연결만).

6차 묶음(프로젝트 설정 맞춤): 2026-11-12, 계약 MV-00-bundle6(작업 브랜치 `ai/MV-00-bundle6-settings`), 사용자 원문 「프로젝트 설정 맞춰서 적용해줄래? 프레임워크」. 설정 저장 2026-11-12 18:20(dev `3f2722c8`)의 범위대로 Framework `Docs/Guide/Delivery.md` 「고정」 tgz(`Snapshots/20261112-uitoolkit-beta`, 모두 베타 고정본)를 넣었다: 갱신 12개(위 표), 새로 `localization.tmp` 0.1.0-pre.1·`ui.contracts` 0.1.0-pre.1, 범위 밖이 된 `platform.network.firebase` 0.1.0-pre.1 제거(게임 코드·다른 패키지 의존 없음 확인). 복사본 SHA-256은 모두 목록과 같다. `ui` 0.1.0-pre.3에서 `IPopupData`·`IScreenData`가 `ui.contracts`(어셈블리 `JJEvol.UI.Contracts.Runtime`, 네임스페이스 `jjevol` 그대로)로 옮겨져 `MV.UI.asmdef`에 그 참조를 더했다(통합 Writer 파일, 총괄 없음·사용자 직접 지시). 검증: compile 잡 `4b1379444a14` PASS(후보 `c3c01d31`), EditMode 잡 `ff3e7368bd8e` 573 PASS·0 FAIL·7 Ignore(Q-04 덤프 대조, 2026-11-11 사용자 「통과로 봐」 — 실행기는 FAIL로 표시), PlayMode 잡 `69605b56638c` 7/7 PASS. 화면·기기 확인 NOT_RUN.

외부 도구(2026-11-11, MV-00-bundle5b):

| 도구 | 쓰는 패키지 | 출처 | 고정 버전·커밋 |
|---|---|---|---|
| DOTween + DOTween Pro (Demigiant) | `effects.dotween-pro`, `ui.dotween-pro` | 저장소에 이미 있음 `Assets/Plugins/Demigiant/`(`DOTween/DOTween.dll`, `DOTweenPro/DOTweenPro.dll`, 커밋 `587c6091`). 유료 Asset Store 자산이라 새로 받지 않았다 | 저장소 안 바이트 그대로. DOTween Utility 패널 「Create ASMDEF」와 같은 내용으로 asmdef 3개를 만들었다: `DOTween/Modules/DOTween.Modules.asmdef`, `DOTweenPro/DOTweenPro.Scripts.asmdef`(참조 `Unity.TextMeshPro`·`DOTween.Modules`), `DOTweenPro/Editor/DOTweenPro.EditorScripts.asmdef`(Editor 전용) |
| spine-csharp (Esoteric Software) | `effects.spine` | https://github.com/EsotericSoftware/spine-runtimes `?path=spine-csharp/src` | 4.2.37, 커밋 `b81e5a58ed38704aee4f866f0e0ac672623ce914`(Framework `SpineValidationHost`와 같음) |
| spine-unity (Esoteric Software) | `effects.spine` | https://github.com/EsotericSoftware/spine-runtimes `?path=spine-unity/Assets/Spine` | 4.2.120, 커밋 `b81e5a58ed38704aee4f866f0e0ac672623ce914` |
| UI Particle `com.coffee.ui-particle` (mob-sakai) | `effects.coffee` | https://github.com/mob-sakai/ParticleEffectForUGUI | 4.11.3, 태그 `4.11.3` = 커밋 `c54f63cb18a22382215b142b63e232f0f86fff58`(MIT) |

버전은 각 커밋의 `package.json`에서 확인했다. Spine 메모: Spine 런타임 소스는 공개지만 사용 조건은 Spine Runtimes License를 따른다. 게임에 Spine 런타임을 쓰려면 Spine 편집기(Spine Editor) 라이선스가 있어야 한다. 출시 전 회사 라이선스 보유를 확인한다.

외부 의존(2026-11-09 사용자 확인 「넣어」): `com.cysharp.unitask` Git `2e993ff18f28c931602a07292df0b0804eebef99`(2.5.11, Framework 검증 Host와 같음), `com.unity.addressables` 4.0.1, `com.unity.localization` 1.5.12, `com.unity.inputsystem` 1.18.0, `com.unity.nuget.newtonsoft-json` 3.2.2, `com.unity.render-pipelines.universal` 17.3.0, `com.unity.ugui` 2.0.0, `com.unity.test-framework` 1.6.0, IDE 연동 2개, 기본 모듈.

연결 보류(고정본 없음, 범위에는 있음): `economy.drop`, `effects.ui`, `platform.ads`·`.admob`, `platform.analytics`·`.firebase`, `platform.localnotifications`·`.unity`, `platform.login`·`.firebase`, `platform.network`·`.data`·`.firebase`, `platform.purchase`·`.network`·`.unityiap`, `platform.push`·`.firebase`, `platform.tracking`·`.apple` (20개, 모두 `com.jjevol.`). 고정본이 나오면 같은 방식으로 더한다.

검증: tgz SHA-256 대조 PASS, manifest JSON·`file:` 경로 확인 PASS. 사용자가 Unity 6000.3.9f1로 한 번 열어 `packages-lock.json` 생성(Editor.log 컴파일 오류 0). 실행기 compile 잡 `8d04cd506014` PASS(후보 `873e204`, 2026-11-09). EditMode·PlayMode·Boot 실행은 NOT_RUN(MV-00의 씬·asmdef 작업 뒤).

실행기: 2026-11-09 등록(`moonvillage`, compile·EditMode·PlayMode, 원문 「실행기에 등록해」), 위임 10일(원문 「10일 위임해」, 2026-11-19 만료).