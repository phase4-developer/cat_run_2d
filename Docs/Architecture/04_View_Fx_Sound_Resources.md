# 04. 표현 계층과 리소스 (MV.View · MV.Fx · MV.Sound · Addressables)

기준 커밋: `dev` 046c8be6 (2026-11-15). 경로는 저장소 루트 기준이고 `파일:줄`은 그 커밋의 줄 번호다. Framework 줄 번호는 `Library/PackageCache` 풀린 본 기준이라 참고용이다. 전체 지도는 [README.md](README.md).

---

## 0. 한눈에 보기

- 시뮬(MV.Run `RunSim`)과 화면은 **한 방향**이다. `MvGameLoop.TickRun`(MV.Boot)이 매 프레임 `Tick → DrainEvents` 한 번 → 같은 이벤트 목록을 `MvSound`·`MvFx`·`WorldView`에 넘기고, 이어서 `WorldView.SyncObjects`·`SyncCats`로 상태를 뷰에 맞춘다(`Assets/Game/Scripts/MV.Boot/MvGameLoop.cs:371-403`).
- 뷰는 상태를 바꾸지 않는다. 뷰가 읽는 창은 `IWorldViewSource`(카메라 입력·옹벽, `MV.View/Runtime/WorldViewSource.cs:9-14`), `IMvFxWorld`(`MV.Fx/MvFxWorld.cs:15-27`), 그리고 `RunState` 직접 읽기다.
- 모든 Addressables 로드는 두 갈래다.
  1. 게임 코드 → Framework `jjevol.AddressableScope.Get<T>/GetPrefab/InstantiateWithLease` → `AddressablesManager`(키당 핸들 1개 + 참조 수) → `AddressableLease<T>`(Dispose = 참조 1 반환).
  2. Framework 매니저 자체 공급자: 이펙트 `AddressablesEffectPrefabProvider`, 소리 `AddressablesClipProvider`. 이 둘은 `AddressablesManager`를 거치지 않고 `Addressables.LoadAssetAsync`를 직접 부르고 자체 참조 수/풀을 가진다.
- Spine·Animator는 MV.View에 없다(`grep spine|Animator` 결과 없음). 캐릭터는 SpriteAtlas 그림 교체 + 코드 계산 흔들림(`CharStateMachine`)이다.

## 1. 어셈블리·의존

| asmdef | 참조(요지) | 비고 |
|---|---|---|
| `MV.View` | MV.Core, MV.Data, MV.Run, JJEvol.Resources.Addressables.Runtime, URP, UniTask, TMP, Unity.Addressables, JJEvol.Tables.Runtime (`MV.View/MV.View.asmdef`) | 월드 렌더 |
| `MV.Fx` | MV.Core, **MV.View**, JJEvol.Effects.Runtime, JJEvol.Effects.Addressables, MV.Data, MV.Run (`MV.Fx/MV.Fx.asmdef`) | `CameraRig`·`DepthSortable`·`MvColor`를 MV.View에서 빌린다 |
| `MV.Sound` | MV.Data, MV.Run, jjevol.Audio | MV.View를 모른다 |
| `MV.Boot` | 위 셋 + UI·Meta·Save | 셋을 묶는 유일한 곳 |

## 2. 실행 순서(DefaultExecutionOrder)와 프레임 단계

| 순서 | 클래스 | 콜백 | 하는 일 |
|---|---|---|---|
| −1000 | Framework `AudioManager` | – | (패키지) |
| 0 (기본) | `MvGameLoop` | `Update` | Tick → 이벤트 → Sound/Fx/World 이벤트 → `SyncObjects`·`SyncCats` |
| 0 | `MvFxRunner`·`MvSoundDriver`(숨은 DontDestroyOnLoad) | `Update` | `MvFx.Tick`(팝업·비행·꼬리) / `MvSound.Tick`(지연 재생) |
| −100 | `CameraRig` | `LateUpdate` | 카메라 모델 Step + 흔들림 → 위치·LookAt·FOV (`MV.View/Runtime/CameraRig.cs:56-74`) |
| 50 | `GroundStripMesh` | `LateUpdate` | 지면 띠 메쉬 재생성 |
| 60 | `PropStreamer` | `LateUpdate` | 소품 그리기 범위 출입 → 풀 대여/반납 (`PropStreamer.cs:90-101`) |
| 60 | `StageMap` | `LateUpdate` | 배경·바닥 판 스냅·길가 장식 (`StageMap.cs:61-75` 파일 내 기준) |
| 100 | `DepthSorter` | `LateUpdate` | 빌보드 회전 일괄 → 깊이 정렬·근평면 숨김·안개 알파 (`DepthSorter.cs:21-27`) |
| 110 | `TintLayer` | `LateUpdate` | 화면 곱하기 쿼드 |
| 120 | `ScreenRig` | `LateUpdate` | 화면 앞 눈(Snow) |
| 55/56/57 | `ObjectViewFactory`/`CharacterPresenter`·`ClimbStairView`/`ProjectileView` | (콜백 없음) | 순서 속성만 있고 실제 동기화는 `MvGameLoop.Update`에서 직접 호출된다 |

주의: `ObjectViewFactory.Sync`는 `Update`에서 `_rig.Model.Pos.z`(직전 프레임 LateUpdate 결과)를 그리기 범위 기준으로 쓴다(`WorldView.cs:104-108`). 카메라는 그 뒤 LateUpdate에서 움직이므로 범위 판정은 1프레임 늦은 카메라 기준이다.

---

## 3. MV.View 클래스 목록

### 3.1 루트·공급원

**`WorldView`** — `MV.View/Runtime/WorldView.cs`, MonoBehaviour, `WorldRoot` 프리팹 루트.
- 책임: 월드 렌더 부품을 묶는 입구. 직렬화 참조: `CameraRig _rig`, `DepthSorter`, `GroundStripMesh`, `PropStreamer`, `TintLayer`, `ObjectViewFactory`, `ScreenRig`, `CharacterPresenter _cats`, `ProjectileView`, `ClimbStairView`(`:18-30`). 공유 `WorldSpriteCache _sprites`를 소유(`:32`), Fx 아틀라스 상수 `FxAtlas = "Atlas_Fx_Common"`(`:16`).
- `Init(MvTables, IWorldViewSource)`(`:46-77`): `ViewConfig.From(RunConfig)` 후 각 부품 Init. `_cats`·`_projs`·`_stairs`가 프리팹에 없으면 GameObject를 만들어 AddComponent(예전 프리팹 호환).
- `SetThemeAsync(ThemeData)`(`:80-95`): 테마 아틀라스(`304_theme.worldAtlas`)와 `Atlas_Fx_Common`을 **먼저** 로드 → 각 부품 `SetTheme` → 이전 테마 아틀라스가 다르면 `ReleaseAtlas(prev)`.
- `RebuildProps(from,len,cliff,groundY)`(`:98`): `PropStreamer.Rebuild` 위임(정본 `buildEnv`).
- `SyncObjects(objs, playerZ, groundY)`(`:104-108`): `ObjectViewFactory.Sync(objs, cameraZ, playerZ, groundY)`.
- `SyncCats(RunState)`(`:114-121`): 캐릭터·투사체·계단 칸 동기화(null이면 Hide).
- `OnRunEvents(events)`(`:124-128`): 오브젝트 반응·캐릭터 기쁨/쏘기.
- `OnDestroy` → `_sprites.Dispose()`(`:133`) = 모든 월드 아틀라스 lease 반환.
- 호출자: `MvGameLoop`(MV.Boot)만.

**`IWorldViewSource` / `FixedWorldViewSource` / `RunStateViewSource`** — `MV.View/Runtime/WorldViewSource.cs`.
- `IWorldViewSource { CameraInput Camera; bool TryGetCliff(out CliffInfo) }`(`:9-14`).
- `RunStateViewSource`(`:35-74`): `RunState` → `CameraInput{Mode=Run/Climb, Px, Pz, Wy}`. Climb 단계면 `ClimbPath.Cat`로 플레이어 높이·z 계산(`:49-63`). 옹벽은 `IRunViewState.HasCliff/Cliff`.
- MV.Boot의 `MvGameLoop.LoopViewSource`(`MvGameLoop.cs:522-534`)가 래퍼: `Run`이 null이면 `WorldViewMode.Lobby` 입력을 준다. 로비→런 전환은 이 `Run` 필드 교체 하나로 카메라가 로비 자세에서 런 자세로 쓸어 간다(`MvGameLoop.cs:349`).

### 3.2 카메라·정렬·빌보드

**`CameraRig`** — `Runtime/CameraRig.cs`, MonoBehaviour(RequireComponent Camera), order −100.
- `Init(tables, source)`(`:26-41`): `CameraModel.FromTables`(표 `320_camera` 행 run/climb/lobby/side), 원근 카메라, near/far, 로비 자세로 Snap.
- `SetTheme`(`:46-49`): 테마 `fog` 색 = 카메라 배경색.
- `Shake(amp,time)`(`:52`): `CameraShake.Request`(최댓값 병합). 호출자: `WorldView.Shake`, `MvFxPlayer.Apply`(`MV.Fx/MvFx.cs:140`).
- `LateUpdate`(`:56-64`): `dt = min(time.dtCap, unscaledDeltaTime)`, 흔들림 Step(표시 전용 `UnityEngine.Random`), `_model.Step(source.Camera, dt, shake)` → `Apply`(위치·LookAt·종횡비 보정 FOV).

