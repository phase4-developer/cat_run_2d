using System;
using jjevol.Effect;
using MV.View;
using UnityEngine;

namespace MV.Fx
{
    /// <summary>
    /// 이펙트 프리팹 루트 = Framework `IEffectRenderer`(06 §6.2.3 혼합 방식, D-45 권장안 (가)).
    /// 생성 때 정본 공식으로 부품(입자·모양)을 만들고 매 프레임 simDt로 C#이 적분해 빌보드 메쉬 1개(서브메쉬 일반/가산)로 그린다.
    /// 프리팹 직렬화 값 = 04_effect 그룹 001 기본값과 06 §6.3 표의 고정 색·수명(시험이 읽어 대조한다).
    /// 정렬은 같은 오브젝트의 <see cref="DepthSortable"/>(rank 3, bias = sortBias, 안개 끔)가 한다.
    /// </summary>
    public sealed class MvFxDriver : MonoBehaviour, IEffectRenderer
    {
        public const string SortingLayer = "World";
        const int MaxParts = 96;
        /// <summary>살아 있는 점 입자 전체(06 §6.14 sparks 상한 판정).</summary>
        public static int LiveParticles { get; private set; }
        /// <summary>sparks 상한(`300_runConfig` fx.sparkCapThreshold·fx.sparkCapCount, <see cref="MvFxPlayer"/>가 표에서 채운다). 표를 읽기 전에는 상한 없음.</summary>
        public static int SparkCapThreshold = int.MaxValue, SparkCapCount = int.MaxValue;
        /// <summary>빌보드 축 카메라. 없으면 Camera.main.</summary>
        public static Camera ViewCamera;

        [Header("종류·기본값(04_effect 그룹 001, 06 §6.3)")]
        public FxKind kind;
        public float count = 8, speed = 4, size = 0.35f, life = 0.6f, grav = 6, radius = 1.5f;
        [Tooltip("고정 색 #rrggbb(호출 색이 있으면 그것)")]
        public string color = "#ffffff";
        public float sortBias = -0.2f;
        [SerializeField] Material _normal;
        [SerializeField] Material _add;

        enum Law : byte { Spark, Debris, Dust, Cloud, Trail, Grow, GrowSqrt, Ring, Impact, Bolt, Fall, Static, Brick, Paw, Hiss, Beam, MeteorBody, Frost }

        struct Part
        {
            public Law law;
            public Vector3 p, v, a, b;
            public float g, t, life, s0, s1, a0, w, h;
            public Color32 c;
            public FxShape shape;
            public bool add, particle;
        }

        readonly Part[] _parts = new Part[MaxParts];
        int _n;
        readonly MvFxMesh _builder = new MvFxMesh();
        Mesh _mesh;
        MeshRenderer _mr;
        DepthSortable _sort;
        Action _onComplete;
        bool _playing, _paused;
        int _token;
        float _meteorTail;
        FxSpawn _spawn;

        public Transform Root => transform;

        void Awake() => Ensure();

        void Ensure()
        {
            if (_mesh != null) return;
            if (!TryGetComponent(out MeshFilter mf)) mf = gameObject.AddComponent<MeshFilter>();
            if (!TryGetComponent(out _mr)) _mr = gameObject.AddComponent<MeshRenderer>();
            _mesh = new Mesh { name = "Fx_" + kind };
            _mesh.MarkDynamic();
            mf.sharedMesh = _mesh;
            _mr.sharedMaterials = new[] { _normal, _add };
            _mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _mr.receiveShadows = false;
            _mr.sortingLayerName = SortingLayer;
            if (!TryGetComponent(out _sort)) _sort = gameObject.AddComponent<DepthSortable>();
            _sort.Rank = DepthMath.RankFx;
            _sort.Bias = sortBias;
            _sort.Fog = false;
            _sort.Refresh();
            if (_sort.Group != null) _sort.Group.sortingLayerName = SortingLayer;
        }

        // ---------------- IEffectRenderer ----------------

