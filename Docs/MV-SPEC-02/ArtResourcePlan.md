# 디자이너 리소스 적용 설계 (MV-ART-CHAR)

작성 2026-11-12. 기준 [Source/06_배경리소스관리예상.md](Source/06_배경리소스관리예상.md)(디자이너 PDF 사본). 이 문서가 MV-SPEC-01 [03 §3.4·§3.5.1](../MV-SPEC-01/03_ResourcePipeline.md)의 캐릭터 부분(이름 `World_Cat_*`, 12프레임 위상 샘플, `101_cat.spritePrefix`·`atlasPattern`)을 대체한다. 03의 이름은 임시 명칭이었다.

## 0. 사용자 지시 (2026-11-12 원문)

> 분신 이미지도 결국 테이블로 관리해야하는 부분이니 … 실제 파일로 매칭해서 테이블로 연결하면 될거고 리소스 관리는 디자이너의 교체 pdf 문서에 적힌게 있으면 그부분 고려하고 런타임중에 불러올때는 캐릭터는 프리팹화 해서 풀로 관리하는게 좋을거 같아 캐릭터 상태머신도 추가하고 이건 분신도 마찬가지야 좀더 코드적으로 구조를 설계해서 만들어가야할 시기인거 같아 단계별로 하나씩 정리하고 들어가는게 이후 수정을 줄일거같아 모든 단계 상승과 다른 종류는 다른 리소스를 연결할수있다고 보면돼 하드 코딩으로 연결하지 말고 다 테이블로 관리시켜 … UI는 이미지 이름으로 해서 addressableImage를 활용하고 인게임내 객체는 프리팹화해서 관리하는게 좋을거 같아 그래야 무기 자체에 별도 스크립트 붙여야 하는 경우 대응 가능하니까 pdf 리소스가 중심이고 html은 참고용이야 그러니 pdf에 정의되고 실제 깃에 올라오지 않은 부분은 무시하고 진행하면돼 addressable 등록도 너가해야해 크기 기준점은 이제 신규 리소스에 맞추고 캐릭터는 규격을 코드에서 관리하는게 아니라 리소스에 따라 달라져야하고 그걸 조절하는건 테이블내 캐릭터 스케일값으로 추가적인 조절정도만 열어주면 될거고 이 배치는 html을 참고하면 될거야 맵 리소스가 없으면 맵은 우선 건드리지마 … 리소스 관리 틀은 pdf 문서에 적힌 대로 가면 돼

## 1. 기준

| 순위 | 자료 | 쓰는 범위 |
|---|---|---|
| 1 | 디자이너 PDF([사본](Source/06_배경리소스관리예상.md)) | 이름·폴더·프리팹 단위·재생 규칙·상태별 포즈 |
| 2 | Git에 실제로 올라온 파일(`cff82f5c` 「캐릭터 리소스 1차 추가」) | PDF에 있어도 Git에 없으면 이번 단계에서 무시 |
| 3 | HTML 「마법냥 런 · 종이 마법 거리 (새 고양이)」(사용자 Downloads, 저장소 밖) | 참고: 배치(발 기준점·배율)와 연출 값 |

### 1.1 Git에 있는 것 / 무시하는 것

| 있음(이번에 연결) | PDF에만 있음(무시, 들어오면 행 추가) |
|---|---|
| `Char_Cat.prefab`, 플레이어 `cat_run_{A,B,C,D}_{0..11}` 48장 | `cat_happy_*`, `cat_stair_*`, 종이 뒷면 텍스처 |
| `Char_Clone.prefab`(샴), `cat_run_{A,B,C,D}_siamese` 4장 | 분신 gray·tux·나머지 종족 |
| | 하수인 스테이지별 이미지, 공통·스테이지 오브젝트 전부, 투사체, 이펙트 |

맵 리소스(바닥·결계문·결계 블록·포털·배경 오브젝트·정산 계단 칸)가 없으므로 맵·오브젝트 뷰는 건드리지 않는다.

## 2. 연결 원칙

