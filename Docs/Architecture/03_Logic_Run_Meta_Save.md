# 03. 게임 로직 (MV.Run · MV.Meta · MV.Content · MV.Save)

기준 커밋: `dev` 046c8be6 (2026-11-15). 경로는 저장소 루트 기준이고 `파일:줄`은 그 커밋의 줄 번호다. Framework 줄 번호는 `Library/PackageCache` 풀린 본 기준이라 참고용이다. 전체 지도는 [README.md](README.md).
> 경로 접두 `S/` = `Assets/Game/Scripts/`. `파일:줄`은 그 파일의 실제 줄 번호다.

---

## 0. 한눈에 보기

### 0.1 어셈블리 의존 방향 (asmdef `references`)

| 어셈블리 | 참조 | 뜻 |
|---|---|---|
| `MV.Run` | MV.Core, MV.Data, JJEvol.Tables.Runtime | 순수 시뮬. Unity API·저장·메타를 모른다. |
| `MV.Save` | MV.Core, MV.Data, JJEvol.Data/Save/Economy.Runtime | 저장소. Framework `DataRepository`·`LocalSaveRepository` 위에 얹음. |
| `MV.Meta` | MV.Core, MV.Data, **MV.Save**, JJEvol.Data/Economy/Tables/Save, UniTask | 영속 변경의 유일한 진입점. **MV.Run을 모른다.** |
| `MV.Content` | MV.Core, MV.Data, **MV.Run, MV.Meta, MV.Save**, … | 퀘스트·연구. Meta에 처리기를 "꽂는" 쪽. |

- `MV.Run → MV.Meta` 연결은 없다. 그래서 `RunResult → MvRunOutcome` 변환은 MV.UI(`S/MV.UI/Result/ResultContracts.cs:45-76`)가, `저장 → RunSetup` 변환은 MV.Boot(`S/MV.Boot/MvRunStart.cs:131-159`)가 한다.
- `MV.Meta → MV.Content` 연결도 없다. Content가 `MvLocalMetaBackend.RegisterContent/RegisterContributor`로 처리기를 등록한다(`S/MV.Meta/MvLocalMetaBackend.cs:632-659`).
- 소스 검색 시험 `S/MV.Meta.Tests/MvMetaEntryPointRuleTests.cs:18-49`: MV.Meta·MV.Save 밖의 게임 코드에서 `ApplyAndSave(` 또는 `.Commit(`이 나오면 실패한다. "영속 쓰기는 Meta/Save만"이 시험으로 강제된다.

### 0.2 런 1회 큰 그림

```
MvGameLoop.StartRun (Boot)
  └ MvMeta.BeginRun() → runSeq
  └ MvRunSnapshot.FromStore(save) → MvRunStart.CreateContext → RunContext (+ MvResearch.Modifiers)
  └ RunSim.Create(ctx, MainRunRule(builder), Pcg32Rng(seed), RunLevelBuilder)
매 프레임 MvGameLoop.TickRun
  └ RunSim.Tick(dt, RunInput) × TicksPerFrame
  └ RunSim.DrainEvents() → MvSound / MvFx / World(View) 로 전달
  └ State.Phase == Result → FinishRun
FinishRun → 지도 팝업 → 결과 팝업(ResultViewData: RunResult → MvRunOutcome)
  └ 「받기」 → MvMeta.Settle(outcome, mul) → MvLocalMetaBackend.Settle → ApplyAndSave 1번
  └ 성공 시 MvQuest.RecordRun(RunResult) (계단·벽 카운터)
```

---

## 1. MV.Run — 런 시뮬레이션

### 1.1 설계 원칙 (코드 주석·구조에서 확인한 것)

- **결정적(deterministic) 시뮬**. 시간·입력·난수를 모두 밖에서 넣는다: `RunSim.Tick(realDt, RunInput)`(`S/MV.Run/Sim/RunSim.cs:82`), 난수 `IRng`는 `Create`에 주입(`RunSim.cs:43-71`).
- **종류 이름으로 분기하지 않는다**. 오브젝트 동작은 `105_objectKind` 행 플래그(`hittable`·`breakable`·`blocks`)와 그 행이 가리키는 컴포넌트 행(106 HitReaction, 107 ChargeGate, 108 ReceiveCondition, 109 MoveAi, 114 HpRule, 102 Enemy)의 `type`으로만 고른다(`S/MV.Run/Sim/RunSim.Rules.cs:8`, `S/MV.Run/State/RunObject.cs:5-8`).
- **정본(웹 프로토 HTML) 비트 일치**. 곱·합 순서를 정본 식 그대로 두고(`S/MV.Run/Sim/RunFormulas.cs:7-10`), JS `Math.round`를 `Math.Floor(x+0.5)`로 흉내 내고(`RunFormulas.cs:14`), sin도 V8과 비트 동일한 `JsMath.Sin`을 직접 구현했다(`S/MV.Run/Gen/JsMath.cs:5-37`).
- **View는 읽기만**. MV.Run은 이펙트·소리를 직접 부르지 않고 `RunEvent`만 쌓는다(`S/MV.Run/Events/RunEvent.cs:6-8`).
- **규칙 확장 지점** `IRunRule`(모드용). 본편은 `MainRunRule`. 현재 MV.Content에 모드 규칙 구현은 없다(Content 폴더에는 Quest·Research뿐).

### 1.2 폴더·클래스 목록

#### Api/

| 클래스 | 파일 | 종류 | 책임 |
|---|---|---|---|
| `RunResult` | `S/MV.Run/Api/RunApi.cs:8-34` | sealed class | 런 결과(정산 입력). Reason, Gold(시작 몫 포함), Souls, Dia, StartGold/StartDia, Tok, Pass, ZoneEnd, CpRun, BarRec, Climbed, Walls, GaugeLv, EqTier, Clones, CardCount, HitWallBar, PendingSettle. "영속 반영은 MV.Meta만"(주석 7행). |
| `IRunLevelBuilder` | `RunApi.cs:40-59` | interface | 레벨 생성기. `NewRun`(상태+코스), `BuildLevel(s, baseZ, level, saved)`, `FieldSpec(area, floor)`. |
| `IRunStateFactory` | `RunApi.cs:65-68` | interface | `NewState`: newRun 1~5단계만(코스 생성 전). SIM이 6단계 자리에서 `IRunRule.BuildCourse`를 부르게 하려고 분리. |
| `RunComponents` | `RunApi.cs:75-91` | static | 표 검증기용 "구현된 컴포넌트 type" 목록. `IsImplemented(column, type)`. Boot가 `options.IsComponentImplemented`로 연결(주석 71-73). |
| `RunCliff` | `S/MV.Run/Api/RunViewState.cs:7-19` | readonly struct | 옹벽 {Z, Low, High}. |
| `IRunViewState` | `RunViewState.cs:25-41` | interface | View가 읽는 값: `WorldY`, `HasCliff`, `Cliff`. 구현은 `RunState`. 사용처 `S/MV.View/Runtime/WorldViewSource.cs:35-69`(`RunStateViewSource`). |

#### Core/

| 클래스 | 파일 | 책임 |
|---|---|---|
| `IRng` | `S/MV.Run/Core/RunRng.cs:10-13` | `[0,1)` 난수 1줄기. |
| `RngUse` | `RunRng.cs:16-37` | 규칙 난수 사용처 enum. 값 = 정본 호출 줄 번호(EnemySpeed=1323, EnemyDrop=1927, Proc=1986, Storm=2004, Blast=2005, Crit=2081, DoubleShot=2090, StormPick=2167, StormJitter=2168, CardDraw=2301). 새 규칙은 9000번대(StairDouble 9001, CraftDouble 9002, PierceChance 9003). |
| `RngDraw` | `RunRng.cs:40-46` | 뽑은 난수 1건 기록(재현·대조). |
| `RngExtensions` | `RunRng.cs:48-53` | `Range`, `Chance`, `Index`. |
| `Pcg32Rng` | `RunRng.cs:56-85` | PCG32(XSH-RR), 53비트 double. 본편 런 난수. |
| `RunRngStreams` | `RunRng.cs:91-97` | 시드 파생 별도 줄기 번호: Mode(`"mode"`), Companion(`"comp"`). |
| `ConstRng` / `ReplayRng` | `RunRng.cs:100-119` | 시험용: 상수 / 덤프 재생(넘게 뽑으면 예외). |
| `RunPhase`, `FinishReason`, `RunEnd`, `FieldReward`, `HitColor`, `HitSource` | `S/MV.Run/Core/RunTypes.cs:6-21` | 상태 enum. `RunPhase {Lobby, Run, Climb, Result}`, `FinishReason {None, Gate, Wall, ModeClear, ModeFail}`. |
| `RunInput` | `RunTypes.cs:28-36` | 한 Tick 입력: `DragTargetX`(목표 x, m 단위), `KeyAxis`(−1/0/+1). 화면 px→m 변환은 MV.UI 몫. |
| `CompanionTrigger`/`CompanionSpec` | `RunTypes.cs:39-54` | 동행 자동 사격 명세(Hits/Chance/Seconds). |
| `RunModifiers` | `RunTypes.cs:57-76` | 메타 능력치 7필드(%): Dmg, FireRate, Range, CritDmg, Gold, CritChance, DoubleChance. `Validate()`. |
| `BarRecord` | `RunTypes.cs:82-107` | 장애물 남은 HP 기록 `Floors[f][idx]`. Floor 수는 표에서(`MvTables.FloorCount`). |
| `FieldCell` | `RunTypes.cs:110-121` | fieldSpec 칸 1개(idx, row, lane, hp, 보상 종류, 양). |
| `RunSetup` | `RunTypes.cs:127-215` | 런 동안 바뀌지 않는 save 스냅숏 + 모드 필드. Seed, Area(0기반), Zone, Level, Souls, Cp, Bars, 로비 강화 Lv, Deck, Area 강화 Lv, 냥환생 Lv, God, Preset, RateOverride, TimeScale, 모드 필드(ForwardSpeed, FailOnWall, FixedWeaponTier, CraftPerMade, ZoneHp, WallHpMul, UseCheckpoint, KeepBars, CardPoolId, Layout), Modifiers, Companions. `Validate(tables)`(184-204, 동행 최대 수 = `tables.MaxCompanions()`), `Clone()`(207-214). |

#### Events/

