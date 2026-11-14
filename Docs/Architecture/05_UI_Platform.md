# 05. 화면과 플랫폼 (MV.UI · MV.Platform)

기준 커밋: `dev` 046c8be6 (2026-11-15). 경로는 저장소 루트 기준이고 `파일:줄`은 그 커밋의 줄 번호다. Framework 줄 번호는 `Library/PackageCache` 풀린 본 기준이라 참고용이다. 전체 지도는 [README.md](README.md).
조사 범위: `Assets/Game/Scripts/MV.UI`, `Assets/Game/Scripts/MV.Platform`, `Assets/Game/Addressables/Local/UI`·`UIAtlas`, 그리고 흐름을 잇는 데 필요한 만큼 `MV.Boot`(GameBootstrap·MvBootMeta·MvGameLoop)와 `MV.Meta`(MvMeta·MvMetaRequest·MvLocalMetaBackend·MvMetaPorts).
Framework UI 패키지: `com.jjevol.ui` 0.1.0-pre.3, `com.jjevol.ui.addressables` 0.1.0-pre.6, `com.jjevol.ui.contracts`, `com.jjevol.ui.urp`. 「Framework」 줄 번호는 `Library/PackageCache` 풀린 본 기준이다.

경로 약어: `UI/` = `Assets/Game/Scripts/MV.UI/`, `PF/` = `Assets/Game/Scripts/MV.Platform/`, `BOOT/` = `Assets/Game/Scripts/MV.Boot/`, `META/` = `Assets/Game/Scripts/MV.Meta/`, `FW/` = `Library/PackageCache/com.jjevol.ui.addressables@952d405d47c4/Runtime/`.

---

## 0. 한눈에 보는 핵심 구조

