# 11. 오케스트라 작업 계약

[입구](README.md) · 앞: [10 아키텍처](10_Architecture.md) · 다음: [12 인수 기준](12_Acceptance.md)

이 절은 jjevol-orchestrate 총괄이 그대로 TaskOrder에 옮길 수 있는 작업 계약 목록이다. 계약마다 ID, 목표, 입력 절, 쓰기 범위, 선행, 완료 조건, 검증을 적었다. 담당 AI 배정과 검토 AI는 총괄이 정한다(중앙 `skills/jjevol-orchestrate` 「기능 그룹 교차 검토」).

## 11.1 공통 규칙

| 항목 | 규칙 |
|---|---|
| 브랜치 | 계약 1건 = 작업 브랜치 1개. 이름 `ai/<계약ID>-<짧은설명>`. 테스트 브랜치(새 프로젝트 규칙, 없으면 로컬 `dev`)에서 만들고 검증 PASS 뒤 테스트 브랜치에 병합한다. `main` Commit·병합과 Push는 사용자가 그 회차에 직접 말할 때만 한다 |
| 통합 Writer | `Packages/manifest.json`, `ProjectSettings/`, 모든 `.asmdef`, `Scenes/Boot.unity`·`Scenes/Game.unity`, Addressables 설정 자산(`AddressableAssetSettings`, 그룹 자산), URP 설정 자산은 **총괄 1명만** 쓴다. 담당은 필요한 변경을 명세나 diff로 돌려준다 |
| 쓰기 범위 | 아래 표의 경로만 쓴다. 같은 시점에 두 담당의 쓰기 범위가 겹치지 않는다(11.4 점검표). 범위 밖이 필요하면 멈추고 총괄에게 요청한다 |
| 표 | TSV 정본은 `Assets/Game/GameData/Table/TSV/`. 표 **행 클래스·표 클래스**는 MV-DATA만 쓴다. 다른 계약이 새 열이 필요하면 MV-DATA 변경 요청을 낸다. 콘텐츠 행 추가는 MV-CONTENT-*가 자기 표 파일만 쓴다 |
| 순수 런 | `MV.Run`은 `UnityEngine`을 참조하지 않는다. 시간·입력·난수는 주입한다([10 §10.4](10_Architecture.md)) |
| 검증 묶음 | 컴파일 확인을 가장 먼저 받는다. EditMode 시험은 계약마다, Unity 화면 캡처 대조와 기기 실행은 **기능 그룹 통합 지점과 릴리스 후보에만** 묶는다(중앙 `prompts/06_QA_ORCHESTRATION.md`). 실행하지 않은 검증은 NOT_RUN으로 적는다 |
| 검증 없이 쌓기 | 컴파일·시험 경로가 막힌 채 쌓는 변경은 기능 1묶음 또는 파일 20개까지 |
| 정본 고정 | 대조 기준은 `MoonVillage.html` SHA256 `fe573cf2…26dfa`([01 §1.2](01_Scope.md)). 정본이 바뀌면 기획 변경으로 다룬다 |
| 결정 대기 | 결정 대기 항목([13](13_Decisions.md))에 걸린 계약은 권장안으로 진행하되 그 부분을 교체 가능하게 둔다. 결정에 막힌 부분만 기다리고 나머지는 진행한다 |
| 회사 경계 | D-01 결정 전까지 회사 경계로 본다. 회사 내용은 이 PC의 저장소 밖으로 내보내지 않는다 |

## 11.2 기능 그룹과 순서

```text
G0 기반 ──┬─> G1 데이터 ──┬─> G3 런 코어 ──┬─> G4 표시 ──┬─> G5 UI ──> G7 콘텐츠 ──> G8 인수
          │               │                │             │
          └─> G2 리소스 도구 ─────────────────┘             └─> G6 플랫폼(스텁) ──┘
G8의 MV-QA-HARNESS(프로토 쪽)는 G0과 동시에 시작할 수 있다.
```

