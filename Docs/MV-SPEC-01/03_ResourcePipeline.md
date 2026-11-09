# 3. 리소스 파이프라인

[입구](README.md) · 앞: [02 비주얼](02_Visual.md) · 다음: [04 게임 규칙](04_GameRules.md)

정본 `MoonVillage.html`에는 이미지·오디오 파일이 하나도 없다. 모든 그림은 Canvas 2D 코드(`mochi`, `drawJunk`, `drawWell` …)가, 모든 소리는 WebAudio 합성(`SND`)이 만든다. 이 절은 그 코드를 **그대로 실행해** PNG·WAV로 굽고, Unity에서 아틀라스·Addressables·표로 연결하는 방법을 정한다.

사용자 원문(2026-11-09): 「리소스도 달동네판에 들어가있는 2d리소스를 활용해서 유니티로 포팅」, 「이미지 리소스 사용은 최적화도 고민해야해 아틀라스 사용같은것들」, 「AddressableImage, AddressableTexture 사용할거야」, 「리소스도 테이블에서 설정가능해야해」, 「테이블 구조는 정크버스터즈 참고」.

표 이름은 [07](07_Tables.md)의 정크버스터즈 방식 목록을 쓴다. **별도의 「논리 키 → 주소」 표는 두지 않는다.** 리소스는 각 표의 열(`icon`, `sprite`, `projSprite`, `bgm`, `hitSound` …)에 **스프라이트 이름 또는 Addressables 주소**를 직접 적는다(§3.8.6).

## 3.1 개요와 원칙

### 3.1.1 흐름

```text
정본 HTML (수정 금지, SHA256 고정)
  └─ Tools/MvExtract (Node + Playwright 헤드리스 Chromium)
       ├─ 그림: 주입 스크립트가 그리기 함수를 함수·인자별로 호출 → PNG + manifest.json
       └─ 소리: SND를 OfflineAudioContext로 렌더 → WAV
  └─ Assets/Game/Art/Source/        (추출 원본. Addressables 밖)
       ├─ Sprite/<아틀라스>/<스프라이트 이름>.png
       ├─ Texture/<주소>.png
       └─ Audio/{Sfx,Music}/<주소>.wav
  └─ MV.Editor 임포터(AssetPostprocessor)가 manifest로 피벗·PPU·9-slice 경계 적용
  └─ SpriteAtlas v2 (Addressables/Remote/UIAtlas, WorldAtlas) → Addressables 주소
  └─ 표 열(스프라이트 이름·주소) → 런타임 해석(§3.8.6)
```

### 3.1.2 원칙

| # | 원칙 | 이유 |
|---|---|---|
| P1 | **그림은 정본 함수를 그대로 실행해 굽는다.** 손으로 다시 그리거나 다른 도구로 흉내 내지 않는다 | 「겉은 정본과 같게」. 같은 브라우저·같은 코드면 화소가 같다(§3.11) |
| P2 | **런타임 벡터 재현은 하지 않는다.** Unity에서 Canvas 2D API(경로·베지어·외곽선)를 다시 구현해 매 프레임 그리지 않는다 | ① 모바일에서 매 프레임 CPU 래스터화는 비싸고 GC가 생긴다 ② 같은 결과를 내려면 Chromium의 안티에일리어싱·선 이음·그라데이션 규칙까지 맞춰야 해 검증이 끝나지 않는다 ③ 아틀라스·배칭·압축 같은 최적화를 쓸 수 없다 |
| P3 | 단, **색·크기가 표에서 바뀌는 단순 도형**(지면 띠, 집 정면·윗면 `drawBox`, 담장 면, 게이트 배경)은 굽지 않고 [02](02_Visual.md)의 메쉬·정점색으로 그린다(D-59) | 크기가 연속값(집 폭 4.2~7.0 m 등)이고 색이 테마 표 값이라 조합을 다 구울 수 없다. 단색·2색 선형 그라데이션은 정점색으로 정확히 같다 |
| P4 | 동적 글자(라벨·숫자·문구)는 굽지 않고 TMP로 쓴다(§3.7) | 표·다국어 연결, 숫자 변경 |
| P5 | 애니메이션은 시간 인자 t·위상 ph를 넣어 **N프레임 샘플**한다. 몸 전체에 걸리는 이동·확대(눌림 `sq`, 뜸 `lift`, 흔들림)는 가능하면 굽지 않고 Transform으로 한다 | 프레임 수·메모리 절약 |
| P6 | **화면 밖·호출 안 되는 함수(잔재)는 굽지 않는다**(§3.5.10) | 범위 고정 |
| P7 | 리소스 이름·아틀라스·주소는 **표 열에서 정한다**. 코드에 스프라이트 이름·아틀라스 switch를 넣지 않는다 | 사용자 지시 「리소스도 테이블에서 설정가능해야해」 |
| P8 | 테마 변형은 **스프라이트 이름을 같게** 두고 `304_theme.worldAtlas`만 바꾼다 | 표 한 칸으로 테마 교체·추가 |

## 3.2 추출 도구 `Tools/MvExtract/`

### 3.2.1 구성과 설치 — 결정 대기(D-50)

| 항목 | 값 |
|---|---|
| 위치 | 새 프로젝트 저장소 `Tools/MvExtract/`(Unity `Assets` 밖). 작업 계약 MV-TOOL-ART(그림), MV-TOOL-SND(소리, 하위 `snd/`) |
| 런타임 | Node.js LTS(22.x), `playwright` 고정 버전(예: 1.4x.y, `package-lock.json` 고정) + Playwright가 내려받는 Chromium 고정 빌드 |
| 외부 파일 | ① three.js r128 `three.min.js`(정본 L729가 cdnjs에서 읽음) ② Google Fonts의 Black Han Sans·Gothic A1(400/700/800/900 요청분) 글꼴 파일 ③ Noto Color Emoji(D-52) |
| 승인 | 설치·내려받기는 외부 설치이므로 **사용자 승인 필요(D-50)**. 승인 전에는 도구 코드·입력 정의만 만들고 실행은 NOT_RUN |
| 오프라인 실행 | 위 ①②③을 `Tools/MvExtract/vendor/`에 두고 `page.route`로 cdnjs·fonts.googleapis.com·fonts.gstatic.com 요청을 로컬 파일로 돌린다. 실행 중 외부 네트워크 접속 0 |
| 명령 | `npm run art -- [--only <glob>] [--group <아틀라스>]`, `npm run snd -- [--only <key>]`, `npm run verify` |

폴더:

```text
Tools/MvExtract/
  package.json, package-lock.json
  canon.json            정본 경로·SHA256(fe573cf2…26dfa)
  vendor/               three.min.js, fonts/*.woff2, NotoColorEmoji.ttf
  inject/
    init.js             addInitScript: 시계·난수·rAF·AudioContext 고정(3.2.2)
    harness.js          addScriptTag: 작업 실행기(3.2.4~3.2.5)
  jobs/
    world_*.json  ui_*.json  lobby_*.json  fx_*.json  snd.json
  out/                  (git 제외) 임시 출력
  run_art.mjs  run_snd.mjs  verify.mjs
```

### 3.2.2 결정성 장치 (같은 입력 → 같은 바이트)

| 장치 | 방법 |
|---|---|
| 정본 고정 | 시작 시 정본 SHA256을 `canon.json`과 비교. 다르면 중단(`--accept-canon <sha>`로만 갱신) |
| 브라우저 | Playwright 고정 Chromium, `--disable-gpu --force-color-profile=srgb --font-render-hinting=none`, `deviceScaleFactor: 1`, 뷰포트 1080×1920 |
| 시계 | `init.js`가 `performance.now`를 `window.__clock`(초 단위 설정값×1000)으로 바꾼다. `requestAnimationFrame`은 저장만 하고 부르지 않는다(메인 루프 `frame` L3823 정지) |
| 난수 | `Math.random`을 시드 고정 PRNG(mulberry32, 시드 = job 이름 해시)로 바꾼다. 그림 함수는 대부분 `hash()`라 결정적이고, 소리 잡음 버퍼(L1634)·잡음 시작 위치(L1645)만 영향 |
| 저장 | 새 브라우저 프로필(로컬 저장 비어 있음 → 기본 `save`, `ce_mute` 없음) |
| 글꼴 | `document.fonts.ready` 뒤 `document.fonts.check('900 20px "Gothic A1"')` 등 4개 확인. 실패하면 중단 |
| 출력 | PNG는 `canvas.toDataURL('image/png')`의 바이트를 그대로 쓴다. 같은 작업 2회 실행 해시 비교가 `npm run verify`의 1단계 |

### 3.2.3 정본 로드와 주입

1. `page.addInitScript(init.js)` → `page.goto(file:///…/MoonVillage.html)` → `boot`(L3878)까지 끝나기를 `window.SND && window.camera` 확인으로 기다린다.
2. `page.addScriptTag(harness.js)`. 주입 코드는 **고전 스크립트**라 정본의 전역 바인딩을 그대로 쓴다.
   - 정본 스크립트는 `'use strict'` IIFE 안이라 `function` 선언(`pj`, `E`, `RR`, `PL`, `LN`, `star4`, `drawFac`, `mochi` …)이 `window` 속성이 **아니다**. 그래서 도구가 응답 본문의 그 IIFE 끝에 `window.__mvScope = code => eval(code)`(`inject/scope_hook.js`)를 붙여 넣고, 주입 코드는 `__mvScope`로 그 범위의 이름을 읽고 **바꿔 끼운다**(3.2.5 억제 규칙의 근거). 디스크의 정본 파일은 그대로다.
   - `let` 전역(`W`, `H`, `R`, `CN`, `save` 내부 값)은 대입할 수 있다(L942 `let W = 360, H = 640`).
   - `const` 전역(`CTX` L2792, `OLC` L2804, `CAT_PAT`, `AREA_TH`)은 바꿀 수 없다. 단 배열·객체의 **내용**(예: `AREA_TH[i].door`)은 바꿀 수 있다 → 새 테마 색 주입에 쓴다(§3.6.4).
3. 정본 파일 자체는 한 글자도 고치지 않는다.

### 3.2.4 그리기 방식 5종

| 방식 | 대상 함수 | 작업면 | 크기·위치 지정 |
|---|---|---|---|
| `direct` | 컨텍스트 `g`를 인자로 받는 함수: `mochi`(L3267), `mochiBack`(L3679), `drawFac`(L3399), `facCushion`(L3396), `fishShape`(L3397), `drawProjShape`(L3368), `paintWeapon`(L3388), `paintCard`(L873), `paintGod`(L2481), `churu`(L3737), `catnipLeaf`(L3741), `star4`(L3261), `E`/`RR`/`PL` | 새 오프스크린 `<canvas>` | 인자 `X, Y`=피벗 화소, `s`=스케일(§3.3) |
| `ctx` | 전역 `CTX`에 그리는 함수: `drawMouse`(L3652), `brickBit`(L3605), `fishShapeW`(L3602), `drawPawFx`(L3747), `drawHissFx`(L3753), `drawAzit`(L3474) | 주 캔버스 `cv`. `cv.width/height`를 작업 크기로 바꾸고 `CTX.setTransform(1,0,0,1,0,0)` 뒤 `clearRect` | 인자로 직접 지정. `drawPawFx`·`drawHissFx`는 `pp={x,y,d:15,s}` 가짜 투영점 |
| `project` | 내부에서 `pj()`로 위치를 구하는 함수: `drawJunk`(L3607), `drawRollGate`(L3632), `drawWell`(L3644), `drawWarpGate`(L3211), `drawBlackHole`(L3203), `drawMoonProp`(L3556), `drawObj`의 coin·gem 분기(L3108~3116) | `cv` | 아래 「투영 감싸기」 |
| `svg` | CSS의 SVG data URI 아이콘 4종(A 노트 §3: `.ic-gold` L599, `.ic-soul` L46, `.ic-dia` L600, `.ic-stair` L589) | 오프스크린 캔버스 | `<img src=data:…>`를 목표 크기로 `drawImage` |
| `dom` | CSS로만 그린 판·틀·막대(UI 스킨, §3.5.8)와 `.ic-brick`(L42) | 빈 `<div>`에 해당 CSS를 입힘 | `locator.screenshot({omitBackground:true})` |

**투영 감싸기(`project` 방식).** 정본 카메라를 런 기준 자세로 둔다: `W=1080; H=1920; camera.aspect=9/16; camera.updateProjectionMatrix()`, 카메라 위치 게임 좌표 `(0, 13, −9)`, 주시점 `(0, 0.5, 5.5)`(범위 밖 L2777~2778의 `ck=0, camLift=0, px=0, pz=0` 값), `camera.updateMatrixWorld(true)`. 그다음 `window.pj`를 감싼다.

```js
const pj0 = window.pj;
window.pj = (x, y, z) => {                    // three 좌표
  const p = pj0(x, y, z); if (!p) return null;
  return { x: AX + (p.x - A.x)*k, y: AY + (p.y - A.y)*k, d: p.d, s: p.s*k };
};
// A = pj0(앵커), k = S / (A.s * norm), (AX, AY) = 출력 캔버스의 피벗 화소
```

- 앵커는 job에 적은 **게임 좌표 점**(함수가 그리는 기준점과 같게. 예: `drawJunk`는 `(wp.x + jx, wp.y + 0.05, wp.z − 0.45)` L3608).
- 오브젝트는 플레이어 줄 `z = 0`에 둔다. 이때 앵커 깊이 `d ≈ 15.3`, `s ≈ 128.7 px/m`라 `k ≈ 1`이다(§3.3). 원근이 있는 부분(우물 벽돌 고리, 담장 면)도 실제 카메라 각도로 그려진다.
- 안개 알파 `1 − fogK(d)`는 `d < 34`이므로 1이다(L2803).
- `norm`은 함수가 자체적으로 곱하는 무작위 배율을 지운다. `drawJunk`: `0.9 + hash(hs + 3)*0.18`(L3608). 무작위 x 흔들림 `jx`도 앵커에 더해 지운다. 이 값들은 Unity에서 같은 공식으로 Transform에 적용한다([04](04_GameRules.md)).

### 3.2.5 억제 규칙(레이어 분리)

한 함수가 여러 부품을 한 번에 그릴 때, 부품을 나눠 굽기 위해 **전역 도우미 함수나 컨텍스트 메서드를 잠시 바꿔 끼운다.** job에 선언형으로 적는다.

```json
"suppress": [
  { "fn": "star4", "argIndex": 4, "in": ["#fff6a0"] },
  { "fn": "E", "argIndex": 5, "in": ["#ff6a5a","#ffd84a","#6ad0ff","#8af06a"], "rMax": 4 },
  { "ctx": "arc" },
  { "ctx": "fillRect", "fillIsGradient": true, "full": true },
  { "fn": "drawFac" }, { "fn": "mochi" }, { "fn": "facCushion" }, { "ctx": "fillText" }
],
"only": null
```

- `suppress`: 조건에 맞는 호출을 건너뛴다. `only`: 조건에 맞는 호출만 그린다(나머지 건너뜀). 둘 다 `fn`(전역 함수 이름) 또는 `ctx`(캔버스 메서드) 대상, 조건은 인자 값 비교(`in`, `eq`, `rMax` 반지름 상한, `fillIsGradient`, `full`=캔버스 전체 사각형).
- 바꿔 끼운 함수는 작업이 끝나면 원래 함수로 되돌린다.
- 쓰는 곳(§3.5): t=8 고양이의 회전 별(색 `#fff6a0`, L3732·mochi L3361)을 몸 그림에서 빼기, 로비 방 배경에서 전구·창밖 눈·난로 빛·비네트·시설·글자 빼기, 집 부품(문·창·물탱크·장독) 하나씩 뽑기.
- **조건이 맞는 호출이 0건이면 실패**로 처리한다(정본이 바뀌어 규칙이 헛도는 것을 막음).

### 3.2.6 입력 정의 파일 (jobs/*.json)

작업 하나 = 출력 스프라이트(또는 프레임 묶음) 하나.

```json
{
  "name": "World_Obj_Junk_Parcel",
  "atlas": "Atlas_World_Theme",
  "themeSet": "all",
  "mode": "project",
  "call": "drawJunk",
  "args": { "o": { "idx": 0, "bar": 0, "lane": 0, "row": 0, "rew": "gold", "shake": 0 },
            "wp": { "x": 0, "y": 0, "z": 0 } },
  "anchor": { "x": "jx", "y": 0.05, "z": -0.45 },
  "norm": "0.9 + hash((o.idx||0)*7.13 + (o.bar||0)*3.1 + 3)*0.18",
  "ppu": 128, "scaleK": 1.0,
  "canvas": { "w": 304, "h": 248, "pivotPx": [152, 232] },
  "pivot": "BottomCenter",
  "border": null,
  "frames": null,
  "suppress": [], "only": null,
  "clock": 0.5,
  "src": "L3607~3630"
}
```