1. **화면 틀은 Framework `jjevol` UI(ui.addressables)**. 화면 종류 3가지: Screen(`UIScreen`), Popup(`UIPopup`), HUD(`UIHudBase`). 열 때는 **문자열 ID**만 넘기고 Framework가 `접두어 + ID`를 Addressables 주소로 써서 프리팹을 인스턴스로 만든다(`UIScreen_`·`UIPopup_`·`UIHud_`, ui.contracts `UIAddress.cs:10-12`). ID 상수는 `UI/Common/MvUiIds.cs:6-35`.
2. **모든 화면 열기는 정적 클래스 `MvUiNav` 한 곳**(`UI/Common/MvUiNav.cs`). 게임 루프(`BOOT/MvGameLoop.cs`)와 로비 화면이 이것만 부른다.
3. **화면 하나 = 3층 구조(D-92)**: 컨트롤러(Framework 타입 상속 MonoBehaviour, 열림·닫힘·소리·토스트만) → 화면 코드 Presenter(순수 C# 클래스, 표·메타에서 모델을 만들고 명령을 부름) → 겉 View(인터페이스 구현 MonoBehaviour, 받은 값만 그림). 컨트롤러는 `[SerializeField] MonoBehaviour view`를 `IXxxView`로 캐스팅해 묶는다.
4. **기본 겉은 전부 「코드로 짜는 더미 겉」**. 프리팹에는 컨트롤러 + 겉 컴포넌트 2개만 있고(GUID로 확인), 겉의 직렬화 참조가 비어 있으면 `Awake → EnsureBuilt → Build`가 자식을 코드로 만든다(`MvUi` 도구 사용). 글꼴은 uGUI 내장 `LegacyRuntime.ttf`(`UI/Common/MvUi.cs:32`), TMP 아님.
5. **메타 화면 8종은 공통 틀 1개**: `MetaPanelPopup`(컨트롤러 추상) + `MetaPanelPresenter`(화면 코드 추상) + `MetaPanelView`(겉) + `MetaPanelModel`(그릴 값). 새 메타 화면 = Popup 하위 1개 + Presenter 하위 1개 + 프리팹 1개.
6. **UI가 게임 상태를 읽는 길은 「Port」 인터페이스**: `IMetaUiPort`(기본 `MvMetaUiPort` → `MvMeta.Current`), `IQuestUiPort`(→ `MvQuest.Current`), `IResearchUiPort`(→ `MvMeta.Current`+`MvResearch`), `IContentMenuPort`, `ILobbySource`(→ `MvSave.Current`), `IRunHudSource`(→ `RunState`). 시험은 가짜 Port를 넣는다.
7. **UI가 상태를 바꾸는 길은 `MvMeta` 명령 → `MvMetaRequest` → `IMvMetaBackend.Handle`(로컬 백엔드 `MvLocalMetaBackend`) → `MvMetaResponse.Data`를 `MvSaveStore.ApplyAndSave`로 한 번에 저장**(`META/MvMeta.cs:67-94`). **현재 메타 요청은 platform.network `NetworkManager`를 타지 않는다** — `MvMetaRequest`는 `NetworkRequest`와 「같은 모양」인 별도 클래스이고 전환은 예정(`META/MvMetaRequest.cs:6-10`). `NetworkManager`+`MvLocalNetworkService`에 실제로 등록된 요청은 점검용 `net.ping` 하나뿐(`BOOT/GameBootstrap.cs:115-116`).
8. **UI는 변경 이벤트를 구독하지 않고 다시 그린다(폴링)**: 로비는 `LateUpdate`마다 저장값을 비교(`UI/Lobby/LobbyScreen.cs:105-137`), HUD는 `LateUpdate`마다 `RunHudPresenter.Tick`(`UI/Hud/RunHud.cs:80-85`), 메타 패널은 누를 때마다 `Draw()` + 일부 화면은 1초마다 `Tick`. `MvMeta.Applied` 이벤트는 UI에서 쓰지 않는다(시험만 사용).
9. **광고·로그인 Platform 코드(`MvAds`·`MvLogin`)는 게임에 연결돼 있지 않다**(D-90, `BOOT/GameBootstrap.cs:27,111`). 실제 광고 경로는 `MvMeta.Ads`(MV.Meta `IMvAdService`, 기본 `MvFakeAdService`) + UI의 가짜 광고 팝업 `UIPopup_FakeAd`. 로그인은 Firebase가 아니라 로컬 게스트 스텁(`MvLocalAccountBackend`).
10. **빨간 점(배지)·토스트·다국어는 아직 자리만 있다**: 배지 패키지(`com.jjevol.badge`) 미사용, 빨간 점은 문자열 `<color=#ff4d4d>●</color>`(로비 퀘스트)·탭 부제 `●`(퀘스트 탭). 토스트는 `MvUiToast.Handler`가 어디서도 설정되지 않아 `Debug.Log("[Toast] …")`로만 나간다. 다국어는 `UIRuntimeOptions.Localize = (c,k)=>k`라 모든 문구가 코드 안 한국어 기본값.

---

## 1. Framework UI(jjevol) — 이 프로젝트가 쓰는 API만

### 1.1 시작
- `UIManagerCreater`(ui.urp, `Boot.unity`에 배치, `uiManagerAddress: UIManager`, `cashScreenTypes: []`, `cashHudTypes: []` — 미리 캐시하는 화면 없음). Boot 8단계에서 `MvUiBoot.InitAsync(ui)` → `creator.StartInitAsync()`(`UI/Common/MvUiBoot.cs:21-25`, 호출 `BOOT/GameBootstrap.cs:108-109`). `DontDestroyOnLoad`로 씬을 넘어 살아남고(`GameBootstrap.cs:74`), Game 씬 루프가 `ui.StartInitAsync(_ct)`로 끝을 다시 기다린다(`BOOT/MvGameLoop.cs:119-120`).
- `UIManager.prefab`(Addressables `Local/UI/UIManager.prefab`)이 Framework `UIManager`를 띄운다. 이 프리팹에 붙은 프로젝트 스크립트는 `StageFitter`뿐(GUID 확인). `UIManager.Init()`이 `screenManager`·`popupManager`·`hudManager`를 만든다(`FW/UIManager.cs:276-299`).
- 정적 옵션(`MvUiBoot.ConfigureOptions`, `UI/Common/MvUiBoot.cs:13-19`): `UIRuntimeOptions.Localize = (collection,key)=>key`(다국어 미연결), `UINavigationOptions.ConfirmExitAsync = _ => true`(뒤로 가기 종료 확인 팝업 없이 종료 허용; Framework는 이 값이 없으면 예외 — `FW/UIManager.cs:685`).
- UI 카메라: 게임 루프가 `GameBootstrap.Ui.mainCamera = 월드 카메라`로 바꿔 끼운다(`BOOT/MvGameLoop.cs:194-199`).

### 1.2 주소 규칙(코드 → 프리팹)
`UIAddress.ScreenPrefix="UIScreen_"`, `PopupPrefix="UIPopup_"`, `HudPrefix="UIHud_"`(ui.contracts `UIAddress.cs:10-12`). Screen은 `UIAddress.Screen(type)`(`FW/UIScreenManager.cs:259`), Popup은 `UIPopupManager.GetAddressableKey → UIAddress.Popup(type)`(`FW/UIPopupManager.cs:207-210`).

### 1.3 Screen(로비만 사용)
- `UIManager.Instance.screenManager.ChangeUIScreenAsync(id, IScreenData)` (`FW/UIScreenManager.cs:132-147`): 기록 비움 → `GetOrLoadUIScreenAsync`(풀 `screenPool[type]`에 있으면 재사용, 없으면 인스턴스 후 풀에 넣음, `:233-255`) → `SetCurrentScreen`(이전 화면이 풀이면 `HideUI`, 아니면 파괴, `:89-118`) → `ShowUI(screenData)`.
- 즉 **로비 화면은 한 번 만들어진 뒤 계속 재사용(캐시)** 되고 런 동안 숨겨질 뿐이다.
- `UIScreen.screenData`(`FW/UIScreen.cs:24-33`)로 데이터를 받는다.

### 1.4 Popup(메타 화면·결과·가짜 광고)
- `UIManager.Instance.SpawnUIPopupAsync(id, IPopupData)` (`FW/UIManager.cs:590-603`) → `popupManager.RequestPopup`(큐) → `CreatePopupAsync`(`FW/UIPopupManager.cs:138-176`): 같은 종류·같은 데이터가 이미 열려 있으면 그것을 맨 위로, 아니면 Addressables `InstantiateAsync` → `MoveToTop` → `ShowUI(data)`.
- `UIPopup.ShowUI()`(`FW/UIPopup.cs:168-196`)가 끝에 `UIManager.UpdateGlobalHudState(this)` → 팝업의 `hudSettings.displayMode`에 따라 HUD를 둔다(KeepPrevious/RequireSpecific/ForceHide, `FW/UIManager.cs:321-342`).
- `ClosePopup(result)`(`FW/UIPopup.cs:284-296`) → `HideUI` → `OnHideTransitionComplete`(`:256-268`) → `onHide`(관리자 목록에서 제거) → `RefreshGlobalHudState` → **`DestroyUI()`(Addressables Release)**. **팝업은 캐시되지 않고 닫을 때마다 파괴, 열 때마다 새로 인스턴스**된다.
- `popupData` 프로퍼티(`FW/UIPopup.cs:57`)로 데이터를 읽는다.

### 1.5 HUD(런 HUD만 사용)
- `hudManager.CachingHudAsync(list)`(`FW/UIHudManager.cs:65-119`)가 `hudPool[type]`에 한 번 인스턴스를 만들고, `ShowHuds(requests, targetUI)`(`:138-181`)가 목록에 있는 것만 켠다. `HideAll()`(`:208`)은 전부 숨김. **HUD도 풀에 남아 재사용**.

### 1.6 그리기 수명(UIBase, `com.jjevol.ui/Runtime/UIBase.cs`)
`ShowUI()` → `SetActive(true)` → `Refresh()` → `_DrawContents()`(`:63-91`). `SetDirty()`는 다음 프레임 `_DrawContents`. 프로젝트 컨트롤러는 모두 `_DrawContents`를 덮어 Presenter를 연다/그린다.

### 1.7 뒤로 가기
`UIManager.Update`가 Esc(에디터는 Backspace도)를 보면 `HandleBackButtonAsync`(`FW/UIManager.cs:640-672`): 맨 위 팝업 `ClosePopup()`, 팝업이 없으면 화면 이전/종료 확인. 프로젝트는 이를 덮지 않는다. 주의: Esc로 닫히는 팝업은 `ClosePopup()`만 불려 `MetaPopupData.OnClosed`가 **불리지 않는다**(OnClosed는 프로젝트 `MetaPanelPopup.Close()`에서만 부름, `UI/Meta/Common/MetaPanelPopup.cs:149-154`). 가짜 광고 팝업은 `HideUI` 덮어쓰기로 이 경우도 「건너뜀」 처리(`UI/Lobby/FakeAdPopup.cs:130-134`).

### 1.8 층(sortingOrder)
`MvUiLayer`(`UI/Common/MvUiIds.cs:38-55`): Screen 100, Hud 200, Result 300, Debug 410, Popup 500, FullPanel 550, Unlock 600, Ad 700, Toast 800 등. 각 컨트롤러 `Awake`에서 `MvUi.EnsureLayer(gameObject, order, raycast)`로 자기 Canvas에 `overrideSorting`(`UI/Common/MvUi.cs:128-136`).

---

## 2. 코드 → 프리팹 주소 대응표

프리팹은 모두 `Assets/Game/Addressables/Local/UI/` 아래(그룹 `Local_UI`, 주소 = 파일 이름). 「붙은 스크립트」는 프리팹의 `m_Script` GUID를 `.cs.meta`와 맞춰 확인한 것.

| MvUiIds 상수 → ID | 주소 | 프리팹 경로 | 붙은 스크립트(컨트롤러 / 겉) | 여는 곳(MvUiNav) |
|---|---|---|---|---|
| `Lobby` | `UIScreen_Lobby` | `Lobby/UIScreen_Lobby.prefab` | `LobbyScreen` / `LobbyView` | `OpenLobbyAsync` (`MvUiNav.cs:16-21`) |
| `Run` | `UIHud_Run` | `Hud/UIHud_Run.prefab` | `RunHud` / `RunHudView` | `EnterRunAsync` (`:24-34`) |
| `Result` | `UIPopup_Result` | `Result/UIPopup_Result.prefab` | `ResultPopup` / `ResultView` | `OpenResultAsync` (`:37-38`) |
| `GodSelect` | `UIPopup_GodSelect` | `Meta/UIPopup_GodSelect.prefab` | `GodSelectPopup` / `MetaPanelView` | `OpenGodSelectAsync` (`:43-44`) |
| `AreaUpgrade` | `UIPopup_AreaUpgrade` | `Meta/UIPopup_AreaUpgrade.prefab` | `AreaUpgradePopup` / `MetaPanelView` | `OpenAreaUpgradeAsync` (`:47-48`) |
| `Rebirth` | `UIPopup_Rebirth` | `Meta/UIPopup_Rebirth.prefab` | `RebirthPopup` / `MetaPanelView` | `OpenRebirthAsync` (`:51-64`, 잠김이면 토스트만) |
| `Map` | `UIPopup_Map` | `Meta/UIPopup_Map.prefab` | `MapPopup` / `MetaPanelView` | `OpenMapAsync` (`:70-74`) |
| `FeatureUnlock` | `UIPopup_FeatureUnlock` | `Meta/UIPopup_FeatureUnlock.prefab` | `FeatureUnlockPopup` / `MetaPanelView` | `OpenFeatureUnlockAsync` (`:77-83`) — 현재 호출하는 곳 없음 |
| `Debug` | `UIPopup_Debug` | `Meta/UIPopup_Debug.prefab` | `DebugPopup` / `MetaPanelView` | `OpenDebugAsync` (`:86-90`, `Debug.isDebugBuild`일 때만) |
| `FakeAd` | `UIPopup_FakeAd` | `Lobby/UIPopup_FakeAd.prefab` | `FakeAdPopup` / `FakeAdView` | `OpenFakeAdAsync` (`:93-94`) |
| `ContentMenu` | `UIPopup_ContentMenu` | `Content/UIPopup_ContentMenu.prefab` | `ContentMenuPopup` / `MetaPanelView` | `OpenContentMenuAsync` (`:97-98`) |
| `Quest` | `UIPopup_Quest` | `Content/Quest/UIPopup_Quest.prefab` | `QuestPopup` / `MetaPanelView` | `OpenQuestAsync` (`:101-102`) |
| `Research` | `UIPopup_Research` | `Content/Research/UIPopup_Research.prefab` | `ResearchPopup` / `MetaPanelView` | `OpenResearchAsync` (`:105-106`) |
| (Framework) | `UIManager` | `UIManager.prefab` | `StageFitter` | `UIManagerCreater` |

아틀라스(`Assets/Game/Addressables/Local/UIAtlas/`): `Atlas_UI_Card`, `Atlas_UI_Common`, `Atlas_UI_Hud`, `Atlas_UI_Icon`, `Atlas_UI_Lobby`(`.spriteatlasv2`). 코드 상수는 `MvUi.AtlasIcon/AtlasHud/AtlasCommon`(`UI/Common/MvUi.cs:15-17`). 그림은 `AddressableImage.SetAtlasSprite(atlas, sprite)`/`SetSprite(name)`(ui.addressables `AddressableImage.cs:280-285`)로 키만 넘겨 Framework가 로드한다(`MvUi.Skin`/`MvUi.Icon`, `MvUi.cs:75-90`). 실제로 스킨을 쓰는 겉은 `RunHudView`(`UI_Skin_StairBox`, `UI_Skin_WpnCard_G1~8`, `UI_Icon_Gold/Soul/Dia` 등)와 `BlessingCardsView`(`Atlas_UI_Card`). 로비·메타·결과 겉은 단색 상자만.

---

## 3. 클래스별 정리

### 3.1 공용(UI/Common)
| 클래스 | 파일 | 종류 | 책임 / 주요 메서드 |
|---|---|---|---|
| `MvUi` | `Common/MvUi.cs` | static 도구 | 코드로 겉을 짜는 도구. `U=10.8`(1u = 폭/100), 색 상수, `Rect/Place/Stretch/Box/Skin/Icon/Label/Shadowed/Button`, `EnsureLayer`(층 Canvas), `SetFill`(막대 채움). 컨트롤러는 쓰지 않고 겉만 쓴다(`:8`). |
| `MvUiBoot` | `Common/MvUiBoot.cs` | static | Boot 8단계. `ConfigureOptions()` 정적 옵션, `InitAsync(creator)`. 호출자: `GameBootstrap.RunAsync`. |
| `MvUiNav` | `Common/MvUiNav.cs` | static | 화면 전환 단일 입구. `OpenLobbyAsync`, `EnterRunAsync`, `OpenResultAsync`, 메타 패널 9종 `OpenXxxAsync`. 호출자: `MvGameLoop`, `LobbyScreen`, `MvFakeAd`, `MvMetaUiPort.FakeAdGate`, `MvBootMeta.OpenMeta`. |
| `MvUiIds` / `MvUiLayer` | `Common/MvUiIds.cs` | static 상수 | UI ID(주소 접두어 뒤 부분)·층 sortingOrder. |
| `MvUiText` | `Common/MvUiText.cs` | static | 문구 찾기. `Get(collection,key,fallback)`: `UIRuntimeOptions.GetLocalizedString`이 키와 다른 값을 주면 그것, 아니면 fallback(`:14-19`). `Name(rowId,fb)`=`name:<id>`, `WealthName(tables,type)`(번역 → `01_wealth.name` 열 → type), `RawName`. |
| `MvUiFormat` | `Common/MvUiFormat.cs` | static | 숫자 표기 `Fmt`(1,000,000↑ `x.xM`, 10,000↑ `x.xK`, 그 밖 쉼표). |
| `MvUnlockText` | `Common/MvUnlockText.cs` | static | `05_contentOpen` 조건 → 「Area 2 · 런 3번」 같은 글. `Phrase`, `Need(row,state)`. 판정은 MV.Meta `MvUnlockRules`. |
| `StageFitter` | `Common/StageFitter.cs` | MonoBehaviour | UIManager 프리팹의 StageRoot를 안전 영역에 맞춤. 기준 화면비를 표 `300 view.refWidthPx/refHeightPx`에서 읽고(`:20-21`), 표가 없으면 맞추지 않음(`:80`). `LateUpdate`마다 해상도·안전영역 바뀌면 `Refit`. |
| `MvUiToast` | `Meta/Common/MetaPanelPopup.cs:26-39` | static | 토스트 연결 지점. `Handler`가 없으면 `Debug.Log("[Toast] …")`. **현재 `Handler`를 설정하는 코드가 없다**(grep 결과). |

### 3.2 로비(UI/Lobby)
| 클래스 | 파일 | 종류 | 책임 / 주요 메서드 |
|---|---|---|---|
| `LobbyScreen` | `Lobby/LobbyScreen.cs` | `UIScreen` 컨트롤러 | `UIScreen_Lobby`. `Awake`: Presenter 이벤트 구독, `hudSettings.displayMode = KeepPrevious`, 층 100(`:76-85`). `ShowUI`: Presenter에 Port·Source·DevBuild·Muted 넣음(`:87-97`). `LateUpdate → Tick()`: 저장값을 읽어 바뀐 항목만 겉에 넘김 + `Presenter.Tick`(1초 부제)(`:105-137`). `PressStart()`: 한 번 보일 때 한 번만 `LobbyViewData.OnStart`(`:115-120`). `OnSideRequested(id)`: 사이드 버튼 → `MvUiNav.OpenXxxAsync`(`:150-189`). |
| `LobbyPresenter` | `Lobby/LobbyPresenter.cs` | 순수 클래스 | 로비 화면 코드. `Draw()`: 진행 상자·사이드 버튼·강화 버튼 모델 생성 후 `View.Render`(`:76-86`). `PressSide(id)`(잠김이면 토스트, 아니면 `SideRequested`, `:178-189`). `PressUpgrade(key)` → `Port.Buy(MvGrowth.Lobby,key)` 1번(`:309-334`). 이벤트: `SideRequested`, `Toast`, `Bought`, `FacilityBump`. 퀘스트 빨간 점 `QuestClaimable()`(`:34-43`). |
| `LobbyMenuModel`·`LobbySideButton`·`LobbyUpgradeItem`·`LobbySideIds`·`ILobbyMenuView` | `Lobby/LobbyModel.cs` | 데이터/인터페이스 | 사이드 버튼(`Dot`=빨간 점, `Locked`, `Visible`, `UnlockKey`), 강화 버튼 1칸(`10_lobbyUpgrade` 행 1개), 겉 인터페이스(`SidePressed`, `UpgradePressed`, `Render`). |
| `LobbyState`·`ILobbySource`·`LobbyViewData`·`ILobbyView`·`MvSaveLobbySource` | `Lobby/LobbyContracts.cs` | 계약 | `LobbyViewData : IScreenData`(OnStart, Source, Port, OnFacilityBump). `MvSaveLobbySource.TryRead`가 `MvSave.Current`의 Progress·Wealth와 `302_area`를 읽음(`:67-83`). |
| `LobbyView` | `Lobby/LobbyView.cs` | MonoBehaviour 겉 | `ILobbyView`+`ILobbyMenuView`. 참조가 비면 코드로 생성. 사이드 셀은 `Dictionary<id,SideCell>`, 강화 셀은 `List<UpCell>`로 **한 번 만든 셀을 재사용하고 모자라면 추가, 남으면 숨김**(`:82-126`). |
| `LobbyStartArea` | `Lobby/LobbyStartArea.cs` | MonoBehaviour | 화면 전체 투명 영역. 포인터 누름·Submit(Enter) → `Pressed`. |
| `LobbyFacilities` | `Lobby/LobbyFacilities.cs` | static | 아지트 시설 단계 계산(정본 상수 문턱). 현재 게임 루프는 `OnFacilityBump`를 넘기지 않는다(`LobbyContracts.cs:47-49`, `MvGameLoop.cs:255`). |
| `FakeAdPopup`·`FakeAdPresenter`·`FakeAdPopupData`·`IFakeAdView` | `Lobby/FakeAdPopup.cs` | `UIPopup` 컨트롤러 등 | `UIPopup_FakeAd`(층 700). 「광고 보기(가짜)」 → `Data.Finish(true)`, 닫기·뒤로·파괴 → `Finish(false)`; 콜백은 정확히 1번(`:24-30`, `:117-141`). |
| `MvFakeAd` | `Lobby/FakeAdPopup.cs:149-203` | static | 보상 광고 1번 입구. 공급자가 `MvFakeAdService`이고 UI가 있으면 가짜 광고 팝업 → 「봤음」이면 `MvMeta.WatchAd(key, done)`. 그 밖은 바로 `meta.WatchAd`. 결과 화면 광고가 사용. |
| `FakeAdView` | `Lobby/FakeAdView.cs` | MonoBehaviour 겉 | 검은 바탕 + 제목 + 두 버튼. |

### 3.3 런 HUD(UI/Hud)
| 클래스 | 파일 | 종류 | 책임 / 주요 메서드 |
|---|---|---|---|
| `RunHud` | `Hud/RunHud.cs` | `UIHudBase` 컨트롤러 | `UIHud_Run`(층 200, raycast 없음). `Bind(IRunHudSource)`: Presenter·가호 카드·종이 막에 출처 연결(`:34-41`). `LateUpdate`: `Presenter.Tick()`, `cards.Tick`, `wipe.Tick`(`:80-85`). `BlessingCardsView`·`ClimbWipeView`가 없으면 자식으로 만든다(`:43-67`). |
| `RunHudPresenter` | `Hud/RunHudPresenter.cs` | 순수 클래스 | `Tick()`: `Source.TryRead` → 이전 값과 비교해 바뀐 항목만 `IRunHudView.SetXxx`(`:33-64`). 문구 함수(`LevelText`, `FloorText`, `WeaponDmgText` 등) — `ui.hud.*` 키 자리 주석. |
| `RunHudState`·`IRunHudSource`·`IRunHudView` | `Hud/RunHudContracts.cs` | 계약 | 한 프레임 값(Level·Area·Floor·재화·계단·게이지·무기·오르기 글). |
| `RunStateHudSource` | `Hud/RunStateHudSource.cs` | 출처 구현 | `Func<RunState>`·`Func<int> savedSouls`로 `RunState` → `RunHudState` 변환. 표(`302_area`, `103_item`, `300 MERGE_N`, `107` 게이지, `320 side.turnSec`)를 읽어 이름·수치 계산(`:26-77`). `Current()`는 가호 카드 화면이 직접 `RunState`를 읽을 때 사용. |
| `RunHudView` | `Hud/RunHudView.cs` | MonoBehaviour 겉 | 아틀라스 스킨 기반 기본 겉. 무기 카드 바탕 `UI_Skin_WpnCard_G{1..8}`(그림 8개, 넘는 등급은 마지막 그림, `:38-39`, `:102-110`). |
| `BlessingCardsView` | `Hud/BlessingCardsView.cs` | MonoBehaviour | 가호 카드 룰렛(`#hc`, 층 220)과 가호 줄. `RunState.Roulette`·`Cards`를 직접 읽음. `Bind(Func<RunState>)`, `Tick(dt)`. |
| `ClimbWipeView`·`PaperPanelGraphic`·`PaperSparkGraphic` | `Hud/*.cs` | MonoBehaviour/Graphic | 계단 정산 종이 막 전환 연출. HUD 첫 자식(HUD 글자 아래)에 그림. |

### 3.4 결과(UI/Result)
| 클래스 | 파일 | 종류 | 책임 / 주요 메서드 |
|---|---|---|---|
| `ResultPopup`·`ResultPopupData` | `Result/ResultPopup.cs` | `UIPopup` 컨트롤러 | `UIPopup_Result`(층 300, `hudSettings.displayMode = ForceHide` → 열리면 HUD 숨김, `:51-56`). `PressCollect()` ×1, `PressX3()` 광고 ×N. `Finish()`: `ClosePopup(true)` 후 `data.OnCollected(LastSettle)`(`:91-98`). |
| `ResultPresenter` | `Result/ResultPresenter.cs` | 순수 클래스 | `Open(data)`/`Draw()`. `TryCollect(mul)`: 열린 동안 정산 1번만(`Collected` 플래그), `settler.Settle(outcome, mul)`(`:87-104`). `TryCollectX3(finished)`: `AdPending=true` → `Watcher.WatchX3` → mul>0이면 `TryCollect(mul)`(`:69-84`). `CanX3`(`:25-32`). |
| `ResultViewData`·`IResultSettler`·`MvMetaResultSettler`·`IResultAdWatcher`·`MvMetaResultAd`·`IResultView`·`IResultAdView` | `Result/ResultContracts.cs` | 계약 | `ResultViewData.Create(RunResult, runSeq, savedArea, savedSouls, tables)`: 런 결과 → 표시값 + `MvRunOutcome`(`:45-86`). 기본 정산기 = `MvMeta.Current.Settle(outcome, mul)`(`:100-105`). 기본 광고 = `MvMetaResultAd`(`08_adPlacement` key `collect3`, 배수 = 그 행 `mul`, `:120-146`). |
| `ResultView` | `Result/ResultView.cs` | MonoBehaviour 겉 | 막·상자·보상 3칸·「×N 보상」·「괜찮아요」. |

### 3.5 메타 공통 틀(UI/Meta/Common)
| 클래스 | 파일 | 종류 | 책임 / 주요 메서드 |
|---|---|---|---|
| `MetaPanelPopup` | `Meta/Common/MetaPanelPopup.cs:46-179` | 추상 `UIPopup` 컨트롤러 | `CreatePresenter()`(하위가 구현), `Layer`(기본 500). `_DrawContents`: 처음 열릴 때만 `SetPort(Data?.Port)` → `OnOpening` → `Presenter.Open()`, 그 뒤는 `Draw()`(`:121-129`). `Update`에서 `Presenter.Tick`(`:134-137`). 겉 이벤트 → Presenter(`PressClose/PressDim/Press/SelectTab`). Presenter의 `CloseRequested` → `Close()` = `ClosePopup(true)` 후 `MetaPopupData.OnClosed`(`:149-154`). `MetaSound` → `MvSound.PlayUi`. |
| `MetaPopupData`·`FeatureUnlockPopupData` | 같은 파일 `:10-21` | `IPopupData` | `Port`, `OnClosed`(로비 다시 그림·런 끝 결과 열기 등). |
| `MetaPanelPresenter` | `Meta/Common/MetaPanelPresenter.cs` | 추상 순수 클래스 | `Open()`=`OnOpen()+Draw()`. `Draw()`: `Model.Clear` → `Build(Model, tables)` → 메타 없으면 모든 버튼 끔 → `View.Render`(`:53-62`). `Press(actionId)`: 메타 없음·꺼진 버튼 무시 → `OnAction` → `Draw()`(`:67-75`). `SelectTab`, `PressClose`, `PressDim`(`DimCloses`일 때만), `Tick`. 도우미 `NameOf/DescOf/GrowthValue/PriceText/Hms`. |
| `MetaPanelModel`·`MetaRow`·`MetaAction`·`MetaTab`·`MetaRowKind`·`IMetaPanelView` | `Meta/Common/MetaPanelModel.cs` | 데이터/인터페이스 | 제목·부제·재화 줄·탭·행·행동 버튼·아래 큰 버튼(`Confirm`)·빈 글·`BottomUp`·`FocusRowId`. |
| `MetaPanelView` | `Meta/Common/MetaPanelView.cs` | MonoBehaviour 겉 | 막+상자+ScrollRect 목록. **`Render`마다 탭·행 GameObject를 전부 Destroy하고 새로 만든다**(`:69-80`, `:97-106`; 주석 「1차는 칸을 매번 다시 만든다. 교체 지점: 재사용 풀」 `:192`). 리스트 아이템 풀링 없음. |
| `IMetaUiPort`·`MvMetaUiPort`·`MetaResult`·`MvDebugRequests` | `Meta/Common/MetaUiPort.cs` | Port | 메타 읽기·명령의 좁은 입구. `MvMetaUiPort`는 매번 `MvMeta.Current`를 읽는다(`:127-133`). `Buy`(Lobby면 `BuyLobbyUp`), `SelectGod`, `PressRebirth`, `WatchAdForReward`(가짜 광고 관문 `FakeAdGate` 포함, `:171-209`), `IsOpen/UnlockState/PendingUnlockShows/MarkUnlockSeen`, `SendDebug`, `SetMuted`. 결과는 `MetaResult`(Ok/Code/Value, 로비 구매 Lv 전후). |

### 3.6 메타 화면들(UI/Meta/*) — 각 Popup은 `CreatePresenter()`만 다름
| 화면 | Popup / Presenter | 표 → 행 | 행동 → 메타 명령 |
|---|---|---|---|
| 야옹신 | `GodSelectPopup` / `GodSelectPresenter` | 탭=`113_god`(showInMenu), 본문=신 정보+`110_card` group | 탭: 잠김이면 토스트. 「이 야옹신 사용」→`Port.SelectGod`→성공 시 닫힘. `DimCloses=true`. |
| Area 강화 | `AreaUpgradePopup` / `AreaUpgradePresenter` | 보유 캣닢 행 + `13_areaUpgrade` 행마다 1칸 | `free`→`WatchAdForReward("dia")`, `buy:<key>`→`Port.Buy(AreaUp)`. |
| 부활 | `RebirthPopup` / `RebirthPresenter` | 상태 행 + `14_rebirthPerk` + 경고 + Confirm | `skip`→광고(`skip`), `go`→`PressRebirth`(2번 눌러야 부활), `perk:<key>`→`Buy(Perk)`. 1초 Tick. `OnOpen`에서 `MvMeta.Current?.DisarmRebirth()` 직접 호출(Port 우회, `RebirthPresenter.cs:22`). |
| 지도 | `MapPopup`(층 550) / `MapPresenter` | `301_zone`×`302_area`×`303_areaFloor`, `05_contentOpen` Area 조건 행 | 행동 없음(닫기만). `BottomUp`·`FocusRowId`=현재 Area. |
| 신기능 해금 | `FeatureUnlockPopup`(층 600) / `FeatureUnlockPresenter` | 받은 key 목록을 1개씩 | 「확인」→`MarkUnlockSeen` → 다음, 끝이면 닫힘. |
| 테스트 | `DebugPopup`(층 410) / `DebugPanelPresenter` | 고정 행 + `06_balancePreset` | 재화·진행 변경은 모두 `Port.SendDebug(debug.*)`. 배속·밸런스는 `MvDebugHooks`(**설정하는 코드 없음 → 버튼 꺼짐**). |

### 3.7 컨텐츠(UI/Content)
| 클래스 | 파일 | 책임 |
|---|---|---|
| `ContentMenuPopup`·`ContentMenuData` | `Content/ContentMenuPopup.cs` | `UIPopup_ContentMenu`. `OnOpening`에서 `StartMode = data.OnStartMode ?? ContentMenuData.StartModeHandler`, `OpenMeta = data.OnOpenMeta ?? ContentMenuData.OpenMetaHandler`(`:41-47`). `OpenMetaHandler`는 `MvBootMeta.Attach`가 설정(`BOOT/MvBootMeta.cs:66`), **`StartModeHandler`는 설정하는 코드 없음 → 모드 카드는 「준비 중」 토스트**(`ContentMenuPresenter.cs:209-214`). |
| `ContentMenuPresenter` | `Content/ContentMenuPresenter.cs` | 「모드」 탭(`400_content` active·contentType≠Main), 「성장」 탭(`05_contentOpen` 중 하드코딩된 메타 시스템 key 목록 `:33-36`). `PressMode`: 잠금·하루 한도 확인 → `RecordModePlay` → 닫기 → `start(key)`. `PressMeta` → `OpenMeta(key)`. 닫힐 때 `SaveSeen` → `MarkMenuSeen`(NEW 본 key·마지막 탭). |
| `IContentMenuPort`·`MvContentMenuPort` | `Content/ContentMenuPort.cs` | `ModePlaysToday`, `RecordModePlay`, `IsNew`, `MarkMenuSeen`, `LastTab`, `TicketCount`(항상 −1). |
| `QuestPopup`·`QuestPopupData` | `Content/Quest/QuestPopup.cs` | `UIPopup_Quest`. `OnOpening`에서 `QuestPresenter.SetQuestPort(data.Quest)`. |
| `QuestPresenter` | `Content/Quest/QuestPresenter.cs` | 탭 = 분류(일일·월간·업적), 행 = 묶음 머리 + 퀘스트 칸. `claim:<id>` → `Quest.Claim(id)`, `claimAll` → 반복 수령(`:104-124`). 탭 부제 `●` = 받을 것 있음. 1초 Tick(초기화 남은 시간). |
| `IQuestUiPort`·`MvQuestUiPort`·`QuestItem`·`QuestGroupItem` | `Content/Quest/QuestUiPort.cs` | `MvQuest.Current.Board()`를 화면용 사본으로 변환(`:62-83`). `Claim` → `MvQuest.Claim` → `MvMeta.Send(meta.questClaim)`. |
| `ResearchPopup` / `ResearchPresenter` | `Content/Research/*.cs` | 탭 = 계열(group), 본문 = 단계 목록. 다음 단계 칸에만 올리기/진행 중/받기/다른 연구 중 버튼. `start:<g>`→`StartResearch`, `claim:<g>`→`ClaimResearch`. 연구 진행 중이면 1초 Tick. |
| `IResearchUiPort`·`MvResearchUiPort` | `Content/Research/ResearchPort.cs` | `MvResearch.Level/Run/Start/Claim`(MV.Content) 경유 → `MvMeta.Send(meta.startResearch/claimResearch)`. |

### 3.8 MV.Platform
| 클래스 | 파일 | 종류 | 책임 / 상태 |
|---|---|---|---|
| `IMvAds`·`IMvAdProvider`·`IMvAdTimer`·`MvAdKind`·`MvAdResult(Code)`·`MvAdErrors` | `PF/Ads/MvAdTypes.cs` | 계약 | 광고를 보여 주고 결과만 알림(한도·보상은 MV.Meta 몫, `:60-61`). |
| `MvAds` | `PF/Ads/MvAds.cs` | 구현 | 한 번에 하나(`IsShowing`, Busy 거절), 공급자 없음·미지원 → 개발 빌드는 `MvFakeAdSession`(3초) 후 Completed, 출시 빌드는 Unsupported(`:42-91`). `CreateDefault`는 `Debug.isDebugBuild`로 가짜 허용. **게임 코드에서 생성·사용하는 곳 없음.** |
| `MvAdManagerProvider` | `PF/Ads/MvAdManagerProvider.cs` | 어댑터 | platform.core `IADManager.ShowRewardAD(new ADRewardRequest(unitId, placement))`를 `IMvAdProvider`로 감쌈. 보상형만, `initComplete`일 때(`:23-31`). 결과 매핑 `Map`(`:34-40`). AdMob 자체는 `IADManager` 구현(Framework 쪽) 몫. **사용처 없음.** |
| `MvFakeAdSession` | `PF/Ads/MvFakeAdSession.cs` | 가짜 광고 1회 | 1초마다 3→0, `Close()`면 Skipped. |
| `IMvLogin` | `PF/Login/IMvLogin.cs` | 계약 | `SignIn(method)`, `RestoreSession`, `Link`, `SignOut`, `Update`, `AccountChanged`. |
| `MvLogin` | `PF/Login/MvLogin.cs` | 구현 | platform.core `LoginService`에 로컬 계정 공급자를 붙인 게스트 스텁(`:33-43`). Google·Apple은 `ConfigureProvider`로 자격 증명 공급자를 넣는 자리만. **게임 코드에서 생성하는 곳 없음.** |
| `MvLocalAccountBackend` | `PF/Login/MvLocalAccountBackend.cs` | `IBackendAuthProvider` | 기기 로컬 게스트 계정(GUID) + 연동 목록 저장. 토큰 검증 없음. Firebase Auth로 교체 예정(`:11-13`). |
| `IMvLoginStore`·`MvPlayerPrefsLoginStore`·`MvMemoryLoginStore`·`MvLoginStoreKeys` | `PF/Login/MvLoginStore.cs` | 저장 | `mv.login.guestId/lastMethod/linked`. |
| `MvLoginMethod`·`MvLoginFailure`·`MvLoginAccount`·`MvLoginResult` | `PF/Login/MvLoginTypes.cs` | 타입 | Guest↔`"anonymous"` 공급자 ID 변환 등. |
| `IServerClock`·`MvLocalServerClock` | `PF/Net/IServerClock.cs` | 시계 | 기기 UTC + 오프셋. 서버 생기면 `SetServerTime`. |
| `MvLocalNetworkService`·`MvNetErrors` | `PF/Net/MvLocalNetworkService.cs` | `INetworkLocalService` | 요청 이름 → 처리기 `Func<NetworkCall,JObject>` 등록표. 없는 이름·빠진 파라미터는 `NetworkRejection`(`net.unknownRequest`/`net.badParams`). 처리기는 상태를 바꾸지 않고 반영은 `NetworkManager.AddResponseApplier`의 반영기 몫(주석 `:12-14`). |
| `MvPingRequest` | `PF/Net/MvPingRequest.cs` | `NetworkRequest` 하위 | 유일한 실제 `NetworkRequest`. 이름 `net.ping`, 파라미터 `value`, 응답 `{echo, serverTime}`. `RegisterLocal`로 로컬 처리기 등록. |

---

## 4. 게임 상태 읽기·명령 경로(UI ↔ 다른 모듈)

```
[읽기]
LobbyScreen ── ILobbySource(MvSaveLobbySource) ──▶ MvSave.Current(Progress, Wealth) + MvTables.Current
LobbyPresenter/MetaPanelPresenter ── IMetaUiPort(MvMetaUiPort) ──▶ MvMeta.Current (Level/IsMax/NextPrice/CanBuy/UnlockState/AdsLeft…)
QuestPresenter ── IQuestUiPort(MvQuestUiPort) ──▶ MvQuest.Current.Board()
ResearchPresenter ── IResearchUiPort(MvResearchUiPort) ──▶ MvResearch.Level/Run(MvMeta.Store)
RunHud ── IRunHudSource(RunStateHudSource) ──▶ RunSim.State (MV.Run) + 표

[명령]
Presenter ─▶ Port.Buy/SelectGod/PressRebirth/WatchAdForReward/MarkUnlockSeen/SendDebug/SetMuted
          ─▶ MvMeta.Buy/BuyLobbyUp/… ─▶ MvMeta.Send(MvMetaRequest(name, JObject))
          ─▶ IMvMetaBackend.Handle (= MvLocalMetaBackend.Dispatch, 판정·새 값 계산만)
          ─▶ MvMetaResponse(Ok → Data, Result / Reject → Code)
          ─▶ (Ok) MvSaveStore.ApplyAndSave(Data, keys)  ← 저장 1번
          ─▶ MvMeta.Applied 이벤트(UI는 미구독)
          ─▶ Port가 MetaResult로 바꿔 Presenter에 반환 ─▶ 실패면 Say(code) 토스트 / 성공이면 소리 ─▶ Draw() 다시 그림
```

- 메타 없음(저장 못 엶) 규칙: `IMetaUiPort.Ready == false`면 메타 패널 모든 버튼 꺼짐(`MetaPanelPresenter.cs:59-60`), 로비 강화·메타 화면 버튼 꺼짐(`LobbyPresenter.cs:111-139`, `:214`).
- 거절 코드는 문구로 바꾸지 않고 그대로 토스트로 나가는 곳이 많다(예: `LobbyPresenter.cs:320`, `AreaUpgradePresenter.cs:58`). 연구만 `ResearchPresenter.FailText`로 문구 변환(`:189-200`).

---

## 5. 흐름(단계별, file:line)

### 5.1 로비 진입
1. Boot 씬: `GameBootstrap.RunAsync`(`BOOT/GameBootstrap.cs:61`) — Addressables 초기화 → 표 로드(`:94`) → 저장 열기(`:99`) → `MvBootMeta.Attach`(`:103`; 저장이 열려 있으면 `MvMeta.Current` 생성·`MvQuest.Install`, `MvBootMeta.cs:86-91`; `ContentMenuData.OpenMetaHandler` 설정 `:66`) → **8단계 `MvUiBoot.InitAsync(ui)`**(`:108-109`) → 소리·로컬 네트워크(`:112-116`) → Game 씬 로드(`:121`).
2. Game 씬: `GameInitializer`가 `MvGameLoop`를 붙이고 `InitAsync(tables)`(`BOOT/GameInitializer.cs:38-39`) → UI 시작 대기(`MvGameLoop.cs:119-120`) → `WorldRoot`·테마·이펙트 준비 → `EnterLobbyAsync()`(`:137`).
3. `EnterLobbyAsync`(`MvGameLoop.cs:243-258`): 테마 적용, 로비 트랙(미리보기 런 상태, Tick 안 함) 깔기, `MvSound.EnterLobby()`, **`MvUiNav.OpenLobbyAsync(new LobbyViewData { OnStart = OnLobbyStart })`**(`:255`).
4. `MvUiNav.OpenLobbyAsync`(`MvUiNav.cs:16-21`): `screenManager.ChangeUIScreenAsync("Lobby", data)` → (처음이면) Addressables `UIScreen_Lobby` 인스턴스 → 풀 저장 → `ShowUI(data)`; 이어서 `hudManager.HideAll()`(런 HUD 끔).
5. `LobbyScreen.Awake`(처음 1회, `LobbyScreen.cs:76-85`) → `ShowUI`(`:87-97`: Presenter에 Port=`data.Port`(null → `MvMetaUiPort.Instance`), Source(null → `MvSaveLobbySource`), DevBuild, Muted) → `base.ShowUI` → `_DrawContents` → `Tick()`(`:99-103`).
6. `Tick()`(`:123-137`): `Source.TryRead` → 처음이면 `Presenter.Draw()`(사이드·강화·진행 상자 모델 → `LobbyView.Render`), Level·Area·재화 글을 `ILobbyView.SetXxx`. 이후 매 `LateUpdate`마다 바뀐 값만 다시 넘김 + `Presenter.Tick` 1초마다 `Draw`(부활 쿨타임 등 부제).

### 5.2 로비 → 런 시작
1. 입력: 화면 누름/Enter → `LobbyStartArea.Pressed`(`LobbyStartArea.cs:19-21`) → `LobbyView.StartPressed`(`LobbyView.cs:55`) → `LobbyScreen.PressStart`(`LobbyScreen.cs:115-120`, 한 번만) → `LobbyViewData.OnStart`. Space 등은 게임 루프 `Update`의 `MvRunInputReader.StartKeyPressed()` → `MvGameLoop.PressStart` → `_lobbyScreen.PressStart()`(`MvGameLoop.cs:311-316`, `:362`).
2. `MvGameLoop.OnLobbyStart`(`:318-332`) → `StartRun()`(`:336-355`): `MvMeta.BeginRun()`으로 런 순번, `RunSim` 생성, 월드 카메라 런 자세, `Phase = Run`.
3. **`MvUiNav.EnterRunAsync(new RunStateHudSource(() => _sim?.State, () => _savedSouls))`**(`MvGameLoop.cs:354`).
4. `EnterRunAsync`(`MvUiNav.cs:24-34`): 현재 화면(로비)을 `HideUI()`(풀에 남음) → `hudManager.CachingHudAsync(["Run"])`(처음이면 `UIHud_Run` 인스턴스, 이후 재사용) → `ShowHuds([Run])` → `hudPool["Run"] as RunHud` → `run.Bind(source)`.

### 5.3 런 중 HUD 갱신
1. `RunHud.Bind`(`RunHud.cs:34-41`): `RunHudPresenter.Bind(source)`(다음 Tick 전체 다시 그림), 출처가 `RunStateHudSource`면 `Cards.Bind(rs.Current)`, `Wipe.Bind(...)`.
2. 매 프레임 게임 루프가 `RunSim.Tick`(`MvGameLoop.cs:371-403`) — HUD에 값을 밀어 넣지 않는다(「HUD는 상태를 직접 읽는다」 `:36`).
3. 같은 프레임 `RunHud.LateUpdate`(`RunHud.cs:80-85`) → `RunHudPresenter.Tick()`(`RunHudPresenter.cs:33-64`) → `RunStateHudSource.TryRead`(`RunStateHudSource.cs:26-77`: RunState·표 → `RunHudState`) → 이전 값과 항목별 비교 → 바뀐 것만 `RunHudView.SetLevel/SetFloor/SetGold/SetSouls/SetDia/SetStairs/SetGauge/SetWeapon/SetMergeHint/SetClimbCount`.
4. 같은 `LateUpdate`에서 `BlessingCardsView.Tick`(룰렛·가호 줄, RunState 직접 읽음)과 `ClimbWipeView.Tick`(계단 정산 종이 막).
5. 런이 없으면(`state == null`) `TryRead` false → HUD는 마지막 값을 유지.

### 5.4 런 종료 → (지도) → 결과 화면 → 로비 복귀
1. `TickRun`에서 `s.Phase == RunPhase.Result`면 `FinishRun()`(`MvGameLoop.cs:402`, `:409-427`): `ResultViewData.Create(r, _runSeq, _savedArea, _savedSouls, _tables)`(`ResultContracts.cs:45-86`), `data.Settler = CountingSettler`(저장 없으면 `NoSaveSettler`), `data.OnCollected = OnCollected`.
2. `OpenMapThenResultAsync`(`MvGameLoop.cs:433-450`): **먼저 지도** `MvUiNav.OpenMapAsync(new MetaPopupData { OnClosed = OpenResult }, playClick:false)`. 지도 팝업은 기본 `KeepPrevious`라 HUD가 그대로 보인다. 지도를 못 열면 결과로 바로.
3. 지도 ✕ → `MetaPanelPopup.OnClosePressed` → `Presenter.PressClose` → `CloseRequested` → `Close()` → `ClosePopup(true)` + `OnClosed`(`MetaPanelPopup.cs:139-154`) → `OpenResult()` → `MvUiNav.OpenResultAsync(data)`(`MvGameLoop.cs:463-467`) → `SpawnUIPopupAsync("Result", new ResultPopupData(data))`.
4. `ResultPopup.Awake`: `ForceHide`(`ResultPopup.cs:54`) → `ShowUI` 시 Framework가 `hudManager.HideAll()`(HUD 숨김). `_DrawContents` → `ResultPresenter.Open(data)` → `Draw()`(제목·진행·보상 3칸·통계·「×N 보상」 켜짐 여부).
5. 「괜찮아요」 → `ResultPopup.PressCollect`(`:72-77`) → `ResultPresenter.TryCollect(1)`(`ResultPresenter.cs:87-104`) → `CountingSettler.Settle`(`MvGameLoop.cs:498-511`) → `MvMetaResultSettler` → **`MvMeta.Settle(outcome, 1)`**(`META/MvMeta.cs:114-144`) → `Send(meta.settle)` → 로컬 백엔드 `Settle`(`META/MvLocalMetaBackend.cs:87`) → `ApplyAndSave` → 결과 `MvSettleResult`. 성공이면 `MvQuest.RecordRun`으로 런 퀘스트 카운터 추가 요청.
6. `ResultPopup.Finish()`(`:91-98`): `ClosePopup(true)`(팝업 파괴) → `data.OnCollected(result)` → `MvGameLoop.OnCollected`(`:479-484`) → `EnterLobbyAsync()` → 5.1의 3~6단계(로비 화면은 풀에서 재사용, `ChangeUIScreenAsync` 후 `hudManager.HideAll`). 전면 광고(`ShowInterstitial`)는 2차라 호출하지 않음(`:482`).

### 5.5 메타 업그레이드 버튼 → 요청 → 응답 → 화면 갱신
**(a) 로비 강화 버튼(캣타워·스크래쳐·생선 건조대)**
1. `LobbyView` 강화 셀 클릭 → `UpgradePressed(key)`(`LobbyView.cs:163`) → `LobbyScreen.PressUpgrade`(`LobbyScreen.cs:146`) → `LobbyPresenter.PressUpgrade(key)`(`LobbyPresenter.cs:309-334`).
2. 가드: `Port.Ready`, 버튼 `Enabled`, 표 행 존재 → `Port.Buy(MvGrowth.Lobby, key)` → `MvMetaUiPort.Buy`(`MetaUiPort.cs:145-149`) → **`MvMeta.BuyLobbyUp(key)`**(`META/MvMeta.cs:249-259`) → `Buy` → `Send(new MvMetaRequest("meta.buyLobbyUp", {key}))`(`:241-246`).
3. `MvMeta.Send`(`:67-94`): `IsReady` 확인 → `_backend.Handle(request)` → `MvLocalMetaBackend.Handle`(`MvLocalMetaBackend.cs:47-54`) → `Dispatch` → `BuyLobbyUp(key)`(`:219`; 비용·MAX 판정, 바뀔 컨테이너 값만 `Data`에, before/after/tier/merged는 `Result`에) → 콘텐츠 기여자(퀘스트 카운터)가 같은 응답 `Data`에 보탬(`:51-52`).
4. 성공이면 `Store.ApplyAndSave(response.Data, keys)` 1번(`MvMeta.cs:83`), 저장 실패면 `meta.saveFailed` 거절로 바꿈(`:85-90`), `Applied` 이벤트.
5. `MvLobbyBuyResult` → `MetaResult.Of`(Lv 전후 포함) → Presenter: 실패면 `Say(r.Code)` 토스트 + `Draw()`; 성공이면 `Bought`(소리 `LobbyUpgradeBuy`, `LobbyScreen.cs:191`), `FacilityBump`(→ `LobbyViewData.OnFacilityBump`, 현재 null), Lv 올랐으면 토스트, `Draw()`.
6. 재화 표시(Level·Area·재화 글)는 다음 `LateUpdate`의 `LobbyScreen.Tick`이 저장값 변화를 감지해 갱신.

**(b) 메타 패널(Area 강화·부활 특성 등)**
1. `MetaPanelView` 행 버튼 → `ActionPressed(id)`(`MetaPanelView.cs:144`) → `MetaPanelPopup.OnActionPressed` → `MetaPanelPresenter.Press(id)`(`MetaPanelPresenter.cs:67-75`).
2. 예: `AreaUpgradePresenter.OnAction("buy:<key>")`(`AreaUpgradePresenter.cs:45-61`) → `Port.Buy(MvGrowth.AreaUp, key)` → `MvMeta.Buy` → `Send("meta.buyAreaUp")` → `MvLocalMetaBackend.BuyAreaUp`(`:251`) → 저장.
3. 실패 → `Say(code)`(→ `MvUiToast.Show`), 성공 → `Play(MetaSound.AreaBuy)`(→ `MvSound.PlayUi(AreaUpgradeBuy)`).
4. `Press`가 끝에서 `Draw()` → `MetaPanelView.Render` → 모든 행 다시 생성.
5. 패널을 닫으면 `MetaPopupData.OnClosed = Redraw`(로비가 넘김, `LobbyScreen.cs:152`) → `LobbyPresenter.Draw()`.

**(c) platform.network 경로(현재 상태)**
- 프로젝트 규칙의 목표 모양은 「`NetworkRequest` 하위 → `NetworkManager.Post` → `INetworkLocalService`(같은 이름·JSON) → `NetworkRejection`로 거절 → `AddResponseApplier` 반영기가 저장 → 콜백」이다(`PF/Net/MvLocalNetworkService.cs:10-17`).
- 실제 연결: Boot 9단계에서 `NetworkManager.SetLocalService(MvPingRequest.RegisterLocal(new MvLocalNetworkService(), new MvLocalServerClock()))`, `DefaultRoute = Local`(`GameBootstrap.cs:115-116`). 등록된 처리기는 `net.ping` 하나.
- 사용 예(시험만): `NetworkManager.Post(new MvPingRequest("t"), r => …)` → `MvLocalNetworkService.HandleAsync`(`MvLocalNetworkService.cs:34-41`) → 처리기가 `RequireString(call,"value")`(없으면 `NetworkRejection(net.badParams)`) → `{echo, serverTime}` → `NetworkResult` → `MvPingRequest.ReadEcho/ReadServerTime`(`MvPingRequest.cs:36-39`). 게임 코드에서 `NetworkManager.Post`를 부르는 곳은 없다.
- 메타 요청(`meta.*`)은 `MvMetaRequest`/`IMvMetaBackend`/`MvMetaResponse.Reject(code)`라는 **같은 모양의 자체 경로**로 돈다. 반영도 `AddResponseApplier`가 아니라 `MvMeta.Send` 안의 `ApplyAndSave`. 전환 계획은 `META/MvMetaRequest.cs:6-10`, `:139-147` 주석.

### 5.6 퀘스트·연구 화면
**퀘스트**
1. 로비 사이드 버튼 「퀘스트」(`LobbyPresenter.cs:132-136`, 잠금 `05_contentOpen[quest]`) → `PressSide("quest")` → `SideRequested` → `LobbyScreen.OnSideRequested`(`LobbyScreen.cs:173-176`) → `MvUiNav.OpenQuestAsync(new QuestPopupData { Port, OnClosed = Redraw })`.
2. `QuestPopup.OnOpening`(`QuestPopup.cs:14-17`) → `QuestPresenter.SetQuestPort(null → MvQuestUiPort.Instance)` → `Open` → `Build`(`QuestPresenter.cs:36-77`): `Quest.Groups()`(→ `MvQuest.Current.Board()`, `QuestUiPort.cs:62-83`) → 분류 탭, 묶음 머리, 칸(진행 글·보상 글·「받기」 켜짐), 받을 칸 있으면 「모두 받기」.
3. 「받기」 → `OnAction("claim:<id>")`(`:92-101`) → `MvQuestUiPort.Claim` → `MvQuest.Claim(id)` → `MvMeta.Send(meta.questClaim)`(`MV.Content/Quest/MvQuest.cs:134`) → 처리기(MV.Content가 `RegisterContent`로 등록) → 저장 → `Draw()`.
4. 로비 빨간 점: `LobbyPresenter.QuestClaimable()`(`:34-43`)이 열린 분류에 `Claimable` 칸이 있으면 `quest.Dot = true` → `LobbyView.Render`가 제목 뒤 `<color=#ff4d4d>●</color>`(`LobbyView.cs:90`). 퀘스트 팝업 안 탭 점은 탭 `Sub = "●"`(`QuestPresenter.cs:49`).

**연구**
1. 로비 「컨텐츠」 → `OpenContentMenuAsync` → 「성장」 탭(`ContentMenuPresenter.BuildMeta`, `:156-176`) → 「고양이 공부방」 행 `meta:research` → `PressMeta`(`:227-246`) → `OpenMeta(key)` = `ContentMenuData.OpenMetaHandler` = `MvBootMeta.OpenMeta`(`BOOT/MvBootMeta.cs:81-84`) → `MvUiNav.OpenResearchAsync()`(메뉴 위에 열림, 메뉴는 열린 채). 「성장」 탭의 다른 key(quest 등)는 `OpenMeta`가 아무것도 하지 않는다.
2. `ResearchPresenter.Build`(`ResearchPresenter.cs:53-138`): `MvResearchRules.Groups(tables)` 탭, 잠금 안내, 계열 머리(누적 능력치), 단계 칸. 다음 단계 칸만 상태별 버튼.
3. 「올리기」 → `start:<g>` → `Research.StartResearch(g)` → `MvResearch.Start(M, g)` → `MvMeta.Send(meta.startResearch)`(비용 먼저, 끝 시각 저장) / 「받기」 → `claim:<g>` → `meta.claimResearch`. 실패는 `FailText(code)` 토스트.
4. 연구 진행 중이면 `Tick`이 1초마다 `Draw()`(남은 시간).
5. 런에 반영: `MvGameLoop.CreateSim`이 `MvResearch.Modifiers(store, tables)`를 런 컨텍스트에 넣음(`MvGameLoop.cs:306`).

### 5.7 광고 보상 흐름
**(a) 메타 패널 보상 광고(Area 강화 「▶ 무료」, 부활 「스킵」)**
1. `AreaUpgradePresenter.OnAction("free")` → `Port.WatchAdForReward("dia", cb)`(`AreaUpgradePresenter.cs:47-55`).
2. `MvMetaUiPort.WatchAdForReward`(`MetaUiPort.cs:171-175`) → `MvMeta.WatchAdForReward(key, done, FakeAdGate)`(`META/MvMeta.cs:355-372`): 행 없음/꺼짐 → `unknownKey`, 오늘 남은 0 → `dailyLimit`, 쿨타임 스킵인데 쿨 없음 → `noCooldown`, 광고 제거면 바로 `Send(meta.grantAd)`.
3. 관문 `FakeAdGate`(`MetaUiPort.cs:183-195`): 공급자가 `MvFakeAdService`이고 `UIManager`가 있으면 `MvUiNav.OpenFakeAdAsync(FakeAdPopupData{ OnFinished = pass })` → `UIPopup_FakeAd`(층 700, HUD KeepPrevious).
4. 사용자: 「광고 보기(가짜)」 → `FakeAdPopup.PressWatch` → `Data.Finish(true)` → `pass(true)` → `ClosePopup`. 닫기·뒤로·파괴 → `Finish(false)`(`FakeAdPopup.cs:117-141`).
5. `pass(true)` → `Ads.ShowReward(row.placement, watched => …)`(= `MvFakeAdService.ShowReward`, `NextRewardResult`(기본 true)로 즉시 응답, `MvMetaPorts.cs:65-70`) → 봤으면 `Send(meta.grantAd {key})`(보상 + 사용 수 한 번에, 로컬 백엔드 `GrantAd` `MvLocalMetaBackend.cs:377`) / 안 봤으면 `adNotWatched`.
6. `MetaResult` → Presenter 콜백: 실패면 토스트, 그리고 `Draw()`.

**(b) 결과 화면 「×N 보상」**
1. `ResultPopup.PressX3`(`ResultPopup.cs:80-89`) → `ResultPresenter.TryCollectX3`(`ResultPresenter.cs:69-84`): `AdPending=true`(두 버튼 막음) → `IResultAdWatcher.WatchX3` = `MvMetaResultAd.WatchX3`(`ResultContracts.cs:140-145`) → `MvFakeAd.Watch("collect3", …)`(`FakeAdPopup.cs:152-170`).
2. `MvFakeAd.Watch`: 가짜 공급자 + UI 있음 → 가짜 광고 팝업 → 봤으면 `meta.WatchAd(key, done)`(`META/MvMeta.cs:336-342`: 한도 확인 → `Ads.ShowReward` → 봤으면 `Send(meta.useAd)`로 사용 수 +1). 그 밖은 바로 `meta.WatchAd`.
3. 봤으면 `done(mul)`(mul = `08_adPlacement[collect3].mul`) → `TryCollect(mul)` → `MvMeta.Settle(outcome, mul)`(정산 1번) → `ResultPopup.Finish()` → 로비. 안 봤으면 정산 없이 버튼 다시 켬.
- 「실제 광고」 대체 지점: `MvMeta`에 `IMvAdService`(MV.Meta) 구현을 넣으면 된다(`MvMeta.cs:46-55`). `MV.Platform.MvAds`(IMvAds, 결과 `MvAdResult`)와 `MvAdManagerProvider`(platform.core `IADManager`)는 이 인터페이스와 직접 연결돼 있지 않다(어댑터 없음). Platform 쪽 `MvAds.FakeAdStarted`/`MvFakeAdSession`(3초 카운트)도 UI가 구독하지 않는다 — UI 가짜 광고 팝업은 카운트다운 없이 버튼 누름으로 끝난다.

### 5.8 로그인 흐름(Platform 코드 기준, 게임 미연결)
게임 코드에서 `MvLogin`을 만드는 곳이 없고(`GameBootstrap.cs:27,111` 「광고·로그인은 2차(D-90)」), 로그인 화면도 없다. 코드가 의도한 흐름:
1. 생성: `new MvLogin(new MvPlayerPrefsLoginStore())` → 내부 `LoginService(options{google,apple,firebase,anonymous})` + `SetBackend(new MvLocalAccountBackend(store))`(`PF/Login/MvLogin.cs:33-43`).
2. 앱 시작 시 `HasLoginHistory`면 `RestoreSession(cb)`(`:69-80`): 저장된 `mv.login.lastMethod` → `_service.RestoreAsync(providerId)` → `MvLocalAccountBackend.RestoreAsync`(`MvLocalAccountBackend.cs:32-39`: 게스트 id 없으면 `NoCurrentUser`, 연동 안 된 공급자면 `provider_not_linked`).
3. 게스트 로그인: `SignIn(MvLoginMethod.Guest, cb)`(`MvLogin.cs:58-67`) → `Begin`(중복 호출 Busy 거절, `:117-123`) → `RunBackend(() => _service.SignInAnonymousAsync())` → 백엔드 `SignInAnonymousAsync` → `EnsureGuestId`(없으면 GUID 생성·저장) → `BackendAuthSession`.
4. Google/Apple: `ConfigureProvider(method, factory)`로 자격 증명 공급자를 넣어야 하며, 없으면 `Availability`가 Available이 아니라 `Unsupported/NotConfigured` 실패(`:131-146`). 있으면 `RequestCredential` → `AuthenticateAsync(credential)` → 로컬 백엔드는 토큰 검증 없이 기기 계정에 공급자를 붙임(`MvLocalAccountBackend.cs:42-48`).
5. `RunBackend`(`MvLogin.cs:149-167`): 성공이면 `lastMethod` 저장 → `SetAccount(ToAccount(session))` → `AccountChanged` 이벤트 → `callback(MvLoginResult.Ok(account))`. 실패는 `FromBackend/FromError`로 `MvLoginFailure` 매핑.
6. `Link`(로그인 후 공급자 연동), `SignOut(keepLastMethod)`(세션만 닫고 기기 게스트 id는 유지).
- Firebase: Frozen에 `com.jjevol.platform.login.firebase-0.1.0-pre.4`가 있으나 MV.Platform asmdef는 그 어셈블리를 참조하지 않는다(`PF/MV.Platform.asmdef:4-10` — `PlatformLibrary.Network/Core/UnityRuntime`, `MV.Core`, `UniTask`만). 교체 지점 주석: `MvLocalAccountBackend.cs:11-13`, `MvLogin.cs:11`.

---

## 6. 주제별 정리

### 6.1 화면 스택·내비게이션
- Screen은 로비 1개뿐이고 `ChangeUIScreenAsync`가 기록을 비우므로 「이전 화면」 스택을 쓰지 않는다. 런 진입 시 로비를 숨기고(HideUI) 런 HUD를 켜며, 로비 복귀 시 같은 로비 인스턴스를 다시 보인다.
- Popup은 Framework `UIPopupManager`의 `_activePopupList`가 스택 역할(맨 위 = 마지막). 로비 → 컨텐츠 메뉴 → 연구처럼 팝업 위에 팝업이 쌓인다(`ContentMenuPresenter.cs:237` 「메뉴는 열린 채」).
- 같은 팝업 종류+같은 데이터 객체 요청이면 새로 만들지 않고 기존 것을 맨 위로(`FW/UIPopupManager.cs:146-160`).

### 6.2 HUD 표시 규칙
- 로비 화면·메타 패널·가짜 광고: `KeepPrevious`(UIPopup/Screen 기본값 `UIHudSettings.displayMode = KeepPrevious`, `FW/UIHudManager.cs:33`; 로비·가짜광고는 명시 설정).
- 결과 팝업: `ForceHide`.
- 런 HUD는 `MvUiNav.EnterRunAsync`가 직접 켜고, `OpenLobbyAsync`가 `HideAll()`로 끈다.

### 6.3 빨간 점(배지)
- Framework badge 패키지 미사용. 로비 사이드 버튼 `LobbySideButton.Dot`(현재 퀘스트만 계산, `LobbyPresenter.cs:136`), 메타 패널 탭 `MetaTab.Sub = "●"`(퀘스트), 연구 탭 부제 「받기!」, 컨텐츠 메뉴 「NEW」 칩(`ContentMenuPresenter.cs:124-128`, 메뉴 닫을 때 `MarkMenuSeen`으로 본 것 저장).

### 6.4 문구·다국어
- 모든 문구는 코드 안 한국어 기본값 + 주석에 키 자리(`// ui.lobby.map` 등). 표 행 이름은 `MvUiText.Name(id, fallback)` = 번역 컬렉션 `name`의 키 `<행 id>`; `description` 컬렉션도 사용(`MetaPanelPresenter.DescOf`).
- `UIRuntimeOptions.Localize`가 키를 그대로 돌려주므로 `MvUiText.Get`은 항상 fallback을 쓴다(`MvUiText.cs:21-23`). 다국어 연결 지점은 `MvUiBoot.ConfigureOptions`(`:15-16`).
- 거절 코드(`meta.notEnough` 등)는 대부분 그대로 토스트 문자열로 나간다.

### 6.5 프리팹 로딩·재사용 요약
| 대상 | 생성 | 재사용 |
|---|---|---|
| 로비 Screen | 첫 `ChangeUIScreenAsync` 때 Addressables 인스턴스 | `screenPool`에 남아 계속 재사용(숨김/보임) |
| 런 HUD | 첫 `CachingHudAsync` 때 | `hudPool`에 남아 재사용 |
| 모든 Popup | 열 때마다 Addressables 인스턴스 | 닫으면 `DestroyUI` → Release(재사용 없음) |
| 로비 사이드·강화 셀 | `LobbyView.Render`에서 모자랄 때 추가 | id/순번별로 재사용, 남으면 숨김 |
| 메타 패널 행·탭 | `MetaPanelView.Render`마다 | 재사용 없음(전부 Destroy 후 재생성) |
| 겉 자식 전체 | 겉 `Awake → EnsureBuilt`에서 코드 생성 | 프리팹에 참조를 이어 두면 코드 생성 생략(교체 지점) |
| 그림 | `AddressableImage`가 아틀라스+스프라이트 키로 로드 | Framework 관리 |

### 6.6 소리
UI는 `MV.Sound.MvSound.PlayUi(MvUiSound.Xxx)`를 직접 부른다(컨트롤러). Presenter는 소리 모듈을 모르고 `MetaSound` 이벤트만 낸다(`MetaPanelPresenter.cs:9`, `MetaPanelPopup.cs:156-166`). 소리 끄기는 `MvSound.Muted` 토글 + `Port.SetMuted` → `meta.setMuted` 저장(`LobbyScreen.cs:177-184`).

---

## 7. 연결 안 됨·주의 목록(문서화 시 표시 권장)
- `MvUiToast.Handler` 미설정 → 토스트는 로그만.
- `ContentMenuData.StartModeHandler` 미설정 → 컨텐츠 「모드」 카드 도전 시 「준비 중」.
- `MvDebugHooks.SetTimeScale/SelectBalance` 미설정 → 테스트 패널 배속·밸런스 버튼 꺼짐.
- `MvUiNav.OpenFeatureUnlockAsync` 호출처 없음(정산 결과 `NewlyOpened`를 UI가 쓰지 않음).
- `LobbyViewData.OnFacilityBump` 게임 루프가 넘기지 않음.
- `MvAds`·`MvAdManagerProvider`·`MvLogin` 게임 미연결(D-90). 메타 광고는 MV.Meta `IMvAdService`(기본 `MvFakeAdService`).
- 메타 요청이 `NetworkManager`를 타지 않음(`net.ping`만 등록).
- Esc 뒤로 가기로 메타 팝업을 닫으면 `OnClosed`가 불리지 않음(로비 다시 그림·런 끝 지도→결과 전환이 그 경로로는 안 일어남; 로비는 LateUpdate 비교로 재화 글은 갱신되나 `Presenter.Draw`는 1초 Tick으로 갱신).
- 글꼴은 TMP가 아닌 내장 `LegacyRuntime.ttf`(`MvUi.cs:28-32`).
