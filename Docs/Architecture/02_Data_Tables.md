# 02. 표 데이터 (MV.Data · TSV → Resources · 드롭)

기준 커밋: `dev` 046c8be6 (2026-11-15). 경로는 저장소 루트 기준이고 `파일:줄`은 그 커밋의 줄 번호다. Framework 줄 번호는 `Library/PackageCache` 풀린 본 기준이라 참고용이다. 전체 지도는 [README.md](README.md).
경로 약어: `Scripts/` = `Assets/Game/Scripts/`. Framework 표 패키지는 `com.jjevol.tables` 0.1.0-pre.3, `com.jjevol.tables.authoring` 0.1.0-pre.11.

---

## 0. 한눈에

```
[시트] --MvSheetTsvExporter--> GameData/Table/TSV/NNN_name.txt  (정본, 사람이 고침)
                                   |
             JJEvol/Tables/Table Builder (Framework TableBuilder)
               └ TableAuthoringOptions.ValidateTables = MvTableAuthoring.ValidateBuildInputs
                     └ MvTableValidator.ValidateAuthoringTsv + MvResourceColumnChecker  (오류면 반영 거부)
                                   v
             GameData/Table/Resources/NNN_name.txt  (빌드 산출물, 평문·BOM, 암호화 끔)
                                   |
 부팅 4단계: GameBootstrap → MvBootMeta.LoadTablesAsync → MvTableLoader.LoadAsync
     GameTableSource.Configure(PackagedTableSource(false))  → Resources.Load<TextAsset>(tableName)
     MvTables.RegisterAll → TableRepository.LoadAllAsync → MvTable<T>.InsertData → Reindex(key·group·sorted 사전)
     MvTableValidator.ValidateRuntime → (오류면 MvTableValidationException) → BuildIndexes(ZOrder, MvDropRoller)
     MvTables.Current = tables   ← 런타임 모든 모듈이 읽는 단일 진입점
```

- 어셈블리 `MV.Data` (`Scripts/MV.Data/MV.Data.asmdef`): 참조 `MV.Core`, `JJEvol.Tables.Runtime`, `JJEvol.Data.Runtime`, `JJEvol.Economy.Runtime`, `UniTask`. MV.Run·MV.UI 등을 모른다(105 컴포넌트 구현 확인은 옵션 콜백으로 주입받음).
- 네임스페이스 `MV.Data`. 파일 26개, 약 7,000줄.

---

## 1. 폴더 구조 (Scripts/MV.Data)

| 폴더 | 파일 | 역할 |
|---|---|---|
| (루트) | `MvTables.cs`, `MvTableLoader.cs` | 표 묶음(레지스트리)·부팅 로더 |
| `Core/` | `MvTable.cs`, `MvSchema.cs`, `MvTableSources.cs`, `ConsumeData.cs`, `ConsumeType.cs`, `RunConfigKeys.cs`, `ContentConfigKeys.cs`, `MvLevelPriceGenerator.cs` | 표 기반 클래스·열 정의·원본·보상 항목·키 상수·가격 생성기 |
| `Tables/` | `Band0Tables.cs`, `Band1Tables.cs`, `Band1xxTables.cs`, `Band3xxTables.cs`, `Band4xxContentTables.cs`, `Band4xxMetaTables.cs`, `Band7xxTables.cs`, `Band8xxTables.cs`, `CharLookResolver.cs` | 표 번호대별 행 타입(+전용 표 클래스) |
| `Loot/` | `MvDropRoller.cs` | 801·802 추첨·확률표·그래프 검사 |
| `Validation/` | `MvTableValidator.cs` + partial 4개(`.Semantics`, `.Content`, `.Meta`, `.Modes`), `MvValidationReport.cs` | 게임 표 검증기·보고서·옵션 |

---

## 2. 핵심 클래스

### 2.1 `MvTables` — 표 레지스트리 (`Scripts/MV.Data/MvTables.cs`)
- 종류: `public sealed class`. 모든 표 인스턴스를 속성으로 들고 있는 묶음. 런타임은 정적 `MvTables.Current`(:16)만 본다.
- 속성 73개(표 72개 + `Drops`): 번호대 주석대로 0(:19~29), 1(:31~37), 1xx(:39~54), 3xx(:56~69), 4xx 콘텐츠(:71~79), 4xx 메타(:81~93), 7xx(:95~98), 8xx(:100~104). 대부분 `MvTable<TRow>(Schema, parse, group?)`, 일부는 전용 서브클래스(아래 2.5).
- 생성자(:108~125): `_all` 리스트(등록 순서 = 로드 순서)와 `_byName`(표 파일 이름 → `IMvTable`, Ordinal) 사전을 만든다. 같은 이름이면 `Dictionary.Add`가 예외.
- 주요 메서드
  - `All` (:128) — 등록 순서 표 목록(검증기·로더가 순회).
  - `Find(string tableName)` (:131) — `105_objectKind` 같은 파일 이름으로 표 찾기. 검증기의 FK 해석에 씀.
  - `RegisterAll()` (:134) — 모든 표를 Framework `TableRepository.Register`.
  - `LoadAll(ITableSource)` (:140) — 저장소를 거치지 않고 지정 원본에서 동기 로드(시험·에디터 검증·핫 리로드용).
  - `BuildIndexes()` (:147) — 검증 뒤 교차 캐시: `TemplateObject.BuildZOrder()` + `Drops = new MvDropRoller(this)`.
  - `Drops` (:154) — `IMvDropSource`(801·802 추첨기). BuildIndexes가 만든다.
  - 계산형 값(「행 수·합계로 계산되는 값은 표에 적지 않는다」 규칙 구현):
    `ZoneIndexOfArea(areaNo)` (:157, 302.zoneId가 301 sort 순 몇 번째), `ZoneFirstArea/ZoneLastArea(zoneId)` (:169/:178, 302 areaNo 최소/최대),
    `FloorCount()` (:190, 302가 쓰는 303 묶음의 floor 최댓값), `StairMaxLv()` (:201, 11 lv 최댓값),
    `LevelMax(ILevelPriced)` (:213, 계단=Σ11.need, 지팡이=Σ12.need+`WEAPON_EXTRA_BUYS`, 그 밖=표 max), `IsComputedMax` (:235),
    `LevelPriceMaxLevel(group)` (:239), `AttendanceCycleDays()` (:247), `GearMaxLevel()` (:255, 499 gear.maxUpgrade),
    `MaxCompanions()` (:258, 499 pet.slots + friend.slotsMax), `WeaponTierMax()` (:264, 103 active tier 최댓값).
- 호출자: `MvTableLoader`(생성·로드·Current 대입), `MvTableValidator.ValidateAuthoring`(새 인스턴스), `MvTableAuthoring.Validate`·`MvResourceColumnChecker.CheckProject`(에디터), 거의 모든 게임 모듈(6절).

### 2.2 `MvTableLoader` — 부팅 로더 (`Scripts/MV.Data/MvTableLoader.cs`)
- 종류: `public static class`.
- `TsvDirectory` (:17) — `Assets/Game/GameData/Table/TSV` 절대 경로. 런타임 경로에서는 쓰지 않음(사용처: 에디터 `MvResourceColumnChecker.cs:120`, 시험 다수).
- `LastReport` (:20) — 마지막 검증 보고(경고 포함). 개발 화면·시험이 읽음.
- `ResetStatics()` (:22~27) — `SubsystemRegistration`에서 `LastReport`·`MvTables.Current`를 null (Domain Reload 꺼짐 대비).
- `DefaultSource()` (:33) — `new PackagedTableSource(useEncrypt: false)`. **Resources 산출물만** 읽는다. 주석이 저장소 규칙(「앞으로 대체 경로로 읽지말고 Resources 산출물을 읽어」)을 그대로 인용. 에디터 분기 없음.
- `LoadAsync(source, options, onProgress)` (:35~59) — 4.1 흐름 참조.
- 호출자: `MV.Boot/MvBootMeta.cs:50~51` (`LoadTablesAsync`) 하나뿐(시험 제외).

