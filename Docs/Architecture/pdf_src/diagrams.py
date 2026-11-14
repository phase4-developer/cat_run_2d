# -*- coding: utf-8 -*-
from svglib import Svg, sequence, C, tw


def d_cycle():
    s = Svg(760, 250)
    names = [
        ('Boot 씬', 'GameBootstrap\n부팅 1~10단계', 'boot'),
        ('Game 씬 준비', 'GameInitializer\nMvGameLoop.InitAsync', 'boot'),
        ('로비', 'Phase = Lobby\n다음 런 트랙 미리보기', 'ui'),
        ('런', 'Phase = Run\n매 프레임 TickRun', 'run'),
        ('지도 팝업', 'FinishRun 뒤\n먼저 연다', 'ui'),
        ('결과 팝업', 'Phase = Result\n「받기」 대기', 'ui'),
    ]
    w, g, x0, y = 108, 22, 10, 40
    xs = []
    for i, (t, sub, k) in enumerate(names):
        x = x0 + i * (w + g)
        xs.append(x)
        s.box(x, y, w, 70, t, sub, kind=k, fs=12.5, sfs=9.5)
        s.badge(x + 10, y + 10, i + 1)
        if i:
            s.arrow(x - g, y + 35, x - 1, y + 35)
    s.text(380, 18, '앱 한 바퀴: Boot 씬은 한 번, Game 씬 안에서 로비 → 런 → 지도 → 결과가 계속 돈다', 12, weight='bold')
    # loop back
    xr = xs[5] + w / 2
    xl = xs[2] + w / 2
    s.line([(xr, y + 70), (xr, y + 120), (xl, y + 120), (xl, y + 72)], color='#3E8E41', marker='ahg', sw=1.8)
    s.arrow(xr, y + 120, xl, y + 120, '「받기」 → MvMeta.Settle (런 결과 저장은 여기서 딱 1번) → EnterLobbyAsync',
            fs=10, color='#3E8E41', marker='ahg', lcolor='#2c6b2f')
    # notes
    s.rect(10, 185, 740, 56, 'note', r=4)
    s.text(380, 213,
           '• Boot 씬 오브젝트는 Game 씬으로 넘어가며 사라지고, UI·소리·저장 안전망·Framework 매니저만 남는다(DontDestroyOnLoad).\n'
           '• 결과 화면 뒤에서도 TickRun은 입력 없이 계속 돌아 월드 연출이 이어진다.  • 같은 저장·같은 런 순번이면 같은 런이 나온다(시드 고정).',
           10, anchor='middle')
    return s.render()


def d_layers():
    s = Svg(760, 430)
    bands = [
        (10, '조립', '#EEF1F8'),
        (90, '화면·표현', '#FBF4EA'),
        (170, '게임 로직', '#F1EEF8'),
        (250, '기반', '#EEF6EE'),
        (330, 'Framework\n패키지', '#F5F5F5'),
    ]
    for y, lab, col in bands:
        s.rect(10, y, 740, 72, 'plain', r=8, fill=col, stroke='#DDD')
        s.text(52, y + 36, lab, 12, weight='bold', color='#444')
    X = [105, 270, 435, 600]
    W = 145
    s.box(X[0], 20, W, 52, 'MV.Boot', '부팅 · 게임 루프 · 모두를 조립', 'boot')
    s.box(X[1], 20, W, 52, 'MV.Editor', '빌드 · 아트 입고 · 표 도구', 'boot', dash=True)
    s.text(X[2] + 150, 46, 'Boot만 모든 모듈을 안다.\n아무 런타임 모듈도 Boot를 모른다.', 10.5, color='#3B5BA9')
    s.box(X[0], 100, W, 52, 'MV.View', '월드·카메라·오브젝트 뷰·풀', 'view')
    s.box(X[1], 100, W, 52, 'MV.Fx', '이펙트 (04_effect)', 'fx')
    s.box(X[2], 100, W, 52, 'MV.Sound', '소리 (03_sound)', 'sound')
    s.box(X[3], 100, W, 52, 'MV.UI', '로비·HUD·결과·메타 화면', 'ui')
    s.box(X[0], 180, W, 52, 'MV.Run', '런 시뮬 (순수 C#)', 'run')
    s.box(X[1], 180, W, 52, 'MV.Content', '퀘스트 · 연구', 'meta')
    s.box(X[2], 180, W, 52, 'MV.Meta', '영속 변경의 유일한 입구', 'meta')
    s.box(X[3], 180, W, 52, 'MV.Save', '저장 컨테이너 20종', 'save')
    s.box(X[0], 260, W, 52, 'MV.Data', '표 72개 (MvTables)', 'data')
    s.box(X[1], 260, W, 52, 'MV.Platform', '광고·로그인·네트워크 틀', 'plat')
    s.box(X[2], 260, W, 52, 'MV.Core', '빈 asmdef', 'plat', dash=True)
    fw = ['AddressablesManager\nAddressableScope', 'UIManager\n(Screen/Popup/HUD)', 'AudioManager\nEffectManager',
          'CustomSceneManager\nTableRepository', 'DataRepository·Save\nResourceService']
    fwW = 126
    for i, t in enumerate(fw):
        s.box(105 + i * (fwW + 5), 340, fwW, 52, t, None, 'fw', fs=9.5, bold=False)
    # arrows in logic layer
    s.arrow(X[1], 206, X[0] + W + 2, 206, '안다', fs=9)
    s.arrow(X[1] + W, 206, X[2] - 2, 206, '안다', fs=9)
    s.arrow(X[2] + W, 206, X[3] - 2, 206, '안다', fs=9)
    # Run <-> Meta wall
    s.line([(X[0] + W / 2, 233), (X[0] + W / 2, 238), (X[2] + W / 2, 238), (X[2] + W / 2, 233)], arrow=False,
           color='#C0392B', dash=True, sw=1.6)
    s.rect(600, 262, 140, 48, 'warn', r=4)
    s.text(670, 286, 'Run ↔ Meta 서로 모름\n(빨간 점선) Boot·UI가\n값을 옮겨 준다', 9, color='#C0392B', weight='bold')
    # view -> run
    s.arrow(X[0] + W / 2, 152, X[0] + W / 2, 178, None)
    s.arrow(X[1], 126, X[0] + W + 2, 126, '안다', fs=9)
    s.text(380, 412, '위층은 아래층을 안다(참조). 아래층은 위층을 모른다. 정확한 참조 관계는 다음 쪽 의존 표를 본다.', 10.5,
           color='#555')
    return s.render()


