using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using jjevol;
using MV.Data;
using MV.Fx;
using MV.Meta;
using MV.Run;
using MV.Save;
using MV.Sound;
using MV.UI;
using MV.View;
using UnityEngine;

namespace MV.Boot
{
    /// <summary>게임 루프 단계(시험·개발 화면이 읽는다).</summary>
    public enum MvLoopPhase
    {
        None,
        Loading,
        Lobby,
        Run,
        Result,
        Failed,
    }

    /// <summary>
    /// Game 씬 게임 루프(10 §10.3, MV-PLAY-INT): 로비 → 런 → 지도 → 결과 → 로비를 한 씬에서 돈다(런 끝 순서 MV-SPEC-02 01 §12-3).
    /// <see cref="GameInitializer"/>가 붙이고 <see cref="InitAsync"/>를 기다린다. `WorldRoot`(월드·`GameCamera`)는 Game 씬에 직접 놓여 있고
    /// 초기화기가 그 참조를 넘긴다(2026-11-16, 에디터에서 월드·카메라를 직접 보고 고치기 위해 Addressables 생성을 뺐다).
    /// 이 클래스는 단계(<see cref="MvLoopPhase"/>) 전환과 시험 훅만 가진다. 일은 모듈이 한다:
    /// <list type="bullet">
    /// <item><see cref="MvStageLoader"/>: 지역 맵 프리팹·테마</item>
    /// <item><see cref="MvLobbyTrack"/>: 로비 트랙(다음 런 미리보기)</item>
    /// <item><see cref="MvRunFactory"/>: 런 순번·시드 → 시뮬</item>
    /// <item><see cref="MvRunFrame"/>: 런 한 프레임(입력·Tick·이벤트 전달)</item>
    /// <item><see cref="MvRunEndFlow"/>: 지도 → 결과 → 정산</item>
    /// <item><see cref="MvLoopFx"/>: 연출 재생기 설치</item>
    /// </list>
    /// 카드 룰렛은 MV.Run이 스스로 고르고(04 §4.8.4), 신 선택은 로비(저장)라 런이 플레이어 선택을 기다리는 곳은 아직 없다.
    /// </summary>
    public sealed class MvGameLoop : MonoBehaviour
    {
        public static MvGameLoop Current { get; private set; }

        public MvLoopPhase Phase { get; private set; }
        /// <summary>이번 런에서 돈 프레임 수(Tick 묶음 수).</summary>
        public int RunFrames { get; private set; }
        /// <summary>이 루프에서 결과까지 간 런 수.</summary>
        public int RunsFinished { get; private set; }
        /// <summary>이 루프에서 결과 팝업을 열라고 한 횟수(런 끝 지도 닫힘 → 결과, 시험이 읽는다).</summary>
        public int ResultOpens => _end.ResultOpens;
        /// <summary>정산 명령을 부른 횟수(받기 1번 = 1).</summary>
        public int SettleCount => _end.SettleCount;
        public bool LastSettleOk => _end.LastSettleOk;
        /// <summary>마지막 런 종료 사유(<see cref="FinishReason"/> 이름).</summary>
        public string LastFinishReason { get; private set; }
        public long CurrentRunSeq => _runSeq;
        public ulong CurrentSeed { get; private set; }
        /// <summary>로비 트랙에 깐 오브젝트 수(시험·QA용).</summary>
        public int LobbyObjectCount => _lobby.ObjectCount;
        /// <summary>로비 트랙 미리보기 런의 프레임 수(Tick하지 않으니 0, 없으면 −1. 시험·QA용).</summary>
        public long LobbyFrame => _lobby.Frame;
        /// <summary>화면을 그리는 카메라가 월드 카메라인지(로비·런 같은 화면, 시험용).</summary>
        public bool WorldCameraActive => _world != null && _world.Rig.Cam.enabled && _world.gameObject.activeInHierarchy;
        /// <summary>파티클 머티리얼 2개(<see cref="MvFx.MatNormalAddress"/>·<see cref="MvFx.MatAddAddress"/>)를 읽었는지.</summary>
        public bool FxMaterialsLoaded => _fx.MaterialsLoaded;

        // ---- 시험 훅(PlayMode 시험이 쓴다. 실제 플레이에서는 기본값) ----
        /// <summary>한 프레임에 돌릴 Tick 수(기본 1).</summary>
        public int TicksPerFrame { get; set; } = 1;
        /// <summary>값이 있으면 실시간 dt 대신 이 dt로 Tick한다.</summary>
        public double? FixedDt { get; set; }
        /// <summary>값이 있으면 포인터 대신 이 함수(런 프레임 → 목표 tx, null = 드래그 없음)로 드래그한다.</summary>
        public Func<int, double?> ScriptedDrag { get; set; }

