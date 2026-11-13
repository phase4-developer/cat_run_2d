using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using MV.Data;
using MV.Run;
using UnityEngine;
using UnityEngine.Rendering;

namespace MV.View
{
    /// <summary>
    /// 런 캐릭터 뷰(MV-SPEC-02 ArtResourcePlan §5, `CatView` 대체): 플레이어 1 + 분신(`RunState.Clones`)을 캐릭터 프리팹으로 그린다.
    /// 외형은 `116_charLook`(<see cref="CharLookResolver"/>: catId·자리·무기 등급 → 행)이 정하고 코드에 그림·아틀라스 이름이 없다.
    /// 플레이어 = `101_cat` key player 행, 분신 = key friend 행, 분신 i번째(0부터) = slot i. 등급 = 장착 최고 등급(<see cref="RunState.EqTier"/>).
    /// 프리팹은 <see cref="CharacterPool"/>, 그림은 행 아틀라스(<see cref="WorldSpriteCache"/>)에서. 지금 행 + 다음 등급 행 아틀라스를 미리 읽고 캐릭터 묶음별 최근 3개만 둔다(03 §3.8.8).
    /// 그림은 <see cref="CharStateMachine"/>이 고른다: 런 중 Walk, 끝나면 Stand, 계단 정산(오르기 단계) 중 플레이어 Stair(분신은 Walk — 정산 중 분신 숨김은 카메라 궤도 회전과 함께 5단계).
    /// 쏜 순간은 런 이벤트 Fire(a = 0 플레이어, 1.. 분신), 기쁨은 GateTake(계단·공방 게이트 통과)·GaugeLevelUp(계단 Lv 상승)으로 모두가 함께 돈다(웹 프로토 `paperSpin`).
    /// 위치(정본 syncScene): 런 = (P.x, WY, P.z), 오르기 = <see cref="CameraModel.ClimbPlayer"/>, 분신 y = <see cref="CatLayout.StairY"/>. 정렬: 순위 Unit, 탐침 +sort.catProbeY, 안개 없음.
    /// 프리팹·그림이 로드되기 전에는 그 캐릭터를 그리지 않는다.
    /// </summary>
    [DefaultExecutionOrder(56)]
    public sealed class CharacterPresenter : MonoBehaviour
    {
        public const string PlayerKey = "player", FriendKey = "friend";
        const int KeepPerFamily = 3;

        [SerializeField] Material _spriteMaterial;

        sealed class Unit
        {
            public CharacterRig Rig;
            public CharLookData Row;
            public readonly CharStateMachine Sm = new CharStateMachine();
            public string SpriteName;
            public CharState Shown;
        }

        readonly List<Unit> _units = new List<Unit>(32);
        readonly List<(double dx, double dz)> _offsets = new List<(double, double)>(32);
        readonly Dictionary<int, List<string>> _lru = new Dictionary<int, List<string>>();
        readonly HashSet<string> _loading = new HashSet<string>(StringComparer.Ordinal);
        readonly HashSet<string> _failed = new HashSet<string>(StringComparer.Ordinal);
        readonly List<double> _stepShownAt = new List<double>(64);

        IReadOnlyList<CharLookData> _looks;
        WorldSpriteCache _sprites;
        CharacterPool _pool;
        ViewConfig _cfg;
        CharAnimParams _anim;
        int _playerCat, _friendCat;
        double _xMax, _stepH, _stepD, _bump, _dropSec;
        ClimbState _climbSeen;
        bool _ready;

        public bool Ready => _ready;
        /// <summary>지금 보이는 캐릭터 수(시험·QA).</summary>
        public int VisibleCount { get; private set; }
        /// <summary>분신 자리 수(= `314_spawnGroup` clone 행 수). 시험·QA용.</summary>
        public int FriendSlots => Math.Max(0, _units.Count - 1);
        /// <summary>순번 0 = 플레이어, 1.. = 분신의 외형 행(마지막으로 고른 것). 시험·QA용.</summary>
        public CharLookData LookOf(int index) => index >= 0 && index < _units.Count ? _units[index].Row : null;
        /// <summary>마지막으로 고른 그림 이름(그림이 아직 없어도 채운다). 시험·QA용.</summary>
        public string SpriteNameOf(int index) => index >= 0 && index < _units.Count ? _units[index].SpriteName : null;
        /// <summary>순번의 마지막 프레임 상태(기쁨 겹침 포함, 시험·QA용).</summary>
        public CharState StateOf(int index) => index >= 0 && index < _units.Count ? _units[index].Shown : CharState.Hidden;

