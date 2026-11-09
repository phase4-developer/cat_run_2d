# 7. 표 설계
[입구](README.md) · 앞: [06 이펙트](06_Effects.md) · 다음: [08 콘텐츠](08_Content.md)

이 문서는 달동네 Unity판의 **모든 표(TSV)의 이름·열·타입·초기 행·검증 규칙**을 정한다. 런 규칙의 의미는 [04](04_GameRules.md), 콘텐츠 볼륨(30 Area 수치·추가 모드·메타 시스템 행)은 [08](08_Content.md), 저장은 [09](09_Save.md), 로딩 흐름과 모듈 경계는 [10](10_Architecture.md)이 정본이다. 여기서는 「표 하나 = 무엇을 조정할 수 있는가」와 「행을 더하면 무엇이 늘어나는가」를 개발자가 추가 질문 없이 구현할 수 있게 적는다.

사용자 원문(2026-11-09): 「테이블 구조와 연결해서 세부 수치들을 조정가능하게… 컨텐츠도 테이블로 확장가능하고 오브젝트 배치나 오브젝트 종류 컨트롤 가능하게」, 「테이블 구조는 정크버스터즈 참고 많이해줘 지금 이 게임도 제대로 테이블 구조가 잡히지 않은거 같아 보상같은건 consumeData형태로 테이블에서 수정 가능해야하고 리소스도 테이블에서 설정가능해야해」.

읽는 순서: §7.0(정크버스터즈와 무엇이 같고 다른가) → §7.1(공통 규칙) → §7.2(표 목록·관계) → 필요한 표의 절. 표를 추가·수정할 때는 §7.13 검증 규칙과 §7.14 확장 예시를 먼저 본다.

---

## 7.0 정크버스터즈 대비 구조 결정

참고 원본(읽기만): 정크버스터즈 TSV 묶음과 `ConsumeData.cs`, 「Consume정의」 시트. **구조만** 따른다. 값·이름은 하나도 옮기지 않았다. 아래 「정크버스터즈」 칸은 구조 설명이다.

### 7.0.1 그대로 따르는 것

| 항목 | 정크버스터즈 구조 | 달동네(MV) 적용 |
|---|---|---|
| 파일 이름 | `NNN_camelCase.txt`, 번호 대역으로 분류 | 같음. 0 공통 / 1 성장 / 1xx 개체 / 3xx 런·배치 / 4xx 콘텐츠 / 7xx 상점 / 8xx 확률 |
| id | 정수. 큰 표는 `{표번호}{그룹3}{순번3}` | 같음. 예 `302001001`(Area 1), `311003007`(템플릿 3의 7번째 오브젝트) |
| 작은 정의 표 id | `01_wealth`처럼 1, 2, 3 | `01_wealth`, `06_balancePreset`, `07_consumeType`(키 `typeId` = consumeType 번호, `id` 열 없음). **규격 v1 §3과 다름**: 규격은 `01_wealth`도 `1001001` 식을 쓴다. 이 기획서는 작은 표를 1, 2, 3으로 썼다 → [13 §13.3.6](13_Decisions.md) D-80 |
| 묶음·순서·사용 | `group`, `sort`, `active`(1/0) | 모든 표에 `sort`·`active`. 묶음이 필요한 표만 `group` |
| 보상·비용 | `rewardType/rewardId/rewardValue`, `priceType/priceId/priceValue`, `;` 나열, 접두 변형(clearReward…, firstClearReward…) | 같음. 런 안 보상(상자·적·장애물)까지 전부 consumeData 3열로 적는다 |
| consumeData 파싱 | `ImportDatas(row, prefix)`: `;`로 자르고 type·id가 value보다 짧으면 마지막 것 반복 | 같은 규칙의 게임 클래스 `ConsumeData`(MV.Data). §7.1.6 |
| consumeType 번호 | 1 Wealth, 3 PlayResource, 5 Item, 8 RandomBox, 50 Drop, 100 AD, 101 Inapp, 300/301 퀘스트 점수, 500 Pass | 같은 번호를 그대로 쓴다(뜻이 같은 것만). 게임 고유는 600번대 |
| 확률 보상 | `801_drop`, `802_randomBox`, rate 10000 = 100% | JJEvol 표 규격 v1 §6~8: 정수 `weight`, 꽝 = rewardType 0 행, 802는 칸(subGroup)마다 1개, 천장 803·804. 적 캣닢 8%는 「보상 800 + 꽝 9200」 묶음(§7.9.3) |
| 성장 비용 | `16_levelPrice`(group, targetLevel, priceType/Id/Value) 행 | 같음. 로비 강화·Area 강화·부활 특성 비용이 전부 행 |
| 앱 전역 설정 | `00_config` 가로 1행 | 같음(메타 상수) |
| 리소스 | 각 표의 `icon`·`largeIcon`·`illust`·`bg`·`bgm`·`hitSound`·`hitEffect` 열 | 같음. §7.10 |
| 퀘스트 열 | `clearType`·`clearCondition`·`clearValue` + reward 3열 | 같음. 4xx 표 |
| 문구 키 | 행 id가 `name`·`description` 키 | 같음. Framework `TableData.nameKey` 기본값이 `"{id}"`라 그대로 맞는다 |

### 7.0.2 바꾼 것과 이유

| 항목 | 정크버스터즈 | 달동네 | 이유 |
|---|---|---|---|
| consumeType 아이콘 | 코드 switch로 아틀라스·아이콘 결정(하드코딩) | **`07_consumeType` 표**(iconAtlas, iconColumn) | 「리소스도 테이블에서」. 새 type을 행으로 추가 |
| 「Consume정의」 | 사람이 읽는 시트(런타임 미사용) | 런타임 표 `07_consumeType` + 검증기 입력 | id가 가리키는 표(idTable)를 검증기가 확인 |
| 런 안 보상 | 해당 없음(슬롯 게임) | 600번대 consumeType(RunGold·RunSouls·RunDia·RunStairs…) + `rewardValuePerZone`·`rewardValueRule` 열 | 프로토 보상이 「기본 + Zone 배수」와 「diaCap」 공식이라 3열만으로 부족 |
| 런 상수 | 대부분 코드·config | `300_runConfig` 세로 키/값 | 프로토 수치 수백 개를 코드 밖으로. 키 이름은 [04](04_GameRules.md)의 UPPER_SNAKE 그대로 |
| 오브젝트 행동 | 개체 표에 고정 열 | **컴포넌트 조합**: `105_objectKind` + 106~109·114 행동 표 | 「오브젝트 종류 컨트롤」. 새 종류 = 행 조합(§7.5, §7.14) |
| 배치 | 스테이지 표에 맵 이름 | `310~314` 배치 표 + 에디터 배치 도구 | 「오브젝트 배치 컨트롤」 |
| 테마 리소스 | 표마다 다른 스프라이트 이름 | 스프라이트 이름은 같게, `304_theme.worldAtlas`만 교체 | 30 Area × 테마 조합을 행 1개로 |
| 확률 표 해석 | 구현마다 다를 수 있음(추첨 코드와 확률 화면 코드가 따로 계산) | `801_drop` = 묶음에서 weight로 1행, `802_randomBox` = 칸마다 1행씩 모두, 추첨과 확률 표시는 같은 그래프 계산(§7.9.3). 엔진은 Framework `com.jjevol.economy.drop`(설계 중)으로 옮긴다 | 구현자마다 다르게 읽지 않게, 공개 확률과 실제 확률이 같게 |
| 성장 공식 | 시트 수식으로 행 생성 | 같은 방식 + 「행 생성 규칙」을 이 문서에 기록, 에디터 메뉴 `MV/Tables/Generate Level Price`가 행을 다시 만든다 | 공식을 바꾸면 도구로 행을 다시 굽는다. 런타임은 행만 읽는다 |

### 7.0.3 가져오지 않은 것

- 슬롯·심볼·캐릭터 등급 표, 구독·계정 혜택 consumeType(102~105), Exp(200). 달동네에 대응 개념이 없다.
- 레이스·길드·Guild Raid·팀 이벤트 표(사용자 제외 지시).
- 서버 검증 열. 결제·서버는 스텁이라 클라이언트 표만 둔다([01](01_Scope.md)).

---

## 7.1 공통 규칙

### 7.1.1 파일·형식

| 항목 | 규칙 |
|---|---|
| 위치 | `Assets/Game/GameData/Table/TSV/NNN_camelCase.txt` (UTF-8, BOM 없음, 줄바꿈 LF) |
| 첫 줄 | 열 이름 헤더. 대소문자 구분. 열 순서는 자유(이름으로 읽음) |
| 주석 열 | 이름이 `#`로 시작하는 열(`#kind`, `#비고`)은 시트에서 내보낼 때 뺀다. 남아 있어도 로더는 열 이름으로 읽으므로 무해하지만 검증기는 경고 |
| 주석 행 | TSV에는 두지 않는다. Framework는 첫 줄을 열 이름으로 읽을 뿐 사람용 줄을 거르지 않는다 |
| 빈칸 | 열마다 기본값(각 표 정의에 적음). consumeData 열이 비면 「보상 없음」 |
| 리스트 | 한 칸 안 여러 값은 `/` (Framework `GetIntList`·`GetFloatList` 구분자). **consumeData만 `;`** |
| 원본 | 구글 시트(가칭 「moonVillage_Table」)에서 TSV로 내보낸다. 정본은 저장소의 TSV. 시트 머리는 JJEvol 표 규격 v1 §4: 1줄 설명(뜻·참조 표·단위·숫자 코드 뜻) → 2줄 형식(`int`·`long`·`string`·`bool(0/1)`·`consume`) → 3줄 열 이름 → 데이터. **내보내기 도구가 열 이름 줄 위 2줄과 `#` 열을 빼고** TSV 첫 줄을 열 이름으로 만든다(Framework `tables.authoring`에는 시트 입력이 없다) |
| 반영 경로(필수) | TSV → Framework `TableBuilder`(표 제작 창) → Resources 산출물. 빌드 때 `TableAuthoringOptions.ValidateTables` 콜백에 게임 검증기 `MvTableValidator`(§7.13)를 등록해 **오류면 반영을 막는다**. 이 경로를 거치지 않은 TSV 복사·직접 편집은 금지(개발용 덮어쓰기 §7.1.9 제외) |

### 7.1.2 열 타입

| 표기 | 의미 | 읽기 | 예 |
|---|---|---|---|
| int | 32비트 정수 | `GetInt` | `302001001` |
| long | 64비트 | `GetLong` | 재화 상한, 시각(ms) |
| float | 실수(소수점 `.`) | `GetFloat`. 결정적 계산(MV.Run)이 쓰는 열은 `GetDouble`로 읽어 double 유지 | `1.25` |
| bool01 | 1/0 | **`GetInt(…) != 0`으로 읽는다**. Framework `GetBoolean`은 `true/false` 문자열만 받고 `1`은 파싱 실패 → 기본값을 조용히 돌려준다 | `active` |
| string | 문자열 | `GetString` | `tower` |
| enum | 문자열 이름 → C# enum `Enum.TryParse(ignoreCase)` | 실패하면 검증 오류 | `BeforePass` |
| color | `#rrggbb` 또는 `#rrggbbaa` | MV.View가 `ColorUtility.TryParseHtmlString` | `#ffd23a` |
| intList / floatList / strList | `/` 구분 | `GetIntList` / `GetFloatList` / `GetStringList` | `0/0.13/0.26` |
| consume | 3열 묶음 `{p}Type`·`{p}Id`·`{p}Value` | `ConsumeData.Import(row, p)` §7.1.6 | `1;2` · `1;2` · `100;5` |
| fk | 다른 표 id(int) | 검증기가 존재 확인 | `kindId=105001001` |
| res | 리소스 이름·주소 | §7.10 | `Obj_Well` |
| sound / effect | `03_sound.key` / `04_effect.key` | 검증기가 존재 확인 | `wood` |

**경고(Framework 사실)**: `TSVRow.GetValue`는 파싱에 실패하면 예외 없이 기본값을 돌려준다. 그래서 오타(`1.2.5`, `ture`)가 0·false로 조용히 바뀐다. 게임 검증기(§7.13)가 **원문 문자열을 다시 읽어** 타입을 확인한다.

### 7.1.3 단위

| 값 | 단위 |
|---|---|
| 거리·위치 | m(월드 단위 1 = 1m). x는 가로(−가 왼쪽), z는 진행 방향, y는 위 |
| 시간 | 열 이름이 `Sec`·`T`로 끝나면 초, `Ms`로 끝나면 ms. 그 밖은 표 정의에 적음 |
| 확률 | 0~1 실수. **단 `801_drop`·`802_randomBox`는 정수 `weight`(묶음 합으로 나눔, 합 10000 권장)** (JJEvol 표 규격 v1 §6~7) |
| 배율 | 1 = 100% |
| 각도 | rad, 각속도 rad/s |
| 색 | sRGB `#rrggbb` |

### 7.1.4 id·key·group·sort·active

- **id**: 정수, 표 안에서 유일. 형식 `{표번호}{그룹 3자리}{순번 3자리}`. 예: `105_objectKind` 1번 그룹 3번째 = `105001003`. 표번호가 2자리면 `16001001`처럼 그대로 붙인다. 작은 정의 표는 1, 2, 3.
- **key**: 코드가 이름으로 찾아야 하는 행만 `key`(string, 표 안 유일, 영문 lowerCamel). **코드에서 id 숫자를 하드코딩하지 않는다.** 예: `MvTables.Current.ObjectKind.ByKey("tower")`.
- **group**: 묶음 정수. 의미는 표마다 정의. `GroupedTable<int, T>`로 읽는다.
- **sort**: 화면 정렬·선택 순서(오름차순). 같으면 id 순.
- **active**: 1 사용, 0 미사용. 0인 행도 **로드는 한다**(다른 행·저장이 참조할 수 있음). 게임 로직이 고르지 않을 뿐. 「업데이트 예정」 표시 여부는 표마다 정의.

### 7.1.5 행 클래스·표 클래스

| 항목 | 규칙 |
|---|---|
| 어셈블리 | `MV.Data`. UnityEngine 타입을 쓰지 않는다(색은 string으로 들고 MV.View가 변환) — MV.Run이 Unity 없이 시험되기 때문 |
| 행 클래스 | `{이름}Data : TableData` (예 `ObjectKindData`, `AreaData`). `TableData`가 `id`·`sort`·`grade`·`nameKey`(기본 `"{id}"`)·`descriptionKey`를 이미 읽는다 |
| 표 클래스 | id 표는 `IndexedTable<T>`. key 조회가 필요한 표는 IndexedTable 위에 보조 사전 `MvKeyIndex<T>`를 `BuildIndexes()`에서 만든다(같은 TSV를 두 번 읽지 않음). 묶음은 `GroupedTable<int, T>` |
| 설정 표 | `00_config` → `ConfigData`(1행). `300_runConfig` → `RunConfigTable`(key → 원문 문자열 사전 + `D("RUN_SPEED")` double, `I()`, `List("FIRE_PHASE")`) |
| 모음 | `MvTables`(MV.Data) 한 객체가 모든 표 참조를 갖는다. 런타임은 `MvTables.Current`만 본다 |
| 이름 | 파일 `105_objectKind` → 클래스 `ObjectKindTable`, 행 `ObjectKindData`, 속성 `MvTables.ObjectKind` |

### 7.1.6 consumeData 규칙

열 묶음: 접두 `p`에 대해 `{p}Type`(string), `{p}Id`(string), `{p}Value`(string). 접두가 `reward`면 `rewardType/rewardId/rewardValue`.

```text
ConsumeData.Import(row, prefix = "reward") -> List<ConsumeData>
  types  = Split(row[prefix+"Type"],  ';')   // 빈칸 → []
  ids    = Split(row[prefix+"Id"],    ';')
  values = Split(row[prefix+"Value"], ';')
  n = values.Count
  if n == 0: return []                         // 보상 없음
  for i in 0..n-1:
    t = types[min(i, types.Count-1)]           // 짧으면 마지막 것 반복 (정크버스터즈 규칙)
    d = ids.Count == 0 ? "0" : ids[min(i, ids.Count-1)]
    type = int.TryParse(t) ? (ConsumeType)int : Enum.Parse<ConsumeType>(t, ignoreCase)
    yield ConsumeData{ type, id = int.Parse(d), value = double.Parse(values[i], Invariant),
                       perZone = PerZone[i] (§7.1.7, 없으면 0), rule = Rule[i] (없으면 Flat) }
```

- 값은 double로 든다(런 츄르는 소수 누적, [04 §4.0.2](04_GameRules.md)). 실제 재화에 넣을 때 `07_consumeType.valueKind`에 따라 내림한다.
- 범위 값: `{p}Value_min`·`{p}Value_max` 두 열이 있으면 `[min, max]` 균등 정수(난수원은 호출자가 넘긴다. 런 안이면 RunRng, 메타면 MetaRng).
- 비용(`price…`)도 같은 클래스. `priceType 100`(AD)이면 광고 시청이 비용, `101`(Inapp)이면 결제 스텁.
- 검증: types·ids의 개수가 「0(ids만 허용), 1, 또는 values와 같은 수」가 아니면 오류. type이 `07_consumeType`에 없거나 active 0이면 오류. id가 `07_consumeType.idTable` 표에 없으면 오류(idTable이 `-`면 id는 0).

### 7.1.7 런 보상 확장 열(600번대 보상에만)

프로토의 런 보상은 「기본 + Zone 배수」 또는 「부활 특성 diaCap」 공식이다([04 §4.3.3](04_GameRules.md)). consumeData 3열 옆에 다음 열을 둔다.

| 열 | 타입 | 뜻 | 기본 |
|---|---|---|---|
| `{p}ValuePerZone` | `;` 나열 실수(위치 대응, 짧으면 마지막 반복) | 값에 `+ perZone × zoneIndex`(0기반 `save.zone`) | 0 |
| `{p}ValueRule` | `;` 나열 enum | `Flat`(값 그대로) / `DiaCap`(값 = `14_rebirthPerk[dcap].base + perLv × lv`, 열 값은 무시) / `HalfDiaCapCeil`(ceil(diaCap / 2)) | Flat |

예: 적 처치 츄르 `rewardType=602`, `rewardValue=6`, `rewardValuePerZone=4` → Zone 0에서 6, Zone 1에서 10. 츄르는 지급 때 `goldMul`이 곱해진다(`07_consumeType[602].applyMul = GoldMul`).

### 7.1.8 로딩

```csharp
// MV.Boot (10 §10.7 흐름의 표 부분)
GameTableSource.Configure(new PackagedTableSource(useEncrypt: false));
var tables = new MvTables();
tables.RegisterAll(TableRepository.Instance);        // 모든 표 Register
await TableRepository.Instance.LoadAllAsync();        // 1회만
var report = MvTableValidator.ValidateRuntime(tables); // §7.13
if (report.HasError) { /* 개발 빌드: 오류 팝업 + 로그. 릴리스: 오류 보고 후 부팅 중단 화면 */ }
tables.BuildIndexes();   // key 사전, 그룹 정렬, 템플릿 z 정렬, ObjectKindRegistry, 레벨 비용 사전
MvTables.Current = tables;
```

### 7.1.9 핫 리로드(개발 전용)

| 항목 | 규칙 |
|---|---|
| 허용 위치 | **로비에서만**. 런 중에는 메뉴 비활성 |
| 방법 | 새 `MvTables` 인스턴스 → 각 표에 `LoadDatas(ITableSource)` → 검증 → 통과하면 `MvTables.Current` 교체. 실패하면 이전 것 유지 + 오류 표시 |
| 덮어쓰기 원본 | `MvDevTableSource`: `Application.persistentDataPath/TableOverride/NNN_name.txt`가 있으면 그 파일, 없으면 패키지 TSV. 개발 빌드에서만 등록 |
| 캐시 | 표에서 만든 캐시는 `BuildIndexes()`에서 다시 만든다. UI는 `SetDirty()`로 다시 그린다 |
| 저장 영향 | 저장은 id·key만 들고 있다. 없어진 id는 §7.13.4 「저장 참조」 규칙으로 처리 |

---

## 7.2 표 목록과 관계

### 7.2.1 전체 목록

「행 소유」는 초기 행을 누가 정하는가(07 = 이 문서, 08 = 콘텐츠 문서). 08 소유 표도 **열 정의는 이 문서**가 정본이다.

| 대역 | 파일 | 클래스 | 읽는 모듈 | 절 | 행 소유 |
|---|---|---|---|---|---|
| 0 | `00_config` | ConfigData(1행) | Meta, UI | 7.3.1 | 07 |
| | `01_wealth` | Framework `WealthTable` | Meta(ResourceService), UI | 7.8.10 | 07(1~4), 08(5~) |
| | `02_playResource` | PlayResourceTable | Meta, Content | 7.8.11 | 08 |
| | `03_sound` | SoundTable | Sound | 7.11.1 | 07 |
| | `04_effect` | EffectTable | Fx | 7.11.2 | 07(목록), [06](06_Effects.md)(파라미터 정본) |
| | `05_contentOpen` | ContentOpenTable | Meta, UI | 7.8.9 | 07(프로토 행), 08(추가) |
| | `06_balancePreset` | BalancePresetTable | Run | 7.3.2 | 07 |
| | `07_consumeType` | ConsumeTypeTable | Data, Meta, UI | 7.8.12 | 07 |
| | `08_adPlacement` | AdPlacementTable | Meta, Platform | 7.8.8 | 07(프로토), 08(추가) |
| | `09_uiResource` | UiResourceTable(key) | UI | 7.8.13 | 07(열), [05](05_Screens.md)(행) |
| 1 | `10_lobbyUpgrade` | LobbyUpgradeTable | Meta, UI | 7.8.1 | 07 |
| | `11_stairLevel` | StairLevelTable | Meta, Run | 7.8.2 | 07 |
| | `12_weaponLevel` | WeaponLevelTable | Meta | 7.8.3 | 07 |
| | `13_areaUpgrade` | AreaUpgradeTable | Meta, Run | 7.8.4 | 07 |
| | `14_rebirthPerk` | RebirthPerkTable | Meta, Run | 7.8.5 | 07 |
| | `16_levelPrice` | LevelPriceTable(group → level) | Meta | 7.8.6 | 07(그룹 1~24), 08(그룹 301~) |
| 1xx | `101_cat` | CatTable | View | 7.7.7 | 07 |
| | `102_enemy` | EnemyTable | Run, View | 7.5.8 | 07 |
| | `103_item` | ItemTable | Run, Meta, UI | 7.7.1 | 07 |
| | `105_objectKind` | ObjectKindTable | Run, View | 7.5.2 | 07 |
| | `106_hitReaction` | HitReactionTable | Run | 7.5.3 | 07 |
| | `107_chargeGate` | ChargeGateTable | Run | 7.5.4 | 07 |
| | `108_receiveCondition` | ReceiveConditionTable | Run | 7.5.5 | 07 |
| | `109_moveAI` | MoveAiTable | Run | 7.5.6 | 07 |
| | `110_card` | CardTable | Run, UI | 7.7.3 | 07, 08(신 확장) |
| | `111_cardPool` | CardPoolTable(group = 신) | Run, UI | 7.7.4 | 07, 08 |
| | `112_cardEffect` | CardEffectTable | Run | 7.7.5 | 07 |
| | `113_god` | GodTable | Run, Meta, UI | 7.7.6 | 07(2종), 08(10종) |
| | `114_hpRule` | HpRuleTable | Run | 7.5.7 | 07 |
| | `115_cardTier` | CardTierTable | Run, UI | 7.7.2 | 07 |
| 3xx | `300_runConfig` | RunConfigTable(세로) | Run, View | 7.3.3 | 07 |
| | `301_zone` | ZoneTable | Meta, UI | 7.4.1 | 08 |
| | `302_area` | AreaTable | Run, Meta, UI | 7.4.2 | 08 |
| | `303_areaFloor` | AreaFloorTable(group) | Run | 7.4.3 | 08 |
| | `304_theme` | ThemeTable | View | 7.4.4 | 07(1~5), 08(6~) |
| | `310_template` | TemplateTable | Run | 7.6.1 | 07 |
| | `311_templateObject` | TemplateObjectTable(group = 템플릿) | Run | 7.6.2 | 07 |
| | `312_templateCycle` | TemplateCycleTable | Run | 7.6.3 | 07 |
| | `313_fieldGen` | FieldGenTable | Run | 7.6.4 | 07 |
| | `314_spawnGroup` | SpawnGroupTable(group) | Run | 7.6.5 | 07 |
| | `320_camera` | CameraTable | View | 7.3.4 | 07 |
| | `330_climb` | ClimbTable | Run | 7.3.5 | 07 |
| 4xx | `400_content` | ContentTable | UI, Content | 7.9.1 | 08 |
| | `401_modeStage` · `402_modeRule` | | Content | 7.9.1 | 08 |
| | `410_questGroup` · `413_repeatQuest` · `414_passQuest` · `420_achievement` | | Meta | 7.9.2 | 08 |
| | `430~498` | | Meta | [08](08_Content.md) | 08 |
| | `499_contentConfig` | 세로(key·value). 묶음 이름은 group 000 행 | Meta, Content | [08](08_Content.md) | 08 |
| 7xx | `700_productTab` · `701_product` · `702_shopSection` | | UI, Platform | 7.9.4 | 08 |
| 8xx | `801_drop` · `802_randomBox` · `803_randomGuaranteeLink` · `804_randomGuarantee` | DropTable · RandomBoxTable · GuaranteeLinkTable · GuaranteeTable(어댑터 `MvDropSource`) | Run, Meta | 7.9.3 | 07(적 드롭·천장), 08 |
| 문구 | Localization `name`·`description`·`ui`·`msg`·`toastMessage` | `LocalizationManager` | 전체 | 7.12 | 07(규칙), 05·08(문구) |

