using System;
using System.Collections.Generic;
using jjevol.Effect;
using MV.Data;
using MV.Run;
using MV.View;
using UnityEngine;

namespace MV.Fx
{
    /// <summary>화면 플래시 표시(06 §6.9, MV-UI-HUD 구현). 목표값 + ease 0.25 s 전환, HoldMs 뒤 0(도중 반전 그대로, D-49).</summary>
    public interface IMvFlashView
    {
        void Flash(in FlashRequest r);
    }

    /// <summary>
    /// 연출 전역 입구(06 §6.1). Boot가 <see cref="Install"/>로 한 번 세우고, 런 화면은 매 프레임 `RunSim.DrainEvents()` 결과를
    /// <see cref="OnRunEvents"/>로 넘긴다(MV.Sound와 같은 이벤트 목록을 함께 넘기면 된다). 세우기 전 호출은 무시한다.
    /// </summary>
    public static class MvFx
    {
        /// <summary>파티클 머티리얼 주소(Addressables, 라벨 fx). <see cref="InstallDefault"/>의 normal·add로 Boot가 읽어 넘긴다.</summary>
        public const string MatNormalAddress = "Fx/Mat_Normal";
        public const string MatAddAddress = "Fx/Mat_Add";

        public static MvFxPlayer Player { get; private set; }

        public static void Install(MvFxPlayer player) => Player = player;

        /// <summary>
        /// 기본 구성: Framework EffectManager(공급자는 Boot가 이미 Addressables로 설정) + 카메라 리그 흔들림 + 팝업·비행 풀 + 우다다 화면.
        /// fxRoot는 월드 원점·회전 없음(게임 좌표 그대로, 06 §6.1). 그림 쪽(view·anchors·flash)은 UI가 나중에 꽂는다.
        /// </summary>
        public static MvFxPlayer InstallDefault(Transform fxRoot, CameraRig rig, Func<RunState> state, Material normal, Material add)
        {
            var t = MvTables.Current;
            var world = new RunStateFxWorld(state);
            var p = new MvFxPlayer(EffectManager.Instance, fxRoot, world, () => MvTables.Current)
            {
                ShakeTarget = rig,
                Pops = MvPopPool.FromTables(t),
                Flies = MvFlyLayer.FromTables(t),
            };
            var cam = rig != null ? rig.Cam : Camera.main;
            var proj = new CameraScreenProjector(() => rig != null ? rig.Cam : Camera.main);
            p.Pops.Projector = proj;
            p.Flies.Projector = proj;
            if (cam != null)
            {
                MvFxDriver.ViewCamera = cam;
                var go = new GameObject("WarpOverlay");
                var ov = go.AddComponent<MvWarpOverlay>();
                ov.Init(cam, MvWarpModel.FromTables(t), normal, add);
                p.Warp = ov;
            }
            Install(p);
            MvFxRunner.Ensure();
            return p;
        }

        public static void OnRunEvents(IEnumerable<RunEvent> events) => Player?.OnRunEvents(events);

        /// <summary>문구 팝업(UI가 05 목록 문구로 부른다, 06 §6.7).</summary>
        public static void Pop(Vector3 world, string text, Color32 color, float size = 1) =>
            Player?.Pops?.Add(new PopRequest { World = world, Text = text, Color = color, Size = size });

        public static void Tick() => Player?.Tick();
    }

    /// <summary>
    /// RunEvent 묶음 → 라우터 → Framework 재생·흔들림·팝업·비행·플래시·우다다. 매 프레임 <see cref="Tick"/>(실시간 층·투사체 꼬리).
    /// </summary>
    public sealed class MvFxPlayer
    {
        readonly IEffectPlayer _effects;
        readonly Transform _root;
        readonly IMvFxWorld _world;
        readonly Func<MvTables> _tables;
        readonly MvFxRouter _router;
        readonly MvFxOutput _out = new MvFxOutput();
        readonly Dictionary<int, float> _trailT = new Dictionary<int, float>();
        readonly List<int> _trailGone = new List<int>();
        readonly Action<EffectEndReason> _onEnded;
        int _handles;

        public CameraRig ShakeTarget;
        public MvPopPool Pops;
        public MvFlyLayer Flies;
        public MvWarpOverlay Warp;
        public IMvFlashView FlashView;
        /// <summary>`300_runConfig` fx.maxHandles·fx.trailInterval(생성 때 표에서 채운다, 코드 기본값 없음).</summary>
        public int MaxHandles;
        public float TrailInterval;
        /// <summary>시험·하네스용: 마지막 라우팅 결과.</summary>
        public MvFxOutput LastOutput => _out;
        public int ActiveHandles => _handles;