def d_boot():
    W = 770
    lanes = [('GameBootstrap.RunAsync\n(Boot 씬, Start에서 시작)', 'boot'),
             ('Framework', 'fw'), ('MV.Data', 'data'), ('MV.Save·Meta', 'meta'),
             ('MV.UI·Sound·Net', 'ui'), ('씬 전환', 'boot')]
    lx = [10, 270, 370, 470, 570, 670]
    lw = [250, 96, 96, 96, 96, 96]
    steps = [
        ('1', 'Addressables.InitializeAsync', 1, 'Addressables\n초기화', False),
        ('2', 'AddressablesManager.InitAsync', 1, '로드·참조 수\n관리자 준비', False),
        ('3', 'Audio·Effect 공급자 지정', 1, '첫 재생 전에\n지정해야 함', False),
        ('4', 'MvBootMeta.LoadTablesAsync', 2, '표 72개 로드\n검증 실패=중단', False),
        ('5', 'MvBootSave.Open', 3, '저장 열기\n실패해도 진행', False),
        ('6', 'MvSaveLifecycle.Ensure', 3, '일시정지 저장\n안전망(DDOL)', False),
        ('6+', 'MvBootMeta.Attach', 3, 'MvMeta·MvQuest\n생성', False),
        ('7', '다국어 (비어 있음)', 4, '키 그대로\n표시', True),
        ('8', 'MvUiBoot.InitAsync', 4, 'UIManager\n프리팹 생성', False),
        ('9', 'MvSound 설치 · NetworkManager', 4, '소리 설치\nnet.ping만 등록', False),
        ('10', 'CustomSceneManager.LoadSceneAsync', 5, 'Game 씬 →\nMvGameLoop', False),
    ]
    top = 60
    rh = 44
    H = top + len(steps) * rh + 70
    s = Svg(W, H)
    for i, (t, k) in enumerate(lanes):
        s.rect(lx[i], 8, lw[i], H - 16, 'plain', r=6, fill='#FAFAFA' if i % 2 else '#F3F5FA', stroke='#E2E2E2')
        s.text(lx[i] + lw[i] / 2, 30, t, 10.5 if i else 11, weight='bold', color=C[k][1] if k != 'fw' else '#555')
    for r, (n, call, lane, res, gray) in enumerate(steps):
        y = top + r * rh + rh / 2
        s.badge(28, y, n, r=11, fs=9.5)
        s.text(46, y, call, 10.5, anchor='start')
        kind = ['boot', 'fw', 'data', 'meta', 'ui', 'boot'][lane]
        if gray:
            kind = 'plat'
        bx = lx[lane] + 4
        s.box(bx, y - 18, lw[lane] - 8, 36, res, None, kind, fs=9.2, bold=False, dash=gray)
        s.line([(250, y), (bx - 2, y)], color='#888', sw=1.1)
    y = top + len(steps) * rh + 10
    s.rect(10, y, 750, 48, 'note', r=4)
    s.text(385, y + 24,
           '모든 단계는 순서대로 await. 어느 단계든 예외면 State = Failed로 멈추고 재시도하지 않는다.\n'
           '10단계는 Game 씬의 GameInitializer → MvGameLoop.InitAsync → 로비 열림까지 기다리므로, State = Succeeded는 로비가 뜬 뒤에 된다.',
           10)
    return s.render()


def d_registry():
    rows = [
        ('MvTableLoader.LoadAsync\n(부팅 4단계)', 'MvTables.Current', '모든 모듈\n(Fx·Sound는 공급 함수로 매번)', 'data'),
        ('MvSave.Init / Retry / Reopen', 'MvSave.Current\n+ CurrentChanged 이벤트', 'BootSave · BootMeta\n로비 · MvGameLoop', 'save'),
        ('MvBootSave.BindResources\n(저장이 열릴 때마다)', 'MvBootSave.Resources\n(재화 ResourceService)', 'MvMeta (공급 함수)', 'save'),
        ('MvBootMeta.OnSaveChanged\n(처음 1번)', 'MvMeta.Current', 'MvGameLoop · UI Port\n결과 정산기', 'meta'),
        ('MvQuest.Install', 'MvQuest.Current', '정산기(RecordRun)\n퀘스트 화면', 'meta'),
        ('MvSound.InstallDefault (9단계)', 'MvSound.Player', 'MvGameLoop · UI', 'sound'),
        ('MvGameLoop.InstallFxAsync', 'MvFx.Player', 'MvGameLoop.TickRun', 'fx'),
        ('GameBootstrap.RunAsync', 'GameBootstrap\n.Ui / State / ServicesReady', 'GameInitializer\nMvGameLoop', 'boot'),
    ]
    rh = 46
    H = 40 + len(rows) * rh + 50
    s = Svg(760, H)
    s.text(120, 18, '채우는 곳', 11.5, weight='bold')
    s.text(380, 18, '정적 슬롯', 11.5, weight='bold')
    s.text(640, 18, '읽는 곳', 11.5, weight='bold')
    for i, (w, slot, rd, k) in enumerate(rows):
        y = 34 + i * rh
        s.box(10, y, 220, rh - 8, w, None, 'plain', fs=9.5, bold=False)
        s.box(270, y, 220, rh - 8, slot, None, k, fs=10)
        s.box(530, y, 220, rh - 8, rd, None, 'plain', fs=9.5, bold=False)
        s.arrow(230, y + (rh - 8) / 2, 268, y + (rh - 8) / 2)
        s.arrow(490, y + (rh - 8) / 2, 528, y + (rh - 8) / 2)
    y = 34 + len(rows) * rh + 4
    s.rect(10, y, 740, 40, 'note', r=4)
    s.text(380, y + 20, 'DI 컨테이너 없이 정적 슬롯으로 서로 찾는다. Play 시작마다 [RuntimeInitializeOnLoadMethod(SubsystemRegistration)]\n'
                        'ResetStatics()로 모두 비운다. 모듈 사이 C# 이벤트는 사실상 MvSave.CurrentChanged 하나뿐이다.', 10)
    return s.render()


