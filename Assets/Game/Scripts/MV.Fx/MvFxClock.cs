using UnityEngine;

namespace MV.Fx
{
    /// <summary>
    /// 이펙트 시간(06 §6.10). 게임 시간 하나(Unity <c>Time.deltaTime</c>, 배속·일시정지 = <c>Time.timeScale</c>, MV-CLOCK-3)를 따른다.
    /// 1배속 기준 프레임 = min(dtCap, deltaTime ÷ 배속). <see cref="RealDt"/> = 그 프레임 × 배속(팝업·비행·우다다, 「실시간」도 배속 적용 — 결정 「전부 배속 적용」),
    /// <see cref="SimDt"/> = <see cref="RealDt"/> × (히트스톱이면 time.hitStopScale). 히트스톱 값은 MV.Run 상태를 읽기만 한다(06 §6.5).
    /// </summary>
    public static class MvFxClock
    {
        public static IMvFxWorld World;
        /// <summary>`300_runConfig` time.dtCap·time.hitStopScale(<see cref="MvFxPlayer"/>가 표에서 채운다). 표를 읽기 전에는 자르지도 늦추지도 않는다.</summary>
        public static double DtCap = double.PositiveInfinity;
        public static double HitStopScale = 1;

        /// <summary>시험이 시간을 직접 넣을 때 쓴다(음수 = Unity 시간).</summary>
        public static float ForcedRealDt = -1f;

        /// <summary>배속(런 <c>Setup.TimeScale</c> = <c>Time.timeScale</c>). 런 밖이면 1.</summary>
        static int Speed => World == null ? 1 : System.Math.Max(1, World.TimeScale);

        /// <summary>이 프레임 게임 시간(배속 포함, 1배속 기준 프레임은 dtCap으로 자름). 시험은 <see cref="ForcedRealDt"/>(1배속 기준 프레임)를 넣는다.</summary>
        public static float RealDt
        {
            get
            {
                int speed = Speed;
                float step = ForcedRealDt >= 0 ? ForcedRealDt : Mathf.Min((float)DtCap, Time.deltaTime / speed);
                return step * speed;
            }
        }

        /// <summary>히트스톱 배율(배속은 <see cref="RealDt"/>에 이미 들어 있다).</summary>
        public static float SimScale
        {
            get
            {
                var w = World;
                if (w == null) return 1f;
                return (float)(w.HitStopActive ? HitStopScale : 1);
            }
        }

        public static float SimDt => RealDt * SimScale;
    }
}