        public void SetSorting(string layer, int order)
        {
            Ensure();
            if (_sort.Group == null) return;
            _sort.Group.sortingLayerName = string.IsNullOrEmpty(layer) ? SortingLayer : layer;
            _sort.Group.sortingOrder = order;
        }

        public void SetPaused(bool paused) => _paused = paused;
        public void SetUnscaledTime(bool unscaled) { }
        /// <summary>skinIndex = <see cref="MvFxParamBank"/> 토큰.</summary>
        public void SetSkin(int skinIndex) => _token = skinIndex;

        public void Play(int animationIndex, bool loop, Action onComplete)
        {
            Ensure();
            ClearParts();
            _onComplete = onComplete;
            if (!MvFxParamBank.TryGet(_token, out _spawn)) _spawn = FxSpawn.Of(null, transform.position);
            _token = 0;
            _spawn.Pos = transform.position;
            if (_spawn.To == default) _spawn.To = _spawn.Pos;
            Spawn(_spawn);
            _playing = true;
            Rebuild();
        }

        public void StopAndClear()
        {
            _playing = false;
            _onComplete = null;
            ClearParts();
            if (_mesh != null) _mesh.Clear();
        }

        void OnDisable() => ClearParts();

        void OnDestroy()
        {
            ClearParts();
            if (_mesh != null) Destroy(_mesh);
        }

        void ClearParts()
        {
            for (int i = 0; i < _n; i++) if (_parts[i].particle) LiveParticles--;
            _n = 0;
        }

        /// <summary>시험·하네스용: 부품 수와 수명.</summary>
        public int PartCount => _n;
        public float PartLife(int i) => _parts[i].life;
        public Color32 PartColor(int i) => _parts[i].c;

        void Update()
        {
            if (!_playing || _paused) return;
            Step(MvFxClock.SimDt);
        }

        /// <summary>simDt만큼 진행(시험이 직접 부를 수 있다).</summary>
        public void Step(float dt)
        {
            if (!_playing) return;
            for (int i = 0; i < _n; i++)
            {
                ref var q = ref _parts[i];
                q.t += dt;
                Integrate(ref q, dt);
            }
            if (kind == FxKind.Meteor) MeteorTail(dt);
            for (int i = _n - 1; i >= 0; i--)
                if (_parts[i].t >= _parts[i].life)
                {
                    if (_parts[i].particle) LiveParticles--;
                    _parts[i] = _parts[--_n];
                }
            Rebuild();
            if (_n == 0)
            {
                _playing = false;
                var cb = _onComplete;
                _onComplete = null;
                cb?.Invoke();
            }
        }

        // ---------------- 생성(06 §6.3) ----------------

        static float R(float a, float b) => a + UnityEngine.Random.value * (b - a);
        static float Or(float v, float d) => float.IsNaN(v) ? d : v;
        Color32 Col(in FxSpawn s, string fallback) => s.HasColor ? s.Color : MvFxRouter.C(string.IsNullOrEmpty(fallback) ? color : fallback);

        Part _scratch;

        /// <summary>부품 1개. 가득 차면(96) 버리는 칸을 돌려준다.</summary>
        ref Part Add(Law law, Vector3 p, float lifeSec, Color32 c, FxShape shape, bool add, bool particle = false)
        {
            if (_n >= MaxParts)
            {
                _scratch = default;
                return ref _scratch;
            }
            if (particle) LiveParticles++;
            _parts[_n] = new Part { law = law, p = p, life = Mathf.Max(0.001f, lifeSec), c = c, shape = shape, add = add, a0 = 1, particle = particle };
            return ref _parts[_n++];
        }