| # | 원칙 |
|---|---|
| R1 | **인게임 객체는 프리팹**(Addressables 주소)으로 불러와 풀로 관리한다. 무기·캐릭터에 따로 스크립트를 붙일 수 있게 하기 위해서다. **UI는 이미지 이름 + `AddressableImage`**(지금 `09_uiResource`·`icon` 열 방식 유지). |
| R2 | 단계(무기 등급)·종류(종족)마다 다른 리소스를 연결할 수 있다. 연결은 **표 행**이 정한다. 코드에 파일 이름·세트 수·종족 수를 넣지 않는다. 개수는 표 행 수로 센다. |
| R3 | 표 행 값 = Git의 실제 파일 이름(PDF 이름 규칙). 검증기가 파일이 있는지 본다. |
| R4 | 모자란 행은 마지막 행을 다시 쓴다(분신 2번째 자리부터 샴 재사용 등, 저장소 규칙). PDF에 있는데 Git에 없는 칸(기쁨·정산 포즈)은 그 행의 걷기 A 이미지 이름을 **더미 값으로 표에 적고** 시트에 더미 색 표시. 코드 대체 경로는 없다. |
| R5 | 크기·기준점은 **리소스가 정한다**(스프라이트 임포트의 피벗·PPU). 코드는 규격 상수를 갖지 않는다. 표에는 캐릭터별 추가 `scale`만 둔다. |

## 3. 표 설계

### 3.1 새 표 `116_charLook` 캐릭터 외형 (행 = 프리팹 1개 + 상태별 이미지)

| 열 | 뜻 | 플레이어 예 | 분신 예 |
|---|---|---|---|
| id | `116001001`~ | 116001001 | 116002001 |
| name | 사람용 용어 | 플레이어 외형 세트 0 | 분신 외형 샴 |
| catId | `101_cat` 행 | 101001001 | 101001002 |
| slot | 자리 순서. 캐릭터(catId)마다 0부터 연속. 분신 i번째(0부터) = slot i 행, 없으면 마지막 slot | 0 | 0 |
| fromTier | 이 외형을 쓰기 시작하는 무기 등급(PDF: 세트 n = 등급 n+1부터). 같은 catId·slot 안에서 현재 등급 이하 중 가장 큰 행 | 1 | 1 |
| prefab | Addressables 주소 | `Char_Cat` | `Char_Clone` |
| atlas | 이 행 이미지가 든 SpriteAtlas 주소 | `atlas_player_0` | `atlas_clone_siamese` |
| walkA / walkB | 왼발·오른발 | `cat_run_A_0` / `cat_run_B_0` | `cat_run_A_siamese` / `cat_run_B_siamese` |
| atkA / atkB | A 차례·B 차례 공격 | `cat_run_C_0` / `cat_run_D_0` | `cat_run_C_siamese` / `cat_run_D_siamese` |
| happy / stair | 기쁨·정산 포즈 | 더미 `cat_run_A_0` | 더미 `cat_run_A_siamese` |
| scale | 리소스 크기에 곱하는 추가 배율(HTML 배치 참고) | 1 | 0.82 |

- 플레이어 12행(세트 0~11, fromTier 1~12), 분신 1행(샴). `101_cat`의 `spritePrefix`·`atlasPattern`·`tierCount`·`patternCount`는 이 표로 옮겨지고 삭제 후보(행 수로 계산되는 값이라 표에 따로 두지 않는다).
- 아틀라스 이름은 PDF에 없다. 위 `atlas_*`는 [결정 후보 C-1].
- 단계 2 적용(MV-DATA-CHARLOOK): 그림 이름 확인은 에디터 시험 `CharLookRows_ReferenceRegisteredArt`(행 아틀라스 묶음 안에 그림이 있는지), 구조 검증은 `MvTableValidator.CheckCharLook`(slot 0부터 연속, slot마다 fromTier 1 행, fromTier 겹침 없음, scale > 0), 고르기는 `CharLookResolver`.

### 3.2 공통 재생 값 `300_runConfig` 키

| 키 | 값 | 출처 |
|---|---|---|
| `char.frameSec` (300107001) | 0.25 | PDF 재생 규칙 |
| `char.dipAt` (300107002) | 0.5 (한 칸 안 비율 → 0.125초) | PDF |
| `char.dipM` (300107003) | 0.07 (내려가는 깊이, m) | HTML `newCatDip·0.07` |
| `char.atkFrames` (300107004) | 1 (공격 이미지 유지 칸 수) | PDF |
| `char.happySpinSec` (300107005) | 0.55 (세로축 1바퀴) | HTML `paperSpin` |
| `char.stairRate` (300107006) | 7.5 rad/s (오르는 중 흔들림 각속도) | HTML `drawClimbCat` `w8 = now*7.5` |
| `char.stairRockRad` (300107007) | 0.17 rad (오르는 중 기울기) | HTML `Math.sin(w8)*0.17` |
| `char.stairBobM` (300107008) | 0.12 m (오르는 중 위로 튐) | HTML `Math.abs(Math.sin(w8))*0.12` |
| `char.stairSwayM` (300107009) | 0.06 m (오르는 중 좌우) | HTML `Math.sin(w8)*0.06` |
| `char.stairIdleRate` (300107010) | 3 rad/s (오르기 전 흔들림 각속도) | HTML `Math.sin(now*3)` |
| `char.stairIdleRockRad` (300107011) | 0.05 rad (오르기 전 기울기) | HTML `*0.05` |

