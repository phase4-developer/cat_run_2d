# -*- coding: utf-8 -*-
import sys
import diagrams as D
from svglib import table

OUT = sys.argv[1]

CSS = """
@page { size: A4; margin: 14mm 13mm 14mm 13mm; }
* { box-sizing: border-box; }
body { font-family: 'Malgun Gothic','Noto Sans KR',sans-serif; font-size: 9.6pt; color: #222; line-height: 1.5; margin: 0; }
h1 { font-size: 24pt; margin: 0 0 6pt; color: #2F3B52; }
h2 { font-size: 15pt; color: #fff; background: #2F3B52; padding: 5pt 10pt; border-radius: 4pt; margin: 0 0 10pt; }
h3 { font-size: 11.5pt; color: #2F3B52; border-left: 4pt solid #3B5BA9; padding-left: 7pt; margin: 14pt 0 6pt; }
.sec { page-break-before: always; }
.fig { break-inside: avoid; margin: 6pt 0 10pt; border: 1px solid #E4E4E4; border-radius: 6pt; padding: 6pt; }
.cap { font-size: 8.6pt; color: #666; margin-top: 3pt; text-align: center; }
table { border-collapse: collapse; width: 100%; margin: 4pt 0 10pt; font-size: 8.6pt; break-inside: auto; }
tr { break-inside: avoid; }
th { background: #E9EDF5; color: #2F3B52; text-align: left; padding: 3.5pt 5pt; border: 1px solid #CFD6E4; }
td { padding: 3pt 5pt; border: 1px solid #DDD; vertical-align: top; }
tbody tr:nth-child(even) td { background: #FAFBFD; }
code, .c { font-family: Consolas, 'Malgun Gothic', monospace; font-size: 8.3pt; background: #F2F4F8; padding: 0 2pt; border-radius: 2pt; }
.lead { font-size: 10.2pt; background: #F5F7FB; border-left: 4pt solid #3B5BA9; padding: 6pt 10pt; margin: 6pt 0 10pt; }
.warn { background: #FDF1F0; border-left: 4pt solid #C0392B; padding: 6pt 10pt; margin: 6pt 0; }
.ok { color: #3E8E41; font-weight: bold; } .no { color: #BBB; }
.legend span { display: inline-block; padding: 1pt 7pt; border-radius: 3pt; margin: 0 3pt 3pt 0; font-size: 8.5pt; border: 1px solid; }
.toc { columns: 2; font-size: 10.5pt; } .toc div { margin: 2pt 0; }
.cover { height: 250mm; display: flex; flex-direction: column; justify-content: center; }
.small { font-size: 8.4pt; color: #555; }
ul { margin: 3pt 0 6pt 14pt; padding: 0; } li { margin: 1.5pt 0; }
"""


def fig(svg, cap=None):
    c = f'<div class="cap">{cap}</div>' if cap else ''
    return f'<div class="fig">{svg}{c}</div>'


def c(s):
    return f'<code>{s}</code>'


parts = []
P = parts.append

# ---------- 표지 ----------
P('<div class="cover">')
P('<div class="small">MoonVillage · 사내 개발 문서</div>')
P('<h1>코드 구조 한눈에 보기</h1>')
P('<div style="font-size:13pt;color:#555;margin-bottom:18pt">클래스 구조 · 함수 흐름 · 리소스 관리와 재사용</div>')
P('<div class="lead">기준 커밋: <b>dev 046c8be6</b> (2026-11-15) · Unity 6000.3.9f1 · URP · uGUI<br>'
  '모든 그림과 표는 코드를 직접 읽어 확인했다. 줄 번호와 세부 근거는 같은 폴더의 '
  '<code>Docs/Architecture/01~05_*.md</code>에 있다.<br>경로 약어 <code>S/</code> = <code>Assets/Game/Scripts/</code></div>')
P('<h3>차례</h3><div class="toc">')
for i, t in enumerate(['게임 한 바퀴', '모듈 지도와 의존', '모듈끼리 찾는 방법(정적 슬롯)', '부팅 순서', '프레임 루프',
                       '시뮬 한 틱', '런 시작', '런 종료와 정산', '메타 요청(강화·환생 등)', '저장', '표 데이터',
                       '리소스 로드', '에셋 수명', '오브젝트 풀과 재사용', '이펙트와 소리', '화면(UI) 구조',
                       '무엇을 고치려면 어디를', '아직 비어 있는 곳'], 1):
    P(f'<div>{i}. {t}</div>')
