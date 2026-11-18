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
    /// `Top`·칸 번호는 늘어난 높이만큼 위로 옮긴다. 프리팹 루트 기준점 = 블록 바닥·앞면(지금과 같음).
    /// 칸 색 = 칸마다 프리팹을 고른다(노션 「신작 배경 리소스 관리」 계단연출 2절): 칸의 남은 칸 수(새 HTML rem)가 받는 카드 등급 행의
    /// `115_cardTier.climbStep`(Obj_ClimbStep_C·_UC·_R·_E·_L), 첫 등급 기준 아래는 `330_climb.stepPrefab`(Obj_ClimbStep). 등급 기준은 115 need 하나뿐이다.
    /// 칸은 시뮬이 보이기 시작한 순서(<see cref="ClimbState.StepsShown"/>)대로 popSec 동안 0에서 커지며 stepPop배(기획서 1.1배)까지 튕겼다 1배로 선다
    /// (<see cref="PopScale"/>, 블록 바닥·앞면 기준).
    /// 옆 시점이면 꼭대기 옆면에 칸 번호. 블록은 카메라 시야 안의 것만 풀에서 꺼내 쓴다. 오르기가 끝난 뒤에도 같은 런이면 마지막 계단을 그대로 둔다(HTML `LASTC`).
    /// </summary>
    [DefaultExecutionOrder(56)]
    public sealed class ClimbStairView : MonoBehaviour
    {
        /// <summary>새 HTML 칸 번호 글자: 높이 0.28 m, 꼭대기 아래 0.45 m, 옆면 바깥 0.01 m. 옆 시점 비율 = (카메라 x − 반폭)/3 m, 0.05 이하면 안 그림.</summary>
        public const float NumberBelowTop = 0.45f, NumberOut = 0.01f, SideFadeM = 3f, SideMin = 0.05f;
        /// <summary>막 생긴 칸의 최소 크기(크기 0 변환을 피하는 값, 눈에는 안 보임).</summary>
        const float MinPop = 1e-3f;
        /// <summary>칸 번호는 블록 안 다른 그림 위에 그린다(정렬 그룹 안 순서).</summary>
        const string FrontName = "Front", SideName = "Side", TopName = "Top";
        const string PrefabFolder = "Assets/Game/Addressables/Local/BG/Prefab/Object/";

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
            public WorldLabel Number;
            public Face Front, Side;
            public Transform Top;
            public float TopY;
            public float Height = -1f;   // 마지막으로 맞춘 기둥 높이(같으면 다시 안 바꿈)
        }

        bool _warnedSimple;
        /// <summary>정렬 부품이 빠져 그리지 않는 계단 칸 프리팹·이미 경고한 프리팹(프리팹마다 한 번).</summary>
        readonly HashSet<GameObject> _badTemplates = new HashSet<GameObject>(), _warnedTemplates = new HashSet<GameObject>();

        MvTables _t;
        ViewConfig _cfg;
        Camera _cam;
        /// <summary>칸 종류별 프리팹 주소: [0] = 첫 등급 아래(330 stepPrefab), [tierIndex + 1] = 115 climbStep.</summary>
        string[] _addresses = Array.Empty<string>();
        double _stepH, _stepD, _halfW, _popSec, _popBack, _lap;
        Func<string, GameObject> _templates;
        readonly Dictionary<string, AddressableLease<GameObject>> _leases = new Dictionary<string, AddressableLease<GameObject>>();
        readonly HashSet<string> _loading = new HashSet<string>();
        bool _ready;
        readonly CancellationTokenSource _cts = new CancellationTokenSource();

        RunState _run;
        ClimbState _climb;
        double _base;
        readonly List<double> _shownAt = new List<double>();
        readonly List<int> _kind = new List<int>();
        readonly List<string> _label = new List<string>();
        readonly List<List<Block>> _pools = new List<List<Block>>();
        int[] _used = Array.Empty<int>();
        readonly Plane[] _planes = new Plane[6];

        /// <summary>시험·QA: 이번 프레임에 켠 블록 수(= 보이는 칸 수), 칸 번호를 켠 블록 수.</summary>
        public int ActiveBlocks { get; private set; }
        public int ActiveNumbers { get; private set; }
        /// <summary>칸 종류별 프리팹 주소([0] = 첫 등급 아래, [tierIndex + 1] = 등급).</summary>
        public IReadOnlyList<string> Addresses => _addresses;
        /// <summary>시험·QA: 칸 i의 프리팹 주소(오르기 시작 뒤).</summary>
        public string AddressOf(int i) => i >= 0 && i < _kind.Count ? _addresses[_kind[i]] : null;

        public void Init(MvTables tables, ViewConfig cfg, Camera cam)
        {
            _t = tables ?? throw new ArgumentNullException(nameof(tables));
            _cfg = cfg ?? throw new ArgumentNullException(nameof(cfg));
            _cam = cam;
            var c = tables.Climb.Config() ?? throw new InvalidOperationException(ClimbData.Table + ": 설정 행(default)이 없습니다");
            int tiers = 0;
            foreach (var r in tables.CardTier.Datas) tiers = Math.Max(tiers, r.tierIndex + 1);
            _addresses = new string[tiers + 1];
            _addresses[0] = c.stepPrefab;
            foreach (var r in tables.CardTier.Datas) _addresses[r.tierIndex + 1] = r.climbStep;
            for (int i = 0; i < _addresses.Length; i++)
                if (string.IsNullOrEmpty(_addresses[i])) throw new InvalidOperationException(CardTierData.Table + ": tierIndex " + (i - 1) + " 행이 없어 계단 칸 프리팹(climbStep)을 정할 수 없습니다");
            _used = new int[_addresses.Length];
            while (_pools.Count < _addresses.Length) _pools.Add(new List<Block>());
            _stepH = c["stepH"];
            _stepD = c["stepD"];
            _halfW = c["stepW"] * 0.5;
            _popSec = c["popSec"];
            _popBack = PopBack(c["stepPop"]);
            _lap = c["lap"];
            _ready = true;
            Hide();
        }

        /// <summary>시험: Addressables 대신 주소로 프리팹을 바로 준다.</summary>
        public void UseTemplates(Func<string, GameObject> byAddress) => _templates = byAddress;

        public void Hide()
        {
            foreach (var pool in _pools) for (int i = 0; i < pool.Count; i++) Park(pool[i]);
            for (int k = 0; k < _used.Length; k++) _used[k] = 0;
            ActiveBlocks = 0;
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
            if (C == null || C.K <= 0 || _cam == null) { Hide(); return; }

            bool ended = live == null || C.EndAt >= 0;   // 끝(새 HTML p ≥ 1, t − sideLead ≥ T)에는 모든 칸 t = 1
            while (_shownAt.Count < Math.Min(C.K, C.StepsShown)) _shownAt.Add(C.Time);
            int shown = ended ? C.K : _shownAt.Count;

            GeometryUtility.CalculateFrustumPlanes(_cam, _planes);
            float sideK = Mathf.Clamp01((float)((_cam.transform.position.x - _halfW) / SideFadeM));
            for (int k = 0; k < _used.Length; k++) _used[k] = 0;
            int numbers = 0, blocks = 0;
            var size = new Vector3((float)(_halfW * 2), (float)_stepH, (float)_stepD);   // 블록 1칸 크기(컬링 상자 가로·깊이)
            for (int i = 0; i < shown; i++)
            {
                double f = ended || _popSec <= 0 ? 1 : Math.Min(1, (C.Time - _shownAt[i]) / _popSec);
                float pop = Mathf.Max(MinPop, (float)PopScale(f, _popBack));   // 막 생긴 칸도 블록은 둔다(크기 0 행렬 피함)
                double zs = C.Z0 + i * _stepD, foot = _base;
                var column = new Bounds(new Vector3(0, (float)(foot + (i + 1) * _stepH * 0.5), (float)(zs + _stepD * 0.5)),
                    new Vector3(size.x, (float)((i + 1) * _stepH), size.z));
                if (!GeometryUtility.TestPlanesAABB(_planes, column)) continue;
                // 칸 하나 = 블록 하나(바닥 foot부터 높이 (i+1)·stepH인 기둥)
                float h = (float)((i + 1) * _stepH);
                int kind = _kind[i];
                var template = Template(kind);
                if (template == null) continue;   // 그 등급 프리팹을 아직 불러오는 중
                var blk = Take(kind, template);
                if (blk == null) continue;   // 프리팹 부품이 빠진 등급(경고 1번)
                blocks++;
                blk.Go.transform.position = new Vector3(0, (float)foot, (float)zs);
                blk.Go.transform.localScale = new Vector3(pop, pop, pop);
                SetHeight(blk, h);
                if (blk.Number != null && sideK > SideMin)
                {
                    blk.Number.gameObject.SetActive(true);
                    blk.Number.transform.localPosition = new Vector3((float)_halfW + NumberOut, h - NumberBelowTop, (float)(_stepD * 0.5));
                    blk.Number.SetText(_label[i]);
                    blk.Number.SetOpacity(sideK);
                    numbers++;
                }
                else if (blk.Number != null && blk.Number.gameObject.activeSelf) blk.Number.gameObject.SetActive(false);
            }
            for (int k = 0; k < _pools.Count; k++)
                for (int i = _used[k]; i < _pools[k].Count; i++) Park(_pools[k][i]);
            ActiveBlocks = blocks;
            ActiveNumbers = numbers;
        }

        void Begin(ClimbState c, double baseY)
        {
            _climb = c;
            _base = baseY;
            _shownAt.Clear();
            _kind.Clear();
            _label.Clear();
            for (int i = 0; i < c.K; i++)
            {
                // 새 HTML startClimb: v = (i+1)·pw, lap = floor((v − 0.001)/LAP), rem = v − lap·LAP, 색 = tierColor(rem) → 그 등급 칸 프리팹
                double v = (i + 1) * c.Pw, lap = Math.Floor((v - 0.001) / _lap), rem = v - lap * _lap;
                _kind.Add(RunFormulas.TierFor(_t, (int)Math.Floor(rem)) + 1);
                _label.Add(Math.Round(v).ToString(System.Globalization.CultureInfo.InvariantCulture));   // 칸 번호 = round(v)
            }
        }

        GameObject Template(int kind)
        {
            string address = _addresses[kind];
            if (_templates != null) return _templates(address);
            if (_leases.TryGetValue(address, out var lease)) return lease.Asset;
            if (_loading.Add(address)) LoadAsync(address, _cts.Token).Forget();
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
                // 다시 시도하지 않는다(표 주소 오류를 드러낸다). 그 등급 칸은 그리지 않는다.
                Debug.LogWarning("[ClimbStairView] 계단 칸 프리팹 로드 실패: " + address);
                lease?.Dispose();
                return;
            }
            _leases[address] = lease;
        }

        Block Take(int kind, GameObject template)
        {
            var pool = _pools[kind];
            if (_used[kind] < pool.Count)
            {
                var b = pool[_used[kind]++];
                if (!b.Go.activeSelf) b.Go.SetActive(true);
                return b;
            }
            if (_badTemplates.Contains(template)) return null;
            var sg = template.GetComponent<SortingGroup>();
            var ds0 = template.GetComponent<DepthSortable>();
            bool hasNumber = template.GetComponentInChildren<WorldLabel>(true) != null;
            if ((sg == null || ds0 == null || !hasNumber) && _warnedTemplates.Add(template))
            {
                // 프리팹에 없는 부품은 만들지 않는다. 정렬 부품이 빠지면 그 등급 칸은 그리지 않고, 번호 글자만 빠지면 번호 없이 그린다.
                Debug.LogWarning($"[ClimbStairView] 계단 칸 프리팹 {template.name}에 빠진 부품이 있습니다(SortingGroup {sg != null}·DepthSortable {ds0 != null}·번호 글자 WorldLabel {hasNumber}). " +
                    $"{PrefabFolder}의 프리팹에 넣어 주세요. 만들지 않습니다.", this);
            }
            if (sg == null || ds0 == null) { _badTemplates.Add(template); return null; }
            var go = Instantiate(template, transform, false);
            go.GetComponent<SortingGroup>().sortingLayerName = ObjectViewFactory.WorldLayer;
            var ds = go.GetComponent<DepthSortable>();
            var number = go.GetComponentInChildren<WorldLabel>(true);
            ds.Rank = DepthMath.RankObject;
            ds.SortOffsetY = (float)_cfg.ObjProbeY;
            var top = go.transform.Find(TopName);
            var blk = new Block { Go = go, Ds = ds,
                Front = MakeFace(go.transform.Find(FrontName)), Side = MakeFace(go.transform.Find(SideName)),
                Top = top, TopY = top != null ? top.localPosition.y : 0f };
            blk.Number = number;   // 글꼴·크기·정렬 순서는 프리팹 값
            if (number != null) number.gameObject.SetActive(false);
            ds.Refresh();
            pool.Add(blk);
            _used[kind]++;
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

        /// <summary>기둥 높이 h(m)에 맞춰 앞판·옆판을 늘리고 윗판을 올린다. 프리팹 원래 높이 = stepH.</summary>
        void SetHeight(Block b, float h)
        {
            if (Mathf.Approximately(b.Height, h)) return;
            b.Height = h;
            float k = h / (float)_stepH, up = h - (float)_stepH;
            StretchFace(b.Front, k);
            StretchFace(b.Side, k);
            if (b.Top != null) { var p = b.Top.localPosition; p.y = b.TopY + up; b.Top.localPosition = p; }
        }

        static void StretchFace(Face f, float k)
        {
            if (f == null) return;
            var p = f.Tr.localPosition; p.y = f.PosY * k; f.Tr.localPosition = p;   // 가운데 기준 면은 높이에 비례해 중심이 올라간다(아래 기준 면은 PosY = 0이라 그대로)
            if (f.Sliced) { var sz = f.Sr.size; sz.y = f.SizeY * k; f.Sr.size = sz; }
            else { var sc = f.Tr.localScale; sc.y = f.ScaleY * k; f.Tr.localScale = sc; }
        }

        /// <summary>
        /// 칸이 생기는 크기(f = 0 → 0, f = 1 → 1). 뒤로 넘는 이징 y = 1 + (b + 1)(f − 1)³ + b(f − 1)²이고, 가장 클 때가 <see cref="PopBack"/>로 맞춘 stepPop배다.
        /// </summary>
        public static double PopScale(double f, double back)
        {
            if (f <= 0) return 0;
            if (f >= 1) return 1;
            double u = f - 1;
            return 1 + (back + 1) * u * u * u + back * u * u;
        }

        /// <summary>최댓값이 peak(예 1.1)가 되는 이징 계수 b. 최댓값 − 1 = 4b³ / (27(b + 1)²)는 b에 대해 늘어나므로 이분법으로 푼다. peak ≤ 1이면 0(넘지 않음).</summary>
        public static double PopBack(double peak)
        {
            double over = peak - 1;
            if (over <= 0) return 0;
            double lo = 0, hi = 1;
            while (Over(hi) < over) hi *= 2;
            for (int i = 0; i < 60; i++)
            {
                double mid = (lo + hi) * 0.5;
                if (Over(mid) < over) lo = mid; else hi = mid;
            }
            return (lo + hi) * 0.5;

            static double Over(double b) => 4 * b * b * b / (27 * (b + 1) * (b + 1));
        }

        static void Park(Block b)
        {
            if (b.Go.activeSelf) b.Go.SetActive(false);
        }

        void OnDestroy()
        {
            _cts.Cancel();
            _cts.Dispose();
            foreach (var l in _leases.Values) l?.Dispose();
            _leases.Clear();
        }
    }
}