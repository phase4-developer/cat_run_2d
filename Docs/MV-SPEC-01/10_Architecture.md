# 10. 아키텍처

[입구](README.md) · 앞: [09 저장](09_Save.md) · 다음: [11 작업 계약](11_WorkContracts.md)

이 절은 모듈 경계, 씬 구성, Framework 범위와 대응표, 폴더·Addressables 구조, 표 로딩 흐름을 정한다. 시각 규칙은 [02](02_Visual.md), 표 정의는 [07](07_Tables.md)이 정본이고 여기서는 연결만 적는다.

표기: **확정**(사용자 결정), **권장**(결정 대기 중 기준안), **게임 구현**(Framework에 없음, 이유 포함).

## 10.1 전제

| 항목 | 값 | 상태 |
|---|---|---|
| Unity | 6000.3.9f1 (Framework 고정본 기준 버전, `Installation.md`) | 권장 |
| 렌더 파이프라인 | URP. `ui.urp`·`scene.ui`가 URP를 전제로 한다(`PackageMap.md` 「의존 관계」) | 권장 |
| UI | **UGUI**(uGUI). UI Toolkit은 쓰지 않는다 | 확정(2026-11-09 사용자 「ui는 UGUI」) |
| UI 이미지 | **`AddressableImage`(아틀라스 키+스프라이트 이름), `AddressableTexture`(단독 텍스처)** | 확정(2026-11-09 사용자 「AddressableImage, AddressableTexture 사용할거야」) |
| 입력 | Input System(드래그·키보드). 프로토 입력 규칙은 [04 §4.1](04_GameRules.md) | 권장 |
| 글꼴 | TMP. Black Han Sans(제목·숫자), Gothic A1(본문) — 프로토와 같은 글꼴. 라이선스는 둘 다 SIL OFL [확인 필요] | 권장 |
| 플랫폼 | Android 우선, iOS 다음. 세로 9:16 기준 | 권장 |
| 결제·서버·광고 SDK | 스텁 지점만 둔다. 실제 SDK 연결은 2차 | 확정(HellEscape 범위 결정 2026-11-07 「결제와 서버는 스텁」 승계) |

## 10.2 Framework 범위

사용자 지정(2026-11-09 원문 「프레임 워크는 ui, addressable, 사운드, 이펙트, 모바일 플랫폼, 로그인, 통신, 광고, 통계툴이 들어가야하고 빼먹은게 있으면 좀 첨언하고 추가해줘」). 아래 표의 「요청」 열이 사용자 지정분이고, 「보강」은 이 기획서가 필요하다고 판단해 더한 것이다. 버전·상태는 Framework `Docs/Guide/Delivery.md`(2026-11-09, master `51d80c2`) 기준이다.

