using System;
using System.Collections.Generic;
using MV.Run;

namespace MV.View
{
    /// <summary>테마 소품 종류(정본 `prop(kind)`: fence / bush / moon(집) / tree(먼 집) / sign).</summary>
    public enum PropKind : byte { Fence, Roadside, House, FarHouse, Pole }

    /// <summary>소품 하나의 배치(게임 좌표). 정렬·안개 탐침은 생성 위치(<see cref="GenX"/>, y, <see cref="Z"/>)다(정본 draw2D L3785).</summary>
    public struct PropSpec
    {
        public PropKind Kind;
        /// <summary>−1 왼쪽, +1 오른쪽.</summary>
        public int Side;
        /// <summary>생성 x(정본 `prop(kind, x, …)`). hh·정렬 탐침에 쓴다.</summary>
        public double GenX;
        public double Z;
        /// <summary>그릴 때 x(정본이 고정값으로 덮어씀: 담장 ±4.78, 길가 ±4.22+…, 전봇대 ±4.5, 집은 생성 x).</summary>
        public double DrawX;
        /// <summary>`hash(wp.z·0.37 + wp.x)` — 색·장식·크기 선택.</summary>
        public double Hh;
        /// <summary>생성 때 쓴 난수(r1~r4 중 그 종류의 값).</summary>
        public double R;
    }

    /// <summary>집 한 채의 치수·부품(정본 `drawMoonProp` 집 분기 L3591~3600, 로컬 m, 앵커 = 앞면 아래 가운데, y 위가 +).</summary>
    public struct HouseSpec
    {
        public double W, H, D;
        public int WallIndex, RoofIndex;
        /// <summary>문 가운데 x(로컬). 문 0.9×1.25, 아래가 0.</summary>
        public double DoorX;
        /// <summary>창 2개(fx 0.2, 0.8): 문과 겹치면 Skip. 창 0.7×0.6, 가운데 y = h·0.75 − 0.3.</summary>
        public bool Win0, Win1;
        public double Win0X, Win1X, WinY;
        public bool Win0Lit, Win1Lit;
        public RoofItem RoofKind;
        /// <summary>옥상 소품 앵커(게임 좌표, 상자 앵커와 별도).</summary>
        public double RoofX, RoofY, RoofZ;
    }

    public enum RoofItem : byte { None, SnowCap, Tank, Jars }

    /// <summary>
    /// 정본 `buildEnv` 소품 배치(L1274~1284, 02 §2.5.2)와 달동네 그리기 선택 규칙(L3556~3601). 순수 계산.
    /// 배치 순서 = 정본 `envG.add` 순서(같은 깊이일 때 그리는 순서).
    /// </summary>
    public static class PropLayout
    {
        public const double Spacing = 3.2;
        public const double FenceX = 4.78, PoleX = 4.5, RoadsideX = 4.22;
        public const double DoorW = 0.9, DoorH = 1.25, WinW = 0.7, WinH = 0.6;

        /// <summary>
        /// `buildEnv(len, from, cliff)` 소품 줄. z는 정본처럼 3.2를 계속 더해 만든다(곱으로 바꾸면 hash 입력 비트가 달라진다).
        /// 달동네(`prop = moon`) 전용: 담장 조건 `th.prop !== 'pine'` 참, 길가는 bush, 먼 집은 tree, `th.house`·`th.home` 없음.
        /// </summary>
        public static void Build(double from, double len, bool cliff, List<PropSpec> into)
        {
            if (into == null) throw new ArgumentNullException(nameof(into));
            double end = from + len + 140;
            for (double z = cliff ? from + 1.6 : Math.Floor((from - 30) / Spacing) * Spacing; z < end; z += Spacing)
            {
                for (int k = 0; k < 2; k++)
                {
                    int sd = k == 0 ? -1 : 1;
                    double r1 = FieldGen.Hash(z * 1.31 + sd * 7.7), r2 = FieldGen.Hash(z * 2.17 + sd * 3.1);
                    double r3 = FieldGen.Hash(z * 0.73 + sd * 9.9), r4 = FieldGen.Hash(z * 3.7 + sd * 1.3);
                    if (r4 > 0.55) into.Add(Make(PropKind.Fence, sd, sd * 5.1, z, r4));
                    else if (r1 > 0.4) into.Add(Make(PropKind.Roadside, sd, sd * (5.3 + r1 * 2.5), z, r1));
                    if (r2 > 0.3) into.Add(Make(PropKind.House, sd, sd * (9.5 + r2 * 10), z + 1.5, r2));
                    if (r3 > 0.5) into.Add(Make(PropKind.FarHouse, sd, sd * (21 + r3 * 20), z + 0.7, r3));
                    if (r1 < 0.05) into.Add(Make(PropKind.Pole, sd, sd * 4.9, z + 0.4, r2));
                }
            }
        }

