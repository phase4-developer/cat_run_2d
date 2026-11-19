using System;
using System.Collections.Generic;
using MV.Run;
using UnityEngine;

namespace MV.View
{
    /// <summary>투사체 판정 구: 반지름(m)과 투사체 위치(x, 높이, z) 기준 중심 오프셋. 기본값 = 반지름 0(선).</summary>
    public readonly struct ProjHitShape
    {
        public readonly float Radius;
        public readonly Vector3 Offset;

        public ProjHitShape(float radius, Vector3 offset)
        {
            Radius = Mathf.Max(0f, radius);
            Offset = offset;
        }
    }

    /// <summary>
    /// 투사체 공간 충돌 검출(<see cref="IProjectileHitQuery"/>)을 Unity Physics로 한다. 하는 일은 물리 질의와 Collider → 오브젝트 연결뿐이다.
    /// 판정 형상 = 오브젝트 프리팹 안 <see cref="RunHitbox"/>의 Collider 그대로(복사본 없음). 등록·해제는 뷰가 오브젝트에 연결·회수될 때 한 번씩 한다(<see cref="RunObjectView"/>).
    /// 순서(시뮬 틱마다): 시뮬 위치 갱신(이동 AI) → <see cref="Prepare"/>(대상 위치 맞추기 콜백 → 살아 있는 등록 대상만 질의 대상으로, <see cref="Physics.SyncTransforms"/> 한 번)
    /// → 투사체마다 <see cref="Cast"/> → 시뮬이 관통·중복 금지·피해를 반영(규칙 정본은 시뮬).
    /// 투사체 높이 = WorldY + <see cref="ProjLayout.ProjY"/>(그림과 같은 높이). 투사체 굵기 = 투사체 프리팹 `Hitbox` 구(<see cref="SetProjectileShapes"/>, MV-HIT-RADIUS).
    /// 구간 검사 = SphereCast(이동 구간 전체를 훑으므로 고속에도 터널링 없음) + 시작점 구 겹침 검사. 반지름 0이면 Raycast(선).
    /// 진입 z = 판정 형상에 처음 닿을 때 투사체 중심 z(구간 시작 z + 이동 거리).
    /// 여러 Collider가 한 대상이면 대상 단위로 가장 앞 진입 z만 낸다. 순서는 Physics 반환 순서가 아니라 (진입 z, Id)로 정렬한다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RunHitWorld : MonoBehaviour, IProjectileHitQuery
    {
        Action<RunState> _placeTargets;
        readonly Dictionary<RunObject, IReadOnlyList<Collider>> _registered = new Dictionary<RunObject, IReadOnlyList<Collider>>();
        readonly Dictionary<Collider, RunObject> _owner = new Dictionary<Collider, RunObject>();
        readonly Dictionary<RunObject, double> _first = new Dictionary<RunObject, double>();
        readonly HashSet<RunObject> _live = new HashSet<RunObject>();
        RaycastHit[] _rays = new RaycastHit[32];
        Collider[] _overlap = new Collider[16];
        double _y;
        ProjHitShape _toy, _spear;

        /// <summary>던진 장난감·창 판정 구(시험·QA).</summary>
        public ProjHitShape ToyShape => _toy;
        public ProjHitShape SpearShape => _spear;

        /// <summary>지난 <see cref="Prepare"/>에서 판정 Collider를 가진 살아 있는 오브젝트 수(시험·QA).</summary>
        public int LiveCount => _live.Count;
        /// <summary>등록된 오브젝트 수(시험·QA).</summary>
        public int RegisteredCount => _registered.Count;

        /// <param name="placeTargets">틱마다 질의 전에 대상(뷰 뿌리)을 그 틱의 시뮬 위치에 놓는다(<see cref="ObjectViewFactory.PlaceHitTargets"/>).</param>
        public void Init(Action<RunState> placeTargets)
        {
            _placeTargets = placeTargets ?? throw new ArgumentNullException(nameof(placeTargets));
        }

        /// <summary>투사체 판정 구(투사체 프리팹 `Hitbox`, <see cref="ProjectileView.HitShape"/>). 넣기 전에는 반지름 0(선).</summary>
        public void SetProjectileShapes(ProjHitShape toy, ProjHitShape spear)
        {
            _toy = toy;
            _spear = spear;
        }

        /// <summary>오브젝트의 판정 Collider 등록(뷰 연결 때). 같은 오브젝트를 다시 등록하면 바꾼다.</summary>
        public void Register(RunObject o, IReadOnlyList<Collider> colliders)
        {
            if (o == null || colliders == null) return;
            _registered[o] = colliders;
        }

        /// <summary>등록 해제(뷰 회수 때).</summary>
        public void Unregister(RunObject o)
        {
            if (o != null) _registered.Remove(o);
        }

        public void Prepare(RunState s)
        {
            if (_placeTargets == null) throw new InvalidOperationException("RunHitWorld.Init 전입니다.");
            _placeTargets(s);
            _owner.Clear();
            _live.Clear();
            _y = s.WorldY + ProjLayout.ProjY;
            foreach (var kv in _registered)
            {
                var o = kv.Key;
                if (o.Dead || o.Kind == null || o.Kind.hittable == MV.Data.Hittable.Never) continue;
                var cols = kv.Value;
                for (int i = 0; i < cols.Count; i++) if (cols[i] != null) _owner[cols[i]] = o;
                _live.Add(o);
            }
            Physics.SyncTransforms();
        }

        public void Cast(RunState s, double x, double z0, double z1, bool spear, List<ProjectileHit> hits)
        {
            _first.Clear();
            var shape = spear ? _spear : _toy;
            float r = shape.Radius;
            var origin = new Vector3((float)x, (float)_y, (float)z0) + shape.Offset;
            // 시작점에서 이미 닿아 있으면(Raycast·SphereCast는 출발점에서 겹친 Collider를 확실히 내지 않는다) 진입 z = z0.
            int m;
            while ((m = Physics.OverlapSphereNonAlloc(origin, r, _overlap, ~0, QueryTriggerInteraction.Collide)) == _overlap.Length)
                _overlap = new Collider[_overlap.Length * 2];
            for (int i = 0; i < m; i++) Note(_overlap[i], z0);
            float len = (float)(z1 - z0);
            if (len > 0)
            {
                int n;
                if (r > 0f)
                {
                    while ((n = Physics.SphereCastNonAlloc(origin, r, Vector3.forward, _rays, len, ~0, QueryTriggerInteraction.Collide)) == _rays.Length)
                        _rays = new RaycastHit[_rays.Length * 2];
                }
                else
                {
                    while ((n = Physics.RaycastNonAlloc(origin, Vector3.forward, _rays, len, ~0, QueryTriggerInteraction.Collide)) == _rays.Length)
                        _rays = new RaycastHit[_rays.Length * 2];
                }
                for (int i = 0; i < n; i++) Note(_rays[i].collider, z0 + _rays[i].distance);
            }
            int start = hits.Count;
            foreach (var kv in _first) hits.Add(new ProjectileHit(kv.Key, kv.Value));
            hits.Sort(start, hits.Count - start, HitOrder.Instance);
        }

        void Note(Collider c, double z)
        {
            if (c == null || !_owner.TryGetValue(c, out var o)) return;
            if (!_first.TryGetValue(o, out double prev) || z < prev) _first[o] = z;
        }

        sealed class HitOrder : IComparer<ProjectileHit>
        {
            public static readonly HitOrder Instance = new HitOrder();

            public int Compare(ProjectileHit a, ProjectileHit b)
            {
                int c = a.Z.CompareTo(b.Z);
                return c != 0 ? c : a.Obj.Id.CompareTo(b.Obj.Id);
            }
        }

        void OnDestroy()
        {
            _registered.Clear();
            _owner.Clear();
            _first.Clear();
            _live.Clear();
        }
    }
}