| 묶음 | 패키지(`com.jjevol.` 생략) | 요청/보강 | 쓰는 곳 | 전달 상태 |
|---|---|---|---|---|
| UI | ui, ui.addressables, ui.urp | 요청 | 모든 화면·팝업·HUD, `AddressableImage`·`AddressableTexture`, 버튼 모듈 | 고정(1차 묶음) |
| UI | ui.economy | 보강 | 재화 칩(츄르·캣닢·생선·목숨 조각), 가격 버튼 | 고정 |
| UI | ui.dotween-pro | 보강 후보 | 팝업 전환. **DOTween Pro 라이선스가 필요**하므로 결정 대기(D-07) | 시험 중 |
| Addressables | resources.addressables | 요청 | 런타임 로드·반환 `AddressablesManager`·`AddressableScope` | 고정 |
| Addressables | `com.artress.managed-addressables` | 요청 | Boot/Revision 제작, 앱 포함 A. 원격 콘텐츠 교체(테마 팩 추가 배포) | 고정 pre.10 |
| 씬 | scene, scene.addressables, scene.ui | 보강 | Boot→Lobby→Run 씬 전환, 로딩·페이드 | 시험 중 |
| 사운드 | audio, audio.addressables | 요청 | 효과음 30클립·BGM 2개(`03_sound` 32행), 볼륨·음소거 | audio 고정 pre.2 / audio.addressables 시험 중 |
| 이펙트 | effects, effects.addressables | 요청 | 파티클·스프라이트 이펙트 풀 | effects 고정 / .addressables 시험 중 |
| 이펙트 | effects.ui | 보강 | 토스트, HUD로 날아가는 아이콘 | 시험 중 |
| 데이터 | save, data | 보강(필수) | 진행 저장(프로토 `save` 전체), Container 분리 | 고정 |
| 데이터 | economy | 보강 | 재화 정본 `ResourceService`, `WealthTable`(`01_wealth`) | 고정 |
| 데이터 | tables, tables.authoring | 보강(필수) | 모든 수치·콘텐츠 표. 사용자 요구 「테이블 구조와 연결해서 세부 수치들을 조정가능」 | tables 고정 / authoring 시험 중 |
| 다국어 | localization, localization.authoring | 보강 | 모든 문구를 키로. 한국어 기본, 영어 2차 | localization 고정 / authoring 시험 중 |
| 모바일 플랫폼 | platform.core, platform.unity | 요청 | 진동(타격·계단·결과 [선택]), Deep Link | 고정 |
| 로그인 | platform.login, .google, .apple, .firebase | 요청 | 계정 연결(게스트 → Google/Apple). 서버 저장 전 단계 | 시험 중. SDK 필요 |
| 통신 | platform.network, .http 또는 .firebase, .network.data | 요청 | 서버 저장·시간·원격 설정. 1차는 **`SetLocalService`로 로컬 스텁**, 공급자 선택은 결정 대기(D-08) | 시험 중 |
| 광고 | platform.ads, .adx 또는 .admob | 요청 | 보상형(×3 보상, Area 강화 무료, 부활 70분 스킵, 일일 캣닢), 전면(Area 4 이후) | 시험 중. HellEscape는 ADX 사용 |
| 통계 | platform.analytics, .firebase, .appsflyer | 요청 | 런 시작·종료·Area 개방·광고·결제 이벤트 | 시험 중. SDK 필요 |
| 결제 | platform.purchase (+ .unityiap 2차) | **보강** | 상점·오퍼([08](08_Content.md))가 있으므로 관리자 계약을 1차부터 쓰고 공급자는 스텁. 나중에 SDK만 붙인다 | 시험 중 |
| 로컬 알림 | platform.localnotifications, .unity | **보강** | 「아홉 목숨(부활) 쿨 6시간 끝」, 연구·상자 타이머 완료 알림 | 시험 중 |
| 추적 동의 | platform.tracking, .apple | **보강** | iOS에서 광고 SDK를 쓰려면 ATT 동의가 필요하다 | 시험 중 |
| 리뷰 | platform.review, .googleplay | **보강(선택)** | Area 3 개방 등 만족 지점의 리뷰 팝업 | 시험 중 |
| Push | platform.push, .firebase | 보강 후보 | 원격 이벤트 알림. 서버 운영이 정해질 때 | 시험 중. 2차 |

쓰지 않는 패키지와 이유:

| 패키지 | 이유 |
|---|---|
| genre.runner 계열 | 코스를 미리 Bake해 `RunnerSession`으로 진행하는 구조다(`Features/Runner.md`). 이 게임의 런은 Level·Area·체크포인트·장애물 HP 지속에 따라 매번 표에서 생성하고, 판정이 레인 x 거리 비교(프로토 `updRun`)라서 코스 Bake·접촉 경로 모델과 맞지 않는다. `Framework 미사용: Features/Runner.md, genre.runner README — 런타임 표 기반 생성과 레인 판정이 Bake 코스 모델과 다름` |
| effects.spine, effects.coffee | 스파인·Coffee UIParticle을 쓰지 않는다 |
| platform.steam | 모바일 전용 |

