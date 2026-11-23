using System.Collections.Generic;
using MV.Run;
using UnityEngine;

namespace MV.View
{
    /// <summary>
    /// 마법서 탑 오브(<c>Obj_Tower_Orb</c>)의 계단 경험치 오브 내보내기(MV-UI3-STAIRFX 노션 「World Object Attraction」).
    /// 모은 피해가 비워질 때(<see cref="RunEventType.WellFlush"/>, 시뮬이 게이지 수령을 예약하는 순간) 구슬 수(e.A, 시뮬 flushBricks 규칙)만큼
    /// 탑 오브에서 <c>Orb Spawn Area</c> 범위 안 무작위 자리로 오브 출발을 요청한다(<see cref="StairOrbFlight.TryLaunch"/>).
    /// 오브(<c>Obj_Stair_Orb</c>)는 MV.Fx가 Framework 비행으로 UI Effect 루트 아래에 재생한다(MV-UI3-STAIRFX2·STAIRFX3).
    /// 수령 번호(e.B, 시뮬 도착으로 받기)를 맡고(<see cref="StairOrbFlight.Claim"/>) 오브마다 실어 보낸다. 오브 하나가 도착(또는 도착 못 하고 끝남)할 때마다
    /// 그 번호가 시뮬로 가서 실제 게이지가 한 몫 오른다. 출발하지 못한 오브(재생기 거절·탑 회수 때 대기분)는 이 모듈이 바로 도착으로 알린다.
    /// 이 모듈은 출발 간격 대기와 퍼질 자리만 정한다. 탑이 회수돼도 이미 나간 오브는 UI 쪽에서 끝까지 난다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class StairOrbEmitter : RunObjectModule
    {
        const string PrefabPath = "Assets/Game/Addressables/Local/BG/Prefab/Object/Obj_Stair_Orb.prefab";

        [Tooltip("계단 경험치 오브 프리팹 Addressables 주소(" + PrefabPath + ", EffectManager 키)")]
        [SerializeField] string orbKey = "Obj_Stair_Orb";
        [Tooltip("오브를 놓는 UI 캔버스(Effect 루트) 정렬 순서에 더할 값. 그 캔버스 위에 그려지게 1 이상.")]
        [SerializeField] int sortingOffset = 1;
        [Tooltip("오브가 나오는 자리(탑 오브 그림)")]
        [SerializeField] Transform origin;
        [Tooltip("오브가 퍼질 범위(BoxCollider의 center·size만 읽는다, 판정에 쓰지 않음)")]
        [SerializeField] BoxCollider spawnArea;
        [Tooltip("오브 사이 출발 간격 초(임시)")]
        [SerializeField] float launchGapSec = 0.04f;

        /// 출발 대기(남은 초, 퍼질 자리, 런 번호, 수령 번호).
        readonly List<(float wait, Vector3 spread, int run, int receiveId)> _queue = new List<(float, Vector3, int, int)>(16);
        Camera _cam;
        System.Random _rng;
        bool _bound;
        static bool s_noPlayerLogged;

        /// <summary>출발 대기 중인 오브 수. 시험·QA.</summary>
        public int QueuedCount => _queue.Count;

        public string OrbKey => orbKey;

        public override void OnBind(RunObjectView view)
        {
            _bound = true;
            _rng = view.Ctx?.FxRng;
            var camT = view.Ctx?.Cam;
            _cam = camT != null ? camT.GetComponent<Camera>() : null;
        }

        public override void OnTick(RunObjectView view, in ObjectFrame f) => StepQueue(Time.deltaTime);   // 게임 시간(MV-CLOCK-3)

        public override void OnUnbind(RunObjectView view)
        {
            // 출발 대기 중인 오브는 지금 보낸다(탑이 회수돼도 경험치 오브는 끝까지 간다).
            StepQueue(float.PositiveInfinity);
            _bound = false;
        }

        public override void OnRunEvent(RunObjectView view, in RunEvent e)
        {
            if (e.Type != RunEventType.WellFlush) return;
            if (StairOrbFlight.Launcher == null || string.IsNullOrEmpty(orbKey))
            {
                if (!s_noPlayerLogged)
                {
                    s_noPlayerLogged = true;
                    Debug.LogWarning($"[StairOrbEmitter] 계단 경험치 오브를 재생할 수 없음: 재생기(MV.Fx StairOrbFx) 미설치이거나 {name}의 orbKey가 비었습니다({PrefabPath}). 오브 없이 게이지만 오릅니다.", this);
                }
                return; // 맡지 않은 수령은 런 프레임이 바로 도착으로 친다
            }
            if (!_bound) OnBind(view);
            int n = System.Math.Max(1, e.A);
            int run = StairOrbFlight.Run;
            StairOrbFlight.Claim(e.B);
            for (int i = 0; i < n; i++) _queue.Add((Mathf.Max(0, launchGapSec) * i, SpreadPoint(), run, e.B));
            StepQueue(0);
        }

        /// <summary>대기 시간을 줄이고 다 된 오브를 출발시킨다. 시험은 직접 부른다.</summary>
        public void StepQueue(float dt)
        {
            if (_queue.Count == 0) return;
            var from = origin != null ? origin.position : transform.position;
            for (int i = 0; i < _queue.Count; i++)
            {
                var q = _queue[i];
                q.wait -= dt;
                _queue[i] = q;
                if (q.wait <= 0) Launch(i--, from);
            }
        }

        void Launch(int index, Vector3 from)
        {
            var q = _queue[index];
            _queue.RemoveAt(index);
            var req = new StairOrbFlight.OrbLaunch
            {
                From = from, Spread = q.spread, WorldCam = _cam, Key = orbKey, SortingOffset = sortingOffset,
                Run = q.run, ReceiveId = q.receiveId,
            };
            // 재생기가 받지 못하면(UI 판 없음 등) 오브 없이 도착으로 친다. 게이지가 기다리지 않게.
            if (!StairOrbFlight.TryLaunch(req)) StairOrbFlight.RaiseArrived(q.run, q.receiveId);
        }

        /// <summary>Orb Spawn Area 상자 안 무작위 월드 점(상자가 없으면 나오는 자리).</summary>
        Vector3 SpreadPoint()
        {
            if (spawnArea == null) return origin != null ? origin.position : transform.position;
            var c = spawnArea.center;
            var s = spawnArea.size;
            var local = new Vector3(c.x + (Rnd() - 0.5f) * s.x, c.y + (Rnd() - 0.5f) * s.y, c.z + (Rnd() - 0.5f) * s.z);
            return spawnArea.transform.TransformPoint(local);
        }

        float Rnd() => _rng != null ? (float)_rng.NextDouble() : Random.value;

        void OnDestroy() => ReleaseAll();

        /// <summary>지워질 때: 아직 출발하지 않은 오브는 도착으로 친다(게이지가 기다리지 않게). 날고 있는 오브는 UI 쪽에서 끝까지 난다.</summary>
        public void ReleaseAll()
        {
            foreach (var q in _queue) StairOrbFlight.RaiseArrived(q.run, q.receiveId);
            _queue.Clear();
        }
    }
}