| 필드 | 뜻 |
|---|---|
| `name` | 스프라이트 이름(§3.4.1). 파일 이름 = `name.png` |
| `atlas` | 기본 아틀라스(§3.8.1). `Atlas_World_Theme`는 테마마다 하나씩 굽는다는 뜻 |
| `themeSet` | `all`(모든 테마 아틀라스에 같은 이름으로 굽기) / `each`(테마 색으로 테마마다 다시 그림) / `none`(테마 무관) |
| `mode` | 3.2.4의 5종 |
| `call`, `args` | 호출할 정본 함수와 인자. 문자열 인자 `"$theme"`는 현재 테마 객체(`AREA_TH[i]`), `"$t"`는 프레임 시간 |
| `anchor`, `norm` | `project` 전용(3.2.4) |
| `ppu`, `scaleK` | 그릴 때 `s = ppu × scaleK`(§3.3). Unity 스프라이트 PPU는 `ppu` 그대로 → 화면에서 `scaleK`배로 보인다 |
| `canvas` | 출력 크기(px)와 피벗 화소. **프레임 묶음은 모든 프레임이 같은 캔버스·같은 피벗**(트림하지 않음) |
| `pivot` | Unity 피벗 이름(§3.4.3). 숫자 피벗은 `pivotPx / canvas`로 계산해 manifest에 적는다 |
| `border` | 9-slice 경계 `[L, B, R, T]` px. UI 스킨·가변 폭 그림만 |
| `frames` | `{ "var": "ph"|"t", "from": 0, "period": 6.2832, "count": 12, "fps": null, "extra": {"thr": [0, 0.12]} }` — §3.4.4 |
| `clock` | `__clock` 초. 시간 함수인데 정지 그림이면 고정값 |
| `src` | 근거 줄. manifest·검수에 그대로 실음 |

테마 반복: `themeSet: "each"`인 job은 `304_theme`의 행마다 1회 실행한다. 실행 전 `AREA_TH`에 그 행 색을 넣고(§3.6.4) 출력 경로를 `Sprite/Atlas_World_Theme_<key>/`로 바꾼다. `all`은 그림은 한 번 그리고 같은 PNG를 모든 테마 폴더에 복사한다.

### 3.2.7 출력 규칙

| 항목 | 규칙 |
|---|---|
| 경로 | `Assets/Game/Art/Source/Sprite/<아틀라스 이름>/<스프라이트 이름>.png`. 테마 아틀라스는 `Atlas_World_Theme_<key>/`. 단독 텍스처는 `Art/Source/Texture/<주소>.png`. 소리는 `Art/Source/Audio/Sfx|Music/<주소>.wav` |
| 형식 | PNG RGBA 8bit, 직선(straight) 알파, sRGB. 캔버스 출력 그대로 |
| 크기 | 4의 배수로 올림(ASTC 블록 정렬, §3.8.3). 늘어난 칸은 투명 |
| 여백 | 그림 바깥 투명 여백 최소 2 px를 캔버스에 포함(외곽선 안티에일리어싱 보존). 아틀라스 패딩은 별도(§3.8.2) |
| manifest | `Art/Source/manifest.json` 한 파일. 항목: `name, atlas, theme, file, w, h, pivot[x,y](0~1), ppu, border, frames[{file, t}], fps, src, args, sha256` |
| 목록 | `Art/Source/sprite_catalog.tsv`(아틀라스 → 스프라이트 이름 목록). 표 검증기가 읽는다(§3.8.6) |
| 실패 | 함수 예외, 빈 그림(알파 0), 억제 규칙 0건 일치, 캔버스 밖으로 잘린 그림(가장자리 2 px 안에 알파>0) → 해당 job 실패, 전체 종료 코드 1 |

### 3.2.8 Unity 임포트 연결

`MV.Editor`의 `MvArtPostprocessor : AssetPostprocessor`가 `Art/Source/Sprite/**` PNG를 들여올 때 manifest를 읽어 적용한다: Texture Type Sprite(2D and UI), Single, `spritePixelsPerUnit = ppu`, 피벗, 9-slice 경계, `Mesh Type`(월드 Tight / UI Full Rect), `Generate Physics Shape` 끔, `Read/Write` 끔, `Mipmap` 끔(아틀라스가 결정, §3.8.4). manifest에 없는 PNG는 경고. 아틀라스 소속은 폴더로 정한다(SpriteAtlas v2의 Objects for Packing = 해당 폴더).

## 3.3 해상도 기준

### 3.3.1 공식

정본 투영 `s = P[5]·H/2/d`(L2797), `P[5] = 1/tan(26°) = 2.0503`(카메라 FOV 52°, 범위 밖 L924). 기준 화면 **1080×1920**에서 `s = 1968.4 / d` px/m.

| 위치 | 뷰 깊이 d (m) | s (px/m, 1920 기준) | 근거 |
|---|---|---|---|
| 화면 맨 아래 지면(z ≈ 플레이어 −3.4 m) | 12.7 | 155 | 아래 광선 66.8° [계산] |
| 플레이어 발(z = 플레이어) | 15.3 | **128.7** | E 노트 §4.3 [계산] |
| 플레이어 몸 중심(y 0.7) | 14.85 | 132.6 | |
| 관문 캣타워 위 발판(관문이 3 m 앞일 때, y 4.6) | 14.3 | 137.7 | L3211 숨김 규칙 |
| 가까운 집 앞면 아래(z ≈ −6, 화면 아래 모서리) | 10.8 | 182 | 집은 메쉬(D-59) |
| 안개 시작 | 34 | 57.9 | L2803 |
| 안개 끝(알파 0) | 84 | 23.4 | L2803 |

정본 자체의 선명도: 캔버스 해상도 `PR2 = min(2, dpr)`(L920) → 폭 360 CSS px 휴대폰에서 정본은 720×1280 px로 그린다. 즉 플레이어 발 기준 정본 실제 해상도는 약 **86 px/m**다.

### 3.3.2 PPU와 여유 배율

| 구분 | 값 | 이유 |
|---|---|---|
| 월드 PPU | **128** (1 Unity 단위 = 1 m) | 1080×1920 기준 플레이어 깊이 128.7 px/m와 1:1. 정본 실제 해상도(86 px/m)의 1.5배라 따로 2배 여유를 두지 않는다 |
| 월드 확대 상한 | 화면 아래 모서리에서 최대 1.2배(155/128) 확대 | 허용. 더 가까운 것은 메쉬 |
| 월드 축소 | 최대 5.5배 축소(먼 안개 경계) | Mipmap 필요(§3.8.4) |
| 그릴 때 배율 `scaleK` | 플레이어 고양이 1.05(L3798), 친구 0.85(L3799), 그 외 1.0 | 굽는 크기에 넣고 Unity Transform 스케일은 1 |
| UI 기준 | 1080×1920 캔버스(CanvasScaler 기준 해상도, [05](05_Screens.md)). 정본 단위 `u = W/100` → **1u = 10.8 px** | |
| UI 굽기 크기 | 기준 화면 표시 크기 ×1.0, 4의 배수 올림. 표시 크기가 64 px 이하인 아이콘은 ×2(최소 128) | 1440 폭 기기에서도 1.33배 확대에 그친다. 작은 아이콘은 확대 시 흐림이 크게 보여 2배 |
| 로비 아지트 | 정본 `s = W*0.115`(L3510) = 124 px/단위 → 굽기 **160 px/단위**(터치 반동 최대 1.18배 L3515 + 여유) | |

### 3.3.3 대표 권장 크기

전체는 §3.5 표. 대표값:

| 그림 | 실제 크기 | 굽는 캔버스(px) |
|---|---|---|
| 플레이어 고양이 뒷모습 1프레임 | 1.8×2.7 m(그림자·망토·모자·던지는 팔 포함) × 134.4 px/m | 244×364 |
| 친구 고양이 1프레임 | 같은 범위 × 108.8 px/m | 196×296 |
| 장애물(택배 상자) | 폭 약 2.1 m, 높이 1.7 m(L3607 해석) | 304×248 |
| 던지는 장난감 | 반지름 0.6 m(blades 기준, L3800) | 160×160 |
| 무기 아이콘(UI) | `#wpnCard` 19u = 205 px 표시(A 노트) | 208×208 |
| 카드 그림(UI) | `#hc.center` 이미지 17u = 184 px 표시 | 192×192 |
| 아지트 시설 | 높이 최대 2.7 단위 × 160 | 최대 352×464 |

## 3.4 스프라이트 이름·피벗·여백·프레임 규칙

### 3.4.1 스프라이트 이름 규칙

정크버스터즈의 `UI_Icon_*`, `Icon_*` 접두 방식을 MV용으로 정한다. **이름 = PNG 파일 이름 = 아틀라스 안 스프라이트 이름 = 표 열에 적는 값.** 영문 PascalCase 토막을 `_`로 잇는다. 숫자 변형은 끝에 붙인다.

| 접두 | 뜻 | 기본 아틀라스 | 예 |
|---|---|---|---|
| `World_Cat_` | 런 고양이(뒷모습) 프레임 | `Atlas_World_Cat_T<n>`, `Atlas_World_Friend_T<n>` | `World_Cat_P0_T3_Run_A_07` |
| `World_Obj_` | 오브젝트 본체(장애물·게이트·우물·관문·계단) | `Atlas_World_Theme_<key>` | `World_Obj_Junk_Parcel` |
| `World_Item_` | 아이템(츄르·캣닢·보상 배지) | 같음 | `World_Item_Churu_0` |
| `World_Enemy_` | 적 | 같음 | `World_Enemy_Mouse_05` |
| `World_Proj_` | 던지는 장난감 | 같음 | `World_Proj_3` |
| `World_Prop_` | 길가 소품 | 같음 | `World_Prop_Jar` |
| `World_House_` | 집 부품(문·창·옥상) | 같음 | `World_House_Win_On` |
| `World_Fence_` | 담장 면 장식 | 같음 | `World_Fence_Gate` |
| `World_Line_` | 빨랫줄·전구줄 매달림 | 같음 | `World_Line_Shirt_3` |
| `Fx_` | 이펙트·빛·입자·우다다 연출 | `Atlas_Fx_Common` | `Fx_Glow_Lin` |
| `Lobby_` | 로비 아지트(시설·정면 고양이·방 부품) | `Atlas_World_Lobby` | `Lobby_Fac_Tower_2` |
| `UI_Icon_` | UI 아이콘(재화·무기·시설·강화) | `Atlas_UI_Icon` | `UI_Icon_Weapon_5` |
| `UI_Emoji_` | 이모지 대체 아이콘 | `Atlas_UI_Icon` | `UI_Emoji_Map` |
| `UI_Skin_` | UI 판·틀·막대(9-slice) | `Atlas_UI_Common` 등 | `UI_Skin_Panel` |
| `Card_` | 가호 카드 그림 | `Atlas_UI_Card` | `Card_Orb_Bolt` |
| `Tex_` | 단독 텍스처(AddressableTexture 주소) | 아틀라스 없음 | `Tex_God_Thunder` |
| `Sfx_`, `Bgm_` | 소리 파일 주소 | — | `Sfx_Throw`, `Bgm_Run` |

- 이름에 테마 이름을 넣지 않는다(P8). 테마는 아틀라스가 정한다.
- 이름 길이 64자 이내, 영문·숫자·`_`만. 검증기가 확인.

### 3.4.2 파일 이름·주소

| 종류 | 파일 | Addressables 주소 |
|---|---|---|
| 아틀라스 스프라이트 | `Art/Source/Sprite/<Atlas>/<Name>.png` | 스프라이트는 주소 없음. **아틀라스 자산**이 주소를 가진다: 주소 = 아틀라스 이름(예: `Atlas_UI_Icon`) |
| 단독 텍스처 | `Addressables/Remote/Texture/<Name>.png` | `<Name>`(예: `Tex_Lobby_Room`) |
| 소리 | `Addressables/Remote/Audio/Sfx/<Name>.wav` | `<Name>`(예: `Sfx_Throw`) = `03_sound.soundName` |

### 3.4.3 피벗 규칙(함수별 정본 앵커)

| 피벗 이름 | 좌표(0~1, Unity: 왼쪽 아래 원점) | 정본 앵커 | 쓰는 함수 |
|---|---|---|---|
| `Feet` | 캔버스 지정 화소(발 아래 가운데) | `mochi`/`mochiBack`의 `(X, Y)`(L3266, L3679) | 고양이, `drawMouse`(L3652) |
| `BottomCenter` | (0.5, 바닥 화소) | 바닥 가운데 | `drawFac`(L3399), 길가 소품(L3570~3583), 캣닢 화분(L3112), 츄르 더미(L3108) |
| `FrontBottom` | 앞면 아래 가운데 화소 | `drawBox` 기준 `pj(x, y, −(z − d/2))`(L2810), `drawJunk`의 `z − 0.45`(L3608) | 장애물, 우다다 계단 단 |
| `Center` | (0.5, 0.5) | 원점 중심 | `drawProjShape`(L3368), `churu`(L3737), `catnipLeaf`, `brickBit`, 빛·링, 카드·아이콘 |
| `GateCenter` | 게이트 중심 | `pj(x, y+1.4+oy, z)`(L3633) | 게이트 롤·배경 |
| `WellMouth` | 우물 입구 중심 | `pj(x, y+1.25, z)`(L3645) | 우물 입구 |
| `TopCenter` | (0.5, 1) | 매달린 점 | 빨래·전구(집게 위치) |
| `PoleBase` | 기둥 바닥 | `x = sd*4.5`, y 0(L3584) | 전봇대 |

manifest에는 화소 피벗을 0~1로 바꾼 값이 들어간다. 프레임 묶음은 모든 프레임이 같은 피벗.

### 3.4.4 애니메이션 프레임 규칙

| 규칙 | 내용 |
|---|---|
| 샘플 변수 | 함수가 위상 `ph`를 받으면 `ph`로(고양이), 시간 `now`를 쓰면 `__clock`으로(쥐, 시설, 별) 샘플한다 |
| 샘플 위치 | `v_i = from + i·period/count`, i = 0…count−1(끝점 제외 → 고리 반복) |
| 재생 | **위상 구동**: 런 고양이는 `frame = floor((ph mod 2π)/2π·count)`로 시뮬 위상을 그대로 따른다(달리기 13 rad/s, 막힘 4 rad/s, 우다다 30 rad/s — L2039, L2264). **시간 구동**: `fps = count/period` |
| 이름 | `<이름>_<2자리 번호>`(예: `World_Enemy_Mouse_00`~`_15`) |
| 고리 이음매 | 정본 성분 주기가 서로 맞지 않으면(예: 꼬리 `sin(ph·1.1)` L3720) 대표 주기를 쓰고 이음매 오차를 manifest `loopError`에 적는다. 꼬리 끝 0.07 m 이하는 허용 [추정] |
| 굽지 않는 움직임 | 몸 전체 이동·확대(`lift`, `sq`의 `scale(1/√sq, sq)`)를 굽지 않아도 되는 것은 Transform으로 한다: 로비·UI 정면 고양이 숨쉬기(L3271), 자는 친구(L3271), 우다다 화살표 출렁(L3208), 매달린 장난감 흔들림(L3222) |

### 3.4.5 테마 변형 규칙

- 테마마다 그림이 달라지는 월드 스프라이트(`themeSet: each`)와 같은 그림(`all`)을 **모두 각 테마 월드 아틀라스에 같은 이름으로 넣는다**(D-58). 런 화면 월드 그림은 고양이·이펙트를 빼면 아틀라스 하나에서 나온다.
- 어떤 테마에만 나오는 소품(눈사람·부표)도 이름은 공통이다. 그 테마 아틀라스에만 있으면 되고, 어떤 소품을 쓰는지는 `304_theme`의 소품 목록 열이 정한다([07](07_Tables.md), 정본 규칙 L3572).
- 검증기: `304_theme`가 가리키는 모든 테마 아틀라스는 「공통 필수 이름 목록」(§3.5에서 `themeSet`이 `all`·`each`인 이름)을 빠짐없이 가져야 한다.

## 3.5 함수 ↔ 스프라이트 대응표

열: 함수(줄) · 스프라이트 이름 · 종류(World Sprite / UI Sprite / Texture) · 캔버스(px) · 피벗 · 프레임 · 테마(`each`/`all`/`none`) · 기본 아틀라스 · 쓰는 표·열.
표·열 이름은 [07](07_Tables.md) 확정 목록 기준이며, **[제안 열]**은 이 문서가 07에 요청하는 열이다(§3.12 가정).

### 3.5.1 캐릭터

