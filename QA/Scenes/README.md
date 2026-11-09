# 장면 정의 형식 (`QA/Scenes/<장면ID>.json`)

MV-QA-HARNESS. 프로토 쪽 도구(`Tools/MvCapture`)와 Unity 쪽 도구(`S/MV.Editor/Capture/`, `S/MV.Boot/DebugInject/`)가 **같은 파일**을 읽는다. 기준 [12 §12.1~12.2](../../Docs/MV-SPEC-01/12_Acceptance.md). 줄 번호 `Lnnnn`은 정본 `Docs/Reference/MoonVillage.html`(SHA256 `fe573cf2…dfa`)의 줄이다.

## 필드

| 필드 | 형식 | 뜻 |
|---|---|---|
| `id` | 문자열 | 파일 이름과 같다. 12 §12.2의 장면 ID(`W-01`, `U-03-P5`처럼 변형은 `-접미사`) |
| `title` | 문자열 | 12 §12.2의 「상태」 칸 요약(설명용, 도구는 읽지 않음) |
| `seed` | 0~2³²−1 정수 | 난수 시드. 프로토는 `Math.random`을 mulberry32(seed)로 바꾼다(아래 「난수」) |
| `rng` | 선택 `{mode, c}` | `mode`: `"mulberry32"`(기본) 또는 `"const"`(모든 호출이 `c`, 04 §4.17.2-5의 ConstRng 대조용) |
| `preset` | `"easy"` \| `"mid"` \| `"orig"` | 밸런스 프리셋 `BALS`(L832). 부팅 직후 `Object.assign(BAL, BALS[p]); DBG.rate = BAL.rate`(L3863 버튼이 하는 상태 변경과 같고, 토스트·버튼 표시는 하지 않는다) |
| `save` | 정본 save 객체 또는 `null` | **정본 save JSON 그대로**(D-67). 페이지 로드 전에 `localStorage['catescape_moon_v1']`(L843)에 넣고, 정본 `load()`(L846)가 `DEF()`(L844)와 합쳐 읽는다. `null`이면 저장 없음 → 첫 실행 `DEF()` |
| `epochMs` | 선택 정수 | 가짜 `Date.now()`의 시작 값(ms). 기본 `1767236400000`(2026-01-01 12:00 KST). 부활 쿨타임 `rb.cdUntil`(L2541)·광고 날짜 `ads.day`(L2423)가 이 시계를 쓴다 |
| `inputs` | 배열 | 입력 기록(아래). 프레임 번호 오름차순 |
| `ops` | 선택 배열 | 테스트 패널 조작(아래). 입력이 아닌 상태 주입 |
| `frames` | 정수 ≥ 1 | 장면 길이(프레임 수) |
| `captureAt` | 정수 배열 | 캡처할 프레임 번호(1…frames). 프레임 *f*를 진행한 **직후** 화면. 장면마다 3장 이상 |
| `mask` | `[{x,y,w,h,note}]` | SSIM 계산에서 뺄 사각형(스테이지 1080×1920 픽셀 좌표). 12 §12.4.1 |
| `protoNA` | 선택 문자열 | 프로토에 없는 장면(예: U-14). 값은 사유. 프로토 도구는 캡처하지 않는다 |

### 정본 save 키 (L844 `DEF()`)

`level`(Level=판 번호), `area`(0부터, Area N = `area`+1), `zone`(= `floor(area/5)`, 정본이 로드 뒤 다시 계산 L851), `gold`(츄르), `souls`(생선), `dia`(캣닢), `bars`(Area별 장애물 남은 체력 `{"A<area>": {floor: {idx: hp}}}`), `cp`(체크포인트 Floor 0~2), `up{stair,weapon,income}`(로비 강화 3종 P), `deck[9]`(시작 덱, 단계별 장수), `god`(야옹신 id), `escaped`, `runs`, `aup{rate,range,clone,crit,dbl}`(Area 강화), `rb{lv,tokens,pending,perks{power,crit,obst,dcap},cdUntil,open}`(아홉 목숨), `ads{day,dia,skip}`, `zoneClear`.
Unity는 이 객체를 MV-SAVE의 정본 가져오기 변환(09 §9.9, D-67)으로 `MV.Save` 묶음에 넣는다. 런 상태(`R`, L1808 `newRun`)는 save에서 만들어지며 장면 정의에 직접 적지 않는다.

## 좌표와 화면

- 스테이지 1080×1920(9:16, `--u` = 10.8px). 프로토는 뷰포트 **1112×1920**, `deviceScaleFactor` 1로 연다. 정본 body 좌우 여백 16px(L26)이 있어 뷰포트를 1080으로 두면 스테이지가 1048×1863이 되기 때문이다(12 §12.1.1의 「뷰포트 1080×1920」을 스테이지 크기로 읽음). 캡처는 `#stage` 영역만 잘라 정확히 1080×1920 PNG.
- 모든 좌표는 스테이지 왼쪽 위 원점, 아래로 +y, 단위 스테이지 픽셀.