        public void Init(MvTables tables, ViewConfig cfg, WorldSpriteCache sprites, Material fallbackMaterial)
        {
            if (tables == null) throw new ArgumentNullException(nameof(tables));
            _cfg = cfg ?? throw new ArgumentNullException(nameof(cfg));
            _sprites = sprites;
            if (_spriteMaterial == null) _spriteMaterial = fallbackMaterial;
            _looks = tables.CharLook.Datas;
            _anim = CharAnimParams.From(tables);
            _playerCat = CatId(tables, PlayerKey);
            _friendCat = CatId(tables, FriendKey);
            CatLayout.CloneOffsets(tables, _offsets);
            _xMax = tables.RunConfig.D(RunConfigKeys.CLONE_X_MAX);
            var c = tables.Climb.Config();
            _stepH = c != null ? c["stepH"] : 0;
            _stepD = c != null ? c["stepD"] : 1;
            _dropSec = c != null ? c["dropSec"] : 0;
            _bump = ViewConfig.ClimbBumpOf(tables.Climb);
            _pool?.Dispose();
            _pool = new CharacterPool(transform, Setup);
            while (_units.Count < 1 + _offsets.Count) _units.Add(new Unit());
            _ready = true;
            Hide();
        }

        static int CatId(MvTables t, string key) => (t.Cat.ByKey(key) ?? throw new KeyNotFoundException(CatData.Table + ": " + key)).id;

        /// <summary>새 인스턴스: 정렬(순위 Unit·탐침·안개 없음)·빌보드(루트: 세로축 회전·흔들림을 루트 안에서 한다)·재질을 붙인다. 프리팹에는 그림만 둔다.</summary>
        void Setup(CharacterRig rig)
        {
            var go = rig.gameObject;
            var sg = go.GetComponent<SortingGroup>();
            if (sg == null) sg = go.AddComponent<SortingGroup>();
            sg.sortingLayerName = ObjectViewFactory.WorldLayer;
            var ds = go.GetComponent<DepthSortable>();
            if (ds == null) ds = go.AddComponent<DepthSortable>();
            ds.Rank = DepthMath.RankUnit;
            ds.Bias = 0f;
            ds.SortOffsetY = (float)_cfg.CatProbeY;
            ds.Fog = false;
            if (go.GetComponent<Billboard>() == null) go.AddComponent<Billboard>();
            var body = rig.Body;
            if (body != null && _spriteMaterial != null) body.sharedMaterial = _spriteMaterial;
            if (rig.Wheel != null && _spriteMaterial != null) rig.Wheel.sharedMaterial = _spriteMaterial;
            ds.Refresh();
        }

        public void Hide()
        {
            for (int i = 0; i < _units.Count; i++) Park(_units[i]);
            VisibleCount = 0;
        }

        void Park(Unit u)
        {
            u.Sm.Set(CharState.Hidden, 0);
            u.Shown = CharState.Hidden;
            if (u.Rig != null && u.Rig.gameObject.activeSelf) u.Rig.gameObject.SetActive(false);
        }

        /// <summary>런 이벤트(같은 프레임 목록): Fire의 a = 쏜 자리(0 플레이어, 1.. 분신). GateTake·GaugeLevelUp = 모두 기쁨.</summary>
        public void OnRunEvents(IReadOnlyList<RunEvent> events, double nowSec)
        {
            if (!_ready || events == null) return;
            for (int i = 0; i < events.Count; i++)
            {
                var e = events[i];
                switch (e.Type)
                {
                    case RunEventType.Fire:
                        int unit = e.A;
                        if (unit >= 0 && unit < _units.Count) _units[unit].Sm.Shot(nowSec);
                        break;
                    case RunEventType.GateTake:
                    case RunEventType.GaugeLevelUp:
                        for (int k = 0; k < _units.Count; k++) _units[k].Sm.Happy(nowSec);
                        break;
                }
            }
        }

        /// <summary>매 프레임(시뮬 뒤): 외형·상태·위치를 맞춘다. <paramref name="s"/>가 null이면 모두 숨긴다.</summary>
        public void Sync(RunState s, double nowSec)
        {
            if (!_ready || s == null) { Hide(); return; }
            var C = s.Phase == RunPhase.Climb ? s.Climb : null;
            TrackSteps(C);
            bool walking = C != null || s.Phase == RunPhase.Run && !s.Fin;
            var state = walking ? CharState.Walk : CharState.Stand;
            _units[0].Sm.Climbing = C != null && C.Pos > 0;
            _units[0].Sm.ClimbedSteps = C != null ? C.Pos : 0;
            int tier = s.EqTier();

            double px = s.P.X, pz = s.P.Z, py = 0, wy = s.WorldY;
            if (C != null) CameraModel.ClimbPlayer(C.Pos, C.K, C.Z0, _stepH, _stepD, _bump, out py, out pz);
            int visible = 0;
            if (Place(0, _playerCat, 0, tier, C != null ? CharState.Stair : state, px, wy + py, pz, nowSec)) visible++;

            int slots = _units.Count - 1;
            int n = Math.Min(s.Clones, slots);
            for (int i = 0; i < slots; i++)
            {
                var u = _units[1 + i];
                if (i >= n) { Park(u); continue; }
                CatLayout.ClonePos(px, pz, _offsets[i].dx, _offsets[i].dz, _xMax, out double cx, out double cz);
                double cy = 0;
                if (C != null)
                {
                    int si = CatLayout.StairIndex(cz, C.Z0, _stepD);
                    cy = CatLayout.StairY(si, C.K, _stepH, Landed(C, si));
                }
                if (Place(1 + i, _friendCat, i, tier, state, cx, wy + cy, cz, nowSec)) visible++;
            }
            VisibleCount = visible;
        }