def d_frame():
    s = Svg(760, 430)
    s.text(380, 14, 'Update — MvGameLoop.TickRun  (S/MV.Boot/MvGameLoop.cs:371-403)', 12, weight='bold')
    s.box(10, 40, 120, 60, '① 입력', 'MvRunInputReader.Read\n결과 단계면 None', 'boot', sfs=9)
    s.box(160, 40, 130, 60, '② 시뮬', 'RunSim.Tick(dt, input)\n× TicksPerFrame', 'run', sfs=9)
    s.box(320, 40, 130, 60, '③ 이벤트 비우기', 'RunSim.DrainEvents()\n프레임당 딱 1번', 'run', sfs=9)
    s.arrow(130, 70, 158, 70)
    s.arrow(290, 70, 318, 70)
    outs = [('MvSound.OnRunEvents', '→ 03_sound → AudioManager', 'sound'),
            ('MvFx.OnRunEvents', '→ 04_effect → EffectManager', 'fx'),
            ('WorldView.OnRunEvents', '피격 흔들림 · 우물 벽돌', 'view'),
            ('ClimbStart 이벤트면', 'WorldView.RebuildProps', 'view')]
    for i, (t, sub, k) in enumerate(outs):
        y = 30 + i * 46
        s.box(490, y, 200, 40, t, sub, k, fs=10.5, sfs=9)
        s.line([(450, 70), (470, 70), (470, y + 20), (488, y + 20)])
        s.badge(482 + 0, y + 20, '', r=0)
    s.text(385, 114, '같은 목록을 위에서부터 차례로 ↗', 9, color='#777')
    s.box(160, 150, 290, 50, '④ 상태 → 뷰 맞추기',
          'WorldView.SyncObjects(state.Objs) · SyncCats(state)', 'view', sfs=9.5)
    s.line([(590, 214), (590, 224), (305, 224), (305, 202)])
    s.box(10, 150, 120, 50, '⑤ 끝났나?', 'Phase == Result\n→ FinishRun()', 'ui', sfs=9)
    s.arrow(160, 175, 132, 175)
    s.text(380, 245, '위치·생사는 이벤트로 보내지 않는다. 매 프레임 state.Objs 전체를 뷰와 비교해 맞춘다(②~④는 단방향, 뷰는 상태를 바꾸지 않는다).',
           9.8, color='#444')
    # LateUpdate bar
    s.text(380, 280, 'LateUpdate 실행 순서 (DefaultExecutionOrder) — 같은 프레임 Update 뒤', 12, weight='bold')
    items = [(-100, 'CameraRig', '카메라 위치·흔들림'), (50, 'GroundStripMesh', '지면 띠'),
             (60, 'PropStreamer\nStageMap', '소품·배경 대여/반납'), (100, 'DepthSorter', '빌보드 회전 일괄\n깊이 정렬'),
             (110, 'TintLayer', '화면 곱하기'), (120, 'ScreenRig', '화면 앞 눈')]
    x0, x1 = 50, 740
    s.line([(x0, 320), (x1, 320)], color='#999')
    for i, (o, n, d) in enumerate(items):
        x = x0 + 18 + i * 124
        s.add(f'<circle cx="{x}" cy="320" r="5" fill="#D9822B"/>')
        s.text(x, 305, str(o), 10, weight='bold', color='#D9822B')
        s.box(x - 58, 332, 116, 56, n, d, 'view', fs=10, sfs=8.8)
    s.rect(10, 398, 740, 28, 'note', r=4)
    s.text(380, 412, 'HUD(RunHud)는 이벤트를 받지 않고 LateUpdate에서 RunState를 직접 읽는다. 이펙트·지연 소리 시간은 숨은 MvFxRunner·MvSoundDriver의 Update가 진행한다.', 9.4)
    return s.render()


