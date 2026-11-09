# 9. 저장
[입구](README.md) · 앞: [08 콘텐츠](08_Content.md) · 다음: [10 아키텍처](10_Architecture.md)

정본은 `localStorage` 키 하나(`catescape_moon_v1`, L843)에 `save` 객체 전체를 JSON으로 쓰고(L847 `persist`), 소리 끄기만 따로 `ce_mute`(L1624, L1694)에 쓴다. Unity는 Framework 저장 묶음 — `LocalSaveRepository`(PlayerPrefs 두 칸 교대 + 복구) · `DataRepository`(DataContainer 묶음) · `RepositoryStorageAdapter`(`LocalAuthority`) — 위에 MoonVillage 컨테이너를 올린다. 이 문서는 컨테이너 묶음, 정본 필드 대응, 저장 시점, 스키마·이관, 손상 처리를 정한다. 담당 모듈은 `MV.Save`(컨테이너·이관), 상태를 바꾸는 유일한 진입점은 `MV.Meta`([10](10_Architecture.md)).

---

## 9.1 원칙

| # | 원칙 | 이유 |
|---|---|---|
| 1 | **저장은 표 id가 아니라 key를 쓴다**(신 `thunder`, 강화 `rate`, 광고 `dia`). 예외: 덱(무기 등급 인덱스 배열)과 Framework 재화 | 표 행 순서·id를 바꿔도 저장이 깨지지 않는다([07 §7.13.4](07_Tables.md)) |
| 2 | 런 상태(`R.*`)는 저장하지 않는다. 런 중 종료하면 그 런은 없어진다(정본 동일, D-66) | 정본에 런 저장이 없다 |
| 3 | 한 사건의 변경은 **한 번의 원자 저장**(`ApplyAndSave`)으로 묶는다(정산·부활·구매·광고 보상) | 반만 저장된 상태(재화만 빠지고 강화가 안 오름 등)를 막는다 |
| 4 | 시각은 모두 **UTC Unix 밀리초**(long). 하루 경계 문자열은 `YYYY-MM-DD` | 정본 `Date.now()`·`toISOString().slice(0,10)`과 같다 |
| 5 | 파싱은 관대하게(`ParseState`): 모르는 필드 무시, 빠진 필드 기본값, 범위 밖 값 자르기 | 정본 `load()`의 `Object.assign(DEF(), s, …)`(L846)과 같은 태도 |
| 6 | 2차 시스템 컨테이너도 **지금 빈 상태로 등록**한다 | 나중에 컨테이너를 추가할 때 스키마 이관이 필요 없게 |

---

## 9.2 컨테이너 묶음

`DataRepository("p0")`에 아래 컨테이너를 등록한다(모두 `saveLocal = true`). dataKey가 저장 JSON의 최상위 키다. 클래스 이름은 `MV.Save` 네임스페이스.

