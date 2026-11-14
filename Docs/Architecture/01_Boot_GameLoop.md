# 01. 부팅·게임 루프·씬·에디터 도구 (MV.Boot · MV.Editor)

기준 커밋: `dev` 046c8be6 (2026-11-15). 경로는 저장소 루트 기준이고 `파일:줄`은 그 커밋의 줄 번호다. Framework 줄 번호는 `Library/PackageCache` 풀린 본 기준이라 참고용이다. 전체 지도는 [README.md](README.md).
경로는 저장소 루트 기준. `MV.Boot/…`는 `Assets/Game/Scripts/MV.Boot/…`의 줄임이다.

---

## 0. 한눈에

- 씬은 2개다: `Boot`(빌드 0번) → `Game`(빌드 1번) (`ProjectSettings/EditorBuildSettings.asset:9,12`).
- 앱 시작은 **Boot 씬의 `GameBootstrap.Start`** 하나에서 시작해 1~10단계를 UniTask로 순서대로 `await`한다. Game 씬에서는 **Framework `CustomSceneManager`가 `GameInitializer`(SceneInitializerBase)를 찾아 `await`** 하고, 이것이 `MvGameLoop`를 코드로 붙인다.
- 의존성 연결은 DI 컨테이너가 아니라 **정적 등록소(static `Current`/`Player`/`Instance`)** 패턴이다. `MvTables.Current`, `MvSave.Current`, `MvMeta.Current`, `MvBootSave.Resources`, `MvSound.Player`, `MvFx.Player`, `MvQuest.Current`, `MvGameLoop.Current`, `GameBootstrap.Ui` 등. 모든 정적 값은 `[RuntimeInitializeOnLoadMethod(SubsystemRegistration)] ResetStatics()`로 Play 시작마다 비운다(Domain Reload 꺼짐 대비).
- 모듈 간 반응은 C# 이벤트 한 개가 중심: `MvSave.CurrentChanged`(저장 열림/닫힘) → `MvBootSave.BindResources`(재화 서비스) + `MvBootMeta.OnSaveChanged`(MvMeta·MvQuest 생성).
- 런 프레임은 `MvGameLoop.Update` → `TickRun`: 입력 → `RunSim.Tick` → `RunSim.DrainEvents` 한 번 → 같은 이벤트 목록을 `MvSound`·`MvFx`·`WorldView`에 차례로 넘김 → `WorldView.SyncObjects/SyncCats`. HUD는 상태를 직접 읽는다(pull).
- MV.Boot는 최상위(조립) 어셈블리다. MV.Boot를 참조하는 asmdef는 `MV.Editor`뿐이다(`MV.Editor/MV.Editor.asmdef`). 게임 런타임 어셈블리 중 MV.Boot를 아는 것은 없다.

---

## 1. MV.Boot 어셈블리

`MV.Boot/MV.Boot.asmdef` 참조: PlatformLibrary.Network, MV.Core, MV.Data, MV.Save, MV.Run, MV.Meta, MV.Content, MV.View, MV.Fx, MV.Sound, MV.UI, MV.Platform, JJEvol.(Resources.Addressables / Audio(+Addressables) / Effects(+Addressables) / Tables / Data / Save / Economy / Localization / Scene(+Addressables, UI) / UI(+Addressables, URP)), Artress.ManagedAddressables, Unity.Addressables, Unity.ResourceManager, Unity.InputSystem, UniTask. `autoReferenced: false`.

파일 9개(asmdef 포함): GameBootstrap.cs(135줄), GameInitializer.cs(42), MvBootMeta.cs(93), MvBootSave.cs(169), MvGameLoop.cs(536), MvRunInputReader.cs(60), MvRunStart.cs(202), MvSaveLifecycle.cs(33).

### 1.1 `GameBootstrap` — `MV.Boot/GameBootstrap.cs`
- 종류: `MonoBehaviour`(sealed). Boot 씬 루트 오브젝트 `Boot`에 붙어 있다(§3).
- 책임: 기획서 10 §10.7의 부팅 1~10단계 실행. 끝나면 Game 씬으로 전환.
- 직렬화 필드: `[SerializeField] UIManagerCreater ui` (:34) — Boot 씬의 `UI` 오브젝트의 `UIManagerCreater`를 가리킨다.
- 정적 상태(시험·다른 씬이 읽음):
  - `State : BootState { NotStarted, Running, Succeeded, Failed }` (:16-22, :37)
  - `LastError` (:39), `Ui`(씬이 바뀌어도 남는 UI 부트스트랩, :42), `ServicesReady`(1~9단계 완료, :45)
  - `ResetStatics()` (:47-54)
- 상수: `GameSceneName = "Game"`, `BootSceneName = "Boot"` (:31-32).
- 진입점: `Start()` → `RunAsync().Forget()` (:56-59). **Awake가 아니라 Start**에서 시작한다.
- `RunAsync()` 단계 (:61-133), 모두 하나의 try/catch 안. 실패하면 `LastError`·`State=Failed`·`Debug.LogException` 후 멈춘다(재시도 없음):
  0. UI 부트스트랩 재사용(:69-74): 같은 실행에서 Boot를 다시 열었고 이전 `Ui`가 살아 있으면 새 `ui`를 Destroy하고 이전 것을 쓴다. 그 뒤 `DontDestroyOnLoad(ui.gameObject)`.
  1. `Addressables.InitializeAsync(false)` await → 실패면 예외 (:78-83). 핸들은 바로 Release.
  2. `AddressablesManager.Instance.InitAsync()` (Framework jjevol.resources.addressables) (:86).
  3. 공급자 지정 (:89-90): `AudioManager.Instance.SetProvider(ProviderKind.Addressables)`, `EffectManager.Instance.SetPrefabProvider(new AddressablesEffectPrefabProvider())`. 소리·이펙트를 처음 부르기 전에 정해야 한다.
  4. 표: `await MvBootMeta.LoadTablesAsync()` (:94) → `MvTableLoader.LoadAsync`가 `MvTables.Current`를 채운다. 검증 오류면 예외로 부팅 중단.
  5·6. 저장: `MvBootSave.Open(MvTables.Current)` (:99) → `MvSaveLifecycle.Ensure()` (:100). 저장을 못 열어도 부팅은 계속한다.
  6 뒤. `MvBootMeta.Attach(MvTables.Current)` (:103) — MvMeta·MvQuest 연결.
  7. 다국어: 없음(주석만, :105). 문구는 키 그대로(`MvUiBoot.ConfigureOptions`).
  8. UI: `Ui = ui; await MV.UI.MvUiBoot.InitAsync(ui)` (:108-109) → 정적 옵션 설정 후 `UIManagerCreater.StartInitAsync`.
  9. 소리·네트워크 (:112-116): `MvSound.InstallDefault()`, 저장 설정에서 `MvSound.Muted` 반영, `NetworkManager.SetLocalService(MvPingRequest.RegisterLocal(new MvLocalNetworkService(), new MvLocalServerClock()))`, `NetworkManager.DefaultRoute = NetworkRoute.Local`. 그 뒤 `ServicesReady = true` (:117).
  10. `await CustomSceneManager.Instance.LoadSceneAsync("Game", SceneLoadOption.Direct())` (:121). 실패면 예외. 성공하면 `State = Succeeded` (:125).
- 주의: Framework `CustomSceneManager.LoadSceneAsync`는 씬 로드 뒤 `SceneInitializerRunner.RunAsync`로 씬 초기화기(=`GameInitializer.OnInitializeAsync`)를 **기다린다**(PackageCache `com.jjevol.scene/Runtime/SceneTransitionPipeline.cs:175`). 따라서 `State == Succeeded`는 **Game 씬 루프가 로비까지 연 뒤**에 된다.
- 광고·로그인은 연결하지 않음(D-90, 주석 :27, :111). 로딩 화면 없음(`SceneLoadOption.Direct()`, 교체 지점 주석 :120).
- 호출자: Unity(Start). 정적 값은 `GameInitializer`, `MvGameLoop`(Ui), 시험(`Assets/Game/Tests/PlayMode/BootFlowTests.cs`)이 읽는다.

