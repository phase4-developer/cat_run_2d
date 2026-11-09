using System;
using System.Collections.Generic;
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

        /// <summary>글자판 문구 공급(UI·Localization 연결 지점). 없으면 <see cref="DefaultLabel"/>.</summary>
        public Func<RunObject, string> LabelText;

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
        }

        readonly Dictionary<int, View> _views = new Dictionary<int, View>(64);
        readonly Dictionary<ObjectShape, Stack<View>> _pool = new Dictionary<ObjectShape, Stack<View>>();
        readonly List<int> _drop = new List<int>(16);
        readonly List<WellBrick> _bricks = new List<WellBrick>(64);
        readonly MeshBuffer _mb = new MeshBuffer();

        ViewConfig _cfg;
        Transform _cam;
        WorldSpriteCache _sprites;
        string _atlas, _fxAtlas;
        int _stamp;

        public int ViewCount => _views.Count;

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

        public void Clear()
        {
            foreach (var v in _views.Values) Release(v);
            _views.Clear();
        }

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
            for (int i = 0; i < objs.Count; i++)
            {
                var o = objs[i];
                if (o == null || o.Dead || o.Z < pz + _cfg.ObjDrawBack || o.Z > pz + _cfg.ObjDrawFront) continue;
                if (!_views.TryGetValue(o.Id, out var v)) _views[o.Id] = v = Show(o, groundY);
                v.Stamp = _stamp;
                v.Go.transform.position = new Vector3((float)o.X, (float)groundY, (float)o.Z);
                Animate(v, nowMs, playerZ);
            }
            _drop.Clear();
            foreach (var kv in _views) if (kv.Value.Stamp != _stamp) _drop.Add(kv.Key);
            for (int i = 0; i < _drop.Count; i++) { Release(_views[_drop[i]]); _views.Remove(_drop[i]); }
        }

        /// <summary>오브젝트 하나의 뷰 루트(시험·QA용). 없으면 null.</summary>
        public GameObject ViewOf(int objId) => _views.TryGetValue(objId, out var v) ? v.Go : null;

        void Release(View v)
        {
            v.Go.SetActive(false);
            v.Obj = null;
            if (!_pool.TryGetValue(v.Shape, out var st)) _pool[v.Shape] = st = new Stack<View>();
            st.Push(v);
        }

        View Show(RunObject o, double groundY)
        {
            var shape = ShapeOf(o.Kind);
            View v;
            if (_pool.TryGetValue(shape, out var st) && st.Count > 0) v = st.Pop();
            else
            {
                var go = new GameObject(shape.ToString());
                go.transform.SetParent(transform, false);
                var sg = go.AddComponent<SortingGroup>();
                sg.sortingLayerName = WorldLayer;
                v = new View { Go = go, Shape = shape, Ds = go.AddComponent<DepthSortable>() };
                v.Ds.Rank = DepthMath.RankObject;
            }
            v.Obj = o;
            v.Ds.SortOffsetY = (float)_cfg.ObjProbeY;
            v.Ds.Bias = o.Kind != null ? (float)o.Kind.sortBias : 0f;
            v.Ds.SetAlphaScale(1f);
            v.Go.transform.position = new Vector3((float)o.X, (float)groundY, (float)o.Z);
            for (int i = 0; i < v.Parts.Count; i++) v.Parts[i].gameObject.SetActive(false);
            v.Base.Clear();
            Build(v, o);
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
            if (soul || !o.IsField) return;
            Part(v, 1, _atlas, ObjectLayout.BadgeSprite(o.Rew),
                new Vector3((float)(jx + ObjectLayout.BadgeX * sc), (float)(ObjectLayout.JunkLiftY + ObjectLayout.BadgeY * sc), (float)ObjectLayout.JunkFrontZ), 1);
        }

        void BuildRollGate(View v, RunObject o)
        {
            // 정본 drawRollGate L3632: 가운데 (0, 1.4), rx = hw·1.02, ry = 1.4, 롤 반지름 0.25. 배경 둥근 모서리는 사각형 근사.
            bool stairGate = o.Kind.key == "sgate";
            string tint = o.Kind.tint != null && o.Kind.tint.Count > 0 ? o.Kind.tint[0] : (stairGate ? "#6fd27a" : "#f08ab0");
            double rx = Math.Max(0.1, o.Hw) * 1.02, ry = 1.4, r = 0.25, cy = 1.4;
            var bg = MeshChild(v.Go.transform, "Bg");
            bg.localPosition = new Vector3(0, (float)cy, 0);
            _mb.Clear();
            Color32 top = MvColor.WithAlpha(tint, 0x22 / 255.0), bottom = MvColor.WithAlpha(tint, 0x55 / 255.0);
            float x0 = (float)-rx, x1 = (float)rx, y0 = (float)-ry, y1 = (float)ry;
            _mb.AddQuad(new Vector3(x0, y0), new Vector3(x1, y0), new Vector3(x1, y1), new Vector3(x0, y1), bottom, bottom, top, top);
            bg.GetComponent<VertexColorFog>().Bind(_mb, "RollGateBg");
            int slot = 0, ny = Math.Max(2, (int)Math.Round(ry * 2 / (r * 1.9))), nx = Math.Max(2, (int)Math.Round(rx * 2 / (r * 1.9)));
            for (int k = 0; k <= ny; k++)
            {
                float y = (float)(cy - ry + k * (ry * 2 / ny));   // 정본 c.y + ry − k·… (캔버스 아래 +) → 아래부터 위로
                Part(v, slot, _atlas, SpriteAt(o, slot), new Vector3(x0, y, 0), 1); slot++;
                Part(v, slot, _atlas, SpriteAt(o, slot), new Vector3(x1, y, 0), 1); slot++;
            }
            for (int k = 1; k < nx; k++) { Part(v, slot, _atlas, SpriteAt(o, slot), new Vector3((float)(-rx + k * (rx * 2 / nx)), (float)(cy + ry), 0), 1); slot++; }
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
            for (int i = 0; i < 3; i++)
                Part(v, 2 + i, _atlas, "World_Obj_Arch_Toy_" + i, new Vector3((float)WarpGateLayout.ToyDx[i], 3.3f, 0), 4);
        }

        void BuildWell(View v, RunObject o)
        {
            Part(v, 0, _atlas, SpriteAt(o, 0), new Vector3(0, 1.25f, 0), 0);
            int n = _bricks.Count;
            if (v.Depth == null || v.Depth.Length != n) { v.Depth = new double[n]; v.Order = new int[n]; v.Scratch = new int[n]; }
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

        void Animate(View v, double nowMs, double playerZ)
        {
            var o = v.Obj;
            double t = nowMs / 1000.0;
            switch (v.Shape)
            {
                case ObjectShape.WarpGate:
                    bool shown = WarpGateLayout.Alpha(playerZ - o.Z, out double a);
                    v.Ds.SetAlphaScale(shown ? (float)a : 0f);
                    for (int i = 0; i < 3; i++)
                    {
                        var b = v.Base[2 + i];
                        v.Parts[2 + i].transform.localPosition = new Vector3(b.x + (float)WarpGateLayout.ToySway(i, t), b.y, b.z);
                    }
                    break;
                case ObjectShape.Stair:
                    var ab = v.Base[3];
                    v.Parts[3].transform.localPosition = new Vector3(ab.x, ab.y + Mathf.Sin((float)(t * 5)) * 0.25f, ab.z);
                    break;
                case ObjectShape.CoinPile:
                    for (int i = 0; i < v.Parts.Count && i < 6; i++)
                    {
                        var b = v.Base[i];
                        v.Parts[i].transform.localPosition = new Vector3(b.x, b.y - (float)(Math.Sin(nowMs / 500.0 + i) * 0.02), b.z);
                    }
                    break;
                case ObjectShape.CatnipPot:
                    if (v.Parts.Count > 1 && v.Parts[1].sprite != null)
                    {
                        float r = (float)(0.14 * (0.8 + 0.2 * Math.Sin(nowMs / 200.0)));
                        v.Parts[1].transform.localScale = Vector3.one * (2 * r / Mathf.Max(0.0001f, v.Parts[1].sprite.bounds.size.x));
                    }
                    break;
                case ObjectShape.Mouse:
                    if (v.Frames != null && v.Frames.Length > 0)
                    {
                        double ph = (nowMs / 200.0) % (Math.PI * 2);
                        int f = Math.Min(v.Frames.Length - 1, (int)(ph / (Math.PI * 2) * v.Frames.Length));
                        if (v.Frames[f] != null) v.Parts[0].sprite = v.Frames[f];
                    }
                    break;
                case ObjectShape.Well:
                    SortBricks(v);
                    break;
            }
            if (v.Label != null && v.Label.gameObject.activeSelf && (o.Kind.labelKind == LabelKind.EnemyHp || o.Kind.labelKind == LabelKind.BoxHp))
                v.Label.SetText(LabelText != null ? LabelText(o) : DefaultLabel(o));
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