| dataKey | 클래스 | 담는 것 | 부활 때 | 진행 초기화 때 |
|---|---|---|---|---|
| `wealthInfos` | Framework `WealthContainer` | 재화(`01_wealth` key별 quantity: `gold`·`dia`·`souls`·`rbToken` + 08의 5~17) | gold·souls 0, dia·rbToken 유지 | 전부 0 |
| `mvProgress` | `MvProgressContainer` | Level, Area, 체크포인트, 벽 기록, 런 수, 최고 Area, 정산 순번 | level·runs·bestArea 유지, 나머지 기본 | 기본 |
| `mvLobby` | `MvLobbyContainer` | 로비 강화 3종, 시작 덱 | 기본 | 기본 |
| `mvAreaUp` | `MvAreaUpContainer` | Area 강화 5종 | 기본 | 기본 |
| `mvRebirth` | `MvRebirthContainer` | 부활 Lv, 적립 대기, 특성 4종, 쿨타임 끝 시각 | lv+1, pending 0, cdUntil 갱신 | 기본 |
| `mvGod` | `MvGodContainer` | 출전 신, (08) 신 레벨·조각 | 유지 | 기본 |
| `mvUnlock` | `MvUnlockContainer` | 열린 기능(sticky), 해금 연출 본 기록, NEW 본 기록 | 유지 | 기본 |
| `mvDaily` | `MvDailyContainer` | 하루 경계, 광고 사용 횟수, 모드 하루 플레이 수 | 유지 | 기본 |
| `mvPlayRes` | `MvPlayResContainer` | 충전형 자원(`02_playResource`, 입장권·에너지) 양과 충전 기준 시각 | 유지 | 기본 |
| `mvMode` | `MvModeContainer` | 모드별 최고 단계·첫 클리어 기록 | 유지 | 기본 |
| `mvQuest` | `MvQuestContainer` | 퀘스트 기간 카운터·수령, 업적, 출석 | 유지 | 기본 |
| `mvResearch` | `MvResearchContainer` | 연구 단계, 진행 중 연구 끝 시각 | 유지 | 기본 |
| `mvShop` | `MvShopContainer` | 상품 구매 기록(횟수·마지막 시각) | 유지 | 기본(개발 빌드만) |
| `mvPet` · `mvFriend` · `mvGear` · `mvPass` · `mvBlessing` | 각 `Mv…Container` | 2차 시스템 예약. 1차는 빈 객체 `{}` | 유지 | 기본 |
| `mvSettings` | `MvSettingsContainer` | 소리 끄기, 컨텐츠 메뉴 마지막 탭 | 유지 | **유지**(정본 `ce_mute`는 진행 초기화와 무관) |
| `mvMeta` | `MvMetaContainer` | 만든 시각, 마지막 저장 시각, 가져오기 출처, 표 묶음 버전 | 유지 | createdAt 새로 |

「부활 때」는 정본 `doRebirth` 유지 목록 `keep = {level, dia, god, rb, ads, runs}`(L2560)을 따른다. 정본에 없던 08 시스템(모드·퀘스트 등)은 부활로 지우지 않는다 [창작].

언어는 저장하지 않는다. Framework `LocalizationRuntimeOptions.ReadLanguage/SaveLanguage`가 따로 가진다([10 §10.7](10_Architecture.md)).

---

## 9.3 컨테이너 필드와 정본 대응

### 9.3.1 mvProgress

| 필드 | 타입 | 기본 | 정본 | 뜻 |
|---|---|---|---|---|
| `level` | int | 1 | `save.level` | 판 번호(정산마다 +1, L2427). 템플릿 순환 입력([07 §7.6.3](07_Tables.md)) |
| `area` | int | 0 | `save.area` | 현재 Area 인덱스(0기반). 표시는 +1 |
| `cp` | int | 0 | `save.cp` | 체크포인트(0~`CP_MAX`) |
| `bars` | object | `{}` | `save.bars` | 벽 기록. 키 `"A{area}"`(L1757 `barKey`), 값 `int[3][]` = Floor별 칸 idx → 남은 HP(0 = 부서짐) |
| `runs` | int | 0 | `save.runs` | 정산한 런 수 |
| `zoneClear` | bool | false | `save.zoneClear` | 마지막 Area 관문 도달 + 생선 충족(zoneEnd) |
| `escaped` | bool | false | `save.escaped` | 정본 잔재(L2657 `#unlockBtn`). 읽기만 하고 쓰지 않는다 |
| `bestArea` | int | 0 | 없음 [창작] | 도달한 가장 큰 `area`. 부활 뒤 빠른 이동([08 §8.5.14](08_Content.md))에 쓴다 |
| `lastSettledRunSeq` | long | 0 | 없음 [창작] | 정산한 런 순번. 같은 런을 두 번 정산하지 않게 막는다(정산 버튼 연타·앱 재개) |

- `save.zone`은 저장하지 않는다. 정본도 로드 직후 `zoneOf(area)`로 다시 계산한다(L849). Unity는 `302_area.zoneId`로 구한다.
- `bars`는 정본처럼 **현재 Area 하나만** 가진다: 정산 때 `bars = {}` 뒤 현재 키만 다시 넣는다(L2430~2435). 개방(pass) 때는 비운다.
- `bars` 형식 검사: Floor 배열 길이 ≤ `FLOOR_COUNT`, idx 수 ≤ rows×3, 값 ≥ 0. 어긋나면 그 Area 기록을 버린다(경고 로그).

