using System;
using MV.Run;

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
        // ---- 새 HTML(2026-11-13 연출 정본) 오브젝트 동작: updObjMesh L1466·drawObj L3673·drawPortal L3727·paperDie L3663 ----

        /// <summary>맞음 눌림(L1468): 106.punch로 시작해 초당 9씩 준다. 크기 (1 + 0.07k, 1 − 0.08k, 1 + 0.07k).</summary>
        public const double PunchDecay = 9, PunchGrow = 0.07, PunchSquash = 0.08;
        public static double PunchStep(double k, double dt) => Math.Max(0, k - dt * PunchDecay);

        /// <summary>마력 수정탑 빛(L3705): 알파 0.35 + 0.1·sin(3t). 프리팹 Glow 기본 알파에 곱할 비율.</summary>
        public static double TowerGlowRatio(double t) => (0.35 + 0.1 * Math.Sin(t * 3)) / 0.35;
        /// <summary>수정탑 구슬 반지름 맥동(L3707): 0.85 + 0.15·sin(6t + i).</summary>
        public static double OrbPulse(double t, int i) => 0.85 + 0.15 * Math.Sin(t * 6 + i);

        /// <summary>게이트 마법 막(L3687): 아래 알파 0.32, 번쩍(0.1 s) 동안 0.55. 프리팹 Curtain 기본 알파에 곱할 비율.</summary>
        public static double CurtainRatio(bool flash) => flash ? 0.55 / 0.32 : 1;
        /// <summary>게이트 막 반짝이(L3688): 6개, 높이 2.75 m를 (0.6t + i/6) 주기로 오르고 반지름 0.07·(1 − ph).</summary>
        public const int GateSparks = 6;
        public const double GatePanelH = 2.75, GateSparkR = 0.07;
        public static double GateSparkPhase(double t, int i) => Frac(t * 0.6 + i / (double)GateSparks);
        public static double GateSparkX(int i, double z, double hw) => -hw + 2 * hw * FieldGen.Hash(i * 3.7 + z);

        /// <summary>포탈 점선 고리 회전(L3734) rad/s.</summary>
        public const double PortalSpin = 1.2;
        /// <summary>포탈 반짝이(L3735): 10개, 각 i·2.3 + 0.4t, 반지름 1.6, 높이 3.2k, 크기 0.12·(1 − k), k = (0.5t + i/10) mod 1.</summary>
        public const int PortalSparks = 10;
        public const double PortalSparkRing = 1.6, PortalSparkH = 3.2, PortalSparkR = 0.12;
        public static double PortalSparkPhase(double t, int i) => Frac(t * 0.5 + i / (double)PortalSparks);
        public static double PortalSparkAngle(double t, int i) => i * 2.3 + t * 0.4;
        /// <summary>포탈 빛(L3737): 알파 0.35, 쓴 뒤 0.15. 프리팹 Glow 기본 알파에 곱할 비율.</summary>
        public static double PortalGlowRatio(bool used) => used ? 0.15 / 0.35 : 1;

        /// <summary>
        /// 결계 블록 자리 흔들기(L3677): hs = idx·7.13 + bar·3.1, x += (hash(hs) − 0.5)·0.35, z += −0.4 + (hash(hs + 1) − 0.5)·0.4.
        /// </summary>
        public static void WallJitter(int idx, int bar, out double jx, out double jz)
        {
            double hs = Math.Max(0, idx) * 7.13 + Math.Max(0, bar) * 3.1;
            jx = (FieldGen.Hash(hs) - 0.5) * 0.35;
            jz = -0.4 + (FieldGen.Hash(hs + 1) - 0.5) * 0.4;
        }
        /// <summary>생선 비석 빛(L3684): 알파 0.35·(0.8 + 0.2·sin 4t). 프리팹 Glow 기본 알파에 곱할 비율.</summary>
        public static double FishGlowRatio(double t) => 0.8 + 0.2 * Math.Sin(t * 4);

        /// <summary>종이 쓰러짐(paperDie L3663) 길이 초.</summary>
        public const double DieSec = 0.6;
        /// <summary>쓰러지는 방향: hash(z·3.3 + x) &gt; 0.5 ? 1 : −1.</summary>
        public static int DieDir(double z, double x) => FieldGen.Hash(z * 3.3 + x) > 0.5 ? 1 : -1;
        /// <summary>쓰러짐 k = t/0.6에서: 뛰어오름 m, 가로 뒤집힘, 세로 눌림, 기울기 rad, 알파.</summary>
        public static void DiePose(double k, int dir, out double hop, out double sx, out double sy, out double roll, out double alpha)
        {
            hop = Math.Sin(Math.Min(1, k * 3) * Math.PI) * 0.35;
            double fall = Math.Min(1, Math.Max(0, (k - 0.15) / 0.6));
            sx = Math.Cos(k * Math.PI * 1.5);
            sy = Math.Max(0.05, Math.Cos(fall * Math.PI / 2));
            roll = dir * fall * 0.5;
            alpha = 1 - Math.Max(0, (k - 0.6) / 0.4);
        }

        static double Frac(double v) => v - Math.Floor(v);

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
