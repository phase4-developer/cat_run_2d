# 5. 화면 명세

[입구](README.md) · 앞: [04 게임 규칙](04_GameRules.md) · 다음: [06 이펙트](06_Effects.md)

이 절은 UI 담당(UGUI)이 이 문서만으로 정본 `MoonVillage.html`과 픽셀 근사로 같은 화면을 만들도록 화면마다 요소·위치·크기·색·글꼴·문구 키·데이터·표시 조건·애니메이션을 정한다. 근거는 정본 CSS(L1~605)·DOM(L607~727)·화면 JS다. 새 설계는 로비 「컨텐츠」 메뉴(§5.15), 메타 공통 패널 틀(§5.16), 모드 전용 화면(§5.17), 일시정지 정의(§5.18)다. 결정 대기 항목은 §5.19.

다른 문서와의 경계:

| 내용 | 정본 문서 |
|---|---|
| 규칙·공식(비용, 보상, 쿨타임, 정산) | [04](04_GameRules.md) |
| 이펙트·흔들림·팝업 숫자 호출 목록·배너·토스트 호출 시점 | [06](06_Effects.md) |
| 표 열 정의, 문구 키 목록 | [07](07_Tables.md) |
| 리소스 추출·아틀라스 묶음·압축 | [03](03_ResourcePipeline.md) |
| 월드 화면(카메라·로비 아지트·오브젝트), 색 공간 | [02](02_Visual.md) |
| 추가 모드·메타 콘텐츠 목록 | [08](08_Content.md) |
| 저장 필드 이름 | [09](09_Save.md) |

## 표 읽는 법

| 열 | 뜻 |
|---|---|
| 위치 | CSS 그대로 스테이지 가장자리 기준 `left/top/right/bottom`을 u로 쓰고, 괄호 안에 1080×1920 기준 px. `가운데`는 가로 가운데 정렬 |
| 크기 | `너비×높이`. u와 (px) |
| 색 | 정본 hex·rgba. `--gold` 같은 이름은 §5.1.6 색 토큰 |
| 글꼴 | `D` = Black Han Sans(디스플레이), `B4`·`B7`·`B8` = Gothic A1 400·700·800. CSS 900은 800으로 본다(§5.1.7). 뒤 숫자는 크기 u. `D*`는 합성 볼드 |
| 문구 키 | `컬렉션:키` → 한국어 원문. 컬렉션은 `ui`(화면 고정 문구, 키 `ui.<화면>.<항목>`), `name`·`description`(표 행 이름·설명, 키 = 행 id), `msg`(긴 안내), `toastMessage`(토스트). `{0}`은 자리 표시 |
| 데이터 | `save.*` = 프로토 저장 필드(Unity 이름은 [09](09_Save.md)), `R.*` = 런 상태(MV.Run), `CN.*` = 계단 오르기 상태, `표.열` = 규약 MV 표 목록(정크버스터즈 방식, 예 `103_item.gradeCol`) |
| L | 정본 줄 번호 |

값 출처 태그: **[프로토]** 정본 원값, **[원작]** HellEscape 원작 참고, **[창작]** 이 문서 창작, **[추정]** 코드만으로 확정 못 한 해석. 태그가 없는 값은 [프로토]다.

## 5.1 공통

### 5.1.1 단위와 스테이지

| 항목 | 값 | 근거 |
|---|---|---|
| 스테이지 비율 | 9:16 고정. 바깥 영역 높이로 맞추고, 너비가 넘치면 너비로 맞춘다. 최소 너비 200px | L944~947 |
| 기본 단위 `u` | **1u = 스테이지 너비 / 100** | L29, L948 |
| 기준 해상도 | 1080×1920 → **1u = 10.8px**, 스테이지 높이 = 177.78u | 환산 |
| 세로 % | `top: y%` = y × 1.7778u = y × 19.2px | 환산 |
| 고정 px | CSS의 `1px`·`2px` 같은 값은 화면에 비례하지 않는다. 환산 배율은 §5.1.4 | L1~605 |
| 레터박스 색 | `#0b0b0e`(`--night`). Unity는 지원 범위 밖에서만 02가 그림(§5.1.2). 정본 스테이지 그림자는 넣지 않는다 | L25, L29 |
| 스테이지 바탕 | `#2e2230` (월드가 다 덮으므로 보통 안 보임) | L583 |

### 5.1.2 CanvasScaler·StageRoot·안전 영역 — 결정 대기(D-30)

[02](02_Visual.md) §2.7·D-41(권장 ②)과 짝을 맞춘다: **월드 렌더는 화면 전체, UI만 `Screen.safeArea` 안**. UI는 폭을 1080으로 고정하고 세로만 늘린다.

| 항목 | 값 | 이유 |
|---|---|---|
| Canvas | Framework 규칙대로 UIManager 아래 Screen Space - Camera 하나 | Framework `Features/UI.md` |
| CanvasScaler | Scale With Screen Size, Reference **1080×1920**, Screen Match Mode **Expand**, Reference PPU 100 | 9:16보다 긴 화면은 폭이 1080(Match 0과 같음), 넓은 화면(태블릿)은 높이가 1920(Match 1과 같음)이 된다 |
| StageRoot | Canvas 바로 아래 RectTransform. 게임 컴포넌트 `StageFitter`가 안전 영역을 Canvas 단위로 바꾼 사각형에 맞춘다. **폭 = min(안전 폭, 안전 높이 × 9/16)** 를 1080으로 보는 배율, 높이 = 안전 높이 / 배율(9:16에서 1920, 긴 폰에서 더 큼). 가로 가운데 | 정본은 `html`에 안전 영역 패딩을 넣고 그 안에 스테이지를 맞춘다(L1, L25~28). 긴 폰에서 위아래를 버리지 않는다 |
| u 계산 | 1u = StageRoot 폭 / 100 = **늘 10.8**. `top: y%`는 StageRoot **실제 높이**의 y% | 긴 폰에서 % 요소(무기·게이지·사이드 버튼)는 비례 이동하고, `u`로 잡은 요소(위 칩·아래 강화 버튼)는 가장자리에 붙는다 — 정본을 긴 브라우저 창에 띄운 것과 같다 |
| UIManager 루트 | `screenRoot`·`hudRoot`·`popupRoot`·`effectRoot`를 StageRoot 자식으로 두고 꽉 채운다. `loadingRoot`만 Canvas 전체 | — |
| 막(dim)·전체 패널 | 막과 지도·가짜 광고 바탕은 **Canvas 전체**(안전 영역 밖까지)를 덮는다. 내용 상자만 StageRoot 기준 | 노치 쪽에 월드가 비치지 않게 |
| 안전 영역 밖 | 월드가 보인다(넓은 화면의 좌우 바깥 포함) | 02 D-41 ② |
| 레터박스 | 지원 범위 밖(9:21보다 길거나 3:4보다 넓음)에서만 02가 그리는 `#0b0b0e` 띠. UI는 그리지 않는다. 정본 스테이지 그림자는 넣지 않는다 | 02 §2.7 |
| 월드 좌표 ↔ UI | `MV.View.WorldToStage(world)` = 카메라 화면 좌표 → StageRoot 로컬 좌표. 날아가는 아이콘·떠오르는 숫자는 이것만 쓴다 | 카메라 rect를 UI와 맞추지 않는다 |
| 좌우 16px 여백 | **만들지 않는다**. 정본 `body` 좌우 `max(16px, 안전영역)`(L25)은 브라우저 표시용 | — |
| 화면 방향 | 세로 고정(Android `Portrait`) | 정본은 가로에서 좌우 필러박스(L944~947) |
| 크기 변경 | 해상도·안전 영역이 바뀌면 `StageFitter`가 다시 계산 | L3880 `resize` |
| 캡처 대조 | 정본 대조 캡처는 9:16(1080×1920) 기기·창에서 한다. 그때 StageRoot = 1080×1920으로 정본과 같다 | [12](12_Acceptance.md) |

### 5.1.3 CSS 위치 → RectTransform 변환 규칙

UGUI는 y가 위로 증가한다. 아래 규칙으로 옮긴다. 모든 값은 StageRoot(폭 1080, 높이 H; 9:16에서 1920) 기준. `top:y%` 요소는 앵커 y = 1 − y/100으로 둬서 높이에 비례하게 한다. 이 문서의 「1080 기준 px」 값은 H = 1920일 때다.

| CSS | anchorMin / anchorMax | pivot | anchoredPosition |
|---|---|---|---|
| `left:a; top:b` | (0,1)/(0,1) | (0,1) | (a, −b) |
| `left:a; top:y%` | (0, 1−y/100) 둘 다 | (0,1) | (a, 0) |
| `right:a; top:b` | (1,1)/(1,1) | (1,1) | (−a, −b) |
| `left:50%; translateX(-50%); top:b` | (.5,1)/(.5,1) | (.5,1) | (0, −b) |
| `left:50%; top:y%; translate(-50%,-50%)` | (.5, 1−y/100) 둘 다 | (.5,.5) | (0,0) |
| `left:a; bottom:b` | (0,0)/(0,0) | (0,0) | (a, b) |
| `left:a; right:c; bottom:b` | (0,0)/(1,0) | (.5,0) | offsetMin.x=a, offsetMax.x=−c, y=b |
| `inset:0` | (0,0)/(1,1) | (.5,.5) | 오프셋 0 |

- `display:flex` → `HorizontalLayoutGroup`/`VerticalLayoutGroup`. `gap` → spacing, `padding` → padding, `align-items` → childAlignment. 내용에 따라 크기가 바뀌면 `ContentSizeFitter`.
- `grid repeat(n,1fr)` → `GridLayoutGroup`(셀 너비 = (부모 너비 − gap×(n−1))/n) 또는 가로 레이아웃 + `LayoutElement.flexibleWidth=1`.
- 내용이 안 바뀌는 요소는 레이아웃 그룹 없이 위치를 고정한다(재계산 비용). 목록·버튼 줄만 레이아웃 그룹을 쓴다.
- `pointer-events:none`(`.ov`, `#deck`, `#hc`, `#ms`, `#pops`, `.fly`) → 해당 Graphic의 `raycastTarget = false`. 버튼만 켠다(L31~32).
- 줄 높이: `line-height` 지정값은 TMP Line Spacing으로 맞춘다. 지정이 없으면 브라우저 기본(약 1.2)로 본다 [추정]. 최종은 캡처 대조([12](12_Acceptance.md))로 맞춘다.

### 5.1.4 고정 px 환산 — 결정 대기(D-31)

정본의 고정 CSS px(테두리 1px, 둥근 9px 등)는 기기 CSS 너비에 따라 커 보이는 정도가 다르다. 휴대폰 세로(CSS 390px, 스테이지 358px)에서 1080 기준 배율은 1080/358 = 3.02다.

**권장: 고정 px × 3 = 1080 기준 px.** 휴대폰 화면 모습과 같다.

| CSS | 1080 기준 | 쓰는 곳 예 |
|---|---|---|
| 1px | 3 | 패널 테두리, `--panel-hi` 윗줄 |
| 1.5px | 4.5 | `.sideBtn`·`#ampRow` 테두리 |
| 2px | 6 | `#godBtn`·`.mp-*` 테두리, `.sh` 그림자 y |
| 3px | 9 | `#fgEdge`, 랩 표시선 |
| 6px / 12px | 18 / 36 | `#fgMask`·`.hcard` 사선 무늬 간격 |
| 9px | 27 | `.up .g`·`.ms-bar` 둥근 모서리 |
| 30px | 90 | `flyTo2` 도착점 상한 |

`-webkit-text-stroke`도 같은 배율이다(1px → 3, .6px → 1.8, .8px → 2.4).

### 5.1.5 층(sortingOrder)

구현: 각 UI 프리팹 루트에 `Canvas`(overrideSorting = true, Sorting Layer `UI`, sortingOrder = 아래 값) + `GraphicRaycaster`를 붙인다. UIManager 루트 순서와 상관없이 층이 정해진다. 같은 층 팝업이 여럿이면 연 순서대로 `기준값 + 2×순번`.

| sortingOrder | 층 | 담는 것 | 정본 z | 처리 |
|---|---|---|---|---|
| 0 | (비움) | 레터박스는 02 소유(월드 쪽) | — | — |
| 10 | Vignette | `#vig` 비네트(로비·런 항상) | auto(L610) | — |
| 100 | Screen | `UIScreen_Lobby` | auto | 로비 안 `#mapBtn` z3은 같은 화면 안 순서로 처리 |
| 200 | HUD | `UIHud_Run` | auto, `#ampRow` 4 | — |
| 210 | FloatText | `UIHud_FloatText`(`#pops`) | auto(L636) | — |
| 220 | HUD 가운데 | `UIHud_Run` 하위 Canvas: `#ms` 등급 미터(5), `#hc` 가호 카드(6), `.flycard`(30), 손패 머지 카드(`.wcard2.mg` 300·`.merged` 250) | 5~300 | **고침**: 손패 머지 카드를 HUD 층 안에 가둔다. 정본은 쌓임 맥락이 없어 광고 화면(40)보다 위지만 둘이 동시에 나오는 경우가 없어 겉모습은 같다 |
| 280 | Overlay | `UIHud_Overlay`: 배너, 플래시, 날아가는 아이콘 | auto | — |
| 300 | Result | `UIPopup_Result`, `UIPopup_ModeResult` | auto(문서 마지막) | 정본대로: 사운드·테스트 버튼(z5)보다 아래 |
| 400 | System | `UIHud_System`: `#sndBtn`, `#dbgBtn` | 5 | 정본대로: 결과 화면 위, 패널 아래 |
| 410 | Debug | `UIPopup_Debug`(`#dbg`) | 6 | 개발 빌드만 |
| 500 | Popup | 야옹신·Area 강화·부활·컨텐츠 메뉴·메타 패널·카드 고르기 | 20 | — |
| 550 | FullPanel | `UIPopup_Map` | 25 | — |
| 600 | Unlock | `UIPopup_FeatureUnlock` | 30 | — |
| 700 | Ad | `UIPopup_FakeAd` | 40 | — |
| 800 | Toast | `effects.ui` 토스트 | auto(패널 아래) | **고침(D-32)**: 정본은 야옹신 잠긴 탭을 누를 때 토스트가 패널 어둠 막(z20) 뒤에 흐리게 깔린다. 맨 위로 올린다 |
| 900 | Loading | `loadingRoot` | — | Framework |

### 5.1.6 색 토큰

UI 고정 색은 아트 소유 `UiPalette` ScriptableObject(코드 상수 아님)로 둔다. 등급색·신 색·무기 등급색처럼 콘텐츠에 붙은 색은 표에서 읽는다(`115_cardTier.color`, `113_god.color`, `103_item.gradeCol`).

| 토큰 | 최종 값 | 근거 |
|---|---|---|
| `--ink` | `#fbf6ec` | L10 |
| `--ink-dim` | `#c9c1b2` | L11 |
| `--night` | `#0b0b0e` | L12 |
| `--panel` | `rgba(44,28,40,.74)` | L582(L13·L279 덮어씀) |
| `--panel-edge` | `rgba(255,226,200,.16)` | L582 |
| `--panel-hi` | 안쪽 윗줄 `inset 0 1px 0 rgba(255,255,255,.08)` → 위 가장자리 3px 선 | L279 |
| `--panel-sh` | `0 .6u 2u rgba(0,0,0,.28)` → 아래로 6.48, 흐림 21.6 | L279 |
| `--gold` | `#f2c14e` | L15 |
| `--soul` | `#6cc0f0` | L16 |
| `--stair` | `#6fe08f` | L17 |
| `--danger` | `#ff5a4a` | L18 |
| 금 버튼 그림자 | `#9a6418` | L201, L361, L489 |
| 광고 버튼 | 바탕 `#ffd84a`, 글자 `#2a1608`, 그림자 `#a87a10` | L363, L374 |
| 초록 버튼 | `#3fa85a`, 그림자 `#23683a` | L359 |
| 닫기 빨강 | `#c8463a`(패널), `#d8443a`·그림자 `#8a2620`(지도) | L181, L351, L451 |
| 꺼진 버튼 | `#5a5662`/글자 `#d8d2c6`(금), `#4a4656`/`#a8a2b4`(초록), `#5a5662`/`#bbb`(광고) | L201, L360, L362, L364 |
| 패널 상자 | `linear-gradient(#4a3242, #2a1c26)` | L595 |
| 무기 등급(`WGRADE`) | `#9ea4ae #46c35a #3a8ff0 #a65be8 #f08a24 #e8443a #1fc8c8 #ffd23a` | L771 → `103_item.gradeCol` |
| 카드 등급(`TIERS`) | COMMON `#c9c2b4`, UNCOMMON `#6fd27a`, RARE `#58a6f0`, EPIC `#c07af0`, LEGENDARY `#f2b33d` | L773~775 → `115_cardTier.color` |

**color-mix 규칙.** `color-mix(in srgb, A p%, B)` = sRGB 감마 값 그대로 채널마다 `A×p + B×(1−p)`(알파도 같은 식). `color-mix(in srgb, A p%, transparent)` = A의 알파 × p. 런타임 색(`--gc`, `--tc`, `--wc`, `--nc`)에 쓰므로 코드 함수 `UiColor.MixSrgb(a, b, p)`로 계산한다.

**색 공간 — 결정 대기(D-37).** 정본은 브라우저 sRGB(감마) 혼합이다. Unity Linear 색 공간에서는 반투명 패널(`--panel` .74, 어둠 막 .72~.78)이 정본보다 밝게 섞인다. 색 공간은 [02](02_Visual.md)가 정하고, Linear이면 UI 반투명은 캡처 대조로 알파를 보정한다.

### 5.1.7 글꼴

TMP 글꼴 자산(이름은 [03](03_ResourcePipeline.md) §3.10의 TMP 에셋 이름):

| 키 | 원본 | 정본 쓰임 |
|---|---|---|
| `Font_BlackHanSans_SDF` | Black Han Sans Regular(400만 있음) | `--f-display`: 제목·숫자·버튼 큰 글 (L19) |
| `Font_GothicA1_Regular_SDF` | Gothic A1 Regular 400 | `--f-body` 보통 (L20) |
| `Font_GothicA1_Bold_SDF` | Gothic A1 Bold 700 | `font-weight:700`, `<b>` 기본 |
| `Font_GothicA1_ExtraBold_SDF` | Gothic A1 ExtraBold 800 | `font-weight:800`·`900`(900은 로드 안 됨 → 800으로 그림, L6) |

규칙:
1. 한 줄에 글꼴이 섞이면(예: `보유 <span class=num>0</span>`) TMP `<font>` 태그를 쓰지 않고 라벨을 나눠 가로 레이아웃으로 붙인다. Addressables 글꼴은 `<font>` 태그 경로에 없다.
2. **합성 볼드 — 결정 대기(D-38).** `font-weight`를 400으로 지정하지 않은 `<b>`·900에 Black Han Sans가 걸리면 브라우저가 가짜 볼드를 만든다. 대상: `#hStair`, HUD·로비 재화 숫자(`#hGold #hSoul #hDia #lGold #lSoul #lDia`), `#lSoulTxt`, `#rbCd`, `#aupDia`, `#rbTok`, `#rateVal`, `#fgNext`·`#fgTop b`(날기 중 글꼴 상속, §5.2.4). 권장: TMP `Bold` 스타일로 재현하고 `Font_BlackHanSans_SDF`의 Bold Style(굵기)·Bold Spacing을 「글자 크기의 약 1/30만큼 굵게, 글자 간격 +3%」로 맞춘다(Chrome 합성 볼드 근사 [추정]). 표에서는 `D*`로 표시.
3. `.num`(L35)의 `tabular-nums`: Black Han Sans 숫자 폭이 이미 고정폭에 가깝다 [추정]. TMP Monospace는 쓰지 않는다.
4. 이모지·특수 기호(✕ ▶ ♪ ★ ✔ 🔒 🌿 🐾 등)는 글꼴로 그리지 않고 TMP 스프라이트 자산 `TMP_Inline`의 `<sprite name=...>`로 넣는다(§5.1.10).

**라벨 TMP 스타일(05가 정함).** 프리팹 라벨은 아래 머티리얼 프리셋 이름 + 글꼴 기호(D/B7/B8/D*)만으로 만든다. 머티리얼 프리셋 자산 이름 = `TMP_Mat_<글꼴>_<프리셋>`(예 `TMP_Mat_BlackHanSans_sh`), 위치는 03 규칙. TMP Style Sheet는 쓰지 않는다(크기는 u 값이 화면마다 달라 스타일로 묶을 이점이 적음).

TMP 머티리얼 프리셋(글자 그림자·외곽선). 두께는 §5.1.4 배율 적용 후 값이다. TMP 외곽선은 CSS `-webkit-text-stroke`처럼 글자 가장자리 중심으로 그린다.