def d_tick():
    s = Svg(760, 330)
    steps = [('Frame++', '로비면 여기서 끝'), ('dt 자르기', 'min(Time_DtCap, realDt)\n드래그 → P.Tx'),
             ('StepRealTimers', '실시간 수령·카드 비행'), ('히트스톱', 'gdt = dt × HitStopScale')]
    for i, (t, d) in enumerate(steps):
        x = 10 + i * 150
        s.box(x, 20, 135, 52, t, d, 'run', fs=11, sfs=9)
        s.badge(x + 10, 30, i + 1, r=8, fs=9)
        if i:
            s.arrow(x - 15, 46, x - 1, 46)
    s.arrow(595, 46, 620, 46)
    s.diamond(680, 46, 110, 56, 'Phase?', 'note')
    s.box(560, 110, 190, 62, 'UpdRun(gdt) × ts', '전진·통과·막힘·사격·명중…\n16단계 (아래 표)', 'run', sfs=9)
    s.box(330, 110, 190, 62, 'UpdClimb(sdt)', '계단 오르기\n칸·lap 카드·등급·워프', 'run', sfs=9)
    s.line([(680, 74), (680, 92), (655, 92), (655, 108)])
    s.line([(680, 92), (425, 92), (425, 108)])
    s.text(662, 101, "Run", 9.5, color="#555", anchor="start")
    s.text(432, 101, 'Climb', 9.5, color='#555', anchor='start')
    s.box(10, 110, 290, 62, '룰렛 (있으면 단계 무관)', 'UpdRoulette — 결과는 시작 때 이미 정해짐\n표시 타이밍만 진행', 'run', sfs=9)
    steps2 = [('KeyAxis', '키보드 이동'), ('StepFx(sdt)', '폭풍·유성 착탄\n게임 시계 수령'),
              ('SyncWells(dt)', '우물 피해 → 게이지')]
    for i, (t, d) in enumerate(steps2):
        x = 160 + i * 200
        s.box(x, 210, 170, 52, t, d, 'run', fs=11, sfs=9)
        s.badge(x + 10, 220, 6 + i, r=8, fs=9)
        if i:
            s.arrow(x - 30, 236, x - 1, 236)
    s.line([(655, 172), (655, 190), (245, 190), (245, 208)])
    s.line([(425, 172), (425, 190)], arrow=False)
    s.line([(155, 172), (155, 190), (245, 190)], arrow=False)
    s.badge(10 + 10, 120, 5, r=8, fs=9)
    s.rect(10, 278, 740, 44, 'note', r=4)
    s.text(380, 300, '규칙 난수는 RunState.Draw(RngUse)로만 뽑고 확률이 0이면 뽑지 않는다. 피해는 floor(dmg)를 [DMG_MIN, DMG_MAX]로 자른 정수.\n'
                     '수치는 RunFormulas·RunParams가 300_runConfig 등 표에서 읽는다. (S/MV.Run/Sim/RunSim.Rules.cs:29-150)', 9.6)
    return s.render()


def d_runstart():
    s = Svg(760, 330)
    s.text(380, 14, '저장 값이 런이 되기까지 (Run은 Save를 모르므로 Boot가 옮긴다)', 12, weight='bold')
    s.box(10, 40, 130, 62, 'MvSaveStore', 'Progress·Wealth·God\nLobby·AreaUp·Rebirth', 'save', sfs=9)
    s.box(170, 40, 130, 62, 'MvRunSnapshot', 'FromStore()\n값만 담은 스냅숏', 'boot', sfs=9)
    s.box(330, 40, 130, 62, 'RunSetup', 'MvRunStart\n.CreateContext()', 'boot', sfs=9)
    s.box(490, 40, 130, 62, 'RunContext', 'Tables + Snapshot\n+ MetaModifiers', 'run', sfs=9)
    s.box(650, 40, 100, 62, 'RunSim', '.Create()', 'run', sfs=9)
    for a, b in [(140, 168), (300, 328), (460, 488), (620, 648)]:
        s.arrow(a, 71, b, 71)
    s.box(170, 130, 130, 48, '런 순번 → 시드', 'MvMeta.BeginRun()\nSeedFor = SplitMix64', 'meta', sfs=8.8)
    s.arrow(235, 130, 235, 104)
    s.box(330, 130, 130, 48, '성장치 반영', '13·14 표 statKey로\nLv → RunSetup 칸', 'data', sfs=8.8)
    s.arrow(395, 130, 395, 104)
    s.box(490, 130, 130, 48, '연구 보정', 'MvResearch\n.Modifiers(store)', 'meta', sfs=8.8)
    s.arrow(555, 130, 555, 104)
    s.box(630, 130, 120, 48, 'Pcg32Rng(seed)\nMainRunRule', None, 'run', fs=9.5)
    s.arrow(690, 130, 690, 104)
    s.box(530, 210, 220, 52, 'RunLevelBuilder.BuildLevel', '이번 런 오브젝트를 전부 미리 만든다\n(런 중 동적 생성 없음)', 'run', fs=10.5, sfs=9)
    s.line([(750, 71), (756, 71), (756, 196), (640, 196), (640, 208)])
    s.box(10, 210, 240, 52, 'LoopViewSource.Run 교체', '카메라가 로비 자세에서 런 자세로 이어서 이동', 'view', fs=10.5, sfs=9)
    s.box(270, 210, 240, 52, 'MvUiNav.EnterRunAsync', '로비 Screen 숨김 · 런 HUD(풀) 켜고 Bind', 'ui', fs=10.5, sfs=9)
    s.rect(10, 278, 740, 44, 'note', r=4)
    s.text(380, 300, '같은 저장·같은 런 순번 = 같은 시드 = 같은 런. 로비 뒤 트랙도 「다음 순번」 sim을 Tick 없이 만들어 그린 것이다.\n'
                     '펫·장비·동행 보정은 아직 RunModifiers.Zero, 메타 보정은 연구만 들어간다.', 9.6)
    return s.render()