P('</div>')
P('<h3>그림 색 구분</h3><div class="legend">')
for name, (f, s_) in [('Boot·조립', ('#E3E9F7', '#3B5BA9')), ('Data·표', ('#E6F2E6', '#3E8E41')),
                      ('Run·시뮬', ('#E2F0FB', '#1F78B4')), ('Meta·Content', ('#EFE6F7', '#7B4FA6')),
                      ('Save·저장', ('#F6E6F0', '#A64F86')), ('View', ('#FDEEDC', '#D9822B')),
                      ('Fx', ('#FDE6E3', '#C8553D')), ('Sound', ('#FFF5D6', '#B8860B')),
                      ('UI', ('#DDF3F1', '#16857A')), ('Framework 패키지', ('#F4F4F4', '#8A8A8A')),
                      ('주의·실패', ('#FDECEC', '#C0392B')), ('메모', ('#FFFBE6', '#C9A227'))]:
    P(f'<span style="background:{f};border-color:{s_};color:{s_}">{name}</span>')
P('</div></div>')

# ---------- 1 ----------
P('<div class="sec"><h2>1. 게임 한 바퀴</h2>')
P('<div class="lead">씬은 둘이다. <b>Boot 씬</b>은 앱을 켤 때 한 번 돌고, <b>Game 씬</b> 안에서 '
  '<code>MvGameLoop</code> 하나가 로비 → 런 → 지도 → 결과 → 로비를 계속 돌린다. 런은 순수 C# 시뮬 '
  '<code>RunSim</code>이 계산하고, 화면은 그 상태를 <b>읽기만</b> 한다. 저장되는 모든 변경은 '
  '<code>MvMeta.Send</code> 한 곳을 지난다.</div>')
P(fig(D.d_cycle(), '그림 1. Game 씬 안의 순환 (S/MV.Boot/MvGameLoop.cs)'))
P('<h3>씬에 놓인 것</h3>')
P(table(['씬', '루트 오브젝트', '역할'], [
    ['Boot (빌드 0번)', 'Boot(<code>GameBootstrap</code>), UI(<code>UIManagerCreater</code>), AudioManager(풀 16), EffectManager(풀 1/32/15초), Main Camera',
     '부팅 1~10단계. 끝나면 Game 씬으로'],
    ['Game (빌드 1번)', 'GameInitializer, Main Camera, Directional Light',
     '<code>GameInitializer</code>가 <code>MvGameLoop</code>를 코드로 붙인다. 월드·FxRoot·스테이지 맵은 실행 중에 만든다'],
], ['17%', '50%', '33%']))
P('<p class="small">에디터에서 Game 씬을 바로 Play하면 GameInitializer가 Boot 씬부터 다시 연다. 데이터를 대체 경로로 만들지 않는다.</p>')
P('</div>')

# ---------- 2 ----------
P('<div class="sec"><h2>2. 모듈 지도와 의존</h2>')
P(fig(D.d_layers(), '그림 2. 어셈블리(asmdef) 층. 위가 아래를 안다'))
yes, no = '<span class="ok">●</span>', '<span class="no">·</span>'
mods = ['Data', 'Run', 'Save', 'Meta', 'Content', 'View', 'Fx', 'Sound', 'UI', 'Platform']
deps = {
    'Boot': mods, 'Editor': ['Data'], 'UI': ['Data', 'Run', 'Save', 'Meta', 'Content', 'View', 'Sound'],
    'Content': ['Data', 'Run', 'Save', 'Meta'], 'Fx': ['Data', 'Run', 'View'], 'View': ['Data', 'Run'],
    'Sound': ['Data', 'Run'], 'Meta': ['Data', 'Save'], 'Run': ['Data'], 'Save': ['Data'], 'Data': [], 'Platform': [],
}
rows = []
for m in ['Boot', 'Editor', 'UI', 'Content', 'Fx', 'View', 'Sound', 'Meta', 'Run', 'Save', 'Data', 'Platform']:
    extra = ' (+Boot)' if m == 'Editor' else ''
    rows.append([f'<b>MV.{m}</b>{extra}'] + [yes if t in deps[m] else no for t in mods])