| 그룹 | 포함 계약 | 끝나면 쓸 수 있는 것 | 통합 지점 검증 |
|---|---|---|---|
| G0 기반 | MV-00 | 빈 프로젝트가 Boot→Game까지 열린다 | 컴파일, Boot 실행 1회 |
| G1 데이터 | MV-DATA, MV-SAVE | 모든 표가 읽히고 저장이 열린다 | EditMode: 표 로드·검증기·저장 왕복 |
| G2 리소스 도구 | MV-TOOL-ART, MV-TOOL-SND, MV-ART-ATLAS | 추출 PNG·오디오·아틀라스·Addressables 주소 | 추출 대조표(12 §12.4), 아틀라스 예산 |
| G3 런 코어 | MV-RUN-GEN, MV-RUN-SIM, MV-META | 화면 없이 런·정산·메타가 프로토와 같은 결과 | EditMode 로직 대조(12 §12.3) 전부 |
| G4 표시 | MV-VIEW, MV-FX, MV-SND | 런·로비 월드 화면, 이펙트, 소리 | **캡처 대조 1차**(월드 장면) |
| G5 UI | MV-UI-HUD, MV-UI-LOBBY, MV-UI-META, MV-UI-CONTENT | 정본 화면 전부 + 컨텐츠 메뉴 | **캡처 대조 2차**(UI 장면), 조작 흐름 |
| G6 플랫폼 | MV-PLAT-ADS, MV-PLAT-LOGIN, MV-PLAT-NET, MV-PLAT-ANALYTICS, MV-PLAT-IAP, MV-PLAT-NOTI | 관리자 계약 + 스텁 공급자 | EditMode 스텁 시험, 개발 빌드 가짜 광고 흐름 |
| G7 콘텐츠 | MV-CONTENT-<모드>, MV-META-<시스템> ([08](08_Content.md) 1차 범위) | 추가 모드·메타 | 표 확장 시험(12 §12.6), 모드별 EditMode |
| G8 인수 | MV-QA-HARNESS, MV-QA-ACCEPT | 「똑같다」 판정 보고서 | [12](12_Acceptance.md) 전체 |

병렬 가능 묶음(쓰기 범위가 겹치지 않음): G1과 G2와 MV-QA-HARNESS / G3 안의 GEN·SIM·META(인터페이스 합의 뒤) / G4 안의 VIEW·FX·SND / G5 안의 네 계약 / G6 여섯 계약 / G7 계약끼리.

## 11.3 계약 목록

경로 접두 `A/` = `Assets/Game/`, `S/` = `Assets/Game/Scripts/`.

### G0 기반

#### MV-00 프로젝트 생성·기반 연결
| 항목 | 내용 |
|---|---|
| 목표 | 새 Unity 프로젝트를 만들고 Framework 패키지·씬·asmdef·폴더·Addressables 기본 구성을 끝낸다. 이 기획서를 새 저장소 `Docs/MV-SPEC-01/`로 옮긴다 |
| 입력 | [10](10_Architecture.md) 전부, [01](01_Scope.md), [13](13_Decisions.md) D-01·D-02 |
| 쓰기 | 프로젝트 전체 골격: `Packages/`, `ProjectSettings/`, 모든 `.asmdef`(10 §10.4의 13개), `A/Scenes/Boot.unity`·`Game.unity`, `A/Addressables/` 폴더와 그룹 자산, URP 자산, `S/MV.Boot/`, `Docs/`, 저장소 규칙 파일(AGENTS.md 등) |
| 선행 | D-01(위치·이름·원격), D-02(연결 방식). 결정 전이면 권장안(`C:/Project/MoonVillage`, 외부 고정 tgz)으로 만들고 경로만 바꿀 수 있게 |
| 완료 | Unity 6000.3.9f1에서 열림, 컴파일 오류 0, Boot 씬 실행 → 10 §10.7의 1~3단계와 10단계가 돌고 빈 `Game` 씬이 열림. 시험 중 패키지는 연결하지 않고 스텁 인터페이스 자리만 있음(D-02 외부일 때) |
| 검증 | 컴파일 확인, Boot 실행 1회(로그 오류 0), 패키지 목록이 10 §10.2와 일치하는지 대조표 |

