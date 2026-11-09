using System;

namespace MV.View
{
    /// <summary>로비 시설 자리(정본 `fac(k, u, v, sc, st)` L3520~3524).</summary>
    public struct LobbySlot
    {
        public string Key;
        public double U, V, Scale;
        public LobbySlot(string key, double u, double v, double scale) { Key = key; U = u; V = v; Scale = scale; }
    }

    /// <summary>
    /// 로비 아지트 투영(02 §2.9, 정본 `iso` L3475~3477). 순수 계산.
    /// 화면 px(위가 0): iso(u, v) = (W/2 + (u − v)·A, H·0.548 + (u + v)·B), A = W·0.4, B = A·0.52. 시설 1 단위 s = W·0.115.
    /// Unity 단위: 기준 프레임 9×16(1 단위 = W/9), 원점 = 화면 가운데, y 위가 +.
    /// </summary>
    public static class LobbyIso
    {
        public const double FrameW = 9, FrameH = 16, CenterY = 0.548, AxisA = 0.4, AxisB = 0.52, UnitS = 0.115;
        /// <summary>LobbyCamera 직교 크기(세로 반 = 8).</summary>
        public const float OrthoSize = 8f;
        public const string WallTop = "#7a5462", WallTop2 = "#8a646c", FloorOut = "#2e2230";

        public static readonly LobbySlot[] Slots =
        {
            new LobbySlot("stove", -0.92, -0.92, 0.95),
            new LobbySlot("tower", -0.78, -0.3, 1.0),
            new LobbySlot("rack", -0.5, -0.97, 0.85),
            new LobbySlot("scratch", 0.55, 0.12, 0.9),
            new LobbySlot("basket", -0.02, 0.56, 0.85),
        };
        /// <summary>친구 방석 자리(iso(0.62, 0.62)), 고양이 자리(iso(0.08, 0.08)).</summary>
        public const double CushionUV = 0.62, CatUV = 0.08;

        /// <summary>정본 화면 px(W×H, 위가 0).</summary>
        public static void IsoPx(double u, double v, double w, double h, out double x, out double y)
        {
            double a = w * AxisA, b = a * AxisB;
            x = w / 2 + (u - v) * a;
            y = h * CenterY + (u + v) * b;
        }

        /// <summary>기준 프레임 단위(9×16, 가운데 원점, y 위 +). 화면비와 무관하다(정본 W:H = 9:16 기준).</summary>
        public static void IsoUnits(double u, double v, out double x, out double y)
        {
            IsoPx(u, v, FrameW, FrameH, out double px, out double py);
            x = px - FrameW / 2;
            y = FrameH / 2 - py;
        }

        /// <summary>시설 1 단위 크기(프레임 단위) = 9·0.115.</summary>
        public static double UnitSize => FrameW * UnitS;

        /// <summary>
        /// 정렬: 화면 y 오름차순(위 = 뒤 먼저). sortingOrder = round(−y·100)(y 위 + 단위라 위쪽이 작은 값).
        /// </summary>
        public static int SortingOrder(double yUnits) => (int)Math.Round(-yUnits * 100);

        /// <summary>터치 반동 크기 1 + 0.18·sin(b·π)·b(b는 프레임마다 0.03씩 줄어든다, 정본 L3515).</summary>
        public static double Bump(double b) => 1 + 0.18 * Math.Sin(b * Math.PI) * b;

        public const double BumpDecayPerFrame = 0.03;

        /// <summary>가랜드 전구 k 켜짐: ((k + floor(t·2.5)) % 3) != 0, t = 로비 누적 초.</summary>
        public static bool BulbOn(int k, double tSec) => (k + (long)Math.Floor(tSec * 2.5)) % 3 != 0;

        /// <summary>시설 그림을 구운 기준 크기(px/단위, 03 §3.5.6 「s = 160·sc」).</summary>
        public const double BakedUnitPx = 160;

        /// <summary>
        /// 시설 스프라이트 이름(03 §3.5.6). 단계 0은 난로만 그림이 있고 나머지는 null(잠김 표시는 UI 이름표) [추정].
        /// 애니메이션: 캣타워 2·3단계 12프레임 4.2 fps, 난로 1~3단계 8프레임 10.2 fps. <paramref name="extra"/> = 건조대 생선 수.
        /// </summary>
        public static string FacSprite(string key, int st, double tSec, int extra = 0)
        {
            switch (key)
            {
                case "tower":
                    if (st <= 0) return null;
                    return st == 1 ? "Lobby_Fac_Tower_1" : "Lobby_Fac_Tower_" + Math.Min(3, st) + "_" + Frame(tSec, 4.2, 12).ToString("00");
                case "stove":
                    if (st <= 0) return "Lobby_Fac_Stove_0";
                    return "Lobby_Fac_Stove_" + Math.Min(3, st) + "_" + Frame(tSec, 10.2, 8).ToString("00");
                case "scratch": return st <= 0 ? null : "Lobby_Fac_Scratch_" + Math.Min(3, st);
                case "basket": return st <= 0 ? null : "Lobby_Fac_Basket_" + Math.Min(3, st);
                case "rack":
                    if (st <= 0) return null;
                    int s3 = Math.Min(3, st), max = s3 * 2;   // 생선 수 1~2 / 1~4 / 1~6
                    return "Lobby_Fac_Rack_" + s3 + "_F" + Math.Max(1, Math.Min(max, extra));
                default: return null;
            }
        }

        static int Frame(double tSec, double fps, int n) => (int)(Math.Floor(Math.Max(0, tSec) * fps) % n);
    }
}