P('<h3>누가 누구를 아는가 (행 → 열 참조, asmdef references 기준)</h3>')
P(table(['참조하는 쪽 ＼ 참조되는 쪽'] + mods, rows, ['20%'] + ['8%'] * 10))
P('<h3>모듈 한 줄 설명</h3>')
P(table(['모듈', '파일 / 줄', '하는 일', '모르는 것'], [
    ['MV.Boot', '8 / 1.3k', '부팅 10단계, MvGameLoop, 저장→런 입력 변환, 입력 읽기', '(최상위)'],
    ['MV.Data', '26 / 7.0k', '표 72개 로드·색인·검증, 드롭 추첨', '다른 게임 모듈 전부'],
    ['MV.Run', '18 / 3.5k', '런 시뮬: 상태·틱·코스 생성·규칙·이벤트', 'Meta·Save·View·UI'],
    ['MV.Meta', '10 / 1.9k', '메타 명령 → 로컬 백엔드 판정 → 원자 저장', 'Run·Content'],
    ['MV.Content', '3 / 0.6k', '퀘스트·연구. Meta 백엔드에 처리기를 꽂는다', 'View·UI'],
    ['MV.Save', '13 / 2.0k', '저장 컨테이너 20종, 열기·이관·참조 정리·저장', 'Run·Meta'],
    ['MV.View', '41 / 6.3k', '월드·카메라·오브젝트 뷰·캐릭터·투사체·배경·풀', 'Meta·Save·UI'],
    ['MV.Fx', '14 / 2.5k', '런 이벤트 → 04_effect → EffectManager', 'Save·UI'],
    ['MV.Sound', '8 / 0.7k', '런 이벤트·UI 신호 → 03_sound → AudioManager', 'View·UI'],
    ['MV.UI', '56 / 6.3k', '로비 Screen, 런 HUD, 결과·지도·메타·퀘스트·연구 Popup', 'Boot'],
    ['MV.Platform', '12 / 1.0k', '광고·로그인·네트워크 틀(대부분 게임에 미연결)', '게임 모듈 전부'],
    ['MV.Editor', '14 / 2.7k', 'APK 빌드, 아트 입고·아틀라스·Addressables 정리, 표 도구', '(에디터 전용)'],
], ['14%', '11%', '52%', '23%']))
P('<h3>서로 모르는 모듈 사이를 잇는 「번역」 지점</h3>')
P(table(['사이', '누가 옮기나', '무엇을'], [
    ['Save → Run', 'MV.Boot <code>MvRunSnapshot.FromStore</code> → <code>MvRunStart.CreateContext</code>', '저장 값 → RunSetup/RunContext'],
    ['Run → Meta', 'MV.UI <code>ResultViewData.Create</code>', 'RunResult → MvRunOutcome(정산 입력)'],
    ['Meta ↔ Content', 'MV.Content가 <code>RegisterContent / RegisterContributor</code>', '퀘스트·연구 처리기를 Meta 백엔드에 꽂음'],
], ['16%', '50%', '34%']))
P('</div>')

# ---------- 3 ----------
P('<div class="sec"><h2>3. 모듈끼리 찾는 방법 — 정적 슬롯</h2>')
P(fig(D.d_registry(), '그림 3. 누가 채우고 누가 읽나'))
P('<h3>앱 내내 남는 것 (DontDestroyOnLoad)</h3><ul>'
  '<li>UI 부트스트랩 <code>UIManagerCreater</code>와 그 아래 UIManager</li>'
  '<li><code>[MvSaveLifecycle]</code>(일시정지 저장), <code>MvSoundDriver</code>(지연 소리), <code>MvFxRunner</code>(팝업·비행·꼬리)</li>'
  '<li>Framework 싱글턴: AudioManager, EffectManager, CustomSceneManager, AddressablesManager</li></ul>'
  '<p><code>MvGameLoop</code>와 그 아래 월드(WorldView·스테이지 맵)는 Game 씬 수명이다.</p>')
P('</div>')

# ---------- 4 ----------
P('<div class="sec"><h2>4. 부팅 순서</h2>')
P(fig(D.d_boot(), '그림 4. GameBootstrap.RunAsync (S/MV.Boot/GameBootstrap.cs:61-133). 가운데 상자는 그 단계가 일을 맡기는 모듈'))
P('<h3>Game 씬 초기화 — MvGameLoop.InitAsync</h3>')
P(table(['순서', '하는 일', '만드는 것 / 잡는 리소스'], [
    ['a', 'UI 재연결 대기 <code>GameBootstrap.Ui.StartInitAsync</code>', '—'],
    ['b', '입력기 생성', '<code>MvRunInputReader</code> (드래그 폭은 300 표 DRAG_SCREEN_M)'],
    ['c', '월드 생성 <code>InstantiateWithLease("WorldRoot")</code>', 'WorldView (lease는 <code>_leases</code>)'],
    ['d', '지역 테마·스테이지 맵 <code>ApplyThemeAsync</code>', '302_area.stageMap 프리팹, 304_theme 아틀라스'],
    ['e', '이펙트 설치 <code>InstallFxAsync</code>', '머티리얼 MV_FxNormal·MV_FxAdd, MvFx.Player'],
    ['f', '씬 카메라 끄고 월드 카메라 사용 → <code>EnterLobbyAsync</code>', '로비 Screen, 로비 BGM, Phase = Lobby'],
], ['8%', '50%', '42%']))
P('</div>')

# ---------- 5 ----------
P('<div class="sec"><h2>5. 프레임 루프</h2>')
P(fig(D.d_frame(), '그림 5. 한 프레임 안의 순서'))
P('</div>')