### 2.3 `IMvTable` / `MvRow` / `MvTable<T>` (`Scripts/MV.Data/Core/MvTable.cs`)
- `IMvTable` (:9~19) — 검증기가 보는 공통 면: `TableName`, `Schema`, `RawRows`(마지막 성공 로드의 원문 `TSVRow` 목록), `Table`(Framework `TableBase`), `HasId/HasGroup/HasKey`.
- `MvRow : TableData` (:25~70) — 모든 행의 기반. Framework `TableData`가 `id·sort·grade·nameKey`를 읽고(`TableData.cs:33~66`), 여기서 `key`, `active`(빈칸이면 1)를 읽음. 보호 도우미 `I/L/D/S/Bool/E/Ints/Doubles/Strs/Consume`.
  - `Bool` (:48) — bool01은 `GetInt != 0` (Framework `GetBoolean`이 `1`을 못 읽어서).
  - `Consume(r, prefix)` (:62~69) — `{p}Type/Id/Value/ValuePerZone/ValueRule` 묶음을 `ConsumeData.TryParse`로 읽되 **형식 오류면 빈 배열로 둔다**(로드는 실패시키지 않고 검증기가 원문으로 오류를 냄).
  - 행 속성은 모두 `{ get; private set; }` → 로드 뒤 외부에서 바꿀 수 없음(사실상 불변).
- `MvTable<T> : IndexedTable<T>, IMvTable` (:80~156) — id 표.
  - 생성자 `(MvSchema schema, Func<TSVRow,T> parse, Func<T,int> group = null)` (:93).
  - `InsertData` (:102) → base(id 사전 채움) → `_raw.Set(datas)` → `Reindex()`.
  - `Reindex` (:123~140): `_sorted`(sort 오름차순, 같으면 id), `_byKey`(id 오름차순에서 첫 key만, Ordinal), `_groups`(group 함수가 있을 때 group → id 순 리스트).
  - `RestoreData` (:117) — 다시 읽기가 실패하면 Framework가 이전 사전을 되돌리고 여기서 재색인.
  - 조회 API: `Get(id)`/`Dict`/`Datas`(Framework `IndexedTable.cs:94~101`), `Sorted` (:143), `ByKey(key)` (:145), `GetGroup(group)` (:148, 없으면 빈 배열), `Groups` (:151), `HasId/HasGroup/HasKey` (:153~155).
- `ConsumeTypeData`/`ConsumeTypeTable : KeyedTable<int, ConsumeTypeData>` (:164~237) — `07_consumeType`은 id 열이 없고 `typeId`가 키. `Get(ConsumeType)` (:233). 스키마는 생성자 안(:218~221).
- `MvWealthTable : WealthTable` (:242~266) — Framework Economy의 `WealthTable`에 원문 보관만 더함. `MvBootSave`가 이 객체를 재화 정의로 넘긴다.
- `MvAtlas` (:269~283) — 리소스 열 기본 아틀라스 상수(`@theme`, `Atlas_UI_Icon`, `address` 등).

### 2.4 `MvSchema` / `MvColumn` / `MvColKind` (`Scripts/MV.Data/Core/MvSchema.cs`)
- `MvColKind` (:7~38): `Int, Long, Float, Bool01, Str, Key, Enum, Color, ColorList, IntList, FloatList, StrList, Fk, FkList, FkGroup, FkKey, Sound, Effect, Res, RunConfigKey`.
- `MvSchema` (:65~150) — 표 1개의 열 정의(플루언트 빌더). 각 행 클래스가 `public static readonly MvSchema Schema`로 선언.
  - 속성: `Table`, `Number`(파일 이름 `_` 앞), `IdColumn`, `FreeIds`, `SingleRow`, `GroupColumn`, `AllowRunConsume`, `KeyPerGroup`, `Columns`, `Consumes`, `IdPrefix`.
  - 빌더: `Id/FreeIdRule/OneRow/KeyUniqueInGroup/RunConsume/Group/Int/Long/Float/Bool/Str/Key/Enum<T>/Color/Colors/Ints/Floats/Strs/Fk/FkList/FkGroup/FkKey/Sound/Effect/Res/ConfigKey/Consume(prefix, runExtension, range)`.
  - **스키마는 파싱에 쓰이지 않는다.** 파싱은 각 행 `Init`이 직접 하고, 스키마는 검증기(형식·FK·리소스)와 시험이 쓴다. 따라서 열을 더하면 `Init`과 `Schema` 둘 다 고쳐야 한다.

### 2.5 전용 표 클래스 (조회 API가 추가된 것)
| 클래스 | 위치 | 추가 API |
|---|---|---|
| `ConfigTable` | `Tables/Band0Tables.cs:43` | `Value` — 00_config 단일 행 |
| `LevelPriceTable` | `Tables/Band1Tables.cs:222` | `Get(group, targetLevel)` (:227) — 다음 구매 비용 행, 없으면 null(MAX) |
| `RunConfigTable` | `Tables/Band3xxTables.cs:37` | `Has/Raw/D/I/List(key)` — 300 키-값. `I`는 반올림(:47). 없는 키는 `KeyNotFoundException` |
| `TemplateObjectTable` | `Tables/Band3xxTables.cs:356` | `InZOrder(templateId)` — BuildIndexes가 만든 z·seq 정렬 캐시 |
| `TemplateCycleTable` | `Tables/Band3xxTables.cs:415` | `TemplateFor(cycleGroup, level)` — Level → 310 id (Direct/Loop) |
| `ClimbTable` | `Tables/Band3xxTables.cs:679` | `Config(key="default")`, `WarpStages()` ; `ClimbData[column]` 인덱서로 설정 열 읽기 |
| `ModeStageTable` | `Tables/Band4xxContentTables.cs:152` | `Get(group, stage)`, `LastStage(group)` |
| `ContentConfigTable` | `Tables/Band4xxContentTables.cs:424` | `Has/Raw/D/I/B/Consume(key)` — 499 키-값(묶음 이름 행 id%1000==0 제외) |
| `ResearchTable` | `Tables/Band4xxMetaTables.cs:124` | `Get(group, level)` |

- 키 상수: `RunConfigKeys` (`Core/RunConfigKeys.cs`, 생성 파일 표시, `All` 배열 :211) · `ContentConfigKeys` (`Core/ContentConfigKeys.cs`, `All` :39, 29개). 코드는 이 상수로만 키를 쓰고, 검증기가 TSV와 양방향 대조(누락·남음 = 오류).
- `CharLookResolver.Resolve(rows, catId, slot, tier)` (`Tables/CharLookResolver.cs:12`) — 116_charLook에서 외형 행 선택(순수 함수). 호출자 `MV.View/Character/CharacterPresenter.cs:187,193`.
- `ILevelPriced` (`Tables/Band1Tables.cs:15`) — `id/key/max/levelPriceGroup`. 10·13·14 행이 구현, `MvTables.LevelMax`·검증기가 씀.

### 2.6 `ConsumeData` / `ConsumeType` / `RewardValueRule`
- `ConsumeData` (`Core/ConsumeData.cs:11`) — `readonly struct` 보상·비용 1항목: `Type, Id, Value(double), PerZone, Rule`.
  - `ValueAtZone(zoneIndex)` (:32) = `Value + PerZone*zoneIndex`.
  - `Import(TSVRow, prefix)` (:39) — 형식 오류면 `FormatException`.
  - `TryParse(types, ids, values, perZones, rules, out list, out error)` (:49~140) — `;`로 나누고, Type·Id가 Value보다 짧으면 마지막 값을 반복(정크버스터즈 `ImportDatas` 규칙). 검증기와 `MvRow.Consume`이 같은 함수를 쓴다.
  - `TryParseType` (:143) — 숫자(`602`) 또는 enum 이름(대소문자 무시).