걷기 좌우 흔들림(HTML `sway`)은 PDF에 없어 넣지 않는다.

## 4. 리소스 규격(크기·기준점)

| 항목 | 값 | 근거 |
|---|---|---|
| 원본 | 512×512 PNG, 흰 테두리 포함 | Git 파일 |
| 피벗 | 발 아래 (240, 487)px → 정규화 (0.469, 0.049) | HTML `NCAT_AX/AY`(470×452로 자른 캔버스 기준 230,427 + 자른 위치 10,60) |
| PPU | 161 (px/m) | HTML `NCAT_PPM` |
| 기록 위치 | 각 PNG `.meta`(스프라이트 임포트). 코드 상수 아님 | R5 |

지금 `.meta`는 피벗 가운데·PPU 100(키 5.12 m)이고, 두 프리팹 루트 위치가 (1.6, 0, 49.7)이다. 1단계에서 고친다.

### 4.1 크기 점검 — 너무 큰가

게임 화면 기준(03 §3.3.1): 플레이어 발 깊이에서 1080×1920은 128.7 px/m, 1440×2560은 171.6 px/m. 리소스는 161 px/m.

| 묶음 | 그림이 차지하는 영역(512 안) | 1080 화면 필요(×0.80) | 1440 화면 필요(×1.07) | 판단 |
|---|---|---|---|---|
| 세트 0~2, 샴 | 249~287 × 326~385 | 약 230×308 | 약 306×411 | 해상도 적정 |
| 세트 3~7 | 265~323 × 373~406 | 약 258×325 | 약 345×433 | 적정 |
| 세트 8~11 | 296~405 × 364~425 | 약 324×340 | 약 432×453 | 적정 |
| 전 52장 공통 | 512² 중 **35~68%가 투명 여백**(그림 위쪽·좌우) | | | **여백이 낭비** |

결론: 해상도(px/m)는 QHD 화면에 맞는 크기라 줄이지 않는다. 낭비는 투명 여백이고, 아틀라스에서 Tight 패킹으로 잘라낸다. 디자이너에게 캔버스를 줄여 달라고 할 필요는 없다. 줄이고 싶으면 아틀라스 축소가 아니라 원본을 80%(410px)로 다시 내보내는 것이 맞고, 그때 PPU를 129로 바꾼다(표·코드 변경 없음).

그 밖: `cat_run_C_9`와 `cat_run_C_10`은 바이트가 같은 파일이다(행은 각각 둔다, 디자이너 확인용 메모).

### 4.2 압축

아틀라스는 원본 PNG의 압축 설정과 상관없이 **묶은 결과 텍스처를 플랫폼 설정으로 따로 압축**한다. 그래서 원본은 무압축으로 두고 아틀라스에서 압축한다.

| 묶음 | 패킹 | 크기 추정 | 압축(Android ASTC 6×6) | 무압축이면 |
|---|---|---|---|---|
| 외형 행 1개(4장, Tight) | 1024×1024 1장 | — | 약 0.47 MB | 4 MB |
| 플레이어 12세트 전부 | 12장 | — | 약 5.6 MB | 48 MB |

행(세트)마다 아틀라스 1개로 나눠, 런에서는 지금 등급 세트 + 다음 세트만 읽는다(지금 `CatView`의 묶음별 최근 3개 규칙 유지). 압축 형식 최종값은 D-54(03 §3.8.3) 결정을 따른다.

## 5. 코드 구조

```text
MV.Data
  CharLookTable (116_charLook) · 300 키 · 검증(파일 존재, slot·fromTier 정렬, 행 수 ≥ 1)
MV.View/Character/
  CharLookResolver   (순수) catId·slot·tier → 행. 모자라면 마지막 행
  CharStateMachine   (순수, EditMode 시험) 상태·시간 → 그릴 이미지 칸·내려감·회전각
  CharacterRig       (MonoBehaviour, 프리팹 루트) SpriteRenderer 참조, 무기·뒷면 자리, 프레임 적용
  CharacterPool      프리팹 주소별 Addressables 로드 + UnityEngine.Pool.ObjectPool, 런 끝에 반환·해제
  CharacterPresenter RunViewState(플레이어 + 분신) → 풀에서 꺼내고 상태머신을 돌린다. CatView 대체
```