# ---------- 6 ----------
P('<div class="sec"><h2>6. 시뮬 한 틱 — RunSim.TickCore</h2>')
P(fig(D.d_tick(), '그림 6. TickCore 순서 (S/MV.Run/Sim/RunSim.Rules.cs:29-52)'))
P('<h3>UpdRun 16단계 (RunSim.Rules.cs:56-150)</h3>')
P(table(['단계', '내용', '끝날 수 있나'], [
    ['1', '시간 진행, 플레이어 x를 목표 쪽으로 따라가기', ''],
    ['2', '골목 아치 통과 → Floor 갱신. 마지막 아치(Gz)를 넘으면', '<b>Finish(Gate)</b> 클리어'],
    ['3', '다음 z 계산. 계단에 닿으면 StartClimb', '이 틱 종료'],
    ['4', '벽 막힘: 앞면에서 이동을 자름', ''],
    ['5', '게이트 통과 → 안이면 지연 수령 등록', ''],
    ['6', '공방 무기 지급 대기 만기 → 무기 지급', ''],
    ['7', '쥐 추적·훔치기(Steal), 뒤로 지나가면 Dead', ''],
    ['8', '게이트 가라앉기(그림용 값)', ''],
    ['9', '막힘 지속 시간 누적. STUCK_END_SEC 넘으면', '<b>Finish(Wall)</b> 실패'],
    ['10', 'Shoot — 본체 + 분신 발사(치명·관통·창·더블샷 추첨)', ''],
    ['11·12', 'MoveProjectiles — 명중하면 Hit(피해·반응·보상 몫·가호)', ''],
    ['(동행)', '동행이 있으면 StepCompanions', ''],
    ['13', '빛·유성', ''],
    ['14', '폭풍·폭발 쿨다운, 화상 틱 피해', ''],
    ['15', '위성·칼날', ''],
    ['16', 'Rule.Tick → Rule.CheckEnd (모드 전용, 본편은 항상 None)', 'ModeClear / ModeFail'],
], ['9%', '68%', '23%']))
P('<h3>피격 반응 (106_hitReaction type별, Hit 안)</h3>')
P(table(['type', '처리'], [
    ['Damage', 'Hp -= dmg. 0 이하면 Break(파괴·남은 보상 몫 지급·드롭 추첨·흔들림), 아니면 누적 피해 비율만큼 보상 몫 선지급'],
    ['ChargeWell', '우물에 피해를 쌓아 둠(Pend) → SyncWells가 묶어서 게이지로'],
    ['ChargeStairGate', '게이트 충전. 문턱마다 계단 수 N += GaugeLv(더블 추첨)'],
    ['ChargeCraftGate', '공방 충전. 문턱마다 무기 제작 수 증가'],
], ['20%', '80%']))
P('</div>')

# ---------- 7 ----------
P('<div class="sec"><h2>7. 런 시작</h2>')
P(fig(D.d_runstart(), '그림 7. MvGameLoop.StartRun (S/MV.Boot/MvGameLoop.cs:336-355)'))
P('<h3>시뮬 상태 RunState에 들어 있는 것</h3>')
P(table(['묶음', '필드', '비고'], [
    ['주입', 'Setup, Tables, Rng, Bal(06_balancePreset)', '바깥은 읽기만'],
    ['상태 기계', 'Phase(Run/Climb/Result), Roulette, Climb, HitStop', ''],
    ['지역·레벨', 'Area, Level, Gz(골 z), Floor, Cp', ''],
    ['오브젝트', 'Objs(생성 순서 = 판정 순서), Projs, NextObjId, NextProjId', 'id는 1부터 계속 증가, 재사용 없음'],
    ['성장', 'GaugeLv, W[](등급별 지팡이 수), Clones, God, Cards', ''],
    ['재화', 'Gold, Souls, Dia, EarnedGold, PendingSettle', '그 밖의 보상은 정산 대기로'],
    ['종료', 'Fin, Reason, Result', 'Finish는 한 번만'],
    ['Events', 'RunEvent 목록', '프레임마다 DrainEvents로 비움'],
], ['14%', '56%', '30%']))
P('</div>')

# ---------- 8 ----------
P('<div class="sec"><h2>8. 런 종료와 정산</h2>')
P(fig(D.d_runend(), '그림 8. 런 끝 → 지도 → 결과 → 정산(저장) → 로비'))
P('<div class="warn">결과 팝업을 Esc(뒤로 가기)로 닫으면 <code>OnClosed</code>가 불리지 않는다. 지도 → 결과 전환이 그 경로로는 일어나지 않는다.</div>')
P('</div>')