| 프리셋 | CSS | TMP 설정 | 쓰는 곳 |
|---|---|---|---|
| `sh` | `text-shadow: 0 2px 0 rgba(0,0,0,.55), 0 0 6px rgba(0,0,0,.35)` (L36) | Underlay 색 `#0000008C` y −6 흐림 0 + Glow(바깥) `#00000059` 흐림 18 근사 | `.sh` 붙은 모든 글(HUD 왼쪽 위, 로비 제목, 안내, 배너, 경고) |
| `pop` | `0 2px 0 rgba(0,0,0,.7)` (L143) | Underlay `#000000B3` y −6 | 떠오르는 숫자, 날아가는 글 |
| `stroke.lv` | stroke .6px `rgba(0,0,0,.5)` (L516) | Outline 1.8 `#00000080` | `#gLv` |
| `stroke.hq` | stroke 2px `rgba(0,0,0,.55)` (L415) | Outline 6 | `.hq` |
| `stroke.ht` | stroke 1px `rgba(0,0,0,.5)` (L414) | Outline 3 | 가호 카드 등급명 |
| `stroke.hn` | stroke .6px `rgba(0,0,0,.6)` (L417) | Outline 1.8 | 가호 카드 이름 |
| `stroke.msT` | stroke .8px `rgba(0,0,0,.45)` (L543) | Outline 2.4 | 등급 미터 제목 |
| `stroke.msC` | stroke .8px `rgba(0,0,0,.4)` (L546) | Outline 2.4 | 등급 미터 다음 목표 |
| `map.zone` | `0 3px 0 #1a1020, 0 0 8px rgba(0,0,0,.6)` (L454) | Underlay `#1a1020` y −9 + Glow `#00000099` 흐림 24 | 지도 구역 이름, Area 이름(`0 2px 0` → y −6) |
| `map.tag` | stroke 1px `#1a1020` (L456) | Outline 3 | 지도 `ZONE n` |
| `map.cp` | stroke .8px `#0c2c58` (L470) | Outline 2.4 | 체크포인트 이름 |
| `map.ft` | stroke .8px `#2a0a40` (L477) | Outline 2.4 | 지도 기능 이름 |
| `shadow.1` | `0 1px 0 #000`·`rgba(0,0,0,.6)` (L224, L499, L550) | Underlay y −3 | 진행바 글, 배지 |

### 5.1.8 공통 부품

**도형 그리기 — 권장 `MvShape`(게임 구현, MV.UI).** CSS의 둥근 사각형·테두리·그라데이션·원은 스프라이트 대신 게임 컴포넌트 `MvShape : MaskableGraphic` + 셰이더 `MV/UI/Shape`(SDF)로 그린다. 매개변수(모서리 반지름 u, 테두리 두께·색, 채움 단색/선형(각도·정지점 3개까지)/원형(중심·정지점), 흐림)를 정점 UV 채널로 넘겨 한 번에 묶어 그린다(Canvas `additionalShaderChannels`에 TexCoord1~3). 이유: 정본은 반지름 16종(.6u~5u)·그라데이션 40여 종이고 런타임 색(`--gc`·`--tc`)이 섞여 스프라이트로는 수가 많고 9-slice 테두리 두께가 반지름과 함께 늘어나는 문제가 있다. 대안: 9-slice 스프라이트(`ui.common`) — 결정이 필요한 범위는 아니고 구현 선택이다.

| 부품 프리팹 | 정본 | 규격 |
|---|---|---|
| `Panel` | `--panel` 계열 | 바탕 `--panel`, 테두리 3px `--panel-edge`, 반지름은 쓰는 곳마다, 윗줄 하이라이트(`--panel-hi`), 그림자(`--panel-sh`) |
| `CurrencyChip` | `.chip` L37~40, L280~281 | 최소 너비 16u(172.8), 안쪽 여백 위 .6u·오른쪽 2.4u·아래 .6u·왼쪽 .8u, 사이 1.4u, 반지름 5u(알약), 글 D* 3.5u(37.8) `--ink`, 아이콘 4.2u(45.36) 정사각, 세로 가운데, Panel 바탕·테두리·그림자. 입력 = `01_wealth` id(아이콘 `01_wealth.icon`), 표시 문자열은 화면이 넘긴다 |
| `SideButton` | `.sideBtn` L339~344 | 너비 17u(183.6), 여백 1u .6u, 사이 .3u, 세로 가운데, Panel 바탕, 테두리 4.5px `--sc`, 반지름 2u, 그림자 hi+sh. 아이콘 6u(64.8), 이름 D 3u `--sc`, 부제 B8 2.1u `--ink-dim` 가운데 줄높이 1.2. 잠김: `grayscale(.8) brightness(.8)` |
| `GoldButton` | `.bigBtn` L361, `#gpPick` L200, `#ulOk` L489 | 바탕 `--gold`, 글자 `#0b0b0e`(해금 확인은 `#2a1608`), D 5u, 반지름 2u(야옹신 2.4u), 여백 1.8u, 그림자 아래 1u `#9a6418`(블러 없음) |
| `GreenButton` | `.row2 button` L359~360 | 최소 너비 17u, D 4u, 반지름 1.6u, 여백 1.4u 1.6u, 바탕 `#3fa85a`, 글자 `#fff`, 그림자 아래 .6u `#23683a` |
| `AdButton` | `.adBtn` L363~364 | D 3.6u, 반지름 1.6u, 여백 1.2u 2.4u, 바탕 `#ffd84a`, 글자 `#2a1608`, 그림자 아래 .7u `#a87a10` |
| `CloseX` | `.x` L351, `#gpClose` L181 | 오른쪽 2u·위 2u, 7u(75.6) 원, 바탕 `#c8463a`, ✕ 스프라이트 3.6u 흰색 |
| `DimBg` | `.pnl`·`#godPanel` 바탕 | 전체, `rgba(10,8,16,.74)`, 레이캐스트 받음 |
| `Row2` | `.row2` L353~360 | 가로, 세로 가운데, 사이 2u, 바탕 `rgba(0,0,0,.28)`, 테두리 3px `--panel-edge`, 반지름 2u, 여백 1.4u 2u. 아이콘 칸 너비 9u·아이콘 6u, 가운데 칸(늘어남) 이름 B7 3.4u `--ink` + 설명 B7 2.6u `--ink-dim`(강조 `#8ff0a0`), 오른쪽 GreenButton |
| `ProgressBar` | `.rbar`·`.bar`·`.up .g` | 쓰는 곳마다 높이·색. 채움은 `Image.fillAmount` 대신 너비 조절(둥근 끝 유지) |
| `RewardSlot` | 결과 보상 칸 `.rw div`(L262), 메타 보상 칸 | **consumeData 한 칸(type·id·value)을 그리는 공용 위젯.** 아이콘·이름·수량을 표에서 찾는다(아래 「consumeData 표시 규칙」). 모양 변형 3종: `Card`(결과 칸: 세로, 아이콘 7u·수량 D 6u·이름 B7 2.8u), `Inline`(아이콘 + 수량 한 줄, 아이콘 3.6u·D 3.6u), `Badge`(아이콘 위 수량 배지) |
| `PriceButton` | 강화 버튼 가격 줄 `.up .p`, Area 강화·부활 행 버튼, 컨텐츠 입장 버튼 | **consumeData 비용(priceType·priceId·priceValue)을 그리는 버튼.** 가격 아이콘 + `fmt(값)`. 보유량 < 값이면 꺼짐. 최대 단계면 문구 `ui:ui.common.max` 「MAX」. 바탕 모양은 쓰는 곳에서 GreenButton·AdButton·가격 줄 중 고른다. 광고 비용(type 100)이면 아이콘 ▶ + 문구 |

**버튼 공통 규칙.**
- Unity `Button.transition = None`. 정본은 누름 효과가 없다. 예외: 로비 강화 버튼만 누르면 .6u 내려간다(L317, .08s).
- 꺼짐은 `UIButtonLock` HardLock + `UIButtonStateVisual`로 「보통/꺼짐」 두 모양(위 표의 꺼진 색)을 바꾼다. 정본은 꺼진 버튼을 눌러도 아무 일이 없다. 잠김 안내 토스트가 있는 버튼(부활 사이드 버튼, 야옹신 잠긴 탭)만 SoftLock + `onLockedClick`.
- 소리 `UIButtonSound`는 정본이 소리를 내는 버튼에만 붙인다(화면마다 표에 적음). 정본은 대부분 버튼에 소리가 없다.

### 5.1.9 그림자·그라데이션·필터 구현

| CSS | Unity 구현 |
|---|---|
| `box-shadow: 0 Yu 0 색`(단단한 아래 그림자) | 같은 모양 `MvShape`를 뒤에 두고 y −Y만큼 내린다 |
| `box-shadow: 0 Y B 색`(흐린 그림자) | `MvShape` 흐림 = B, y −Y |
| `box-shadow: 0 0 B 색`(빛 번짐) | `MvShape` 흐림 = B, 오프셋 0. 알파 애니메이션 가능 |
| `inset 0 1px 0 rgba(255,255,255,.08)` | 위 가장자리 안쪽 3px 선(`MvShape` 상단 테두리만) |
| `linear-gradient`·`radial-gradient` | `MvShape` 채움 모드. 정지점 4개 이상이면 사전 렌더 스프라이트 |
| `repeating-linear-gradient` 사선 무늬 | 이어 붙는 타일 스프라이트(`UI_Skin_HatchTile`, `Atlas_UI_Hud`) `Image.type = Tiled` |
| `filter: brightness(k)`·`grayscale(g)` | 게임 컴포넌트 `MvFilterGroup`: 하위 Graphic 전체에 `_Brightness`·`_Gray` 머티리얼을 건다. 머티리얼은 (k, g)를 0.05 단위로 묶어 공유. 애니메이션 중에만 걸고 끝나면 기본 머티리얼로 되돌린다 |
| `filter: drop-shadow(0 0 B 색)`(아이콘 테두리 빛) | 아이콘 스프라이트의 흐린 사본 `<이름>_glow`(추출 도구가 생성, [03](03_ResourcePipeline.md))을 뒤에 깔고 색을 곱한다 |
| `transform: skewX(-8deg)` | 게임 `MvSkew : BaseMeshEffect`(정점 x += y × tan) |
| `opacity` | `CanvasGroup.alpha` |
| `outline: .7u solid` (지도 현재 Area) | 테두리 바깥 .7u에 그린 `MvShape` 링 |

### 5.1.10 아이콘·이미지

**코드에 이미지 이름을 쓰지 않는다.** 화면의 모든 이미지는 표에서 정한다(규약 규칙 6).

| 이미지 종류 | 설정 위치 | 아틀라스 결정 순서 |
|---|---|---|
| 표 행에 붙은 그림(재화·무기·카드·신·강화·퍽·컨텐츠·해금) | 그 표의 `icon`·`largeIcon`·`bg` 열(스프라이트 이름 또는 Addressables 주소) | 행의 `atlas`/`iconAtlas` 열 → `07_consumeType.iconAtlas` → 아래 표의 기본 아틀라스 |
| consumeData로 가리키는 보상·비용 | `07_consumeType`의 `iconAtlas`·`iconColumn` + `idTable`이 가리키는 표의 그 열 | 같음 |
| 표 행이 없는 화면 고정 그림(사이드 버튼 아이콘, 닫기, 잠금, 비네트, 레터박스 등) | **`09_uiResource`**(제안, id·key·atlas·sprite·address·비고; 번호는 [07](07_Tables.md) 확정). 화면 코드는 `key`(예 `lobby.side.basket`)로만 찾는다 | 행의 `atlas` 열 |
| 큰 단독 이미지(야옹신 초상, 비네트, 컨텐츠 배너) | 표의 `illust`·`bg` 열 또는 `09_uiResource.address` → `AddressableTexture` | — |

**consumeData 표시 규칙(`RewardSlot`·`PriceButton` 공통).** 입력 `(type, id, value)`.
1. `07_consumeType[type]`에서 `idTable`, `iconAtlas`, `iconColumn`을 읽는다.
2. `iconColumn`이 열 이름이면 `idTable[id].<열>`이 스프라이트 이름, 고정 이름이면 그 이름(예 AD 100 → `ad`).
3. 이름은 `name` 컬렉션의 키 = `idTable`의 행 id(예 `01_wealth` 1 → 「츄르」). 고정 타입(AD 등)은 `ui:ui.consume.<codeName>`.
4. 수량은 `fmt(value)`, 앞에 붙는 기호(`+`, `×`)는 쓰는 화면이 정한다.
5. 확률 보상(Drop 50, RandomBox 8)은 상자 아이콘 하나와 「?」를 그리고 누르면 [08](08_Content.md)의 내용 보기로 넘긴다 [창작]. 내용 보기의 확률은 추첨과 같은 그래프 계산(하위 확률 = 부모 행 확률 × 하위 weight / 하위 합)으로 표시하고, 꽝 행도 「꽝」으로 보인다. 하위 묶음이 여러 칸으로 펼쳐져 값이 「기대 등장 횟수」가 되는 항목은 %가 아니라 「평균 N개」로 적는다. 천장이 있으면 확률에 섞지 않고 「N회 안에 확정」 줄을 따로 둔다([07 §7.9.3](07_Tables.md)).

**아틀라스(UI).** `AddressableImage.atlasKey` = 아틀라스 자산 주소, `spriteName` = 표에서 읽은 스프라이트 이름. 이름·크기·압축은 [03 §3.8](03_ResourcePipeline.md)이 정본이다.

| atlasKey | 담는 것(05가 쓰는 것) | 언제 로드 |
|---|---|---|
| `Atlas_UI_Common` | 공통 스킨 `UI_Skin_*`(판·칩·팝업 상자·알약·결과 칸·해금 상자), 공통 버튼 | 시작 |
| `Atlas_UI_Hud` | 계단 상자·손패 틀·장착 틀·등급 미터 카드·막대 채움·사선 타일 `UI_Skin_HatchTile` | 런 진입 전 |
| `Atlas_UI_Lobby` | 강화 버튼 `UI_Skin_UpButton`, 지도 스킨 `UI_Skin_Map*` | 로비 진입 전 |
| `Atlas_UI_Icon` | `UI_Icon_*`(재화·무기 8·시설·계단·벽돌), `UI_Emoji_*`(바구니·지도·불꽃·잎·발·닫기·소리·재생·잠금·강화·퍽·지도 기능), 컨텐츠·메타 아이콘(§5.15·§5.16, 새 이름은 03 규칙 `UI_Icon_Content_<key>`·`UI_Icon_Meta_<key>`로 추가) | 시작 |
| `Atlas_UI_Card` | 가호 카드 그림 `Card_*`, 카드 틀 `UI_Skin_HCard*` | 시작 |

`MvShape`(§5.1.9)로 그리는 판·테두리는 스킨 스프라이트 대신 쓸 수 있다. 어느 쪽으로 그릴지는 화면별로 하나로 정하고 프리팹에 고정한다(혼용 금지).

TMP 스프라이트 자산 `TMP_Inline`: 글 속 아이콘 `herb`·`paw`·`lock`·`check`·`star`·`close`·`play`·`note`·`gold`·`soul`·`dia`·`token`. 그림은 `Atlas_UI_Icon`의 같은 원본에서 별도 텍스처로 굽는다(TMP 스프라이트 자산은 SpriteAtlas를 공유하지 못함, [03](03_ResourcePipeline.md)에 행 필요).

**정본 아이콘 → 설정 위치·스프라이트.** 스프라이트 이름은 [03](03_ResourcePipeline.md) 목록이고, 실제 값은 표 셀이 정한다.

| 정본 | 그림 | 설정 위치 = 스프라이트(아틀라스) | 표시 크기(최대) | L |
|---|---|---|---|---|
| `.ic-gold` 츄르 | SVG: 주황 막대 −35° | `01_wealth[1].icon` = `UI_Icon_Gold`(`Atlas_UI_Icon`) | 7u(결과) | L599 |
| `.ic-soul` 생선 | SVG: 하늘색 물고기 | `01_wealth[3].icon` = `UI_Icon_Soul` | 4.2u | L46 |
| `.ic-dia` 캣닢 | SVG: 초록 잎 | `01_wealth[2].icon` = `UI_Icon_Dia` | 7u | L600 |
| 🐾 목숨 조각 | 이모지 | `01_wealth[4].icon` = `UI_Emoji_Paw` | 6.4u | L693 |
| `.ic-stair` 캔 | SVG: 흰 깡통 | `09_uiResource` `hud.stairCan` = `UI_Icon_Stair` | 5.6u | L589 |
| `.ic-brick` 벽돌 | CSS 도형 4.4u×2.6u | `09_uiResource` `hud.brick` = `UI_Icon_Brick` | 4.4u | L42 |
| 🧺 🗺️ | 사이드 버튼 이모지 | `09_uiResource` `lobby.side.basket` = `UI_Emoji_Basket`, `lobby.side.map` = `UI_Emoji_Map` | 8u | L658~659 |
| 🔥 아홉 목숨 | 이모지 | `05_contentOpen[부활].icon` = `UI_Emoji_Fire` | 6u | L660 |
| 🐾 🎯 🐱 💥 ⚔️ | Area 강화 행 | `13_areaUpgrade.icon` = `UI_Emoji_Paw`·`_Target`·`_Cat`·`_Boom`·`_Swords` | 6u | L2516~2520 |
| 🗡️ 💥 🧱 🌿 | 부활 퍽 행 | `14_rebirthPerk.icon` = `UI_Emoji_Dagger`·`_Boom`·`_Brick`·`_Herb` | 6u | L2534~2537 |
| 🔥 🛡️ ⚔️ 🌙 🐾 🐣 | 지도 기능 상자 | `05_contentOpen.icon` = `UI_Emoji_Fire`·`_Shield`·`_Swords`·`_Moon`·`_Paw`·`_Chick` | 7u | L2582~2586 |
| ✕ ▶ ♪ 🔒 | 닫기·광고·소리·잠금 | `09_uiResource` `common.close`·`common.play`·`common.sound`·`common.lock` = `UI_Emoji_Close`·`_Play`·`_Sound`·`_Lock` | 3.4~7u | L351, L713 |
| `weaponURL(t)` | 캔버스 96×96 | `103_item.icon` = `UI_Icon_Weapon_<t>` | 11u | L870~871, L3388 |
| `cardURL(c)` | 캔버스 120×120 | `110_card.icon` = `Card_*`(`Atlas_UI_Card`). 원소별 그림은 신 덮어쓰기 행의 icon | 17u | L912~914, L3758 |
| `facIconURL(k, 단계)` | 캔버스 96×96 | `10_lobbyUpgrade.icon`(단계 목록 `/`) = `UI_Icon_Fac_Tower_<n>`·`UI_Icon_Fac_Rack_<n>` | 8.6u | L3463 |
| `cannonURL()` | `facIconURL('tower', 2)` | `09_uiResource` `hud.gaugeIcon` = `UI_Icon_Fac_Tower_2` | 8.4u | L869 |
| `paintGod`(야옹신 초상) | 캔버스 200×234 등 | `113_god.portrait` = `Tex_God_<key>`(400×468) → `AddressableTexture`. 등급 미터(21u×23u, 비율 다름)는 같은 그림을 `preserveAspect`+가운데 잘라 씀 | 22u×25.7u | L2481~2491 |
| `#vig` 비네트 | 타원 방사형 | `09_uiResource` `stage.vignette` = `Tex_Lobby_Vignette`(540×960) → `AddressableTexture` | 전체 | L45 |

**이모지 그림 — 결정 대기(D-34).** 정본 이모지는 기기 글꼴(안드로이드는 Noto Color Emoji)로 그려져 기기마다 다르다. 권장: 안드로이드 기준 Noto Color Emoji(SIL OFL 1.1)를 잘라 스프라이트로 쓴다.

큰 단독 이미지는 `AddressableTexture`: 야옹신 초상, 비네트, 컨텐츠 메뉴 배너(§5.15). 그 밖은 모두 아틀라스.

### 5.1.11 애니메이션 공통

**구현 수단 — 결정 대기(D-07).** 권장 기본: 게임 자체 트윈 `MvTween`(코루틴 또는 Update 구동, `Time.unscaledDeltaTime`). DOTween Pro를 쓰기로 하면 같은 데이터로 시퀀스만 바꾼다. 애니메이션 정의는 코드 상수가 아니라 `UiAnimClip` ScriptableObject(키프레임 표)로 둔다.

| 규칙 | 내용 |
|---|---|
| 시간 | UI 애니메이션은 실시간. 히트스톱(×0.12)·테스트 배속의 영향을 받지 않는다(정본 CSS 애니메이션과 `stepHand`·`stepPops`가 실시간, L3826~3832) |
| 곡선 | 아래 표. 곡선은 **키프레임 구간마다** 따로 적용한다(CSS 규칙) |
| 빠진 속성 | 어떤 키프레임에 없는 속성은 그 속성을 가진 앞뒤 키프레임 사이로 보간. 0%/100%가 없으면 요소의 원래 값 |
| 유지 | `forwards`는 마지막 값 유지, 없으면 끝나고 원래 값 |
| 다시 재생 | 정본은 클래스를 빼고 다시 붙여 처음부터 재생한다 → `Play()`는 항상 0초부터 |
| transition | 값이 바뀌면 **현재 값에서** 새 값으로 간다(중간에 바뀌면 그 자리에서 다시 출발) |
| 연출 줄이기 | 정본 `prefers-reduced-motion`(L508)은 1차에 넣지 않는다(D-39) |

