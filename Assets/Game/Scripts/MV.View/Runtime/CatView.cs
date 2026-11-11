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
    /// 런 고양이 뷰(02 §2.1.4 고양이 행, 정본 `drawCatW` L3364·L3798~3799): 플레이어 1 + 친구(`RunState.Clones`)를
    /// 발 아래 앵커 빌보드로 그린다. 친구 자리 수 = `314_spawnGroup` clone 행 수(표 검증: ≥ MAX_CLONES), 무늬·단계 그림 수 = `101_cat`.
    /// 그림이 모자라면 마지막 그림을 다시 쓴다(친구 5번째부터 마지막 무늬, 2026-11-11 사용자 결정).
    /// 런 화면은 항상 뒷모습, 크기 배율(1.05·0.85)은 굽는 크기에 들어 있어 Transform 배율 1(03 §3.3.2).
    /// 정렬: 순위 Unit, 탐침 +sort.catProbeY, 편향 0, 안개 알파 없음(02 §2.3.1 5·6행).
    /// 아틀라스: 플레이어·친구 `101_cat.atlasPattern`을 필요할 때 읽고, 다음 단계를 미리 읽으며 묶음별 최근 3개만 둔다(03 §3.8.8).
    /// 로드 전에는 그 고양이를 그리지 않는다.
    /// </summary>
    [DefaultExecutionOrder(56)]
    public sealed class CatView : MonoBehaviour
    {
        const int KeepPerFamily = 3;

        [SerializeField] Material _spriteMaterial;

        sealed class Cat
        {
            public GameObject Go;
            public DepthSortable Ds;
            public SpriteRenderer Sr;
            /// <summary>마지막으로 고른 그림(무늬·단계·묶음·프레임). 이름은 시험이 물을 때만 만든다(런 중 할당 없음).</summary>
            public int Pat = -1, Tier, Frame;
            public CatAnim Anim;
        }

        readonly List<Cat> _cats = new List<Cat>(32);
        readonly List<(double dx, double dz)> _offsets = new List<(double, double)>(32);
        readonly Dictionary<int, Sprite[]> _frames = new Dictionary<int, Sprite[]>(16);
        readonly HashSet<string> _loading = new HashSet<string>(StringComparer.Ordinal);
        readonly HashSet<string> _failed = new HashSet<string>(StringComparer.Ordinal);
        readonly List<int> _lruPlayer = new List<int>(4), _lruFriend = new List<int>(4);
        readonly List<double> _stepShownAt = new List<double>(64);

        WorldSpriteCache _sprites;
        ViewConfig _cfg;
        CatArt _art;
        double _xMax, _stepH, _stepD, _bump, _dropSec;
        ClimbState _climbSeen;
        bool _ready;

        public bool Ready => _ready;
        /// <summary>지금 보이는 고양이 수(시험·QA).</summary>
        public int VisibleCount { get; private set; }

        /// <summary>친구 자리 수(= `314_spawnGroup` clone 행 수). 시험·QA용.</summary>
        public int FriendSlots => Math.Max(0, _cats.Count - 1);

        /// <summary>순번 0 = 플레이어, 1.. = 친구. 시험·QA용.</summary>
        public GameObject CatObject(int index) => index >= 0 && index < _cats.Count ? _cats[index]?.Go : null;

        /// <summary>마지막으로 고른 프레임 이름(그림이 아직 없어도 채운다). 시험·QA용.</summary>
        public string SpriteNameOf(int index)
        {
            var c = index >= 0 && index < _cats.Count ? _cats[index] : null;
            if (c == null || c.Pat < 0 || _art == null) return null;
            string name = _art.FrameName(c.Pat, c.Tier, c.Anim == CatAnim.Throw && c.Pat != 0 ? CatAnim.Run : c.Anim);
            return c.Anim == CatAnim.Idle ? name : name + c.Frame.ToString("00");
        }

        public void Init(MvTables tables, ViewConfig cfg, WorldSpriteCache sprites, Material fallbackMaterial)
        {
            _cfg = cfg ?? throw new ArgumentNullException(nameof(cfg));
            _sprites = sprites;
            if (_spriteMaterial == null) _spriteMaterial = fallbackMaterial;
            _art = CatArt.From(tables);
            CatLayout.CloneOffsets(tables, _offsets);
            _xMax = tables.RunConfig.D(RunConfigKeys.CLONE_X_MAX);
            var c = tables.Climb.Config();
            _stepH = c != null ? c["stepH"] : 0;
            _stepD = c != null ? c["stepD"] : 1;
            _dropSec = c != null ? c["dropSec"] : 0;
            _bump = ViewConfig.ClimbBumpOf(tables.Climb);
            while (_cats.Count < 1 + _offsets.Count) _cats.Add(Make(_cats.Count));
            _ready = true;
            Hide();
        }

        Cat Make(int index)
        {
            var go = new GameObject(index == 0 ? "Player" : "Friend" + index);
            go.transform.SetParent(transform, false);
            var sg = go.AddComponent<SortingGroup>();
            sg.sortingLayerName = ObjectViewFactory.WorldLayer;
            var ds = go.AddComponent<DepthSortable>();
            ds.Rank = DepthMath.RankUnit;
            ds.Bias = 0f;
            ds.SortOffsetY = (float)_cfg.CatProbeY;
            ds.Fog = false;
            var body = new GameObject("Body");
            body.transform.SetParent(go.transform, false);
            body.AddComponent<Billboard>();
            var sr = body.AddComponent<SpriteRenderer>();
            if (_spriteMaterial != null) sr.sharedMaterial = _spriteMaterial;
            ds.Refresh();
            go.SetActive(false);
            return new Cat { Go = go, Ds = ds, Sr = sr };
        }

        public void Hide()
        {
            for (int i = 0; i < _cats.Count; i++) if (_cats[i] != null && _cats[i].Go.activeSelf) _cats[i].Go.SetActive(false);
            VisibleCount = 0;
        }

        /// <summary>
        /// 매 프레임(시뮬 뒤): 위치·프레임을 맞춘다. <paramref name="s"/>가 null이면 모두 숨긴다.
        /// 위치(정본 syncScene): 런 = (P.x, WY, P.z), 오르기 = <see cref="CameraModel.ClimbPlayer"/>의 (py, pz), 친구 y = <see cref="CatLayout.StairY"/>.
        /// </summary>
        public void Sync(RunState s, double nowSec)
        {
            if (!_ready || s == null) { Hide(); return; }
            var C = s.Phase == RunPhase.Climb ? s.Climb : null;
            TrackSteps(C);
            bool running = s.Phase == RunPhase.Run && !s.Fin;
            int top = s.EqTier();
            int pt = _art.PlayerTier(top), ft = _art.FriendTier(top);
            // 손패 최고가 오르면 쓸 다음 t를 미리 읽는다(03 §3.8.8). 앞에 둔 것이 최근.
            int pn = _art.PlayerTier(top + 1), fn = _art.FriendTier(top + 1);
            if (pn != pt) Want(true, pn);
            Want(true, pt);
            if (fn != ft) Want(false, fn);
            Want(false, ft);

            double px = s.P.X, pz = s.P.Z, py = 0, wy = s.WorldY, ph = s.P.Phase;
            if (C != null) CameraModel.ClimbPlayer(C.Pos, C.K, C.Z0, _stepH, _stepD, _bump, out py, out pz);
            int visible = 0;
            var anim = CatLayout.AnimOf(C != null, running, s.P.ThrowT, true);
            if (Place(_cats[0], 0, pt, anim, ph, px, wy + py, pz, nowSec)) visible++;

            int slots = _cats.Count - 1;
            int n = Math.Min(s.Clones, slots);
            var fa = CatLayout.AnimOf(C != null, running, 0, false);
            for (int i = 0; i < slots; i++)
            {
                var cat = _cats[1 + i];
                if (i >= n) { if (cat.Go.activeSelf) cat.Go.SetActive(false); continue; }
                CatLayout.ClonePos(px, pz, _offsets[i].dx, _offsets[i].dz, _xMax, out double cx, out double cz);
                double cy = 0;
                if (C != null)
                {
                    int si = CatLayout.StairIndex(cz, C.Z0, _stepD);
                    cy = CatLayout.StairY(si, C.K, _stepH, Landed(C, si));
                }
                if (Place(cat, _art.FriendPattern(i), ft, fa, ph + i * CatLayout.FriendPhaseStep, cx, wy + cy, cz, nowSec)) visible++;
            }
            VisibleCount = visible;
        }

        bool Place(Cat cat, int pat, int tier, CatAnim anim, double ph, double x, double y, double z, double nowSec)
        {
            var frames = FramesOf(pat, tier, anim);
            int f = anim == CatAnim.Idle ? 0 : CatLayout.FrameIndex(ph);
            var sprite = frames != null && f < frames.Length ? frames[f] : null;
            cat.Pat = pat;
            cat.Tier = tier;
            cat.Anim = anim;
            cat.Frame = f;
            if (sprite == null)
            {
                if (cat.Go.activeSelf) cat.Go.SetActive(false);
                return false;
            }
            if (cat.Sr.sprite != sprite) cat.Sr.sprite = sprite;
            cat.Go.transform.position = new Vector3((float)x, (float)y, (float)z);
            var body = cat.Sr.transform;
            if (anim == CatAnim.Idle)
            {
                CatLayout.IdleBreath(nowSec, pat, out double sx, out double sy);
                body.localScale = new Vector3((float)sx, (float)sy, 1f);
            }
            else if (body.localScale != Vector3.one) body.localScale = Vector3.one;
            if (!cat.Go.activeSelf) cat.Go.SetActive(true);
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

        static int FrameKey(int pat, int tier, CatAnim anim) => (pat * 1000 + tier) * 10 + (int)anim;

        Sprite[] FramesOf(int pat, int tier, CatAnim anim)
        {
            if (pat != 0 && anim == CatAnim.Throw) anim = CatAnim.Run;
            int key = FrameKey(pat, tier, anim);
            if (_frames.TryGetValue(key, out var arr)) return arr;
            string atlas = _art.AtlasOf(pat, tier);
            if (_sprites == null || !_sprites.IsLoaded(atlas)) return null;
            string name = _art.FrameName(pat, tier, anim);
            arr = anim == CatAnim.Idle ? new[] { _sprites.Get(atlas, name) } : _sprites.GetFrames(atlas, name, CatLayout.Frames);
            _frames[key] = arr;
            return arr;
        }

        /// <summary>묶음(플레이어·친구)의 단계 t 아틀라스를 최근으로 올리고 없으면 읽는다.</summary>
        void Want(bool player, int tier)
        {
            var lru = player ? _lruPlayer : _lruFriend;
            int at = lru.IndexOf(tier);
            if (at != 0)
            {
                if (at > 0) lru.RemoveAt(at);
                lru.Insert(0, tier);
            }
            if (_sprites == null) return;
            string atlas = _art.AtlasOf(player ? 0 : 1, tier);
            if (!_sprites.IsLoaded(atlas) && !_failed.Contains(atlas) && _loading.Add(atlas)) LoadAsync(atlas, this.GetCancellationTokenOnDestroy()).Forget();
            while (lru.Count > KeepPerFamily)   // 지금 것 + 다음 것 + 바로 전 것
            {
                int old = lru[lru.Count - 1];
                lru.RemoveAt(lru.Count - 1);
                Release(player, old);
            }
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

        void Release(bool player, int tier)
        {
            string atlas = _art.AtlasOf(player ? 0 : 1, tier);
            if (_sprites == null || _loading.Contains(atlas)) return;
            for (int pat = player ? 0 : 1; pat <= (player ? 0 : _art.FriendPatterns); pat++)
                for (int a = 0; a <= (int)CatAnim.Idle; a++)
                    _frames.Remove(FrameKey(pat, tier, (CatAnim)a));
            _sprites.ReleaseAtlas(atlas);
        }
    }
}