### G1 데이터

#### MV-DATA 표·검증기·로더
| 항목 | 내용 |
|---|---|
| 목표 | [07](07_Tables.md)의 모든 표(0·1·1xx·3xx 대역, `07_consumeType`, 문구)와 [08](08_Content.md)의 4xx·7xx·8xx 표의 행 클래스·표 클래스·TSV 원본·게임 검증기·`MvTables.RegisterAll`을 만든다. 공용 `ConsumeData`(정크버스터즈 `ImportDatas` 규칙: `{prefix}Type/Id/Value`, `;` 나열)와 `07_consumeType` 기반 아이콘·이름 해석, 리소스 열 검사기를 포함한다. 확률 표 801·802·803·804는 JJEvol 표 규격 v1 §6~9 모양이고, 추첨·확률 계산을 한 그래프로 하는 `MvDropRoller`(Framework `com.jjevol.economy.drop`이 나오면 어댑터 `MvDropSource`로 교체)를 만든다. 시트 → TSV 내보내기 도구(사람용 2줄·`#` 열 제거)와 `TableBuilder` + `ValidateTables` 콜백 등록을 필수 반영 경로로 만든다(07 §7.1.1). 행 값은 07·08의 예시 행(프로토 원값)을 그대로 넣는다. D-80(작은 표 id 형식) 답에 따라 첫 작업에서 재화 id를 일괄 정한다 |
| 입력 | [07](07_Tables.md) 전부, [08](08_Content.md) 표 정의 절, [10 §10.7](10_Architecture.md) 4단계 |
| 쓰기 | `S/MV.Data/`, `A/GameData/Table/TSV/`(모든 TSV의 최초 작성), `S/MV.Editor/Tables/`(검증기 등록) |
| 선행 | MV-00 |
| 완료 | 모든 표가 `LoadAllAsync`로 읽힘. 검증기가 07 §7.13 규칙 전부를 검사. 07 §7.14 확장 예시 행을 넣으면 검증 통과, 일부러 깨면 해당 오류 문구가 나옴 |
| 검증 | EditMode: 표별 행 수·대표 값(07 예시 행) 대조, 검증기 양·음 사례, 문구 키 누락 0. 추첨: 시드 고정 10만 회 빈도가 표시 확률과 ±0.5%p 안, 적 캣닢 묶음이 같은 u에서 정본 `u < 0.08`과 같은 결과, 순환·깊이 초과 묶음은 로드 오류 |

#### MV-SAVE 저장·이관
| 항목 | 내용 |
|---|---|
| 목표 | [09](09_Save.md)의 DataContainer 묶음, 저장 트리거, 버전·이관 체인, 진행 초기화, 손상 처리 |
| 입력 | [09](09_Save.md), [07](07_Tables.md) `01_wealth` |
| 쓰기 | `S/MV.Save/` |
| 선행 | MV-00. MV-DATA와 병렬(재화 표 키만 합의) |
| 완료 | 정본 `DEF()`와 같은 첫 실행 상태. 저장→재시작→로드 왕복이 같음. 이관 체인 시험 통과 |
| 검증 | EditMode: 기본값 대조(정본 DEF 전 필드), 왕복, v0→v1 이관, 손상 파일 처리 |

### G2 리소스 도구