**`CameraModel`** — `Math/CameraModel.cs`, 순수 C#. 정본 `syncScene` 카메라 공식. `WorldViewMode {Lobby, Run, Climb}`(`:8`), `CameraInput` 구조체(`:11-19`), `Step(in CameraInput, dt, shake)`(`:64`), `Pos/Look/CamLift/Ck/OrbitK`.

**`Billboard`** — `Runtime/Billboard.cs`, MonoBehaviour. 정적 목록 `s_all`에 OnEnable 등록/OnDisable O(1) 제거(`:59-77`). `SyncAll(cameraRotation)`(`:79-86`): 회전 = 카메라 회전 복사(LookAt 구형 빌보드 아님) × `Roll`(종이 쓰러짐용 화면 평면 기울기). 호출자: `DepthSorter.LateUpdate`만.
- MV-VIEW-BILLBOARD2(876446e0) 규칙: 오브젝트 프리팹은 「서 있는 이미지들을 빈 부모 `Billboard` 하나로 묶고 그 부모에만 Billboard」를 붙인다. 코드는 프리팹에 Billboard를 자동으로 붙이지 않고, 없으면 오류 로그 1회(`ObjectViewFactory.cs:230-232`). 바닥에 까는 이미지는 묶음 밖.

**`DepthSortable`** — `Runtime/DepthSortable.cs`, MonoBehaviour(RequireComponent SortingGroup). 월드 그림 단위 루트. 정적 목록 `All`.
- 필드: `Rank`(0 소품, 1 오브젝트, 2 유닛, 3 이펙트 — `DepthMath.RankProp/Object/Unit/Fx`, `Math/DepthMath.cs:15`), `Bias`, `SortOffsetY`(탐침 높이), `Fog`.
- 메서드: `SetAlphaScale`(그림 자체 알파), `SetFixedAnchor`, `Refresh()`(자식 SpriteRenderer·IFogAlphaTarget 다시 수집 — 자식을 바꾼 뒤 반드시 호출), `SetBaseColor`, 내부 `ApplyVisible/ApplyOrder/ApplyFogAlpha`.

**`DepthSorter`** — `Runtime/DepthSorter.cs`, order 100. `LateUpdate` → `Billboard.SyncAll` → `SortAll(DepthSortable.All, …)`(`:21-49`): 카메라 전방축 투영 깊이 d, 근평면 컷이면 숨김, `sortingOrder = DepthMath.SortingOrder(d, bias, rank, quant)`, 안개 알파 `FogAlpha(dAnchor)`. 정본 「화가 알고리즘」 대체.

**`Math/*`(순수 계산, 시험 직접 호출)**: `DepthMath`(깊이·안개·정렬 순서·`RankFarFirst`), `ViewConfig`(`300_runConfig` 뷰 키 1회 읽기, 코드 기본값 없음), `CatLayout`(분신 오프셋·계단 y), `ObjectLayout`(우물 벽돌·잡동사니 지터), `ObjectReaction`(맞음 흔들림/번쩍/눌림/쓰러짐 시간·곡선), `ProjLayout`(투사체·창·회전 장난감 배치와 등급별 그림 주소), `PropLayout`(소품 배치·그리기 범위), `ClothLineLayout`, `FenceDeco`, `GroundStripBuilder`, `BoxBillboardBuilder`, `MeshBuffer`(재사용 정점 버퍼), `MvColor`(정본 색 함수, 결과 캐시), `ScreenLayers.SnowField`, `LobbyTrack.Pick`(로비 트랙에 깔 오브젝트 선택).

### 3.3 월드 그림 캐시

**`WorldSpriteCache`** — `Runtime/WorldSpriteCache.cs`, 순수 C#(IDisposable). `WorldView`가 1개 소유하고 `PropStreamer`·`ObjectViewFactory`·`ScreenRig`·`CharacterPresenter`·`ProjectileView`에 같은 인스턴스를 넘긴다(`WorldView.cs:52-69`).
- `LoadAtlasAsync(atlasKey)`(`:31-49`): `AddressableScope.Get<SpriteAtlas>` → `Entry{Lease}`. 이미 있으면 true. 대기 중 같은 키가 먼저 들어오면 새 lease를 즉시 Dispose(중복 참조 방지).
- `Get(atlas, sprite)`(`:52-64`): `SpriteAtlas.GetSprite`가 호출마다 **복제본**을 만들므로 이름당 1번만 부르고 캐시. 없는 이름은 `World_Missing`으로 대체(경고 1회).
- `GetAny`(분할 아틀라스 순회), `GetFrames(atlas, prefix, n)`(`_00`~`_NN` 배열, 런 중 할당 0).
- `ReleaseAtlas(key)`(`:90-99`): 캐시한 복제 Sprite를 `Object.Destroy` → lease Dispose(순서 중요).
- `Dispose`: 전부 ReleaseAtlas. UGUI용 Framework `AddressableImage`가 Image 전용이라 월드용을 게임에 둔 것(`:12-16` 주석).

### 3.4 오브젝트 뷰(풀·재사용의 중심)

**`ObjectViewFactory`** — `Runtime/ObjectViewFactory.cs`(1144줄), MonoBehaviour, order 55.
- 상태: `_views : Dictionary<int objId, View>`(`:122`) = **sim 오브젝트 id로 키를 잡은 뷰 레지스트리**. 풀은 두 종류: 코드 모양 풀 `_pool : Dictionary<ObjectShape, Stack<View>>`(`:123`), 프리팹 풀 `_prefabPool : Dictionary<string address, Stack<View>>`(`:128`). 프리팹 lease 캐시 `_prefabs : Dictionary<string, AddressableLease<GameObject>>`(`:129`), 로딩 중 집합 `_prefabLoading`(`:130`).
- `View`(`:41-78`): 루트 `Go`(SortingGroup + DepthSortable), `Obj`(현재 묶인 `RunObject`), `Shape`, `Prefab`(주소, null = 코드 모양), `Inst`(프리팹 인스턴스), `Rig`(`ObjectPrefabRig`), `P`(`PrefabParts` — Glow·Orbs·Curtain·Circle_Spin·Body/Chest_i·Sparks), 코드 모양용 `Parts`(SpriteRenderer 슬롯)·`Base`, 반응 상태 `Shake/Flash/Punch/Die/Gone/Grow`.
- `ObjectShape`(`:14`) = {Single, Junk, RollGate, WarpGate, Well, Stair, CoinPile, CatnipPot, Mouse}. `ShapeOf(ObjectKindData)`(`:370-378`)는 `105_objectKind.viewShape` 이름 1:1 매핑, 없으면 Single + 경고 1회.
- `PrefabFor(tables, stageMap, o)`(`:176-182`): `305_stageObject`(맵·종류·보상) → 없으면 `105_objectKind.prefab` → 없으면 null(코드 모양으로 그림).
- `Init(cfg, camera, sprites, fxAtlas)`(`:145-152`), `SetTheme`(`:155-159`: 아틀라스 바꾸고 `Clear()`), `SetStage(tables, stageMap)`(`:165-173`: Clear 후 105 전체 prefab + 이 맵의 305 prefab을 **Preload**).
- `Preload/LoadPrefabAsync`(`:198-221`): `PrefabLoader`(기본 `AddressableScope.GetPrefab`, 시험이 교체 가능 `:39`). 실패하면 `_prefabs[address] = null`로 남겨 **재시도하지 않는다**(표 주소 오류를 드러냄). 파괴/취소 후 도착한 lease는 즉시 Dispose.
- `EnsureInstance(View)`(`:224-236`): lease가 있으면 `Instantiate(lease.Asset, v.Go.transform)` 1번, `ObjectPrefabRig`·`PrefabParts`(`BuildParts :239-294`) 수집, `Ds.Refresh()`. 아직 로드 전이면 Preload만 하고 그리지 않음 → 이후 프레임 `AnimatePrefab`이 다시 `EnsureInstance`(`:983`).
- `Sync(objs, pz, playerZ, groundY)`(`:393-420`): 아래 §7.3 흐름.
- `OnRunEvents(events)`(`:449-472`): `Hit`(106_hitReaction objShake/flash/punch), `Break`(종이 쓰러짐 시작 `StartDie`), `WellFlush`(우물 벽돌/수정탑 구슬 빼기). 뷰가 없는(그리기 범위 밖) 오브젝트 이벤트는 버린다.
- `Release(View)`(`:576-593`) / `ResetMotion`(`:561-574`) / `Show`(`:595-632`) — §7.3.
- 코드 모양 그리기: `Build`(`:666-702`) → `BuildJunk/BuildRollGate/BuildWarpGate/BuildWell`, `Part(v, slot, atlas, sprite, …)`(`:641-664`: 슬롯 SpriteRenderer가 없으면 `Billboard`+`SpriteRenderer` 자식 생성, 있으면 재사용). `BuildLabel`(`:861-874`: `WorldLabel` TMP, 체력 라벨 또는 105.labelKey).
- 매 프레임 `Animate`(`:890`) / `AnimatePrefab`(`:981`): 관문 흐려짐, 롤 관문 가라앉음, 반짝이, `Rig.SetFill(hp, max)`(`:1048`) 등.
- `OnDestroy`(`:337-343`): CTS 취소 + 모든 prefab lease Dispose.
- 시험·QA 읽기: `ViewOf`, `TryPrefabView`, `TryReaction`, `GoneBricks/GoneOrbs`, `TryMotion`, `ViewCount`.

