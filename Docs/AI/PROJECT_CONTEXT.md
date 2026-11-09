# 프로젝트 문맥

<!-- jjevol-project-settings:start — 설정 화면(「프로젝트 설정」)이 만든 절. 고치려면 그 화면에서 고른다. -->
## 프로젝트 설정

저장: 2026-11-09 19:36 KST, 사용자가 설정 화면에서 고름. 정본은 `Docs/AI/ProjectSettings.json`.

- Framework 연결 방식: 외부
- 프로젝트 기본: 어떤 프로젝트인가 = 새 게임; 누구 프로젝트인가 = 회사
- 게임 형태: 2D / 3D = 2.5D; 렌더 파이프라인 = URP Universal; Unity 버전 = 6000.3.9f1; 출시 플랫폼 = Android, iOS; 화면 방향 = 세로; 입력 = 터치; UI 방식 = uGUI
- 기획과 데이터: 기획서 = 확정 기획서 있음; 기획서 위치 = Docs/MV-SPEC-01/README.md; 바꿀 게임 규칙 정리 위치 = Docs/MV-SPEC-01/04_GameRules.md; 게임 수치를 고치는 곳(정본) = Framework 표(TSV, 표 제작 도구); 지원 언어 = 한국어, 영어; 저장 = 서버 저장; 수익 = 광고, 인앱 결제
- 표·폴더 규칙: 표 규칙 = JJEvol 표 규격 v1; 쓸 보상 표 = Drop, 랜덤박스; 폴더 규칙 = JJEvol 폴더 규격 v1
- Framework: Framework 사용 = 쓴다; Framework 연결 방식 = 외부; 영역: 콘텐츠 로드 = 쓴다; 영역: 씬 전환 = 쓴다; 영역: 화면·팝업 = 쓴다; 영역: 소리·연출 = 쓴다; 영역: 저장 = 쓴다; 영역: 표 읽기·제작 = 쓴다; 영역: 언어 읽기·제작 = 쓴다; 영역: 재화(돈·아이템) = 쓴다; 영역: 플랫폼 기본 = 쓴다; 같은 범위 안 버전 갱신 = AI에게 맡김
- 더할 기능: 광고 = AdMob; 인앱 결제 = Unity 결제; 로그인 = Firebase 계정; 통계 = Firebase; 알림 = 원격 푸시, 로컬 알림; 스토어 리뷰·추적 동의 = iOS 추적 동의(ATT); 서버 통신 = 씀: Firebase 서버; 장르 기능 = 없음
- AI 권한·자동 진행: Commit = 검증 통과하면 묻지 않고 Commit; 병합 = 테스트 브랜치에 병합, main은 내가 직접; 테스트 브랜치 이름 = dev; AI가 Unity로 돌릴 수 있는 것 = EditMode 시험, 컴파일, PlayMode 시험; QA 대상 = Android 에뮬레이터; 병렬 담당 = 자동; 외부 패키지·SDK 설치 = 매번 확인; 같은 문제 자동 수정 횟수 = 2번
- 아트·품질: 아트 공급 = 임시 아트로 먼저; 목표 FPS = 60
- Framework 범위(함께 필요한 패키지 포함): `com.artress.managed-addressables` (고정 1.3.12-pre.10), `com.jjevol.audio` (고정 0.1.0-pre.3), `com.jjevol.audio.addressables` (고정 0.1.0-pre.2), `com.jjevol.data` (고정 0.1.0-pre.1), `com.jjevol.economy` (고정 0.1.0-pre.4), `com.jjevol.economy.drop` (시험 중), `com.jjevol.effects` (고정 0.1.0-pre.2), `com.jjevol.effects.addressables` (고정 0.1.0-pre.2), `com.jjevol.effects.ui` (시험 중), `com.jjevol.localization` (고정 0.1.0-pre.3), `com.jjevol.localization.authoring` (고정 0.1.0-pre.9), `com.jjevol.platform.ads` (시험 중), `com.jjevol.platform.ads.admob` (시험 중), `com.jjevol.platform.analytics` (시험 중), `com.jjevol.platform.analytics.firebase` (시험 중), `com.jjevol.platform.core` (고정 0.1.0-pre.2), `com.jjevol.platform.localnotifications` (시험 중), `com.jjevol.platform.localnotifications.unity` (시험 중), `com.jjevol.platform.login` (시험 중), `com.jjevol.platform.login.firebase` (시험 중), `com.jjevol.platform.network` (시험 중), `com.jjevol.platform.network.data` (시험 중), `com.jjevol.platform.network.firebase` (시험 중), `com.jjevol.platform.purchase` (시험 중), `com.jjevol.platform.purchase.network` (시험 중), `com.jjevol.platform.purchase.unityiap` (시험 중), `com.jjevol.platform.push` (시험 중), `com.jjevol.platform.push.firebase` (시험 중), `com.jjevol.platform.tracking` (시험 중), `com.jjevol.platform.tracking.apple` (시험 중), `com.jjevol.platform.unity` (고정 0.1.0-pre.2), `com.jjevol.resources.addressables` (고정 0.1.0-pre.3), `com.jjevol.save` (고정 0.1.0-pre.3), `com.jjevol.scene` (고정 0.1.0-pre.2), `com.jjevol.scene.addressables` (고정 0.1.0-pre.2), `com.jjevol.scene.ui` (고정 0.1.0-pre.3), `com.jjevol.tables` (고정 0.1.0-pre.2), `com.jjevol.tables.authoring` (고정 0.1.0-pre.7), `com.jjevol.ui` (고정 0.1.0-pre.2), `com.jjevol.ui.addressables` (고정 0.1.0-pre.4), `com.jjevol.ui.economy` (고정 0.1.0-pre.5), `com.jjevol.ui.urp` (고정 0.1.0-pre.6)
- 확인 필요: 외부 연결은 고정본만 넣는다. 고정본이 없어 연결을 보류할 패키지: com.jjevol.economy.drop, com.jjevol.effects.ui, com.jjevol.platform.ads, com.jjevol.platform.ads.admob, com.jjevol.platform.analytics, com.jjevol.platform.analytics.firebase, com.jjevol.platform.localnotifications, com.jjevol.platform.localnotifications.unity, com.jjevol.platform.login, com.jjevol.platform.login.firebase, com.jjevol.platform.network, com.jjevol.platform.network.data, com.jjevol.platform.network.firebase, com.jjevol.platform.purchase, com.jjevol.platform.purchase.network, com.jjevol.platform.purchase.unityiap, com.jjevol.platform.push, com.jjevol.platform.push.firebase, com.jjevol.platform.tracking, com.jjevol.platform.tracking.apple