# ---------- 9 ----------
P('<div class="sec"><h2>9. 메타 요청 (강화·환생·신 선택·광고·퀘스트·연구)</h2>')
P(fig(D.d_metareq(), '그림 9. 예: 지역 강화 버튼 (S/MV.Meta/MvMeta.cs:67-94)'))
P('<h3>요청 이름 (서버 함수 이름과 같게 둔다)</h3>')
P(table(['요청', '하는 일'], [
    ['meta.settle', '런 정산'], ['meta.buyLobbyUp / buyAreaUp / buyPerk', '로비 강화 / 지역 강화 / 환생 특성'],
    ['meta.rebirth', '환생'], ['meta.selectGod', '신 선택'], ['meta.grantAd / useAd', '광고 보상'],
    ['meta.questRecord / questClaim', '퀘스트 기록 / 보상 받기'], ['meta.startResearch / claimResearch', '연구 시작 / 완료'],
    ['meta.markSeen / setMuted / recordModePlay', '본 표시 / 소리 끄기 / 모드 플레이 기록'], ['debug.* (8종)', '개발 빌드 전용'],
], ['42%', '58%']))
P('<div class="warn"><b>규칙</b>: 백엔드 처리기는 상태를 바꾸지 않고 새 값만 돌려준다. MV.Meta·MV.Save 밖에서 '
  '<code>ApplyAndSave(</code>·<code>.Commit(</code>를 쓰면 시험 <code>MvMetaEntryPointRuleTests</code>가 실패한다.<br>'
  '<b>현재 상태</b>: 프로젝트 설정은 <code>NetworkManager</code> + <code>NetworkRequest</code>를 쓰라고 했지만 '
  '<code>MvMetaRequest</code>는 모양만 같은 별도 클래스다. NetworkManager에 등록된 요청은 점검용 net.ping 하나뿐이다.</div>')
P('</div>')

# ---------- 10 ----------
P('<div class="sec"><h2>10. 저장</h2>')
P(fig(D.d_saveopen(), '그림 10. 저장 열기와 쓰기'))
P('<h3>저장 컨테이너 (S/MV.Save/MvSaveContainers.cs)</h3>')
P(table(['컨테이너', '담는 것'], [
    ['MvProgressContainer', 'level, area, cp, bars{"A{area}"}(벽 남은 HP), runs, zoneClear, bestArea, lastSettledRunSeq'],
    ['MvLobbyContainer / MvAreaUpContainer', '로비 강화 up·deck / 지역 강화 lv'],
    ['MvRebirthContainer', 'lv, pending, perks, cdUntil'],
    ['MvGodContainer', 'sel, gods'],
    ['MvUnlockContainer', 'opened, seenUnlock, seenNew'],
    ['MvDailyContainer', 'day, ads, modePlays, free'],
    ['MvQuestContainer / MvResearchContainer', 'daily·monthly·achieve·attend / 연구 lv·run'],
    ['MvSettingsContainer / MvMetaContainer', 'muted, contentTab / createdAt, lastSaveAt, tableVersion'],
    ['재화', 'Framework WealthContainer 그대로 + ResourceService'],
    ['예약(빈 객체)', 'Pet, Friend, Gear, Pass, Blessing, PlayRes, Mode, Shop'],
], ['34%', '66%']))
P('</div>')

# ---------- 11 ----------
P('<div class="sec"><h2>11. 표 데이터</h2>')
P(fig(D.d_tables(), '그림 11. 시트에서 게임까지'))
P('<h3>표 묶음 (Resources 산출물 72개)</h3>')
P(table(['번대', '내용', '대표 표', '주로 읽는 모듈'], [
    ['0번대', '공통 정의', '00_config, 01_wealth, 03_sound, 04_effect, 05_contentOpen, 07_consumeType, 08_adPlacement', 'Boot, Sound, Fx, Meta, UI'],
    ['1번대', '로비 성장', '10_lobbyUpgrade, 11_stairLevel, 12_weaponLevel, 13_areaUpgrade, 14_rebirthPerk, 16_levelPrice', 'Meta, Run, UI'],
    ['1xx', '런 오브젝트·카드', '102_enemy, 103_item, 105_objectKind, 106~109 컴포넌트, 110_card, 113_god, 116_charLook', 'Run, View, Fx'],
    ['3xx', '런 세계·생성·카메라', '300_runConfig(키-값 183), 302_area, 303_areaFloor, 304_theme, 310~314 템플릿, 320_camera, 330_climb', 'Run, View, Boot'],
    ['4xx', '모드·퀘스트·메타', '400_content, 410~420 퀘스트, 440_research, 499_contentConfig', 'Content, Meta'],
    ['7xx', '상점', '700~702', '(검증만)'],
    ['8xx', '확률', '801_drop, 802_randomBox, 803/804 천장', 'Run(801만)'],
], ['9%', '17%', '52%', '22%']))
P('<h3>표를 다룰 때 지킬 것</h3><ul>'
  '<li>코드에 숫자·개수를 박지 않는다. 표 값과 표 행 수에서 읽는다. 오브젝트 동작은 이름이 아니라 105 표 플래그와 106~109·114 컴포넌트 type으로 정해진다.</li>'
  '<li>열을 더하면 행 클래스 <code>Init</code> + <code>Schema</code> + TSV 세 곳을 함께 고친다(스키마는 파싱에 쓰이지 않는다). 키-값 표(300·499)는 <code>RunConfigKeys</code>·<code>ContentConfigKeys</code> 상수도.</li>'
  '<li>검증기 본문은 하나(<code>MvTableValidator.Validate</code>)를 에디터 빌드와 부팅이 같이 쓴다: 형식 → 참조 → 07↔enum → 300↔RunConfigKeys → 의미 → 드롭 → 이름.</li>'
  '<li>드롭: 오브젝트 파괴 → <code>RunSim.PayShares</code> → <code>Drops.RollDrop</code>(id 순 누적 가중치). 런 골드·소울·다이아 말고는 PendingSettle → 정산 때 Meta로.</li></ul>')
