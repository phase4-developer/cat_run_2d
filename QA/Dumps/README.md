# 로직 대조 덤프 (`QA/Dumps/`)

MV-QA-HARNESS 프로토 쪽 산출물. [12 §12.3](../../Docs/MV-SPEC-01/12_Acceptance.md) 로직 대조의 기준 파일이며 MV-RUN-GEN·MV-RUN-SIM의 EditMode 시험이 읽는다. 모두 정본 `Docs/Reference/MoonVillage.html`(SHA256 `fe573cf2…dfa`)을 고치지 않고 Playwright로 돌려 만든다. 각 파일 머리에 `protoSha256`·`command`가 있다. 숫자는 JS double 그대로(JSON 최단 왕복 표기). 비교 허용 오차는 12 §12.3.3.

다시 만들기(`Tools/MvCapture`에서): `node src/cli.js gen`(약 1분), `node src/cli.js sim`(수 분). 같은 명령을 다시 돌리면 파일 SHA256이 같아야 한다(`MANIFEST.tsv`).

## GEN — `gen/GEN_<preset>.json` (preset = easy · mid · orig)

정본 `buildLevel`(L1783)의 결과. Level 1~20 × Area 1~15 × Floor 1~3 = 900칸 × 프리셋 3.

- 만드는 법: 칸마다 `save = Object.assign(DEF(), {level, area, zone: floor(area/5), cp, bars: {}})` 뒤 정본 `newRun()`(L1808, 안에서 `buildLevel(0, save.level, …)`)을 부르고 `R.objs`를 읽는다. **Floor N = 체크포인트 `cp` = N−1**(그 Floor부터 필드를 깐다, L1786).
- 난수: `rng = {mode:"const", c:0.5}` — 모든 `Math.random()`이 0.5(04 §4.17.2-5 ConstRng). 생성 중 규칙 난수는 쥐 속도 `spd = rnd(1.1,1.6)` = 1.35뿐이다.
- 그 밖 save는 `saveBase`(= `DEF()`) 그대로: rb.perks 0이라 `obstMul()`=1, `diaCap()`=10.
- 머리: `bal`(프리셋 값), `saveBase`, `objKeys{종류: [키…]}`.
- `entries[]`: `{level, area(1부터), floor(1부터), cp, tpl, base, gz, Z{name,hp,zi}, gates[{z,floor}], objs[]}`. `objs`의 한 행 = `[k, ...objKeys[k] 순서의 값]`, 없는 키는 `null`. 오브젝트의 원시값 속성만 남겼다(그림 참조 `mesh` 등 제외). 그림용 값 `ph`도 들어 있으니 대조에서 뺀다.
- 주요 키: `k` 종류(tower·sgate·enemy·cgate·stair·coin·gem·arch·wall·mouse …), `x`·`z`(m, +z 진행), `hp`·`max`, `hw`·`hd`(판정 반폭·반깊이), `reward`·`dia`(상자 보상), `rew`·`amt`(벽 보상 gold|soul|dia·양), `bar`·`row`·`lane`·`idx`(필드 칸), `label`·`gate`(아치), `lvl`(레벨).

## FIELD — `gen/FIELD_<preset>.json` (D-27 골든)

정본 `fieldSpec(A, f)`(L1766)와 그 안의 `hash`(L858 `x => { s = sin(x·127.1+311.7)·43758.5453; s − floor(s) }`) 입력·결과. **Area 1~30 × Floor 1~3**.

- `fields[]`: `{area, floor, final[h0,h1,rows,souls], finalRow, seed, soulHash[[x, hash(x)]…], diaHash[[x, hash(x)]…], cells[{idx,r,l,hp,rew,amt}]}`.
- `finalRow`: 정본은 `FINAL[min(A, 14)]`이라 Area 16~30은 Area 15 값을 다시 쓴다(D-71 미정 — 08이 16~30 값을 넣으면 그 칸은 다시 만든다).
- `soulHash`: 생선 칸 고르기 루프(`k`마다 `seed + k·7.31`)에서 부른 순서 그대로. `diaHash`: 칸 idx마다 `seed + idx·3.7 + 11`.
- C# `Hash`는 `diaHash`·`soulHash`의 x를 넣어 결과가 **완전 일치**해야 한다(12 §12.3.3). 어긋나는 플랫폼은 `cells`를 표 데이터로 굳힌다(D-27).

