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
        }

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
            foreach (var kv in _views) if (kv.Value.Stamp != _stamp) _drop.Add(kv.Key);
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
                if (e.Type != RunEventType.Hit && e.Type != RunEventType.WellFlush) continue;
                if (!_views.TryGetValue(e.ObjId, out var v) || v.Obj == null) continue;
                if (e.Type == RunEventType.Hit)
                {
                    var r = v.Obj.Reaction;
                    if (r == null) continue;
                    if (r.objShake > 0 && (v.Shape == ObjectShape.Junk || v.Shape == ObjectShape.Well)) v.Shake = ObjectReaction.ShakeSec;
                    if (r.flash && v.Shape == ObjectShape.RollGate) v.Flash = ObjectReaction.FlashSec;
                }
                else if (v.Shape == ObjectShape.Well) FlushBricks(v, Math.Max(1, e.A));
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
            v.Shake = v.Flash = 0;
            v.HasJunkFlash = false;
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
            switch (v.Shape)
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
            if (v.Label != null && v.Label.gameObject.activeSelf && (o.Kind.labelKind == LabelKind.EnemyHp || o.Kind.labelKind == LabelKind.BoxHp))
                v.Label.SetText(LabelText != null ? LabelText(o) : DefaultLabel(o));
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