**연결 방식에 따른 제약.** 「외부」(고정 tgz)로 정하면 위 표의 「시험 중」 패키지는 고정본이 나올 때까지 연결할 수 없다(`profiles/game.detail.md` 「Framework 전달 경로」). 이 경우 1차 개발은 고정 13개+save+managed로 하고, 씬·로그인·통신·광고·통계·결제·알림은 **게임 쪽 인터페이스 뒤의 스텁**으로 시작한 뒤 고정본이 나오면 교체한다(작업 계약 [11](11_WorkContracts.md) MV-PLAT-*). 「내부」면 모두 폴더 연결로 바로 쓴다. 결정 D-02.

## 10.3 씬 구성

| 씬 | 내용 | 시작 담당 |
|---|---|---|
| `Boot` (Build 0) | `GameBootstrap`, `UIManagerCreater`(+자식 EventSystem), Main Camera(URP Base, Tag MainCamera), `AudioManager`, `EffectManager` | Framework `NewGame.md` 5~6단계 순서 그대로 |
| `Game` | 런과 로비를 **한 씬**에서 처리한다. 프로토도 로비→런 전환이 씬 교체 없이 즉시다(화면을 누르면 바로 출발). `WorldRoot`(월드 렌더), `GameCamera`(FOV 52 원근), `LobbyStage`(아이소메트릭 아지트) | `GameInitializer : SceneInitializerBase` |
| `Mode_*` (선택) | 추가 콘텐츠 모드 중 규칙이 크게 다른 것(예: 골목 수비전 = Hell War류)은 별도 씬. Addressable 씬 | `ModeInitializer` |

이유: 로비에서 탭하면 0프레임 안에 런이 시작되고 카메라가 로비 시점에서 런 시점으로 보간된다(프로토 `syncScene` L2746~, 로비 `camPos=(0,4.2,-4.4)` 시선 `(0,0.2,3.4)`). 씬을 나누면 이 감각을 재현할 수 없다.

## 10.4 모듈 경계 (asmdef)

| asmdef | 책임 | 의존 | 상태 Owner |
|---|---|---|---|
| `MV.Core` | 공통 타입(게임 좌표 `GVec`, ID 구조체, 난수 `IRng`, 시간 `IClock`), 상수 키 | 없음 | — |
| `MV.Data` | 표 행·표 클래스·검증기·`MvTables`(모든 표 묶음), 리소스 키 해석 | tables, MV.Core | 정적 정의 |
| `MV.Save` | `DataContainer` 구현(진행·재화·강화·설정), 이관 | data, save, economy, MV.Data | 영속 상태 |
| `MV.Run` | **순수 C# 런 시뮬레이션.** Floor 생성, 이동·사격·피격·게이트·우물·계단·카드·적·종료 판정. 렌더·UI·Unity API 없음(시간·입력·난수는 주입) | MV.Core, MV.Data | Run 상태 |
| `MV.Meta` | 로비 강화, Area 강화, 부활, 신·카드 덱, Area 개방, 지도, 광고 한도, 결과 정산 | MV.Data, MV.Save | 영속 상태 변경의 유일한 진입점 |
| `MV.Content` | 추가 모드·퀘스트·펫·연구·챔피언·장비·상점(규칙). 모드는 `IModeRule` 구현 | MV.Run, MV.Meta | 모드별 Run 상태 |
| `MV.View` | 월드 렌더: 카메라 리그, 빌보드·정렬·안개·밤·틴트, 지면·계단 메쉬, 테마 소품 배치, 오브젝트 뷰 | MV.Run(읽기), resources.addressables, URP | 표시 상태만 |
| `MV.Fx` | 이펙트·팝업·흔들림·히트스톱 요청을 `EffectManager`·자체 풀로 재생 | effects, MV.View | — |
| `MV.Sound` | 사운드 키 → `AudioManager`, 최소 간격 GAP 관리 | audio | — |
| `MV.UI` | UGUI 화면·팝업·HUD(`UIScreen`/`UIPopup`/`UIHud`) | ui.*, ui.economy, MV.Meta(명령), MV.Run(읽기) | 화면 상태만 |
| `MV.Platform` | 광고·로그인·통신·통계·결제·알림·추적 동의의 게임 인터페이스와 스텁/공급자 연결 | platform.* | — |
| `MV.Boot` | 시작 순서, 표 등록, 저장 열기 | 전부 | — |
| `MV.Editor` | 표 검증기 등록(`TableAuthoringOptions`), 리소스 임포트 후처리, 캡처 도구 | tables.authoring, Editor | — |