| CSS 곡선 | cubic-bezier |
|---|---|
| `ease` | (.25,.1,.25,1) |
| `ease-in` | (.42,0,1,1) |
| `ease-out` | (0,0,.58,1) |
| `ease-in-out` | (.42,0,.58,1) |
| 날아가는 아이콘 | (.5,0,.8,.6) (L144) |
| 가호 카드 날아가기 | (.55,0,.75,.4) (L563) |
| 해금 상자 등장 | (.2,1.6,.4,1) — y가 1을 넘는 튀어 오름(L482) |
| `steps(2)` | 구간을 2단계 계단으로(L141, §5.2.7) |

### 5.1.12 문구·숫자 표기

| 함수 | 규칙 | L |
|---|---|---|
| `fmt(n)` | `n = floor(n)`; 1,000,000 이상 `x.xM`; 10,000 이상 `x.xK`(`n/1000`); 그 밖 천 단위 쉼표(`1,234`) | L855 |
| `hms(ms)` | `s = ceil(ms/1000)` → `HH:MM:SS`(두 자리 0 채움) | L2540 |
| 소수 2자리 | `toFixed(2)` (예: 소득 배율 `×1.05`) | L825 |

- 소수 1자리 반올림은 JS `toFixed`와 같게 「0.5 올림」으로 맞추고 경계값(9,950 / 99,950 / 999,950)을 대조 시험한다 [추정].
- 문구(규약 규칙 7): 화면 고정 문구 `ui:ui.<화면>.<항목>`, 토스트 `toastMessage:<항목>`, 긴 안내 `msg:<항목>`, 표 행의 이름·설명은 `name:<행 id>`·`description:<행 id>`(별도 키 열 없음). 모든 화면 문구는 `LocalizationManager.GetLoc(컬렉션, 키)` 또는 `LocalizeTmpTextEvent`로 넣는다. 한국어 원문은 이 문서 표에 적은 그대로다. 아래 표에서 `ui:` 접두는 줄여 `ui.lobby.hint`처럼 쓴다.

### 5.1.13 만들지 않는 것과 정본 결함 처리

| 정본 | 상태 | 처리 | L |
|---|---|---|---|
| `#tierCards`, `#tierNext` | `display:none!important` | **만들지 않음** | L62~77 |
| `#tierUp` 등급 게이지 | `display:none!important` | 만들지 않음 | L120~133, L380~405 |
| `#lNote`, `.road .note` | `display:none!important` | 만들지 않음 | L229, L441 |
| `#unlockBtn` | 항상 `hidden`, 옛 코드 | 만들지 않음 | L2625, L2654 |
| `#roul` 가호 룰렛 상자 | `showCard` 호출 0건, 늘 숨김 | 만들지 않음(가호 연출은 `#hc` 가운데 카드) | L232~246, L676 |
| `#mulFx` 도장 글 | `stamp()` 호출 0건 | 만들지 않음 | L79~85, L2217 |
| `#hc` 머리 위 모드(`.ttl` 보임) | `updHeadUI`는 오르기 중엔 숨기고 룰렛 때는 가운데 모드만 씀 | 가운데 모드만 구현 | L2338~2355 |
| `.hrow2 .fan .wcard .mtag`, `flyToWorld` | 사용처 없음 | 만들지 않음 | L100~118, L1712 |
| `.ic-part .ore .ic-keg .ic-energy` | 사용처 없음 | 만들지 않음 | L57, L78, L94, L511 |
| `#noGL` | Unity에 해당 없음 | 만들지 않음 | L276, L735 |
| `#wpnCard.merge` | CSS 없음 → 효과 없음 | 정본대로(효과 없음) | L1220 |
| `glv` 배경 반짝임, `gp2` 테두리 흰색 | `!important`에 막혀 안 보임 | 정본대로: `glv`는 크기만, `gp2`는 빛·밝기만(D-39) | L92, L522, L584, L586 |
| 소득 버튼 `tick` | `.g`가 없어 안 보임 | 정본대로(D-39) | L225 |
| `#aupFree` 색 | `.row2 button`이 `.adBtn`을 이김 → 초록 | D-33 | L359, L363 |
| `#stairBox.fly`의 `.fly` 상속 | 날기 중 글꼴·그림자 바뀜 | 정본대로 재현(§5.2.4) | L74, L144 |
| 부활 버튼 「한 번 더」 문구 안 풀림 | `rbArm`이 시간값으로 남음 | 고침(D-36) | L2556, L2576 |
| `finish('area')` 결과 문구 | `R.hitWall`이 없어 오류 → 결과 화면이 안 뜸 [추정] | 고침(D-36), 규칙은 [04](04_GameRules.md) | L1827, L2415 |
| 토스트가 패널 뒤 | z 순서 | 고침(D-32) | L251, L177 |

### 5.1.14 UI 클래스 목록

Address는 Framework 규칙 `UIScreen_{ID}`·`UIPopup_{ID}`·`UIHud_{ID}`. ID 상수는 `JJEvol/UI/Generate UI ID Constants`로 만든다.

| Address | C# 클래스 | 층 | 여는 때 | 절 | 작업 계약 |
|---|---|---|---|---|---|
| `UIScreen_Lobby` | `LobbyScreen : UIScreen` | 100 | `toLobby` | §5.4 | MV-UI-LOBBY |
| `UIHud_Run` | `RunHud : UIHud` | 200·220 | `startRun` ~ 결과 | §5.2, §5.3 | MV-UI-HUD |
| `UIHud_FloatText` | `FloatTextHud : UIHud` | 210 | 런 중 | §5.2.10 | MV-UI-HUD |
| `UIHud_Overlay` | `OverlayHud : UIHud` | 280 | 항상(Game 씬) | §5.13 | MV-UI-HUD |
| `UIHud_System` | `SystemHud : UIHud` | 400 | 항상(Game 씬) | §5.14 | MV-UI-HUD |
| `UIPopup_Result` | `ResultPopup : UIPopup` | 300 | `finish` | §5.6 | MV-UI-META |
| `UIPopup_GodSelect` | `GodSelectPopup : UIPopup` | 500 | 야옹신 버튼 | §5.7 | MV-UI-META |
| `UIPopup_AreaUpgrade` | `AreaUpgradePopup : UIPopup` | 500 | 바구니 버튼 | §5.8 | MV-UI-META |
| `UIPopup_Rebirth` | `RebirthPopup : UIPopup` | 500 | 아홉 목숨 버튼 | §5.9 | MV-UI-META |
| `UIPopup_Map` | `MapPopup : UIPopup` | 550 | 지도 버튼 | §5.10 | MV-UI-META |
| `UIPopup_FeatureUnlock` | `FeatureUnlockPopup : UIPopup` | 600 | 신기능 해금 | §5.11 | MV-UI-META |
| `UIPopup_FakeAd` | `FakeAdPopup : UIPopup` | 700 | 광고 공급자 없음(개발 빌드) | §5.12 | MV-PLAT-ADS |
| `UIPopup_Debug` | `DebugPopup : UIPopup` | 410 | 테스트 버튼(개발 빌드) | §5.14 | MV-UI-HUD |
| `UIPopup_ContentMenu` | `ContentMenuPopup : UIPopup` | 500 | 컨텐츠 버튼 | §5.15 | MV-UI-CONTENT |
| `UIPopup_ModeResult` | `ModeResultPopup : UIPopup` | 300 | 모드 런 끝 | §5.17.1 | MV-UI-CONTENT |
| `UIHud_Defence` | `DefenceHud : UIHud` | 200 | 골목 수비전 런 | §5.17.2 | MV-UI-CONTENT |
| `UIPopup_CardPick` | `CardPickPopup : UIPopup` | 500 | 수비전 레벨업 3택 | §5.17.3 | MV-UI-CONTENT |
| (프리팹 원본, 주소 없음) `MetaFrame` | `MetaPanelPopup : UIPopup`(추상) | 500 | 메타 패널마다 변형 | §5.16 | MV-UI-CONTENT |
| `UIPopup_Quest`·`UIPopup_Attendance`·`UIPopup_Research`·`UIPopup_Shop`(1차), `UIPopup_Pet`·`UIPopup_Friend`·`UIPopup_Gear`·`UIPopup_Pass`·`UIPopup_Blessing`(2차) | `<이름>Popup : MetaPanelPopup` | 500 | 컨텐츠 메뉴 「성장」 탭 | §5.16 | MV-META-<시스템> |
| 토스트 프리팹 `Effect_Toast` | `effects.ui` `ToastMessageEffectRenderer` | 800 | `toast()` | §5.13 | MV-UI-HUD |

위젯(프리팹 부품, 주소 없음): `CurrencyChip`, `RewardSlot`, `PriceButton`, `SideButton`, `UpgradeButton`, `DeckWidget`, `StairBoxWidget`, `FuelGaugeWidget`, `StairGaugeWidget`(`#ampRow`), `WeaponWidget`, `HandWidget`, `BuffBar`, `WarnWidget`, `ClimbMeterWidget`(`#ms`), `BlessingCardsWidget`(`#hc`), `FlyIconLayer`, `ContentCard`, `MetaRowCell`, `MetaGridCell`, `MetaHeaderCell`, `DefenceBar`.

공통 수명: 영속 데이터를 보여 주는 화면은 `DataRepository.Changed`에 구독해 `SetDirty`(다음 프레임 한 번 다시 그림). 런 HUD는 매 프레임 `LateUpdate`에서 런 상태를 읽되 정본처럼 **값이 바뀐 항목만** 글자를 바꾼다(L2720 `ui.lastStairs` 같은 키 비교).

## 5.2 HUD — `UIHud_Run`

`RunHud : UIHud`. 정본 `#hud.ov`(L611~635). 런 시작(`startRun`, L2659)에 보이고, 결과(`finish`, L2417)와 로비(`toLobby`, L2473)에서 숨긴다. HUD 전체는 누를 수 없다(`raycastTarget=false`): 드래그 입력이 아래로 지나간다. 작업 계약 MV-UI-HUD.

### 5.2.1 배치 개요 (1080×1920)

| 블록 | 위치 | 크기 | 절 |
|---|---|---|---|
| 왼쪽 위 Level·골목 | left 3u(32.4), top 3u(32.4) | 내용 | 5.2.2 |
| 오른쪽 위 재화 칩 3개 | right 3u, top 3u | 칩 세로 3개 | 5.2.2 |
| 계단 상자 `#stairBox` | 가운데, top 11u(118.8) | 42u(453.6) × 내용 | 5.2.3~5.2.4 |
| 계단 게이지 `#ampRow` | left 2u(21.6), top 27%(48u, 518.4) | 13.5u(145.8) × 내용 | 5.2.5 |
| 무기 `#wpn` | right 2u(21.6), top 30%(53.33u, 576) | 25u(270) × 내용 | 5.2.6, §5.3 |
| 가호 줄 `#buffs` | left 3u(32.4), bottom 3.4u(36.72) | 최대 너비 62u(669.6) | 5.2.7 |
| 경고 `#warn` | 가운데, top 47%(83.56u, 902.4) | 내용 | 5.2.7 |
| 등급 미터 `#ms`(오르기 중) | 중심 (50%, 44%) = (540, 844.8), 전체 ×1.1 | 내용 | 5.2.8 |
| 가호 카드 `#hc`(룰렛 중) | 중심 (50%, 46%) = (540, 883.2) | 카드 25u×33u × 장수 | 5.2.9 |
| 비네트 | 전체 | — | 층 10, 로비에서도 보임 |

### 5.2.2 왼쪽 위·재화 칩

| 요소 | 위치·크기 | 스타일 | 글꼴·문구 | 데이터 | 표시 | L |
|---|---|---|---|---|---|---|
| 블록 `.tl` | left 3u top 3u, 세로, 사이 1u(10.8) | 글 그림자 `sh`(자식 모두 상속) | — | — | 런 | L49, L612 |
| Level | — | `--ink` | D 4.6u(49.68) `ui.hud.level` 「Level {0}」 | `R.level` | 항상 | L50, L282, L2714 |
| 골목 칩 `#hFloor` | 왼쪽 정렬, 최대 너비 22u(237.6), 여백 .6u 2u | Panel 바탕, 테두리 3px `--panel-edge`, 반지름 2u, 윗줄 하이라이트(그림자 없음) | B8 2.5u(27) 줄높이 1.25, 두 줄. `ui.hud.floor` 「{0} · Area {1}」 + 줄바꿈 + `ui.hud.floorN` 「골목 {0}」 또는 `ui.hud.floorPlay` 「플레이 구간」 | `name:<302_area id>`, `save.area+1`, `R.floor`(0이면 플레이 구간) | 항상 | L51, L283, L2714 |
| 칩 묶음 `.tr` | right 3u top 3u, 세로, 오른쪽 정렬, 사이 1.2u | — | — | — | 항상 | L37, L52 |
| 츄르 칩 | `CurrencyChip`(§5.1.8) | — | D* 3.5u `fmt` | `01_wealth` 1, 값 `R.gold` | 항상 | L614, L2715 |
| 생선 칩 | 같음 | — | D* 「{0}/{1}」(fmt 아님) | `01_wealth` 3, `save.souls + R.souls`, `302_area.needSouls`(현재 Area) | 항상 | L615, L2715 |
| 캣닢 칩 | 같음 | — | D* 숫자 그대로 | `01_wealth` 2, `R.dia` | 항상 | L616, L2715 |

### 5.2.3 계단 상자 `#stairBox`

| 요소 | 위치·크기 | 스타일 | 글꼴·문구 | 데이터 | L |
|---|---|---|---|---|---|
| 상자 | 가운데, top 11u(118.8), 너비 42u(453.6), 여백 1.2u 2.2u, 세로, 사이 1u | 바탕 세로 `rgba(34,28,44,.82)→rgba(16,13,22,.78)`, 테두리 3px `rgba(255,210,122,.32)`, 반지름 3u, 하이라이트+그림자 | — | — | L53~54, L284, L539 |
| 윗줄 `.sb-top` | 가로 가운데, 사이 1.6u | — | — | — | L55 |
| 캔 아이콘 | 5.6u(60.48) 정사각 | `09_uiResource hud.stairCan` | — | — | L589, L619 |
| 숫자 `#hStair` | — | `#ffd27a`, 줄높이 1 | D* 8.4u(90.72) | 날기 중 `max(0, CN.n − CN.shown)`(남은 칸) / 장전 중 `CN.hudLeft ?? CN.n` / 평소 `R.stairs` | L58, L285, L2718 |
| 단위 `#hUnit` | — | `--ink-dim` | B8 3.4u `ui.hud.stairUnit` 「계단」(늘 같음) | — | L59, L2720 |
| 연료 게이지 | 상자 안 아래 | §5.2.4 | — | — | L620 |

| 상태 | 조건 | 모양 | L |
|---|---|---|---|
| `pulse` | 계단 게이트 보상이 날아와 도착(flyTo 콜백) | 0% `scale 1.15`·테두리 `#b8ffcb` → 100% `scale 1`·원래 테두리, .5s ease-out, 기준점 가운데 | L60~61, L2047 |
| `fly` | 계단 오르기 날기 단계 | 테두리 `#ffd27a`, 빛 `0 0 4u rgba(255,210,122,.5)`. **정본대로 재현**: 일반 `.fly` 규칙도 함께 걸려, 상자 안에서 글꼴을 따로 정하지 않은 글(`#hUnit`, 연료 게이지 글)이 D 글꼴이 되고 모든 글에 프리셋 `pop` 그림자가 붙는다 | L74, L144, L2722 |

### 5.2.4 연료 게이지 `#fuelG` (오르기 날기 중만)

`FuelGaugeWidget`. 날기 상태에만 보이므로 위 `fly` 상속이 늘 적용된다 → 이 게이지의 글은 모두 D 글꼴. 구간·색·문구 계산은 [04](04_GameRules.md) 계단 오르기와 정본 `updFuelGauge`(L2697~2710).

| 요소 | 크기 | 스타일 | 글꼴·문구 | L |
|---|---|---|---|---|
| 묶음 | 위 여백 .8u | — | — | L524 |
| 머리줄 `.fg-h` | 가운데, 사이 1u, 아래 여백 .5u | `#ffd9b8` | D 2.1u | L525, L588 |
| 다음 목표 `#fgNext` | 줄바꿈 없음 | 색은 아래 규칙 | D* 2.8u(굵기 900 → 합성 볼드) | L526 |
| 랩 표시줄 `#fgTop` | 높이 2.8u | 라벨: 아래 붙음, 가운데 기준, 여백 0 .6u, 바탕 `#f2b33d`, 테두리 4.5px `#fff`, 반지름 .6u | D* 1.9u `#2a1608` `ui.hud.fuelLap` 「카드{0} 레전더리」 | L534~535, L2702 |
| 막대 `.fg-bar` | 높이 3.8u(41.04), 반지름 1.1u, 넘침 자름 | 바탕 `#14121c`, 안쪽 테두리 6px `rgba(0,0,0,.55)` | — | L527 |
| 구간 `#fgSeg i` | 오른쪽이 0, 왼쪽·너비 % | 색 = 등급색 또는 `#4a4658`, 안쪽 위 1u `rgba(255,255,255,.28)`, 오른쪽 4.5px `rgba(0,0,0,.45)` | — | L528 |
| 랩 선 `.lap` | 너비 0, 왼쪽 9px `#fff`, 빛 18px `#fff` | — | — | L529 |
| 소모 가림 `#fgMask` | 오른쪽 기준, 너비 = `shown/n` | 135° 사선 무늬 `rgba(8,6,14,.9)`/`rgba(24,20,36,.9)` 각 18px → 타일 스프라이트 `UI_Skin_HatchTile`(`09_uiResource hud.hatch`) | — | L530 |
| 경계선 `#fgEdge` | 너비 9px, left `(100 − used)% − 3px` | `#fff`, 빛 30px·12px `#fff` | — | L531 |
| 아래 라벨 `#fgLb` | 높이 3.2u, 구간 가운데, top .4u | 색 = 구간 등급색, 그림자 `shadow.1` | D* 2.2u, 등급 머리글자(C/U/R/E/L), 구간 너비 ≥ 6%일 때만 | L532~533 |

| `#fgNext` 조건 | 문구 | 색 |
|---|---|---|
| 상한 또는 소진, 등급 있음 | `ui.hud.fuelGot` 「{0} 확보」 | 그 등급색 |
| 상한 또는 소진, 등급 없음 | `ui.hud.fuelNone` 「등급 없음」 | `#aaa` |
| 남은 칸으로 닿음 | `ui.hud.fuelNeed` 「{0}까지 {1} 소모」 | 목표 등급색 |
| 모자람 | `ui.hud.fuelShort` 「{0}까지 {1} 부족」 | `#9a96a2` |

등급 이름은 `name:<115_cardTier id>`. 다시 그리기는 `CN.n|CN.o.z`가 바뀔 때만(L2699).

### 5.2.5 계단 게이지 `#ampRow`

`StairGaugeWidget`. 벽돌 우물에 맞혀 모은 벽돌로 차는 세로 게이지(규칙 [04](04_GameRules.md)).

| 요소 | 위치·크기 | 스타일 | 글꼴·문구 | 데이터 | L |
|---|---|---|---|---|---|
| 상자 | left 2u, top 27%(518.4), 너비 13.5u(145.8), 여백 1.4u 1u 1.6u, 세로 가운데, 사이 .9u | 바탕 세로 `rgba(78,48,44,.92)→rgba(40,24,26,.88)`, 테두리 4.5px `rgba(240,160,110,.7)`, 반지름 2.2u, 하이라이트+그림자 | 기본 B8 2.6u | — | L512~523, L584~588 |
| 아이콘 `#gIcon` | 8.4u(90.72) | `09_uiResource hud.gaugeIcon` | — | — | L514, L2661 |
| 이름 `.ag-l` | 세로 가운데, 줄높이 1.15 | `#ffd9b8` | B8 2.3u `ui.hud.gaugeName` 「계단」 + 아래 줄 D 5u `#fff` 외곽선 `stroke.lv` `ui.hud.gaugeLv` 「Lv {0}」 | `R.gauge.lv` | L515~516, L624 |
| 막대 `#gBar` | 4.4u×24u(47.52×259.2) | 바탕 `rgba(4,6,18,.85)`, 테두리 4.5px `rgba(240,160,110,.6)`, 반지름 2.2u, 넘침 자름 | — | — | L517, L586 |
| 채움 `#gFill` | 아래에서 위로, 높이 % | 위로 `#a8452a → #e88a5a(60%) → #ffd8b0`, 빛 2u `#e88a5a`, 윗면 1u `#fff` 불투명 .85 흐림 | — | `lv ≥ 8`이면 100%, 아니면 `fill / gaugeNeed(lv)` | L518~519, L587, L2724 |
| 칸당 `.ag-p` | — | `#ffd9b8` | D 2.1u `ui.hud.gaugePer` 「칸당 」 + D 3.4u `#ffe08a` 「+{0}」 | `R.gauge.lv` | L520~521, L624 |

