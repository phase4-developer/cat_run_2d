using System.Collections.Generic;
using MV.Data;
using MV.Run;
using UnityEngine;
using UnityEngine.Rendering;

namespace MV.View
{
    /// <summary>
    /// 런 투사체 뷰(02 §2.1.4 「던진 장난감 L3804, 회전 장난감 L3800」·「창 궤적 L3802~3803」 행, 정본 `draw2D` L3800~3804).
    /// 던진 장난감 = 빌보드 1장(`World_Proj_{t}`, 테마 아틀라스, 로컬 z 회전 −(ms/110 + sz)), 창 = 월드 선 메쉬 2겹 + 머리 빌보드
    /// (`World_Proj_SpearHead_Fire`/`_Bolt`, `Atlas_Fx_Common`), 회전 장난감 = 플레이어 둘레 ORB_COUNT개 빌보드(orb 카드).
    /// 정렬: 순위 Unit, 탐침 = 위치(오프셋 0), 편향 sort.proj / sort.blade, 안개 알파 없음(02 §2.3.1 7·8행, §2.4.1 대상 아님).
    /// 틴트는 화면 전체 곱하기 층(<see cref="TintLayer"/>)이 덮으므로 따로 하지 않는다.
    /// 풀: 투사체 `Id`로 묶고 최대 <see cref="MaxProjs"/>개([창작], 정본·표에 상한 없음). 넘는 것은 그리지 않는다.
    /// </summary>
    [DefaultExecutionOrder(57)]
    public sealed class ProjectileView : MonoBehaviour
    {
        /// <summary>동시에 그리는 투사체 최대 수([창작] 64).</summary>
        public const int MaxProjs = 64;

        [SerializeField] Material _spriteMaterial;
        /// <summary>창 선 메쉬(정점색). 비우면 스프라이트 머티리얼(정점색·Cull Off)을 쓴다.</summary>
        [SerializeField] Material _lineMaterial;

        sealed class Item
        {
            public GameObject Go;
            public DepthSortable Ds;
            public Transform Spin;
            public SpriteRenderer Sr;
            public GameObject Line;
            public MeshFilter LineMf;
            public int Stamp;
        }

        readonly Dictionary<int, Item> _active = new Dictionary<int, Item>(MaxProjs);
        readonly Stack<Item> _free = new Stack<Item>(MaxProjs);
        readonly List<int> _drop = new List<int>(MaxProjs);
        Item[] _blades = System.Array.Empty<Item>();
        Sprite[] _projSprites = System.Array.Empty<Sprite>();
        OrbConfig _orb;
        Sprite _headFire, _headBolt;
        Mesh _lineFire, _lineOther;

        MvTables _tables;
        ViewConfig _cfg;
        WorldSpriteCache _sprites;
        string _fxAtlas, _themeAtlas;
        string[] _names;
        double _stepH, _stepD, _bump;
        int _made, _stamp;
        bool _ready;

        // `R.spinA` 대체(MV.Run이 아직 올리지 않음): 게임 시간 차 × 정본 속도. 시뮬이 SpinA를 올리기 시작하면 그 값을 쓴다.
        RunState _spinOf;
        double _spinLocal, _spinLastTime;
        bool _simSpins;

        public bool Ready => _ready;
        /// <summary>지금 보이는 투사체·창 수(시험·QA).</summary>
        public int VisibleProjs { get; private set; }
        /// <summary>지금 보이는 회전 장난감 수(시험·QA).</summary>
        public int VisibleBlades { get; private set; }
        /// <summary>만든 풀 항목 수(회전 장난감 제외, 시험·QA).</summary>
        public int PoolMade => _made;