| 클래스 | 파일 | 책임 |
|---|---|---|
| `RunEventType` | `S/MV.Run/Events/RunEvent.cs:9-19` | 이벤트 종류 50여 개(RunStarted, FloorEnter, Fire, ProjImpact, ProjRemoved, Hit, Break, Gain, Hitstop, Shake, Gate*, StairsReceived, Weapon*, WellFlush, Gauge*, Steal, Blocked, God*, Climb*, Roulette*, CardApplied, Toast, Banner, RunFinished, CompanionShot, WeaponRejected). |
| `RunEvent` | `RunEvent.cs:47-89` | readonly struct. 범용 칸(ObjId, KindId, A/B/C, X/Z, Amount/2/3, Flag, Quiet, Color, Key, Ids, Ids2)에 종류별 뜻을 매핑(주석 21-46). `ToString()`은 바이트 비교용 고정 표기(71-79). |

#### State/

| 클래스 | 파일 | 책임 |
|---|---|---|
| `RunPlayer` | `S/MV.Run/State/RunState.cs:7-14` | 플레이어 X, Z, Tx(목표 x), Phase(달리기 위상), ThrowT. |
| `RunProjectile` | `RunState.cs:17-29` | 투사체: Id, X/Z/Sz(출발 z), D(피해), Crit, Tier, Pierce, Spear, Dead, HitIds. |
| `RunGateMark` | `RunState.cs:32-38` | 골목 아치 통과 표시(Z, Floor 1..3, Passed). |
| `PendingWeapon` | `RunState.cs:41-46` | 공방 무기 지급 대기(게임 시간). |
| `PendingReceive` | `RunState.cs:52-65` | 수령 지연 타이머(계단 수령·우물 게이지·무기): PreLeft(게임 시간) → Left(Cond.clock이 Game/Real). |
| `CompanionRuntime` | `RunState.cs:68-75` | 동행 진행. |
| `StormJob` / `MeteorJob` | `RunState.cs:78-93` | 폭풍·유성 진행. |
| `ClimbState` | `RunState.cs:96-113` | 계단 오르기 진행(N, K, Pw, Z0, ZEnd, DH, T, Time, Pos, Lap, Tier, Cards…). |
| `RouletteCard` / `RouletteState` | `RunState.cs:116-141` | 카드 룰렛(결과는 시작할 때 확정). |
| **`RunState`** | `RunState.cs:147-331` | 런 상태 루트(정본 `R`). 아래 1.3 참고. |
| **`RunObject`** | `S/MV.Run/State/RunObject.cs:10-101` | 런 오브젝트 1개. 아래 1.3 참고. |
| `RunObjectFactory` | `RunObject.cs:104-127` | `Create(tables, kind, x, z)`: 종류 행에서 컴포넌트 행 캐시(Enemy, Reaction, Charge, Receive, Move, HpRule)·hw/hd·Shares·DropGroup을 채운다. |

#### Gen/

| 클래스 | 파일 | 책임 |
|---|---|---|
| **`RunLevelBuilder`** | `S/MV.Run/Gen/RunLevelBuilder.cs` | `IRunLevelBuilder`+`IRunStateFactory` 구현. `NewRun`(20-25), `NewState`(28-79), `BuildLevel`(83-162), `FieldSpec`(166-179). 보조: `Preset`(191-197), `StartGod`(200), `KegStart`(203-209), `BaseTier`(218-232), `BuildCardPool`(257-284). |
| `LevelGen` | `S/MV.Run/Gen/LevelGen.cs` | 템플릿 해석: `TplFor`(14-19, 312_templateCycle), `AreaRow`(22-27), `HpRuleOf`(30-40), `InitHp`(52-69), `RewardValue`(72-81), `SetReward/SetKindReward`(84-104), `Spawn`(107-116), `MakeObjs`(132-158). |
| `FieldGen` | `S/MV.Run/Gen/FieldGen.cs` | 골목 장애물 칸 생성: `Hash`(18-24, 정본 `frac(sin(n·127.1+311.7)·43758.5453)`), `Build`(56-116), `InputFor`(119-136), `RewardTypeOf`(139-140). 칸 위치는 `CellMode.Hash`(런타임 해시) 또는 `Baked`(303 행 굳힘 값). |
| `JsMath` | `S/MV.Run/Gen/JsMath.cs` | V8과 비트 동일한 `Sin`(double-double, FMA 없음). |

#### Rule/

| 클래스 | 파일 | 책임 |
|---|---|---|
| `IRunRule` | `S/MV.Run/Rule/IRunRule.cs:10-32` | 확장 훅 7개: ①`Build(RunContext)→RunSetup` ②`BuildCourse(s, CourseRequest)` ③`OnHit(s, HitInfo)` `Tick(s, dt)` ④`CheckEnd(s)→RunEnd` ⑤`Settle(s, reason)→RunResult` `OnEvent(s, e)`. |
| `RunContext` | `IRunRule.cs:37-46` | Build 입력: Tables, Snapshot(RunSetup), MetaModifiers, Companions. |
| `CourseRequest` | `IRunRule.cs:49-57` | (BaseZ, Level, Saved). |
| `HitInfo` | `IRunRule.cs:60-75` | 피격 1회(ObjId, Kind, Dmg 정수, Crit, Killed, Source). |
| **`MainRunRule`** | `S/MV.Run/Rule/MainRunRule.cs:11-43` | 본편 규칙. Build = 스냅숏 복사 + Modifiers·Companions 넣기(21-29), BuildCourse = `builder.BuildLevel`(32), Settle = `RunSettlement.Main`(40). 나머지 훅은 빈 구현. |
| `RunSettlement` | `MainRunRule.cs:46-100` | 본편 정산 계산(`Main` 52-87, `FloorSouls` 89-99). |

#### Sim/

| 클래스 | 파일 | 책임 |
|---|---|---|
| **`RunSim`** (partial ×3) | `S/MV.Run/Sim/RunSim.cs`, `RunSim.Rules.cs`, `RunSim.Companion.cs` | 시뮬 진입점. 공개: `State`, `Rule`, `Create(...)` 3종, `Tick`, `DrainEvents`, `NearestBlockerAhead`(`RunSim.Rules.cs:169-179`, UI 경고용). 규칙 본체는 private. |
| `RunParams` | `S/MV.Run/Sim/RunParams.cs:7-68` | 런 동안 안 바뀌는 표 상수 캐시(`300_runConfig`·`330_climb`·`314_spawnGroup[clone]`). `From(tables)`(27-63), `Shake(t, prefix)`(66-67, `prefix.amp/.time` 키). |
| `RunFormulas` | `S/MV.Run/Sim/RunFormulas.cs` | 순수 스탯 함수(아래 1.6). |

### 1.3 상태 모델

**`RunState`** (`RunState.cs:147-331`) — MV.Run 밖은 읽기만.

- 주입(162-170): `Setup`, `Tables`, `Rng`(규칙 난수), `RngLog`(null 아니면 기록), `Bal`(06_balancePreset 행).
- 확장(172-180): `Rule`(internal set, `RunSim.Create`가 붙임), `ModeRng`·`CompanionRng`(시드 파생 별도 줄기, 생성자 155-156), `Companions`.
- 상태 기계(182-186): `Phase`(기본 Run), `Roulette`(겹침 상태, null 아니면 진행 중), `Climb`, `HitStop`.
- Area·레벨(188-203): Area, ZoneIndex, ZoneHp, StartLevel, Level, Tpl, Base, Gz(Area 아치 z = 골), Floor(HUD 0..3), Cp, CpRun, LvClimbed, SavedBars, Gates.
- 오브젝트(205-209): `Objs`(GEN 생성 순서 = 판정 순서), `Projs`, `NextObjId=1`, `NextProjId=1`.
- 성장(214-229): GaugeLv, GaugeFill, Bt(공방 지급 등급), `W[]`(등급별 지팡이 장수, 길이 = `WeaponTierMax+1`, 생성자 158), Clones, God, El, CardPool, Cards, CardOrder.
- 사격(231-234): `FireT[]`(유닛별 다음 발사까지, 길이 = 1 + `CloneCap`, 생성자 159), Vol(발사 횟수).
- 계단(236-241), 재화(243-263: Gold(double), Souls, Dia, StartGold, StartDia, `EarnedGold`=Gold−StartGold, Walls, StuckT, WeaponRejected, ShiftSum, PendingSettle), 신 효과(265-269), 시간(271-282: Time, RealTime, Frame, WorldY, HasCliff, Cliff), 종료(284-288: Fin, Reason, HitWall, Result), `Events`(291).
- 메서드: `Draw(RngUse)`(294-299, 규칙 난수는 반드시 이것으로), `AddObj`(302-307), `FindObj`(309-313, 선형 탐색), `CardN`, `EqTier`(319-323, W에서 장수>0인 최고 등급), `Emit`(326-330: `Events.Add` 후 `Rule.OnEvent`).

**`RunObject`** (`RunObject.cs:10-101`) — 종류(Kind)+컴포넌트 캐시, 위치·판정(X, Z, Hw, Hd), 체력(HasHp, Hp, Max — HasHp=false면 무한: 우물·게이트·계단·아치), 상태(Dead, Done, Used, Pre, Lvl), 충전 게이트(F, N, Made, Extra, Sink), 우물(Pend, Ft), 이동 AI(Spd), 보상(RewardType/Id/Amount, DropGroup, Shares, SharesPaid), 골목 칸(Bar, Row, Lane, Idx, Rew; `IsField => Idx>=0`), 표시(LabelKey/Arg), 신 효과(Burning, BurnT, BurnDps, BurnTick, Chill).

### 1.4 오브젝트 ID·생성·제거·재사용

- **ID 부여**: `RunState.AddObj`가 `o.Id = NextObjId++` 후 `Objs` 끝에 붙인다(`RunState.cs:302-307`). 투사체는 `AddProj`에서 `Id = S.NextProjId++`(`RunSim.Rules.cs:312`). 둘 다 **런 안에서 단조 증가, 재사용(재활용) 없음**. 새 런은 새 `RunState`라 1부터 다시 시작한다.
- **생성 지점**: 레벨 생성 때만 오브젝트가 생긴다 — `RunLevelBuilder.BuildLevel`이 목록을 만든 뒤 마지막에 순서대로 `s.AddObj`(`RunLevelBuilder.cs:161`). 런 중 동적 스폰 코드는 SIM에 없다(쥐 무리도 템플릿 행에서 미리 깐다, `LevelGen.MakeObjs` 132-158).
- **"죽음"은 제거가 아니다**: 파괴(`Break`, `RunSim.Rules.cs:422`)·쥐 접촉(`StepChase` 254)·뒤로 지나감(265)은 `o.Dead = true`만 한다. 판정 함수들이 `Dead`를 걸러낸다(`Hittable` 156, `Breakable` 165, `Blocks` 166).
- **실제 제거 지점**:
  - `ShiftWorld(cut, L)`(`RunSim.Rules.cs:790-798`): 계단 오르기 시작 때 `o.Z <= cut`인 오브젝트를 `Objs.RemoveAll`로 지우고 나머지·아치·Gz를 L만큼 앞으로 민다.
  - 투사체: `MoveProjectiles` 끝에서 Dead 투사체를 `ProjRemoved` 이벤트와 함께 `Projs.RemoveAt`(352-353). 계단 시작 때 전부 `ProjRemoved` 후 `Projs.Clear()`(776-777).
