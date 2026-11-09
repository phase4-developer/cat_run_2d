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

## Framework

- 연결 방식 「외부」: `Docs/AI/PROJECT_CONTEXT.md` 「Framework 기준선」의 고정 tgz만 `Packages/Frozen/`에 두고 `file:Frozen/...`으로 연결한다. 시험 중 패키지는 연결하지 않는다.
- 외부 패키지·SDK 설치는 매번 사용자에게 확인한다.
