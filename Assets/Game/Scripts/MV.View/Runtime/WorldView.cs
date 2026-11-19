using System.Threading;
using Cysharp.Threading.Tasks;
using MV.Data;
using MV.Run;
using UnityEngine;

namespace MV.View
{
    /// <summary>
    /// 월드 렌더 입구(`WorldRoot`, 02 §2.10). 카메라 리그·깊이 정렬·지면·틴트를 한 번에 묶는다.
    /// 씬 연결(Boot·`Game` 씬 배치)은 G4 통합에서 총괄이 한다. 통합 코드는 <see cref="Init"/> → <see cref="SetThemeAsync"/> 순서로 부른다.
    /// 길가 소품은 스테이지 맵 프리팹(<see cref="StageMap"/>)이 맡는다.
    /// </summary>
    public sealed class WorldView : MonoBehaviour
    {
        public const string FxAtlas = "Atlas_Fx_Common";

        [SerializeField] CameraRig _rig;
        [SerializeField] DepthSorter _sorter;
        [SerializeField] GroundStripMesh _ground;
        [SerializeField] TintLayer _tint;
        [SerializeField] ObjectViewFactory _objects;
        [SerializeField] ScreenRig _screen;
        [SerializeField] Material _screenMultiply;
        /// <summary>런 캐릭터(플레이어·분신). 필드 이름은 예전 CatView 자리(프리팹 참조 유지).</summary>
        [SerializeField] CharacterPresenter _cats;
        [SerializeField] ProjectileView _projs;
        /// <summary>계단 정산 칸(Obj_ClimbStep 쌓기). WorldRoot 프리팹 자식 ClimbStairs.</summary>
        [SerializeField] ClimbStairView _stairs;
        /// <summary>투사체 판정(프리팹 Collider, Unity Physics). WorldRoot 프리팹 자식 HitWorld.</summary>
        [SerializeField] RunHitWorld _hits;

        readonly WorldSpriteCache _sprites = new WorldSpriteCache();
        ViewConfig _cfg;
        string _themeAtlas;

        public ViewConfig Config => _cfg;
        public CameraRig Rig => _rig;
        public WorldSpriteCache Sprites => _sprites;
        public ObjectViewFactory Objects => _objects;
        public ScreenRig Screen => _screen;
        public CharacterPresenter Cats => _cats;
        public ProjectileView Projectiles => _projs;
        public ClimbStairView Stairs => _stairs;
        /// <summary>런 시뮬의 투사체 판정 질의(<see cref="MV.Run.RunSim.HitQuery"/>). 오브젝트 뷰가 없으면 null.</summary>
        public RunHitWorld Hits => _hits;

        public void Init(MvTables tables, IWorldViewSource source)
        {
            _cfg = ViewConfig.From(tables.RunConfig);
            _sprites.ServeAtlasRequests();   // 프리팹이 직접 참조한 지연 바인딩 아틀라스 그림(글자판 게이지 등)
            _rig.Init(tables, source);
            _sorter.Init(_cfg, _rig.Cam);
            _sorter.SetFocus(_rig.TryGetFocus);
            _ground.Init(_cfg, tables, _rig, source);
            if (_tint != null) _tint.Init(_rig.Cam, _screenMultiply);
            if (_objects != null) _objects.Init(_cfg, _rig.Cam.transform, _sprites, FxAtlas);
            if (_screen != null) _screen.Init(_cfg, _rig.Cam, _sprites, FxAtlas);
            // 하위 관리자는 모두 WorldRoot 프리팹의 자식이다. 없으면 만들지 않고 프리팹 경로와 자식 이름을 경고로 알린다.
            var spriteMat = _objects != null ? _objects.SpriteMaterial : null;
            if (Has(_cats, "Cats", nameof(CharacterPresenter))) _cats.Init(tables, _cfg, _sprites, spriteMat);
            if (Has(_projs, "Projectiles", nameof(ProjectileView))) _projs.Init(tables, _cfg, _sprites, spriteMat, FxAtlas);
            if (Has(_stairs, "ClimbStairs", nameof(ClimbStairView))) _stairs.Init(tables, _cfg, _rig.Cam);
            if (_objects != null && Has(_hits, "HitWorld", nameof(RunHitWorld)))
                _objects.ConnectHits(_hits);   // 뷰 연결·회수 때 프리팹 Hitbox 등록·해제, 틱마다 대상 위치 맞추기
        }

        public const string PrefabPath = "Assets/Game/Prefabs/WorldRoot.prefab";

        bool Has(Object part, string child, string type)
        {
            if (part != null) return true;
            Debug.LogWarning($"[WorldView] {PrefabPath}에 자식 {child}({type})가 없거나 WorldView에 연결되지 않았습니다. 만들지 않습니다.", this);
            return false;
        }