### 9.3.2 mvLobby · mvAreaUp · mvRebirth · mvGod

| 컨테이너.필드 | 타입 | 기본 | 정본 |
|---|---|---|---|
| `mvLobby.up` | `{stair, weapon, income}` int | 0 | `save.up` |
| `mvLobby.deck` | int[9] | `00_config.startDeck` = `0/1/0/0/0/0/0/0/0` | `save.deck`(인덱스 = 무기 등급, 0 미사용). 길이 ≠ 9면 기본(L846) |
| `mvAreaUp.lv` | `{rate, range, clone, crit, dbl}` int | 0 | `save.aup` |
| `mvRebirth.lv` | int | 0 | `save.rb.lv` |
| `mvRebirth.pending` | int | 0 | `save.rb.pending` |
| `mvRebirth.perks` | `{power, crit, obst, dcap}` int | 0 | `save.rb.perks` |
| `mvRebirth.cdUntil` | long(ms) | 0 | `save.rb.cdUntil` |
| `mvGod.sel` | string | `00_config.startGodKey` = `thunder` | `save.god` |
| `mvGod.gods` | `{key: {lv, shard}}` | `{}` | 없음, [08 §8.3](08_Content.md) |

- 목숨 조각 보유량(`save.rb.tokens`)은 재화 `wealthInfos.rbToken`이다(consumeType 1, `01_wealth` 4).
- 부활 해금(`save.rb.open`, DEF에 없고 L2431에서 생김)은 `mvUnlock.opened`에 `rebirth`로 넣는다([07 §7.8.9](07_Tables.md) `05_contentOpen[rebirth]` sticky).
- 강화 키(`up`·`lv`·`perks`)는 표 key(`10_lobbyUpgrade`·`13_areaUpgrade`·`14_rebirthPerk`)다. 표에 없는 키는 버리고, 표 max를 넘으면 max로 자른다.

### 9.3.3 mvUnlock — 해금·NEW 기록

| 필드 | 타입 | 뜻 | 쓰는 곳 |
|---|---|---|---|
| `opened` | string[] | sticky 해금이 성립한 `05_contentOpen.key`. 조건이 나중에 거짓이 돼도 열린 채 | 부활 버튼, 컨텐츠 메뉴([05](05_Screens.md)) |
| `seenUnlock` | string[] | 「NEW FEATURE」 해금 연출(`ui.unlock.tag`)을 이미 본 key | 연출 한 번만 |
| `seenNew` | string[] | 컨텐츠 메뉴에서 NEW 태그를 이미 본 key. 메뉴를 닫을 때 그때 보인 열린 항목을 모두 넣는다([05 §5.15](05_Screens.md)) | 로비 「NEW」 부제, 카드 태그 |

- NEW 표시 = `열림 && key ∉ seenNew`. 해금 연출 = `열림 && key ∉ seenUnlock`.
- 정본 이행: 가져온 저장에서 `rb.open == true`면 `opened`·`seenUnlock`에 `rebirth`를 넣는다(이미 본 것으로 본다).

### 9.3.4 mvDaily — 하루 단위 횟수

| 필드 | 타입 | 뜻 |
|---|---|---|
| `day` | string | 마지막으로 맞춘 날 `YYYY-MM-DD` |
| `ads` | `{placementKey: 사용 수}` | `08_adPlacement.key`별 오늘 사용 횟수. 남은 횟수 = `dailyLimit − ads[key]` |
| `modePlays` | `{contentKey: 수}` | 모드(`400_content.key`)별 오늘 입장 횟수([05](05_Screens.md) 「모드 일일 횟수」, 08 일일 제한이 있으면 그 값과 비교) |
| `free` | `{key: 수}` | 그 밖의 하루 무료 횟수(상점 무료 상품 등, 08이 key를 정함) |

