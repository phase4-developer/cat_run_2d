using System;
using UnityEngine;

namespace MV.View
{
    /// <summary>
    /// 계단 경험치 오브 하나(디자이너 <c>Obj_Stair_Orb</c> 뿌리, MV-UI3-STAIRFX 노션 「World Object Attraction」).
    /// 순서: 탑 오브에서 나와 퍼질 자리까지 이동(<see cref="riseSec"/>) → 잠깐 멈춤(<see cref="holdSec"/>, 노션 약 0.2 s) → HUD 도착 자리로 이동(<see cref="flySec"/>) → 사라짐.
    /// 도착 자리는 카메라가 움직여도 HUD 자리에 닿도록 매 프레임 다시 구한다(<see cref="StairOrbFlight.TryTargetWorld"/>).
    /// 시간은 실시간(시뮬 게이지 수령 108 wellGauge도 Real 시계). 끝나면 빌려 준 쪽(<see cref="StairOrbEmitter"/>)에 돌려 달라고 알린다.
    /// 연출 값은 이 프리팹 필드가 정본이다(노션에 수치가 없는 이동 시간·곡선·배율은 임시 값, 원장 MV-UI3-STAIRFX).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class StairOrb : MonoBehaviour
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
        [Tooltip("다시 쓸 때 처음부터 재생할 파티클(자식 포함). 비면 파티클을 건드리지 않는다.")]
        [SerializeField] ParticleSystem particle;

        enum Phase { Idle, Rise, Hold, Fly }

        Phase _phase;
        float _t;
        Vector3 _from, _spread, _baseScale;
        bool _scaleCaptured;
        Camera _cam;
        Action<StairOrb, bool> _done;

        /// <summary>날고 있는가(풀 대여 중).</summary>
        public bool Busy => _phase != Phase.Idle;

        /// <summary>전체 연출 초(멈춤 포함). 시험·QA.</summary>
        public float TotalSec => Mathf.Max(0, riseSec) + Mathf.Max(0, holdSec) + Mathf.Max(0, flySec);

        void CaptureScale()
        {
            if (_scaleCaptured) return;
            _scaleCaptured = true;
            _baseScale = transform.localScale;
        }

        /// <summary>출발. <paramref name="from"/> 탑 오브 월드 자리, <paramref name="spread"/> 퍼질 월드 자리, <paramref name="cam"/> 월드 카메라.</summary>
        /// <paramref name="done"/>(오브, 중단 여부): 중단 = 날던 중 꺼지거나 지워짐(이때 돌려받는 쪽은 바로 비활성화하지 말고 나중에 회수한다).
        public void Launch(Vector3 from, Vector3 spread, Camera cam, Action<StairOrb, bool> done)
        {
            CaptureScale();
            _from = from;
            _spread = spread;
            _cam = cam;
            _done = done;
            _t = 0;
            _phase = Phase.Rise;
            transform.position = from;
            transform.localScale = _baseScale * startScale;
            if (particle)
            {
                particle.Clear(true);
                particle.Play(true);
            }
        }

        /// <summary>풀 반환 전: 멈추고 제모습으로(파티클 잔상 지움).</summary>
        public void ResetLook()
        {
            CaptureScale();
            _phase = Phase.Idle;
            _done = null;
            _cam = null;
            transform.localScale = _baseScale;
            if (particle)
            {
                particle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                particle.Clear(true);
            }
        }

        /// <summary>시간을 직접 진행(시험). 실행 중에는 LateUpdate가 실시간으로 부른다.</summary>
        public void Step(float dt)
        {
            if (_phase == Phase.Idle) return;
            _t += Mathf.Max(0, dt);
            if (_phase == Phase.Rise)
            {
                float k = riseSec > 0 ? Mathf.Clamp01(_t / riseSec) : 1;
                float e = riseCurve != null ? riseCurve.Evaluate(k) : k;
                transform.position = Vector3.LerpUnclamped(_from, _spread, e);
                transform.localScale = _baseScale * Mathf.LerpUnclamped(startScale, spreadScale, e);
                if (k < 1) return;
                _t -= Mathf.Max(0, riseSec);
                _phase = Phase.Hold;
            }
            if (_phase == Phase.Hold)
            {
                if (_t < holdSec) return;
                _t -= Mathf.Max(0, holdSec);
                _phase = Phase.Fly;
            }
            if (_phase == Phase.Fly)
            {
                float k = flySec > 0 ? Mathf.Clamp01(_t / flySec) : 1;
                float e = flyCurve != null ? flyCurve.Evaluate(k) : k;
                var to = _spread;
                if (_cam != null)
                {
                    float depth = Vector3.Dot(_spread - _cam.transform.position, _cam.transform.forward);
                    if (StairOrbFlight.TryTargetWorld(_cam, depth, out var w)) to = w;
                }
                transform.position = Vector3.LerpUnclamped(_spread, to, e);
                transform.localScale = _baseScale * Mathf.LerpUnclamped(spreadScale, endScale, e);
                if (k >= 1) Finish(false);
            }
        }

        void LateUpdate() => Step(Time.unscaledDeltaTime);

        /// <summary>날던 중이면 중단으로 끝낸다(치우기 전).</summary>
        public void Abort() { if (_phase != Phase.Idle) Finish(true); }

        void Finish(bool aborted)
        {
            var done = _done;
            _done = null;
            _phase = Phase.Idle;
            done?.Invoke(this, aborted);
        }

        // 날던 중 꺼지거나 지워지면(테마 교체·런 종료) 도착으로 끝낸다. HUD 게이지가 기다리지 않게.
        void OnDisable() { if (_phase != Phase.Idle) Finish(true); }
        void OnDestroy() { if (_phase != Phase.Idle) Finish(true); }
    }
}