        void Spawn(in FxSpawn s)
        {
            var p = s.Pos;
            switch (kind)
            {
                case FxKind.Sparks:
                    Sparks(p, Col(s, null), (int)Or(s.Count, count), Or(s.Speed, speed), Or(s.Size, size), Or(s.Life, life), Or(s.Grav, grav));
                    break;
                case FxKind.Debris: // 표 2
                {
                    int n = (int)Or(s.Count, count);
                    float sz = Or(s.Size, size);
                    var c = Col(s, null);
                    for (int i = 0; i < n; i++)
                    {
                        ref var q = ref Add(Law.Debris, p + new Vector3(R(-0.6f, 0.6f), R(0, 0.6f), 0), R(0.6f, 0.9f), c, FxShape.Rect, false, true);
                        q.v = new Vector3(R(-3, 3), R(3, 7), R(-1, 3));
                        q.w = sz; q.h = sz * 0.6f;
                    }
                    break;
                }
                case FxKind.Dust: // 표 7, 변형 1 = 신 폭풍 불구름(06 §6.4.3)
                {
                    if (s.Variant == 1)
                    {
                        ref var q = ref Add(Law.Cloud, p, Or(s.Life, 1.7f), Col(s, "#4a2418"), FxShape.Glow, false);
                        q.s0 = Or(s.Size, 3f);
                        break;
                    }
                    int n = (int)Or(s.Count, count);
                    var c = Col(s, null);
                    for (int i = 0; i < n; i++)
                    {
                        ref var q = ref Add(Law.Dust, p + new Vector3(R(-0.8f, 0.8f), R(-0.3f, 0.4f), R(-0.3f, 0.3f)), R(0.45f, 0.7f), c, FxShape.Glow, false, true);
                        q.v = new Vector3(R(-1.4f, 1.4f), R(0.4f, 1.4f), R(-0.6f, 0.6f));
                    }
                    break;
                }
                case FxKind.Trail: // 표 8
                {
                    string hex = s.Variant == 1 ? "#ff8a3a" : s.Variant == 2 ? "#8fdcff" : color;
                    ref var q = ref Add(Law.Trail, p, Or(s.Life, life), s.HasColor ? s.Color : MvFxRouter.C(hex), FxShape.Glow, true, true);
                    q.s0 = s.Variant == 1 ? 0.75f : 0.55f;
                    q.v = s.Variant == 1 ? new Vector3(0, 1.5f, 0) : Vector3.zero;
                    break;
                }
                case FxKind.Impact: // 표 6
                {
                    var c = MvFxRouter.C(s.Crit ? "#ffb07a" : color);
                    ref var q = ref Add(Law.Impact, p + new Vector3(0, 0, -0.05f), s.Crit ? 0.13f : Or(s.Life, life), c, FxShape.Glow, true);
                    q.s0 = s.Crit ? 1.7f : Or(s.Size, size);
                    break;
                }
                case FxKind.Boom: // 표 3
                {
                    float r = Or(s.Radius, radius);
                    var c = Col(s, null);
                    ref var o = ref Add(Law.GrowSqrt, p, 0.45f, c, FxShape.Glow, true);
                    o.s0 = 0.5f; o.s1 = r * 2.4f;
                    ref var i2 = ref Add(Law.Grow, p, 0.25f, new Color32(255, 255, 255, 255), FxShape.Glow, true);
                    i2.s0 = 0.4f; i2.s1 = r * 1.2f;
                    if (!s.NoRing)
                    {
                        ref var g = ref Add(Law.Ring, new Vector3(p.x, 0.15f, p.z), 0.5f, c, FxShape.Ellipse, false);
                        g.s0 = 0.3f; g.s1 = r * 1.2f; g.a0 = 0.9f;
                    }
                    Sparks(p, c, 14, r * 3, 0.45f, 0.6f, 4);
                    break;
                }
                case FxKind.Bolt: // 표 4: 6구간, 중간 점 x·z ±0.35
                {
                    var a = s.Pos; var b = s.To;
                    var prev = a;
                    float lf = Or(s.Life, life);
                    var segC = MvFxRouter.C(color);
                    var nodeC = MvFxRouter.C("#7fd6ff");
                    for (int i = 1; i <= 6; i++)
                    {
                        var nx = Vector3.Lerp(a, b, i / 6f);
                        if (i < 6) nx += new Vector3(R(-0.35f, 0.35f), 0, R(-0.35f, 0.35f));
                        ref var seg = ref Add(Law.Bolt, (prev + nx) * 0.5f, lf, segC, FxShape.Rect, true);
                        seg.w = 0.1f; seg.h = Mathf.Max(0.1f, (nx - prev).magnitude);
                        ref var node = ref Add(Law.Bolt, prev, lf, nodeC, FxShape.Glow, true);
                        node.s0 = 0.9f; node.a0 = 0.8f;
                        prev = nx;
                    }
                    ref var end = ref Add(Law.Bolt, b, lf, segC, FxShape.Glow, true);
                    end.s0 = 2.2f;
                    break;
                }
                case FxKind.Fireball: // 표 9: (x+2.5, 9, z−3) → (x, 0.6, z)
                {
                    float sz = Or(s.Size, size), lf = Or(s.Life, life);
                    var to = s.Pos; var from = to + new Vector3(2.5f, 9f - 0.6f, -3f);
                    ref var q1 = ref Add(Law.Fall, from, lf, MvFxRouter.C(color), FxShape.Glow, true);
                    q1.a = from; q1.b = to; q1.s0 = sz;
                    ref var q2 = ref Add(Law.Fall, from, lf, MvFxRouter.C("#ffd27a"), FxShape.Glow, true);
                    q2.a = from; q2.b = to; q2.s0 = sz * 0.5f;
                    break;
                }
                case FxKind.Paw: // 표 13
                {
                    ref var q = ref Add(Law.Paw, p, Or(s.Life, life), MvFxRouter.C(color), FxShape.Disc, false);
                    q.s0 = 1.25f;
                    break;
                }
                case FxKind.Hiss: // 표 14
                    Add(Law.Hiss, p, Or(s.Life, life), MvFxRouter.C(color), FxShape.Ellipse, false);
                    break;
                case FxKind.BrickChunk: // 표 12
                {
                    string[] energy = { "#c8643c", "#d9874a", "#b8543a", "#e0a060" };
                    ref var q = ref Add(Law.Brick, p, Or(s.Life, life), s.HasColor ? s.Color : MvFxRouter.C(energy[Mathf.Abs(s.Variant) % 4]), FxShape.Rect, false, true);
                    q.v = new Vector3(s.Dir * R(1.5f, 3f), R(4, 6.5f), R(-1.5f, 1f));
                    q.g = Or(s.Grav, grav); q.w = 0.48f; q.h = 0.28f;
                    break;
                }
                case FxKind.HolyBeam: // 표 10
                {
                    ref var beam = ref Add(Law.Beam, p + new Vector3(0, 0, 11), 0.45f, MvFxRouter.C(color), FxShape.Rect, true);
                    beam.w = 0.35f; beam.h = 22f;
                    ref var outer = ref Add(Law.Grow, p, 0.45f, MvFxRouter.C("#ffc94a"), FxShape.Glow, true);
                    outer.s0 = 0.9f; outer.s1 = 0.9f; outer.a0 = 0.5f;
                    for (int i = 0; i < 10; i++) Sparks(p + new Vector3(0, 0, i * 2), MvFxRouter.C("#ffe08a"), 2, 3, 0.45f, 0.5f, -2);
                    ref var flash = ref Add(Law.Grow, p, 0.3f, new Color32(255, 255, 255, 255), FxShape.Glow, true);
                    flash.s0 = 3f;
                    break;
                }
                case FxKind.Meteor: // 표 11: 진행 k², 0.6 s
                {
                    var from = s.Pos; var to = s.To == s.Pos ? s.Pos + new Vector3(-6, -21.2f, 10) : s.To;
                    ref var body = ref Add(Law.MeteorBody, from, 0.6f, MvFxRouter.C(color), FxShape.Rect, false);
                    body.a = from; body.b = to; body.w = 0.7f; body.h = 0.7f;
                    ref var glow = ref Add(Law.MeteorBody, from, 0.6f, MvFxRouter.C("#ff5a1f"), FxShape.Glow, true);
                    glow.a = from; glow.b = to; glow.s0 = 4f; glow.a0 = 0.9f;
                    _meteorTail = 0;
                    break;
                }
                case FxKind.Frost: // 표 5 (D-46 미구현 예약, 드라이버만)
                {
                    var c = MvFxRouter.C(color);
                    for (int i = 0; i < 8; i++)
                    {
                        ref var q = ref Add(Law.Frost, p, 0.55f, c, FxShape.Rect, true, true);
                        q.v = new Vector3(R(-3, 3), R(1, 4), R(-2, 2)); q.g = 9; q.w = 0.08f; q.h = 0.24f;
                    }
                    ref var gl = ref Add(Law.Grow, p, 0.4f, MvFxRouter.C("#aee9ff"), FxShape.Glow, true);
                    gl.s0 = 2.4f; gl.s1 = 1.5f; gl.a0 = 0.8f;
                    break;
                }
            }
        }

