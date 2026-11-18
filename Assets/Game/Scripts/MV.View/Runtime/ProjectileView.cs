using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using jjevol;
using MV.Data;
using MV.Run;
using UnityEngine;

namespace MV.View
{
    /// <summary>
    /// 런 투사체 관리자(02 §2.1.4 「던진 장난감 L3804, 회전 장난감 L3800」·「창 궤적 L3802~3803」 행, 정본 `draw2D` L3800~3804).
    /// 생성·연결·풀·회수와 그림 임대만 한다. 그리기는 종류별 프리팹 모듈이 한다:
    /// 던진 장난감 = <see cref="ToyProjectile"/>(`Proj_Toy`), 창 = <see cref="SpearProjectile"/>(`Proj_Spear`), 회전 장난감 = <see cref="OrbBlade"/>(`Proj_OrbBlade`, ORB_COUNT개).
    /// 그림(`103_item.projSprite`)·마법탄 프리팹(`300 proj.prefab`)·꼬리(`projTail`)·가운데(`projCenter`)·창 장식은 Init 때 모두 읽기 시작하고, 읽히기 전에는 그 투사체를 그리지 않는다.
    /// 틴트는 화면 전체 곱하기 층(<see cref="TintLayer"/>)이 덮으므로 따로 하지 않는다.
    /// 풀: 투사체 `Id`로 묶고 최대 <see cref="MaxProjs"/>개([창작], 정본·표에 상한 없음). 넘는 것은 그리지 않는다.
    /// </summary>
    [DefaultExecutionOrder(57)]
    public sealed class ProjectileView : MonoBehaviour
    {
        /// <summary>동시에 그리는 투사체 최대 수([창작] 64).</summary>
        public const int MaxProjs = 64;

        [SerializeField] ToyProjectile _toyPrefab;
        [SerializeField] SpearProjectile _spearPrefab;
        [SerializeField] OrbBlade _bladePrefab;

        readonly Dictionary<int, ProjectileSlot> _active = new Dictionary<int, ProjectileSlot>(MaxProjs);
        readonly Stack<ToyProjectile> _freeToys = new Stack<ToyProjectile>(MaxProjs);
        readonly Stack<SpearProjectile> _freeSpears = new Stack<SpearProjectile>(MaxProjs);
        readonly List<int> _drop = new List<int>(MaxProjs);
        OrbBlade[] _blades = System.Array.Empty<OrbBlade>();
        Sprite[] _projSprites = System.Array.Empty<Sprite>();
        readonly Dictionary<string, AddressableLease<Sprite>> _spriteLeases = new Dictionary<string, AddressableLease<Sprite>>(System.StringComparer.Ordinal);
        readonly Dictionary<string, AddressableLease<GameObject>> _prefabLeases = new Dictionary<string, AddressableLease<GameObject>>(System.StringComparer.Ordinal);
        readonly HashSet<string> _loading = new HashSet<string>(System.StringComparer.Ordinal);
        readonly CancellationTokenSource _cts = new CancellationTokenSource();
        string _projPrefab;
        string[] _tails;
        string[] _centers;
        OrbConfig _orb;
        Sprite _headFire, _headBolt;
        Material _imageMaterial;

        MvTables _tables;
        ViewConfig _cfg;
        WorldSpriteCache _sprites;
        string _fxAtlas;
        string[] _names;
        ClimbPath _path;
        int _made, _stamp;
        bool _ready;

        // `R.spinA` 대체(MV.Run이 아직 올리지 않음): 게임 시간 차 × 정본 속도. 시뮬이 SpinA를 올리기 시작하면 그 값을 쓴다.
        RunState _spinOf;
        double _spinLocal, _spinLastTime;
        bool _simSpins;

        /// <summary>창 장식 파티클 주소(불 / 그 외). 프리팹이 아직 없으면 장식 없이 창만 그린다.</summary>
        const string SpearFxBolt = "VFX_Spear_Bolt", SpearFxFire = "VFX_Spear_Fire";

        public bool Ready => _ready;
        /// <summary>지금 보이는 투사체·창 수(시험·QA).</summary>
        public int VisibleProjs { get; private set; }
        /// <summary>지금 보이는 회전 장난감 수(시험·QA).</summary>
        public int VisibleBlades { get; private set; }
        /// <summary>만든 풀 항목 수(회전 장난감 제외, 시험·QA).</summary>
        public int PoolMade => _made;