- `ConsumeType` enum (`Core/ConsumeType.cs:10~38`) — 값 = `07_consumeType.typeId`, 이름 = `codeName`. 1 Wealth, 3 PlayResource, 5 Item, 8 RandomBox, 50 Drop, 100 AD, 101 Inapp, 300/301 퀘스트 포인트, 500 Pass, 600~609 런 전용(RunGold 602, RunSouls 603, RunDia 604 …), 610 Gear, 620 PetCard, 621 FriendCoin, 622 GodShard, 630 Blessing.
  - 손으로 쓴 enum. 에디터 `MvConsumeTypeCodegen`(Framework 생성기)으로 이관 예정이지만 고정본 0.1.0-pre.7 기준 이관 경로가 막혀 있다고 주석에 기록(:6~9).
- `RewardValueRule` (:41) — `Flat, DiaCap, HalfDiaCapCeil`. DiaCap 계열은 표 값 0, 호출자가 14_rebirthPerk로 수량 계산.

### 2.7 원본(ITableSource) 구현 (`Core/MvTableSources.cs`)
- `MvTsvDirectorySource(directory, fallback)` (:14) — 폴더의 `NNN_name.txt`를 평문 TSV로 읽음. `ReadHeaders`(:44)로 첫 줄(열 이름) 제공. **사용처는 에디터·시험뿐**(TableBuilder 입력에 없는 표 보충, `MvResourceColumnChecker`, `*TestKit`).
- `MvMemoryTableSource(fallback)` (:55) — 메모리 TSV. 작성 검증(TableBuilder 입력)과 일부러 깬 표 시험이 씀. `Set(name, text)`, `TryGetText`.
- 런타임 원본은 Framework `PackagedTableSource` 하나(아래 3절).

### 2.8 `MvLevelPriceGenerator` (`Core/MvLevelPriceGenerator.cs`)
- 정적 클래스. `16_levelPrice` 행을 공식으로 만드는 **생성/대조 도구**. 런타임 가격 조회는 표(`LevelPriceTable.Get`)이고, 이 생성기는 검증기가 표와 비교해 다르면 **경고만** 낸다(손 조정 허용).
- `Prices(formula, base, growth, max)` (:20) — `Pow`: `round(base*growth^(lv-1))`, `LinearNext`: `round(base+growth*lv)`; `JsRound`(:17)=floor(x+0.5).
- `Generate(MvTables)` (:49) — 10(골드)·13(다이아)·14(토큰) 묶음.
- `GenerateContent(MvTables)` (:102) — 08 메타 묶음(펫 301, 친구 302, 신 레벨 304, 장비 306~313). 일부 계수(`s_godShard`, `s_gearUpgradeGold`, `PetPiece`, `PetFoodPrice`)가 코드 배열에 있음 — 주석상 「08 문장 그대로인 생성 도구 입력이고 정본은 16 행」.
- `RowId(group, level)` (:65) = `16000000 + group*1000 + level`.
- 호출자: 검증기 `CheckLevelPrices`(Semantics) · `CheckContentLevelPrices`(Content), 시험.

### 2.9 추첨: `IMvDropSource` / `MvDropRoller` / `MvDropGraph` (`Loot/MvDropRoller.cs`)
- `MvDropResult` (:8) — `Type, Id, Value(long), Rule, RowId`. `MvDropOdds` (:30) — 확률 표시 1줄(`Probability`는 기대 등장 횟수).
- `IMvDropSource` (:58~66): `RollDrop(dropGroup, next, results, times)`, `OpenBox(boxGroup, next, results, times)`, `DropOdds(dropGroup)`, `BoxOdds(boxGroup)`. 난수는 호출자가 `Func<double>`(균등 [0,1))로 주입.
- `MvDropRoller` (:74~227) — D-85 임시 구현. Framework `com.jjevol.economy.drop` 고정본이 들어오면 같은 인터페이스 어댑터 `MvDropSource`로 교체 예정(주석 :70, `MvTables.cs:153`).
  - 생성자(:81) — `MvDropGraph.FindGraphErrors`로 순환·깊이 초과가 있으면 `InvalidOperationException`.
  - 선택 규칙 `Pick`(:154): weight 합 total, **id 오름차순** 누적, `u < cum/total`인 첫 행. 수량 `Quantity`(:144): min<max일 때만 난수 하나 더.
  - `Emit`(:127): 꽝(rewardType 0) 무시; 결과가 `Drop(50)`이면 그 묶음을 수량만큼 재귀 추첨; `RandomBox(8)`는 **열지 않고 상자로 지급**(`expandBox:false` 고정); 그 밖은 결과에 추가. Rule이 Flat이 아니면 Value=0.
  - `OpenBoxOnce`(:115): 802 `subGroup`(칸)마다 1행씩 뽑아 모두 지급.
  - `MaxDepth` = `MvDropGraph.MaxDepth` = 5 (:232).
- `MvDropGraph.FindGraphErrors(MvTables)` (:251) — 801/802 묶음 그래프 DFS(순환·깊이>5). 검증기 `CheckLoot`와 로더 생성자가 공유.
- 803·804(보장/천장)는 행 타입·검증만 있고 **런타임 로직은 아직 없다**(Guarantee 사용처 grep 결과 MV.Data 검증기뿐). 현재 TSV 데이터도 0행.

### 2.10 검증: `MvTableValidator` (partial) / `MvValidationReport`
- `MvValidationReport` (`Validation/MvValidationReport.cs:39`) — `Issues`, `HasError`, `ErrorCount`, `WarningCount`, `Error(...)`, `Warn(...)`, `Summary(maxLines)`. 문구 형식 `[표] id=값 열=이름: 내용`(`MvIssue.ToString` :28).
- `MvTableValidationException` (:97) — `Report`를 들고 부팅을 멈춘다.
- `IMvResourceCatalog` (:104) — 아틀라스/주소 카탈로그 인터페이스(에디터 쪽이 제공).
- `MvValidationOptions` (:112) — `Headers`(원문 첫 줄 공급), `Resources`(카탈로그), `Localization`(`ITableLocalization`), `IsComponentImplemented`(105 컴포넌트 구현 확인 콜백, MV.Run이 주입).
- `MvTableValidator` 입구 (`Validation/MvTableValidator.cs`)
  - `ValidateAuthoring(tableTexts, options, fallback)` (:38) — 원문으로 **새** `MvTables`를 채워(`TryLoad` :59) 검사. 헤더 공급자를 원문/TSV 폴더에서 자동 구성.
  - `ValidateAuthoringTsv(tableTexts, tsvDirectory)` (:55) — Editor 어셈블리용 입구(fallback = `MvTsvDirectorySource`).
  - `ValidateRuntime(tables, options)` (:84) — 이미 로드된 표 검사(부팅).
  - 공통 `Validate` (:92~101) 순서:
    1. `CheckFormat` (:114) 표마다 — 중복 열, `#` 주석 열(경고), 모르는 열(경고), 사람용 `name` 열 위치(키 열 바로 다음, 헤더가 있을 때만), 필수 열/값, 단일 행, id 중복, id 접두 = 표 번호, `id/1,000,000 == 표 번호`, 공백, 타입(`TypeError`), key 중복(그룹 범위 옵션), consume 묶음 형식(`CheckConsumeFormat` :249).
    2. `CheckReferences` (:263) 표마다 — Fk/FkGroup/FkList/FkKey 대상 존재, Sound(`_n` 변형 소리 연속성 `CheckSoundKey`), Effect key, RunConfigKey(`.amp/.time` 접두 포함), Res(카탈로그 있을 때만), consume 항목 `CheckConsumeRef`(:414: 07 존재·active·600번대 런 전용 제한·idTable/idKind 대상 존재), 04_effect children/part 규칙.
    3. `CheckConsumeTypeTable` (:438) — 07 표 ↔ C# `ConsumeType` enum 1:1 (값·이름), idKind 규칙, settleTo 형식.
    4. `CheckRunConfigKeys` (:468) — 300 ↔ `RunConfigKeys.All` 양방향, unit별 값 형식(color/address/숫자 목록).
    5. `CheckSemantics` (`MvTableValidator.Semantics.cs:13~37`) — 게임 의미 검사 20여 개: BalancePreset, World(301/302/304), AreaFloor, Templates, TemplateCycle, SpawnGroup, ObjectKind(+`IsComponentImplemented`), Components, Cards, LevelPrices(생성기 대조, 경고), AdPlacement(필수 key `dia/skip/collect3/interstitial/modeTicket` :11), ContentOpen, UiResource, ValueFormats, PairValues, `CheckContentData`(.Content.cs), `CheckMetaSystems`(.Meta.cs: 펫·연구·친구·룬·장비·축복·출석·로드맵·신 레벨, 16 묶음 빈틈), `CheckModesAndShop`(.Modes.cs: 400/401/402, 퀘스트 410~420, 499 키 대조, 7xx 상점), CharLook, StageObject.
    6. `CheckLoot` (`Semantics.cs:471`) — 801/802: 묶음 번호 = 첫 행 id, weight 합 0이면 오류·10000이 아니면 경고, 꽝 행 규칙, Rule≠Flat이면 min/max 0, `0<min≤max`, consume 참조; `MvDropGraph` 오류; 803/804 countType·rewardMode·칸 존재.
    7. `CheckNames` (:495) — `options.Localization`이 준비됐을 때만: active 행마다 `name:{id}` 문구 존재(경고). **현재 부팅은 Localization을 넘기지 않아 실행되지 않는다.**
  - 오류 = 로드/반영 실패, 경고 = 로그.