### 7.2.2 참조 관계(외래 키)

```text
302_area ─zoneId→ 301_zone     ─themeId→ 304_theme     ─floorGroup→ 303_areaFloor.group
302_area ─bgm→ 03_sound.key    ─clearReward*→ 07_consumeType(+idTable)
312_templateCycle ─templates→ 310_template        311_templateObject ─templateId→ 310_template
311_templateObject ─kindId→ 105_objectKind        311_templateObject ─groupId→ 314_spawnGroup.group
314_spawnGroup ─kindId→ 105_objectKind
105_objectKind ─hitReactionId→ 106  ─chargeGateId→ 107  ─receiveId→ 108  ─moveAiId→ 109  ─hpRuleId→ 114
105_objectKind ─enemyId→ 102_enemy     102_enemy ─hpRuleId→ 114  ─moveAiId→ 109  ─dropId→ 801_drop.dropGroup
105/102 ─hitSound·breakSound→ 03_sound.key     ─hitEffect·breakEffect→ 04_effect.key
313_fieldGen ─wallKindId·archKindId·archGoalKindId·bonusCoinKindId·bonusGemKindId→ 105_objectKind
110_card ─effectId→ 112_cardEffect  ─tierId→ 115_cardTier     111_cardPool ─godId→ 113_god  ─cardId→ 110_card
10/13/14 ─levelPriceGroup→ 16_levelPrice.group    16_levelPrice ─priceType/priceId→ 07_consumeType(+idTable)
08_adPlacement ─adReward*→ 07_consumeType         05_contentOpen ─contentId→ 400_content
400_content ─openId→ 05_contentOpen  ─modeRuleId→ 402  ─stageGroup→ 401.group  ─spendResource*→ 01/02  ─hudId→ (HUD 이름, 05)
08_adPlacement[modeTicket] ─adReward→ 02_playResource(입장권)     09_uiResource ─atlas/sprite/address→ 리소스(§7.10)
801_drop·802_randomBox ─rewardType/rewardId→ 07_consumeType(+idTable)   (50 → 801.dropGroup, 8 → 802.group, 순환 금지·깊이 ≤ 5)
803_randomGuaranteeLink ─guaranteeId→ 804   ─sourceBoxGroup→ 802.group     804 ─rewardBoxGroup→ 802.group
07_consumeType ─idTable→ (표 파일 이름 문자열)
```

---

## 7.3 설정·밸런스 표

### 7.3.1 00_config — 앱 전역 1행(메타 상수)

가로 1행. MV.Meta·MV.UI가 읽는다. 런 시뮬(MV.Run)이 쓰는 상수는 여기 두지 않고 `300_runConfig`에 둔다(모듈 경계: [10](10_Architecture.md)).

| 열 | 타입 | 값 | 출처·근거 | 뜻 |
|---|---|---|---|---|
| id | int | 1 | | 1행 고정 |
| dataVersion | int | 1 | [창작] | 표 묶음 버전. QA 캡처·오류 보고에 기록 |
| startDeck | intList | `0/1/0/0/0/0/0/0/0` | [프로토] L844 DEF.deck | 새 저장의 시작 덱(인덱스 = 무기 등급, 0 미사용) |
| startGodKey | string | `thunder` | [프로토] L844 | 새 저장의 출전 신(`113_god.key`) |
| collectAdMul | int | 3 | [프로토] L2445 | 결과 「×3 받기」 표시 배율. 실제 배율 정본은 `08_adPlacement[collect3].mul`(다르면 검증 경고) |
| interstitialMinArea | int | 3 | [프로토] L2444 | 「그냥 받기」(×1) 뒤 전면 광고 조건 `save.area ≥ 3`(0기반, 개방 반영 후, [04 D-24](04_GameRules.md)) |
| rebirthArmSec | float | 2.5 | [프로토] L2576 | 부활 버튼 두 번 누름 허용 시간. 04의 `RB_ARM_SEC` |
| rebirthCooldownSec | int | 21600 | [프로토] L2537 `RB_CD` 6시간 | 부활 쿨타임. 04의 `RB_CD_SEC` |
| adDayUtcOffsetHour | int | 0 | [프로토] L2423 `toISOString` | 광고 일일 횟수 초기화 시각대. 0 = UTC 0시([04 D-29](04_GameRules.md) 권장 A). 저장 형식은 [09](09_Save.md) |
| progressResetArmSec | float | 2.5 | [프로토] L3873 | 진행 초기화 두 번 누름 시간(개발 빌드, [09 D-69](09_Save.md)) |
| defaultMute | bool01 | 0 | [프로토] | 첫 실행 음소거 |

### 7.3.2 06_balancePreset — 밸런스 프리셋 3종

| 열 | 타입 | 뜻 |
|---|---|---|
| id | int | 1, 2, 3 |
| key | string | `easy` / `mid` / `orig` |
| sort, active | int | |
| rate | float | 초당 발사 기준(`iv = 1/(rate × …)`) |
| scale | float | 게이트·제작·장애물 HP 배율 |
| diff | float | 계단 게이트 감쇠 `1/(1 + n × diff)` |
| gauge | float | 우물 게이지 필요량 배율 |
| isDefault | bool01 | 1행만 1 |

| id | key | rate | scale | diff | gauge | isDefault | 이름(`name` 키 = id) | 근거 |
|---|---|---|---|---|---|---|---|---|
| 1 | easy | 2.0 | 3 | 0.02 | 1 | 1 | 쉬움(기본) | [프로토] L832~833, D-03 |
| 2 | mid | 1.4 | 5 | 0.01 | 1.4 | 0 | 임시 기본 | [프로토] L832 |
| 3 | orig | 0.95 | 10 | 0 | 2 | 0 | 원작 근사 | [프로토] L832 |

- 릴리스는 isDefault 고정. 개발 빌드 디버그 패널만 바꾼다([05](05_Screens.md)). 디버그 연사 슬라이더(0.95~4, 0.05 간격, L717)는 디버그 UI 상수라 표에 두지 않는다.
- 적용되는 곳·안 되는 곳은 [04 §4.15](04_GameRules.md).

### 7.3.3 300_runConfig — 런 상수(세로 키/값)

| 열 | 타입 | 뜻 |
|---|---|---|
| id | int | `300{그룹 3}{순번 3}` |
| key | string | 키 이름. 그룹 001~008은 [02](02_Visual.md)·[06](06_Effects.md)의 소문자 점 표기(`view.fogStart`), 그룹 101~105는 [04](04_GameRules.md)의 UPPER_SNAKE(`RUN_SPEED`), 그룹 106은 [05](05_Screens.md)의 `ui.hand.*` |
| value | string | **문자열로 저장**. `unit = color`면 색, `/`가 있으면 리스트, 그 밖은 double로 읽는다 |
| unit | string | 단위(`m`, `s`, `ms`, `color`, `배` …) |
| #근거 | string | 정본 줄·태그(로더 무시) |

**그룹 배정(겹침 금지)**

| 그룹 | 접두 | 소유 문서 | 키 수 | 내용 |
|---|---|---|---|---|
| 001 | `time.*` | [02 §2.8](02_Visual.md) | 3 | dtCap 0.05, hitStopScale 0.12, targetFps 60 |
| 002 | `view.*` | [02 §2.2.6](02_Visual.md) | 25 | 컬링·안개·빨랫줄·기준 해상도·외곽선색 |
| 003 | `sort.*` | [02 §2.3](02_Visual.md) | 7 | 깊이 정렬 편향 |
| 004 | `shake.*.amp/.time` | [06 §6.6](06_Effects.md) | 18 | 흔들림 9종 |
| 005 | `hitstop.*` | [06 §6.6](06_Effects.md) | 3 | enemyKill 0.03, wallBreak 0.06, coinBreak 0.03 |
| 006 | `fx.*` | [06](06_Effects.md) | 6 | 입자 상한, brickFlushInterval 0.22 등 |
| 007 | `pop.*`, `fly.*`, `flyCard.*` | [06](06_Effects.md) | 18 | 숫자 팝업·비행 |
| 008 | `banner.*`, `toast.*`, `flash.*`, `warp.*` | [06](06_Effects.md) | 15 | 배너·토스트·플래시·우다다 화면 |
| 101~106 | UPPER_SNAKE | **이 문서**(값 근거는 04) | 아래 | 런 규칙 상수 |

그룹 001~008의 키·값은 02·06 표가 정본이고 이 문서에 옮겨 적지 않는다(두 곳에 값이 있으면 어긋난다). 런 규칙 그룹 101~106은 아래가 정본이다.

**그룹 101 이동·막힘** (`300101xxx`)

| key | value | unit | 근거 | 쓰는 곳 |
|---|---|---|---|---|
| DRAG_SCREEN_M | 7.2 | m | [프로토] L3843~3852 | 드래그: 게임 영역 폭 = 7.2m |
| LANE_X_MAX | 3.2 | m | [프로토] L3845, L2025 | 목표 x·현재 x 제한 |
| KEY_MOVE_SPEED | 9 | m/s | [프로토] L3833 | 키 이동 |
| MOVE_FOLLOW_K | 14 | 1/s | [프로토] L2025 | x 추종 |
| RUN_SPEED | 4 | m/s | [프로토] L2029 | 전진 |
| PHASE_RUN | 13 | 1/s | [프로토] L2029 | 달리기 위상(그림) |
| PHASE_BLOCKED | 4 | 1/s | [프로토] L2029 | 막힘 위상 |
| PHASE_CLIMB | 30 | 1/s | [프로토] L2264 | 계단 위상 |
| CLIMB_X_FOLLOW_K | 6 | 1/s | [프로토] L2264 | 계단 중 x 복귀 |
| PLAYER_HALF_W | 0.32 | m | [프로토] L2033 | 막힘·경고 폭 |
| WALL_FACE_GAP | 0.35 | m | [프로토] L2066 | 막힐 때 앞면과 간격 |
| WALL_FACE_EPS | 0.02 | m | [프로토] L2066 | 앞면 판정 여유 |
| WARN_DIST | 9 | m | [프로토] L2738 | 「전방 장애물!」 거리 |

**그룹 102 사격·피해** (`300102xxx`)

| key | value | unit | 근거 | 쓰는 곳 |
|---|---|---|---|---|
| FIRE_PHASE | 0/0.13/0.26/0.07/0.19 | s | [프로토] L1811 | 유닛별 첫 발사 위상(본체, 분신 1~4) |
| MAX_CLONES | 4 | 명 | [프로토] L830 | 분신 상한, clone 카드 후보 제외 |
| CLONE_X_MAX | 3.4 | m | [프로토] L2073 | 분신 x 제한 |
| DBL_SHOT_DX | 0.2 | m | [프로토] L2088 | 2연발 좌우 간격 |
| PROJ_SPAWN_DZ | 0.4 | m | [프로토] L2081 | 탄 출발 z |
| THROW_T | 0.12 | s | [프로토] L2086 | 던지기 자세(그림) |
| PROJ_SPEED | 20 | m/s | [프로토] L2094 `PSPD` | 탄 속도 |
| PROJ_RANGE | 22 | m | [프로토] L2094 `RANGE` | 기본 사거리(× (1 + aup.range × perLv)) |
| DMG_MIN | 1 | | [프로토] L1905 | 피해 하한 |
| DMG_MAX | 99999 | | [프로토] L1905 | 피해 상한 |
| INCOME_PER_LV | 0.05 | 배/단계 | [프로토] L1746 | goldMul의 생선 건조대 항 |
| ENEMY_X_MAX | 3 | m | [프로토] L1320 | 적 무리 생성 x 제한 |

**그룹 103 우물·수령** (`300103xxx`) — 시간 지연의 정본은 `108_receiveCondition.delayMs`

| key | value | unit | 근거 | 뜻 |
|---|---|---|---|---|
| WELL_FLUSH_BEHIND | 0.5 | m | [프로토] L1471 | 우물이 플레이어 뒤로 지나면 즉시 배출 |
| WELL_BRICK_MAX | 4 | 개 | [프로토] L1471 | 조각 수 상한 |
| WELL_BRICK_DIV | 0.1 | 배 | [프로토] L1471 | 조각 1개 기준 = gaugeNeed × 0.1 |
| WELL_BRICK_LIFE | 0.26 | s(게임) | [프로토] L1872 | 조각 튐 |
| ORE_ICON_GAP_MS | 60 | ms | [프로토] L1878~1886 | 벽돌이 안 보일 때 아이콘 간격 |
| CRAFT_ICON_MAX | 5 | 개 | [프로토] L2051 | 공방 비행 아이콘 수 상한 |

**그룹 104 레벨·필드·정산** (`300104xxx`)

| key | value | unit | 근거 | 뜻 |
|---|---|---|---|---|
| FLOOR_COUNT | 3 | 개 | [프로토] L1790 | Area당 골목 수 |
| CP_MAX | 2 | | [프로토] L2401 | 체크포인트 상한(골목 3은 항상 플레이) |
| PLAYABLE_AREAS | 5 | 개 | [프로토] L757 `PROTO_AREAS` | 이 번호 Area가 마지막(zoneEnd). 결정 대기(D-63, 04 D-26) |
| TOK_BASE | 1 | 개 | [프로토] L2407 | 목숨 조각 기본 |
| TOK_SOUL_DIV | 4 | 생선/개 | [프로토] L2407 | floor(souls/4) |
| TOK_GATE | 2 | 개 | [프로토] L2407 | Area 아치 도달 |
| TOK_OPEN | 3 | 개 | [프로토] L2407 | 개방(pass·zoneEnd) |
| FAIL_SFX_DELAY_MS | 350 | ms | [프로토] L2408 | 실패 소리 지연 |
| LOBBY_STAIR_MAX_LV | 4 | Lv | [프로토] L810 `LOBBY_CANNON_MAX` | 캣타워로 올리는 시작 계단 Lv 상한 |
| WEAPON_EXTRA_BUYS | 60 | 회 | [프로토] L818 | 무기 Lv 8 이후 추가 구매 수(16_levelPrice 행 생성에만) |
| ARCH_LABEL_HIDE | 3 | m | [프로토] L1479 | 아치 글자판 숨김 거리(MV.View) |
| MERGE_N | 3 | 장 | [프로토] L1166, L819 | 손패·덱 머지 장수(3장 → 다음 등급 1장) |

**그룹 105 카드 효과 범위** (`300105xxx`) — 효과별 배율·주기는 `112_cardEffect.p1~p4`

| key | value | unit | 근거 |
|---|---|---|---|
| ORB_COUNT | 3 | 개 | [프로토] L1842 |
| ORB_RADIUS | 1.5 | m | [프로토] L2140 |
| ORB_ANG_SPEED | 4 | rad/s | [프로토] 그림 |
| ORB_Z_BACK | 1.4 | m | [프로토] L2141 |
| BURN_TICK | 0.5 | s | [프로토] L1989 |
| CHAIN_FIRE_BOX | 2.4 | m | [프로토] L1997 |
| STORM_LIFE | 1.7 | s | [프로토] L2160 |
| STORM_K0 | 0.12 | s | [프로토] L2162 |
| STORM_DK | 0.17 | s | [프로토] L2162 |
| STORM_PICK_DZ | 7 | m | [프로토] L2165 |
| STORM_BOX | 1.3 | m | [프로토] L2168 |
| BLAST_BOX | 3.6 | m | [프로토] L2177 |
| METEOR_MIN_DZ | 2 | m | [프로토] L2128 |
| METEOR_MAX_DZ | 20 | m | [프로토] L2128 |
| METEOR_FALL | 0.6 | s | [프로토] L2210 |
| METEOR_BOX | 3.2 | m | [프로토] L2212 |
| BLADE_ANG_SPEED | 5 | rad/s | [프로토] L2146 |

**그룹 106 손패 연출 시간** (`300106xxx`) — 키 이름은 [05 §5.3](05_Screens.md) `ui.hand.*`

| key | value | 근거 |
|---|---|---|
| ui.hand.budget | 3 | [프로토] L1162 한 묶음(전달+머지) 시간 예산(초) |
| ui.hand.leftMin | 0.4 | [프로토] L1171 남은 예산 하한 |
| ui.hand.dealCost | 0.12 | [프로토] L1172 1장 전달 = 0.12단위 |
| ui.hand.unitMin | 0.07 | [프로토] L1173 1단위 시간 하한 |
| ui.hand.unitMax | 0.54 | [프로토] L1173 1단위 시간 상한 |
| ui.hand.dealGapMax | 0.05 | [프로토] L1180 전달 간격 = min(0.05, u × dealGapMul) |
| ui.hand.dealGapMul | 0.12 | [프로토] L1180 `u×0.12`(05 표에 없던 계수. 값이 dealCost와 같아도 뜻이 달라 따로 둔다) |
| ui.hand.mergeMul | 0.92 | [프로토] L1188 머지 시간 md = u × 0.92 |
| ui.hand.moveMul | 0.9 | [프로토] L1189 모으는 이동 = md × 0.9 |
| ui.hand.afterMax | 0.04 | [프로토] L1194 머지 뒤 쉼 = min(0.04, u × afterMul) |
| ui.hand.afterMul | 0.08 | [프로토] L1194 |

머지 장수(3장 → 1장)는 연출 값이 아니라 규칙이라 그룹 104 `MERGE_N`(3, [프로토] L1166·L819, 덱 머지도 같은 값)에 둔다.

키 수: 그룹 101 = 13, 102 = 12, 103 = 6, 104 = 12(`MERGE_N` 포함), 105 = 17, 106 = 11 → **이 문서 소유 키 71개**. 02·06 그룹 001~008 = 95개. 합계 **166개**.

**04 본문 이름 → 실제 키(같은 값을 두 곳에 두지 않는다)**

| 04 본문 표기 | 실제 위치 | 이유 |
|---|---|---|
| `DT_CAP` / `HITSTOP_SCALE` | `time.dtCap` / `time.hitStopScale` | 02 정본 |
| `HITSTOP_ENEMY` / `HITSTOP_WALL` / `HITSTOP_BOX` | `hitstop.enemyKill` / `hitstop.wallBreak` / `hitstop.coinBreak` | 06 정본. 히트스톱은 게임 시간에 영향을 주므로 MV.Run도 이 키를 읽는다 |
| `SHAKE_WALL_BREAK`·`SHAKE_BLOCKED`·`SHAKE_STEAL`·`SHAKE_LAP`·`SHAKE_TIER`·`SHAKE_WARP`·`SHAKE_BLAST` | `shake.wallBreak`·`shake.blocked`·`shake.steal`·`shake.climbLap`·`shake.climbTier`·`shake.warpStage`·`shake.godBlast` (`.amp`/`.time`) | 06 정본 |
| `WELL_FLUSH_SEC` | `fx.brickFlushInterval` | 06 정본(값 0.22, 실시간) |
| `WELL_BRICK_FLY` / `FLY_MS` | `108_receiveCondition[wellGauge]`·`[sgatePass]`.`delayMs`(규칙) + `fly.removeMs`(그림) | 규칙 지연은 108이 정본. 그림 값이 다르면 검증 경고 |
| `CRAFT_ICON_GAP_MS` | `fly.cloneGapMs` | 06 정본 |
| `FOG_START`/`FOG_LEN` | `view.fogStart`/`view.fogRange` | 02 정본 |
| `RB_OPEN_AREA` | `05_contentOpen[rebirth]`(conditionType 1 = areaNo 2, sticky) | 해금은 해금 표 한 곳에서(§7.8.9) |
| `RB_ARM_SEC` / `RB_CD_SEC` | `00_config.rebirthArmSec` / `rebirthCooldownSec` | 메타 상수(04 §4.19.4 요청) |
| `RL_*` 8개 | `330_climb` 설정 행의 `rl*` 열 | 04 §4.19.4 요청 |
| `GAUGE_MAX` / `CRAFT_CAP` | `107_chargeGate[well].cap` / `[cgate].cap` | 컴포넌트 값 |
| `LAUNCH_AREAS` | 두지 않음 | 정본에서 규칙에 쓰지 않음 |

규칙: 코드는 키를 상수 클래스 `RunConfigKeys`로만 쓴다. 검증기가 그 목록과 TSV를 대조해 누락·남는 키를 오류로 낸다. MV.Run은 런 시작 때 값을 `RunConstants` 구조체로 한 번 복사한다(프레임마다 사전 조회 금지).

### 7.3.4 320_camera — 카메라 프리셋

MV.View 소유. 열 정의와 값은 [02 §2.2.2](02_Visual.md)를 그대로 옮긴다. 식은 02가 정본.

| 열 | 타입 | 뜻 |
|---|---|---|
| id, key | int, string | `run` / `climb` / `lobby` |
| anchor | enum | `Player`(플레이어 기준) / `World`(월드 좌표 그대로) |
| posXMul / posY / posZ | float | 위치 = `(px·posXMul, camLift + posY, pz + posZ)` |
| lookXMul / lookY / lookZ | float | 주시점 = `(px·lookXMul, camLift + lookY, pz + lookZ)` |
| posLerp | float | 위치 보간 `min(1, dt·posLerp)` |
| snap | bool01 | 1이면 즉시 |
| liftLerp | float | 그 모드의 camLift 계수 |
| blendLerp | float | 그 모드로 들어갈 때 ck 계수 |
| fovV | float | 9:16 기준 세로 FOV |
| near / far | float | |

| id | key | anchor | posXMul | posY | posZ | lookXMul | lookY | lookZ | posLerp | snap | liftLerp | blendLerp | fovV | near | far |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 320001001 | run | Player | 0.25 | 13 | −9 | 0.25 | 0.5 | 5.5 | 10 | 0 | 6 | 2 | 52 | 0.1 | 400 |
| 320001002 | climb | Player | 0.25 | 15 | −10.5 | 0.25 | 0.5 | 5.5 | 10 | 0 | 12 | 3 | 52 | 0.1 | 400 |
| 320001003 | lobby | World | 0 | 4.2 | −4.4 | 0 | 0.2 | 3.4 | 0 | 1 | 0 | 0 | 52 | 0.1 | 400 |

### 7.3.5 330_climb — 계단 오르기·룰렛·워프 단계

한 TSV에 두 종류 행을 둔다. `rowType` 열로 구분하고, 그 종류에 쓰지 않는 열은 빈칸.

| 열 | 타입 | 뜻 |
|---|---|---|
| id | int | 설정 행 `330000001`, 워프 단계 `330001001`~ |
| rowType | enum | `Config` / `WarpStage` |
| key | string | 설정 행 `default` (모드가 다른 오르기를 쓰면 행 추가 + `402_modeRule.climbKey`) |

**설정 행(`rowType=Config`)** — 의미는 [04 §4.8](04_GameRules.md)

| 열 | 값 | 근거 | 뜻 |
|---|---|---|---|
| lap | 75 | [프로토] L773, L2274 | 랩 1회 계단 수 |
| maxCards | 3 | [프로토] `MAX_CLIMB_CARDS` | 한 번 오를 때 카드 최대 |
| lapTier | 4 | [프로토] L2276 | 랩 카드 등급(LEGENDARY) |
| kDirectMax / kCompress / kMax | 60 / 3 / 150 | [프로토] L2246 | 칸 압축 |
| stepH / stepD / stepW | 0.42 / 0.72 / 8.6 | [프로토] L2246 | 칸 높이·깊이·폭(m) |
| z0Gap | 1.4 | [프로토] L2246 | 첫 칸 z = 계단 z + 1.4 |
| shiftFrom / shiftExtra | 0.5 / 8 | [프로토] L2226, L2247 | 오브젝트 제거 기준, 밀어내기 여분 |
| landGap | 0.2 | [프로토] L2290 | 착지 z = zEnd + 0.2 |
| tBase / tPerStep / tMax | 1.6 / 0.05 / 9 | [프로토] L2259 | 오르는 시간 min(9, 1.6 + 0.05k) |
| accelP | 0.12 | [프로토] L2263 | 처음 12% 가속 |
| riseMin / risePerK / riseAhead | 30 / 1.6 / 6 | [프로토] L2265~2266 | 칸 등장 |
| dropSec / dropH | 0.2 / 5 | [프로토] L2267 | 칸 낙하 연출 |
| tickEvery | 4 | [프로토] L2270 | 칸 4개마다 tick 소리 |
| rlEndBase / rlEndStep | 1.0 / 0.45 | [프로토] L2362 | 카드 i 확정 = 1.0 + 0.45i(게임 초) |
| rlFlipSec | 0.06 | [프로토] L2367 | 얼굴 넘김 간격 |
| rlSlowWin / rlSlowK | 0.4 / 0.35 | [프로토] L2367 | 마지막 0.4초 감속 |
| rlFlyDelay | 1.5 | [프로토] L2371 | 확정 → 비행(게임 초) |
| rlFlyMs | 620 | [프로토] L2375 | 비행 → 적용(실시간 ms). 그림 `flyCard.removeMs`와 같아야 함(검증 경고) |
| rlEndPad | 0.3 | [프로토] L2391 | 전부 도착 후 종료 여유 |

**워프 단계 행(`rowType=WarpStage`)** — 정본 `WARP_ST` L3155, 값 정본 [06 §6.11.1](06_Effects.md)

