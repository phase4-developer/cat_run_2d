using System.Collections.Generic;
using MV.Data;
using UnityEngine;
using UnityEngine.Rendering;

namespace MV.View
{
    /// <summary>
    /// 테마 소품 배치·표시(02 §2.5.2, 03 §3.5.2). <see cref="PropLayout"/>가 만든 배치에서 그리기 범위(PZ−4 ~ PZ+90) 안의 것만
    /// 뷰를 꺼내 쓰고(풀), 밖으로 나가면 돌려준다. 정렬·안개는 루트의 <see cref="DepthSortable"/>(탐침 = 생성 위치)가 받는다.
    /// 1차 범위: 길가 소품·전봇대(+가로등 빛)·집(상자 메쉬·문·창·옥상 소품·창 불빛)·담장 몸체. 담장 장식·빨랫줄은 아직 없음.
    /// </summary>
    [DefaultExecutionOrder(60)]
    public sealed class PropStreamer : MonoBehaviour
    {
        public const string GlowSprite = "Fx_Glow_Lin";
        public const string LightLayer = "Light", WorldLayer = "World";

        [SerializeField] Material _spriteMaterial;
        [SerializeField] Material _additiveMaterial;
        [SerializeField] Material _vertexColorMaterial;

        readonly List<PropSpec> _specs = new List<PropSpec>(1024);
        readonly List<GameObject> _views = new List<GameObject>(1024);
        readonly Dictionary<PropKind, Stack<GameObject>> _pool = new Dictionary<PropKind, Stack<GameObject>>();
        readonly MeshBuffer _mb = new MeshBuffer();
        readonly Dictionary<GameObject, List<GameObject>> _lights = new Dictionary<GameObject, List<GameObject>>();

        ViewConfig _cfg;
        CameraRig _rig;
        WorldSpriteCache _sprites;
        ThemeData _theme;
        string _atlas, _fxAtlas;
        double _groundY;
        bool _night;
        Color _lampColor, _winColor;

        public IReadOnlyList<PropSpec> Specs => _specs;

        public void Init(ViewConfig cfg, CameraRig rig, WorldSpriteCache sprites, string fxAtlas)
        {
            _cfg = cfg;
            _rig = rig;
            _sprites = sprites;
            _fxAtlas = fxAtlas;
            MvColor.TryColor32("#ffd890", out var lamp);
            MvColor.TryColor32("#ffcf70", out var win);
            float la = (float)cfg.LightAlpha;
            _lampColor = new Color(lamp.r / 255f, lamp.g / 255f, lamp.b / 255f, la);
            _winColor = new Color(win.r / 255f, win.g / 255f, win.b / 255f, la);
        }

        /// <summary>테마와 아틀라스(`304_theme.worldAtlas`, 미리 로드되어 있어야 한다).</summary>
        public void SetTheme(ThemeData theme)
        {
            _theme = theme;
            _atlas = theme?.worldAtlas;
            _night = theme != null && theme.night != 0;   // 0이 아니면 참(항구 0.2도 참, D-44)
            ClearViews();
        }

        /// <summary>정본 `buildEnv(len, from, cliff)` 소품 줄을 다시 만든다. y = 그 구간 지면 높이.</summary>
        public void Rebuild(double from, double len, bool cliff, double groundY)
        {
            ClearViews();
            _specs.Clear();
            _groundY = groundY;
            PropLayout.Build(from, len, cliff, _specs);
            for (int i = 0; i < _specs.Count; i++) _views.Add(null);
        }

        void LateUpdate()
        {
            if (_cfg == null || _rig == null || !_rig.Ready || _theme == null) return;
            double pz = _rig.Model.Pos.z;
            for (int i = 0; i < _specs.Count; i++)
            {
                bool want = PropLayout.InDrawRange(_specs[i].Z, pz, _cfg);
                var v = _views[i];
                if (want && v == null) _views[i] = Show(_specs[i]);
                else if (!want && v != null) { Hide(_specs[i].Kind, v); _views[i] = null; }
            }
        }

