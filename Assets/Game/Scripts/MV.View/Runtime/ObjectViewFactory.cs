using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using jjevol;
using MV.Data;
using MV.Run;
using UnityEngine;
using UnityEngine.Rendering;

namespace MV.View
{
    /// <summary>불러온 오브젝트 프리팹 원본과 반환 손잡이(<see cref="ObjectViewFactory.PrefabLoader"/> 결과). <see cref="Release"/>가 null이면 반환할 것이 없다.</summary>
    public readonly struct PrefabHandle
    {
        public readonly GameObject Asset;
        public readonly IDisposable Release;

        public PrefabHandle(GameObject asset, IDisposable release)
        {
            Asset = asset;
            Release = release;
        }
    }

    /// <summary>
    /// 런 오브젝트 뷰 관리자(02 §2.10, 03 §3.5.3): 생성·시뮬 오브젝트 연결·풀 대여/반환·회수만 한다. 오브젝트별 그림·동작은 뷰(<see cref="RunObjectView"/>)와
    /// 프리팹에 붙은 모듈(<see cref="RunObjectModule"/>)이 정하고, 이 관리자는 종류를 보고 동작을 고르지 않는다.
    /// <see cref="Sync"/>: 그리기 범위(PZ+objDrawBack ~ PZ+objDrawFront)의 살아 있는 오브젝트에 뷰를 붙이고(프리팹 주소별 풀), 뷰 뿌리를 시뮬 위치에 놓고,
    /// 빠진 뷰는 모듈이 회수를 막지 않을 때(<see cref="RunObjectView.ReleaseReady"/>) 돌려준다. 뷰 뿌리 = SortingGroup + <see cref="DepthSortable"/>(순위 Object, 탐침 +sort.objProbeY, 편향 105.sortBias).
    /// 프리팹 주소가 없거나(305·105 빈칸) 로드에 실패한 오브젝트는 만들지 않고 그리지 않으며, 표 위치·주소를 경고 로그로 한 번 알린다(대체 그림 없음).
    /// 투사체 판정(<see cref="RunHitWorld"/>)은 시뮬 틱마다 <see cref="PlaceHitTargets"/>로 범위 안 뷰를 만들고 뿌리를 그 틱의 시뮬 위치에 놓는다(등록·해제는 뷰 연결·회수).
    /// </summary>
    [DefaultExecutionOrder(55)]
    public sealed class ObjectViewFactory : MonoBehaviour
    {
        public const string WorldLayer = "World";
        public const string Star = "Fx_Star4";

        [SerializeField] Material _spriteMaterial;
        /// <summary>월드 스프라이트 공유 머티리얼(고양이 뷰가 프리팹 값이 없을 때 빌린다).</summary>
        public Material SpriteMaterial => _spriteMaterial;

        /// <summary>글자판을 모두 숨긴다(시험: TMP 글꼴 없이 구조만 볼 때). 글자 내용은 늘 <see cref="ObjectLabel.Of"/>(105.labelKind).</summary>
        public bool HideLabels { get => _ctx.HideLabels; set => _ctx.HideLabels = value; }

        /// <summary>결계 블록 글자판 「+골드」에 곱할 골드 배율(정본 `goldMul()` L1492). 통합 코드가 런 상태에서 매 프레임 넣는다.</summary>
        public double GoldMul { get => _ctx.GoldMul; set => _ctx.GoldMul = value; }
        /// <summary>마법서 탑 게이지 단계(<see cref="ObjectViewContext.GaugeLv"/>).</summary>
        public int GaugeLv { get => _ctx.GaugeLv; set => _ctx.GaugeLv = value; }

        /// <summary>무기문 위 카드 묶음 그림 이름(103_item.icon, Atlas_UI_Icon). 통합 코드가 런 상태에서 매 프레임 넣는다.</summary>
        public string GateWeaponIcon { get => _ctx.GateWeaponIcon; set => _ctx.GateWeaponIcon = value; }
        public string GateWeaponFrameAtlas { get => _ctx.GateWeaponFrameAtlas; set => _ctx.GateWeaponFrameAtlas = value; }
        public string GateWeaponFrame { get => _ctx.GateWeaponFrame; set => _ctx.GateWeaponFrame = value; }
        /// <summary>게이트 등급 색(<see cref="ObjectViewContext.GateWeaponColor"/>).</summary>
        public string GateWeaponColor { get => _ctx.GateWeaponColor; set => _ctx.GateWeaponColor = value; }

        /// <summary>오브젝트 프리팹 로더(주소 → 원본). 기본은 Addressables. 시험은 에셋 원본을 바로 주거나 로드 없이 뷰 구조만 본다.</summary>
        public Func<string, CancellationToken, UniTask<PrefabHandle>> PrefabLoader = LoadAddressable;