| 열 | 타입 | 뜻 |
|---|---|---|
| at | int | 표시 칸 수 문턱 |
| accent / sky1 / sky2 / sky3 | color | 단계 색 |
| night | float | 밤 정도 |
| enterEffect | effect | 단계 진입 이펙트(`climb.stage`) |
| enterSound | sound | 단계 진입 소리(`tierup_{n}` 규칙은 06) |

| id | at | 이름(`name`) | accent | sky1 | sky2 | sky3 | night |
|---|---|---|---|---|---|---|---|
| 330001001 | 0 | 우다다 시작 | #ffd84a | #8ac8f0 | #c8e4f4 | #f4ead8 | 0 |
| 330001002 | 25 | 지붕 위 질주 | #ff9a5a | #e8806a | #f8b880 | #ffe0b0 | 0.2 |
| 330001003 | 75 | 달빛 계단 | #9ab8ff | #1c1e4e | #3a3a7a | #6a5a9a | 0.65 |
| 330001004 | 150 | 보름달 점프 | #fff0a0 | #0e1032 | #2a2a6a | #4a4a8a | 0.8 |

등급 문턱(5/10/20/40/75)과 등급색은 `115_cardTier`.

---

## 7.4 세계 표 — Zone·Area·Floor·테마

행 값(30 Area 수치·이름·테마 배정)은 [08 §8.2](08_Content.md)가 정본이다. 이 절은 열 정의, 계산 규칙, 프로토 테마 5행을 정한다. 08이 정한 열 이름을 그대로 확정한다.

### 7.4.1 301_zone

| 열 | 타입 | 기본 | 뜻 |
|---|---|---|---|
| id | int | | `301001001`~`301001006` |
| sort, active | int | | 지도 순서(아래 → 위), 1/0 |
| areaFrom / areaTo | int | | `302_area.areaNo` 범위. 검증: Zone끼리 겹치지 않고 빈틈 없음 |
| mapIcon | res | 공용 | 지도 Zone 머리 아이콘. 기본 아틀라스 `Atlas_UI_Lobby` |
| mapCol1 / mapCol2 | color | | 지도 섹션 색. [프로토] L2580 `ZONE_COL`(Zone 1~3). [05](05_Screens.md)의 `mapColor1/mapColor2`가 이 열이다(이름은 행 정본인 08과 맞춤) |
| bgm | sound | 빈칸 | 비우면 Area 값, 그것도 비면 `bgm_run` |

행 6개(이름·색·범위)의 정본은 [08 §8.2.1](08_Content.md). `mapIcon` 값은 03 규칙 `UI_Icon_Map_Zone{n}`으로 읽는다.

zoneIndex(0기반, 보상 `ValuePerZone` 계산) = Zone을 sort 순으로 늘어놓은 위치. 프로토 `zoneOf(a) = floor(a/5)`(L758)와 같은 결과다.

### 7.4.2 302_area

| 열 | 타입 | 기본 | 뜻 |
|---|---|---|---|
| id | int | | `302001001`~ (순번 = areaNo) |
| areaNo | int | | 1~. `save.area + 1`. 검증: 1부터 빈틈 없음 |
| zoneId | fk 301 | | |
| sort, active | int | | active 0이면 지도에 「업데이트 예정」(L2589). 진행 끝은 `PLAYABLE_AREAS`(D-63) |
| themeId | fk 304 | | 정본 `AREA_TH[area % 5]`(L760)를 행마다 명시 |
| hp | int | | `R.Z.hp`. [프로토] L755 `AREA_HP` |
| needSouls | int | | 다음 Area 개방 누적 생선. [프로토] L756 |
| floorGroup | int | areaNo | `303_areaFloor.group`. **04 §4.19.4의 `floorSetId`와 같은 열**(이름은 08과 맞춰 `floorGroup`) |
| fieldSeedArea | int | areaNo − 1 | fieldSpec 시드에 넣는 A(0기반). 정본은 「클램프 전 A」 = areaNo − 1 |
| bgm | sound | 빈칸 | 비우면 Zone 값 |
| mapIcon | res | 빈칸 | 지도 Area 배너 아이콘(기본 아틀라스 `Atlas_UI_Lobby`) |
| bg | res | 빈칸 | 배너 배경(`Tex_*` 주소, AddressableTexture) |
| clearRewardType / Id / Value | consume | 빈칸 | 개방 순간 보상. 정본에 없음 → 빈칸 [프로토] |
| firstClearRewardType / Id / Value | consume | 빈칸 | 첫 Area 아치 도달 보상(확장용) |

### 7.4.3 303_areaFloor

| 열 | 타입 | 기본 | 뜻 |
|---|---|---|---|
| id | int | | `303{group 3}{floor 3}` (예 `303001002`). 모드용 묶음 901~ |
| group | int | | `302_area.floorGroup` |
| floor | int | | 1~3 (`FLOOR_COUNT`) |
| h0 / h1 | int | | 첫 줄 / 마지막 줄 칸 HP(scale 곱하기 전). [프로토] L761~767 `FINAL` |
| rows | int | | 줄 수(한 줄 = 3칸) |
| souls | int | | 그 Floor 생선 합 |
| cellMode | enum | `Hash` | `Hash` = 런타임 fieldSpec 해시(정본, [04 §4.14.3](04_GameRules.md)) / `Baked` = 아래 세 열 사용(D-61) |
| soulCells | intList | 빈칸 | Baked: 생선 칸 idx(0기반, r = idx/3, l = idx%3) |
| soulAmts | intList | 빈칸 | Baked: 칸별 생선 수(합 = souls) |
| diaCells | intList | 빈칸 | Baked: 캣닢 칸 idx |

줄 r(0기반) HP: `hp = round((h0 + (h1 − h0) × r / max(1, rows − 1)) × scale)`, 칸 최대 = `max(1, round(hp × obstMul()))`. 칸 보상 값은 `313_fieldGen`.

**굳힘 값·골든(Area 1~15)** — 프로토 `fieldSpec`을 node로 돌린 결과. `cellMode=Baked`로 바꿀 때 그대로 넣고, Hash 모드 EditMode 시험의 골든으로도 쓴다(04 D-27). 형식 `soulCells | soulAmts | diaCells`.

| 묶음 | Floor 1 | Floor 2 | Floor 3 |
|---|---|---|---|
| 1 | 4/8/15 \| 3/3/4 \| 9/16/18 | 3/8/21/22 \| 4/4/4/5 \| 1/2/6/23/27/29 | 10/16/21/35 \| 5/6/6/6 \| 1/20/28/32/34 |
| 2 | 6/9/15 \| 3/3/4 \| 0/1/22/23 | 5/13/21/29 \| 4/4/4/5 \| 14/16/17 | 10/22/24/29 \| 5/6/6/6 \| 2/5/19/26 |
| 3 | 6/25/26/28 \| 3/3/4/4 \| 2/12 | 11/12/18/33 \| 5/5/5/5 \| 10/17/21/24/27/29 | 6/9/11/23/41 \| 5/5/5/5/6 \| 2/19/27/35 |
| 4 | 4/5/25/29 \| 3/3/4/4 \| (없음) | 15/21/22/28 \| 5/5/5/5 \| 6/8/12/13/20/35 | 7/12/24/34/36 \| 5/5/5/5/6 \| 15/35 |
| 5 | 8/16/27/30 \| 4/4/5/5 \| 21 | 4/13/16/24/26 \| 4/4/5/5/5 \| 2/22/32 | 11/18/23/25/35/47 \| 4/5/5/5/5/5 \| 2/3/9/12/13/26/33/44/46 |
| 6 | 20/22/30/34 \| 4/4/5/5 \| 4/7/8/14/16/19/26/31 | 32/33/35/36/37 \| 4/4/5/5/5 \| 1/8/12/31 | 4/6/26/28/38/41 \| 4/5/5/5/5/5 \| 2/16/18/29 |
| 7 | 6/29/32/33/41 \| 3/3/4/4/4 \| 25/28 | 3/11/13/25/37/43 \| 3/4/4/4/4/4 \| 4/9/17/42/44 | 7/12/13/17/37/45 \| 4/5/5/5/5/5 \| 5/15/16/23/25/27/32/40/47/51 |
| 8 | 8/10/14/19/29 \| 3/3/4/4/4 \| 2/6/7/17/33 | 3/9/10/12/23/33 \| 3/4/4/4/4/4 \| 20/40 | 3/25/35/39/42/47 \| 4/5/5/5/5/5 \| 4/10/12/13/15/18/21/22/46/51/52 |
| 9 | 3/9/27/32/37 \| 3/3/4/4/4 \| 4/7/10/14/24/25 | 4/20/21/31/44/46 \| 3/4/4/4/4/4 \| 7/23/26/40 | 4/8/15/19/30/48 \| 4/5/5/5/5/5 \| 3/10/11/43/46/50/53 |
| 10 | 3/4/7/9/33 \| 3/3/4/4/4 \| 11/13/30/37/40 | 4/7/8/9/13/30 \| 3/4/4/4/4/4 \| 5/10/34/39/45 | 9/31/33/34/47/52 \| 4/5/5/5/5/5 \| 5/11/28/45/50 |
| 11 | 4/6/29/34/41 \| 3/3/4/4/4 \| 15/17 | 8/11/17/20/34/44 \| 3/4/4/4/4/4 \| 7/12/15/16/22/23/24/36/43 | 5/8/12/27/31/42 \| 4/5/5/5/5/5 \| 15/19/24/28/52 |
| 12 | 4/7/25/26/29 \| 3/3/4/4/4 \| 19/22/27/31 | 6/16/24/25/34/36 \| 3/4/4/4/4/4 \| 9/15/17/37/47 | 14/22/29/34/36/37 \| 4/5/5/5/5/5 \| 4/7/18/19/24/30/32/53 |
| 13 | 8/10/15/28/33 \| 3/3/4/4/4 \| 25/30/37/38 | 6/8/9/13/20/33 \| 3/4/4/4/4/4 \| 0/1/4/23/27/35/43/47 | 16/29/31/35/42/43 \| 4/5/5/5/5/5 \| 2/3/15/20/30/46/47 |
| 14 | 3/17/28/30/31 \| 3/3/4/4/4 \| 0/19/21/25/29/35 | 6/9/16/36/43/44 \| 3/4/4/4/4/4 \| 4/7/8/15/20/29/31/32/45 | 8/22/24/28/38/48 \| 4/5/5/5/5/5 \| 0/16/31/44 |
| 15 | 3/17/20/29/40 \| 3/3/4/4/4 \| 1/2/5/6/15/27/35/37/39 | 14/24/29/31/34/46 \| 3/4/4/4/4/4 \| 3/4/7/12/15/18/30/38/40/43 | 14/24/31/41/49/52 \| 4/5/5/5/5/5 \| 2/11/18/23/48 |

- 위 값은 **정본 FINAL(Area 1~15)과 시드 A = 0~14** 조합이다. 08이 Area 16~30에 원작 Floor 값을 넣으면(08 D-71) 그 칸은 Hash가 새로 계산한다. 16~30 골든은 MV-QA-HARNESS가 30×3 JSON으로 만든다.
- Baked 행을 여러 Area가 공유하면 그 Area들은 칸이 모두 같다(검증 경고).

### 7.4.4 304_theme

월드 그림 테마. Unity는 테마별 월드 아틀라스에 미리 구운 스프라이트를 쓰고, 색 열은 하늘·안개·땅 셰이더와 굽기 도구(MV-TOOL-ART) 입력이다([02](02_Visual.md), [03 §3.6](03_ResourcePipeline.md)).

| 열 | 타입 | 뜻 |
|---|---|---|
| id | int | `304001001`~ |
| key | string | `market` 등. 08 Area 표가 key로 적는다 |
| sort, active | int | |
| worldAtlas | res | 테마별 월드 아틀라스 주소 `Atlas_World_Theme_<key>`. **스프라이트 이름은 모든 테마에서 같다**(§7.10.4). 기본 아틀라스 값 `@theme`이 이 열을 뜻한다 |
| roadsideProps | strList | 길가 소품 스프라이트 이름 목록([03 §3.5.2](03_ResourcePipeline.md)) |
| lineKind | enum | 빨랫줄 종류(`Clothes` / `Bulbs` …, 03 §3.5.2) |
| skyTop / skyBot / fog | color | 하늘 위·아래, 안개 |
| grass / grass2 / road / road2 / stone | color | 땅 |
| hillFar / hillNear | color | 언덕 |
| walls / roofs | strList(color) | 집 벽·지붕 색 후보 |
| wallTop / door | color | |
| prop | string | 하늘 소품(`moon`) |
| leafSet | string | 잎 색 묶음(`LEAF`, L742) |
| deco | string | 장식 묶음 |
| tint | color | 전체 색조(빈칸 = 없음) |
| night | float | 0~1 |
| bgm | sound | 빈칸 = Area/Zone 값 |

프로토 5행 [프로토] L743~753 (`roadsideProps`·`lineKind` 값은 03 §3.5.2):

| 열 | 304001001 | 304001002 | 304001003 | 304001004 | 304001005 |
|---|---|---|---|---|---|
| key | market | stairs | harbor | roof | snow |
| worldAtlas | Atlas_World_Theme_market | Atlas_World_Theme_stairs | Atlas_World_Theme_harbor | Atlas_World_Theme_roof | Atlas_World_Theme_snow |
| skyTop | #8ac8f0 | #9ac0e8 | #e8906a | #1a1c48 | #141640 |
| skyBot | #d8ecf8 | #f0dcc8 | #ffd0a0 | #3a3a7a | #3a3a78 |
| fog | #e4dccf | #ddd0c6 | #e8c4a8 | #4a4870 | #5a5e8a |
| grass | #cbbba2 | #bdb4ac | #c8b0a0 | #a8a4c0 | #e8eef8 |
| grass2 | #c3b399 | #b5aca4 | #c0a898 | #a09cb8 | #e0e8f4 |
| road | #e4d8c4 | #d8d0c8 | #dcc8b4 | #c4c0d8 | #f6f8fc |
| road2 | #dacdb7 | #cec6be | #d2bea8 | #bab6d0 | #eef2f8 |
| stone | #a8947e | #9a928a | #9a8070 | #8a86a2 | #c0cade |
| hillFar | #a8b8c8 | #a8a8c0 | #c08a8a | #3a3a6a | #3a3a6a |
| hillNear | #c8b8a8 | #c0b0a8 | #d0a080 | #4a4a7a | #4a4a7a |
| walls | #f2e6d2/#ead2c2/#d8e2ea/#f6eaca | #ece4f2/#f2ded2/#dceae2/#fff2da | #f2e2d2/#d2e2f2/#eadab8/#f0d8d8 | #e0d8f0/#e8d8e4/#d0dcf0 | #dcdff2/#e6dff2/#d4e2f2 |
| wallTop | #a86a5a | #7a8a9a | #5a7a9a | #7a7a9a | #ffffff |
| roofs | #d0704a/#5a7ab8/#6a9a7a/#a87a6a | #5a7ab8/#d0704a/#6a9a7a/#7a7a8a | #4a6ab8/#e8604a/#3a8aa0/#c8a060 | #5a6ab8/#9a6a8a/#4a8a9a | #f4f8ff |
| door | #4a7ac8 | #3a8a7a | #e8604a | #5a7ad8 | #5a7ad8 |
| prop / leafSet | moon / LEAF | moon / LEAF | moon / LEAF | moon / LEAF | moon / LEAF |
| deco | market | stairs | harbor | roof | snow |
| tint | (빈칸) | #fff0de | #ffd6b8 | #9698da | #acb4e2 |
| night | 0 | 0 | 0.2 | 1 | 1 |

테마 6~15는 [08 §8.2.5](08_Content.md)(결정 대기 D-72). 새 테마는 행 추가 + 아틀라스 굽기(03 §3.6.4).

---

## 7.5 오브젝트 종류 — 컴포넌트 조합(핵심)

사용자 요구 「오브젝트 종류 컨트롤」의 답이다. **오브젝트 종류 하나 = `105_objectKind` 행 1개 + 행동 컴포넌트 행 참조.** 코드는 「행동 타입」(enum) 단위로만 있고, 종류(우물·적·게이트…)는 코드에 없다. 새 종류는 이미 있는 행동 타입의 조합이면 **행만 추가**하면 된다(§7.14.1). 새 행동 타입이 필요할 때만 코드를 늘린다.

### 7.5.1 구조

```text
105_objectKind (판정 플래그·크기·그림·소리·보상)
   ├─ hpRuleId        → 114_hpRule        IHpRule          처음 체력
   ├─ hitReactionId   → 106_hitReaction   IHitReaction     맞았을 때
   ├─ chargeGateId    → 107_chargeGate    IChargeGate      피해를 충전으로 바꿀 때(게이트·우물)
   ├─ receiveId       → 108_receiveCondition IReceiveCondition 보상·충전을 언제 받나(파괴·통과·묶음·도착·접촉)
   ├─ moveAiId        → 109_moveAI        IMoveAi          움직임·접촉 행동
   ├─ enemyId         → 102_enemy         (적이면 체력·이동·보상·그림을 102 행이 덮어씀)
   └─ reward*/hitReward* consumeData      IRunRewardSink   보상 지급
```

**인터페이스(MV.Run, 확정 이름)**

```csharp
public interface IHpRule           { double InitHp(in SpawnContext ctx, HpRuleData d); }
public interface IHitReaction      { void OnHit(RunObject o, in HitContext hit, IRunWorld w, HitReactionData d); }
public interface IChargeGate       { double Threshold(RunObject o, IRunWorld w, ChargeGateData d);
                                     void   AddCharge(RunObject o, double dmg, IRunWorld w, ChargeGateData d); }
public interface IReceiveCondition { void Tick(RunObject o, IRunWorld w, in FrameContext f, ReceiveConditionData d); }
public interface IMoveAi           { void Init(RunObject o, IRunRng rng, MoveAiData d);
                                     void Tick(RunObject o, IRunWorld w, double gdt, MoveAiData d); }
public interface IRunRewardSink    { void Grant(IReadOnlyList<ConsumeData> rewards, in RewardContext ctx); }
```

| 이름 | 어셈블리 | 역할 |
|---|---|---|
| `ObjectKindDef` | MV.Run | 105 행 + 참조 행 + 행동 구현을 묶은 불변 객체 |
| `ObjectKindRegistry` | MV.Run | `Build(MvTables)`: 105 전 행을 `ObjectKindDef`로 조립. 행동 타입 enum → 상태 없는 구현 객체(싱글턴) 사전 |
| `RunObject` | MV.Run | 런 안의 오브젝트 1개의 **상태**(kindDef, x, z, hp, max, dead, done, F, n, made, got, pend, ft, flash, punch, spd, rew/amt/idx/bar/row/lane, lvl). 행동 구현은 상태를 갖지 않는다 |
| `RunRewardSink` | MV.Run | `IRunRewardSink` 구현. 600번대 consumeType을 런 값(R.gold, R.souls, R.dia, R.stairs …)에 더하고 `Gain` RunEvent를 낸다. 1~599 type이 오면 정산 대기 목록에 넣는다(런 중 메타 재화 직접 변경 금지) |
| `ConsumeData` | MV.Data | consumeData 1항목(type, id, value, perZone, rule) |

**조립 규칙**: `ObjectKindRegistry.Build`는 각 컴포넌트 id가 0 또는 빈칸이면 「없음」 구현(`NoHp`, `NoReaction`, `NoCharge`, `ReceiveOnBreak`, `StaticMove`)을 넣는다. 타입 enum에 구현이 등록되지 않았으면 검증 오류(§7.13).

**판정 순서와 플래그 사용처**([04 §4.3](04_GameRules.md) 정본)

| 플래그 | 값 | 쓰는 곳 |
|---|---|---|
| `hittable` | `Always` / `BeforePass`(`!done && o.z ≥ P.z`) / `Never` | 투사체 명중 후보, 정전기 연쇄·불씨 대상 |
| `breakable` | bool01 | 범위 스킬(폭풍·하악질·위성·빛·유성·칼날) 대상. 우물은 1이라 범위 스킬로도 채워진다 |
| `blocks` | bool01 | 플레이어 전진 막힘(wall만 1) |
| `hw` / `hd` | m | 판정 반폭·반깊이 |

### 7.5.2 105_objectKind

| 열 | 타입 | 기본 | 뜻 |
|---|---|---|---|
| id | int | | `105001001`~ |
| key | string | | 코드·배치·편집기가 쓰는 이름 |
| sort, active | int | | |
| protoType | int | 빈칸 | 정본 `PLACE` 타입 번호(7·5·14·13·15·2·3). 정본 대조 시험용 |
| hw / hd | float | | 판정 크기 |
| hittable | enum | Always | |
| breakable / blocks | bool01 | 0 | |
| hpRuleId | fk 114 | 0 | |
| hitReactionId | fk 106 | 0 | |
| chargeGateId | fk 107 | 0 | |
| receiveId | fk 108 | 0 | 0 = OnBreak |
| moveAiId | fk 109 | 0 | 0 = Static |
| enemyId | fk 102 | 0 | 0이 아니면 102 행의 hpRuleId·moveAiId·보상·그림·소리가 이 행 값을 덮어쓴다 |
| rewardSource | enum | Row | `Row` = 이 행의 reward 열 / `FieldCell` = 필드 칸의 보상(wall, §7.6.4) / `Enemy` = 102 행 |
| rewardType / rewardId / rewardValue | consume | 빈칸 | 파괴(또는 receive 조건 성립) 보상 |
| rewardValuePerZone / rewardValueRule | §7.1.7 | 0 / Flat | |
| hitRewardType / hitRewardId / hitRewardValue | consume | 빈칸 | **맞을 때마다** 보상(상자 타격당 츄르 1) |
| sprite | res(`/` 변형) | | 본체 스프라이트. 기본 아틀라스 `@theme` |
| spritePick | enum | Single | `Single` / `JunkCycle`(wall: `v = (lane + row×2 + bar) % 3`로 목록에서 고르고 색 2종은 `idx % 2`, [03 §3.5.3](03_ResourcePipeline.md)) / `Anim`(프레임 접두 + 번호) |
| spriteSoul | res | 빈칸 | 보상이 생선(603)인 칸이면 이 스프라이트(`World_Obj_Junk_FishBox`) |
| tint | strList(color) | 빈칸 | 메쉬로 그리는 부분 색(게이트 배경, 관문 발판). [03 제안 열] |
| labelKind | enum | None | 글자판 종류: `None`·`WellText`·`StairText`·`StairGate`·`CraftGate`·`Arch`·`WallCell`·`EnemyHp`·`BoxHp`. 글자판 규칙은 [02](02_Visual.md)·[05](05_Screens.md) |
| labelKey | string | 빈칸 | 글자판 문구 `ui` 키(§7.12) |
| sortBias | float | 0 | 깊이 정렬 편향([02 §2.3](02_Visual.md)) |
| hitSound / breakSound | sound | 빈칸 | 맞음·파괴 소리(투사체 명중음 hit/crit는 투사체가 낸다) |
| hitEffect / breakEffect | effect | 빈칸 | 맞음·파괴 이펙트(06 키) |

**초기 행** [프로토] 근거: 크기·플래그 L1314~1331, L1783~1807, 소리 [03 §3.9.3](03_ResourcePipeline.md), 이펙트 [06 §6.4](06_Effects.md)

| id | key | protoType | hw / hd | hittable | breakable | blocks | hpRule | hitReaction | chargeGate | receive | moveAi | enemyId |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 105001001 | tower | 7 | 1.25 / 1.1 | Always | 1 | 0 | none | chargeWell | well | wellGauge | 0 | 0 |
| 105001002 | enemy | 5 | 0.6 / 0.5 | Always | 1 | 0 | (102) | damageEnemy | 0 | onBreak | (102) | 102001001 |
| 105001003 | sgate | 14 | 1.25 / 0.12 | BeforePass | 0 | 0 | none | chargeStairGate | sgate | sgatePass | 0 | 0 |
| 105001004 | cgate | 13 | 1.25 / 0.12 | BeforePass | 0 | 0 | none | chargeCraftGate | cgate | cgatePass | 0 | 0 |
| 105001005 | stair | 15 | 1.6 / 1.0 | Never | 0 | 0 | none | none | 0 | onArriveStair | 0 | 0 |
| 105001006 | coin | 2 | 0.9 / 0.7 | Always | 1 | 0 | box06 | damageBox | 0 | onBreak | 0 | 0 |
| 105001007 | gem | 3 | 0.9 / 0.7 | Always | 1 | 0 | box06 | damageBox | 0 | onBreak | 0 | 0 |
| 105001008 | coinBonus | (2) | 0.9 / 0.7 | Always | 1 | 0 | bonus025 | damageBox | 0 | onBreak | 0 | 0 |
| 105001009 | gemBonus | (3) | 0.9 / 0.7 | Always | 1 | 0 | bonus04 | damageBox | 0 | onBreak | 0 | 0 |
| 105001010 | wall | | 1.15 / 0.55 | Always | 1 | 1 | field | damageWall | 0 | onBreak | 0 | 0 |
| 105001011 | arch | | 4.6 / 0.3 | Never | 0 | 0 | none | none | 0 | onArriveFloor | 0 | 0 |
| 105001012 | archGoal | | 4.6 / 0.3 | Never | 0 | 0 | none | none | 0 | onArriveGoal | 0 | 0 |

(표의 컴포넌트 칸은 읽기 쉽게 key로 적었다. TSV에는 해당 표의 id를 쓴다. 예 `chargeWell` → `106001007`.)

