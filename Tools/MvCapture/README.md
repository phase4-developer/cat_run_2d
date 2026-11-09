# MvCapture — 프로토 같은 상태 캡처·로직 덤프 (MV-QA-HARNESS 프로토 쪽)

정본 `Docs/Reference/MoonVillage.html`을 **바꾸지 않고** Playwright Chromium에 주입 스크립트를 넣어 같은 상태(시드·save·시간·입력)로 돌린다. 기준 [12 §12.1~12.3](../../Docs/MV-SPEC-01/12_Acceptance.md), 장면 정의 형식 [QA/Scenes/README.md](../../QA/Scenes/README.md), 덤프 형식 [QA/Dumps/README.md](../../QA/Dumps/README.md).

## 준비 (D-50 승인 범위)

```
cd Tools/MvCapture
npm ci                              # playwright 1.65.0 (package-lock.json)
npx playwright install chromium     # chromium-1254
```

글꼴 TTF는 Git에 없다. `vendor/SOURCES.md`의 URL에서 받아 `vendor/fonts/`에 둔다(SHA256을 도구가 확인).

## 명령

| 명령 | 하는 일 |
|---|---|
| `node src/cli.js verify` | 정본·vendor SHA256 확인 |
| `node src/cli.js check` | `QA/Scenes/*.json` 형식 검사 |
| `node src/cli.js capture [장면ID…] [--runs 2] [--workers 4] [--out 폴더]` | 장면마다 `runs`번 독립 실행해 `captureAt` 프레임을 찍고 해시 비교. PNG는 `QA/Captures/Proto/<장면>/<프레임>.png`, 표는 `QA/Captures/Proto/hashes.tsv`(`repeatN` 칸이 `SAME`이면 픽셀 동일). 불일치가 있으면 종료 코드 2 |
| `node src/cli.js probe <장면ID> "<식>" [--to N] [--every N]` | 장면을 돌리며 매 프레임 정본 스코프에서 `(식)()`을 평가해 출력. 장면의 captureAt 프레임을 찾을 때 쓴다. 예: `probe W-02 "() => [mode, R && R.p.z]" --to 600 --every 30` |
| `node src/cli.js gen [--preset easy]` | GEN·FIELD 덤프 → `QA/Dumps/gen/` |
| `node src/cli.js sim [SIM_BASE …]` | SIM 덤프 → `QA/Dumps/sim/` |

## 구성

| 파일 | 내용 |
|---|---|
| `src/inject.js` | 페이지 로드 전 주입: 가짜 시계(performance.now·Date·AudioContext.currentTime·rAF·타이머·CSS 애니메이션), 난수(mulberry32/상수, 호출 기록), save 주입, `window.claude.hot.ready` 부팅 고리 |
| `src/harness.js` | 정본·vendor SHA256 확인, 네트워크 가로채기(정본·three.js·글꼴만 로컬 응답, 나머지 차단), CDP로 정본 IIFE 스코프 접근 함수 `__mvq.ev` 만들기, `#stage` 1080×1920 캡처 |
| `src/scene.js` | 장면 정의 검사, 프레임 진행기(ops → inputs → 1프레임), 캡처 |
| `src/gen.js`, `src/sim.js` | 덤프 |

### 정본 내부 접근

정본 스크립트는 IIFE라 `buildLevel`·`save`·`R` 같은 이름이 밖에서 안 보인다. 정본 L3888은 `window.claude.hot.ready`가 있으면 그 함수에 `boot`를 넘긴다. 주입 스크립트가 그 함수를 만들어 그 안에서 `debugger`로 멈추고, 하네스가 CDP `Debugger.evaluateOnCallFrame`으로 바로 바깥 프레임(정본 IIFE)에서 `__mvq.ev = s => eval(s)`를 만든다. 이후 덤프·프리셋 적용이 정본 함수를 그대로 부른다. 정본 바이트와 실행 경로는 바뀌지 않는다(부팅을 글꼴 로드 뒤로 미루는 것만 다르다).

### 재현성에서 막은 것

- 실제 rAF·타이머·CSS 애니메이션 시계 → 가짜 시계.
- 정본 소리(L1630~1690)가 오디오 시계로 재생 간격을 거르고 잡음에 `Math.random`을 써서 실행마다 난수 소비가 달라졌다 → `AudioContext.currentTime`도 가짜 시계.
- 시스템 글꼴·이모지 차이 → 고정 TTF + Noto Color Emoji, 그 밖 네트워크 차단.
- 렌더링: 정본은 WebGL 없이 Canvas 2D(L917~922). `--disable-gpu`로 소프트웨어 래스터.

### 알려진 차이

- 뷰포트 1112×1920(스테이지 1080×1920). 정본 body 좌우 16px 여백 때문(`QA/Scenes/README.md` 「좌표와 화면」).
- 글꼴은 Google Fonts가 주는 분할 woff2가 아니라 google/fonts 저장소 TTF(Unity와 같은 원본).
- 부팅 시각이 「글꼴 로드 완료 뒤」로 고정된다(정본은 로드와 동시에 부팅하고 글꼴이 늦게 오면 대체 글꼴로 먼저 그린다).
