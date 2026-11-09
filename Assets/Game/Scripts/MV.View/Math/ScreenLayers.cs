using System;
using MV.Run;

namespace MV.View
{
    /// <summary>
    /// 화면 공간 눈(02 §2.5.5, 정본 `drawSnow(70, 1)` L3604). 기준 px(§2.7.4) 단위. 순수 계산.
    /// x = (hash(i·1.3)·W + sin(t·0.8 + i)·14) mod W, y = (hash(i·2.7)·H + t·(22 + hash(i·5.1)·34)·sp) mod H, r = 1 + hash(i·3.3)·2.2.
    /// y는 화면 위가 0(캔버스 좌표).
    /// </summary>
    public static class SnowField
    {
        public const string Sprite = "Fx_Dot";
        /// <summary>`rgba(255,255,255,.88)`.</summary>
        public const float Alpha = 0.88f;

        public static void Particle(int i, double tSec, double w, double h, double sp, out double x, out double y, out double r)
        {
            x = Mod(FieldGen.Hash(i * 1.3) * w + Math.Sin(tSec * 0.8 + i) * 14, w);
            y = Mod(FieldGen.Hash(i * 2.7) * h + tSec * (22 + FieldGen.Hash(i * 5.1) * 34) * sp, h);
            r = 1 + FieldGen.Hash(i * 3.3) * 2.2;
        }

        /// <summary>JS `((a % m) + m) % m`(음수도 0..m).</summary>
        static double Mod(double a, double m) => ((a % m) + m) % m;
    }

    /// <summary>우다다 입구 상자 하나(`drawBox(x, y, z, w, h, d, col, true, topCol)`, 오브젝트 앵커 기준 로컬 m).</summary>
    public struct GateBox
    {
        public double X, Y, W, H, D;
    }

    /// <summary>
    /// 골목 관문 캣타워(우다다 입구, 정본 `drawWarpGate` L3211~3225). 순수 계산.
    /// 기둥마다 받침(1.9×0.3×1.7)·중간 발판(y 2.1, 1.7×0.32×1.5), 위 발판(0, 4.6, 11×0.55×1.5). 색 P1 몸·P2 윗면.
    /// </summary>
    public static class WarpGateLayout
    {
        public const double PostX = 4.6, PostHalfW = 0.42, PostH = 4.6;
        public const string P1 = "#9a7ab8", P2 = "#c0a0e0", P1Gold = "#f2c14e", P2Gold = "#ffe08a";
        public const string PostColor = "#dcc49a", PostLine = "#b89a6a";
        /// <summary>매달린 장난감 3개(dx, 색). 줄 y 4.6 → 3.3, 흔들림 sin(t·2 + dx)·0.3, 반지름 0.26.</summary>
        public static readonly double[] ToyDx = { -2.4, 2.0, -0.2 };
        public static readonly string[] ToyColor = { "#ff8fb0", "#7ad0f0", "#ffd84a" };

        /// <summary>그리는 순서대로 5개: 왼쪽 받침·왼쪽 중간, 오른쪽 받침·오른쪽 중간, 위 발판.</summary>
        public static readonly GateBox[] Boxes =
        {
            new GateBox { X = -PostX, Y = 0, W = 1.9, H = 0.3, D = 1.7 },
            new GateBox { X = -PostX, Y = 2.1, W = 1.7, H = 0.32, D = 1.5 },
            new GateBox { X = PostX, Y = 0, W = 1.9, H = 0.3, D = 1.7 },
            new GateBox { X = PostX, Y = 2.1, W = 1.7, H = 0.32, D = 1.5 },
            new GateBox { X = 0, Y = PostH, W = 11, H = 0.55, D = 1.5 },
        };

        /// <summary>
        /// 알파: 플레이어가 관문 z보다 3 m 넘게 지나면(pz = 플레이어 z − 관문 z &gt; 3) 숨김(false),
        /// pz &gt; −0.6이면 max(0, 0.35 − (pz + 0.6)·0.1), 그 밖은 1.
        /// </summary>
        public static bool Alpha(double pz, out double alpha)
        {
            alpha = 1;
            if (pz > 3) { alpha = 0; return false; }
            if (pz > -0.6) alpha = Math.Max(0, 0.35 - (pz + 0.6) * 0.1);
            return true;
        }

        public static double ToySway(int i, double tSec) => Math.Sin(tSec * 2 + ToyDx[i]) * 0.3;
    }
}