        MvTables _tables;
        WorldView _world;
        readonly MvLoopViewSource _viewSource = new MvLoopViewSource();
        readonly MvLobbyTrack _lobby = new MvLobbyTrack();
        readonly MvRunEndFlow _end = new MvRunEndFlow();
        readonly MvLoopFx _fx = new MvLoopFx();
        MvStageLoader _stage;
        MvRunFrame _frame;
        LobbyScreen _lobbyScreen;
        RunSim _sim;
        long _runSeq;
        int _savedArea, _savedSouls;
        CancellationToken _ct;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Current = null;

        /// <summary>세션 런 배속(<c>RunSetup.TimeScale</c>). 플레이어 HUD 1x/2x 토글과 테스트 패널 ×1~×3이 바꾸고, 새 런에도 넘긴다.</summary>
        int _timeScale = 1;

        void Awake()
        {
            Current = this;
            MvRunSpeed.Set = SetTimeScale;
            MvRunSpeed.Get = () => _timeScale;
            MvDebugHooks.SetTimeScale = v => SetTimeScale((int)Math.Round(v));
        }

        /// <summary>런 배속을 바꾼다. 시뮬이 매 틱 <c>Setup.TimeScale</c>을 읽으므로 다음 틱부터 진행 속도가 바뀌고, HUD는 같은 값을 읽어 표시한다.</summary>
        public void SetTimeScale(int timeScale)
        {
            _timeScale = Math.Max(1, timeScale);
            var setup = _sim?.State?.Setup;
            if (setup != null) setup.TimeScale = _timeScale;
        }

        void OnDestroy()
        {
            if (Current == this)
            {
                Current = null;
                MvRunSpeed.Set = null;
                MvRunSpeed.Get = null;
                MvDebugHooks.SetTimeScale = null;
            }
            _fx.Dispose();
            _stage?.Dispose();
        }

        // ═════════ 준비 ═════════

        /// <summary>씬의 월드를 초기화하고 이펙트를 만들어 로비를 연다. 실패하면 예외(씬 초기화 실패로 남는다).</summary>
        public async UniTask InitAsync(MvTables tables, WorldView world)
        {
            _tables = tables ?? throw new ArgumentNullException(nameof(tables));
            _world = world != null ? world : throw new ArgumentNullException(nameof(world), "Game 씬에 WorldRoot(WorldView)가 없습니다.");
            _ct = this.GetCancellationTokenOnDestroy();
            Phase = MvLoopPhase.Loading;
            try
            {
                // UI는 Boot 8단계에서 시작했고 씬이 바뀌면 UIManagerCreater가 이 씬에 다시 붙는다. 그 끝을 기다린다.
                var ui = GameBootstrap.Ui;
                if (ui) await ui.StartInitAsync(_ct);
                Alive();

                _frame = new MvRunFrame(tables);
                _stage = new MvStageLoader(tables, _world, _viewSource, Alive);

                _world.Init(tables, _viewSource);
                await _stage.ApplyAsync(CurrentArea(), _ct);
                Alive();

                await _fx.InstallAsync(transform, _world, () => _sim?.State, Alive, _ct);
                Alive();
                await EnterLobbyAsync();
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                Phase = MvLoopPhase.Failed;
                throw;
            }
        }

        /// <summary>기다리는 동안 씬이 바뀌어 이 오브젝트가 사라졌으면 초기화를 취소로 끝낸다.</summary>
        void Alive()
        {
            if (!this) throw new OperationCanceledException(_ct);
        }

        /// <summary>
        /// 화면 카메라: 로비·런 모두 씬의 `GameCamera`(월드 카메라)가 그리고 UI는 그 위에 겹친다
        /// (UIManagerCreater.mainCamera를 바꾸면 다음 LateUpdate에 다시 붙는다).
        /// </summary>
        static void UseCamera(Camera cam)
        {
            var ui = GameBootstrap.Ui;
            if (ui) ui.mainCamera = cam;
        }

        static int CurrentArea()
        {
            var store = MvSave.Current;
            return store != null && !store.IsDisposed ? store.Progress.Area : 0;
        }

        // ═════════ 로비 ═════════