---

## 3. 표 빌드 파이프라인 (TSV → Resources)

### 3.1 파일 위치
- 정본(사람이 고침): `Assets/Game/GameData/Table/TSV/NNN_name.txt` — 72개 + `Report/res_unextracted.tsv`(리소스 미추출 목록).
- 산출물(런타임이 읽음): `Assets/Game/GameData/Table/Resources/NNN_name.txt` — 같은 72개 이름 집합(확인: `ls` diff 동일). 산출물은 UTF-8 BOM이 붙은 평문 TSV(암호화 끔)라 바이트는 TSV와 다르다(예: `801_drop.txt` 첫 바이트 BOM).
- 시트 원본: Google Sheets(링크는 `ProjectSettings/JJEvolTableAuthoringSettings.json`). Framework 관리 기록 `ProjectSettings/JJEvolTableAuthoring/*.json`(통합 Writer 영역).

### 3.2 단계
1. **시트 → TSV**: `MvSheetTsvExporter.Convert` (`Scripts/MV.Editor/Tables/MvTableAuthoring.cs:105~131`), 메뉴 `MV/Tables/Export Sheet TSV…` (:133). 시트 머리 2줄(설명·형식) 제거, `#` 열 제거, 맨 앞 id 입력 열 `table/category/index` 제거, LF·BOM 없음. 출력은 TSV 폴더(「직접 Resources 복사 금지」 주석 :103).
2. **검증 콜백 등록**: `MvTableAuthoring` (`[InitializeOnLoad]`, :17~34) — `TableAuthoringOptions.ValidateTables = ValidateBuildInputs`, `ValidationVersion = "mv-data-1"`, `UseEncryption = false`, `EncryptBytes = null`.
3. **TableBuilder 실행**: Framework 메뉴 `JJEvol/Tables/Table Builder` (`com.jjevol.tables.authoring/Editor/CreateTable.cs:14`). `TableBuildPlan`이 `request.Validator ?? TableAuthoringOptions.ValidateTables`를 부른다(`TableBuildPlan.cs:105`). 검증기 미등록이면 창에 경고(`CreateTable.cs:234`).
4. **게임 검증**: `MvTableAuthoring.ValidateBuildInputs(inputs)` (:39) → `Validate(texts)` (:54):
   - `MvTableValidator.ValidateAuthoringTsv(texts, TSV 폴더)` — 빌드 입력에 없는 표는 TSV 폴더에서 보충해 전체 교차 검사.
   - 오류가 없으면 `MvResourceColumnChecker.Check(...)`(아트 카탈로그·임시 목록·파일 이름)로 리소스 열 확인 → `MergeResourceIssues`(:74): 리소스 Error는 오류, 미추출 등은 경고.
   - 오류면 `Debug.LogError` + `InvalidDataException` → **반영 거부**(:42~46).
5. **산출**: TableBuilder가 `Resources/NNN_name.txt`(평문)를 쓴다. 출력 폴더는 `MvTableAuthoring.OutputAssetPath` 상수(:24) = `Assets/Game/GameData/Table/Resources`.
- 수동 검증 메뉴: `MV/Tables/Validate TSV` (:91) — TSV 폴더 전체 검사.
- ConsumeType 코드 생성: `MvConsumeTypeCodegen` (`Scripts/MV.Editor/Tables/MvConsumeTypeCodegen.cs:27`) — 메뉴 「계획 보기」(:65)/「적용」(:80). `Plan()`(:40)이 07 원문을 게임 검증 → `TableBuilder.Plan(request with consumeTypes)`. 출력 예정 `Scripts/MV.Data/Core/Generated/`. 손 enum과 충돌해 이관은 막혀 있음(주석 :21~23).

### 3.3 Framework 표 런타임 (짧게)
- `ITableSource` — `HasTable/Load/LoadAsync/RequiresRemoteUpdate`.
- `GameTableSource` (`Runtime/Tables/GameTableSource.cs`) — 정적 현재 원본. `Configure(source)`(:29), 기본값 `new PackagedTableSource()`(암호화 켬, :27). `SubsystemRegistration`에서 null로 리셋.
- `PackagedTableSource(useEncrypt)` (`Runtime/Tables/PackagedTableSource.cs:7~16`) — `HasTable = Resources.Load<TextAsset>(tableName) != null`, `Load → TableUtil.LoadLocalTable(tableName, useEncrypt)`(경로 `"Resources/"+tableName`, `TableUtil.cs:105~109`).
- `TableBase` (`Runtime/Tables/TableBase.cs`) — `LoadDatas()`(GameTableSource.Current), `LoadDatas(ITableSource)`(:32), `LoadDatasAsync()`(:39). `ReplaceData`는 **읽기 성공 후** 비우고 넣으며, `InsertData` 예외 시 `CaptureData/RestoreData`로 이전 행 복구(:45~74).
- `IndexedTable<T>` — `Dict`(id→행), `Datas`, `Get(id)`(:94~101). `KeyedTable<TKey,T>` — 같은 구조, 키 함수.
- `TableRepository` (`Runtime/Tables/TableRepository.cs`) — 정적 등록 목록 + `loaded` 1회 플래그. `LoadAllAsync`(:35)는 이미 loaded면 즉시 반환, 아니면 등록 순서대로 `LoadDatasAsync` 후 진행률 콜백. `SubsystemRegistration`에서 `Clear()`.
- `TableData` — `id/sort/grade/nameKey`, `name => GetLoc("name", nameKey)`; `TableRuntimeOptions.Localization`이 없으면 **키를 그대로 반환**(`TableData.cs:70~80`).

### 3.4 저장소 규칙 준수 상태
- 런타임 원본은 `MvTableLoader.DefaultSource()` = `PackagedTableSource(false)` 단 하나이고, 유일한 호출자 `MvBootMeta.LoadTablesAsync`가 그것을 명시적으로 넘긴다. `#if UNITY_EDITOR` 분기·TSV 폴백 없음. 산출물이 없으면 `Resources.Load` 실패 → 로드 예외로 부팅 실패(규칙 「산출물이 없으면 에디터에서도 오류로 드러낸다」).
- 주의(문서 불일치): `MvTableLoader.TsvDirectory` 주석(:16)과 `MvTsvDirectorySource` 주석(`MvTableSources.cs:10~11`)은 아직 「에디터 전용 대체 원본」「에디터 개발 대체(표 반영 전)」라고 적혀 있다. 실제 런타임 경로에서는 쓰지 않으므로 주석만 옛 표현.

---

## 4. 흐름

