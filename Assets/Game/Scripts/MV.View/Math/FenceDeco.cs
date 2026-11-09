namespace MV.View
{
    /// <summary>담장 장식 종류(정본 `drawMoonProp` fence 분기 L3561~3567).</summary>
    public enum FenceDecoKind : byte { None, Awning, Gate, Flower }

    /// <summary>담장 한 칸의 장식 선택 결과. 눈 캡은 장식과 별개로 겹친다.</summary>
    public struct FenceDecoSpec
    {
        public FenceDecoKind Kind;
        /// <summary>차양 줄무늬 색(짝수 칸). 홀수 칸은 <see cref="FenceDeco.AwningLight"/>.</summary>
        public string AwningColor;
        /// <summary>대문 색 = `th.door`, 없으면 `#4a7ac8`.</summary>
        public string GateColor;
        public bool SnowCap;
    }

    /// <summary>
    /// 담장 장식(02 §2.6.5, 정본 L3561~3567). 순수 계산. 모양은 담장 루트 기준 로컬 m(y 위가 +, z = 게임 z − 담장 z).
    /// 차양·대문·눈 캡은 담장 면 메쉬 쿼드, 대문 손잡이·꽃은 화면을 보는 빌보드(정본이 화면 타원으로 그림).
    /// </summary>
    public static class FenceDeco
    {
        public const double HalfLen = 1.58, Height = 1.25;
        public const int AwningCells = 6;
        public const string AwningLight = "#fff4ea", AwningRed = "#e8504a", AwningBlue = "#4a8ad8", DefaultDoor = "#4a7ac8";
        public const string KnobColor = "#ffd84a", FlowerA = "#ff9ab0", FlowerB = "#ffd84a";
        /// <summary>꽃 알파(정본 `globalAlpha *= 0.7`).</summary>
        public const double FlowerAlpha = 0.7;
        /// <summary>대문: 면 z ±0.55, 높이 1.05, 면에서 길 쪽으로 0.01 m. 손잡이 (z+0.3, 0.5) 반지름 0.05 m.</summary>
        public const double GateHalfW = 0.55, GateH = 1.05, GateInset = 0.01, KnobZ = 0.3, KnobY = 0.5, KnobR = 0.05;
        /// <summary>꽃: 면 앞 0.02, 높이 0.7. 큰 타원 0.18×0.12, 작은 타원은 화면 x로 0.25·(−sd), y로 0.08 아래, 0.14×0.1.</summary>
        public const double FlowerInset = 0.02, FlowerY = 0.7;
        /// <summary>차양: 위 가장자리 y = h + 0.1(면 위), 아래 가장자리 y = h − 0.3, 길 쪽으로 0.8 m.</summary>
        public const double AwningTop = 0.1, AwningDrop = 0.3, AwningOut = 0.8;
        /// <summary>눈 캡 두께 0.12, 외곽선 `rgba(74,52,70,.4)` 굵기 ×0.7.</summary>
        public const double SnowH = 0.12;

        /// <summary>정본 선택 순서: market이고 hh &gt; 0.3 → 차양, 아니면 hh &gt; 0.62 → 대문, 아니면 hh &lt; 0.25 → 꽃.</summary>
        public static FenceDecoSpec Pick(string deco, double hh, string door)
        {
            var s = new FenceDecoSpec { SnowCap = deco == "snow" };
            if (deco == "market" && hh > 0.3)
            {
                s.Kind = FenceDecoKind.Awning;
                s.AwningColor = hh > 0.65 ? AwningRed : AwningBlue;
            }
            else if (hh > 0.62)
            {
                s.Kind = FenceDecoKind.Gate;
                s.GateColor = string.IsNullOrEmpty(door) ? DefaultDoor : door;
            }
            else if (hh < 0.25) s.Kind = FenceDecoKind.Flower;
            return s;
        }

        /// <summary>
        /// 차양 칸 i(0..5)의 네 꼭짓점(로컬 m): p1 (xi, h+0.1, za), p2 (xi, h+0.1, zb), p3 (xi − sd·0.8, h−0.3, zb), p4 (xi − sd·0.8, h−0.3, za).
        /// <paramref name="xi"/> = 안쪽 면 x(로컬). 색은 i 홀수면 밝은 색.
        /// </summary>
        public static void AwningCell(int i, int sd, double xi, out Vector3d p1, out Vector3d p2, out Vector3d p3, out Vector3d p4)
        {
            double z0 = -HalfLen, z1 = HalfLen;
            double za = z0 + (z1 - z0) * i / AwningCells, zb = z0 + (z1 - z0) * (i + 1) / AwningCells;
            double yt = Height + AwningTop, yb = Height - AwningDrop, xo = xi - sd * AwningOut;
            p1 = new Vector3d(xi, yt, za);
            p2 = new Vector3d(xi, yt, zb);
            p3 = new Vector3d(xo, yb, zb);
            p4 = new Vector3d(xo, yb, za);
        }

        public static string AwningCellColor(in FenceDecoSpec s, int i) => i % 2 != 0 ? AwningLight : s.AwningColor;
    }
}