def d_runend():
    actors = [('loop', 'MvGameLoop', 'MV.Boot', 'boot'), ('nav', 'MvUiNav·팝업', 'MV.UI', 'ui'),
              ('set', 'CountingSettler', 'MvGameLoop 안', 'boot'), ('meta', 'MvMeta', 'MV.Meta', 'meta'),
              ('be', 'MvLocalMetaBackend', '판정만', 'meta'), ('q', 'MvQuest', 'MV.Content', 'meta'),
              ('st', 'MvSaveStore', 'MV.Save', 'save')]
    msgs = [
        ('loop', 'loop', 'TickRun: state.Phase == Result → FinishRun()', 'self'),
        ('loop', 'loop', 'ResultViewData.Create: RunResult → MvRunOutcome', 'self'),
        ('loop', 'nav', 'OpenMapAsync (OnClosed = OpenResult)'),
        ('nav', 'nav', '플레이어가 지도를 닫음', 'self'),
        ('nav', 'loop', 'OnClosed', 'ret'),
        ('loop', 'nav', 'OpenResultAsync(data) · HUD 숨김'),
        ('nav', 'set', '「받기」 → Settler.Settle(outcome, mul)'),
        ('set', 'meta', 'MvMetaResultSettler → Settle'),
        ('meta', 'be', 'Send(meta.settle)'),
        ('be', 'be', '저장값으로 다시 판정\n(클라이언트 값 안 믿음)', 'self'),
        ('be', 'q', 'contributor: 퀘스트 카운터 보탬'),
        ('be', 'meta', 'Response.Data', 'ret'),
        ('meta', 'st', 'ApplyAndSave(Data, keys) — 1번'),
        ('meta', 'set', 'MvSettleResult', 'ret'),
        ('set', 'q', 'RecordRun(계단·벽) — 별도 저장'),
        ('nav', 'loop', 'OnCollected', 'ret'),
        ('loop', 'loop', 'EnterLobbyAsync → 다음 순번 로비', 'self'),
    ]
    return sequence(actors, msgs, width=770, gap=29, fs=9.8).render()


def d_metareq():
    actors = [('btn', '버튼', '컨트롤러', 'ui'), ('pr', 'Presenter', '순수 C#', 'ui'), ('port', 'Port', 'IMetaUiPort 등', 'ui'),
              ('meta', 'MvMeta', '명령 + Send', 'meta'), ('be', 'MvLocalMetaBackend', '서버 자리', 'meta'),
              ('q', 'Contributor', '퀘스트', 'meta'), ('st', 'MvSaveStore', '저장', 'save')]
    msgs = [
        ('btn', 'pr', '클릭'),
        ('pr', 'port', 'BuyAreaUp(key)'),
        ('port', 'meta', 'MvMeta.BuyAreaUp(key)'),
        ('meta', 'meta', 'new MvMetaRequest("meta.buyAreaUp", {key})', 'self'),
        ('meta', 'be', 'Handle(request)'),
        ('be', 'be', '표 확인 · 가격 계산 · 재화 부족?\n상태는 바꾸지 않고 새 값만 계산', 'self'),
        ('be', 'q', '같은 응답에 mvQuest 값 보탬'),
        ('be', 'meta', 'MvMetaResponse (Ok / 거절 코드)', 'ret'),
        ('meta', 'st', 'Ok면 ApplyAndSave(Data, keys) — 원자 저장', ''),
        ('st', 'meta', '실패면 SaveFailed → 화면은 Reopen', 'ret'),
        ('meta', 'meta', 'Applied 이벤트(시험만 구독)', 'self'),
        ('meta', 'pr', '결과 객체', 'ret'),
        ('pr', 'btn', 'Draw() → View.Render(model)', 'ret'),
    ]
    return sequence(actors, msgs, width=770, gap=30, fs=9.8).render()


def d_saveopen():
    s = Svg(760, 420)
    s.text(380, 14, 'MvSaveStore.Open — 부팅 5단계 (S/MV.Save/MvSaveStore.cs:210-316)', 12, weight='bold')
    nodes = [
        (40, '키 공급 → 암호화 코덱', '실패: ProtectionFailed'),
        (100, 'LocalSaveRepository.Load', 'PlayerPrefs 두 칸'),
        (160, None, None),
        (220, '스키마 이관', 'v0(웹 프로토) → v1, 바로 저장'),
        (280, '컨테이너 20종 등록·채움', '없으면 MvSaveDefaults'),
        (340, '참조 정리 MvSaveReferenceFix', '없는 신·덱 길이·상한 → 바뀌면 저장'),
    ]
    for y, t, d in nodes:
        if t:
            s.box(230, y, 300, 46, t, d, 'save', fs=11, sfs=9)
    s.diamond(380, 183, 170, 40, '한 칸 손상?', 'note', fs=10)
    for a, b in [(86, 98), (146, 162), (204, 218), (266, 278), (326, 338)]:
        s.arrow(380, a, 380, b)
    s.box(570, 163, 180, 40, '백업 칸으로 복구', 'RecoveredBackup', 'warn', fs=10, sfs=9)
    s.arrow(465, 183, 568, 183, '예', fs=9)
    s.line([(660, 205), (660, 243), (532, 243)])
    s.text(392, 210, '아니오', 9, anchor='start', color='#555')
    s.text(668, 222, '다시 읽고 계속', 9, anchor='start', color='#555')
    s.box(10, 100, 190, 60, '열기 실패해도 부팅 계속', 'MvMeta.Current = null로 로비까지\n팝업의 다시 시도 → Retry', 'warn', fs=10, sfs=8.8)
    s.arrow(228, 63, 200, 112, None, dash=True, color='#C0392B', marker='ahr')
    s.box(570, 340, 180, 46, 'CurrentChanged(store)', 'ResourceService · MvMeta 생성', 'meta', fs=10, sfs=8.8)
    s.arrow(530, 363, 568, 363)
    s.box(10, 220, 190, 110, '쓰기 (사건마다)', 'MvMeta.Send →\nApplyAndSave(Data, keys)\n하나라도 실패하면\n아무것도 안 바뀜\n일시정지·종료 →\nSaveIfDirty', 'meta', fs=10.5, sfs=8.8)
    return s.render()