### 1.2 `GameInitializer` — `MV.Boot/GameInitializer.cs`
- 종류: `MonoBehaviour`, Framework `jjevol.SceneInitializerBase` 파생(sealed). Game 씬 루트 오브젝트 `GameInitializer`에 붙어 있다.
- 책임: Game 씬 초기화(10 §10.3). `MvGameLoop`를 붙이고 `InitAsync`를 기다린다.
- 정적: `InitializeCount` (:17), `ResetStatics` (:19-20).
- `OnInitializeAsync()` (:22-40):
  - `InitializeCount++`.
  - **Boot 없이 Game 씬을 바로 실행한 경우**(`GameBootstrap.State == NotStarted && !ServicesReady`): `SceneManager.LoadScene("Boot", Single)`로 Boot부터 다시 연다(:25-31). (에디터에서 대체 경로로 데이터를 만들지 않는다는 저장소 규칙과 일치)
  - Boot는 돌았는데 `ServicesReady`가 거짓이거나 `MvTables.Current == null`이면 경고만 남기고 루프를 만들지 않는다(:32-37).
  - 정상: `gameObject.AddComponent<MvGameLoop>()` → `await loop.InitAsync(tables)` (:38-39).
- 호출자: Framework `SceneInitializerRunner`(CustomSceneManager의 씬 전환 파이프라인, 또는 요청 밖에서 씬이 열릴 때 `CustomSceneManager.HandleSceneLoaded` → `InitializeOutsideRequestAsync` — PackageCache `CustomSceneManager.cs:77-81,226-246`).

### 1.3 `MvBootMeta` — `MV.Boot/MvBootMeta.cs`
- 종류: `static class`.
- 책임: 부팅 4단계(표 검증 옵션)와 6단계 뒤(영속 변경 진입점 `MvMeta.Current` 생성)의 런 코어 연결. MV.Data는 MV.Run을, MV.Meta는 MV.Run을 모르므로 Boot가 사이를 잇는다.
- 정적: `_tables`, `TableOptions`(:21), `HasMeta`(:24), `MetaReady`(:27, `MvMeta.Current.IsReady`), `MetaUsesCurrent`(:30-33, MvMeta가 현재 저장소·표를 보는지), `ResetStatics`(:35-40).
- 메서드:
  - `CreateTableOptions()` (:43-47): `new MvValidationOptions { IsComponentImplemented = RunComponents.IsImplemented }` — 105 컴포넌트 type이 MV.Run에 구현됐는지 검증기에 넘긴다.
  - `LoadTablesAsync()` (:50-51): `MvTableLoader.LoadAsync(MvTableLoader.DefaultSource(), CreateTableOptions())`.
  - `Attach(MvTables)` (:60-68): `MvMeta.Current = null`, `MvSave.CurrentChanged`에 `OnSaveChanged` 구독(중복 방지로 -= 후 +=), `MV.UI.ContentMenuData.OpenMetaHandler = OpenMeta`(컨텐츠 메뉴 「성장」 탭 → 시스템 화면), 그리고 현재 저장으로 즉시 `OnSaveChanged(MvSave.Current)`.
  - `Detach()` (:71-78): 시험 정리용. 구독 해제, 핸들러 해제, `MvQuest.Current?.Dispose()`, `MvMeta.Current = null`.
  - `OpenMeta(string key)` (:81-84): key가 `MvResearchRules.UnlockKey`면 `MvUiNav.OpenResearchAsync()`.
  - `OnSaveChanged(MvSaveStore)` (:86-91): 저장이 열려 있고 `MvMeta.Current`가 아직 없을 때만 `MvMeta.Current = new MvMeta(() => MvSave.Current, () => MvBootSave.Resources, _tables)` 그리고 `MV.Content.MvQuest.Install(MvMeta.Current)`. **한 번 만들면 저장을 다시 열어도 같은 인스턴스**를 쓴다(공급 함수로 매번 현재 저장·재화 서비스를 읽음).
- 호출 대상: MV.Data(`MvTableLoader`), MV.Run(`RunComponents`), MV.Meta(`MvMeta`, `MvResearchRules`), MV.Save(`MvSave.CurrentChanged`), MV.Content(`MvQuest`), MV.UI(`ContentMenuData`, `MvUiNav`).
- 호출자: `GameBootstrap.RunAsync`(:94, :103).

### 1.4 `MvBootSave` — `MV.Boot/MvBootSave.cs`
- 종류: `static class`.
- 책임: 부팅 5·6단계. 표 값으로 저장 설정(`MvSaveConfig`)과 참조 정리 목록(`MvSaveReferenceCatalog`)을 만들어 `MvSave.Init` 호출, 열린 저장소에 Framework 재화 서비스(`ResourceService`)를 묶는다. 저장 API는 동기.
- 정적: `WealthTypeId = new ConsumeTypeId(1)`(07_consumeType 1=재화, :19), `LastOpen`(:22), `PendingNotice`(:28), `Resources : ResourceService`(:31), `_wealthDefinitions`(:33), `ResetStatics`(:35-42).
- 메서드:
  - `ConsumeNotice()` (:44-49): 안내를 꺼내고 비운다. **현재 게임 코드에서 `PendingNotice`/`ConsumeNotice`를 읽는 곳은 MV.Boot와 시험(`BootFlowTests.cs`)뿐**이다(grep 확인). 저장 열기 실패 팝업을 UI가 아직 읽지 않는다.
  - `Open(MvTables)` (:52-74): 표가 없으면 예외 → `MvWealthIds.Mismatches(01_wealth)`가 있으면 예외(부팅 중단) → `_wealthDefinitions` 저장 → `MvSave.CurrentChanged += BindResources` → `MvSave.Init(new MvSaveOptions{ Config = CreateConfig, References = CreateReferences })` → `LastOpen`, `PendingNotice` 기록 → `Report`(로그).
  - `Report` (:76-91): Notice가 None/ToastRecovered/그 밖(팝업 대기)에 따라 로그.
  - `BindResources(MvSaveStore)` (:94-103): `MvSave.CurrentChanged` 처리기. 이전 `Resources.Dispose()` 후, 저장이 열려 있으면 `new ResourceService(store.Repository, _wealthDefinitions, WealthTypeId)`.
  - `CreateConfig(MvTables)` (:106-123): `00_config`(startDeck·startGodKey·dataVersion·adDayUtcOffsetHour), `RunConfig CP_MAX`, `FloorCount()`, `08_adPlacement` dailyLimit → `MvSaveConfig`. 시작 덱은 `PadStartDeck`(:129-134)로 `WeaponTierMax()+1` 길이까지 0 채움.
  - `CreateReferences(MvTables)` (:137-164): 지역별 벽 칸 상한(`302_area`×`AreaFloor` rows*3), 신 key, 무기 단계 상한, Area 상한, 로비/Area 강화·환생 perk 상한, 광고 자리 key, 컨텐츠 개방 key. `ModeContentKeys = null`(400_content 표 미존재, :160-161).
- 호출자: `GameBootstrap`(:99), `MvBootMeta.OnSaveChanged`(공급 함수 `() => MvBootSave.Resources`), `MvGameLoop.PeekRunSeq/CreateSim`(저장이 없을 때 `CreateConfig`로 기본 스냅숏).