        public void Init(MvTables tables, ViewConfig cfg, WorldSpriteCache sprites, Material fallbackMaterial, string fxAtlas)
        {
            _tables = tables;
            _cfg = cfg;
            _sprites = sprites;
            _fxAtlas = fxAtlas;
            if (_spriteMaterial == null) _spriteMaterial = fallbackMaterial;
            if (_lineMaterial == null) _lineMaterial = _spriteMaterial;
            _names = ProjLayout.NamesByTier(tables);
            _projSprites = new Sprite[_names?.Length ?? 0];
            _orb = OrbConfig.From(tables.RunConfig);
            var c = tables.Climb.Config();
            _stepH = c != null ? c["stepH"] : 0;
            _stepD = c != null ? c["stepD"] : 1;
            _bump = ViewConfig.ClimbBumpOf(tables.Climb);
            if (_blades.Length != _orb.Count)
            {
                for (int i = 0; i < _blades.Length; i++)
                {
                    if (_blades[i] == null) continue;
                    if (Application.isPlaying) Destroy(_blades[i].Go); else DestroyImmediate(_blades[i].Go);
                }
                _blades = new Item[System.Math.Max(0, _orb.Count)];
            }
            for (int i = 0; i < _blades.Length; i++)
                if (_blades[i] == null) _blades[i] = Make("Blade" + i, (float)cfg.SortBlade);
            _ready = true;
            Hide();
        }

        /// <summary>테마 아틀라스(`304_theme.worldAtlas`, 미리 로드). 캐시한 그림을 비운다.</summary>
        public void SetTheme(string worldAtlas)
        {
            _themeAtlas = worldAtlas;
            System.Array.Clear(_projSprites, 0, _projSprites.Length);
        }

        Item Make(string name, float bias)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var sg = go.AddComponent<SortingGroup>();
            sg.sortingLayerName = ObjectViewFactory.WorldLayer;
            var ds = go.AddComponent<DepthSortable>();
            ds.Rank = DepthMath.RankUnit;
            ds.Bias = bias;
            ds.SortOffsetY = 0f;
            ds.Fog = false;

            var line = new GameObject("Line", typeof(MeshFilter), typeof(MeshRenderer));
            line.transform.SetParent(go.transform, false);
            var mr = line.GetComponent<MeshRenderer>();
            mr.sharedMaterial = _lineMaterial;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;
            mr.sortingOrder = 0;
            line.SetActive(false);

            var bill = new GameObject("Bill");
            bill.transform.SetParent(go.transform, false);
            bill.AddComponent<Billboard>();
            var spin = new GameObject("Spin");
            spin.transform.SetParent(bill.transform, false);
            var sr = spin.AddComponent<SpriteRenderer>();
            if (_spriteMaterial != null) sr.sharedMaterial = _spriteMaterial;
            sr.sortingOrder = 1;   // 창 선 위에 머리

            ds.Refresh();
            go.SetActive(false);
            return new Item { Go = go, Ds = ds, Spin = spin.transform, Sr = sr, Line = line, LineMf = line.GetComponent<MeshFilter>() };
        }

        public void Hide()
        {
            foreach (var kv in _active) Release(kv.Value);
            _active.Clear();
            for (int i = 0; i < _blades.Length; i++)
                if (_blades[i] != null && _blades[i].Go.activeSelf) _blades[i].Go.SetActive(false);
            VisibleProjs = 0;
            VisibleBlades = 0;
        }

        void Release(Item it)
        {
            if (it.Go.activeSelf) it.Go.SetActive(false);
            _free.Push(it);
        }