        void ClearViews()
        {
            for (int i = 0; i < _views.Count; i++)
                if (_views[i] != null) { Hide(_specs[i].Kind, _views[i]); _views[i] = null; }
        }

        void Hide(PropKind kind, GameObject v)
        {
            v.SetActive(false);
            if (_lights.TryGetValue(v, out var lights))
                for (int i = 0; i < lights.Count; i++) lights[i].SetActive(false);
            if (!_pool.TryGetValue(kind, out var st)) _pool[kind] = st = new Stack<GameObject>();
            st.Push(v);
        }

        GameObject Take(PropKind kind)
        {
            if (_pool.TryGetValue(kind, out var st) && st.Count > 0) return st.Pop();
            var go = new GameObject(kind.ToString());
            go.transform.SetParent(transform, false);
            var sg = go.AddComponent<SortingGroup>();
            sg.sortingLayerName = WorldLayer;
            go.AddComponent<DepthSortable>().Rank = DepthMath.RankProp;
            return go;
        }

        GameObject Show(in PropSpec p)
        {
            var go = Take(p.Kind);
            var root = go.transform;
            var ds = go.GetComponent<DepthSortable>();
            // 정렬·안개 탐침 = 생성 위치(정본 draw2D L3785·drawProp L2900).
            ds.SetFixedAnchor(new Vector3((float)p.GenX, (float)_groundY, (float)p.Z));
            switch (p.Kind)
            {
                case PropKind.Roadside:
                    root.position = new Vector3((float)p.DrawX, (float)_groundY, (float)p.Z);
                    BillboardSprite(root, "Body", PropLayout.RoadsideSprite(_theme.deco, p.Hh), Vector3.zero, 0, false);
                    break;
                case PropKind.Pole:
                    root.position = new Vector3((float)p.DrawX, (float)_groundY, (float)p.Z);
                    BillboardSprite(root, "Body", "World_Prop_Pole", Vector3.zero, 0, p.Side < 0);
                    SetLight(root, "Lamp", _night, new Vector3(-p.Side * 1.0f, 4.1f, 0), -0.2f, 3.0f, _lampColor);
                    break;
                case PropKind.House:
                case PropKind.FarHouse:
                    BuildHouse(root, p);
                    break;
                case PropKind.Fence:
                    BuildFence(root, p);
                    break;
            }
            go.SetActive(true);
            ds.Refresh();
            return go;
        }

        SpriteRenderer BillboardSprite(Transform root, string name, string sprite, Vector3 localPos, int order, bool flipX)
        {
            var t = root.Find(name);
            if (t == null)
            {
                var go = new GameObject(name);
                t = go.transform;
                t.SetParent(root, false);
                go.AddComponent<Billboard>();
                var r = go.AddComponent<SpriteRenderer>();
                r.sharedMaterial = _spriteMaterial;
            }
            t.localPosition = localPos;
            var sr = t.GetComponent<SpriteRenderer>();
            sr.sprite = _sprites.Get(_atlas, sprite);
            sr.flipX = flipX;
            sr.sortingOrder = order;
            sr.drawMode = SpriteDrawMode.Simple;
            sr.color = Color.white;
            t.gameObject.SetActive(sr.sprite != null);
            return sr;
        }

        void Disable(Transform root, string name)
        {
            var t = root.Find(name);
            if (t != null) t.gameObject.SetActive(false);
        }