**`ObjectPrefabRig`** — `Runtime/ObjectPrefabRig.cs`. 프리팹 단계 그림 `_stages[]`(츄르 더미 Pile_1~3, 마법석 Crystal_L·C·R). `SetFill(hp,max)` → `ShownStages = ceil(n·hp/max)`(hp>0이면 최소 1). 단계 수 = 배열 길이(프리팹이 정함, 코드 상수 없음).

**`WorldLabel`** — `Runtime/WorldLabel.cs`. TMP 월드 텍스트, 공유 머티리얼(라벨별 인스턴스 금지), `SetText`는 값이 바뀔 때만 재생성. `IFogAlphaTarget`.

**`VertexColorFog`** — `Runtime/VertexColorFog.cs`. 정점색 메쉬(상자·담장·관문 배경)의 안개 알파를 정점색 알파에 다시 쓴다(MPB·머티리얼 복제 없음).

### 3.5 캐릭터(플레이어·분신)

**`CharacterPresenter`** — `Character/CharacterPresenter.cs`, MonoBehaviour, order 56. `CatView`를 대체.
- `Init(tables, cfg, sprites, fallbackMaterial)`(`:69-91`): `116_charLook` 행 목록, `CharAnimParams.From`, `101_cat`의 player/friend id, `CatLayout.CloneOffsets`(분신 자리 수 = `314_spawnGroup` clone 행 수), `CharacterPool` 생성(`new CharacterPool(transform, Setup)`), `_units`를 1 + 분신 자리 수만큼 준비.
- `Setup(CharacterRig)`(`:96-113`): 새 인스턴스에 SortingGroup(World)·DepthSortable(RankUnit, `sort.catProbeY`, Fog 끔)·Billboard(루트)·공유 머티리얼을 붙인다(프리팹에는 그림만).
- `Sync(RunState, now)`(`:150-182`): 단계(Run 걷기 / 끝 Stand / Climb 중 플레이어 Stair) 결정, 등급 `s.EqTier()`, 플레이어 1 + `min(s.Clones, slots)` 분신 `Place`, 나머지는 `Park`.
- `Place`(`:184-219`): `CharLookResolver.Resolve(looks, catId, slot, tier)`(MV.Data) → 행. `Want(row)`로 아틀라스 LRU 갱신·로드, 다음 등급 행도 미리 `Want`. `CharStateMachine.Evaluate` → `SpriteOf(row, pose)`. 행의 `prefab` 주소가 바뀌었으면 기존 Rig 반납 후 `_pool.Get(row.prefab)`. 프리팹 또는 그림이 아직 없으면 그리지 않는다(return false).
- `Want/Load/InUse/LoadAsync`(`:237-285`): 캐릭터 묶음(catId·slot)마다 최근 아틀라스 3개(`KeepPerFamily = 3`, `:26`)만 유지 — 지금·다음 등급·바로 전. 밀려난 것은 다른 묶음이 쓰지 않고 로딩 중이 아니면 `_sprites.ReleaseAtlas`. 실패 아틀라스는 `_failed`에 넣어 재시도 안 함. 바퀴 아틀라스(`wheelAtlas`)는 LRU 밖.
- `OnRunEvents`(`:129-147`): `Fire`(e.A = 쏜 자리) → `Sm.Shot`, `GateTake`/`GaugeLevelUp` → 모두 `Sm.Happy`.
- `OnDestroy` → `_pool.Dispose()`.

**`CharacterPool`** — `Character/CharacterPool.cs`, 순수 C#(IDisposable).
- 주소마다 `Entry{Lease, ObjectPool<CharacterRig> Pool, All, Loading, Failed}`(`:19-25`).
- `Get(address)`(`:44-55`): 풀이 있으면 `Pool.Get()`, 없으면 로드 시작하고 null(그 프레임은 안 그림).
- `LoadAsync`(`:64-110`): `AddressableScope.GetPrefab` → 프리팹에 `CharacterRig`가 없으면 오류 + Failed. 성공 시 `UnityEngine.Pool.ObjectPool<CharacterRig>`(create = Instantiate + `rig.Address` 기록 + `onCreate`(=Setup), get = SetActive(true), release = `rig.Clear()` + SetActive(false), destroy = Destroy).
- `Release(rig)`(`:57-62`): 풀이 있으면 `Pool.Release`, 아니면 Destroy.
- `Dispose`(`:112-127`): 모든 인스턴스 Destroy → 풀 Dispose → lease Dispose. **lease는 Dispose 때까지 계속 쥔다**(런 사이에 반환하지 않음).

**`CharacterRig`** — `Character/CharacterRig.cs`. 프리팹 컴포넌트: `Body`·`Wheel`(정산 바퀴) SpriteRenderer, `Address`(풀이 기록). `Apply(sprite, in CharFrame, scale, wheelSprite)`(`:40`)로 그림·흔들림·뒤집기·바퀴 회전 적용, `Clear()`(`:90`) = 풀 반납 때 그림 비우기.

**`CharStateMachine` / `CharAnimParams` / `CharFrame`** — `Character/CharStateMachine.cs`. `CharState {Hidden, Stand, Walk, Happy(겹침), Stair}`, `CharPose {WalkA, WalkB, AtkA, AtkB, Happy, Stair, HappyBack}`(`:22`). 값은 `300_runConfig char.*`(`CharAnimParams.From :64`). `Set(state, now)`(`:123`), `Shot(now)`(`:131`), `Happy(now)`(`:134`), `Evaluate(params, now, unit) → CharFrame`(`:146`), `SpriteOf(row, pose)`(`:192`: 116 행의 walkA/walkB/atkA/atkB/happy/stair/happyBack 열 선택).

### 3.6 투사체

**`ProjectileView`** — `Runtime/ProjectileView.cs`, order 57.
- 풀: `_active : Dictionary<int projId, Item>`, `_free : Stack<Item>`, 최대 `MaxProjs = 64`(`:27`, 주석상 [창작] 값 — 표에 없음). 회전 장난감은 별도 고정 배열 `_blades`(개수 = `OrbConfig.Count`, 표).
- `Init`(`:83-113`): 등급별 그림 주소 `ProjLayout.NamesByTier`(`103_item.projSprite`), 꼬리 `TailsByTier`(`103_item.projTail`), 가운데 이펙트 `CentersByTier`(`103_item.projCenter`, proj_Image 아래 꼬리와 같은 자리), 마법탄 프리팹 `300_runConfig proj.prefab`. `LoadArt()`(`:115-121`)로 **Init 때 전부 미리 로드**: 프리팹 `AddressableScope.GetPrefab`, 그림 `AddressableScope.Get<Sprite>`(단독 Sprite 주소 `projectile_N`).
- `Sync(RunState, now)`(`:240-273`): `s.Projs` 각 투사체를 id로 찾고 없으면 `Rent()`(free에서 Pop 또는 새로 `Make`, 64 초과면 그리지 않음), `Fresh = true`. 이번 스탬프에 안 보인 항목은 `Release`(SetActive false + free Push). 창(Spear)은 선 메쉬 2겹 + 머리 스프라이트(`Atlas_Fx_Common`), 장난감은 `EnsureRig`(`:173`)로 프리팹 인스턴스를 항목에 1번 붙이고 `rig.SetSprite/SetTail/SetSpin`. **재사용 첫 프레임 `Fresh`면 `rig.ResetTrails()`**로 이전 위치에서 꼬리 선이 이어지지 않게 한다(`:322-326` 부근).
- `OnDestroy`(`:437-447`): 모든 sprite/prefab lease Dispose, 선 메쉬 Destroy.

**`ProjectileRig`** — `Runtime/ProjectileRig.cs`. `Image`, `Card`(VFX_Card 자리), `SetSprite`, `SetSpin(deg)`, `SetTail(address, prefab)`(주소가 바뀌면 옛 꼬리 Destroy 후 새로 Instantiate — 꼬리는 풀 없음), `ResetTrails()`.

### 3.7 계단 정산 칸

**`ClimbStairView`** — `Runtime/ClimbStairView.cs`, order 56.
- `Init`(`:68`): 주소 `_address = 330_climb.stepPrefab`(`:75`). `PrefabLoader`(기본 `AddressableScope.GetPrefab`, `:31`), 시험용 `UseTemplate(prefab)`.
- `Template()`(`:183-193`): 첫 필요 시점에 지연 로드(실패 시 재시도 안 함 `:195-209`).
- 풀: `List<Block> _pool` + 사용 수 `_used`. `Sync`(`:97`)는 매 프레임 `_used = 0`부터 시야 안 블록만 `Take(template)`(`:211-238`: 풀 앞에서 재사용, 모자라면 Instantiate + SortingGroup/DepthSortable(RankObject)/칸 번호 `WorldLabel` 생성), 남은 블록은 `Park`(SetActive false). 같은 런이면 오르기 끝난 뒤에도 마지막 계단 유지.
- `OnDestroy`: lease Dispose.

### 3.8 배경·소품·스테이지 맵

