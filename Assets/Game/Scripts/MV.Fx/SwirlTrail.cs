using UnityEngine;

namespace MV.Fx
{
    /// <summary>
    /// 휘몰아치는 꼬리. 자식 줄기(<see cref="Strands"/>)를 진행 방향 둘레로 나선처럼 돌려,
    /// 줄기마다 붙은 TrailRenderer가 서로 꼬인 리본 자취를 남기게 한다.
    /// 진행 방향은 매 프레임 위치 변화로 구하므로 부모(proj_Image)가 돌아도 모양이 따라 돌지 않는다.
    /// 한 프레임에 많이 움직이면 그 사이를 잘게 나눠 점을 넣어 나선이 각지지 않게 한다.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(1000)]
    public sealed class SwirlTrail : MonoBehaviour
    {
        [Tooltip("나선으로 돌 줄기들(각각 TrailRenderer)")] public Transform[] Strands;
        [Header("모양")]
        [Tooltip("나선 반지름(m)")] public float Radius = 0.24f;
        [Tooltip("한 바퀴 도는 데 걸리는 진행 거리(m). 작을수록 촘촘히 꼬임")] public float Wavelength = 1.6f;
        [Tooltip("반지름 출렁임(0~1). 줄기마다 박자가 달라 들쭉날쭉한 불꽃처럼 보임")] [Range(0f, 1f)] public float Wobble = 0.35f;
        [Tooltip("출발 직후 반지름이 다 펴질 때까지의 거리(m)")] public float RampDistance = 0.6f;
        [Tooltip("줄기마다 도는 방향을 번갈아 바꿈(엇갈려 땋인 모양)")] public bool Braid = false;
        [Header("샘플")]
        [Tooltip("자취 점 간격(m). 작을수록 매끈")] public float SubStep = 0.06f;
        [Tooltip("이보다 멀리 한 번에 옮겨지면 순간이동(풀 재사용)으로 보고 자취를 지움(m)")] public float Teleport = 3f;

        TrailRenderer[] _trails;
        Vector3 _last, _dir = Vector3.forward;
        float _phase, _travel;
        bool _has;

        void Awake() => CacheTrails();

        void CacheTrails()
        {
            if (Strands == null) Strands = new Transform[0];
            _trails = new TrailRenderer[Strands.Length];
            for (int i = 0; i < Strands.Length; i++) if (Strands[i] != null) _trails[i] = Strands[i].GetComponent<TrailRenderer>();
        }

        void OnEnable()
        {
            _has = false; _travel = 0f;
            _phase = Random.value * Mathf.PI * 2f;
        }

        void LateUpdate()
        {
            if (Strands == null || Strands.Length == 0) return;
            if (_trails == null || _trails.Length != Strands.Length) CacheTrails();
            var p = transform.position;
            if (!_has) { _has = true; _last = p; Place(p, _phase, false); return; }

            var d = p - _last; float m = d.magnitude;
            if (m > Teleport)
            {
                _travel = 0f; _last = p; Place(p, _phase, false);
                foreach (var tr in _trails) if (tr != null) tr.Clear();
                return;
            }
            if (m > 1e-5f)
            {
                _dir = Vector3.Slerp(_dir, d / m, 0.6f).normalized;
                int n = Mathf.Clamp(Mathf.CeilToInt(m / Mathf.Max(0.01f, SubStep)), 1, 32);
                float dPh = m / Mathf.Max(0.05f, Wavelength) * Mathf.PI * 2f;
                for (int s = 1; s < n; s++)   // 사이 점: 자취에 직접 넣는다
                {
                    float k = (float)s / n;
                    _travel += m / n;
                    Place(Vector3.Lerp(_last, p, k), _phase + dPh * k, true);
                }
                _travel += m / n;
                _phase += dPh;
            }
            _last = p;
            Place(p, _phase, false);   // 마지막 점은 TrailRenderer가 스스로 찍는다
        }

        void Place(Vector3 c, float phase, bool addPoint)
        {
            var a = Vector3.Cross(_dir, Vector3.up);
            if (a.sqrMagnitude < 1e-4f) a = Vector3.Cross(_dir, Vector3.right);
            a.Normalize();
            var b = Vector3.Cross(a, _dir);
            float ramp = RampDistance > 0f ? Mathf.Clamp01(_travel / RampDistance) : 1f;
            int count = Strands.Length;
            for (int i = 0; i < count; i++)
            {
                var s = Strands[i]; if (s == null) continue;
                float dirSign = Braid && (i & 1) == 1 ? -1f : 1f;
                float ph = phase * dirSign + i * Mathf.PI * 2f / count;
                float r = Radius * ramp * (1f + Wobble * Mathf.Sin(phase * 0.5f + i * 1.7f));
                var w = c + (a * Mathf.Cos(ph) + b * Mathf.Sin(ph)) * r;
                if (addPoint) { var tr = _trails[i]; if (tr != null && tr.emitting) tr.AddPosition(w); }
                else s.position = w;
            }
        }
    }
}