| 함수(줄) | 스프라이트 이름 | 종류 | 캔버스 | 피벗 | 프레임 | 테마 | 아틀라스 | 표·열 |
|---|---|---|---|---|---|---|---|---|
| `mochiBack` L3679 (플레이어 pat 0) | `World_Cat_P0_T{t}_Run_A_{00..11}` | World | 244×408(피벗 화소 122,344) | Feet | ph 0~2π, 12 | none | `Atlas_World_Cat_T{t}` | `101_cat.spritePrefix`[제안 열] = `World_Cat_P0` |
| 〃 던지는 중(`thr > 0`, L3712) | `World_Cat_P0_T{t}_Run_B_{00..11}` | World | 244×408(피벗 화소 122,344) | Feet | 12 | none | 〃 | 〃 (`thr > 0`이면 `_B`) |
| 〃 우다다(`st:'climb'`, 먼지 포함) | `World_Cat_P0_T{t}_Climb_{00..11}` | World | 244×408(피벗 화소 122,344) | Feet | 12 | none | 〃 | 〃 |
| 〃 정지(`st:'idle'`) | `World_Cat_P0_T{t}_Idle` | World | 244×408(피벗 화소 122,344) | Feet | 1 + Transform 숨쉬기 | none | 〃 | 〃 |
| `mochiBack` 친구 pat 1~4, t = 손패 최고−2(최소 1, 최대 6) L3799 | `World_Cat_P{p}_T{t}_{Run_A|Climb}_{00..11}`, `_Idle` | World | 196×296 | Feet | 12+12+1 | none | `Atlas_World_Friend_T{t}` (t 1~6) | `101_cat.spritePrefix` 친구 행 |
| t=8 회전 별(L3732, 몸 그림에서 억제) | `Fx_Star4_Pale` | World | 32×32 | Center | 1(회전·크기는 런타임 `now*1.5`, `0.07+0.03 sin(now*6+i)`) | none | `Atlas_Fx_Common` | 101_cat 연출 규칙 |
| `mochi` L3267 로비 주인공(`s*1.25`, idle, t 1~8, pat 0) | `Lobby_Cat_Front_T{t}`, `Lobby_Cat_Front_T{t}_Blink` | World(로비) | 320×484(피벗 화소 148,450) | Feet | 2(눈 감음 0.12 s / 3.6 s, L3316) + Transform 숨쉬기 | none | `Atlas_World_Lobby` | `101_cat.lobbySprite`[제안 열] |
| 〃 t=8 정면 회전 별(L3361) | `Fx_Star4_Pale` 공용 | — | — | — | — | — | — | — |
| `mochi` 자는 친구(`s*0.62`, sleep, t 1, pat 1~4) L3528 | `Lobby_Cat_Sleep_P{1..4}` | World(로비) | 144×132(피벗 화소 72,114) | Feet | 1 + Transform(`0.97+0.025 sin`) | none | `Atlas_World_Lobby` | 〃 |
| `paintGod` L2481 (신 초상: 하늘·달/해·`mochi` god) | `Tex_God_Thunder`, `Tex_God_Flame` | Texture | 400×468 | Center | 1 | none | 단독(AddressableTexture) | `113_god.portrait`[제안 열] |

프레임 합계: 플레이어 8룩 × 37 = 296, 친구 6룩 × 4무늬 × 25 = 600. 묶음 로드(§3.8.5). 다른 방식(파츠·팔레트 교체)은 D-51.

### 3.5.2 길가 소품·집·담장 (`drawMoonProp` L3556~3601, `drawClothes` L3544)

달동네 5테마는 모두 `prop:'moon'`이라 `drawMoonProp`만 쓴다(L2902). 집 정면·윗면, 담장 면은 메쉬(D-59, §3.5.9)이고 아래 부품만 굽는다.

| 함수(줄) | 스프라이트 이름 | 종류 | 캔버스 | 피벗 | 프레임 | 테마 | 아틀라스 | 표·열 |
|---|---|---|---|---|---|---|---|---|
| bush/stone → 화분 L3573 | `World_Prop_Pot` | World | 100×152(피벗 화소 50,136. 바닥 그림자 포함) | BottomCenter | 1 | all | `Atlas_World_Theme_<key>` | `304_theme.roadsideProps`[제안 열] |
| 장독 L3574 | `World_Prop_Jar` / 눈 뚜껑 `World_Prop_Jar` (snow 테마 아틀라스에서만 눈 그림) | World | 100×132(피벗 화소 50,116) | BottomCenter | 1 | each(snow만 다름) | 〃 | 〃 |
| 봉투 L3575 | `World_Prop_Bag` | World | 100×124(피벗 화소 50,108) | BottomCenter | 1 | all | 〃 | 〃 |
| 상자 L3576 (색 `hh<0.25` 파랑/빨강 × 과일 2색) | `World_Prop_Crate_{Blue,Red}_{A,B}`(A 과일 `#f0a070`, B `#ffd84a`) | World | 108×100(피벗 화소 54,84) | BottomCenter | 1 | all | 〃 | 〃 |
| 상자 항구(생선 `fishShapeW`) | `World_Prop_Crate_{Blue,Red}_Fish`(정본 항구 상자는 늘 빨강. `_Blue_Fish`는 파랑 몸 + 생선 조합) | World | 108×112(피벗 화소 54,96) | BottomCenter | 1 | all | 〃 | 〃 |
| 부표 L3577 | `World_Prop_Buoy` | World | 96×104(피벗 화소 48,88) | BottomCenter | 1 | all | 〃 | 〃 |
| 눈사람 L3578 | `World_Prop_Snowman` | World | 96×136(피벗 화소 48,120) | BottomCenter | 1 | all | 〃 | 〃 |
| 전봇대+가로등 L3584~3590 | `World_Prop_Pole` (오른쪽 기준, 왼쪽은 flipX) | World | 200×696 | PoleBase | 1 | all | 〃 | `304_theme.night`가 0보다 크면 빛 `Fx_Glow_Lin` 색 `#ffd890` r = 3.0 m |
| 집 문 L3594 (`th.door`) | `World_House_Door` | World | 128×176 | BottomCenter | 1 | **each** | 〃 | 집 배치 규칙([02](02_Visual.md)) |
| 집 창 L3595~3596 (불 켬 `#ffd878` / 끔 `#a8d0f0`) | `World_House_Win_On`, `World_House_Win_Off` | World | 104×88 | Center | 1 | all | 〃 | 불 켬 판정 L3596, 빛 `Fx_Glow_Lin` r = S·1.4 |
| 옥상 물탱크 L3598 | `World_House_Tank` | World | 152×136 | BottomCenter | 1 | all | 〃 | |
| 옥상 장독대 3개 L3599 | `World_House_Jars` | World | 176×96 | BottomCenter | 1 | all | 〃 | |
| 지붕 눈 L3597 (가변 폭) | `World_House_SnowCap` | World | 128×40, border [24,0,24,0] | BottomCenter | 1 | each(snow) | 〃 | |
| 담장 대문 L3563 (`th.door`, 면 좌표) | `World_Fence_Gate` | World(메쉬 텍스처) | 144×136 | BottomCenter | 1 | each | 〃 | 담장 메쉬 장식 |
| 담장 꽃 얼룩 L3565 | `World_Fence_Flower` | 〃 | 64×48 | Center | 1 | all | 〃 | |
| 담장 차양 L3561 (6칸, 빨강·파랑) | `World_Fence_Awning_Red`, `_Blue` | 〃 | 208×64 | TopCenter | 1 | all | 〃 | market 테마 |
| 담장 위 눈 L3567 | `World_Fence_SnowCap` | 〃 | 128×24, border [16,0,16,0] | BottomCenter | 1 | each(snow) | 〃 | |
| 빨래 3종 × 7색 L3550~3553 (`CLOTH`) | `World_Line_Shirt_{0..6}`, `World_Line_Towel_{0..6}`, `World_Line_Socks_{0..6}` | World | 128×104 / 72×112 / 80×72 | TopCenter | 1(흔들림은 런타임 x 이동 `sin(tt*1.8+i)*0.04*q`) | all | 〃 | `304_theme.lineKind`[제안 열] |
| 전구줄 4색 × 켬/끔 L3548 | `World_Line_Bulb_{0..3}_On`, `_Off` | World | 24×32 | TopCenter | 1(깜빡임 런타임 `((i + (now/350|0)) % 3) !== 0`) | all | 〃 | snow·roof 테마 |

담장 장식은 「측면 정사영」 감싸기로 굽는다: `pj'(x, y, z) → (AX + z·S, AY − y·S)`로 담장 면(z–y 평면)을 정면처럼 그려 면 좌표 텍스처를 얻는다. 차양은 정본에서 면 밖으로 0.8 m 튀어나오지만(L3561) 면 텍스처로 근사한다 [추정, 12 캡처 대조로 확인].

### 3.5.3 오브젝트·아이템 (`drawObj` L3099)

| 함수(줄) | 스프라이트 이름 | 종류 | 캔버스 | 피벗 | 프레임 | 테마 | 아틀라스 | 표·열 |
|---|---|---|---|---|---|---|---|---|
| `drawJunk` 생선 상자(`rew:'soul'`) L3610 | `World_Obj_Junk_FishBox` | World | 296×264(피벗 화소 140,232) | FrontBottom | 1 | all | `Atlas_World_Theme_<key>` | `105_objectKind.sprite`(wall 행 변형 목록 `sprite` = `/` 리스트) |
| 〃 v0 택배 상자 L3614 | `World_Obj_Junk_Parcel` | World | 296×264(피벗 화소 140,232) | FrontBottom | 1 | all | 〃 | 〃 |
| 〃 v1 쓰레기 봉투 2색(`idx%2`) L3616 | `World_Obj_Junk_Bag_Dark`, `_Blue` | World | 296×264(피벗 화소 140,232) | FrontBottom | 1 | all | 〃 | 〃 |
| 〃 v2 우유 상자 2색 L3618 | `World_Obj_Junk_Crate_Red`, `_Blue` | World | 296×264(피벗 화소 140,232) | FrontBottom | 1 | all | 〃 | 〃 |
| 〃 보상 배지(dia: 캣닢 잎 0.26q, 그 외: 츄르 0.55q) L3626 | `World_Item_Badge_Dia`, `World_Item_Badge_Gold` | World | 48×80 / 80×40 | Center | 1 | all | 〃 | 보상 종류의 `07_consumeType` 배지 [제안 열 `worldBadge`] |
| 〃 맞음 흰 섬광 L3628 | `Fx_FlashEllipse` | World | 144×104 | Center | 1(알파 ×0.35) | none | `Atlas_Fx_Common` | |
| `drawRollGate` 배경(초록 `#6fd27a`/분홍 `#f08ab0`) L3634 | **메쉬**(정점색 그라데이션, §3.5.9) | — | — | — | — | — | — | `105_objectKind.tint`[제안 열] |
| 〃 휴지 롤 L3638 | `World_Obj_Gate_RollPaper` | World | 76×76 | Center | 1 | all | 〃 | `105_objectKind.sprite`(sgate) |
| 〃 털실 롤 4색 L3639 | `World_Obj_Gate_RollYarn_{0..3}` | World | 76×76 | Center | 1 | all | 〃 | cgate |
| 〃 위로 흐르는 흰 줄 5개 L3635 | `Fx_Stripe` | World | 16×64 | Center | 1(런타임 스크롤) | none | `Atlas_Fx_Common` | |
| `drawWarpGate` 사이잘 기둥(일반·금빛) L3216 | `World_Obj_Arch_Post`, `World_Obj_Arch_Post_Gold` | World | 280×444(피벗 화소 60,436. 기둥 x = 4.6 m의 원근 기울기 포함, 왼쪽은 flipX) | BottomCenter | 1 | all | 테마 | `105_objectKind.sprite`(arch) |
| 〃 받침·중간·위 발판(`drawBox`) L3217~3219 | **메쉬**(색 P1/P2) | — | — | — | — | — | — | `105_objectKind.tint` |
| 〃 매달린 장난감 3색 L3221 | `World_Obj_Arch_Toy_{0..2}` | World | 80×80(외곽선 `max(1.5, 0.05·s)`, L3213 `c0.s` 대신 공 위치 s [추정]) | Center | 1(흔들림 런타임 `sin(t*2+dx)*0.3`) | all | 테마 | |
| 〃 금빛 광륜·별 L3223 | `Fx_Glow_Lin` + `Fx_Star4` | — | — | — | — | — | Fx | |
| `drawWell` 입구 L3645 | `World_Obj_Well_Mouth` | World | 248×128 | WellMouth | 1 | all | 테마 | `105_objectKind.sprite`(tower) |
| 〃 벽돌 4색(우물 고리) L3646~3649, 색 L1340 | `World_Obj_Well_Brick_{0..3}` | World | 80×48 | Center | 1(배치는 3D 고리 44개, [02](02_Visual.md)) | all | 테마 | |
| 〃 위 잡동사니 5색 L1346 | `World_Obj_Well_Junk_{0..4}` | World | 80×48(정본은 벽돌과 같은 식 `0.54·0.3·s`로 그림) | Center | 1 | all | 테마 | |
| coin 츄르 더미 L3108(츄르 6개 개별 출렁 `sin(t+i)*0.02c`) | `World_Item_Churu_{0..2}`(조립) | World | 96×40 | Center | 1 | all | 테마 | `105_objectKind.sprite`(coin) |
| gem 캣닢 화분 L3112 | `World_Item_CatnipPot` | World | 192×212(피벗 화소 96,204) | BottomCenter | 1 | all | 테마 | `105_objectKind.sprite`(gem) |
| 〃 반짝이 L3116 | `Fx_Star4` | — | — | — | 크기 `0.14*(0.8+0.2 sin(now/200))` 런타임 | — | Fx | |
| `drawMouse` L3652 | `World_Enemy_Mouse_{00..15}` | World | 160×148(피벗 화소 64,128) | Feet | t 0~2π(`now/200`), 16, 12.7 fps | all | 테마 | `102_enemy.sprite` |
| `drawBlackHole` 계단 단 3개 L3204~3206 | `World_Obj_Stair_Step_{0..2}` | World | 1076×104 / 1048×160 / 1020×220(피벗 화소 518,52 / 504,113 / 490,173 = `drawBox` 기준점) | FrontBottom | 1 | all | 테마 | `105_objectKind.sprite`(stair) |
| 〃 화살표 L3208 | `World_Obj_Stair_Arrow` | World | 168×176(피벗 화소 84,96) | Center | 1(출렁 런타임 `sin(t*5)*0.25`) | all | 테마 | |
| `brickBit`/`energyOrb` L3605(벽돌 조각 4색 `ENERGY_C` L3096) | `Fx_BrickBit_{0..3}` | World | 40×24 | Center | 1(회전 런타임) | none | Fx | `04_effect` |

### 3.5.4 투사체·전투 연출

| 함수(줄) | 스프라이트 이름 | 종류 | 캔버스 | 피벗 | 프레임 | 테마 | 아틀라스 | 표·열 |
|---|---|---|---|---|---|---|---|---|
| `drawProjShape` t 1~8 L3368 (게임 반지름 0.5 m, blades 0.6 m) | `World_Proj_{1..8}` | World | 160×160 | Center | 1(회전 런타임 `now/110`, `now/180`) | all | 테마 | `103_item.projSprite` |
| spear 머리 3겹(fire `#ffb03a`/그 외 `#ff3a4a`) L3802 | `World_Proj_SpearHead_Fire`, `_Bolt` | World | 96×96 | Center | 1 | none | `Atlas_Fx_Common` | `110_card` spear 행 [제안 열 `projSprite`] |
| spear 궤적 선 L3802 | **선 메쉬**(LineRenderer, 굵기 `max(4, s*0.32)`, 흰 심) | — | — | — | — | — | — | [06](06_Effects.md) |
| `drawPawFx` 발바닥 L3747 | `Fx_Paw` | World | 160×128 | Center | 1(낙하·페이드 런타임, 수명 0.55 s) | none | Fx | `04_effect.prefab` |
| 〃 착지 링 | `Fx_RingEllipse` | World | 192×80(rx 84, ry = rx·0.33, 굵기 rx·0.2/0.9) | Center | 1(스케일 런타임) | none | Fx | 〃 |
| `drawHissFx` 3겹 링 + 방사선 10 L3753 | `Fx_Hiss` | World | 512×288 | Center | 1(반지름 `0.8+k*5`, 알파 `1−k` 런타임) | none | Fx | 〃 |
| `drawFx` 빛(glowTex 프로필 `c → c+'99'@0.45 → 0`) L3142 | `Fx_Glow_Fx` | World | 64×64 | Center | 1(색 틴트) | none | Fx | 〃 |
| `drawLights`·라벨 빛(선형 `c+'77' → 0`) L3603, L3131 | `Fx_Glow_Lin` | World | 64×64 | Center | 1(색 틴트, 가산) | none | Fx | 〃 |
| `drawFx` 링(G.ring) L3146 | `Fx_RingEllipse` 공용 | — | — | — | — | — | — | |
| `drawFx` 사각 파편 L3148 | `Fx_Square` | World | 8×8 | Center | 1(색 틴트, 외곽선 1px은 생략 [추정]) | none | Fx | |
| `star4` L3261 | `Fx_Star4`(흰), `Fx_Star4_Pale`(`#fff6a0`) | World | 32×32 | Center | 1 | none | Fx | |
| `drawSnow` 입자 L3604 | `Fx_Dot` | World/UI | 8×8 | Center | 1(화면 공간 입자) | none | Fx | |

### 3.5.5 우다다 연출 (`drawWarp` L3163, `drawWarpOut` L3197, 집중선 L3814)

