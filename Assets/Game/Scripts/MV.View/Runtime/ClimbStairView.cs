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
    /// <summary>
    /// 계단 정산 칸(새 HTML `startClimb`·`updClimb`·`drawStairs3D`). 칸 i는 바닥(오르기 시작 WY)부터 높이 (i+1)·stepH인 기둥이고,
    /// 디자이너 프리팹(`330_climb.stepPrefab`, 한 블록 = stepW × stepH × stepD)을 칸마다 1개만 두고, 세로로 칸 높이만큼 늘려 한 장짜리 기둥으로 만든다(웹 `drawStairs3D`처럼 블록을 쌓은 느낌이 아니라 점점 길어지는 계단).
    /// 늘리는 방법: 프리팹 안 `Front`·`Side` SpriteRenderer가 Sliced/Tiled면 size.y만 늘려 테두리 두께를 지키고, Simple이면 세로 스케일로 늘린다(테두리도 늘어남, 경고 1회).
    /// `Top`·`Stripe`·칸 번호는 늘어난 높이만큼 위로 옮긴다. 프리팹 루트 기준점 = 블록 바닥·앞면(지금과 같음).
    /// 칸은 시뮬이 보이기 시작한 순서(<see cref="ClimbState.StepsShown"/>, MV-SPEC-02 6-5e)대로 dropH 위에서 dropSec 동안 (1 − f)²로 떨어지고,
    /// 알파는 0.4 + 0.6f. 꼭대기 블록의 줄무늬는 그 칸 남은 칸 수의 카드 등급 색(없으면 프리팹 색), 아래 블록 줄무늬는 끈다(HTML 줄무늬는 칸마다 꼭대기 1줄).
    /// 옆 시점이면 꼭대기 옆면에 칸 번호. 블록은 카메라 시야 안의 것만 풀에서 꺼내 쓴다. 오르기가 끝난 뒤에도 같은 런이면 마지막 계단을 그대로 둔다(HTML `LASTC`).
    /// </summary>
    [DefaultExecutionOrder(56)]
    public sealed class ClimbStairView : MonoBehaviour
    {
        /// <summary>새 HTML 칸 번호 글자: 높이 0.28 m, 꼭대기 아래 0.45 m, 옆면 바깥 0.01 m. 옆 시점 비율 = (카메라 x − 반폭)/3 m, 0.05 이하면 안 그림.</summary>
        public const float NumberSize = 0.28f, NumberBelowTop = 0.45f, NumberOut = 0.01f, SideFadeM = 3f, SideMin = 0.05f;
        /// <summary>떨어지는 칸의 알파 = AlphaFrom + (1 − AlphaFrom)·f(새 HTML `0.4 + 0.6*f`).</summary>
        public const float AlphaFrom = 0.4f;
        /// <summary>칸 번호는 블록 안 다른 그림 위에 그린다(정렬 그룹 안 순서).</summary>
        const int NumberOrder = 10;
        const string StripeName = "Stripe", FrontName = "Front", SideName = "Side", TopName = "Top";

        public Func<string, CancellationToken, UniTask<AddressableLease<GameObject>>> PrefabLoader = AddressableScope.GetPrefab;

        /// <summary>세로로 늘리는 면(앞판·옆판). 프리팹 원래 값(높이 1칸 기준)을 기억해 두고 매번 그 값에서 다시 계산한다.</summary>
        sealed class Face
        {
            public SpriteRenderer Sr;
            public Transform Tr;
            public float PosY, SizeY, ScaleY;
            public bool Sliced;
        }

        sealed class Block
        {
            public GameObject Go;
            public DepthSortable Ds;
            public SpriteRenderer Stripe;
            public Color StripeBase;
            public WorldLabel Number;
            public Face Front, Side;
            public Transform Top, StripeTr;
            public float TopY, StripeY;
            public float Height = -1f;   // 마지막으로 맞춘 기둥 높이(같으면 다시 안 바꿈)
        }

        bool _warnedSimple;

        MvTables _t;
        ViewConfig _cfg;
        Camera _cam;
        Material _labelMaterial;
        string _address;
        double _stepH, _stepD, _halfW, _dropSec, _dropH, _lap;
        GameObject _template;
        AddressableLease<GameObject> _lease;
        bool _loading, _ready;
        readonly CancellationTokenSource _cts = new CancellationTokenSource();

        RunState _run;
        ClimbState _climb;
        double _base;
        readonly List<double> _shownAt = new List<double>();
        readonly List<int> _tier = new List<int>();
        readonly List<string> _label = new List<string>();
        readonly List<Block> _pool = new List<Block>();
        int _used;
        readonly Plane[] _planes = new Plane[6];

        /// <summary>시험·QA: 이번 프레임에 켠 블록 수(= 보이는 칸 수), 칸 번호를 켠 블록 수.</summary>
        public int ActiveBlocks => _used;
        public int ActiveNumbers { get; private set; }
        public string Address => _address;

        public void Init(MvTables tables, ViewConfig cfg, Camera cam, Material labelMaterial)
        {
            _t = tables ?? throw new ArgumentNullException(nameof(tables));
            _cfg = cfg ?? throw new ArgumentNullException(nameof(cfg));
            _cam = cam;
            _labelMaterial = labelMaterial;
            var c = tables.Climb.Config() ?? throw new InvalidOperationException(ClimbData.Table + ": 설정 행(default)이 없습니다");
            _address = c.stepPrefab;
            _stepH = c["stepH"];
            _stepD = c["stepD"];
            _halfW = c["stepW"] * 0.5;
            _dropSec = c["dropSec"];
            _dropH = c["dropH"];
            _lap = c["lap"];
            _ready = true;
            Hide();
        }

        /// <summary>시험: Addressables 대신 프리팹을 바로 준다.</summary>
        public void UseTemplate(GameObject prefab) => _template = prefab;

        public void Hide()
        {
            for (int i = 0; i < _pool.Count; i++) Park(_pool[i]);
            _used = 0;
            ActiveNumbers = 0;
        }

        /// <summary>매 프레임(시뮬 뒤). 런이 바뀌면 지난 계단을 지운다.</summary>
        public void Sync(RunState s)
        {
            if (!_ready || s == null) { Hide(); _run = null; _climb = null; return; }
            if (!ReferenceEquals(s, _run)) { _run = s; _climb = null; }
            var live = s.Phase == RunPhase.Climb ? s.Climb : null;
            if (live != null && !ReferenceEquals(live, _climb)) Begin(live, s.WorldY);
            var C = _climb;
            if (C == null || C.K <= 0) { Hide(); return; }
            var template = Template();
            if (template == null || _cam == null) { Hide(); return; }

            bool ended = live == null || C.T > 0 && C.Time >= C.T;   // 끝(p ≥ 1)에는 모든 칸 t = 1
            while (_shownAt.Count < Math.Min(C.K, C.StepsShown)) _shownAt.Add(C.Time);
            int shown = ended ? C.K : _shownAt.Count;

            GeometryUtility.CalculateFrustumPlanes(_cam, _planes);
            float sideK = Mathf.Clamp01((float)((_cam.transform.position.x - _halfW) / SideFadeM));
            _used = 0;
            int numbers = 0;
            var size = new Vector3((float)(_halfW * 2), (float)_stepH, (float)_stepD);   // 블록 1칸 크기(컬링 상자 가로·깊이)
            for (int i = 0; i < shown; i++)
            {
                double f = ended || _dropSec <= 0 ? 1 : Math.Min(1, (C.Time - _shownAt[i]) / _dropSec);
                double lift = _dropH * (1 - f) * (1 - f);
                double zs = C.Z0 + i * _stepD, foot = _base + lift;
                var column = new Bounds(new Vector3(0, (float)(foot + (i + 1) * _stepH * 0.5), (float)(zs + _stepD * 0.5)),
                    new Vector3(size.x, (float)((i + 1) * _stepH), size.z));
                if (!GeometryUtility.TestPlanesAABB(_planes, column)) continue;
                float alpha = AlphaFrom + (1 - AlphaFrom) * (float)f;
                // 칸 하나 = 블록 하나(바닥 foot부터 높이 (i+1)·stepH인 기둥)
                float h = (float)((i + 1) * _stepH);
                var blk = Take(template);
                blk.Go.transform.position = new Vector3(0, (float)foot, (float)zs);
                SetHeight(blk, h);
                blk.Ds.SetAlphaScale(alpha);
                if (blk.Stripe != null) { blk.Stripe.enabled = true; blk.Ds.SetBaseColor(blk.Stripe, StripeColor(i, blk.StripeBase)); }
                if (sideK > SideMin)
                {
                    blk.Number.gameObject.SetActive(true);
                    blk.Number.transform.localPosition = new Vector3((float)_halfW + NumberOut, h - NumberBelowTop, (float)(_stepD * 0.5));
                    blk.Number.SetText(_label[i]);
                    blk.Number.SetOpacity(sideK * alpha);
                    numbers++;
                }
                else if (blk.Number.gameObject.activeSelf) blk.Number.gameObject.SetActive(false);
            }
            for (int i = _used; i < _pool.Count; i++) Park(_pool[i]);
            ActiveNumbers = numbers;
        }

        void Begin(ClimbState c, double baseY)
        {
            _climb = c;
            _base = baseY;
            _shownAt.Clear();
            _tier.Clear();
            _label.Clear();
            for (int i = 0; i < c.K; i++)
            {
                // 새 HTML startClimb: v = (i+1)·pw, lap = floor((v − 0.001)/LAP), rem = v − lap·LAP, 색 = tierColor(rem)
                double v = (i + 1) * c.Pw, lap = Math.Floor((v - 0.001) / _lap), rem = v - lap * _lap;
                _tier.Add(RunFormulas.TierFor(_t, (int)Math.Floor(rem)));
                _label.Add(Math.Round(v).ToString(System.Globalization.CultureInfo.InvariantCulture));   // 칸 번호 = round(v)
            }
        }

        Color StripeColor(int i, Color fallback)
        {
            int tier = i < _tier.Count ? _tier[i] : -1;
            if (tier < 0) return fallback;
            foreach (var r in _t.CardTier.Datas)
                if (r.tierIndex == tier && ColorUtility.TryParseHtmlString(r.color, out var col)) return col;
            return fallback;
        }

        GameObject Template()
        {
            if (_template != null) return _template;
            if (_lease != null) return _lease.Asset;
            if (!_loading && !string.IsNullOrEmpty(_address))
            {
                _loading = true;
                LoadAsync(_address, _cts.Token).Forget();
            }
            return null;
        }

        async UniTaskVoid LoadAsync(string address, CancellationToken ct)
        {
            AddressableLease<GameObject> lease = null;
            try { lease = await PrefabLoader(address, ct); }
            catch (OperationCanceledException) { }
            if (this == null || ct.IsCancellationRequested) { lease?.Dispose(); return; }
            if (lease == null || lease.Asset == null)
            {
                // 다시 시도하지 않는다(표 주소 오류를 드러낸다). 계단 칸은 그리지 않는다.
                Debug.LogError("[ClimbStairView] 계단 칸 프리팹 로드 실패: " + address);
                lease?.Dispose();
                return;
            }
            _lease = lease;
        }

        Block Take(GameObject template)
        {
            if (_used < _pool.Count)
            {
                var b = _pool[_used++];
                if (!b.Go.activeSelf) b.Go.SetActive(true);
                return b;
            }
            var go = Instantiate(template, transform, false);
            var sg = go.GetComponent<SortingGroup>();
            if (sg == null) sg = go.AddComponent<SortingGroup>();
            sg.sortingLayerName = ObjectViewFactory.WorldLayer;
            var ds = go.GetComponent<DepthSortable>();
            if (ds == null) ds = go.AddComponent<DepthSortable>();
            ds.Rank = DepthMath.RankObject;
            ds.SortOffsetY = (float)_cfg.ObjProbeY;
            var stripe = go.transform.Find(StripeName);
            var top = go.transform.Find(TopName);
            var blk = new Block { Go = go, Ds = ds, Stripe = stripe != null ? stripe.GetComponent<SpriteRenderer>() : null,
                Front = MakeFace(go.transform.Find(FrontName)), Side = MakeFace(go.transform.Find(SideName)),
                Top = top, TopY = top != null ? top.localPosition.y : 0f, StripeTr = stripe, StripeY = stripe != null ? stripe.localPosition.y : 0f };
            if (blk.Stripe != null) blk.StripeBase = blk.Stripe.color;
            blk.Number = WorldLabel.Create(go.transform, "Number", NumberSize, _labelMaterial);   // 정렬·안개 기본 색을 잡기 전에 붙인다
            blk.Number.Text.sortingOrder = NumberOrder;
            blk.Number.gameObject.SetActive(false);
            ds.Refresh();
            _pool.Add(blk);
            _used++;
            return blk;
        }

        Face MakeFace(Transform t)
        {
            if (t == null) return null;
            var sr = t.GetComponent<SpriteRenderer>();
            if (sr == null) return null;
            bool sliced = sr.drawMode != SpriteDrawMode.Simple;
            if (!sliced && !_warnedSimple)
            {
                _warnedSimple = true;
                Debug.LogWarning("[ClimbStairView] 계단 칸 프리팹의 " + t.name + "가 Simple이라 세로 스케일로 늘립니다(테두리도 늘어남). Sprite에 9-slice 테두리를 잡고 Draw Mode를 Sliced로 바꿔 주세요.");
            }
            return new Face { Sr = sr, Tr = t, PosY = t.localPosition.y, SizeY = sliced ? sr.size.y : 0f, ScaleY = t.localScale.y, Sliced = sliced };
        }

        /// <summary>기둥 높이 h(m)에 맞춰 앞판·옆판을 늘리고 윗판·줄무늬를 올린다. 프리팹 원래 높이 = stepH.</summary>
        void SetHeight(Block b, float h)
        {
            if (Mathf.Approximately(b.Height, h)) return;
            b.Height = h;
            float k = h / (float)_stepH, up = h - (float)_stepH;
            StretchFace(b.Front, k);
            StretchFace(b.Side, k);
            if (b.Top != null) { var p = b.Top.localPosition; p.y = b.TopY + up; b.Top.localPosition = p; }
            if (b.StripeTr != null) { var p = b.StripeTr.localPosition; p.y = b.StripeY + up; b.StripeTr.localPosition = p; }
        }

        static void StretchFace(Face f, float k)
        {
            if (f == null) return;
            var p = f.Tr.localPosition; p.y = f.PosY * k; f.Tr.localPosition = p;   // 가운데 기준 면은 높이에 비례해 중심이 올라간다(아래 기준 면은 PosY = 0이라 그대로)
            if (f.Sliced) { var sz = f.Sr.size; sz.y = f.SizeY * k; f.Sr.size = sz; }
            else { var sc = f.Tr.localScale; sc.y = f.ScaleY * k; f.Tr.localScale = sc; }
        }

        static void Park(Block b)
        {
            if (b.Go.activeSelf) b.Go.SetActive(false);
        }

        void OnDestroy()
        {
            _cts.Cancel();
            _cts.Dispose();
            _lease?.Dispose();
            _lease = null;
        }
    }
}