**`PropStreamer`** — `Runtime/PropStreamer.cs`, order 60. 테마 소품(담장·길가 소품·집·먼 집·전봇대) + 빨랫줄/전구줄.
- `Rebuild(from,len,cliff,groundY)`(`:81-88`): `PropLayout.Build`로 배치 목록 `_specs` 재작성, 뷰 슬롯 null 초기화.
- `LateUpdate`(`:90-101`): `PropLayout.InDrawRange(z, cameraZ)`에 들어오면 `Show(spec)`(→ `Take(kind)` 풀 Pop 또는 SortingGroup+DepthSortable(RankProp) 새로 생성 `:121-130`), 나가면 `Hide(kind, go)`(SetActive false, 딸린 빛 끄기, `_pool[kind]` Push `:112-119`).
- 빨랫줄은 `_cloth : Dictionary<double z, ClothView>` + `_clothPool`(`:356-395`).
- 그림은 `WorldSpriteCache.Get(_atlas or _fxAtlas, name)`만 — 자체 로드는 없다(테마 아틀라스는 `WorldView.SetThemeAsync`가 미리 로드).

**`GroundStripMesh`**(`Runtime/GroundStripMesh.cs`, order 50): 매 프레임 카메라 뒤 지면 띠·옹벽 메쉬, 공유 `MV_WorldVertexColor`, 정렬 레이어 `Ground`. **`TintLayer`**(order 110): 테마 `tint`가 있으면 카메라 앞 곱하기 쿼드(`MV/ScreenMultiply`). **`ScreenRig`**(order 120): 테마 `deco = snow`면 화면 앞 70개 눈 스프라이트를 `SnowField` 식으로 배치.

**`StageMap`** — `Runtime/StageMap.cs`, order 60. 스테이지 맵 프리팹(`Stage1` 등, `Local/BG/Prefab/Stage1/Stage1.prefab`) 루트.
- `Bind(cam, groundY)`(`:42-51`): 카메라·지면 높이 공급 함수 연결. `HideBuiltInGround`면 `GroundStripMesh` 렌더러를 끄고, `HideBuiltInProps`면 `PropStreamer`를 끈다(`FindObjectsByType`).
- `LateUpdate`: Backdrop을 카메라 앞 먼 곳에 붙이고 원경 텍스처 오프셋 시차, Ground 판을 `GroundSnap`(44m) 단위로 카메라 z에 맞춰 옮김(무한 바닥), 길가 장식은 `DecoTemplate` 복제 목록을 늘려 재사용(줄지 않음).
- `OnDestroy`: 숨겼던 GroundStripMesh 렌더러 복구, 원경 머티리얼 사본 Destroy.

**`LobbyStage`** — `Runtime/LobbyStage.cs`. 옛 직교 아지트 로비(`Tex_Lobby_Room` + `Atlas_World_Lobby`, `_2`). `LoadAsync`(`:70-…`)는 `AddressableScope.Get<Texture2D>(RoomTexture)` + 아틀라스 로드. **현재 게임 루프는 쓰지 않는다**(`MvGameLoop.cs:35` 주석 「아지트 방(LobbyStage)은 쓰지 않는다」). 프리팹 `Local/World/LobbyStage.prefab`은 시험(`MV.View.Tests/WorldDecoTests.cs:126-130`)만 참조.

---

## 4. MV.Fx 클래스 목록

**`MvFx`**(정적 입구) — `MV.Fx/MvFx.cs:21-64`.
- 상수 `MatNormalAddress = "MV_FxNormal"`, `MatAddAddress = "MV_FxAdd"`(`:24-25`, 주소 = `Local/Effect/Shared/*.mat` 파일 이름).
- `InstallDefault(fxRoot, CameraRig rig, Func<RunState> state, Material normal, Material add)`(`:35-55`): `RunStateFxWorld(state)` + `MvFxPlayer(EffectManager.Instance, fxRoot, world, () => MvTables.Current)`, `ShakeTarget = rig`, `Pops = MvPopPool.FromTables`, `Flies = MvFlyLayer.FromTables`, 둘 다 `CameraScreenProjector`. `MvFxDriver.ViewCamera = cam`. `MvFxRunner.Ensure()`.
  - 사실: 인자 `normal`·`add`는 본문에서 **쓰이지 않는다**. 실제 머티리얼은 각 이펙트 프리팹의 `MvFxDriver._normal/_add` 직렬화 참조(`MvFxDriver.cs:31-32, 70`). Boot가 두 머티리얼을 lease로 들고 있는 효과(참조 유지)만 있다(`MvGameLoop.cs:166-181`).
  - `MvWarpOverlay`(옛 우다다 덮개)는 만들지 않는다(`:49-50`). `FlashView`, `Pops.View`, `Flies.View`는 InstallDefault에서 설정하지 않으며, 게임 코드(MV.Fx 밖, 시험 제외)에 `IMvPopView`/`IMvFlyView`/`IMvHudAnchors`/`IMvFlashView` 구현이 없다(grep 결과 없음) → 현재 팝업·비행·플래시는 수명 계산만 돌고 화면에 그려지지 않는다.
- `OnRunEvents(events)`(`:57`), `Pop(world,text,color,size)`(`:60`, UI용), `Tick()`(`:63`).

**`MvFxPlayer`** — `MvFx.cs:69-200`.
- 생성자(`:94-116`): `MvFxRouter`, `MvFxClock.World/HitStopScale/DtCap`, `MaxHandles = fx.maxHandles`, `TrailInterval = fx.trailInterval`, `MvFxDriver.SparkCapThreshold/Count`(모두 `300_runConfig`).
- `OnRunEvents`(`:125-134`): 이벤트마다 `_out.Clear()` → `_router.Route(e, _world, _out)` → `Apply(_out)`.
- `Apply`(`:136-150`): Spawns → `Spawn`, Shakes → `CameraRig.Shake`, Pops/Flies/Flashes/Warp 전달.
- `Spawn(t, in FxSpawn)`(`:153-165`): 동시 핸들 수가 `MaxHandles` 이상이면 버림. `04_effect` 행(`t.Effect.ByKey(s.Key)`)의 `prefab` 주소 → `MvFxParamBank.Put(s)` 토큰 → `new EffectRequest(effectType: row.prefab, skinIndex: token, sortingLayer: "World", parent: fxRoot, localPos: s.Pos, onEnded: 핸들 수 감소)` → `IEffectPlayer.Play(req)`.
- `Tick`(`:167-174`): 팝업·비행 `Step(RealDt)`, `StepTrails(SimDt)`(원소 투사체마다 `fx.trailInterval`마다 `fx.trail` 스폰, `:177-199`).

**`MvFxRouter`** — `MV.Fx/MvFxRouter.cs`(491줄). `RunEvent` → `04_effect` 그룹 key → `FxSpawn` 목록(+팝업·비행·흔들림·우다다 단계). `Route(in RunEvent, IMvFxWorld, MvFxOutput)`(`:47`), 처리 이벤트: RunStarted(Reset), Fire(투사체 원소 기억), ProjRemoved, Shake(그대로 전달), ProjImpact, Hit(`RouteHit :276`), Break(`RouteBreak :321`), Gain(HUD 비행), GateTake, WellFlush, Steal, Blocked, GodProc/GodChain/BurnTick/OrbTick/GodStorm*/GodBlast/Beam/Meteor*, Climb*(Start/WarpStage/Lap/TierUp/End), RouletteChosen(`:54-266`). 그룹 자식이 여럿이면 part 행(`PlayParts :389`). 표시 전용 난수 `UnityEngine.Random.value`. 흔들림·히트스톱은 표 열이 아니라 MV.Run의 Shake/Hitstop 이벤트만 쓴다(중복 방지, `:11-16`).

**`MvFxOutput` / `PopRequest` / `FlyRequest` / `FlashRequest` / `FlyTarget`** — `MvFxOutput.cs`. 프레임마다 Clear 후 재사용(할당 없음).

**`FxSpawn` / `FxKind` / `MvFxParamBank`** — `MvFxSpawn.cs`. `FxSpawn.Of(key,pos)`(값 NaN = 프리팹 기본값). `MvFxParamBank`(`:107-132`): 1024칸 고리 버퍼. Framework `EffectRequest`에 사용자 데이터 칸이 없어 `skinIndex`에 토큰을 실어 보내고 드라이버의 `SetSkin`이 꺼낸다(덮어써졌으면 기본값).

**`IMvFxWorld` / `RunStateFxWorld` / `IMvFxProjectiles`** — `MvFxWorld.cs`. 상태 읽기 전용 창(`TryObj`, Player X/Z, WorldY, HitStopActive, TimeScale, `TryProjectile`).

**`MvFxClock`** — `MvFxClock.cs`. `RealDt = min(dtCap, unscaledDeltaTime)`, `SimDt = RealDt × (히트스톱이면 hitStopScale) × max(1, 배속)`.

**`MvFxDriver`** — `MvFxDriver.cs`, MonoBehaviour, **Framework `IEffectRenderer` 구현**. 이펙트 프리팹(`Local/Effect/Prefab/Sparks.prefab` 등 14종) 루트에 붙는다.
- `Ensure`(`:62-82`): MeshFilter/MeshRenderer(서브메쉬 normal/add), `DepthSortable(RankFx, sortBias, Fog 끔)`.
- `SetSkin(token)`(`:95`), `Play(anim, loop, onComplete)`(`:97-109`): 토큰으로 `FxSpawn` 복원 → 정본 공식으로 부품(최대 96) 생성 → 재생 시작. `StopAndClear`(`:111-117`), `OnDisable → ClearParts`.
- `Update → Step(MvFxClock.SimDt)`(`:138-169`): 부품 적분, 수명 끝 제거, 메쉬 재빌드(빌보드 축 `ViewCamera`), 부품 0이면 `onComplete` 호출 → Framework가 핸들을 끝내고 풀로 돌린다.
- `LiveParticles`·`SparkCap*`: 전역 점 입자 상한.