## 입력 기록 `inputs[]`

| `type` | 필드 | 프로토 재생 |
|---|---|---|
| `down` | `frame, x, y` | 그 점의 맨 위 요소(`elementFromPoint`)에 `pointerdown`(pointerId 1, touch) |
| `move` | `frame, x, y` | `down`을 받은 요소에 `pointermove` |
| `up` | `frame, x, y` | 같은 요소에 `pointerup`, 손을 뗀 점이 같은 요소면 `click` |
| `tap` | `frame, x, y` | 같은 프레임에 `down` + `up`(버튼 누르기, 로비에서 캔버스 탭 = 출발 L3843) |
| `key` | `frame, key, state` | `key` = `ArrowLeft`\|`ArrowRight`, `state` = `down`\|`up` (L3848~3853) |

드래그 조향은 정본 L3844: `tx = clamp(px0 + (x − x0)/(W/7.2), −3.2, 3.2)`, W = 1080.

## 테스트 패널 조작 `ops[]`

정본 테스트 패널(`#dbg`, L3857~3870)과 `window.__ce`(L3885)에 있는 조작만 쓴다. Unity 개발 빌드 주입 도구가 같은 이름을 구현한다.

| `op` | 필드 | 정본 동작 |
|---|---|---|
| `card` | `id` | 런 중 카드 추가 `applyCard(CARDS id)`(L2311, 패널 `#cardSeg` L3864). id: `atk rate gold orb proc chain storm blast frate spear` |
| `deliver` | `n` | 무기 n장 손패로 배달 `__ce.deliver(n)`(L3885) |
| `debug` | `id` | 패널 버튼 클릭: `addGold`(+500) `addSoul`(+50) `lvUp` `rbOpen` `rbCdClear` `addDia`(+300) `addTok`(+200) |
| `speed` | `v` | 배속 `DBG.ts`(L3862, 1·2·4) |

## 한 프레임 순서

프레임 *f*(0부터) 처리: ① `ops` 중 `frame == f` → ② `inputs` 중 `frame == f` → ③ 시계 +1/60초, 만기 타이머 실행, 정본 `frame(now)`(L3823) 1회 → ④ CSS 애니메이션 시각 맞춤. ③까지 마친 것이 프레임 *f*+1이고 `captureAt`은 이 번호다.

## 프로토 쪽 고정 방법 (12 §12.1.1 구현)

| 대상 | 방법 (`Tools/MvCapture/src/inject.js`) |
|---|---|
| 정본 | 실행마다 SHA256 확인. 파일은 그대로 응답하고 바꾸지 않는다 |
| 시간 | `performance.now`·`Date`·`AudioContext.currentTime`을 가짜 시계로, `requestAnimationFrame`·`setTimeout`·`setInterval`을 가짜 큐로. 실제 rAF는 돌지 않고 하네스가 1/60초씩 진행. CSS 애니메이션·트랜지션은 `document.getAnimations()`의 `currentTime`을 가짜 시계로 맞춤 |
| 난수 | mulberry32(seed) 또는 상수. 기록을 켜면 호출마다 `(순번, 프레임, 값, 줄, 열, 함수, via)` |
| 부팅 | 정본 L3888의 `window.claude.hot.ready(boot)` 고리로 부팅 시점을 잡아 글꼴 로드(`document.fonts`) 뒤 `boot({})`. 같은 고리에서 CDP 디버거로 정본 IIFE 스코프 접근(`__mvq.ev`)을 만든다(덤프·프리셋용) |
| 네트워크 | 정본·three.js r128·글꼴만 로컬 파일로 응답, 그 밖은 차단 |
| 글꼴 | Black Han Sans, Gothic A1 400/700/800 TTF + 이모지는 Noto Color Emoji(`Tools/MvCapture/vendor/SOURCES.md`) |
| 소리 | 오디오 시계를 가짜 시계에 묶는다(정본 소리 잡음 L1646이 같은 `Math.random`을 쓰므로 실제 시계면 실행마다 난수 소비가 달라짐) |

Unity 쪽 대응은 12 §12.1.2. `seed`는 mulberry32 그대로 쓰되, 로직 대조는 SIM 덤프의 난수 기록 재생(12 §12.3.2)으로 한다 — 프로토는 그림·소리·로직이 난수 한 줄기를 나눠 쓰므로 시드만으로는 Unity 런과 같아지지 않는다.

## 장면 목록

12 §12.2의 36개(W-01~17, F-01~05, U-01~14). U-03은 P=0·5·MAX 세 파일. U-14(컨텐츠 메뉴)는 정본에 없어 `protoNA`. 캡처 결과 해시는 `QA/Captures/Proto/hashes.tsv`, 각 캡처 순간의 상태(mode·z·x·level·area·난수 호출 수)도 같은 표에 있다. 장면을 고치면 `node Tools/MvCapture/src/cli.js check`로 형식을 검사하고 `probe`로 프레임을 찾는다(`Tools/MvCapture/README.md`).
