# 인게임 HUD 재구성 — 모듈 계약 설계안 (승인 전)

작성: Claude, 2026-11-19. 기준은 dev `e6e048f0`입니다.

- 코드·프리팹·표는 아직 바꾸지 않았습니다. 이 설계안이 승인되면 작업 단위(4절)로 나눠 진행합니다.
- 조사 근거는 세 가지입니다.
  - dev의 HUD 프리팹·스크립트 구조
  - 기획서(MV-SPEC-01·02)와 표 300·330
  - 정크버스터즈(`jb_steam`) UI 계층 패턴(경로·패턴 이름만 참고)

---

## 1. 지금 구조의 문제 (확인된 사실)

| 문제 | 근거 |
|---|---|
| 한 클래스가 다 한다: `IngameHudView`(605줄)가 상단 줄·배속·손패 머지·계단 카드·룰렛·카드 비행·Card Pos를 모두 처리 | `MV.UI/Hud/Ingame/IngameHudView.cs` 기능 블록 241~603행 |
| 부품이 UIBase가 아니다: `HudWeaponSlot`·`HudSkillCard`·`HudClimbCard`는 MonoBehaviour라 로비·팝업에서 재사용할 수명 구조가 없다 | 각 파일 |
| 디자이너 예시 인스턴스를 복제 원본으로 쓴다: IngameScreen 안의 예시 10개(Slot_Weapon_Stack 3, Slot_Weapon 1, Card_Class 2, Card_Skill 2, Slot_Currency 2)에서 템플릿을 모은다. 「시작부터 카드가 여러 개」의 원인 후보다(추정, 화면 확인 안 함) | `IngameHudView.Init` 123~169행 |
| 풀이 직접 만든 것이고 일부는 생성·파괴: 스택 칸·계단 카드는 SetActive 재사용, 날아가는 카드·Holder는 Instantiate·Destroy | 372·451·568·580행, 180~184행 |
| 기획 상수를 쓰지 않는다: 손패는 §4.10과 `ui.hand.*` 11개 대신 하드코딩 시간(0.3/0.15/0.2/0.06/1.15)과 비율(.5/.6/.4)을 쓴다 | 65~69행, 332·350행 |
| 매 프레임 폴링과 비교: 런 이벤트(`WeaponDeliver`, `ClimbTierUp`, `Roulette*`, `CardApplied`)가 있는데 HUD는 쓰지 않고, `W` 배열·참조·해시를 비교한다 | 252~315·390·501·591행, `RunEvent.cs` 9~48행 |
| 빈 함수가 있다: `SetFloor`·`SetSouls`·`SetMergeHint`·`SetClimbCount`·`SetGauge`가 비어 있어, 계단 수·게이지가 표시되지 않을 수 있다(추정) | 191~211행 |

---

## 2. 설계 원칙

1. **화면은 조합만 한다.** 디자이너 프리팹 하나에 그 프리팹을 맡는 컴포넌트 하나를 둔다. 화면은 섹션에 데이터를 내려 주고, 단계(달리기·계단·룰렛)에 따라 섹션을 켜고 끄기만 한다.
2. **같은 부품은 같은 컴포넌트를 쓴다.** `Slot_Weapon`·`Card_Skill`·`Card_Class`·`Slot_Currency`는 `UIBase`를 상속하고 `ShowUI(데이터)`로 그린다. 인게임·로비·팝업에서 그대로 쓴다. 로비 분석 세션과 같은 컴포넌트를 공유한다.
3. **데이터는 스냅샷과 이벤트 두 갈래다.**
   - 계속 보이는 값(레벨·재화·계단 수·게이지·장착 등급)은 스냅샷에서 읽는다.
   - 변화 연출(지급·머지·등급 상승·룰렛·카드 적용)은 런 이벤트로 받는다.
   - 섹션은 정크버스터즈처럼 `SetSubscribe`에서 직접 구독하고, 화면이 신호를 중계하지 않는다.
4. **풀링은 Unity `ObjectPool<T>`로 한다.**
   - 섹션이 풀과 대여·반환 시점을 소유하고, 슬롯이 `actionOnRelease`에서 자기 데이터·연출을 정리한다.
   - 복제 원본은 디자이너 프리팹 **에셋**을 직렬화 참조로 받는다. 화면 안 예시 인스턴스를 원본으로 쓰지 않는다.