## SIM — `sim/SIM_<id>.json`

같은 save·시드·입력 기록으로 정본을 프레임마다 돌린 결과와 **로직 난수 기록**(12 §12.3.2).

| 파일 | 입력 | 범위 |
|---|---|---|
| `SIM_BASE.json` | 장면 `QA/Scenes/W-02.json`(W-02~W-10, W-17, F-01·F-02·F-05, U-01, U-13이 같은 런을 공유) | 출발 ~ 결과 화면 + 60프레임 |
| `SIM_RUN-01.json` | `sim/inputs/RUN-01.json` 첫 실행 save + 드래그 | 런 전체 |
| `SIM_RUN-02.json` | `sim/inputs/RUN-02.json` 강한 save(Area 포털 통과) + 드래그 | 런 전체 |
| `SIM_RUN-03.json` | `sim/inputs/RUN-03.json` Area 3 중간 save + 방향키 | 런 전체 |

`sim/inputs/RUN-*.json`은 장면 정의 형식([QA/Scenes/README.md](../Scenes/README.md)) 그대로다(캡처 대상이 아니어서 `QA/Scenes` 밖에 둔다).

- 머리: `scene`(장면 정의 전체), `frames`(실제 기록한 프레임 수 — 결과 화면 진입 뒤 60프레임에서 멈춤), `sCols`, `addCols`, `chgCols`, `rngCols`, `logicLines`, `rngTotal`(전체 `Math.random` 호출 수), `logicRngTotal`.
- `rows[]`: 프레임마다 한 줄(`f` = 장면 프레임 번호, captureAt과 같은 번호 체계).
  - `mode`: 정본 `mode`(lobby·run·cannon·result).
  - `s`: `sCols` 순서 — 플레이어 `p.x, p.z, p.tx`, 런 재화 `R.gold, R.souls, R.dia`, 게이지 `lv, fill`, 투사체 수, `R.stairs`, `R.climbed`, `save.level`, `R.floor`, `R.time`.
  - `add`: 새로 나타난 오브젝트 `[id, k, x, z, hp, dead, max]`. id는 덤프가 붙인 등장 순번(1부터).
  - `chg`: x·z·hp·dead 중 바뀐 오브젝트 `[id, x, z, hp, dead]`. `rm`: `R.objs`에서 빠진 id.
  - `ev`: 덤프가 상태 차이로 만든 사건 — `['mode',전,후]`, `['level',전,후]`, `['floor',…]`, `['stairs',…]`, `['climbed',…]`, `['cards',{id:n}]`, `['dead',id,k]`, `['finish',reason,pass]`. Unity `RunEvent`와는 종류 이름이 다르므로 MV-RUN-SIM 시험이 대응표를 둔다.
  - `rng`: 이 프레임에 뽑힌 **로직 난수**만 `[seq, line, value]`. `seq`는 부팅부터 센 전체 `Math.random` 호출 순번, `line`은 정본 호출 줄(04 §4.17.1 「규칙」 줄: 1323(rnd 경유만)·1927·1986·2004·2005·2081·2090·2167·2168·2301·2302). Unity `ReplayRng`는 `value`를 이 순서대로 돌려주고, 더 뽑거나 덜 뽑으면 실패다.
- 난수 줄기: 장면 `seed`의 mulberry32. 그림·소리 난수도 같은 줄기를 쓰므로(정본 구조) `seq`에 빈 번호가 있다. 그림 난수 값은 대조하지 않는다.

## MANIFEST.tsv

덤프 파일별 SHA256·크기·만든 명령. 「덤프 재생성 해시 동일」 검증은 두 번 만든 결과가 이 표와 같은지 본다.