### 4.1 표 로딩 (부팅 4단계)
1. `MV.Boot/GameBootstrap.cs:56~58` `Start()` → `RunAsync()`. 1~3단계(Addressables 초기화·관리자·오디오/이펙트 공급자) 뒤
2. `GameBootstrap.cs:94` `await MvBootMeta.LoadTablesAsync()`.
3. `MV.Boot/MvBootMeta.cs:43~47` `CreateTableOptions()` — `IsComponentImplemented = RunComponents.IsImplemented`(MV.Run 목록 주입; MV.Data는 MV.Run을 모름).
4. `MvBootMeta.cs:50~51` → `MvTableLoader.LoadAsync(MvTableLoader.DefaultSource(), options)`.
5. `MvTableLoader.cs:37` `GameTableSource.Configure(PackagedTableSource(useEncrypt:false))`.
6. `:38` `new MvTables()` — 72개 표 인스턴스 생성(빈 상태).
7. `:39~43` 첫 부팅이면 `tables.RegisterAll()` → `TableRepository.LoadAllAsync(onProgress)` — 표마다 `LoadDatasAsync` → `GameTableSource.Current.LoadAsync(name)` → `Resources/NNN_name` TextAsset → `TSVRow` 목록 → `MvTable<T>.InsertData`(행 파싱 `Init`, `RawStore` 보관, `Reindex`).
   - `:44~49` 같은 세션 재부팅(`TableRepository.loaded == true`)이면 저장소 1회 규칙을 건드리지 않고 새 인스턴스마다 `t.Table.LoadDatasAsync()`로 직접 읽는다.
8. `:51` `MvTableValidator.ValidateRuntime(tables, options)` → `LastReport` 저장, 경고는 `Debug.LogWarning`, 오류면 `MvTableValidationException`(:54) → `GameBootstrap` catch에서 `BootState.Failed`(:127~131).
9. `:56` `tables.BuildIndexes()` — `TemplateObject.BuildZOrder()` + `Drops = new MvDropRoller(this)`.
10. `:57` `MvTables.Current = tables`.
11. 이어서 `GameBootstrap.cs:99` `MvBootSave.Open(MvTables.Current)`(재화 정의 = `tables.Wealth`, 저장 참조 정리용 표 값 수집 `MvBootSave.cs:54~162`), `:103` `MvBootMeta.Attach(MvTables.Current)`(→ `MvMeta(…, tables)`), Game 씬의 `GameInitializer.cs:32~40`이 `MvTables.Current`를 `MvGameLoop.InitAsync(tables)`에 넘김.
- 조회 API 요약: `MvTables.Current.<표>.Get(id)` / `.ByKey(key)` / `.GetGroup(g)` / `.Sorted` / `.Datas`, 키-값 표는 `RunConfig.D/I/List(RunConfigKeys.X)`, `ContentConfig.I/B/Consume(ContentConfigKeys.X)`, 단일 행 `Config.Value`.

### 4.2 검증 흐름
- **작성 시(에디터)**: TableBuilder → `MvTableAuthoring.ValidateBuildInputs`(`MvTableAuthoring.cs:39`) → `Validate`(:54) → `MvTableValidator.ValidateAuthoringTsv`(:55) → `ValidateAuthoring`(:38): `MvMemoryTableSource(입력 원문, fallback=TSV 폴더)` → 새 `MvTables` → `TryLoad`(:59, 표 없음/읽기 실패 = 오류) → 헤더 공급자 설정 → `Validate`(:92). 통과 시 `MvResourceColumnChecker`로 리소스 열 확인 → 보고 병합 → 오류면 예외로 반영 거부.
- **실행 시(부팅)**: `MvTableLoader.LoadAsync:51` → `ValidateRuntime`(:84) → `Validate`(:92). 헤더 공급자 없음 → `name` 열 위치 검사·헤더 기반 중복 열 검사는 생략(행의 `Contains`로 필수 열만 확인), 리소스 카탈로그 없음 → Res 검사 생략, Localization 없음 → `CheckNames` 생략. `IsComponentImplemented`만 주입됨.
- 같은 `Validate` 본체를 두 곳이 공유하므로 규칙은 한 곳(MV.Data)에만 있다. 원문 행(`RawRows`)을 다시 읽어 타입을 확인하는 이유: `TSVRow.GetInt` 등이 형식 오류를 조용히 기본값으로 바꾸기 때문(`MvTable.cs:13` 주석).

### 4.3 드롭/보상 계산 흐름 (런 안)
1. 오브젝트 생성 시 드롭 묶음 결정: `MV.Run/State/RunObject.cs:121` `o.DropGroup = o.Enemy.dropId`(102_enemy), `:124` `105_objectKind.dropId`가 있으면 덮어씀.
   고정 보상은 `LevelGen.SetReward/SetKindReward`(`MV.Run/Gen/LevelGen.cs:84,97`)가 105/311의 `ConsumeData` 목록을 오브젝트에 넣고, 값은 `LevelGen.RewardValue`(:72~81) — Flat=`ValueAtZone(ZoneIndex)`, DiaCap/HalfDiaCapCeil=`RunFormulas.DiaCap`(14_rebirthPerk, `RunFormulas.cs:174`).
2. 파괴·몫 지급: `MV.Run/Sim/RunSim.Rules.cs:435` `PayShares` — 마지막 몫에서 `DropGroup != 0`이면 `(T.Drops ?? new MvDropRoller(T)).RollDrop(o.DropGroup, () => s.Draw(RngUse.EnemyDrop), _dropBuf)`(:451). 난수는 런 RNG 스트림.
3. `MvDropRoller.RollDrop` → `RollDropOnce`(:105) → `Pick`(id 순 누적 비중) → `Emit`(:127): Drop(50)은 재귀, RandomBox(8)는 상자 자체로 결과, 그 밖 결과 추가.
4. `RunSim.Rules.cs:457` `DropValue(d)` — Rule이 DiaCap/HalfDiaCapCeil이면 `DiaCap(T, PerkDcap)`(또는 /2 올림), 아니면 `d.Value`.
5. `RunSim.Rules.cs:468` `Grant(type, id, value, gm, at)` — RunGold(602)는 ×goldMul, RunSouls(603)·RunDia(604)는 런 상태에 합산, None 무시, **그 밖(Wealth·RandomBox·Gear 등)은 `s.PendingSettle`에 `ConsumeData`로 쌓음**(:478) + `RunEvent.Gain` 발행.
6. 런 종료: `MV.Run/Rule/MainRunRule.cs:85` `r.PendingSettle.AddRange(s.PendingSettle)` → `RunApi`의 결과 → `MV.Meta/MvRunOutcome.cs:36` `PendingSettle` → `MV.Meta/MvMeta.cs:124~125`에서 저장(JSON `pending`). 주석상 Wealth·RbPending만 반영하고 나머지는 결과로 돌려준다(`MvRunOutcome.cs:35`).
- `OpenBox`/`DropOdds`/`BoxOdds`는 MV.Data 밖에서 아직 호출되지 않는다(상자 열기 UI·확률 표시는 미연결). 803/804 보장 로직도 미구현.

---

## 5. 표 목록 (Resources 산출물 72개 = TSV 72개)

열 표기: 스키마의 주요 열만. `(F→표)`=FK, `(G→표)`=묶음 FK, `{p}`=consume 열 묶음(`{p}Type/Id/Value…`). 행 수는 현재 TSV 데이터 행 수. 「읽는 모듈」은 MV.Data 밖 비시험 코드의 `t.<표>.<조회>` 패턴 grep 결과(6절 참조). 모든 표는 MV.Data 검증기가 읽는다.

