# MoonVillage 저장소 규칙

AI 작업자가 이 저장소에서 따르는 규칙이다. 여기 없는 것은 `Docs/AI/PROJECT_CONTEXT.md`의 「AI가 따를 규칙」과 기획서 `Docs/MV-SPEC-01/`을 따른다.

## 먼저 읽을 것

- `Docs/AI/PROJECT_CONTEXT.md` — 프로젝트 설정(설정 화면이 만든 절), Framework 기준선, 실행기 등록.
- `Docs/MV-SPEC-01/README.md` — 기획서 입구. 작업 계약은 `11_WorkContracts.md`, 인수 기준은 `12_Acceptance.md`.
- 대조 기준 `Docs/Reference/MoonVillage.html`(SHA256 `Docs/Reference/SHA256.txt`). 바이트를 바꾸지 않는다.

## 경계

- 회사 프로젝트다. 기획·코드·아트를 이 PC의 이 저장소 밖으로 내보내지 않는다. 중앙 보고에는 ID·상태·경로만 쓴다.
- Push·원격 변경과 `main` 반영은 사용자가 그 회차에 직접 말할 때만 한다.

## Git

- 테스트 브랜치는 `dev`다. 계약 1건 = 작업 브랜치 1개, 이름 `ai/<계약ID>-<짧은설명>`, `dev`에서 만든다.
- 검증을 통과한 자기 작업 브랜치 변경은 묻지 않고 Commit하고 `dev`에 병합한다. 실행하지 않은 검증은 NOT_RUN으로 적는다.
- 검증 없이 쌓는 변경은 기능 1묶음 또는 파일 20개까지.

## Unity

- Unity 6000.3.9f1, URP, uGUI, Input System. 세로 화면.
- AI가 Unity로 돌리는 것은 중앙 실행기(`unity_job.py`, 프로젝트 id `moonvillage`)의 compile·EditMode·PlayMode뿐이다.
- 통합 Writer: `Packages/manifest.json`, `ProjectSettings/`, 모든 `.asmdef`, `Assets/Game/Scenes/*.unity`, Addressables 설정·그룹 자산, URP 설정 자산은 총괄 1명만 쓴다(기획서 11 §11.1).
- 새 파일·폴더를 Commit할 때 `.meta`도 함께 넣는다. 실행기는 실행 중 생긴 파일을 후보 불일치로 본다.
- 새 파일·폴더를 Commit할 때 `.meta`도 함께 넣는다. 이미 있던 자산에 Unity가 새로 쓴 `.meta`는 실행기가 `project_generated`로 적고 PASS로 둔다. 담당이 쓰기 범위를 보고 바로 Commit한다. 그 밖의 `SOURCE_CANDIDATE_CHANGED`(아틀라스·Addressables 그룹 등)는 담당이 범위를 확인해 Commit하고 FAIL한 모드부터 다시 낸다. 총괄이 대신 Commit하지 않는다.
- 시험은 적당히: 계약 브랜치는 compile 1회와 바꾼 모듈의 EditMode 1회. dev 병합 뒤 전체 시험은 그룹 끝이나 통합 Writer 파일(asmdef·Scene·Addressables·manifest·ProjectSettings)·쓰기 범위가 겹친 병합 뒤에만 한다. 그 밖의 병합은 계약 브랜치 PASS SHA를 증거로 둔다.
- 런타임 데이터는 빌드 산출물로만 읽는다. 표는 `Assets/Game/GameData/Table/Resources` 산출물(TableBuilder)만 읽고, 에디터에서도 TSV 폴더 같은 대체 경로로 우회하지 않는다. 산출물이 없으면 에디터에서도 오류로 드러낸다. 같은 이유로 실기에서만 드러나는 차이(IL2CPP 코드 제거, Addressables 경로)를 에디터 전용 분기로 가리지 않는다(2026-11-10 사용자 원문 「앞으로 대체 경로로 읽지말고 Resources 산출물을 읽어 그런 우회로 처리하니까 문제야 이건 패키지 프로젝트에도 전달하면서 규칙에 추가하라해 필요하면 전체 규칙으로 설정도하고」).

## Framework

- 연결 방식 「외부」: tgz를 `Packages/Frozen/`에 두고 `file:Frozen/...`으로 연결한다. 기준선은 `Docs/AI/PROJECT_CONTEXT.md` 「Framework 기준선」에 적는다.
- **프로젝트 설정이 이 파일보다 먼저다.** `PROJECT_CONTEXT.md` 설정 절의 「Framework 범위」에 적힌 패키지는 사용자가 범위로 승인한 것이라 묻지 않고 연결한다. 고정본이 있으면 그것을, 아직 없으면 Framework 후보 tgz를 쓰고 기준선에 「시험 중」과 SHA를 적는다(사용자 2026-11-10: 「저거 프로젝트 설정을 우선하도록 규칙 바꿔줘」). 통합 Writer(`Packages/manifest.json`)는 그대로 총괄 1명이 쓴다.
- 범위 밖 서드파티 SDK(Firebase·AdMob·Unity IAP 본체 등)와 유료만 설치 전에 사용자에게 확인한다. 결제·통계·알림은 패키지만 넣어 둔다(D-88).
