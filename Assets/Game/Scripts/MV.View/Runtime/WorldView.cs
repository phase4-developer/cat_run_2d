using System.Threading;
using Cysharp.Threading.Tasks;
using MV.Data;
using UnityEngine;

namespace MV.View
{
    /// <summary>
    /// 월드 렌더 입구(`WorldRoot`, 02 §2.10). 카메라 리그·깊이 정렬·지면·소품·틴트를 한 번에 묶는다.
    /// 씬 연결(Boot·`Game` 씬 배치)은 G4 통합에서 총괄이 한다. 통합 코드는 <see cref="Init"/> → <see cref="SetThemeAsync"/> →
    /// <see cref="RebuildProps"/> 순서로 부른다.
    /// </summary>
    public sealed class WorldView : MonoBehaviour
    {
        public const string FxAtlas = "Atlas_Fx_Common";

        [SerializeField] CameraRig _rig;
        [SerializeField] DepthSorter _sorter;
        [SerializeField] GroundStripMesh _ground;
        [SerializeField] PropStreamer _props;
        [SerializeField] TintLayer _tint;
        [SerializeField] ObjectViewFactory _objects;
        [SerializeField] ScreenRig _screen;
        [SerializeField] Material _screenMultiply;

        readonly WorldSpriteCache _sprites = new WorldSpriteCache();
        ViewConfig _cfg;
        string _themeAtlas;

        public ViewConfig Config => _cfg;
        public CameraRig Rig => _rig;
        public WorldSpriteCache Sprites => _sprites;
        public ObjectViewFactory Objects => _objects;
        public PropStreamer Props => _props;
        public ScreenRig Screen => _screen;

        public void Init(MvTables tables, IWorldViewSource source)
        {
            _cfg = ViewConfig.From(tables.RunConfig);
            _rig.Init(tables, source);
            _sorter.Init(_cfg, _rig.Cam);
            _ground.Init(_cfg, tables, _rig, source);
            _props.Init(_cfg, _rig, _sprites, FxAtlas);
            if (_tint != null) _tint.Init(_rig.Cam, _screenMultiply);
            if (_objects != null) _objects.Init(_cfg, _rig.Cam.transform, _sprites, FxAtlas);
            if (_screen != null) _screen.Init(_cfg, _rig.Cam, _sprites, FxAtlas);
        }

        /// <summary>테마 아틀라스·Fx 아틀라스를 먼저 로드한 뒤 색·소품을 바꾼다(03 §3.8.5·§3.8.8). 이전 테마 아틀라스는 그 뒤 반환.</summary>
        public async UniTask<bool> SetThemeAsync(ThemeData theme, CancellationToken ct = default)
        {
            if (theme == null) return false;
            bool ok = await _sprites.LoadAtlasAsync(theme.worldAtlas, ct);
            ok &= await _sprites.LoadAtlasAsync(FxAtlas, ct);
            string prev = _themeAtlas;
            _themeAtlas = theme.worldAtlas;
            _rig.SetTheme(theme);
            _ground.SetTheme(theme);
            _props.SetTheme(theme);
            if (_tint != null) _tint.SetTheme(theme);
            if (_objects != null) _objects.SetTheme(theme);
            if (_screen != null) _screen.SetTheme(theme);
            if (prev != null && prev != _themeAtlas) _sprites.ReleaseAtlas(prev);
            return ok;
        }

        /// <summary>정본 `buildEnv(len, from, cliff)`에 해당. 런 시작·계단 뒤 새 지형마다 부른다.</summary>
        public void RebuildProps(double from, double len, bool cliff, double groundY) => _props.Rebuild(from, len, cliff, groundY);

        /// <summary>
        /// 런 오브젝트 뷰 맞추기(매 프레임, 통합 코드가 시뮬 뒤에 부른다). PZ = 카메라 모델 z, 플레이어 z는 관문 흐려짐에 쓴다.
        /// 교체 지점: 지면 높이는 MV-RUN WY 읽기 인터페이스가 들어오면 그 값을 쓴다.
        /// </summary>
        public void SyncObjects(System.Collections.Generic.IReadOnlyList<MV.Run.RunObject> objs, double playerZ, double groundY)
        {
            if (_objects == null || _rig == null || !_rig.Ready) return;
            _objects.Sync(objs, _rig.Model.Pos.z, playerZ, groundY);
        }

        /// <summary>정본 `shake(a, t)`.</summary>
        public void Shake(double amp, double time) => _rig.Shake(amp, time);

        void OnDestroy() => _sprites.Dispose();
    }
}