| key | reward (Type / Value / PerZone / Rule) | hitReward | sprite / spritePick | labelKind / labelKey | hitSound / breakSound | hitEffect / breakEffect | 근거 |
|---|---|---|---|---|---|---|---|
| tower | — | — | `World_Obj_Well_Mouth` / Single | WellText / `ui.run.label.tower` | clank / — | run.towerHit / — | L1910~1913 |
| enemy | (102) | — | (102) | EnemyHp | (102) | (102) | L1921~1929 |
| sgate | — | — | `World_Obj_Gate_RollPaper` / Single, tint `#6fd27a` | StairGate / `ui.run.label.sgate` | — / — | — / — (통과 이펙트는 108) | L1945~1951, L3634 |
| cgate | — | — | `World_Obj_Gate_RollYarn_0/…_1/…_2/…_3` / Single, tint `#f08ab0` | CraftGate / `ui.run.label.cgate` | — / — | — | L1952~1956 |
| stair | — | — | `World_Obj_Stair_Step_0/…_1/…_2` / Single | StairText / `ui.run.label.stair` | — / — | — | L1435, L2032 |
| coin | 602 / 20 / 10 / Flat | 602 / 1 | `World_Item_Churu_0/…_1/…_2` / Single | BoxHp | — / coin | run.coinHit / run.coinBreak | L1960~1967 |
| gem | 604 / 10 / 0 / DiaCap | 602 / 1 | `World_Item_CatnipPot` / Single | BoxHp | — / gem | run.coinHit / run.coinBreak | L1960~1967 |
| coinBonus | 602 / 12 / 6 / Flat | 602 / 1 | coin과 같음 | BoxHp | — / coin | run.coinHit / run.coinBreak | L1788 |
| gemBonus | 604 / 1 / 0 / Flat | 602 / 1 | gem과 같음 | BoxHp | — / gem | run.coinHit / run.coinBreak | L1788 |
| wall | (FieldCell) | — | `World_Obj_Junk_Parcel/World_Obj_Junk_Bag_Dark/World_Obj_Junk_Bag_Blue/World_Obj_Junk_Crate_Red/World_Obj_Junk_Crate_Blue` / JunkCycle, spriteSoul `World_Obj_Junk_FishBox` | WallCell | — / wood | run.wallHit / run.wallBreak | L1932~1941, L3610~3618 |
| arch | — | — | `World_Obj_Arch_Post` / Single, tint 발판색 03 | Arch / `ui.run.floorN` | — | — | L1791 |
| archGoal | — | — | `World_Obj_Arch_Post_Gold` / Single | Arch / `ui.run.areaGate` | — | — | L1801 |

- 파괴 보상 소리: wall은 보상 종류에 따라 `soul`/`gem`/`coin` 소리를 더 낸다(L1937~1941). 이것은 `07_consumeType.gainSound`(§7.8.12)로 정한다 — 종류별 「얻음」 소리는 consumeType 행이 가진다.
- `gem` 행의 `rewardValue = 10`은 `DiaCap` 규칙이라 무시되지만 `14_rebirthPerk[dcap].base`와 같게 적는다(다르면 검증 경고).
- 츄르(602)는 지급 때 `goldMul()`이 곱해진다(타격당 1 포함, [04 §4.3.3](04_GameRules.md)).
- `arch`·`archGoal`의 hw 4.6은 기둥 위치(±4.6, 그림)다. hittable Never라 판정에 쓰지 않는다.
- `stair`는 정본에서 피격 소리 코드(L1919)가 있으나 hittable이 아니어서 울리지 않는다(잔재, [04 §4.16](04_GameRules.md)).

### 7.5.3 106_hitReaction

| 열 | 타입 | 뜻 |
|---|---|---|
| id, key | | |
| type | enum | `None` / `Damage`(hp −= 피해, 0 이하면 파괴) / `ChargeStairGate` / `ChargeCraftGate` / `ChargeWell`(피해를 107 컴포넌트에 넘김) |
| flash | bool01 | 맞으면 섬광(그림) |
| punch | float | 맞으면 punch 값(그림 크기 출렁, [02](02_Visual.md)) |
| objShake | float | 맞으면 오브젝트 흔들림 값 |
| breakHitstop | string | 파괴 시 히트스톱 키(`300_runConfig` `hitstop.*`) |
| breakShake | string | 파괴 시 화면 흔들림 키 앞부분(`shake.wallBreak` → `.amp`/`.time`) |
| breakStat | enum | 파괴 시 세는 런 통계: `None` / `Walls`(R.walls) / `Kills` / `Boxes` — 퀘스트(`413_repeatQuest.clearType`)가 읽는다. Kills·Boxes는 [창작] 집계(정본은 walls만) |

| id | key | type | flash | punch | objShake | breakHitstop | breakShake | breakStat | 근거 |
|---|---|---|---|---|---|---|---|---|---|
| 106001001 | none | None | 0 | 0 | 0 | | | None | |
| 106001002 | damageEnemy | Damage | 1 | 1 | 0 | hitstop.enemyKill | | Kills | L1921~1929 |
| 106001003 | damageWall | Damage | 1 | 0 | 1 | hitstop.wallBreak | shake.wallBreak | Walls | L1932~1941 |
| 106001004 | damageBox | Damage | 1 | 1 | 0 | hitstop.coinBreak | | Boxes | L1960~1967 |
| 106001005 | chargeStairGate | ChargeStairGate | 1 | 0 | 0 | | | None | L1945~1951 |
| 106001006 | chargeCraftGate | ChargeCraftGate | 1 | 0 | 0 | | | None | L1952~1956 |
| 106001007 | chargeWell | ChargeWell | 0 | 1 | 0 | | | None | L1910~1913 |

flash·punch·objShake의 시간·크기 값은 [02](02_Visual.md)·[06](06_Effects.md). 이 표는 「켤지」만 정한다.

### 7.5.4 107_chargeGate

| 열 | 타입 | 뜻 |
|---|---|---|
| id, key | | |
| gateType | enum | `StairGate` / `CraftGate` / `Well` |
| base | float | 기준량 |
| step | float | 단계 증가량(growth에 따라 더하기 또는 곱하기) |
| growth | enum | `None` / `Add`(base + step·k) / `Pow`(base · step^(lv−1)) |
| cap | int | 단계 상한(공방 20장, 게이지 Lv 8). 0 = 없음 |
| mul | float | 문턱 추가 배율(계단 게이트 2) |
| useScale | bool01 | × `06_balancePreset.scale` |
| useGauge | bool01 | × `06_balancePreset.gauge` |
| useAreaHp | bool01 | × `302_area.hp / 100` |
| diffMul | float | 감쇠 `1/(1 + n × diff × diffMul)`. 0 = 감쇠 없음 |
| perPass | enum | 문턱 1회 넘을 때 늘어나는 양: `GaugeLv`(현재 게이지 Lv만큼 계단) / `One`(무기 1장) / `Level`(게이지 Lv +1) |

| id | key | gateType | base | step | growth | cap | mul | useScale | useGauge | useAreaHp | diffMul | perPass | 근거 |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 107001001 | sgate | StairGate | 1.9 | 0 | None | 0 | 2 | 1 | 0 | 1 | 1 | GaugeLv | L835 `GATE_MAX[0]`, L1945~1951 |
| 107001002 | cgate | CraftGate | 1.9 | 3.8 | Add | 20 | 1 | 1 | 0 | 1 | 0 | One | L837~838 |
| 107001003 | well | Well | 150 | 1.6 | Pow | 8 | 1 | 0 | 1 | 1 | 0 | Level | L1745~1747, L1887~1893 |

- 문턱 식: StairGate `M = base × mul × scale × hp/100`(Area 1 easy 11.4), CraftGate `T(k) = (base + step·k) × scale × hp/100`(5.7, 17.1, 28.5 …), Well `need(lv) = base × step^(lv−1) × hp/100 × gauge`(150, 240, 384 …). 넘침 처리·반복은 [04 §4.4~4.5](04_GameRules.md).
- `GATE_MAX[1..7]`(3.5~15)는 정본에서 쓰지 않는다(잔재, 04 §4.16). 표에 두지 않는다.

### 7.5.5 108_receiveCondition

| 열 | 타입 | 뜻 |
|---|---|---|
| id, key | | |
| type | enum | `OnBreak`(파괴 즉시) / `OnPass`(이번 프레임 `oz < o.z ≤ P.z`, 1회) / `OnFlush`(우물 묶음 배출) / `OnArrive`(플레이어 z가 o.z 이상) / `OnTouch`(접촉, 109가 판정) |
| margin | float | OnPass 여유(m) |
| insideX | float | OnPass 「안」 판정 `|P.x − o.x| < insideX`. 밖이면 `GateMiss` |
| preDelayMs | int | 게임 시간 선지연(ms) |
| delayMs | int | 수령 지연(ms) |
| clock | enum | delayMs의 시계: `Real`(실시간, dt 상한 0.05) / `Game`(배속·히트스톱 적용) |
| arriveAction | enum | OnArrive일 때 할 일: `None` / `StartClimb`(계단) / `FloorBanner`(아치) / `FinishGate`(Area 아치 → `finish('gate')`) |
| passEffect | effect | 통과 이펙트 |
| passSound | sound | 통과 소리 |

| id | key | type | margin | insideX | preDelayMs | delayMs | clock | arriveAction | passEffect | passSound | 근거 |
|---|---|---|---|---|---|---|---|---|---|---|---|
| 108001001 | onBreak | OnBreak | | | 0 | 0 | Game | None | | | |
| 108001002 | sgatePass | OnPass | 0.2 | 1.45 | 0 | 560 | Real | None | run.statGatePass | gate | L2041~2046 |
| 108001003 | cgatePass | OnPass | 0.2 | 1.45 | 0 | 500 | Game | None | run.cloneGatePass | forge | L2047~2058 |
| 108001004 | wellGauge | OnFlush | | | 260 | 560 | Real | None | run.brickFlush | | L1470~1473, L1872~1873 |
| 108001005 | onArriveStair | OnArrive | | | 0 | 0 | Game | StartClimb | | gate | L2032, L2260 |
| 108001006 | onArriveFloor | OnArrive | | | 0 | 0 | Game | FloorBanner | | floor | L2026~2028 |
| 108001007 | onArriveGoal | OnArrive | | | 0 | 0 | Game | FinishGate | | | L2029 |

결정 대기: 수령 지연을 규칙에 남기는지 [04 D-21](04_GameRules.md), 이원 시계 [04 D-22](04_GameRules.md). 권장대로 위 값이 규칙이다.

### 7.5.6 109_moveAI

| 열 | 타입 | 뜻 |
|---|---|---|
| id, key | | |
| type | enum | `Static` / `Chase`(앞에서 다가옴 + 가로 추적) |
| aggro | float | 감지 거리(앞, m) |
| speedMin / speedMax | float | 생성 때 균등 난수 속도(m/s, RunRng) |
| xFollow | float | 가로 추적 `clamp(dx, −1, 1) × xFollow` m/s |
| touchX / touchZ | float | 접촉 판정 `|dx| < touchX && |dz| < touchZ` |
| touchAction | enum | `None` / `Steal`(츄르를 훔치고 사라짐) |
| stealBase / stealPerZone | float | 훔치는 양 `min(floor(R.gold), stealBase + stealPerZone × zone)` |
| stealShake | string | 흔들림 키(`shake.steal`) |
| stealSound / stealEffect | sound / effect | `steal` / `run.steal` |
| despawnBehind | float | 플레이어 뒤 이 거리면 보상 없이 사라짐. 0 = 안 사라짐 |

| id | key | type | aggro | speedMin | speedMax | xFollow | touchX | touchZ | touchAction | stealBase | stealPerZone | despawnBehind | 근거 |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 109001001 | static | Static | | | | | | | None | | | 0 | |
| 109001002 | mouse | Chase | 24 | 1.1 | 1.6 | 0.5 | 0.8 | 0.7 | Steal | 5 | 4 | 4 | L2059~2064, L1320 |

- 접촉 판정(`touchX`·`touchZ`·`touchAction`)은 type과 무관하게 돈다. `Static`에 Steal을 주면 「밟으면 훔치는 함정」이 된다(§7.14.1).
- 훔친 뒤 오브젝트는 보상 없이 사라진다(정본 쥐와 같음). 플레이어는 죽지 않는다.

### 7.5.7 114_hpRule

| 열 | 타입 | 뜻 |
|---|---|---|
| id, key | | |
| type | enum | `None`(체력 없음) / `AreaHpMul`(`round(hp × mul)`, hp = `302_area.hp`) / `FieldLerp`(필드 칸, §7.4.3 식) / `Fixed`(fixed 값) |
| mul | float | AreaHpMul 배율 |
| fixed | int | Fixed 값 |
| useScale | bool01 | × `06_balancePreset.scale` (정본 적/상자는 0) |
| useObstMul | bool01 | × `obstMul()`(부활 특성 obst). 정본은 wall만 1 |

| id | key | type | mul | fixed | useScale | useObstMul | 근거 |
|---|---|---|---|---|---|---|---|
| 114001001 | none | None | | | 0 | 0 | |
| 114001002 | enemy04 | AreaHpMul | 0.4 | | 0 | 0 | L1320 |
| 114001003 | box06 | AreaHpMul | 0.6 | | 0 | 0 | L1324 |
| 114001004 | bonus025 | AreaHpMul | 0.25 | | 0 | 0 | L1788 |
| 114001005 | bonus04 | AreaHpMul | 0.4 | | 0 | 0 | L1788 |
| 114001006 | field | FieldLerp | | | 1 | 1 | L1773, L1795 |

반올림은 JS `Math.round`와 같게 `Math.Floor(x + 0.5)`([04 §4.0.2](04_GameRules.md)).

### 7.5.8 102_enemy

| 열 | 타입 | 뜻 |
|---|---|---|
| id, key | | |
| sort, active | | |
| hpRuleId | fk 114 | |
| moveAiId | fk 109 | |
| rewardType / Id / Value, rewardValuePerZone, rewardValueRule | consume | 처치 보상 |
| dropId | int | `801_drop.dropGroup`. 처치 때 확률 보상 |
| sprite | res | 프레임 이름 접두(`World_Enemy_Mouse` → `_00`~`_15`). 기본 아틀라스 `@theme` |
| frameCount / fps | int / float | 16 / 12.7 ([03 §3.5.3](03_ResourcePipeline.md)) |
| hitSound / breakSound | sound | |
| hitEffect / breakEffect | effect | |
| hw / hd / scale | float | 판정 반너비·반깊이(m)와 그림 배율. 모드 전용 적(§7.9.5)이 쓰고, 본편 쥐는 105_objectKind[enemy] 값을 따른다(빈칸 = 105 값) |
| icon | res | HUD·결과에 쓰는 적 아이콘(예 수비전 「남은 쥐」 칩, [05 §5.17.2](05_Screens.md)). 빈칸 = 표시 안 함. 기본 아틀라스 `Atlas_UI_Icon` |

| id | key | hpRuleId | moveAiId | reward (Type/Value/PerZone) | dropId | sprite | breakSound | breakEffect | 이름 | 근거 |
|---|---|---|---|---|---|---|---|---|---|---|
| 102001001 | mouse | 114001002 | 109001002 | 602 / 6 / 4 | 801001 | World_Enemy_Mouse | coin | run.enemyKill | 생선 도둑 쥐 | L1320, L1921~1929, L3651 |

key `mouse`는 정본 화면(L3117 `drawMouse`)과 맞춘 이름이다(정본 코드의 kind는 `enemy`). 화면 이름은 `name:102001001` = 「생선 도둑 쥐」.

---

## 7.6 배치 표 — 템플릿·필드·무리

사용자 요구 「오브젝트 배치 컨트롤」의 답이다. 런 1판의 코스는 **플레이 구간(템플릿) → 보너스 상자 2줄 → 골목 아치·필드 × 3 → Area 아치**다([04 §4.14](04_GameRules.md)). 플레이 구간은 `310`·`311`(손으로 놓은 배치), 나머지는 `313`(규칙 생성)이 정한다.

### 7.6.1 310_template

| 열 | 타입 | 뜻 |
|---|---|---|
| id | int | `310{묶음 3}{순번 3}`. 정본 템플릿 = 묶음 000(`310000001`~`008`), 모드 전용 = 08이 정한 묶음(예 `310000101`·`310000102`, [08 §8.4.7](08_Content.md)) |
| key | string | `tpl1`~`tpl8` |
| sort, active | int | |
| endZ | float | 템플릿 길이. 보너스 상자 줄 시작 z = base + endZ [프로토] L739 `TPL_END` |
| #비고 | | |

| id | key | endZ | 오브젝트 수 |
|---|---|---|---|
| 310000001 | tpl1 | 189 | 8 |
| 310000002 | tpl2 | 203 | 10 |
| 310000003 | tpl3 | 224 | 11 |
| 310000004 | tpl4 | 224 | 11 |
| 310000005 | tpl5 | 231 | 11 |
| 310000006 | tpl6 | 224 | 11 |
| 310000007 | tpl7 | 231 | 11 |
| 310000008 | tpl8 | 238 | 11 |

### 7.6.2 311_templateObject

| 열 | 타입 | 뜻 |
|---|---|---|
| id | int | `311{템플릿 순번 3}{seq 3}` (템플릿 `310000101`이면 `311101001`~) |
| templateId | fk 310 | |
| seq | int | 템플릿 안 순서(z 오름차순 권장. 로더가 z로 다시 정렬하고 같은 z는 seq 순) |
| kindId | fk 105 | 오브젝트 종류 |
| x / z | float | 위치(z는 템플릿 시작 기준) |
| groupId | int | 0이 아니면 `314_spawnGroup.group` — 이 행 위치를 기준점으로 무리를 깐다(kindId는 무리 행의 kindId가 우선, 이 행 kindId는 편집기 표시용) |
| active | bool01 | |
| hpRuleId | fk 114 | 빈칸 = 종류 기본. 이 배치만 체력 규칙을 바꿀 때 |
| reward 3열 | consume | 빈칸 = 종류 기본. 이 배치만 보상을 바꿀 때(덮어쓰기) |

**초기 84행** [프로토] L738 `PLACE` (노트 B §1.1 원문). 정본 type 번호 → kindId: 7 tower 105001001, 5 enemy 105001002(groupId 1 = mouse5), 14 sgate 105001003, 13 cgate 105001004, 15 stair 105001005, 2 coin 105001006, 3 gem 105001007. 모든 z < endZ(검증됨).

```tsv
id	templateId	seq	kindId	x	z	groupId	active	#kind
311001001	310000001	1	105001001	0	24.82	0	1	tower
311001002	310000001	2	105001003	0	52.16	0	1	sgate
311001003	310000001	3	105001002	0	57.42	1	1	enemy
311001004	310000001	4	105001005	0	66.5	0	1	stair
311001005	310000001	5	105001004	2.25	100.17	0	1	cgate
311001006	310000001	6	105001004	-2.25	123.83	0	1	cgate
311001007	310000001	7	105001003	2.25	143.9	0	1	sgate
311001008	310000001	8	105001005	0	157.5	0	1	stair
311002001	310000002	1	105001001	0	17.17	0	1	tower
311002002	310000002	2	105001003	-2.08	39.25	0	1	sgate
311002003	310000002	3	105001007	2.39	55.67	0	1	gem
311002004	310000002	4	105001002	0	57.42	1	1	enemy
311002005	310000002	5	105001005	0	66.5	0	1	stair
311002006	310000002	6	105001004	-2.25	100.11	0	1	cgate
311002007	310000002	7	105001004	2.25	122.77	0	1	cgate
311002008	310000002	8	105001003	-2.25	146.65	0	1	sgate
311002009	310000002	9	105001005	0	157.5	0	1	stair
311002010	310000002	10	105001006	1.48	188.3	0	1	coin
311003001	310000003	1	105001001	0	17.17	0	1	tower
311003002	310000003	2	105001003	-2.08	39.25	0	1	sgate
311003003	310000003	3	105001002	0	57.42	1	1	enemy
311003004	310000003	4	105001004	2.25	62.45	0	1	cgate
311003005	310000003	5	105001005	0	73.5	0	1	stair
311003006	310000003	6	105001001	2.2	108.52	0	1	tower
311003007	310000003	7	105001003	2.25	130	0	1	sgate
311003008	310000003	8	105001004	-2.25	153.99	0	1	cgate
311003009	310000003	9	105001005	0	171.5	0	1	stair
311003010	310000003	10	105001006	2.14	204.9	0	1	coin
311003011	310000003	11	105001007	-2.09	210.7	0	1	gem
311004001	310000004	1	105001001	-2.17	17.17	0	1	tower
311004002	310000004	2	105001001	0	39.43	0	1	tower
311004003	310000004	3	105001002	0	57.42	1	1	enemy
311004004	310000004	4	105001003	2.25	62.4	0	1	sgate
311004005	310000004	5	105001005	0	73.5	0	1	stair
311004006	310000004	6	105001004	0	107.2	0	1	cgate
311004007	310000004	7	105001003	2.25	131	0	1	sgate
311004008	310000004	8	105001003	-2.25	154.48	0	1	sgate
311004009	310000004	9	105001007	2.11	167.04	0	1	gem
311004010	310000004	10	105001005	0	178.5	0	1	stair
311004011	310000004	11	105001006	0	210.53	0	1	coin
311005001	310000005	1	105001004	0	19.34	0	1	cgate
311005002	310000005	2	105001001	-1.77	40.37	0	1	tower
311005003	310000005	3	105001002	0	57.42	1	1	enemy
311005004	310000005	4	105001003	2.25	62.01	0	1	sgate
311005005	310000005	5	105001005	0	73.5	0	1	stair
311005006	310000005	6	105001004	-2.11	107.16	0	1	cgate
311005007	310000005	7	105001003	2.25	130.99	0	1	sgate
311005008	310000005	8	105001004	0	154.83	0	1	cgate
311005009	310000005	9	105001005	0	171.5	0	1	stair
311005010	310000005	10	105001006	2.28	202.72	0	1	coin
311005011	310000005	11	105001007	-1.91	215.46	0	1	gem
311006001	310000006	1	105001001	-2.17	19.81	0	1	tower
311006002	310000006	2	105001003	2.25	43.1	0	1	sgate
311006003	310000006	3	105001002	0	57.42	1	1	enemy
311006004	310000006	4	105001003	-2.25	66.39	0	1	sgate
311006005	310000006	5	105001005	0	80.5	0	1	stair
311006006	310000006	6	105001004	-2.11	114.11	0	1	cgate
311006007	310000006	7	105001003	2.25	138.11	0	1	sgate
311006008	310000006	8	105001005	0	150.5	0	1	stair
311006009	310000006	9	105001004	2.25	183.54	0	1	cgate
311006010	310000006	10	105001006	-2.33	199.18	0	1	coin
311006011	310000006	11	105001007	2.52	209.49	0	1	gem
311007001	310000007	1	105001001	0	19.81	0	1	tower
311007002	310000007	2	105001003	-2.25	43.99	0	1	sgate
311007003	310000007	3	105001002	0	57.42	1	1	enemy
311007004	310000007	4	105001007	2.07	63.18	0	1	gem
311007005	310000007	5	105001005	0	73.5	0	1	stair
311007006	310000007	6	105001004	-2.25	107.12	0	1	cgate
311007007	310000007	7	105001004	2.25	130.99	0	1	cgate
311007008	310000007	8	105001003	-2.25	155.39	0	1	sgate
311007009	310000007	9	105001005	0	171.5	0	1	stair
311007010	310000007	10	105001006	2.48	204.05	0	1	coin
311007011	310000007	11	105001006	-2.11	216.46	0	1	coin
311008001	310000008	1	105001001	0	19.81	0	1	tower
311008002	310000008	2	105001003	2.25	43.62	0	1	sgate
311008003	310000008	3	105001002	0	57.42	1	1	enemy
311008004	310000008	4	105001004	-2.25	67.13	0	1	cgate
311008005	310000008	5	105001005	0	80.5	0	1	stair
311008006	310000008	6	105001001	0	115.15	0	1	tower
311008007	310000008	7	105001003	2.25	138.79	0	1	sgate
311008008	310000008	8	105001004	-2.11	162.32	0	1	cgate
311008009	310000008	9	105001005	0	178.5	0	1	stair
311008010	310000008	10	105001006	2.49	207.69	0	1	coin
311008011	310000008	11	105001007	-2.14	221.9	0	1	gem
```

검증(§7.13): 템플릿마다 「stair가 1개 이상」, 「골목 wall은 템플릿에 두지 않는다」(shiftWorld가 계단 뒤를 지우기 때문, [04 §4.19.2](04_GameRules.md)), 「모든 z < endZ」, 「x는 ±LANE_X_MAX + hw 안」.

### 7.6.3 312_templateCycle

Level(판 번호) → 템플릿. 정본 `tplFor`(L857): Level 1~8은 그대로, 9부터 3~8 반복.

| 열 | 타입 | 뜻 |
|---|---|---|
| id | int | `312{묶음 3}{순번 3}` |
| cycleGroup | int | 0 = 본편. 모드는 `402_modeRule.cycleGroup` |
| levelFrom / levelTo | int | 범위(levelTo 0 = 끝없음) |
| templates | intList | 310 id 목록 |
| mode | enum | `Direct`(Level − levelFrom 번째) / `Loop`((Level − levelFrom) % 개수 번째) |