#### MV-TOOL-ART 그림 추출 도구
| 항목 | 내용 |
|---|---|
| 목표 | 정본 HTML의 그리기 함수를 함수·인자별 PNG로 뽑는 도구([03 §3.2~3.5](03_ResourcePipeline.md)). 정본 파일은 고치지 않고 주입 스크립트로 처리 |
| 입력 | [03](03_ResourcePipeline.md) §3.1~3.7, §3.11 |
| 쓰기 | `Tools/MvExtract/`(도구), `A/Art/Source/`(출력 PNG), 추출 정의 JSON |
| 선행 | **Node·Playwright 설치 승인(03의 결정 ID)**. 승인 전에는 정의 JSON과 스크립트까지만 쓰고 실행은 NOT_RUN |
| 완료 | [03 §3.5](03_ResourcePipeline.md) 대응표의 모든 스프라이트가 PNG로 나옴. 같은 입력이면 같은 바이트(결정적). 표 리소스 열 중 정본에 없는 이름은 `Art/Temp/temp_art_list.tsv`로 뽑고 [03 §3.13](03_ResourcePipeline.md) 방식으로 임시 PNG를 만든다(카탈로그 `temp` 1) |
| 검증 | 03 §3.11 대조(키별 정본 캡처와 차이), 재실행 해시 동일 |

#### MV-TOOL-SND 소리 굽기
| 항목 | 내용 |
|---|---|
| 목표 | `SND` 합성음과 BGM을 OfflineAudioContext로 렌더해 WAV→OGG로 굽는다([03 §3.9](03_ResourcePipeline.md)) |
| 입력 | [03 §3.9](03_ResourcePipeline.md) |
| 쓰기 | `Tools/MvExtract/sound/`, `A/Addressables/Remote/Audio/` |
| 선행 | MV-TOOL-ART와 같은 설치 승인. 쓰기 범위가 달라 병렬 가능 |
| 완료 | `03_sound` 32행(효과음 30클립 + BGM 2, [03 §3.9.3](03_ResourcePipeline.md))의 파일이 모두 있고, 길이·채널·샘플레이트가 03 표와 같음 |
| 검증 | 파형 대조(정본 실시간 녹음 vs 구운 파일, 03 §3.11), 청취 확인은 사람 확인 NOT_RUN |

#### MV-ART-ATLAS 아틀라스·임포트 설정
| 항목 | 내용 |
|---|---|
| 목표 | 추출 PNG의 임포트 설정, SpriteAtlas v2 그룹, 압축, Addressables 주소, 각 표 리소스 열(icon·sprite·bg 등)의 이름이 실제 스프라이트·주소와 맞는지 확인([03 §3.8](03_ResourcePipeline.md)) |
| 입력 | [03 §3.8](03_ResourcePipeline.md), [07 §7.10](07_Tables.md) |
| 쓰기 | `A/Addressables/Remote/UIAtlas/`, `WorldAtlas/`, `Texture/`, `S/MV.Editor/Import/`(임포트 후처리·리소스 열 검사기). Addressables 그룹 **자산** 변경은 총괄에게 명세로 |
| 선행 | MV-TOOL-ART 출력, MV-DATA(표 클래스) |
| 완료 | 03 §3.8의 그룹별 크기·압축·개수 예산 안. 모든 표의 리소스 열 값이 실제 스프라이트·주소로 풀림(검사기 오류 0) |
| 검증 | EditMode: 모든 리소스 키 해석 성공, 아틀라스 크기·포맷 검사 스크립트 |

### G3 런 코어

#### MV-RUN-GEN 레벨 생성
| 항목 | 내용 |
|---|---|
| 목표 | `tplFor`·`makeObjs`·`fieldSpec`·`buildLevel`·`newRun`을 표 구동으로 구현([04](04_GameRules.md) 레벨 생성 절, [07 §7.5~7.6](07_Tables.md)) |
| 입력 | [04](04_GameRules.md), [07](07_Tables.md) §7.4~7.6 |
| 쓰기 | `S/MV.Run/Gen/`, `S/MV.Run.Tests/Gen/` |
| 선행 | MV-DATA. 오브젝트 상태 구조(`RunObject`)는 MV-RUN-SIM과 먼저 합의하고 그 파일은 SIM이 쓴다 |
| 완료 | Level 1~20 × Area 1~15 × Floor 1~3의 생성 결과(오브젝트 종류·x·z·hp·보상)가 프로토 덤프와 같음 |
| 검증 | EditMode 대조: 프로토에서 뽑은 생성 덤프 JSON([12 §12.3](12_Acceptance.md))과 전 필드 비교. hash 결과 고정표 대조 |