**`MvPopPool`**(`MvPopPool.cs`): 숫자 팝업 고정 슬롯 풀(`pop.max`), 넘치면 가장 오래된 것 재사용(`Add :97-112`), `Step(realDt)`(`:120-143`). **`MvFlyLayer`**: HUD 보상 비행(CSS cubic-bezier, 표 값). **`MvFxMesh`**: 드라이버 메쉬 빌더. **`MvWarpOverlay`/`MvWarpModel`**: 옛 우다다 덮개(현재 InstallDefault가 설치하지 않음; MV.UI `ClimbWipeView`가 대체 — `MV.UI/Hud/ClimbWipeView.cs:11`). **`CardLightningFx`**: 카드 번개 MonoBehaviour(`Play(a,b)`), `Fx_Card_Lightning.prefab`(Local/Effect/Prefab/Card)용 — 게임 코드 호출처는 grep으로 찾지 못했다(프리팹 쪽에서 켜질 때 재생하는 구조, `CardLightningFx.cs:7-9` 주석).

**`MvFxRunner`** — `MvFxRunner.cs`. 숨은 DontDestroyOnLoad 오브젝트 `MvFx`, `Update → MvFx.Tick()`.

---

## 5. MV.Sound 클래스 목록

**`MvSound`**(정적 입구) — `MV.Sound/MvSound.cs:11-48`. `InstallDefault()`(`:19-25`: `MvSoundPlayer(new MvAudioManagerOut(), new MvUnscaledClock(), () => MvTables.Current, new Random())` + `MvSoundDriver.Ensure()`), `PlayUi(MvUiSound)`/`PlayUi(string key)`, `Muted`, `EnterLobby()`, `EnterRun()`, `OnRunEvents(events)`, `Tick()`.
- 호출자: `GameBootstrap.RunAsync` 9단계(`MV.Boot/GameBootstrap.cs:112-114`, 저장 설정의 음소거 적용), `MvGameLoop.EnterLobbyAsync`(`MvGameLoop.cs:253` EnterLobby), `MvGameLoop.TickRun`(`:393` OnRunEvents), MV.UI(`MvUiNav.cs:72`, `LobbyScreen.cs:159-191`, `MetaPanelPopup.cs:141-163`, `FakeAdPopup.cs:119,125`, `ResultPopup` 2곳).

**`MvSoundPlayer`** — `MvSoundPlayer.cs`.
- `Play(key)`(`:82-99`): 음소거 → `03_sound` 행(`t.Sound.ByKey`)이 Sfx인지 → GAP(키 끝 `_숫자`를 뗀 묶음 키, `gapMs` 안이면 버림) → `_out.PlaySfx(row.soundName, volume, pitch(1 ± pitchRand))`. 없는 키는 경고 1회.
- `PlayDelayed(key, ms)`/`Tick()`(`:102-126`): 지연 재생(결과 fail 350ms, 덱 머지 450ms) 대기열.
- `OnRunEvent`(`:134-140`): `RunStarted`면 `EnterRun()` → `MvRunSoundRouter.Route` → 큐 재생.
- BGM `SetBgmLayer`(`:158-173`): Lobby/Run 레이어. 처음은 처음부터, 레이어 전환이면 0.03초 크로스페이드 + **재생 위치 유지**(D-57 같은 길이 두 믹스). `LobbyBgmKey = "bgm_lobby"`, `RunBgmKey = "bgm_run"`(`:56-59`). `RunBgmKey`를 바꾸는 게임 코드는 없다(grep) → 런 BGM은 항상 `bgm_run`.
- `Muted` setter(`:68-76`): 효과음은 호출 차단, 음악은 `SetMusicOn(false)`로 트랙만 끔.

**`MvRunSoundRouter`** — `MvRunSoundRouter.cs`. `RunEvent` → `MvSoundCue(key, delayMs)`. 키는 이벤트를 낸 표 행의 소리 열(hitSound·breakSound·passSound·stealSound·cardSound·enterSound·gainSound·bgm)에서, 열이 없는 정본 고정 키만 `MvSoundKeys`. 처리: RunStarted, FloorEnter, Fire(본체만 throw), ProjImpact, Hit, Break, Gain, GateTake, GaugeLevelUp, Steal, Blocked, ClimbStart, ClimbStepShown, ClimbLap, ClimbTierUp, ClimbEnd, RouletteFlip, RouletteChosen, CardApplied, RunFinished(gate면 즉시 win, 아니면 지연 win/fail `:123-129`).

**`IMvAudioOut` / `IMvSoundClock` / `MvUnscaledClock`** — `IMvAudioOut.cs`. 출력 추상화(`PlaySfx`, `PlayMusic(address, vol, crossfade, keepPosition)`, `StopMusic`, `SetMusicOn`). 시험은 기록 대역.

**`MvAudioManagerOut`** — `MvAudioManagerOut.cs`. Framework 구현: `AudioManager.Instance.PlayAsync(address, AudioTrack.Sfx, volume, pitch, false, 0f)`(debounce 0 — GAP은 게임이 함), 음악 `PlayMusicAsync(address, volume, 1f, crossfade)` 후 `keepPosition`이면 이전 `timeSamples` + 로드 동안 흐른 샘플을 새 소스에 넣음(`:81-99`), `StopAllOnTrack(Music)`, `SetTrackOn(Music, on)`.

**`MvSoundKeys`**(고정 키·`Variant`·`GapGroup`), **`MvUiSound`/`MvUiSounds.Cue`**(UI 호출 지점 → 키·지연), **`MvSoundDriver`**(숨은 DontDestroyOnLoad `MvSound`, `Update → MvSound.Tick`).

---

## 6. 리소스(Addressables) 정리

### 6.1 폴더·그룹·주소 규칙

- 규칙(AGENTS.md, `MV.Editor/Import/MvFolderArt.cs:8` 주석, `MvArtRules.cs:95-125`): 그룹 = 최상단 분류 폴더 `Local/<Category>` → `Local_<Category>`, **주소 = 파일 이름(확장자 제외)**, 하위 폴더는 정리용. 원격은 언어팩만; 나머지는 모두 Local(앱 포함). 에디터 메뉴 `MV/Art/Ensure Import·Atlas·Addressables`(`MvArtSetup.cs:46`, `EnsureAddressables :409`)가 멱등으로 맞춘다. 빌드는 `MvBuild`가 플레이어 빌드 전 콘텐츠 빌드(`MvBuild.cs:112-128`).
- 실제 그룹(`Assets/AddressableAssetsData/AssetGroups/`), 항목 수 = `m_GUID` 수 − 1:

| 폴더 `Local/…` | 파일(메타 제외) | 그룹 | 주소 예 | 라벨 | 로드하는 코드 |
|---|---|---|---|---|---|
| Audio/Music, Audio/Sfx | wav 33(Music 2, Sfx 31) | `Local_Audio_Music`(2)·`Local_Audio_Sfx`(31) — Audio만 하위 폴더로 그룹이 나뉨 | `Bgm_Lobby`, `Bgm_Run`, `Sfx_Buy`, `Sfx_Card_0` | bgm, sfx | Framework `AudioManager`(→`AddressablesClipProvider`), 주소는 `03_sound.soundName` |
| BG | png 41, mat 7, prefab 13 | `Local_BG`(13) | 맵 `Stage1`, 맵 부품 `Stage1_Arch/_Block_*/_Portal`, 오브젝트 `Obj_Tower`, `Obj_CoinPile`, `Obj_CraftGate`, `Obj_GemRock`, `Obj_StairGate`, `Obj_StairZone`, `Obj_ClimbStep` | (확인 안 함) | 맵: `MvGameLoop.LoadStageMapAsync`(`302_area.stageMap`), 오브젝트: `ObjectViewFactory`(105/305 prefab), 계단 칸: `ClimbStairView`(`330_climb.stepPrefab`). png·mat은 프리팹이 직접 참조(주소 없음) |
| Character | spriteatlasv2 17, png 101, prefab 6 | `Local_Character`(23) | `Char_Cat`, `Char_Clone`, `Char_Clone_calico/gray/siamese/tux`, `atlas_player_0..11`, `atlas_player_wheel`, `atlas_clone_*` | char | `CharacterPool`(prefab), `CharacterPresenter`→`WorldSpriteCache`(atlas), 주소는 `116_charLook.prefab/atlas/wheelAtlas` |
| Effect | prefab 14, mat 4, shader 1 | `Local_Effect`(16) | `Sparks`, `Debris`, `Dust`, `Trail`, `Impact`, `Boom`, `Bolt`, `Fireball`, `Paw`, `Hiss`, `BrickChunk`, `HolyBeam`, `Meteor`, `Fx_Card_Lightning`, `MV_FxNormal`, `MV_FxAdd` | fx | 프리팹: Framework `EffectManager`(→`AddressablesEffectPrefabProvider`), 주소는 `04_effect.prefab`. 머티리얼 2개: `MvGameLoop.InstallFxAsync` |
| Font | asset 2, mat 2, otf/ttf | **그룹 없음** | – | – | Addressables로 읽지 않음(참조로 포함되는 것으로 보임, 확인 안 함) |
| Mode | .gitkeep만 | 그룹 없음 | – | – | – |
| Projectile | png 12, prefab 14, mat 2 | `Local_Projectile`(26) | `projectile`(마법탄 프리팹), `projectile_0..11`(등급 그림 Sprite), `VFX_Tail`, `VFX_Tail_0..11` | (확인 안 함) | `ProjectileView.LoadArt`(Init 때 전부) |
| Texture | .gitkeep만 | `Local_Texture`(5) — 항목은 `Assets/Game/Art/Source/Texture/`의 파일(예: `Tex_Lobby_Room.png`) | `Tex_God_Flame`, `Tex_God_Thunder`, `Tex_Lobby_Room`, `Tex_Lobby_Vignette`, `Tex_UI_Vignette` | ui.god 등 | `LobbyStage.LoadAsync`(미사용 경로), UI 쪽 |
| Theme/Update | .gitkeep만 | 그룹 없음 | – | – | – |
| UI | prefab 14 | `Local_UI`(13) + `Dev_Debug`(1: `UIPopup_Debug`) | `UIManager`, `UIScreen_Lobby`, `UIHud_Run`, `UIPopup_Result/Map/Quest/…` | ui.core 등 | Framework UI(`MvUiNav` → UIManager, 주소 = 접두어 + ID, `MV.UI/Common/MvUiIds.cs:4`) |
| UIAtlas | spriteatlasv2 5 | `Local_UIAtlas`(5) | `Atlas_UI_Card/Common/Hud/Icon/Lobby` | ui.* | Framework `AddressableImage`(`MV.UI/Common/MvUi.cs:75-84`, `BlessingCardsView`, `RunHudView`) |
| World | prefab 2 | `Local_World`(2) | `WorldRoot`, `LobbyStage` | world, lobby | `MvGameLoop.InitAsync`(`WorldRoot`) |
| WorldAtlas | spriteatlasv2 32 | `Local_WorldAtlas`(32) | `Atlas_Fx_Common`, `Atlas_World_Theme_<theme>`(16), `Atlas_World_Cat_T1..8`, `Atlas_World_Friend_T1..6`, `Atlas_World_Lobby(_2)` | theme.<name>, cat, lobby, fx | `WorldSpriteCache`(테마: `WorldView.SetThemeAsync`, Fx: 같은 곳) |

- `Default Local Group`은 항목 0. 그룹에 없는 폴더(Font·Mode·Theme)는 규칙상 폴더만 있고 아직 Addressables 자산이 없거나 직접 참조된다.

### 6.2 Framework API(쓰는 것만)

`com.jjevol.resources.addressables`(`Library/PackageCache/com.jjevol.resources.addressables@c69ee9aec34b/Runtime/`):
- `AddressablesManager`(PersistentSingleton): 키당 `AssetRefInfo{handle, result, refCount}`(`AddressablesManager.cs:85-112`). `LoadAssetAsync<T>(key, ct)`(`:219-290`): 키별 세마포어로 동시 로드 1개, 캐시에 있으면 `AddRef` + 예약된 지연 해제 취소, 타입 불일치면 오류. `ReleaseAsset(key, delay)`(`:441-470`): refCount−1, 0이면 즉시(또는 delay 뒤) `Addressables.Release(handle)`. `CaptureRelease(key)`(`:75-84`): 1회용 해제 Action(같은 획득 레코드일 때만 해제).
- `AddressableScope`(`AddressableScope.cs:45-96`): `Get<T>(key, ct) → AddressableLease<T>`(로드 + CaptureRelease), `GetPrefab(key)`, `InstantiateWithLease(key, parent, ct) → (instance, lease, destroyOnDispose)`: lease는 `RetainUntilDestroyed`로 **인스턴스가 실제 파괴된 뒤(LastPostLateUpdate까지 기다림) 반환**, destroyOnDispose는 Dispose 시 `Destroy(go)`.
- `AddressableLease<T>`(`:9-22`): `Key`, `Asset`, 암시적 T 변환, `Dispose()`는 원자적으로 1번만 해제.
- 게임은 `AddressableMultiScope`·`LoadAssetsAsync`·라벨 로드를 쓰지 않는다(grep 결과 없음).

`com.jjevol.effects` + `.addressables`: `EffectManager.Instance.SetPrefabProvider(new AddressablesEffectPrefabProvider())`(Boot 3단계), `Play(EffectRequest)`(동기 반환, 내부 비동기 로드·바인딩 후 재생, `EffectManager.cs:186-191`). 타입별 `EffectPool`(기본 `PoolProfile{minRetain 1, maxPool 32, idleSeconds 15}`, `EffectManager.cs:11-16`; 게임은 `SetPoolProfile`/`PreloadAsync`를 부르지 않음). 프리팹은 `EffectPrefabCache`가 키당 1 레코드로 보관, 그 타입 인스턴스가 모두 사라지면 `Release(key)`로 lease 반환(`EffectPool.cs:112`, `EffectPrefabCache.cs:104-111`). 공급자는 `Addressables.LoadAssetAsync<GameObject>`를 직접 부른다(`AddressablesEffectPrefabProvider.cs:12-30`) → `AddressablesManager` 참조 수와 별개.

`com.jjevol.audio` + `.addressables`: `AudioManager.Instance.SetProvider(ProviderKind.Addressables)`(Boot 3단계). `AddressablesClipProvider`(`AddressablesClipProvider.cs:11-99`): 키당 실제 핸들 1개 + 재생 1회당 lease 1개, 참조 0이면 엔트리 해제. 게임은 `PlayAsync`·`PlayMusicAsync`·`StopAllOnTrack`·`SetTrackOn`·`CurrentMusic`만 쓴다.

### 6.3 게임 코드의 모든 Addressables 로드 지점

| # | 위치 | API | 주소 출처 | 시점 | 보관·반환 |
|---|---|---|---|---|---|
| 1 | `MV.Boot/GameBootstrap.cs:78-86` | `Addressables.InitializeAsync(false)` → `AddressablesManager.Instance.InitAsync()` | – | 부팅 1·2단계 | init 핸들은 즉시 Release |
| 2 | `MV.Boot/GameBootstrap.cs:89-90` | 공급자 설정(소리·이펙트) | – | 부팅 3단계(첫 로드 전이어야 함 — 이펙트는 이후 설정 시 예외 `EffectManager.cs:153`) | – |
| 3 | `MV.Boot/MvGameLoop.cs:125` | `InstantiateWithLease("WorldRoot", transform)` | 상수 `WorldRootAddress`(`:42`) | Game 씬 초기화 | `_leases`에 보관, `OnDestroy`(`:99-106`)에서 destroy → lease |
| 4 | `MV.Boot/MvGameLoop.cs:232` | `InstantiateWithLease(row.stageMap, _world.transform)` | `302_area.stageMap`(지역 행) | 지역이 바뀔 때(`LoadStageMapAsync :223-239`, 같은 지역이면 건너뜀) | `_stageLeases`, 다음 지역 로드 직전 또는 OnDestroy에 Dispose(이전 맵 Destroy → 파괴 확인 뒤 lease 반환) |
| 5 | `MV.Boot/MvGameLoop.cs:177` | `AddressableScope.Get<Material>` ×2 | `MV_FxNormal`, `MV_FxAdd` | Fx 설치 시 1번 | `_leases`, OnDestroy |
| 6 | `MV.View/Runtime/WorldSpriteCache.cs:35` | `Get<SpriteAtlas>` | 테마 `304_theme.worldAtlas`, `Atlas_Fx_Common`, `116_charLook.atlas/wheelAtlas` | 테마 적용 시(선행 로드), 캐릭터는 필요 시 | `ReleaseAtlas`(테마 교체·캐릭터 LRU 밖) 또는 `WorldView.OnDestroy` |
| 7 | `MV.View/Runtime/ObjectViewFactory.cs:202-220` | `PrefabLoader`=`GetPrefab` | `105_objectKind.prefab` 전체 + `305_stageObject.prefab`(이 맵) | `SetStage` 때 Preload, 그 밖은 처음 보일 때 | `_prefabs`에 영구 보관(맵이 바뀌어도 이전 맵 것 반환 안 함), OnDestroy에서 Dispose. 실패는 null로 고정 |
| 8 | `MV.View/Character/CharacterPool.cs:69` | `GetPrefab` | `116_charLook.prefab` | 캐릭터가 처음 그 행을 쓸 때 | Pool Dispose(Presenter 파괴·재 Init)까지 |
| 9 | `MV.View/Runtime/ProjectileView.cs:138, 152` | `Get<Sprite>`, `GetPrefab` | `300 proj.prefab`, `103_item.projSprite`, `103_item.projTail`, `103_item.projCenter` | Init 때 전부 | OnDestroy |
| 10 | `MV.View/Runtime/ClimbStairView.cs:197-208` | `PrefabLoader`=`GetPrefab` | `330_climb.stepPrefab` | 첫 계단 Sync 때 지연 | OnDestroy |
| 11 | `MV.View/Runtime/LobbyStage.cs:75` | `Get<Texture2D>` + 아틀라스 | `Tex_Lobby_Room`, `Atlas_World_Lobby(_2)` | (현재 루프 미사용) | OnDestroy·재 Load |
| 12 | Framework EffectManager | 내부 | `04_effect.prefab` | 첫 `Play` 때 | 그 타입 인스턴스가 모두 사라지면 |
| 13 | Framework AudioManager | 내부 | `03_sound.soundName` | 재생 때마다 lease | 재생 끝·참조 0 |
| 14 | MV.UI(범위 밖 요약) | `AddressableImage`, UIManager | UI ID, `Atlas_UI_*` | 화면 열 때 | Framework |