| id | cycleGroup | levelFrom | levelTo | templates | mode |
|---|---|---|---|---|---|
| 312000001 | 0 | 1 | 8 | 310000001/310000002/310000003/310000004/310000005/310000006/310000007/310000008 | Direct |
| 312000002 | 0 | 9 | 0 | 310000003/310000004/310000005/310000006/310000007/310000008 | Loop |

검증 예: Level 1~8 → tpl1~8, 9 → tpl3, 14 → tpl8, 15 → tpl3. 범위가 겹치거나 비면 오류.

### 7.6.4 313_fieldGen — 보너스 줄·골목 필드 생성 규칙

1행(`key = default`). 모드가 다른 생성 규칙을 쓰면 행 추가 + `402_modeRule.fieldGenKey`. 값은 [04 §4.14.3~4.14.4](04_GameRules.md) 표와 같다 [프로토] L858, L1766~1807.

| 열 | 값 | 뜻 |
|---|---|---|
| id / key | 313000001 / default | |
| lanes | 3 | 레인 수 |
| laneX0 / laneStep | −2.4 / 2.4 | 레인 x = laneX0 + laneStep × l |
| rowStep | 1.7 | 줄 간격(m) |
| wallKindId | 105001010 | 칸 오브젝트 종류(크기 1.15 / 0.55는 105 행) |
| archKindId / archGoalKindId | 105001011 / 105001012 | 골목 아치, Area 아치 |
| soulPerRows | 3 | 생선 칸 수 = max(1, ceil(rows / 3)) |
| soulMinIdx | 3 | 첫 줄(idx 0~2) 제외 |
| soulHashStep / soulTryMax | 7.31 / 400 | 생선 칸 해시 간격, 시도 상한 |
| diaHashStep / diaHashAdd / diaChance | 3.7 / 11 / 0.14 | 캣닢 칸 해시 |
| seedA / seedF / seedC | 97 / 131 / 7 | seed = (A + 1) × 97 + f × 131 + 7, A = `302_area.fieldSeedArea` |
| goldBase / goldPerZone / goldPerRow | 10 / 6 / 0.2 | 츄르 칸 값 round((10 + 6·zi)(1 + 0.2·r)) |
| soulRewardType | 603 | 생선 칸 보상 type(값 = 칸 생선 수) |
| diaRewardType / diaRewardRule | 604 / DiaCap | 캣닢 칸 보상(값 = diaCap) |
| goldRewardType | 602 | 츄르 칸 보상(× goldMul) |
| bonusRows | 2 | 보너스 상자 줄 수 |
| bonusLaneX | −2.2/0/2.2 | 보너스 상자 x |
| bonusZ0 / bonusRowStep | 3 / 4.5 | 보너스 z = base + endZ + 3 + r × 4.5 |
| bonusGemRow / bonusGemLane | 1 / 1 | 둘째 줄 가운데만 캣닢 |
| bonusCoinKindId / bonusGemKindId | 105001008 / 105001009 | |
| bonusToArch | 14 | 보너스 시작 → 첫 아치 |
| archToField / fieldToNext | 6 / 6 | 아치 → 필드 첫 줄, 필드 끝 → 다음 아치 |

해시 상수(127.1, 311.7, 43758.5453)는 표에 두지 않는다. 바꾸면 모든 배치가 바뀐다([04 §4.14.3](04_GameRules.md)).

### 7.6.5 314_spawnGroup — 무리·분신 오프셋

| 열 | 타입 | 뜻 |
|---|---|---|
| id | int | `314{group 3}{idx 3}` |
| group | int | 무리 번호(311 `groupId`가 가리킴) |
| key | string | 무리 이름(같은 group은 같은 key) |
| idx | int | 0~ |
| kindId | fk 105 | 깔 종류(분신 그룹은 0) |
| dx / dz | float | 기준점에서의 오프셋. **dz에 정본 +6을 합쳐 저장** |

| id | group | key | idx | kindId | dx | dz | 근거 |
|---|---|---|---|---|---|---|---|
| 314001001 | 1 | mouse5 | 0 | 105001002 | −1.6 | 6 | L1320 오프셋 (−1.6,0), z+6 |
| 314001002 | 1 | mouse5 | 1 | 105001002 | 0 | 6.9 | (0, 0.9) |
| 314001003 | 1 | mouse5 | 2 | 105001002 | 1.6 | 6 | (1.6, 0) |
| 314001004 | 1 | mouse5 | 3 | 105001002 | −0.8 | 8.2 | (−0.8, 2.2) |
| 314001005 | 1 | mouse5 | 4 | 105001002 | 0.8 | 8.2 | (0.8, 2.2) |
| 314002001 | 2 | clone | 0 | 0 | −0.95 | −0.9 | L831 `CLONE_OFF` |
| 314002002 | 2 | clone | 1 | 0 | 0.95 | −0.9 | |
| 314002003 | 2 | clone | 2 | 0 | −1.9 | −1.8 | |
| 314002004 | 2 | clone | 3 | 0 | 1.9 | −1.8 | |

- 무리 x = clamp(기준 x + dx, −ENEMY_X_MAX, ENEMY_X_MAX). 분신 x = clamp(P.x + dx, −CLONE_X_MAX, CLONE_X_MAX).
- 분신 그룹 행 수는 `MAX_CLONES` 이상이어야 한다(검증).

### 7.6.6 배치 편집 도구 `MV/Placement Editor` (MV-DATA, 개발 전용)

| 기능 | 규칙 |
|---|---|
| 열기 | Unity 메뉴 `MV/Placement Editor`. 편집 모드에서만 |
| 보기 | 템플릿을 고르면 씬 뷰에 레인(±3.2 m), z 눈금(10 m), 오브젝트를 `105.hw/hd` 상자와 스프라이트로 그린다. endZ 선, 보너스 줄·첫 아치 위치(313 값으로 계산) 표시 |
| 편집 | 끌어서 x·z 이동(0.05 m 눈금), 종류 바꾸기(105 key 드롭다운), 추가·삭제, 무리(groupId) 지정 |
| 검사 | 저장 전에 §7.13 배치 규칙을 돌려 오류면 저장 막음 |
| 저장 | `311_templateObject.txt`의 해당 템플릿 행만 다시 쓴다(id는 `311{템플릿}{seq}`로 다시 매김, 다른 템플릿 행은 그대로). `310.endZ`도 같이 저장 |
| 시험 | 「이 템플릿으로 런」 버튼: 개발 빌드에서 Level을 그 템플릿이 나오는 값으로 두고 Area 1 런 시작 |
| 범위 | 필드(313)는 규칙 생성이라 편집하지 않는다. 결정 대기(D-65) |

---

## 7.7 전투 표 — 무기·카드·신·고양이

### 7.7.1 103_item — 무기 카드 등급 1~8

consumeType 5(Item)의 id 표. 로비 시작 덱(`save.deck`)과 런 손패의 「무기 카드」가 이 행이다.

| 열 | 타입 | 뜻 |
|---|---|---|
| id | int | `103001001`~`008` |
| key | string | `w1`~`w8` |
| tier | int | 1~8 |
| sort, active | int | |
| dmg | int | 기본 피해. 정본 `wDmg(t) = round(10 × 1.8^(t−1))`의 **반올림 결과를 저장** |
| col | color | 3D 금속색(그림 보조) `WCOL` |
| gradeCol | color | 등급색 `WGRADE`(손패 틀·팝업·「변신!」 색) |
| spin | bool01 | 투사체 회전 그림 여부. 정본 `spin: !(t===4 \|\| t===7)` L2087 |
| icon | res | UI 아이콘. 기본 아틀라스 `Atlas_UI_Icon` (`UI_Icon_Weapon_{t}`) |
| projSprite | res | 투사체 그림. 기본 아틀라스 `@theme` (`World_Proj_{t}`) |
| lookName | string | 고양이 모습 짧은 이름의 문구 키(`ui` 컬렉션 `ui.item.look{t}`). 정본 `LOOKS[t-1]`의 ` · ` 앞부분. HUD 피해 줄 `ui.hud.wDmg`의 {1}([05 §5.2.6](05_Screens.md)) |

이름 `name:{id}` = 무기 이름, 설명 `description:{id}` = 고양이 외형 전체(`LOOKS`, L3264). 짧은 이름 `lookName` 키의 ko 값은 외형 칸의 ` · ` 앞(예 tier 4 「깡통 투구」, tier 1 「방울 목걸이」).

| id | tier | dmg | col | gradeCol | spin | 이름 | 외형(description) |
|---|---|---|---|---|---|---|---|
| 103001001 | 1 | 10 | #c08a52 | #9ea4ae | 1 | 털뭉치 | 방울 목걸이 |
| 103001002 | 2 | 18 | #d6a56a | #46c35a | 1 | 솔방울 | 새싹 머리 |
| 103001003 | 3 | 32 | #d9dde3 | #3a8ff0 | 1 | 병뚜껑 | 병뚜껑 모자 |
| 103001004 | 4 | 58 | #a9bfd6 | #a65be8 | 0 | 깡통 | 깡통 투구 · 파란 목도리 |
| 103001005 | 5 | 105 | #b4bac4 | #f08a24 | 1 | 생선뼈 | 해적 반다나 · 생선뼈 목걸이 |
| 103001006 | 6 | 189 | #ece6d6 | #e8443a | 1 | 고무공 | 털모자 · 줄무늬 목도리 |
| 103001007 | 7 | 340 | #cfe0ff | #1fc8c8 | 0 | 방울 장난감 | 방울 마법사 모자 · 망토 |
| 103001008 | 8 | 612 | #ffe08a | #ffd23a | 1 | 황금 털뭉치 | 황금 왕관 · 빨간 망토 |

근거 [프로토] L769~772, L3264. 등급 9 이상을 만들려면 행 추가 + `12_weaponLevel` 행 + 손패 머지 상한(코드 상수 8 → `103_item` 최대 tier로 바뀜).

### 7.7.2 115_cardTier — 가호 카드 등급

| 열 | 타입 | 뜻 |
|---|---|---|
| id | int | `115001001`~`005` |
| key | string | `common`·`uncommon`·`rare`·`epic`·`legendary` |
| tierIndex | int | 0~4 (`110_card.tier`, `330_climb.lapTier`가 이 값) |
| need | int | 마지막 랩 나머지 계단 수 문턱 |
| color | color | 등급색(룰렛·카드 틀·`$tier` 색 토큰) |
| frameSprite | res | 카드 틀. 기본 아틀라스 `Atlas_UI_Card` |
| cardSound | sound | 확정 소리 `card_{tierIndex}` |

| id | key | tierIndex | need | color | 이름 | cardSound |
|---|---|---|---|---|---|---|
| 115001001 | common | 0 | 5 | #c9c2b4 | COMMON | card_0 |
| 115001002 | uncommon | 1 | 10 | #6fd27a | UNCOMMON | card_1 |
| 115001003 | rare | 2 | 20 | #58a6f0 | RARE | card_2 |
| 115001004 | epic | 3 | 40 | #c07af0 | EPIC | card_3 |
| 115001005 | legendary | 4 | 75 | #f2b33d | LEGENDARY | card_4 |

근거 [프로토] L773~775. 검증: need 오름차순, 마지막 need = `330_climb.lap`.

### 7.7.3 110_card — 가호 카드

id 형식은 [08 §8.3.4](08_Content.md)와 같다: 공용 = `110000{순번}`, 신 카드 = `110{신 순번 3}{슬롯 순번 3}`.

| 열 | 타입 | 뜻 |
|---|---|---|
| id | int | |
| group | int | 0 = 공용, n = `113_god.cardGroup` |
| key | string | 슬롯 키(`atk`, `orb`, `proc` …). 같은 group 안에서 유일 |
| tier | int | 0~4 (`115_cardTier.tierIndex`) |
| effectId | fk 112 | 효과 |
| sort | int | |
| active | bool01 | 1 = 공용·출전 신 기본 풀에 들어감. 0 = 풀 항목이 직접 지목할 때만(§7.7.4) |
| icon | res | 카드 그림. 기본 아틀라스 `Atlas_UI_Card` |
| projSprite | res | 이 카드가 만드는 특수 투사체 그림(spear 레이저 등). 빈칸 = 무기 투사체 |

이름·설명 = `name:{id}`, `description:{id}`. 04가 예전에 쓰던 `110_card.enabled`는 이 표의 `active`, `.scope`는 `group`(0 = Common, 그 밖 = God)이다.

| id | group | key | tier | effect key | active | 이름 / 설명 | 근거 |
|---|---|---|---|---|---|---|---|
| 110000001 | 0 | atk | 0 | atk | 1 | 발톱 갈기 / 공격력 +20% | [프로토] L776~782 |
| 110000002 | 0 | rate | 0 | rate | 1 | 냥냥펀치 연타 / 연사 +20% | 〃 |
| 110000003 | 0 | clone1 | 0 | clone1 | 1 | 친구 부르기 / 친구 고양이 +1 | 〃 |
| 110000004 | 0 | gold | 0 | gold | 1 | 츄르 파티 / 이번 판 츄르 +50% | 〃 |
| 110000005 | 0 | crit | 1 | crit | 0 | (08) | 잔재 효과 L2075, [08 §8.3.6](08_Content.md) |
| 110000006 | 0 | dbl | 1 | dbl | 0 | (08) | 〃 |
| 110000007 | 0 | pierce | 2 | pierce | 0 | (08) | 〃 |
| 110000008 | 0 | clone2 | 2 | clone2 | 0 | (08) | L2314 |
| 110001001 | 1 | orb | 1 | orb | 1 | 털실 위성 / 주위를 도는 털실뭉치 3개 | [프로토] L783~791 |
| 110001002 | 1 | proc | 2 | procBolt | 1 | 찌릿 정전기 / 공격 50% 확률로 정전기, 피해 +60% | 〃 |
| 110001003 | 1 | chain | 2 | chainBolt | 1 | 정전기 연쇄 / 명중 시 정전기가 주변 2곳으로 튐 | 〃 |
| 110001004 | 1 | storm | 3 | storm | 1 | 냥냥펀치 소나기 / 명중 시 하늘에서 거대 냥냥펀치 4발 | 〃 |
| 110001005 | 1 | blast | 4 | blast | 1 | 하악질 / 명중 시 확률로 주변을 날려버리는 하악질 | 〃 |
| 110001006 | 1 | frate | 4 | frate | 1 | 우다다 연사 / 연사 +50% | 〃 |
| 110001007 | 1 | spear | 4 | spear | 1 | 레이저 포인터 / 2번째 공격마다 관통 레이저 포인터 | 〃 |
| 110002001 | 2 | orb | 1 | orb | 1 | 햇살 털실 / 주위를 도는 햇살 털실 3개 | [프로토] L792~800 |
| 110002002 | 2 | proc | 2 | procFire | 1 | 따끈 화상 / 공격 50% 확률로 3초간 화상 | 〃 |
| 110002003 | 2 | chain | 2 | chainFire | 1 | 불씨 파편 / 명중 시 불씨가 주변 폭발, 피해 40% | 〃 |
| 110002004 | 2 | storm | 3 | storm | 1 | 생선 소나기 / 명중 시 생선 4마리가 쏟아짐 | 〃 |
| 110002005 | 2 | blast | 4 | blast | 1 | 햇살 폭발 / 명중 시 확률로 거대 햇살 폭발 | 〃 |
| 110002006 | 2 | frate | 4 | frate | 1 | 햇살 연사 / 연사 +50% | 〃 |
| 110002007 | 2 | spear | 4 | spear | 1 | 돋보기 햇빛 / 2번째 공격마다 관통 돋보기 햇빛 | 〃 |

신 3~12의 카드 70행은 [08 §8.3.4](08_Content.md). 효과는 **카드 행의 effectId로 정한다** — 같은 슬롯 `proc`이라도 thunder는 procBolt, flame은 procFire. 정본의 「신 속성 el에 따라 분기」(L1986~2003)를 표로 푼 것이다. `R.el`(그림 색)은 출전 신 카드가 1장 이상이면 `113_god.element`.

### 7.7.4 111_cardPool — 카드 뽑기 풀

| 열 | 타입 | 뜻 |
|---|---|---|
| id | int | `111{group 3}{순번 3}` |
| group | int | 풀 번호 |
| key | string | 풀 이름(같은 group은 같은 key). `402_modeRule.cardPoolId`가 key로 가리킨다 |
| entryType | enum | `Card`(cardId 1장, active 무관) / `GodCards`(출전 신 group의 active 1 카드 전부) / `CommonActive`(group 0의 active 1 카드 전부) / `Exclude`(cardId를 풀에서 뺌) |
| cardId | fk 110 | Card·Exclude일 때 |
| weight | int | 1(정본은 같은 등급 안 균등). 2 이상이면 그만큼 중복 |

| id | group | key | entryType | cardId | 비고 |
|---|---|---|---|---|---|
| 111001001 | 1 | main | CommonActive | | 정본 `CARDS` = 공용 4 + 신 7 (L808) |
| 111001002 | 1 | main | GodCards | | |
| 111002001 | 2 | defence | CommonActive | | [08 §8.3.6] 골목 수비전 |
| 111002002 | 2 | defence | Exclude | 110000004 | gold 제외 |
| 111002003 | 2 | defence | Card | 110000005 | crit |
| 111002004 | 2 | defence | Card | 110000006 | dbl |
| 111002005 | 2 | defence | Card | 110000007 | pierce |
| 111002006 | 2 | defence | Card | 110000008 | clone2 |
| 111002007 | 2 | defence | GodCards | | |
| 111003001 | 3 | rush | CommonActive | | main과 같음 |
| 111003002 | 3 | rush | GodCards | | |

뽑기([04 §4.8.4](04_GameRules.md)): 풀을 만든 뒤 `tier == k`이고 유효한 카드(분신 4명이면 CloneAdd 효과 제외) 중 균등(RunRng). 비면 k−1, … 0. 그래도 없으면 풀 전체에서 균등.

### 7.7.5 112_cardEffect — 카드 효과

| 열 | 타입 | 뜻 |
|---|---|---|
| id | int | `112001001`~ |
| key | string | |
| type | enum | 아래 목록 |
| p1~p4 | float | 타입별 뜻(아래) |
| active | bool01 | 0 = 구현은 있으나 기본 풀에서 안 나옴(잔재) |
| procEffect / procSound | effect / sound | 발동 그림·소리(06·03 키, 빈칸 = 타입 기본) |

**type 목록과 파라미터 뜻(정본)** — n = 같은 카드 장수

| type | p1 | p2 | p3 | p4 | 공식(요약, 정본 [04 §4.9](04_GameRules.md)) |
|---|---|---|---|---|---|
| DmgMul | 장당 배율 | | | | baseDmg × (1 + p1·n) |
| FireRateMul | 장당 배율 | | | | iv 분모 × (1 + p1·n) |
| CloneAdd | 장당 분신 | | | | clones = min(MAX_CLONES, clones + p1) (획득 즉시) |
| GoldMul | 장당 배율 | | | | goldMul × (1 + p1·n) |
| Orbit | 주기(s) | 피해 배율 | 가로 여유(m) | 앞 여유(m) | p1초마다 범위 breakable에 baseDmg × p2·n |
| ProcExtraHit | 확률 | 피해 배율 | | | 명중 p1 확률로 dmg × p2·n 추가타(bolt) |
| ProcBurn | 확률 | 초당 배율 | 지속(s) | | 명중 p1 확률로 화상 dps = dmg × p2·n, p3초, BURN_TICK마다 dps × tick |
| ChainJump | 기본 횟수 | 피해 배율 | 거리(m) | | 1회 + n·p1 연쇄, 최초 명중점에서 p3 안 가장 가까운 대상, dmg × p2 |
| ChainSplash | 피해 배율 | 그림 반경 | 반경 증가 | | 상자 `CHAIN_FIRE_BOX + hw` 안, dmg × p1·n, 그림 반경 p2 + p3·n |
| Storm | 확률 | 쿨(s) | 피해 배율 | 타격 수 | 명중 p1, 쿨 p2, p4발, baseDmg × p3·n |
| Blast | 확률 | 장당 확률 증가 | 쿨(s) | 피해 배율 | 확률 p1 + p2(n−1), 쿨 p3, 상자 BLAST_BOX, baseDmg × p4·n |
| PierceEveryN | 주기 | 장당 배율 | 관통 | | 본체 p1번째 볼리마다 창 1발, 피해 × (1 + p2·n), 관통 p3 |
| CritChance | 장당 확률 | | | | 치명 확률 + p1·n |
| DoubleChance | 장당 확률 | | | | 2연발 확률 min(1, p1·n + …) |
| Pierce | 장당 관통 | | | | 관통 + p1·n |
| Spin | 주기(s) | 피해 배율 | 가로 여유 | 앞 여유 | p1초마다 baseDmg × p2·n |
| Beam | 주기(s) | 피해 배율 | 길이(m) | 가로 여유 | p1초마다 길이 p3 광선 |
| Meteor | 기본 쿨 | 쿨 감소/장 | 최소 쿨 | 피해 배율 | 쿨 max(p3, p1 − p2(n−1)), baseDmg × p4·n |
| ProcChill | 확률 | 지속(s) | 받는 피해 증가 | 이동 배율 | [08 §8.3.3] 새 type(2차). 정본 frost는 p3만 있음 |
| Shield / CloneDmgMul / DropBonus | 08 정의 | | | | [08 §8.3.3] 새 type(2차) |

04 본문 type 이름 대응: `AddClone`→CloneAdd, `Proc`→ProcExtraHit(bolt)·ProcBurn(fire), `Chain`→ChainJump(bolt)·ChainSplash(fire), `Spear`→PierceEveryN, `DoubleShot`→DoubleChance, `Blade`→Spin, `Chill`→ProcChill. 04의 `112_cardEffect[proc]`·`[chain]`은 아래 `procBolt`/`procFire`, `chainBolt`/`chainFire` 두 행으로 나뉜다.

| id | key | type | p1 | p2 | p3 | p4 | active | 근거 |
|---|---|---|---|---|---|---|---|---|
| 112001001 | atk | DmgMul | 0.2 | | | | 1 | L1751 |
| 112001002 | rate | FireRateMul | 0.2 | | | | 1 | L2074 |
| 112001003 | clone1 | CloneAdd | 1 | | | | 1 | L2313 |
| 112001004 | gold | GoldMul | 0.5 | | | | 1 | L1746 |
| 112001005 | orb | Orbit | 0.3 | 1.2 | 1.5 | 1.7 | 1 | L2140~2144 |
| 112001006 | procBolt | ProcExtraHit | 0.5 | 0.6 | | | 1 | L1986~1988 |
| 112001007 | procFire | ProcBurn | 0.5 | 0.3 | 3 | | 1 | L1989, L2136~2138 |
| 112001008 | chainBolt | ChainJump | 1 | 0.5 | 9 | | 1 | L1990~1996 |
| 112001009 | chainFire | ChainSplash | 0.4 | 1.6 | 0.3 | | 1 | L1997~2003 |
| 112001010 | storm | Storm | 0.25 | 2.6 | 1.5 | 4 | 1 | L2004, L2157~2174 |
| 112001011 | blast | Blast | 0.1 | 0.04 | 1.4 | 4 | 1 | L2005, L2175~2182 |
| 112001012 | frate | FireRateMul | 0.5 | | | | 1 | L2074 |
| 112001013 | spear | PierceEveryN | 2 | 1 | 99 | | 1 | L2082~2086 |
| 112001014 | crit | CritChance | 0.15 | | | | 0 | L2075 잔재 |
| 112001015 | dbl | DoubleChance | 0.2 | | | | 0 | L2075 잔재 |
| 112001016 | pierce | Pierce | 1 | | | | 0 | L2075 잔재 |
| 112001017 | clone2 | CloneAdd | 2 | | | | 0 | L2314 잔재 |
| 112001018 | spin | Spin | 0.3 | 1.5 | 1.4 | 1.6 | 0 | L2146~2150 잔재 |
| 112001019 | beam | Beam | 5 | 3 | 22 | 0.7 | 0 | L2119~2125 잔재 |
| 112001020 | meteor | Meteor | 6 | 1 | 2.5 | 5 | 0 | L2127~2133 잔재 |
| 112001021 | frost | ProcChill | | | 0.25 | | 0 | L1904 잔재(얼림 설정 코드 없음) |

잔재 7종(crit~frost)은 정본에서 얻을 수 없다. 구현하고 active 0으로 둔다(D-62, 04 D-20과 같은 결정). 08 신 확장이 이 행을 effectId로 가리키면 그 카드는 정상 동작한다(카드 행 active가 정한다).

### 7.7.6 113_god — 야옹신

| 열 | 타입 | 뜻 |
|---|---|---|
| id | int | `113001001`~ |
| key | string | `thunder`, `flame` … |
| sort, active | int | active 0 = 지도·신 화면에 잠김 칸 |
| element | string | 그림 속성(`bolt`, `fire`, 08 신규). 피해에 영향 없음 |
| color | color | 대표색 |
| projTint | color | 투사체 색(빈칸 = color) |
| cardGroup | int | `110_card.group` |
| unlockType | int | 0 무료 / 1 contentOpen / 2 구매 [08] |
| openId | fk 05 | unlockType 1일 때 |
| unlockPriceType / Id / Value | consume | unlockType 2일 때 |
| passiveStat / passivePerLv | enum / float | 신 레벨 패시브 [08 §8.3.5] |
| gearSet | bool01 | 장비 세트 대상 [08 §8.5.8] |
| portrait | res | 초상 `Tex_God_<Key>`(key 첫 글자 대문자, 예 `Tex_God_Thunder`. [03](03_ResourcePipeline.md) 명명. AddressableTexture) |
| icon | res | 신 버튼 아이콘. 기본 아틀라스 `Atlas_UI_Icon` |