#### MV-RUN-SIM 런 시뮬레이션
| 항목 | 내용 |
|---|---|
| 목표 | 이동·사격·분신·투사체·피격 분기·게이트 충전과 수령·우물·계단·적 AI·카드 룰렛·계단 오르기·신 효과·종료 판정·`RunEvent` 발행([04](04_GameRules.md)) |
| 입력 | [04](04_GameRules.md) 전부, [07 §7.3, §7.5, §7.7](07_Tables.md) |
| 쓰기 | `S/MV.Run/`(Gen 제외), `S/MV.Run.Tests/`(Gen 제외) |
| 선행 | MV-DATA, MV-RUN-GEN과 `RunObject` 합의 |
| 완료 | 같은 시드·입력 기록이면 프로토와 같은 프레임별 결과(허용 오차 [12 §12.3](12_Acceptance.md)). 오브젝트 종류 동작이 전부 30~36 표 조합으로 결정되고 종류 이름 분기(`if kind=="tower"`)가 없음 |
| 검증 | EditMode: 입력 기록 재생 대조, 공식 단위 시험(04의 공식마다 1개 이상), 새 종류 행 추가 시험(12 §12.6-A) |

#### MV-META 메타 규칙
| 항목 | 내용 |
|---|---|
| 목표 | 결과 정산(collect, ×3, 토큰), Area 개방, 로비 강화 3종, Area 강화 5종, 부활 4종, 신·덱 머지, 광고 한도·일일 초기화, 신기능 해금 판정. 영속 변경의 유일한 진입점 |
| 입력 | [04](04_GameRules.md) 메타 절, [07 §7.8](07_Tables.md), [09](09_Save.md) |
| 쓰기 | `S/MV.Meta/`, `S/MV.Meta.Tests/` |
| 선행 | MV-DATA, MV-SAVE |
| 완료 | 정본 메타 공식과 같은 비용·효과·상태 변화. 광고 공급자 없이도 동작(가짜 광고 경로) |
| 검증 | EditMode: 강화 비용 표(07 예시 P=0~10 등) 대조, 정산 시나리오 대조(프로토 덤프), 일일 초기화 시계 주입 시험 |

### G4 표시

#### MV-VIEW 월드 렌더
| 항목 | 내용 |
|---|---|
| 목표 | 카메라 리그(런·로비·계단 오르기·흔들림), 빌보드·정렬·안개·밤·틴트 셰이더, 지면·계단 메쉬, 테마 소품 배치, 오브젝트 뷰·월드 라벨(TMP), 로비 아지트([02](02_Visual.md)) |
| 입력 | [02](02_Visual.md), [03 §3.5~3.8](03_ResourcePipeline.md), [07 §7.10](07_Tables.md) |
| 쓰기 | `S/MV.View/`, `A/Art/Shaders/`, `A/Art/Materials/`, `A/Addressables/Remote/World/`(월드 프리팹) |
| 선행 | MV-RUN-SIM(읽기 인터페이스), MV-ART-ATLAS |
| 완료 | 12 §12.2 월드 장면(W-01~)의 캡처 대조가 허용 오차 안 |
| 검증 | 통합 지점 **캡처 대조 1차**. 드로우콜·메모리 예산([03 §3.8](03_ResourcePipeline.md)) 측정 |

#### MV-FX 이펙트
| 항목 | 내용 |
|---|---|
| 목표 | `RunEvent` → `EffectManager` 이펙트, 데미지 팝업 풀(최대 60), 히트스톱 표시 쪽, HUD 비행 연출, 우다다 연출([06](06_Effects.md)) |
| 입력 | [06](06_Effects.md), [07 §7.11](07_Tables.md) |
| 쓰기 | `S/MV.Fx/`, `A/Addressables/Remote/Effect/`, `A/GameData/Table/TSV/04_effect.txt`(행만) |
| 선행 | MV-RUN-SIM(RunEvent), MV-VIEW(월드→화면 변환 API) |
| 완료 | 06의 모든 이펙트 키가 재생되고 수량·수명·색이 06 표와 같음 |
| 검증 | 12 §12.2의 FX 장면 캡처(연속 프레임) 대조, 파티클 수 예산 |