### 5.1 상태머신 (플레이어·분신 같은 것)

| 상태 | 들어가는 조건 | 그림 | 움직임 |
|---|---|---|---|
| Walk | 런 진행 중 / 로비 대기 | A·B를 `char.frameSec`마다 교대 | 칸 `char.dipAt`에서 `char.dipM`만큼 내려갔다 올라옴 |
| Attack (Walk 위에 겹침) | 쏜 순간부터 `char.atkFrames`칸 | A 차례면 atkA, B 차례면 atkB | Walk와 같음 |
| Happy | 게이트 통과, 계단 Lv 상승, 무기 업그레이드 | happy(지금 더미) | 세로축 회전 `char.happySpinSec`, 끝나면 Walk |
| Stair | 계단 정산 시작~끝 | stair(지금 더미) | 흔들림은 코드(PDF). 위치는 지금 계단 오르기 계산 그대로 |
| Hidden | 분신 자리 비활성·정산 중 분신 | — | 풀에 반환 |

상태 전환은 `RunEvent`(게이트 통과·계단 Lv·무기 획득·정산 시작/끝)만 보고 정한다. 런 규칙(`MV.Run`)은 고치지 않는다.

단계 4 적용(MV-VIEW-CHAR-2):
- Happy: `GateTake`(계단·공방 게이트 통과 = HTML 두 게이트의 `paperSpin`, 공방 게이트가 PDF 「무기 업그레이드」)와 `GaugeLevelUp`(계단 Lv 상승)에서 플레이어·분신 모두. 가로 배율 = cos(2π·(1−(1−k)³)), 루트에 걸어 무기도 함께 돈다. 뒷면 종이 텍스처는 아직 없어 앞면이 뒤집혀 보인다.
- Stair: 오르기 단계(`RunPhase.Climb`)의 플레이어. `ClimbState.Pos > 0`이면 오르는 중 흔들림, 아니면 오르기 전 흔들림. 계단 기울기(HTML `slope*0.85`)·옆으로 뒤집기·0.86배는 카메라 궤도 회전과 함께 단계 5. 분신은 단계 5까지 지금 계단 위치에서 Walk.
- 빌보드는 프리팹 루트에 붙이고, 몸은 루트 안에서 내려감·좌우·기울기를 받는다.
- 로비 대기 걷기: 사용자 결정(2026-11-12 「로비를 트랙 화면으로 바꿔야해」)으로 로비를 트랙 위 같은 화면으로 바꿨다(MV-VIEW-LOBBY-TRACK). 로비 냥법사는 미리보기 런 상태(단계 Run)라 Walk(A·B 교대)로 제자리 걷는다.

## 6. 단계

각 단계 = 계약 1건 = 브랜치 1개(`ai/<계약ID>-…`, dev에서). 앞 단계가 dev에 병합된 뒤 다음 단계를 시작한다.

| 단계 | 계약 | 내용 | 쓰기 범위 | 완료 기준 |
|---|---|---|---|---|
| 0 | MV-ART-CHAR-0 | 이 설계와 PDF 사본 | `Docs/` | 이 문서 dev 병합 |
| 1 | MV-ART-CHAR-1 리소스 등록 | `.meta` 피벗·PPU·무압축 원본, 행별 SpriteAtlas(Tight), Addressables 그룹 등록(PDF 폴더 `Local/Character` → 그룹 `Local_Character`(기존 `Local_UI`와 같은 규칙), 주소 = 파일 이름: 프리팹·아틀라스), 프리팹 루트 위치 0. `MvArtSetup` 소유 그룹에 추가해 결정적으로 등록 | `Assets/Game/Addressables/Local/Character/**`, `AddressableAssetsData/**`, `MV.Editor/Import/**` (통합 Writer: 총괄 단독) | compile, 등록 검사 EditMode, 주소로 프리팹·아틀라스 로드 시험 |
| 2 | MV-DATA-CHARLOOK 표 | `116_charLook` TSV·시트·Resources 산출물, 스키마, 검증기, 300 키, `101_cat` 옮긴 열 정리 | `MV.Data/**`, `GameData/Table/**` | compile, MV.Data EditMode, 산출물 재생성 |
| 3 | MV-VIEW-CHAR-1 구조 | Resolver·상태머신(Walk/Attack/Hidden)·Rig·Pool·Presenter, CatView 대체(플레이어+분신) | `MV.View/**`, 프리팹에 `CharacterRig` 부착 | compile, MV.View EditMode(상태머신·Resolver), PlayMode 1회 |
| 4 | MV-VIEW-CHAR-2 상태 추가 | Happy(세로축 회전)·Stair 상태, 로비 대기 걷기 | `MV.View/**`, 로비 뷰 | compile, EditMode. 화면 확인 NOT_RUN → 사용자 확인 |
| 5 | (대기) 계단 정산 연출 | HTML 참고: 카메라 궤도 회전 0.42초, 3D 종이 계단, 칸 수 글자. 정산 계단 칸·정산용 배경 오브젝트 프리팹이 Git에 오면 시작 | — | — |
| 6 | (대기) 오브젝트·투사체·하수인 | `105_objectKind`·`102_enemy`·`103_item` 리소스 열을 프리팹 주소로. 금화·원석은 프리팹 안 오브젝트 단계별 비활성화. 리소스가 Git에 오면 시작 | — | — |