        bool Place(int index, int catId, int slot, int tier, CharState state, double x, double y, double z, double nowSec)
        {
            var u = _units[index];
            var row = CharLookResolver.Resolve(_looks, catId, slot, tier);
            u.Row = row;
            if (row == null) { Park(u); return false; }
            Want(row, catId, slot);
            bool wheel = !string.IsNullOrEmpty(row.wheel);
            if (wheel) Load(row.wheelAtlas);
            var next = CharLookResolver.Resolve(_looks, catId, slot, tier + 1);
            if (next != null && next != row) Want(next, catId, slot);

            u.Sm.Set(state, nowSec);
            var frame = u.Sm.Evaluate(_anim, nowSec, index);
            u.Shown = frame.State;
            u.SpriteName = CharStateMachine.SpriteOf(row, frame.Pose);

            if (_sprites == null) return false;   // 그림 캐시가 없으면(시험) 프리팹도 읽지 않는다
            if (u.Rig != null && u.Rig.Address != row.prefab)
            {
                _pool.Release(u.Rig);
                u.Rig = null;
            }
            if (u.Rig == null) u.Rig = _pool.Get(row.prefab);
            var sprite = _sprites.IsLoaded(row.atlas) ? _sprites.Get(row.atlas, u.SpriteName) : null;
            if (u.Rig == null || sprite == null)
            {
                if (u.Rig != null && u.Rig.gameObject.activeSelf) u.Rig.gameObject.SetActive(false);
                return false;
            }
            var wheelSprite = wheel && frame.HasWheel && _sprites.IsLoaded(row.wheelAtlas) ? _sprites.Get(row.wheelAtlas, row.wheel) : null;
            u.Rig.Apply(sprite, frame, row.scale, wheelSprite);
            u.Rig.transform.position = new Vector3((float)x, (float)y, (float)z);
            if (!u.Rig.gameObject.activeSelf) u.Rig.gameObject.SetActive(true);
            return true;
        }

        /// <summary>오르기 칸이 보인 시각(오르기 시계 C.Time)을 적어 둔다 — 정본 `st.t`(보인 뒤 흐른 시간) 대신.</summary>
        void TrackSteps(ClimbState C)
        {
            if (C == null) { _climbSeen = null; return; }
            if (!ReferenceEquals(C, _climbSeen)) { _climbSeen = C; _stepShownAt.Clear(); }
            while (_stepShownAt.Count < C.StepsShown) _stepShownAt.Add(C.Time);
        }

        bool Landed(ClimbState C, int i)
        {
            if (i < 0 || i >= _stepShownAt.Count) return false;
            if (C.T > 0 && C.Time >= C.T) return true;   // 끝(p ≥ 1)에는 모든 칸 t = 1
            return C.Time - _stepShownAt[i] >= _dropSec;
        }

        /// <summary>캐릭터 묶음(catId·slot)의 아틀라스를 최근으로 올리고 없으면 읽는다. 묶음마다 최근 3개(지금·다음·바로 전)만 둔다.</summary>
        void Want(CharLookData row, int catId, int slot)
        {
            int family = catId * 31 + slot;
            if (!_lru.TryGetValue(family, out var lru)) _lru[family] = lru = new List<string>(KeepPerFamily + 1);
            string atlas = row.atlas;
            int at = lru.IndexOf(atlas);
            if (at != 0)
            {
                if (at > 0) lru.RemoveAt(at);
                lru.Insert(0, atlas);
            }
            if (_sprites == null) return;
            Load(atlas);
            while (lru.Count > KeepPerFamily)
            {
                string old = lru[lru.Count - 1];
                lru.RemoveAt(lru.Count - 1);
                if (!InUse(old) && !_loading.Contains(old)) _sprites.ReleaseAtlas(old);
            }
        }

        /// <summary>아틀라스가 없으면 읽기 시작한다(실패한 것은 다시 읽지 않는다). 바퀴 아틀라스는 등급과 상관없이 하나라 묶음 최근 목록에 넣지 않고 둔다.</summary>
        void Load(string atlas)
        {
            if (_sprites == null || string.IsNullOrEmpty(atlas)) return;
            if (!_sprites.IsLoaded(atlas) && !_failed.Contains(atlas) && _loading.Add(atlas)) LoadAsync(atlas, this.GetCancellationTokenOnDestroy()).Forget();
        }

        /// <summary>다른 묶음이 같은 아틀라스를 들고 있으면 반환하지 않는다.</summary>
        bool InUse(string atlas)
        {
            foreach (var l in _lru.Values) if (l.Contains(atlas)) return true;
            return false;
        }

        async UniTaskVoid LoadAsync(string atlas, CancellationToken ct)
        {
            try
            {
                if (!await _sprites.LoadAtlasAsync(atlas, ct)) _failed.Add(atlas);   // 실패는 다시 읽지 않는다(오류 1번)
            }
            catch (OperationCanceledException)
            {
            }
            finally
            {
                _loading.Remove(atlas);
            }
        }

        void OnDestroy()
        {
            _pool?.Dispose();
            _pool = null;
        }
    }
}