        static async UniTask<PrefabHandle> LoadAddressable(string address, CancellationToken ct)
        {
            var lease = await AddressableScope.GetPrefab(address, ct);
            return new PrefabHandle(lease != null ? lease.Asset : null, lease);
        }

        readonly ObjectViewContext _ctx = new ObjectViewContext();
        readonly Dictionary<int, RunObjectView> _views = new Dictionary<int, RunObjectView>(64);
        readonly Dictionary<string, Stack<RunObjectView>> _prefabPool = new Dictionary<string, Stack<RunObjectView>>();
        readonly Dictionary<string, PrefabHandle> _prefabs = new Dictionary<string, PrefabHandle>();
        readonly HashSet<string> _prefabLoading = new HashSet<string>();
        readonly List<int> _drop = new List<int>(16);
        readonly CancellationTokenSource _cts = new CancellationTokenSource();
        MvTables _tables;
        string _stageMap;
        int _stamp;
        double _lastMs = -1;
        /// <summary>마지막 <see cref="Sync"/>의 카메라 기준 z(PZ). 그 전에는 NaN.</summary>
        double _lastPz = double.NaN;

        public int ViewCount => _views.Count;

        public void Init(ViewConfig cfg, Transform camera, WorldSpriteCache sprites, string fxAtlas)
        {
            _ctx.Cfg = cfg;
            _ctx.Cam = camera;
            _ctx.Sprites = sprites;
            _ctx.FxAtlas = fxAtlas;
            _ctx.SpriteMaterial = _spriteMaterial;
        }

        /// <summary>
        /// 투사체 판정 연결: 뷰가 연결·회수될 때 그 프리팹 Hitbox를 <paramref name="hits"/>에 등록·해제하고,
        /// <paramref name="hits"/>는 시뮬 틱마다 <see cref="PlaceHitTargets"/>를 부른 뒤 질의한다.
        /// </summary>
        public void ConnectHits(RunHitWorld hits)
        {
            _ctx.Hits = hits;
            if (hits != null) hits.Init(PlaceHitTargets);
        }

        /// <summary>테마 아틀라스(`304_theme.worldAtlas`, 미리 로드). 뷰는 다시 만든다.</summary>
        public void SetTheme(ThemeData theme)
        {
            _ctx.Atlas = theme?.worldAtlas;
            Clear();
        }

        /// <summary>
        /// 지금 스테이지 맵(`302_area.stageMap`). 오브젝트 프리팹은 `305_stageObject`(이 맵 행)가 먼저, 없으면 `105_objectKind.prefab`.
        /// 쓸 프리팹을 미리 불러 두고 뷰는 다시 만든다.
        /// </summary>
        public void SetStage(MvTables tables, string stageMap)
        {
            _tables = tables;
            _stageMap = stageMap;
            Clear();
            if (tables == null) return;
            foreach (var k in tables.ObjectKind.Datas) Preload(k.prefab);
            foreach (var s in tables.StageObject.Datas) if (s.stageMap == stageMap) Preload(s.prefab);
        }

        /// <summary>오브젝트의 프리팹 주소: `305_stageObject`(맵·종류·보상) → `105_objectKind.prefab`. 둘 다 없으면 null(그리지 않음).</summary>
        public static string PrefabFor(MvTables tables, string stageMap, RunObject o) =>
            o?.Kind == null ? null : StageObjectData.PrefabFor(tables, stageMap, o.Kind, o.IsField ? RewardOf(o.Rew) : StageObjectReward.Any);

        /// <summary>런 칸 보상 → 305 reward(이름 1:1). 이름이 없으면 Any.</summary>
        public static StageObjectReward RewardOf(FieldReward r) => Enum.TryParse(r.ToString(), out StageObjectReward x) ? x : StageObjectReward.Any;

        /// <summary>시험·QA: 오브젝트 뷰가 쓰는 프리팹 주소와 인스턴스(로드 전이면 null).</summary>
        public bool TryPrefabView(int objId, out string address, out GameObject instance)
        {
            address = null;
            instance = null;
            if (!_views.TryGetValue(objId, out var v)) return false;
            address = v.Prefab;
            instance = v.Instance;
            return true;
        }

        /// <summary>오브젝트 하나의 뷰 뿌리(시험·QA용). 없으면 null.</summary>
        public GameObject ViewOf(int objId) => _views.TryGetValue(objId, out var v) ? v.gameObject : null;

        void Preload(string address)
        {
            if (string.IsNullOrEmpty(address) || _prefabs.ContainsKey(address) || _prefabLoading.Contains(address)) return;
            _prefabLoading.Add(address);
            LoadPrefabAsync(address, _cts.Token).Forget();
        }

