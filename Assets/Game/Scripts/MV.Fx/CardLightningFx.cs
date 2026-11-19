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
        [Tooltip("번개 줄기 상단(시작점)에 띄울 구름. 하늘에서 내리꽂을 때(시작점이 끝점보다 Cloud Min Drop 이상 위)만 나온다")]
        public ParticleSystem Cloud;
        [Tooltip("구름을 띄우는 최소 높이 차(m). 적→적 연쇄 번개처럼 옆으로 치는 번개에는 구름을 띄우지 않는다")] public float CloudMinDrop = 1f;
        [Tooltip("구름 위치 = 시작점 + 이 값(구름 아랫부분에서 줄기가 나오게)")] public Vector3 CloudOffset = new Vector3(0f, 0.25f, 0f);
        [Tooltip("연쇄 번개(옆으로 치는 번개)의 연결 지점 효과 2개: [0] = 시작점, [1] = 끝점. 하늘에서 내리꽂는 번개에는 나오지 않는다")]
        public ParticleSystem[] ChainPoints = new ParticleSystem[0];
        [Tooltip("연결 지점 효과를 내보내는 시간(초). 지나면 새로 내보내기만 멈추고 나온 입자는 제 수명대로 사라진다")] public float ChainPointTime = 0.3f;
        [Tooltip("같은 자리(이 거리 m 안)에 방금(0.15초 안) 연결 지점 효과가 났으면 또 내지 않는다(A→B→C에서 B가 겹치지 않게)")] public float ChainPointMerge = 0.4f;

        [Tooltip("연쇄 번개와 같은 순간(0.1초 안)·같은 자리(가로 이 거리 m 안)에 친 내리꽂는 번개는 구름을 띄우지 않는다(연쇄 번개 중에는 구름 없음)")]
        public float CloudChainRadius = 1.5f;

        static readonly System.Collections.Generic.List<Vector4> s_recentPoints = new System.Collections.Generic.List<Vector4>(16);
        /// <summary>최근 연쇄 번개 양 끝점(xyz, w = 시각).</summary>
        static readonly System.Collections.Generic.List<Vector4> s_recentChains = new System.Collections.Generic.List<Vector4>(8);
        /// <summary>최근 구름을 띄운 번개들(같은 순간 연쇄 번개가 오면 구름을 지운다).</summary>
        static readonly System.Collections.Generic.List<CardLightningFx> s_cloudOwners = new System.Collections.Generic.List<CardLightningFx>(8);
        Vector3 _cloudAt;
        float _cloudTime = -1f;
        float _chainStopAt = -1f;

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
            bool sky = from.y - to.y >= CloudMinDrop;
            float now = Time.time;
            if (!sky) { SuppressCloudsNear(from, now); SuppressCloudsNear(to, now); }   // 연쇄 번개 양 끝(시작 적·맞은 적)
            if (Cloud != null && sky && !ChainedNear(to, now))
            {
                Cloud.transform.position = from + CloudOffset;
                Cloud.Play(true);
                _cloudAt = to; _cloudTime = now;
                if (!s_cloudOwners.Contains(this)) s_cloudOwners.Add(this);
            }
            if (!sky && ChainPoints != null)
            {
                for (int i = 0; i < ChainPoints.Length && i < 2; i++)
                {
                    var cp = ChainPoints[i];
                    if (cp == null) continue;
                    var at = i == 0 ? from : to;
                    if (RecentlyPlayed(at)) continue;
                    cp.transform.position = at;
                    cp.Clear(true);
                    cp.Play(true);
                }
                _chainStopAt = Time.time + ChainPointTime;
            }
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

        static bool NearXZ(Vector3 a, Vector3 b, float r) { float dx = a.x - b.x, dz = a.z - b.z; return dx * dx + dz * dz <= r * r; }

        /// <summary>연쇄 번개 끝점 하나를 기록하고, 같은 순간·같은 자리에서 구름을 띄운 내리꽂는 번개가 있으면 그 구름을 지운다.</summary>
        void SuppressCloudsNear(Vector3 from, float now)
        {
            for (int i = s_recentChains.Count - 1; i >= 0; i--) if (now - s_recentChains[i].w > 0.1f) s_recentChains.RemoveAt(i);
            s_recentChains.Add(new Vector4(from.x, from.y, from.z, now));
            for (int i = s_cloudOwners.Count - 1; i >= 0; i--)
            {
                var b = s_cloudOwners[i];
                if (b == null || b.Cloud == null || now - b._cloudTime > 0.1f) { s_cloudOwners.RemoveAt(i); continue; }
                if (!NearXZ(b._cloudAt, from, CloudChainRadius)) continue;
                b.Cloud.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                s_cloudOwners.RemoveAt(i);
            }
        }

        /// <summary>같은 순간(0.1초 안) 이 자리 근처에서 연쇄 번개가 시작됐는지.</summary>
        bool ChainedNear(Vector3 at, float now)
        {
            for (int i = 0; i < s_recentChains.Count; i++)
            {
                var c = s_recentChains[i];
                if (now - c.w <= 0.1f && NearXZ(new Vector3(c.x, c.y, c.z), at, CloudChainRadius)) return true;
            }
            return false;
        }

        /// <summary>같은 자리에서 방금 연결 지점 효과가 났는지(여러 번개 오브젝트가 같은 적을 공유하는 연쇄 번개용). 아니면 기록한다.</summary>
        bool RecentlyPlayed(Vector3 at)
        {
            float now = Time.time;
            for (int i = s_recentPoints.Count - 1; i >= 0; i--)
            {
                var r = s_recentPoints[i];
                if (now - r.w > 0.15f) { s_recentPoints.RemoveAt(i); continue; }
                if ((new Vector3(r.x, r.y, r.z) - at).sqrMagnitude <= ChainPointMerge * ChainPointMerge) return true;
            }
            s_recentPoints.Add(new Vector4(at.x, at.y, at.z, now));
            return false;
        }

        void Update()
        {
            if (_chainStopAt >= 0f && Time.time >= _chainStopAt)
            {
                _chainStopAt = -1f;
                if (ChainPoints != null) foreach (var cp in ChainPoints) if (cp != null) cp.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }
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