5. **값의 위치는 성격으로 나눈다(7절 결정으로 갱신).**
   - 연출 값(이동·모임·비행 시간, 곡선, 배율)은 섹션·부품 프리팹의 직렬화 필드에 둔다. 기획서 값이 있으면 그것을 기본값으로 쓴다.
   - 게임 결과에 영향을 주는 값(`MERGE_N`, `maxCards`, `330_climb.rl*`처럼 시뮬이 쓰는 것)은 표에 둔다.
   - 연출이 시뮬 시간과 맞아야 하면 UI가 시뮬 상태를 읽는다(룰렛 카드 비행은 반대로 HUD 프리팹 `flySec`이 정본이고 시뮬이 화면 도착을 받는다, MV-UI3-CLIMBFX3). 같은 값을 두 곳에 두지 않는다.
6. **연출은 부품·섹션 컴포넌트가 맡는다.** 화면은 연출을 하지 않는다.

---

## 3. 모듈 계약

공통 사항:
- 「편집 원본」은 사람이 고치는 곳이다.
- 「통합 완료 조건」의 화면 확인은 사용자 확인이 필요하다(NOT_RUN 예정).
- 클래스 이름은 제안이다. 기존 파일·클래스(`HudWeaponSlot` 등)는 이름을 그대로 두고 기반 클래스만 바꾼다.

### 3.1 화면과 연결

| 모듈 | 책임 | 상태·수명 | 편집 원본 | 연결 계약 | 기획 근거 | 공급 → 소비 | 통합 완료 조건 |
|---|---|---|---|---|---|---|---|
| `RunHud` (기존, `UIHudBase`) | HUD 진입점. 런을 묶고 매 프레임 피드를 돌린다 | 런 바인딩 | `UIHud_Run.prefab` | `Bind(run)` → 피드 시작. 화면 컴포넌트를 직렬화 참조로 갖는다 | — | MvUiNav → RunHud | 런 진입·재시작 때 HUD가 새 런 값으로 초기화된다 |
| `RunHudFeed` (기존 `RunHudPresenter`·`RunStateHudSource`를 정리) | 스냅샷 생성 + `RunState.Events`를 C# 이벤트로 전달 | 이벤트 커서. 런마다 초기화 | 없음(코드) | `HudSnapshot Current`, `event Action<RunEvent> OnEvent` | 04 §4.13 | RunState → 섹션들 | 한 이벤트가 정확히 한 번 전달된다(EditMode 시험) |
| `IngameScreenView` (`IngameHudView` 교체) | 섹션 참조를 갖고 단계별로 섹션을 켜고 끈다. 노션 시퀀스 1·6단계(진입 때 Top·Mid 끄기, 끝나면 켜기) | 현재 단계 | `IngameScreen.prefab` 루트 | 섹션마다 `ShowUI(...)`·`HideUI()`만 호출 | 노션 [HUD] 인게임 시퀀스 | 피드 → 섹션 | 달리기 → 계단 → 룰렛 → 달리기 전환에서 해당 섹션만 보인다 |

### 3.2 공용 부품 (`UIBase`, 화면 간 재사용)

| 모듈 | 책임 | 상태·수명 | 편집 원본 | 연결 계약 | 기획 근거 | 공급 → 소비 | 통합 완료 조건 |
|---|---|---|---|---|---|---|---|
| `HudWeaponSlot` (`Slot_Weapon`, `_Stack` 변형) | 등급 틀·아이콘·공격력 표시. 자기 등장·모임·번쩍·튐 연출 | 표시 단계. 반환 때 연출·데이터 정리 | `Slot_Weapon.prefab` | `ShowUI(int tier)`, `PlayIn/Gather/Flash/Pop(dur)` | 103_item, 09_uiResource, SRC §6-4 UI | 무기 섹션 → 슬롯 | 단계 1~12 모두 표에 맞는 틀·아이콘으로 표시된다 |
| `HudClimbCard` (`Card_Class`) | 등급 카드 표시(등급·층·진행·제목·레벨) | 표시 데이터 | `Card_Class.prefab` | `ShowUI(ClimbCardData)` | 115_cardTier, 노션 | 계단 섹션 → 카드 | 등급 5단계 표시가 표와 같다 |
| `HudSkillCard` (`Card_Skill`) | 스킬 카드 표시. 「?」·롤링 띠·확정 카드 | 롤 단계 | `Card_Skill.prefab` | `ShowUI(SkillCardData)`, `BeginRoll`, `Advance`, `Reveal` | §4.8.4, 노션 | 룰렛 섹션·Card Pos → 카드 | 같은 카드가 룰렛과 Card Pos에서 똑같이 보인다 |
| `Slot_Currency` 컴포넌트 | 재화 아이콘·수 | 없음 | `Slot_Currency.prefab` | `ShowUI(CurrencyData)` | SRC §12-2 | 상단 줄·로비 → 칸 | 로비와 인게임에 같은 컴포넌트가 붙는다 |