**하루 경계**(04 D-29, 08 D-77 권장 A):
- `today = (DateTime.UtcNow + 00_config.adDayUtcOffsetHour 시간).ToString("yyyy-MM-dd")`. 값 0 = 정본(`toISOString`, 한국 09:00 초기화).
- 횟수를 읽거나 쓰기 전에 `MvDaily.Roll()`: `day ≠ today`면 `ads`·`modePlays`·`free`를 비우고 `day = today`. 정본 `adDay()`(L2423)는 `{day, dia:3, skip:4}`(남은 횟수)를 저장했지만 Unity는 **사용 수**를 저장한다 → 표에서 한도를 바꾸면 바로 반영된다.
- 정본 이행: `ads.dia`·`ads.skip`(남은 수)은 `used = max(0, dailyLimit − 남은 수)`로 바꾼다(그 날짜가 오늘일 때만, 아니면 버림).
- 기기 시계를 앞뒤로 돌리는 것은 막지 않는다(서버 없음). 서버 연결 시 서버 날짜로 바꾼다(04 D-29 C).

### 9.3.5 mvPlayRes · mvMode · mvQuest · mvResearch · mvShop (08 시스템)

필드 이름의 정본은 이 표이고, 규칙(충전 주기·기간·보상)은 [08](08_Content.md)이다.

| 컨테이너 | 형식 | 비고 |
|---|---|---|
| `mvPlayRes` | `{resourceKey: {n, at}}` | `02_playResource` key별 보유 `n`, 마지막 충전 기준 시각 `at`(ms). 읽을 때 `(now − at) / 주기`만큼 충전(최대치까지)하고 `at`을 그만큼 앞으로 |
| `mvMode` | `{contentKey: {best, first: int[]}}` | 최고 도달 단계 `best`, 첫 클리어 보상 받은 단계 목록 `first` |
| `mvQuest` | `{daily:{period, cnt:{}, got:[]}, weekly:{…}, achieve:{cnt:{}, got:[]}, attend:{period, days, lastDay}, pass:{}}` | `period` = 기간 시작 날짜 문자열(일간 `YYYY-MM-DD`, 주간 그 주 월요일). 기간이 바뀌면 그 묶음만 비움. 하루 기준은 9.3.4와 같다 |
| `mvResearch` | `{lv:{key: int}, run:{key, finishAt}}` | 진행 중 연구 1개(08 규칙), 끝 시각 ms. 앱을 다시 켜면 `now ≥ finishAt`이면 완료 처리 |
| `mvShop` | `{buys:{productKey:{n, at}}, guarantee:{guaranteeId: int}}` | 구매 횟수·마지막 시각. 결제는 스텁이지만 기록 형식은 지금 정한다. `guarantee` = 천장 셈 값(키 `804_randomGuarantee` id, [07 §7.9.3](07_Tables.md)). 1차는 행이 없어 비어 있다 |

### 9.3.6 mvSettings · mvMeta

| 필드 | 타입 | 기본 | 뜻 |
|---|---|---|---|
| `mvSettings.muted` | bool | false | 정본 `ce_mute`(L1624). 소리 버튼(`#sndBtn`, [05](05_Screens.md))이 반전. `AudioManager` 마스터 음소거로 적용 |
| `mvSettings.contentTab` | string | `""` | 컨텐츠 메뉴 마지막 탭([05](05_Screens.md)) |
| `mvMeta.createdAt` / `lastSaveAt` | long(ms) | | 새 저장 / 마지막 저장 시각 |
| `mvMeta.importSource` | string | `""` | `""` / `proto-paste`(D-67) |
| `mvMeta.tableVersion` | int | | 마지막으로 저장할 때의 `00_config.dataVersion`. 표 버전이 올라가면 §9.6 참조 정리를 다시 돈다 |

`muted`는 진행 초기화(D-69)에서 지우지 않는다. 소리 버튼은 다른 저장과 묶지 않고 단독 `ApplyAndSave(…, "mvSettings")`.

