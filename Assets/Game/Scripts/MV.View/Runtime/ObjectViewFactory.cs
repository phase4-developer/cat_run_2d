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
    /// <summary>오브젝트 그림 모양(정본 `drawObj` L3099 분기).</summary>
    public enum ObjectShape : byte { Single, Junk, RollGate, WarpGate, Well, Stair, CoinPile, CatnipPot, Mouse }

    /// <summary>
    /// 런 오브젝트 → 스프라이트 뷰(02 §2.10, 03 §3.5.3). <see cref="Sync"/>가 매 프레임 그리기 범위(PZ+objDrawBack ~ PZ+objDrawFront)의
    /// 살아 있는 오브젝트에 뷰를 붙이고(모양별 풀), 빠진 것은 돌려준다. 뷰 루트 = SortingGroup + <see cref="DepthSortable"/>
    /// (순위 Object, 탐침 +sort.objProbeY, 편향 105.sortBias). 우물 벽돌은 그룹 안에서 <see cref="DepthMath.RankFarFirst"/>로 정렬.
    /// </summary>
    [DefaultExecutionOrder(55)]
    public sealed class ObjectViewFactory : MonoBehaviour
    {
        public const string WorldLayer = "World";
        public const string StairArrow = "World_Obj_Stair_Arrow", Star = "Fx_Star4";
        /// <summary>글자판 높이·글자 크기 m [추정: 정본 labelPlane 위치는 캡처 대조로 조정].</summary>
        public const float LabelY = 2.2f, LabelSize = 0.5f;

        [SerializeField] Material _spriteMaterial;
        [SerializeField] Material _vertexColorMaterial;
        [SerializeField] Material _labelMaterial;
        /// <summary>글자판 공유 TMP 머티리얼(계단 칸 번호도 같이 쓴다).</summary>
        public Material LabelMaterial => _labelMaterial;

        /// <summary>글자판 문구 공급(UI·Localization 연결 지점). 없으면 <see cref="DefaultLabel"/>.</summary>
        public Func<RunObject, string> LabelText;

        /// <summary>오브젝트 프리팹 로더(주소 → 임대). 기본은 Addressables. 시험은 로드 없이 뷰 구조만 보도록 바꾼다.</summary>
        public Func<string, CancellationToken, UniTask<AddressableLease<GameObject>>> PrefabLoader = AddressableScope.GetPrefab;

        sealed class View
        {
            public GameObject Go;
            public RunObject Obj;
            public ObjectShape Shape;
            public DepthSortable Ds;
            public WorldLabel Label;
            public readonly List<SpriteRenderer> Parts = new List<SpriteRenderer>(8);
            public readonly List<Vector3> Base = new List<Vector3>(8);
            public Sprite[] Frames;
            public double[] Depth;
            public int[] Order, Scratch;
            public int Stamp;
            // ---- 맞음 반응(06 §6.13) ----
            /// <summary>흔들림·번쩍 남은 시간(초).</summary>
            public double Shake, Flash;
            /// <summary>장애물 흰 섬광 칸(slot 2)이 있는가(생선 비석은 없음, L3612 return).</summary>
            public bool HasJunkFlash;
            /// <summary>우물 벽돌별 숨김 남은 시간·다시 자람(0~1).</summary>
            public double[] Gone, Grow;
            /// <summary>롤 관문 반지름(가로·세로 m).</summary>
            public double Rx, Ry;
            public bool Gold;
            // ---- 프리팹 뷰(105.prefab · 305_stageObject) ----
            /// <summary>프리팹 주소. null = 코드 모양(<see cref="Shape"/>)으로 그린다.</summary>
            public string Prefab;
            /// <summary>프리팹 인스턴스(로드 전에는 null, 로드되면 EnsureInstance가 만든다).</summary>
            public GameObject Inst;
            public ObjectPrefabRig Rig;
            // ---- 새 HTML 동작(2026-11-13 연출 정본) ----
            /// <summary>맞음 눌림 k(106.punch에서 시작, 초당 9씩 줄어듦).</summary>
            public double Punch;
            /// <summary>종이 쓰러짐 경과 초(−1 = 살아 있음). 0.6초 동안 뷰를 남겨 쓰러뜨린다.</summary>
            public double Die = -1;
            public int DieDir;
            public Vector3 DieAt;
            public PrefabParts P;
        }

        /// <summary>
        /// 프리팹 자식 이름으로 찾은 움직이는 부분(디자이너 프리팹 = 그림 정본, 동작 = 새 HTML). 없는 자식은 null로 두고 건너뛴다.
        /// </summary>
        sealed class PrefabParts
        {
            public Vector3 InstScale = Vector3.one;
            /// <summary>프리팹에 붙어 있는 빌보드(카메라를 보는 이미지). 쓰러짐 기울기를 받는다.</summary>
            public Billboard[] Boards;
            /// <summary>빛(수정탑·포탈·생선 비석 Glow)과 기본 색.</summary>
            public SpriteRenderer Glow;
            public Color GlowBase;
            /// <summary>수정탑 구슬 묶음(Orbs)과 구슬(Orb_i). 빠짐·다시 자람은 벽돌과 같은 규칙.</summary>
            public Transform Pile;
            public Vector3 PileBase;
            public Transform[] Orbs;
            public Vector3[] OrbScale;
            public double[] Gone, Grow;
            /// <summary>게이트 마법 막(Curtain)과 기본 색.</summary>
            public SpriteRenderer Curtain;
            public Color CurtainBase;
            /// <summary>포탈 점선 고리(Circle_Spin).</summary>
            public Transform Spin;
            public Quaternion SpinBase;
            /// <summary>결계 블록 상자 그림(Body/Chest_i). 줄(bar) 순서로 하나만 켠다.</summary>
            public GameObject[] Chests;
            /// <summary>반짝이(게이트 막 위·포탈 둘레).</summary>
            public SpriteRenderer[] Sparks;
        }

        /// <summary>포탈 반짝이 색 `#fff4c0`(drawPortal L3735).</summary>
        static readonly Color s_portalSpark = new Color(1f, 0xf4 / 255f, 0xc0 / 255f, 1f);

        /// <summary>롤 관문 배경 높이(가운데 y = 1.4, 세로 반지름 1.4), 롤 반지름 0.25(L3633).</summary>
        const double GateCy = 1.4, GateRy = 1.4, GateRoll = 0.25;
        /// <summary>`Fx_Stripe.png`(64×16) 안 불투명 영역 56×4 px 비율 [측정]. 줄 길이·굵기를 이 영역으로 맞춘다.</summary>
        const float StripeFillX = 56f / 64f, StripeFillY = 4f / 16f;
        public const string FlashEllipse = "Fx_FlashEllipse", Stripe = "Fx_Stripe", GlowLin = "Fx_Glow_Lin", Square = "Fx_Square";

        readonly List<int> _brickPool = new List<int>(64);
        readonly System.Random _brickRng = new System.Random(0x5EED);   // 표시 전용(정본 Math.random). 규칙 난수와 무관.
        double _lastMs = -1;

        readonly Dictionary<int, View> _views = new Dictionary<int, View>(64);
        readonly Dictionary<ObjectShape, Stack<View>> _pool = new Dictionary<ObjectShape, Stack<View>>();
        readonly List<int> _drop = new List<int>(16);
        readonly List<WellBrick> _bricks = new List<WellBrick>(64);
        readonly MeshBuffer _mb = new MeshBuffer();

        readonly Dictionary<string, Stack<View>> _prefabPool = new Dictionary<string, Stack<View>>();
        readonly Dictionary<string, AddressableLease<GameObject>> _prefabs = new Dictionary<string, AddressableLease<GameObject>>();
        readonly HashSet<string> _prefabLoading = new HashSet<string>();
        readonly CancellationTokenSource _cts = new CancellationTokenSource();
        MvTables _tables;
        string _stageMap;

        ViewConfig _cfg;
        Transform _cam;
        WorldSpriteCache _sprites;
        string _atlas, _fxAtlas;
        int _stamp;

        public int ViewCount => _views.Count;
        /// <summary>월드 스프라이트 공유 머티리얼(고양이 뷰가 프리팹 값이 없을 때 빌린다).</summary>
        public Material SpriteMaterial => _spriteMaterial;

        public void Init(ViewConfig cfg, Transform camera, WorldSpriteCache sprites, string fxAtlas)
        {
            _cfg = cfg;
            _cam = camera;
            _sprites = sprites;
            _fxAtlas = fxAtlas;
            ObjectLayout.WellBricks(_bricks);
        }

        /// <summary>테마 아틀라스(`304_theme.worldAtlas`, 미리 로드). 뷰는 다시 만든다.</summary>
        public void SetTheme(ThemeData theme)
        {
            _atlas = theme?.worldAtlas;
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

        /// <summary>오브젝트의 프리팹 주소: `305_stageObject`(맵·종류·보상) → `105_objectKind.prefab`. 둘 다 없으면 null(코드 모양).</summary>
        public static string PrefabFor(MvTables tables, string stageMap, RunObject o)
        {
            if (o?.Kind == null) return null;
            string p = tables != null ? StageObjectData.Find(tables.StageObject.Datas, stageMap, o.Kind.key, o.IsField ? RewardOf(o.Rew) : StageObjectReward.Any) : null;
            if (string.IsNullOrEmpty(p)) p = o.Kind.prefab;
            return string.IsNullOrEmpty(p) ? null : p;
        }

        /// <summary>런 칸 보상 → 305 reward(이름 1:1). 이름이 없으면 Any.</summary>
        public static StageObjectReward RewardOf(FieldReward r) => Enum.TryParse(r.ToString(), out StageObjectReward x) ? x : StageObjectReward.Any;

        /// <summary>시험·QA: 오브젝트 뷰가 쓰는 프리팹 주소와 인스턴스(로드 전이면 null).</summary>
        public bool TryPrefabView(int objId, out string address, out GameObject instance)
        {
            address = null;
            instance = null;
            if (!_views.TryGetValue(objId, out var v)) return false;
            address = v.Prefab;
            instance = v.Inst;
            return true;
        }

        void Preload(string address)
        {
            if (string.IsNullOrEmpty(address) || _prefabs.ContainsKey(address) || _prefabLoading.Contains(address)) return;
            _prefabLoading.Add(address);
            LoadPrefabAsync(address, _cts.Token).Forget();
        }

        async UniTaskVoid LoadPrefabAsync(string address, CancellationToken ct)
        {
            AddressableLease<GameObject> lease = null;
            try { lease = await PrefabLoader(address, ct); }
            catch (OperationCanceledException) { }
            if (this == null || ct.IsCancellationRequested) { lease?.Dispose(); return; }
            _prefabLoading.Remove(address);
            if (lease == null || lease.Asset == null)
            {
                // 실패는 다시 시도하지 않는다(표 주소 오류를 드러낸다). 그 오브젝트는 그리지 않는다.
                Debug.LogError("[ObjectViewFactory] 오브젝트 프리팹 로드 실패: " + address);
                lease?.Dispose();
                _prefabs[address] = null;
                return;
            }
            _prefabs[address] = lease;
        }

        /// <summary>프리팹이 로드됐으면 인스턴스를 뷰 루트 아래에 만든다(빌보드, 단계 그림 rig). 로드 전이면 아무것도 안 그린다.</summary>
        void EnsureInstance(View v)
        {
            if (v.Inst != null || v.Prefab == null) return;
            if (!_prefabs.TryGetValue(v.Prefab, out var lease)) { Preload(v.Prefab); return; }
            if (lease == null || lease.Asset == null) return;
            v.Inst = Instantiate(lease.Asset, v.Go.transform, false);
            // 빌보드는 프리팹이 정한다: 서 있는 이미지를 빈 부모 `Billboard` 하나로 묶고 그 부모에만 붙인다(바닥에 까는 이미지는 묶음 밖). 코드는 붙이지 않는다.
            if (v.Inst.GetComponentInChildren<Billboard>(true) == null && s_noBillboard.Add(v.Prefab))
                Debug.LogError("[ObjectViewFactory] 오브젝트 프리팹에 Billboard가 없습니다(카메라를 볼 이미지 자식에 붙여야 함): " + v.Prefab);
            v.Rig = v.Inst.GetComponent<ObjectPrefabRig>();
            v.P = BuildParts(v);
            v.Ds.Refresh();
        }

        /// <summary>프리팹 자식을 이름으로 찾아 동작 부분을 모은다. 반짝이는 fx 아틀라스 `Fx_Star4`로 만든다.</summary>
        PrefabParts BuildParts(View v)
        {
            var it = v.Inst.transform;
            var p = new PrefabParts { InstScale = it.localScale, Boards = v.Inst.GetComponentsInChildren<Billboard>(true) };
            var glow = Deep(it, "Glow");
            if (glow != null && glow.TryGetComponent(out p.Glow)) p.GlowBase = p.Glow.color;
            switch (v.Shape)
            {
                case ObjectShape.Well:
                {
                    p.Pile = Deep(it, "Orbs");
                    if (p.Pile != null)
                    {
                        p.PileBase = p.Pile.localPosition;
                        var orbs = new List<Transform>();
                        for (int i = 0; ; i++) { var t = p.Pile.Find("Orb_" + i); if (t == null) break; orbs.Add(t); }
                        p.Orbs = orbs.ToArray();
                        p.OrbScale = new Vector3[p.Orbs.Length];
                        for (int i = 0; i < p.Orbs.Length; i++) p.OrbScale[i] = p.Orbs[i].localScale;
                        p.Gone = new double[p.Orbs.Length];
                        p.Grow = new double[p.Orbs.Length];
                        for (int i = 0; i < p.Grow.Length; i++) p.Grow[i] = 1;
                    }
                    break;
                }
                case ObjectShape.RollGate:
                {
                    var c = Deep(it, "Curtain");
                    if (c != null && c.TryGetComponent(out p.Curtain)) p.CurtainBase = p.Curtain.color;
                    // 막 위 반짝이는 막과 같은 빌보드 묶음 안에 둔다(묶음이 카메라를 보므로 따로 돌리지 않는다).
                    var board = p.Boards.Length > 0 ? p.Boards[0].transform : it;
                    p.Sparks = MakeSparks(board, ObjectReaction.GateSparks, Color.white, board == it);
                    break;
                }
                case ObjectShape.Stair:
                {
                    p.Spin = Deep(it, "Circle_Spin");
                    if (p.Spin != null) p.SpinBase = p.Spin.localRotation;
                    // 포탈 반짝이는 바닥 원 둘레(월드 축)라 빌보드 인스턴스가 아니라 뷰 루트 아래에 각자 빌보드로 둔다.
                    p.Sparks = MakeSparks(v.Go.transform, ObjectReaction.PortalSparks, s_portalSpark, true);
                    break;
                }
                case ObjectShape.Junk:
                {
                    var body = Deep(it, "Body");
                    if (body != null)
                    {
                        var list = new List<GameObject>();
                        for (int i = 0; ; i++) { var t = body.Find("Chest_" + i); if (t == null) break; list.Add(t.gameObject); }
                        p.Chests = list.ToArray();
                    }
                    break;
                }
            }
            return p;
        }

        /// <summary>이름으로 자식을 찾는다(빌보드 묶음 아래까지). 없으면 null.</summary>
        static Transform Deep(Transform t, string name)
        {
            for (int i = 0; i < t.childCount; i++)
            {
                var c = t.GetChild(i);
                if (c.name == name) return c;
                var d = Deep(c, name);
                if (d != null) return d;
            }
            return null;
        }

        SpriteRenderer[] MakeSparks(Transform parent, int n, Color color, bool billboard)
        {
            var sprite = _sprites != null ? _sprites.Get(_fxAtlas, Star) : null;
            var arr = new SpriteRenderer[n];
            for (int i = 0; i < n; i++)
            {
                var go = new GameObject("Spark" + i);
                go.transform.SetParent(parent, false);
                if (billboard) go.AddComponent<Billboard>();
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = sprite;
                if (_spriteMaterial != null) sr.sharedMaterial = _spriteMaterial;
                sr.color = color;
                sr.sortingOrder = 50;   // 그룹 안 그림 위(정본은 막·마법진 위에 그린다)
                arr[i] = sr;
            }
            return arr;
        }

        static void SetSpark(SpriteRenderer sr, Vector3 local, double radius)
        {
            if (sr == null) return;
            var tr = sr.transform;
            tr.localPosition = local;
            float w = sr.sprite != null ? Mathf.Max(0.0001f, sr.sprite.bounds.size.x) : 1f;
            tr.localScale = Vector3.one * (float)(2 * Math.Max(0, radius) / w);
        }

        void OnDestroy()
        {
            _cts.Cancel();
            _cts.Dispose();
            foreach (var l in _prefabs.Values) l?.Dispose();
            _prefabs.Clear();
        }

        public void Clear()
        {
            foreach (var v in _views.Values) Release(v);
            _views.Clear();
        }

        static readonly HashSet<string> s_noBillboard = new HashSet<string>();
        static readonly ObjectShape?[] s_shapeOfData = BuildShapeMap();
        static readonly HashSet<ObjectViewShape> s_warnedShapes = new HashSet<ObjectViewShape>();

        /// <summary>MV.Data `ObjectViewShape` → `ObjectShape`(이름 1:1). 이름이 없는 값은 null.</summary>
        static ObjectShape?[] BuildShapeMap()
        {
            var values = (ObjectViewShape[])Enum.GetValues(typeof(ObjectViewShape));
            int max = 0;
            foreach (var v in values) max = Math.Max(max, (int)v);
            var map = new ObjectShape?[max + 1];
            foreach (var v in values)
                if (Enum.TryParse(v.ToString(), out ObjectShape shape)) map[(int)v] = shape;
            return map;
        }

        /// <summary>
        /// 모양 선택: `105_objectKind.viewShape`(07 §7.5.2)만 읽는다. 표 값에 맞는 모양이 없으면 기본 모양(Single) + 경고 1번.
        /// </summary>
        public static ObjectShape ShapeOf(ObjectKindData k)
        {
            if (k == null) return ObjectShape.Single;
            int i = (int)k.viewShape;
            if (i >= 0 && i < s_shapeOfData.Length && s_shapeOfData[i].HasValue) return s_shapeOfData[i].Value;
            if (s_warnedShapes.Add(k.viewShape))
                Debug.LogWarning($"[ObjectViewFactory] 105 {k.key}: viewShape {k.viewShape}에 맞는 ObjectShape가 없어 Single로 그린다");
            return ObjectShape.Single;
        }

        /// <summary>기본 글자판: 체력 라벨은 남은 체력 올림, 그 외는 105.labelKey(Localization 연결 전 키 그대로).</summary>
        public static string DefaultLabel(RunObject o)
        {
            var lk = o.Kind != null ? o.Kind.labelKind : LabelKind.None;
            if (lk == LabelKind.None) return null;
            if (lk == LabelKind.EnemyHp || lk == LabelKind.BoxHp) return o.HasHp ? Math.Ceiling(Math.Max(0, o.Hp)).ToString("0") : null;
            return o.Kind.labelKey;
        }

        /// <summary>
        /// 매 프레임: 그리기 범위의 살아 있는 오브젝트에 뷰를 맞춘다. <paramref name="pz"/> = 카메라 기준 z(PZ),
        /// <paramref name="playerZ"/> = 플레이어 z(관문 흐려짐), <paramref name="groundY"/> = 지면 높이.
        /// </summary>
        public void Sync(IReadOnlyList<RunObject> objs, double pz, double playerZ, double groundY)
        {
            if (_cfg == null || objs == null) return;
            _stamp++;
            double nowMs = Time.realtimeSinceStartupAsDouble * 1000.0;
            double dt = _lastMs < 0 ? 0 : Math.Max(0, Math.Min(0.1, (nowMs - _lastMs) / 1000.0));
            _lastMs = nowMs;
            for (int i = 0; i < objs.Count; i++)
            {
                var o = objs[i];
                if (o == null || o.Dead || o.Z < pz + _cfg.ObjDrawBack || o.Z > pz + _cfg.ObjDrawFront) continue;
                if (!_views.TryGetValue(o.Id, out var v)) _views[o.Id] = v = Show(o, groundY);
                v.Stamp = _stamp;
                v.Go.transform.position = new Vector3((float)o.X, (float)groundY, (float)o.Z);
                Animate(v, nowMs, playerZ, dt);
            }
            _drop.Clear();
            foreach (var kv in _views)
            {
                if (kv.Value.Stamp == _stamp) continue;
                // 종이 쓰러짐(paperDie): 죽은 오브젝트 뷰를 0.6초 남겨 쓰러뜨린 뒤 돌려준다.
                if (kv.Value.Die >= 0 && kv.Value.Die < ObjectReaction.DieSec) { AnimateDie(kv.Value, dt); continue; }
                _drop.Add(kv.Key);
            }
            for (int i = 0; i < _drop.Count; i++) { Release(_views[_drop[i]]); _views.Remove(_drop[i]); }
        }

        /// <summary>오브젝트 하나의 뷰 루트(시험·QA용). 없으면 null.</summary>
        public GameObject ViewOf(int objId) => _views.TryGetValue(objId, out var v) ? v.Go : null;

        /// <summary>시험·QA: 뷰의 흔들림·번쩍 남은 시간. 뷰가 없으면 false.</summary>
        public bool TryReaction(int objId, out double shake, out double flash)
        {
            shake = flash = 0;
            if (!_views.TryGetValue(objId, out var v)) return false;
            shake = v.Shake;
            flash = v.Flash;
            return true;
        }

        /// <summary>시험·QA: 우물 뷰에서 지금 빠져 있는(숨김) 벽돌 수.</summary>
        public int GoneBricks(int objId)
        {
            if (!_views.TryGetValue(objId, out var v) || v.Gone == null) return 0;
            int n = 0;
            for (int i = 0; i < v.Gone.Length; i++) if (v.Gone[i] > 0) n++;
            return n;
        }

        /// <summary>
        /// 런 이벤트 → 뷰 반응(06 §6.13). 통합 코드가 <see cref="Sync"/> 전에 같은 프레임의 이벤트 목록을 넘긴다.
        /// Hit: `106_hitReaction.objShake` → 흔들림 0.12 s(장애물·우물), `flash` → 롤 관문 번쩍 0.1 s. WellFlush: 벽돌 A개를 빼서 2.2 s 숨김.
        /// 뷰가 없는(그리기 범위 밖) 오브젝트의 이벤트는 버린다.
        /// </summary>
        public void OnRunEvents(IReadOnlyList<RunEvent> events)
        {
            if (events == null) return;
            for (int i = 0; i < events.Count; i++)
            {
                var e = events[i];
                if (e.Type != RunEventType.Hit && e.Type != RunEventType.WellFlush && e.Type != RunEventType.Break) continue;
                if (!_views.TryGetValue(e.ObjId, out var v) || v.Obj == null) continue;
                if (e.Type == RunEventType.Hit)
                {
                    var r = v.Obj.Reaction;
                    if (r == null) continue;
                    if (r.objShake > 0 && (v.Shape == ObjectShape.Junk || v.Shape == ObjectShape.Well)) v.Shake = ObjectReaction.ShakeSec;
                    if (r.flash && v.Shape == ObjectShape.RollGate) v.Flash = ObjectReaction.FlashSec;
                    if (r.punch > 0) v.Punch = r.punch;   // 새 HTML hit: o.punch = 106.punch
                }
                else if (e.Type == RunEventType.Break) StartDie(v);
                else if (v.Shape == ObjectShape.Well)
                {
                    if (v.P != null && v.P.Orbs != null) FlushOrbs(v.P, Math.Max(1, e.A));
                    else FlushBricks(v, Math.Max(1, e.A));
                }
            }
        }

        /// <summary>정본 `flushBricks` L1861: 보이는 벽돌을 층 높은 순(잡동사니 먼저)으로 늘어놓고 앞 10개 안에서 무작위로 n개를 뺀다.</summary>
        void FlushBricks(View v, int n)
        {
            if (v.Gone == null) return;
            _brickPool.Clear();
            for (int layer = 5; layer >= 0; layer--)
                for (int i = 0; i < _bricks.Count && i < v.Gone.Length; i++)
                    if (_bricks[i].Layer == layer && v.Gone[i] <= 0) _brickPool.Add(i);
            for (int k = 0; k < n && _brickPool.Count > 0; k++)
            {
                int at = Math.Min(_brickPool.Count - 1, (int)Math.Floor(_brickRng.NextDouble() * Math.Min(_brickPool.Count, ObjectReaction.BrickPickTop)));
                int b = _brickPool[at];
                _brickPool.RemoveAt(at);
                v.Gone[b] = ObjectReaction.BrickGoneSec;
                v.Grow[b] = 0;
            }
        }

        /// <summary>프리팹 수정탑: 보이는 구슬 중 무작위 n개를 빼서 2.2초 숨긴다(정본 flushBricks와 같은 규칙, 튀는 조각·게이지 비행은 MvFxRouter).</summary>
        void FlushOrbs(PrefabParts p, int n)
        {
            _brickPool.Clear();
            for (int i = 0; i < p.Orbs.Length; i++) if (p.Gone[i] <= 0) _brickPool.Add(i);
            for (int k = 0; k < n && _brickPool.Count > 0; k++)
            {
                int at = Math.Min(_brickPool.Count - 1, (int)Math.Floor(_brickRng.NextDouble() * Math.Min(_brickPool.Count, ObjectReaction.BrickPickTop)));
                int b = _brickPool[at];
                _brickPool.RemoveAt(at);
                p.Gone[b] = ObjectReaction.BrickGoneSec;
                p.Grow[b] = 0;
            }
        }

        /// <summary>시험·QA: 프리팹 수정탑에서 지금 빠져 있는 구슬 수.</summary>
        public int GoneOrbs(int objId)
        {
            if (!_views.TryGetValue(objId, out var v) || v.P == null || v.P.Gone == null) return 0;
            int n = 0;
            for (int i = 0; i < v.P.Gone.Length; i++) if (v.P.Gone[i] > 0) n++;
            return n;
        }

        /// <summary>시험·QA: 맞음 눌림 k와 쓰러짐 경과 초(−1 = 살아 있음).</summary>
        public bool TryMotion(int objId, out double punch, out double die)
        {
            punch = 0; die = -1;
            if (!_views.TryGetValue(objId, out var v)) return false;
            punch = v.Punch;
            die = v.Die;
            return true;
        }

        void StartDie(View v)
        {
            if (v.Die >= 0) return;
            v.Die = 0;
            v.DieDir = ObjectReaction.DieDir(v.Obj.Z, v.Obj.X);
            v.DieAt = v.Go.transform.position;
            v.Punch = 0;
            if (v.Label != null) v.Label.gameObject.SetActive(false);
        }

        /// <summary>종이 쓰러짐(paperDie L3663): 뛰어오르며 가로로 뒤집히고, 세로로 눌리며 기울고, 뒤 40%에서 사라진다.</summary>
        void AnimateDie(View v, double dt)
        {
            v.Die += dt;
            double k = Math.Min(1, v.Die / ObjectReaction.DieSec);
            ObjectReaction.DiePose(k, v.DieDir, out double hop, out double sx, out double sy, out double roll, out double alpha);
            // 정본 캔버스 회전(시계 +)을 Unity z 회전(반시계 +)으로.
            float deg = (float)(-roll * 180 / Math.PI);
            if (v.Inst != null && v.P != null)
            {
                v.Inst.transform.localPosition = new Vector3(0, (float)hop, 0);
                v.Inst.transform.localScale = Vector3.Scale(v.P.InstScale, new Vector3((float)sx, (float)sy, 1));
                for (int i = 0; i < v.P.Boards.Length; i++) v.P.Boards[i].Roll = deg;
            }
            else
            {
                v.Go.transform.position = v.DieAt + new Vector3(0, (float)hop, 0);
                v.Go.transform.localScale = new Vector3((float)sx, (float)sy, 1);
                for (int i = 0; i < v.Parts.Count; i++)
                    if (v.Parts[i].TryGetComponent(out Billboard b)) b.Roll = deg;
            }
            v.Ds.SetAlphaScale((float)alpha);
        }

        /// <summary>풀에서 다시 쓰는 뷰의 동작 상태를 처음으로 돌린다.</summary>
        static void ResetMotion(View v)
        {
            v.Punch = 0;
            v.Die = -1;
            v.Go.transform.localScale = Vector3.one;
            for (int i = 0; i < v.Parts.Count; i++)
                if (v.Parts[i].TryGetComponent(out Billboard b)) b.Roll = 0;
            var p = v.P;
            if (p == null || v.Inst == null) return;
            v.Inst.transform.localScale = p.InstScale;
            for (int i = 0; i < p.Boards.Length; i++) p.Boards[i].Roll = 0;
            if (p.Gone != null)
                for (int i = 0; i < p.Gone.Length; i++) { p.Gone[i] = 0; p.Grow[i] = 1; p.Orbs[i].localScale = p.OrbScale[i]; }
        }

        void Release(View v)
        {
            v.Go.SetActive(false);
            v.Obj = null;
            if (v.Prefab != null)
            {
                if (!_prefabPool.TryGetValue(v.Prefab, out var ps)) _prefabPool[v.Prefab] = ps = new Stack<View>();
                ps.Push(v);
                return;
            }
            if (!_pool.TryGetValue(v.Shape, out var st)) _pool[v.Shape] = st = new Stack<View>();
            st.Push(v);
        }

        View Show(RunObject o, double groundY)
        {
            var shape = ShapeOf(o.Kind);
            string prefab = PrefabFor(_tables, _stageMap, o);
            View v;
            Stack<View> st;
            bool pooled = prefab != null ? _prefabPool.TryGetValue(prefab, out st) : _pool.TryGetValue(shape, out st);
            if (pooled && st.Count > 0) v = st.Pop();
            else
            {
                var go = new GameObject(prefab ?? shape.ToString());
                go.transform.SetParent(transform, false);
                var sg = go.AddComponent<SortingGroup>();
                sg.sortingLayerName = WorldLayer;
                v = new View { Go = go, Shape = shape, Prefab = prefab, Ds = go.AddComponent<DepthSortable>() };
                v.Ds.Rank = DepthMath.RankObject;
            }
            v.Obj = o;
            v.Ds.SortOffsetY = (float)_cfg.ObjProbeY;
            v.Ds.Bias = o.Kind != null ? (float)o.Kind.sortBias : 0f;
            v.Ds.SetAlphaScale(1f);
            v.Shake = v.Flash = 0;
            v.HasJunkFlash = false;
            ResetMotion(v);
            v.Go.transform.position = new Vector3((float)o.X, (float)groundY, (float)o.Z);
            for (int i = 0; i < v.Parts.Count; i++) v.Parts[i].gameObject.SetActive(false);
            v.Base.Clear();
            if (v.Prefab != null)
            {
                EnsureInstance(v);
                if (v.Inst != null) v.Inst.transform.localPosition = Vector3.zero;
            }
            else Build(v, o);
            BuildLabel(v, o);
            v.Go.SetActive(true);
            v.Ds.Refresh();
            return v;
        }

        string SpriteAt(RunObject o, int i)
        {
            var list = o.Kind?.sprite;
            if (list == null || list.Count == 0) return null;
            return list[((i % list.Count) + list.Count) % list.Count];
        }

        SpriteRenderer Part(View v, int slot, string atlas, string sprite, Vector3 local, int order, bool flipX = false)
        {
            while (v.Parts.Count <= slot)
            {
                var go = new GameObject("P" + v.Parts.Count);
                go.transform.SetParent(v.Go.transform, false);
                go.AddComponent<Billboard>();
                var r = go.AddComponent<SpriteRenderer>();
                r.sharedMaterial = _spriteMaterial;
                v.Parts.Add(r);
            }
            while (v.Base.Count <= slot) v.Base.Add(Vector3.zero);
            var sr = v.Parts[slot];
            sr.sprite = _sprites != null ? _sprites.Get(atlas, sprite) : null;
            sr.flipX = flipX;
            sr.sortingOrder = order;
            sr.color = Color.white;
            sr.transform.localPosition = local;
            sr.transform.localScale = Vector3.one;
            sr.transform.localRotation = Quaternion.identity;
            sr.gameObject.SetActive(sr.sprite != null);
            v.Base[slot] = local;
            return sr;
        }

        void Build(View v, RunObject o)
        {
            switch (v.Shape)
            {
                case ObjectShape.Junk: BuildJunk(v, o); break;
                case ObjectShape.RollGate: BuildRollGate(v, o); break;
                case ObjectShape.WarpGate: BuildWarpGate(v, o); break;
                case ObjectShape.Well: BuildWell(v, o); break;
                case ObjectShape.Stair:
                    // 정본 drawBlackHole L3203: 먼 단부터 i = 2, 1, 0. 단 앵커 z = 0.3 + i·0.8 − d/2(d 0.8).
                    for (int i = 2; i >= 0; i--) Part(v, 2 - i, _atlas, SpriteAt(o, i), new Vector3(0, 0, (float)(0.3 + i * 0.8 - 0.4)), 2 - i);
                    Part(v, 3, _atlas, StairArrow, new Vector3(0, 2.4f, 0.6f), 3);
                    break;
                case ObjectShape.CoinPile:
                    for (int i = 0; i < s_churu.Length / 3; i++)
                    {
                        var p = Part(v, i, _atlas, SpriteAt(o, i), new Vector3((float)s_churu[i * 3], (float)-s_churu[i * 3 + 1], 0), i);
                        p.transform.localRotation = Quaternion.Euler(0, 0, (float)(-s_churu[i * 3 + 2] * Mathf.Rad2Deg));
                    }
                    break;
                case ObjectShape.CatnipPot:
                    Part(v, 0, _atlas, SpriteAt(o, 0), Vector3.zero, 0);
                    Part(v, 1, _fxAtlas, Star, new Vector3(0.1f, 1.37f, 0), 1);
                    break;
                case ObjectShape.Mouse:
                    string prefix = o.Enemy?.sprite;
                    if (!string.IsNullOrEmpty(prefix) && !prefix.EndsWith("_")) prefix += "_";
                    v.Frames = string.IsNullOrEmpty(prefix) || _sprites == null ? null : _sprites.GetFrames(_atlas, prefix, Math.Max(1, o.Enemy.frameCount));
                    var m = Part(v, 0, _atlas, null, Vector3.zero, 0);
                    m.sprite = v.Frames != null && v.Frames.Length > 0 ? v.Frames[0] : null;
                    m.gameObject.SetActive(m.sprite != null);
                    break;
                default:
                    Part(v, 0, _atlas, SpriteAt(o, 0), Vector3.zero, 0);
                    break;
            }
        }

        /// <summary>츄르 더미 6개 (dx, dy, rot) — 정본 L3109, 크기 단위 c = 1 m(캔버스 y 아래 +).</summary>
        static readonly double[] s_churu = { -0.42, -0.12, -0.5, 0.4, -0.1, 0.45, 0, -0.22, 0.08, -0.2, -0.46, -0.25, 0.24, -0.44, 0.35, 0.02, -0.7, -0.08 };

        void BuildJunk(View v, RunObject o)
        {
            ObjectLayout.JunkJitter(o.Idx, o.Bar, out double jx, out double sc);
            bool soul = o.Rew == FieldReward.Soul && o.IsField;
            string sprite = soul ? o.Kind.spriteSoul : SpriteAt(o, ObjectLayout.JunkSpriteIndex(Math.Max(0, o.Lane), Math.Max(0, o.Row), Math.Max(0, o.Bar), Math.Max(0, o.Idx)));
            var body = Part(v, 0, _atlas, sprite, new Vector3((float)jx, (float)ObjectLayout.JunkLiftY, (float)ObjectLayout.JunkFrontZ), 0);
            body.transform.localScale = Vector3.one * (float)sc;
            if (soul) return;
            // 맞음 흰 섬광(L3629): 그림 발 위 0.6q, 반지름 1.1q × 0.8q, 알파 0.35. 흔들리는 동안만 켠다.
            var fl = Part(v, 2, _fxAtlas, FlashEllipse,
                new Vector3((float)jx, (float)(ObjectLayout.JunkLiftY + ObjectReaction.JunkFlashY * sc), (float)ObjectLayout.JunkFrontZ), 2);
            if (fl.sprite != null)
            {
                var b = fl.sprite.bounds.size;
                fl.transform.localScale = new Vector3((float)(2 * ObjectReaction.JunkFlashRx * sc) / Mathf.Max(0.0001f, b.x),
                    (float)(2 * ObjectReaction.JunkFlashRy * sc) / Mathf.Max(0.0001f, b.y), 1f);
                fl.color = new Color(1f, 1f, 1f, (float)ObjectReaction.JunkFlashAlpha);
                v.HasJunkFlash = true;
            }
            fl.gameObject.SetActive(false);
            if (!o.IsField) return;
            Part(v, 1, _atlas, ObjectLayout.BadgeSprite(o.Rew),
                new Vector3((float)(jx + ObjectLayout.BadgeX * sc), (float)(ObjectLayout.JunkLiftY + ObjectLayout.BadgeY * sc), (float)ObjectLayout.JunkFrontZ), 1);
        }

        void BuildRollGate(View v, RunObject o)
        {
            // 정본 drawRollGate L3632: 가운데 (0, 1.4), rx = hw·1.02, ry = 1.4, 롤 반지름 0.25. 배경 둥근 모서리는 사각형 근사.
            bool stairGate = o.Kind.key == "sgate";
            string tint = o.Kind.tint != null && o.Kind.tint.Count > 0 ? o.Kind.tint[0] : (stairGate ? "#6fd27a" : "#f08ab0");
            double rx = Math.Max(0.1, o.Hw) * 1.02, ry = GateRy, r = GateRoll, cy = GateCy;
            v.Rx = rx;
            v.Ry = ry;
            float x0 = (float)-rx, x1 = (float)rx, y0 = (float)-ry, y1 = (float)ry;
            // 배경 그라데이션: 평소(아래 `55`)와 번쩍(아래 `aa`) 두 장을 만들어 켜고 끈다(런 중 메쉬 다시 쓰기 없음).
            for (int f = 0; f < 2; f++)
            {
                var bg = MeshChild(v.Go.transform, f == 0 ? "Bg" : "BgFlash");
                bg.localPosition = new Vector3(0, (float)cy, 0);
                _mb.Clear();
                Color32 top = MvColor.WithAlpha(tint, ObjectReaction.GateBgTop),
                    bottom = MvColor.WithAlpha(tint, f == 0 ? ObjectReaction.GateBgBottom : ObjectReaction.GateBgBottomFlash);
                _mb.AddQuad(new Vector3(x0, y0), new Vector3(x1, y0), new Vector3(x1, y1), new Vector3(x0, y1), bottom, bottom, top, top);
                bg.GetComponent<VertexColorFog>().Bind(_mb, f == 0 ? "RollGateBg" : "RollGateBgFlash");
                bg.GetComponent<MeshRenderer>().sortingOrder = 0;
                bg.gameObject.SetActive(f == 0);
            }
            // 위로 흐르는 흰 줄 5개(L3635): 배경 위·롤 아래, 관문 면(XY 평면)에 둔다.
            for (int i = 0; i < ObjectReaction.GateStripes; i++)
            {
                var sr = Flat(v.Go.transform, StripeName(i), _fxAtlas, Stripe, 1);
                if (sr.sprite == null) continue;
                var b = sr.sprite.bounds.size;
                sr.transform.localScale = new Vector3((float)(rx * ObjectReaction.StripeHalfW * 2) / Mathf.Max(0.0001f, b.x * StripeFillX),
                    (float)ObjectReaction.StripeThick / Mathf.Max(0.0001f, b.y * StripeFillY), 1f);
                sr.color = new Color(1f, 1f, 1f, (float)ObjectReaction.StripeAlpha);
            }
            int slot = 0, ny = Math.Max(2, (int)Math.Round(ry * 2 / (r * 1.9))), nx = Math.Max(2, (int)Math.Round(rx * 2 / (r * 1.9)));
            for (int k = 0; k <= ny; k++)
            {
                float y = (float)(cy - ry + k * (ry * 2 / ny));   // 정본 c.y + ry − k·… (캔버스 아래 +) → 아래부터 위로
                Part(v, slot, _atlas, SpriteAt(o, slot), new Vector3(x0, y, 0), 2); slot++;
                Part(v, slot, _atlas, SpriteAt(o, slot), new Vector3(x1, y, 0), 2); slot++;
            }
            for (int k = 1; k < nx; k++) { Part(v, slot, _atlas, SpriteAt(o, slot), new Vector3((float)(-rx + k * (rx * 2 / nx)), (float)(cy + ry), 0), 2); slot++; }
        }

        /// <summary>
        /// 빌보드가 아닌 평면 스프라이트 자식(이름으로 찾고 없으면 만든다). 관문 면(XY 평면)에 붙는 줄·흰 줄용. 그림이 없으면 끈다.
        /// </summary>
        SpriteRenderer Flat(Transform root, string name, string atlas, string sprite, int order)
        {
            var t = root.Find(name);
            SpriteRenderer sr;
            if (t == null)
            {
                var go = new GameObject(name);
                go.transform.SetParent(root, false);
                sr = go.AddComponent<SpriteRenderer>();
                sr.sharedMaterial = _spriteMaterial;
            }
            else sr = t.GetComponent<SpriteRenderer>();
            sr.sprite = _sprites != null ? _sprites.Get(atlas, sprite) : null;
            sr.sortingOrder = order;
            sr.color = Color.white;
            sr.flipX = false;
            sr.transform.localPosition = Vector3.zero;
            sr.transform.localRotation = Quaternion.identity;
            sr.transform.localScale = Vector3.one;
            sr.gameObject.SetActive(sr.sprite != null);
            return sr;
        }


        void BuildWarpGate(View v, RunObject o)
        {
            bool gold = o.Kind.key == "archGoal";
            string p1 = gold ? WarpGateLayout.P1Gold : WarpGateLayout.P1, p2 = gold ? WarpGateLayout.P2Gold : WarpGateLayout.P2;
            var boxes = WarpGateLayout.Boxes;
            for (int i = 0; i < boxes.Length; i++)
            {
                var b = boxes[i];
                var t = MeshChild(v.Go.transform, "Box" + i);
                if (t.GetComponent<Billboard>() == null) t.gameObject.AddComponent<Billboard>();
                t.localPosition = new Vector3((float)b.X, (float)b.Y, (float)(-b.D / 2));   // 앵커 = 앞면 아래 가운데
                _mb.Clear();
                BoxBillboardBuilder.Build(_mb, b.W, b.H, b.D, p1, p2, double.PositiveInfinity, _cfg.OutlineColor);
                t.GetComponent<VertexColorFog>().Bind(_mb, "GateBox");
                t.GetComponent<MeshRenderer>().sortingOrder = i == 4 ? 3 : 1;
            }
            // 기둥(원근 기울기 포함 그림, 왼쪽 flipX)은 받침 위·중간 발판 아래(정본 순서: 받침 → 기둥 → 중간 발판).
            Part(v, 0, _atlas, SpriteAt(o, 0), new Vector3((float)-WarpGateLayout.PostX, 0, 0), 0, true);
            Part(v, 1, _atlas, SpriteAt(o, 0), new Vector3((float)WarpGateLayout.PostX, 0, 0), 0);
            // 매달린 장난감 줄(L3221, 관문 면 XY 평면) → 공. 줄은 발판 위, 공은 줄 위.
            for (int i = 0; i < 3; i++)
            {
                var str = Flat(v.Go.transform, StringName(i), _fxAtlas, Square, 4);
                if (str.sprite != null) str.color = MvColor.ToColor32(_cfg.OutlineColor);
                Part(v, 2 + i, _atlas, "World_Obj_Arch_Toy_" + i, new Vector3((float)WarpGateLayout.ToyDx[i], (float)ObjectReaction.ToyBallY, 0), 5);
            }
            // 금빛 관문: 광륜(L3215, 맨 먼저)·기둥 위 별(L3223, 맨 나중).
            v.Gold = gold;
            if (gold)
            {
                var glow = Part(v, 5, _fxAtlas, GlowLin, new Vector3(0, (float)ObjectReaction.GlowY, 0), -1);
                if (glow.sprite != null)
                {
                    glow.transform.localScale = Vector3.one * ((float)(ObjectReaction.GlowR * 2) / Mathf.Max(0.0001f, glow.sprite.bounds.size.x));
                    var c = (Color)MvColor.ToColor32(ObjectReaction.GlowColor);
                    c.a = 1f;   // Fx_Glow_Lin 중심 알파 ≈ 0.45(정본 0.55) [근사]
                    glow.color = c;
                }
                for (int s = 0; s < 2; s++)
                {
                    var star = Part(v, 6 + s, _fxAtlas, Star, new Vector3((float)(s == 0 ? -WarpGateLayout.PostX : WarpGateLayout.PostX), (float)ObjectReaction.StarY, 0), 6);
                    star.color = MvColor.ToColor32(ObjectReaction.StarColor);
                }
            }
        }

        void BuildWell(View v, RunObject o)
        {
            Part(v, 0, _atlas, SpriteAt(o, 0), new Vector3(0, 1.25f, 0), 0);
            int n = _bricks.Count;
            if (v.Depth == null || v.Depth.Length != n) { v.Depth = new double[n]; v.Order = new int[n]; v.Scratch = new int[n]; }
            if (v.Gone == null || v.Gone.Length != n) { v.Gone = new double[n]; v.Grow = new double[n]; }
            for (int i = 0; i < n; i++) { v.Gone[i] = 0; v.Grow[i] = 1; }
            for (int i = 0; i < n; i++)
            {
                var b = _bricks[i];
                Part(v, 1 + i, _atlas, (b.Junk ? "World_Obj_Well_Junk_" : "World_Obj_Well_Brick_") + b.Variant, new Vector3((float)b.X, (float)b.Y, (float)b.Z), 1 + i);
            }
        }

        void BuildLabel(View v, RunObject o)
        {
            string text = LabelText != null ? LabelText(o) : DefaultLabel(o);
            if (text == null)
            {
                if (v.Label != null) v.Label.gameObject.SetActive(false);
                return;
            }
            if (v.Label == null) v.Label = WorldLabel.Create(v.Go.transform, "Label", LabelSize, _labelMaterial);
            v.Label.transform.localPosition = new Vector3(0, LabelY, 0);
            v.Label.GetComponent<Renderer>().sortingOrder = 100;   // 그룹 안 맨 위(정본 편향 −0.05와 같은 결과)
            v.Label.SetText(text);
            v.Label.gameObject.SetActive(true);
        }

        Transform MeshChild(Transform root, string name)
        {
            var t = root.Find(name);
            if (t == null)
            {
                var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer), typeof(VertexColorFog));
                t = go.transform;
                t.SetParent(root, false);
                go.GetComponent<MeshRenderer>().sharedMaterial = _vertexColorMaterial;
            }
            t.gameObject.SetActive(true);
            return t;
        }

        void Animate(View v, double nowMs, double playerZ, double dt)
        {
            var o = v.Obj;
            double t = nowMs / 1000.0;
            if (v.Shake > 0) v.Shake = Math.Max(0, v.Shake - dt);
            if (v.Flash > 0) v.Flash = Math.Max(0, v.Flash - dt);
            if (v.Punch > 0) v.Punch = ObjectReaction.PunchStep(v.Punch, dt);
            if (v.Prefab != null) AnimatePrefab(v, o, playerZ, t, dt);
            else switch (v.Shape)
            {
                case ObjectShape.WarpGate:
                    bool shown = WarpGateLayout.Alpha(playerZ - o.Z, out double a);
                    v.Ds.SetAlphaScale(shown ? (float)a : 0f);
                    for (int i = 0; i < 3; i++)
                    {
                        var b = v.Base[2 + i];
                        double sw = WarpGateLayout.ToySway(i, t);
                        v.Parts[2 + i].transform.localPosition = new Vector3(b.x + (float)sw, b.y, b.z);
                        PlaceString(v.Go.transform.Find(StringName(i)), WarpGateLayout.ToyDx[i], sw);
                    }
                    if (v.Gold)
                        for (int s = 0; s < 2 && 6 + s < v.Parts.Count; s++)
                        {
                            var star = v.Parts[6 + s];
                            if (star.sprite == null) continue;
                            star.transform.localScale = Vector3.one * (float)(2 * ObjectReaction.StarRadius(t) / Mathf.Max(0.0001f, star.sprite.bounds.size.x));
                        }
                    break;
                case ObjectShape.Stair:
                    var ab = v.Base[3];
                    v.Parts[3].transform.localPosition = new Vector3(ab.x, ab.y + Mathf.Sin((float)(t * 5)) * 0.25f, ab.z);
                    break;
                case ObjectShape.CoinPile:
                {
                    // 크기 k = 0.55 + 0.45·hp/max(L1525): 츄르 자리·크기 모두 k배(정본 c = s·body.scale.y).
                    float k = o.HasHp ? (float)ObjectReaction.PickupScale(o.Hp, o.Max, o.Dead) : 1f;
                    for (int i = 0; i < v.Parts.Count && i < 6; i++)
                    {
                        var b = v.Base[i];
                        v.Parts[i].transform.localPosition = new Vector3(b.x * k, (b.y - (float)(Math.Sin(nowMs / 500.0 + i) * 0.02)) * k, b.z);
                        v.Parts[i].transform.localScale = Vector3.one * k;
                    }
                    break;
                }
                case ObjectShape.CatnipPot:
                {
                    float k = o.HasHp ? (float)ObjectReaction.PickupScale(o.Hp, o.Max, o.Dead) : 1f;
                    if (v.Parts.Count > 0) v.Parts[0].transform.localScale = Vector3.one * k;
                    if (v.Parts.Count > 1 && v.Parts[1].sprite != null)
                    {
                        float r = (float)(0.14 * (0.8 + 0.2 * Math.Sin(nowMs / 200.0)));
                        var b = v.Base[1];
                        v.Parts[1].transform.localPosition = new Vector3(b.x * k, b.y * k, b.z);
                        v.Parts[1].transform.localScale = Vector3.one * (2 * r * k / Mathf.Max(0.0001f, v.Parts[1].sprite.bounds.size.x));
                    }
                    break;
                }
                case ObjectShape.Mouse:
                    if (v.Frames != null && v.Frames.Length > 0)
                    {
                        double ph = (nowMs / 200.0) % (Math.PI * 2);
                        int f = Math.Min(v.Frames.Length - 1, (int)(ph / (Math.PI * 2) * v.Frames.Length));
                        if (v.Frames[f] != null) v.Parts[0].sprite = v.Frames[f];
                    }
                    break;
                case ObjectShape.Well:
                    AnimateBricks(v, dt);
                    SortBricks(v);
                    break;
                case ObjectShape.Junk:
                {
                    // 벽 흔들림(L1484): 그림 전체 x += sin(shake·90)·0.05, 흔들리는 동안 흰 섬광(L3629).
                    bool on = v.Shake > 0;
                    if (on) v.Go.transform.position += new Vector3((float)ObjectReaction.WallShakeX(v.Shake), 0, 0);
                    if (v.HasJunkFlash && v.Parts.Count > 2 && v.Parts[2].gameObject.activeSelf != on) v.Parts[2].gameObject.SetActive(on);
                    break;
                }
                case ObjectShape.RollGate:
                    AnimateRollGate(v, t);
                    break;
            }
            if (v.Prefab == null) v.Go.transform.localScale = PunchScale(v.Punch);
            if (v.Label != null && v.Label.gameObject.activeSelf && (o.Kind.labelKind == LabelKind.EnemyHp || o.Kind.labelKind == LabelKind.BoxHp))
                v.Label.SetText(LabelText != null ? LabelText(o) : DefaultLabel(o));
        }

        /// <summary>
        /// 프리팹 뷰: 로드되면 인스턴스를 만들고 새 HTML 동작을 프리팹 자식에 입힌다(그림 = 디자이너 프리팹, 동작 = 새 HTML 2026-11-13).
        /// 공통: 맞음 눌림(punch), 체력 단계 그림(<see cref="ObjectPrefabRig"/>). 결계문 흐려짐(WarpGate), 게이트 가라앉기·막 번쩍·반짝이(RollGate),
        /// 수정탑 흔들림·빛·구슬 맥동·빠짐(Well), 포탈 고리 회전·반짝이·빛(Stair), 결계 블록 자리 흔들기·흔들림·상자 그림·생선 빛(Junk).
        /// </summary>
        void AnimatePrefab(View v, RunObject o, double playerZ, double t, double dt)
        {
            EnsureInstance(v);
            if (v.Inst == null) return;
            var p = v.P;
            float sx = 0f, dy = 0f;
            switch (v.Shape)
            {
                case ObjectShape.WarpGate:
                {
                    bool shown = WarpGateLayout.Alpha(playerZ - o.Z, out double a);
                    v.Ds.SetAlphaScale(shown ? (float)a : 0f);
                    break;
                }
                case ObjectShape.RollGate:
                {
                    dy = (float)ObjectReaction.GateSinkY(o.Sink);
                    v.Ds.SetAlphaScale(ObjectReaction.GateHidden(o.Sink) ? 0f : 1f);
                    if (p?.Curtain != null) v.Ds.SetBaseColor(p.Curtain, Alpha(p.CurtainBase, ObjectReaction.CurtainRatio(v.Flash > 0)));
                    if (p?.Sparks != null)
                        for (int i = 0; i < p.Sparks.Length; i++)
                        {
                            double ph = ObjectReaction.GateSparkPhase(t, i);
                            var at = new Vector3((float)ObjectReaction.GateSparkX(i, o.Z, o.Hw), (float)(ObjectReaction.GatePanelH * ph), -0.05f);
                            SetSpark(p.Sparks[i], at, ObjectReaction.GateSparkR * (1 - ph));
                        }
                    break;
                }
                case ObjectShape.Well:
                    if (p == null) break;
                    if (p.Glow != null) v.Ds.SetBaseColor(p.Glow, Alpha(p.GlowBase, ObjectReaction.TowerGlowRatio(t)));
                    if (p.Pile != null) p.Pile.localPosition = p.PileBase + new Vector3((float)ObjectReaction.WellShakeX(v.Shake), 0, 0);
                    if (p.Orbs != null)
                        for (int i = 0; i < p.Orbs.Length; i++)
                        {
                            double sc = ObjectReaction.BrickStep(ref p.Gone[i], ref p.Grow[i], dt);
                            p.Orbs[i].localScale = p.OrbScale[i] * (float)(sc * ObjectReaction.OrbPulse(t, i));
                        }
                    break;
                case ObjectShape.Stair:
                    if (p == null) break;
                    if (p.Spin != null) p.Spin.localRotation = p.SpinBase * Quaternion.Euler(0, 0, (float)(t * ObjectReaction.PortalSpin * 180 / Math.PI));
                    if (p.Glow != null) v.Ds.SetBaseColor(p.Glow, Alpha(p.GlowBase, ObjectReaction.PortalGlowRatio(o.Used)));
                    if (p.Sparks != null)
                        for (int i = 0; i < p.Sparks.Length; i++)
                        {
                            double k = ObjectReaction.PortalSparkPhase(t, i), a = ObjectReaction.PortalSparkAngle(t, i);
                            var at = new Vector3((float)(Math.Cos(a) * ObjectReaction.PortalSparkRing), (float)(k * ObjectReaction.PortalSparkH), (float)(Math.Sin(a) * ObjectReaction.PortalSparkRing));
                            SetSpark(p.Sparks[i], at, ObjectReaction.PortalSparkR * (1 - k));
                        }
                    break;
                case ObjectShape.Junk:
                {
                    sx = (float)ObjectReaction.WallShakeX(v.Shake);
                    ObjectReaction.WallJitter(o.Idx, o.Bar, out double jx, out double jz);
                    v.Go.transform.position += new Vector3((float)jx, 0, (float)jz);
                    if (p?.Chests != null && p.Chests.Length > 0)
                    {
                        int pick = Math.Max(0, o.Bar) % p.Chests.Length;   // 정본 chest(bar % 3), 개수 = 프리팹 Chest_ 자식 수
                        for (int i = 0; i < p.Chests.Length; i++) if (p.Chests[i].activeSelf != (i == pick)) p.Chests[i].SetActive(i == pick);
                    }
                    if (p?.Glow != null) v.Ds.SetBaseColor(p.Glow, Alpha(p.GlowBase, ObjectReaction.FishGlowRatio(t)));
                    break;
                }
            }
            v.Inst.transform.localPosition = new Vector3(sx, dy, 0);
            v.Inst.transform.localScale = Vector3.Scale(p != null ? p.InstScale : Vector3.one, PunchScale(v.Punch));
            if (v.Rig != null) v.Rig.SetFill(o.HasHp ? o.Hp : 1, o.HasHp ? o.Max : 1);
        }

        /// <summary>맞음 눌림 크기 (1 + 0.07k, 1 − 0.08k, 1 + 0.07k)(updObjMesh L1468).</summary>
        static Vector3 PunchScale(double k)
        {
            float g = (float)(k * ObjectReaction.PunchGrow), s = (float)(k * ObjectReaction.PunchSquash);
            return new Vector3(1 + g, 1 - s, 1 + g);
        }

        static Color Alpha(Color c, double ratio)
        {
            c.a = Mathf.Clamp01(c.a * (float)ratio);
            return c;
        }

        static readonly string[] s_stringNames = { "String0", "String1", "String2" };
        static readonly string[] s_stripeNames = { "Stripe0", "Stripe1", "Stripe2", "Stripe3", "Stripe4" };
        static string StringName(int i) => s_stringNames[i];
        static string StripeName(int i) => s_stripeNames[i];

        /// <summary>장난감 줄: 발판 (dx, 4.6) → 공 (dx + sw, 3.3) 선분을 관문 면 위 얇은 사각형으로.</summary>
        static void PlaceString(Transform str, double dx, double sw)
        {
            if (str == null || !str.gameObject.activeSelf) return;
            var sr = str.GetComponent<SpriteRenderer>();
            if (sr == null || sr.sprite == null) return;
            double ax = dx, ay = ObjectReaction.ToyTopY, bx = dx + sw, by = ObjectReaction.ToyBallY;
            double len = Math.Sqrt((bx - ax) * (bx - ax) + (by - ay) * (by - ay));
            var size = sr.sprite.bounds.size;
            str.localPosition = new Vector3((float)((ax + bx) / 2), (float)((ay + by) / 2), 0);
            str.localRotation = Quaternion.Euler(0, 0, (float)(Math.Atan2(by - ay, bx - ax) * 180 / Math.PI) + 90f);
            str.localScale = new Vector3((float)ObjectReaction.ToyStringThick / Mathf.Max(0.0001f, size.x), (float)len / Mathf.Max(0.0001f, size.y), 1f);
        }

        /// <summary>우물: 벽돌 숨김·다시 자람(L1474~1475)과 더미 흔들림 x(L1477).</summary>
        static void AnimateBricks(View v, double dt)
        {
            if (v.Gone == null) return;
            float sx = (float)ObjectReaction.WellShakeX(v.Shake);
            int n = Math.Min(v.Gone.Length, v.Parts.Count - 1);
            for (int i = 0; i < n; i++)
            {
                double sc = ObjectReaction.BrickStep(ref v.Gone[i], ref v.Grow[i], dt);
                var tr = v.Parts[1 + i].transform;
                var b = v.Base[1 + i];
                tr.localPosition = new Vector3(b.x + sx, b.y, b.z);
                tr.localScale = Vector3.one * (float)sc;
            }
        }

        /// <summary>롤 관문: 번쩍 배경 바꾸기, 흰 줄 흐름, 통과 뒤 가라앉기(안쪽 y −sink·6, 0.5 넘으면 숨김, L1503).</summary>
        static void AnimateRollGate(View v, double t)
        {
            var o = v.Obj;
            var root = v.Go.transform;
            float dy = (float)ObjectReaction.GateSinkY(o.Sink);
            v.Ds.SetAlphaScale(ObjectReaction.GateHidden(o.Sink) ? 0f : 1f);
            bool flash = v.Flash > 0;
            var bg = root.Find("Bg");
            if (bg != null)
            {
                bg.localPosition = new Vector3(0, (float)GateCy + dy, 0);
                if (bg.gameObject.activeSelf == flash) bg.gameObject.SetActive(!flash);
            }
            var bgFlash = root.Find("BgFlash");
            if (bgFlash != null)
            {
                bgFlash.localPosition = new Vector3(0, (float)GateCy + dy, 0);
                if (bgFlash.gameObject.activeSelf != flash) bgFlash.gameObject.SetActive(flash);
            }
            for (int i = 0; i < ObjectReaction.GateStripes; i++)
            {
                var s = root.Find(StripeName(i));
                if (s == null || !s.gameObject.activeSelf) continue;
                s.localPosition = new Vector3(0, (float)(GateCy + ObjectReaction.StripeY(i, t, v.Ry)) + dy, 0);
            }
            if (dy != 0)
                for (int i = 0; i < v.Parts.Count && i < v.Base.Count; i++)
                {
                    var b = v.Base[i];
                    v.Parts[i].transform.localPosition = new Vector3(b.x, b.y + dy, b.z);
                }
        }

        /// <summary>우물 벽돌 그룹 안 정렬: 카메라 깊이 내림차순(먼 것 먼저, 정본 `list.sort((a,b)=>b.d−a.d)`).</summary>
        void SortBricks(View v)
        {
            if (_cam == null || v.Depth == null) return;
            Vector3 cp = _cam.position, fwd = _cam.forward;
            int n = v.Depth.Length;
            for (int i = 0; i < n; i++) v.Depth[i] = Vector3.Dot(v.Parts[1 + i].transform.position - cp, fwd);
            DepthMath.RankFarFirst(v.Depth, n, v.Order, v.Scratch);
            for (int i = 0; i < n; i++) v.Parts[1 + i].sortingOrder = 1 + v.Order[i];
        }
    }
}