- **풀링**: MV.Run 안에는 풀이 없다(`new RunObject`/`new RunProjectile`). 뷰 쪽 재사용은 MV.View 몫이다(최근 커밋 `MV-VIEW-OBJRESET`: "같은 번호의 새 오브젝트는 뷰를 새로 만들고, 돌려줄 때 동작 초기화" — 런이 바뀌면 같은 Id가 다시 나오기 때문으로 보인다; View 쪽은 [04](04_View_Fx_Sound_Resources.md) §7.3).

### 1.5 난수·결정성

- 규칙 난수는 하나(`RunState.Rng`)이고 `Draw(RngUse)`로만 뽑는다(`RunState.cs:294-299`). 뽑는 곳: EnemySpeed(`LevelGen.cs:149`), Crit(`RunSim.Rules.cs:289`), PierceChance(292, 확률 0이면 안 뽑음), DoubleShot(300), StairDouble(389), CraftDouble(406), EnemyDrop(451), Proc(500), Storm(535), Blast(542), StormPick/StormJitter(654-657), CardDraw(878, 882).
- **확률 0이면 뽑지 않는다**는 규칙이 MV-SPEC-02 새 규칙에 적용된다(예: `pierceC > 0 && s.Draw(...)` 292, `dblC > 0 && ...` 389·406). 그래서 강화가 0이면 정본과 난수 소비가 같다.
- 모드·동행은 별도 줄기 `ModeRng`·`CompanionRng`(`RunState.cs:155-156`; 동행은 `CompanionOnProjectileHit`에서 `CompanionRng.Chance`, `RunSim.Companion.cs:21`).
- 그림 난수(파티클 위상 등)는 구독자(MV.Fx/View/UI)의 FxRng 몫(`RunRng.cs:6`, `LevelGen.cs:130`).
- 시드: Boot가 런 순번으로 결정(`MvRunSnapshot.SeedFor(runSeq)` SplitMix64, `S/MV.Boot/MvRunStart.cs:112-121`) → `new Pcg32Rng(seed)`(`S/MV.Boot/MvGameLoop.cs:307`). "같은 저장·같은 순번 = 같은 런".
- 필드 칸 배치는 난수가 아니라 해시(`FieldGen.Hash`)라 시드와 무관하게 Area·Floor로 고정.
- 이벤트 `ToString()`·`RngDraw.ToString()`이 `"R"` 형식 고정 표기라 덤프 바이트 비교가 가능(`RunEvent.cs:71-81`, `RunRng.cs:45`).

### 1.6 수치 공식 (`RunFormulas`) — 모두 표 행에서 읽음

| 함수 | 줄 | 식 / 출처 |
|---|---|---|
| `BaseDmg(s)` | 165 | `floor(103_item[eqTier].dmg × (1 + AtkPct/100))` — 정수 버림 |
| `AtkPct(s)` | 156-161 | `(Σ카드 DmgMul p1·n + 14_rebirthPerk[DmgMul].perLv × lv) × 100 + Modifiers.Dmg` |
| `CritMul(s)` | 168 | `14[CritMul] 값 + Modifiers.CritDmg/100` |
| `CritChance(s)` | 239-240 | 카드 합 + `13_areaUpgrade[CritChance]` + Modifiers/100 (상한 없음) |
| `DoubleChance(s)` | 243-244 | min(1, 카드 + 13[DoubleChance] + Modifiers/100) |
| `PierceChance(s)` | 225 | 13[PierceChance].perLv × lv |
| `FireInterval(s)` | 232-236 | `1 / (rate × Π카드 FireRateMul × (1+13[FireRate]) × (1+Mod/100))`, rate = `RateOverride ?? 06.rate` |
| `ProjRange(s)` | 247-248 | `300.PROJ_RANGE × (1+13[Range]) × (1+Mod/100)` |
| `GoldMul(s)` | 228-229 | `(1 + 300.INCOME_PER_LV × UpIncome) × Π카드 GoldMul × (1+Mod.Gold/100)` |
| `Threshold(g, k, bal, zoneHp)` | 256-260 | 107 행: growth(Add/Pow/None) → `ApplyGateFlags`(scale → zoneHp/100 → gauge → mul, 263-270) |
| `StairGateM(...)` | 276-280 | `302_area.sgateM[stairLv−1]`(칸 넘으면 마지막), 없으면 107.base → 플래그 |
| `CloneCap(t)` | 148-153 | min(`300.MAX_CLONES`, `314_spawnGroup`의 `key=="clone"` 행 수) |
| `WeaponTierMax(t)` | 142-145 | 켜진 103_item tier 최대 |
| `StartGold/StartDia` | 185-194 | `302.startGold + 14[StartGold]` / `floor(14[StartDia])` |
| `PerLevelReward` | 200-204 | `13[stat].perLv × lv × Level` (런 종료 때 더함) |
| `RewardShare/SharesDue` | 209-222 | 보상 N등분(마지막 몫이 나머지 흡수) / 누적 피해 k/N 문턱 수 |
| `Tokens` | 324-329 | `TOK_BASE + floor(souls/TOK_SOUL_DIV) + gate?TOK_GATE + open?TOK_OPEN` |
| `ClimbK/ClimbT/TierFor/ClimbTiers` | 295-319 | 330_climb 설정, 115_cardTier.need |

### 1.7 공개 API (View/UI/Boot가 쓰는 것)

| API | 위치 | 호출자 |
|---|---|---|
| `RunSim.Create(RunContext, IRunRule, IRng, IRunLevelBuilder)` | `RunSim.cs:30-36` | `MvGameLoop.CreateSim`(`S/MV.Boot/MvGameLoop.cs:307`) |
| `RunSim.Create(RunSetup, MvTables, IRng, IRunLevelBuilder[, IRunRule])` | `RunSim.cs:24-25, 43-71` | 시험·위 오버로드 |
| `RunSim.Tick(realDt, RunInput)` | `RunSim.cs:82` | `MvGameLoop.TickRun`(387) |
| `RunSim.DrainEvents()` | `RunSim.cs:74-79` | `MvGameLoop.TickRun`(392) → `MvSound.OnRunEvents`, `MvFx.OnRunEvents`, `_world.OnRunEvents` |
| `RunSim.State` (`RunState`) | `RunSim.cs:13` | View(`RunStateViewSource`), HUD(`RunStateHudSource`), `_world.SyncObjects(s.Objs, …)`(`MvGameLoop.cs:399`) |
| `RunSim.NearestBlockerAhead()` | `RunSim.Rules.cs:169-179` | UI 벽 경고 |
| `IRunViewState` | `RunViewState.cs:25-41` | MV.View |
| `RunResult` | `RunApi.cs:8-34` | MV.UI 결과(`ResultViewData.Create`), MV.Content `MvQuest.RecordRun` |
| `RunLevelBuilder`, `MainRunRule`, `Pcg32Rng` | 위 | MV.Boot |
| `RunComponents.IsImplemented` | `RunApi.cs:89-90` | Boot → 표 검증 |

---

## 2. 흐름 — MV.Run

### 2.1 런 생성/시작

1. `MvGameLoop.StartRun`(`S/MV.Boot/MvGameLoop.cs:336-355`): `runSeq = MvMeta.BeginRun()`(저장이 없으면 `PeekRunSeq`) → `CreateSim(runSeq, …)`.
2. `MvGameLoop.CreateSim`(297-308): `MvRunSnapshot.FromStore(store, FloorCount)`(`MvRunStart.cs:38-61`; 저장이 없으면 `Defaults`) → `seed = SeedFor(runSeq)` → `MvRunStart.CreateContext(snap, tables, seed)`(`MvRunStart.cs:131-159`: RunSetup 채움, 13·14 Lv은 `statKey`로 칸 선택 `ApplyGrowth` 165-197, Bars 복사, `MetaModifiers = Zero`) → 저장이 있으면 `ctx.MetaModifiers = MvResearch.Modifiers(store, tables)`(306) → `RunSim.Create(ctx, new MainRunRule(builder), new Pcg32Rng(seed), builder)`.
3. `RunSim.Create(ctx, rule, …)`(`RunSim.cs:30-36`): ① `rule.Build(ctx)` → `MainRunRule.Build`(`MainRunRule.cs:21-29`)가 스냅숏 `Clone` + Modifiers + Companions.
4. `RunSim.Create(setup, …, rule)`(`RunSim.cs:43-71`): null 검사 → `setup.Validate(tables)`(49-50, 실패 시 `ArgumentException`) → builder가 `IRunStateFactory`면:
   - `factory.NewState(setup, tables, rng)` = `RunLevelBuilder.NewState`(`RunLevelBuilder.cs:28-79`): `new RunState`(W·FireT 크기 표에서) → `Bal = Preset(t, setup.Preset)`(null이면 isDefault 행) → Area/Zone/ZoneHp(`setup.ZoneHp ?? 302.hp`) → 시작 재화 Gold/Dia + StartGold/StartDia(44-45) → `StartGod`(null이면 `00_config.startGodKey`), `El` → `BuildCardPool`(111_cardPool: CommonActive/GodCards/Card/Exclude, 257-284) → Bt·W(덱 복사, 또는 FixedWeaponTier) → 게이지 `KegStart`(11_stairLevel) → Clones = min(CloneCap, AupClone) → FireT 초기값 = `300.FIRE_PHASE` → P=(0,0,0).
   - `s.Rule = rule` → ② `rule.BuildCourse(s, new CourseRequest(0, setup.Level, KeepBars ? Bars : null))`(58) → `RunLevelBuilder.BuildLevel`(아래 2.2).
   - (시험 대역 builder면 `NewRun`이 코스까지 만들고 BuildCourse는 건너뜀, 60-64.)
5. 동행 런타임 생성(65-66) → `new RunSim` → `OnCreated()`(`RunSim.Rules.cs:19-25`): `_p = RunParams.From(T)`, 이 Area의 `302.sgateM` 캐시, `RunStarted`·`WeaponHandReset` 이벤트.
6. Boot가 `RunStateViewSource`를 걸고 HUD로 전환(`MvGameLoop.cs:349-354`).

### 2.2 레벨(코스) 생성 — `RunLevelBuilder.BuildLevel` (`RunLevelBuilder.cs:83-162`)