---

## 9.4 저장 시점

정본 `persist()` 호출 위치와 Unity 저장 묶음. 「키」 = `ApplyAndSave`에 넘기는 dataKey 목록(그 사건에 바뀌는 것만).

| 사건 | 정본 | Unity 진입점(MV.Meta) | 키 |
|---|---|---|---|
| 결과 정산(×1·×3) | L2425~2437 `collect` | `Settle(runResult, mul)` | `wealthInfos`, `mvProgress`, `mvRebirth`(pending), 개방 시 + `mvLobby`·`mvAreaUp`·`mvUnlock` |
| 신 선택 | L2513 | `SelectGod(key)` | `mvGod` |
| Area 강화 구매 | L2530 | `BuyAreaUp(key)` | `wealthInfos`, `mvAreaUp` |
| 부활 특성 구매 | L2553 | `BuyPerk(key)` | `wealthInfos`, `mvRebirth` |
| 부활 | L2558~2562 `doRebirth` | `Rebirth()` | 부활 열의 모든 키(§9.2) |
| 광고 캣닢 +100 | L2574 | `GrantAd("dia")` | `wealthInfos`, `mvDaily` |
| 광고 쿨타임 −70분 | L2575 | `GrantAd("skip")` | `mvRebirth`, `mvDaily` |
| 로비 강화 구매 | L2644 | `BuyLobbyUp(key)` | `wealthInfos`, `mvLobby` |
| 테스트 패널(츄르·생선·Level·부활 해금·쿨 해제·캣닢·조각) | L3863~3869 | `DevGrant…`(개발 빌드만) | 해당 키 |
| 진행 초기화 | L3873 | `DevResetProgress()`(D-69) | 초기화 열의 모든 키 |
| 소리 끄기 | L1694 | `ToggleMute()` | `mvSettings` |
| 08 시스템(모드 입장·정산, 퀘스트 수령, 출석, 연구 시작·완료, 상점 구매, 입장권 광고) | 없음 | 각 `Mv…Service` | 해당 컨테이너 + 재화 |
| 해금 연출 봄 / 컨텐츠 메뉴 닫음 | 없음 | `MarkSeen…` | `mvUnlock`(+ `mvSettings.contentTab`) |
| 앱 일시정지·종료 | 없음 | `OnApplicationPause(true)` | `IsDirty`일 때만 `Save()`(평소엔 사건마다 저장했으므로 할 일 없음) |

- 정산 `Settle`은 `lastSettledRunSeq`를 같은 묶음에 올린다. 결과 화면 버튼은 누르는 순간 잠긴다([05](05_Screens.md) `UIButtonLock`).
- 재화 변경은 `ResourceService`(Framework)로 계산하되 저장은 같은 `ApplyAndSave` 한 번에 넣는다(재화만 먼저 저장하지 않는다). 지급 묶음 준비는 `LocalRewardService.PrepareMutation` → `Commit` 흐름을 쓴다.
- 런 중에는 저장하지 않는다. 런 안의 보상은 정산 때 한 번에 들어간다([07 §7.8.12](07_Tables.md) 600번대).

---

## 9.5 스키마·버전·이관

### 9.5.1 이름

| 항목 | 값 |
|---|---|
| 프로필 id | `p0`(단일 프로필) |
| PlayerPrefs 네임스페이스 | `moonvillage.progress.v1` (`LocalSaveRepository(profileId, codec, ns)`) |
| 컨테이너 스키마 id | `mv:local:v1` — 저장 JSON의 `_containerSchema`(`RepositoryStorageAdapter.SchemaKey`) |
| 봉투 버전 | Framework `LocalSaveCodec.CurrentVersion`(현재 1). 봉투 형식만 다루며 게임이 건드리지 않는다 |

### 9.5.2 열기 순서(부팅 6단계, [10 §10.7](10_Architecture.md))