P('</div>')

# ---------- 12 ----------
P('<div class="sec"><h2>12. 리소스 로드</h2>')
P(fig(D.d_loadpaths(), '그림 12. 로드 경로'))
P('<h3>Addressables 폴더 → 그룹 (주소 = 파일 이름, 확장자 제외)</h3>')
P(table(['폴더 Local/…', '그룹', '주소 예', '누가 로드하나 (주소 출처)'], [
    ['Audio/Music·Sfx', 'Local_Audio_Music · Local_Audio_Sfx', 'Bgm_Lobby, Sfx_Coin', 'AudioManager (03_sound.soundName)'],
    ['BG', 'Local_BG', 'Stage1, Obj_Tower, Obj_ClimbStep', '맵: MvGameLoop(302.stageMap) · 오브젝트: ObjectViewFactory(105/305.prefab) · 계단: ClimbStairView(330.stepPrefab)'],
    ['Character', 'Local_Character', 'Char_Cat, atlas_player_0..11', 'CharacterPool · CharacterPresenter (116_charLook)'],
    ['Effect', 'Local_Effect', 'Sparks, Bolt, MV_FxNormal', 'EffectManager (04_effect.prefab) · 머티리얼: MvGameLoop'],
    ['Projectile', 'Local_Projectile', 'projectile_0..11, VFX_Tail', 'ProjectileView (Init 때 전부)'],
    ['UI', 'Local_UI (+Dev_Debug)', 'UIManager, UIScreen_Lobby, UIPopup_Result', 'Framework UI (MvUiNav: 접두어 + ID)'],
    ['UIAtlas', 'Local_UIAtlas', 'Atlas_UI_Hud, Atlas_UI_Card', 'AddressableImage'],
    ['World', 'Local_World', 'WorldRoot, LobbyStage', 'MvGameLoop.InitAsync'],
    ['WorldAtlas', 'Local_WorldAtlas', 'Atlas_World_Theme_*, Atlas_Fx_Common', 'WorldSpriteCache (304_theme.worldAtlas)'],
    ['Texture', 'Local_Texture', 'Tex_Lobby_Room', '항목은 Art/Source/Texture에 있음'],
    ['Font · Mode · Theme', '그룹 없음', '—', '—'],
], ['15%', '22%', '25%', '38%']))
P('<p class="small">그룹 항목·라벨·아틀라스는 손으로 맞추지 않는다. 메뉴 <code>MV/Art/Ensure Import·Atlas·Addressables</code>(<code>MvArtSetup.EnsureAll</code>)가 폴더 규칙대로 맞추고, 아트 폴더가 바뀌면 자동으로 요청된다. 원격은 언어팩만 쓸 예정이다(아직 없음).</p>')
P('</div>')

# ---------- 13 ----------
P('<div class="sec"><h2>13. 에셋 수명</h2>')
P(fig(D.d_lifetime(), '그림 13. 잡는 시점과 놓는 시점'))
P(table(['수명', '에셋', '잡는 곳 → 놓는 곳'], [
    ['앱 전체', 'UIManager, 쓰는 중인 이펙트 프리팹, 재생 중인 소리', 'Framework 매니저 → 풀 컬링·재생 끝'],
    ['Game 씬', 'WorldRoot, 이펙트 머티리얼 2개', '<code>MvGameLoop._leases</code> → <code>MvGameLoop.OnDestroy</code>'],
    ['WorldView', '오브젝트 프리팹(105/305), 캐릭터 프리팹, 투사체 그림, 계단 칸', '각 컴포넌트 → 각 OnDestroy'],
    ['지역', '스테이지 맵(302_area.stageMap)', '<code>_stageLeases</code> → 다음 지역 로드 직전'],
    ['테마', '테마 월드 아틀라스(304_theme.worldAtlas)', '<code>WorldView.SetThemeAsync</code> → 새것을 먼저 로드한 뒤 옛것 반환'],
    ['캐릭터 묶음', '116_charLook.atlas', '<code>CharacterPresenter</code> → 묶음당 최근 3개 넘으면 반환'],
], ['13%', '42%', '45%']))
P('</div>')