def d_tables():
    s = Svg(760, 300)
    s.rect(10, 26, 360, 260, 'plain', r=8, fill='#F6F8FB', stroke='#DDD')
    s.rect(390, 26, 360, 260, 'plain', r=8, fill='#F4FAF4', stroke='#DDD')
    s.text(190, 14, '에디터 (사람이 고치는 곳)', 11.5, weight='bold', color='#3B5BA9')
    s.text(570, 14, '실행 중 (게임이 읽는 곳)', 11.5, weight='bold', color='#3E8E41')
    s.box(30, 40, 150, 46, '구글 시트', '기획·수치 원본', 'plain', fs=11, sfs=9)
    s.box(200, 40, 150, 46, 'TSV (정본)', 'GameData/Table/TSV\nNNN_name.txt', 'data', fs=11, sfs=8.8)
    s.arrow(180, 63, 198, 63)
    s.text(189, 96, 'MvSheetTsvExporter', 8.8, color='#666')
    s.box(30, 120, 320, 56, 'TableBuilder (Framework)', '+ MvTableAuthoring.ValidateBuildInputs\n= 게임 검증기 + 리소스 이름 검사 → 오류면 반영 거부', 'boot', fs=11, sfs=8.8)
    s.arrow(275, 86, 275, 118)
    s.box(30, 200, 320, 56, 'Resources 산출물', 'GameData/Table/Resources/NNN_name.txt\n평문 · 암호화 끔', 'data', fs=11, sfs=8.8)
    s.arrow(190, 176, 190, 198)
    s.box(410, 40, 320, 46, '부팅 4단계 MvTableLoader.LoadAsync', 'PackagedTableSource → Resources.Load', 'data', fs=10.5, sfs=8.8)
    s.line([(350, 228), (380, 228), (380, 63), (408, 63)])
    s.box(410, 100, 320, 46, '색인 만들기 (로드 때 1번)', 'id · key · group · 정렬 사전', 'data', fs=10.5, sfs=8.8)
    s.box(410, 160, 320, 46, 'ValidateRuntime → BuildIndexes', '오류면 MvTableValidationException = 부팅 중단', 'data', fs=10.5, sfs=8.8)
    s.box(410, 220, 320, 50, 'MvTables.Current', '앱 내내 1개 · 행은 private set(읽기 전용)', 'data', fs=11, sfs=8.8)
    for a, b in [(86, 98), (146, 158), (206, 218)]:
        s.arrow(570, a, 570, b)
    s.text(195, 275, 'TSV만 고치고 빌드를 안 돌리면 옛 값이 나온다 (TSV 대체 경로 없음)', 9, color='#C0392B')
    return s.render()


def d_loadpaths():
    s = Svg(760, 300)
    s.text(380, 14, 'Addressables를 읽는 길은 두 갈래', 12, weight='bold')
    s.text(14, 30, '① 게임 코드 경로', 10, anchor='start', weight='bold', color='#3B5BA9')
    s.text(14, 212, '② 이펙트·소리 경로', 10, anchor='start', weight='bold', color='#555')
    s.box(10, 40, 230, 92, '게임 코드', 'MvGameLoop · WorldView\nObjectViewFactory · CharacterPool\nProjectileView · ClimbStairView\nWorldSpriteCache', 'boot', fs=11.5, sfs=9)
    s.box(270, 40, 220, 92, 'AddressableScope', 'Get<T>(key)\nGetPrefab(key)\nInstantiateWithLease(key, parent)', 'fw', fs=11.5, sfs=9.2)
    s.box(520, 40, 230, 92, 'AddressablesManager', '키마다 핸들 1개 + 참조 수\n같은 키 동시 로드는 1번만\n참조 0 → Addressables.Release', 'fw', fs=11.5, sfs=9)
    s.arrow(240, 86, 268, 86)
    s.arrow(490, 86, 518, 86)
    s.box(270, 150, 220, 52, 'AddressableLease<T>', 'Dispose() = 참조 1 반환', 'note', fs=11, sfs=9)
    s.arrow(380, 132, 380, 148, None)
    s.text(600, 175, 'InstantiateWithLease의 lease는\n인스턴스가 실제 파괴된 뒤 풀린다', 9.4, color='#555')
    s.box(10, 222, 230, 66, 'Framework 매니저', 'EffectManager · AudioManager', 'fw', fs=11.5, sfs=9)
    s.box(270, 222, 220, 66, '자체 공급자', 'AddressablesEffectPrefabProvider\nAddressablesClipProvider', 'fw', fs=10.5, sfs=8.8)
    s.box(520, 222, 230, 66, 'Addressables 직접 로드', '자체 참조 수·풀을 따로 가짐\n(AddressablesManager와 별개)', 'fw', fs=10.5, sfs=8.8)
    s.arrow(240, 255, 268, 255)
    s.arrow(490, 255, 518, 255)
    return s.render()


