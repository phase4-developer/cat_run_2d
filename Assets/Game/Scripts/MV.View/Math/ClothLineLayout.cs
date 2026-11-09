using System;
using MV.Run;

namespace MV.View
{
    public enum ClothKind : byte { Shirt, Towel, Socks }

    /// <summary>빨래 한 장(정본 `drawClothes` L3549~3553).</summary>
    public struct ClothItem
    {
        /// <summary>줄 점 번호(1, 3, 5, 7, 9).</summary>
        public int Point;
        public int ColorIndex;
        public ClothKind Kind;
    }

    /// <summary>
    /// 골목을 가로지르는 빨랫줄·전구줄(02 §2.5.4, 03 §3.5.2, 정본 `drawClothes` L3543~3554). 순수 계산.
    /// 줄은 x −4.75..4.75를 0.95 간격 11점, 높이 `groundY + clothY − sag`, sag = 0.4·(1 − (x/4.75)²).
    /// </summary>
    public static class ClothLineLayout
    {
        public const int PointCount = 11;
        public const double HalfSpan = 4.75, PointStep = 0.95, Sag = 0.4;
        /// <summary>줄 굵기 m(정본 `max(1, q0·0.035)` px).</summary>
        public const double LineWidth = 0.035;
        /// <summary>전구: 줄 점에서 화면 아래로 0.13 m가 가운데, 반지름 0.09×0.13, 빛 반지름 0.7 m.</summary>
        public const double BulbDrop = 0.13, BulbLight = 0.7;
        /// <summary>전구 깜빡임 주기 ms(정본 `now/350`).</summary>
        public const double BulbPeriodMs = 350;
        /// <summary>빨래 흔들림: x += sin(t·1.8 + i)·0.04 m.</summary>
        public const double SwayFreq = 1.8, SwayAmp = 0.04;
        public static readonly string[] BulbColors = { "#ff6a5a", "#ffd84a", "#6ad0ff", "#8af06a" };
        public static readonly string[] ClothColors = { "#f2a8b8", "#8ac0e8", "#f6e08a", "#ffffff", "#a8d8a0", "#c8a8e8", "#f0a070" };

        /// <summary>전구줄 테마(정본 `deco === 'snow' || 'roof'`). 아니면 빨래.</summary>
        public static bool UsesBulbs(string deco) => deco == "snow" || deco == "roof";

        public static double PointX(int i) => -HalfSpan + i * PointStep;

        /// <summary>점 i의 처짐(m). 줄 높이 = groundY + clothY − 이 값.</summary>
        public static double PointSag(int i)
        {
            double x = PointX(i) / HalfSpan;
            return Sag * (1 - x * x);
        }

        /// <summary>전구 i(1..9) 켜짐: ((i + floor(nowMs/350)) % 3) != 0.</summary>
        public static bool BulbOn(int i, double nowMs) => (i + (long)Math.Floor(nowMs / BulbPeriodMs)) % 3 != 0;

        public static string BulbColor(int i) => BulbColors[i % 4];

        /// <summary>전구 스프라이트 이름(`World_Line_Bulb_{0..3}_On/_Off`). 꺼진 그림은 `shadeC(col, −0.5)`로 구운 것.</summary>
        public static string BulbSprite(int i, bool on) => "World_Line_Bulb_" + (i % 4) + (on ? "_On" : "_Off");

        /// <summary>점 i(홀수)의 빨래. hash(z·0.71 + i·1.3) &lt; 0.3이면 없음(false).</summary>
        public static bool TryCloth(double z, int i, out ClothItem item)
        {
            double hh = FieldGen.Hash(z * 0.71 + i * 1.3);
            item = default;
            if (hh < 0.3) return false;
            item = new ClothItem
            {
                Point = i,
                ColorIndex = (int)(hh * 97) % ClothColors.Length,   // JS `hh*97|0`
                Kind = (ClothKind)((int)(hh * 13) % 3),
            };
            return true;
        }

        public static string ClothSprite(in ClothItem c) => "World_Line_" + c.Kind + "_" + c.ColorIndex;

        public static double Sway(int i, double tSec) => Math.Sin(tSec * SwayFreq + i) * SwayAmp;
    }
}