        /// <summary>표 1 sparks. 살아 있는 입자가 700 초과면 min(n, 4).</summary>
        void Sparks(Vector3 p, Color32 c, int n, float spd, float sz, float lf, float g)
        {
            if (LiveParticles > SparkCapThreshold) n = Mathf.Min(n, SparkCapCount);
            for (int i = 0; i < n; i++)
            {
                ref var q = ref Add(Law.Spark, p, lf * R(0.7f, 1.2f), c, FxShape.Glow, true, true);
                q.v = new Vector3(R(-1, 1), R(0.2f, 1.4f), R(-1, 1)) * (spd * R(0.4f, 1f));
                q.s0 = sz * R(0.6f, 1.2f);
                q.g = g;
            }
        }

        void MeteorTail(float dt)
        {
            _meteorTail += dt;
            if (_meteorTail < 0.02f) return;
            _meteorTail = 0;
            for (int i = 0; i < _n; i++)
                if (_parts[i].law == Law.MeteorBody && _parts[i].shape == FxShape.Rect)
                {
                    var at = _parts[i].p;
                    ref var q = ref Add(Law.Grow, at, 0.45f, MvFxRouter.C("#ff7a2a"), FxShape.Glow, true);
                    q.s0 = 2.2f; q.s1 = -1.1f; q.a0 = 0.8f;
                    return;
                }
        }