# ---------- 14 ----------
P('<div class="sec"><h2>14. 오브젝트 풀과 재사용</h2>')
P(fig(D.d_pool(), '그림 14. ObjectViewFactory (S/MV.View/Runtime/ObjectViewFactory.cs:393-420, 576-632)'))
P(table(['풀', '키', '대여 / 반납', '초기화·상한'], [
    ['ObjectViewFactory', 'objId → View, 프리팹 주소별·모양별 스택', '그리기 범위 안 → Show / 안 보이면 Release', '꺼낼 때·돌려줄 때 ResetMotion. 상한 없음'],
    ['ProjectileView', '투사체 id → Item', 'free Stack', '상한 64(표에 없는 값), 다시 빌린 첫 프레임 ResetTrails'],
    ['CharacterPresenter·Pool', '자리(unit index) + 주소별 ObjectPool', '행의 prefab 주소가 바뀌면 반납 후 새로', '반납 때 rig.Clear()'],
    ['PropStreamer', 'PropKind별 Stack', '그리기 범위 출입', ''],
    ['ClimbStairView', '리스트 앞에서 _used개', '나머지 비활성', ''],
    ['EffectPool (Framework)', '이펙트 타입별 idle Stack', 'Play / 부품 0개면 반납', 'minRetain 1 · maxPool 32 · idle 15초'],
    ['AudioManager (Framework)', '소스 풀', '재생마다', '16개, 확장 가능'],
    ['UI Screen·HUD', 'screenPool · hudPool', '로비·런 HUD 계속 재사용', ''],
    ['UI Popup', '없음', '열 때 생성 / 닫으면 파괴', ''],
    ['메타 패널 행', '없음', 'Render마다 전부 파괴 후 다시 생성', ''],
], ['21%', '27%', '27%', '25%']))
P('<p class="small">빌보드(2.5D 정면 보기)는 프리팹이 빈 부모 하나에 <code>Billboard</code>를 붙여 정하고, 회전은 <code>DepthSorter</code>가 <code>Billboard.SyncAll</code>로 한 번에 적용한다. 캐릭터는 Spine·Animator 없이 아틀라스 그림 교체와 코드 흔들림(<code>CharStateMachine</code>)으로 움직인다.</p>')
P('</div>')

# ---------- 15 ----------
P('<div class="sec"><h2>15. 이펙트와 소리</h2>')
P(fig(D.d_fxsound(), '그림 15. 재생 흐름'))
P('</div>')

P('<div class="sec"><h2>16. 화면(UI) 구조</h2>')
P(fig(D.d_ui(), '그림 16. 컨트롤러 → Presenter → View'))
P(fig(D.d_screenlife(), '그림 17. 화면 종류별 생성·재사용'))
P('</div>')

P('<div class="sec"><h2>16. 화면(UI) 구조 — 화면 → 프리팹 대응</h2>')
P(table(['ID', '주소', '컨트롤러 / View', '여는 곳 (MvUiNav)'], [
    ['Lobby', 'UIScreen_Lobby', 'LobbyScreen / LobbyView', 'OpenLobbyAsync'],
    ['Run', 'UIHud_Run', 'RunHud / RunHudView', 'EnterRunAsync'],
    ['Result', 'UIPopup_Result', 'ResultPopup / ResultView', 'OpenResultAsync'],
    ['Map', 'UIPopup_Map', 'MapPopup / MetaPanelView', 'OpenMapAsync'],
    ['GodSelect', 'UIPopup_GodSelect', 'GodSelectPopup / MetaPanelView', 'OpenGodSelectAsync'],
    ['AreaUpgrade', 'UIPopup_AreaUpgrade', 'AreaUpgradePopup / MetaPanelView', 'OpenAreaUpgradeAsync'],
    ['Rebirth', 'UIPopup_Rebirth', 'RebirthPopup / MetaPanelView', 'OpenRebirthAsync (잠기면 토스트만)'],
    ['FeatureUnlock', 'UIPopup_FeatureUnlock', 'FeatureUnlockPopup / MetaPanelView', 'OpenFeatureUnlockAsync (부르는 곳 없음)'],
    ['ContentMenu', 'UIPopup_ContentMenu', 'ContentMenuPopup / MetaPanelView', 'OpenContentMenuAsync'],
    ['Quest', 'UIPopup_Quest', 'QuestPopup / MetaPanelView', 'OpenQuestAsync'],
    ['Research', 'UIPopup_Research', 'ResearchPopup / MetaPanelView', 'OpenResearchAsync'],
    ['FakeAd', 'UIPopup_FakeAd', 'FakeAdPopup / FakeAdView', 'OpenFakeAdAsync'],
    ['Debug', 'UIPopup_Debug', 'DebugPopup / MetaPanelView', 'OpenDebugAsync (개발 빌드만)'],
], ['15%', '24%', '33%', '28%']))
P('<h3>UI가 게임 상태를 읽는 길 (Port)</h3>')
P(table(['Port', '기본 구현이 보는 것', '쓰는 화면'], [
    ['IMetaUiPort', 'MvMeta.Current', '메타 화면 8종'], ['IQuestUiPort', 'MvQuest.Current', '퀘스트'],
    ['IResearchUiPort', 'MvMeta.Current + MvResearch', '연구'], ['IContentMenuPort', '해금·모드', '컨텐츠 메뉴'],
    ['ILobbySource', 'MvSave.Current', '로비'], ['IRunHudSource', 'RunState (RunStateHudSource)', '런 HUD'],
], ['25%', '45%', '30%']))
P('</div>')

