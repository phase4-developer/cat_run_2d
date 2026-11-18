using System;
using System.Collections.Generic;
using MV.Run;
using UnityEngine;

namespace MV.View
{
    /// <summary>
    /// 투사체 공간 충돌 검출(<see cref="IProjectileHitQuery"/>)을 Unity Physics로 한다. 판정 형상 = 화면에 있는 오브젝트 프리팹 안 <see cref="RunHitbox"/>의 Collider 그대로다(복사본 없음).
    /// 오브젝트 연출(흔들림·눌림·가라앉기·쓰러짐)은 프리팹 안 빌보드 묶음만 움직이므로 Hitbox는 프리팹 뿌리 = 시뮬 위치에 있다(<see cref="ObjectViewFactory"/>).
    /// 순서: 시뮬 위치 갱신(이동 AI) → <see cref="Prepare"/>(오브젝트 뿌리를 (x, WorldY, z)로 맞추고 <see cref="Physics.SyncTransforms"/> 한 번) → 투사체마다 <see cref="Cast"/> → 시뮬이 피해 반영.
    /// 투사체 높이 = WorldY + <see cref="ProjLayout.ProjY"/>(그림과 같은 높이). 구간 검사 = Raycast(이동 구간) + 시작점 겹침 검사.
    /// 여러 Collider가 한 대상이면 대상 단위로 가장 앞 진입 z만 낸다. 순서는 Physics 반환 순서가 아니라 (진입 z, Id)로 정렬한다.
    /// 프리팹이 없거나 프리팹에 Hitbox가 없는 피격 가능 오브젝트는 맞지 않고 오류 로그를 남긴다(다른 크기로 대신하지 않는다).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RunHitWorld : MonoBehaviour, IProjectileHitQuery
    {
        /// <summary>오브젝트 → 판정 Collider(뿌리를 시뮬 위치에 맞춘 뒤). 그리기 범위 밖·로드 전이면 null, 프리팹에 Hitbox가 없으면 빈 목록.</summary>
        public delegate IReadOnlyList<Collider> HitShapeOf(RunObject o, double worldY);

        HitShapeOf _shapeOf;
        Func<RunObject, string> _addressOf;
        readonly Dictionary<Collider, RunObject> _owner = new Dictionary<Collider, RunObject>();
        readonly Dictionary<RunObject, double> _first = new Dictionary<RunObject, double>();
        readonly HashSet<RunObject> _live = new HashSet<RunObject>();
        readonly HashSet<string> _reported = new HashSet<string>(StringComparer.Ordinal);
        RaycastHit[] _rays = new RaycastHit[32];
        Collider[] _overlap = new Collider[16];
        double _y;

        /// <summary>지난 <see cref="Prepare"/>에서 판정 Collider를 가진 오브젝트 수(시험·QA).</summary>
        public int LiveCount => _live.Count;

        /// <param name="shapeOf">오브젝트 → 프리팹 안 판정 Collider(<see cref="ObjectViewFactory.HitShape"/>).</param>
        /// <param name="addressOf">오브젝트 → 프리팹 주소(<see cref="ObjectViewFactory.PrefabAddress"/>). 프리팹 없는 종류를 알리는 데만 쓴다.</param>
        public void Init(HitShapeOf shapeOf, Func<RunObject, string> addressOf)
        {
            _shapeOf = shapeOf ?? throw new ArgumentNullException(nameof(shapeOf));
            _addressOf = addressOf ?? throw new ArgumentNullException(nameof(addressOf));
        }

        public void Prepare(RunState s)
        {
            if (_shapeOf == null) throw new InvalidOperationException("RunHitWorld.Init 전입니다.");
            _owner.Clear();
            _live.Clear();
            _y = s.WorldY + ProjLayout.ProjY;
            foreach (var o in s.Objs)
            {
                if (o == null || o.Dead || o.Kind == null || o.Kind.hittable == MV.Data.Hittable.Never) continue;
                var cols = _shapeOf(o, s.WorldY);
                if (cols == null)
                {
                    if (_addressOf(o) == null)
                        Report("noprefab:" + o.Kind.key, $"[RunHitWorld] 피격 가능 종류 {o.Kind.key}에 프리팹이 없어 판정 형상이 없습니다(맞지 않음).");
                    continue;
                }
                if (cols.Count == 0)
                {
                    Report("nohitbox:" + o.Kind.key, $"[RunHitWorld] 프리팹 {_addressOf(o)}에 {nameof(RunHitbox)}(Collider)가 없습니다(맞지 않음).");
                    continue;
                }
                for (int i = 0; i < cols.Count; i++) if (cols[i] != null) _owner[cols[i]] = o;
                _live.Add(o);
            }
            Physics.SyncTransforms();
        }

        public void Cast(RunState s, double x, double z0, double z1, List<ProjectileHit> hits)
        {
            _first.Clear();
            var origin = new Vector3((float)x, (float)_y, (float)z0);
            // 시작점이 이미 형상 안이면(Raycast는 출발점을 품은 Collider를 내지 않는다) 진입 z = z0.
            int m;
            while ((m = Physics.OverlapSphereNonAlloc(origin, 0f, _overlap, ~0, QueryTriggerInteraction.Collide)) == _overlap.Length)
                _overlap = new Collider[_overlap.Length * 2];
            for (int i = 0; i < m; i++) Note(_overlap[i], z0);
            float len = (float)(z1 - z0);
            if (len > 0)
            {
                int n;
                while ((n = Physics.RaycastNonAlloc(origin, Vector3.forward, _rays, len, ~0, QueryTriggerInteraction.Collide)) == _rays.Length)
                    _rays = new RaycastHit[_rays.Length * 2];
                for (int i = 0; i < n; i++) Note(_rays[i].collider, _rays[i].point.z);
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

        void Report(string key, string message)
        {
            if (_reported.Add(key)) Debug.LogError(message);
        }

        void OnDestroy()
        {
            _owner.Clear();
            _first.Clear();
            _live.Clear();
        }
    }
}