규칙:
- `MV.Run`은 Unity를 참조하지 않는다. 그래야 프로토 HTML과 같은 입력으로 같은 결과를 내는지 EditMode 시험으로 대조할 수 있다([12](12_Acceptance.md) 로직 대조).
- 영속 상태는 `MV.Meta`만 바꾼다. UI는 명령을 호출하고 `DataRepository.Changed`로 다시 그린다(Framework `UIBase.SetDirty`).
- 런 중 표시 효과(흔들림·팝업·이펙트·소리)는 `MV.Run`이 이벤트(`RunEvent` 큐: `Hit`, `Break`, `GateFill`, `GateTake`, `GateMiss`, `Steal`, `ClimbStart` …)로 내보내고 `MV.Fx`·`MV.Sound`·`MV.UI`가 구독한다.

## 10.5 프로토 함수 → Framework 타입 또는 게임 구현

줄 번호는 `MoonVillage.html`(SHA256은 [01](01_Scope.md)). 「게임 구현」은 Framework에 해당 기능이 없다는 뜻이고 이유를 적는다.

| 프로토 함수/시스템 (줄) | 대상 | 비고 |
|---|---|---|
| `KEY`, `DEF`, `load`, `persist`, `save` (L843~852) | `LocalSaveRepository` + `DataRepository` + `RepositoryStorageAdapter`(`LocalAuthority`) | [09](09_Save.md) |
| 재화 `gold`/`dia`/`souls`/`tok` 증감 | `ResourceService`, `LocalRewardService` | 수령 ID 중복 방지는 게임(`MV.Meta`) |
| 상수 표 `PLACE`~`STAIR_C`, `AREA_*`, `FINAL`, `WEAPONS`, `TIERS`, `GODS`, `UPS`, `AUP`, `RBP`, `MAP_FEATS`, `WARP_ST` 등 | `IndexedTable`/`KeyedTable`/`GroupedTable` 행 클래스 + 게임 검증기. 표 구조는 정크버스터즈 방식(번호 대역 `NNN_camelCase`, 정수 id, 보상·비용 consumeData 3열) | [07](07_Tables.md) |
| 보상·비용(`gold += …`, `dia += …`, 비용 차감 등 프로토 하드코딩) | 게임 `ConsumeData`(MV.Data) + `07_consumeType` 표 → 재화는 `ResourceService` | [07](07_Tables.md). 정크버스터즈 `ConsumeData.ImportDatas` 규칙 |
| DOM 화면 `#lobby`, `#result`, `#godPanel`, `#aupPanel`, `#rbPanel`, `#mapPanel`, `#unlockOv`, `#adFake`, `#dbg` | `UIScreen`/`UIPopup` + `UIManager` | [05](05_Screens.md) |
| `#hud`와 하위, `#pops`, `#hc`, `#ms`, `#roul` | `UIHud` + 게임 위젯 | 데미지 숫자 풀은 게임 구현(최대 60, Framework에 숫자 팝업 풀 없음. `effects.ui` Toast는 1개 메시지용) |
| `toast`, `banner` (L2448~2453) | `effects.ui` Toast(토스트), 배너는 게임 `UIHud` 위젯 | 배너는 위치·글꼴이 고정이라 게임 위젯 |
| `flyTo`, `flyTo2`, `flyToWorld` (L1712~1743) | `effects.ui` `EffectIconBehaviour` 기반 + 게임 경로 계산 | 월드→화면 변환은 `MV.View` |
| `SND` (L1622~1699) | `AudioManager.PlayAsync(key, AudioTrack.Sfx)`, `PlayMusicAsync` | 합성음은 파일로 굽는다([03 §3.9](03_ResourcePipeline.md)). 최소 간격 GAP은 게임(`MV.Sound`) |
| `FX`, `sparks`, `debris`, `boom`, `dust`, `impact`, `trail`, `bolt`, `frostHit` (L1542~1621) | `EffectManager.Play/PlayAsync` + 프리팹(파티클/스프라이트) | [06](06_Effects.md) |
| `shake`, `hitstop` (L1602~1604) | 게임 구현(`MV.View` 카메라 리그, `MV.Run` 시간 배율) | Framework에 카메라 흔들림·히트스톱 없음 |
| `pj`, `drawBox`, `fogK`, `shadeC`, `mixC`, 깊이 정렬 (L2792~2826) | 게임 구현(`MV.View`) — URP 원근 카메라 + 빌보드 + 커스텀 셰이더 | [02](02_Visual.md). 2.5D 투영 재현은 장르 고유 |
| `mochi`, `drawJunk`, `drawWell`, `drawMouse`, `drawRollGate`, `drawProp`, `drawAzit` 등 그리기 | 추출 PNG → 아틀라스 → `SpriteRenderer`(월드) / `AddressableImage`(UI) | [03](03_ResourcePipeline.md) |
| `fakeAd`, `adDay` (L2419~2423) | `AdManager.ShowRewardAd/ShowInterstitialAd` + 게임 한도 표 | 공급자 없으면 `Unsupported` → 개발 빌드는 「가짜 광고」 팝업으로 대체(프로토 동작 유지) |
| `#dbg` 테스트 패널 (L715~728) | 게임 구현 `DebugPanelPopup`(개발 빌드만) | |
| `buildLevel`, `makeObjs`, `fieldSpec`, `hit`, `updRun`, `startClimb`… | 게임 구현(`MV.Run`) | Framework 미사용: genre.runner — 10.2 이유 |
| `renderLobbyUI`의 강화·덱 머지 연출, `HAND` 손패 머지 (L1157~1224) | `UIBase` + 게임 위젯, 시간 연출은 게임 코드 | DOTween Pro 미보유 시 자체 보간 |
| 언어 문구(한국어 하드코딩 전부) | `LocalizationManager.GetLoc`, `LocalizeTmpTextEvent`, 표 문구는 `TableRuntimeOptions.Localization` | 키 규칙 [07 §7.12](07_Tables.md) |
| 시계(부활 쿨·광고 일자) | `IClock`(게임). 서버 시간은 통신 스텁 뒤 | 기기 시계 조작 방지는 서버 연결 뒤 |