참고: 표는 Addressables가 아니라 `Assets/Game/GameData/Table/Resources` 산출물(TableBuilder)만 읽는다(AGENTS.md 규칙, `MvBootMeta.cs:51`).

---

## 7. 단계별 흐름

### 7.1 부팅 → 런 시작 전 월드/뷰 준비

1. `GameBootstrap.RunAsync`(Boot 씬): `Addressables.InitializeAsync` → `AddressablesManager.InitAsync` → `AudioManager.SetProvider(Addressables)` / `EffectManager.SetPrefabProvider(...)` → 표 로드(`MvBootMeta.LoadTablesAsync`) → 저장·메타 → UI(`MvUiBoot.InitAsync`) → `MvSound.InstallDefault()` + 음소거 적용 → Game 씬 로드(`GameBootstrap.cs:76-123`).
2. Game 씬 `GameInitializer.OnInitializeAsync`(`GameInitializer.cs:22-40`): Boot 없이 열렸으면 Boot 씬으로 되돌림, 아니면 `AddComponent<MvGameLoop>()` → `InitAsync(tables)`.
3. `MvGameLoop.InitAsync`(`MvGameLoop.cs:111-148`):
   1. UI 초기화 대기, `MvRunInputReader` 생성.
   2. `InstantiateWithLease("WorldRoot")` → `WorldView` 확보 → `_world.Init(tables, _viewSource)`(`WorldView.cs:46-77`: CameraRig/DepthSorter/Ground/Props/Tint/Objects/Screen/Cats/Projectiles/Stairs Init — 이 중 `ProjectileView.Init`이 투사체 그림·프리팹 로드를 바로 시작).
   3. `ApplyThemeAsync(CurrentArea())`(`:207-217`): `302_area`(areaNo = area+1) 행 → `LoadStageMapAsync`(`:223-239`): `ObjectViewFactory.SetStage(tables, stageMap)`(Clear + 오브젝트 프리팹 Preload) → 맵 프리팹 InstantiateWithLease → `StageMap.Bind(cam, () => Wy)`. 이어 테마 id가 바뀌었으면 `WorldView.SetThemeAsync(theme)`(아틀라스 선행 로드 → 부품 SetTheme → 이전 테마 아틀라스 반환).
   4. `InstallFxAsync`(`:162-173`): `FxRoot` 생성, `MV_FxNormal/Add` 머티리얼 lease, `MvFx.InstallDefault(fxRoot, _world.Rig, () => _sim?.State, …)`.
   5. `TakeSceneCamera` → `EnterLobbyAsync`(`:243-258`): 다시 ApplyTheme(같으면 건너뜀) → `BuildLobbyTrack`(`:264-275`: 다음 런과 같은 순번·시드로 `RunSim`을 만들되 Tick하지 않음, `LobbyTrack.Pick`으로 `lobby.objZMax` 안 오브젝트 선택, `Rig.Init`(로비 자세), 월드 카메라를 UI 메인 카메라로, `RebuildProps`, `SyncLobbyTrack`) → `MvSound.EnterLobby()`(로비 BGM) → 로비 화면 열기.
4. 로비 중 `Update` → `SyncLobbyTrack`(`:278-284`): `SyncObjects(_lobbyObjs, …)` + `SyncCats(lobbyState)`.
5. 출발 `StartRun`(`:336-355`): `MvMeta.BeginRun()`으로 순번, 새 `RunSim`, `_viewSource.Run = new RunStateViewSource(state)`(카메라가 로비→런 자세로 보간), `RebuildProps`, HUD 진입. 로비 오브젝트 목록은 비우지만 뷰는 다음 `Sync`에서 정리된다(아래 7.3: id는 같고 `RunObject`가 다르므로 새 뷰).

### 7.2 프레임마다 sim → view 동기화

`MvGameLoop.Update`(`:357-368`) → `TickRun`(`:371-403`):
1. 입력 읽기(`MvRunInputReader.Read` 또는 시험 `ScriptedDrag`).
2. `dt = FixedDt ?? Time.unscaledDeltaTime`, `TicksPerFrame`번 `_sim.Tick(dt, input)`(Result가 되면 중단).
3. `events = _sim.DrainEvents()` **한 번**.
4. 같은 목록을 순서대로: `MvSound.OnRunEvents` → `MvFx.OnRunEvents` → `_world.OnRunEvents`(오브젝트 반응·캐릭터 쏘기/기쁨).
5. `ClimbStart` 이벤트마다 `_world.RebuildProps(e.Z, …, cliff: true, WY + e.Amount2)`(계단 위 새 지형).
6. `_world.SyncObjects(s.Objs, s.P.Z, s.WorldY)` → `ObjectViewFactory.Sync`.
7. `_world.SyncCats(s)` → `CharacterPresenter.Sync`, `ProjectileView.Sync`, `ClimbStairView.Sync`.
8. Result 단계로 바뀌었으면 `FinishRun`(지도 → 결과 → 로비).
9. 같은 프레임 LateUpdate: `CameraRig`(−100) → `GroundStripMesh`(50) → `PropStreamer`·`StageMap`(60) → `DepthSorter`(100: 빌보드 회전 + 정렬/안개) → `TintLayer`(110) → `ScreenRig`(120). 별도로 `MvFxRunner.Update`(팝업·비행·꼬리), `MvSoundDriver.Update`(지연 소리), 각 `MvFxDriver.Update`(입자 적분).

### 7.3 오브젝트 생성 → 풀 대여 → 초기화 → 반납 → 재사용 (`ObjectViewFactory`)

1. `Sync`(`:393-420`)가 `_stamp++`, 실시간 dt(최대 0.1s) 계산.
2. 각 `RunObject o`: 죽었거나 그리기 범위(`PZ + objDrawBack` ~ `PZ + objDrawFront`, `ViewConfig`) 밖이면 건너뜀.
3. **id 재사용 판정(MV-VIEW-OBJRESET, bac813e6)**: `_views.TryGetValue(o.Id, out v) && v.Obj != o`이면 `Release(v); v = null`(`:404-405`). 이유: 오브젝트 번호는 런마다(로비 트랙·새 런) 1부터 다시 쓰이므로, 번호가 같아도 다른 `RunObject` 참조면 이전 런의 흔들림·눌림·쓰러짐·이전 참조를 이어 쓰지 않게 새로 꺼낸다.
4. 뷰가 없으면 `Show(o, groundY)`(`:595-632`) = **풀 대여 + 초기화**:
   - 키 결정: `prefab = PrefabFor(...)`가 있으면 `_prefabPool[prefab]`, 없으면 `_pool[ShapeOf(kind)]`. 스택에 있으면 Pop, 없으면 새 루트 `GameObject`(SortingGroup "World" + DepthSortable Rank=Object) 생성.
   - 초기화: `Obj = o`, `SortOffsetY = sort.objProbeY`, `Bias = 105.sortBias`, `SetAlphaScale(1)`, `Shake/Flash = 0`, `HasJunkFlash = false`, `ResetMotion(v)`, 위치, 코드 모양 Parts 전부 끄기, `Base.Clear()`.
   - 프리팹 뷰: `EnsureInstance`(풀에서 나온 뷰는 이미 `Inst`가 있어 그대로 재사용) + 인스턴스 localPosition 0. 코드 모양: `Build(v, o)`로 슬롯 SpriteRenderer를 다시 채움(슬롯 객체 재사용).
   - `BuildLabel`, `SetActive(true)`, `Ds.Refresh()`(자식 렌더러 목록 갱신).
5. 이번 프레임 표시: `Stamp = _stamp`, 위치 갱신, `Animate`(흔들림·번쩍·눌림 감소, 프리팹이면 `AnimatePrefab` — 로드 지연 시 여기서 `EnsureInstance` 재시도, `Rig.SetFill(hp,max)`).
6. 이번 스탬프가 아닌 뷰: 종이 쓰러짐 진행 중(`0 ≤ Die < ObjectReaction.DieSec`)이면 `AnimateDie`로 남겨 두고, 아니면 `_drop` → **반납** `Release(v)`(`:576-593`): `Shake/Flash = 0`, `ResetMotion`(`:561-574`: Punch 0, Die −1, 루트 scale 1, Parts·Boards의 `Billboard.Roll = 0`, 인스턴스 scale 원래 값, 수정탑 구슬 Gone/Grow/scale 복구), 인스턴스 localPosition 0, `SetAlphaScale(1)`, `SetActive(false)`, `Obj = null` → 프리팹 뷰는 `_prefabPool[Prefab]`, 코드 뷰는 `_pool[Shape]`에 Push.
7. 재사용: 다음에 같은 프리팹 주소/모양의 오브젝트가 오면 4번에서 Pop. 반납 때와 꺼낼 때 둘 다 동작을 초기화한다(주석 `:578`).
8. 전체 정리: `SetTheme`·`SetStage` → `Clear()`(`:345-349`) = 모든 뷰 Release(풀로). 풀의 GameObject와 프리팹 인스턴스는 파괴하지 않고 계속 재사용된다(풀 크기 상한 없음, 파괴는 컴포넌트 파괴 시 계층째).