1. 313_fieldGen 행(`FieldGenKey`, 기본 `"default"`) 선택(87). cp = UseCheckpoint면 min(CP_MAX, Setup.Cp)(90). 템플릿 id = `LevelGen.TplFor(t, level)`(91, 312_templateCycle).
2. ① 템플릿 오브젝트: `LevelGen.MakeObjs`(96) — 311_templateObject 행을 z 순서로, `groupId`가 있으면 314_spawnGroup 행마다 1개(x는 ±ENEMY_X_MAX), 이동 AI에 속도 범위가 있으면 RunRng 1회(EnemySpeed). `Spawn`이 `RunObjectFactory.Create` → `InitHp`(114_hpRule: AreaHpMul/Fixed, ×scale, ×obstMul) → `SetKindReward`(105.rewardSource Row/Enemy, 값은 zone·DiaCap 규칙).
3. ② 보너스 상자 줄(99-108): 313의 bonusRows×bonusLaneX, 보석 칸 1개.
4. ③ 골목 아치·필드(111-141): `f = cp .. FloorCount−1`마다 아치 오브젝트 + `RunGateMark` → `FieldSpecFor(s, f)`(=`FieldGen.Build`)의 칸마다 벽 오브젝트: `mx = max(1, round(c.Hp × obstMul × WallHpMul))`, 저장 기록이 있으면 `min(kept, mx)`, hp≤0이면 `Dead=Pre=true`(이미 부순 칸, 보상 없음).
5. ④ Area 아치(골) `gz`(144-149).
6. 모든 오브젝트 `Lvl=level`, 상태 칸(Level, Tpl, Base, Gz, Floor=0, Cp, CpRun, LvClimbed=0, SavedBars) 채우고 순서대로 `AddObj`(151-161).

`FieldGen.Build`(`FieldGen.cs:56-116`): n = rows×lanes 칸, 시드 = `(seedArea+1)·seedA + floor·seedF + seedC`. Soul 칸 sN개를 해시로 고르고(Hash 모드) 또는 303 굳힘 값(Baked 모드), 칸 HP는 h0→h1 선형 보간 × scale, Gold 양 = `round((goldBase + goldPerZone·zone)·(1+goldPerRow·r))`, Dia 칸은 해시 < diaChance일 때 값 = DiaCap.

### 2.3 한 틱 처리 순서 — `TickCore` (`RunSim.Rules.cs:29-52`)

1. `Frame++`, Lobby면 끝(32-33).
2. `dt = min(300.Time_DtCap, realDt)`, `RealTime += dt`, 드래그 입력이면 `P.Tx = clamp(DragTargetX, ±LANE_X_MAX)`(34-36).
3. `StepRealTimers(dt)`(38, 717-732): 실시간 시계 수령(`ClockKind.Real`) 만기 → `Deliver`; 룰렛 카드 비행 만기 → `ApplyCard`.
4. 히트스톱: Run 단계에서 `HitStop>0`이면 `gdt = dt × Time_HitStopScale`(40-42). `ts = Setup.TimeScale`(디버그 배속), `sdt = gdt × ts`(43-44).
5. 단계별 진행(46-48): `Run`이면 `UpdRun(gdt)`를 ts번(중간에 Phase 바뀌면 멈춤), `Climb`면 `UpdClimb(sdt)`. 룰렛이 있으면 단계와 무관하게 `UpdRoulette(sdt)`.
6. 키 입력 `KeyAxis`면 `P.Tx += axis × dt × KEY_MOVE_SPEED`(49).
7. `StepFx(sdt)`(50, 638-690): 폭풍 타격·유성 착탄(피해 포함), 수령 앞 지연(PreLeft) 감소, 게임 시계 수령 만기 → `Deliver`.
8. `SyncWells(dt)`(51, 694-713): 우물에 쌓인 피해(Pend)를 묶음 간격·뒤로 지나감 조건으로 비우고 `WellFlush` 이벤트 + 게이지 수령 대기(`PendingReceive{Type=GaugeFill}`) 추가.

**`UpdRun(dt)`** (`RunSim.Rules.cs:56-150`) — 정본 updRun 16단계:

| 단계 | 줄 | 내용 |
|---|---|---|
| 1 | 60-62 | `Time += dt`, P.X를 Tx 쪽으로 `MOVE_FOLLOW_K` 추종, 레인 clamp |
| 2 | 64-72 | 골목 아치 통과 → `Floor` 갱신, 이전 골목 Soul 칸 모두 부쉈는지 `FloorEnter(flag)`. `P.Z > Gz`면 **`Finish(Gate)`** |
| 3 | 74-81 | 다음 z `nz = P.Z + (ForwardSpeed ?? RunSpeedAt)·dt`(중간 구역은 `RUN_SPEED_MID`, 238-244). OnArrive/StartClimb 계단에 닿으면 `StartClimb` 성공 시 P.Z 고정하고 **이 틱 종료** |
| 4 | 83-92 | 벽 막힘: `blocks` 오브젝트 앞면에서 nz를 자름 → `blocked` |
| 5 | 94-98 | OnPass 게이트 통과 → `PassGate`(183-206: 게이트 안이면 `GateTake` + 지연 수령 등록, 밖이면 `GateMiss`) |
| 6 | 100-110 | 공방 무기 지급 대기(게임 시간) 만기 → `DeliverWeapons(N × CraftPerMade)` |
| 7 | 112-113 | 이동 AI Chase(`StepChase` 248-266: 접근·접촉 시 훔치기 `Steal`(EarnedGold만), 뒤로 지나가면 Dead) |
| 8 | 115 | 게이트 가라앉기(그림) |
| 9 | 117-129 | 막힘 지속: 처음이면 `Blocked`+흔들림, `StuckT += dt`, `FailOnWall && StuckT ≥ STUCK_END_SEC`면 **`Finish(Wall)`**. 막힘 중에도 사격은 계속 |
| 10 | 131-132 | `Shoot(dt)` |
| 11·12 | 134 | `MoveProjectiles(dt)` (명중 → `Hit`, 죽은 탄 제거) |
| (동행) | 136 | 동행이 있으면 `StepCompanions(dt, vol0)` |
| 13 | 138 | 빛·유성(`StepBeamMeteor`, 휴면 카드) |
| 14 | 140-141 | 폭풍·폭발 쿨다운, `StepBurn`(화상 틱 피해) |
| 15 | 143 | 위성·칼날(`StepOrbSpin`) |
| 16 | 145-149 | `Rule.Tick` → (이미 끝났거나 Run이 아니면 끝) → `Rule.CheckEnd`: Clear→`Finish(ModeClear)`, Fail→`Finish(ModeFail)` |

### 2.4 전투·피해 계산

**사격** `Shoot(dt)` (`RunSim.Rules.cs:270-308`)
1. 이번 틱 공통값: 발사 간격 iv, 장착 등급 tier, crC/dbC, 카드 관통 수, 관통 확률, 창(PierceEveryN) 카드, 속성 `MainElem`(신 카드 보유 시 `S.El`).
2. 유닛 = 본체 + 분신(`1 + Clones`). 분신 위치 = `RunParams.CloneOff[i−1]`(314 clone 행), x는 ±CLONE_X_MAX.
3. 유닛별 `FireT[i] -= dt`, 0 이하면 `FireT[i] += iv`(누적 오차 유지) → `crit = Draw(Crit) < crC` → `d = floor(BaseDmg × (crit ? CritMul : 1))`(290) → 관통 +1 추첨(292) → 본체면 `Vol++`.
4. 본체이고 `Vol % p1 == 0`이면 창 투사체(피해 ×(1+p2·n), 관통 p3) 1발(294-299). 아니면 `Draw(DoubleShot) < dbC`면 좌우 2발(±DBL_SHOT_DX), 아니면 1발.
5. `AddProj`(310-315): Id 부여, `ProjSpawnDz` 앞에서 출발, `Fire` 이벤트.

**투사체 이동** `MoveProjectiles` (`324-354`): 탄마다 `nzb = Z + PROJ_SPEED·dt` 구간에서 Hittable·x폭 안·아직 안 맞힌 오브젝트 중 앞면이 가장 가까운 것 1개 → `ProjImpact` → `Hit(best, b.D, crit, proc:true, …)` → (동행 Chance 판정) → `HitIds`에 추가, `--Pierce < 0`이면 Dead. 사거리(`ProjRange`) 넘으면 Dead. 끝에서 Dead 탄 제거.

**피해 적용** `Hit(o, dmg, crit, proc, quiet, color, source)` (`358-417`)
1. Chill이면 ProcChill 카드 배율(362, 휴면).
2. **최종 피해 = `min(DMG_MAX, max(DMG_MIN, floor(dmg)))`**(363) — 정수 버림, 최소값 표.
3. `Hit` 이벤트(365).
4. 반응 type(106)별:
   - `ChargeWell`: `o.Pend += dmg`(우물, 나중에 SyncWells가 게이지로).
   - `Damage`: `Hp -= dmg`; ≤0이면 `Break`(파괴: Dead, breakStat Walls++, `Break` 이벤트, 남은 몫 전부 지급, 흔들림·히트스톱, 419-429), 아니면 `PayShares(o, SharesDue(Max−Hp, Max, Shares))`(누적 피해 비율만큼 보상 몫 선지급).
   - `ChargeStairGate`: `F += dmg / (1 + N·diff·diffMul)`, `F ≥ M`(= `StairGateM`)마다 `N += GaugeLv`(StairDouble이면 ×2), `GateFill` 이벤트(377-394).
   - `ChargeCraftGate`: `F += dmg`, `Made < cap`이고 문턱(`Threshold(g, Made)`) 넘을 때마다 Made++(CraftDouble이면 Extra++), `GateFill`(395-411).
5. `killed = !wasDead && o.Dead`. `proc`이면 `GodOnHit`(490-547): Proc 카드(ExtraHit/Burn) 확률, ChainJump(가까운 대상으로 연쇄 hit), ChainSplash(범위), Storm(쿨다운+확률 → StormJob), Blast(쿨다운+확률 → 범위 hit). 이 안의 hit은 `proc:false`라 재귀 가호 없음.
6. `Rule.OnHit(s, HitInfo)`(416) — 가호까지 끝난 뒤, 중첩 hit은 안쪽부터.

**보상 지급** `PayShares` → `Grant` (`435-481`): 몫마다 `RewardShare`, 마지막 몫(=파괴) 때 `801_drop` 추첨(`MvDropRoller.RollDrop`, 난수 EnemyDrop). `Grant`: RunGold는 `× GoldMul`, RunSouls/RunDia는 정수 가산, 그 밖의 소비 종류는 `PendingSettle`(정산 대기, 지급은 Meta). `Gain` 이벤트.

**동행** (`RunSim.Companion.cs`): Seconds 타이머/Hits(Vol 배수)/Chance(명중마다) → 대상 정렬(앞면 z, |x−P.x|, Objs 순) → `FireCompanion`: `BaseDmg × DmgPct/100`으로 즉시 hit(proc 없음, 90-101).