        /// <summary>
        /// 매 프레임(시뮬 뒤). <paramref name="s"/>가 null이면 모두 숨긴다. 런이 끝났거나(`Fin`) 계단 오르기 중이면 투사체를 숨긴다
        /// (정본 L2245 `for (const b of R.projs) projG.remove(b.m)`). 회전 장난감은 끝나기 전까지 오르기 중에도 그린다(정본 blades는 unitG).
        /// </summary>
        public void Sync(RunState s, double nowSec)
        {
            if (!_ready || s == null) { Hide(); return; }
            double wy = s.WorldY;
            bool fire = ProjLayout.IsFire(s.El);
            _stamp++;
            int visible = 0;
            if (!s.Fin && s.Phase != RunPhase.Climb)
            {
                var projs = s.Projs;
                for (int i = 0; i < projs.Count; i++)
                {
                    var b = projs[i];
                    if (b == null || b.Dead) continue;
                    if (!_active.TryGetValue(b.Id, out var it))
                    {
                        it = Rent();
                        if (it == null) continue;   // 풀 최대치를 넘음
                        _active[b.Id] = it;
                    }
                    it.Stamp = _stamp;
                    if (PlaceProj(it, b, fire, wy, nowSec)) visible++;
                }
            }
            _drop.Clear();
            foreach (var kv in _active) if (kv.Value.Stamp != _stamp) _drop.Add(kv.Key);
            for (int i = 0; i < _drop.Count; i++)
            {
                Release(_active[_drop[i]]);
                _active.Remove(_drop[i]);
            }
            VisibleProjs = visible;
            VisibleBlades = SyncBlades(s, fire, wy, nowSec);
        }

        Item Rent()
        {
            if (_free.Count > 0) return _free.Pop();
            if (_made >= MaxProjs) return null;
            _made++;
            return Make("Proj" + _made, (float)_cfg.SortProj);
        }

        bool PlaceProj(Item it, RunProjectile b, bool fire, double wy, double nowSec)
        {
            Sprite sprite;
            if (b.Spear)
            {
                sprite = SpearHead(fire);
                var mesh = SpearLine(fire);
                if (it.LineMf.sharedMesh != mesh) it.LineMf.sharedMesh = mesh;
                if (!it.Line.activeSelf) it.Line.SetActive(true);
                it.Spin.localRotation = Quaternion.identity;
                it.Spin.localScale = Vector3.one;
            }
            else
            {
                sprite = ProjSprite(b.Tier);
                if (it.Line.activeSelf) it.Line.SetActive(false);
                it.Spin.localRotation = Quaternion.Euler(0f, 0f, ProjLayout.ToDegrees(ProjLayout.ProjAngle(nowSec, b.Sz)));
                float k = (float)(ProjLayout.ProjRadius / ProjLayout.BakedRadius);
                it.Spin.localScale = new Vector3(k, k, 1f);
            }
            if (sprite == null)
            {
                if (it.Go.activeSelf) it.Go.SetActive(false);
                return false;
            }
            if (it.Sr.sprite != sprite) it.Sr.sprite = sprite;
            it.Go.transform.position = new Vector3((float)b.X, (float)(wy + ProjLayout.ProjY), (float)b.Z);
            if (!it.Go.activeSelf) it.Go.SetActive(true);
            return true;
        }

        int SyncBlades(RunState s, bool fire, double wy, double nowSec)
        {
            bool orb = ProjLayout.HasEffect(s, _tables, CardEffectType.Orbit);
            bool spin = ProjLayout.HasEffect(s, _tables, CardEffectType.Spin);
            double spinA = SpinAngle(s, orb, spin);
            if (!orb || s.Fin)
            {
                for (int i = 0; i < _blades.Length; i++) if (_blades[i].Go.activeSelf) _blades[i].Go.SetActive(false);
                return 0;
            }
            double px = s.P.X, pz = s.P.Z;
            var C = s.Phase == RunPhase.Climb ? s.Climb : null;
            if (C != null) CameraModel.ClimbPlayer(C.Pos, C.K, C.Z0, _stepH, _stepD, _bump, out _, out pz);
            int visible = 0;
            for (int i = 0; i < _blades.Length; i++)
            {
                var it = _blades[i];
                var sprite = ProjSprite(ProjLayout.BladeTier(i, fire));
                if (sprite == null)
                {
                    if (it.Go.activeSelf) it.Go.SetActive(false);
                    continue;
                }
                if (it.Sr.sprite != sprite) it.Sr.sprite = sprite;
                ProjLayout.BladePos(spinA, i, _orb, px, pz, out double x, out double y, out double z);
                it.Go.transform.position = new Vector3((float)x, (float)(wy + y), (float)z);
                it.Spin.localRotation = Quaternion.Euler(0f, 0f, ProjLayout.ToDegrees(ProjLayout.BladeAngle(nowSec, i)));
                float k = (float)(ProjLayout.BladeRadius / ProjLayout.BakedRadius);
                it.Spin.localScale = new Vector3(k, k, 1f);
                if (!it.Go.activeSelf) it.Go.SetActive(true);
                visible++;
            }
            return visible;
        }

