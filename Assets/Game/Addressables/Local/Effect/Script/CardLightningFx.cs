using UnityEngine;

namespace MV.Fx
{
    /// <summary>
    /// 카드 번개 이펙트(웹 프로토 `bolt(a, b)` 이식). 시작점→끝점을 지그재그 선 2겹(파란 빛 + 흰 심지)으로 잇고,
    /// 꺾이는 점마다 작은 빛, 끝점에 큰 빛을 터뜨린 뒤 <see cref="Duration"/> 동안 깜빡이며 사라진다.
    /// 쓰는 법: <see cref="Play(Vector3, Vector3)"/>(월드 좌표). 인자 없이 켜지면 머리 위 <see cref="SkyOffset"/>에서 내리꽂는다.
    /// </summary>
    public sealed class CardLightningFx : MonoBehaviour
    {
        [Header("모양")]
        [Tooltip("꺾이는 마디 수(정본 N = 6)")] [Min(1)] public int Segments = 6;
        [Tooltip("마디마다 흔들리는 폭(정본 ±0.35)")] public float Jitter = 0.35f;
        [Tooltip("인자 없이 재생할 때 시작점(끝점 기준 로컬, 정본 감전 = 위 4.5)")] public Vector3 SkyOffset = new Vector3(0f, 4.5f, 0f);

        [Header("시간")]
        [Tooltip("보이는 시간(정본 0.22초)")] public float Duration = 0.22f;
        [Tooltip("지그재그를 다시 뽑는 간격(0이면 한 번만)")] public float Rejitter = 0.06f;
        [Tooltip("깜빡일 확률(정본 25%)과 그때 밝기(정본 0.4)")] [Range(0, 1)] public float FlickerChance = 0.25f;
        [Range(0, 1)] public float FlickerAlpha = 0.4f;

        [Header("미리보기")]
        [Tooltip("켜질 때 자동 재생")] public bool PlayOnEnable = true;
        [Tooltip("미리보기용 반복(게임에서 Play로 부를 때는 끈다)")] public bool Loop = true;
        public float LoopInterval = 0.6f;
        [Tooltip("끝나면 오브젝트를 끈다(풀 반납용, Loop가 꺼져 있을 때만)")] public bool DisableWhenDone = false;

        [Header("연결")]
        public LineRenderer Glow;
        public LineRenderer Core;
        public ParticleSystem Joints;
        public ParticleSystem Impact;

        Vector3 _from, _to;
        float _t = -1f, _jt, _loopT;
        Vector3[] _pts = new Vector3[0];
        Gradient _glowG, _coreG;

        void Awake()
        {
            if (Glow != null) _glowG = Glow.colorGradient;
            if (Core != null) _coreG = Core.colorGradient;
        }

        void OnEnable()
        {
            if (PlayOnEnable) PlayDefault();
        }

        /// <summary>이 오브젝트 위치를 끝점으로, 위 <see cref="SkyOffset"/>에서 내리꽂는다.</summary>
        public void PlayDefault()
        {
            var p = transform.position;
            Play(p + SkyOffset, p);
        }

        /// <summary>월드 좌표 <paramref name="from"/> → <paramref name="to"/>로 번개를 친다(연쇄 번개는 적→적으로 여러 번 부른다).</summary>
        public void Play(Vector3 from, Vector3 to)
        {
            _from = from; _to = to; _t = 0f; _jt = 0f; _loopT = 0f;
            Build(true);
            if (Impact != null) { Impact.transform.position = to; Impact.Play(true); }
        }

        void Build(bool emitJoints)
        {
            int n = Mathf.Max(1, Segments);
            if (_pts.Length != n + 1) _pts = new Vector3[n + 1];
            _pts[0] = _from; _pts[n] = _to;
            for (int i = 1; i < n; i++)
                _pts[i] = Vector3.Lerp(_from, _to, (float)i / n) + new Vector3(Random.Range(-Jitter, Jitter), Random.Range(-Jitter, Jitter), Random.Range(-Jitter, Jitter));
            SetLine(Glow, true);
            SetLine(Core, true);
            if (emitJoints && Joints != null)
            {
                var ep = new ParticleSystem.EmitParams { applyShapeToPosition = false };
                for (int i = 0; i < n; i++) { ep.position = _pts[i]; Joints.Emit(ep, 1); }
            }
        }

        void SetLine(LineRenderer lr, bool on)
        {
            if (lr == null) return;
            lr.useWorldSpace = true;
            lr.positionCount = _pts.Length;
            lr.SetPositions(_pts);
            lr.enabled = on;
        }

        void Update()
        {
            if (_t < 0f)
            {
                if (Loop && PlayOnEnable) { _loopT += Time.deltaTime; if (_loopT >= LoopInterval) PlayDefault(); }
                return;
            }
            _t += Time.deltaTime;
            float k = Mathf.Clamp01(_t / Mathf.Max(0.01f, Duration));
            if (Rejitter > 0f) { _jt += Time.deltaTime; if (_jt >= Rejitter) { _jt = 0f; Build(false); } }
            float a = (1f - k) * (Random.value < FlickerChance ? FlickerAlpha : 1f);
            Fade(Glow, _glowG, a);
            Fade(Core, _coreG, a);
            if (_t >= Duration)
            {
                _t = -1f; _loopT = 0f;
                if (Glow != null) Glow.enabled = false;
                if (Core != null) Core.enabled = false;
                if (!Loop && DisableWhenDone) gameObject.SetActive(false);
            }
        }

        static void Fade(LineRenderer lr, Gradient src, float a)
        {
            if (lr == null || src == null) return;
            var ak = src.alphaKeys;
            for (int i = 0; i < ak.Length; i++) ak[i].alpha *= a;
            var g = new Gradient(); g.SetKeys(src.colorKeys, ak);
            lr.colorGradient = g;
        }
    }
}
