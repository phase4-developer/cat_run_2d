# MV-SPEC-01 달동네 Unity 포팅 기획서 — 입구

작업 ID `MV-SPEC-01`. 작성 2026-11-09 KST, Claude. 상태: **초판 완료, 결정 대기 항목 있음**([13](13_Decisions.md)).

웹 프로토타입 「CAT ESCAPE 달동네」(`MoonVillage.html`, 단일 HTML, Canvas 2D)를 Unity로 옮기는 정밀 기획서다. 이 기획서를 jjevol-orchestrate 총괄에게 넘기면 담당들이 추가 질문 없이 개발할 수 있어야 한다. 완성품 조건은 둘이다.

- **비주얼과 플레이 감각이 `MoonVillage.html`과 같다.**
- **내부는 JJEvol Framework와 표 기반이다.** 수치·콘텐츠·오브젝트 종류·배치를 표에서 조정하고 행 추가로 늘린다.

## 핵심 방침 (확정분)

| 방침 | 내용 | 근거 |
|---|---|---|
| 표 구조 | **정크버스터즈 방식**: `NNN_camelCase` 번호 대역(0 공통·1 성장·1xx 개체·3xx 런·배치·4xx 콘텐츠·7xx 상점·8xx 확률), 정수 id `{표}{그룹3}{순번3}` | 사용자 2026-11-09 「테이블 구조는 정크버스터즈 참고 많이해줘」 |
| 보상·비용 | 전부 consumeData 3열(`rewardType/rewardId/rewardValue`, `priceType/…`, `;` 목록) + `07_consumeType` 정의 표 | 「보상같은건 consumeData형태로 테이블에서 수정 가능해야하고」 |
| 확률 보상 | JJEvol 표 규격 v1 §6~9: `801_drop` 묶음에서 정수 weight로 1행(꽝 = rewardType 0 행), `802_randomBox` 칸마다 1행, 천장 803·804, 추첨과 확률 표시는 한 계산. 엔진은 Framework `com.jjevol.economy.drop`(설계 중)으로 옮긴다 → [07 §7.9.3](07_Tables.md), [13 §13.3.6](13_Decisions.md) | 2026-11-09 공용 기준 전파 |
| 리소스 | 각 표의 `icon`·`sprite`·`bg`·`bgm`·`hitSound`·`hitEffect` 열에 직접. 아틀라스·아이콘 하드코딩 금지 | 「리소스도 테이블에서 설정가능해야해」 |
| 오브젝트 종류 | `105_objectKind` = 행동 컴포넌트(106 피격·107 충전·108 수령·109 이동·114 HP) 조합. 코드에 종류 이름 분기 없음 | 사용자 원문 「오브젝트 종류 컨트롤 가능하게」 |
| UI | UGUI, `AddressableImage`·`AddressableTexture` | 사용자 확정 |
| 이미지 최적화 | SpriteAtlas 그룹·ASTC·Area/테마 단위 로드 | 「이미지 리소스 사용은 최적화도 고민해야해 아틀라스 사용같은것들」 → [03 §3.8](03_ResourcePipeline.md) |
| 아트 | 정본 그림 추출이 정식 아트. 새 테마는 재가공. 정본에 없는 그림만 임시 아트(같은 이름, `Art/Temp/`, 카탈로그 `temp` 열) → [03 §3.13](03_ResourcePipeline.md) | 사용자 2026-11-09, 프로젝트 설정 「임시 아트로 먼저」 |
| 콘텐츠 | 로비 「컨텐츠」 버튼 → 추가 컨텐츠 메뉴(`400_content` 행) → 누르면 시작 | 사용자 원문 |

## 읽는 순서

| 역할 | 먼저 읽을 것 |
|---|---|
| 총괄 | 이 문서 → [01](01_Scope.md) → [13](13_Decisions.md) → [11](11_WorkContracts.md) → [12](12_Acceptance.md) → [10](10_Architecture.md) |
| 런·규칙 담당 | [04](04_GameRules.md) → [07](07_Tables.md) → [10](10_Architecture.md) |
| 렌더·리소스 담당 | [02](02_Visual.md) → [03](03_ResourcePipeline.md) → [06](06_Effects.md) |
| UI 담당 | [05](05_Screens.md) → [03 §3.8 아틀라스](03_ResourcePipeline.md) → [07 §7.10 리소스 열·§7.12 문구](07_Tables.md) |
| 표·데이터 담당 | [07](07_Tables.md) → [08](08_Content.md) → [09](09_Save.md) |
| QA | [12](12_Acceptance.md) → [11](11_WorkContracts.md)의 검증 열 |

## 목차