이름 `name:{id}`, 부제 `description:{id}`.

| id | key | element | color | cardGroup | unlockType | active | 이름 / 부제 | 근거 |
|---|---|---|---|---|---|---|---|---|
| 113001001 | thunder | bolt | #9ab8ff | 1 | 0 | 1 | 달빛 야옹신 / 털실 · 정전기 · 냥냥펀치 | [프로토] L784 |
| 113001002 | flame | fire | #ff8a3d | 2 | 1 (openId 5001004) | 1 | 햇살 야옹신 / 햇살 · 범위 · 폭발 | [프로토] L792, 해금 [08] |

신 3~12와 나머지 열 값은 [08 §8.3.2](08_Content.md). 정본은 thunder만 열려 있다(L2497). flame의 Area 4 해금은 08 결정(D-74).

### 7.7.7 101_cat — 플레이어·친구 고양이 그림

| 열 | 타입 | 뜻 |
|---|---|---|
| id, key | | `101001001 player`, `101001002 friend` |
| spritePrefix | res | 프레임 이름 접두(`World_Cat_P0`, 친구는 03 규칙) |
| atlasPattern | string | `Atlas_World_Cat_T{t}` / `Atlas_World_Friend_T{t}` (t = 외형 등급) |
| tierCount | int | 외형 등급 수(8 / 6, 03 §3.8.1) |
| patternCount | int | 무늬 수(1 / 4) |
| lobbySprite | res | 로비 아지트 정면 그림. 기본 아틀라스 `Atlas_World_Lobby` |

크기 배율은 `300_runConfig` `view.playerCatScale`·`view.cloneCatScale`([02](02_Visual.md))이 정본이다. 프레임 이름·수는 [03 §3.5.1](03_ResourcePipeline.md).

---

## 7.8 메타 표 — 성장·비용·광고·해금·재화

### 7.8.1 10_lobbyUpgrade — 로비 강화 3종

| 열 | 타입 | 뜻 |
|---|---|---|
| id, key | | |
| sort, active | int | |
| max | int | 최대 구매 횟수 |
| levelPriceGroup | int | `16_levelPrice.group`. 구매 횟수 P에서 다음 구매 비용 = targetLevel P+1 행 |
| effectType | enum | `StartStairLv`(11_stairLevel로 시작 게이지 환산) / `DeckAdd`(12_weaponLevel 등급 카드 1장 덱 추가) / `GoldMul`(goldMul × (1 + INCOME_PER_LV × P)) |
| icon | res | 버튼 아이콘. 기본 아틀라스 `Atlas_UI_Icon` |
| facSprite | res | 로비 아지트 시설 그림 접두(`Lobby_Fac_Tower` → 단계 번호 붙임). 기본 아틀라스 `Atlas_World_Lobby` |
| costFormula / costBase / costGrowth | enum / float / float | **행 생성 도구 전용**(런타임 무시). `Pow`: round(base × growth^P), `LinearNext`: base + growth × (P+1). 04 본문의 `costFormula`가 이 열 |

| id | key | max | levelPriceGroup | effectType | costFormula | costBase | costGrowth | 이름 | 근거 |
|---|---|---|---|---|---|---|---|---|---|
| 10001001 | stair | 30 | 1 | StartStairLv | Pow | 25 | 1.18 | 캣타워 | [프로토] L821~823, `KEG_MAXP` L820 |
| 10001002 | weapon | 149 | 2 | DeckAdd | Pow | 30 | 1.043 | 스크래쳐 | [프로토] L824~826, `W_MAXP` L818 |
| 10001003 | income | 50 | 3 | GoldMul | LinearNext | 20 | 8 | 생선 건조대 | [프로토] L827~828 |

검증: `max(stair) = Σ 11_stairLevel.need(lv < LOBBY_STAIR_MAX_LV)` = 7+10+13 = 30. `max(weapon) = Σ 12_weaponLevel.need(lv 1~7) + WEAPON_EXTRA_BUYS` = 89 + 60 = 149.

### 7.8.2 11_stairLevel — 캣타워 단계

| 열 | 타입 | 뜻 |
|---|---|---|
| id | int | `11001001`~ |
| lv | int | 시작 게이지 Lv |
| need | int | 다음 Lv까지 구매 수(`KEG_STEP(lv) = 4 + 3lv`) |

| id | lv | need |
|---|---|---|
| 11001001 | 1 | 7 |
| 11001002 | 2 | 10 |
| 11001003 | 3 | 13 |
| 11001004 | 4 | 16 |

근거 [프로토] L809~812. Lv 4 행의 need 16은 표시(`step/need`)에만 쓴다(로비 상한 `LOBBY_STAIR_MAX_LV` = 4).

### 7.8.3 12_weaponLevel — 스크래쳐 단계

| 열 | 타입 | 뜻 |
|---|---|---|
| id | int | `12001001`~ |
| lv | int | 무기 Level |
| need | int | 다음 Level까지 구매 수 `round(8 × 1.15^(lv−1))` |
| itemId | fk 103 | 이 Level에서 사는 카드 |

| id | lv | need | itemId |
|---|---|---|---|
| 12001001 | 1 | 8 | 103001001 |
| 12001002 | 2 | 9 | 103001002 |
| 12001003 | 3 | 11 | 103001003 |
| 12001004 | 4 | 12 | 103001004 |
| 12001005 | 5 | 14 | 103001005 |
| 12001006 | 6 | 16 | 103001006 |
| 12001007 | 7 | 19 | 103001007 |
| 12001008 | 8 | 21 | 103001008 |

근거 [프로토] L813~819. Lv 8 이후는 상승 없음(need 21은 표시용).

### 7.8.4 13_areaUpgrade — 장난감 바구니(Area 강화 5종)

| 열 | 타입 | 뜻 |
|---|---|---|
| id, key | | |
| sort, active | int | |
| max | int | |
| perLv | float | 단계당 효과(연사·사거리·치명·2연발 +0.1, 친구 +1) |
| statKey | enum | `FireRate`·`Range`·`Clone`·`CritChance`·`DoubleChance` |
| valueFormat | string | 표시 문구 틀(`{0}%`, `{0}`). 05가 쓴 열 |
| valueBase / valueStep | float | 표시 숫자 = valueBase + valueStep × 단계 |
| resetOn | enum | `AreaChange`(정본: Area가 바뀌면 0, L2433) |
| levelPriceGroup | int | 11~15 |
| icon | res | 기본 아틀라스 `Atlas_UI_Icon` |
| costBase / costGrowth | float | 생성 도구 전용 round(base × 1.6^L) |

| id | key | max | perLv | statKey | valueFormat | valueBase | valueStep | levelPriceGroup | costBase | 이름 |
|---|---|---|---|---|---|---|---|---|---|---|
| 13001001 | rate | 10 | 0.1 | FireRate | {0}% | 0 | 10 | 11 | 40 | 냥냥펀치 연사 |
| 13001002 | range | 10 | 0.1 | Range | {0}% | 0 | 10 | 12 | 40 | 사거리 |
| 13001003 | clone | 4 | 1 | Clone | {0} | 0 | 1 | 13 | 100 | 친구 수 |
| 13001004 | crit | 10 | 0.1 | CritChance | {0}% | 0 | 10 | 14 | 80 | 치명타 확률 |
| 13001005 | dbl | 10 | 0.1 | DoubleChance | {0}% | 0 | 10 | 15 | 80 | 2연발 확률 |

근거 [프로토] L2515~2521. 비용 재화 캣닢(`1 / 2`). 이름의 영문 부분(Fire Rate 등)은 정본 문구 그대로 `name` 값에 둔다([05](05_Screens.md)).

### 7.8.5 14_rebirthPerk — 아홉 목숨 특성 4종

| 열 | 타입 | 뜻 |
|---|---|---|
| id, key | | |
| sort, active | int | |
| max | int | |
| base / perLv / floor | float | 값 = max(floor, base + perLv × lv) (floor 빈칸 = 없음) |
| statKey | enum | `DmgMul`·`CritMul`·`ObstMul`·`DiaCap` |
| valueFormat | string | 표시 문구 틀(`{0}%`, `{0}`) |
| valueBase / valueStep | float | 표시 숫자 = valueBase + valueStep × 단계(규칙 값 base/perLv와 따로 둔다: 표시는 %, 규칙은 배수) |
| levelPriceGroup | int | 21~24 |
| icon | res | 기본 아틀라스 `Atlas_UI_Icon` |
| costBase / costGrowth | float | 생성 도구 전용 round(base × 1.5^L) |

| id | key | max | base | perLv | floor | statKey | valueFormat | valueBase | valueStep | levelPriceGroup | costBase | 이름 |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 14001001 | power | 20 | 1 | 0.15 | | DmgMul | {0}% | 0 | 15 | 21 | 50 | 공격력 |
| 14001002 | crit | 20 | 2.2 | 0.2 | | CritMul | {0}% | 220 | 20 | 22 | 50 | 치명타 피해 |
| 14001003 | obst | 10 | 1 | −0.05 | 0.5 | ObstMul | {0}% | 100 | −5 | 23 | 60 | 장애물 체력 |
| 14001004 | dcap | 20 | 10 | 2 | | DiaCap | {0} | 10 | 2 | 24 | 60 | 캣닢 획득량 |

근거 [프로토] L1751~1754, L2533~2537. 비용 재화 목숨 조각(`1 / 4`). 부활해도 유지된다(L2559).

### 7.8.6 16_levelPrice — 모든 성장 비용

| 열 | 타입 | 뜻 |
|---|---|---|
| id | int | `16{group 3}{targetLevel 3}` (예 `16001001`, `16304002`) |
| group | int | 성장 표의 `levelPriceGroup` |
| targetLevel | int | 이 구매로 도달하는 수(= 현재 P + 1) |
| priceType / priceId / priceValue | consume | 비용(`;`로 여러 재화 가능) |
| needPiece | int | 함께 드는 카드 수(그룹 301 펫, [08 §8.5.2](08_Content.md)). 빈칸 = 0 |

**그룹 배정**

| group | 대상 | 재화 | 행 수 | 소유 |
|---|---|---|---|---|
| 1 / 2 / 3 | 로비 stair / weapon / income | 츄르 `1 / 1` | 30 / 149 / 50 | 07 |
| 11~15 | Area 강화 rate / range / clone / crit / dbl | 캣닢 `1 / 2` | 10 / 10 / 4 / 10 / 10 | 07 |
| 21~24 | 부활 power / crit / obst / dcap | 목숨 조각 `1 / 4` | 20 / 20 / 10 / 20 | 07 |
| 301 / 302 / 304 / 306~313 | 펫 / 친구 / 신 레벨 / 장비 | 08 정의 | 08 | [08](08_Content.md) |

**행 생성 규칙**(도구 `MV/Tables/Generate Level Price`가 아래 공식으로 그룹 1~24 행을 다시 쓴다. 런타임은 행만 읽는다)

| group | targetLevel L(1~max) 비용 | 공식 [프로토] |
|---|---|---|
| 1 | round(25 × 1.18^(L−1)) | L822 |
| 2 | round(30 × 1.043^(L−1)) | L825 |
| 3 | 20 + 8 × L | L828 `20+8(P+1)` |
| 11~15 | round(costBase × 1.6^(L−1)) | L2521 `aupCost(a, l)`, l = 현재 단계 |
| 21~24 | round(costBase × 1.5^(L−1)) | L2536 `rbCost(p, l)` |

반올림은 JS `Math.round`(`Math.Floor(x + 0.5)`). 생성 결과(검산 완료, node):

| group | priceValue 목록(L = 1부터) |
|---|---|
| 1 | 25/30/35/41/48/57/67/80/94/111/131/154/182/215/254/299/353/417/492/580/685/808/954/1125/1328/1567/1849/2181/2574/3038 |
| 2 | 30/31/33/34/36/37/39/40/42/44/46/48/50/52/54/56/59/61/64/67/70/73/76/79/82/86/90/93/98/102/106/111/115/120/126/131/137/142/149/155/162/169/176/183/191/199/208/217/226/236/246/257/268/279/291/304/317/331/345/360/375/391/408/426/444/463/483/504/525/548/572/596/622/648/676/705/736/767/800/835/871/908/947/988/1030/1075/1121/1169/1219/1272/1327/1384/1443/1505/1570/1637/1708/1781/1858/1938/2021/2108/2198/2293/2392/2494/2602/2714/2830/2952/3079/3211/3349/3493/3644/3800/3964/4134/4312/4497/4691/4892/5103/5322/5551/5790/6039/6298/6569/6852/7146/7454/7774/8108/8457/8821/9200/9596/10008/10439/10887/11356/11844/12353/12884/13438/14016/14619/15248 |
| 3 | 28/36/44/52/60/68/76/84/92/100/108/116/124/132/140/148/156/164/172/180/188/196/204/212/220/228/236/244/252/260/268/276/284/292/300/308/316/324/332/340/348/356/364/372/380/388/396/404/412/420 |
| 11, 12 | 40/64/102/164/262/419/671/1074/1718/2749 |
| 13 | 100/160/256/410 |
| 14, 15 | 80/128/205/328/524/839/1342/2147/3436/5498 |
| 21, 22 | 50/75/113/169/253/380/570/854/1281/1922/2883/4325/6487/9731/14596/21895/32842/49263/73895/110842 |
| 23 | 60/90/135/203/304/456/683/1025/1538/2307 |
| 24 | 60/90/135/203/304/456/683/1025/1538/2307/3460/5190/7785/11677/17516/26274/39410/59116/88674/133010 |

TSV 예:
```tsv
id	group	targetLevel	priceType	priceId	priceValue
16001001	1	1	1	1	25
16001002	1	2	1	1	30
16011001	11	1	1	2	40
16021001	21	1	1	4	50
```

검증: 그룹마다 targetLevel 1~max가 빈틈없이 있어야 한다(max = 해당 성장 표의 max). 생성 도구 결과와 TSV가 다르면 경고(손으로 고친 값 표시).

### 7.8.7 성장 구매 공통 흐름(MV.Meta)

1. 현재 단계 P(저장 값)와 표 `max`를 비교. P ≥ max면 버튼 `MAX`.
2. `16_levelPrice[group][P+1]`의 price consumeData를 읽는다.
3. 모든 비용을 `ResourceService`로 확인. 모자라면 버튼 비활성(정본은 실패 문구 없음, L2530).
4. `LocalRewardService.PrepareMutation`으로 비용 차감 + 단계 +1을 한 변경으로 묶어 `Commit` → 저장 1회([09](09_Save.md)).
5. 소리·연출: 로비 강화 `buy`(+ 덱 머지면 450ms 뒤 `merge`), Area 강화 `buy`, 부활 특성 `lvup`([03 §3.9.3](03_ResourcePipeline.md)).

### 7.8.8 08_adPlacement — 광고 자리

| 열 | 타입 | 뜻 |
|---|---|---|
| id, key | | `8001{순번}` 형식 `8001001`~ |
| sort, active | int | |
| adType | enum | `Reward` / `Interstitial` |
| placement | string | `AdManager`에 넘기는 자리 이름(`new AdRequest(placement)`) |
| dailyLimit | int | 하루 횟수. 0 = 무제한. 날짜 기준은 `00_config.adDayUtcOffsetHour` |
| mul | int | 보상 배율(결과 ×3). 0 = 해당 없음 |
| minArea | int | 이 값 이상 `save.area`(0기반)에서만 노출 |
| noAdsSkip | bool01 | 1이면 광고 제거(01_wealth 17 = 1)일 때 광고 없이 바로 처리. 전면은 1 |
| adRewardType / Id / Value | consume | 시청 보상 |
| icon | res | 기본 아틀라스 `Atlas_UI_Icon` |

| id | key | adType | placement | dailyLimit | mul | minArea | adReward | 근거 |
|---|---|---|---|---|---|---|---|---|
| 8001001 | collect3 | Reward | result_x3 | 0 | 3 | 0 | — | [프로토] L2445, 한도 없음([04 §4.19.2](04_GameRules.md)) |
| 8001002 | dia | Reward | basket_free_dia | 3 | 0 | 0 | `1 / 2 / 100` | [프로토] L2574 |
| 8001003 | skip | Reward | rebirth_skip | 4 | 0 | 0 | `609 / 0 / 4200` | [프로토] L2575 (70분) |
| 8001004 | interstitial | Interstitial | result_interstitial | 0 | 0 | 3 | — | [프로토] L2444(×1 수령 뒤), noAdsSkip 1 |
| 8001005 | modeTicket | Reward | content_ticket | 3 | 0 | 0 | `3 / 0 / 1` | [08 §8.7.4] |
| 8001006 | shopFree | Reward | shop_free | 2 | 0 | 0 | `0 / 0 / 0`(701 상품 보상) | [08 §8.7.4] |
| 8001007 | blessing | Reward | blessing | 4 | 0 | 0 | `630 / 0 / 10` | [08 §8.7.4] |

- key는 [05 §5.12](05_Screens.md)가 쓰는 이름(`dia`·`skip`·`collect3`·`interstitial`·`modeTicket`)과 같다. 화면 코드는 key로 찾는다.
- 04 본문의 `rewardAmount`는 `adRewardValue`다. 전면 광고 조건 `mul == 1 && save.area ≥ minArea`([04 D-24](04_GameRules.md)).
- 사용 횟수는 저장 `mvDaily.ads[key]`([09 §9.3.4](09_Save.md)). 정본 `ads.dia`·`ads.skip`(남은 횟수)은 `dailyLimit − ads[key]`로 바꿔 든다.

### 7.8.9 05_contentOpen — 해금

정크버스터즈 열 + `key`. 행 정본은 [08 §8.6.2](08_Content.md)(18행, `5001001`~`5001018`).

| 열 | 타입 | 뜻 |
|---|---|---|
| id | int | `5001{순번}` |
| key | string | 시스템 키(`rebirth`, `contentMenu`, `mode.rapidRush` …) |
| contentId | int | 모드 행이면 `400_content.id`, 메타 시스템이면 0 |
| sort, active | int | active 0 = 지도에 「업데이트 예정」 |
| icon | res | 기본 아틀라스 `Atlas_UI_Icon` |
| conditionType | int | 0 항상 / 1 Area 도달(`areaNo ≥ conditionValue`, 즉 `save.area + 1 ≥ value`) / 2 본편 런 정산 수 ≥ value |
| conditionValue | int | |
| sticky | bool01 | 1이면 한 번 열리면 유지(부활로 Area가 1로 돌아가도). 저장 `mvUnlock.opened`([09 §9.3.3](09_Save.md)) |
| showOnMap | bool01 | 지도 Area 칸에 표시(정본 `MAP_FEATS` 대체) |

정본 대응 행:

| id | key | conditionType / Value | sticky | 근거 |
|---|---|---|---|---|
| 5001001 | rebirth | 1 / 2 | 1 | [프로토] `rbOpen = rb.open \|\| area ≥ 1` L2536. `rb.open`이 sticky다. 04의 `RB_OPEN_AREA`는 이 행 |

- 정본 지도의 「레이스」(Area 7, L2585)는 행을 만들지 않는다(사용자 제외 지시, D-64).
- 판정 시점: Area 개방·런 정산 직후 MV.Meta가 1회. 새로 열리면 `showUnlock` 연출([05](05_Screens.md)).

### 7.8.10 01_wealth — 재화

Framework `WealthTable`이 읽는 표. `ResourceService(data, wealthTable, ConsumeTypeId: 1)`.

| 열 | 타입 | 뜻 |
|---|---|---|
| id | int | 양의 정수(1~) |
| type | string | 재화 이름 키(Framework 열). 08 문서의 `key`가 이 열 |
| sort, active | int | active는 `GetInt != 0`으로 읽힌다(Framework) |
| icon | res | 기본 아틀라스 `Atlas_UI_Icon` |
| maxValue | long | 0 = 상한 없음 |
| chargeShopSectionId | int | 모자랄 때 여는 상점 칸(`702_shopSection`). 0 = 없음 |

| id | type | icon | maxValue | chargeShopSectionId | 이름 | 근거 |
|---|---|---|---|---|---|---|
| 1 | gold | UI_Icon_Gold | 0 | 0 | 츄르 | [프로토] `save.gold` |
| 2 | dia | UI_Icon_Dia | 0 | 0 | 캣닢 | [프로토] `save.dia` |
| 3 | souls | UI_Icon_Soul | 0 | 0 | 생선 | [프로토] `save.souls` |
| 4 | rbToken | UI_Icon_RbToken | 0 | 0 | 목숨 조각 | [프로토] `save.rb.tokens` |

5~17은 [08 §8.7.1](08_Content.md). 아이콘 이름은 [03 §3.4.1](03_ResourcePipeline.md) 규칙(`UI_Icon_*`)이 정본이다(08 표의 `Icon_*` 표기는 `UI_Icon_*`로 고쳐진다).

### 7.8.11 02_playResource — 충전형 자원

| 열 | 타입 | 뜻 |
|---|---|---|
| id | int | 101~ |
| key | string | `ticket.alleyWar` … |
| type | string | 자원 종류 |
| chargeType | int | 1 시간 회복 / 2 일일 초기화 |
| chargeSec | int | 1회 회복 간격(초) |
| chargeAmount | int | 1회 회복량(일일 초기화면 그날 채움량) |
| chargeMax | int | 회복 상한(구매·보상은 넘길 수 있음) |
| sort, active | int | |
| icon | res | 기본 아틀라스 `Atlas_UI_Icon` |

행은 [08 §8.7.2](08_Content.md)(101~105).

### 7.8.12 07_consumeType — 보상·비용 종류 정의

정크버스터즈 「Consume정의」를 런타임 표로 올린 것. **코드에 아이콘·아틀라스 switch를 두지 않는다.**

| 열 | 타입 | 뜻 |
|---|---|---|
| typeId | int | consumeType 번호(키. 이 표만 `id` 열을 두지 않는다 — 규격 v1 §3) |
| codeName | string | C# enum `ConsumeType` 이름. Framework 표 제작 창의 코드 생성이 이 열로 enum을 만들고, 검증기가 enum과 대조 |
| idTable | string | consumeId가 가리키는 표 파일 이름. `-` = id 안 씀(0) |
| idKind | enum | `row`(행 id) / `group`(묶음 id: 50 → 801.dropGroup, 8 → 802.group) / `none` |
| valueMeaning | string | 값의 뜻(사람용) |
| valueKind | enum | `Int`(지급 때 내림) / `Double`(런 츄르) |
| iconAtlas | string | 아이콘 아틀라스 |
| iconColumn | string | idTable 행의 아이콘 열 이름, 또는 `=스프라이트이름`(고정) |
| worldBadge | res | 월드 장애물 글자판 보상 배지([03 §3.5.3](03_ResourcePipeline.md)) |
| gainSound / gainEffect | sound / effect | 런 안에서 얻을 때 소리·이펙트(wall 파괴 시 보상별 소리) |
| applyMul | enum | `None` / `GoldMul`(지급 때 goldMul 곱) |
| settleTo | string | 런 종료 정산 대상 `type/id`(예 `1/1`), 또는 `rbPending`. 빈칸 = 런 안에서만 쓰임 |
| collectMul | bool01 | 결과 ×3 광고 배율 적용 여부 |
| active | bool01 | |

| typeId | codeName | idTable | valueMeaning | valueKind | iconAtlas | iconColumn | 그 밖 |
|---|---|---|---|---|---|---|---|
| 1 | Wealth | 01_wealth | 개수 | Int | Atlas_UI_Icon | icon | |
| 3 | PlayResource | 02_playResource | 개수(id 0 = 고른 모드 입장권) | Int | Atlas_UI_Icon | icon | |
| 5 | Item | 103_item | 장수(시작 덱 추가, 3장 머지) | Int | Atlas_UI_Icon | icon | |
| 8 | RandomBox | 802_randomBox | 개봉 횟수(id = group) | Int | Atlas_UI_Icon | icon | |
| 50 | Drop | 801_drop | 굴림 횟수(id = dropGroup) | Int | Atlas_UI_Icon | =UI_Icon_Drop | 표시 안 함이 기본 |
| 100 | AD | - | 광고 1회(비용 전용) | Int | Atlas_UI_Icon | =UI_Icon_Ad | |
| 101 | Inapp | - | 가격(스텁) | Int | Atlas_UI_Icon | =UI_Icon_Inapp | |
| 300 | DailyQuestPoint | - | 점수 | Int | Atlas_UI_Icon | =UI_Icon_QuestDaily | |
| 301 | WeeklyQuestPoint | - | 점수 | Int | Atlas_UI_Icon | =UI_Icon_QuestWeekly | |
| 500 | Pass | 410_questGroup | 패스 XP | Int | Atlas_UI_Icon | =UI_Icon_PassXp | |
| 600 | StairLv | - | 런 게이지 Lv + 값 | Int | Atlas_UI_Icon | =UI_Icon_StairLv | 런 전용 |
| 601 | Card | 110_card | 장수(id 0 = 현재 풀에서 무작위) | Int | Atlas_UI_Card | icon | 런 전용(applyCard) |
| 602 | RunGold | - | 런 츄르 | Double | Atlas_UI_Icon | =UI_Icon_Gold | applyMul GoldMul, settleTo `1/1`, collectMul 1, gainSound coin, worldBadge `World_Item_Badge_Gold` |
| 603 | RunSouls | - | 런 생선 | Int | Atlas_UI_Icon | =UI_Icon_Soul | settleTo `1/3`, collectMul 0, gainSound soul, gainEffect run.wallRewardSoul |
| 604 | RunDia | - | 런 캣닢 | Int | Atlas_UI_Icon | =UI_Icon_Dia | settleTo `1/2`, collectMul 1, gainSound gem, gainEffect run.wallRewardDia, worldBadge `World_Item_Badge_Dia` |
| 605 | RunStairs | - | 모은 계단 수 | Int | Atlas_UI_Hud | =UI_Icon_Stair | 런 전용 |
| 606 | RunWeapon | 103_item | 런 손패 카드 장수 | Int | Atlas_UI_Icon | icon | 런 전용(공방 수령 경로) |
| 607 | GaugeFill | - | 우물 게이지 양 | Double | Atlas_UI_Hud | =UI_Icon_Gauge | 런 전용 |
| 608 | RbPending | - | 목숨 조각 적립 대기 | Int | Atlas_UI_Icon | =UI_Icon_RbToken | settleTo `rbPending`, collectMul 0 |
| 609 | RebirthCooldownSec | - | 부활 쿨타임 감소(초) | Int | Atlas_UI_Icon | =UI_Icon_Clock | 메타 |
| 610 | Gear | 471_gearRarity | 장비 개수 | Int | Atlas_UI_Icon | icon | [08 §8.7.3] |
| 620 | PetCard | 430_pet | 장 | Int | Atlas_UI_Icon | icon | [08] |
| 621 | FriendCoin | 450_friend | 개(id 0 = 지금 올리는 친구) | Int | Atlas_UI_Icon | icon | [08] |
| 622 | GodShard | 113_god | 개(id 0 = 지금 올리는 신) | Int | Atlas_UI_Icon | icon | [08] |
| 630 | Blessing | 480_blessing | 런 수 | Int | Atlas_UI_Icon | icon | [08] |