        /// <summary>테마 아틀라스·Fx 아틀라스를 먼저 로드한 뒤 색을 바꾼다(03 §3.8.5·§3.8.8). 이전 테마 아틀라스는 그 뒤 반환.</summary>
        public async UniTask<bool> SetThemeAsync(ThemeData theme, CancellationToken ct = default)
        {
            if (theme == null) return false;
            bool ok = await _sprites.LoadAtlasAsync(theme.worldAtlas, ct);
            ok &= await _sprites.LoadAtlasAsync(FxAtlas, ct);
            ok &= await _sprites.LoadAtlasAsync(MvAtlas.UiIcon, ct);   // 무기문 위 카드 묶음(UI 아이콘 그대로, 테마와 무관해 반환하지 않는다)
            ok &= await _sprites.LoadAtlasAsync(MvAtlas.UiCard, ct);   // 무기문 위 카드 묶음 등급 틀(09_uiResource 등급 틀, 반환하지 않는다)
            string prev = _themeAtlas;
            _themeAtlas = theme.worldAtlas;
            _rig.SetTheme(theme);
            _ground.SetTheme(theme);
            if (_tint != null) _tint.SetTheme(theme);
            if (_objects != null) _objects.SetTheme(theme);
            if (_screen != null) _screen.SetTheme(theme);
            if (prev != null && prev != _themeAtlas) _sprites.ReleaseAtlas(prev);
            return ok;
        }

        /// <summary>
        /// 런 오브젝트 뷰 맞추기(매 프레임, 통합 코드가 시뮬 뒤에 부른다). PZ = 카메라 모델 z, 플레이어 z는 관문 흐려짐에 쓴다.
        /// 교체 지점: 지면 높이는 MV-RUN WY 읽기 인터페이스가 들어오면 그 값을 쓴다.
        /// </summary>
        public void SyncObjects(System.Collections.Generic.IReadOnlyList<MV.Run.RunObject> objs, double playerZ, double groundY)
        {
            if (_objects == null || _rig == null || !_rig.Ready) return;
            _objects.Sync(objs, _rig.Model.Pos.z, playerZ, groundY);
        }

        /// <summary>
        /// 런 고양이·투사체 맞추기(매 프레임, 통합 코드가 시뮬 뒤에 부른다). 런이 없으면(null) 숨긴다. 정본 `draw2D` L3797~3804.
        /// 투사체·창·회전 장난감(<see cref="ProjectileView"/>)과 계단 칸(<see cref="ClimbStairView"/>)도 같은 프레임에 맞춘다(통합 코드 호출을 늘리지 않으려고 여기서 함께).
        /// </summary>
        public void SyncCats(RunState state)
        {
            bool off = state == null || _rig == null || !_rig.Ready;
            double now = Time.realtimeSinceStartupAsDouble;
            if (_objects != null && !off) _objects.GoldMul = RunFormulas.GoldMul(state);   // 결계 블록 글자판 「+골드」(정본 L1492)
            if (_objects != null && !off) _objects.GaugeLv = state.GaugeLv;   // 오브 탑 단계 연출
            if (_objects != null && !off)
            {
                _objects.GateWeaponIcon = WeaponIcon(state);
                _objects.GateWeaponFrameAtlas = _frameAtlas;
                _objects.GateWeaponFrame = _frame;
                _objects.GateWeaponColor = _gradeCol;   // 게이트 보석·파티클 등급색(GradeColorTint)
            }
            if (_cats != null) { if (off) _cats.Hide(); else _cats.Sync(state, now, _rig.Model.OrbitE); }
            if (_projs != null) { if (off) _projs.Hide(); else _projs.Sync(state, now); }
            if (_stairs != null) { if (off) _stairs.Hide(); else _stairs.Sync(state); }
        }

        /// <summary>지금 런 무기 단계(Bt)의 103_item.icon(등급 틀 <see cref="_frame"/>도 함께 고른다). 같은 단계면 다시 찾지 않는다.</summary>
        string WeaponIcon(RunState state)
        {
            if (state.Tables == null) return null;
            if (state.Bt == _iconTier && state.Tables == _iconTables) return _icon;
            _iconTier = state.Bt;
            _iconTables = state.Tables;
            _icon = null;
            _gradeCol = null;
            foreach (var it in state.Tables.Item.Datas) if (it.tier == state.Bt) { _icon = it.icon; _gradeCol = it.gradeCol; break; }
            var fr = MvUiGrade.Frame(state.Tables, state.Bt);   // HUD 무기 칸과 같은 규칙(등급 번호 = 103_item.tier)
            _frameAtlas = fr?.atlas;
            _frame = fr?.sprite;
            return _icon;
        }

        int _iconTier = -1;
        MvTables _iconTables;
        string _icon, _frameAtlas, _frame, _gradeCol;

        /// <summary>런 이벤트 → 월드 뷰 반응(오브젝트 맞음·우물 벽돌 빠짐, 06 §6.13). <see cref="SyncObjects"/> 전에 같은 프레임 목록으로 부른다.</summary>
        public void OnRunEvents(System.Collections.Generic.IReadOnlyList<RunEvent> events)
        {
            if (_objects != null) _objects.OnRunEvents(events);
            if (_cats != null) _cats.OnRunEvents(events, Time.realtimeSinceStartupAsDouble);
        }

        /// <summary>정본 `shake(a, t)`.</summary>
        public void Shake(double amp, double time) => _rig.Shake(amp, time);

        void OnDestroy() => _sprites.Dispose();
    }
}