        async UniTaskVoid LoadPrefabAsync(string address, CancellationToken ct)
        {
            PrefabHandle h = default;
            try { h = await PrefabLoader(address, ct); }
            catch (OperationCanceledException) { }
            if (this == null || ct.IsCancellationRequested) { h.Release?.Dispose(); return; }
            _prefabLoading.Remove(address);
            if (h.Asset == null)
            {
                // 실패는 다시 시도하지 않는다(표 주소 오류를 드러낸다). 그 오브젝트는 그리지 않는다.
                Debug.LogWarning($"[ObjectViewFactory] 오브젝트 프리팹 로드 실패 — 주소 {address}: {AddressHint(address)}");
                h.Release?.Dispose();
                _prefabs[address] = default;
                return;
            }
            _prefabs[address] = h;
        }

        /// <summary>프리팹이 로드됐으면 인스턴스를 뷰 뿌리 아래에 만들어 뷰에 붙인다. 로드 전이면 아무것도 안 그린다.</summary>
        void EnsureInstance(RunObjectView v)
        {
            if (v.Instance != null || v.Prefab == null) return;
            if (!_prefabs.TryGetValue(v.Prefab, out var h)) { Preload(v.Prefab); return; }
            if (h.Asset == null) return;
            v.AttachInstance(Instantiate(h.Asset, v.transform, false));
        }

        void OnDestroy()
        {
            _cts.Cancel();
            _cts.Dispose();
            foreach (var h in _prefabs.Values) h.Release?.Dispose();
            _prefabs.Clear();
        }

        public void Clear()
        {
            foreach (var v in _views.Values) Release(v);
            _views.Clear();
        }

        /// <summary>이 관리자가 이미 알린 「프리팹 주소 없음」(맵·종류·보상마다 한 번, 실행 순서와 무관).</summary>
        readonly HashSet<string> _noPrefab = new HashSet<string>();

        /// <summary>프리팹 위치 안내: Addressables 주소 = 파일 이름(확장자 제외), 오브젝트 프리팹은 앱에 넣는 Local 그룹 폴더 아래에 둔다(10 §10.6).</summary>
        public static string AddressHint(string address) =>
            $"Assets/Game/Addressables/Local/<분류>/…/{address}.prefab 이 Local_<분류> 그룹에 있어야 합니다(주소 = 파일 이름)";

        bool InRange(RunObject o, double pz) => o.Z >= pz + _ctx.Cfg.ObjDrawBack && o.Z <= pz + _ctx.Cfg.ObjDrawFront;

        /// <summary>오브젝트의 뷰(없거나 같은 번호의 다른 오브젝트면 새로 연결). 오브젝트 번호는 런마다 1부터 다시 쓴다. 프리팹 주소가 없으면 null(만들지 않음).</summary>
        RunObjectView ViewFor(RunObject o)
        {
            if (_views.TryGetValue(o.Id, out var v))
            {
                if (v.Obj == o) return v;
                Release(v);
            }
            string prefab = PrefabFor(_tables, _stageMap, o);
            if (prefab == null) { ReportNoPrefab(o); _views.Remove(o.Id); return null; }
            _views[o.Id] = v = Show(o, prefab);
            return v;
        }

        static void Place(RunObjectView v, RunObject o, double groundY) => v.transform.position = new Vector3((float)o.X, (float)groundY, (float)o.Z);

        /// <summary>
        /// 매 프레임: 그리기 범위의 살아 있는 오브젝트에 뷰를 맞춘다. <paramref name="pz"/> = 카메라 기준 z(PZ),
        /// <paramref name="playerZ"/> = 플레이어 z(관문 흐려짐), <paramref name="groundY"/> = 지면 높이.
        /// </summary>
        public void Sync(IReadOnlyList<RunObject> objs, double pz, double playerZ, double groundY)
        {
            if (_ctx.Cfg == null || objs == null) return;
            _stamp++;
            _lastPz = pz;
            double nowMs = Time.realtimeSinceStartupAsDouble * 1000.0;
            double dt = _lastMs < 0 ? 0 : Math.Max(0, Math.Min(0.1, (nowMs - _lastMs) / 1000.0));
            _lastMs = nowMs;
            var frame = new ObjectFrame(nowMs / 1000.0, dt, playerZ);
            _ctx.WallRows.Collect(objs);
            for (int i = 0; i < objs.Count; i++)
            {
                var o = objs[i];
                if (o == null || o.Dead || !InRange(o, pz)) continue;
                var v = ViewFor(o);
                if (v == null) continue;
                v.Stamp = _stamp;
                Place(v, o, groundY);
                EnsureInstance(v);
                v.Tick(frame);
            }
            _drop.Clear();
            foreach (var kv in _views)
            {
                var v = kv.Value;
                if (v.Stamp == _stamp) continue;
                // 범위·목록에서 빠진 뷰: 모듈이 회수를 막으면(종이 쓰러짐 등) 그 연출만 이어 가고, 아니면 돌려준다.
                if (!v.ReleaseReady) { v.Tick(frame); continue; }
                _drop.Add(kv.Key);
            }
            for (int i = 0; i < _drop.Count; i++) { Release(_views[_drop[i]]); _views.Remove(_drop[i]); }
        }