### 2.5 스테이지 진행(골목·계단·룰렛) · 클리어/실패

- **골목(Floor) 진행**: 아치 z를 넘으면 `Floor` 갱신·`FloorEnter`(2단계). 체크포인트 cp 이전 골목은 애초에 생성하지 않는다(`BuildLevel` f = cp부터).
- **계단 수령**: 계단 게이트(ChargeStairGate)를 쏘아 N을 모으고 → 통과 시 게이트 안에 있으면 지연 수령(`PendingReceive{RunStairs}`) → `Deliver`에서 `Stairs += N`(`740-743`).
- **계단 오르기**: 계단 오브젝트(OnArrive/StartClimb)에 도달하면 `StartClimb`(771-787): 계단 0이면 토스트만. 있으면 투사체 전부 제거, `Phase=Climb`, k = min(kMax, n), 옹벽(Cliff) 기록, `ShiftWorld`로 지난 오브젝트 삭제·앞으로 밀기, `ClimbState` 생성, `ClimbStart`.
- `UpdClimb`(800-848): 가운데로 당기기, 진행률 p, 칸 보이기(`ClimbStepShown`), lap마다 카드(`ClimbLap`), 남은 칸으로 등급(`TierFor`, `ClimbTierUp`), 워프 단계, 다 오르고 `sideOut` 지나면 `ClimbEnd` → `EndClimb`(850-861: Climbed 누적, P.Z 착지, `WorldY += DH`, Phase=Run, `StartRoulette(tiers, N)`).
- **룰렛**: `StartRoulette`(885-906): 앞 룰렛 미적용 카드는 즉시 적용(D-23), 등급마다 `DrawCard`(그 등급 이하로 내려가며 풀에서 CardDraw 난수, 분신 상한이면 CloneAdd 제외) — **결과는 시작 때 확정**. `UpdRoulette`(908-941)는 표시 타이밍(Flip→Chosen→Fly)만 진행, 비행 끝(실시간) → `ApplyCard`(943-954: Cards 증가, CloneAdd면 Clones += p1 (상한 MaxClones), `CardApplied`).
- **무기**: `AddWeapon`(212-225) — W[t]++ 후 `MERGE_N`장마다 윗등급으로 머지, `WEAPON_STACK_MAX` 초과면 거절(`WeaponRejected`).
- **게이지**: `GaugeAdd`(755-767) — 우물 107 행 문턱마다 GaugeLv++(`GaugeLevelUp`), cap 도달 시 Fill=0. GaugeLv는 계단 게이트 1회 충전량이자 M 표의 Lv.
- **종료 조건**:
  - 클리어(Area 돌파 아치): `P.Z > Gz` → `Finish(Gate)`(72).
  - 실패(벽): 연속 `STUCK_END_SEC` 막힘 → `Finish(Wall)`(127).
  - 모드: `Rule.CheckEnd` → ModeClear/ModeFail(147-149). 본편 MainRunRule은 항상 None.
- **`Finish(reason)`**(959-967): 한 번만(`Fin`), `Phase=Result`, `Rule.Settle` → `State.Result`, `RunFinished(flag = Pass||ZoneEnd)` 이벤트.
- **정산 계산** `RunSettlement.Main`(`MainRunRule.cs:52-87`): 이번 Level의 필드 칸 HP를 BarRecord에 기록 → 앞에서부터 골목 Soul 칸을 모두 부순 골목 수(`FloorSouls`) = cpN(≤CP_MAX) → `CpRun = max(Cp, cpN)` → 302.needSouls와 누적 Souls 비교로 `Pass`(gate && enough && !last) / `ZoneEnd`(gate && enough && last, last = Area ≥ PLAYABLE_AREAS−1) → Gold/Dia에 레벨당 보상 더함 → Tok 계산 → PendingSettle 복사. `UseCheckpoint/KeepBars` 꺼지면 CpRun 0·BarRec null.

### 2.6 이벤트 계통 (View·Fx·Sound로)

- 생성: SIM 내부에서만 `RunState.Emit(in RunEvent)`(`RunState.cs:326-330`). 쌓이고 나서 `Rule.OnEvent` 호출(모드가 관찰).
- 소비: Boot가 매 프레임 1번 `DrainEvents()`(목록 복사 후 비움, `RunSim.cs:74-79`) → `MvSound.OnRunEvents` → `MvFx.OnRunEvents` → `_world.OnRunEvents`(`MvGameLoop.cs:392-395`), `ClimbStart`면 계단 위 지형 재생성(396-398). 같은 프레임에 Tick이 여러 번(`TicksPerFrame`)이면 이벤트가 모아서 한 번 넘어간다.
- 오브젝트 위치·생사는 이벤트가 아니라 `State.Objs`를 매 프레임 동기화한다(`_world.SyncObjects(s.Objs, s.P.Z, s.WorldY)` 399).

---

## 3. MV.Meta — 메타 진행 (영속 변경의 유일한 진입점)

### 3.1 구조: 요청 → 백엔드 판정 → 원자 저장 → 알림

| 클래스 | 파일 | 종류 | 책임 |
|---|---|---|---|
| **`MvMeta`** | `S/MV.Meta/MvMeta.cs:21-412` | sealed class | 화면이 부르는 명령 모음 + `Send`. `static Current`(Boot가 세움). 메인 스레드 전용. |
| `MvMetaRequest` | `S/MV.Meta/MvMetaRequest.cs:11-33` | sealed class | 요청 1건(Name + JObject Params). 주석: platform.network `NetworkRequest`와 같은 모양, 전환 때 하위 클래스로 바꿀 자리(8-9행). |
| `MvMetaRequestNames` | `MvMetaRequest.cs:36-80` | static | 요청 이름(서버 함수 이름과 같게): `meta.settle`, `meta.buyLobbyUp/AreaUp/Perk`, `meta.rebirth`, `meta.selectGod`, `meta.grantAd`, `meta.useAd`, `meta.recordModePlay`, `meta.markSeen`, `meta.setMuted`, `meta.questRecord/questClaim`, `meta.startResearch/claimResearch`, `debug.*` 8종. |
| `MvMetaErrors` | `MvMetaRequest.cs:83-107` | static | 거절 코드(`NetworkRejection.Code` 자리). |
| `MvMetaResponse` | `MvMetaRequest.cs:113-137` | sealed class | `Ok`(Code==null), `Data`(컨테이너 dataKey → 새 값, 병합 저장 대상), `Result`(화면용 부가 정보), `ResetProgress`. |
| `IMvMetaBackend` | `MvMetaRequest.cs:144-147` | interface | `Handle(request)`. "처리기는 상태를 바꾸지 않는다 — 새 값만 계산". 주석: platform.network `INetworkLocalService` 자리. |
| **`MvLocalMetaBackend`** (partial ×2) | `S/MV.Meta/MvLocalMetaBackend.cs`, `MvLocalMetaBackend.Research.cs` | sealed class | 로컬 백엔드(서버 자리). 요청 판정 → `Data`/`Result` 계산. 콘텐츠 처리기·응답 보탬 등록소. |
| `MvMetaRules` | `S/MV.Meta/MvMetaRules.cs:23-117` | static | 순수 메타 공식(아래 3.3). |
| `MvUnlockRules` | `S/MV.Meta/MvUnlockRules.cs:41-129` | static | 해금 판정 한 곳(05_contentOpen). `State`(70-76), `IsOpen`, `Raise`, `MapArea`. |
| `MvUnlockState`/`MvUnlockProgress` | `MvUnlockRules.cs:8-34` | enum/struct | Hidden/Locked/Open, (Area, Runs, Level). |
| `MvResearchRules` | `S/MV.Meta/MvResearchRules.cs:40-126` | static | 440_research 규칙: `Groups`, `Steps`, `Next`, `FinishAt`, `IsDone`, `Status`, `Reached`, `Totals`(112-125). |
| `MvRunOutcome` / `MvRunEnd` | `S/MV.Meta/MvRunOutcome.cs:7-37` | class/enum | 정산 입력(MV.Run을 모르므로 UI가 RunResult를 이 모양으로 옮김). |
| `MvSettleResult`, `MvLobbyBuyResult` | `MvRunOutcome.cs:40-78` | class | 결과 화면용. |
| `MvWealthChange` | `S/MV.Meta/MvWealthChange.cs:16-55` | sealed class | 콘텐츠 처리기용 재화 변경 묶음(백엔드 내부 `WealthDelta`와 같은 규칙). |
| `IMvClock`·`MvSystemClock`·`MvManualClock`·`MvOffsetClock` | `S/MV.Meta/MvMetaPorts.cs:9-40` | 시계 | `MvOffsetClock`은 개발 빌드 `debug.addTime` 보정(메모리만). |
| `IMvAdService`·`MvFakeAdService` | `MvMetaPorts.cs:46-78` | 광고 포트 | 공급자 없으면 즉시 "봤음". |

### 3.2 `MvMeta` 공개 API (화면·Boot가 부름)

| 메서드 | 줄 | 뜻 |
|---|---|---|
| `Send(request)` | 67-94 | 핵심. 아래 4.1 흐름. |
| `SendDebug(name, key, amount)` | 97 | 테스트 패널 |
| `BeginRun()` | 102-107 | 런 순번 발급 = max(발급, LastSettledRunSeq)+1 (중복 정산 방지) |
| `CanCollectX3(outcome)` | 110-111 | 결과 광고 ×N 버튼 활성(얻은 몫 기준) |
| `Settle(outcome, mul)` | 114-144 | `meta.settle` 요청 → `MvSettleResult` |
| `SettleWithAd(outcome, done)` | 147-155 | collect3 광고 후 ×mul 정산 |
| `Level/IsMax/NextPrice/CanBuy/CanAfford` | 198-239 | 성장 읽기(10 lobbyUpgrade, 13 areaUpgrade, 14 rebirthPerk, 16 levelPrice) |
| `Buy/BuyLobbyUp/BuyAreaUp/BuyPerk` | 241-262 | 성장 구매 요청 |
| `StairStart/WeaponLevel/GoldMul/PerkValue/AreaUpValue` | 265-273 | 효과 값 읽기 |
| `RebirthOpen/CanRebirth/Rebirth/PressRebirth/DisarmRebirth` | 277-302 | 부활(두 번 누르기 `00_config.rebirthArmSec`) |
| `GodUnlocked/SelectGod` | 308-309 | 신 선택 |
| `Today/AdsLeft/ModePlaysToday/SetMuted/RecordModePlay/SkipsAd/WatchAd/WatchAdForReward` | 314-372 | 광고·일일 |
| `IsOpen/UnlockState/PendingUnlockShows/IsNew/MarkSeen` | 377-398 | 해금 |
| `Local` | 403 | 로컬 백엔드(콘텐츠 등록·읽기용) |
| `StartResearch/ClaimResearch` | 408-411 | 연구 |