        /// <summary>
        /// 가산광 빌보드(`Light` 레이어). SortingGroup 밖이어야 레이어가 유지되므로 루트의 형제(이 스트리머 아래)에 둔다.
        /// 위치 = 루트 + <paramref name="local"/>, 화면 아래로 <paramref name="screenDown"/> m. 반지름 <paramref name="radius"/> m.
        /// </summary>
        void SetLight(Transform root, string name, bool on, Vector3 local, float screenDown, float radius, Color color)
        {
            string key = root.GetInstanceID() + "_" + name;
            var t = transform.Find(key);
            if (!on) { if (t != null) t.gameObject.SetActive(false); return; }
            if (t == null)
            {
                var go = new GameObject(key);
                t = go.transform;
                t.SetParent(transform, false);
                go.AddComponent<Billboard>();
                var child = new GameObject("Glow");
                child.transform.SetParent(t, false);
                var r = child.AddComponent<SpriteRenderer>();
                r.sharedMaterial = _additiveMaterial;
                r.sortingLayerName = LightLayer;
            }
            t.position = root.position + local;
            var sr = t.GetChild(0).GetComponent<SpriteRenderer>();
            sr.sprite = _sprites.Get(_fxAtlas, GlowSprite);
            sr.color = color;
            sr.transform.localPosition = new Vector3(0, screenDown, 0);
            float size = sr.sprite != null ? Mathf.Max(0.0001f, sr.sprite.bounds.size.x) : 1f;
            sr.transform.localScale = Vector3.one * (2f * radius / size);
            t.gameObject.SetActive(sr.sprite != null);
            if (!_lights.TryGetValue(root.gameObject, out var list)) _lights[root.gameObject] = list = new List<GameObject>(2);
            if (!list.Contains(t.gameObject)) list.Add(t.gameObject);
        }

        void BuildHouse(Transform root, in PropSpec p)
        {
            var th = _theme;
            var h = PropLayout.House(p, _groundY, th.walls?.Count ?? 0, th.roofs?.Count ?? 0, _night, th.deco);
            // 앵커 = 앞면(카메라 쪽) 아래 가운데 (x, y, z − d/2).
            root.position = new Vector3((float)p.GenX, (float)_groundY, (float)(p.Z - h.D / 2));
            string wall = th.walls != null && th.walls.Count > 0 ? th.walls[h.WallIndex] : "#c8a8a0";
            string roof = th.roofs != null && th.roofs.Count > 0 ? th.roofs[h.RoofIndex] : null;

            var box = root.Find("Box");
            if (box == null)
            {
                var go = new GameObject("Box", typeof(MeshFilter), typeof(MeshRenderer), typeof(VertexColorFog), typeof(Billboard));
                box = go.transform;
                box.SetParent(root, false);
                go.GetComponent<MeshRenderer>().sharedMaterial = _vertexColorMaterial;
            }
            _mb.Clear();
            var shape = BoxBillboardBuilder.Build(_mb, h.W, h.H, h.D, wall, roof, double.PositiveInfinity, _cfg.OutlineColor);
            box.GetComponent<VertexColorFog>().Bind(_mb, "HouseBox");
            box.GetComponent<MeshRenderer>().sortingOrder = 0;

            // 문·창은 상자와 같은 빌보드 평면의 자식(상자 Box 아래)으로 둔다.
            BillboardSprite(box, "Door", "World_House_Door", new Vector3((float)h.DoorX, 0, 0), 1, false);
            Window(box, "Win0", h.Win0, h.Win0X, h.WinY, h.Win0Lit);
            Window(box, "Win1", h.Win1, h.Win1X, h.WinY, h.Win1Lit);
            SetLight(root, "Win0Light", h.Win0Lit, new Vector3((float)h.Win0X, (float)h.WinY, 0), 0, 1.4f, _winColor);
            SetLight(root, "Win1Light", h.Win1Lit, new Vector3((float)h.Win1X, (float)h.WinY, 0), 0, 1.4f, _winColor);

            if (h.RoofKind == RoofItem.SnowCap)
            {
                Disable(root, "RoofItem");
                var cap = BillboardSprite(box, "SnowCap", "World_House_SnowCap", new Vector3(0, (float)(h.H + shape.T * 0.25), 0), 3, false);
                cap.drawMode = SpriteDrawMode.Sliced;
                cap.size = new Vector2((float)h.W, (float)(shape.T * 0.75));
            }
            else if (h.RoofKind == RoofItem.None) { Disable(box, "SnowCap"); Disable(root, "RoofItem"); }
            else
            {
                Disable(box, "SnowCap");
                // 옥상 소품은 별도 앵커(02 §2.1.4): (wp.x + (hh−0.5)·bw·0.4, y + bh, wp.z + bd·0.15). 정렬은 집과 같은 항목.
                var roofSr = BillboardSprite(root, "RoofItem", h.RoofKind == RoofItem.Tank ? "World_House_Tank" : "World_House_Jars", Vector3.zero, 4, false);
                roofSr.transform.position = new Vector3((float)h.RoofX, (float)h.RoofY, (float)h.RoofZ);
            }
        }