| 절 | 파일 | 내용 |
|---|---|---|
| 1 | [01_Scope.md](01_Scope.md) | 정본 SHA256, 기준 프리셋, 포함·제외, 결정 상태 |
| 2 | [02_Visual.md](02_Visual.md) | 2.5D 투영 재현 방식, 카메라 공식, 정렬·안개·밤·틴트, 해상도·안전 영역 |
| 3 | [03_ResourcePipeline.md](03_ResourcePipeline.md) | 코드 그림 → PNG·아틀라스 추출 도구와 절차, 함수↔스프라이트 대응표 전체, **아틀라스·압축·메모리 최적화**, 합성음 → 오디오 파일(`03_sound` 33행) |
| 4 | [04_GameRules.md](04_GameRules.md) | 조작·이동·사격·분신·오브젝트·게이트·우물·계단·카드·적·Floor·체크포인트·정산·메타·신·카드 효과 (HTML 수식 원문) |
| 5 | [05_Screens.md](05_Screens.md) | 화면 명세 전체, 로비 「컨텐츠」 메뉴 신규 설계 |
| 6 | [06_Effects.md](06_Effects.md) | 이펙트·흔들림·히트스톱·배너·토스트 |
| 7 | [07_Tables.md](07_Tables.md) | **표 설계(핵심)**: 정크버스터즈 대비 구조, consumeData, 오브젝트 종류 조합, 배치, 전투·메타, 리소스 열 규칙, 검증기, 확장 예시 |
| 8 | [08_Content.md](08_Content.md) | 30 Area, 신·카드, 모드·메타 콘텐츠의 달동네판 |
| 9 | [09_Save.md](09_Save.md) | 저장 스키마, 버전·이관 |
| 10 | [10_Architecture.md](10_Architecture.md) | 모듈, 씬, Framework 범위·대응표, 폴더·Addressables, 로딩 흐름 |
| 11 | [11_WorkContracts.md](11_WorkContracts.md) | 오케스트라 작업 계약 목록 |
| 12 | [12_Acceptance.md](12_Acceptance.md) | 「똑같다」 판정, 표 확장 시험 |
| 13 | [13_Decisions.md](13_Decisions.md) | 미해결·사용자 결정 목록 |

## 표기 규칙

- `L123`: 정본 HTML 줄 번호. 범위는 `L100~120`.
- 값의 출처 표시: **[프로토]** 정본 원값, **[원작]** HellEscape 원작 규칙·표 참고값, **[창작]** 이 기획서의 임시 창작값. 창작값은 플레이 시험으로 조정한다.
- **[추정]**: 코드만으로 확정하지 못한 해석. 근거 줄을 함께 적는다.
- **결정 대기**: 사용자 답 전까지 권장안을 기준으로 썼다. 답이 바뀌면 영향 범위는 [13](13_Decisions.md)에 적었다.

## 근거 자료

- 정본: [`../Reference/MoonVillage.html`](../Reference/MoonVillage.html) = `C:/Project/MoonVillage/Docs/Reference/MoonVillage.html` (SHA256 `fe573cf2…26dfa`)
- 공통 분석(출발점): HellEscape 저장소 `C:/Project/HellEscape/Docs/HE-PROTO-REF-01/PrototypeAnalysis.md`
- 콘텐츠 참고: HellEscape `Docs/OriginalGameRules.md`, HellEscape `Docs/ContentStatus.md`, HellEscape 원작 메타 표(`Assets/HellEscapeProduct/Resources/HellEscapeProduct/`, 값은 [원작] 표시로만)
- 표 구조 참고: 정크버스터즈 표 TSV `C:/Project/projectSlot/Assets/ProjectSpin/Table/TSV/`, `ConsumeData.cs`, 구글 시트 「junkBusters_Table」의 「Consume정의」(구조만 참고, 값·이름은 옮기지 않음)
- Framework: `C:/Project/JJEvolFramework/Docs/Guide/`(AIUsage, PackageMap, Delivery, NewGame, Installation, Features), 패키지 README
- 규칙: `C:/Project/JJEvolAutomation/policies/External.md`·Core·Common, `profiles/game*.md`, `skills/jjevol-plan`·`jjevol-orchestrate`(+references)·`jjevol-framework`·`jjevol-art-ui`

## 저장 위치

정본 위치는 새 프로젝트 `C:/Project/MoonVillage/Docs/MV-SPEC-01/`이다(2026-11-09 사용자 지정으로 이동). 작성 이력은 HellEscape 저장소 브랜치 `claude/MV-SPEC-01-moonvillage-spec`(마지막 `6158132a`)에 남아 있다. 프로젝트 설정은 `C:/Project/MoonVillage/Docs/AI/ProjectSettings.json`(새 게임·회사·2.5D·URP Universal·Unity 6000.3.9f1·Android·iOS·세로·터치·uGUI).