# ---------- 17 ----------
P('<div class="sec"><h2>17. 무엇을 고치려면 어디를 보나</h2>')
P(table(['하고 싶은 것', '시작점'], [
    ['수치 바꾸기', 'GameData/Table/TSV/NNN_*.txt 고치기 → TableBuilder 실행'],
    ['표·열 추가', 'S/MV.Data/Tables/Band*Tables.cs (Init·Schema), MvTables.RegisterAll, MvTableValidator*.cs'],
    ['런 규칙·전투', 'S/MV.Run/Sim/RunSim.Rules.cs (TickCore·UpdRun·Hit), RunFormulas, RunConfigKeys'],
    ['코스(레벨) 생성', 'S/MV.Run/Gen/RunLevelBuilder.cs, LevelGen, FieldGen'],
    ['새 오브젝트 종류', '105_objectKind 행 + 106~109·114 컴포넌트, 그림은 105.prefab → Addressables/Local/BG'],
    ['오브젝트 그림·풀', 'S/MV.View/Runtime/ObjectViewFactory.cs (Sync·Show·Release·ResetMotion)'],
    ['카메라', 'S/MV.View/Runtime/CameraRig.cs, RunStateViewSource, MvGameLoop.LoopViewSource'],
    ['이펙트 추가', '04_effect 행 + Local/Effect 프리팹, 이벤트 → 그룹은 MvFxRouter'],
    ['소리 추가', '03_sound 행 + Local/Audio/Sfx, 이벤트 → 소리는 MvRunSoundRouter, UI 소리는 MvUiSounds'],
    ['새 메타 화면', 'MetaPanelPopup·MetaPanelPresenter 하위 + UIPopup_&lt;Id&gt; 프리팹 + MvUiIds·MvUiNav'],
    ['새 메타 명령', 'MvMetaRequestNames + MvLocalMetaBackend 처리기 + MvMeta 명령 메서드'],
    ['저장 항목 추가', 'S/MV.Save/MvSaveContainers.cs, MvSaveKeys, MvSaveDefaults, MvSaveNormalize'],
    ['부팅 단계', 'S/MV.Boot/GameBootstrap.cs RunAsync'],
    ['빌드', 'S/MV.Editor/Build/MvBuild.cs BuildAndroidDev (표 빌드 → Addressables → APK)'],
], ['22%', '78%']))
P('</div>')

# ---------- 18 ----------
P('<div class="sec"><h2>18. 아직 비어 있거나 연결되지 않은 곳</h2>')
P(table(['영역', '현재 상태', '영향'], [
    ['다국어', '부팅 7단계가 비어 있고 Localize가 키를 그대로 돌려준다. 언어팩 데이터·로더 없음', '코드 안 한국어 기본 문구가 그대로 나온다'],
    ['서버 요청 경로', 'MvMetaRequest가 NetworkRequest를 쓰지 않음. NetworkManager에는 net.ping만', '서버 전환 때 바꿀 곳'],
    ['광고·로그인', 'MvAds·MvLogin 틀만(D-90). 광고는 MvFakeAdService + 가짜 광고 팝업, 로그인은 로컬 게스트', ''],
    ['토스트', 'MvUiToast.Handler 미설정', '로그로만 나간다'],
    ['모드', 'ContentMenuData.StartModeHandler 미설정, 401/402 표는 검증만', '모드 카드는 「준비 중」'],
    ['해금 연출', 'OpenFeatureUnlockAsync를 부르는 곳 없음', '정산 결과의 NewlyOpened를 쓰지 않음'],
    ['뒤로 가기', 'Esc로 팝업을 닫으면 OnClosed가 안 불림', '지도 → 결과 전환이 안 일어남'],
    ['배지', 'com.jjevol.badge 미사용, 퀘스트 ● 문자열뿐', ''],
    ['Fx 그림', '팝업·비행·플래시 View 구현 없음, InstallDefault 머티리얼 인자 미사용', '화면에 안 나옴'],
    ['저장 열기 실패 안내', 'MvBootSave.PendingNotice를 UI가 읽지 않음', ''],
    ['표만 있는 것', '천장 803/804, 상자 열기, 상점 7xx, 메타 표 다수', '검증만 한다'],
    ['메모리', 'ObjectViewFactory._prefabs가 이전 맵 전용 프리팹을 맵이 바뀌어도 놓지 않음', 'WorldView 파괴 때까지 남음'],
    ['안 쓰는 코드', 'LobbyStage, MvWarpOverlay, MvSaveTriggers', ''],
], ['15%', '55%', '30%']))
P('</div>')

html_doc = ('<!doctype html><html lang="ko"><head><meta charset="utf-8"><title>MoonVillage 코드 구조</title>'
            f'<style>{CSS}</style></head><body>' + ''.join(parts) + '</body></html>')
with open(OUT, 'w', encoding='utf-8') as f:
    f.write(html_doc)
print('ok', len(html_doc))