### 1.5 `MvSaveLifecycle` — `MV.Boot/MvSaveLifecycle.cs`
- 종류: `MonoBehaviour`(sealed), 코드로 만드는 숨은 싱글턴.
- 책임: 앱 일시정지·종료 때 메모리만 바뀐 저장을 디스크에 쓴다(09 §9.4).
- `Ensure()` (:15-22): 없으면 `new GameObject("[MvSaveLifecycle]") { hideFlags = HideInHierarchy }` + `DontDestroyOnLoad` + `AddComponent`.
- `OnApplicationPause(bool)` → `MvSave.OnApplicationPause(paused)` (:24); `OnApplicationQuit()` → `MvSave.OnApplicationPause(true)` (:26). `OnApplicationFocus`는 다루지 않는다.
- MV.Save 쪽: `MvSave.OnApplicationPause`는 paused·저장 열림·재열기 불필요일 때만 `Current.SaveIfDirty()`를 부르고 예외는 로그로 삼킨다(`MV.Save/MvSave.cs:71-76`). `SaveIfDirty`는 저장소가 dirty일 때만 쓴다(`MV.Save/MvSaveStore.cs:375-378`).
- 호출자: `GameBootstrap.RunAsync`(:100).

### 1.6 `MvRunSnapshot` / `MvRunStart` — `MV.Boot/MvRunStart.cs`
- `MvRunSnapshot` (plain class, :15-122): 런 시작에 쓰는 저장 값 스냅숏(MV.Run 타입 없이 값만). MV.Meta가 MV.Run을 모르므로 저장 → 스냅숏 → `RunContext` 변환을 Boot가 한다.
  - 필드: Area(0기반), Level, Cp, Souls(재화 `rescueCat`), God, Deck, UpStair/UpWeapon/UpIncome, AreaUps, Perks, Bars(Floor별 칸 → 남은 HP), LastSettledRunSeq.
  - `FromStore(MvSaveStore, floorCount)` (:38-61): 저장 컨테이너(Progress·Wealth·God·Lobby·AreaUp·Rebirth)에서 읽는다. 벽 기록은 `store.Progress.ToJsonString()`을 `ParseBars`로 다시 파싱(교체 지점 주석 :58).
  - `Defaults(MvSaveConfig)` (:64-73): 저장을 못 열었을 때 새 저장 기본값과 같은 스냅숏.
  - `ParseBars(json, area, floorCount)` (:86-106): `bars["A{area}"]` JSON → `Dictionary<int,double>[]`.
  - `SeedFor(runSeq)` (:112-121): SplitMix64. 같은 저장·같은 런 순번 = 같은 시드(결정적 런).
- `MvRunStart` (static, :125-201):
  - `CreateContext(snap, tables, seed)` (:131-159): `RunSetup`(Seed·Area·Zone(`tables.ZoneIndexOfArea`)·Level·Souls·Cp·Up*·God·Preset=null·Deck 복사·Bars→`BarRecord`) + `ApplyGrowth` → `new RunContext { Tables, Snapshot = setup, MetaModifiers = RunModifiers.Zero }`.
  - `ApplyGrowth` (:165-197): `13_areaUpgrade.statKey`(`AreaStatKey`)와 `14_rebirthPerk.statKey`(`RebirthStatKey`)로 Lv를 RunSetup 칸(AupRate…, PerkPower…)에 옮긴다. 항목 key 이름을 코드에 박지 않는다.
- 호출자: `MvGameLoop.PeekRunSeq`(:291-292), `MvGameLoop.CreateSim`(:301-305).

### 1.7 `MvRunInputReader` — `MV.Boot/MvRunInputReader.cs`
- 종류: plain class(sealed). Framework에 입력 패키지가 없어 게임 코드로 둔다.
- 책임: Input System에서 런 입력(04 §4.1.2)을 읽어 `MV.Run.RunInput`으로 만든다. 터치·마우스 모두 `Pointer.current`.
- 생성자 (:20-23): `dragScreenM`(표 `RunConfig DRAG_SCREEN_M`, 0 이하면 7.2).
- `Reset()` (:26): 새 런에서 드래그 기억을 버린다.
- `GameAreaWidth(w,h)` (:29): 9:16 게임 영역 폭.
- `Read(RunState)` (:31-51): 누른 순간 `_sx`(화면 x)와 `_px = s.P.Tx` 기억, 드래그 중 `DragTargetX = _px + (x − _sx) / (W / dragScreenM)` (:44). 키보드 ←/→ → `KeyAxis`. clamp·dt 곱은 MV.Run이 한다.
- `StartKeyPressed()` (static, :54-58): Space/Enter/NumpadEnter `wasPressedThisFrame`.
- 호출자: `MvGameLoop.InitAsync`(생성, :123), `TickRun`(:381), `StartRun`(Reset, :347), `Update`(StartKeyPressed, :362).

### 1.8 `MvGameLoop` — `MV.Boot/MvGameLoop.cs`
- 종류: `MonoBehaviour`(sealed). 씬에 미리 두지 않고 `GameInitializer`가 `AddComponent`로 붙인다. `[DefaultExecutionOrder]` 없음(기본 0). 참고로 MV.View의 `CameraRig`는 -100, `ObjectViewFactory` 55, `CharacterPresenter`·`ClimbStairView` 56, `ProjectileView` 57, `PropStreamer` 60, `DepthSorter` 100이라 **같은 프레임에서 카메라 리그가 먼저, 루프(0)가 그 다음, 뷰 컴포넌트들이 뒤**에 돈다.
- 책임: Game 씬 한 곳에서 로비 → 런 → 지도 → 결과 → 로비 순환(10 §10.3, MV-SPEC-02 01 §12-3). 월드 프리팹·스테이지 맵·이펙트 머티리얼 로드와 수명(lease) 관리.
- 상태 enum `MvLoopPhase { None, Loading, Lobby, Run, Result, Failed }` (:19-27).
- 정적 `Current` (:43, Awake에서 설정 :97, OnDestroy에서 해제 :101).
- 공개 상태(시험·QA용): `Phase`, `RunFrames`, `RunsFinished`, `SettleCount`, `LastSettleOk`, `LastFinishReason`, `CurrentRunSeq`, `CurrentSeed`, `LobbyObjectCount`, `LobbyFrame`, `WorldCameraActive`, `FxMaterialsLoaded`, `MapOpen`, `ResultOpen` (:45-64, :453, :477).
- 시험 훅: `TicksPerFrame`(기본 1), `FixedDt`, `ScriptedDrag` (:68-72). 공개 조작: `PressStart()`(:311), `PressMapClose()`(:456), `PressCollect()`(:470).
- 내부 참조: `_tables`, `_world : WorldView`, `_lobbySim`, `_lobbyObjs`, `_sceneCamera`, `_leases`(월드·머티리얼 수명), `_viewSource : LoopViewSource`, `_input`, `_lobbyScreen`, `_resultPopup`, `_mapPopup`, `_sim : RunSim`, `_runSeq`, `_savedArea/_savedSouls`, `_themeId`, `_stageArea`, `_stageMap`, `_stageLeases`(지역 맵 수명), `_ct`(OnDestroy 취소 토큰) (:74-92).