        public MvFxPlayer(IEffectPlayer effects, Transform root, IMvFxWorld world, Func<MvTables> tables, Func<float> rnd01 = null)
        {
            _effects = effects;
            _root = root;
            _world = world;
            _tables = tables ?? (() => MvTables.Current);
            _router = new MvFxRouter(_tables, rnd01);
            _onEnded = _ => _handles = Math.Max(0, _handles - 1);
            MvFxClock.World = world;
            var t = _tables();
            if (t == null)
            {
                Debug.LogError("[MvFxPlayer] 표(MvTables)가 없어 연출 값을 읽지 못했습니다. 이펙트를 재생하지 않습니다.");
                return;
            }
            var c = t.RunConfig;
            MvFxClock.HitStopScale = c.D(RunConfigKeys.Time_HitStopScale);
            MvFxClock.DtCap = c.D(RunConfigKeys.Time_DtCap);
            MaxHandles = c.I(RunConfigKeys.Fx_MaxHandles);
            TrailInterval = (float)c.D(RunConfigKeys.Fx_TrailInterval);
            MvFxDriver.SparkCapThreshold = c.I(RunConfigKeys.Fx_SparkCapThreshold);
            MvFxDriver.SparkCapCount = c.I(RunConfigKeys.Fx_SparkCapCount);
        }

        /// <summary>비행 도착(HUD 숫자 올리기·게이지 깜빡임은 UI가 구독).</summary>
        public event Action<FlyTarget, int> FlyArrived;

        void OnFlyArrived(FlyTarget t, int amount) => FlyArrived?.Invoke(t, amount);

        bool _flyHooked;

        public void OnRunEvents(IEnumerable<RunEvent> events)
        {
            if (events == null) return;
            foreach (var e in events)
            {
                _out.Clear();
                _router.Route(e, _world, _out);
                Apply(_out);
            }
        }

        void Apply(MvFxOutput o)
        {
            var t = _tables();
            for (int i = 0; i < o.Spawns.Count; i++) Spawn(t, o.Spawns[i]);
            for (int i = 0; i < o.Shakes.Count; i++) ShakeTarget?.Shake(o.Shakes[i].x, o.Shakes[i].y);
            if (Pops != null) for (int i = 0; i < o.Pops.Count; i++) Pops.Add(o.Pops[i]);
            if (Flies != null) for (int i = 0; i < o.Flies.Count; i++) Flies.FlyTo(o.Flies[i]);
            if (FlashView != null) for (int i = 0; i < o.Flashes.Count; i++) FlashView.Flash(o.Flashes[i]);
            if (Warp != null)
            {
                if (o.WarpBegin) Warp.Begin();
                if (o.WarpStage >= 0) Warp.SetStage(o.WarpStage);
                if (o.WarpEnd) Warp.End();
            }
        }

        /// <summary>기본 종류 1개 재생(04_effect prefab 주소, skinIndex = 값 토큰, 정렬 레이어 World).</summary>
        public EffectHandle Spawn(MvTables t, in FxSpawn s)
        {
            if (_effects == null || _handles >= MaxHandles) return null;
            var row = t?.Effect.ByKey(s.Key);
            if (row == null || string.IsNullOrEmpty(row.prefab)) return null;
            int token = MvFxParamBank.Put(s);
            var req = new EffectRequest(effectType: row.prefab, skinIndex: token, sortingLayer: MvFxDriver.SortingLayer, parent: _root,
                localPos: s.Pos, onEnded: _onEnded);
            _handles++;
            var h = _effects.Play(req);
            if (h == null) _handles--;
            return h;
        }

        public void Tick()
        {
            if (!_flyHooked && Flies != null) { Flies.Arrived += OnFlyArrived; _flyHooked = true; }
            float real = MvFxClock.RealDt;
            Pops?.Step(real);
            Flies?.Step(real);
            StepTrails(MvFxClock.SimDt);
        }

        /// <summary>정본 L2113: 원소 투사체마다 0.03 s(simDt)마다 trail 1개.</summary>
        void StepTrails(float dt)
        {
            if (_world is not IMvFxProjectiles projs) return;
            var els = _router.ProjectileElements;
            if (els.Count == 0) { _trailT.Clear(); return; }
            var t = _tables();
            _trailGone.Clear();
            foreach (var kv in _trailT) if (!els.ContainsKey(kv.Key)) _trailGone.Add(kv.Key);
            foreach (var id in _trailGone) _trailT.Remove(id);
            foreach (var kv in els)
            {
                _trailT.TryGetValue(kv.Key, out var acc);
                acc += dt;
                if (acc > TrailInterval && projs.TryProjectile(kv.Key, out var pos))
                {
                    acc = 0;
                    var s = FxSpawn.Of("fx.trail", pos);
                    s.Variant = kv.Value == "fire" ? 1 : kv.Value == "bolt" ? 2 : 0;
                    Spawn(t, s);
                }
                _trailT[kv.Key] = acc;
            }
        }
    }

    /// <summary>투사체 위치 읽기(꼬리 trail). <see cref="RunStateFxWorld"/>가 구현.</summary>
    public interface IMvFxProjectiles
    {
        bool TryProjectile(int id, out Vector3 pos);
    }
}