화면 전체를 덮는 화면 공간 연출이다. 하늘 그라데이션(3단 `s1→s2→s3`, `WARP_ST` L3155)과 양옆 지붕(원근 다각형), 휴지 계단 발판은 **셰이더·메쉬**([06](06_Effects.md))로 그리고, 아래만 굽는다. 색이 `night`로 섞이는 부품(`mixC`)은 흰 스프라이트를 부품별로 나눠 각 섞인 색을 곱한다.

| 함수(줄) | 스프라이트 이름 | 종류 | 캔버스 | 피벗 | 테마 | 아틀라스 |
|---|---|---|---|---|---|---|
| 해 원(0단계 `#fff3b0`) L3176 | `Fx_Disc`(흰 원, 색 틴트) | UI | 128×128 | Center | none | `Atlas_Fx_Common` |
| 노을 광륜 L3177 | `Fx_Glow_Lin` | — | — | — | — | — |
| 달 크레이터 `rgba(200,190,160,.35)` L3178 | `Fx_Disc` 틴트 | — | — | — | — | — |
| 초승달 가림(하늘색 `s1` 원) L3179 | `Fx_Disc` 틴트 | — | — | — | — | — |
| 휴지 롤 몸·구멍·외곽선 L3185 | `Fx_Warp_RollBody`, `Fx_Warp_RollHole`, `Fx_Warp_RollLine` | UI | 48×48(반지름 20, 구멍 0.35배, 선 OLC 0.12배. 세 장 같은 피벗) | Center | none | Fx |
| 별(night>0.5, 2px) L3172 | `Fx_Dot` | — | — | — | — | — |
| 섬광 링 `drawWarpOut` L3199 | `Fx_Ring` | UI | 256×256(반지름 118, 굵기 16 = wo 0의 값 [추정]) | Center | none | Fx |
| 집중선 18줄 L3814 | `Fx_SpeedLine` | UI | 8×128 | 아래 가운데 | none | Fx |
| 단계 이름 L3193 | TMP(§3.7) | — | — | — | — | — |

### 3.5.6 로비 아지트 (`drawAzit` L3474)

로비는 화면 고정 아이소메트릭이다(`iso` L3476, 화면 크기 W·H 비례). 방은 텍스처 1장, 움직이는 것은 스프라이트로 나눈다.

| 함수(줄) | 스프라이트 이름 | 종류 | 캔버스 | 피벗 | 프레임 | 아틀라스 | 표·열 |
|---|---|---|---|---|---|---|---|
| 방 배경(벽·징두리·창(달·건물)·선반·달력 '12월'·바닥·러그) L3478~3505 | `Tex_Lobby_Room` | Texture | 1080×1920 | Center | 1. 억제: 전구 `E`(4색·`shadeC −0.5` 색, rx≤3), 전구 빛 `fillRect` 24×24, 창밖 눈 `ctx.arc`, 전체 그라데이션 `fillRect`(난로 빛·비네트), `drawFac`·`mochi`·`facCushion`·`star4`, 시설 이름표 `RR`(`rgba(40,24,36,.72)`)·`fillText`(달력 '12월'은 남김: 억제 조건을 시설 글자 크기 `W*0.026`로 한정) | 단독 | LobbyStage 프리팹 |
| 비네트 L3537 | `Tex_Lobby_Vignette` | Texture | 540×960 | Center | 1(늘려 씀) | 단독 | 〃 |
| 난로 빛 L3505 | `Fx_Glow_Lin` 틴트 `rgba(255,170,90)` 알파 `0.22+0.04 sin(7t)+stove·0.05` | — | — | — | — | — | — |
| 가랜드 전구 4색 × 켬/끔 L3497 | `Lobby_Bulb_{0..3}_On`, `_Off` | World(로비) | 16×16(화면 3×4 px 타원을 160/(1080·0.115)배) | Center | 1(깜빡임 `((k + (t*2.5|0)) % 3) !== 0`, 빛 `Fx_Glow_Lin` r 12px) | `Atlas_World_Lobby` | |
| 창밖 눈 26개 L3489 | `Fx_Dot`(창 사각형 마스크 안) | — | — | — | — | — | — |
| `drawFac` 캣타워 st 1~3 L3401 | `Lobby_Fac_Tower_1`, `Lobby_Fac_Tower_{2,3}_{00..11}` | World(로비) | 336×536(피벗 화소 168,498. 단계 공통, s = 160·1.0) | BottomCenter | st1: 1, st2·3: 12(t 0~2.856 s, 공 흔들림 `sin(t*2.2)`), 4.2 fps | `Atlas_World_Lobby` | `10_lobbyUpgrade.facSprite`[제안 열] |
| 〃 스크래쳐 st 1~3 L3415 | `Lobby_Fac_Scratch_{1..3}` | 〃 | 268×252(피벗 화소 134,216. s = 160·0.9) | BottomCenter | 1(st3 별은 `Fx_Star4` 런타임) | 〃 | 〃 |
| 〃 생선 건조대 st 1~3 × 생선 수 `nF`(1~2/1~4/1~6) L3424 | `Lobby_Fac_Rack_{st}_F{n}` (12종) | 〃 | 312×284(피벗 화소 156,248. s = 160·0.85) | BottomCenter | 1(흔들림 `sin(t*1.6+i)*0.03` 생략 [추정]) | 〃 | 〃 |
| 〃 장난감 바구니 st 1~3 L3436 | `Lobby_Fac_Basket_{1..3}` | 〃 | 220×232(피벗 화소 110,196. s = 160·0.85) | BottomCenter | 1 | 〃 | LobbyStage(Area 강화 합계) |
| 〃 난로 st 0~3 L3446 | `Lobby_Fac_Stove_0`, `Lobby_Fac_Stove_{1..3}_{00..07}` | 〃 | 320×428(피벗 화소 160,392. s = 160·0.95) | BottomCenter | st0: 1, st1~3: 8(t 0~0.785 s, 불꽃 `0.9+0.1 sin(t*9)`), 10.2 fps | 〃 | LobbyStage(부활) |
| `facCushion` 친구 방석 4색 L3523 | `Lobby_Cushion_{0..3}` | 〃 | 176×80(피벗 화소 88,36) | Center | 1 | 〃 | |
| 감정 말풍선 L3534 | `Lobby_Bubble` | 〃 | 112×108(피벗 화소 56,48 = 타원 중심, 꼬리 포함) | Center | 1(크기 `k`·알파 런타임) | 〃 | |
| 말풍선 기호 ♪ ♥ ✦ (Gothic A1 900, 색 `#7a5ac8`/`#e8504a`/`#f2b33d`) | `UI_Emoji_Note`, `UI_Emoji_Heart`, `UI_Emoji_Sparkle` | UI | 64×64 | Center | 1 | `Atlas_UI_Icon` | |
| 터치 반동 별 6개 L3516 | `Fx_Star4_Pale`(색 `#fff3a0` 틴트) | — | — | — | — | — | — |
| 시설 이름표·자는 친구 'Z' L3519, L3526 | TMP + `UI_Skin_Pill` | — | — | — | — | — | — |

### 3.5.7 UI 아이콘·카드·이모지

| 함수(줄) | 스프라이트 이름 | 종류 | 캔버스 | 피벗 | 아틀라스 | 표·열 |
|---|---|---|---|---|---|---|
| `paintWeapon` t 1~8 L3388(배경 광륜 + `drawProjShape` R 62) | `UI_Icon_Weapon_{1..8}` | UI | 208×208 | Center | `Atlas_UI_Icon` | `103_item.icon` |
| `facIconURL` 캣타워 st 1~3 L3463(사용 L869, L2637) | `UI_Icon_Fac_Tower_{1..3}` | UI | 128×128(96 px 원본을 늘리지 않고 같은 `drawFac` 호출을 4/3배로 다시 그림. 2·3단계는 정본처럼 가장자리까지) | Center | `Atlas_UI_Icon` | `10_lobbyUpgrade.icon`(stair, 단계 목록 `/`) |
| 〃 생선 건조대 st 1~3 L2638 | `UI_Icon_Fac_Rack_{1..3}` | UI | 128×128 | Center | 〃 | `10_lobbyUpgrade.icon`(income) |
| `#gIcon` = `cannonURL()` = 캣타워 2단계 L869 | `UI_Icon_Fac_Tower_2` 공용 | — | — | — | — | HUD 고정 |
| SVG `.ic-gold` 츄르 L599 | `UI_Icon_Gold` | UI | 96×96 | Center | 〃 | `01_wealth.icon`(1) |
| SVG `.ic-dia` 캣닢 L600 | `UI_Icon_Dia` | UI | 96×96 | Center | 〃 | `01_wealth.icon`(2) |
| SVG `.ic-soul` 생선 L46 | `UI_Icon_Soul` | UI | 96×68 | Center | 〃 | `01_wealth.icon`(3) |
| SVG `.ic-stair` 휴지 롤(계단) L589 | `UI_Icon_Stair` | UI | 96×96 | Center | 〃 | `07_consumeType.iconColumn`(600 StairLv) |
| CSS `.ic-brick` L42(날아가는 벽돌) | `UI_Icon_Brick` | UI | 104×84(`dom`, `--u` 10.8 px, drop-shadow 번짐 포함) | Center | 〃 | `04_effect`(fly) |
| `paintCard2` atk·rate·gold·storm·blast L3758 (원소 무관) | `Card_Atk`, `Card_Rate`, `Card_Gold`, `Card_Storm`, `Card_Blast` | UI | 192×192 | Center | `Atlas_UI_Card` | `110_card.icon` |
| `paintCard` clone1 L901 | `Card_Clone1` | UI | 192×192 | Center | 〃 | 〃 |
| `paintCard2` orb·spear (원소별 색) | `Card_Orb_Bolt`, `Card_Orb_Fire`, `Card_Spear_Bolt`, `Card_Spear_Fire` | UI | 192×192 | Center | 〃 | 〃 |
| `paintCard` proc·chain·frate (원소별 E1/E2, L874) | `Card_Proc_{Bolt,Fire}`, `Card_Chain_{Bolt,Fire}`, `Card_Frate_{Bolt,Fire}` | UI | 192×192 | Center | 〃 | 〃 |
| 이모지 🧺 🗺️ 🔥 🌿 🐾 ✕ ♪ ▶ (마크업 L658~713) | `UI_Emoji_Basket`, `_Map`, `_Fire`, `_Herb`, `_Paw`, `_Close`, `_Sound`, `_Play` | UI | 96×96 | Center | `Atlas_UI_Icon` | 화면 프리팹·`05_contentOpen.icon` |
| 이모지 🔒(L2499) 🎯 🐱 💥 ⚔️(AUP L2516~2520) 🗡️ 🧱(RBP L2534~2537) 🛡️ 🌙 🐣(MAP_FEATS L2582~2586) | `UI_Emoji_Lock`, `_Target`, `_Cat`, `_Boom`, `_Swords`, `_Dagger`, `_Brick`, `_Shield`, `_Moon`, `_Chick` | UI | 96×96 | Center | 〃 | `13_areaUpgrade.icon`, `14_rebirthPerk.icon`, `05_contentOpen.icon` |

카드 그림 16장(원소 무관 6 + 원소별 5종 × 2). `frate`의 글자 `50%`(`bold 26px sans-serif`, L895)는 그림에 포함해 굽는다(굽는 Chromium의 sans-serif = 고정 글꼴로 vendor에 둔 글꼴 지정). 🏁 레이스는 제외 콘텐츠라 굽지 않는다([01](01_Scope.md)).

### 3.5.8 UI 스킨 (CSS로 그린 판·틀·막대)

`dom` 방식으로 빈 요소에 해당 CSS를 입혀 찍는다. 크기는 `2×(모서리 반지름 + 테두리 + 그림자 번짐) + 8 px`의 정사각, 9-slice 경계 = 반지름 + 테두리 + 번짐. 2색 선형 그라데이션 판은 9-slice 늘림으로 거의 같다. 정확한 배치·전체 목록은 [05](05_Screens.md)가 정하고 여기서는 굽는 규칙과 대표 목록만 둔다.

| CSS(줄, A 노트) | 스프라이트 이름 | 변형 | 아틀라스 |
|---|---|---|---|
| `--panel` 판, `.chip`(L38, L280) | `UI_Skin_Panel`, `UI_Skin_Chip` | 1 | `Atlas_UI_Common` |
| `#stairBox`(L53, L284, L539) | `UI_Skin_StairBox`, `UI_Skin_StairBox_Fly` | 2 | `Atlas_UI_Hud` |
| `.wcard2` 손패 카드 틀(L494~505) | `UI_Skin_WCard_G{1..8}` | 무기 등급색 `WGRADE` 8 | `Atlas_UI_Hud` |
| `#wpnCard`(L506, L576) | `UI_Skin_WpnCard_G{1..8}` | 8 | `Atlas_UI_Hud` |
| `.hcard` / `#hc.center .hcard`(L411, L555) | `UI_Skin_HCard_T{0..4}`, `UI_Skin_HCardCenter_T{0..4}` | 카드 등급색 `TIERS` 5 | `Atlas_UI_Card` |
| `#card`(L236, L326) | `UI_Skin_CardBig_T{0..4}` | 5 | `Atlas_UI_Card` |
| `.ms-card`(L429, L602) | `UI_Skin_MsCard_T{0..4}` | 5 | `Atlas_UI_Hud` |
| `.up` 강화 버튼(L164~590) | `UI_Skin_UpButton` | 1 | `Atlas_UI_Lobby` |
| `.gp-box`, `.pnl .box`(L178, L348, L595) | `UI_Skin_PopupBox` | 1(테두리색 `--pc`는 별도 `UI_Skin_PopupEdge` 틴트) | `Atlas_UI_Common` |
| `.rw div`(L262, L329) | `UI_Skin_ResultCell` | 1 | `Atlas_UI_Common` |
| `.mp-zone`·`.mp-cp`·`.mp-ft`(L453~479) | `UI_Skin_MapZone_Z{1..6}`, `UI_Skin_MapCp`, `UI_Skin_MapFt`, `UI_Skin_MapFt_On` | | `Atlas_UI_Lobby` |
| `.ul-box`(L482) | `UI_Skin_UnlockBox` | 1 | `Atlas_UI_Common` |
| 막대 채움 `#gFill`, `.ms-bar i`, `#lSoulFill`, `#rBar`, `.up .g b`(L587, L548, L158, L260, L223) | `UI_Skin_Bar_Stair`, `_Ms`, `_Soul`, `_Result`, `_Up` | 1(Simple 늘림 — CSS처럼 막대 폭에 맞춰 그라데이션이 줄어듦) | `Atlas_UI_Hud` |
| `#fgMask` 사선 무늬(L530) | `UI_Skin_HatchTile` | 1(Tiled, 68×68. 45° 주기 16.97 px가 정수가 아니라 x 주기 17 px로 맞춤 [추정]) | `Atlas_UI_Hud` |
| `#vig` 화면 비네트(L45) | `Tex_UI_Vignette` | Texture | 단독 |
| 알약 이름표(L3519 `rgba(40,24,36,.72)`) | `UI_Skin_Pill` | 1 | `Atlas_UI_Common` |
| `color-mix()` 등급색 변형 | 등급별로 굽는다(색이 정해진 5·8종). UI에 틴트 셰이더를 쓰지 않는다(§3.8.9) | | |

### 3.5.9 굽지 않는 것 (메쉬·셰이더·TMP)

| 함수(줄) | 대체 | 담당 문서 |
|---|---|---|
| `drawAlley`·`drawGround` 지면 띠·줄눈·경계석·눈더미·옹벽 L3542, L2874 | 지면 메쉬 + 정점색(`304_theme` 색) + 안개 셰이더. 텍스처 없음 | [02](02_Visual.md) |
| `drawBox` 집 정면·윗면·오른쪽 그늘·외곽선 L2809 | 「상자 빌보드」 메쉬(정면 사각 + 기운 윗면 평행사변형, 정점색 `shadeC` 정확 계산) | [02](02_Visual.md), D-59 |
| 담장 면·윗면 `fence` L3558~3569 | 길과 평행한 세로 쿼드 메쉬 + §3.5.2 장식 | [02](02_Visual.md) |
| `drawRollGate` 배경 그라데이션 L3634 | 9-slice 메쉬 정점색 `A+'22' → A+'55'|'aa'` | [02](02_Visual.md) |
| 빨랫줄 줄 L3546 | 선 메쉬(처짐 `0.4(1−(x/4.75)²)`) | [02](02_Visual.md) |
| 우다다 하늘·지붕·휴지 계단 발판 L3166~3190 | 화면 셰이더·메쉬 | [06](06_Effects.md) |
| 테마 틴트 `multiply` L3809, 가산 빛 L3810, 안개 | 렌더 패스·셰이더 | [02](02_Visual.md) |
| 오브젝트 라벨 글자판 `textSprite`/`labelPlane`(L1020~1029) | TMP 월드 라벨(§3.7) | [05](05_Screens.md) |
| `drawCardFan` 무기 카드 부채 L1453 | UI 프리팹(카드 바탕 `#1e2436` + `UI_Icon_Weapon_*` + TMP `×n`) | [05](05_Screens.md) |