## 10.6 폴더와 Addressables

Managed Addressables 폴더 규칙(`Features/Addressables.md`)을 따른다. Content Root는 `Assets/Game/Addressables`.

```text
Assets/Game/
  Addressables/
    Local/
      UI/            UIManager, 로딩 화면, 타이틀(앱 포함 최소)
      Font/          타이틀 전용 Static TMP 폰트(Boot 전용, 게임 폰트와 분리)
    Remote/
      UI/            UIScreen_*, UIPopup_*, UIHud_* 프리팹
      UIAtlas/       UI 아틀라스(SpriteAtlas) — [03 §3.8]
      WorldAtlas/    월드 아틀라스(공용·테마별) — [03 §3.8]
      Texture/       AddressableTexture 단독 텍스처(아지트 배경 등)
      Audio/Sfx/     효과음 30클립
      Audio/Music/   BGM
      Effect/        이펙트 프리팹
      Mode/          모드 씬·프리팹
      Theme/Update/<ThemeId>/   테마 팩(추가 Area 테마를 Revision으로 배포)
  Art/Source/        추출 원본 PNG(아틀라스 입력, Addressables 밖)
  GameData/Table/TSV/        표 원본(편집 정본) — [07]
  GameData/Table/Resources/  tables.authoring 출력
  Scripts/<asmdef>/
  Scenes/            Boot, Game
```