#### MV-SND 소리 재생
| 항목 | 내용 |
|---|---|
| 목표 | `RunEvent`·UI 이벤트 → `AudioManager`, 최소 간격 GAP, 피치 무작위, BGM 로비/런 레이어 전환 |
| 입력 | [03 §3.9](03_ResourcePipeline.md), [07 §7.11](07_Tables.md) |
| 쓰기 | `S/MV.Sound/`, `A/GameData/Table/TSV/03_sound.txt`(행만) |
| 선행 | MV-TOOL-SND, MV-RUN-SIM |
| 완료 | 정본 SND 호출 지점마다 같은 키가 같은 조건으로 울림 |
| 검증 | EditMode: 이벤트→키 대응표 시험, GAP 시험. 청취는 사람 확인 NOT_RUN |

### G5 UI

| ID | 목표 | 입력 | 쓰기 | 선행 |
|---|---|---|---|---|
| MV-UI-HUD | 런 HUD, 토스트·배너·플래시, 룰렛·HAND 카드 연출, 데미지 숫자 표시 층 | [05](05_Screens.md) HUD·HAND 절, [06](06_Effects.md) | `S/MV.UI/Hud/`, `A/Addressables/Remote/UI/Hud/` | MV-RUN-SIM, MV-ART-ATLAS |
| MV-UI-LOBBY | 로비 메인, 강화 버튼 3종·덱 머지 연출, 결과(정산) 화면, 가짜 광고 팝업 | [05](05_Screens.md) 로비·결과 절 | `S/MV.UI/Lobby/`, `A/Addressables/Remote/UI/Lobby/` | MV-META |
| MV-UI-META | 야옹신, Area 강화, 부활, 지도, 신기능 해금 오버레이, 테스트 패널, 메타 공통 패널 틀 | [05](05_Screens.md) 해당 절 | `S/MV.UI/Meta/`, `A/Addressables/Remote/UI/Meta/` | MV-META |
| MV-UI-CONTENT | 로비 「컨텐츠」 버튼과 추가 컨텐츠 메뉴(표 `400_content` 구동, 잠금 `05_contentOpen`), 항목 탭 → 모드 시작 | [05](05_Screens.md) 컨텐츠 메뉴 절, [08 §8.4](08_Content.md) | `S/MV.UI/Content/`, `A/Addressables/Remote/UI/Content/` | MV-DATA, MV-UI-LOBBY의 로비 버튼 자리(합의만) |

> **1차 UI 범위(2026-11-10 사용자 결정 D-92)**: 사용자 원문 「아 UI도 그냥 리소스 없이 OnGUI나 더미로 구성하면 많이 축소 가능해?」 → 권장안 설명 후 「응 그렇게 진행하자」. 「구조는 실제, 겉은 더미」. ① 런 HUD(MV-UI-HUD)는 추출 스킨으로 만든다(플레이 중 손맛 화면). ② 로비·메타·컨텐츠(MV-UI-LOBBY·META·CONTENT)는 OnGUI를 쓰지 않고 uGUI + Framework `UIManager` 프리팹 구조로 만들되 겉은 기본 상자·글자·버튼 배치만 한다. 정본 배치·크기 맞춤과 스킨 입히기는 하지 않는다. ③ 화면 컨트롤러와 표·메타 규칙 연결 코드는 실제로 만든다(표에 행을 더하면 화면에 바로 나와야 한다). ④ 덱 머지·해금 오버레이·화면 전환 애니메이션 같은 연출은 2차(후속 계약). ⑤ 나중에 겉만 갈아 끼울 수 있게 화면 코드(컨트롤러)와 겉(프리팹·스킨)을 나눈다. 그래서 1차 공통 완료는 「05의 요소·데이터·동작이 있고 표 행 추가가 화면에 나온다(EditMode)」이며, 아래 「공통 완료」의 위치·크기·색·캡처 조건은 MV-UI-HUD와 2차에만 적용한다. 연출 후속 계약: MV-UI-FX2(덱 머지·해금 오버레이·화면 전환).