```text
1. repo = new LocalSaveRepository("p0", codec, "moonvillage.progress.v1")
2. loaded = repo.Load()
3. if loaded.Status ∈ {Loaded, RecoveredBackup}:
       payload = loaded.Document.Snapshot.Payload
       schema = payload["_containerSchema"]
       if schema != "mv:local:v1":
           payload' = MvSaveMigrator.Run(payload, from: schema)   // IMvSaveMigration 체인
           repo.Save(new LocalSaveSnapshot(payload'), loaded.Document)
4. data = new DataRepository("p0"); 컨테이너 전부 Register
5. adapter = RepositoryStorageAdapter.Open(data, repo, "mv:local:v1",
                RepositoryStorageMode.LocalAuthority, MvSaveDefaults.Create)
6. MvSaveReferenceFix.Run(data, MvTables.Current)   // §9.6, 바뀐 것이 있으면 ApplyAndSave
```

**이관은 Open 앞이다.** `RepositoryStorageAdapter`는 스키마 id가 다르면 예외를 던지고(「explicit migration is required」) 스스로 이관하지 않는다. [10 §10.7](10_Architecture.md) 6단계의 「Open → 이관」 순서는 이 문서 순서(이관 → Open)로 읽는다.

### 9.5.3 이관 체인

```csharp
namespace MV.Save
{
    public interface IMvSaveMigration
    {
        string FromSchema { get; }   // 예 "mv:local:v1"
        string ToSchema { get; }     // 예 "mv:local:v2"
        JObject Apply(JObject payload);   // 새 객체를 돌려준다. 실패는 예외
    }
}
```

- `MvSaveMigrator`가 from → … → `mv:local:vN`까지 순서대로 적용한다. 빠진 고리가 있으면 실패(§9.7 「이관 실패」).
- 규칙: **필드 추가만 하는 변경은 스키마를 올리지 않는다**(ParseState 기본값으로 충분). 필드 이름 변경·형식 변경·컨테이너 합치기/나누기만 새 스키마 + 이관 1개.
- 컨테이너 추가는 원칙 6(미리 등록)으로 피한다. 그래도 추가하면 이관에서 빈 객체를 넣는다.
- 봉투 버전 이관(`ILocalSaveMigration`)은 Framework 몫이고 게임은 쓰지 않는다.
- 시험: 이관마다 「이전 스키마 고정 JSON → 이관 → ParseState 성공 + 기대 JSON과 같음」 EditMode 시험 1개.

### 9.5.4 새 저장

`MvSaveDefaults.Create()`가 모든 컨테이너 기본값 JSON을 만든다(DEF, L844 대응). 시작 덱·출전 신은 `00_config`에서 읽는다. `mvMeta.createdAt = now`.

---

## 9.6 표가 바뀐 뒤 저장 정리

[07 §7.13.4](07_Tables.md) 규칙을 `ParseState`와 부팅 6단계 `MvSaveReferenceFix`에서 적용한다.

| 저장 값 | 표에 없거나 범위 밖이면 |
|---|---|
| `mvGod.sel` | `00_config.startGodKey` |
| `mvLobby.deck` 길이 | 103 최대 tier + 1로 맞춤. 넘치는 등급은 최대 등급 칸에 더함 |
| `mvProgress.area` | `302_area` 마지막 행으로, `cp = 0`, `bars = {}` |
| 강화 단계 | 표 max로 자름 |
| `mvDaily.ads`·`modePlays`·`mvUnlock`의 key | 무시하고 다음 저장 때 지움 |
| `bars` 형식 어긋남 | 그 Area 기록 버림 |

정리로 값이 바뀌면 한 번 저장하고 경고 로그 1줄(바뀐 필드 목록).

---

## 9.7 손상·실패 처리

`LocalSaveRepository.Load()` 결과(`LocalSaveLoadStatus`)별 처리.