        static void Integrate(ref Part q, float dt)
        {
            switch (q.law)
            {
                case Law.Spark:
                case Law.Frost:
                    q.v.y -= q.g * dt; q.p += q.v * dt; break;
                case Law.Debris:
                    q.v.y -= 16 * dt; q.p += q.v * dt;
                    if (q.p.y < 0.1f) { q.p.y = 0.1f; q.v = new Vector3(q.v.x * 0.5f, -q.v.y * 0.3f, q.v.z * 0.5f); }
                    break;
                case Law.Dust:
                case Law.Trail:
                    q.p += q.v * dt; break;
                case Law.Brick:
                    q.v.y -= q.g * dt; q.p += q.v * dt; break;
                case Law.Fall:
                    q.p = Vector3.Lerp(q.a, q.b, Mathf.Clamp01(q.t / q.life)); break;
                case Law.MeteorBody:
                {
                    float k = Mathf.Clamp01(q.t / q.life);
                    q.p = Vector3.Lerp(q.a, q.b, k * k);
                    break;
                }
            }
        }

        // ---------------- 그리기 ----------------

        void Rebuild()
        {
            if (_mesh == null) return;
            _builder.Clear();
            var cam = ViewCamera != null ? ViewCamera : Camera.main;
            Vector3 R1 = cam != null ? cam.transform.right : Vector3.right;
            Vector3 U = cam != null ? cam.transform.up : Vector3.up;
            Vector3 F = cam != null ? cam.transform.forward : Vector3.forward;
            var o = transform.position;
            for (int i = 0; i < _n; i++) Draw(ref _parts[i], o, R1, U, F);
            _builder.Apply(_mesh);
        }