공통 완료: 05의 요소 표 위치·크기·색·문구 키가 맞고, 12 §12.2 UI 장면 캡처가 허용 오차 안. 모든 이미지는 `AddressableImage`/`AddressableTexture`, 문구는 문구 키. 공통 검증: 통합 지점 **캡처 대조 2차**, 조작 흐름 시험(12 §12.5).

### G6 플랫폼(스텁 우선)

| ID | 목표 | 쓰기 | 완료 |
|---|---|---|---|
| MV-PLAT-ADS | `AdManager` 연결, 게임 인터페이스 `IMvAds`, 공급자 없음 → 개발 빌드 가짜 광고(정본 3초) | `S/MV.Platform/Ads/` | `08_adPlacement` 모든 배치가 보상·한도대로 동작 |
| MV-PLAT-LOGIN | `LoginManager` 게스트 로그인 스텁, 계정 공급자 자리 | `S/MV.Platform/Login/` | 게스트로 시작, 공급자 교체 지점 |
| MV-PLAT-NET | `NetworkManager` 로컬 서비스 스텁(D-08), 서버 시간 인터페이스 | `S/MV.Platform/Net/` | 오프라인 정상 동작, 서버 붙일 자리 |
| MV-PLAT-ANALYTICS | `AnalyticsManager` 이벤트 목록(런 시작·끝, Area 개방, 강화, 광고, 모드 시작) + 로그 공급자 | `S/MV.Platform/Analytics/` | 이벤트 이름·파라미터 표가 코드와 일치 |
| MV-PLAT-IAP | `PurchaseManager` 스텁 공급자, 상점 상품([08](08_Content.md)) 연결 | `S/MV.Platform/Iap/` | 스텁 구매 성공·실패·복원 흐름 |
| MV-PLAT-NOTI | `LocalNotificationManager`(부활 쿨다운·일일 보상 알림) | `S/MV.Platform/Noti/` | 예약·취소 시험 |

선행: MV-00, MV-META. D-02가 「외부」이고 패키지 고정본이 없으면 게임 인터페이스 + 스텁만 만들고 Framework 연결은 고정본이 나오면 하는 후속 계약으로 남긴다([10 §10.2](10_Architecture.md)). 검증: EditMode 스텁 시험. 실제 SDK 연결은 범위 밖.

### G7 콘텐츠

[08](08_Content.md)의 1차 출시 범위 표에 있는 모드·메타마다 계약 1건을 만든다. 이름 규칙 `MV-CONTENT-<모드id>`(모드), `MV-META-<시스템>`(퀘스트·펫 등).

| 항목 | 내용 |
|---|---|
| 목표 | 08의 해당 절 규칙·표 행 구현. 모드는 `IModeRule` 구현으로 기존 런 코어를 재사용 |
| 입력 | [08](08_Content.md) 해당 절, [05](05_Screens.md) 메타 공통 패널 틀 |
| 쓰기 | 모드: `S/MV.Content/Modes/<모드id>/`, `A/Addressables/Remote/Mode/<모드id>/`, 그 모드의 TSV 행. 메타: `S/MV.Content/<시스템>/`, `S/MV.UI/Content/<시스템>/`, 그 시스템 표 TSV |
| 선행 | G3, G5(공통 패널 틀), MV-UI-CONTENT(모드) |
| 완료 | 컨텐츠 메뉴에서 시작 → 끝 → 보상 수령까지. 행 추가만으로 단계·항목이 늘어남 |
| 검증 | EditMode 규칙 시험, 표 확장 시험(12 §12.6-C) |

### G8 인수