호출자(검색 확인): `MvBootMeta.OnSaveChanged`가 생성(`S/MV.Boot/MvBootMeta.cs:86-91`), `MvGameLoop`(BeginRun 339), UI 포트들(`MetaUiPort.cs:122-127`, `ResearchPort.cs:27-32`, `ContentMenuPort.cs:29-34`, `ResultContracts.cs:95-135`, `FakeAdPopup.cs:146-154`, `RebirthPresenter.cs:22`).

### 3.3 메타 공식과 표 출처 (`MvMetaRules`)

| 함수 | 줄 | 표 |
|---|---|---|
| `CollectAdMul` | 36-40 | `08_adPlacement[collect3].mul` |
| `NextPrice` | 46-47 | `16_levelPrice[group][P+1].price` (없으면 MAX) |
| `StairStart` | 53-57 | `11_stairLevel.need`, 상한 = lv 최댓값 |
| `WeaponLevel` | 60-65 | `12_weaponLevel.need` |
| `DeckAdd` | 80-90 | `300.MERGE_N`장 머지, 상한 = 덱 길이 −1 |
| `GoldMul` | 93 | `300.INCOME_PER_LV` |
| `Tokens` | 96-102 | `300.TOK_*` (RunFormulas.Tokens와 같은 식을 Meta가 다시 계산) |
| `NeedSouls` | 105-110 | `302_area.needSouls` |
| `IsLastArea` | 113 | `300.PLAYABLE_AREAS` |
| `ZoneIndex` | 116 | `302.zoneId → 301 sort` |

### 3.4 `MvLocalMetaBackend` 처리기 요약

- `Handle`(47-54): `Dispatch` → 성공이고 초기화 응답이 아니면 **등록된 contributor들이 같은 응답 `Data`에 값을 보탬**(퀘스트 카운터). 저장 1번에 묶기 위한 장치.
- `Dispatch`(56-83): 저장소·재화 서비스 없으면 `NoStore`. switch로 기본 요청, 아니면 `_content`(Content 등록) → `_debug`(개발 빌드) → `UnknownRequest`.
- `Settle`(87-171): 서버 판정 자리 — 클라이언트가 보낸 pass/tok을 믿지 않고 **저장된 Area·재화로 다시 판정**한다. runSeq 중복 거절, mul은 1 또는 collect3.mul만, 금액 = `floor((gold−startGold)×mul + startGold)`, Dia 동일, Souls 그대로. PendingSettle 중 Wealth·RbPending만 반영(나머지 `unsettled`로 돌려줌). progress(level+1, runs+1, lastSettledRunSeq, cp, area, bars), rebirth.pending += tok. **pass면 로비 강화·덱·Area 강화를 null(기본값)로 초기화**(153-158). 해금 갱신(`WriteUnlocks`). Result에 areaBefore/After, pass, zoneEnd, gold, dia, souls, tok, zoneChanged, newlyOpened, interstitial, unsettled.
- `BuyLobbyUp/AreaUp/Perk` → `Buy`(219-287): MAX·가격 확인 → `WealthDelta.Cost` → 같은 Data에 재화 행 + 단계 +1. weapon(DeckAdd 효과)은 덱에 카드 추가·머지(235-246).
- `Rebirth`(298-325): 해금·쿨타임 확인 → gold·souls 0, goldenFish += pending, progress/lobby/areaUp 기본값, rebirth.lv+1, cdUntil = now + `00_config.rebirthCooldownSec`. sticky 해금 유지.
- `SelectGod`(341-347), `GodUnlocked`(329-339; unlockType 0=항상, 1=05_contentOpen, 그 밖=`mvGod.gods`에 있음).
- `GrantAd/UseAd/RecordModePlay`(377-431): 08_adPlacement 한도, `mvDaily` 날짜 롤(`RolledDaily` 354-362, `00_config.adDayUtcOffsetHour`).
- `MarkSeen/SetMuted`(443-460).
- `StartResearch/ClaimResearch`(`MvLocalMetaBackend.Research.cs:31-78`): 해금(`research`)·진행 중 1칸 확인 → 비용 + `run{key, finishAt}` 저장 / 끝 시각 지났으면 lv+1, run=null.
- 디버그 처리기(479-576): grant, levelUp, reset(`MvMetaResponse.Reset`), openRebirth(+`DebugRebirthPending = 10` 상수), clearCooldown, unlockAll, resetDaily, addTime(시계 보정만).
- `WealthDelta`(594-630): 비용·획득·고정값을 모아 `ResourceService.Get`(예약 제외)으로 부족 검사, `WealthContainer` 행(`type`, `quantity`)으로 Data["wealthInfos"]에 넣음. 주석: Framework `LocalRewardService.PrepareMutation/Commit`은 재화 컨테이너만 저장해 다른 컨테이너와 원자로 못 묶어서 대체(276-277, 590-593).

### 3.5 NetworkRequest / INetworkLocalService 현황 (프로젝트 규칙 대비)

- 규칙 취지: "서버 판정 변경은 `NetworkRequest` 하위 클래스로, 로컬 `INetworkLocalService`가 처리".
- **현재 구현 상태**:
  - Framework `PlatformLibrary.Network` 쪽 틀은 MV.Platform에 있다: `MvLocalNetworkService : INetworkLocalService`(`S/MV.Platform/Net/MvLocalNetworkService.cs:18-49`, 이름→처리기 사전, `HandleAsync`, 거절은 `NetworkRejection`), 예시 요청 `MvPingRequest : NetworkRequest`(`S/MV.Platform/Net/MvPingRequest.cs:11-40`, `net.ping`).
  - Boot 9단계에서 `NetworkManager.SetLocalService(MvPingRequest.RegisterLocal(new MvLocalNetworkService(), new MvLocalServerClock()))`, `NetworkManager.DefaultRoute = NetworkRoute.Local`(`S/MV.Boot/GameBootstrap.cs:115-116`).
  - **그러나 게임 메타 요청(정산·구매·부활·광고·퀘스트·연구)은 아직 NetworkManager를 거치지 않는다.** `MvMetaRequest`는 `NetworkRequest`와 같은 모양의 자체 클래스이고, `MvMeta.Send`가 `IMvMetaBackend.Handle`(= `MvLocalMetaBackend`)을 동기 호출한다(`MvMetaRequest.cs:7-9, 139-143`, `MvLocalMetaBackend.cs:14`). 전환 지점: `MvMetaRequest`를 `NetworkRequest` 하위로, `MvMeta.Send`를 `NetworkManager.Post`로, `MvLocalMetaBackend`의 처리기를 `MvLocalNetworkService.Register`로, 반영은 `NetworkManager.AddResponseApplier`로(MvLocalNetworkService 주석 13행).
  - 구조적으로는 이미 같은 계약을 따른다: 처리기는 상태를 바꾸지 않고 새 값만 반환 → 반영은 한 곳(`MvMeta.Send`)에서 원자 저장.

---

## 4. 흐름 — Meta·Content·Save

### 4.1 메타 요청 공통 흐름 (request → response → store → callback)

1. 화면/Boot가 `MvMeta.Xxx()` 호출 → `new MvMetaRequest(name, params)` → `MvMeta.Send`(`MvMeta.cs:67-94`).
2. `IsReady` 아니면 `NoStore` 거절(70). `IsReady` = 저장소 열림·`RequiresReopen` 아님·ResourceService 있음(62).
3. `_backend.Handle(request)`(71) → `MvLocalMetaBackend.Handle`(`MvLocalMetaBackend.cs:47-54`) → `Dispatch` → 처리기(판정, 새 값 계산만) → 성공이면 contributor(퀘스트)들이 `response.Data`에 `mvQuest`를 보탬.
4. 실패 응답이면 그대로 반환(72).
5. 성공: `keys = response.Data`의 최상위 키들(73). `ResetProgress`면 `Store.DevResetProgress()`(78-82). `keys`가 있으면 **`Store.ApplyAndSave(response.Data, keys)` 한 번**(83) — 저장 실패 예외면 로그 + `SaveFailed` 거절(85-90; 화면은 토스트 후 `MvSave.Reopen`).
6. `Applied?.Invoke(request, response)`(92) → 화면은 이것 또는 `DataRepository.Changed`로 다시 그린다(주석 17-19).
7. 응답 반환 → 각 명령 메서드가 `Result`를 화면용 결과 객체로 풀어 줌(예: `Settle` 128-143, `BuyLobbyUp` 251-259).

※ `MvSaveTriggers`(`S/MV.Save/MvSaveSchema.cs:67-87`)에 사건별 키 묶음이 정의돼 있지만 `MvMeta.Send`는 이를 쓰지 않고 응답 Data 키로 저장 대상을 정한다(검색상 `MvSaveTriggers.` 호출처 없음).

### 4.2 런 종료 → 보상 → 메타 반영 → 저장

1. SIM `Finish(reason)` → `State.Result`(`RunSim.Rules.cs:959-967`).
2. `MvGameLoop.TickRun`이 `s.Phase == Result`를 보고 `FinishRun`(`MvGameLoop.cs:402, 409-427`): `_questRun = r` 보관, `ResultViewData.Create(r, runSeq, savedArea, savedSouls, tables)`(`S/MV.UI/Result/ResultContracts.cs:45-76`: 결과 종류 ZoneClear/AreaOpen/LevelDone/Fail, **`MvRunOutcome` 생성** — End = Gate/Wall/Other, Gold/Souls/Dia/StartGold/StartDia/CpRun/Bars(=BarRec.Floors)/PendingSettle), Settler = `CountingSettler`(저장 없으면 `NoSaveSettler`).
3. 지도 팝업 → 닫으면 결과 팝업(`OpenMapThenResultAsync` 433-450).
4. 「받기」 → `ResultPresenter.TryCollect(mul)`(`S/MV.UI/Result/ResultPresenter.cs:87-96`) → `CountingSettler.Settle`(`MvGameLoop.cs:498-511`) → `MvMetaResultSettler` → `MvMeta.Current.Settle(outcome, mul)`(`ResultContracts.cs:102-104`). 광고 ×N 경로는 `SettleWithAd`(collect3).
5. `MvMeta.Settle`(`MvMeta.cs:114-144`): JObject 파라미터(runSeq, end, mul, gold, souls, dia, cpRun, startGold, startDia, bars 배열, pending) → `Send(meta.settle)`.
6. `MvLocalMetaBackend.Settle`(87-171) 판정(3.4 참고) → contributor가 `RunComplete/GoldEarn/SoulEarn/AreaOpen` 퀘스트 카운터 보탬(`MvQuest.EventsOf` 170-175).
7. `MvMeta.Send` → `ApplyAndSave`(wealthInfos, mvProgress, mvRebirth, [mvLobby, mvAreaUp], [mvUnlock], [mvQuest], + mvMeta.lastSaveAt) 1번.
8. 성공이면 `CountingSettler`가 `MvQuest.Current.RecordRun(_questRun)`으로 계단·벽 카운터를 **별도 요청**(`meta.questRecord`)으로 저장(`MvGameLoop.cs:503-509`; 이 값은 정산 응답에 없어서).
9. `OnCollected` → 로비로(479-484). 전면 광고(`ShowInterstitial`)는 2차.