### 3.5.10 제외(잔재 — 호출되지 않거나 화면에 안 나옴)

| 함수·값(줄) | 판정 근거 |
|---|---|
| `drawSpace`, `STARS` L2827~2842 | 호출 0건 |
| `drawSky` L2843~2869 | 호출 0건(달동네 테마는 `prop:'moon'`) |
| `drawProp`의 비-moon 분기 전부 L2903~2958 | 달동네 5테마 모두 `prop:'moon'`(L744~753) |
| `drawShip`, `drawShipAt`, `SHIP_COL` L2961~3016 | 호출 0건 |
| `drawCat` L3026~3080 | 호출 0건(`mochi`·`mochiBack`이 대체) |
| `drawTP` L3083~3090 | 호출 0건 |
| `fishIcon` L3092, `COINP` L3098 | 호출 0건 |
| `drawCellSpace`, `PLANET_C` L3227~3254 | 호출 0건(cell 본체 그림 없음 — [04](04_GameRules.md) 의문점) |
| `shadowAt` L2822, `IMG`/`wImg` L2805 | 호출 0건 |
| `bladePath` L862, `drawKeg` L1445, `blob` L1018 | 호출 0건 |
| `buildCat`·`CATM`·`animCat`, `buildWeapon`, `buildEnv` 지형·원경, `buildObjMesh` 본체 메쉬, 지형 텍스처 4종(L992~1011), 조명 | 3D 경로는 그려지지 않음(L919~922) |
| `paintCard`의 orb·storm·blast·spear·gold·rate case, `flame`, 기본 case(UP) | `paintCard2`가 먼저 처리(L882) 또는 해당 id 없음 |
| `paintCard` fire·bolt·frost·meteor·beam·crit·pierce·spin·dbl·clone2 | 현재 카드 목록(L777~806)에 없는 id. 08에서 카드를 추가하면 같은 도구로 굽는다 |
| `.ic-keg` L57, `.ic-part` L78, `.ore` L94, `.ic-energy` L511 | 마크업·JS 사용 0건 |
| `facIconURL`의 scratch·basket·stove | 호출 0건(L2637~2638은 tower·rack만) |

## 3.6 테마 색

### 3.6.1 판정 기준

| 판정 | 조건 | 처리 |
|---|---|---|
| **틴트(곱하기)** | 그림이 **흰색 한 가지 + 알파**뿐이고 색이 런타임 값(머티리얼 색·표 색·`night` 혼합) | 흰 스프라이트 × `SpriteRenderer.color`/`Image.color`. 예: 빛, 링, 원판, 입자, 우다다 휴지 부품 |
| **정점색(메쉬)** | 단색 또는 2색 선형 그라데이션 면이고 크기가 연속값 | [02](02_Visual.md) 메쉬. 예: 지면, 집 정면·윗면, 담장 면, 게이트 배경 |
| **테마별 굽기(`each`)** | 테마 색이 들어가는데 외곽선(`OLC #4a3446`)·하이라이트·다른 고정색과 섞여 있음 | 흰 스프라이트에 곱하면 외곽선까지 물들어 틀린다 → 테마마다 정본 함수에 그 테마 색을 넣어 다시 굽는다 |
| **다색 그라데이션** | 2개 이상 독립 색 정지점(예: 우다다 하늘 `s1→s2→s3`, 신 초상 하늘) | 틴트 불가. 셰이더 매개변수(색 3개)나 변형별 굽기 |
| **고정** | 테마·표와 무관한 색 | 1회 굽기(`all`) |

### 3.6.2 대상별 판정표

| 대상 | 테마·표 색 입력 | 판정 |
|---|---|---|
| 지면(`grass`, `grass2`, `road`, `road2`, `stone`, `fog`) | 304_theme | 정점색 + 안개 셰이더 |
| 집 정면(`walls[]`)·윗면(`roofs[]`) | 304_theme 목록 | 정점색(상자 빌보드) |
| 집 문(`door`) | 304_theme | 테마별 굽기 |
| 담장 면(`walls[]`, `wallTop`) | 304_theme | 정점색 |
| 담장 대문(`door`) | 304_theme | 테마별 굽기 |
| 눈 덮개·눈 장독(`deco:'snow'`) | deco | 테마별 굽기(snow 아틀라스만 다름) |
| 창·물탱크·장독대·소품 | 고정 | 1회 |
| 게이트 배경(`#6fd27a`/`#f08ab0`) | 105_objectKind | 정점색 |
| 관문 발판(P1/P2) | 105_objectKind | 정점색 |
| 가로등·창 빛(`#ffd890`, `#ffcf70`) | 고정(밤 여부는 `night`) | 틴트 |
| 우다다 지붕 벽·지붕(`mixC(..., '#2a2a5a', night)`) | 330_climb(WARP_ST) | 정점색 |
| 우다다 휴지 롤(`mixC`) | 330_climb | 틴트(부품별 흰 스프라이트) |
| 우다다 하늘 3단 | 330_climb | 셰이더 색 3개 |
| 무기 등급 틀(`WGRADE`), 카드 등급 틀(`TIERS`) | 103_item·115_cardTier | 등급별 굽기(UI는 틴트 셰이더 금지) |
| 벽돌 4색·잡동사니 5색·벽돌 조각 4색 | 고정 목록 | 색별 굽기 |
| 방석 4색, 친구 무늬 | 고정 목록 | 색별 굽기 |

### 3.6.3 런타임 안개·밤·틴트

스프라이트 알파 안개 `1 − clamp01((d−34)/50)`, 지면 색 안개, 테마 틴트(`multiply`), 가산 빛, 눈 입자는 모두 **런타임 셰이더·렌더 패스**다. 굽는 그림에는 안개·틴트를 넣지 않는다(굽는 위치 d ≈ 15.3이라 안개 0). 정의는 [02](02_Visual.md).

### 3.6.4 새 테마 추가 절차 (표 → 재추출)

1. `304_theme`에 행 추가(색 전부, deco, night, tint, `worldAtlas = Atlas_World_Theme_<newKey>`).
2. `npm run art -- --theme <newKey>`: 도구가 TSV 행을 읽어 `AREA_TH[0]`의 내용을 그 색으로 바꾸고(객체 내용 변경, 3.2.3), `themeSet: each` job을 다시 굽고 `all` job은 복사해 새 테마 아틀라스 폴더를 만든다.
3. MV-ART-ATLAS 규칙대로 아틀라스 생성 → Addressables `Theme/Update/<ThemeId>/`로 배포 가능([10 §10.6](10_Architecture.md)).
4. 코드 수정 없음. 결정 D-53.

## 3.7 글자

### 3.7.1 월드 라벨은 굽지 않는다

오브젝트 라벨(`textSprite` L1020, `labelPlane` L1025, `drawLabel` L1030, `outlineText` L1031)은 정본에서도 캔버스에 **매번 다시 그리는 동적 글자**다(체력·보상 수치·진행도). 굽지 않고 **TMP 월드 공간 텍스트**로 쓴다.

| 이유 | 설명 |
|---|---|
| 값이 바뀐다 | 체력 `fmt(o.hp)`(L1495), 게이트 `o.n`·`칸당 +lv`(L1509), 카드 수 `×n` |
| 다국어 | 고정 문구(「벽돌 우물 · 맞히면 계단 Lv UP」 L1349 등)는 Localization `ui` 컬렉션 키 |
| 선명도 | SDF는 거리에 따라 커져도 선명하다. 굽는 글자는 원근 확대에 흐려진다 |
| 메모리 | 라벨마다 텍스처를 만들면 개수만큼 메모리. TMP는 글꼴 아틀라스 하나 |

재현 규칙: `outlineText`의 외곽선 굵기 `max(3, size*0.16)`(L1033), 색 `rgba(0,0,0,.8)`, 둥근 이음 → TMP 머티리얼 Outline Width = 0.16 × 2 / 패딩비 [추정, 캡처 대조로 조정], Outline Color `#000000CC`. 라벨 판 위치·월드 크기·화면 정렬·알파(`opacity × (1 − fogK)`)·정렬 편향(−0.05)은 [02](02_Visual.md)·[05](05_Screens.md). 라벨 바탕 판(관문 현판 `#7a4a22`/`#5a3a4a` + 테두리 L1384, 게이지 막대)은 **판만** 스프라이트로 굽는다: `World_Label_Arch`, `World_Label_Arch_Gold`(9-slice), `World_Label_GaugeBg`, `World_Label_GaugeFill`(흰 틴트). 테마 월드 아틀라스에 `all`로 넣는다.

### 3.7.2 예외(그림에 포함해 굽는 글자)

| 글자 | 줄 | 이유 |
|---|---|---|
| 달력 「12월」 | L3496 | 방 배경의 고정 장식. 굽는 Chromium에 Gothic A1 900을 로컬로 넣어 같은 모양 |
| 카드 `frate`의 「50%」 | L895 | 카드 그림의 일부(그림 글자) |
| 이모지 ✕ ♪ ▶ ♥ ✦ | 마크업, L3534 | 게임 글꼴에 없는 기호라 아이콘으로 굽는다(§3.5.7) |

로비 시설 이름표 「캣타워 Lv.2」(L3519), 자는 친구 'Z'(L3526), 우다다 단계 이름(L3193), 숫자 팝업(DOM `div.pop`)은 TMP다.

## 3.8 아틀라스·최적화 (핵심)

### 3.8.1 SpriteAtlas v2 그룹

| 아틀라스(= Addressables 주소) | 포함 | 최대 크기 | 예상 크기 [추정] | 압축(Android/iOS) | Mipmap | 타이트 | 회전 | 패딩 |
|---|---|---|---|---|---|---|---|---|
| `Atlas_UI_Common` | 공통 스킨(판·칩·팝업·알약·결과 칸), 공통 버튼 | 2048 | 1024×1024 | ASTC 4x4 | 끔 | 끔 | 끔 | 4 |
| `Atlas_UI_Hud` | HUD 스킨(계단 상자·손패 틀 8·장착 틀 8·막대·사선), 경고 | 2048 | 1024×1024 | ASTC 4x4 | 끔 | 끔 | 끔 | 4 |
| `Atlas_UI_Lobby` | 강화 버튼·지도 스킨·로비 전용 UI | 2048 | 1024×1024 | ASTC 4x4 | 끔 | 끔 | 끔 | 4 |
| `Atlas_UI_Icon` | `UI_Icon_*`, `UI_Emoji_*` | 2048 | 1024×1024 | ASTC 4x4 | 끔 | 끔 | 끔 | 4 |
| `Atlas_UI_Card` | `Card_*` 16, 카드 틀 `UI_Skin_HCard*`·`CardBig` | 2048 | 1024×1024 | ASTC 4x4 | 끔 | 끔 | 끔 | 4 |
| `Atlas_World_Theme_<key>` (market, stairs, harbor, roof, snow) | 런 월드 그림 전부(오브젝트·아이템·적·투사체·소품·집 부품·담장 장식·빨래·라벨 판) — 테마마다 같은 이름 목록 | 2048 | 2048×2048 | ASTC 6x6 | **켬** | 켬 | 끔 | 8 |
| `Atlas_World_Cat_T{1..8}` | 플레이어 고양이 룩 t의 37프레임 | 2048 | 2048×1024 | ASTC 6x6 | 켬 | 켬 | 끔 | 8 |
| `Atlas_World_Friend_T{1..6}` | 친구 고양이 룩 t × 무늬 4의 100프레임 | 2048 | 2048×2048 | ASTC 6x6 | 켬 | 켬 | 끔 | 8 |
| `Atlas_World_Lobby` | 시설·정면 고양이·자는 친구·방석·전구·말풍선 | 2048 | 2048×2048 | ASTC 6x6 | 끔(고정 크기 화면) | 켬 | 끔 | 4 |
| `Atlas_Fx_Common` | `Fx_*` 전부(빛·링·원·별·발바닥·하악·파편·벽돌 조각·우다다 부품·집중선·줄무늬) | 1024 | 1024×512 | ASTC 4x4(부드러운 그라데이션 보존) | 켬 | 끔 | 끔 | 4 |
| 단독 텍스처 `Tex_*` | 방 배경 1080×1920, 신 초상 2, 비네트 | 2048 | — | ASTC 6x6(방), 4x4(초상·비네트) | 끔 | — | — | — |

규칙:
- **회전은 전부 끔.** UGUI `Image`는 회전된 아틀라스 스프라이트를 지원하지 않고, 월드 셰이더가 UV로 무언가 계산할 여지를 막지 않기 위해서다.
- **타이트 패킹은 월드만 켬.** 월드 스프라이트는 Tight 메쉬로 투명 영역 과다 그리기(overdraw)를 줄인다. UI는 `Image`가 사각형으로 그리므로 Full Rect + 타이트 끔(타이트로 묶으면 이웃 스프라이트가 사각 안으로 들어와 보인다).
- **패딩**: Mipmap을 켜는 아틀라스는 8 px(밉 3단계까지 번짐 1 px 이하), 나머지 4 px.
- **Include in Build 끔**(모든 SpriteAtlas). Addressables가 아틀라스를 로드한다. 프리팹이 스프라이트를 직접 참조하면 아틀라스가 중복 포함되므로 UI 프리팹은 `AddressableImage.ClearSourceSpriteReference()`로 원본 참조를 비운 상태로 저장한다(Framework README).
- 아틀라스 하나가 2048을 넘으면 같은 이름 뒤에 `_2`를 붙여 나누고, 표는 행의 `atlas` 열로 가리킨다(§3.8.6 a).

### 3.8.2 공통 임포트 설정

| 항목 | 월드 스프라이트 | UI 스프라이트 | 단독 텍스처 |
|---|---|---|---|
| Texture Type | Sprite (2D and UI) | 같음 | Sprite(AddressableTexture가 Texture2D를 Sprite로 만듦) 또는 Default |
| sRGB | 켬 | 켬 | 켬 |
| Alpha Is Transparency | 켬 | 켬 | 켬 |
| Read/Write | **끔** | **끔** | **끔** |
| Generate Physics Shape | 끔 | 끔 | — |
| Mesh Type | Tight | Full Rect | Full Rect |
| Filter | Bilinear | Bilinear | Bilinear |
| Wrap | Clamp | Clamp | Clamp |
| Aniso | 0(아틀라스에서 1) | 0 | 0 |
| Max Size | 원본 크기 이상(2048) | 같음 | 2048 |
| 개별 압축 | 없음(아틀라스가 압축) | 없음 | §3.8.1 |

### 3.8.3 압축 — 결정 대기(D-54)

| 대상 | Android | iOS | 비트/화소 | 이유 |
|---|---|---|---|---|
| 월드 아틀라스(테마·고양이·로비) | ASTC 6x6 | ASTC 6x6 | 3.56 | 굵은 외곽선·평면 색이라 6x6에서 차이 작음. 메모리 절반 |
| UI 아틀라스·Fx | ASTC 4x4 | ASTC 4x4 | 8.0 | 가는 테두리·그라데이션 판·작은 아이콘의 번짐·띠 무늬 방지 |
| 방 배경 | ASTC 6x6 | 같음 | 3.56 | 큰 평면 |
| 신 초상·비네트·하늘 | ASTC 4x4 | 같음 | 8.0 | 넓은 그라데이션 띠 방지 |
| ASTC 미지원 Android(구형 GPU) | Unity가 RGBA32로 풀어 씀 | — | 32 | 대상 비율 낮음. 별도 ETC2 빌드는 만들지 않는다 |

품질 기준(§3.11과 [12 §12.4.3](12_Acceptance.md)): 압축 뒤 원본 PNG 대비 **ΔE2000 평균 ≤ 2.0, 99백분위 ≤ 10**, 알파 오차 평균 ≤ 2/255. 실패한 아틀라스만 한 단계 올린다(6x6 → 5x5 → 4x4). 그라데이션 띠는 눈으로도 확인(사람 확인, NOT_RUN 가능).

### 3.8.4 Mipmap

| 대상 | 설정 | 이유 |
|---|---|---|
| UI 전부 | 끔 | 기준 해상도 근처 크기로만 보인다. 메모리 +33% 낭비 |
| 월드 테마·고양이 | **켬**(Box 필터, 밉 스트리밍 끔) | 원근으로 128 → 23 px/m(5.5배)까지 줄어든다(§3.3.1). 밉이 없으면 먼 소품·빨래·전봇대가 반짝인다(정본은 매 프레임 벡터로 정확한 크기로 그려 반짝임이 없음) |
| 로비 | 끔 | 화면 고정 크기 |
| Fx | 켬 | 먼 곳 파편·빛 |
| 단독 텍스처 | 끔 | 화면 크기 |

### 3.8.5 Addressables 그룹·라벨·로드 수명

Managed Addressables 폴더 규칙([10 §10.6](10_Architecture.md))에 맞춘다.