| 상태 | 뜻 | 처리 |
|---|---|---|
| `Missing` | 첫 실행 | `Open`이 `MvSaveDefaults.Create`로 새 저장 |
| `Loaded` | 정상 | 계속 |
| `RecoveredBackup` | 최신 칸이 깨져 이전 칸을 읽음 | 계속 + 토스트 `toastMessage.saveRecovered` 「이전 저장을 불러왔습니다」 1회 |
| `Corrupt` · `ProtectionFailed` | 두 칸 다 읽을 수 없음 / 무결성·복호화 실패 | **자동으로 새로 만들지 않는다.** 복구 팝업: 「다시 시도」(`LoadRecovery()`) / 「새로 시작」(두 번 확인 후 새 저장, 이전 원문은 개발 빌드면 파일로 내보냄) |
| `UnsupportedVersion` | 더 새 앱이 쓴 저장 | 팝업 「앱을 업데이트해 주세요」, 저장 건드리지 않음 |
| `MigrationFailed` | 봉투 이관 실패 | `Corrupt`와 같은 팝업 |
| `StorageUnavailable` | PlayerPrefs 접근 불가 | 팝업 「저장소를 사용할 수 없습니다」 + 다시 시도 |
| 게임 이관 실패(§9.5.3) | 스키마 체인 빈틈·예외 | `Corrupt`와 같은 팝업. 원문은 그대로 둔다 |

**쓰기 실패**: `ApplyAndSave`가 `LocalSaveWriteException`(쓰기 확인 실패)을 던지면 같은 변경을 다시 시도하지 않는다. `DataRepository`·`LocalSaveRepository`를 버리고 §9.5.2 순서로 다시 연 뒤(진행 중이던 사건은 저장된 값 기준으로 다시 판단), 토스트 `toastMessage.saveFailed`. `RepositoryRecoveryRequiredException`도 같은 처리.

**동시성**: 저장은 메인 스레드에서만. 같은 프레임에 두 사건이 오면 순서대로 각각 저장한다.

---

## 9.8 암호화·변조

- Framework `LocalSaveEncryption(keyId, byte[32])`(AES-256-CBC + HMAC)을 쓸 수 있으나 **기본 키가 없다** — 키를 어디서 줄지 결정 필요(D-68).
- 암호화를 켜도 로컬 단일 플레이어 게임이라 변조를 완전히 막지는 못한다. 목표는 「평문 편집 방지」 수준.
- 암호화 키를 바꾸면 이전 저장을 못 읽는다(`ProtectionFailed`). 키 교체는 `keyId`를 바꾸고 이전 키를 읽기용으로 남기는 방식만 허용.

---

## 9.9 프로토 저장 가져오기(개발 전용)

웹 프로토에서 진행한 저장으로 같은 상태를 Unity에서 재현해 비교하려는 용도(D-67).

1. 개발 빌드 테스트 패널 「프로토 저장 붙여넣기」: `localStorage.catescape_moon_v1` JSON 문자열을 붙여넣는다.
2. 정본 `load()`(L846)와 같은 기본값 합치기 → 아래 대응으로 컨테이너 JSON 생성 → 확인 팝업(Area·Level·재화 요약) → 한 번의 `ApplyAndSave(replace: true)`.
3. `mvMeta.importSource = "proto-paste"`.

| 정본 | Unity |
|---|---|
| `level, area, cp, bars, runs, zoneClear, escaped` | `mvProgress` 같은 이름. `bestArea = area` |
| `gold, souls, dia, rb.tokens` | `wealthInfos` `gold`·`souls`·`dia`·`rbToken`(gold는 floor) |
| `up`, `deck` | `mvLobby` |
| `aup` | `mvAreaUp.lv` |
| `rb.lv, pending, perks, cdUntil` | `mvRebirth` |
| `rb.open` | `mvUnlock.opened`·`seenUnlock`에 `rebirth` |
| `god` | `mvGod.sel` |
| `ads.day, dia, skip` | `mvDaily`(§9.3.4 이행 규칙) |
| `zone` | 버림(다시 계산) |
| `ce_mute` | 붙여넣기 대상 아님(소리 버튼으로 맞춤) |

---

## 9.10 시험 기준(MV-SAVE)

