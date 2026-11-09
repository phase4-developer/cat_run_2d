using UnityEngine;

namespace MV.Fx
{
    /// <summary>
    /// 이펙트 시간(06 §6.10). 월드 이펙트는 simDt = min(dtCap, realDt) × (히트스톱이면 time.hitStopScale) × 배속,
    /// 팝업·비행·우다다 실시간 연출은 RealDt. 히트스톱 값은 MV.Run 상태를 읽기만 한다(06 §6.5).
    /// </summary>
    public static class MvFxClock
    {
        public static IMvFxWorld World;
        public static double DtCap = 0.05;
        public static double HitStopScale = 0.12;

        /// <summary>시험이 시간을 직접 넣을 때 쓴다(음수 = Unity 시간).</summary>
        public static float ForcedRealDt = -1f;

        public static float RealDt => ForcedRealDt >= 0 ? ForcedRealDt : Mathf.Min((float)DtCap, Time.unscaledDeltaTime);

        public static float SimScale
        {
            get
            {
                var w = World;
                if (w == null) return 1f;
                double k = (w.HitStopActive ? HitStopScale : 1) * System.Math.Max(1, w.TimeScale);
                return (float)k;
            }
        }

        public static float SimDt => RealDt * SimScale;
    }
}