        void Window(Transform box, string name, bool show, double x, double y, bool lit)
        {
            if (!show) { Disable(box, name); return; }
            BillboardSprite(box, name, lit ? "World_House_Win_On" : "World_House_Win_Off", new Vector3((float)x, (float)y, 0), 2, false);
        }

        void BuildFence(Transform root, in PropSpec p)
        {
            var th = _theme;
            int sd = p.Side;
            root.position = new Vector3((float)p.DrawX, (float)_groundY, (float)p.Z);
            var body = root.Find("Body");
            if (body == null)
            {
                var go = new GameObject("Body", typeof(MeshFilter), typeof(MeshRenderer), typeof(VertexColorFog));
                body = go.transform;
                body.SetParent(root, false);
                go.GetComponent<MeshRenderer>().sharedMaterial = _vertexColorMaterial;
            }
            string col = th.walls != null && th.walls.Count > 0 ? th.walls[(int)System.Math.Floor(p.Hh * th.walls.Count)] : "#c8a8a0";
            Color32 c = MvColor.ToColor32(col), shade = MvColor.ToColor32(MvColor.Shade(col, -0.12));
            Color32 top = MvColor.ToColor32(th.wallTop), ol = MvColor.ToColor32(_cfg.OutlineColor);
            // 정본 L3558~3569: 안쪽 면 x = −sd·0.2(루트 기준), z ±1.58, 높이 1.25, 윗면 바깥 x = +sd·0.2.
            float xi = -sd * 0.2f, xo = sd * 0.2f, z0 = -1.58f, z1 = 1.58f, h = 1.25f, lw = 0.035f;
            var a = new Vector3(xi, 0, z0); var b = new Vector3(xi, 0, z1); var cc = new Vector3(xi, h, z1); var d = new Vector3(xi, h, z0);
            var e = new Vector3(xo, h, z1); var f = new Vector3(xo, h, z0);
            _mb.Clear();
            _mb.AddQuad(a, b, cc, d, c);
            Outline(a, b, cc, d, Vector3.right, lw, ol);
            _mb.AddQuad(a, b, new Vector3(xi, 0.22f, z1), new Vector3(xi, 0.22f, z0), shade);
            _mb.AddQuad(d, cc, e, f, top);
            Outline(d, cc, e, f, Vector3.up, lw, ol);
            body.GetComponent<VertexColorFog>().Bind(_mb, "Fence");
            body.GetComponent<MeshRenderer>().sortingOrder = 0;
        }

        void Outline(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 planeNormal, float w, Color32 col)
        {
            Edge(a, b, planeNormal, w, col); Edge(b, c, planeNormal, w, col); Edge(c, d, planeNormal, w, col); Edge(d, a, planeNormal, w, col);
        }

        void Edge(Vector3 a, Vector3 b, Vector3 planeNormal, float w, Color32 col)
        {
            var n = Vector3.Cross(b - a, planeNormal).normalized;
            _mb.AddLine(a, b, n, w, col);
        }
    }
}