주요 메서드:
- `InitAsync(MvTables)` (:111-148): 아래 §2.2 흐름.
- `Alive()` (:151-154): await 뒤 오브젝트가 사라졌으면 `OperationCanceledException`.
- `Keep(lease, destroy)` (:156-160): `_leases`에 보관 → `OnDestroy`에서 일괄 Dispose(:99-106).
- `InstallFxAsync()` (:162-173): `FxRoot` 자식 생성 → 머티리얼 `MvFx.MatNormalAddress("MV_FxNormal")`, `MvFx.MatAddAddress("MV_FxAdd")`를 `AddressableScope.Get<Material>`로 로드(:175-181, lease는 `_leases`) → `MvFx.InstallDefault(fxRoot, _world.Rig, () => _sim?.State, normal, add)`.
- `TakeSceneCamera()` (:187-192) / `UseCamera(cam)` (:194-199): 씬 Main Camera를 찾아 두었다가 `GameBootstrap.Ui.mainCamera = 월드 카메라`, 씬 카메라 `enabled = false`(AudioListener는 남김). `UIManagerCreater.LateUpdate`가 카메라 변경을 감지해 UI 캔버스를 다시 붙인다(PackageCache `com.jjevol.ui.urp/Runtime/UIManagerCreater.cs:445-455`).
- `CurrentArea()` (:201-205): 저장의 `Progress.Area`, 없으면 0.
- `ApplyThemeAsync(area)` (:207-217): `302_area` 행 → `LoadStageMapAsync` → `themeId`가 바뀌었을 때만 `_world.SetThemeAsync(304_theme 행)`.
- `LoadStageMapAsync(area, row)` (:223-239): 지역이 바뀌었을 때만. 이전 `_stageLeases` Dispose → `_world.Objects.SetStage(_tables, row.stageMap)` → `AddressableScope.InstantiateWithLease(row.stageMap, _world.transform)` → `StageMap.Bind(_world.Rig.Cam, () => 카메라 Wy)`. 행·주소 없음/로드 실패는 `Debug.LogError`(대체 지면 없음).
- `EnterLobbyAsync()` (:243-258): `_sim=null`, `_viewSource.Run=null` → `ApplyThemeAsync` → `BuildLobbyTrack()` → `MvSound.EnterLobby()` → `await MvUiNav.OpenLobbyAsync(new LobbyViewData { OnStart = OnLobbyStart })` → `Phase = Lobby`.
- `BuildLobbyTrack()` (:264-275): 다음 런과 같은 순번·시드로 `RunSim`을 만들되 Tick하지 않음 → `LobbyTrack.Pick(..., RunConfig Lobby_ObjZMax, _lobbyObjs)` → 월드 활성·`Rig.Init`·카메라 사용 → `RebuildProps` → `SyncLobbyTrack`.
- `SyncLobbyTrack()` (:278-284): `_world.SyncObjects(_lobbyObjs, …)`, `_world.SyncCats(s)`.
- `PeekRunSeq()` (:287-294): 저장(또는 기본) 스냅숏의 `LastSettledRunSeq + 1`. `MvMeta.BeginRun`을 부르지 않는다.
- `CreateSim(runSeq, out snap, out seed)` (:297-308): 스냅숏 → `SeedFor` → `MvRunStart.CreateContext` → 저장이 있으면 `ctx.MetaModifiers = MV.Content.MvResearch.Modifiers(store, _tables)` → `RunSim.Create(ctx, new MainRunRule(builder), new Pcg32Rng(seed), builder)`.
- `OnLobbyStart()` (:318-332): 로비 화면의 출발 콜백. `StartRun()`; 예외면 로그 후 로비 재진입.
- `StartRun()` (:336-355): 아래 §2.3.
- `Update()` (:357-368): Lobby면 `SyncLobbyTrack` + 출발 키 확인; Run/Result면 `TickRun()`.
- `TickRun()` (:371-403): 아래 §2.4.
- `FinishRun()` (:409-427), `OpenMapThenResultAsync` (:433-450), `OpenResultAsync` (:463-467), `OnCollected` (:479-484): 아래 §2.5.
- 내부 클래스:
  - `CountingSettler : IResultSettler` (:487-512): 정산 횟수를 세고 실제 정산기(기본 `MvMetaResultSettler.Instance` → `MvMeta.Current.Settle`)에 넘김. 정산 성공 뒤 `MvQuest.Current?.RecordRun(_questRun)`로 런 안 퀘스트 카운터(계단·벽) 기록.
  - `NoSaveSettler` (:515-519): 저장이 없을 때 정산하지 않고 빈 결과.
  - `LoopViewSource : IWorldViewSource` (:522-534): 런이 있으면 `RunStateViewSource`의 카메라·절벽, 없으면 `WorldViewMode.Lobby` 카메라. 로비 → 런 전환 때 이 객체 하나의 `Run`만 바꿔 카메라가 로비 자세에서 런 자세로 쓸어 간다.
- 호출 대상 요약: MV.Run(`RunSim`, `MainRunRule`, `RunLevelBuilder`, `Pcg32Rng`, `RunInput`, `RunPhase`, `RunEventType`, `LobbyTrack`), MV.View(`WorldView`, `StageMap`, `RunStateViewSource`, `IWorldViewSource`), MV.UI(`MvUiNav`, `LobbyViewData`, `LobbyScreen`, `ResultViewData`, `ResultPopup`, `MapPopup`, `MetaPopupData`, `RunStateHudSource`, `IResultSettler`, `MvMetaResultSettler`), MV.Sound(`MvSound`), MV.Fx(`MvFx`), MV.Meta(`MvMeta`), MV.Save(`MvSave`), MV.Content(`MvResearch`, `MvQuest`), Framework(`AddressableScope`, `UIManagerCreater`).

---

## 2. 단계별 흐름 (file:line)

### 2.1 앱 시작 → 첫 화면(로비)
1. Unity가 빌드 0번 씬 `Boot.unity`를 연다. 루트: Main Camera, UI(`UIManagerCreater` + 자식 EventSystem), AudioManager, EffectManager, Boot(`GameBootstrap`) (§3).
2. Play 시작 전 `SubsystemRegistration` 단계에서 각 정적 클래스의 `ResetStatics`(GameBootstrap :47, GameInitializer :19, MvBootMeta :35, MvBootSave :35, MvGameLoop :94, MvSave `MV.Save/MvSave.cs:38`, MvTableLoader 등)와 Framework `CustomSceneManager.ResetSession`(sceneLoaded 구독, PackageCache `CustomSceneManager.cs:58-72`)이 돈다.
3. Framework 매니저들(AudioManager, EffectManager, UIManagerCreater)의 Awake. (각 Framework 내부)
4. `GameBootstrap.Start` (GameBootstrap.cs:56) → `RunAsync().Forget()`.
5. 1단계 Addressables 초기화 (:78) → 2단계 `AddressablesManager.InitAsync` (:86) → 3단계 오디오·이펙트 공급자 (:89-90).
6. 4단계 표 (:94): `MvBootMeta.LoadTablesAsync` (MvBootMeta.cs:50) → `MvTableLoader.LoadAsync` (`MV.Data/MvTableLoader.cs:35`): `GameTableSource.Configure(PackagedTableSource(useEncrypt:false))` → 처음이면 `RegisterAll` + `TableRepository.LoadAllAsync`, 재부팅이면 표마다 `LoadDatasAsync` → `MvTableValidator.ValidateRuntime` → 오류면 `MvTableValidationException` → `BuildIndexes` → `MvTables.Current = tables`. 원본은 `Assets/Game/GameData/Table/Resources`의 TableBuilder 산출물뿐(에디터 TSV 우회 없음, `MvTableLoader.cs:29-33`).
7. 5·6단계 저장 (:99): `MvBootSave.Open` (MvBootSave.cs:52) → `MvSave.Init` (`MV.Save/MvSave.cs:49`) → `SetCurrent` → `CurrentChanged` 발행 → `MvBootSave.BindResources`가 `ResourceService` 생성.
8. `MvSaveLifecycle.Ensure()` (:100) → 숨은 DDOL 오브젝트.
9. `MvBootMeta.Attach` (:103) → `CurrentChanged` 구독 + 즉시 `OnSaveChanged` → 저장이 열려 있으면 `MvMeta.Current` 생성 + `MvQuest.Install`.
10. 8단계 UI (:108-109): `MvUiBoot.InitAsync` (`MV.UI/Common/MvUiBoot.cs:21-25`) → `ConfigureOptions`(Localize = 키 그대로, ConfirmExitAsync = 항상 true) → `UIManagerCreater.StartInitAsync` (Addressables 주소 `UIManager` 프리팹, 씬 직렬화 `uiManagerAddress: UIManager`).
11. 9단계 (:112-117): `MvSound.InstallDefault()` (`MV.Sound/MvSound.cs:19-25`: `MvSoundPlayer`(AudioManager 출력, unscaled 시계, `() => MvTables.Current`) 설치 + `MvSoundDriver.Ensure()` — DDOL, `Update`에서 `MvSound.Tick()`), Muted 반영, 로컬 네트워크 서비스, `ServicesReady = true`.
12. 10단계 (:121): `CustomSceneManager.LoadSceneAsync("Game", Direct)` → Game 씬 로드 → Boot 씬 오브젝트 파괴(DDOL인 UI·SaveLifecycle·SoundDriver·Framework 싱글턴만 남음) → `SceneInitializerRunner`가 `GameInitializer.OnInitializeAsync` (GameInitializer.cs:22) await.
13. `GameInitializer` → `AddComponent<MvGameLoop>()` (:38; 이때 `MvGameLoop.Awake`로 `Current` 설정, MvGameLoop.cs:97) → `await loop.InitAsync(tables)` (:39).
14. `MvGameLoop.InitAsync` (MvGameLoop.cs:111-148):
   a. `GameBootstrap.Ui.StartInitAsync(_ct)` — 새 씬에서 UI 재연결 완료를 기다림 (:119-120).
   b. `_input = new MvRunInputReader(RunConfig DRAG_SCREEN_M)` (:123).
   c. `AddressableScope.InstantiateWithLease("WorldRoot", transform)` (:125) → `Keep` (:127) → `GetComponent<WorldView>()`, 없으면 예외 (:128-129) → `_world.Init(tables, _viewSource)` (:130).
   d. `ApplyThemeAsync(CurrentArea())` (:131) → 스테이지 맵 프리팹 + 테마 아틀라스.
   e. `InstallFxAsync()` (:134) → 머티리얼 2개 + `MvFx.InstallDefault`(`MV.Fx/MvFx.cs:35-55`: `MvFxPlayer`(EffectManager.Instance, fxRoot, `RunStateFxWorld`), `MvPopPool.FromTables`, `MvFlyLayer.FromTables`, 카메라 투영기, `MvFxRunner.Ensure()` — DDOL, `Update`에서 `MvFx.Tick()`).
   f. `TakeSceneCamera()` (:136) → `EnterLobbyAsync()` (:137) → 로비 화면 열림, `Phase = Lobby` (:257).
   g. 예외면 `Phase = Failed` 후 다시 던짐(:143-147). 취소는 그대로 던짐.