- **정산**(`collect(mul)`, [04 §4.11.2](04_GameRules.md)): settleTo가 있는 600번대 값을 `floor(값) × (collectMul ? mul : 1)`로 대상에 넣는다. 츄르 `floor(R.gold) × mul`, 캣닢 `R.dia × mul`, 생선 그대로, 목숨 조각은 `rb.pending += R.tok`. 한 번의 `ApplyAndSave`로 묶는다([09](09_Save.md)).
- 600번대를 런 밖(메타)에서 지급하면 검증 오류(608·609·630 제외 — 630 축복은 광고 보상(§7.8.12, `08_adPlacement`)으로 런 밖에서 준다). 1~599를 런 안 오브젝트 보상으로 쓰면 「정산 대기 목록」에 넣었다가 정산 때 지급.
- 새 type 추가 = 이 표 행 + C# enum 값 + 지급 처리(`IConsumeHandler` 1개) 등록.

### 7.8.13 09_uiResource — 표 행이 없는 화면 고정 그림

[05 §5.1](05_Screens.md) 제안을 0번대 표로 확정한다. 사이드 버튼 아이콘·닫기·잠금·HUD 고정 아이콘·비네트처럼 「그림을 가진 표 행」이 없는 화면 그림을 여기서 설정한다. **화면 코드는 `key`로만 찾고 스프라이트 이름을 코드에 쓰지 않는다.**

| 열 | 타입 | 뜻 |
|---|---|---|
| id | int | `9{group 3}{순번 3}`: 001 공용, 002 HUD, 003 로비, 004 화면 장식 |
| key | string | `<화면>.<항목>` (`lobby.side.basket`) |
| group, sort, active | int | |
| atlas | string | 아틀라스 이름. 빈칸이면 `address`를 쓴다 |
| sprite | res | 아틀라스 안 스프라이트 이름 |
| address | res | 단독 텍스처 주소(`Tex_*`) → `AddressableTexture` |
| #비고 | | |

| id | key | atlas | sprite | address | 근거 |
|---|---|---|---|---|---|
| 9001001 | common.close | Atlas_UI_Common | UI_Emoji_Close | | ✕ L351 |
| 9001002 | common.play | Atlas_UI_Common | UI_Emoji_Play | | ▶ 광고 버튼 |
| 9001003 | common.sound | Atlas_UI_Common | UI_Emoji_Sound | | ♪ L713 |
| 9001004 | common.lock | Atlas_UI_Common | UI_Emoji_Lock | | 🔒 |
| 9002001 | hud.stairCan | Atlas_UI_Hud | UI_Icon_Stair | | L589 |
| 9002002 | hud.brick | Atlas_UI_Hud | UI_Icon_Brick | | L42 |
| 9002003 | hud.gaugeIcon | Atlas_UI_Hud | UI_Icon_Fac_Tower_2 | | L869 `cannonURL` |
| 9002004 | hud.hatch | Atlas_UI_Hud | UI_Skin_HatchTile | | L530 소모 가림 무늬 |
| 9003001 | lobby.side.basket | Atlas_UI_Lobby | UI_Emoji_Basket | | 🧺 L658 |
| 9003002 | lobby.side.map | Atlas_UI_Lobby | UI_Emoji_Map | | 🗺️ L659 |
| 9003003 | lobby.side.content | Atlas_UI_Lobby | UI_Icon_Content | | `05_contentOpen[contentMenu].icon`이 비었을 때 |
| 9004001 | stage.vignette | | | Tex_Lobby_Vignette | L45 `#vig` 540×960 |

- 행 목록은 [05](05_Screens.md)가 늘린다(그 문서의 「`09_uiResource` `<key>`」 표기가 행이다). 이 표는 열·id 규칙만 소유한다.
- 같은 그림을 표 행이 이미 가진 경우(재화 아이콘 = `01_wealth.icon`)는 여기 두지 않는다.
- 검증(§7.13): atlas·sprite 또는 address 중 하나만 채움, key 유일, 이름이 카탈로그에 있음.

---

## 7.9 콘텐츠·상점·확률 표(4xx·7xx·8xx)

열 정의 정본은 [08](08_Content.md)(4xx·7xx)이고, 이 절은 목록·관계·공통 규칙과 이 문서가 소유한 확률 표 열을 정한다.

### 7.9.1 모드(400~402)

| 표 | id | 핵심 열 | 관계 |
|---|---|---|---|
| `400_content` | `400000001`(본편), `400001001`~`004` | id, key, contentType(0 본편 / 1~4 모드 종류, [08 §8.4.2](08_Content.md)), active, sort, icon, bg, bgm, banner, hudId(모드 HUD, 비면 `Run` — [05](05_Screens.md) 제안), modeRuleId, stageGroup, openId, spendResourceType/Id/Value(입장 비용) | → 402, 401.group, 05, 02, 08_adPlacement[modeTicket] |
| `401_modeStage` | `401{모드 3}{단계+1 3}` | 단계별 templateId·floorGroup·enemyHp·배율, clearReward…, firstClearReward… | → 310, 303(묶음 901~), 102 |
| `402_modeRule` | `402001001`~`004` | IModeRule 키, cardPoolId(→111 key), cycleGroup(→312), fieldGenKey(→313), climbKey(→330), layout, themeId | → 111, 312, 313, 330, 304 |

모드 전용 오브젝트(08 요청, 이 문서 표에 행 추가): `102_enemy` `mouseDefence`·`bossMouse`, `105_objectKind` `cardbox`, `109_moveAI` `Advance`, `114_hpRule` `ModeStage` — §7.9.5.

### 7.9.2 퀘스트·업적·메타 시스템(410~499)