### 3.3 섹션 (`UIBase`, 화면 안 영역)

| 모듈 | 책임 | 상태·수명 | 편집 원본 | 연결 계약 | 기획 근거 | 공급 → 소비 | 통합 완료 조건 |
|---|---|---|---|---|---|---|---|
| `TopBarSection` (`Top`) | 레벨·배속 1x/2x·재화 | 배속 상태(`MvRunSpeed`) | `IngameScreen/Top` | 스냅샷 `level`, 재화. 토글 → `MvRunSpeed.Set` | SRC §12-2, 05 §5.2.2, 지시문 1x/2x | 스냅샷 → 섹션 | 1x↔2x 전환이 시뮬 배속에 반영되고, UI·손패는 실시간 그대로다 |
| `WeaponSection` (`Mid/Weapon`) | 장착 칸 + 스택 목록. 손패 큐 실행 | `ObjectPool<HudWeaponSlot>`(스택 칸), 손패 모델 | `Mid/Weapon`, 복제 원본은 `Slot_Weapon_Stack.prefab` 에셋 | 이벤트 `WeaponDeliver{A}`·`WeaponRejected` → 손패 모델 → 슬롯 연출 | 04 §4.10(handReset·Deliver·Sort·Work·Unit·stepHand), 05 §5.3, 300 `ui.hand.*`·`MERGE_N`, SRC §6-3·6-4 | 이벤트 → 모델 → 섹션 → 슬롯 | 시작 때 칸 1개(시작 무기). 3장 지급 → 모임 → 윗단계 1장. 연쇄 머지. 개수 제한 없음(`WEAPON_STACK_MAX`=0) |
| `WeaponHandModel` (순수 C#, 기존 `HudWeaponHand` 교체) | §4.10 손패 상태 기계: 배달 큐, 정렬, 3→1 계산, 단위 시간 u | 큐와 손패 사본. 런마다 리셋 | `WeaponSection` 프리팹 직렬화 필드(기획서 §4.10 값이 기본값, 표 `ui.hand.*`는 7.1에 따라 제거) | `Enqueue(deliver)`, `Step(realDt)` → 단계 목록 | 04 §4.10 공식 그대로 | 이벤트 → 모델 → 섹션 | §4.10 예시 입력에 대해 단계·시간이 기획 공식과 같다(EditMode) |
| `StairSection` (`Mid/Stair`) | 계단 수·계단 레벨 게이지 | 없음 | `Mid/Stair` | 스냅샷 `Stairs`·`GaugeLv`·`GaugeFill` | SRC §12-2, 05 §5.2.5 | 스냅샷 → 섹션 | 계단 수와 게이지가 런 값과 같다(지금 빈 함수) |
| `GradeStepSection` (`ClimbStair/Grade Step Section`) | Out/Prev/Current/Next/In 다섯 자리의 카드 이동 | `ObjectPool<HudClimbCard>`(보이는 5장 + 이동 중) | 다섯 Holder 위치는 프리팹, 원본은 `Card_Class.prefab` 에셋 | 이벤트 `ClimbTierUp`·계단 진행 → 한 칸씩 이동 | 노션 시퀀스 2·3, SRC §6-5·8-1, 330 `lap`·`riseBase`·`risePerStep` | 이벤트 → 섹션 → 카드 | Current 등급이 오를 때 카드가 한 칸씩 이동하고, 끝나면 등급이 확정된다 |
| `SkillRollSection` (`ClimbStair/Skill Section`) | 확정된 등급의 스킬 카드 롤링 → 확정 → 제목 표시 | 롤 상태(결과는 시뮬이 이미 확정) | `Skill Section`, 카드는 `Card_Skill` | 이벤트 `Roulette*`, 시간은 330 `rl*` | §4.8.4, 05 §5.2.9, 노션 4·5 | 이벤트 → 섹션 → 카드 | 시뮬이 확정한 카드와 같은 카드에서 멈춘다 |
| `CardPosSection` (`Mid/Card Pos`) | 확정 카드가 날아와 Holder에 쌓임 | Holder 수는 330 `maxCards`(=3). 카드는 `ObjectPool<HudSkillCard>` | `Card Pos`·Holder 위치 | 이벤트 `CardApplied`. 비행 시간(`flySec`)·곡선은 프리팹 직렬화 필드, 도착하면 시뮬이 적용(MV-UI3-CLIMBFX3) | §4.8.4, 노션 6, 300 `flyCard.*` | 이벤트 → 섹션 → 카드 | 최대 3장이 순서대로 쌓이고, 재시작하면 비워진다 |
| `ClimbNoticeView` (기존, dev `18bcf772`) | 계단 정산 글자·토스트 | 기존 | `HUD_ClimbNotice.prefab` | 기존 | — | 기존 | 변경 없음 |

---

## 4. 작업 단위 (승인 뒤 순서대로, 계약 1건 = 브랜치 1개)

| 순서 | 작업 | 내용 | 검증 |
|---|---|---|---|
| 1 | 피드 | `RunHudFeed`(스냅샷 + 이벤트). 섹션이 쓸 연결 계약을 먼저 고정 | compile, EditMode(이벤트 1회 전달) |
| 2 | 공용 부품 | 슬롯·카드·재화 컴포넌트를 `UIBase`로 바꾸고 `ShowUI(데이터)` 적용 | compile, EditMode(프리팹 부착·표 매핑) |
| 3 | 손패 모델 | `WeaponHandModel`(§4.10, `ui.hand.*`) | EditMode(공식 대조) |
| 4 | 무기 섹션 | 풀 + 모델 연결. 시작 때 칸 1개 | EditMode, PlayMode(대표 경로) |
| 5 | 상단 줄·계단 섹션 | 배속, 계단 수·게이지 | EditMode |
| 6 | 계단 카드 섹션 | Out~In 이동 | EditMode, PlayMode |
| 7 | 룰렛·Card Pos | 롤링·비행·쌓임 | EditMode, PlayMode |
| 8 | 화면 조합 | `IngameScreenView`로 교체, `IngameHudView` 삭제 | 전체 HUD 시험 + 사용자 화면 확인 |

- 대표 경로를 먼저 맞춘다: 무기 1장 지급부터 화면 반영까지 4번에서 먼저 연결한 뒤 나머지 섹션으로 넓힌다.
- 작업자는 자기 모듈과 3절의 연결 계약만 구현한다. 계약을 바꿔야 하면 이 설계안을 고친 뒤 진행한다.

---

## 5. 결정이 필요한 것

결정 카드로 묻습니다.

1. 디자이너 예시 인스턴스(IngameScreen 안 10개)를 어떻게 다룰지
2. 룰렛·계단 표시 기준: 기획서(여러 장 중앙 배치, 중앙 카드 뒤집기)인지, 노션(Card_Skill 1장 롤링, Out~In 이동)인지
3. 시간 값이 표에 없는 연출(계단 카드 한 칸 이동, Card Pos 비행)
4. 무기 머지 연출: 노션에 「연출 구성 미정」이라고 되어 있다

카드로 묻지 않고 남겨 둔 것:
- 75 단위로 카드가 여러 장(최대 3) 나올 때 Grade Step과 Card_Skill이 어떻게 반복되는가: 노션에 없다. 1장 경로를 먼저 만들고 나중에 질문한다.
- 플레이어 배속을 저장할지·기본값: 문서에 없다. 저장하지 않고 1x로 시작하는 안으로 두고 확인한다.
- 계단 진입 때 Top·Mid를 끄는 것: 노션에 「변경될 수 있음」이라고 되어 있다. 노션대로 끄되, 섹션 단위로 켜고 끌 수 있게 만든다.
- 무기 단계: S01은 8, SRC·103_item·코드는 12다. 나중 자료(SRC·표)를 따라 12로 한다.

---

## 6. 기획 상수 목록 (이번 HUD가 소비하는 것)

| 대상 | 키 | 현재 값 |
|---|---|---|
| 손패 | 300 `ui.hand.budget / leftMin / dealCost / unitMin / unitMax / dealGapMax / dealGapMul / mergeMul / moveMul / afterMax / afterMul` | 3 / 0.4 / 0.12 / 0.07 / 0.54 / 0.05 / 0.12 / 0.92 / 0.9 / 0.04 / 0.08 |
| 머지·스택 | 300 `MERGE_N`, `WEAPON_STACK_MAX` | 3, 0(제한 없음) |
| 룰렛 | 330 `rlEndBase / rlEndStep / rlFlipSec / rlSlowWin / rlSlowK / rlFlyDelay / rlEndPad / maxCards` | 1.0 / 0.45 / 0.06 / 0.4 / 0.35 / 1.5 / 0.3 / 3 (비행 시간은 Card Pos 프리팹 `flySec` 0.62, MV-UI3-CLIMBFX3) |
| 카드 비행 | 300 `flyCard.time / removeMs / bezier` | 0.6 / 620 / .55,0,.75,.4 |
| 계단 | 330 `lap / lapTier / riseBase / risePerStep / riseCap / popSec / stepPop` | 75 / 4 / 0.5 / 0.1 / 1.5 / 0.2 / 1.1 |
| 등급 문턱 | 115_cardTier `need` | 5/10/20/40/75 |
| 지급 지연 | 108_receiveCondition `cgatePass.delayMs` | 500(게임 시간) |
| 표에 없고 기획서에만 있는 값(직렬화 기본값으로 둘 것) | 05 §5.3 위치 이동 .28s, `wdeal` .25s, `wmerged` max(0.15, u) / 05 §5.2.9 확정 `chosen` .8s | — |

---

## 7. 사용자 결정 반영 (2026-11-19)

| 질문 | 결정 | 설계에 반영한 것 |
|---|---|---|
| 디자이너 예시 인스턴스 10개 | 프리팹은 그대로 두고, 실행할 때 숨긴다 | 섹션이 `Init`에서 예시를 끈다. 풀 복제 원본은 프리팹 **에셋**(`Slot_Weapon_Stack.prefab`, `Card_Class.prefab`, `Card_Skill.prefab`)을 직렬화 참조로 받는다 |
| 룰렛·계단 표시 기준 | 노션 표시 + 기획서 시간 | 모양·순서는 노션(Card_Skill 1장 롤링, Out→Prev→Current→Next→In)을 따른다. 확정 시점·넘김 간격은 기획서 §4.8.4와 시뮬(`330_climb.rl*`)을 따른다 |
| 표·기획서에 없는 시간 값 | 「이런 연출값은 프리팹 직렬화값으로 사용하고 관련 테이블값은 제거」 | 연출 값은 섹션·부품 프리팹의 직렬화 필드에 둔다. 관련 표 키 제거안은 아래 7.1에 있다(표 구조 변경이라 승인 뒤 진행) |
| 무기 머지 연출 | 「기획서 내용도 참고하고 이게 같이 전달한 영상 참고하라고 한거야」 | 순서는 기획서 05 §5.3.3·§4.10을, 움직임·템포는 참고 영상(`rec.mp4`)을 기준으로 한다. 영상은 이전처럼 정지 화면 몇 장으로 짐작하지 않는다. 머지 구간의 프레임을 촘촘히 확인해 시간을 재고, 잴 수 없는 것은 질문으로 올린다 |

### 7.1 HUD 관련 표 키 제거안 (승인 필요)

dev에서 실제로 읽는 코드를 확인했습니다.

| 표 키(300_runConfig) | 지금 읽는 곳 | 성격 | 제안 |
|---|---|---|---|
| `ui.hand.*` 11개 | 키 선언(`RunConfigKeys`)뿐, 소비 코드 없음 | 손패 UI 연출(실시간) | 표에서 제거. `WeaponSection` 프리팹의 직렬화 필드로 옮기고 기획서 값을 기본값으로 둔다 |
| `flyCard.time`, `flyCard.bezier` | `MV.Fx/MvFlyLayer` | 카드 비행 모양 | 표에서 제거. 카드 비행을 맡는 컴포넌트의 직렬화 필드로 옮긴다 |
| `flyCard.removeMs` | `MvFlyLayer`, 검증기가 `330.rlFlyMs`와 같은지 검사 | 시뮬 적용 시점(`rlFlyMs`)과 같아야 하는 값 | 표에서 제거. 이후 `330.rlFlyMs`도 삭제하고 Card Pos 프리팹 `flySec`을 정본으로 둔다(MV-UI3-CLIMBFX3). 같은 값을 두 곳에 두지 않는다. 검증기 규칙도 함께 지운다 |
| `fly.*`(재화 비행) | `MvFlyLayer`, 검증기가 `fly.removeMs`와 `108.cgatePass.delayMs`를 대조 | 재화가 상단 줄로 날아가는 연출. `removeMs`는 시뮬 지급 지연과 같아야 함 | 같은 원칙으로 제거(모양 → 직렬화 필드, `removeMs` → 시뮬 값). 범위에 넣을지 선택 |

함께 바뀌는 것:
- `RunConfigKeys` 목록, `MvTableValidator.Semantics`(검사 2개), `MvTablesLoadTests`의 300 행 수 기대값
- 표 빌드(setup `BuildTablesOnly`) 산출물·빌드 기록 json
- `MvFlyLayer`가 표 대신 직렬화 값을 읽게 바꾸는 일. 이 컴포넌트가 프리팹에 붙어 있는지는 확인 전이다.

이번 범위 밖이지만 같은 원칙에 걸리는 표 연출 값(사용자 결정 전에는 손대지 않음):
- `flash.*`(`MvFxDriver`)
- `shake.*`(`CameraRig`, 시뮬도 일부 읽음)
- `330.popSec`·`stepPop`(`ClimbStairView` 3D 계단 연출)
- `330.tickEvery`(사운드)

**저장소 규칙 충돌:** `AGENTS.md` 「표에 있는 값과 개수는 표에서 읽는다」는 이번 결정과 부딪힌다. 「게임 규칙 값은 표, 연출 값은 프리팹 직렬화」로 문장을 고치는 안을 함께 올린다.

### 7.2 원작 스크린샷 관찰 (사용자 제공, 2026-11-19, 계단문·무기문 7장)

정지 화면이라 시간은 잴 수 없다. 보이는 것만 적는다. 템포는 참고 영상으로 따로 확인한다.

| 관찰 | 설계에 쓰는 곳 |
|---|---|
| 무기문(「Craft: [무기]」 + 진행 막대)을 지나면 무기 아이콘이 받은 장수만큼 날아간다. 한 장면에 창 아이콘 2개가 공중에 있다. 도착점은 오른쪽 EQUIPPED 아래 세로 스택이다 | `WeaponSection`: 지급 연출은 「월드 문 위치 → HUD 스택 칸」 비행이다. 비행이 끝난 뒤 손패 큐에 넣는다 |
| 스택 칸에 새 무기가 들어올 때 칸 위에서 반짝임이 터진다 | `HudWeaponSlot.PlayIn` 반짝임 |
| EQUIPPED: 큰 장착 카드(공격력 숫자 6)와 그 아래 세로 스택. 장착 카드 등급 색이 바뀐다(창 → 도끼) | 장착 칸과 스택 칸 구성(디자이너 프리팹 `Equip`·`Stack`과 같음) |
| 계단문(「Stairs +1」 + 진행 막대 + 계단 수)과 오른쪽 위 계단 수(15 → 18 → 0) | `StairSection` 계단 수. 문 자체는 월드 오브젝트라 HUD 범위 밖 |
| 왼쪽 세로 게이지(아래 1, 위 2) | 계단 레벨 게이지. 노션은 가로로 합친다고 했으므로 디자이너 프리팹 `Progress_StairLevel`을 따른다 |
| 왼쪽 아래 스킬 카드(「Double Chance 50%」) | `CardPosSection`의 확정 카드 자리 |

### 7.3 참고 영상 프레임 분석 (rec.mp4 96초, BlueStacks 동봉 ffmpeg 4.4.4로 직접 디코드, 2026-11-19)

방법: 우측 EQUIPPED·스택 영역을 10fps로 잘라 변화량을 재고, 변화 구간(11.8~15.7초 등)을 30fps 원본 해상도로 다시 봤다. 프레임: scratchpad `media/pan`, `media/hi`, 시트 `media/sh_*.png`.

| 관찰 | 시각(초) | 기획서 05 §5.3.3과 대조 |
|---|---|---|
| 계단 오르는 동안 우측 패널 전체가 숨는다(3.3~8.0, 18.6~23.3) | 3.3, 18.6 | 기획서에 없음 → 질문 후보 |
| 무기문(Craft) 통과 직후 회색 카드 모양 수십 장이 월드 문 자리에서 사선 줄로 스택 쪽으로 연달아 날아오고, 스택이 한 번에 길어진다 | 11.80~11.87 | 05는 「제자리보다 ch 아래에서 나타나 .28s」(HTML 연출). 영상은 월드→스택 비행. 스크린샷 관찰(7.2)과 같음 |
| 스택 정렬: 높은 등급(초록·파랑)이 위, 회색(1단계)이 아래 | 12~15 | 같음(높은 등급 위) |
| 머지: 회색 3장이 둘째 장 자리에서 하나로 접히고, 그 자리 카드가 흰 빛으로 약 1.3배 커졌다가 방사형 반짝임과 함께 윗단계 색(초록)으로 바뀌며 제자리 크기로 돌아온다. 아래 칸들은 위로 당겨진다 | 12.20~12.47(1회 ≈0.25~0.3초) | 05 「모으기 → 결과 wmerged scale 1.3·밝기 3 → 없음」과 같은 모양. 모으는 이동은 프레임에서 거의 안 보임 |
| 연쇄: 회색 머지가 약 0.4초 간격으로 이어지고, 초록 3장이 모이면 파랑으로 같은 연출 | 11.9, 12.3, 12.7 … 14.7~15.0 | 05 연쇄와 같음. 간격 ≈ md(0.497)+쉼(0.04)과 비슷 |
| 반짝임은 새로 생긴 윗단계 칸 위에서 터진다 | 12.3, 14.8 | 7.2 스크린샷 관찰과 같음 |

정하지 못한 것(무기 섹션 계약 전에 사용자 확인): ① 전달 연출을 영상대로 월드 무기문 → 스택 비행으로 할지(05 문구와 다름), ② 계단 중 패널 숨김을 넣을지(기획서에 없음).

### 7.4 사용자 결정 (2026-11-19, 영상 분석 뒤)

1. 머지 연출: 영상의 3장 수렴·확대·반짝임·등급 변경·재정렬을 05 §5.3.3 범위(모으기 md·결과 wmerged·연쇄) 안에서 만든다. 시간은 `WeaponHandModel`·`WeaponHandTiming`(프리팹 직렬화).
2. 계단 이동 중 우측 패널 숨김은 넣지 않는다.
3. 시트 300 탭의 `300102007`(PROJ_SPEED)은 별도 계약(MV-SETTINGS-PROJSPEED `0039d863`)의 변경으로 두고, 이번 범위에서 고치지 않는다. HUD3 표 빌드에서 지운 키가 다시 생기거나 데이터가 어긋나는지만 본다.
4. 무기 지급 연출은 넣는다(사용자 「무기 지급 연출은 추가해야할거같아」). 구조 = **둘로 나눔**:
   - 월드 부품: 무기문 위에 쌓여 늘어나는 무기 카드 묶음. 무기문 프리팹 자식, 빌보드·깊이 정렬은 기존 글자판(`Obj_Text_Count`, SpriteRenderer+TMP)과 같은 방식. 그림은 103_item.icon을 Atlas_UI_Icon 스프라이트 그대로 SpriteRenderer로 쓴다(아틀라스 복제 없음, 월드 배치 1회 추가, 밉맵 없음 주의).
   - HUD 부품: 통과 순간 각 카드의 화면 위치·크기를 한 번 투영해 그 자리에 HUD 카드를 꺼내고, HUD 좌표계에서 스택 칸까지 날린다. 기존 `MvFlyLayer` ToHud(Projector·Anchors·지연 70ms, `CRAFT_ICON_MAX` 5)를 쓰고, 없는 `IMvFlyView`·`IMvHudAnchors` 구현을 무기 섹션 쪽에 만든다. 도착이 손패 모델 Deliver 시점이 된다.
   - 문 위 묶음 장 수 = 만든 장 수(Made+Extra, 글자판 숫자와 같은 값) 그대로, 상한 없음(사용자 2026-11-19 「만든 장 수 그대로」). 통과 때 날아가는 장 수도 만든 장 수 그대로(사용자 2026-11-19 「날아가는 것도 만든 장 수 그대로 해」, 너무 많으면 나중에 장 수를 머지해 줄이는 방식 검토 예정 — 우선은 그대로). 지금 `MvFxRouter`가 `CRAFT_ICON_MAX`(5)로 자르는 부분은 무기 섹션 계약에서 바꾸고, 이 표 키를 지울지는 표 구조 변경이라 그때 제거안을 보여 승인받는다. 묶음 배치 모양은 영상 기준으로 무기 섹션 계약에서 정한다.