### 0번대 — 공통 정의 (`Tables/Band0Tables.cs`, 01·07은 `Core/MvTable.cs`)
| 표 | 행 | 용도 | 주요 열 | 읽는 모듈 |
|---|---|---|---|---|
| `00_config` | 1 | 전역 단일 설정 | dataVersion, startDeck, startGodKey(→113 key), rebirthArmSec, rebirthCooldownSec, adDayUtcOffsetHour, progressResetArmSec, defaultMute | Boot, Meta, Run |
| `01_wealth` | 18 | 재화 정의(Framework WealthTable) | id, type, sort, icon, maxValue, chargeShopSectionId | Boot(MvBootSave→ResourceService), UI |
| `02_playResource` | 5 | 충전형 자원(에너지 등) | key, chargeType, chargeSec/Amount/Max | UI |
| `03_sound` | 33 | 소리 정의 | key, soundType, soundName(주소), volume, pitchRand, gapMs | Sound |
| `04_effect` | 75 | 이펙트 정의(group/part 트리) | key, type, prefab, children, when, anchor, shake(→300 접두), hitStop | Fx |
| `05_contentOpen` | 18 | 콘텐츠 열림 조건 | key, contentId(→400), conditionType/Value, showCondition*, sticky | Meta, UI, Boot |
| `06_balancePreset` | 3 | 난이도 프리셋 | key, rate, scale, diff, gauge, isDefault | Run, UI |
| `07_consumeType` | 26 | 보상·비용 종류(키 typeId) | typeId, codeName, idTable, idKind, valueKind, iconAtlas/Column, applyMul, settleTo, collectMul | Sound, 검증기 전반 |
| `08_adPlacement` | 7 | 광고 자리 | key, adType, placement, dailyLimit, chargeMax/Sec, mul, minArea, {adReward} | Meta, UI, Boot |
| `09_uiResource` | 12 | UI 리소스 묶음 | key, group, atlas, sprite, address | (검증만) |

### 1번대 — 로비 성장 (`Tables/Band1Tables.cs`)
| 표 | 행 | 용도 | 주요 열 | 읽는 모듈 |
|---|---|---|---|---|
| `10_lobbyUpgrade` | 3 | 로비 강화(계단·지팡이·골드) | key, max, levelPriceGroup(G→16), effectType, costFormula/Base/Growth | Meta, UI, Boot, Content |
| `11_stairLevel` | 4 | 계단 Lv별 필요 수 | lv, need | Meta, Run |
| `12_weaponLevel` | 12 | 지팡이 Lv별 필요 수·무기 | lv, need, itemId(F→103) | Meta, Run, UI |
| `13_areaUpgrade` | 8 | 지역 강화(다이아) | key, max, perLv, statKey, valueFormat, resetOn, levelPriceGroup | Meta, UI, Boot, Run |
| `14_rebirthPerk` | 9 | 환생 특성(토큰) | key, max, base, perLv, floor, statKey(DiaCap 등), levelPriceGroup | Meta, Run, UI, Boot |
| `16_levelPrice` | 937 | 레벨별 가격 묶음 | group, targetLevel, {price}, needPiece | Meta (+생성기 대조) |

### 1xx — 런 오브젝트·카드 (`Tables/Band1xxTables.cs`)
| 표 | 행 | 용도 | 주요 열 | 읽는 모듈 |
|---|---|---|---|---|
| `101_cat` | 2 | 고양이 캐릭터 | key, lobbySprite | View |
| `102_enemy` | 3 | 적 | key, hpRuleId(→114), moveAiId(→109), {reward}(런 확장), dropId(G→801), sprite, hit/break 소리·이펙트, hw/hd/scale | Run, Fx, Sound |
| `103_item` | 12 | 무기(등급 tier) | key, tier, dmg, col, projSprite, lookName | Run, UI, View, Boot |
| `105_objectKind` | 13 | 오브젝트 종류(컴포넌트 조합) | key, hittable, breakable, blocks, hpRuleId, hitReactionId, chargeGateId, receiveId, moveAiId, enemyId, rewardSource, {reward}, dropId, rewardShares, sprite, viewShape, labelKind, 소리·이펙트 | Run, Fx, Sound, View |
| `106_hitReaction` | 7 | 피격 반응 | type, flash, punch, breakHitstop/Shake(→300), breakStat | Run |
| `107_chargeGate` | 3 | 충전 게이트(계단·제작·우물) | gateType, base, step, growth, cap, perPass | Run |
| `108_receiveCondition` | 7 | 보상 수령 조건 | type(OnBreak/OnPass…), margin, delayMs, arriveAction, pass 이펙트·소리 | Run, Sound |
| `109_moveAI` | 3 | 이동 AI | type, aggro, speedMin/Max, touchAction(Steal), stealBase/PerZone | Run, Sound |
| `110_card` | 92 | 카드(묶음=신 카드 세트) | group, key(묶음 안 유일), tier, effectId(→112), icon | Run, UI, View |
| `111_cardPool` | 11 | 카드 풀 | group, key, entryType, cardId, weight | Run |
| `112_cardEffect` | 26 | 카드 효과 | key, type, p1~p4, proc 이펙트·소리 | Run, View |
| `113_god` | 12 | 신 | key, element, color, cardGroup, unlockType, openId(→05), {unlockPrice}, passiveStat | Run, Meta, UI, Boot |
| `114_hpRule` | 7 | 체력 규칙 | type, mul, fixed, useScale, useObstMul | Run |
| `115_cardTier` | 5 | 카드 등급 연출 | tierIndex, need, color, frameSprite, cardSound | Fx, Sound, UI, View, Run |
| `116_charLook` | 16 | 캐릭터 외형(slot·tier) | catId(→101), slot, fromTier, prefab, atlas, walk/atk/happy 스프라이트, scale | View(CharLookResolver) |

### 3xx — 런 세계·생성·카메라 (`Tables/Band3xxTables.cs`)
| 표 | 행 | 용도 | 주요 열 | 읽는 모듈 |
|---|---|---|---|---|
| `300_runConfig` | 183 | 런 상수 키-값 | key(=RunConfigKeys), value, unit | Run, Fx, View, UI, Meta, Boot, Sound |
| `301_zone` | 6 | 존 | sort, mapIcon, mapCol1/2, bgm, weaponLookSet | UI |
| `302_area` | 30 | 지역(스테이지) | areaNo, zoneId(→301), themeId(→304), hp, needSouls, sgateM, startGold, floorGroup(G→303), stageMap, {clearReward}, {firstClearReward} | Run, Boot, UI, Meta |
| `303_areaFloor` | 94 | 지역 층 데이터 | group, floor, h0, h1, rows, souls, cellMode, soulCells, diaCells | Run, Boot, UI |
| `304_theme` | 15 | 시각 테마 | worldAtlas, 색 다수, prop/leafSet, night, bgm | Boot(MvGameLoop) |
| `305_stageObject` | 5 | 스테이지 맵별 오브젝트 프리팹 | stageMap, kind(→105 key), reward, prefab | View |
| `310_template` | 10 | 배치 템플릿 | key, endZ, midFromZ/ToZ | Run |
| `311_templateObject` | 123 | 템플릿 안 오브젝트 | templateId(→310), seq, kindId(→105), x, z, groupId(G→314), hpRuleId, {reward} | Run (`InZOrder`) |
| `312_templateCycle` | 2 | Level→템플릿 순환 | cycleGroup, levelFrom/To, templates(→310 목록), mode | Run (`TemplateFor`) |
| `313_fieldGen` | 1 | 필드(벽·아치·보너스) 생성 상수 | lanes, laneX0/Step, wallKindId 등 105 FK, soul/dia 해시, gold 공식, bonus 배치 | Run |
| `314_spawnGroup` | 35 | 묶음 배치 오프셋 | group, key, idx, kindId, dx, dz | Run, View |
| `320_camera` | 4 | 카메라 프리셋 | key, anchor, pos/look, lerp, fovV, near, far | View, UI |
| `330_climb` | 5 | 계단 오르기 설정·워프 단계 | rowType(Config/WarpStage), key, 설정 열 30여 개, stepPrefab, at, sky 색 | Run, View, Fx, Sound, UI |