- 리소스는 **각 표의 리소스 열에서 설정한다**(정크버스터즈 방식, 2026-11-09 사용자 「리소스도 테이블에서 설정가능해야해」). `icon`·`sprite`·`bg`·`bgm`·`hitSound`·`hitEffect` 같은 열에 스프라이트 이름 또는 Addressables 주소를 직접 쓴다. 아틀라스는 행의 `atlas`/`iconAtlas` 열 → `07_consumeType.iconAtlas` → 표 정의의 기본 아틀라스 순으로 정한다. 월드 테마 변형은 스프라이트 이름은 같게 두고 `304_theme.worldAtlas`만 바꾼다. 규칙 전체는 [07 §7.10](07_Tables.md).
- `UIManager` 프리팹 주소는 `UIManager`, 화면은 `UIScreen_{ID}` 등 Framework 규칙(`Features/UI.md`).

## 10.7 시작과 표 로딩 흐름

Framework `NewGame.md` 6단계 순서를 따르고 게임 단계를 끼운다.

1. `Addressables.InitializeAsync` → `ManagedPackagedRevisionLoader.LoadAsync` → `OpenForApplicationAsync` 성공 확인(실패 시 복구 화면, 게임 구현).
2. `AddressablesManager.Instance.InitAsync()`.
3. `AudioManager.Instance.SetProvider(ProviderKind.Addressables)`, `EffectManager.Instance.SetPrefabProvider(new AddressablesEffectPrefabProvider())`.
4. `GameTableSource.Configure(new PackagedTableSource(useEncrypt: false))` → `MvTables.RegisterAll()`(모든 표 `TableRepository.Register`) → `TableRepository.LoadAllAsync()` → **게임 검증기 실행**(`MvTableValidator.ValidateRuntime`, 실패 시 개발 빌드는 오류 화면, 출시 빌드는 로그+안전 기본값 없음=시작 중단).
5. `ResourceService` 생성(표를 읽은 뒤, `WealthTable` 복사 때문). 고정본 Framework는 `ResourceService`가 `WealthContainer`를 요구하므로, 실제로는 6단계에서 저장을 연 **뒤에** 붙이고 저장을 다시 열면(`MvSave.CurrentChanged`) 다시 붙인다(MV-G1-INT).
6. 저장: `LocalSaveRepository(profileId, codec, "moonvillage.progress.v1")` → **게임 스키마 이관(`MvSaveMigrator`)** → `DataRepository`에 컨테이너 등록 → `RepositoryStorageAdapter.Open(..., LocalAuthority, InitialJson)` → 참조 정리(`MvSaveReferenceFix`). 이관은 반드시 Open **앞**이다. Adapter는 스키마 id가 다르면 예외를 던지고 스스로 이관하지 않는다([09 §9.5.2](09_Save.md)).
7. `LocalizationRuntimeOptions.ReadLanguage/SaveLanguage` → `InitLocalization()`.
8. `UIRuntimeOptions.Localize`, `UINavigationOptions.ConfirmExitAsync` → `ui.StartInitAsync()`.
9. 플랫폼 공급자 등록(10.2. 스텁 포함) → `AdManager.Initialize()` 등.
10. `CustomSceneManager.Instance.LoadSceneAsync("Game")` → `GameInitializer`가 로비 화면을 연다.

표 핫리로드(개발 빌드): 테스트 패널의 「표 다시 읽기」가 각 표의 `LoadDatas()`를 다시 부르고 검증기를 돌린다. 런 중에는 막고 로비에서만 허용한다.