### 4.3 메타 업그레이드 요청 흐름 (예: Area 강화)

1. UI `MetaUiPort` → `MvMeta.BuyAreaUp(key)`(`MvMeta.cs:261`) → `Buy(MvGrowth.AreaUp, key)`(241-246) → `Send(meta.buyAreaUp {key})`.
2. `MvLocalMetaBackend.BuyAreaUp`(251-259): `13_areaUpgrade.ByKey(key)` active 확인 → 현재 Lv(`S.AreaUp.GetLevel`) → lv 사전 복사 후 +1 → `Buy`(271-287): `LevelMax(row)` 초과면 `Max`, `MvMetaRules.NextPrice(16_levelPrice)`, 비용은 Wealth만 허용, `WealthDelta.TryWrite`(부족이면 `NotEnough`) → `Success({wealthInfos, mvAreaUp:{lv}}, {key, after})`.
3. contributor: `AreaUpgrade` 퀘스트 카운터 +1(`MvQuest.cs:179-181`).
4. `MvMeta.Send` → `ApplyAndSave` 1번 → `Applied` 알림.
5. 다음 런 시작 때 `MvRunSnapshot.FromStore`가 `AreaUps`를 읽고(`MvRunStart.cs:51`) `ApplyGrowth`가 `statKey`로 `RunSetup.Aup*`에 옮긴다(165-181) → SIM `RunFormulas.Aup*`가 `perLv × lv`로 사용.
- 로비 강화(`BuyLobbyUp` 219-249)는 weapon이 덱 머지(`DeckAdd`), stair는 시작 게이지 Lv 결과만 돌려줌. 냥환생(`BuyPerk` 261-269)도 같은 `Buy`.

### 4.4 퀘스트 진행 갱신 흐름 (MV.Content)

| 클래스 | 파일 | 책임 |
|---|---|---|
| `MvQuestEvent` | `S/MV.Content/Quest/MvQuest.cs:13-25` | 카운터 1건(clearType, condition, amount) |
| `MvQuestBackend` | `MvQuest.cs:31-68` | 처리기 등록(`Register` 34-45: questRecord·questClaim), 응답 보탬(`Contributor` 51-65) |
| `MvQuest` | `MvQuest.cs:77-197` | 화면 입구. `Current`, `Install(meta)`(91-95), `Board`, `LifetimeCount`, `HasClaimable`, `Claim`, `Record`, `RecordRun`(150-155), `RecordModeClear`, `EventsOf`(163-192), `LobbyCondition`(195, 10_lobbyUpgrade.sort) |
| `MvQuestRules` | `S/MV.Content/Quest/MvQuestRules.cs:67-372` | 순수 규칙: 기간 키(UTC 일/월), `Read`(104-119, 기간 바뀐 칸은 빈 값으로 롤), `Stale`, `Write`, `Slot`, `Supported`, `Add`(183-191: achieve·daily·monthly 세 칸에 동시, 조건 있으면 0과 조건 키 둘 다), `Entry/StepEntry`, `Board`(259-278: 410 묶음, 413 칸, 420 업적 단계), `Record`(286-303), `Claim`(310-371) |
| `MvQuestEntry/GroupView/Snapshot/State` | `MvQuestRules.cs:15-60` | 화면 모델 |

연결: `MvBootMeta.OnSaveChanged`가 `MvMeta` 생성 직후 `MvQuest.Install(MvMeta.Current)`(`S/MV.Boot/MvBootMeta.cs:89-90`) → `MvQuestBackend.Register(meta.Local, meta.IsOpen)` + `meta.Local.RegisterContributor(...)`(`MvQuest.cs:97-104`).

갱신 경로 두 가지:
1. **메타 요청에 얹기(저장 1번)**: 정산·로비 강화·Area 강화·부활·광고 요청이 성공하면 `MvLocalMetaBackend.Handle`이 contributor를 부르고(`MvLocalMetaBackend.cs:51-52`), contributor가 `EventsOf`로 카운터를 뽑아 현재 `mvQuest`를 롤·가산해 `response.Data["mvQuest"]`에 넣는다(`MvQuest.cs:54-64`). 기간만 바뀌었어도(Stale) 굴린 값을 같이 씀. 퀘스트 요청 자신과 이미 mvQuest가 있는 응답은 건너뜀(56-57).
2. **클라이언트 전용 값 기록**: 런 안 계단·벽(·제작)은 `MvQuest.RecordRun` → `Record` → `meta.questRecord {ev:[{t,c,v}]}`(136-141) → `MvQuestRules.Record`(유효 type 확인, DailyPoint는 거절) → `Data{mvQuest}`. 모드 클리어 `RecordModeClear`.

보상 받기: `MvQuest.Claim(id)` → `meta.questClaim` → `MvQuestRules.Claim`(310-371): 413(반복)/420(업적, 단계 순서 강제) 행 찾기 → 해금(quest/achievement) → 상태 Claimable 확인 → 보상 Wealth는 `MvWealthChange`, DailyQuestPoint는 카운터 DailyPoint로 → monthly는 `claims[id]+1`, 그 밖은 `got`에 추가 → `Data{mvQuest, wealthInfos}`.

### 4.5 연구 (MV.Content/Research)

- `MvResearch`(`S/MV.Content/Research/MvResearch.cs:14-72`): `Level/Run`(저장 읽기), `Start/Claim`(→ `MvMeta.StartResearch/ClaimResearch`), `ToModifiers`(MetaStat → `RunModifiers` 7필드, 39-57), `Modifiers(store, tables)`(67-71).
- 런 반영: `MvGameLoop.CreateSim`이 `ctx.MetaModifiers = MvResearch.Modifiers(store, tables)`(`MvGameLoop.cs:306`) → `MainRunRule.Build`가 `RunSetup.Modifiers`로 → `RunFormulas`의 `Modifiers.*` 항. 1차에선 연구가 메타 능력치의 유일한 출처(주석 12행).

### 4.6 저장 로드/세이브 흐름 (MV.Save)

**클래스**

| 클래스 | 파일 | 책임 |
|---|---|---|
| `MvSave` | `S/MV.Save/MvSave.cs:14-120` | 정적 진입점. `Current`, `Init`(49-54), `Retry`, `StartFresh`(60-65), `Reopen`, `OnApplicationPause`(71-76, dirty면 저장), `Shutdown`, `CurrentChanged` 이벤트, 시험용 `PreferencesOverride`/`UseMemoryPreferences`. |
| **`MvSaveStore`** | `S/MV.Save/MvSaveStore.cs:111-426` | 저장소. Framework `DataRepository`(컨테이너) + `LocalSaveRepository`(PlayerPrefs 두 칸) + `RepositoryStorageAdapter`(LocalAuthority) 위에 컨테이너 20개 등록(147-175), `Open`(210-316), `OpenFresh`(194-208), `ApplyAndSave`(351-369), `SaveIfDirty`(375-380), `GetWealth`, `ExportPayloadJson`, 개발 도구 `DevResetProgress`(394-401)·`DevImportProtoSave`(404-411). |
| `MvSaveOptions/OpenStatus/Notice/OpenResult` | `MvSaveStore.cs:12-104` | 열기 설정·결과(Opened/Created/RecoveredBackup/Corrupt/ProtectionFailed/UnsupportedVersion/MigrationFailed/GameMigrationFailed/StorageUnavailable → 화면 Notice). |
| `MvSaveContainer` | `S/MV.Save/MvSaveContainer.cs:17-85` | 컨테이너 부모(Framework `DataContainer`). 상태 = 정규화된 불변 JObject. `ParseState`(45-68): 병합(replace=false)은 받은 최상위 필드만 바꾸고 `null`은 필드 삭제(=기본값), 교체는 빠진 필드 기본값. 변경 키 = 달라진 최상위 필드. |
| 컨테이너 20종 | `S/MV.Save/MvSaveContainers.cs` | `MvProgressContainer`(level, area, cp, bars{"A{area}"}, runs, zoneClear, bestArea, lastSettledRunSeq), `MvLobbyContainer`(up, deck), `MvAreaUpContainer`(lv), `MvRebirthContainer`(lv, pending, perks, cdUntil), `MvGodContainer`(sel, gods), `MvUnlockContainer`(opened, seenUnlock, seenNew), `MvDailyContainer`(day, ads, modePlays, free), `MvPlayRes`, `MvMode`, `MvQuestContainer`(daily/monthly/achieve/attend), `MvResearchContainer`(lv, run), `MvShopContainer`, 2차 예약(Pet, Friend, Gear, Pass, Blessing — 빈 객체), `MvSettingsContainer`(muted, contentTab), `MvMetaContainer`(createdAt, lastSaveAt, importSource, tableVersion). 재화는 Framework `WealthContainer` 그대로. |
| `MvSaveKeys/MvSaveSchema/MvSaveTriggers` | `S/MV.Save/MvSaveSchema.cs` | dataKey 상수(`mvProgress`…), 스키마 `mv:local:v1`(v0 = 웹 프로토), ProfileId `p0`, 네임스페이스 `moonvillage.progress.v1`. |
| `MvSaveNormalize` | `S/MV.Save/MvSaveNormalize.cs` | 컨테이너별 정규화(관대한 읽기, 범위 clamp). |
| `MvSaveDefaults` | `S/MV.Save/MvSaveDefaults.cs:13-50` | 새 저장 기본값(재화 행 전부 0). |
| `MvSaveMigrator`/`MvSaveMigrationV0ToV1`/`MvProtoSave` | `S/MV.Save/MvSaveMigration.cs:27-130+` | 게임 스키마 이관 체인(Open 앞에서). v0→v1 = 프로토 평면 JSON 변환. |
| `MvSaveReferenceCatalog`/`MvSaveReferenceFix` | `S/MV.Save/MvSaveReferenceFix.cs:14-110+` | 표가 바뀐 뒤 저장 정리(없는 신 → startGodKey, 덱 길이, Area 상한, 칸 수, Lv 상한, 광고·모드·해금 key 정리, tableVersion). 바뀐 게 있으면 `ApplyAndSave` 1번. |
| `MvSaveConfig`/`MvSaveTime` | `S/MV.Save/MvSaveConfig.cs` | 기본값이 읽는 설정(Boot가 표로 채움; 코드 기본값은 표 없는 시험용 — 주석 9-10). |
| `MvWealthIds` | `S/MV.Save/MvWealthIds.cs:13-70` | 재화 id(1001001…) ↔ `WealthContainer` type 이름. `Mismatches`로 01_wealth와 대조. |
| `IMvSaveKeyProvider`/`MvDevSaveKeyProvider`/`MvSaveKeySource`/`MvPlayerPrefs`/`MvMemorySavePrefs` | `S/MV.Save/MvSaveKeyProvider.cs` | 암호화 키(개발 = 공개 고정 키, 출시 키 미정 D-68 → 출시 빌드는 `ProtectionFailed`), PlayerPrefs 접근. |
| `MvSaveJson`/`MvJsonRead` | `S/MV.Save/MvSaveJson.cs` | JSON 도우미. |