| 그룹(폴더) | 내용 | 묶음 방식 | 라벨 | 로드 | 해제 |
|---|---|---|---|---|---|
| `Local/Font` | 타이틀 전용 Static 글꼴 | 함께 | `boot` | Boot | 안 함 |
| `Remote/UIAtlas` | `Atlas_UI_Common`, `Atlas_UI_Hud`, `Atlas_UI_Icon` | 따로(아틀라스별) | `ui.core` | Boot 뒤 미리 로드 | 안 함(상주) |
| 〃 | `Atlas_UI_Lobby` | 따로 | `ui.lobby` | 로비 첫 진입 | 안 함(로비 왕복이 잦음) |
| 〃 | `Atlas_UI_Card` | 따로 | `ui.card` | 신 선택·룰렛 첫 표시 | 안 함 |
| `Remote/WorldAtlas` | `Atlas_World_Theme_<key>` | 따로 | `theme.<key>` | 런 시작 전(로비에서 현재 Area 테마를 미리 로드) | 다른 테마 Area로 바뀌고 런이 끝난 뒤 |
| 〃 | `Atlas_World_Cat_T{n}`, `Atlas_World_Friend_T{n}` | 따로 | `cat` | 필요 룩만: 런 시작 시 손패 최고 t(플레이어)·t−2(친구), 손패 최고가 오르면 다음 t를 미리 | 최근 사용 3개(LRU)만 유지 |
| 〃 | `Atlas_World_Lobby` | 따로 | `lobby` | 로비 첫 진입 | 안 함(상주) |
| 〃 | `Atlas_Fx_Common` | 따로 | `fx` | Boot 뒤 | 안 함 |
| `Remote/Texture` | `Tex_*` | 따로 | `lobby`/`ui.god` | 사용 화면 | 화면 닫을 때(AddressableTexture가 반환) |
| `Remote/Audio/Sfx` | `Sfx_*` 30 | 함께 | `sfx` | Boot 뒤 전부 | 안 함 |
| `Remote/Audio/Music` | `Bgm_Lobby`, `Bgm_Run` | 따로 | `bgm` | 로비 | 안 함(Streaming) |
| `Remote/Theme/Update/<ThemeId>` | 추가 테마 팩(새 `Atlas_World_Theme_<key>`) | 따로 | `theme.<key>` | 그 테마 Area 개방 시 내려받기 | 같음 |

로드 단위는 **Area(테마)와 고양이 룩**이다. 로비 자원은 상주한다(로비 ↔ 런 전환이 0프레임이라 [10 §10.3](10_Architecture.md), 다시 로드하면 끊김).

### 3.8.6 표 → 스프라이트 해석 (아틀라스 결정 순서)

표 열에는 **스프라이트 이름**(아틀라스 안 이름) 또는 **Addressables 주소**(단독 텍스처·소리·이펙트 프리팹)를 쓴다. 아틀라스는 다음 순서로 정한다([07](07_Tables.md) 규칙 6).

1. **행의 `atlas`/`iconAtlas` 열**이 비어 있지 않으면 그 값.
2. 보상·비용 표시처럼 consumeType으로 아이콘을 찾는 경우 **`07_consumeType.iconAtlas`**.
3. **표 정의의 기본 아틀라스**(아래). 값 `@theme`은 「현재 Area의 `304_theme.worldAtlas`」라는 뜻이다.

| 표·열 | 값 형식 | 기본 아틀라스 | 해석 주체 |
|---|---|---|---|
| `01_wealth.icon`, `02_playResource.icon` | 스프라이트 이름 | `Atlas_UI_Icon` | `AddressableImage` |
| `07_consumeType.iconColumn` | 열 이름 또는 고정 스프라이트 이름 | `07_consumeType.iconAtlas` | 〃 |
| `103_item.icon` | 스프라이트 이름 | `Atlas_UI_Icon` | 〃 |
| `103_item.projSprite` | 스프라이트 이름 | `@theme` | `WorldSpriteCache` |
| `105_objectKind.sprite` | 스프라이트 이름 또는 `/` 변형 목록 | `@theme` | 〃 |
| `102_enemy.sprite` | 프레임 이름 접두(`World_Enemy_Mouse`) | `@theme` | 〃 |
| `101_cat.spritePrefix`[제안] | 접두(`World_Cat_P0`) | `Atlas_World_Cat_T{t}`/`Atlas_World_Friend_T{t}` 규칙 | 〃 |
| `110_card.icon` | 스프라이트 이름 | `Atlas_UI_Card` | `AddressableImage` |
| `113_god.portrait`[제안] | 주소 `Tex_God_*` | 단독 | `AddressableTexture` |
| `10_lobbyUpgrade.icon`, `13_areaUpgrade.icon`, `14_rebirthPerk.icon`, `05_contentOpen.icon`, `400_content.icon` | 스프라이트 이름 | `Atlas_UI_Icon` | `AddressableImage` |
| `400_content.bg` | 주소 `Tex_*` | 단독 | `AddressableTexture` |
| `304_theme.worldAtlas` | 아틀라스 주소 | — | `WorldSpriteCache` |
| `302_area.bgm`, 각 표 `*Sound` 열 | `03_sound.key` | — | `MV.Sound` |
| 각 표 `*Effect` 열 | `04_effect.key` | — | `MV.Fx` |

**검증**(MV-DATA, `TableAuthoringOptions.ValidateTables`에 등록하는 게임 검증기): 모든 리소스 열 값이 해석된 아틀라스의 `sprite_catalog.tsv`에 있는지, 주소 값이 Addressables에 있는지, `03_sound.key`·`04_effect.key`가 있는지 확인한다. `@theme` 열은 **모든** `304_theme.worldAtlas`에서 확인한다. 없으면 표 로드 실패(개발 빌드 오류 화면).

### 3.8.7 UI: AddressableImage / AddressableTexture 사용 기준

| 구분 | 쓰는 것 | 조건 |
|---|---|---|
| 아틀라스 안 스프라이트(아이콘·스킨·카드·이모지) | **`AddressableImage`** — `atlasKey`(= 아틀라스 주소) + `spriteName`, 코드에서 `SetAtlasSprite(atlas, name)` | 기본. 9-slice(Sliced)·Tiled·Filled 모두 가능 |
| 큰 단독 그림(신 초상, 컨텐츠 배너 `400_content.bg`, 로비 방 배경을 UI로 쓸 때) | **`AddressableTexture`** — `spriteKey`(= 텍스처 주소) | 한 장이 512 px 이상이거나 한 화면에서만 쓰고 닫으면 버리는 그림. Texture2D에서 만든 Sprite는 Full Rect·경계 없음이라 **9-slice 불가**(Framework README) → Simple로만 |
| 등급·상태 색 변형 | 굽힌 변형 스프라이트로 `spriteName` 교체 | `Image.color` 곱은 흰 단색 도형에만 |

`AddressableImage`는 `GetSprite`가 돌려준 복제본을 교체·파괴 시 스스로 반환한다(Framework README). 게임 코드가 아틀라스에서 직접 `GetSprite`하지 않는다.

### 3.8.8 월드 SpriteRenderer 로더 `WorldSpriteCache` (게임 구현)

Framework에는 월드 `SpriteRenderer`용 아틀라스 로더가 없다(`AddressableImage`는 UGUI `Image` 전용). `MV.View`에 게임 구현을 둔다.

```csharp
// MV.View — 게임 구현. Framework 미사용 이유: ui.addressables의 AddressableImage는 UGUI Image 전용
public sealed class WorldSpriteCache : IDisposable {
    // atlasKey → (lease, name → Sprite 복제본)
    readonly Dictionary<string, Entry> _atlases;
    public async UniTask<bool> LoadAtlasAsync(string atlasKey, CancellationToken ct);
        // AddressableScope.Get<SpriteAtlas>(atlasKey, ct) → AddressableLease<SpriteAtlas> 보관
    public Sprite Get(string atlasKey, string spriteName);
        // 캐시에 없으면 lease.Asset.GetSprite(name) 1회 호출 후 캐시. 이후 같은 Sprite 반환
    public void ReleaseAtlas(string atlasKey);
        // 캐시한 Sprite 복제본 전부 Object.Destroy → lease.Dispose()
    public void Dispose();
}
```

| 규칙 | 이유 |
|---|---|
| `SpriteAtlas.GetSprite`는 **호출할 때마다 새 Sprite 복제본을 만든다.** 이름당 1회만 부르고 캐시한다 | 매 프레임·매 생성마다 부르면 Sprite 객체가 쌓이고 GC·메모리 누수 |
| 프레임 묶음(`World_Cat_*_Run_A_00..11`)은 로드 직후 배열로 한 번에 캐시 | 런 중 할당 0B([12 §12.7](12_Acceptance.md)) |
| `@theme` 해석은 현재 테마 키로 `304_theme.worldAtlas`를 읽어 `atlasKey`로 바꾼다. 테마가 바뀌면 새 아틀라스를 먼저 로드하고, 이전 테마 오브젝트가 화면에서 모두 사라진 뒤 `ReleaseAtlas` | 같은 이름 규칙(P8) 덕분에 오브젝트 코드는 이름만 안다 |
| 반환은 복제본 `Destroy` 뒤 lease `Dispose` 순서 | 아틀라스 텍스처가 내려간 뒤 Sprite가 남지 않게 |
| 로드 실패 시 빈 Sprite 대신 `World_Missing`(분홍 X, 모든 테마 아틀라스에 포함) | 개발 중 누락을 화면에서 바로 보이게 |

### 3.8.9 배칭을 위한 머티리얼 공유 규칙

| 규칙 | 내용 |
|---|---|
| 월드 스프라이트 머티리얼 2개 | `MV_WorldSprite`(알파 블렌드, 안개 알파를 셰이더가 뷰 깊이로 계산 — 오브젝트별 속성 없음), `MV_WorldAdditive`(빛). 모든 월드 `SpriteRenderer`가 둘 중 하나의 **공유 머티리얼**을 쓴다 |
| 색·알파 | `SpriteRenderer.color`(정점색)로만 바꾼다. **`MaterialPropertyBlock`·머티리얼 복제 금지**(배칭이 깨짐) |
| 같은 텍스처 연속 | 깊이 정렬 순서에서 아틀라스가 바뀌는 지점마다 배치가 끊긴다 → 런 월드 그림을 **테마 아틀라스 하나**에 모은 이유(D-58). 고양이·이펙트만 다른 아틀라스 |
| 메쉬(지면·집·담장·게이트 배경) | `MV_WorldVertexColor` 머티리얼 1개 공유, 정점색. 집·담장은 구간(청크)별로 하나의 메쉬로 합쳐 다시 만든다([02](02_Visual.md)) |
| UI | 모든 `AddressableImage`가 기본 UI 머티리얼. 커스텀 UI 셰이더·`Mask` 남용 금지(`RectMask2D` 우선). 한 화면은 가능한 한 같은 아틀라스에서 |
| TMP | 글꼴 에셋당 머티리얼 1개(외곽선 있는 것·없는 것 2개까지). 라벨마다 머티리얼 인스턴스 금지 |

### 3.8.10 드로우콜 예산

측정: Frame Debugger와 `ProfilerRecorder`(`Batches Count`, `SetPass Calls Count`), 기준 기기·기준 장면([12](12_Acceptance.md) W-장면).

| 화면 | Batches 목표 | 상한 | SetPass 상한 | 구성 [추정] |
|---|---|---|---|---|
| 런(일반 골목) | ≤ 60 | 80 | 20 | 지면 1~2, 집·담장 메쉬 4, 월드 스프라이트(테마 아틀라스 + 고양이 2 + Fx 사이 끊김) 25~35, 라벨 TMP 8, 가산 빛 2, 후처리(틴트·눈) 2, HUD 10~15 |
| 런(우다다 비행) | ≤ 40 | 60 | 16 | 하늘·지붕·계단 메쉬 5, 롤·별 스프라이트 10, 고양이 3, HUD |
| 로비 | ≤ 30 | 45 | 12 | 방 텍스처 1, 아지트 아틀라스 1~4, UI 15 |
| 팝업(신 선택·지도) | ≤ 35 | 50 | 12 | |

### 3.8.11 메모리 예산 (텍스처 총량, 런 화면)

ASTC 6x6 2048² = 1.87 MB(밉 포함 2.49 MB), 4x4 1024² = 1.0 MB, 2048×1024 6x6 = 0.93 MB(밉 1.24 MB).

| 항목 | 개수 | 크기 | 합 |
|---|---|---|---|
| 테마 월드 아틀라스 | 1(전환 중 2) | 2.49 MB | 2.5~5.0 MB |
| 플레이어 고양이 | 최대 3(LRU) | 1.24 MB | 3.7 MB |
| 친구 고양이 | 최대 3 | 2.49 MB | 7.5 MB |
| Fx | 1 | 0.67 MB(1024×512, 밉) | 0.7 MB |
| UI 상주(Common·Hud·Icon) | 3 | 1.0 MB | 3.0 MB |
| UI Lobby·Card | 2 | 1.0 MB | 2.0 MB |
| 로비 상주(아지트 아틀라스 + 방 텍스처) | — | 1.87 + 0.92 MB | 2.8 MB |
| TMP 글꼴(§3.10) | 4 | Alpha8 | 7.0 MB |
| 렌더 타깃(1080×1920 색·깊이, 후처리 1장) | — | — | 약 24 MB(텍스처 메모리 측정에서 분리 표기) |
| **합(렌더 타깃 제외)** | | | **약 30~33 MB** |

- **목표: 텍스처 총량(렌더 타깃 제외) ≤ 48 MB, 상한 64 MB.**
- 오디오: 효과음 30개 Decompress On Load PCM 합 약 1.0 MB, BGM Streaming 버퍼만.
- Memory Profiler 스냅샷에서 `Texture2D` 항목을 아틀라스별로 확인하고, 이름이 `(Clone)`인 Sprite 수가 「캐시된 이름 수 + 화면의 AddressableImage 수」를 넘지 않는지 본다.

### 3.8.12 검수 방법

| 검사 | 도구 | 기준 |
|---|---|---|
| 배치 수·끊긴 이유 | Frame Debugger(Batch break cause) | §3.8.10. 「다른 텍스처」 외의 끊김 원인(머티리얼 다름, MPB)이 0 |
| 텍스처 메모리 | Memory Profiler 패키지 스냅샷(런 Area 1 Floor 1 중간, Area 4 밤, 우다다 비행, 로비) | §3.8.11 |
| 중복 아틀라스 | Addressables Analyze `Check Duplicate Bundle Dependencies` | 중복 0 |
| 아틀라스 채움률 | SpriteAtlas 미리보기 페이지 수, `sprite_catalog.tsv` | 각 아틀라스 1페이지, 채움 60% 이상(미만이면 한 단계 작은 크기) |
| Sprite 복제 누수 | 런 10판 반복 후 스냅샷 | `Sprite` 수 증가 0 |
| 압축 품질 | §3.11.2 | ΔE 기준 |

## 3.9 소리 변환

### 3.9.1 굽기 방식 (MV-TOOL-SND)

정본 `SND`(L1622~1696)는 즉시 실행 함수 안에 갇혀 있어 `tone`·`noise`·`LIB`에 바깥에서 닿을 수 없다. 그래서 **오디오 컨텍스트 생성자를 바꿔 끼워** 정본 코드가 스스로 OfflineAudioContext에 그리게 한다.

1. `init.js`가 `window.AudioContext`와 `webkitAudioContext`를 다음 생성자로 바꾼다: `new OfflineAudioContext({ numberOfChannels: 1, length: ceil(L × 44100), sampleRate: 44100 })`를 만들고 `window.__off`에 보관.
2. `init.js`가 `setInterval`을 감싸 지연이 50 ms인 호출(BGM 스케줄러 `sched`, L1635)은 등록만 하고 `window.__sched`에 보관한다(효과음 렌더에 BGM이 섞이지 않게).
3. 효과음: 페이지 로드 → `SND.init()`(공개 L1695) → `SND.play(key, a)` → `__off.startRendering()` → 결과 버퍼를 16bit PCM WAV로 저장. 효과음 하나마다 페이지를 새로 연다(컨텍스트가 하나뿐이고 `init`이 다시 만들지 않음, L1627). **재생은 1.0014 s(128×345 샘플) 무음 뒤에 하고 그 앞을 잘라 낸다**: 컴프레서가 컨텍스트 시작 직후 감쇠 상태에서 약 0.5 s에 걸쳐 풀리므로 시각 0에 재생하면 정본 실시간보다 작게 구워진다(click −13 dB). (MV-TOOL-SND 실측, 총괄 결정)
4. BGM: `SND.intensity = 0|1` 설정 → `SND.init()` → `__off.suspend(t)`를 0.05 s 간격(렌더 단위 128 샘플로 반올림)으로 걸고, 멈출 때마다 `__sched()`를 불러 실시간 스케줄과 같게 예약 → 전체 렌더. 정본은 `next = currentTime + 0.1`(L1635)에서 시작하므로 첫 루프가 아니라 **두 번째 루프 [0.1 s + n, 0.1 s + 2n)**(n = 16.5517 s, 729,931 샘플)을 잘라 1루프로 쓴다. 첫 루프에는 컴프레서 시작 감쇠가 섞이고, 두 번째 루프가 반복 재생 이음매와 같다. (MV-TOOL-SND, 총괄 결정)
5. 출력 수준은 **정본 그대로 들리는 크기**: 버스 게인(효과음 0.6, BGM 0.15, L1632~1633)과 컴프레서(threshold −16 dB, ratio 5, 나머지 기본값, L1630)가 렌더에 포함된다. 그래서 `03_sound.volume = 1.0`이 정본 크기다.
6. 잡음 버퍼(`Math.random`)는 시드 고정(3.2.2) → 한 번 구운 결과로 고정한다. 정본은 재생마다 잡음 모양이 조금씩 다르지만 들리는 차이는 없다고 본다 [추정]. 한 노드에 입력이 셋 이상 모이면 Chromium이 더하는 순서가 실행마다 달라 1~2 ULP 흔들리므로, 굽기 도구는 그 연결을 이득 1 GainNode 사슬(입력 2개 이하)로 바꾼다. 값은 같다.
7. 뒤쪽 무음 정리: **하지 않는다**(`snd.json` `trim.enabled=false`). 길이는 3.9.3 표 길이를 따른다. 켜면 wood·land가 표 길이보다 짧아지고, 잘리는 꼬리는 −90 dBFS 미만이라 들리는 차이가 없다. (MV-TOOL-SND, 총괄 결정)