| # | 시험 | 종류 |
|---|---|---|
| 1 | 새 저장 → 모든 컨테이너 기본값 = DEF 대응 | EditMode |
| 2 | 정산 1회 → 재화·진행·pending이 한 번에 저장(중간 예외 주입 시 아무것도 안 바뀜) | EditMode |
| 3 | 같은 런 순번 정산 두 번 → 두 번째 무시 | EditMode |
| 4 | 부활 → 유지 목록 정확(level·dia·god·rb·daily·runs + 08 시스템) | EditMode |
| 5 | 하루 경계: day 다름 → 사용 수 0, offset 0/9 각각 | EditMode |
| 6 | 표 정리: 없는 신 key·덱 길이·Area 초과 | EditMode |
| 7 | 손상: 최신 칸 깨짐 → RecoveredBackup, 둘 다 깨짐 → 팝업 경로 | EditMode(가짜 PlayerPrefs) |
| 8 | 이관 체인 고정 JSON(이관이 생길 때마다) | EditMode |
| 9 | 프로토 붙여넣기 → 정본 상태 표본 3개와 필드 같음 | EditMode |

실행 기록은 작업 계약 [11](11_WorkContracts.md) MV-SAVE에 남긴다. 실행하지 않은 시험은 NOT_RUN.

---

## 9.11 의문점·결정 필요

| ID | 질문 | 선택지 | 권장 | 영향 |
|---|---|---|---|---|
| **D-66** | 런 중 앱 종료 | A 정본처럼 그 런 버림(진행·재화 변화 없음) / B 런 상태 저장 후 이어하기 / C 종료 시점까지 모은 것 자동 정산(×1) | **A**. 정본과 같고 구현이 단순. 런은 1~3분이라 손실이 작다. 광고 직전 종료 악용도 없다 | 저장 범위, 런 코드 직렬화 불필요 |
| **D-67** | 웹 프로토 저장 가져오기 | A 개발 빌드 붙여넣기 도구 / B 출시 기능(계정 이전) / C 하지 않음 | **A**. 정본 대조 검수용(같은 상태에서 화면·값 비교). 출시 사용자에게 프로토 저장은 없다 | 테스트 패널 1개, §9.9 |
| **D-68** | 저장 암호화 키 | A 끔(무결성은 봉투 체크만) / B 빌드에 키 내장(난독화 수준), 출시 빌드만 켬 / C 서버에서 키 받기 | **B**. 서버가 없으므로 C 불가. 개발 빌드는 꺼서 저장을 열어 볼 수 있게. 키 보관 위치(저장소 밖 설정 파일)는 사용자 결정 | `LocalSaveEncryption` 연결, 키 관리, 키 교체 규칙 |
| **D-69** | 진행 초기화 | A 개발 빌드 테스트 패널만(정본 두 번 누름 2.5초, `00_config.progressResetArmSec`) / B 출시 설정 화면에도(두 번 확인) / C 없음 | **A**. 정본에서도 테스트 패널 기능이다. 출시 요구가 생기면 B로 확장 | 테스트 패널, 초기화 열(§9.2) |

### 9.11.1 다른 문서에 넘기는 가정

- **[10](10_Architecture.md)**: 부팅 6단계는 「저장소 → 이관 → 컨테이너 등록 → Open → 참조 정리」 순서다(§9.5.2).
- **[05](05_Screens.md)**: 「하루」 기준은 UTC + `adDayUtcOffsetHour`(기본 0, 정본 동일)다. 04 D-29·08 D-77 권장 A를 따른다([05 §5.12](05_Screens.md)도 같은 기준). NEW·해금 연출 기록은 `mvUnlock.seenNew`·`seenUnlock`, 마지막 탭은 `mvSettings.contentTab`, 소리는 `mvSettings.muted`.
- **[08](08_Content.md)**: 08 시스템 저장 형식은 §9.3.5. 퀘스트 주간 기간 시작은 월요일(UTC + offset) [창작]. 다르게 정하면 이 표만 고친다.
- **[07](07_Tables.md)**: 저장은 key 기반이므로 key 열은 출시 뒤 바꾸지 않는다(바꾸면 이관 필요).
