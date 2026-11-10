using System;

namespace MV.View
{
    /// <summary>
    /// 오브젝트 뷰 반응(06 §6.13, 정본 `updObjMesh` L1464~1539·`hit` L1904~1975·`drawJunk` L3629·`drawRollGate` L3632~3642·`drawWarpGate` L3211~3225).
    /// 「켤지」는 `106_hitReaction`(flash·objShake)이 정하고, 시간·크기는 여기 정본 값이다(07 §7.5.3). 순수 계산.
    /// </summary>
    public static class ObjectReaction
    {
        /// <summary>맞음 흔들림 시간(`o.shake = 0.12`, 벽·우물), 관문 번쩍 시간(`o.flash = 0.1`).</summary>
        public const double ShakeSec = 0.12, FlashSec = 0.1;
        /// <summary>장애물 맞음 흰 섬광(L3629): 중심 = 그림 발 위 0.6q, 반지름 1.1q × 0.8q, 알파 0.35.</summary>
        public const double JunkFlashY = 0.6, JunkFlashRx = 1.1, JunkFlashRy = 0.8, JunkFlashAlpha = 0.35;
        /// <summary>관문 배경 아래쪽 알파: 평소 `55`, 번쩍 `aa`(L3634).</summary>
        public const double GateBgTop = 0x22 / 255.0, GateBgBottom = 0x55 / 255.0, GateBgBottomFlash = 0xaa / 255.0;
        /// <summary>관문 위로 흐르는 흰 줄(L3635): 5개, 속도 0.6/s, 폭 ±0.8rx, 굵기 0.03 m, 알파 0.5.</summary>
        public const int GateStripes = 5;
        public const double StripeSpeed = 0.6, StripeHalfW = 0.8, StripeThick = 0.03, StripeAlpha = 0.5;
        /// <summary>우물 벽돌 빠짐(L1868): 숨김 2.2 s 뒤 다시 자람(dt·5), 고를 때 위 10개 안에서.</summary>
        public const double BrickGoneSec = 2.2, BrickGrowRate = 5;
        public const int BrickPickTop = 10;
        /// <summary>금빛 관문 광륜(L3215): 중심 y + 2.3, 반지름 4.5 m, 색 `#ffe08a` 알파 0.55 → 0.</summary>
        public const double GlowY = 2.3, GlowR = 4.5, GlowAlpha = 0.55;
        public const string GlowColor = "#ffe08a", StarColor = "#fff3a0";
        /// <summary>금빛 관문 별(L3223): 기둥 위 (±4.6, y + 5.6), 반지름 0.5 + 0.1·sin(t·4).</summary>
        public const double StarY = 5.6;
        /// <summary>매달린 장난감 줄(L3221): 발판 y 4.6 → 공 y 3.3, 굵기 max(1, lw·0.6) → 0.03 m.</summary>
        public const double ToyTopY = 4.6, ToyBallY = 3.3, ToyStringThick = 0.03;

        /// <summary>벽 흔들림 x(`sin(shake·90)·0.05`, shake &gt; 0일 때만).</summary>
        public static double WallShakeX(double shake) => shake > 0 ? Math.Sin(shake * 90) * 0.05 : 0;

        /// <summary>우물 벽돌 더미 흔들림 x(`sin(shake·80)·0.06`).</summary>
        public static double WellShakeX(double shake) => shake > 0 ? Math.Sin(shake * 80) * 0.06 : 0;

        /// <summary>관문 가라앉음: 안쪽 y = −sink·6(L1503).</summary>
        public static double GateSinkY(double sink) => sink > 0 ? -sink * 6 : 0;

        /// <summary>sink &gt; 0.5면 관문을 숨긴다(L1503 `g.visible = false`).</summary>
        public static bool GateHidden(double sink) => sink > 0.5;

        /// <summary>동전·보석 크기 k = 0.55 + 0.45·hp/max(L1525, 죽으면 0).</summary>
        public static double PickupScale(double hp, double max, bool dead)
        {
            if (dead) return 0;
            if (!(max > 0)) return 1;
            return Math.Max(0.001, 0.55 + 0.45 * (hp / max));
        }

        /// <summary>
        /// 관문 흰 줄 i의 높이(관문 가운데 기준 m, 위 +): 정본 화면 y = c.y + ry − k·2ry, k = (t·0.6 + i/5) % 1.
        /// </summary>
        public static double StripeY(int i, double tSec, double ry)
        {
            double k = (tSec * StripeSpeed + (double)i / GateStripes) % 1;
            if (k < 0) k += 1;
            return -ry + k * ry * 2;
        }

        /// <summary>금빛 관문 별 반지름(0.5 + 0.1·sin(t·4)).</summary>
        public static double StarRadius(double tSec) => 0.5 + 0.1 * Math.Sin(tSec * 4);

        /// <summary>
        /// 우물 벽돌 한 개의 다시 자람: gone &gt; 0이면 줄이고 0 이하가 되면 grow = 0부터 시작, 그 뒤 grow += dt·5(최대 1). 반환 = 배율(숨김이면 0).
        /// </summary>
        public static double BrickStep(ref double gone, ref double grow, double dt)
        {
            if (gone > 0)
            {
                gone -= dt;
                if (gone > 0) return 0;
                grow = 0;
                return 0.01;
            }
            if (grow < 1) grow = Math.Min(1, grow + dt * BrickGrowRate);
            return Math.Max(0.01, grow);
        }
    }
}