| 형식 | 값 |
|---|---|
| 샘플레이트 | 44100 Hz |
| 채널 | 모노(정본 발진기·잡음이 모노) |
| 원본 | WAV 16bit PCM, `Addressables/Remote/Audio/Sfx·Music/<soundName>.wav`에 바로 쓴다. 해시 `Tools/MvExtract/sound/manifest.json` |
| 빌드 안 형식 | **Unity 오디오 임포터가 Vorbis로 압축**(D-56). 효과음 Quality 70, Decompress On Load, Force To Mono, Preload; BGM Quality 60, **Streaming**, Load In Background |
| 피치 무작위 | 정본에 없음 → `pitchRand = 0`. 굽지도, 재생 시 적용하지도 않음 |
| 음 높이 인자 있는 소리 | `tierup(t)`, `card(t)`는 인자마다 따로 굽는다. 재생 피치로 바꾸면 길이도 바뀌어 정본과 달라진다 |

### 3.9.2 효과음 키 세기

정본 `LIB`(L1648~1676)의 키는 **28개**다. 호출(`SND.play`, 40곳) 대조 결과:

| 구분 | 키 | 개수 |
|---|---|---|
| 호출됨, 인자 없음 | throw, hit, crit, wood, coin, gem, soul, gate, forge, clank, lvup, land, tick, merge, buy, click, warn, steal, fail, win, floor | 21 |
| 호출됨, 인자 있음 | tierup(t = 1~4: L2281 `t > C.tier`라 1부터, L3165 `min(4, st+1)`), card(t = 0~4: L2277 `4`, L2369 `c.pick.t`) | 2 → 클립 4 + 5 = 9 |
| 호출 없음(잔재) | load, fuse, boom, stamp(L2217의 `stamp()`는 다른 함수), whoosh | 5 |

→ **구울 효과음 클립 30개**(21 + 9), BGM 2개, `03_sound` 행 **32개**. [01](01_Scope.md)·[10](10_Architecture.md)의 「효과음 31종」은 이 표로 바로잡는다(§3.12 가정).

### 3.9.3 `03_sound` 행 (확정)

열: `id, key, soundType, soundName(주소), volume, pitchRand, gapMs, 비고`. id 형식 `3{그룹 3자리}{순번 3자리}` — 그룹 001 효과음, 002 BGM. 길이 L = 정본 정지 시각 `max(at + d + 0.05)`(L1641, L1647).

| id | key | soundType | soundName | volume | pitchRand | gapMs | 길이 L(s) | 정본 정의(L1649~1676) · 호출 |
|---|---|---|---|---|---|---|---|---|
| 3001001 | throw | Sfx | Sfx_Throw | 1.0 | 0 | 70 | 0.12 | 잡음 BP 1800→4200 · L2091 |
| 3001002 | hit | Sfx | Sfx_Hit | 1.0 | 0 | 35 | 0.12 | 삼각 230→110 + 잡음 · L2107 |
| 3001003 | crit | Sfx | Sfx_Crit | 1.0 | 0 | 50 | 0.15 | 사각 340→140 + 잡음 + 사인 1250→1900 · L2107 |
| 3001004 | wood | Sfx | Sfx_Wood | 1.0 | 0 | 50 | 0.29 | 잡음 LP 1500→260 + 사인 125→52 + 사각 · L1936, L2068 |
| 3001005 | coin | Sfx | Sfx_Coin | 1.0 | 0 | 45 | 0.27 | 사각 G6 → C7 · L1925, L1941, L1966 |
| 3001006 | gem | Sfx | Sfx_Gem | 1.0 | 0 | 60 | 0.35 | 사인 A6·C#7·E7 · L1939, L1964 |
| 3001007 | soul | Sfx | Sfx_Soul | 1.0 | 0 | 20 | 0.61 | 사인 C5·G5·C6·G6 + 잡음 HP · L1937 |
| 3001008 | gate | Sfx | Sfx_Gate | 1.0 | 0 | 20 | 0.33 | 삼각 392→784, 784→1175 · L2046, L2260 |
| 3001009 | forge | Sfx | Sfx_Forge | 1.0 | 0 | 20 | 0.26 | 사각 880 + 잡음 + 삼각 660 · L2051 |
| 3001010 | clank | Sfx | Sfx_Clank | 1.0 | 0 | 60 | 0.13 | 사각 640→390 + 잡음 BP · L1912, L1919 |
| 3001011 | lvup | Sfx | Sfx_Lvup | 1.0 | 0 | 20 | 0.37 | 사각 C5·E5·G5·C6 · L1221, L1890, L2553, L2613 |
| 3001012 | tierup_1 | Sfx | Sfx_Tierup_1 | 1.0 | 0 | 20 | 0.32 | b = 523·2^(1/5) = 600.8 Hz, 삼각 b + b×1.5 · L2281, L3165 |
| 3001013 | tierup_2 | Sfx | Sfx_Tierup_2 | 1.0 | 0 | 20 | 0.32 | b = 690.1 Hz |
| 3001014 | tierup_3 | Sfx | Sfx_Tierup_3 | 1.0 | 0 | 20 | 0.32 | b = 792.7 Hz |
| 3001015 | tierup_4 | Sfx | Sfx_Tierup_4 | 1.0 | 0 | 20 | 0.32 | b = 910.6 Hz |
| 3001016 | land | Sfx | Sfx_Land | 1.0 | 0 | 20 | 0.35 | 잡음 LP 700→150 + 사인 110→60 · L2286 |
| 3001017 | tick | Sfx | Sfx_Tick | 1.0 | 0 | 20 | 0.075 | 사각 1400 · L2270, L2368 |
| 3001018 | card_0 | Sfx | Sfx_Card_0 | 1.0 | 0 | 20 | 0.52 | r = 0(C) 장3화음+8도 아르페지오 · L2369 |
| 3001019 | card_1 | Sfx | Sfx_Card_1 | 1.0 | 0 | 20 | 0.52 | r = 2(D) |
| 3001020 | card_2 | Sfx | Sfx_Card_2 | 1.0 | 0 | 20 | 0.52 | r = 4(E) |
| 3001021 | card_3 | Sfx | Sfx_Card_3 | 1.0 | 0 | 20 | 0.52 | r = 5(F) |
| 3001022 | card_4 | Sfx | Sfx_Card_4 | 1.0 | 0 | 20 | 0.52 | r = 7(G) · L2277, L2369 |
| 3001023 | merge | Sfx | Sfx_Merge | 1.0 | 0 | 20 | 0.22 | 사각 660→990 + 삼각 990→1320 · L1193, L2644 |
| 3001024 | buy | Sfx | Sfx_Buy | 1.0 | 0 | 20 | 0.20 | 사각 880, 1320 · L2377, L2530, L2644 |
| 3001025 | click | Sfx | Sfx_Click | 1.0 | 0 | 20 | 0.08 | 사각 900 · L2444, L2445, L2610, L3856, `UIButtonSound` 기본 |
| 3001026 | warn | Sfx | Sfx_Warn | 1.0 | 0 | 20 | 0.25 | 사각 880 → 660 · L2739 |
| 3001027 | steal | Sfx | Sfx_Steal | 1.0 | 0 | 20 | 0.21 | 사각 700→300 · L2063 |
| 3001028 | fail | Sfx | Sfx_Fail | 1.0 | 0 | 20 | 0.65 | 삼각 G4·E4·C4·G3 하행 · L2408(350 ms 지연) |
| 3001029 | win | Sfx | Sfx_Win | 1.0 | 0 | 20 | 0.63 | 사각 C5·E5·G5·C6·E6 · L2408, L2563 |
| 3001030 | floor | Sfx | Sfx_Floor | 1.0 | 0 | 20 | 0.67 | 삼각 C4·G4·C5 · L2028 |
| 3002001 | bgm_lobby | Music | Bgm_Lobby | 1.0 | 0 | 0 | 16.5517(루프) | intensity 0 믹스 · L2473 |
| 3002002 | bgm_run | Music | Bgm_Run | 1.0 | 0 | 0 | 16.5517(루프) | intensity 1 믹스 · L2661 |

gapMs 출처: `GAP`(L1677) — throw 70, hit 35, crit 50, wood 50, coin 45, tick 20, clank 60, gem 60. 없는 키는 20(L1678 `|| 0.02`).

### 3.9.4 BGM

| 항목 | 값(L1680~1692) |
|---|---|
| 템포 | 116 BPM, 1 step = 8분음표 = 60/116/2 = 0.25862 s |
| 루프 | 64 step = **16.5517 s** = 729,931 샘플(44.1 kHz, 반올림. 루프당 0.03 샘플 오차 무시) |
| 화성 | C–Am–F–G(ROOT [0,9,5,7], 6 초과는 한 옥타브 아래) |
| 베이스 | 4 step마다 삼각 `130.8·2^(r/12)/2` Hz, 0.3 s, v 0.55 |
| 아르페지오 | 2 step마다 사각 523.3 Hz 기준, ARP [0,1,2,3,2,1,2,1], 0.16 s, v **0.035(로비) / 0.05(런)** |
| 런 추가 | 하이햇(홀수 step 잡음 HP 7000), 킥(step 0·8 사인 120→45), 스네어(step 4·12 잡음 BP 1800) |
| 루프 경계 | 마지막 음 끝이 16.38 s(하이햇 step 63) < 16.55 s → 경계를 넘는 꼬리 없음, 이음매 없음 [계산] |

**두 믹스 파일 + 같은 위치 전환**(권장, D-57). 로비 믹스와 런 믹스를 따로 굽는다. 아르페지오 음량이 0.035 → 0.05로 바뀌는 것은 지수 엔벨로프라 「드럼 층을 더하는 방식」으로는 정확히 합쳐지지 않는다(감쇠 곡선이 v에 따라 달라짐, L1638). 전환 규칙(`MV.Sound`):

1. `AudioManager.PlayMusicAsync("Bgm_Lobby")`로 시작(loop).
2. intensity가 바뀌면 현재 `AudioSource.timeSamples`를 읽고 `PlayMusicAsync(다른 키, crossfadeSeconds: 0.03)`의 반환 `AudioSource.timeSamples`에 같은 값을 넣는다. 정본도 다음 step부터 바뀐다(0.25 s 예약 창, L1683).
3. 음소거 중에도 step은 진행된다(L1684) → Unity는 음소거를 트랙 볼륨 0(`SetTrackOn(Music,false)`)으로 하고 재생은 계속한다.

### 3.9.5 재생 규칙 (MV.Sound)

| 규칙 | 내용 |
|---|---|
| 호출 | `AudioManager.Instance.PlayAsync(row.soundName, AudioTrack.Sfx, row.volume, 1f, false, debounce: 0f)` |
| 최소 간격 GAP | **재생 쪽에서 처리.** `MV.Sound`가 「GAP 묶음 키」별 마지막 재생 시각을 `Time.unscaledTime`으로 기록하고 `gapMs` 안의 요청은 버린다(큐잉 없음, L1678). GAP 묶음 키 = `key`에서 끝의 `_숫자`를 뗀 것(`tierup_3` → `tierup`) — 정본은 인자와 상관없이 `tierup` 하나로 간격을 잰다 |
| Framework debounce | 키·클립 debounce가 겹치지 않게 `debounce: 0f`로 끈다(Framework audio README: 키 debounce는 전체 문자열 기준) |
| 트랙 | 효과음·UI 클릭 모두 `Sfx`(정본은 버스 하나 + 음소거 버튼 하나, L713). BGM은 `Music` |
| 음소거 | 정본 `ce_mute`(L1624) → 저장 설정([09](09_Save.md)). 음소거 중 효과음은 호출 자체를 막는다(L1678) |
| 지연 재생 | `fail` 350 ms(L2408), 로비 덱 머지 `merge` 450 ms(L2644)는 호출하는 쪽이 지연 |
| 컴프레서 | 클립에 이미 포함. Master에 추가 압축을 걸지 않는다(겹쳐 울릴 때의 상호 압축 차이는 허용 [추정]) |

### 3.9.6 잔재 사운드 판정 — 결정 대기(D-55)

| key | 정의 | 판정 |
|---|---|---|
| load | 삼각 170→90 + 잡음 LP 900 | 굽지 않음 |
| fuse | 잡음 HP 5000→8000 0.6 s | 굽지 않음 |
| boom | 잡음 LP + 사인 88→30 + 사각, 1.05 s | 굽지 않음 |
| stamp | 사각 196→392 + 294→587 | 굽지 않음 |
| whoosh | 잡음 BP 380→2400 1.6 s | 굽지 않음 |

`03_sound` 행을 만들지 않는다. 도구 `jobs/snd.json`에는 `"enabled": false`로 정의를 남겨, 추가 콘텐츠([08](08_Content.md))가 쓰면 켜고 행을 더한다.

## 3.10 글꼴

| 항목 | Black Han Sans | Gothic A1 |
|---|---|---|
| 정본 쓰임 | 제목·숫자 `--f-display`(A 노트 L19), 라벨 기본 글꼴(L1031) | 본문 `--f-body` 400·700·800(L6, L20), 캔버스 900 요청(L3496 등 → 800으로 표시 [추정]) |
| TMP 에셋 | `Font_BlackHanSans_SDF` | `Font_GothicA1_Regular_SDF`(400), `_Bold_SDF`(700), `_ExtraBold_SDF`(800, 900 요청도 이것) |
| 방식 | **Static 부분 집합** + **Dynamic 대체** | 같음 |
| 글리프 범위 | ① ASCII 0x20~0x7E ② 숫자·기호 `·×+−%→♪` 등 Localization에 쓰인 기호 ③ **Localization `name`·`description`·`ui`·`msg`·`toastMessage` 컬렉션 ko 문자열에 쓰인 한글 전부**(빌드 전 Editor 도구가 수집) | 같음 |
| 대체(Fallback) | 원본 TTF로 만든 Dynamic SDF(1024×1024 시작, 다중 아틀라스) — 표에 없던 글자(닉네임 등) | 같음 |
| SDF 설정 | Sampling 64 pt, Padding 6, SDF32, Atlas 1024×1024(예상 한글 600자 내외 [추정]) | Regular·ExtraBold 2048×1024, Bold 1024×1024 |
| 메모리 | Alpha8 1 MB | 2 + 1 + 2 MB → 합 약 6~7 MB |
| 합성 볼드 | 정본 `<b>` 안 Black Han Sans는 브라우저 합성 볼드(A 노트 §1). TMP `FontStyles.Bold` + 에셋 `boldStyle` 값으로 맞춘다(캡처 대조로 값 결정) | 900 요청 → ExtraBold |
| 위치 | `Addressables/Remote/Font/`(게임용). 타이틀 전용 Static은 `Local/Font`([10 §10.6](10_Architecture.md)) | |

라이선스 확인 항목(작업 계약 MV-UI-HUD 이전, 사람 확인):

| # | 확인 | 근거·방법 |
|---|---|---|
| 1 | 두 글꼴 모두 **SIL Open Font License 1.1**인지 | 각 글꼴 저장소의 `OFL.txt` 원문 |
| 2 | Reserved Font Name(RFN) 선언 여부 | `OFL.txt` 첫머리 저작권 줄. RFN이 있으면 부분 집합 글꼴 **파일**을 원래 이름으로 배포하지 않는다(TMP 아틀라스는 래스터 결과지만 Dynamic 대체로 TTF를 넣으면 해당) |
| 3 | 앱에 TTF를 넣는 경우 저작권 고지·라이선스 전문 포함 | OFL 조건 2. 앱 설정 「오픈소스 라이선스」 화면에 `OFL.txt` 전문 |
| 4 | 글꼴 단독 판매 금지 조건과 충돌 없음 | 게임에 포함 배포는 허용 |
| 5 | Noto Color Emoji(D-52)도 OFL 1.1 | 같은 방식 고지 |
| 6 | 굽는 그림의 `sans-serif` 글자(`50%`)에 쓰는 글꼴 | vendor 글꼴을 명시 지정(Gothic A1 700 권장 [추정])해 OS 글꼴 의존 제거 |