| 표 | 비고 |
|---|---|
| `410_questGroup` | 묶음(일일·주간·패스 101~106). resetType(Daily/Weekly/None), 날짜 기준 UTC(04 D-29, 08 D-77) |
| `413_repeatQuest` · `414_passQuest` · `420_achievement` | 정크버스터즈 열: id, group, sort, active, clearType, clearCondition, clearValue, rewardType/Id/Value, icon. clearType 목록은 08(예: 런 정산 수, 벽 파괴 = `106.breakStat Walls`, 처치 = `Kills`, Area 개방, 강화 구매, 광고 시청) |
| `430_pet`·`431_petBuild`·`440_research`·`450_friend`·`451_friendPower`·`460_rune`·`470_gearPart`·`471_gearRarity`·`480_blessing`·`485_attendance`·`490_roadMap`·`495_godLevel` | [08 §8.5](08_Content.md) |
| `499_contentConfig` | 세로 키/값(`300_runConfig`와 같은 열: id, key, value, unit, #근거). **group 000 행은 묶음 이름**(key = 묶음 이름, value 빈칸), 나머지는 `499{묶음 3}{순번 3}` |

### 7.9.3 801_drop · 802_randomBox · 803·804 천장 (이 문서 소유)

**기준(2026-11-09 갱신)**: JJEvol 표 규격 v1 초안 §6~9(`JJEvolAutomation` `docs/Guides/05_게임_테이블_규격.md`)를 따른다. 추첨 엔진은 Framework 하위 패키지 `com.jjevol.economy.drop`(0.1.0-pre.1, 2026-11-09 Framework 로컬 master 병합 `48868b1b`, Push 전, EditMode 43/43 PASS·Player NOT_RUN)이다. 새 프로젝트가 받는 방식은 D-02(고정 tgz 권장)를 따르며, 고정본 묶음에 들어가기 전에는 게임 구현을 쓴다. 패키지는 표·열 이름을 정하지 않으므로 게임은 아래 모양의 표를 패키지 입력으로 넘기는 **어댑터 `MvDropSource`**(MV.Data)만 둔다. 패키지가 나오기 전에는 같은 동작의 게임 구현 `MvDropRoller`(MV.Data)를 쓰고, 나오면 어댑터로 바꾼다(표·행은 그대로). 이전 판의 「801 = 행마다 독립 확률 `rate`」, 「802 = `rate` 가중 1개」 해석은 폐기한다.

공통 규칙:
- 확률은 **정수 `weight`**(묶음 합으로 나눈 값). 읽기 쉽게 묶음 합을 10000으로 맞춘다(권장, 강제 아님). 소수·float 합 비교 금지.
- 「꽝」(아무것도 안 나옴)은 `rewardType` 0, 나머지 0인 행에 비중을 준다. 빈 결과를 확률에서 숨기지 않는다.
- 묶음 ID는 **그 묶음 첫 행의 id**(9자리)다. 예: `801001001`로 시작하는 묶음의 `dropGroup` = `801001001`. consumeType 50·8의 `{p}Id`도 이 9자리 값이다.
- 수량 범위는 `rewardValue_min`·`rewardValue_max`(같으면 고정, 균등 정수).
- 결과 행이 Drop(50)·RandomBox(8)를 가리키면 `rewardValue`는 그 **실행 횟수**다(정크버스터즈는 하위 수량을 무시했으나 이 규격은 반영). 패키지 모드: Drop(50) = 펼침(`Expand`), 결과가 RandomBox(8)이면 **상자로 지급(`Grant`)** 하고 그 자리에서 열지 않는다.
- 패키지 연결(Drop 세션 회신, 패키지 커밋 전): 열은 `LootTsvLayout`으로 넘긴다(801 Table=`dropGroup`, 802 Table=`group`·Slot=`subGroup`, Type=`rewardType`, Item=`rewardId`, Min/Max=`rewardValue_min/_max`, Weight=`weight`, Entry=`id`). `ValidateTables`에서 패키지 Build를 돌리고 `LootCatalog.Issues` 경고도 오류로 올린다. 801·802 행은 `rewardType` 빈칸 금지(꽝은 0), 꽝 행에도 `rewardId` 칸을 둔다(0). 비중은 정수로만 적는다(`1e3` 같은 지수 표기는 패키지가 거부). 하위 묶음이 여러 칸으로 펼쳐지면 패키지의 Probability는 「기대 등장 횟수」(1 초과 가능)라서 확률 공개 화면은 그 값을 「확률」이 아니라 「평균 개수」로 적는다([05](05_Screens.md)).
- 추첨과 확률 표시(내용 보기 화면, [05](05_Screens.md))는 **같은 그래프 계산**을 쓴다. 하위 확률 = 부모 행 확률 × 하위 비중 / 하위 합.
- 난수: 런 안이면 RunRng(04 §4.17), 메타면 MetaRng. 추첨은 **균등 난수 1개 `u`**를 받아 `u × 합`이 누적 비중 구간에 드는 행을 고른다(행 순서 = id 오름차순). 정본의 `Math.random() < p` 한 번과 같은 호출 수가 되게 하려는 것이다(기록 재생 비교, [12](12_Acceptance.md)).

**801_drop — 묶음에서 1행 추첨**: 한 번 실행하면 같은 `dropGroup` 안에서 **한 행**을 `weight`로 뽑는다. 독립 확률(예: 적 캣닢 8%)은 「보상 행 + 꽝 행」 묶음 하나로 적는다.

| 열 | 타입 | 뜻 |
|---|---|---|
| id | int | `801{분류 3}{순번 3}` |
| dropGroup | int | 묶음 ID = 묶음 첫 행 id |
| rewardType / rewardId | consume | 결과. 0 = 꽝. Drop·RandomBox를 가리켜도 된다 |
| rewardValue_min / rewardValue_max | long | 수량 범위(같으면 고정) |
| rewardValueRule | enum | **게임 확장 열**(§7.1.7). `Flat`(기본) / `DiaCap` / `HalfDiaCapCeil`. Flat이 아니면 min·max는 0으로 두고 어댑터가 추첨 뒤 수량을 계산한다. 패키지 입력에는 넘기지 않는다 |
| weight | int | 비중 |
| icon | res | 표시용(빈칸 = consumeType 아이콘) |
| #분류, #이름 | | 기획자용 |

| id | dropGroup | rewardType | rewardId | rewardValue_min | rewardValue_max | rewardValueRule | weight | 근거 |
|---|---|---|---|---|---|---|---|---|
| 801001001 | 801001001 | 604 | 0 | 0 | 0 | HalfDiaCapCeil | 800 | [프로토] L1925~1927 적 처치 8% 캣닢 |
| 801001002 | 801001001 | 0 | 0 | 0 | 0 | Flat | 9200 | 꽝 92% |

정본 순서: 처치 보상 지급 → 드롭 추첨. 보상 행을 꽝 행보다 앞(작은 id)에 두어 `u < 0.08`과 같은 판정이 되게 한다.

**802_randomBox — 칸(subGroup)마다 1행씩 모두 지급**: 한 번 열면 같은 `group` 안에서 `subGroup`마다 한 행씩 `weight`로 뽑아 모두 준다. 같은 `subGroup` 안에서는 하나만, `subGroup`이 다르면 각각 나온다. 칸 결과는 보상을 바로 줄 수도 있고(`rewardType` 1·5·620 …) Drop을 거칠 수도 있다(`rewardType` 50).

| 열 | 타입 | 뜻 |
|---|---|---|
| id | int | `802{분류 3}{순번 3}` |
| group | int | 상자 ID = 상자 첫 행 id. consumeType 8의 id |
| subGroup | int | 칸 번호, 1부터 |
| rewardType / rewardId | consume | 그 칸의 결과(0 = 꽝) |
| rewardValue_min / rewardValue_max | long | 수량 범위 |
| weight | int | 칸 안 비중 |
| icon | res | 확률 표시용 아이콘(그룹 첫 행 값이 상자 아이콘) |
| nameKey | string | 상자 이름 문구 키(그룹 첫 행만, 빈칸 = `{id}`) |

상자 등급처럼 같은 상자에 다른 구성이 필요하면 `group`을 따로 둔다(희귀도 → 품목 2단계는 「칸 결과 `rewardType` 50 → 801 묶음」으로 적는다). 행은 [08 §8.5.12](08_Content.md).

**804_randomGuarantee — 천장 정책 / 803_randomGuaranteeLink — 연결**: 규격 §8 열을 그대로 쓴다. 1차 출시 행은 **0개**([창작] 정본에 천장 없음). 표·로더·검증·저장 칸만 만들어 두고, 확률형 상품을 넣을 때 행을 추가한다.

| 표 | 열 |
|---|---|
| `804_randomGuarantee` | id, active, countType(1 FailStreak / 2 AttemptCycle), triggerCount(≥1), rewardMode(1 Replace / 2 Add), rewardBoxGroup, rewardBoxSubGroup(0 = 전체 칸), priority |
| `803_randomGuaranteeLink` | id, guaranteeId(→804), sourceBoxGroup(→802.group), sourceBoxSubGroup(0 = 상자 1회 = 1번 셈) |

- 셈 값은 저장 데이터다. 키는 `guaranteeId`, 컨테이너 `mvShop.guarantee`([09 §9.3.5](09_Save.md)).
- 확률 공개 화면은 천장을 확률에 섞지 않고 「N회 안에 확정」으로 따로 적는다.

### 7.9.4 상점(700~702)

| 표 | 핵심 열 |
|---|---|
| `700_productTab` | id, sort, active, icon |
| `702_shopSection` | id, active, sort, sectionType, hideIfEmpty, tabIds(`/`, 700 id) — 열 정본 [08 §8.5.7](08_Content.md) |
| `701_product` | id, tabId, active, sort, icon, rewardType/Id/Value, priceType/Id/Value(100 AD / 101 Inapp / 1 재화), chargeType(0 무제한 / 1 평생 / 2 일일), chargedQuantity, googleProductId·appleProductId(스텁), minArea, minLevel, cooldownHours, perArea — 열 정본 [08 §8.5.7](08_Content.md) |

행은 [08 §8.5.7](08_Content.md). 구매 기록 저장은 [09](09_Save.md) `mvShop`.

### 7.9.5 모드 전용 개체 행(이 문서 표에 추가, 값 정본 08)

| 표 | id | key | 열 요점 |
|---|---|---|---|
| `114_hpRule` | 114001007 | modeStage | type `ModeStage`: HP = 현재 `401_modeStage.enemyHp`(×배율 열) |
| `109_moveAI` | 109001003 | advance | type `Advance`: z가 매초 speed만큼 감소, x 고정, 추적·훔치기 없음. speed = `401_modeStage.enemySpeed`(행 speedMin/Max 빈칸) |
| `102_enemy` | 102001002 | mouseDefence | hpRuleId modeStage, moveAiId advance, 보상 없음, sprite `World_Enemy_Mouse` |
| `102_enemy` | 102001003 | bossMouse | hw 2.2, hd 1.2(102에 `hw`·`hd` 덮어쓰기 열 추가, 빈칸 = 105 값), sprite `World_Enemy_BigMouse`(03 신규), hpRuleId modeStage |
| `105_objectKind` | 105001013 | cardbox | breakable 1, hittable Always, hpRule box06, receive onBreak, reward `601 / 0 / 1`(현재 풀 무작위 1장), sprite 08·03 |
| `310_template` | 310000101, 310000102 | rush1, rush2 | 츄르 털이 전용(08 §8.4.7), 311 행 `311101xxx`·`311102xxx` |
| `111_cardPool` | 111002xxx, 111003xxx | defence, rush | §7.7.4 |

`102_enemy`에 열 추가: `hw`, `hd`(float, 빈칸 = 105 값), `scale`(그림 배율, 빈칸 = 1).

---

## 7.10 리소스 열 규칙

사용자 원문 「리소스도 테이블에서 설정가능해야해」의 답이다. 별도의 「논리 키 → 주소」 표는 두지 않는다. **그림·소리·이펙트는 그것을 쓰는 표 행의 리소스 열에 직접 적는다.** 이름 규칙·아틀라스 구성의 정본은 [03 §3.4, §3.8](03_ResourcePipeline.md).

### 7.10.1 값의 종류

| 열 이름 | 값 | 로더 |
|---|---|---|
| `icon`, `largeIcon`, `mapIcon`, `frameSprite`, 그 밖 UI 그림 | 아틀라스 안 **스프라이트 이름** | `AddressableImage(atlasKey, spriteName)` |
| `illust`, `bg`, `portrait`, `address` | **Addressables 주소**(`Tex_*`) | `AddressableTexture` |
| `sprite`, `projSprite`, `spriteSoul`, `worldBadge`, `roadsideProps` | 월드 스프라이트 이름(변형은 `/` 목록) | `WorldSpriteCache`(03 §3.8.8) |
| `spritePrefix` + 프레임 번호 | 접두 + `_{00..}` | `WorldSpriteCache` |
| `worldAtlas`, `atlas`, `iconAtlas`, `atlasPattern` | 아틀라스 주소(= 아틀라스 이름) | |
| `bgm`, `*Sound` | `03_sound.key` | `AudioManager.PlayAsync(soundName, …)` |
| `*Effect`, `enterEffect` | `04_effect.key` | `EffectManager.PlayAsync(EffectRequest)` |
| `color`, `tint`, `col` | `#rrggbb` | MV.View·MV.UI |

### 7.10.2 아틀라스 결정 순서

1. 행의 `atlas`(또는 `iconAtlas`) 열이 비어 있지 않으면 그 값.
2. consumeData로 아이콘을 찾는 경우(보상 칸·가격 버튼) `07_consumeType.iconAtlas`. 스프라이트는 `iconColumn`이 `=이름`이면 그 이름, 아니면 `idTable` 행의 그 열 값.
3. 표 정의의 **기본 아틀라스**(아래 표). 값 `@theme` = 현재 Area 테마의 `304_theme.worldAtlas`.

### 7.10.3 표별 리소스 열과 기본 아틀라스(전체)

| 표 | 열 | 기본 아틀라스 / 종류 |
|---|---|---|
| 01_wealth, 02_playResource | icon | `Atlas_UI_Icon` |
| 05_contentOpen | icon | `Atlas_UI_Icon` |
| 07_consumeType | iconColumn / worldBadge | iconAtlas / `@theme` |
| 08_adPlacement | icon | `Atlas_UI_Icon` |
| 09_uiResource | sprite / address | 행의 atlas / 주소 |
| 10_lobbyUpgrade | icon / facSprite | `Atlas_UI_Icon` / `Atlas_World_Lobby` |
| 13_areaUpgrade, 14_rebirthPerk | icon | `Atlas_UI_Icon` |
| 101_cat | spritePrefix / lobbySprite | atlasPattern(`Atlas_World_Cat_T{t}` 등) / `Atlas_World_Lobby` |
| 102_enemy | sprite(접두), hitSound, breakSound, hitEffect, breakEffect | `@theme` |
| 103_item | icon / projSprite | `Atlas_UI_Icon` / `@theme` |
| 105_objectKind | sprite, spriteSoul, tint, 소리·이펙트 4열 | `@theme` |
| 108_receiveCondition | passEffect, passSound | 06 / 03 키 |
| 109_moveAI | stealSound, stealEffect | |
| 110_card | icon / projSprite | `Atlas_UI_Card` / `@theme` |
| 112_cardEffect | procEffect, procSound | |
| 113_god | portrait / icon | `Tex_God_*` 단독 / `Atlas_UI_Icon` |
| 115_cardTier | frameSprite, cardSound | `Atlas_UI_Card` |
| 301_zone | mapIcon, bgm | `Atlas_UI_Lobby` |
| 302_area | mapIcon / bg / bgm | `Atlas_UI_Lobby` / `Tex_*` |
| 304_theme | worldAtlas, roadsideProps, bgm | (아틀라스 자신) / 자신의 worldAtlas |
| 330_climb(워프 행) | enterEffect, enterSound | |
| 400_content | icon / bg / bgm | `Atlas_UI_Icon` / `Tex_*` |
| 4xx·7xx·8xx 그 밖 | icon | `Atlas_UI_Icon`(08 표에 다르게 적힌 것은 그 값) |

### 7.10.4 테마 변형

- 월드 그림은 **이름이 테마와 무관**하다(`World_Obj_Junk_Parcel`). 테마마다 `Atlas_World_Theme_<key>` 아틀라스가 같은 이름 목록을 가진다.
- 런이 시작되면 `302_area.themeId → 304_theme.worldAtlas`를 `WorldSpriteCache`의 현재 아틀라스로 정한다. 표 열 값은 바꾸지 않는다.
- 새 테마 = `304_theme` 행 + 아틀라스 굽기(03 §3.6.4). 오브젝트 표는 손대지 않는다.

### 7.10.5 없는 리소스

| 상황 | 개발 빌드 | 릴리스 |
|---|---|---|
| 검증 단계(표 로드)에서 이름이 카탈로그에 없음 | 오류(표 로드 실패 화면) | 빌드 전 검증에서 막힘(출시 불가) |
| 런타임에 아틀라스 로드 실패 | 오류 로그 + 자홍색 대체 스프라이트 | 빈 그림 + 오류 보고 1회 |
| 소리·이펙트 키 없음 | 오류 로그 | 무시 |

검증 근거는 [03 §3.8.6](03_ResourcePipeline.md): 각 아틀라스의 `sprite_catalog.tsv`, Addressables 주소 목록, `03_sound.key`, `04_effect.key`. `@theme` 열은 **모든** 테마 아틀라스에서 확인한다.

---

## 7.11 소리·이펙트 표

### 7.11.1 03_sound

| 열 | 타입 | 뜻 |
|---|---|---|
| id | int | `3{그룹 3}{순번 3}` — 그룹 001 효과음, 002 BGM |
| key | string | 다른 표의 소리 열 값. 변형 소리는 `tierup_1`~`tierup_4`, `card_0`~`card_4`처럼 끝에 번호(코드가 `key + "_" + n`으로 만든다) |
| soundType | enum | `Sfx` / `Music` |
| soundName | res | Addressables 주소(`Sfx_*`, `Bgm_*`) |
| volume | float | 0~1 (버스 볼륨 위에 곱함) |
| pitchRand | float | 무작위 피치 폭(0 = 없음, 정본 0) |
| gapMs | int | 같은 키 최소 간격(ms). 간격 안 요청은 버림(정본 `GAP`, L1678~1679) |
| #비고 | | |

행은 **32행**(효과음 30: `3001001`~`3001030`, BGM 2: `3002001 bgm_lobby`, `3002002 bgm_run`)이며 정본은 [03 §3.9.3](03_ResourcePipeline.md). 이 문서의 소리 열 값(`clank`, `wood`, `coin`, `gem`, `soul`, `gate`, `forge`, `floor`, `steal`, `card_n`, `bgm_run`)은 그 행의 key다.

### 7.11.2 04_effect

열과 행의 정본은 [06 §6.4](06_Effects.md). 열 목록을 확정한다.

| 열 | 타입 | 뜻 |
|---|---|---|
| id | int | `400{그룹 1}{순번 3}` 형식 7자리: 4001xxx 기본 종류(14), 4002xxx 런 사건(23), 4003xxx 신 연출, 4004xxx 우다다, 4005xxx 룰렛·UI |
| key | string | `fx.sparks`, `run.wallBreak` … (다른 표의 이펙트 열 값) |
| type | enum | `particle` / `shape` / `group`(children 묶음) 등 06 정의 |
| prefab | res | 프리팹 Addressables 주소(`Fx/Sparks`) |
| poolSize | int | |
| blend | enum | `add` / `normal` |
| sortBias | float | 깊이 정렬 편향 |
| color | string | `#rrggbb` 또는 토큰 `$tier`(115 색) / `$elem`(원소색) / `$rew`(보상별 분기) |
| count, speed, size, life, grav, radius | float | 기본 파라미터 |
| flag | string | 자식 행 조건(`$rew` 분기 등) |
| children | strList | 함께 재생할 04 key 목록 |
| y | float | 높이 |
| anchor | enum | `hit` / `obj` / `player` / `climbTop` |
| chance | float | 재생 확률 |
| shake | string | `shake.*` 키 앞부분 |
| hitStop | string | `hitstop.*` 키 |
| #근거 | | |

이 문서가 참조하는 key: `run.towerHit`, `run.enemyKill`, `run.wallHit`, `run.wallBreak`, `run.wallRewardSoul`, `run.wallRewardDia`, `run.coinHit`, `run.coinBreak`, `run.statGatePass`(sgate), `run.cloneGatePass`(cgate), `run.steal`, `run.brickFlush`, `climb.stage`.

---

## 7.12 문구 키

Localization(Unity String Tables)을 `LocalizationManager.GetLoc(collection, key)`로 읽는다. 표 행 문구는 `TableRuntimeOptions.Localization`(`ITableLocalization`)이 `TableData.GetLoc`로 연결한다.

| 컬렉션 | 키 | 쓰는 곳 |
|---|---|---|
| `name` | 행 id 문자열(`302001001`) | 모든 표 행의 이름 |
| `description` | 행 id 문자열 | 설명·부제·외형 |
| `ui` | `ui.<화면>.<항목>` | 화면 고정 문구([05](05_Screens.md) 정본) |
| `msg` | `msg.<항목>` | 런 팝업 문구·긴 안내 |
| `toastMessage` | `toastMessage.<항목>` | 토스트 |

규칙:
- 표에 문구 키 열을 따로 두지 않는다(행 id가 키). 예외: 한 행이 여러 문구를 쓰면 `labelKey`처럼 `ui` 키 열을 둔다.
- 자리 표시 `{0}`, `{1}` — `string.Format(CultureInfo.InvariantCulture, …)`. 숫자 서식(`fmt`, `toFixed`)은 [05](05_Screens.md) 함수로 먼저 문자열로 만든 뒤 넣는다.
- 영문 대문자 고정 문구(COMMON, LEGENDARY, Area, Zone, MAX)도 `name`/`ui`에 둔다(번역 대비).
- 문구가 없으면 개발 빌드는 `#키`를 그대로 보여 주고 경고. 검증기가 active 행의 `name` 키 누락을 경고로 낸다.

이 문서가 정한 런 문구 키(05·06이 같은 키를 쓴다):

| 키 | ko 원문 [프로토] | 근거 |
|---|---|---|
| `ui.run.label.tower` | 벽돌 우물 · 맞히면 계단 Lv UP | L1349 |
| `ui.run.label.stair` | 우다다 계단 · 모은 계단만큼 돌파 | L1435 |
| `ui.run.label.sgate` | 계단 모으기 | L1506 |
| `ui.run.label.sgatePer` | 칸당 +{0} | L1509 |
| `ui.run.label.cgate` | 장난감 공방 | L1512 |
| `ui.run.label.cgateHint` | 맞히고 → 통과 | L1512 |
| `ui.run.floorN` | 골목 {0} | L1791 `FLOOR_N` |
| `ui.run.areaGate` | Area {0} 돌파 | L1801 |
| `toastMessage.noStairs` | 모은 계단이 없습니다 | L2244 |
| `toastMessage.rouletteNone` | 우다다 {0}칸 · 5칸 미만이라 가호 없음 | L2359 |
| `toastMessage.rebirthLocked` | 부활은 Area 2를 열면 해금됩니다 | L2572 |
| `msg.popClimb` | {0}칸 우다다! | L2285 |
| `msg.popTransform` | 변신! {0} | L1221 |
| `msg.popSteal` | 츄르 -{0} | L2063 |
| `msg.popStealNone` | 휙! | L2063 |
| `msg.popBlastBolt` / `msg.popBlastFire` | 하악! / 햇살 폭발! | L2179 |
| `ui.item.look1` ~ `ui.item.look8` | 방울 목걸이 / 새싹 머리 / 병뚜껑 모자 / 깡통 투구 / 해적 반다나 / 털모자 / 방울 마법사 모자 / 황금 왕관 | L3264 `LOOKS`의 ` · ` 앞(`103_item.lookName`) |

강화 표시(`13_areaUpgrade`·`14_rebirthPerk`)는 문구 키가 아니라 행의 `valueFormat`(`{0}%`) + `valueBase + valueStep × 단계`로 만든다([05](05_Screens.md)).

---

## 7.13 검증 규칙(`MvTableValidator`)

두 곳에서 같은 검사 함수를 부른다: 에디터 `TableBuilder` 빌드 때 `TableAuthoringOptions.ValidateTables` 콜백(작성 검증, **필수 경로** — §7.1.1 「반영 경로」, 등록은 `MV.Editor`), 부팅·핫 리로드 때 `MvTableValidator.ValidateRuntime`(§7.1.8). 결과는 `오류`(로드 실패) / `경고`(로그). 문구 형식: `[표 파일] id=값 열=이름: 내용`.

### 7.13.1 형식

| 규칙 | 등급 |
|---|---|
| 헤더에 정의된 필수 열이 모두 있음, 같은 열 이름 두 번 없음 | 오류 |
| id 중복 없음, id가 `{표번호}…` 접두로 시작(작은 정의 표 제외) | 오류 |
| 원문 문자열 기준 타입 확인(int·float·bool01은 `0`/`1`만·enum·color·list) — `TSVRow`의 조용한 기본값을 믿지 않음 | 오류 |
| key 열 유일, 영문·숫자·`.`·`_`만 | 오류 |
| 단일 행 표(`00_config`)는 정확히 1행, `06_balancePreset.isDefault` 정확히 1행 | 오류 |

### 7.13.2 참조·consumeData

| 규칙 | 등급 |
|---|---|
| 모든 fk 열 값이 대상 표에 있음(0·빈칸 허용 열 제외) | 오류 |
| consumeData: §7.1.6 개수 규칙, type이 07에 있고 active, id가 idTable에 있음 | 오류 |
| 600번대 type은 런 표(102·105·313·801·802 중 런 드롭 그룹·401 clearReward 제외)에서만, 608·609와 메타 보관 재화 610·620·621·622·630은 어디서나 | 오류 |
| `07_consumeType.codeName`과 C# `ConsumeType` enum이 1:1, idKind와 `{p}Id`가 맞음(group이면 묶음 첫 행 id) | 오류 |
| `300_runConfig`·`499_contentConfig` 키 = 코드 상수 목록(누락·남음) | 오류 |
| 소리·이펙트 열 값이 `03_sound.key`·`04_effect.key`에 있음(변형 소리는 `_n` 붙인 key 확인) | 오류 |
| 리소스 열(§7.10.5) | 오류 |
| active 행의 `name:{id}` 문구 존재 | 경고 |

### 7.13.3 의미 규칙

| 표 | 규칙 | 등급 |
|---|---|---|
| 301·302 | areaNo 1부터 연속, Zone 범위 겹침·빈틈 없음, `PLAYABLE_AREAS ≤ active Area 수` | 오류 |
| 303 | Area가 쓰는 floorGroup은 floor 1~FLOOR_COUNT 존재(모드 전용 묶음 901~은 floor 1부터 빈틈 없이), rows ≥ 1, h0 ≤ h1. Baked: soulCells·soulAmts 개수 같음, 합 = souls, 모든 idx < rows×3, 생선 idx ≥ soulMinIdx, 생선·캣닢 칸 겹침 없음 | 오류 |
| 303 | 같은 Baked 묶음을 여러 Area가 공유 | 경고 |
| 310·311 | 템플릿마다 stair ≥ 1, 모든 z < endZ, `|x| ≤ LANE_X_MAX + hw`, wall 종류 없음(blocks=1 종류는 템플릿 금지) | 오류 |
| 312 | cycleGroup 0이 Level 1~∞를 빈틈·겹침 없이 덮음 | 오류 |
| 314 | clone 그룹 행 수 ≥ MAX_CLONES | 오류 |
| 105 | 컴포넌트 type에 구현 등록됨, enemyId ≠ 0이면 rewardSource=Enemy, blocks=1이면 hpRule 있음, hittable BeforePass면 receive OnPass | 오류 |
| 105 | gem 행 rewardValue ≠ `14_rebirthPerk[dcap].base` | 경고 |
| 107 | Pow/Add면 step > 0, cap ≥ 0 | 오류 |
| 108 | OnPass면 margin > 0, insideX > 0 | 오류 |
| 109 | Chase면 speedMin ≤ speedMax, aggro > 0 | 오류 |
| 110·111 | 카드 tier가 115에 있음, 풀마다 tier 0 카드 ≥ 1 | 오류 |
| 115 | need 오름차순, 마지막 need = `330_climb.lap` | 오류 |
| 10·13·14·16 | levelPriceGroup 행이 targetLevel 1~max 빈틈없이 있음. `10[stair].max = Σ11.need(lv<LOBBY_STAIR_MAX_LV)`, `10[weapon].max = Σ12.need(lv 1~7) + WEAPON_EXTRA_BUYS` | 오류 |
| 16 | 생성 도구 결과와 다름 | 경고 |
| 801·802 | weight ≥ 0 정수, 묶음(802는 칸)마다 weight 합 > 0, dropGroup·group = 묶음 첫 행 id, 802 subGroup ≥ 1 | 오류 |
| 801·802 | 꽝 행(rewardType 0)은 rewardId·rewardValue_min·_max가 모두 0. 그 밖 행은 0 < min ≤ max(rewardValueRule ≠ Flat이면 min·max 0) | 오류 |
| 801·802 | 50·8 참조를 따라간 그래프에 순환 없음, 깊이 ≤ 5 | 오류 |
| 801·802 | 묶음 weight 합 ≠ 10000 | 경고(읽기 쉬움 권장일 뿐 동작은 비율) |
| 803·804 | 804 triggerCount ≥ 1, countType·rewardMode 값 범위, rewardBoxGroup·sourceBoxGroup이 802에 있음 | 오류 |
| 08 | placement·key 유일, dailyLimit ≥ 0. 화면이 찾는 key(`dia`·`skip`·`collect3`·`interstitial`·`modeTicket`)가 있음 | 오류 |
| 09 | key 유일, `atlas+sprite`와 `address` 중 하나만 채움 | 오류 |
| 13·14 | valueFormat에 `{0}`가 정확히 1개 | 오류 |
| 짝 값 | `330.rlFlyMs = flyCard.removeMs`, `00_config.collectAdMul = 08[collect3].mul`, `108[sgatePass].delayMs = fly.removeMs` | 경고 |

### 7.13.4 저장 참조(표가 바뀐 뒤 옛 저장을 읽을 때)

| 저장 값 | 표에 없으면 |
|---|---|
| 출전 신 key | `00_config.startGodKey` |
| 덱 등급(인덱스) > 103 최대 tier | 최대 tier로 합침 |
| `save.area` ≥ Area 수 | 마지막 Area, cp 0 |
| 성장 단계 > 표 max | max로 자름 |
| 광고·해금·모드 key | 무시(다음 저장 때 지움) |

이 처리는 MV.Save의 상태 파싱(`ParseState`) 단계에서 한다([09](09_Save.md)).

---

## 7.14 확장 예시 — 표만 고쳐서 늘리기

목표: 아래 두 예시가 **코드 수정 없이** 표 행 추가(+ 그림 굽기·문구)만으로 끝나야 한다. 끝나지 않으면 표 설계가 틀린 것이다.

### 7.14.1 새 오브젝트 종류 「쥐구멍 함정」 [창작]

규칙: 길 위에 놓인 쥐구멍. 쏘아서 부수면 츄르를 주고, 부수기 전에 밟으면 츄르를 훔쳐 가고 사라진다. 막지 않는다(blocks 0).

| 단계 | 표 | 할 일 |
|---|---|---|
| 1 | `114_hpRule` | 체력 규칙 행(기존 `box06`을 써도 되지만 예시로 새 행) |
| 2 | `109_moveAI` | 움직이지 않고 밟으면 훔치는 행(`Static` + `touchAction Steal`, §7.5.6) |
| 3 | `105_objectKind` | 종류 행: 컴포넌트 5개를 id로 조립, 보상·그림·소리·이펙트 열 |
| 4 | `311_templateObject` | 템플릿 3에 1개 배치(편집 도구로 저장해도 같은 행) |
| 5 | 문구 | `name:105001014` 「쥐구멍 함정」 |
| 6 | 그림 | 모든 `Atlas_World_Theme_<key>`에 `World_Obj_Trap_MouseHole` 추가(03 §3.6). 빠진 테마가 있으면 검증 오류 |

```tsv
id	key	type	mul	fixed	useScale	useObstMul
114001008	trap03	AreaHpMul	0.3		0	0
```

```tsv
id	key	type	aggro	speedMin	speedMax	xFollow	touchX	touchZ	touchAction	stealBase	stealPerZone	stealShake	stealSound	stealEffect	despawnBehind
109001004	mouseHole	Static					0.9	0.5	Steal	3	2	shake.steal	steal	run.steal	0
```

```tsv
id	key	sort	active	hw	hd	hittable	breakable	blocks	hpRuleId	hitReactionId	chargeGateId	receiveId	moveAiId	enemyId	rewardSource	rewardType	rewardId	rewardValue	rewardValuePerZone	rewardValueRule	hitRewardType	hitRewardId	hitRewardValue	sprite	spritePick	labelKind	hitSound	breakSound	hitEffect	breakEffect
105001014	mouseHole	14	1	0.8	0.5	Always	1	0	114001008	106001004	0	108001001	109001004	0	Row	602	0	5	3	Flat				World_Obj_Trap_MouseHole	Single	BoxHp		wood	run.coinHit	run.wallBreak
```

```tsv
id	templateId	seq	kindId	x	z	groupId	active	#kind
311003012	310000003	12	105001014	0	120	0	1	mouseHole
```

결과: 템플릿 3 판에서 z 120에 쥐구멍이 생긴다. HP = round(Area HP × 0.3), 부수면 츄르 5 + 3×zone(× goldMul), 밟으면 `min(floor(츄르), 3 + 2×zone)` 훔침. `ObjectKindRegistry.Build`가 행을 읽어 `ObjectKindDef`를 만들고 `RunObject`가 컴포넌트로 돈다 — 새 C# 클래스 없음.

**코드가 필요해지는 경우**(표만으로 안 되는 경계):
- 새 컴포넌트 type(예 「밟으면 느려짐」 `touchAction Slow`) → `IMoveAi`·`IHitReaction` 등 구현 1개 + enum 값 + 레지스트리 등록. 표 행은 그 뒤 같은 방식.
- 새 보상 종류 → `07_consumeType` 행 + `IConsumeHandler` 1개(§7.8.12).

### 7.14.2 Area 31 추가

[08 §8.1.4](08_Content.md) C5 외삽 공식으로 값을 만든다: `hp(31) = R1(hp(30) × 1.15) = R1(69000) = 69000`, Floor h0·h1 = Area 30 값 × 1.15를 R1, rows·souls는 Area 30과 같음, `needSouls(31) = 1695 + 60 = 1755`. needSouls는 R1을 하지 않는다(1800으로 올리면 증가분 105 > Area 생선 합 70이라 막힌다, 08 §8.2.3 「여유」 규칙).

| 단계 | 표 | 할 일 |
|---|---|---|
| 1 | `302_area` | 행 1개(themeId는 기존 테마 재사용 `moonstair`) |
| 2 | `303_areaFloor` | 묶음 31의 Floor 3행 |
| 3 | `301_zone` | Zone 6 `areaTo` 30 → 31 (또는 Zone 7 행 추가 + 지도 색) |
| 4 | `300_runConfig` | `PLAYABLE_AREAS` 30 → 31(D-63). 08의 `openAreas`를 쓰면 그 값도 같이 |
| 5 | 문구 | `name:302001031` 지역 이름 |
| 6 | 검증 | §7.13.3: areaNo 연속, Zone 범위 빈틈 없음, `PLAYABLE_AREAS ≤ active Area 수`, 생선 여유(합 − 증가 ≥ 10) 경고 |

```tsv
id	areaNo	zoneId	sort	active	themeId	hp	needSouls	floorGroup	bgm	mapIcon	clearRewardType	clearRewardId	clearRewardValue
302001031	31	301001006	31	1	304001015	69000	1755	31				
```

```tsv
id	group	floor	h0	h1	rows	souls	cellMode
303031001	31	1	3700	1200000	14	18	Hash
303031002	31	2	1200000	12000000	16	23	Hash
303031003	31	3	12000000	24000000	18	29	Hash
```

- R1(≥ 1000은 유효숫자 2자리): 3200×1.15 = 3680 → 3700, 1,000,000×1.15 = 1,150,000 → 1,200,000, 10,500,000×1.15 = 12,075,000 → 12,000,000, 21,000,000×1.15 = 24,150,000 → 24,000,000.
- 필드 칸은 Hash라 굳힘 열이 필요 없다. 시드 A = `fieldSeedArea`(빈칸 = areaNo − 1 = 30).
- 런·지도·결과·Area 강화 초기화는 모두 `302_area` 행 수와 `PLAYABLE_AREAS`로 돈다 — 코드 수정 없음.

---

## 7.15 의문점·결정 필요

| ID | 질문 | 선택지 | 권장 | 영향 |
|---|---|---|---|---|
| **D-60** | 보상·비용 표기 형식 | A consumeData 3열(`…Type`/`…Id`/`…Value`, `;` 목록) / B 한 칸 문자열(`602:0:20`) / C 보상 전용 표(`34_Reward` 식 묶음 id) | **A**. 사용자 지시(「보상같은건 consumeData형태로 테이블에서 수정 가능」)와 정크버스터즈 구조에 맞고, 표 편집기에서 열별 필터가 된다. 묶음이 큰 보상은 `802_randomBox` 그룹을 consumeType 8로 가리킨다 | 모든 보상·비용 열, `ConsumeData.Import`, 검증기 |
| **D-61** | 필드 칸(생선·캣닢 위치) 생성 | A 런타임 Hash(정본 fieldSpec) + Baked 열은 대체용 / B 전부 Baked(표에 칸 목록) / C Hash만(Baked 열 삭제) | **A**. 정본과 같은 배치가 기본이고, 기획이 특정 Area를 손으로 바꾸고 싶을 때만 `cellMode=Baked`. Area 1~15 골든 값을 시험에 쓴다(04 D-27) | `303_areaFloor`, 313, EditMode 시험 |
| **D-62** | 정본에서 얻을 수 없는 잔재 카드 효과 7종 | A 구현 + active 0 / B 표에서 삭제 / C 기본 풀에 넣음 | **A**(04 D-20과 같음). 08 신 확장이 쓰면 바로 동작 | `110_card`·`112_cardEffect` 행, 카드 구현 범위 |
| **D-63** | 진행 끝 Area(`PLAYABLE_AREAS`) | A 5(정본 `PROTO_AREAS`) / B 30(08 행 전부) / C 출시 단계별로 표 값만 올림 | **C**. 첫 빌드는 5로 정본과 같게 검수, 08 Area 16~30 검수가 끝나면 값만 30. 08의 `openAreas`와 둘 중 하나로 합칠 것(권장: `300_runConfig.PLAYABLE_AREAS` 하나, 302 active는 지도 표시만) | 302 active, 지도 「업데이트 예정」, 결과 zoneEnd, 04 D-26 |
| **D-64** | 정본 지도의 「레이스」 항목 | A 넣지 않음 / B 잠긴 카드로 표시 | **A**. 사용자 제외 지시(레이스·길드·Guild Raid·팀 이벤트). 기록용 | `05_contentOpen` 행 없음 |
| **D-65** | 배치 편집 도구 범위 | A 템플릿(310·311)만 / B 필드(303 Baked 칸)도 편집 / C 편집 도구 없이 TSV 직접 | **A**. 필드는 규칙 생성이라 편집 대상이 아님. Baked가 필요하면 굳힘 결과를 TSV로 넣는다. 도구는 개발 전용 | MV-DATA 작업량, `MV/Placement Editor` |

### 7.15.1 다른 문서와 다른 이름(이 문서가 정본)

| 다른 문서 표기 | 이 문서 | 비고 |
|---|---|---|
| 04 `floorSetId` | `302_area.floorGroup` | 08과 같은 이름 |
| 04 `RL_*` 8개 | `330_climb` 설정 행(`330000001`)의 `rl*` 열 | 워프 행과 rowType으로 구분 |
| 04 `RB_OPEN_AREA` | `05_contentOpen[rebirth]` conditionType 1 = areaNo(2), sticky | |
| 04 `RB_ARM_SEC`·`RB_CD_SEC` | `00_config.rebirthArmSec`·`rebirthCooldownSec` | |
| 04 `DT_CAP`·`HITSTOP_*`·`SHAKE_*`·`FOG_*`·`WELL_FLUSH_SEC`·`CRAFT_ICON_GAP_MS` | 02·06 키(`time.*`·`hitstop.*`·`shake.*`·`view.fog*`·`fx.brickFlushInterval`·`fly.cloneGapMs`) | §7.3.3 대응표 |
| 04 카드 효과 이름 AddClone·Proc·Chain·Spear·DoubleShot·Blade·Chill | CloneAdd·ProcExtraHit/ProcBurn·ChainJump/ChainSplash·PierceEveryN·DoubleChance·Spin·ProcChill | §7.7.5 |
| 04 `rewardAmount` | `08_adPlacement.adRewardValue` | |
| 04 `34_Reward` 같은 보상 표 | consumeData 3열 + `801_drop`·`802_randomBox` | D-60 |
| 110 `enabled`·`scope` | `active`·`group` | 공통 열 규칙 |
| 05 `301_zone.mapColor1/2` | `mapCol1/mapCol2` | 행 정본 08과 같은 이름 |
| 05 `ui.hand.*` 10개 | 그대로 + `ui.hand.dealGapMul` 추가, 머지 장수는 104 `MERGE_N` | §7.3.3 |
| 05 강화 표시 `valueFormat`·`valueBase`·`valueStep` | 그대로(13·14) | |
| 08 `Icon_*` 스프라이트 이름 | 03 규칙 `UI_Icon_*` | 08 문서가 고침 |
| 08 `304_theme.worldAtlas` = `WorldAtlas_<key>` | `Atlas_World_Theme_<key>` | 03 이름 규칙 |
| 08 `openAreas` | `300_runConfig.PLAYABLE_AREAS`로 합치기 권장 | D-63 |
| 06 `330_climb` 워프 행 id | 설정 행 `330000001`, 워프 `330001001`~`004` | rowType 열 |

### 7.15.2 다른 문서에 넘기는 가정

- **08**: 4xx·7xx 표 열 정의는 08이 정본이고 이 문서는 목록·관계(§7.2, §7.9)만 가진다. 08의 consumeType(610·620·621·622·630), 16_levelPrice 그룹(301 이상), `01_wealth` 5~17, `02_playResource` 101~105, `05_contentOpen` 5001001~018, `08_adPlacement` 모드·상점·축복 행을 이 문서 표에 그대로 받는다. 600번대 type을 메타 보상에 쓰지 않는다(608·609·630 제외).
- **05**: `09_uiResource` 행과 `ui.*` 문구는 05가 늘린다. 화면은 `08_adPlacement.key`로 광고 자리를 찾는다.
- **02·06**: `300_runConfig` 그룹 001~008(95키)과 `04_effect` 행·`320_camera` 열은 02·06이 정본이다.
- **03**: `03_sound` 32행, 아틀라스 이름, `sprite_catalog.tsv` 형식은 03이 정본이다. 이 문서는 열 정의와 검증 규칙만 가진다.
- **09**: 저장은 표 id가 아니라 **key**를 저장한다(덱·재화 제외). 표에서 행이 빠졌을 때 처리는 §7.13.4.