        static Color32 A(Color32 c, float a)
        {
            c.a = (byte)Mathf.Clamp(Mathf.RoundToInt(a * 255f), 0, 255);
            return c;
        }

        void Draw(ref Part q, Vector3 o, Vector3 R1, Vector3 U, Vector3 F)
        {
            float k = Mathf.Clamp01(q.t / q.life);
            var c = q.p - o;
            switch (q.law)
            {
                case Law.Spark:
                    Glow(c, q.s0, A(q.c, 1 - k), q.add, R1, U); break;
                case Law.Debris:
                case Law.Brick:
                {
                    float sc = q.law == Law.Debris ? 1 - 0.7f * k : 1;
                    _builder.Quad(c, R1 * (q.w * 0.5f * sc), U * (q.h * 0.5f * sc), q.c, FxShape.Rect, 0, false);
                    break;
                }
                case Law.Frost:
                    _builder.Quad(c, R1 * (q.w * 0.5f), U * (q.h * 0.5f), A(q.c, 1 - k), FxShape.Rect, 0, true); break;
                case Law.Dust:
                    Glow(c, 1.1f * (1 + 1.8f * k), A(q.c, 0.55f * (1 - k)), false, R1, U); break;
                case Law.Cloud:
                    Glow(c, q.s0, A(q.c, 0.9f * Mathf.Min(1, Mathf.Min((1 - k) * 4, k * 8))), false, R1, U); break;
                case Law.Trail:
                    Glow(c, q.s0 * (1 - 0.6f * k), A(q.c, 0.9f * (1 - k)), true, R1, U); break;
                case Law.Impact:
                    Glow(c, q.s0 * (1 + 0.9f * k), A(q.c, 0.95f * (1 - k)), true, R1, U); break;
                case Law.Grow:
                    Glow(c, q.s0 + q.s1 * k, A(q.c, q.a0 * (1 - k)), true, R1, U); break;
                case Law.GrowSqrt:
                    Glow(c, q.s0 + q.s1 * Mathf.Sqrt(k), A(q.c, 1 - k), true, R1, U); break;
                case Law.Ring: // 타원 선 rx = sc·0.9, ry = rx·0.33, 굵기 sc·0.2, 일반 블렌딩(D-49)
                {
                    float sc = q.s0 + q.s1 * k, rx = sc * 0.9f, ry = rx * 0.33f, th = Mathf.Max(0.04f, sc * 0.2f);
                    _builder.Quad(c, R1 * (rx + th * 0.5f), U * (ry + th * 0.5f), A(q.c, q.a0 * (1 - k)), FxShape.Ellipse, th / (rx + th * 0.5f), false);
                    break;
                }
                case Law.Bolt:
                {
                    float a = (1 - k) * (UnityEngine.Random.value < 0.25f ? 0.4f : 1f) * q.a0;
                    if (q.shape == FxShape.Rect) _builder.Quad(c, R1 * (q.w * 0.5f), U * (q.h * 0.5f), A(q.c, a), FxShape.Solid, 0, true);
                    else Glow(c, q.s0, A(q.c, a), true, R1, U);
                    break;
                }
                case Law.Fall:
                    Glow(c, q.s0, q.c, true, R1, U); break;
                case Law.MeteorBody:
                    if (q.shape == FxShape.Rect) _builder.Quad(c, R1 * (q.w * 0.5f), U * (q.h * 0.5f), q.c, FxShape.Rect, 0, false);
                    else Glow(c, q.s0, A(q.c, q.a0), true, R1, U);
                    break;
                case Law.Beam:
                {
                    float w = q.w * (1 + 0.5f * k);
                    _builder.Quad(c, R1 * (w * 0.5f), U * (q.h * 0.5f), A(q.c, 1 - k), FxShape.Solid, 0, true);
                    break;
                }
                case Law.Paw:
                    DrawPaw(c, k, q.c, R1, U); break;
                case Law.Hiss:
                    DrawHiss(c, k, R1, U, F); break;
            }
        }