| 상태 | 모양 | L |
|---|---|---|
| 채움 변화 | 높이 .25s ease-out | L518 |
| `gpulse`(벽돌 묶음 도착) | 막대 0% 빛 4u `#5ad8ff`·밝기 1.9 → 100% 없음, .45s ease-out. 흰 테두리는 `!important`에 막혀 안 보임 → 넣지 않음(D-39) | L522~523, L1874 |
| `glvup`(게이지 Lv 업) | 상자 0% `scale 1.08` → 100% 없음, .9s ease-out. 배경 반짝임은 막혀 안 보임 → 넣지 않음(D-39) | L92~93, L1891 |
| `calm`(계단 오르기 중, 정본 mode `cannon`) | 불투명 0으로 .2s. 끝나면 **바로** 1 | L138, L2735 |

### 5.2.6 무기 카드 `#wpn`

`WeaponWidget`. 세로 묶음: 장착 카드 → 손패(§5.3) → 안내 글.

| 요소 | 위치·크기 | 스타일 | 글꼴·문구 | 데이터 | L |
|---|---|---|---|---|---|
| 묶음 | right 2u, top 30%(576), 너비 25u(270), 세로, 사이 1.2u | — | — | — | L566, L575 |
| 장착 카드 `#wpnCard` | 오른쪽 붙음, 19u(205.2) 정사각, 여백 .8u | 테두리 .6u(6.48) `--gc`, 바탕 세로 `mix(--gc 45%, #120e1c) → #120e1c`, 반지름 2.4u, 하이라이트+그림자 | — | `--gc = 103_item[손패 표시 최고 등급].gradeCol` | L576, L506, L1218 |
| 아이콘 `#wIcon` | 10.5u(113.4) | `103_item.icon` | — | 같음 | L577, L1219 |
| 이름 `#wName` | 가운데, 줄높이 1.15 | `#fff` | D 2.6u `name:<103_item id>` | 같음 | L578, L507 |
| 피해 `#wDmg` | — | `--ink-dim` | B7 2u `ui.hud.wDmg` 「피해 {0} · {1}」 | {0} = `fmt(103_item.dmg × (1 + 0.2 × 공격력 카드 장수))`, {1} = 고양이 모습 짧은 이름(정본 `LOOKS[t-1]`의 ` · ` 앞 → `103_item.lookName` 열 필요, [07](07_Tables.md)) | L579, L2725 |
| 안내 `.hint3` | 오른쪽 정렬 | `--ink-dim`, 불투명 .8, 그림자 `sh` | B8 2.1u `ui.hud.mergeHint` 「같은 무기 3장 → 머지」 | — | L580 |

등급이 오르면 아이콘·이름만 바꾼다. 장착 카드 자체 연출은 없다(`.merge` 클래스에 CSS 없음). 「변신!」 팝업·불꽃·`lvup` 소리는 [06](06_Effects.md)(L1221).

### 5.2.7 가호 줄·경고

| 요소 | 위치·크기 | 스타일 | 글꼴·문구 | 데이터 | 표시 | L |
|---|---|---|---|---|---|---|
| 가호 줄 `#buffs` | left 3u, bottom 3.4u, 최대 너비 62u, 사이 1u, 줄바꿈 **아래에서 위로**(한 줄 최대 6개) | — | — | — | 런 | L134, L300 |
| 가호 칸 `.bf` | 8.4u(90.72) 정사각 | 반지름 2u, Panel 바탕, 테두리 4.5px `--c`(등급색), 그림자 `--panel-sh` | — | 보유 카드(정본 `CARDS` 순서: 공용 → 신 고유), `110_card.icon`, `--c = 115_cardTier.color` | 장수 > 0 | L135~136, L2728~2733 |
| 장수 배지 | 오른쪽 아래 −.8u, 여백 0 1u | 바탕 `--c`, 글 `#0b0b0e`, 반지름 1.4u | D 2.6u 「×{0}」 | 장수 | 장수 > 1 | L137, L302 |
| 경고 `#warn` | 가운데, top 47%(902.4), 여백 1.2u 4u, 줄바꿈 없음 | 바탕 `rgba(90,16,16,.55)`, 테두리 3px `rgba(255,120,100,.45)`, 반지름 2.4u, 그림자 `sh` | D 4.6u `#fff` `ui.hud.warn` 「전방 장애물!」 + 줄바꿈 B8 2.6u 불투명 .9 `ui.hud.warnSub` 「닿으면 런 종료 · 한 줄만 뚫으면 관문 통과」 | 앞쪽 9m 안 장애물(조건 [04](04_GameRules.md)) | 런 모드 | L139~141, L303~304, L2736~2741 |

- 가호 칸 `arrive`: 0% `scale 1.6`·밝기 3·빛 5u `--c` → 100% 없음, .5s ease-out. 가호 카드가 날아와 도착한 칸만(L564~565, L2730).
- 경고 깜빡임 `blink .5s steps(2) infinite`(L141): 키프레임 50%에만 `color: --danger`가 있고 구간마다 2단계라서 0.125초씩 **`#fff` → `#ffada5` → `#ff5a4a` → `#ffada5`** 를 되풀이한다(가운데 색 = 두 색의 sRGB 중간). 작은 줄도 같은 색. 처음 나타날 때 소리 `03_sound` `warn`.

### 5.2.8 등급 미터 `#ms` (계단 오르기 중)

`ClimbMeterWidget`. 층 220. 계단 오르기(`CN` 있음) 동안만 보인다. 결과 화면에서는 숨김(L2339). 계산식은 정본 `updHeadUI`(L2343~2351)와 [04](04_GameRules.md).

| 요소 | 위치·크기 | 스타일 | 글꼴·문구 | 데이터 | L |
|---|---|---|---|---|---|
| 묶음 | 중심 (50%, 44%), 전체 `scale 1.1`, 세로 가운데, 사이 1u | — | — | — | L426, L540, L601 |
| 카드 번호 `#msN` | 카드 기준 왼쪽 −2.6u, 위 −2.2u, 맨 앞. 최소 너비 11u(118.8), 높이 5u(54) | 바탕 `#2f8ef0`, 테두리 6px `#fff`, 반지름 1.2u, `skewX(-8°)`(`MvSkew`) | D 3u `#fff` `ui.hud.msCard` 「카드 {0}」 | `CN.lap + 1` | L427~428, L541, L2346 |
| 카드 `.ms-card` | 너비 25u(270), 여백 1u, 사이 .6u, 반지름 2u | `--tc` = 현재 등급색(없으면 `#6a6676`). 바탕 세로 `mix(--tc 55%, #1a2a5a) → #14204a`, 테두리 .6u `--tc`, 그림자 아래 .8u `#164a8a` + 빛 6u `--tc`(알파 ×.7). 바탕·테두리 변화 .25s | — | `CN.tier` | L429, L542, L602 |
| 등급명 `#msT` | 너비 100%, 위 모서리만 1u | 바탕 `--tc` | D 4u `#fff` `stroke.msT`, `name:<115_cardTier id>` 또는 `ui.hud.msNoTier` 「등급 없음」 | 같음 | L430, L543 |
| 초상 `#msG` | 21u×23u(226.8×248.4) | 반지름 .8u, 테두리 .6u `--tc`, 바탕 `#1a2a44` | — | `113_god.portrait`(출전 신) | L431, L603 |
| 다음 목표 `.ms-c` | 아래 여백 −2.6u(아래로 걸침), 여백 .2u 3u | 바탕 `mix(--nc 70%, #1a1a24)`, 테두리 6px `#fff`, 반지름 1.2u. 캔 아이콘 숨김 | D 3u `#fff` `stroke.msC` `ui.hud.msNext` 「{0}까지 {1}」 또는 `ui.hud.msMax` 「최고 등급」 | `--nc` = 다음 등급색(상한이면 LEGENDARY 색) | L432~434, L544~546 |
| 막대 `.ms-bar` | 위 여백 3.2u, 26u×2.6u(280.8×28.08), 반지름 27px | 바탕 `rgba(40,40,52,.8)`, 테두리 4.5px `rgba(0,0,0,.4)` | — | — | L435, L547 |
| 채움 `#msBar` | 너비 % | **단색 = 다음 등급색**(정본 인라인 색이 CSS 그라데이션을 덮음). 너비 .15s, 색 .2s | — | `(rem − prev)/(need − prev)`, 상한이면 100% | L436, L548, L2350 |
| 확정 줄 `#msGot` | 최소 높이 4u, 사이 .8u | `#fff`, 그림자 `shadow.1` | B8 2.4u `ui.hud.msGot` 「확정」 + 확정 카드마다 칸 3.6u×4.6u, 반지름 .6u, 바탕 등급색, 테두리 4.5px `#fff`, 빛 24px 등급색, D 2.4u `#2a1608` 머리글자 | `CN.cards` | L549~550, L2351 |

`bump`(키 `CN.lap×6 + tier + 1`이 바뀔 때): 카드 `msup` .5s ease-out — 0% `scale 1.25 rotateY(70°)`·밝기 2.6 → 55% `scale .96` → 100% 없음(L551~552). `rotateY`는 가로 크기 `cos(각도)`로 흉내 낸다.

### 5.2.9 가호 카드 `#hc` (오르기 끝 룰렛)

`BlessingCardsWidget`. 층 220. 계단 오르기가 끝나고 확정 카드가 1장 이상이면 등급 수만큼 카드를 띄워 돌린다(정본 `startRoulette`·`updRoulette`, L2357~2374). 없으면 토스트 `toastMessage:rouletteNone` 「우다다 {0}칸 · 5칸 미만이라 가호 없음」(L2359).

| 요소 | 위치·크기 | 스타일 | 글꼴·문구 | L |
|---|---|---|---|---|
| 줄 `#hcRow` | 중심 (540, 883.2), 가로, 아래 맞춤, 사이 3u | — | — | L407, L553, L2354 |
| 카드 `.hcard` | 25u×33u(270×356.4), 반지름 2.4u | 바탕 원형(중심 50% 40%) `mix(--tc 60%, #fff) → --tc(55%) → mix(--tc 55%, #000)` 위에 45° 사선 `rgba(255,255,255,.12)` 18px / 투명 18px(카드 틀 스킨 `UI_Skin_HCardCenter_T{0..4}`에 구워 넣음, [03](03_ResourcePipeline.md)). 테두리 .6u `mix(--tc 60%, #000)`. 그림자 아래 .8u `mix(--tc 45%, #000)` + 빛 4u `--tc`(알파 ×.6) | — | L411~413, L555~556 |
| 등급명 `.ht` | 위 .8u, 가운데 | `#fff` `stroke.ht` | D 3.4u `name:<115_cardTier id>` | L414, L557 |
| 그림 | 17u(183.6), 위 여백 3u | 바탕 `rgba(10,8,24,.55)`, 반지름 1.6u, 안쪽 테두리 6px `rgba(255,255,255,.5)` | `110_card.icon` | L416, L558 |
| 이름 `.hn` | 위 여백 1u, 여백 0 .6u, 가운데, 줄높이 1.1 | `#fff` `stroke.hn` | B8 3u `name:<110_card id>` | L417, L559 |
| 「?」 `.hq` | — | — | 룰렛은 늘 앞면이라 **만들지 않음** | L415, L560 |

| 시각(카드 i, 0부터) | 일 | L |
|---|---|---|
| 0 | 카드 수 확정, 모두 앞면(그 등급 카드 풀 무작위) | L2361~2362 |
| 매 넘김 | 다음 카드 그림. 간격 `0.06 + (남은 < 0.4 ? (0.4 − 남은)×0.35 : 0)`초. 첫 카드만 소리 `tick` | L2368 |
| `1.0 + 0.45i` | 확정: `chosen` .8s ease-out 끝 유지(0% `scale 1.35` 밝기 3 → 25% `1.08` 밝기 1.6 → 45% 밝기 2.2 → 65% 밝기 1.3 → 100% `scale 1.04`, 빛 8u `--tc` + 3u `#fff`), 별 14개, 소리 `card`. 월드 효과·팝업 글은 [06](06_Effects.md) | L2369~2372, L561~562 |
| 확정 + 1.5 | 카드 날아가기 | L2367 |
| 모두 도착 + 0.3 | 끝, 줄 비움 | L2373, L2391 |

별 `.star`(L424~425, L2332~2333): ★ 스프라이트 3u `#fff`, 빛 18px `#fff` + 36px `--tc`. 카드 가운데에서 각도 a 무작위, 거리 r = 14 + 무작위×8 u, `dx = cos a × r`, `dy = sin a × r × 0.7`. 0% `scale .4` 불투명 1 → 100% (dx, dy) `scale 1.1 rotate 160°` 불투명 0, .7s ease-out, 0.75초 뒤 제거.

카드 날아가기 `flycard`(L2375~2390, L563): 카드를 복제해 같은 자리에 두고 원본은 숨긴다. 목적지 = 같은 카드 가호 칸이 있으면 그 칸 중심, 없으면 마지막 칸 오른쪽(칸 너비 12% 띄움), 칸이 없으면 가호 줄 왼쪽 아래 8.4u 칸. 이동+크기(목적지 칸 너비/카드 너비) .6s cubic-bezier(.55,0,.75,.4), 불투명 → .85. 620ms 뒤 복제 제거 → 카드 적용, 소리 `buy`, 가호 칸 `arrive`.

### 5.2.10 날아가는 아이콘·떠오르는 숫자

**날아가는 아이콘** `FlyIconLayer`(층 280, `effects.ui` `EffectIconBehaviour` 기반, 풀). 정본 `.fly`(L144~145): 글 D 6u `--stair`, 그림자 `pop`. 시작 2프레임 뒤 이동, 560ms 뒤 제거 + 콜백.

| 종류 | 시작 | 끝 | 시간·곡선 | L |
|---|---|---|---|---|
| `flyTo` 월드→HUD | 월드 점의 화면 위치, `scale 1.3` | 목적지 요소 중심, `scale .6`, 불투명 .4 | 위치·크기 .55s cubic-bezier(.5,0,.8,.6), 불투명 .55s ease | L1735~1741 |
| `flyTo2` 로비 버튼→덱 | 버튼 가로 중심, 위에서 높이×.35, `scale 1.2` | 덱 목록 가로 중심, 위에서 `min(높이, 90)`, `scale .7`, 불투명 유지 | 같음 | L1719~1725 |

| flyTo 상황 | 목적지 | 내용 | 도착 콜백 | L |
|---|---|---|---|---|
| 우물 벽돌 | 계단 게이지 막대 | 벽돌 4.4u×2.6u(`UI_Icon_Brick`) | 마지막 조각: 게이지 추가 + `gpulse` | L1873, L1883 |
| 적 처치·벽·츄르 더미 | 츄르 칩 숫자 | `01_wealth[1].icon` 4~4.4u | — | L1926, L1942, L1967 |
| 벽 생선 | 생선 칩 숫자 | `01_wealth[3].icon` 4u×5u | — | L1938 |
| 벽·화분 캣닢 | 캣닢 칩 숫자 | `01_wealth[2].icon` 4.4~5u | — | L1940, L1965 |
| 계단 게이트 | `#hStair` | 캔 4.4u + 「+{0}」 | `R.stairs += n`, 계단 상자 `pulse` | L2047 |
| 무기 공방 게이트 | 손패 | `103_item.icon`, 최대 5개 70ms 간격 | — | L2052 |

**떠오르는 숫자** `UIHud_FloatText`(`FloatTextHud`, 층 210, 정본 `#pops` L142~143, L1700~1711). 호출 목록·색·크기는 [06](06_Effects.md).

| 항목 | 값 |
|---|---|
| 풀 | TMP 라벨 61개(넘치면 가장 오래된 것 재사용) |
| 글 | D, 크기 `4.2 × size` u, 색 인자, 그림자 `pop`, 줄바꿈 없음, 가운데 기준 |
| 위치 | 매 프레임 월드 점(y 초당 +1.4m)을 화면 좌표로(`MV.View.WorldToStage`). 카메라 뒤면 숨김 |
| 수명 | 0.8초, 실시간(히트스톱 영향 없음) |
| 크기 | 진행 k < 0.15면 `1 + (0.15 − k)×3`, 이후 1 |
| 불투명 | `1 − max(0, (k − 0.6)/0.4)` |

## 5.3 HAND 카드 연출 — `HandWidget`

정본 HAND(L1156~1222): 런 중 받은 무기 카드를 장착 카드 아래 세로로 쌓고, 같은 등급 3장이 모이면 다음 등급 1장으로 합치는 모습을 **시간차로 보여 주는 표시 전용** 장치다. 실제 무기 수는 런 규칙이 즉시 합친다(L1848~1849).