### 4xx — 모드·퀘스트·메타 (`Tables/Band4xxContentTables.cs`, `Band4xxMetaTables.cs`)
| 표 | 행 | 용도 | 주요 열 | 읽는 모듈 |
|---|---|---|---|---|
| `400_content` | 5 | 콘텐츠(모드) | key, contentType, modeRuleId(→402), stageGroup(G→401), openId(→05), {spendResource}, dailyLimit, hudId | Meta, UI |
| `401_modeStage` | 53 | 모드 단계 | group, stage, templateId, floorGroup, baseHp, hpMul, survivalSec, enemy/box/boss 수치, {clearReward} | (검증만, 미연결) |
| `402_modeRule` | 4 | 모드 규칙 | ruleClass, layout, cardPoolId(→111 key), fieldGenKey, climbKey, hpSource, 아레나 열, xp/카드 선택 | (검증만) |
| `410_questGroup` | 14 | 퀘스트 묶음 | group, periodType, category, groupType, progressScope | Content |
| `413_repeatQuest` | 16 | 반복 퀘스트 | questGroup(G→410), clearType, clearValue(Step), {reward} | Content |
| `414_passQuest` | 60 | 패스 퀘스트 | questGroup, step, clearType, clearValue, {reward}, {premiumReward} | (검증만) |
| `420_achievement` | 19 | 업적 | questGroup, progress, clearType, prevClearValue, clearValue, {reward} | Content |
| `430_pet` | 16 | 펫 | rarity, statKey, value, skill*, levelPriceGroup | (검증·가격 생성기만) |
| `431_petBuild` | 16 | 펫 옵션 | tier, tierRate, statKey, valueMin/Max | (검증만) |
| `440_research` | 25 | 연구 | group, level, statType, statValue, {price}, seconds | Meta (`ResearchTable.Get`) |
| `450_friend` | 10 | 친구 | rarity, statKey, baseValue, perLevel, maxLevel, {unlockPrice}, skill*, levelPriceGroup | (검증·생성기만) |
| `451_friendPower` | 3 | 친구 파워 | unlockLevel, {price}, statKey, value | (검증만) |
| `460_rune` | 4 | 룬 | grade, color, randomBoxRowId(→802) | (검증만) |
| `470_gearPart` | 7 | 장비 부위 | statKey, icon, sort | (검증만) |
| `471_gearRarity` | 8 | 장비 희귀도 | rarity, color, value, {sellPrice}, levelPriceGroup, frame | (검증·생성기만) |
| `480_blessing` | 4 | 축복 | key, effectType, value1/2, adRuns | (검증만) |
| `485_attendance` | 7 | 출석 | day, {reward} | (`AttendanceCycleDays` 외 미연결) |
| `490_roadMap` | 6 | 로드맵 | zoneId(→301), gainPerRun, maxRange, catchUpGain, startGap | (검증만) |
| `495_godLevel` | 5 | 신 레벨 | level, dmgPct, passiveMul, levelPriceGroup | (검증·생성기만) |
| `499_contentConfig` | 40 | 콘텐츠 상수 키-값 | key(=ContentConfigKeys), value, unit(`consume`이면 type/id/value) | MvTables 계산(`GearMaxLevel`, `MaxCompanions`) 경유 Run |

### 7xx — 상점 (`Tables/Band7xxTables.cs`)
| 표 | 행 | 용도 | 주요 열 | 읽는 모듈 |
|---|---|---|---|---|
| `700_productTab` | 6 | 상점 탭 | sort, icon | (검증만) |
| `701_product` | 29 | 상품 | tabId(→700), popupType, chargeType, {reward}, {price}, google/appleProductId, start/endTime, minArea, cooldownHours | (검증만, 결제 연결은 MV-PLAT-IAP) |
| `702_shopSection` | 4 | 상점 구역 | sectionType, hideIfEmpty, tabIds(→700 목록) | (검증만) |

### 8xx — 확률 (`Tables/Band8xxTables.cs`)
| 표 | 행 | 용도 | 주요 열 | 읽는 모듈 |
|---|---|---|---|---|
| `801_drop` | 4 | 드롭 묶음 | dropGroup(=첫 행 id), rewardType/Id, rewardValue_min/max, rewardValueRule, weight | Run (`Drops.RollDrop`) |
| `802_randomBox` | 34 | 랜덤 상자(칸=subGroup) | group, subGroup, rewardType/Id, rewardValue_min/max, weight, nameKey | (검증만, OpenBox 미호출) |
| `803_randomGuaranteeLink` | 0 | 보장 연결 | guaranteeId(→804), sourceBoxGroup, sourceBoxSubGroup | (검증만) |
| `804_randomGuarantee` | 0 | 보장(천장) | countType, triggerCount, rewardMode, rewardBoxGroup/SubGroup, priority | (검증만) |

공통 규칙(검증기가 강제): id = 표번호×1,000,000 + category×1,000 + index, 사람용 `name` 열은 키 열 바로 다음(코드는 안 읽음), `active` 빈칸 = 1, bool01 = 0/1, 목록 구분 `/`, consume 구분 `;`.

---

## 6. MV.Data 소비자 (모듈별, 시험 제외)

추출 방법: `Scripts/MV.*/`에서 `.<표>.<Datas|Get|GetGroup|ByKey|Sorted|Value|I|D|B|Raw|Has|…>` 패턴과 MvTables 계산 메서드를 grep. 괄호는 등장 횟수(대략).

| 모듈 | 표를 받는 방법 | 읽는 표 / API |
|---|---|---|
| **MV.Boot** | `MvTables.Current` (`GameBootstrap.cs:99,103`, `GameInitializer.cs:32`), `MvBootSave.Open(tables)`, `MvGameLoop.InitAsync(tables)` | Area(4), RunConfig(3), Wealth(2), RebirthPerk(2), AreaUpgrade(2), AdPlacement(2), Theme, LobbyUpgrade, Item, God, ContentOpen, Config, AreaFloor; `FloorCount`, `LevelMax`, `WeaponTierMax`, `ZoneIndexOfArea`. 로더 호출(`MvBootMeta.cs:50`) |
| **MV.Run** | `RunContext.Tables`(`MvRunStart.CreateContext` :131) → `RunState.Tables`, `RunSim.T`(`RunSim.Rules.cs:17`) | RunConfig(11), ObjectKind(8), CardEffect(7), Card(7), Area(4), SpawnGroup, HpRule, Climb, ChargeGate, TemplateObject, Template, God, Enemy, CardPool, BalancePreset, WeaponLevel, TemplateCycle, StairLevel, ReceiveCondition, RebirthPerk, MoveAi, Item, HitReaction, FieldGen, Config, CardTier, AreaUpgrade, AreaFloor; `Drops`, `FloorCount`, `MaxCompanions`, `StairMaxLv`, `WeaponTierMax`, `ZoneIndexOfArea`; 타입 `ConsumeData`, `MvDropResult`, `RewardValueRule` |
| **MV.Meta** | `MvMeta.Tables`(생성자 주입 `MvMeta.cs:48~53`) | AdPlacement(9), ContentOpen(8), RunConfig(4), Research(4), RebirthPerk(3), Config(3), AreaUpgrade(3), LobbyUpgrade(2), God(2), WeaponLevel, StairLevel, LevelPrice, Content, Area; `LevelMax`, `StairMaxLv`, `WeaponTierMax`, `ZoneIndexOfArea`; `ConsumeData`(별칭 `MvConsume`) |
| **MV.Content** | 호출자가 `MvTables t` 전달 | QuestGroup(3), Achievement(3), RepeatQuest(2), LobbyUpgrade; `ConsumeData`(퀘스트 보상, `MvQuestRules.cs:38`) |
| **MV.UI** | `MvTables.Current` 또는 `MvMeta.Current?.Tables`(`MetaUiPort.cs:131`, `ResultContracts.cs:135`, `LobbyContracts.cs:78`, `StageFitter.cs:79`) | ContentOpen(14), AdPlacement(8), Wealth(5), RunConfig(5), Area(5), God(4), Card(4), WeaponLevel(3), Zone(2), LobbyUpgrade(2), Item(2), Content(2), Climb(2), CardTier(2), AreaUpgrade(2), RebirthPerk, PlayResource, Camera, BalancePreset, AreaFloor; `StairMaxLv`, `ZoneFirstArea` |
| **MV.View** | 호출자 전달 | RunConfig, Climb, StageObject, Camera, SpawnGroup, ObjectKind, Item, CharLook, Cat, CardTier, CardEffect, Card; `CharLookResolver` |
| **MV.Fx** | `Func<MvTables>` 공급 함수(기본 `() => MvTables.Current`, `MvFx.cs:39,99`, `MvFxRouter.cs:32`) — 매번 최신 Current를 읽음 | RunConfig(11), Effect(5), ObjectKind(4), Climb(2), Enemy, CardTier |
| **MV.Sound** | `Func<MvTables>` (`MvSound.cs:21`, `MvSoundPlayer.cs:45`, `MvRunSoundRouter.cs:39`) | Sound(2), ReceiveCondition(2), ObjectKind(2), RunConfig, MoveAi, Enemy, ConsumeType, Climb, CardTier |
| **MV.Save** | 표 타입을 모름. Boot가 `MvTables.Current`에서 값만 뽑아 넘김(`MvSaveReferenceFix.cs:11`, `MvSaveStore.cs:26`) | 간접: FloorCount 등(MvBootSave가 채움) |
| **MV.Platform / MV.Core** | 사용 없음 | — |
| **MV.Editor** | `MvTableAuthoring`, `MvConsumeTypeCodegen`, `MvResourceColumnChecker`(`new MvTables()` + `LoadAll(MvTsvDirectorySource)`) | 전 표(검증·리소스 확인) |