        async UniTask EnterLobbyAsync()
        {
            Phase = MvLoopPhase.Loading;
            _sim = null;
            _viewSource.Run = null;
            _end.Clear();
            await _stage.ApplyAsync(CurrentArea(), _ct);
            if (_stage.Map != null) _stage.Map.Reshuffle();   // 판마다 길가 장식 배치를 새로 섞는다

            // 로비 트랙: 다음 런 순번·시드로 미리보기 런을 만든다(Tick하지 않음, 순번을 쓰지 않는다). 월드 카메라는 로비 자세(Run = null).
            var preview = MvRunFactory.Create(_tables, _world, MvRunFactory.PeekRunSeq(_tables), out _, out _);
            _world.gameObject.SetActive(true);
            _world.Rig.Init(_tables, _viewSource);
            _world.Rig.Cam.enabled = true;
            UseCamera(_world.Rig.Cam);
            _lobby.Build(preview, _tables, _world);
            MvSound.EnterLobby();

            _lobbyScreen = await MvUiNav.OpenLobbyAsync(new LobbyViewData { OnStart = OnLobbyStart });
            if (_lobbyScreen == null) throw new InvalidOperationException("로비 화면을 열지 못했습니다.");
            Phase = MvLoopPhase.Lobby;
        }

        /// <summary>로비 출발(시험·키 입력). 로비가 떠 있으면 런이 이 프레임에 시작된다.</summary>
        public bool PressStart()
        {
            if (Phase != MvLoopPhase.Lobby || _lobbyScreen == null) return false;
            _lobbyScreen.PressStart();
            return Phase == MvLoopPhase.Run;
        }

        void OnLobbyStart()
        {
            if (Phase != MvLoopPhase.Lobby) return;
            try
            {
                StartRun();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                _sim = null;
                _viewSource.Run = null;
                EnterLobbyAsync().Forget(Debug.LogException);
            }
        }

        // ═════════ 런 ═════════

        void StartRun()
        {
            var meta = MvMeta.Current;
            _runSeq = meta != null && meta.IsReady ? meta.BeginRun() : MvRunFactory.PeekRunSeq(_tables);
            _sim = MvRunFactory.Create(_tables, _world, _runSeq, out var snap, out ulong seed);
            _sim.State.Setup.TimeScale = _timeScale;
            CurrentSeed = seed;
            _lobby.Clear();
            _savedArea = snap.Area;
            _savedSouls = snap.Souls;
            RunFrames = 0;
            _frame.Reset();

            _viewSource.Run = new RunStateViewSource(_sim.State, _tables);   // 카메라가 로비 자세에서 런 자세로 쓸어 간다(02 §2.2.2)
            Phase = MvLoopPhase.Run;

            MvUiNav.EnterRunAsync(new RunStateHudSource(() => _sim?.State, () => _savedSouls, _world.Rig.Model.SideRow?.turnSec ?? 0)).Forget(Debug.LogException);
        }

        void Update()
        {
            if (Phase == MvLoopPhase.Lobby)
            {
                _lobby.Sync(_world);
                if (MvRunInputReader.StartKeyPressed()) PressStart();
                return;
            }
            if (Phase != MvLoopPhase.Run && Phase != MvLoopPhase.Result) return;
            if (_sim == null) return;
            bool playing = Phase == MvLoopPhase.Run;
            _sim.State.ShownHandTier = MV.UI.WeaponSection.ShownTier;   // 피해·투사체·외형 = HUD 손패 맨 위 무기(사용자 2026-11-19)
            _frame.Step(_sim, _world, playing, ScriptedDrag, RunFrames, FixedDt, TicksPerFrame);
            if (playing) RunFrames++;
            if (playing && _sim.State.Phase == RunPhase.Result) FinishRun();
        }

        // ═════════ 결과 ═════════

        void FinishRun()
        {
            Phase = MvLoopPhase.Result;
            RunsFinished++;
            var r = _sim.State.Result;
            LastFinishReason = r != null ? r.Reason.ToString() : "None";
            if (r == null)
            {
                Debug.LogWarning("[MvGameLoop] 런이 결과 없이 끝났습니다. 로비로 돌아갑니다.");
                EnterLobbyAsync().Forget(Debug.LogException);
                return;
            }
            var data = ResultViewData.Create(r, _runSeq, _savedArea, _savedSouls, _tables);
            // 전면 광고(result.ShowInterstitial)는 2차(MV-PLAT-ADS, D-90).
            _end.Begin(data, () => Phase == MvLoopPhase.Result, _ => EnterLobbyAsync().Forget(Debug.LogException));
        }

        /// <summary>런 끝 지도가 열려 있다(결과 전, 시험용).</summary>
        public bool MapOpen => _end.MapOpen;

        /// <summary>런 끝 지도 닫기(시험용). 닫으면 결과가 열린다.</summary>
        public bool PressMapClose() => _end.PressMapClose();

        /// <summary>뒤로 가기·Esc(시험용): Framework <c>UIManager</c>의 뒤로 가기 처리를 그대로 부른다.</summary>
        public UniTask PressBack() => UIManager.Instance.HandleBackButtonAsync();

        /// <summary>결과 「받기」(시험용). 팝업이 열려 있으면 정산 1번 → 로비.</summary>
        public bool PressCollect() => _end.PressCollect();

        public bool ResultOpen => _end.ResultOpen;
    }
}