15. `CustomSceneManager.LoadSceneAsync`가 끝나면 `GameBootstrap.State = Succeeded` (GameBootstrap.cs:125).

에디터에서 Game 씬을 바로 Play한 경우: Framework `HandleSceneLoaded`가 `GameInitializer`를 요청 밖에서 실행 → `State == NotStarted`이므로 `SceneManager.LoadScene("Boot")` (GameInitializer.cs:25-31) → 위 1부터 정상 순서.

### 2.2 로비 프레임
- `MvGameLoop.Update` (:357-364): `Phase == Lobby` → `SyncLobbyTrack()`(로비 트랙 오브젝트·걷는 냥법사 동기화, 런 Tick 없음) → `MvRunInputReader.StartKeyPressed()`면 `PressStart()` → `_lobbyScreen.PressStart()` → 로비 화면이 `LobbyViewData.OnStart` = `OnLobbyStart` 호출.
- 로비 버튼(UI)으로 출발해도 같은 `OnLobbyStart`로 들어온다.

### 2.3 런 시작
1. `OnLobbyStart` (:318) — `Phase == Lobby`일 때만 → `StartRun()` (:323).
2. `StartRun` (:336-355):
   - 런 순번: `MvMeta.Current`가 준비됐으면 `meta.BeginRun()`(`MV.Meta/MvMeta.cs:102-107`, 마지막 정산 순번과 발급 순번 중 큰 값 + 1), 아니면 `PeekRunSeq()` (:339).
   - `CreateSim(_runSeq, …)` (:340) → 스냅숏(`MvRunSnapshot.FromStore` 또는 `Defaults`) → `SeedFor` → `MvRunStart.CreateContext` → 연구 메타 능력치(`MvResearch.Modifiers`) → `RunSim.Create(ctx, MainRunRule, Pcg32Rng, RunLevelBuilder)`.
   - 로비 미리보기 sim·오브젝트 목록 버림 (:342-343), `_savedArea/_savedSouls` 기억(결과 화면 비교용), `RunFrames = 0`, `_input.Reset()`.
   - `_viewSource.Run = new RunStateViewSource(_sim.State, _tables)` (:349) — 카메라가 로비 → 런 자세로 이동.
   - `_world.RebuildProps(...)` (:351), `Phase = Run` (:352).
   - `MvUiNav.EnterRunAsync(new RunStateHudSource(() => _sim?.State, () => _savedSouls))` (:354) — 로비 화면 숨김, `hudManager.CachingHudAsync` + `ShowHuds`로 런 HUD를 켜고 `RunHud.Bind(source)` (`MV.UI/Common/MvUiNav.cs:24-34`). Forget(비동기, 런은 이 프레임에 이미 시작).
3. 같은 로비 프레임 안에서 `Phase`가 Run이 되므로 `PressStart()`는 true를 돌려준다(:315).

### 2.4 런 프레임 루프 (`MvGameLoop.Update` → `TickRun`)
`Update` (:357-368): Phase가 Run 또는 Result이고 `_sim != null`이면 `TickRun()`.
`TickRun` (:371-403) 순서:
1. 입력 (:374-381): Result 단계면 `RunInput.None`; 시험 훅 `ScriptedDrag`가 있으면 그 값; 아니면 `_input.Read(s)`.
2. dt (:383): `FixedDt ?? Time.unscaledDeltaTime`(실시간, timeScale 무시).
3. 시뮬 (:384-389): `TicksPerFrame`번 `_sim.Tick(dt, input)`, 도중 `s.Phase == RunPhase.Result`면 멈춤. Run 단계면 `RunFrames++`.
4. 이벤트 (:392): `var events = _sim.DrainEvents()` — 한 프레임에 **한 번만** 비운다.
5. 같은 목록을 순서대로 전달 (:393-395): `MvSound.OnRunEvents(events)` → `MvFx.OnRunEvents(events)` → `_world.OnRunEvents(events)`(오브젝트 피격·우물 벽돌).
6. `RunEventType.ClimbStart` 이벤트마다 `_world.RebuildProps(e.Z, …, cliff:true, s.WorldY + e.Amount2)` (:396-398) — 계단 위 새 지형.
7. 뷰 동기화 (:399-400): `_world.SyncObjects(s.Objs, s.P.Z, s.WorldY)`, `_world.SyncCats(s)`.
8. 런이 막 끝났으면(`Phase == Run && s.Phase == Result`) `FinishRun()` (:402).
- HUD(`RunHud`)는 `RunStateHudSource`를 통해 상태를 **직접 읽는다**(이벤트를 받지 않음, 클래스 주석 :36).
- 소리·이펙트의 시간 진행은 루프가 아니라 각각의 DDOL 드라이버(`MvSoundDriver.Update` → `MvSound.Tick`, `MvFxRunner.Update` → `MvFx.Tick`)가 한다. 이 드라이버들과 `MvGameLoop`의 Update 순서는 `DefaultExecutionOrder`로 정해져 있지 않다.
- Result 단계에서도 `TickRun`은 계속 돈다(입력 None) — 결과 화면 뒤로 월드 연출이 이어진다.