        static PropSpec Make(PropKind kind, int sd, double x, double z, double r)
        {
            double hh = FieldGen.Hash(z * 0.37 + x);
            double drawX;
            switch (kind)
            {
                case PropKind.Fence: drawX = sd * FenceX; break;
                case PropKind.Roadside: drawX = sd * RoadsideX + sd * (hh - 0.5) * 0.12; break;
                case PropKind.Pole: drawX = sd * PoleX; break;
                default: drawX = x; break;
            }
            return new PropSpec { Kind = kind, Side = sd, GenX = x, Z = z, DrawX = drawX, Hh = hh, R = r };
        }

        /// <summary>
        /// 길가 소품 스프라이트 이름(정본 kind 선택 L3572 + 상자 색·과일 L3576). deco별 hh 문턱은 정본 그대로.
        /// </summary>
        public static string RoadsideSprite(string deco, double hh)
        {
            string kind = deco == "market" ? (hh < 0.5 ? "crate" : "pot")
                : deco == "harbor" ? (hh < 0.4 ? "buoy" : hh < 0.7 ? "crate" : "jar")
                : deco == "snow" ? (hh < 0.45 ? "snowman" : "jar")
                : (hh < 0.4 ? "pot" : hh < 0.7 ? "jar" : "bag");
            switch (kind)
            {
                case "pot": return "World_Prop_Pot";
                case "jar": return "World_Prop_Jar";
                case "bag": return "World_Prop_Bag";
                case "buoy": return "World_Prop_Buoy";
                case "snowman": return "World_Prop_Snowman";
                default:
                    string col = hh < 0.25 ? "Blue" : "Red";
                    if (deco == "harbor") return "World_Prop_Crate_" + col + "_Fish";
                    return "World_Prop_Crate_" + col + ((int)(hh * 10) % 2 == 0 ? "_A" : "_B");
            }
        }

        /// <summary>집·먼 집 치수와 부품(정본 L3591~3600). <paramref name="night"/>는 `304_theme.night`가 0이 아니면 참.</summary>
        public static HouseSpec House(in PropSpec p, double groundY, int wallCount, int roofCount, bool night, string deco)
        {
            bool far = p.Kind == PropKind.FarHouse;
            double hh = p.Hh;
            var h = new HouseSpec
            {
                W = (far ? 4.2 : 4.6) + hh * 2.4,
                H = 2.2 + hh * 2.4 + (far ? 1.2 : 0),
                D = 3.6,
                WallIndex = wallCount > 0 ? (int)Math.Floor(hh * 7) % wallCount : 0,
                RoofIndex = roofCount > 0 ? (int)Math.Floor(hh * 11) % roofCount : 0,
            };
            h.DoorX = (hh - 0.5) * h.W * 0.4;
            h.WinY = h.H * 0.75 - WinH * 0.5;
            for (int i = 0; i < 2; i++)
            {
                double fx = i == 0 ? 0.2 : 0.8;
                double cx = -h.W / 2 + h.W * fx;
                bool show = !(Math.Abs(cx - h.DoorX) < 0.9);
                bool lit = night && FieldGen.Hash(p.Z * 0.3 + fx * 7 + p.GenX) > 0.35;
                if (i == 0) { h.Win0 = show; h.Win0X = cx; h.Win0Lit = show && lit; }
                else { h.Win1 = show; h.Win1X = cx; h.Win1Lit = show && lit; }
            }
            h.RoofKind = deco == "snow" ? RoofItem.SnowCap : hh > 0.62 ? RoofItem.Tank : hh > 0.25 ? RoofItem.Jars : RoofItem.None;
            h.RoofX = p.GenX + (hh - 0.5) * h.W * 0.4;
            h.RoofY = groundY + h.H;
            h.RoofZ = p.Z + h.D * 0.15;
            return h;
        }

        /// <summary>
        /// 빨랫줄 z 목록(정본 draw2D L3788): ceil((PZ+from)/step)·step부터 PZ+to 미만, hash(z·0.37) &lt; skip이면 생략.
        /// </summary>
        public static void ClothLines(double pz, ViewConfig cfg, List<double> into)
        {
            if (into == null) throw new ArgumentNullException(nameof(into));
            into.Clear();
            double step = cfg.ClothStep;
            for (double z = Math.Ceiling((pz + cfg.ClothFrom) / step) * step; z < pz + cfg.ClothTo; z += step)
            {
                if (FieldGen.Hash(z * 0.37) < cfg.ClothSkip) continue;
                into.Add(z);
            }
        }

        /// <summary>소품 그리기 범위(정본 L3785): z ∈ [PZ+propDrawBack, PZ+propDrawFront].</summary>
        public static bool InDrawRange(double z, double pz, ViewConfig cfg) => !(z < pz + cfg.PropDrawBack || z > pz + cfg.PropDrawFront);
    }
}