def d_lifetime():
    s = Svg(760, 330)
    s.text(380, 14, '에셋을 언제 잡고 언제 놓나 (시간 →)', 12, weight='bold')
    # timeline phases
    ph = [('Boot', 10, 80), ('로비', 90, 170), ('런 1', 170, 300), ('결과', 300, 350), ('로비', 350, 410),
          ('런 2 (다른 지역)', 410, 600), ('결과·로비…', 600, 750)]
    for n, a, b in ph:
        s.rect(130 + (a - 10) * 0.82, 30, (b - a) * 0.82 - 2, 22, 'plain', r=3, fill='#EEE', stroke='#CCC')
        s.text(130 + (a - 10) * 0.82 + (b - a) * 0.41, 41, n, 9.5)

    def X(v):
        return 130 + (v - 10) * 0.82
    bars = [
        ('앱 전체', 'UIManager · Framework 매니저', 10, 750, 'fw'),
        ('Game 씬', 'WorldRoot · 이펙트 머티리얼 2개', 80, 750, 'boot'),
        ('WorldView', '오브젝트·캐릭터·투사체 프리팹', 80, 750, 'view'),
        ('지역', '스테이지 맵 (지역 1)', 80, 410, 'view'),
        ('지역', '스테이지 맵 (지역 2)', 410, 750, 'view'),
        ('테마', '테마 월드 아틀라스 A', 80, 420, 'view'),
        ('테마', '아틀라스 B (먼저 로드 후 A 반환)', 400, 750, 'view'),
        ('재생 중', '소리 클립 · 이펙트 인스턴스', 170, 300, 'sound'),
        ('재생 중', '소리 클립 · 이펙트 인스턴스', 410, 600, 'sound'),
    ]
    rowmap = {}
    y = 64
    for lab, txt, a, b, k in bars:
        key = (lab, txt) if lab not in ('지역', '테마', '재생 중') else lab
        if key not in rowmap:
            rowmap[key] = y
            s.text(120, y + 11, lab, 10, anchor='end', weight='bold', color='#444')
            y += 30
        yy = rowmap[key]
        s.rect(X(a), yy, X(b) - X(a), 22, k, r=4)
        if txt:
            s.text(X(a) + 6, yy + 11, txt, 9.2, anchor='start')
    s.rect(10, y + 6, 740, 66, 'note', r=4)
    s.text(380, y + 39,
           '• 같은 지역·같은 테마면 다시 로드하지 않는다.  • 런과 런 사이에는 월드 에셋을 놓지 않는다(뷰 GameObject는 풀로만 돌아간다).\n'
           '• 캐릭터 아틀라스(116_charLook)는 묶음마다 최근 3개만 유지(LRU).  • 로드 실패는 재시도 없이 로그 1번 + 그 대상만 안 그림.\n'
           '• 비동기 로드가 끝났을 때 요청한 쪽이 이미 파괴됐으면 lease를 바로 반환한다.', 9.4)
    s.h = y + 80
    return s.render()


def d_pool():
    s = Svg(760, 360)
    s.text(380, 14, 'ObjectViewFactory.Sync — 오브젝트 뷰 한 개의 일생 (매 프레임)', 12, weight='bold')
    s.box(10, 40, 160, 56, 'RunObject o', 'Dead도 아니고\n그리기 범위 안?', 'run', fs=11, sfs=9)
    s.diamond(290, 68, 180, 56, '_views[o.Id]가 있나?', 'note', fs=10)
    s.arrow(170, 68, 198, 68)
    s.diamond(290, 165, 190, 56, 'v.Obj == o ?\n(같은 객체인가)', 'warn', fs=9.5)
    s.arrow(290, 96, 290, 136, '있음', fs=9)
    s.box(520, 40, 230, 66, 'Show(o) = 풀에서 대여', '프리팹 주소별 _prefabPool\n또는 모양별 _pool에서 Pop\n없으면 새로 만듦', 'view', fs=10.5, sfs=8.8)
    s.arrow(380, 68, 518, 68, '없음', fs=9)
    s.box(520, 120, 230, 60, '초기화', 'ResetMotion · 알파 1 · 흔들림 0\n위치 · 라벨 · SetActive(true)', 'view', fs=10.5, sfs=8.8)
    s.arrow(635, 106, 635, 118)
    s.line([(385, 165), (450, 165), (450, 90), (518, 90)])
    s.text(456, 130, '다름 → 이전 뷰\nRelease 후\n새로 대여', 8.8, anchor='start')
    s.box(520, 200, 230, 50, '이번 프레임 표시', 'Stamp 갱신 · 위치 · Animate', 'view', fs=10.5, sfs=8.8)
    s.arrow(635, 180, 635, 198)
    s.line([(290, 193), (290, 225), (518, 225)])
    s.text(330, 216, '같음 → 그대로 사용', 9)
    s.box(260, 280, 240, 60, '이번 프레임에 안 보임', '쓰러지는 연출 중이면 끝날 때까지 남김\n아니면 Release', 'plain', fs=10.5, sfs=8.8)
    s.box(520, 280, 230, 60, 'Release(v) = 풀에 반납', 'ResetMotion · SetActive(false)\nObj = null → 스택에 Push', 'view', fs=10.5, sfs=8.8)
    s.arrow(500, 310, 518, 310)
    s.line([(750, 310), (756, 310), (756, 34), (700, 34), (700, 38)], color='#3E8E41', marker='ahg')
    s.text(694, 30, '다음 대여 때 재사용', 9, color='#3E8E41', anchor='end')
    s.rect(10, 120, 178, 150, 'note', r=4)
    s.text(99, 195, '핵심 규칙\n(MV-VIEW-OBJRESET)\n'
                     '• 시뮬 안에는 풀이 없다.\n  id는 런 안에서 계속 증가,\n  새 런은 1부터 다시.\n'
                     '• 그래서 같은 id라도 객체가\n  다르면 새 뷰를 꺼낸다.\n'
                     '• 꺼낼 때와 돌려줄 때\n  둘 다 동작을 초기화한다.', 9.4)
    return s.render()