### 2.5 런 종료 → 지도 → 결과 → 정산(저장) → 로비
1. `FinishRun` (:409-427): `Phase = Result`, `RunsFinished++`, `r = _sim.State.Result`, `_questRun = r`, `LastFinishReason`. 결과가 null이면 로비로 복귀.
2. `ResultViewData.Create(r, _runSeq, _savedArea, _savedSouls, _tables)` (:422). `Settler = new CountingSettler(this, meta 준비됨 ? null(→MvMetaResultSettler) : NoSaveSettler)` (:424), `OnCollected` 콜백 (:425).
3. `OpenMapThenResultAsync` (:433-450): `MvUiNav.OpenMapAsync(new MetaPopupData { OnClosed = OpenResult }, playClick:false)` — 지도 팝업을 먼저 연다. 지도를 닫으면 `OpenResult` → `OpenResultAsync` → `MvUiNav.OpenResultAsync(data)`(결과 팝업, `MvUiNav.cs:37-38`). 지도를 못 열면 결과로 바로.
4. 결과 팝업 「받기」(또는 시험 `PressCollect`, :470) → UI가 `data.Settler.Settle(outcome, mul)` 호출 → `CountingSettler.Settle` (:498-511) → `MvMetaResultSettler.Settle` (`MV.UI/Result/ResultContracts.cs:96-106`) → `MvMeta.Current.Settle(outcome, mul)` (`MV.Meta/MvMeta.cs` 정산 절, 「영속 반영은 이 한 번」) — **런 결과의 저장은 여기서 한 번** 일어난다. 성공이면 `MvQuest.Current?.RecordRun(_questRun)`.
5. UI가 `OnCollected(MvSettleResult)` 호출 (:479-484) → `EnterLobbyAsync()` → 로비 트랙 재구성(다음 순번), 로비 화면. 전면 광고(`result.ShowInterstitial`)는 2차(D-90).

### 2.6 앱 일시정지·종료 시 저장
- `MvSaveLifecycle.OnApplicationPause(true)` / `OnApplicationQuit()` (MvSaveLifecycle.cs:24-26) → `MvSave.OnApplicationPause(true)` (`MV.Save/MvSave.cs:71-76`) → `Current.SaveIfDirty()`(dirty일 때만, `MvSaveStore.cs:375`). 예외는 로그만.
- 런 정산·메타 명령은 각자 `MvMeta.Send` 경로에서 즉시 저장한다(MV.Meta 범위). Lifecycle은 「메모리만 바뀐 것」의 안전망이다.

### 2.7 저장 재열기(복구 팝업 등) 시 재배선
- `MvSave.Retry()/StartFresh()/Reopen()` (`MvSave.cs:56-69`) → `Init`/`OpenFresh` → `SetCurrent` → `CurrentChanged(store)`.
- 구독자: `MvBootSave.BindResources`(재화 서비스 새로 묶기, 이전 것 Dispose), `MvBootMeta.OnSaveChanged`(MvMeta가 아직 없으면 그때 생성 + MvQuest 설치; 이미 있으면 그대로).
- `MvSave.Shutdown()`은 `CurrentChanged(null)` → `BindResources`가 `Resources = null`.

---

## 3. 씬 구성 (YAML grep)

### 3.1 `Assets/Game/Scenes/Boot.unity` (15,995 바이트)
루트 5개(`SceneRoots`, :540-547): Main Camera, UI, AudioManager, EffectManager, Boot.

| 오브젝트 | 컴포넌트(스크립트 GUID) | 핵심 직렬화 값 |
|---|---|---|
| `Boot` | `MV.Boot.GameBootstrap` (058f17099e894a6fa76579b7f0dfba3d = `MV.Boot/GameBootstrap.cs.meta`) | `ui: {fileID: 1477456060}` → UI의 UIManagerCreater (:327-338) |
| `UI` | `jjevol.UIManagerCreater` (JJEvol.UI.URP.Runtime, 1dc16205…) | `mainCamera` → Boot Main Camera, `cashScreenTypes: []`, `cashHudTypes: []`, `createId: 0`, `uiManagerAddress: UIManager` (:428-443). 자식 `EventSystem`(EventSystem + `InputSystemUIInputModule`) |
| `AudioManager` | `jjevol.Audio.AudioManager` (2fa4332f…) | `initialPoolSize: 16`, `poolCanExpand: 1`, `defaultDebounceSeconds: 0.1`, 볼륨 1 (:380-388) |
| `EffectManager` | `jjevol.Effect.EffectManager` (7b235a05…) | `_defaultProfile { minRetain: 1, maxPool: 32, idleSeconds: 15 }`, `_profilesPreset: []` (:148-156) — Framework 이펙트 풀 기본값 |
| `Main Camera` | Camera + URP `UniversalAdditionalCameraData` | 태그 MainCamera |

Framework 스크립트 GUID는 Framework 패키지 안의 .meta라 게임 Assets에는 없고, `m_EditorClassIdentifier`로 확인했다(Boot.unity:150, 253, 337, 382, 438, 504, 535).

### 3.2 `Assets/Game/Scenes/Game.unity` (10,722 바이트)
- `GameInitializer` 오브젝트: `MV.Boot.GameInitializer` (a14d77b514884467943553991cbbe246 = `MV.Boot/GameInitializer.cs.meta`) (:237-247). 직렬화 필드 없음.
- `Main Camera`(URP) — `MvGameLoop.TakeSceneCamera`가 찾아 렌더를 끈다.
- `Directional Light`.
- 월드(`WorldRoot`)·FxRoot·스테이지 맵은 씬에 없고 런타임에 `MvGameLoop` 아래로 만들어진다.

### 3.3 씬을 만드는 도구
`MV.Editor/Setup/MvProjectSkeleton.cs`의 `CreateBootScene`(:102-124)·`CreateGameScene`(:126-138)이 위 구성을 처음 만든다(없을 때만). 씬 파일은 통합 Writer 영역이다(AGENTS.md).

---

## 4. 리소스 로드·캐시·풀·재사용 (Boot 관점)

### 4.1 Addressables 설정 (`Assets/AddressableAssetsData`)
- `AddressableAssetSettings.asset`: `m_BuildRemoteCatalog: 0`(:21), `m_BuildAddressablesWithPlayerBuild: 0`(:67, 0 = 환경설정 따름 → `MvBuild`가 판정해 필요 시 직접 빌드).
- 그룹(`AssetGroups/`, 각각 BundledAssetGroupSchema + ContentUpdateGroupSchema):

| 그룹 | 항목 수 | 라벨 | 주소 예 |
|---|---|---|---|
| Default Local Group | 0 (`m_SerializeEntries: []`) | — | — |
| Dev_Debug | 2 | debug | UIPopup_Debug |
| Local_Audio_Music | 3 | bgm | Bgm_Lobby, Bgm_Run |
| Local_Audio_Sfx | 32 | sfx | Sfx_Click, Sfx_Coin, Sfx_Tierup_0 |
| Local_BG | 14 | (없음) | Stage1_Block_Cat, Obj_Tower |
| Local_Character | 24 | char | atlas_player_0, Char_Clone |
| Local_Effect | 17 | fx | Paw, Bolt, Fireball (+ MV_FxNormal/MV_FxAdd 머티리얼) |
| Local_Projectile | 27 | (없음) | projectile_4, VFX_Tail_11 |
| Local_Texture | 6 | lobby, ui.core, ui.god | Tex_God_Flame, Tex_UI_Vignette |
| Local_UI | 14 | ui.core | UIManager, UIScreen_Lobby, UIPopup_Result |
| Local_UIAtlas | 6 | ui.core, ui.card, ui.lobby | Atlas_UI_Hud, Atlas_UI_Card |
| Local_World | 3 | world, lobby | WorldRoot, LobbyStage |
| Local_WorldAtlas | 33 | cat, fx, lobby, theme.<이름>(15종) | Atlas_World_Theme_cafe |

- 규칙: 주소 = 파일 이름(확장자 제외), 그룹 = `Assets/Game/Addressables/Local/<Category>` 최상위 폴더 → `Local_<Category>`. 원격 그룹 없음(언어팩만 원격 예정). 폴더는 `Assets/Game/Addressables/Local/{Audio,BG,Character,Effect,Font,Mode,Projectile,Texture,Theme,UI,UIAtlas,World,WorldAtlas}`.
- 그룹 항목·라벨·아틀라스는 손으로가 아니라 `MvArtSetup.EnsureAll`(§5.2)이 폴더 규칙으로 맞춘다(소유 그룹: UIAtlas·WorldAtlas·Texture·Character·BG·Projectile, Effect는 더하기만).