        void Glow(Vector3 c, float diameter, Color32 col, bool add, Vector3 R1, Vector3 U)
        {
            if (col.a <= 2 || diameter <= 0.01f) return;
            float r = diameter * 0.5f;
            _builder.Quad(c, R1 * r, U * r, col, FxShape.Glow, 0, add);
        }

        static readonly Vector2[] s_toes = { new Vector2(-0.55f, -0.42f), new Vector2(-0.2f, -0.62f), new Vector2(0.2f, -0.62f), new Vector2(0.55f, -0.42f) };
        static readonly (float m, string col, float th)[] s_hissRings = { (1f, "#c8a8ff", 0.35f), (0.8f, "#ffffff", 0.18f), (0.6f, "#9a7ad6", 0.12f) };

        /// <summary>표 13 `drawPawFx`(단위 m, 정본 s 배수 그대로).</summary>
        void DrawPaw(Vector3 c, float k, Color32 pad, Vector3 R1, Vector3 U)
        {
            float drop = k < 0.35f ? 1 - k / 0.35f : 0;
            float alpha = k < 0.7f ? 1 : 1 - (k - 0.7f) / 0.3f;
            if (drop <= 0)
            {
                float rx = 2.4f * (0.6f + k), ry = 0.7f * (0.6f + k), th = 0.06f;
                _builder.Quad(c, R1 * rx, U * ry, A(new Color32(255, 255, 255, 255), 0.6f * alpha), FxShape.Ellipse, th / rx, false);
            }
            float sc = 1.25f * (1 + 0.5f * drop);
            var at = c + U * (drop * 6f);
            float ol = Mathf.Max(0.04f, 0.06f);
            _builder.Quad(at, R1 * (0.85f * 0.5f * sc), U * (0.62f * 0.5f * sc), A(pad, alpha), FxShape.Disc, ol / (0.42f * sc), false);
            _builder.Quad(at, R1 * (0.5f * 0.5f * sc), U * (0.36f * 0.5f * sc), A(MvFxRouter.C("#f4a6b4"), alpha), FxShape.Disc, 0, false);
            var toes = s_toes;
            for (int i = 0; i < toes.Length; i++)
            {
                // 정본 화면 y는 아래가 +라 위쪽 발가락은 −y.
                var tp = at + R1 * (toes[i].x * sc) - U * (toes[i].y * sc);
                _builder.Quad(tp, R1 * (0.2f * 0.5f * sc), U * (0.17f * 0.5f * sc), A(pad, alpha), FxShape.Disc, ol / (0.1f * sc), false);
                _builder.Quad(tp, R1 * (0.11f * 0.5f * sc), U * (0.09f * 0.5f * sc), A(MvFxRouter.C("#f4a6b4"), alpha), FxShape.Disc, 0, false);
            }
        }

        /// <summary>표 14 `drawHissFx`.</summary>
        void DrawHiss(Vector3 c, float k, Vector3 R1, Vector3 U, Vector3 F)
        {
            float r = 0.8f + 5 * k, a = 1 - k;
            foreach (var g in s_hissRings)
            {
                float rx = r * g.m, ry = rx * 0.55f;
                _builder.Quad(c, R1 * (rx + g.th * 0.5f), U * (ry + g.th * 0.5f), A(MvFxRouter.C(g.col), a), FxShape.Ellipse, g.th / (rx + g.th * 0.5f), false);
            }
            var white = A(new Color32(255, 255, 255, 255), a);
            for (int i = 0; i < 10; i++)
            {
                float ang = i / 10f * Mathf.PI * 2;
                var dir = R1 * Mathf.Cos(ang) + U * (Mathf.Sin(ang) * 0.55f);
                _builder.Line(c + dir * (0.7f * r), c + dir * (1.1f * r), 0.08f, F, white, false);
            }
        }
    }
}