        /// <summary>
        /// 투사체 판정 직전(시뮬 틱마다, <see cref="RunHitWorld.Prepare"/>): 그리기 범위의 살아 있는 피격 가능 오브젝트에 뷰를 만들고(틱 사이에 생긴 오브젝트도)
        /// 뷰 뿌리를 그 틱의 시뮬 위치 (x, WorldY, z)에 놓는다. 그림 연출은 하지 않는다(화면 갱신은 <see cref="Sync"/>).
        /// </summary>
        public void PlaceHitTargets(RunState s)
        {
            if (_ctx.Cfg == null || s == null || double.IsNaN(_lastPz)) return;
            for (int i = 0; i < s.Objs.Count; i++)
            {
                var o = s.Objs[i];
                if (o == null || o.Dead || o.Kind == null || o.Kind.hittable == Hittable.Never || !InRange(o, _lastPz)) continue;
                var v = ViewFor(o);
                if (v == null) continue;
                v.Stamp = _stamp;
                EnsureInstance(v);
                Place(v, o, s.WorldY);
            }
        }

        /// <summary>
        /// 런 이벤트 → 그 오브젝트 뷰의 모듈(06 §6.13). 통합 코드가 <see cref="Sync"/> 전에 같은 프레임의 이벤트 목록을 넘긴다.
        /// 오브젝트를 가리키는 이벤트(<see cref="RunEvent.TargetsObjectId"/>, 규칙 정본은 RunEvent)만 넘기고, 뷰가 없는(그리기 범위 밖) 오브젝트의 이벤트는 버린다.
        /// 그 이벤트에 무엇을 할지는 모듈이 정한다.
        /// </summary>
        public void OnRunEvents(IReadOnlyList<RunEvent> events)
        {
            if (events == null) return;
            for (int i = 0; i < events.Count; i++)
            {
                var e = events[i];
                if (!e.TargetsObjectId || !_views.TryGetValue(e.ObjId, out var v) || v.Obj == null) continue;
                v.OnRunEvent(e);
            }
        }

        void Release(RunObjectView v)
        {
            v.Unbind();
            v.gameObject.SetActive(false);
            if (!_prefabPool.TryGetValue(v.Prefab, out var ps)) _prefabPool[v.Prefab] = ps = new Stack<RunObjectView>();
            ps.Push(v);
        }

        /// <summary>프리팹 주소가 없는 오브젝트: 만들지 않고, 어느 표 칸을 채워야 하는지 한 번 알린다.</summary>
        void ReportNoPrefab(RunObject o)
        {
            string kind = o.Kind?.key ?? "(종류 없음)";
            string reward = o.IsField ? RewardOf(o.Rew).ToString() : StageObjectReward.Any.ToString();
            if (!_noPrefab.Add(_stageMap + "|" + kind + "|" + reward)) return;
            Debug.LogWarning($"[ObjectViewFactory] 오브젝트 프리팹 주소가 없어 만들지 않습니다 — 종류 {kind}, 스테이지 맵 {_stageMap ?? "(없음)"}, 보상 {reward}. " +
                $"{StageObjectData.Table}(stageMap·kind·reward) 행의 prefab 또는 {ObjectKindData.Table}.prefab에 주소를 넣고, {AddressHint("<주소>")}", this);
        }

        RunObjectView Show(RunObject o, string prefab)
        {
            Stack<RunObjectView> st;
            RunObjectView v = _prefabPool.TryGetValue(prefab, out st) && st.Count > 0 ? st.Pop() : Create(prefab);
            v.Stamp = _stamp;
            v.gameObject.SetActive(true);
            v.Bind(o);
            EnsureInstance(v);
            v.Ds.Refresh();
            return v;
        }

        /// <summary>새 뷰 뿌리: SortingGroup + DepthSortable + <see cref="RunObjectView"/>. 그림·동작은 프리팹 인스턴스(<see cref="EnsureInstance"/>)의 모듈이 맡는다.</summary>
        RunObjectView Create(string prefab)
        {
            var go = new GameObject(prefab);
            go.SetActive(false);
            go.transform.SetParent(transform, false);
            var sg = go.AddComponent<SortingGroup>();
            sg.sortingLayerName = WorldLayer;
            var ds = go.AddComponent<DepthSortable>();
            ds.Rank = DepthMath.RankObject;
            var v = go.AddComponent<RunObjectView>();
            v.Setup(_ctx, prefab);
            return v;
        }
    }
}