**소유.** 표시 상태는 `HandDisplayModel`(MV.View, 순수 C#)에 두고 `HandWidget`은 그리기만 한다. 고양이 겉모습과 `#wDmg`가 **표시 등급**(`handTier`)을 따르므로(L3798, L2725) 월드 렌더도 같은 모델을 읽는다. 입력 = 런 이벤트 `WeaponDelivered(n, tier)`(정본 `handDeliver`, L2057), 런 시작 `handReset`(L1159).

### 5.3.1 배치

| 값 | 공식(정본) | 1080 기준 |
|---|---|---|
| 상자 너비 bw | 25u | 270 |
| 카드 한 변 ch | `min(bw, W×0.105)` | 113.4 |
| 간격 gap | `max(3px, bw×0.04)` | 10.8 |
| 최대 높이 | `H × 0.34` | 652.8 |
| 줄 간격 step | 1장이면 0, 아니면 `min(ch + gap, (최대 − ch)/(n − 1))` | 5장까지 124.2, 6장부터 겹침 |
| 상자 높이 | `ch + step×(n − 1)`(0장이면 0), 변화 .25s | — |
| 카드 i 위치 | left `bw − ch`(156.6, 오른쪽 붙음), top `i × step` | — |
| 순서 | 높은 등급이 위, 같은 등급은 먼저 받은 것이 위(L1158) | — |
| 겹침 | i가 클수록 앞 | — |

카드 `.wcard2`(L494~505, L567~572): ch 정사각, 테두리 .45u(4.86) `--gc`(`103_item.gradeCol`), 반지름 1.2u, 바탕 원형(50% 45%) `mix(--gc 55%, #1a1a2e) → #120e1c`, 그림자 아래 6px `rgba(0,0,0,.5)` + 안쪽 윗줄 3px `rgba(255,255,255,.3)`. 그림 92%(`103_item.icon`) + 그림자 아래 3px `rgba(0,0,0,.7)`.

### 5.3.2 시간 규칙 (실시간)

정본 값을 `300_runConfig`(키 묶음 `ui.hand`, [07 §7.3.3](07_Tables.md) 그룹 106)로 둔다. 머지 장수 3은 그룹 104 `MERGE_N`.

| 키(제안) | 값 | 뜻 | L |
|---|---|---|---|
| `ui.hand.budget` | 3 | 한 묶음(전달+머지)을 이 초 안에 끝냄 | L1162 |
| `ui.hand.leftMin` | 0.4 | 남은 예산 하한 | L1171 |
| `ui.hand.dealCost` | 0.12 | 1장 전달 = 0.12단위 | L1172 |
| `ui.hand.unitMin`·`unitMax` | 0.07·0.54 | 1단위 시간 범위 | L1173 |
| `ui.hand.dealGapMax` | 0.05 | 전달 간격 = `min(0.05, u×0.12)` | L1180 |
| `ui.hand.dealGapMul` | 0.12 | 위 식의 `u×0.12` 계수([07 §7.3.3](07_Tables.md) 그룹 106) | L1180 |
| `ui.hand.mergeMul` | 0.92 | 머지 시간 md = `u × 0.92` | L1188 |
| `ui.hand.moveMul` | 0.9 | 모으는 이동 = `md × 0.9` | L1189 |
| `ui.hand.afterMax`·`afterMul` | 0.04·0.08 | 머지 뒤 쉼 = `min(0.04, u×0.08)` | L1194 |

`u = clamp(left / U, 0.07, 0.54)`, `left = max(0.4, 3 − (지금 − t0))`, `U = 남은 머지 수 + 남은 전달 수 × 0.12`(0이면 0.54). 남은 머지 수는 손패+대기열로 「3장 → 다음 등급, 최고 8」을 끝까지 계산(L1163~1169). 완전히 쉬는 동안 `t0` = 지금.

### 5.3.3 단계와 연출

| 단계 | 일·연출 | L |
|---|---|---|
| 전달 | 대기열 1장을 손패에 넣고 정렬. 새 카드는 제자리보다 ch 아래에서 나타나 위치 .28s ease + `wdeal` .25s ease-out(불투명 0·오른쪽 35% → 원래) | L1180, L1211~1213, L501 |
| 머지 찾기 | 3장 이상인 **가장 낮은 등급**. 없으면 끝 | L1183~1186 |
| 모으기 | 그 등급 앞 3장이 2번째 카드 자리로 이동(`md×0.9`, ease-in) + `wmg`(md, ease-in, 끝 유지: 60% 밝기 1.7 → 100% 밝기 3·`scale 1.06`). 층 220 안 맨 앞 | L1187~1191, L502~503, L572 |
| 결과 | md 뒤 3장 제거, 다음 등급 1장을 모인 자리에 넣고 정렬 → 제자리로 .28s. `wmerged`(`max(0.15, u)`, ease-out: 0% `scale 1.3`·밝기 3·빛 4u `--gc` → 없음). 소리 `merge` | L1192~1194, L1214~1221, L504~505 |
| 연쇄 | 머지할 것이 없을 때까지 하나씩 반복 | L1195 |
| 등급 상승 | 최고 등급이 오르면 장착 카드 갱신, 런 중이면 「변신!」 팝업·불꽃·`lvup`([06](06_Effects.md)) | L1218~1221 |

예시 [계산]: 빈 손패에 1등급 9장 → 전달 9×0.05 = 0.45초 + 머지 4×(0.497+0.04) ≈ 2.15초 → 약 2.6초.

## 5.4 로비 — `UIScreen_Lobby`

`LobbyScreen : UIScreen`. 정본 `#lobby.ov`(L637~661), `toLobby`(L2472). 배경은 월드 카메라의 아지트·레벨 미리보기([02](02_Visual.md))와 비네트. 작업 계약 MV-UI-LOBBY.

### 5.4.1 출발 입력

정본은 캔버스를 누르면 바로 런이 시작되고 같은 손가락으로 드래그가 이어진다(L3843). Space·Enter도 시작(L3850).

| 항목 | 구현 |
|---|---|
| 출발 영역 | 로비 화면 맨 아래 투명 전체 Graphic `StartArea`(raycast 받음). 누르면 `LobbyScreen.OnStartPressed` → 런 시작 |
| 드래그 이어받기 | 런 입력 어댑터는 Input System 포인터를 직접 읽고 시작 포인터의 처음 x를 기준으로 삼는다(정본 `drag.sx`). UI 버튼 위에서 시작한 포인터는 무시 |
| 키보드 | 선택된 UI가 없을 때 Space·Enter |
| 나머지 글·패널 | `raycastTarget = false`(덱 포함, L203) |

### 5.4.2 요소

| 요소 | 위치·크기 | 스타일 | 글꼴·문구 | 데이터 | 동작 | L |
|---|---|---|---|---|---|---|
| 윗줄 `.ltop` | 위·좌·우 0, 여백 3u, 양 끝 정렬 | — | — | — | — | L149 |
| 제목 묶음 `.ttl` | (3u, 3u), 세로 사이 1u | 그림자 `sh` | — | — | — | L150 |
| Level | — | `--ink`, 줄높이 1 | D 6.6u(71.28) `ui.lobby.level` 「Level {0}」 | `save.level` | — | L151, L307 |
| 구역 상자 `.zone` | 여백 1.2u 2.4u | Panel 바탕, 반지름 1.6u, 테두리 3px, 하이라이트+그림자 | — | — | — | L152, L308 |
| 구역 줄 `#lZoneNo` | — | `--ink-dim`, 자간 .06em | B7 2.8u `ui.lobby.zoneNo` 「{0} · Zone {1} · Area {2}」 | `name:<301_zone id>`, 구역 번호, `save.area+1` | — | L153, L2621 |
| Area 이름 `#lZone` | — | `--gold` | D 4.6u `name:<302_area id>` | 현재 Area | — | L154 |
| 재화 칩 | (right 3u, top 3u) | `CurrencyChip` ×3 | D* 3.5u `fmt` | `01_wealth` 1·3·2 = `save.gold`·`save.souls`·`save.dia` | — | L643~647 |
| 지도 버튼 `#mapBtn` | 가운데, top 2.2u(23.76), 17u(183.6) 정사각, 여백 .8u, 사이 .2u | `SideButton` `--sc #ffd27a`, 테두리 6px, 반지름 2.6u | 아이콘 8u `lobby.side.map`, D 3.4u `ui.lobby.map` 「지도」, 2.2u `ui.lobby.mapSub` 「Area {0}」 | `save.area+1` | 소리 `click` → §5.10 | L339~343, L439~440, L2610 |
| 진행 상자 `.road` | top 24.5u(264.6), left 23u(248.4), right 3u → 너비 74u(799.2), 여백 1.1u 1.8u, 사이 .6u | Panel 바탕, 반지름 2u, 테두리, 하이라이트+그림자 | — | — | — | L155, L309, L443~444 |
| 진행 글 `.rlab` | 양 끝, 줄바꿈 없음 | — | B8 2.3u 왼쪽 `ui.lobby.roadNext` 「Area {0} 개방: Area 포털 도달 + 누적 생선」 또는 `ui.lobby.roadZoneClear` 「Zone {0} 클리어 · Zone {1} 업데이트 예정」, 오른쪽 D* 「{0} / {1}」 | `save.area+2`; 구역 끝 = `save.zoneClear` && 공개 마지막 Area; `fmt(save.souls)`, `302_area.needSouls` | — | L156, L442, L2623~2624 |
| 진행 막대 | 높이 1.7u(18.36), 반지름 2u | 바탕 `rgba(255,255,255,.12)`, 채움 가로 `#1d6f69 → --soul` | — | `min(souls, need)/need` | 전환 없음 | L157~158, L444 |
| 출발 안내 `.hint` | 가운데, top 35u(378), 여백 .8u 4u, 줄바꿈 없음 | 바탕 `rgba(40,24,36,.78)`, 테두리 3px `rgba(255,226,200,.25)`, 반지름 3u, 그림자 `sh` | D 4.4u `ui.lobby.hint` 「드래그해서 출발」 + 줄바꿈 B7 2.4u 불투명 .85 `ui.lobby.hintSub` 「좌우 드래그 · 방향키로 이동, 공격은 자동」 | — | `breathe` 1.6s ease-in-out 무한(50% 불투명 .6) | L160~162, L312~313, L596~597 |
| 야옹신 버튼 `#godBtn` | left 2.4u(25.92), top 33%(633.6), 너비 17u, 여백 1u, 사이 .4u | Panel 바탕, 테두리 6px `--gc`, 반지름 2u, **그림자 없음** | 초상 11u×12.9u(118.8×139.32) 반지름 1.2u, D 3u `--gc` `name:<113_god id>`, B8 2.1u `--ink-dim` `ui.lobby.godSub` 「야옹신 선택」 | `save.god`, `113_god.color`·`portrait` | 야옹신 패널(소리 없음) | L172~176, L338, L2493, L2510 |
| 장난감 바구니 `#aupBtn` | left 2.4u, top 50%(960) | `SideButton` `--sc #5fd0ff` | 아이콘 `lobby.side.basket`, `ui.lobby.aup` 「장난감 바구니」, 부제 `RewardSlot Inline`(캣닢, `fmt`) | `save.dia` | §5.8 | L345, L658, L2569, L2571 |
| 아홉 목숨 `#rbBtn` | left 2.4u, top 63%(1209.6) | `SideButton` `--sc #ff9a5a`. 잠김 `grayscale(.8) brightness(.8)` | 아이콘 `05_contentOpen[부활].icon`, `ui.lobby.rb` 「아홉 목숨」, 부제 아래 | 아래 | 잠김: SoftLock → 토스트 `toastMessage:rbLocked` 「부활은 Area 2를 열면 해금됩니다」. 열림: §5.9 | L346, L660, L2567~2568, L2572 |
| 컨텐츠 버튼 | §5.15.1 | | | | | 새 설계 |
| 시작 무기 덱 | right 2.4u, top 34%(652.8), 너비 19u | §5.5.2 | | | | L202~212, L314 |
| 강화 버튼 줄 `#ups` | left·right 3u, bottom 3.4u, 3칸 사이 1.6u → 칸 30.27u(326.9) | §5.5.1 | | | | L163, L315 |

아홉 목숨 부제(L2567~2568): 잠김 `ui.lobby.rbLocked` 「Area {0} 해금」(`05_contentOpen.conditionValue`) / 쿨타임 중 `hms(남은 ms)` / 그 밖 `ui.lobby.rbPending` 「조각 +{0} 대기」.

갱신: 1초마다 사이드 버튼 부제를 다시 그린다(L2565). 시설 반짝임(`AZ.bump`)은 이벤트 `LobbyFacilityBump(key)`로 월드 렌더에 넘긴다([02](02_Visual.md)).

### 5.4.3 세로 배치 점검 (1080 기준 [추정])

| y(u) | 왼쪽 | 가운데 | 오른쪽 |
|---|---|---|---|
| 2~23 | Level·구역 | 지도 버튼 2.2~19.2 | 재화 칩 3~23.3 |
| 24~32 | 사운드·테스트 버튼 24~30.4(§5.14) | 진행 상자 24.5~32.3 | |
| 35~52 | | 출발 안내(x 약 22~78) | 컨텐츠 버튼 36~51.6(x 80.6~97.6) |
| 58.7~81 | 야옹신 버튼 | | 덱 60.4~ |
| 88.9~104 | 장난감 바구니 | | |
| 112~127 | 아홉 목숨 | | |
| 약 137~174.4 | 강화 버튼 3칸 | | |

## 5.5 강화 버튼 3종·덱 머지 연출

### 5.5.1 강화 버튼 `UpgradeButton`

정본은 버튼을 매번 새로 만든다(L2632). Unity는 3개를 두고 내용만 바꾼다. 종류·순서는 `10_lobbyUpgrade`(`sort`: 캣타워 → 스크래쳐 → 생선 건조대). 비용은 `16_levelPrice`(행의 `levelPriceGroup`, 다음 단계)의 consumeData. 단계 계산은 [04](04_GameRules.md).

| 부분 | 크기 | 스타일 | 글꼴·문구 | L |
|---|---|---|---|---|
| 버튼 `.up` | 칸 너비, 여백 1.6u 1u 1.2u, 세로 가운데 사이 .6u, 넘침 자름 | 바탕 세로 `#5c3e4c → #35232e`, 테두리 3px `rgba(255,220,190,.16)`, 반지름 2.4u, 하이라이트 + 아래 .9u `#0e0c14` + `--panel-sh` | — | L164~165, L316, L324, L590 |
| 이름 `.n` | — | `--gold` | D 4u `name:<10_lobbyUpgrade id>`. 캣타워만 3.3u, 자간 −.04em, 줄바꿈 없음 | L166, L318~319, L536 |
| 아이콘 `.ic` | 높이 8.6u(92.88) | — | 캣타워·건조대 `10_lobbyUpgrade.icon` + `_<아지트 단계>`, 스크래쳐 `103_item[무기 Level].icon` | L218~219, L591~592, L2636~2638 |
| 레벨 `.l` | — | `--ink-dim` | B8 2.5u | L167, L320 |
| 진행바 `.g`(캣타워·스크래쳐) | 너비 100%, 높이 3u, 반지름 27px | 바탕 `#0e0d12`, 테두리 3px `rgba(255,255,255,.2)`, 채움 가로 `#3fa85a → #8ff0a0`, 너비 .3s ease-out. 가운데 글 D 2.4u `#fff` `shadow.1` | — | L222~224, L323 |
| 설명 `.e` | 최소 높이 2.4em, 가운데, 줄높이 1.2 | — | B7 2.4u | L168, L321 |
| 가격 `.p` | `PriceButton` 줄 모양: 너비 100%, 여백 .8u 2.4u, 반지름 1.4u, 사이 1u | 바탕 `#0e0d12`, 테두리 3px `rgba(242,193,78,.25)` | 아이콘 3.6u(consumeData) + D 3.6u `fmt(값)` 또는 `ui.common.max` 「MAX」 | L169~170, L322 |

| 버튼 | `.l` | `.e` | 진행바 글 | L |
|---|---|---|---|---|
| 캣타워 Lv < 4 | `ui.up.stairLv` 「계단 Lv {0}」 | `ui.up.stairDesc` 「다 차면 Lv {0} · 계단 게이트 1칸 +{1}」 | 「{0}/{1}」 | L823 |
| 캣타워 Lv 4 | 같음 | `ui.up.stairMax` 「로비 최대 · 이후는 벽돌 우물로」 | 「MAX」, 100% | L823 |
| 스크래쳐 | `ui.up.weaponLv` 「Level {0}」 | 마지막 칸 `ui.up.weaponDescLast` 「{0} 카드 +1 · 다 차면 {1} 카드로」 / 그 밖 `ui.up.weaponDesc` 「{0} 카드 1장 추가」 | 「{0}/{1}」(8등급이면 「{0}」, 100%) | L826 |
| 생선 건조대 | `ui.up.incomeLv` 「Lv {0}」 | `ui.up.incomeDesc` 「츄르 ×{0} → ×{1}」(소수 2자리) | 없음 | L828 |

| 상태 | 조건 | 모양 | L |
|---|---|---|---|
| 꺼짐 | 최대 또는 비용 부족 | `grayscale(.9) brightness(.7)`, 눌러도 무시 | L171 |
| 누름 | 켜짐 | 아래로 .6u, .08s | L316~317 |
| `lvup` | 구매로 단계가 오름 | 0% `scale 1.12`·빛 5u `--gold`·테두리 `--gold` → 원래, .7s ease-out | L227~228, L2647 |
| `tick` | 단계 그대로 | 진행바만 0% 빛 2u `#8ff0a0`·`scaleY 1.3` → 원래, .35s. 건조대는 진행바가 없어 안 보임(정본대로) | L225~226 |

구매 순서(L2641~2650):

| 시각 | 일 |
|---|---|
| 0 | MV.Meta 구매(차감·저장), 소리 `buy`, 시설 반짝임(`tower`/`scratch`/`rack`), 다시 그림(스크래쳐는 덱 보류), 새 버튼에 `lvup`/`tick`. 단계 상승 배너: 캣타워 `ui.banner.stairUp` 「캣타워 Lv {0}」/`ui.banner.stairUpSub` 「계단 게이트 1칸당 계단 +{0}」, 무기 `ui.banner.weaponUp` 「무기 Level {0}」/`ui.banner.weaponUpSub` 「이제 {0} 카드 · 장난감 공방도 {0}」 |
| 0 (스크래쳐) | 사기 전 Level 등급 카드 1장을 덱에 넣고 3장 머지 연쇄(MV.Meta). 무기 그림 9u가 버튼에서 덱으로 `flyTo2` |
| 0.45 | 머지가 있었으면 소리 `merge` |
| 0.56 | 덱 다시 그림: 머지면 결과 등급 첫 카드 `merged` + 장착 칸 `eqpop` + 토스트 `toastMessage:deckMerge` 「머지 → {0}」, 아니면 새 카드 `deal` |

가격은 모두 `PriceButton` 규칙(§5.1.8)이다. 비용이 consumeData라서 나중에 비용 재화를 바꿔도 화면 코드는 그대로다.

### 5.5.2 시작 무기 덱 `DeckWidget`

| 요소 | 크기 | 스타일 | 글꼴·문구 | 데이터 | L |
|---|---|---|---|---|---|
| 상자 `#deck` | right 2.4u, top 34%, 너비 19u, 여백 1.2u, 세로 사이 .8u, 넘침 자름 | Panel 바탕, 테두리 3px, 반지름 2u, 하이라이트+그림자 | — | — | L202~203, L212, L314 |
| 제목 `.dk-h` | 가운데 | `--ink-dim` | D 2.8u `ui.lobby.deckTitle` 「시작 무기」 | — | L204 |
| 장착 칸 `#deckEq` | 여백 .6u, 세로 가운데 | 바탕 160° `#3a4566 → #161b2c`, 테두리 6px **최고 등급색**(`103_item.gradeCol`), 반지름 1.4u | 그림 11u, D 3u `--gold` 줄높이 1.1 `name:<103_item id>`, B8 2.3u `--ink-dim` `ui.lobby.deckDmg` 「피해 {0}」 | `save.deck` 최고 등급, `103_item.dmg` | L206~209, L1729 |
| 목록 `#deckList` | 세로 사이 .5u | — | — | — | L213 |
| 카드 `.dcard` | 높이 5.4u(58.32), 그림 5u | 반지름 .9u, 테두리 6px `--wc`, 바탕 `mix(--wc 30%, #161b2c)` | — | 등급 8 → 1, 장수만큼 | L214~215, L1730~1732 |
| 아래 글 `.dk-f` | 가운데 | `--ink-dim` | B8 2.1u `ui.lobby.deckFoot` 「같은 카드 3장 → 머지」 | — | L205 |

| 연출 | 대상 | 키프레임 | L |
|---|---|---|---|
| `merged` | 머지 결과 등급 첫 카드 | 0% `translateY(60%) scale(1.6)` 밝기 3.4 빛 6u `--wc` → 원래, .55s ease-out | L115, L216 |
| `deal` | 머지 없을 때 새 등급의 마지막 카드 | 0% `translate(40%, −90%) rotate(25°) scale(.6)` 불투명 0 → 원래, .25s ease-out | L108, L217 |
| `eqpop` | 장착 칸 | 0% `scale 1.12` 밝기 2 → 원래, .5s ease-out | L210~211 |

덱이 길어지면 강화 버튼과 겹칠 수 있다(정본도 높이 제한 없음). 정본대로 두고 캡처 대조 대상에 적는다.

## 5.6 결과 — `UIPopup_Result`

`ResultPopup : UIPopup`, 층 300. 정본 `#result.ov`(L686~698, CSS L253~267·L329~331·L373~375), `finish`(L2394~2418), `collect`(L2424~2443). 런 끝에 `ResultViewData`(제목 종류·진행·보상 consumeData 목록·통계 줄·광고 배수 가능 여부)를 받아 그린다. 컨텐츠 모드(§5.15)도 같은 팝업을 쓴다.

| 요소 | 위치·크기 | 스타일 | 글꼴·문구 | 데이터 | L |
|---|---|---|---|---|---|
| 막 | 전체 | `rgba(5,4,8,.72)`, 누름 막음(눌러도 안 닫힘) | — | — | L253 |
| 상자 | 가운데, 너비 80%(864), 세로 가운데 사이 3u | 바탕 없음 | — | — | L254 |
| 제목 `#rTitle` | 가운데, 줄높이 1.15 | `--gold`, 실패면 `#ffb08a` | D 7.6u(82.08) 아래 표 | `ResultViewData.kind` | L255~256, L331, L2410~2411 |
| 진행 글 | 너비 100%, 양 끝, 아래 사이 1.2u | — | B8 3.4u 왼쪽 `ui.result.progNext` 「Area {0} 개방까지 생선」 또는 `ui.result.progZone` 「Zone {0} · 업데이트 예정」, 오른쪽 D* 「{0} / {1}」 | `save.souls + R.souls`, `302_area.needSouls` | L257~258, L2412~2413 |
| 진행 막대 | 높이 3u, 반지름 2u, 넘침 자름 | 바탕 `rgba(255,255,255,.1)`, 채움 가로 `#9a6418 → --gold` | — | `min(100, have/need)` | L259~260 |
| 보상 칸 | 3칸 격자 사이 2u, 너비 100% | `RewardSlot Card` ×3(아래) | — | consumeData 목록 | L261~265, L329 |
| 통계 `#rStat` | 가운데, 줄높이 1.5, 줄바꿈 유지 | `--ink-dim` | B7 2.7u, 4줄 `msg:result.stat1`~`stat4`(정본 L2416 문구, 인자는 레벨·구역·Area·막힌 곳/우다다 칸·파괴·계단 Lv·생선/최종 무기·모습·친구·가호 장수/체크포인트·다음 조건) | 런 통계 | L266, L330, L2415~2416 |
| 버튼 묶음 | 세로 가운데 사이 2u | — | — | — | L373 |
| 광고 버튼 `#x3Btn` | — | `AdButton` 큰 모양: 바탕 `#ffd84a`, 글 `#2a1608`, 반지름 2.4u, 여백 1.8u 8u, 그림자 아래 1.2u `#a87a10` | D 5.6u `ui.result.adX3` 「▶ 광고 보고 ×3 받기」 | `08_adPlacement collect3` | L374 |
| 그냥 받기 `#nextBtn` | 여백 1u | 바탕 없음, 글 `--ink`, 밑줄 | D 3.6u `ui.result.next` 「그냥 받기」 | — | L267, L375 |

보상 칸 `RewardSlot Card`: 여백 2.4u, 세로 사이 1u, 반지름 2u, 테두리 3px `--panel-edge`, 바탕 세로 `rgba(48,42,62,.92) → rgba(26,22,34,.92)` + 윗줄 하이라이트, 아이콘 7u(`07_consumeType.iconAtlas` + 대상 표 icon), 값 D 6u(합성 볼드 아님) 「+{0}」(츄르 `fmt`, 캣닢·조각 숫자 그대로), 이름 B7 2.8u `--ink-dim` `name:<대상 행 id>`. 정본 3칸 = 츄르 `R.gold`, 캣닢 `R.dia`, 목숨 조각 `R.tok`(조각 아이콘은 정본 🐾 글자 6.4u → `01_wealth[4].icon`).

| 제목 종류 | 조건(정본) | 문구 | 색 |
|---|---|---|---|
| 구역 클리어 | 관문 도달 + 생선 충족 + 공개 마지막 Area | `ui.result.zoneClear` 「Zone {0} 클리어!」 | gold |
| Area 개방 | 관문 도달 + 생선 충족 | `ui.result.areaOpen` 「Area {0} 개방!」 | gold |
| 레벨 완주 | 관문 도달 | `ui.result.levelDone` 「LEVEL COMPLETED!」 | gold |
| 실패 | 그 밖 | `ui.result.fail` 「아지트로 귀환」 | `#ffb08a` |

| 동작 | 일 | L |
|---|---|---|
| 열림 | HUD·경고 숨김, 내용 채움, 소리 `win`(관문 도달) 또는 `fail`(350ms 뒤). 열림 연출 없음 | L2407, L2417 |
| 광고 버튼 | 츄르 ≥ 1 또는 캣닢 > 0일 때만 켜짐. 소리 `click` → §5.12 흐름(`collect3`) → 성공 시 배수 3 정산 | L2414, L2445 |
| 그냥 받기 | 소리 `click` → 배수 1 정산 | L2444 |
| 정산 | MV.Meta 정산·저장 → 배수 1이고 Area ≥ 3(인덱스)이면 전면 광고(`interstitial`) 뒤 → 로비 | L2424~2443 |
| 로비 도착 | Area 개방이면 배너(구역이 바뀌면 `ui.banner.zone` 「Zone {0} · {1}」/`ui.banner.zoneSub` 「Area {0} 시작」, 아니면 `ui.banner.area` 「Area {0}」/`ui.banner.areaSub` 「{0} · 강화 초기화」), 구역 클리어면 `ui.banner.zoneClear` 「Zone {0} 클리어」/`ui.banner.zoneClearSub` 「Zone {0}는 업데이트 예정」. 새 해금이 있으면 900ms 뒤 §5.11 | L2438~2441 |

**정본 버그(D-36).** 통계 문구가 관문 도달이 아닌 모든 종료에서 `R.hitWall.bar`를 읽는다(L2415). 장애물 충돌이 아닌 종료(예: Area 포털 쪽 종료 경로)에서는 값이 없어 예외가 나고 결과 화면이 열리지 않는다 [추정]. Unity는 막힌 곳이 없으면 그 조각을 빼고 연다(고침 권장).

## 5.7 야옹신 — `UIPopup_GodSelect`

`GodSelectPopup : UIPopup`, 층 500. 정본 `#godPanel`(L663~673, CSS L177~201·L376~378·L595), `renderGodPanel`(L2494~2509). 데이터 `113_god`(`sort`·`active`), 고유 카드 `111_cardPool` → `110_card`, 등급 `115_cardTier`.

| 요소 | 위치·크기 | 스타일 | 글꼴·문구 | 데이터 | L |
|---|---|---|---|---|---|
| 막 | 전체 | `rgba(10,8,16,.74)`. **막을 누르면 닫힘** | — | — | L177, L2512 |
| 상자 `.gp-box` | 가운데, 너비 94u(1015.2), 최대 높이 94%, 여백 3u, 세로 사이 1.6u, 세로 스크롤 | 바탕 세로 `#4a3242 → #2a1c26`(L595가 덮음), 테두리 6px `rgba(255,255,255,.2)`, 반지름 3u. `--gc` = 보고 있는 신 색 | — | — | L178~179, L595 |
| 제목 | 가운데 | `--gold` | D 5u `ui.god.title` 「달의 야옹신」 | — | L180 |
| 닫기 `#gpClose` | right 2u top 2u, 7u 원 | `CloseX`: 바탕 `#c8463a`, 글 `#fff` 3.6u 굵기 900 「✕」 | — | — | L181 |
| 탭 `#gpTabs` | 5칸 격자, 사이 1u | 칸: 여백 1u .4u, 반지름 1.4u, 테두리 6px `--gc`, 글 `--gc`, 바탕 투명 | B8 2.3u 이름 + 아래 줄 B7 2u 불투명 .8 상태(`ui.god.inUse` 「사용 중」/`ui.god.open` 「해금」/「<sprite lock> 」+`ui.god.locked` 「잠김」) | `113_god` 순서, 잠김 칸은 업데이트 예정 신 이름 | L183~185, L376~377 |
| 탭 선택 | — | 바탕 `--gc`, 글 `#15131c` | — | 보고 있는 신 | L184 |
| 탭 잠김 | — | 불투명 .45, 점선 테두리(`MvShape` dash) | — | — | L378 |
| 주인공 줄 | 가로 가운데 정렬, 사이 2u | — | — | — | L186 |
| 초상 `#gpIcon` | 22u×25.7u(237.6×277.56), 반지름 1.6u | — | `113_god.portrait` | — | L187 |
| 정보 묶음 | 세로 사이 .6u | — | — | — | L188 |
| Level | — | `#ffb04a` | D 3.2u `ui.god.level` 「Level 1」 | 고정 | L189 |
| 이름 `#gpName` | — | `--gc` | D 6u `name:<113_god id>` | — | L190 |
| 부제 `#gpSub` | — | — | B8 2.8u `description:<113_god id>` | — | L191 |
| 설명 | 줄높이 1.35 | `--ink-dim` | B7 2.3u `ui.god.note`(정본 L669 고정 문구) | — | L192 |
| 묶음 제목 `.gp-h` | 여백 1u, 반지름 1.4u, 가운데 | 바탕 `#15131c` | D 3.4u `ui.god.cardsTitle` 「고유 가호 ({0}장)」 | 고유 카드 수 | L193 |
| 카드 격자 | 3칸, 사이 1.4u | 칸 `.gpc`: 여백 .8u, 세로 사이 .4u, 테두리 .6u `--tc`, 반지름 1.6u, 바탕 `mix(--tc 24%, #15131c)` | 이름 띠: 너비 100%, 여백 .3u 0, 2.5u 굵기 900 `#fff` 바탕 `mix(--tc 55%, #000)` 반지름 .8u `name:<110_card id>` / 그림 12u `110_card.icon` / 등급 1.9u 굵기 900 `--tc` 자간 .08em `name:<115_cardTier id>` / 설명 B7 2.1u 줄높이 1.25 `description:<110_card id>` | 신의 고유 카드 | L194~199 |
| 사용 버튼 `#gpPick` | 너비 100%, 여백 1.8u, 반지름 2.4u | `GoldButton`: 바탕 `--gold`, 글 `#0b0b0e`, 그림자 아래 1u `#9a6418`. 꺼짐: 바탕 `#5a5662`, 글 `#d8d2c6`, 그림자 없음 | D 5u `ui.god.use` 「이 야옹신 사용」 / 이미 사용 중이면 `ui.god.inUse` 「사용 중」(꺼짐) | `save.god` | L200~201, L2508 |

| 동작 | 일 | L |
|---|---|---|
| 열기 | 보고 있는 신 = 사용 중 신 → 그림 → 열림(소리 없음) | L2510 |
| 탭(열림) | 보고 있는 신 변경 → 다시 그림 | L2501 |
| 탭(잠김) | 토스트 `toastMessage:godLocked` 「{0} · {1} — 업데이트에서 해금 예정」(이름·설명) | L2501 |
| 사용 | `save.god` 저장 → 카드 풀 교체 → 로비 야옹신 버튼 다시 그림 → **패널 닫힘** | L2513 |
| 닫기·막 | 닫힘 | L2511~2512 |

토스트(층 800)는 이 패널 위에 보인다(D-32: 정본은 패널 뒤에 가려짐).

## 5.8 Area 강화 — `UIPopup_AreaUpgrade`

`AreaUpgradePopup : UIPopup`, 층 500. 정본 `#aupPanel`(L699~702), `renderAup`(L2522~2532). 공용 패널 틀 `.pnl`(L347~364, L595) — §5.9·§5.16도 같은 틀:

| 틀 요소 | 값 | L |
|---|---|---|
| 막 | `rgba(10,8,16,.74)`, 누르면 **안 닫힘** | L347 |
| 상자 | 가운데, 너비 92u(993.6), 최대 높이 92%, 세로 스크롤, 여백 3u, 세로 사이 1.6u, 바탕 세로 `#4a3242 → #2a1c26`, 테두리 4.5px `--pc`, 반지름 3u | L348~349, L595 |
| 제목 | D 6u `--pc` 가운데 | L350 |
| 닫기 | `CloseX`(right 2u top 2u 7u 원 `#c8463a`) | L351 |
| 부제 `.sub` | B8 2.6u `--ink-dim` 가운데 | L352 |
| 행 `.row2` | 가로 가운데 사이 2u, 여백 1.4u 2u, 반지름 2u, 바탕 `rgba(0,0,0,.28)`, 테두리 3px `--panel-edge` | L353 |
| 행 아이콘 | 칸 너비 9u, 그림 6u 가운데 | L354 |
| 행 가운데 | 세로 사이 .4u, 남는 너비 전부. 제목 B7 3.4u, 값 B7 2.6u `--ink-dim`, 강조 `#8ff0a0` | L355~358 |
| 행 버튼 | `PriceButton` 초록: 최소 너비 17u, D 4u, 반지름 1.6u, 여백 1.4u 1.6u, 바탕 `#3fa85a`, 글 `#fff`, 그림자 아래 .6u `#23683a`. 꺼짐: 바탕 `#4a4656`, 글 `#a8a2b4`, 그림자 없음 | L359~360 |
| 큰 버튼 `.bigBtn` | `GoldButton`: D 5u, 반지름 2u, 여백 1.8u, 바탕 `--gold`, 글 `#0b0b0e`, 그림자 아래 1u `#9a6418`. 꺼짐 `#5a5662`/`#d8d2c6` | L361~362 |
| 광고 버튼 `.adBtn` | `AdButton`: D 3.6u, 반지름 1.6u, 여백 1.2u 2.4u, 바탕 `#ffd84a`, 글 `#2a1608`, 그림자 아래 .7u `#a87a10`. 꺼짐 `#5a5662`/`#bbb` | L363~364 |

여기서 `--pc = #5fd0ff`.

| 요소 | 글꼴·문구 | 데이터 | L |
|---|---|---|---|
| 제목 | `ui.aup.title` 「장난감 바구니 · Area {0}」 | `save.area+1` | L2523 |
| 부제 | `ui.aup.sub` 「이 Area에서만 유지되는 강화 · 캣닢 사용 · Area가 바뀌면 초기화」 | — | L700 |
| 보유 행 | 아이콘 `01_wealth[2].icon`, 제목 `ui.aup.have` 「보유 」+ D* `fmt(캣닢)`, 값 `ui.aup.adInfo` 「광고 보고 캣닢 ×100 · 오늘 {0}회 남음」(숫자 강조색) | `save.dia`, `08_adPlacement dia` 남은 횟수 | L701, L2523 |
| 무료 버튼 `#aupFree` | `ui.aup.free` 「▶ 무료」. 클래스는 광고 버튼이지만 행 버튼 규칙이 이겨 **초록으로 보인다** → 정본대로 초록(D-33) | 남은 횟수 0이면 꺼짐 | L701, L359, L363 |
| 강화 행 ×5 | 아이콘 `13_areaUpgrade.icon`, 제목 `name:<13_areaUpgrade id>`(예 「Fire Rate · 냥냥펀치 연사」), 값 `ui.aup.val` 「Lv {0} · {1}」 + 최대면 `ui.aup.max` 「 (MAX)」, 아니면 「 → 」 + 강조색 다음 값 | `13_areaUpgrade`(sort), 단계 `save.aup` | L2515~2531 |
| 행 가격 | `PriceButton` 「<sprite herb> {0}」 또는 「MAX」, 최대·부족이면 꺼짐 | 단계 비용 consumeData(`16_levelPrice`) | L2528 |

| 동작 | 일 | L |
|---|---|---|
| 강화 구매 | MV.Meta 구매 → 소리 `buy` → 시설 반짝임 `basket` → 다시 그림 + 로비 다시 그림(덱 제외) | L2529 |
| 무료(광고) | §5.12 흐름(`dia`) → 성공 시 캣닢 +100, 남은 횟수 −1 → 다시 그림 + 사이드 버튼 | L2574 |
| 닫기 | 닫힘 → 로비 다시 그림(덱 제외) | L2573 |

값 표기(정본 `f(l)`: `l×10%`, `l`, `220+l×20%`, `100−l×5%` 등)는 표에서 정해야 한다 → `13_areaUpgrade`·`14_rebirthPerk`에 `valueFormat`(예 `{0}%`)·`valueBase`·`valueStep` 열 필요([07](07_Tables.md)).

## 5.9 부활 — `UIPopup_Rebirth`

`RebirthPopup : UIPopup`, 층 500. 정본 `#rbPanel`(L703~709, CSS L365~369), `renderRb`(L2543~2557), `doRebirth`(L2559~2564). `.pnl` 틀(§5.8), `--pc = #ff9a5a`.

| 요소 | 크기·스타일 | 글꼴·문구 | 데이터 | L |
|---|---|---|---|---|
| 제목 | — | `ui.rb.title` 「아홉 목숨 · 부활」 | — | L703 |
| 위 칸 `.rbTop` | 격자 1fr : 1.6fr, 사이 1.6u. 칸: 바탕 `rgba(0,0,0,.28)`, 테두리 3px `--panel-edge`, 반지름 2u, 여백 1.6u, 세로 가운데 사이 .8u | 칸 머리 B8 2.6u `--ink-dim` | — | L365~366, L368 |
| 부활 레벨(왼쪽) | — | `ui.rb.level` 「부활 레벨」 + D 8u `#ffb05a` 줄높이 1 숫자 | `save.rb.lv` | L367, L704 |
| 쿨타임(오른쪽) | — | `ui.rb.cd` 「부활 쿨타임」 + D 4.6u `#fff` `hms` 또는 `ui.rb.ready` 「준비됨」 | `save.rb.cdUntil` | L369, L705 |
| 쿨타임 버튼 | 가로 사이 1u | `AdButton` `ui.rb.skip` 「▶ 70분 스킵」(`08_adPlacement skip`, 쿨타임 중 + 남은 횟수 > 0일 때만 켜짐) + `AdButton` `ui.rb.skipDia` 「다이아」 **늘 꺼짐**(유료 재화 스텁) | — | L705, L2546 |
| 안내 | B8 2.6u `--ink-dim` | `ui.rb.adInfo` 「광고 스킵 오늘 {0}회 · 유료 다이아는 향후」 | 남은 횟수 | L2546 |
| 보유 줄 `.sub` | 3조각 라벨(§5.1.12 섞인 글꼴 규칙) | `ui.rb.subA` 「보유 조각 」 + D* `#ffb05a` `fmt(조각)` + `ui.rb.subB` 「 · 영구 능력치는 부활해도 유지」 | `save.rb.tokens` | L706 |
| 영구 능력 행 ×4 | `.row2` | 아이콘 `14_rebirthPerk.icon`, 제목 `name:<14_rebirthPerk id>`(예 「Power · 공격력」), 값 §5.8과 같은 형식 | `14_rebirthPerk`, `save.rb.perks` | L2533~2537, L2548~2555 |
| 행 가격 | `PriceButton` 초록 | 「<sprite paw> {0}」 / 「MAX」 | 조각 consumeData(`16_levelPrice`) | L2553 |
| 경고 `.sub` | — | `ui.rb.warn` 「부활하면 Area 진척도·츄르·생선·로비 강화·Area 강화가 초기화되고, 쌓아 둔 목숨 조각을 받습니다」 | — | L708 |
| 부활 버튼 `#rbGo` | `GoldButton`(`.bigBtn`) | 아래 표 | — | L709, L2556~2557 |

| `#rbGo` 상태 | 문구 | 켜짐 |
|---|---|---|
| 잠김 | `ui.rb.goLocked` 「Area 2 개방 시 해금」(숫자는 `05_contentOpen.conditionValue`) | 꺼짐 |
| 쿨타임 중 | `ui.rb.goCd` 「쿨타임 {0}」 | 꺼짐 |
| 확인 대기 | `ui.rb.goArm` 「한 번 더 누르면 부활 · 조각 +{0}」 | 켜짐 |
| 기본 | `ui.rb.goBase` 「부활 · 조각 +{0}」 | 켜짐 |

| 동작 | 일 | L |
|---|---|---|
| 열기 | 잠김이면 토스트만(§5.4). 열리면 확인 대기 끔 → 그림 | L2572 |
| 영구 능력 구매 | MV.Meta 구매 → 소리 `lvup` → 시설 반짝임 `stove` → 다시 그림 | L2554 |
| 부활 1번 | 확인 대기 시작(2.5초 창), 2.6초 뒤 다시 그림 | L2576 |
| 부활 2번(2.5초 안) | MV.Meta 부활 → 패널 닫힘 → 로비 → 배너 `ui.banner.rebirth` 「부활 Lv {0}」/`ui.banner.rebirthSub` 「목숨 조각 +{0} · 처음부터 다시」 + 소리 `win` | L2559~2564 |
| 70분 스킵 | §5.12 흐름(`skip`) → 쿨타임 −70분, 횟수 −1 | L2575 |
| 갱신 | 열려 있는 동안 1초마다 다시 그림 | L2565 |
| 쿨타임 끝 알림 | `LocalNotificationManager`로 쿨타임 끝 예약 [창작] | — |

**정본 버그(D-36).** 문구는 「확인 대기 시각이 0이 아님」으로 고르고, 두 번째 누름은 「2.5초 안」으로 판정한다. 그래서 2.5초가 지나도 「한 번 더 누르면 부활」이 남아 있고, 그때 누르면 부활하지 않고 대기만 다시 시작한다. Unity는 2.5초가 지나면 확인 대기를 끄고 기본 문구로 되돌린다(고침 권장).

## 5.10 지도 — `UIPopup_Map`

`MapPopup : UIPopup`, 층 550. 정본 `#mapPanel`(L710, CSS L445~480·L593~594), `renderMap`(L2587~2609). 전체 화면, 바탕 `#2a1e2a`, 세로 배치(머리 → 스크롤). 열 때 소리 `click`. 데이터 `301_zone`·`302_area`·`303_areaFloor`·`05_contentOpen`.

| 요소 | 위치·크기 | 스타일 | 글꼴·문구 | 데이터 | L |
|---|---|---|---|---|---|
| 머리 `.mp-head` | 위, 여백 3u 0 2u, 가로 가운데(글자 기준선) 사이 2u | 바탕 `#22161f`, 아래 테두리 6px `#5a3e4c` | D 6u `--gold` `ui.map.title` 「달동네 지도」 + B8 2.8u `--ink-dim` `ui.map.cur` 「{0} · Area {1} · 생선 {2}/{3}」 | 현재 구역 이름·Area·생선·필요량 | L446~448, L594, L2607 |
| 스크롤 | 남은 높이, 세로 `ScrollRect` | 내용은 **아래에서 위로** 쌓임(정본 `column-reverse`) | — | — | L449~450 |
| 구간 칸 `.mp-sec` | 너비 100%, 여백 1.4u 3u | 바탕 `mix(c1 55%, #141020)`(구역색) | — | `301_zone.mapCol1`·`mapCol2` 열 | L452, L2593 |
| 구역 머리 `.mp-zone` | 바깥 1u 2u, 높이 17u(183.6), 반지름 2u, 세로 가운데 | 바탕 135° `c1 → c2`, 테두리 6px `rgba(255,255,255,.25)`, 그림자 아래 .8u `rgba(0,0,0,.35)` | D 7u `#fff` 자간 .04em 그림자 `map.zone` `name:<301_zone id>` + D 3.6u `#ffd84a` 외곽선 3px `#1a1020` `ui.map.zoneTag` 「ZONE {0}」(+ 2번째 구역부터 `ui.map.soonSuffix` 「 · 업데이트 예정」) | — | L453~456, L2594 |
| 구역 잠김 | — | `grayscale(.75) brightness(.7)` | — | 구역 첫 Area > 현재 Area | L457 |
| 기능 상자 `.mp-ft` | 여백 1.2u 0, 가로 줄바꿈 사이 2u | 항목: 늘어남, 최소 너비 30u, 반지름 1.6u, 여백 1.2u 2u, 가로 사이 1.6u, 바탕 세로 `#a24be6 → #6a22b0`, 테두리 6px `#40106e`, 그림자 아래 .7u `#2c0a4e`. 해금됨 `#3fc36a → #1f8a44`·`#0e5a28`·`#0a4a1e` | 아이콘 7u `05_contentOpen.icon`, 이름 D 4.2u `#ffd84a` 외곽선 `#2a0a40` `name:<05_contentOpen id>`, 상태 B8 2.3u `ui.map.featOn` 「해금됨」/`ui.map.featAt` 「Area {0} 개방 시 해금」/`ui.map.featSoon` 「New Feature · 업데이트 예정」 + 줄바꿈 `description:<05_contentOpen id>` | `05_contentOpen`(Area 조건 행, 그 Area 배너 바로 아래) | L474~479, L2596 |
| Area 배너 `.mp-area` | 높이 11u(118.8), 반지름 1.6u, 가로 사이 2u, 여백 0 3u, 바깥 .6u 0 | 바탕 가로 `c2 → c1`, 테두리 6px `rgba(255,255,255,.3)`, 그림자 아래 .7u `rgba(0,0,0,.35)` | 번호(left 3u top .6u) 2.4u 굵기 900 `#ffd84a` 「AREA {0}」, 이름 D 5u `#fff` 위 여백 2u `name:<302_area id>`, 오른쪽 끝 칩 2.8u 굵기 900 바탕 `rgba(0,0,0,.35)` 반지름 1u 여백 .4u 1.4u `ui.map.start` 「시작」/`ui.map.need` 「생선 {0}」 | `302_area`(zone, sort), 앞 Area의 `needSouls` | L458~462, L2598~2600 |
| 배너 잠김 | — | 이름·칩 불투명 .35 + 가운데 자물쇠 7u(`<sprite lock>`) | — | Area > 현재 | L463~464 |
| 현재 위치 | — | 외곽선 .7u `#ffd84a` + 빛 4u `#ffd84a`. 태그(right −1u, top −4.6u) 바탕 `#ffd84a`, 글 `#2a1608` 굵기 900 2.6u, 반지름 1.2u, 여백 .4u 1.4u, 그림자 아래 6px `#a87a10` `ui.map.here` 「현재 위치」 | — | 현재 Area(구역 클리어 전) | L465~466 |
| 체크포인트 ×3 `.mp-cp` | 여백 1.2u 0. 골목 1 왼쪽, 2 가운데, 3 오른쪽. 칸: 최소 너비 30u, 여백 1.2u 3u, 반지름 1.6u | 바탕 세로 `#5fb4ff → #2f86e6`, 테두리 6px `#1c5fb0`, 그림자 아래 .7u `#164a8a`. 잠김 `grayscale(.6) brightness(.75)` | D 3.8u 외곽선 `#0c2c58` `ui.map.cp` 「골목 {0}」 + B8 2.4u `ui.map.cpSouls` 「생선 {0}/{1}」(현재 Area)/`ui.map.cpDone` 「완료」(지난 Area)/`ui.map.soon` 「업데이트 예정」(미공개)/`ui.map.cpBase` 「체크포인트」. 완료면 체크(right −2u top −2u) 6u `#4cff6a` | `303_areaFloor`, `save.cp`, `save.bars` | L467~473, L2602~2603 |
| 끝 글 `.mp-end` | 맨 위, 여백 4u 0 3u, 가운데 | `--ink-dim` 굵기 900 3u | `ui.map.end`(정본 「Zone 4~6 · … — 업데이트 예정」) | 표에 없는 구역 이름 | L480, L2606 |
| 닫기 `.mp-x` | 가운데, bottom 2.4u, 16u×8u, 반지름 2u | 바탕 `#d8443a`, 글 `#fff` 4.6u 굵기 900, 그림자 아래 .8u `#8a2620` | 「✕」 | — | L451, L2611 |

화면 아래에서 위로 읽는 순서(구역마다): 구역 머리 → [Area 1 기능 상자] → Area 1 배너 → 골목 1·2·3 → [Area 2 기능 상자] → Area 2 배너 → … → (맨 위) 끝 글.

- 정본은 구역 3개 × Area 5개를 상수로 돈다(L2591). Unity는 `301_zone`(sort) × `302_area`(zone, sort)를 돌며 같은 모양으로 만든다 → 행 추가 = 지도 확장. 기능 상자 항목은 정본 `MAP_FEATS`(L2581) 대신 `05_contentOpen` 행을 쓴다. 정본의 「레이스」 항목은 범위 밖이므로 넣지 않는다([01](01_Scope.md)).
- 열 때 현재 Area 배너가 화면 가운데에 오도록 스크롤한다(현재 없음이면 맨 아래).

## 5.11 신기능 해금 — `UIPopup_FeatureUnlock`

`FeatureUnlockPopup : UIPopup`, 층 600. 정본 `#unlockOv`(L711, CSS L481~491), `showUnlock`(L2612~2615). 정본은 「아홉 목숨」 하나만 Area 2 개방 정산 900ms 뒤 연다(L2441). Unity는 정산 뒤 로비에서 900ms 뒤, 조건을 새로 채웠고 아직 보지 않은 `05_contentOpen` 행마다 차례로 연다.

| 요소 | 크기 | 스타일 | 글꼴·문구 | 데이터 | L |
|---|---|---|---|---|---|
| 막 | 전체 | `rgba(8,6,14,.78)`, 누름 막음 | — | — | L481 |
| 상자 `.ul-box` | 가운데, 너비 72u(777.6), 여백 4u, 세로 가운데 사이 1.4u | 바탕 원형(50% 30%) `#4a2a66 → #1c1428(75%)`, 테두리 6px `#ffb05a`, 반지름 3u | — | — | L482 |
| 태그 | — | `#ffd84a`, 굵기 900, 자간 .2em | 3u `ui.unlock.tag` 「NEW FEATURE」 | — | L484 |
| 아이콘 | 20u(216) | 빛 4u `#ff9a3a`. `ulGlow` 1.2s ease-in-out 무한 왕복(→ `scale 1.08`, 빛 7u `#ffcf5a`) | `05_contentOpen.icon` | — | L485~486 |
| 이름 | — | `#fff` | D 9u `name:<05_contentOpen id>` | — | L487 |
| 설명 | 줄높이 1.45, 가운데 | `--ink-dim` | B7 3u `description:<05_contentOpen id>` | — | L488 |
| 확인 `#ulOk` | 위 여백 1.6u, 여백 1.6u 10u, 반지름 2u | `GoldButton`: 바탕 `--gold`, 글 `#2a1608`, 그림자 아래 1u `#9a6418` | D 5u `ui.common.ok` 「확인」 | — | L489 |

| 연출·동작 | 값 | L |
|---|---|---|
| 열림 `ulIn` | 상자 0% `scale .3 rotate(−8°)` 불투명 0 → 100% 원래, .6s cubic-bezier(.2,1.6,.4,1) + 소리 `lvup` | L482~483, L2613 |
| 확인 | 닫힘 → 해금된 로비 버튼(`05_contentOpen`의 대상 버튼; 정본 `#rbBtn`)에 `sbHint` 1s ease-in-out × 4회(50%에 `scale 1.15`, 빛 5u `#ffb05a`) → 사이드 버튼 다시 그림 → 「본 해금」 저장 | L490~491, L2614 |

## 5.12 가짜 광고 → `AdManager` — `UIPopup_FakeAd`

정본은 광고 대신 3초 검은 화면을 보여 주고 끝나면 보상을 준다(`#adFake` L712, CSS L370~372, `fakeAd` L2419~2422). Unity는 Framework `AdManager`를 부르고, 광고 SDK가 없을 때(에디터·개발 빌드)만 `FakeAdPopup`(층 700)을 쓴다. 작업 계약 MV-PLAT-ADS.

**가짜 광고 화면**: 전체 바탕 `#0b0b0e`, 누름 막음. 가운데 D 6u `#fff` 문구(정본 「보상 광고 · 츄르·캣닢 ×3」 등 → `08_adPlacement`의 `name:<id>`) + 위 여백 2u B7 3u `--ink-dim` `ui.ad.sub` 「프로토 시뮬레이션 · {0}」(3 → 0, 1초마다).

`AdService`(MV.Platform) 흐름:

| 단계 | 일 |
|---|---|
| 1 | 누른 버튼 `UIButtonLock` HardLock + `LoadingIndicator.Scope` |
| 2 | `08_adPlacement` 행(`key`)의 하루 남은 횟수 확인. 0이면 버튼은 이미 꺼져 있음 |
| 3 | `AdManager.ShowRewardAd(new AdRequest(placement), cb)` |
| 4a | `RewardGranted` → MV.Meta가 행의 `adRewardType/Id/Value`(consumeData) 지급(배수형은 호출 화면이 처리), 하루 횟수 −1, 저장 → 호출 화면 다시 그림 |
| 4b | 실패 `Unsupported`(SDK 없음): 개발 빌드면 `FakeAdPopup` 3초 → 4a처럼 지급. 출시 빌드면 토스트 `toastMessage:adFail` |
| 4c | 그 밖 실패(NoFill 등) → 토스트 `toastMessage:adFail`, 횟수 안 줄임 |
| 4d | 건너뜀(`IsSkipped`) → 지급 없음, 토스트 없음 |
| 5 | 잠금·로딩 해제 |

| placement(`08_adPlacement.key`) | 보상 | 하루 | 호출 화면 | L |
|---|---|---|---|---|
| `dia` | 캣닢 +100 | 3 | Area 강화 | L2574 |
| `skip` | 부활 쿨타임 −70분 | 4 | 부활 | L2575 |
| `collect3` | 결과 츄르·캣닢 ×3 | 제한 없음 | 결과 | L2445 |
| `interstitial` | 없음(`ShowInterstitialAd`) | — | 결과 정산(배수 1, Area 인덱스 ≥ 3) | L2436 |

「하루」: 정본은 UTC 날짜(`toISOString`)로 끊는다(L2423, 한국 시간 오전 9시 초기화). Unity도 같은 UTC 기준(`00_config.adDayUtcOffsetHour` 기본 0, [04 D-29](04_GameRules.md)·[08 D-77](08_Content.md) 권장 A). 저장 형식은 [09](09_Save.md).

## 5.13 토스트·배너·플래시

| 종류 | 담당 | 층 | 정본 |
|---|---|---|---|
| 토스트 | `Effect_Toast`(`effects.ui` `ToastMessageEffectRenderer`) | 800 | `#toast` L684, CSS L251~252, `toast()` L2449, 시간 L3828 |
| 배너 | `OverlayHud.ShowBanner` | 280 | `#banner` L683, CSS L247~250·L305~306, `banner()` L2450~2453 |
| 플래시 | `OverlayHud.Flash` | 280 | `#flash` L685, CSS L146 |

**토스트**: 가운데, top 58%(1113.6), 여백 1.6u 4u, 줄바꿈 없음, 바탕 `--panel`, 반지름 2u, B8 4u. 나타남·사라짐 불투명 .2s, 1.4초 유지. 새 토스트가 오면 글만 바꾸고 시간을 다시 잰다. 문구 키 `toastMessage:<항목>`:

| 키 | 문구 | 호출 |
|---|---|---|
| `deckMerge` | 머지 → {0} | 스크래쳐 구매 머지 |
| `noStairs` | 모은 계단이 없습니다 | 계단 0으로 오르기 시작 |
| `rouletteNone` | 우다다 {0}칸 · 5칸 미만이라 가호 없음 | 룰렛 없음 |
| `godLocked` | {0} · {1} — 업데이트에서 해금 예정 | 잠긴 신 |
| `rbLocked` | 부활은 Area 2를 열면 해금됩니다 | 잠긴 부활 |
| `adFail` | 광고를 불러오지 못했습니다 [창작] | §5.12 |
| `contentLocked`·`contentNoCount`·`contentNoCost` | §5.15 | 컨텐츠 메뉴 |
| `debug.*` | 테스트 패널 문구 | §5.14 |

**배너**: 전체 가운데, 큰 글 D 9.4u(101.52) 줄높이 1.05 가운데 그림자 `sh` + 아래 작은 글 4.4u `--gold`. 연출 `bn` 1.5s ease-out 끝 유지(0% 불투명 0 `scale 1.4` → 15% 1·`scale 1` → 75% 1 → 100% 0), 1.5초 뒤 숨김. 새 배너는 처음부터 다시. 초기 CSS 11u/5u(L248~249)는 L305~306이 덮으므로 9.4u/4.4u만 쓴다. 키 목록은 [06](06_Effects.md).

**플래시**: 전체 흰색 Image, 불투명 전환 .25s. 진행 중 새 목표가 오면 현재 값에서 새 목표로 다시 전환(CSS transition 재목표와 같음). 호출(등급 상승 색 플래시, 신 폭발 등)과 값은 [06](06_Effects.md).

## 5.14 시스템 버튼·테스트 패널

`UIHud_System`(`SystemHud`, 층 400 — 정본대로 결과 화면보다 위) + `UIPopup_Debug`(`DebugPopup`, 층 410, **개발 빌드 전용**: `UNITY_EDITOR || DEVELOPMENT_BUILD`).

| 요소 | 위치·크기 | 스타일 | 글꼴·문구 | 동작 | L |
|---|---|---|---|---|---|
| 소리 버튼 `#sndBtn` | left 3u, top 24u(259.2), 6.4u(69.12) 원 | 바탕 `--panel`, 테두리 3px `--panel-edge`, 불투명 .75. 꺼짐: 불투명 .45 + 가로줄 | B8 3.4u 「♪」 | 소리 `click` → `settings.muted` 반전 | L713, L332, L334~335 |
| 테스트 버튼 `#dbgBtn` | left 11u(118.8), top 24u, 높이 6.4u, 여백 0 2.2u, 반지름 3.2u | 같은 바탕, 불투명 .75 | B8 2.4u `ui.debug.btn` 「테스트」 | 테스트 패널 열고 닫기 | L714, L332~333, L3858 |
| 테스트 패널 `#dbg` | left 3u, top 32u(345.6), 너비 64u(691.2), 여백 3u, 세로 사이 2u | 바탕 `rgba(20,18,26,.96)`, 테두리 3px `--panel-edge`, 반지름 2u | 행 제목 3.2u, 설명 `.cap` 2.6u `--ink-dim` 줄높이 1.4 | — | L715~726, L269~275 |

행 버튼: B8 3u, 여백 1u 2u, 바탕 `#2c2836`, 테두리 3px, 반지름 1.2u. 켜짐 바탕 `--gold` 글 `#0b0b0e`. 행은 양 끝 정렬, 버튼 묶음은 오른쪽 정렬 줄바꿈 사이 1u.

| 행 | 내용 | 정본/새 |
|---|---|---|
| 배속 | ×1 / ×2 / ×3 | L716 |
| 연사 | 슬라이더 0.95~4, 0.05 단위, 값 D* 「2.00」 | L717 |
| 밸런스 | 원작 근사 / 임시 기본 / 쉬움(기본) + 설명 줄(`06_balancePreset`) | L718~719 |
| 카드 시험 | 털실 위성 / 정전기 / 연쇄 / 냥냥펀치 / 하악질 / 레이저 + 설명 줄 | L720~721 |
| 재화 | 츄르 +500 / 생선 +50 | L722 |
| 진행 | 레벨 +1 / 초기화(2번 누름) | L723 |
| 부활·캣닢 | 부활 바로 열기 / 쿨타임 해제 / 캣닢 +300 / 조각 +200 | L724 |
| 표 다시 읽기 | 로비에서만, 표 다시 읽기 → 화면 다시 그림 | [창작] |
| 컨텐츠 | 전부 해금 / 하루 횟수 초기화 | [창작] |
| 광고 결과 | 성공 / 실패 / 건너뜀 강제 | [창작] |
| 시간 | +1시간(쿨타임·하루 초기화 시험) | [창작] |
| 캡처 | 현재 상태 저장(정본 대조용) | [창작] |

문구는 `ui.debug.*`, 토스트는 `toastMessage:debug.*`. 출시 빌드에는 버튼과 패널 프리팹을 넣지 않는다.

## 5.15 로비 「컨텐츠」 메뉴 — `UIPopup_ContentMenu` [창작]

사용자 원문: 「메뉴는 로비에서 컨텐츠 버튼 누르면 추가 컨텐츠 메뉴 등장해서 누르면 시작하는 형태로」. 규칙·행 값은 [08](08_Content.md) §8.4·§8.6, 이 절은 화면만 정한다. 작업 계약 MV-UI-CONTENT. 정본 지도 Area 3 「⚔️ 도전 · 특수 규칙 코스」(L2582)가 이 메뉴에 해당한다.

### 5.15.1 로비 버튼

| 항목 | 값 |
|---|---|
| 모양 | `SideButton`, `--sc #c08aff` |
| 위치 | right 2.4u(25.92), top 36u(388.8), 너비 17u → x 80.6~97.6u, y 36~약 51.6u. 출발 안내(가운데 x 22~78u)·덱(top 34% = 60.4u)과 겹치지 않는다(§5.4.3) |
| 아이콘 | `05_contentOpen[5001003 contentMenu].icon`(없으면 `09_uiResource lobby.side.content`) |
| 이름 | `ui.lobby.content` 「컨텐츠」 |
| 부제 | 열린 모드 수 `ui.lobby.contentSub` 「{0}개 열림」, 새 항목이 있으면 `ui.lobby.contentNew` 「NEW」(`#ffd84a`) |
| 빨간 점 | 오른쪽 위(−1u, −1u) 3.4u 원, 바탕 `#ff5a4a`, 테두리 6px `#fff`. 새로 열린 항목·안 본 항목·받을 보상(메타 탭)이 있을 때 |
| 잠김 | 「모드」 탭(5001003, Area 3)과 「성장」 탭 항목이 **모두** 잠겼을 때만 `.locked` 모양 + 누르면 토스트 `toastMessage:contentLocked` 「Area {0}에서 열려요」. 하나라도 열리면 켜짐(D-35) |
| 누름 | 소리 `click` → 메뉴 열기. 처음 열 때 `Atlas_UI_Icon`·카드 배경 텍스처를 `AcquireLoadingAsync`로 미리 읽는다 |

### 5.15.2 팝업 배치

`.pnl` 틀(§5.8), `--pc = #c08aff`, 상자 높이 고정 150u(1620)(목록이 짧아도 같은 크기).

| 요소 | 위치·크기 | 스타일 | 글꼴·문구 | 데이터 |
|---|---|---|---|---|
| 제목 | 위 가운데 | `--pc` | D 6u `ui.content.title` 「컨텐츠」 | — |
| 닫기 | `CloseX` | — | — | — |
| 부제 | 제목 아래 | `.sub` | `ui.content.sub` 「누르면 바로 시작 · 입장권은 매일 다시 채워짐」 | — |
| 탭 | 2칸 격자 사이 1u, 높이 6.4u | 야옹신 탭 모양(§5.7), `--gc = --pc`. 빨간 점 2.4u는 탭 오른쪽 위 | B8 2.6u `ui.content.tabMode` 「모드」 / `ui.content.tabMeta` 「성장」 | — |
| 광고 충전 줄 | 탭 아래, `.row2` 한 줄 | — | `ui.content.adInfo` 「광고 보고 입장권 +1 · 오늘 {0}회 남음」 | `08_adPlacement modeTicket` 남은 횟수(모드 공용) |
| 목록 | 남은 높이, `ScrollRect` 세로, 사이 1.6u | — | — | 아래 |

### 5.15.3 모드 카드 `ContentCard`

한 장 = `400_content` 한 행(`contentType ≥ 1`, `active 1`, `sort` 순). 높이 약 30u(324), 너비 100%.

| 부분 | 위치·크기 | 스타일 | 글꼴·문구 | 데이터 |
|---|---|---|---|---|
| 카드 | 반지름 2u, 넘침 자름, 테두리 4.5px `--pc` | 바탕 `400_content.bg`(`AddressableTexture`, 가운데 잘라 채움) + 왼쪽→오른쪽 그라데이션 `rgba(20,14,28,.92) → rgba(20,14,28,.35)`(글 가독성) | — | `bg` |
| 아이콘 | 왼쪽 2u, 세로 가운데, 16u(172.8) | 바탕 `mix(--pc 30%, #161b2c)` 반지름 1.6u | — | `400_content.icon` |
| 이름 | 아이콘 오른쪽 2u, 위 2u | `#fff` 그림자 `sh` | D 4.4u `name:<400_content id>` | — |
| 설명 | 이름 아래 .6u, 최대 2줄 | `--ink-dim` | B7 2.4u `description:<400_content id>` | — |
| 정보 칩 줄 | 설명 아래 1u, 가로 사이 1u | 칩: 바탕 `rgba(0,0,0,.45)`, 반지름 1.2u, 여백 .4u 1.2u | B8 2.3u: 입장권 `RewardSlot Inline`(`02_playResource` icon) 「{0}/{1}」(보유/`chargeMax`), `ui.content.next` 「도전 {0}단계」, `ui.content.best` 「최고 {0}단계」(0이면 숨김) | 저장의 `clears[contentId]`, 입장권 |
| 버튼 줄 | 카드 아래 2u, 오른쪽 정렬, 사이 1.2u | — | — | — |
| 도전 | 최소 너비 22u | `PriceButton` 초록(§5.8 행 버튼), 비용 칸 = `spendResourceType/Id/Value` | D 3.6u `ui.content.go` 「도전 {0}단계」 + 비용 아이콘·수 | 비용 부족이면 꺼짐 |
| 소탕 | 최소 너비 15u | `PriceButton`(바탕 `#2f86e6`, 그림자 `#164a8a`) | D 3.2u `ui.content.sweep` 「소탕」 + 입장권 1 | `sweepActive 1` && 최고 ≥ 1일 때만 보임 |
| ▶ +1 | — | `AdButton` 작은 모양 | D 3.2u `ui.content.adPlus` 「▶ +1」 | 남은 광고 횟수 0 또는 입장권이 `chargeMax` 이상이면 꺼짐 |

| 상태 | 조건 | 모양 |
|---|---|---|
| 열림 | `openId`의 `05_contentOpen` 조건 충족 | 위 그대로 |
| 잠김 | 조건 미충족 | 카드 `grayscale(.75) brightness(.7)`, 가운데 자물쇠 7u, 버튼 줄 대신 `#ffd84a` B8 2.6u `ui.content.lockAt` 「Area {0}에서 열려요」. 카드를 누르면 토스트 `toastMessage:contentLocked` |
| 새로 열림 | 열렸고 아직 안 봄 | 카드 오른쪽 위 태그(지도 「현재 위치」 태그 모양 §5.10) `ui.content.new` 「NEW」. 메뉴를 닫을 때 「본 항목」 저장 |
| 2차(`active 0`) | — | 목록에서 숨김([08](08_Content.md) §8.6.3) |

### 5.15.4 시작 흐름

| 순서 | 일 | 실패 시 |
|---|---|---|
| 1 | 「도전」 누름 → 버튼 HardLock | — |
| 2 | 잠금·입장권·비용 확인(MV.Meta `ContentEntry.CanEnter`) | 토스트 `toastMessage:contentNoCost` 「{0}이(가) 부족합니다」(consumeData 이름) |
| 3 | `ContentEntry.TryEnter`: 비용 차감·저장 | — |
| 4 | 메뉴 닫힘 → 로비 숨김 → **Game 씬 안에서** 런 시작(`402_modeRule`의 `IModeRule`, [08](08_Content.md) §8.4.1 ⑤). 시작 배너 = 큰 글 `name:<400_content id>`, 작은 글 `ui.banner.modeStage` 「{0}단계」. BGM = `400_content.bgm` | 시작 오류면 비용 환불 + 토스트 `toastMessage:contentStartFail` 「시작하지 못했습니다 · 입장권을 돌려드렸어요」 |
| 5 | 런 HUD = 모드별(`alleyWar` → `UIHud_Defence`, 그 밖 → `UIHud_Run`) | — |
| 6 | 끝 → `UIPopup_ModeResult`(§5.17.1) → 「받기」 → 로비 → 메뉴 다시 열림(같은 탭·스크롤 위치) | — |

소탕: 확인 창 없이 입장권 1 차감 → `ModeResultPopup`을 「소탕」 제목으로 바로 연다(런 없음). 광고 +1: §5.12 흐름(`modeTicket`, 보상 id 0 = 이 카드의 입장권).

### 5.15.5 「성장」 탭

`05_contentOpen`에서 `contentId 0`이고 메타 시스템 `key`인 행(퀘스트·출석·연구·상점·업적; 2차 펫·친구·장비·패스·축복)을 `sort` 순으로 그린다. 카드는 모드 카드보다 낮은 `MetaRowCell`(§5.16) 모양(높이 13u): 아이콘 9u `05_contentOpen.icon`, 이름 `name:<05_contentOpen id>`, 설명 `description:<05_contentOpen id>`, 오른쪽 빨간 점(받을 보상)과 화살표 「›」. 누르면 그 시스템의 `UIPopup_<이름>`을 메뉴 위에 연다(메뉴는 열린 채). 잠김·NEW·2차 처리는 모드 카드와 같다.

### 5.15.6 표에 필요한 열(08·07 확인)

| 표 | 열 | 05가 쓰는 곳 |
|---|---|---|
| `400_content` | `icon`, `bg`, `banner`, `bgm`, `openId`, `spendResourceType/Id/Value`, `sweepActive`, `sort`, `active` | 카드·결과 머리·시작 |
| `400_content` | `hudId`(08 §8.4.2, 비면 `Run`) | 모드 HUD 고르기 |
| `02_playResource` | `icon`, `chargeMax` | 입장권 칩 |
| `05_contentOpen` | `key`, `contentId`, `icon`, `conditionType`, `conditionValue`, `sort` | 잠금 문구·성장 탭·지도 기능 상자·해금 팝업 |
| `08_adPlacement` | `modeTicket` 행 | ▶ +1 |

## 5.16 메타 공통 패널 틀 — `MetaFrame` / `MetaPanelPopup`

[08](08_Content.md) §8.5의 메타 화면(1차 퀘스트·출석·연구·상점, 2차 펫·친구 고양이·장비·패스·축복)은 모두 이 틀의 프리팹 변형이다. 새 화면 = 프리팹 변형 1개 + `IMetaPanelSource` 구현 1개 + `05_contentOpen` 행 1개.

| 요소 | 위치·크기 | 스타일 | 글꼴·문구 |
|---|---|---|---|
| 막 | Canvas 전체 | `rgba(10,8,16,.74)`, 누르면 안 닫힘 | — |
| 상자 | 가운데, 94u × 150u(1015.2×1620), 여백 3u, 세로 사이 1.4u | `.pnl` 상자 바탕, 테두리 4.5px `--pc`(시스템마다 `05_contentOpen` 행 색 또는 기본 `#9fc4ff`), 반지름 3u | — |
| 제목 | 위 가운데 | `--pc` | D 6u `name:<05_contentOpen id>` |
| 닫기 | `CloseX` | — | — |
| 재화 줄 | 제목 아래, 오른쪽 정렬 사이 1u | `CurrencyChip` 작은 모양(아이콘 3.6u, D* 3u) | 그 화면이 쓰는 재화(`IMetaPanelSource.Currencies`) |
| 부제·시간 | 재화 줄 아래 | `.sub` | 예 `ui.meta.resetIn` 「초기화까지 {0}」(`hms`) |
| 탭(선택) | 최대 5칸 격자, 사이 1u | 야옹신 탭 모양, 빨간 점 2.4u | B8 2.4u |
| 본문 | 남은 높이, `ScrollRect`(재사용 풀) | — | — |
| 아래 줄(선택) | 상자 아래, 너비 100% | `GoldButton` | `ui.meta.claimAll` 「모두 받기」 등 |
| 빈 상태 | 본문 가운데 | `--ink-dim` B8 3u | `ui.meta.empty` 「지금은 할 일이 없어요」 |

본문 칸 종류:

| 칸 | 모양 | 용도 |
|---|---|---|
| `MetaRowCell` | `.row2`(§5.8): 아이콘 9u, 제목 B7 3.4u, 설명 B7 2.6u `--ink-dim`, 선택 진행 막대(높이 1.7u, 지도 진행 막대 색), 오른쪽 행동 버튼(`PriceButton`/`AdButton`/`GoldButton`/완료 표시 `ui.meta.done` 「완료」 회색) | 퀘스트·연구·업적·상점 상품 |
| `MetaGridCell` | `.gpc`(§5.7) 3칸 격자 칸 | 출석 칸, 펫·장비 목록 |
| `MetaHeaderCell` | `.gp-h`(§5.7) | 묶음 제목(일일/월간, 상점 구역) |
| `RewardSlot` 줄 | 행 안 보상 가로 나열(Inline·Badge) | 퀘스트·출석·상점 보상(consumeData) |

데이터 계약 `IMetaPanelSource`(MV.Meta 구현, MV.UI 사용): `Title`, `Tabs`, `Currencies`, `Rows(tab)`(칸 종류·아이콘·문구 키·진행·보상 consumeData 목록·행동 종류·가격 consumeData·상태), `OnAction(rowId)`, `ResetAt`. 화면은 이 결과만 그린다. 결제(상점 유료 상품)는 Framework 결제 스텁을 부르고 화면은 「준비 중」 토스트만 낸다([01](01_Scope.md)).

## 5.17 모드 전용 화면

### 5.17.1 모드 결과 — `UIPopup_ModeResult`

`ModeResultPopup : UIPopup`, 층 300. 결과(§5.6)와 같은 막·상자·보상 칸 모양을 쓰고 아래만 다르다. **×3 광고 버튼 없음**([08](08_Content.md) §8.4.1 ⑥).

| 요소 | 값 |
|---|---|
| 머리 그림 | 상자 위 56u×20u, 반지름 2u, `400_content.banner`(`AddressableTexture`) |
| 제목 | 클리어 `ui.modeResult.clear` 「{0}단계 클리어!」(gold) / 실패 `ui.modeResult.fail` 「도전 실패」(`#ffb08a`) / 소탕 `ui.modeResult.sweep` 「{0}단계 소탕」(gold) |
| 부제 | B8 3.4u `name:<400_content id>` |
| 보상 칸 | `RewardSlot Card`, 3칸 격자, 넘치면 줄바꿈(최대 6). 실패면 `ui.modeResult.noReward` 「보상 없음」 |
| 통계 | 모드가 준 줄(`IModeRule.Settle` 결과의 문구 키·인자), B7 2.7u `--ink-dim` |
| 받기 | `GoldButton` D 5u `ui.modeResult.ok` 「받기」 → MV.Meta 지급 → 로비 → 컨텐츠 메뉴 다시 열기 |
| 소리 | 클리어 `win`, 실패 `fail` |

### 5.17.2 골목 수비전 HUD — `UIHud_Defence`

`DefenceHud : UIHud`, 층 200. `UIHud_Run`의 왼쪽 위 블록·재화 칩 대신 아래를 쓴다. 무기 묶음(§5.2.6, 손패 포함)·가호 줄(§5.2.7)·떠오르는 숫자·날아가는 아이콘은 그대로 재사용한다. 계단 상자·계단 게이지·경고·등급 미터·가호 카드 룰렛은 없다.

| 요소 | 위치·크기 | 스타일 | 글꼴·문구 | 데이터 |
|---|---|---|---|---|
| 단계 | left 3u, top 3u | 그림자 `sh` | D 4.6u `ui.defence.stage` 「{0}단계」 + B8 2.5u `name:<400_content id>` | 단계 |
| 시간 | 가운데, top 3u | 계단 상자 모양(§5.2.3) 너비 30u | D* 6u `#ffd27a` `mm:ss`(남은 생존 시간, 0이면 `ui.defence.finish` 「남은 쥐 처치!」) | `survivalSec − 경과` |
| 가게 HP `DefenceBar` | 가운데, top 13u, 60u×3.2u | 막대(§5.6 진행 막대 모양), 채움 `#e8504a → #ff9a5a`, 칸 구분선 HP 수만큼 | 왼쪽 B8 2.4u `ui.defence.shop` 「생선 가게」, 오른쪽 D* 「{0}/{1}」 | 가게 HP |
| 방어선 | 월드 쪽(02·06): z = `arenaLineZ`의 빨간 점선 | — | — | — |
| 남은 쥐 | right 3u, top 3u, `CurrencyChip` 모양 | 아이콘 `102_enemy[mouseDefence].icon` | D* 「{0}」 | 생존 + 남은 스폰(목표 처치 미달이면 「{처치}/{minKills}」) |
| XP 막대 | left·right 3u, bottom 15u, 높이 2u | 바탕 `rgba(255,255,255,.12)`, 채움 `#5fd0ff → #c08aff` | 왼쪽 위 D 2.8u `ui.defence.lv` 「Lv {0}」 | XP·레벨 |
| 피해 | 가게 HP가 줄 때 | HP 막대 흔들림(±1u, .25s) + 화면 가장자리 빨간 비네트 .3s(불투명 .35 → 0) | — | `ShopHit` 이벤트 |

### 5.17.3 카드 고르기 — `UIPopup_CardPick`

`CardPickPopup : UIPopup`, 층 500. 수비전 레벨업(대기열에서 하나씩)과 택배 상자 외 모든 「3택」에 쓴다. **열려 있는 동안 런은 일시정지**(§5.18).

| 요소 | 값 |
|---|---|
| 막 | `rgba(5,4,8,.72)`, 누름 막음 |
| 제목 | 가운데 top 30%, D 6u `--gold` 그림자 `sh` `ui.cardPick.title` 「가호를 하나 고르세요」 + B8 2.6u `ui.cardPick.sub` 「Lv {0}」 |
| 카드 3장 | 가운데(50%, 50%), 가로 사이 3u. 카드 모양은 가호 카드 `.hcard` 가운데 모드(§5.2.9, 25u×33u)를 그대로 쓰고 아래 설명 B7 2.2u `#fff` `description:<110_card id>` 2줄을 더한다(카드 높이 38u) |
| 나타남 | 카드마다 0.08초 간격 `deal`(§5.5.2와 같은 키프레임, .25s) |
| 고름 | 누른 카드 `chosen`(§5.2.9) .8s + 소리 `card`, 나머지는 불투명 0으로 .2s → .5초 뒤 닫힘 → 카드 날아가기(§5.2.9 `flycard`) → 가호 칸 `arrive` → 일시정지 해제 |
| 대기열 | 레벨업이 여러 번이면 닫힌 뒤 바로 다음 3택 |

## 5.18 일시정지 정의

[06](06_Effects.md) D-47과 [02](02_Visual.md)이 이 절을 따른다.

| 원인 | 런 진행(MV.Run Tick) | 런 이펙트(06, 게임 시간) | UI 실시간 연출(떠오르는 숫자·날아가는 아이콘·HAND·배너·플래시·토스트·등급 미터) | 입력 |
|---|---|---|---|---|
| 앱 백그라운드(`OnApplicationPause(true)`) | 멈춤 | 멈춤 | 멈춤 | — |
| 카드 고르기 팝업(§5.17.3) | 멈춤 | 멈춤 | 멈춤. **팝업 자체 연출만 진행** | 팝업만 |
| 테스트 패널·소리 버튼(런 중) | 계속(정본: 패널이 열려도 런이 돈다) | 계속 | 계속 | 패널·버튼, 그 밖 드래그 계속 |
| 로비·결과의 팝업 | 런 없음 | — | 계속 | — |
| 히트스톱([06](06_Effects.md)) | 멈칫 | 멈칫 | **계속**(정본대로) | 계속 |

구현: `MV.View`의 `UiClock`이 실시간 `deltaTime`(`Time.unscaledDeltaTime`)을 내고, 일시정지 원인 집합이 비어 있지 않으면 0을 낸다. `MvTween`·`FloatTextHud`·`FlyIconLayer`·`HandWidget`·배너·플래시·토스트는 모두 이 시계만 쓴다. 일시정지를 거는 팝업은 자기 연출에 원래 실시간 시계를 쓴다(`MvTween.UseClock(Clock.Unscaled)`). 앱 복귀 때 런을 바로 다시 돌리지 않고 1프레임 뒤 재개한다(긴 dt 방지; dt 상한은 [04](04_GameRules.md)). 정본에는 일시정지가 없다 — 위 표는 [창작].

## 5.19 의문점·결정 필요

| ID | 질문 | 선택지 | 권장 | 영향 |
|---|---|---|---|---|
| D-30 | 화면비·안전 영역에서 UI 배치 | ① 9:16 고정 상자(정본 픽셀 동일, 긴 폰에서 UI 위아래가 빔) / ② 폭 1080 고정·세로 늘림(% 위치는 비례, u 위치는 가장자리) / ③ 화면 전체(안전 영역 무시) | **②** — [02](02_Visual.md) D-41 ②와 짝. 9:16에서는 ①과 같다 | §5.1.2. ①이면 02의 월드 확장과 어긋나 UI 밖에 월드만 보인다 |
| D-31 | CSS 고정 px 환산 | ① ×3(휴대폰 모습) / ② ×1(1080 기준 그대로, 테두리가 매우 얇음) / ③ u 비례 | **①** | 모든 테두리·그림자·외곽선 두께 |
| D-32 | 정본 층 이상 고치기 | 토스트를 맨 위로(800) · 손패 머지 카드를 HUD 층 안에 가둠 / 정본대로 | **고침** | 정본에서 토스트가 야옹신 패널 막 뒤에 깔리는 것만 달라진다 |
| D-33 | `#aupFree` 색 | ① 정본대로 초록(행 버튼 규칙이 이김) / ② 의도대로 노랑 광고 버튼 | **① 정본대로** | 「광고 버튼은 노랑」 통일을 원하면 ② |
| D-34 | 이모지 그림 | ① Noto Color Emoji(SIL OFL) 잘라 씀 / ② 새로 그림 / ③ 글꼴 그대로 | **①**(안드로이드 정본 모습) | 03 `UI_Emoji_*` 원본 |
| D-35 | 메타 시스템 입구와 컨텐츠 버튼 잠금 | ① 컨텐츠 메뉴 「모드」·「성장」 2탭, 버튼은 어느 탭이든 열리면 켜짐 / ② 메뉴는 모드만(08 §8.6.3: Area 3까지 버튼 잠김), 메타는 로비 사이드 버튼 따로 / ③ 메타를 지도 화면에 | **①** — 사용자 원문(컨텐츠 버튼 → 메뉴 → 눌러 시작)에 메타도 같은 입구로 모음. 08은 버튼을 5001003으로 잠그므로 ①이면 08 §8.6.3 문장 수정 필요 | 로비 버튼 수·08 해금 흐름 |
| D-36 | 정본 버그 | 부활 「한 번 더」 문구 · 결과 통계 `R.hitWall` 예외 / 정본대로 | **고침** | §5.6, §5.9. 수치·흐름 변화 없음 |
| D-37 | UI 색 공간·반투명 섞기 | ① Gamma(브라우저와 같은 sRGB 섞기) / ② Linear + UI 별도 처리 | [02](02_Visual.md) 결정을 따름. Linear이면 UI 카메라·Canvas를 sRGB 섞기로 맞추는 처리가 필요 | 모든 반투명 판·그라데이션 |
| D-38 | 합성 볼드 재현 | ① TMP Bold 스타일로 근사 / ② 무시(얇게) | **①** | `D*` 표시 글 |
| D-39 | 막혀서 안 보이는 연출 | ① 정본대로 넣지 않음(`glv` 반짝임, `gp2` 흰 테두리, 건조대 `tick`) / ② 의도대로 넣음. 함께: 움직임 줄이기 설정 | **① 정본대로**, 움직임 줄이기는 1차 제외 | 캡처 대조 기준 |

참고: 연출 구현 방식 D-07(자체 코루틴 트윈 `MvTween` 권장, DOTween Pro는 선택)은 [10](10_Architecture.md)·[13](13_Decisions.md) 소관.

### 5.19.1 다른 문서에 기대한 가정

| 문서 | 가정 |
|---|---|
| [02](02_Visual.md) | 월드는 화면 전체·UI는 안전 영역(D-41 ②), 지원 범위 밖 레터박스는 02가 그림, `WorldToStage` 제공, 색 공간(D-37), 수비전 방어선 그림 |
| [03](03_ResourcePipeline.md) | 아틀라스 `Atlas_UI_Common/Hud/Lobby/Icon/Card`와 스프라이트 이름, TMP 글꼴 자산 이름, TMP 스프라이트 자산 `TMP_Inline`, 머티리얼 프리셋 `TMP_Mat_*`, 컨텐츠·메타 아이콘 이름 규칙(03 기준 `UI_Icon_*`·`Tex_*`로 통일함. 모드 아이콘 `UI_Icon_Mode_*`, 모드 배경 `Tex_Mode_*`) |
| [04](04_GameRules.md) | 표시 등급 `HandDisplayModel` 입력 이벤트, 런 dt 상한, 일시정지 중 Tick 정지 |
| [06](06_Effects.md) | 팝업 글·배너 문구 목록, 플래시 호출, 히트스톱은 UI 시계에 영향 없음 |
| [07](07_Tables.md) | `09_uiResource` 표(제안, 번호 확정), `103_item.lookName`, `301_zone.mapCol1/mapCol2`, `13_areaUpgrade`·`14_rebirthPerk` 값 표기 열, `300_runConfig` `ui.hand.*` 키, `07_consumeType.iconAtlas/iconColumn`, `08_adPlacement` 행(dia·skip·collect3·interstitial·modeTicket) |
| [08](08_Content.md) | `400_content.hudId`(제안)·`banner`, `05_contentOpen` 메타 행의 `key`·아이콘·문구, D-35 결과에 따른 §8.6.3 수정, 메타 화면 `IMetaPanelSource` 내용 |
| [09](09_Save.md) | 본 해금·본 컨텐츠(NEW), 모드 단계·입장권·광고 횟수(날짜 기준), `settings.muted`, 컨텐츠 메뉴 마지막 탭 |
| [12](12_Acceptance.md) | 9:16 1080×1920에서 정본 캡처 대조, 이 문서의 「만들지 않는 것」 목록 |