### 4.2 Boot가 쓰는 로드 경로
- **표**: Resources(`Assets/Game/GameData/Table/Resources/*.txt`, TableBuilder 산출물)에서 `PackagedTableSource`로 1회 로드 → `MvTables.Current`에 캐시(앱 수명 동안). 같은 세션 재부팅이면 `TableRepository.loaded`로 저장소 재등록 없이 다시 읽는다(`MvTableLoader.cs:37-48`).
- **UI**: `UIManagerCreater`가 Addressables `UIManager` 프리팹을 만들고, 화면(Screen)은 Framework가 풀링(`MvUiNav` 주석: 「로비: Screen(풀링)」), HUD는 `hudManager.CachingHudAsync` 후 `hudPool`에서 꺼냄(`MvUiNav.cs:24-34`), 팝업은 `SpawnUIPopupAsync`.
- **월드·맵·머티리얼**: Framework `AddressableScope`.
  - `Get<T>(key)` → `AddressablesManager.LoadAssetAsync<T>` + `manager.CaptureRelease(key)`를 담은 `AddressableLease<T>`(Dispose = 참조 해제) (PackageCache `com.jjevol.resources.addressables/Runtime/AddressableScope.cs:47-53`).
  - `InstantiateWithLease(key, parent)` → 프리팹 lease + `Instantiate` → `(instance, lease, destroyOnDispose)`; lease는 `RetainUntilDestroyed`로 **인스턴스가 실제로 파괴된 뒤 LastPostLateUpdate에 해제**된다(같은 파일 :65-86).
  - `MvGameLoop`는 `WorldRoot`·머티리얼 lease를 `_leases`(OnDestroy에서 Dispose), 지역 맵 lease를 `_stageLeases`(지역이 바뀌면 Dispose 후 새로 로드)에 보관한다. 같은 지역·같은 테마면 다시 로드하지 않는다(`_stageArea`, `_themeId` 비교, MvGameLoop.cs:212, 225).
- **소리**: `AudioManager` Addressables 공급자(3단계) + AudioManager 풀 16개(씬 값).
- **이펙트**: `EffectManager` + `AddressablesEffectPrefabProvider`(3단계), 풀 프로필 minRetain 1 / maxPool 32 / idle 15초(씬 값). 게임 쪽 팝업·비행 풀은 `MvPopPool.FromTables`, `MvFlyLayer.FromTables`(MV.Fx).
- **로비 ↔ 런 재사용**: `WorldView`는 앱 동안 하나를 유지하고, 로비 트랙과 런 모두 같은 `WorldView`에 `SyncObjects`/`SyncCats`/`RebuildProps`로 다시 그린다. 카메라 공급원 `LoopViewSource`도 하나를 유지하고 `Run` 필드만 교체한다. 런 오브젝트 뷰 풀링·재사용 세부는 MV.View(`ObjectViewFactory` 등) 범위.

### 4.3 `Assets/Resources`
- `BillingMode.json` = `{"androidStore":"GooglePlay"}` (Unity IAP 스토어 지정).
- `DOTweenSettings.asset` (DOTween 설정).
- 게임 표는 여기가 아니라 `Assets/Game/GameData/Table/Resources/`에 있다(`00_config.txt`, `01_wealth.txt`, `02_playResource.txt`, `03_sound.txt`, `04_effect.txt` …).

### 4.4 `Assets/Game/link.xml`
IL2CPP 코드 제거가 Framework 자동 등록(`RuntimeInitializeOnLoadMethod`: 오디오 Addressables 공급자, Addressables 씬 공급원, 씬 전환 UI 등)과 리플렉션으로 읽는 표·저장 형식을 지우지 않도록 **Framework·PlatformLibrary·게임 런타임 어셈블리 전부를 `preserve="all"`** 로 남긴다(JJEvol.* 19개, PlatformLibrary.* 18개, MV.Boot/Content/Core/Data/Fx/Meta/Platform/Run/Save/Sound/UI/View 12개). 계기: 실기 「Audio provider Addressables is not installed」(MV-BUILD-ANDROID, 파일 주석 1-2줄).

---

## 5. MV.Editor (요약)

`MV.Editor/MV.Editor.asmdef`: Editor 전용. 참조 MV.Core, MV.Data, **MV.Boot**, JJEvol.Tables(+Authoring.Editor), JJEvol.Data/Scene/UI/UI.Addressables/UI.URP/Audio/Effects, Unity.Addressables(+Editor), URP, InputSystem, UnityEngine.UI.

### 5.1 Build — `MV.Editor/Build/MvBuild.cs` (static)
- 중앙 실행기 build 모드가 `-executeMethod MV.Editor.Build.MvBuild.BuildAndroidDev`로 부른다. 끝에 `EditorApplication.Exit(code)`.
- 출력 `Builds/Android/MoonVillage-dev.apk`(:23). 종료 코드 0 성공, 1 빌드 실패, 2 대상 아님, 3 EDM4U 없음, 4 Addressables 실패, 5 PlayerSettings(IL2CPP·ARM64 아님), 6 표 산출물 실패(:28-34).
- `RunAndroidDev` 순서(:66-155): ① 활성 대상 Android 확인 → ② EDM4U `PlayServicesResolver.ResolveSync(true)` 리플렉션 → ③ AdMob Android 앱 ID가 비면 Google 공개 시험 ID 주입(`EnsureAdMobAndroidAppId`, :184-210) → ③b IL2CPP + ARM64 확인(`IsDeviceReady`, :164-167; 설정은 바꾸지 않음) → ③c **TableBuilder로 TSV → Resources 표 산출물 생성**(검증기 `MvTableAuthoring.ValidateBuildInputs`, 암호화 끔, :99-110) → ④ Addressables 자동 빌드 여부 판정 후 필요하면 `BuildPlayerContent` → ⑤ `BuildPipeline.BuildPlayer`(EditorBuildSettings의 enabled 씬, Development, APK) → Addressables 임시 `link.xml` 삭제.

### 5.2 Import — `MV.Editor/Import/*` (아트 입고 파이프라인)
- `MvArtPostprocessor` (AssetPostprocessor): `OnPreprocessTexture`에서 `Art/(Source|Temp)/Sprite/Atlas_*/*.png`, `Art/(Source|Temp)/Texture/*.png`에 manifest의 PPU·피벗·9-slice와 공통 값을 적용(`MvArtImport.Apply`). `OnPostprocessAllAssets`에서 아트·아틀라스·캐릭터·BG·투사체·이펙트 폴더가 바뀌면 `MvArtSetup.RequestEnsure()`.
- `MvArtSetup` (static, 메뉴 `MV/Art/Ensure Import·Atlas·Addressables`, 배치 `RunBatch`): `EnsureAll()` = 임포트 값 → SpriteAtlas v2(폴더마다) → 캐릭터 아틀라스 → Addressables 그룹 항목·주소·라벨을 멱등으로 맞춤. 대화형에선 `delayCall`로 1번, 배치 모드면 즉시.
- `MvArtRules`: 경로 규칙(`TryParse`), 갈래(World/UI/Texture), 아틀라스 사양(`MvAtlasSpec`: 형식·라벨), 그룹 이름 상수(`Local_<Category>`), 소유 그룹 목록.
- `MvArtImport`: PNG 1장 임포트 값 계산·적용·차이(`Desired/Apply/Differences`).
- `MvArtManifest`: 추출 manifest·`sprite_catalog.tsv`·`temp_art_list.tsv` 읽기.
- `MvArtCatalog` (`IMvResourceCatalog`): 프로젝트의 스프라이트·아틀라스·주소 존재 확인(표 리소스 열 검사용).
- `MvAtlasBudget`: 아틀라스 크기·압축 메모리 추정(MaxRects, ASTC).
- `MvCharacterArt`: 캐릭터 PNG → 아틀라스 이름(`atlas_player_0` 등), 캐릭터 프리팹 주소.
- `MvFolderArt`: 폴더 규칙 항목(BG 프리팹, 투사체 프리팹·그림, 이펙트 프리팹) → 주소 = 파일 이름, 중복 보고.
- `MvResourceColumnChecker`: 표의 리소스 이름 열이 실제 그림·아틀라스·주소와 맞는지 검사(오류/미추출/다른 계약 대기/정보), 미추출 목록 작성.