다른 풀과 비교:
- `ProjectileView`: 투사체 id → Item, free Stack, 상한 64, 재대여 첫 프레임 `ResetTrails`.
- `CharacterPresenter/CharacterPool`: 자리(unit index) 고정 + 주소별 `ObjectPool<CharacterRig>`; 행의 prefab 주소가 바뀌면 반납 후 새 주소에서 Get; 반납 시 `rig.Clear()`.
- `PropStreamer`: `PropKind`별 Stack, 그리기 범위 출입으로 대여/반납. 빨랫줄은 z 키 Dictionary + Stack.
- `ClimbStairView`: 리스트 앞에서부터 `_used`만큼 쓰고 나머지 비활성.
- `StageMap` 장식: 필요한 수까지 복제만 늘리고 매 프레임 재배치.
- Framework `EffectPool`: 타입별 idle Stack, `PoolProfile` 기준 컬링.
- `MvPopPool`: 고정 슬롯, 넘치면 가장 오래된 것 재사용.

### 7.4 이펙트 재생 흐름

1. MV.Run이 `RunEvent`를 낸다 → `MvGameLoop.TickRun` → `MvFx.OnRunEvents(events)`(`MvGameLoop.cs:394`).
2. `MvFxPlayer.OnRunEvents` → 이벤트마다 `MvFxRouter.Route(e, RunStateFxWorld, out)`(`MvFx.cs:125-134`): 표 `04_effect` 그룹(예 `run.projHit`) → 자식/part 행 → `FxSpawn` 목록(위치 = 게임 좌표, y는 WY 더함), 팝업·비행·흔들림.
3. `Apply` → `Spawn`(`MvFx.cs:153-165`): 핸들 상한(`fx.maxHandles`) 확인 → `04_effect.prefab` 주소 → `MvFxParamBank.Put` 토큰 → `EffectManager.Play(new EffectRequest(effectType: 주소, skinIndex: 토큰, sortingLayer: "World", parent: FxRoot, localPos))`.
4. Framework: 타입 풀에서 인스턴스 대여(없으면 `AddressablesEffectPrefabProvider`로 프리팹 로드 후 Instantiate) → `IEffectRenderer`인 `MvFxDriver`에 `SetSorting`·`SetSkin(token)`·`Play(...)`.
5. `MvFxDriver.Play`(`MvFxDriver.cs:97-109`): 토큰으로 `FxSpawn` 복원(덮어써졌으면 프리팹 기본값), 부품 생성 → `Update`마다 `Step(SimDt)`(히트스톱·배속 반영) → 메쉬 재빌드 → `DepthSorter`가 `DepthSortable(RankFx)`로 정렬.
6. 부품 0 → `onComplete` → Framework가 핸들 종료(`EffectEndReason`) → `MvFxPlayer._onEnded`가 핸들 수 감소 → 인스턴스는 타입 풀 idle로. 타입 인스턴스가 모두 컬링되면 프리팹 lease 반환.
7. 흔들림은 `CameraRig.Shake`(최댓값 병합), 투사체 꼬리는 `MvFxPlayer.Tick → StepTrails`가 `fx.trail` 스폰.

### 7.5 사운드 재생 흐름

- 런 이벤트: `MvGameLoop.TickRun` → `MvSound.OnRunEvents` → `MvSoundPlayer.OnRunEvent`(`RunStarted`면 `EnterRun` → BGM 레이어 Run, 위치 유지 0.03s 크로스페이드) → `MvRunSoundRouter.Route`(이벤트 → 표 소리 열 key + 지연) → `Play(cue)` → `PlayDelayed`(지연이면 대기열, `MvSoundDriver.Update → Tick`에서 시각 순 재생) → `Play(key)`: 음소거·`03_sound` 행·GAP 묶음·피치 난수 → `MvAudioManagerOut.PlaySfx(soundName, …)` → `AudioManager.PlayAsync(address, Sfx, vol, pitch, loop:false, debounce:0)` → `AddressablesClipProvider.AcquireAsync`(키당 핸들 1개, 재생당 lease) → 재생 끝에 lease 반환.
- UI: `MvSound.PlayUi(MvUiSound)` → `MvUiSounds.Cue`(키·지연) → 위와 같은 `Play` 경로. 화면 열기 click(`MvUiNav.cs:72`), 로비·메타·결과 버튼.
- 로비 BGM: `MvGameLoop.EnterLobbyAsync` → `MvSound.EnterLobby()` → `SetBgmLayer(Lobby)` → `PlayMusic(Bgm_Lobby의 soundName, vol, crossfade, keep)`.
- 음소거: 로비 버튼(`LobbyScreen.cs:179`) → `MvSound.Muted` → 효과음 차단 + 음악 트랙 off(음악은 계속 흐름).

### 7.6 에셋 로드/해제 수명 요약

| 수명 범위 | 에셋 | 잡는 곳 | 놓는 곳 |
|---|---|---|---|
| 앱 전체 | Effect 프리팹(사용 중인 타입), 오디오 클립(재생 중) | Framework 매니저(DontDestroyOnLoad) | 풀 컬링/재생 끝 |
| Game 씬(MvGameLoop 수명) | `WorldRoot` 인스턴스·lease, Fx 머티리얼 2개 | `MvGameLoop._leases` | `MvGameLoop.OnDestroy` |
| WorldView 수명 | `WorldSpriteCache`의 아틀라스들, 오브젝트 프리팹 lease(`_prefabs`), 캐릭터 프리팹(`CharacterPool`), 투사체 그림·프리팹, 계단 칸 프리팹 | 각 컴포넌트 | 각 `OnDestroy`(`WorldView.cs:133`, `ObjectViewFactory.cs:337`, `CharacterPresenter.cs:287`, `ProjectileView.cs:437`, `ClimbStairView` OnDestroy) |
| 지역(area) | 스테이지 맵 프리팹 인스턴스·lease | `MvGameLoop._stageLeases` | 다음 지역 로드 직전(Destroy → 파괴 확인 뒤 lease) |
| 테마 | 테마 월드 아틀라스 | `WorldView.SetThemeAsync` | 다음 테마 적용 뒤 `ReleaseAtlas(prev)`(새 것을 먼저 로드하고 옛 것을 놓는다) |
| 캐릭터 묶음 LRU | `116_charLook.atlas` | `CharacterPresenter.Want` | 묶음당 4번째로 밀리면(다른 묶음 미사용·로딩 아님) |

공통 규칙:
- 같은 키를 여러 곳이 잡으면 `AddressablesManager` 참조 수가 늘고, 각 lease Dispose가 1씩 내린다. 0이 되면 즉시 `Addressables.Release`.
- 로드 실패는 재시도하지 않고 오류 로그 1회 + 그 대상만 안 그린다(ObjectViewFactory·CharacterPool·CharacterPresenter·ClimbStairView). 대체 경로·대체 그림으로 가리지 않는다(단, 아틀라스 안 없는 스프라이트 이름은 `World_Missing`으로 보이게 함).
- 비동기 로드가 컴포넌트 파괴/취소 뒤 끝나면 받은 lease를 즉시 Dispose(모든 로더 공통 패턴).
- `SpriteAtlas.GetSprite` 복제본은 `WorldSpriteCache`가 이름당 1회 만들고 아틀라스 반환 때 Destroy.
- 런 사이(런 → 결과 → 로비 → 새 런)에는 월드 뷰 자산을 놓지 않는다. 뷰 GameObject는 풀로만 돌아간다.

---

## 8. 눈에 띈 점(문서화 대상, 수정 제안 아님)

1. `MvFx.InstallDefault`의 `normal`·`add` 인자는 사용되지 않는다(`MvFx.cs:35-55`); 이펙트 머티리얼은 프리팹 직렬화 참조.
2. 팝업·비행·플래시의 그림 쪽(`IMvPopView`·`IMvFlyView`·`IMvHudAnchors`·`IMvFlashView`) 구현이 게임 코드에 없어 현재 화면 출력이 없다.
3. `ObjectViewFactory._prefabs`는 맵이 바뀌어도 이전 맵 전용 프리팹 lease를 놓지 않는다(컴포넌트 파괴 때까지).
4. `ProjectileView.MaxProjs = 64`는 주석상 [창작] 값으로 표에 없다(`ProjectileView.cs:24-27`).
5. Addressables 그룹은 `Local_Audio_Music`/`Local_Audio_Sfx`로 Audio만 하위 폴더 기준으로 나뉘어 있고, Font·Mode·Theme 폴더에는 그룹이 없다. `Local_Texture` 항목은 `Local/Texture`가 아니라 `Assets/Game/Art/Source/Texture/`에 있다.
6. `LobbyStage`(아지트 직교 로비)와 `MvWarpOverlay`는 코드·프리팹은 남아 있으나 현재 루프에서 쓰지 않는다.
7. 오브젝트 그리기 범위는 직전 프레임 카메라 z 기준(동기화가 Update, 카메라가 LateUpdate).