        /// <summary>
        /// 회전 장난감 각(정본 `R.spinA`). 교체 지점: MV.Run이 `RunState.SpinA`를 정본처럼 올리면(0이 아니게 되면) 그 값을 그대로 쓴다.
        /// 그 전에는 게임 시간(`RunState.Time`) 차 × (orb 4 + spin 5)로 뷰가 올린다.
        /// </summary>
        double SpinAngle(RunState s, bool orb, bool spin)
        {
            if (!ReferenceEquals(s, _spinOf))
            {
                _spinOf = s;
                _spinLocal = 0;
                _spinLastTime = s.Time;
                _simSpins = false;
            }
            double dt = s.Time - _spinLastTime;
            _spinLastTime = s.Time;
            if (dt > 0) _spinLocal += dt * ProjLayout.SpinRate(orb, spin, _orb);
            if (s.SpinA != 0) _simSpins = true;
            return _simSpins ? s.SpinA : _spinLocal;
        }

        Sprite ProjSprite(int tier)
        {
            if (_projSprites.Length == 0) return null;
            int t = ProjLayout.ArtTier(tier, _names);
            var sp = _projSprites[t];
            if (sp != null) return sp;
            if (_sprites == null || !_sprites.IsLoaded(_themeAtlas)) return null;
            sp = _sprites.Get(_themeAtlas, ProjLayout.ProjSpriteName(t, _names));
            _projSprites[t] = sp;
            return sp;
        }

        Sprite SpearHead(bool fire)
        {
            var sp = fire ? _headFire : _headBolt;
            if (sp != null) return sp;
            if (_sprites == null || !_sprites.IsLoaded(_fxAtlas)) return null;
            sp = _sprites.Get(_fxAtlas, fire ? ProjLayout.SpearHeadFire : ProjLayout.SpearHeadBolt);
            if (fire) _headFire = sp; else _headBolt = sp;
            return sp;
        }

        /// <summary>창 선(머리 기준 로컬): 뒤 2.8 m → 머리, XZ 평면 띠 2겹(넓은 반투명 색 + 가는 흰색). 끝은 캔버스 기본 butt.</summary>
        Mesh SpearLine(bool fire)
        {
            var m = fire ? _lineFire : _lineOther;
            if (m != null) return m;
            var mb = new MeshBuffer();
            string el = fire ? "fire" : null;
            AddBand(mb, ProjLayout.SpearWide, MvColor.WithAlpha(ProjLayout.SpearColor(el), ProjLayout.SpearWideAlpha));
            AddBand(mb, ProjLayout.SpearCore, MvColor.ToColor32(ProjLayout.SpearCoreColor));
            m = new Mesh { name = fire ? "SpearLine_Fire" : "SpearLine" };
            mb.ApplyTo(m);
            m.RecalculateBounds();
            if (fire) _lineFire = m; else _lineOther = m;
            return m;
        }

        static void AddBand(MeshBuffer mb, double width, Color32 col)
        {
            ProjLayout.SpearTailPoint(0, 0, 0, out _, out _, out double tz);
            float h = (float)(width / 2), z0 = (float)tz;
            mb.AddQuad(new Vector3(-h, 0f, z0), new Vector3(h, 0f, z0), new Vector3(h, 0f, 0f), new Vector3(-h, 0f, 0f), col);
        }

        void OnDestroy()
        {
            if (_lineFire != null) Destroy(_lineFire);
            if (_lineOther != null) Destroy(_lineOther);
        }
    }
}