## 7. 결정 후보

| # | 내용 | 권장 |
|---|---|---|
| C-1 | 아틀라스 이름(PDF에 없음) | **1단계 적용**: `atlas_<이미지 하위 폴더 소문자>_<파일 이름 마지막 토막>`(예: `atlas_player_0`, `atlas_clone_siamese`). 파일에서 자동으로 묶는다(`MvCharacterArt`) |
| C-2 | PDF 분신 종족 표기 차이(표 「삼색」 ↔ 이름 `siamese`, `cat_run_B_siam` 오타) | **결정(사용자 2026-11-12 「pdf와 파일 이름이 틀리면 오타를 수정하고 둘다 오타면 우선 무시해서 사용해줘」)**: PDF·파일 대조 결과 다른 것은 PDF `cat_run_B_siam` 하나이고 파일(`cat_run_B_siamese`)이 규칙에 맞아 파일은 그대로. 바꾼 파일 없음 |
| C-3 | 기쁨·정산 포즈 이미지가 오기 전 더미 | 걷기 A 이미지를 표에 더미로(R4) |

## 8. 2차 갱신 (MV-ART-2, 2026-11-13)

기준이 디자이너 PDF에서 노션 갱신본 세 페이지(「신작 캐릭터 리소스 관리」·「신작 배경 리소스 관리」·「신작 이펙트 리소스 관리」)로 바뀌었다. 사용자 원문(2026-11-13): 「노션 세페이지의 토클을 펼쳐서 내용을 모두 읽고 기존 리소스 작업에서 변경된 사항(추가 포함) 적용 작업을 해야해」. 원장 MV-ART-2 행.

| 바뀐 것 | 적용 |
|---|---|
| 세트 그림 6장(happy·stair 실제 파일) + 뒷면 `cat_happy_back_n` | `116_charLook` happy·stair 더미 26칸을 실제 이름으로, 새 열 `happyBack`(분신은 A 그림 더미) |
| 정산 바퀴 `cat_stair_wheel`, 계단 한 칸당 약 0.6바퀴 시계 방향 | 새 열 `wheel`·`wheelAtlas`(`atlas_player_wheel`, 분신 빈칸), 300 `char.wheelTurnsPerStep`(300107012) 0.6. 바퀴 PNG 피벗 = 바퀴 중심, `Char_Cat` `stair_wheel` 자식 위치로 보정 |
| 분신 4종 프리팹 따로(`Char_Clone_calico/gray/siamese/tux`, 옛 `Char_Clone` 변형) | 116 분신 4행. 자리 순서 = 노션 표 순서(삼색·회색 줄무늬·흰 얼룩·턱시도, 사용자 카드 결정 2026-11-13). `Char_Clone`은 바탕 프리팹이라 남김 |
| 배경 공통·스테이지1 오브젝트 프리팹, 맵 `Stage1` | `Local_BG` 그룹(주소 = 파일 이름). 연결은 MV-VIEW-STAGE·MV-VIEW-OBJ 단계 |
| 투사체 `projectile.prefab` + `projectile_0~11` + `VFX_Tail_0~11`, 카드 효과 `Fx_Card_Lightning` | `Local_Projectile` 그룹, 이펙트는 `Local_Effect`. 연결은 MV-VIEW-PROJ-2 |