        /// <param name="imageMaterial">마법탄 프리팹(외부 Addressables 프리팹) proj_Image에 넣을 월드 스프라이트 머티리얼.</param>
        public void Init(MvTables tables, ViewConfig cfg, WorldSpriteCache sprites, Material imageMaterial, string fxAtlas)
        {
            if (_toyPrefab == null || _spearPrefab == null || _bladePrefab == null
                || !_toyPrefab.HasRefs || !_spearPrefab.HasRefs || !_bladePrefab.HasRefs)
            {
                Debug.LogWarning("[ProjectileView] 투사체 프리팹(Proj_Toy·Proj_Spear·Proj_OrbBlade) 참조가 비었습니다.", this);
                return;
            }
            _tables = tables;
            _cfg = cfg;
            _sprites = sprites;
            _fxAtlas = fxAtlas;
            _imageMaterial = imageMaterial;
            _names = ProjLayout.NamesByTier(tables);
            _tails = ProjLayout.TailsByTier(tables);
            _centers = ProjLayout.CentersByTier(tables);
            _projSprites = new Sprite[_names?.Length ?? 0];
            _projPrefab = tables.RunConfig.Raw(RunConfigKeys.Proj_Prefab);
            if (_sprites != null) LoadArt();
            _orb = OrbConfig.From(tables.RunConfig);
            _path = ClimbPath.From(tables.Climb);
            if (_blades.Length != _orb.Count)
            {
                foreach (var b in _blades) if (b != null) Discard(b.gameObject);
                _blades = new OrbBlade[System.Math.Max(0, _orb.Count)];
            }
            for (int i = 0; i < _blades.Length; i++)
                if (_blades[i] == null) _blades[i] = Spawn(_bladePrefab, "Blade" + i, (float)cfg.SortBlade);
            _ready = true;
            Hide();
        }

        static void Discard(GameObject go)
        {
            if (Application.isPlaying) Destroy(go); else DestroyImmediate(go);
        }

        T Spawn<T>(T prefab, string name, float bias) where T : ProjectileSlot
        {
            var slot = Instantiate(prefab, transform, false);
            slot.name = name;
            slot.SetBias(bias);
            slot.Hide();
            return slot;
        }

        /// <summary>마법탄 프리팹·등급 그림·등급 꼬리·가운데 이펙트·창 장식을 읽기 시작한다(같은 주소는 한 번). 그림 캐시가 없는 시험에서는 부르지 않는다.</summary>
        void LoadArt()
        {
            var ct = _cts.Token;
            LoadPrefab(_projPrefab, ct);
            if (_names != null) foreach (var n in _names) LoadSprite(n, ct);
            if (_tails != null) foreach (var n in _tails) LoadPrefab(n, ct);
            if (_centers != null) foreach (var n in _centers) LoadPrefab(n, ct);
            LoadPrefab(SpearFxBolt, ct);
            LoadPrefab(SpearFxFire, ct);
        }

        void LoadSprite(string address, CancellationToken ct)
        {
            if (string.IsNullOrEmpty(address) || _spriteLeases.ContainsKey(address) || !_loading.Add("s:" + address)) return;
            LoadSpriteAsync(address, ct).Forget();
        }

        void LoadPrefab(string address, CancellationToken ct)
        {
            if (string.IsNullOrEmpty(address) || _prefabLeases.ContainsKey(address) || !_loading.Add("p:" + address)) return;
            LoadPrefabAsync(address, ct).Forget();
        }

        async UniTaskVoid LoadSpriteAsync(string address, CancellationToken ct)
        {
            try
            {
                var lease = await AddressableScope.Get<Sprite>(address, ct);
                if (this == null) { lease?.Dispose(); return; }
                if (lease == null || lease.Asset == null) { Debug.LogError($"ProjectileView: 마법탄 그림 로드 실패 {address}"); lease?.Dispose(); return; }
                _spriteLeases[address] = lease;
                System.Array.Clear(_projSprites, 0, _projSprites.Length);
            }
            catch (System.OperationCanceledException)
            {
            }
        }

        async UniTaskVoid LoadPrefabAsync(string address, CancellationToken ct)
        {
            try
            {
                var lease = await AddressableScope.GetPrefab(address, ct);
                if (this == null) { lease?.Dispose(); return; }
                if (lease == null || lease.Asset == null) { Debug.LogWarning($"ProjectileView: 프리팹 로드 실패 {address}"); lease?.Dispose(); return; }
                if (address == _projPrefab && lease.Asset.GetComponent<ProjectileRig>() == null)
                {
                    Debug.LogWarning($"ProjectileView: {address}에 {nameof(ProjectileRig)}가 없습니다");
                    lease.Dispose();
                    return;
                }
                _prefabLeases[address] = lease;
            }
            catch (System.OperationCanceledException)
            {
            }
        }

