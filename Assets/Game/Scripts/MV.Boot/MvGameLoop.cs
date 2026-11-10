using System;
using System.Collections.Generic;
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
    /// Game 씬 게임 루프(10 §10.3, MV-PLAY-INT): 로비 → 런 → 결과 → 로비를 한 씬에서 돈다.
    /// <see cref="GameInitializer"/>가 붙이고 <see cref="InitAsync"/>를 기다린다. 씬 파일은 바꾸지 않고 `WorldRoot`·`LobbyStage`를
    /// Addressables로 만들어 이 오브젝트 아래에 둔다.
    /// 런 프레임: 입력 → <see cref="RunSim.Tick"/> → <see cref="RunSim.DrainEvents"/> 한 번 → 같은 목록을 소리·이펙트·월드 뷰에 넘긴다. HUD는 상태를 직접 읽는다.
    /// 카드 룰렛은 MV.Run이 스스로 고르고(04 §4.8.4), 신 선택은 로비(저장)라 런이 플레이어 선택을 기다리는 곳은 아직 없다.
    /// 교체 지점: 선택을 기다리는 런 상태가 생기면 <see cref="TickRun"/>에서 「// 1차 자동 선택:」으로 첫 선택지를 고르고 선택 UI는 MV-UI 2차가 붙인다.
    /// </summary>
    public sealed class MvGameLoop : MonoBehaviour
    {
        public const string WorldRootAddress = "WorldRoot";
        public const string LobbyStageAddress = "LobbyStage";
        public static MvGameLoop Current { get; private set; }

        public MvLoopPhase Phase { get; private set; }
        /// <summary>이번 런에서 돈 프레임 수(Tick 묶음 수).</summary>
        public int RunFrames { get; private set; }
        /// <summary>이 루프에서 결과까지 간 런 수.</summary>
        public int RunsFinished { get; private set; }
        /// <summary>정산 명령을 부른 횟수(받기 1번 = 1).</summary>
        public int SettleCount { get; private set; }
        public bool LastSettleOk { get; private set; }
        /// <summary>마지막 런 종료 사유(<see cref="FinishReason"/> 이름).</summary>
        public string LastFinishReason { get; private set; }
        public long CurrentRunSeq => _runSeq;
        public ulong CurrentSeed { get; private set; }
        /// <summary>로비 시설 단계를 넣은 횟수(로비 진입·강화 반짝임·1초마다).</summary>
        public int FacilityApplyCount { get; private set; }
        /// <summary>파티클 머티리얼 2개(<see cref="MvFx.MatNormalAddress"/>·<see cref="MvFx.MatAddAddress"/>)를 읽었는지.</summary>
        public bool FxMaterialsLoaded { get; private set; }

        // ---- 시험 훅(PlayMode 시험이 쓴다. 실제 플레이에서는 기본값) ----
        /// <summary>한 프레임에 돌릴 Tick 수(기본 1).</summary>
        public int TicksPerFrame { get; set; } = 1;
        /// <summary>값이 있으면 실시간 dt 대신 이 dt로 Tick한다.</summary>
        public double? FixedDt { get; set; }
        /// <summary>로비에 있는 동안 시설 단계를 다시 넣는 간격(초, 기본 1). 메타 팝업에서 산 것을 닫은 뒤 반영한다.</summary>
        public float FacilityRefreshSec { get; set; } = 1f;
        /// <summary>값이 있으면 포인터 대신 이 함수(런 프레임 → 목표 tx, null = 드래그 없음)로 드래그한다.</summary>
        public Func<int, double?> ScriptedDrag { get; set; }

        MvTables _tables;
        WorldView _world;
        LobbyStage _lobby;
        Camera _sceneCamera;
        readonly List<IDisposable> _leases = new List<IDisposable>();
        readonly LoopViewSource _viewSource = new LoopViewSource();
        MvRunInputReader _input;
        LobbyScreen _lobbyScreen;
        ResultPopup _resultPopup;
        RunSim _sim;
        long _runSeq;
        int _savedArea, _savedSouls;
        int _themeId = int.MinValue;
        float _facilityTimer;
        CancellationToken _ct;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Current = null;

        void Awake() => Current = this;

        void OnDestroy()
        {
            if (Current == this) Current = null;
            foreach (var d in _leases) d?.Dispose();
            _leases.Clear();
        }

        // ═════════ 준비 ═════════

        /// <summary>월드·로비 무대·이펙트를 만들고 로비를 연다. 실패하면 예외(씬 초기화 실패로 남는다).</summary>
        public async UniTask InitAsync(MvTables tables)
        {
            _tables = tables ?? throw new ArgumentNullException(nameof(tables));
            _ct = this.GetCancellationTokenOnDestroy();
            Phase = MvLoopPhase.Loading;
            try
            {
                // UI는 Boot 8단계에서 시작했고 씬이 바뀌면 UIManagerCreater가 이 씬에 다시 붙는다. 그 끝을 기다린다.
                var ui = GameBootstrap.Ui;
                if (ui) await ui.StartInitAsync(_ct);
                Alive();

                _input = new MvRunInputReader(tables.RunConfig.D(RunConfigKeys.DRAG_SCREEN_M));

                var world = await AddressableScope.InstantiateWithLease(WorldRootAddress, transform, _ct);
                Alive();
                Keep(world.lease, world.destroyOnDispose);
                _world = world.instance ? world.instance.GetComponent<WorldView>() : null;
                if (_world == null) throw new InvalidOperationException($"'{WorldRootAddress}' 프리팹에 WorldView가 없습니다.");
                _world.Init(tables, _viewSource);
                await ApplyThemeAsync(CurrentArea());
                Alive();

                var lobby = await AddressableScope.InstantiateWithLease(LobbyStageAddress, transform, _ct);
                Alive();
                Keep(lobby.lease, lobby.destroyOnDispose);
                _lobby = lobby.instance ? lobby.instance.GetComponent<LobbyStage>() : null;
                if (_lobby == null) throw new InvalidOperationException($"'{LobbyStageAddress}' 프리팹에 LobbyStage가 없습니다.");
                _lobby.Init(_world.Sprites);
                if (!await _lobby.LoadAsync(_ct)) Debug.LogWarning("[MvGameLoop] 로비 그림 일부를 불러오지 못했습니다.");
                Alive();

                await InstallFxAsync();
                Alive();
                TakeSceneCamera();
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

        void Keep(IDisposable lease, IDisposable destroy)
        {
            if (destroy != null) _leases.Add(destroy);
            if (lease != null) _leases.Add(lease);
        }

        async UniTask InstallFxAsync()
        {
            var fxRoot = new GameObject("FxRoot").transform;
            fxRoot.SetParent(transform, false);
            var normal = await LoadMaterialAsync(MvFx.MatNormalAddress);
            Alive();
            var add = await LoadMaterialAsync(MvFx.MatAddAddress);
            Alive();
            FxMaterialsLoaded = normal != null && add != null;
            if (!FxMaterialsLoaded) Debug.LogWarning("[MvGameLoop] 우다다 머티리얼을 찾지 못했습니다(" + MvFx.MatNormalAddress + ", " + MvFx.MatAddAddress + ").");
            MvFx.InstallDefault(fxRoot, _world.Rig, () => _sim?.State, normal, add);
        }

        async UniTask<Material> LoadMaterialAsync(string address)
        {
            var lease = await AddressableScope.Get<Material>(address, _ct);
            if (lease == null) return null;
            _leases.Add(lease);
            return lease.Asset;
        }

        /// <summary>
        /// 화면 카메라: 로비는 LobbyCamera, 런은 GameCamera가 그리고 UI는 그 위에 겹친다(UIManagerCreater.mainCamera를 바꾸면 다음 LateUpdate에 다시 붙는다).
        /// 씬의 Main Camera는 렌더를 끈다(AudioListener는 남긴다).
        /// </summary>
        void TakeSceneCamera()
        {
            foreach (var root in gameObject.scene.GetRootGameObjects())
                foreach (var cam in root.GetComponentsInChildren<Camera>(true))
                    if (cam.CompareTag("MainCamera") && !cam.transform.IsChildOf(transform)) { _sceneCamera = cam; break; }
        }

        void UseCamera(Camera cam)
        {
            var ui = GameBootstrap.Ui;
            if (ui) ui.mainCamera = cam;
            if (_sceneCamera) _sceneCamera.enabled = false;
        }

        int CurrentArea()
        {
            var store = MvSave.Current;
            return store != null && !store.IsDisposed ? store.Progress.Area : 0;
        }

        async UniTask ApplyThemeAsync(int area)
        {
            AreaData row = null;
            foreach (var a in _tables.Area.Datas) if (a.areaNo == area + 1) { row = a; break; }
            if (row == null || row.themeId == _themeId) return;
            var theme = _tables.Theme.Get(row.themeId);
            if (theme == null) return;
            _themeId = row.themeId;
            if (!await _world.SetThemeAsync(theme, _ct)) Debug.LogWarning("[MvGameLoop] 테마 그림 일부를 불러오지 못했습니다: " + theme.key);
        }

        // ═════════ 로비 ═════════

        async UniTask EnterLobbyAsync()
        {
            Phase = MvLoopPhase.Loading;
            _sim = null;
            _viewSource.Run = null;
            _resultPopup = null;
            await ApplyThemeAsync(CurrentArea());

            _world.gameObject.SetActive(false);
            _lobby.SetActive(true);
            UseCamera(_lobby.Cam);
            MvSound.EnterLobby();
            ApplyFacilities();
            _facilityTimer = 0f;

            _lobbyScreen = await MvUiNav.OpenLobbyAsync(new LobbyViewData { OnStart = OnLobbyStart, OnFacilityBump = BumpFacility });
            if (_lobbyScreen == null) throw new InvalidOperationException("로비 화면을 열지 못했습니다.");
            Phase = MvLoopPhase.Lobby;
        }

        /// <summary>로비 아지트 시설 단계(정본 azStages)를 메타 값으로 로비 무대에 넣는다.</summary>
        void ApplyFacilities()
        {
            if (_lobby == null) return;
            ILobbySource src = MvSaveLobbySource.Instance;
            foreach (var k in LobbyFacilities.Keys) _lobby.SetFacility(k, src.FacilityStage(k, out int extra), extra);
            FacilityApplyCount++;
        }

        /// <summary>강화 구매 반짝임(<see cref="LobbyViewData.OnFacilityBump"/>, 시험도 부른다): 단계를 다시 넣고 그 시설을 튕긴다.</summary>
        public void BumpFacility(string key)
        {
            if (_lobby == null) return;
            ApplyFacilities();
            _lobby.Bump(key);
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
            var store = MvSave.Current;
            bool hasSave = store != null && !store.IsDisposed;
            var snap = hasSave ? MvRunSnapshot.FromStore(store, _tables.RunConfig.I(RunConfigKeys.FLOOR_COUNT))
                : MvRunSnapshot.Defaults(MvBootSave.CreateConfig(_tables));
            var meta = MvMeta.Current;
            _runSeq = meta != null && meta.IsReady ? meta.BeginRun() : snap.LastSettledRunSeq + 1;
            CurrentSeed = MvRunSnapshot.SeedFor(_runSeq);

            var builder = new RunLevelBuilder();
            var ctx = MvRunStart.CreateContext(snap, _tables, CurrentSeed);
            _sim = RunSim.Create(ctx, new MainRunRule(builder), new Pcg32Rng(CurrentSeed), builder);
            _savedArea = snap.Area;
            _savedSouls = snap.Souls;
            RunFrames = 0;
            _input.Reset();

            _viewSource.Run = new RunStateViewSource(_sim.State, _tables);
            _lobby.SetActive(false);
            _world.gameObject.SetActive(true);
            _world.Rig.Init(_tables, _viewSource);   // 로비 자세에서 런 자세로 쓸어 간다(02 §2.2.2)
            _world.Rig.Cam.enabled = true;
            UseCamera(_world.Rig.Cam);
            var s = _sim.State;
            _world.RebuildProps(s.Base, Math.Max(0, s.Gz - s.Base), false, s.WorldY);
            Phase = MvLoopPhase.Run;

            MvUiNav.EnterRunAsync(new RunStateHudSource(() => _sim?.State, () => _savedSouls)).Forget(Debug.LogException);
        }

        void Update()
        {
            if (Phase == MvLoopPhase.Lobby)
            {
                _facilityTimer += Time.unscaledDeltaTime;
                if (_facilityTimer >= FacilityRefreshSec)
                {
                    _facilityTimer = 0f;
                    ApplyFacilities();
                }
                if (MvRunInputReader.StartKeyPressed()) PressStart();
                return;
            }
            if (Phase != MvLoopPhase.Run && Phase != MvLoopPhase.Result) return;
            if (_sim == null) return;
            TickRun();
        }

        /// <summary>한 프레임: 입력 → Tick(×<see cref="TicksPerFrame"/>) → 이벤트 한 번 → 소리·이펙트·월드.</summary>
        void TickRun()
        {
            var s = _sim.State;
            RunInput input;
            if (Phase != MvLoopPhase.Run) input = RunInput.None;
            else if (ScriptedDrag != null)
            {
                var tx = ScriptedDrag(RunFrames);
                input = tx.HasValue ? RunInput.Drag(tx.Value) : RunInput.None;
            }
            else input = _input.Read(s);

            double dt = FixedDt ?? Time.unscaledDeltaTime;
            int n = Math.Max(1, TicksPerFrame);
            for (int i = 0; i < n; i++)
            {
                _sim.Tick(dt, input);
                if (s.Phase == RunPhase.Result) break;
            }
            if (Phase == MvLoopPhase.Run) RunFrames++;

            var events = _sim.DrainEvents();
            MvSound.OnRunEvents(events);
            MvFx.OnRunEvents(events);
            _world.OnRunEvents(events);   // 오브젝트 맞음·우물 벽돌 빠짐(06 §6.13)
            foreach (var e in events)
                if (e.Type == RunEventType.ClimbStart)   // 계단 위 새 지형(정본 buildEnv(len, from, cliff))
                    _world.RebuildProps(e.Z, Math.Max(0, s.Gz - e.Z), true, s.WorldY + e.Amount2);
            _world.SyncObjects(s.Objs, s.P.Z, s.WorldY);
            _world.SyncCats(s);   // 플레이어·친구 고양이(02 §2.1.4)

            if (Phase == MvLoopPhase.Run && s.Phase == RunPhase.Result) FinishRun();
        }

        // ═════════ 결과 ═════════

        RunResult _questRun;

        void FinishRun()
        {
            Phase = MvLoopPhase.Result;
            RunsFinished++;
            var r = _sim.State.Result;
            _questRun = r; // 정산 성공 뒤 런 안 퀘스트 카운터(계단·벽) 기록
            LastFinishReason = r != null ? r.Reason.ToString() : "None";
            if (r == null)
            {
                Debug.LogWarning("[MvGameLoop] 런이 결과 없이 끝났습니다. 로비로 돌아갑니다.");
                EnterLobbyAsync().Forget(Debug.LogException);
                return;
            }
            var data = ResultViewData.Create(r, _runSeq, _savedArea, _savedSouls, _tables);
            var meta = MvMeta.Current;
            data.Settler = new CountingSettler(this, meta != null && meta.IsReady ? null : NoSaveSettler.Instance);
            data.OnCollected = OnCollected;
            OpenResultAsync(data).Forget(Debug.LogException);
        }

        async UniTask OpenResultAsync(ResultViewData data)
        {
            _resultPopup = await MvUiNav.OpenResultAsync(data);
            if (_resultPopup == null) throw new InvalidOperationException("결과 팝업을 열지 못했습니다.");
        }

        /// <summary>결과 「받기」(시험용). 팝업이 열려 있으면 정산 1번 → 로비.</summary>
        public bool PressCollect()
        {
            if (Phase != MvLoopPhase.Result || _resultPopup == null) return false;
            _resultPopup.PressCollect();
            return true;
        }

        public bool ResultOpen => Phase == MvLoopPhase.Result && _resultPopup != null;

        void OnCollected(MvSettleResult result)
        {
            if (Phase != MvLoopPhase.Result) return;
            // 전면 광고(result.ShowInterstitial)는 2차(MV-PLAT-ADS, D-90).
            EnterLobbyAsync().Forget(Debug.LogException);
        }

        /// <summary>정산 1번을 세고 실제 정산기(기본 MvMeta.Current)에 넘긴다.</summary>
        sealed class CountingSettler : IResultSettler
        {
            readonly MvGameLoop _loop;
            readonly IResultSettler _inner;

            public CountingSettler(MvGameLoop loop, IResultSettler inner)
            {
                _loop = loop;
                _inner = inner ?? MvMetaResultSettler.Instance;
            }

            public MvSettleResult Settle(MvRunOutcome outcome, int mul)
            {
                _loop.SettleCount++;
                var r = _inner.Settle(outcome, mul);
                _loop.LastSettleOk = r != null && r.Ok;
                if (_loop.LastSettleOk && _loop._questRun != null)
                {
                    // 본편 런에서만 생기는 값(계단·벽)은 정산 응답에 없어 기록 요청으로 보낸다(정산 카운터는 정산 응답에 함께 저장됨).
                    var q = MV.Content.MvQuest.Current?.RecordRun(_loop._questRun);
                    if (q != null && !q.Ok) Debug.LogWarning("[MvGameLoop] 퀘스트 런 기록 실패 " + q);
                    _loop._questRun = null;
                }
                return r;
            }
        }

        /// <summary>저장이 없을 때(복구 대기·데이터 없음): 정산하지 않고 실패 결과를 돌려준다(진행을 바꾸지 않는다).</summary>
        sealed class NoSaveSettler : IResultSettler
        {
            public static readonly NoSaveSettler Instance = new NoSaveSettler();
            public MvSettleResult Settle(MvRunOutcome outcome, int mul) => new MvSettleResult { Mul = mul };
        }

        /// <summary>월드 뷰 공급원: 런이 있으면 그 상태, 없으면 로비 자세.</summary>
        sealed class LoopViewSource : IWorldViewSource
        {
            public RunStateViewSource Run;

            public CameraInput Camera => Run != null ? Run.Camera : new CameraInput { Mode = WorldViewMode.Lobby };

            public bool TryGetCliff(out CliffInfo cliff)
            {
                if (Run != null) return Run.TryGetCliff(out cliff);
                cliff = default;
                return false;
            }
        }
    }
}
