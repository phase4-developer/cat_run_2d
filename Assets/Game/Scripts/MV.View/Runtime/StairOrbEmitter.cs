using System.Collections.Generic;
using MV.Run;
using UnityEngine;
using UnityEngine.Pool;

namespace MV.View
{
    /// <summary>
    /// 마법서 탑 오브(<c>Obj_Tower_Orb</c>)의 계단 경험치 오브 내보내기(MV-UI3-STAIRFX 노션 「World Object Attraction」).
    /// 모은 피해가 비워질 때(<see cref="RunEventType.WellFlush"/>, 시뮬이 게이지 수령을 예약하는 순간) 구슬 수(e.A, 시뮬 flushBricks 규칙)만큼
    /// <c>Obj_Stair_Orb</c>(<see cref="StairOrb"/>)를 빌려 탑 오브에서 <c>Orb Spawn Area</c> 범위 안 무작위 자리로 내보낸다. 이동·멈춤·HUD 비행은 오브 부품이 맡는다.
    /// 오브는 탑 뷰 밖(뷰 관리자 뿌리 아래)에 둬서 탑이 회수돼도 끝까지 난다. 풀(<see cref="ObjectPool{T}"/>)과 대여·반환은 이 모듈이 갖는다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class StairOrbEmitter : RunObjectModule
    {
        const string PrefabPath = "Assets/Game/Addressables/Local/BG/Prefab/Object/Obj_Stair_Orb.prefab";

        [Tooltip("계단 경험치 오브 원본(Obj_Stair_Orb 뿌리의 StairOrb). 비면 오브 없이 게이지만 오른다(경고 1회).")]
        [SerializeField] StairOrb orbPrefab;
        [Tooltip("오브가 나오는 자리(탑 오브 그림)")]
        [SerializeField] Transform origin;
        [Tooltip("오브가 퍼질 범위(BoxCollider의 center·size만 읽는다, 판정에 쓰지 않음)")]
        [SerializeField] BoxCollider spawnArea;
        [Tooltip("오브 사이 출발 간격 초(임시)")]
        [SerializeField] float launchGapSec = 0.04f;
        [Tooltip("풀에 남겨 둘 꺼진 오브 수(동시 표시 수 상한이 아니다)")]
        [SerializeField] int poolKeep = 16;

        ObjectPool<StairOrb> _pool;
        readonly List<StairOrb> _all = new List<StairOrb>(16);
        readonly List<StairOrb> _aborted = new List<StairOrb>(4);
        /// 출발 대기(남은 초, 퍼질 자리). 오브는 출발할 때 빌린다(대기 중 엉뚱한 자리에 보이지 않게).
        readonly List<(float wait, Vector3 spread)> _queue = new List<(float, Vector3)>(16);
        Transform _parent;
        Camera _cam;
        System.Random _rng;
        static bool s_missingLogged;

        /// <summary>지금 날고 있는 오브 수(빌려 준 수). 시험·QA.</summary>
        public int ActiveCount => _pool != null ? _pool.CountActive : 0;

        /// <summary>출발 대기 중인 오브 수. 시험·QA.</summary>
        public int QueuedCount => _queue.Count;

        ObjectPool<StairOrb> Pool => _pool ??= new ObjectPool<StairOrb>(Create, o => o.gameObject.SetActive(true), OnRelease, OnDestroyOrb,
            collectionCheck: false, defaultCapacity: 8, maxSize: Mathf.Max(1, poolKeep));

        StairOrb Create()
        {
            var o = Instantiate(orbPrefab, _parent != null ? _parent : transform.root);
            o.name = orbPrefab.name;
            _all.Add(o);
            return o;
        }

        static void OnRelease(StairOrb o)
        {
            if (o == null) return;
            o.ResetLook();
            o.gameObject.SetActive(false);
        }

        void OnDestroyOrb(StairOrb o)
        {
            _all.Remove(o);
            if (o != null) Kill(o.gameObject);
        }

        static void Kill(GameObject go)
        {
            if (Application.isPlaying) Destroy(go);
            else DestroyImmediate(go);
        }

        public override void OnBind(RunObjectView view)
        {
            _parent = view.transform.parent != null ? view.transform.parent : view.transform;
            _rng = view.Ctx?.FxRng;
            var camT = view.Ctx?.Cam;
            _cam = camT != null ? camT.GetComponent<Camera>() : null;
            CollectAborted();
        }

        public override void OnTick(RunObjectView view, in ObjectFrame f)
        {
            CollectAborted();
            StepQueue(Time.unscaledDeltaTime);
        }

        public override void OnUnbind(RunObjectView view)
        {
            // 출발 대기 중인 오브는 지금 보낸다(탑이 회수돼도 경험치 오브는 끝까지 간다).
            StepQueue(float.PositiveInfinity);
        }

        public override void OnRunEvent(RunObjectView view, in RunEvent e)
        {
            if (e.Type != RunEventType.WellFlush) return;
            if (orbPrefab == null)
            {
                if (!s_missingLogged)
                {
                    s_missingLogged = true;
                    Debug.LogWarning($"[StairOrbEmitter] 계단 경험치 오브 원본 없음: {name}의 orbPrefab에 {PrefabPath}(StairOrb)를 연결하세요. 오브 없이 게이지만 오릅니다.", this);
                }
                return;
            }
            if (_parent == null) OnBind(view);
            CollectAborted();
            int n = System.Math.Max(1, e.A);
            for (int i = 0; i < n; i++) _queue.Add((Mathf.Max(0, launchGapSec) * i, SpreadPoint()));
            StairOrbFlight.RaiseLaunched(n);
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
            Pool.Get().Launch(from, q.spread, _cam, OnOrbDone);
        }

        void OnOrbDone(StairOrb o, bool aborted)
        {
            StairOrbFlight.RaiseArrived();
            // 꺼지는 중(OnDisable)에는 다시 끌 수 없어 다음 프레임에 돌려받는다.
            if (aborted) { if (o != null && !_aborted.Contains(o)) _aborted.Add(o); }
            else if (_pool != null) _pool.Release(o);
        }

        void CollectAborted()
        {
            if (_aborted.Count == 0 || _pool == null) return;
            foreach (var o in _aborted)
            {
                if (o == null) { _all.Remove(o); continue; }
                _pool.Release(o);
            }
            _aborted.Clear();
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

        /// <summary>모든 오브를 치운다(지워질 때). 대기·비행 중인 오브도 도착으로 친다(게이지가 기다리지 않게).</summary>
        public void ReleaseAll()
        {
            foreach (var q in _queue) StairOrbFlight.RaiseArrived();
            _queue.Clear();
            for (int i = _all.Count - 1; i >= 0; i--)
            {
                var o = _all[i];
                if (o == null) continue;
                o.Abort();
                Kill(o.gameObject);
            }
            _all.Clear();
            _aborted.Clear();
            _pool = null;
        }
    }
}