def d_fxsound():
    s = Svg(760, 250)
    s.text(380, 14, '이펙트와 소리는 「이벤트 → 표 → Framework 매니저」로 흐른다', 12, weight='bold')
    row = [('RunEvent', 'RunSim이 Emit', 'run'), ('MvFxRouter', '이벤트 → 04_effect\n그룹·자식 행', 'fx'),
           ('MvFxPlayer.Spawn', '핸들 상한 확인\n값은 ParamBank 토큰', 'fx'), ('EffectManager.Play', '타입 풀에서 대여\n없으면 프리팹 로드', 'fw'),
           ('MvFxDriver', 'IEffectRenderer\nSimDt로 진행·메쉬', 'fx')]
    for i, (t, d, k) in enumerate(row):
        x = 10 + i * 150
        s.box(x, 36, 136, 60, t, d, k, fs=10.5, sfs=8.8)
        if i:
            s.arrow(x - 14, 66, x - 1, 66)
    row2 = [('RunEvent\n/ UI 신호', None, 'run'), ('MvRunSoundRouter\nMvUiSounds', None, 'sound'),
            ('MvSoundPlayer.Play', '음소거·03_sound 행\nGAP 묶음·피치·지연', 'sound'), ('AudioManager\n.PlayAsync', None, 'fw'),
            ('ClipProvider', '키당 핸들 1 + 재생당\nlease, 끝나면 반환', 'fw')]
    for i, (t, d, k) in enumerate(row2):
        x = 10 + i * 150
        s.box(x, 130, 136, 60, t, d, k, fs=10.5, sfs=8.8)
        if i:
            s.arrow(x - 14, 160, x - 1, 160)
    s.rect(10, 204, 740, 40, 'note', r=4)
    s.text(380, 224, 'BGM은 Lobby/Run 레이어를 0.03초 크로스페이드로 바꾸며 재생 위치를 이어 간다. 음소거는 효과음을 막고 음악 트랙만 끈다.\n'
                     '이펙트 부품이 0개가 되면 핸들 종료 → 인스턴스는 타입 풀 idle로. 타입 인스턴스가 모두 컬링되면 프리팹 lease 반환.', 9.4)
    return s.render()


def d_ui():
    s = Svg(760, 330)
    s.text(380, 14, '화면 하나 = 3층 (D-92)', 12, weight='bold')
    s.box(10, 36, 220, 84, '컨트롤러', 'Framework 타입 상속 MonoBehaviour\nUIScreen / UIPopup / UIHudBase\n열기·닫기·소리·토스트만', 'ui', fs=12, sfs=9)
    s.box(270, 36, 220, 84, 'Presenter', '순수 C# 클래스\n표·Port에서 모델을 만들고\n명령을 부른다', 'ui', fs=12, sfs=9)
    s.box(530, 36, 220, 84, 'View (IXxxView)', '받은 모델만 그린다\n지금은 EnsureBuilt가\n코드로 자식을 만든다(임시 겉)', 'ui', fs=12, sfs=9)
    s.arrow(230, 78, 268, 78)
    s.arrow(490, 78, 528, 78)
    s.text(640, 134, '↑ Presenter가 View.Render(model) 호출', 9, color='#555')
    s.box(270, 150, 220, 56, 'Port 인터페이스', 'IMetaUiPort · IQuestUiPort\nILobbySource · IRunHudSource', 'meta', fs=11, sfs=8.8)
    s.arrow(380, 120, 380, 148, '읽기·명령', fs=9)
    s.box(270, 226, 220, 40, 'MvMeta · MvQuest · MvSave · RunState', None, 'meta', fs=9.5)
    s.arrow(380, 206, 380, 224)
    s.text(620, 192, '시험은 가짜 Port를 넣는다', 9.5, color='#555')
    s.box(10, 150, 220, 116, 'MvUiNav (정적)', '모든 화면 열기는 여기 한 곳\n주소 = 접두어 + MvUiIds\nUIScreen_Lobby\nUIHud_Run\nUIPopup_Result …', 'ui', fs=11.5, sfs=9)
    s.rect(10, 280, 740, 44, 'note', r=4)
    s.text(380, 302, '화면은 이벤트를 구독하지 않는다. 로비·HUD는 LateUpdate마다 값을 읽어 바뀐 것만 그리고, 메타 패널은 누를 때와 1초 Tick마다 Draw한다.\n'
                     '새 메타 화면 = MetaPanelPopup 하위 + MetaPanelPresenter 하위 + 프리팹 1개 + MvUiIds·MvUiNav 한 줄.', 9.4)
    return s.render()


def d_screenlife():
    s = Svg(760, 150)
    kinds = [('Screen', '로비 1개', '첫 열기 때 생성 → screenPool에서 계속 재사용 (숨김/보임)', 'ui'),
             ('HUD', '런 HUD 1개', 'CachingHudAsync 때 생성 → hudPool에서 재사용', 'ui'),
             ('Popup', '결과·지도·메타 8종·퀘스트·연구·가짜 광고', '열 때마다 생성 → 닫으면 DestroyUI (재사용 없음)', 'warn')]
    for i, (k, n, d, kind) in enumerate(kinds):
        y = 8 + i * 46
        s.box(10, y, 110, 38, k, None, kind, fs=12)
        s.box(130, y, 200, 38, n, None, 'plain', fs=10, bold=False)
        s.box(340, y, 410, 38, d, None, 'plain', fs=10, bold=False)
    return s.render()