        GameObject Prefab(string address) => address != null && _prefabLeases.TryGetValue(address, out var l) ? l.Asset : null;

        public void Hide()
        {
            foreach (var kv in _active) Release(kv.Value);
            _active.Clear();
            foreach (var b in _blades) if (b != null) b.Hide();
            VisibleProjs = 0;
            VisibleBlades = 0;
        }

        void Release(ProjectileSlot slot)
        {
            slot.Hide();
            if (slot is ToyProjectile toy) _freeToys.Push(toy);
            else if (slot is SpearProjectile spear) _freeSpears.Push(spear);
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
                    if (_active.TryGetValue(b.Id, out var slot) && (slot is SpearProjectile) != b.Spear)
                    {
                        Release(slot);   // 같은 번호가 다른 종류로 다시 쓰였다
                        _active.Remove(b.Id);
                        slot = null;
                    }
                    if (slot == null)
                    {
                        slot = Rent(b.Spear);
                        if (slot == null) continue;   // 풀 최대치를 넘음
                        slot.Fresh = true;
                        _active[b.Id] = slot;
                    }
                    slot.Stamp = _stamp;
                    var pos = new Vector3((float)b.X, (float)(wy + ProjLayout.ProjY), (float)b.Z);
                    bool shown = slot is SpearProjectile spear
                        ? spear.Place(SpearHead(fire), fire ? SpearFxFire : SpearFxBolt, Prefab(fire ? SpearFxFire : SpearFxBolt), pos)
                        : PlaceToy((ToyProjectile)slot, b, pos, nowSec);
                    if (shown) visible++;
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

        ProjectileSlot Rent(bool spear)
        {
            if (spear && _freeSpears.Count > 0) return _freeSpears.Pop();
            if (!spear && _freeToys.Count > 0) return _freeToys.Pop();
            if (_made >= MaxProjs) return null;
            _made++;
            float bias = (float)_cfg.SortProj;
            return spear ? Spawn(_spearPrefab, "Spear" + _made, bias) : (ProjectileSlot)Spawn(_toyPrefab, "Toy" + _made, bias);
        }

        bool PlaceToy(ToyProjectile toy, RunProjectile b, Vector3 pos, double nowSec)
        {
            string tail = ProjLayout.ProjSpriteName(b.Tier, _tails);
            string center = ProjLayout.ProjSpriteName(b.Tier, _centers);
            return toy.Place(Prefab(_projPrefab), _projPrefab, _imageMaterial, ProjSprite(b.Tier),
                tail, Prefab(tail), center, Prefab(center), ProjLayout.ToDegrees(ProjLayout.ProjAngle(nowSec, b.Sz)), pos);
        }

        int SyncBlades(RunState s, bool fire, double wy, double nowSec)
        {
            bool orb = ProjLayout.HasEffect(s, _tables, CardEffectType.Orbit);
            bool spin = ProjLayout.HasEffect(s, _tables, CardEffectType.Spin);
            double spinA = SpinAngle(s, orb, spin);
            if (!orb || s.Fin)
            {
                foreach (var b in _blades) b.Hide();
                return 0;
            }
            double px = s.P.X, pz = s.P.Z;
            var C = s.Phase == RunPhase.Climb ? s.Climb : null;
            if (C != null) _path.Cat(C.Pos, C.K, C.Z0, out _, out pz);
            float k = (float)(ProjLayout.BladeRadius / ProjLayout.BakedRadius);
            int visible = 0;
            for (int i = 0; i < _blades.Length; i++)
            {
                ProjLayout.BladePos(spinA, i, _orb, px, pz, out double x, out double y, out double z);
                var pos = new Vector3((float)x, (float)(wy + y), (float)z);
                if (_blades[i].Place(ProjSprite(ProjLayout.BladeTier(i, fire)), pos, ProjLayout.ToDegrees(ProjLayout.BladeAngle(nowSec, i)), k)) visible++;
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
            string address = ProjLayout.ProjSpriteName(t, _names);
            if (address == null || !_spriteLeases.TryGetValue(address, out var lease)) return null;
            sp = lease.Asset;
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

        void OnDestroy()
        {
            _cts.Cancel();
            _cts.Dispose();
            foreach (var l in _spriteLeases.Values) l?.Dispose();
            foreach (var l in _prefabLeases.Values) l?.Dispose();
            _spriteLeases.Clear();
            _prefabLeases.Clear();
        }
    }
}