현재 MV.Data 밖에서 읽히지 않는 표(검증·생성기 전용): 09, 401, 402, 414, 430, 431, 450, 451, 460, 470, 471, 480, 485(계산 메서드만), 490, 495, 499(계산 메서드만), 700~702, 802~804. 기획서상 기능이 뒤 계약에서 연결될 자리다.

---

## 7. 캐싱·재사용·불변성

- **한 번 로드, 한 인스턴스**: 부팅 때 `MvTableLoader.LoadAsync`가 새 `MvTables`를 만들어 `MvTables.Current`에 넣는다. 모든 모듈은 그 인스턴스를 직접 들거나(`MvMeta.Tables`, `RunState.Tables`) 공급 함수로 매번 `Current`를 읽는다(Fx·Sound). 교체 시점은 재부팅(또는 시험의 핫 리로드)뿐.
- **Framework 1회 규칙**: `TableRepository.loaded`가 true면 `LoadAllAsync`는 아무 것도 안 함. 그래서 같은 세션 재부팅은 새 인스턴스를 저장소에 등록하지 않고 직접 `LoadDatasAsync`(`MvTableLoader.cs:44~49`). 저장소에는 첫 부팅 인스턴스가 등록된 채 남는다(재사용 안 함). Domain Reload 꺼짐 대비로 `TableRepository`/`GameTableSource`/`MvTableLoader`/`MvBootMeta` 모두 `SubsystemRegistration`에서 정적 상태를 지움.
- **사전 구성**: 로드(`InsertData`) 때 한 번에 `Dict`(id), `_byKey`, `_groups`, `_sorted`를 만든다(같은 TSV를 두 번 읽지 않음, `MvTable.cs:79` 주석). 교차 표 캐시(`TemplateObject._byZ`, `Drops`)는 검증 뒤 `BuildIndexes`에서 만든다.
- **매 호출 계산(캐시 없음)**: `MvTables`의 계산 메서드(`FloorCount`, `StairMaxLv`, `LevelMax`, `ZoneIndexOfArea` …), `LevelPriceTable.Get`, `ModeStageTable.Get`, `ResearchTable.Get`, `TemplateCycleTable.TemplateFor`, `ClimbTable.WarpStages`(매번 새 리스트+정렬), `RunConfigTable.D/I`(매번 문자열 파싱)는 호출마다 선형 탐색/파싱한다. `MvDropRoller.Pick`도 매번 `OrderBy(id)` 정렬.
- **불변성**: 행 속성은 `private set`, 목록은 `IReadOnlyList`, `ConsumeData`/`MvDropResult`는 `readonly struct`. 표 객체는 `LoadDatas`로 다시 채울 수 있지만, 실패하면 Framework가 이전 행을 복구(`TableBase.cs:51~67`) + `MvTable.RestoreData`가 재색인. `MvTables.Current`는 public setter라 기술적으로 누구나 바꿀 수 있다(관례상 로더만).
- **원문 보관**: 모든 표가 `RawStore`로 마지막 로드 원문 행을 들고 있어(메모리 2배) 검증기가 원문을 다시 해석한다.

---

## 8. 다국어(언어팩) 현황

- **데이터 없음, 로딩 코드 없음.** `Assets/` 아래에 게임 문구 표·언어팩 자산이 없고(`find -ipath "*locali*"` 결과 GoogleMobileAds 에디터 파일뿐), `Assets/Game/Addressables/Local/` 하위 분류에도 언어 폴더가 없다(Audio, BG, Character, Effect, Font, Mode, Projectile, Texture, Theme, UI, UIAtlas, World, WorldAtlas).
- 부팅 7단계는 비어 있음: `GameBootstrap.cs:105` 「7. 다국어: 아직 없다(문구 키 그대로). 연결되면 8단계 앞에서 InitLocalization.」
- UI는 `MvUiBoot.ConfigureOptions`(`MV.UI/Common/MvUiBoot.cs:13~16`)에서 `UIRuntimeOptions.Localize = (collection, key) => key`(키 그대로). 교체 지점 주석: `MvUiText.cs:9`, `RunHudPresenter.cs:66`, `ResultPresenter.cs:106`, `ObjectViewFactory.cs:35,380`.
- 표 행 `name`은 Framework `TableData.GetLoc` — `TableRuntimeOptions.Localization`이 null이므로 `nameKey`(= id 문자열)를 그대로 돌려준다. 게임 코드에서 `TableRuntimeOptions.Localization`을 설정하는 곳 없음.
- 검증기는 `MvValidationOptions.Localization`이 준비되면 `name:{id}` 문구를 확인하도록 되어 있으나(`MvTableValidator.cs:100, 495`) 부팅·작성 검증 모두 넘기지 않는다.
- 설계 의도(저장소 규칙 AGENTS.md): 원격 리소스는 언어팩만, 로컬 기본본 적용 후 원격본으로 덧씌움. 패키지 `com.jjevol.localization`/`.authoring`/`.tmp`와 `com.unity.localization` 1.5.12가 manifest에 있고 `MV.Boot.asmdef:26`이 `JJEvol.Localization.Runtime`을 참조하지만, 사용 코드는 아직 없다. 지원 언어 설정 = 한국어·영어(`Docs/AI/PROJECT_CONTEXT.md:11`).

---

## 9. 새 개발자가 알아야 할 함정

1. 열을 추가하면 **행 클래스 `Init` + `Schema` + TSV** 세 곳을 함께 고친다(스키마는 파싱에 쓰이지 않음). 키-값 표(300·499)는 `RunConfigKeys`/`ContentConfigKeys` 상수도 고친다(검증기가 양방향 대조).
2. TSV만 고치고 TableBuilder를 돌리지 않으면 런타임은 옛 Resources 산출물을 읽는다(대체 경로 없음).
3. 새 `ConsumeType`은 07 행 + enum 값 + 지급 처리(`RunSim.Grant` 또는 정산) 세 곳. 이름이 다르면 검증 오류.
4. consume 열 형식 오류는 로드 때 조용히 빈 목록이 되고 검증 단계에서 오류로 잡힌다 — 시험에서 `ValidateRuntime`을 생략하면 놓친다.
5. `MvDropRoller`는 상자(8)를 열지 않고 결과로 돌려준다. DiaCap 계열 Rule은 Value=0이라 호출자가 14_rebirthPerk로 계산해야 한다.
6. 시험/에디터에서 `BuildIndexes`를 안 부르면 `Drops`가 null — `RunSim`은 `T.Drops ?? new MvDropRoller(T)`로 대비한다(`RunSim.Rules.cs:451`).