#### MV-QA-HARNESS 같은 상태 캡처 도구
| 항목 | 내용 |
|---|---|
| 목표 | 프로토와 Unity를 **같은 상태**로 만들고 같은 크기로 캡처하는 양쪽 도구([12 §12.1~12.2](12_Acceptance.md)). 프로토 쪽은 정본을 고치지 않고 Playwright 주입 스크립트로 `Math.random` 시드 고정, `save` 주입, 시간 고정, 장면 진입 후 캡처. Unity 쪽은 같은 장면 정의 JSON을 읽어 상태를 주입하고 캡처하는 Editor/개발 빌드 도구. 생성·시뮬 덤프(JSON) 출력 기능 포함 |
| 쓰기 | `Tools/MvCapture/`(프로토 쪽), `S/MV.Editor/Capture/`, `S/MV.Boot/DebugInject/`(개발 빌드 전용), `QA/Scenes/*.json`(장면 정의) |
| 선행 | 프로토 쪽: 설치 승인만(G0과 동시 시작 가능). Unity 쪽: MV-RUN-SIM, MV-VIEW |
| 완료 | 12 §12.2 장면 목록 전부를 양쪽에서 캡처. 프로토 쪽은 같은 장면을 두 번 찍으면 픽셀 동일 |
| 검증 | 프로토 재현성(2회 해시 동일), Unity 쪽 주입 상태 덤프가 장면 정의와 일치 |

#### MV-QA-ACCEPT 인수 판정
| 항목 | 내용 |
|---|---|
| 목표 | [12](12_Acceptance.md) 전체를 돌리고 판정 보고서 작성 |
| 쓰기 | `QA/Reports/` |
| 선행 | G1~G7 통합 |
| 완료 | 12의 모든 항목이 PASS 또는 사람 확인 NOT_RUN(사유 명시). FAIL은 원인 계약에 수정 작업으로 되돌림 |
| 검증 | 보고서에 후보 SHA, 정본 SHA256, 장면별 수치 |

## 11.4 쓰기 범위 겹침 점검

| 경로 | 유일 Writer |
|---|---|
| `Packages/`, `ProjectSettings/`, `*.asmdef`, `Scenes/*.unity`, Addressables 설정·그룹 자산, URP 자산 | 총괄(MV-00 및 이후 통합) |
| `S/MV.Data/`, TSV 최초 작성 | MV-DATA |
| `S/MV.Save/` | MV-SAVE |
| `S/MV.Run/Gen/` | MV-RUN-GEN |
| `S/MV.Run/`(Gen 제외) | MV-RUN-SIM |
| `S/MV.Meta/` | MV-META |
| `S/MV.View/`, `A/Art/Shaders|Materials/`, 월드 프리팹 | MV-VIEW |
| `S/MV.Fx/`, 이펙트 프리팹, `04_effect` 행 | MV-FX |
| `S/MV.Sound/`, `03_sound` 행 | MV-SND |
| `Tools/MvExtract/`, `A/Art/Source/` | MV-TOOL-ART(소리 하위 폴더는 MV-TOOL-SND) |
| 아틀라스·텍스처 폴더 | MV-ART-ATLAS |
| `S/MV.UI/<Hud|Lobby|Meta|Content>/`와 대응 UI 프리팹 폴더 | 해당 MV-UI-* |
| `S/MV.Platform/<영역>/` | 해당 MV-PLAT-* |
| `S/MV.Content/<모드|시스템>/`, 그 표 TSV | 해당 MV-CONTENT-*/MV-META-* |
| `Tools/MvCapture/`, `S/MV.Editor/Capture/`, `QA/` | MV-QA-* |

TSV는 파일 단위로 Writer가 하나다. MV-DATA가 최초 작성 뒤, 위 표에 적힌 계약이 자기 표 파일의 행만 바꾼다. 열 추가는 MV-DATA 변경 요청으로만 한다.

## 11.5 원장 기록

계약을 시작할 때 새 프로젝트의 TaskBoard(또는 저장소 규칙의 원장)에 계약 ID, 담당, 작업 브랜치, 기준 SHA, 쓰기 범위를 적는다. 끝날 때 결과 SHA, 검증 결과(PASS/FAIL/NOT_RUN), 남은 결정 ID를 적는다. 담당 대화는 계약 1건에 1개다.
