using UnityEngine;

namespace MV.Fx
{
    /// <summary>
    /// 이펙트 프리팹 안의 디자이너 파티클(ParticleSystem, 또는 묶음 Transform)에 붙여 호출 값을 따르게 한다.
    /// 04_effect가 넘기는 색·개수·반경·치명타·고리 끔·두 번째 점(To)·수명을 <see cref="MvFxDriver"/>가 재생 직전에 <see cref="Apply"/>로 넘긴다.
    /// 붙이지 않은 파티클은 프리팹에 저장된 값 그대로 재생한다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MvFxParticleBind : MonoBehaviour
    {
        public enum SizeBy { None, Radius, Crit }
        public enum MoveBy { None, FromOffset, ToTarget }
        public enum ColorFrom { Call, WeaponGrade }

        static bool s_hasGrade;
        static Color32 s_grade;

        /// <summary>장착 무기 등급 색(`103_item.gradeCol`). <see cref="MvFxRouter"/>가 등급이 바뀔 때 부른다. 빈칸이면 프리팹 색.</summary>
        public static void SetWeaponGrade(string hex)
        {
            s_hasGrade = !string.IsNullOrEmpty(hex) && MV.View.MvColor.TryColor32(hex, out s_grade);
        }

        [Header("색")]
        [Tooltip("호출 색이 있으면 시작 색을 그 색으로 바꿈(치명타 Impact는 치명타 색). 없으면 프리팹 색")] public bool Tint;
        [Tooltip("두 번째 시작 색을 흰색 쪽으로 섞는 정도(색 사이 무작위)")] [Range(0f, 1f)] public float Lighten = 0.4f;
        [Tooltip("Tint 색 출처: Call = 호출 색(04_effect), WeaponGrade = 장착 무기 등급 색(103_item.gradeCol)")] public ColorFrom Source;
        [Header("개수·크기")]
        [Tooltip("호출이 고리 끔(noRing)이면 이 파티클은 내보내지 않음")] public bool RingOnly;
        [Tooltip("Burst 개수 = 프리팹 개수 × 호출 개수 / 드라이버 기본 개수")] public bool ScaleBurst;
        [Tooltip("이 Transform 크기: Radius = 호출 반경 / 드라이버 기본 반경, Crit = 치명타일 때 CritScale배")] public SizeBy ScaleBy;
        [Tooltip("치명타일 때 크기 배율(ScaleBy = Crit)")] public float CritScale = 1.4f;
        [Header("위치·이동")]
        [Tooltip("재생 위치를 두 번째 점(To, 번개 끝점 등)으로 옮김")] public bool AtTarget;
        [Tooltip("FromOffset = 시작점(호출 위치 + Offset)에서 호출 위치로, ToTarget = 호출 위치에서 두 번째 점(To)으로 이동")] public MoveBy Move;
        [Tooltip("FromOffset 출발 오프셋 / ToTarget에서 To가 없을 때 쓸 도착 오프셋")] public Vector3 Offset;
        [Tooltip("이동 시간(0 = 호출 수명)")] public float MoveTime;
        [Tooltip("이동 가속(1 = 일정, 2 = 갈수록 빨라짐)")] public float Ease = 1f;
        [Header("번개")]
        [Tooltip("같은 오브젝트의 CardLightningFx를 호출 위치 → 두 번째 점(To)으로 재생")] public bool PlayLightning;

        bool _cached;
        ParticleSystem _ps;
        ParticleSystem.MinMaxGradient _color;
        short[] _bursts;
        Vector3 _scale;
        Vector3 _from, _to;
        float _t, _dur;
        bool _moving;

        void Cache()
        {
            if (_cached) return;
            _cached = true;
            _scale = transform.localScale;
            _ps = GetComponent<ParticleSystem>();
            if (_ps == null) return;
            _color = _ps.main.startColor;
            var e = _ps.emission;
            _bursts = new short[e.burstCount];
            for (int i = 0; i < _bursts.Length; i++) _bursts[i] = (short)e.GetBurst(i).count.constantMax;
        }

        /// <summary>재생 직전 1번. <paramref name="life"/> = 호출 수명(없으면 드라이버 기본).</summary>
        public void Apply(in FxSpawn s, bool hasColor, Color32 color, float countRatio, float radiusRatio, float life)
        {
            Cache();
            if (ScaleBy == SizeBy.Radius) transform.localScale = _scale * Mathf.Max(0.05f, radiusRatio);
            else if (ScaleBy == SizeBy.Crit) transform.localScale = _scale * (s.Crit ? CritScale : 1f);

            bool hasTo = s.To != default && (s.To - s.Pos).sqrMagnitude > 1e-6f;
            if (AtTarget) transform.position = hasTo ? s.To : s.Pos;
            _moving = false;
            if (Move != MoveBy.None)
            {
                _from = Move == MoveBy.FromOffset ? s.Pos + Offset : s.Pos;
                _to = Move == MoveBy.FromOffset ? s.Pos : (hasTo ? s.To : s.Pos + Offset);
                _dur = MoveTime > 0f ? MoveTime : Mathf.Max(0.01f, life);
                _t = 0f;
                _moving = true;
                transform.position = _from;
            }

            if (PlayLightning && TryGetComponent(out CardLightningFx bolt)) bolt.Play(s.Pos, hasTo ? s.To : s.Pos + Vector3.down * 3.5f);

            if (_ps == null) return;
            var main = _ps.main;
            bool useGrade = Tint && Source == ColorFrom.WeaponGrade && s_hasGrade;
            bool useCall = Tint && Source == ColorFrom.Call && hasColor;
            if (useGrade || useCall) { Color c = useGrade ? s_grade : color; main.startColor = new ParticleSystem.MinMaxGradient(c, Color.Lerp(c, Color.white, Lighten)); }
            else main.startColor = _color;
            var e = _ps.emission;
            e.enabled = !(RingOnly && s.NoRing);
            if (_bursts == null) return;
            for (int i = 0; i < _bursts.Length; i++)
            {
                var b = e.GetBurst(i);
                short n = _bursts[i];
                if (ScaleBurst) n = (short)Mathf.Clamp(Mathf.RoundToInt(n * countRatio), 1, 64);
                b.count = n;
                e.SetBurst(i, b);
            }
        }

        /// <summary>이동 진행(시험이 직접 부를 수 있다).</summary>
        public void Step(float dt)
        {
            if (!_moving) return;
            _t += dt;
            float k = Mathf.Clamp01(_t / _dur);
            transform.position = Vector3.Lerp(_from, _to, Mathf.Pow(k, Mathf.Max(0.01f, Ease)));
            if (k >= 1f) _moving = false;
        }

        void Update() { if (_moving) Step(MvFxClock.SimDt); }
    }
}
