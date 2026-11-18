using System;
using System.Collections.Generic;
using MV.Run;
using UnityEngine;

namespace MV.View
{
    /// <summary>
    /// 투사체 공간 충돌 검출(<see cref="IProjectileHitQuery"/>)을 Unity Physics로 한다. 판정 형상의 정본 = 오브젝트 프리팹 안 <see cref="RunHitbox"/>의 Collider.
    /// 화면 뷰(흔들림·눌림·쓰러짐이 뷰 루트를 움직이고, 그리기 범위에서만 있음)와 분리해, 프리팹에서 떼어 낸 판정 형상 복사본을 오브젝트마다 두고 시뮬 위치에만 놓는다.
    /// 순서: 시뮬 위치 갱신(이동 AI) → <see cref="Prepare"/>(복사본을 (x, WorldY, z)로 옮기고 <see cref="Physics.SyncTransforms"/> 한 번) → 투사체마다 <see cref="Cast"/> → 시뮬이 피해 반영.
    /// 투사체 높이 = WorldY + <see cref="ProjLayout.ProjY"/>(그림과 같은 높이). 구간 검사 = Raycast(이동 구간) + 시작점 겹침 검사.
    /// 여러 Collider가 한 대상이면 대상 단위로 가장 앞 진입 z만 낸다. 순서는 Physics 반환 순서가 아니라 (진입 z, Id)로 정렬한다.
    /// 판정 형상이 없는 피격 가능 오브젝트는 맞지 않고 오류 로그를 남긴다(다른 크기로 대신하지 않는다).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RunHitWorld : MonoBehaviour, IProjectileHitQuery
    {
        sealed class Entry
        {
            public RunObject Obj;
            public string Address;
            public GameObject Go;
            public int Stamp;
        }

        Func<RunObject, string> _addressOf;
        Func<string, GameObject> _assetOf;
        Transform _templates, _live;
        readonly Dictionary<string, GameObject> _template = new Dictionary<string, GameObject>(StringComparer.Ordinal);
        readonly Dictionary<string, Stack<GameObject>> _pool = new Dictionary<string, Stack<GameObject>>(StringComparer.Ordinal);
        readonly Dictionary<int, Entry> _entries = new Dictionary<int, Entry>();
        readonly Dictionary<Collider, Entry> _owner = new Dictionary<Collider, Entry>();
        readonly Dictionary<Entry, double> _first = new Dictionary<Entry, double>();
        readonly List<int> _drop = new List<int>();
        readonly List<Collider> _cols = new List<Collider>();
        readonly HashSet<string> _reported = new HashSet<string>(StringComparer.Ordinal);
        RaycastHit[] _rays = new RaycastHit[32];
        Collider[] _overlap = new Collider[16];
        double _y;
        int _stamp;
        // 임시 진단(MV-DIAG-GATE, HitDebug): 오브젝트마다 남기는 근접 로그 수 상한, Prepare 요약 간격.
        const int DbgPerObject = 12, DbgSummaryEvery = 300;
        readonly Dictionary<int, int> _dbgNear = new Dictionary<int, int>();
        readonly List<Collider> _dbgCols = new List<Collider>();

        /// <summary>지금 판정 형상을 가진 오브젝트 수(시험·QA).</summary>
        public int LiveCount => _entries.Count;

        /// <summary>오브젝트의 판정 형상 복사본(시험·QA). 없으면 null.</summary>
        public GameObject HitboxOf(int objId) => _entries.TryGetValue(objId, out var e) ? e.Go : null;

        /// <param name="addressOf">오브젝트 → 프리팹 주소(<see cref="ObjectViewFactory.PrefabAddress"/>). 없으면 null.</param>
        /// <param name="assetOf">주소 → 로드된 프리팹 원본(<see cref="ObjectViewFactory.LoadedPrefab"/>). 로드 전이면 null.</param>
        public void Init(Func<RunObject, string> addressOf, Func<string, GameObject> assetOf)
        {
            _addressOf = addressOf ?? throw new ArgumentNullException(nameof(addressOf));
            _assetOf = assetOf ?? throw new ArgumentNullException(nameof(assetOf));
            if (_templates != null) return;
            _templates = new GameObject("HitTemplates").transform;
            _templates.SetParent(transform, false);
            _templates.gameObject.SetActive(false);
            _live = new GameObject("Hitboxes").transform;
            _live.SetParent(transform, false);
        }

        public void Prepare(RunState s)
        {
            if (_addressOf == null) throw new InvalidOperationException("RunHitWorld.Init 전입니다.");
            _stamp++;
            _y = s.WorldY + ProjLayout.ProjY;
            float wy = (float)s.WorldY;
            foreach (var o in s.Objs)
            {
                if (o == null || o.Dead || o.Kind == null || o.Kind.hittable == MV.Data.Hittable.Never) continue;
                if (_entries.TryGetValue(o.Id, out var e) && e.Obj != o) { Release(e); _entries.Remove(o.Id); e = null; }
                if (e == null)
                {
                    e = Acquire(o);
                    if (e == null) continue;
                    _entries[o.Id] = e;
                }
                e.Stamp = _stamp;
                e.Go.transform.SetPositionAndRotation(new Vector3((float)o.X, wy, (float)o.Z), Quaternion.identity);
            }
            _drop.Clear();
            foreach (var kv in _entries) if (kv.Value.Stamp != _stamp) _drop.Add(kv.Key);
            foreach (int id in _drop) { Release(_entries[id]); _entries.Remove(id); _dbgNear.Remove(id); }
            Physics.SyncTransforms();
            if (HitDebug.On && _stamp % DbgSummaryEvery == 1)
            {
                int hittable = 0;
                foreach (var o in s.Objs) if (o != null && !o.Dead && o.Kind != null && o.Kind.hittable != MV.Data.Hittable.Never) hittable++;
                HitDebug.Log($"Prepare #{_stamp}: 피격 가능 오브젝트 {hittable}개, 판정 형상 {_entries.Count}개, 투사체 {s.Projs.Count}개, 투사체 높이 y={_y:F2}, 플레이어 z={s.P.Z:F2}");
            }
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
            foreach (var kv in _first) hits.Add(new ProjectileHit(kv.Key.Obj, kv.Value));
            hits.Sort(start, hits.Count - start, HitOrder.Instance);
            if (HitDebug.On) DebugNear(x, z0, z1);
        }

        /// <summary>임시 진단: 게이트 근처를 지나는 투사체 구간마다 Ray와 Hitbox 위치, Raycast 결과를 남긴다(게이트당 최대 <see cref="DbgPerObject"/>번).</summary>
        void DebugNear(double x, double z0, double z1)
        {
            foreach (var e in _entries.Values)
            {
                if (e.Stamp != _stamp) continue;
                var o = e.Obj;
                var lk = o.Kind.labelKind;
                if (lk != MV.Data.LabelKind.StairGate && lk != MV.Data.LabelKind.CraftGate) continue;
                if (Math.Abs(x - o.X) > 3 || z1 < o.Z - 1.5 || z0 > o.Z + 1.5) continue;
                _dbgNear.TryGetValue(o.Id, out int n);
                if (n >= DbgPerObject) continue;
                _dbgNear[o.Id] = n + 1;
                e.Go.GetComponentsInChildren(true, _dbgCols);
                var sb = new System.Text.StringBuilder();
                foreach (var c in _dbgCols)
                    sb.Append($" [{c.name} 켜짐={c.enabled && c.gameObject.activeInHierarchy} 중심={c.bounds.center:F2} 크기={c.bounds.size:F2} 등록={_owner.ContainsKey(c)}]");
                string res = _first.TryGetValue(e, out double hz) ? $"맞음(진입 z={hz:F2})" : "안 맞음";
                HitDebug.Log($"{o.Kind.key}#{o.Id} 근처 탄: Ray 시작=({x:F2}, {_y:F2}, {z0:F2}) → z {z1:F2} | 게이트 시뮬 위치=({o.X:F2}, {o.Z:F2}) Done={o.Done} | Collider {_dbgCols.Count}개:{sb} | Raycast {res}");
            }
        }

        void Note(Collider c, double z)
        {
            if (c == null || !_owner.TryGetValue(c, out var e) || e.Stamp != _stamp) return;
            if (!_first.TryGetValue(e, out double prev) || z < prev) _first[e] = z;
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

        Entry Acquire(RunObject o)
        {
            string address = _addressOf(o);
            if (address == null)
            {
                Report("noprefab:" + o.Kind.key, $"[RunHitWorld] 피격 가능 종류 {o.Kind.key}에 프리팹이 없어 판정 형상이 없습니다(맞지 않음).");
                return null;
            }
            var tpl = Template(address);
            if (tpl == null) return null;
            GameObject go;
            if (_pool.TryGetValue(address, out var st) && st.Count > 0) go = st.Pop();
            else
            {
                go = Instantiate(tpl, _live, false);
                go.name = tpl.name;
            }
            go.SetActive(true);
            var e = new Entry { Obj = o, Address = address, Go = go };
            go.GetComponentsInChildren(true, _cols);
            foreach (var c in _cols) _owner[c] = e;
            var lk = o.Kind.labelKind;
            if (lk == MV.Data.LabelKind.StairGate || lk == MV.Data.LabelKind.CraftGate)
                HitDebug.Log($"판정 형상 생성: {o.Kind.key}#{o.Id} 프리팹={address} Collider {_cols.Count}개 시뮬 위치=({o.X:F2}, {o.Z:F2})");
            return e;
        }

        void Release(Entry e)
        {
            e.Go.GetComponentsInChildren(true, _cols);
            foreach (var c in _cols) _owner.Remove(c);
            e.Go.SetActive(false);
            if (!_pool.TryGetValue(e.Address, out var st)) _pool[e.Address] = st = new Stack<GameObject>();
            st.Push(e.Go);
        }

        /// <summary>프리팹의 <see cref="RunHitbox"/>들을 프리팹 뿌리 기준 자세 그대로 떼어 낸 판정 형상 원본(주소마다 한 번).</summary>
        GameObject Template(string address)
        {
            if (_template.TryGetValue(address, out var tpl)) return tpl;
            var prefab = _assetOf(address);
            if (prefab == null)
            {
                Report("unloaded:" + address, $"[RunHitWorld] 프리팹 {address}이 아직 로드되지 않아 판정 형상이 없습니다(맞지 않음).");
                return null;
            }
            var boxes = prefab.GetComponentsInChildren<RunHitbox>(true);
            if (boxes.Length == 0)
            {
                Report("nohitbox:" + address, $"[RunHitWorld] 프리팹 {address}에 {nameof(RunHitbox)}(Collider)가 없습니다(맞지 않음).");
                _template[address] = null;
                return null;
            }
            tpl = new GameObject("Hit_" + address);
            tpl.transform.SetParent(_templates, false);
            // 프리팹 뿌리는 뷰 루트 아래 localPosition 0에 놓이고 회전·배율은 프리팹 값 그대로다(ObjectViewFactory.EnsureInstance).
            var pr = prefab.transform;
            var rootM = Matrix4x4.TRS(Vector3.zero, pr.localRotation, pr.localScale) * pr.worldToLocalMatrix;
            foreach (var hb in boxes)
            {
                var mtx = rootM * hb.transform.localToWorldMatrix;
                var copy = Instantiate(hb.gameObject, tpl.transform, false);
                copy.name = hb.name;
                copy.transform.localPosition = mtx.GetColumn(3);
                copy.transform.localRotation = mtx.rotation;
                copy.transform.localScale = mtx.lossyScale;
            }
            _template[address] = tpl;
            return tpl;
        }

        void Report(string key, string message)
        {
            if (_reported.Add(key)) Debug.LogError(message);
        }

        void OnDestroy()
        {
            _entries.Clear();
            _owner.Clear();
        }
    }
}