### 5.3 Setup — `MV.Editor/Setup/MvProjectSkeleton.cs` (static)
- 메뉴 `MoonVillage/Setup/Build Project Skeleton`, 배치 `RunBatch`. 없는 것만 만든다: Addressables 로컬 폴더들, `Art/Source`, 표 TSV·Resources 폴더, Addressables 기본 설정, Boot·Game 씬(§3.3), EditorBuildSettings(Boot, Game).

### 5.4 Tables — `MV.Editor/Tables/*`
- `MvTableAuthoring` (`[InitializeOnLoad]` static): 정적 생성자에서 `Register()` → Framework `TableAuthoringOptions.ValidateTables = ValidateBuildInputs`, `ValidationVersion = "mv-data-1"`, 암호화 끔. TSV 경로 `Assets/Game/GameData/Table/TSV`, 산출물 `Assets/Game/GameData/Table/Resources`. `Validate` = `MvTableValidator.ValidateAuthoringTsv` + `MvResourceColumnChecker.Check` 합침(리소스 오류만 반영 거부). 메뉴 `MV/Tables/Validate TSV`.
- `MvSheetTsvExporter` (같은 파일): 시트 복사 TSV → 표 TSV(앞 id 입력 열 `table, category, index` 제거). 메뉴 `MV/Tables/Export Sheet TSV…`.
- `MvConsumeTypeCodegen`: `07_consumeType.txt` → `MV.Data.ConsumeType` enum 코드 생성(Framework `ConsumeTypesGenerationSettings`). 메뉴 「계획 보기」/「적용」. 손으로 쓴 enum과의 충돌로 이관은 Framework 요청 대기(주석).

---

## 6. Framework 사용 요약 (MV.Boot가 부르는 API만)

| Framework | MV.Boot 호출 지점 | 뜻 |
|---|---|---|
| `AddressablesManager.Instance.InitAsync()` (jjevol.resources.addressables) | GameBootstrap.cs:86 | Addressables 관리자 준비(로드·참조 계수·인스턴스 추적) |
| `AddressableScope.Get<T>` / `InstantiateWithLease` | MvGameLoop.cs:125, 177, 232 | 참조 계수 lease 기반 로드·생성 |
| `AudioManager.Instance.SetProvider(ProviderKind.Addressables)` (jjevol.Audio) | GameBootstrap.cs:89 | 소리 클립 공급자 |
| `EffectManager.Instance.SetPrefabProvider(new AddressablesEffectPrefabProvider())` (jjevol.Effect) | GameBootstrap.cs:90 | 이펙트 프리팹 공급자 |
| `UIManagerCreater.StartInitAsync` / `.mainCamera` (jjevol UI URP) | MvUiBoot.cs:24, MvGameLoop.cs:120, 197 | UIManager 생성·씬 재연결·카메라 바인딩(LateUpdate 재연결) |
| `CustomSceneManager.Instance.LoadSceneAsync(name, SceneLoadOption.Direct())` (jjevol.scene) | GameBootstrap.cs:121 | 씬 전환 + 씬 초기화기 await |
| `SceneInitializerBase.OnInitializeAsync` | GameInitializer.cs:22 | 씬 초기화 훅 |
| `NetworkManager.SetLocalService`, `DefaultRoute = Local` (PlatformLibrary.Network) | GameBootstrap.cs:115-116 | 로컬 서버(백엔드 없음) |
| `ResourceService(repository, WealthTable, ConsumeTypeId)` (jjevol.Economy) | MvBootSave.cs:102 | 재화 서비스 |
| `UIRuntimeOptions.Localize`, `UINavigationOptions.ConfirmExitAsync` | MvUiBoot.cs:16-18 (MV.UI) | UI 정적 옵션(Play마다 Framework가 비움) |

---

## 7. 정적 등록소·연결 패턴 정리

| 정적 슬롯 | 설정하는 곳 | 읽는 곳(Boot 범위) |
|---|---|---|
| `MvTables.Current` | `MvTableLoader.LoadAsync` (4단계) | GameInitializer, MvBootSave, MvSound/MvFx 공급 함수 |
| `MvSave.Current` + `CurrentChanged` | `MvSave.Init/Retry/StartFresh/Reopen/Shutdown` | MvBootSave.BindResources, MvBootMeta.OnSaveChanged, MvGameLoop(스냅숏·Area) |
| `MvBootSave.Resources` | `BindResources` | `MvMeta`(공급 함수) |
| `MvMeta.Current` | `MvBootMeta.OnSaveChanged` | MvGameLoop.StartRun/FinishRun, MvMetaResultSettler |
| `MvQuest.Current` | `MvQuest.Install` (MvBootMeta) | CountingSettler.Settle |
| `MvSound.Player` | `MvSound.InstallDefault` (9단계) | MvGameLoop(EnterLobby, OnRunEvents) |
| `MvFx.Player` | `MvFx.InstallDefault` (MvGameLoop.InstallFxAsync) | MvGameLoop.TickRun |
| `GameBootstrap.Ui/State/ServicesReady` | GameBootstrap.RunAsync | GameInitializer, MvGameLoop |
| `MvGameLoop.Current` | MvGameLoop.Awake | 시험·개발 화면 |
| `ContentMenuData.OpenMetaHandler` | MvBootMeta.Attach | MV.UI 컨텐츠 메뉴 |

- 수명 오브젝트(DDOL): UI 부트스트랩(GameBootstrap :74), `[MvSaveLifecycle]`, `MvSoundDriver`, `MvFxRunner`, Framework 싱글턴(CustomSceneManager 등). Game 씬의 `MvGameLoop`는 씬 수명이다.
- 모든 정적 슬롯은 `SubsystemRegistration` 시점 `ResetStatics`로 비운다 → Domain Reload를 꺼도 Play마다 깨끗하게 시작.

---

## 8. 눈여겨볼 점·교체 지점(코드 주석 근거)
- 7단계 다국어 없음(GameBootstrap.cs:105) — 문구 키 그대로.
- 로딩 화면 없음(`SceneLoadOption.Direct()`, :120).
- Managed Addressables Revision 로더 미사용(:76-77 주석; 주석은 1.3.12-pre.10 기준이나 `Packages/Frozen`에는 `com.artress.managed-addressables-1.3.12-pre.20.tgz`가 있다 — 주석이 고정본보다 뒤처졌을 수 있음, 확인 필요).
- `MvBootSave.PendingNotice`를 UI가 아직 소비하지 않음(grep: Boot와 시험만).
- 런 메타 능력치: 펫·장비·동행은 `RunModifiers.Zero`(MvRunStart.cs:127-129), 연구만 `MvResearch.Modifiers`로 반영(MvGameLoop.cs:306).
- 벽 기록은 JSON 재파싱(MvRunStart.cs:58) — MV.Save에 `GetBars`가 생기면 교체.
- 선택을 기다리는 런 상태가 생기면 `TickRun`에서 자동 선택 지점(MvGameLoop.cs:38 주석).
- 전면 광고는 2차(MvGameLoop.cs:482).
- `400_content` 표가 없어 `ModeContentKeys = null`(MvBootSave.cs:160-161).