**로드 흐름 (부팅)**

1. `GameBootstrap` 5·6단계: `MvBootSave.Open(MvTables.Current)` → `MvSaveLifecycle.Ensure()` → `MvBootMeta.Attach(tables)`(`S/MV.Boot/GameBootstrap.cs:99-103`).
2. `MvBootSave.Open`(`S/MV.Boot/MvBootSave.cs:52-74`): `MvWealthIds.Mismatches(01_wealth)` 다르면 예외로 시작 중단 → `CurrentChanged += BindResources` → `MvSave.Init(new MvSaveOptions{ Config = CreateConfig(tables)(106-123: 00_config·300·08 값), References = CreateReferences(tables) })` → `PendingNotice` 기록.
3. `MvSave.Init` → `MvSaveStore.Open`(`MvSaveStore.cs:210-316`):
   1. config 검증, 시계, prefs(기본 PlayerPrefs).
   2. 키 공급 → `LocalSaveCodec`(암호화). 실패면 `ProtectionFailed`(219-222).
   3. `LocalSaveRepository.Load()`(224-225). 한 칸 손상이면 `LoadRecovery` → 깨진 칸 삭제 후 재로드 → `RecoveredBackup`(228-245).
   4. 상태별 실패 처리(247-258).
   5. 스키마가 현재(v1)가 아니면 `MvSaveMigrator.Run` → 변환본을 바로 저장(260-283).
   6. `new MvSaveStore`(컨테이너 등록) → `RepositoryStorageAdapter.Open(Repository, files, schema, LocalAuthority, 기본값 생성기)`(285-297) — 여기서 컨테이너에 값이 채워진다(없으면 `MvSaveDefaults.Create`).
   7. 참조 정리 `MvSaveReferenceFix.Run`(299-310).
   8. `MvSaveOpenResult(Opened/Created/RecoveredBackup)`.
4. `MvSave.SetCurrent` → `CurrentChanged(store)` → `MvBootSave.BindResources`: `new ResourceService(store.Repository, 01_wealth, ConsumeTypeId(1))`(94-103) → `MvBootMeta.OnSaveChanged`: `MvMeta.Current = new MvMeta(() => MvSave.Current, () => MvBootSave.Resources, tables)` + `MvQuest.Install`(`MvBootMeta.cs:86-91`). (구독 순서는 등록 순서: BootSave가 먼저 등록.)
5. 열지 못하면(복구·업데이트·저장소 팝업) `MvMeta.Current = null` 상태로 로비까지 가고, 팝업의 다시 시도/새로 시작이 `MvSave.Retry/StartFresh`로 열면 그때 `CurrentChanged`로 생성. `MvMeta`는 저장소·재화 서비스를 공급 함수로 매번 읽어 재오픈에도 같은 인스턴스를 쓴다(`MvMeta.cs:44-62`).

**세이브 흐름**

1. 사건마다: `MvMeta.Send` → `MvSaveStore.ApplyAndSave(data, keys)`(`MvSaveStore.cs:357-369`): data 복제, 대상 키에 `mvMeta` 자동 추가 + `lastSaveAt = now` → `RepositoryStorageAdapter.ApplyAndSave(payload, replace, targets)` (Framework: 저장 확인 → 메모리 반영 → 알림; 하나라도 해석 실패하면 아무것도 안 바뀜, 주석 354-355).
2. 각 컨테이너 `ParseState`가 병합·정규화 후 바뀐 최상위 키를 보고(`MvSaveContainer.cs:45-68`).
3. 쓰기 실패(`LocalSaveWriteException`·`RepositoryRecoveryRequiredException`)면 `RequiresReopen` → `MvMeta.IsReady=false`, 화면이 `MvSave.Reopen`.
4. 일시정지·종료: `MvSaveLifecycle.OnApplicationPause/Quit` → `MvSave.OnApplicationPause(true)` → `SaveIfDirty`(dirty일 때만 `mvMeta`만 저장)(`S/MV.Boot/MvSaveLifecycle.cs:24-26`, `MvSave.cs:71-76`, `MvSaveStore.cs:375-380`).
5. 영속 쓰기는 MV.Meta·MV.Save 밖에서 금지(시험 `MvMetaEntryPointRuleTests`).

---

## 5. 표(MV.Data) 값 vs 코드에 박힌 값

### 5.1 표에서 읽는 것 (대표)

- 런 상수 전부 `300_runConfig`(`RunParams.From` 27-63: DtCap, HitStopScale, LANE_X_MAX, RUN_SPEED(_MID), STUCK_END_SEC, WEAPON_STACK_MAX, MAX_CLONES, CLONE_X_MAX, PROJ_SPEED, DMG_MIN/MAX, WELL_*, CP_MAX, PLAYABLE_AREAS, MERGE_N, BURN_TICK, STORM_*, METEOR_*, FIRE_PHASE …), 계단 연출 `330_climb`, 흔들림 `"<prefix>.amp/.time"` 키.
- 오브젝트 동작 102·105~109·114, 템플릿 310~314, 필드 303·313, Area 302(hp, sgateM, startGold, needSouls, zoneId, floorGroup), 난이도 06, 무기 103, 카드 110~112·115, 신 113, 드롭 801, 강화 10·11·12·13·14·16, 광고 08, 해금 05, 콘텐츠 400, 퀘스트 410·413·420, 연구 440, 재화 01, 설정 00_config(startGodKey, startDeck, rebirthArmSec, rebirthCooldownSec, adDayUtcOffsetHour, dataVersion).
- 배열 크기도 표에서: `W` 길이 = `WeaponTierMax+1`, `FireT` 길이 = 1+`CloneCap`, 덱 길이 = 103 tier 최대+1, Floor 수 = `FloorCount()`, 동행 최대 = `MaxCompanions()`.

### 5.2 코드에 남아 있는 상수·문자열 (문서화·점검 후보)

| 위치 | 값 | 비고 |
|---|---|---|
| `RunSim.Rules.cs:251` | `Clamp(P.X − o.X, −1, 1)` | Chase x 추종 방향 정규화(속도는 표 xFollow) |
| `RunSim.Rules.cs:333, 513, 622` | `1e9` | 최소값 탐색 초기값 |
| `RunParams.cs:49` | `"STORM_K0"` 문자열 | 다른 키는 `RunConfigKeys` 상수인데 이것만 리터럴 |
| `RunParams.cs:59`, `RunFormulas.cs:151` | `g.key == "clone"` | 314_spawnGroup 분신 행 식별 key |
| `RunSim.Rules.cs:122, 553, 821, 829, 837` | `"shake.blocked"`, `"shake.godBlast"`, `"shake.climbLap"`, `"shake.climbTier"`, `"shake.warpStage"` | 300 키 접두 |
| `RunSim.Rules.cs:775, 888` | `"run.toast.noStairs"`, `"run.toast.noCard"` | 문구 키 |
| `RunLevelBuilder.cs:14` | `FieldGenKey = "default"` | 313 행 key |
| `RunLevelBuilder.cs:264` | `CardPool.ByKey("main")` | 111 기본 풀 |
| `FieldGen.cs:20-22` | 127.1, 311.7, 43758.5453 | 의도적(주석 15: 바꾸면 모든 배치가 바뀜) |
| `RunRng.cs:61, 94-96` | PCG 기본 stream, 줄기 번호 | 의도적 고정 |
| `MvRunStart.cs:46-50` | `"rescueCat"`, `"stair"`, `"weapon"`, `"income"` | 재화 type·로비 강화 key 리터럴 |
| `MvMetaRules.cs:25-30` | `LobbyStair="stair"`, `LobbyWeapon="weapon"`, `LobbyIncome="income"`, `RebirthKey="rebirth"`, `CollectAdKey="collect3"` | 표 key 상수 |
| `MvMeta.cs:160-161`, `MvLocalMetaBackend.cs:178` | `"interstitial"`, 대체 placement `"result_interstitial"` | 08 key |
| `MvLocalMetaBackend.cs:334-337` | `god.unlockType` 0/1/그 밖 switch | 숫자 분기 |
| `MvLocalMetaBackend.cs:477` | `DebugRebirthPending = 10` | 개발 패널 전용(정본 rbOpen) |
| `MvResearchRules.cs:45`, `MvQuestRules.cs:75-76` | `"research"`, `"quest"`, `"achievement"` | 05 key |
| `MvSaveNormalize.cs:16-18` | LobbyUpKeys/AreaUpKeys/PerkKeys 고정 key 배열 | 저장 정규화의 고정 필드 |
| `MvSaveConfig.cs:15-27` | StartDeck, "thunder", CpMax 2, FloorCount 3, 광고 3/4 | 표 없는 시험용 기본값(게임은 Boot가 표로 덮음) |
| `MvSaveMigration.cs:116-117` | `ProtoDefAdsDia=3`, `ProtoDefAdsSkip=4` | 프로토 가져오기 환산 |
| `MvWealthIds.cs:15-41` | 재화 id·type 대응표 | Boot가 01_wealth와 대조해 다르면 시작 중단 |
| `RunFormulas.PerkLv` `RunFormulas.cs:97` | GoldCap 미반영 | 효과 뜻 [미확인] |

---

## 6. 시험 폴더에서 읽은 의도 (간단히)

- `S/MV.Run.Tests/{Gen, Rule, Sim, ViewApi}` — 생성·규칙·시뮬·뷰 API별 시험(골든 해시·덤프 대조 구조가 `FieldGen.HashLog`, `ReplayRng`, `RunEvent.ToString`에 반영됨).
- `S/MV.Meta.Tests/` — Settle·Growth·RebirthAd·Research·Debug·UnlockRules + 진입점 규칙 시험.
- `S/MV.Save.Tests/` — Corruption·Defaults·Migration·ReferenceFix·RoundTrip.
- `S/MV.Content.Tests/{Quest, Research}`.