## 3.11 검수

### 3.11.1 추출 PNG ↔ 정본 대조 (도구 안, `npm run verify`)

| 단계 | 방법 | 기준 |
|---|---|---|
| V1 결정성 | 같은 job 2회 실행 PNG SHA256 비교 | 완전 일치 |
| V2 감싸기 무결성 | `project` job마다 감싸기 없이(`k = 1`이 되게 `H`를 `2·S·d/P5`로 맞춤) 정본 함수를 주 캔버스에 그린 결과를 피벗 기준으로 잘라 추출 PNG와 비교 | 화소 완전 일치 목표. 감싸기·평행이동 경로의 부동소수 반올림으로 안티앨리어싱 화소만 다르면 ΔE2000 최대 ≤ 3을 통과로 본다(2026-11-09 총괄 결정, MV-TOOL-ART 실측 19개 중 7개가 2~20화소·ΔE 최대 2.98)([12 §12.4.3](12_Acceptance.md)) |
| V3 장면 대조 | 정본 실제 런 장면을 고정 상태(같은 시드·같은 위치)로 캡처하고, 같은 장면을 추출 스프라이트를 정본 투영 위치·크기에 찍어 합성한 그림과 비교 | ΔE2000 평균 ≤ 1.0(샘플링 차이만) |
| V4 억제 규칙 | 억제한 그림 + 따로 뽑은 부품을 합성한 결과 = 억제 없는 원래 그림 | ΔE2000 99백분위 ≤ 3(캔버스 전체 기준. 회전 합성 재표본화로 물체 화소만 센 값이 조금 넘는 것은 기록만 한다 — 2026-11-09 총괄 결정) |
| V5 프레임 | 프레임 i를 정본 `ph = v_i`로 직접 그린 것과 비교, 고리 이음매 오차 기록 | 완전 일치, 이음매는 manifest |
| V6 소리 | 구운 WAV를 정본 실시간 녹음(같은 시드, AudioWorklet으로 PCM을 그대로 받음 — MediaRecorder는 Opus 손실이라 쓰지 않음)과 비교: 길이 ±1 ms, RMS 차 ≤ 0.5 dB, 스펙트럼 상관 ≥ 0.98 | 청취 확인은 사람 확인(NOT_RUN 가능) |

### 3.11.2 Unity 쪽 대조 (MV-ART-ATLAS, MV-VIEW)

- **압축 품질**: Editor 도구가 아틀라스에서 각 스프라이트를 `Texture2D`로 읽어(에디터 전용 읽기) 원본 PNG와 ΔE2000 비교 → §3.8.3 기준. 결과는 `QA/Reports/atlas_quality.tsv`.
- **화면 대조**: [12 §12.4](12_Acceptance.md)의 장면 캡처 대조(MV-QA-HARNESS). 그림 차이 원인이 「그림 추출」이면 이 문서 계약(MV-TOOL-ART)으로 되돌린다.

### 3.11.3 재추출 자동화

| 상황 | 명령·동작 |
|---|---|
| 정본이 바뀜 | SHA 불일치로 중단 → 사람이 `--accept-canon <sha>` → 전체 재추출 → V1~V6 → 바뀐 PNG 목록(이전 manifest와 sha256 비교) 보고 |
| 테마 추가·색 변경 | `npm run art -- --theme <key>`(§3.6.4) |
| 카드·무기 추가([08](08_Content.md)) | `jobs/ui_card.json`에 행 추가 → `--only Card_*` |
| 일부만 | `--only <glob>`, `--group <아틀라스>` |
| Unity 반영 | PNG·manifest가 바뀌면 `MvArtPostprocessor`가 설정 재적용, SpriteAtlas는 다음 Pack/빌드 때 갱신. 스프라이트 이름이 사라지면 표 검증기(§3.8.6)가 실패시킨다 |

산출 보고: `Tools/MvExtract/out/report.md`(job 수, 실패, 바뀐 이름, 아틀라스별 예상 화소 합). 커밋은 `Art/Source/`(PNG·WAV·manifest·catalog)만.

## 3.12 의문점·결정 필요

### 결정 목록

| ID | 질문 | 선택지 | 권장안 | 영향 |
|---|---|---|---|---|
| **D-50** | 추출·굽기 도구 설치와 외부 파일 내려받기 승인 | A 승인: Node.js LTS, Playwright(고정 버전)+Chromium, three.js r128, Black Han Sans·Gothic A1 글꼴 파일, Noto Color Emoji / B 거절: 사람이 브라우저에서 수동 캡처 | **A** | A: 모든 그림·소리를 결정적으로 자동 생성, 재추출 가능. 대가: PC에 도구 설치(약 300 MB). B: 수백 장 수동 작업, 화소 일치 검증 불가. MV-QA-HARNESS도 같은 설치를 쓴다 |
| **D-51** | 런 고양이(뒷모습) 프레임 방식 | A 전량 플립북(플레이어 296 + 친구 600프레임, 룩별 아틀라스) / B 파츠 분해(몸·머리·꼬리·발·모자) + 런타임 조립 / C 무늬 팔레트 교체 셰이더 | **A** | A: 정본과 화소 일치, 구현 단순. 대가: 디스크 약 25 MB(밉 포함), 런 중 고양이 텍스처 약 11 MB. B: 메모리 1/10이지만 정본 함수가 한 번에 그려 억제 규칙이 매우 많아지고 조립 오차. C: 외곽선 안티에일리어싱 경계에서 색 번짐 |
| **D-52** | 이모지(🧺🗺️🔥🌿🐾🔒🎯🐱💥⚔️🗡️🧱🛡️🌙🐣, ✕♪▶♥✦) 대체 | A Noto Color Emoji(OFL)로 같은 도구에서 굽기 / B 달동네 화풍으로 새로 그리기(창작) / C OS 이모지 글꼴 그대로 TMP | **A** | A: 모든 기기 같은 모양, 라이선스 명확. 정본(기기 이모지)과 모양은 기기마다 다름. B: 화풍 통일, 작업량. C: 기기마다 다르고 TMP 컬러 이모지 지원이 약함 |
| **D-53** | 새 테마·테마 색 변경 처리 | A 표 색으로 **재추출(굽기)** + 테마 아틀라스 배포 / B 외곽선·하이라이트를 분리한 마스크 텍스처 + 런타임 색 입히기 셰이더 | **A** | A: 정본과 같은 그림, 셰이더 단순, 배칭 유지. 대가: 테마 추가마다 도구 실행(코드 수정은 없음). B: 표만 고치면 즉시 반영되지만 마스크 분해 오차·셰이더 복잡·UI 배칭 문제 |
| **D-54** | 텍스처 압축 기준 | A 월드 ASTC 6x6 / UI·Fx 4x4 / B 전부 4x4 / C 전부 6x6 | **A** | A: 메모리와 선명도 균형(합 약 30 MB). B: 월드 메모리 2.2배. C: UI 테두리·그라데이션 번짐 |
| **D-55** | 잔재 효과음 5종(load, fuse, boom, stamp, whoosh) | A 굽지 않음(정의만 도구에 보관) / B 굽어 두기 | **A** | A: 범위 고정. 08이 쓰면 그때 켬. B: 미사용 자원 5개 |
| **D-56** | 오디오 빌드 형식 변환 위치 | A WAV 원본 → Unity 임포터가 Vorbis 압축 / B 외부 ffmpeg로 OGG 변환 후 넣기 | **A** | A: 추가 설치 없음, 품질 값을 Unity에서 조정. B: ffmpeg 설치(외부), 이중 압축 위험 |
| **D-57** | BGM 런 강도 전환 | A **두 믹스 파일**(Bgm_Lobby, Bgm_Run) + 같은 위치 전환 / B 기본 층 + 드럼 층 동시 재생, 드럼 볼륨 0/1 | **A** | A: 정본과 같은 소리(아르페지오 0.035/0.05 포함). 파일 2개(약 260 KB). B: 아르페지오 크기 차이를 정확히 못 냄, 두 소스 동기 유지 필요 |
| **D-58** | 월드 아틀라스 구성 | A 테마마다 **런 월드 그림 전부를 담은 아틀라스 1장**(공통 그림을 테마마다 복제) / B 공통 아틀라스 + 테마 차이 아틀라스 | **A** | A: 깊이 정렬 중 아틀라스 전환이 줄어 배치 수 감소, 「이름 같게 + worldAtlas만 교체」 규칙이 단순. 대가: 빌드 용량 테마당 약 2.5 MB 중복. B: 용량 절약, 배치 끊김 증가(소품·오브젝트가 깊이로 섞임) |
| **D-59** | `drawBox` 계열(집·관문 발판)·담장 면·지면 처리 | A 메쉬 + 정점색(정본 공식 그대로) / B 크기·색 조합별 스프라이트 굽기 / C 진짜 3D 큐브 | **A** | A: 연속 크기·테마 색을 정확히, 메모리 거의 0. B: 조합 폭발. C: 정본의 「기운 윗면 고정」 모양과 달라짐(E 노트 의문 9). [02](02_Visual.md)와 같이 확정 |

### 다른 문서에 기대한 가정

| 대상 | 가정 |
|---|---|
| [01](01_Scope.md), [10](10_Architecture.md) | 「효과음 31종」 → **효과음 클립 30개 + BGM 2개 = `03_sound` 32행**(LIB 28키 중 사용 23, 잔재 5)으로 고친다 |
| [02](02_Visual.md) | 상자 빌보드 메쉬(`drawBox` 재현), 담장 쿼드 메쉬, 지면 정점색 메쉬, 게이트 배경 메쉬, 안개·틴트·가산 빛 셰이더, 뷰 깊이 정렬, 런 기준 카메라 (0,13,−9)→(0,0.5,5.5) FOV 52를 02가 정의한다. 집 문·창·옥상 배치 공식(L3594~3599)도 02 |
| [05](05_Screens.md) | UI 기준 1080×1920, UI 스킨 전체 목록·9-slice 사용처, 라벨 TMP 스타일 |
| [06](06_Effects.md) | `04_effect` 프리팹이 `Atlas_Fx_Common` 스프라이트를 쓰고, 우다다 하늘·지붕은 셰이더·메쉬 |
| [07](07_Tables.md) | 제안 열: `101_cat.spritePrefix`, `101_cat.lobbySprite`, `113_god.portrait`, `10_lobbyUpgrade.facSprite`, `105_objectKind.tint`(게이트 배경·관문 발판 색), `304_theme.worldAtlas`·`roadsideProps`·`lineKind`, `07_consumeType.worldBadge`, `110_card.projSprite`. 표 정의에 「기본 아틀라스」 칸(값 `@theme` 허용). 리소스 열 검증기 |
| [08](08_Content.md) | 카드·무기·모드 추가 시 그림은 같은 도구로 굽고 이름 규칙(§3.4.1)을 따른다. 잔재 효과음이 필요하면 D-55를 바꾼다 |
| [11](11_WorkContracts.md) | MV-TOOL-ART 완료 기준 = §3.5의 모든 이름 + V1~V5 통과, MV-TOOL-SND = §3.9.3 32행 + V6, MV-ART-ATLAS = §3.8 |

### 남은 의문

1. 담장 차양(L3561)은 면 밖으로 0.8 m 튀어나오는 경사면인데 면 텍스처로 근사했다. 캡처 대조에서 차이가 크면 경사 쿼드 하나를 더한다(02).
2. 생선 건조대 생선 흔들림(`sin(t*1.6+i)*0.03`, L3434)·캣타워 별 크기 변화를 굽지 않았다(진폭이 작음 [추정]). 대조에서 보이면 프레임으로 굽는다.
3. `drawFx` 사각 파편의 1 px 외곽선(L3148)을 생략했다. 파편이 2~6 px라 영향이 작다고 본다 [추정].
4. 정본 아지트 프레임 의존 연출(`AZ.emote += 1/60` L3533, `bump −0.03` L3515)은 60 fps 기준 dt로 바꿔 쓴다(E 노트 의문 7) — 굽기와 무관, [05](05_Screens.md)·[06](06_Effects.md)이 확정.
5. 정본은 효과음마다 잡음 모양이 매번 다르지만 한 가지로 굽는다. 타격음이 단조롭게 느껴지면 잡음 위주 소리(hit, crit, wood, throw)만 변형 3개를 굽고 무작위 선택하는 방식을 검토한다(그때 `03_sound` 행 추가).

## 3.13 정본에 없는 그림 — 임시 아트 (2026-11-09 사용자 확정)

사용자 「기획서에 추가하고」(임시 아트 규칙). 프로젝트 설정 `art_source = placeholder`(임시 아트로 먼저)와 짝이다.

### 3.13.1 세 갈래

| 갈래 | 대상 | 만드는 법 | 임시인가 |
|---|---|---|---|
| A 정본 추출 | 정본 `MoonVillage.html`이 그리는 모든 그림(고양이·쥐·장애물·우물·집·지면·UI·이모지) | §3.2 추출 도구 | **아니다(최종)**. 정본과 같아야 하므로 이것이 정식 아트 |
| B 정본 재가공 | 새 테마 10종(D-72), 정본 그림의 크기·색만 바꾼 것(예: 대왕쥐 = 쥐 그림 확대 + 색) | 같은 도구에서 팔레트·배율만 바꿔 굽기 | 아니다(최종). 단 화풍 검수는 사람이 한다 |
| C 정본에 없음 | 아래 §3.13.2. 2차 메타 콘텐츠와 추가 모드의 아이콘·배경 대부분 | §3.13.3 임시 아트 | **임시**. 정식 아트가 오면 PNG만 바꾼다 |

### 3.13.2 C 대상 찾는 법

목록을 손으로 관리하지 않는다. 추출 도구의 마지막 단계가 **모든 표 리소스 열 값**([07 §7.10](07_Tables.md))과 `09_uiResource`를 모아, A·B 결과(`sprite_catalog.tsv`)에 없는 이름을 C로 판정하고 `Art/Temp/temp_art_list.tsv`(이름, 쓰는 표·행, 크기, 갈래)를 만든다. 현재 기획서 기준으로 C에 들어갈 것으로 보이는 묶음 [추정]:

- 추가 모드 아이콘·배경·입장권: `UI_Icon_Mode_*`, `Tex_Mode_*`, `UI_Icon_Ticket_*`
- 새 재화·메타 아이콘: `01_wealth` 5~17행 icon, `UI_Icon_Pet_*`, `UI_Icon_Research_*`, `UI_Icon_Meta_*`, 장비·룬·축복·출석·로드맵 아이콘
- consumeType 고정 아이콘 중 정본에 없는 것: `UI_Icon_Drop`, `UI_Icon_Ad`, `UI_Icon_Inapp`, `UI_Icon_NoAds`, `UI_Icon_QuestDaily`, `UI_Icon_QuestWeekly`, `UI_Icon_PassXp`
- 새 야옹신 11종의 카드·메뉴 아이콘, 모드 전용 적·오브젝트 중 B로 만들 수 없는 것

### 3.13.3 임시 아트 만드는 법(위에서부터 먼저)

1. **정본 그림 조합**: 정본 스프라이트를 겹치고 색·크기만 바꾼다(예: 펫 = 정본 고양이 그림 + 색, 장비 = 이모지 🛡️⚔️ + 등급색 테두리).
2. **이모지 굽기**: Noto Color Emoji(OFL, D-52와 같은 원본)를 둥근 배경판 위에 굽는다. 배경판 색은 그 행의 등급·종류 색.
3. **글자 판**(최후): 단색 판 + 이름 앞 2글자. 모드 배경 `Tex_Mode_*`는 해당 Area 테마 색 그라데이션 + 모드 이름.

AI 생성·구매·외주 그림은 이번 범위에 넣지 않는다(설치·구매·권리 확인이 필요해 사용자 결정 사항).

### 3.13.4 정식 아트로 바꾸기 쉽게

| 규칙 | 내용 |
|---|---|
| 이름 | **정식과 같은 스프라이트 이름**을 쓴다(`UI_Icon_Pet_sparrow`). 표 값은 처음부터 정식 이름이라 교체할 때 표를 고치지 않는다 |
| 위치 | PNG 원본은 `Art/Temp/`. 아틀라스는 정식과 같은 아틀라스에 들어간다(§3.8 묶음 그대로) |
| 표시 | `sprite_catalog.tsv`에 `temp` 열(1 = 임시). 정식 PNG를 `Art/` 정식 폴더에 넣으면 도구가 정식을 우선하고 temp 0으로 바꾼다 |
| 크기·피벗 | §3.4 규칙을 정식과 똑같이 따른다(바꿔 끼워도 배치가 움직이지 않게) |
| 검수 | 개발 빌드는 통과. **출시 후보 빌드**에서 temp 1이 남으면 검수 보고서에 목록으로 경고(막지는 않음. 막을지는 출시 때 사용자 결정) |
| 화면 대조 | [12](12_Acceptance.md)의 정본 대조 장면은 A 그림만 쓰므로 임시 아트와 무관 |