### AI가 따를 규칙

사용자가 설정 화면에서 고른 값이다. 이 저장소의 규칙 파일(AGENTS.md 등)과 다르면 그 파일이 먼저다.

- 검증을 통과한 자기 작업 브랜치 변경은 묻지 않고 Commit한다.
- AI 결과는 `dev`에 병합한다. `main` 반영과 Push는 사용자가 그 회차에 직접 말할 때만 한다.
- AI가 Unity로 돌리는 것은 EditMode, compile, PlayMode뿐이다(실행기가 그 밖은 거절한다).
- 병렬 담당: 쓰기 파일이 겹치지 않는 작업마다 담당 1명.
- 외부 패키지·SDK 설치는 매번 확인한다.
- 같은 문제의 자동 수정은 2번까지. 넘으면 원인과 선택지를 보고하고 멈춘다.

### 적용 상태

- Commit·병합: 대화가 따름 — 「AI가 따를 규칙」으로 PROJECT_CONTEXT.md에 들어가 대화가 시작할 때 읽는다
- AI의 Unity 실행: 도구가 따름 — 실행기(`unity_job.py submit`)가 고르지 않은 compile·EditMode·PlayMode를 거절한다
- 실행기 등록: 다음 단계 필요 — 이 폴더는 실행기에 등록되지 않았다. 사용자가 「실행기에 등록해」라고 하면 `unity_job.py register`로 등록한다
- Framework 범위: 다음 단계 필요 — manifest에 아직 없는 패키지 42개. 「Framework 연결해줘」(`jjevol-framework`)로 연결한다
- 담당 수·설치·수정 횟수: 대화가 따름 — 「AI가 따를 규칙」으로 대화가 따른다

<!-- jjevol-project-settings:end -->
