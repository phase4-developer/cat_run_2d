using jjevol.Effect;
using MV.View;
using UnityEngine;

namespace MV.Fx
{
    /// <summary>
    /// 계단 경험치 오브 하나(디자이너 <c>Obj_Stair_Orb</c> 뿌리, MV-UI3-STAIRFX 노션 「World Object Attraction」).
    /// Framework <c>EffectManager</c>가 빌려 주고(<see cref="StairOrbFx"/>, UI Effect 루트 아래) 이 부품은 이펙트 동작(<see cref="IEffectBehaviour"/>)으로 움직인다.
    /// 순서: 탑 오브에서 나와 퍼질 자리까지 이동(<see cref="riseSec"/>) → 잠깐 멈춤(<see cref="holdSec"/>, 노션 약 0.2 s) → HUD 도착 자리로 이동(<see cref="flySec"/>) → 반환.
    /// UI 카메라가 그리므로 HUD보다 앞에 보인다(MV-UI3-STAIRFX2). 출발·퍼질 자리는 월드 점이라 매 프레임 월드 카메라로 화면에 투영하고(카메라가 움직여도 탑을 따라간다),
    /// 도착 자리는 HUD 자리(<see cref="StairOrbFlight.Target"/>, 화면 px)다. 크기는 퍼질 자리에서 월드에서 보이던 크기와 같게 맞춘다.
    /// 연출 값은 이 프리팹 필드가 정본이다(노션에 수치가 없는 이동 시간·곡선·배율은 임시 값, 원장 MV-UI3-STAIRFX).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class StairOrb : MonoBehaviour, IEffectBehaviour
    {
        [Tooltip("탑 오브 → 퍼질 자리 이동 초(임시)")]
        [SerializeField] float riseSec = 0.3f;
        [Tooltip("퍼질 자리 이동 곡선(시간 0~1 → 진행 0~1)")]
        [SerializeField] AnimationCurve riseCurve = new AnimationCurve(new Keyframe(0, 0, 2, 2), new Keyframe(1, 1, 0, 0));
        [Tooltip("퍼진 뒤 멈춤 초(노션 약 0.2 s)")]
        [SerializeField] float holdSec = 0.2f;
        [Tooltip("퍼질 자리 → HUD 도착 자리 이동 초(임시)")]
        [SerializeField] float flySec = 0.45f;
        [Tooltip("HUD로 가는 이동 곡선(시간 0~1 → 진행 0~1, 임시 = 점점 빨라짐)")]
        [SerializeField] AnimationCurve flyCurve = new AnimationCurve(new Keyframe(0, 0, 0, 0), new Keyframe(1, 1, 2, 2));
        [Tooltip("시작 크기 배율(임시)")]
        [SerializeField] float startScale = 0.4f;
        [Tooltip("퍼진 자리 크기 배율(임시)")]
        [SerializeField] float spreadScale = 1f;
        [Tooltip("도착 때 크기 배율(임시)")]
        [SerializeField] float endScale = 0.6f;
        [Tooltip("반환할 때 잔상(꼬리)을 지울 파티클(자식 포함). 재생은 EffectManager가 한다.")]
        [SerializeField] ParticleSystem particle;

        enum Phase { Idle, Rise, Hold, Fly }

        Phase _phase;
        float _t, _fit = 1;
        Vector3 _from, _spread, _baseScale;
        bool _scaleCaptured;
        Camera _worldCam, _uiCam;
        RectTransform _plane;
        EffectHandle _handle;

        /// <summary>날고 있는가.</summary>
        public bool Busy => _phase != Phase.Idle;

        /// <summary>IEffectBehaviour: 날기가 끝났으면 true.</summary>
        public bool IsFinished => _phase == Phase.Idle;

        /// <summary>전체 연출 초(멈춤 포함). 시험·QA.</summary>
        public float TotalSec => Mathf.Max(0, riseSec) + Mathf.Max(0, holdSec) + Mathf.Max(0, flySec);

        /// <summary>월드 크기 → UI 크기 맞춤 배율(시험·QA).</summary>
        public float Fit => _fit;

        void CaptureScale()
        {
            if (_scaleCaptured) return;
            _scaleCaptured = true;
            _baseScale = transform.localScale;
        }

        /// <summary>EffectManager가 재생을 시작할 때. 요청 표식으로 출발 정보를 찾는다. 못 찾으면 바로 돌려준다.</summary>
        public void Begin(EffectHandle handle)
        {
            _handle = handle;
            if (handle == null || !StairOrbFx.TryGet(handle.Request.skinIndex, out var r))
            {
                _phase = Phase.Idle;
                if (handle != null) handle.Despawn();
                return;
            }
            Launch(r.From, r.Spread, r.WorldCam, handle.transform.parent);
        }

        /// <summary>
        /// 출발. <paramref name="from"/> 탑 오브 월드 자리, <paramref name="spread"/> 퍼질 월드 자리, <paramref name="worldCam"/> 월드 카메라,
        /// <paramref name="plane"/> 오브가 놓일 UI 판(Effect 루트 RectTransform). 판이 없으면 월드 자리 그대로 난다.
        /// </summary>
        public void Launch(Vector3 from, Vector3 spread, Camera worldCam, Transform plane)
        {
            CaptureScale();
            _from = from;
            _spread = spread;
            _worldCam = worldCam;
            _plane = plane as RectTransform;
            var canvas = plane != null ? plane.GetComponentInParent<Canvas>(true) : null;
            _uiCam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.rootCanvas.worldCamera : null;
            if (plane != null) SetLayer(transform, plane.gameObject.layer); // UI 카메라는 UI 레이어만 그린다
            _fit = FitScale();
            _t = 0;
            _phase = Phase.Rise;
            transform.position = WorldToUi(from);
            transform.localScale = _baseScale * (_fit * startScale);
        }

        static void SetLayer(Transform t, int layer)
        {
            t.gameObject.layer = layer;
            for (int i = 0; i < t.childCount; i++) SetLayer(t.GetChild(i), layer);
        }

        /// <summary>퍼질 자리에서 월드 카메라로 보이던 크기(px)를 UI 카메라에서도 같게 하는 localScale 배율.</summary>
        float FitScale()
        {
            if (_plane == null || _worldCam == null) return 1;
            float worldPpu = PixelsPerUnit(_worldCam, _spread);
            float uiPpu = _uiCam != null ? PixelsPerUnit(_uiCam, _plane.position) : 1f; // Overlay: 월드 1 = 1 px
            var parent = transform.parent;
            float parentScale = parent != null ? Mathf.Abs(parent.lossyScale.x) : 1f;
            if (worldPpu <= 0 || uiPpu <= 0 || parentScale <= 0) return 1;
            return worldPpu / uiPpu / parentScale;
        }

        static float PixelsPerUnit(Camera c, Vector3 at)
        {
            if (c.orthographic) return c.orthographicSize > 0 ? c.pixelHeight / (2f * c.orthographicSize) : 0;
            float depth = Vector3.Dot(at - c.transform.position, c.transform.forward);
            if (depth <= 0) return 0;
            return c.pixelHeight / (2f * depth * Mathf.Tan(c.fieldOfView * 0.5f * Mathf.Deg2Rad));
        }

        Vector3 ScreenToUi(Vector2 screen)
        {
            if (_plane != null && RectTransformUtility.ScreenPointToWorldPointInRectangle(_plane, screen, _uiCam, out var w)) return w;
            return screen;
        }

        Vector3 WorldToUi(Vector3 world) =>
            _plane != null && _worldCam != null ? ScreenToUi(_worldCam.WorldToScreenPoint(world)) : world;

        /// <summary>IEffectBehaviour: 매 프레임(요청의 unscaledTime = 실시간, 시뮬 게이지 수령 108 wellGauge도 Real 시계).</summary>
        public void Tick(float dt) => Step(dt);

        /// <summary>시간을 직접 진행(시험). 실행 중에는 EffectHandle이 <see cref="Tick"/>으로 부른다.</summary>
        public void Step(float dt)
        {
            if (_phase == Phase.Idle) return;
            _t += Mathf.Max(0, dt);
            if (_phase == Phase.Rise)
            {
                float k = riseSec > 0 ? Mathf.Clamp01(_t / riseSec) : 1;
                float e = riseCurve != null ? riseCurve.Evaluate(k) : k;
                transform.position = Vector3.LerpUnclamped(WorldToUi(_from), WorldToUi(_spread), e);
                transform.localScale = _baseScale * (_fit * Mathf.LerpUnclamped(startScale, spreadScale, e));
                if (k < 1) return;
                _t -= Mathf.Max(0, riseSec);
                _phase = Phase.Hold;
            }
            if (_phase == Phase.Hold)
            {
                transform.position = WorldToUi(_spread);
                if (_t < holdSec) return;
                _t -= Mathf.Max(0, holdSec);
                _phase = Phase.Fly;
            }
            if (_phase == Phase.Fly)
            {
                float k = flySec > 0 ? Mathf.Clamp01(_t / flySec) : 1;
                float e = flyCurve != null ? flyCurve.Evaluate(k) : k;
                var from = WorldToUi(_spread);
                var to = TryTarget(out var target) ? target : from;
                transform.position = Vector3.LerpUnclamped(from, to, e);
                transform.localScale = _baseScale * (_fit * Mathf.LerpUnclamped(spreadScale, endScale, e));
                if (k >= 1) Finish();
            }
        }

        /// <summary>HUD 도착 자리(UI 판 위 월드 점). 판이 없으면 false(오브는 퍼진 자리에서 끝난다).</summary>
        public bool TryTarget(out Vector3 ui)
        {
            ui = default;
            if (_plane == null || StairOrbFlight.Target == null || !StairOrbFlight.Target(out var screen)) return false;
            ui = ScreenToUi(screen);
            return true;
        }

        void Finish()
        {
            _phase = Phase.Idle;
            var h = _handle;
            _handle = null;
            // 도착 신호는 요청이 끝날 때(onEnded) 한 번 간다(StairOrbFx).
            if (h != null) h.Despawn();
        }

        /// <summary>IEffectBehaviour: 요청이 끝나 풀로 돌아갈 때. 제모습으로 두고 꼬리 잔상을 지운다.</summary>
        public void End()
        {
            CaptureScale();
            _phase = Phase.Idle;
            _handle = null;
            _worldCam = _uiCam = null;
            _plane = null;
            transform.localScale = _baseScale;
            if (particle) particle.Clear(true);
        }
    }
}
