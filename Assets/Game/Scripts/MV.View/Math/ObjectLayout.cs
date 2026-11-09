using System;
using System.Collections.Generic;
using MV.Run;

namespace MV.View
{
    /// <summary>우물 벽돌 하나(우물 앵커 기준 로컬 m, 게임 z).</summary>
    public struct WellBrick
    {
        public double X, Y, Z;
        public int Layer;
        /// <summary>스프라이트 변형 번호(벽돌 0..3, 잡동사니 0..4).</summary>
        public int Variant;
        public bool Junk;
    }

    /// <summary>
    /// 오브젝트 뷰 배치 규칙(03 §3.5.3, 정본 `drawJunk` L3608, `drawWell` L3644, 우물 생성 L1338~1347). 순수 계산.
    /// </summary>
    public static class ObjectLayout
    {
        /// <summary>
        /// 골목 장애물 변형 v = (lane + row·2 + bar) % 3: 0 택배 상자, 1 봉투(idx 홀수 어두운 색), 2 우유 상자(idx 홀수 빨강).
        /// 반환값은 <c>105_objectKind.sprite</c> 목록(Parcel/Bag_Dark/Bag_Blue/Crate_Red/Crate_Blue) 안 번호.
        /// </summary>
        public static int JunkSpriteIndex(int lane, int row, int bar, int idx)
        {
            int v = (lane + row * 2 + bar) % 3;
            bool odd = idx % 2 != 0;
            if (v == 0) return 0;
            if (v == 1) return odd ? 1 : 2;
            return odd ? 3 : 4;
        }

        /// <summary>장애물 그림 흔들기: hs = idx·7.13 + bar·3.1, x 편차 (hash(hs) − 0.5)·0.35, 크기 0.9 + hash(hs + 3)·0.18. 앵커 (x+jx, y+0.05, z−0.45).</summary>
        public static void JunkJitter(int idx, int bar, out double jx, out double scale)
        {
            double hs = Math.Max(0, idx) * 7.13 + Math.Max(0, bar) * 3.1;
            jx = (FieldGen.Hash(hs) - 0.5) * 0.35;
            scale = 0.9 + FieldGen.Hash(hs + 3) * 0.18;
        }

        public const double JunkFrontZ = -0.45, JunkLiftY = 0.05;
        /// <summary>보상 배지: 장애물 그림 기준 (0.7, 0.25) 크기 단위(q). dia는 캣닢 잎, 그 외 츄르.</summary>
        public const double BadgeX = 0.7, BadgeY = 0.25;

        public static string BadgeSprite(FieldReward rew) => rew == FieldReward.Dia ? "World_Item_Badge_Dia" : "World_Item_Badge_Gold";

        public const int BrickLayers = 4, BricksPerLayer = 11, WellJunkCount = 9;
        public const double BrickRadius = 1.0, BrickY0 = 0.17, BrickDy = 0.32;

        /// <summary>
        /// 우물 벽돌 44개(4층 × 11) + 위 잡동사니 9개. 벽돌 각 a = (i + (ly%2)·0.5)/11·2π, (cos a, 0.17 + ly·0.32, −sin a)(three z를 게임 z로 뒤집음),
        /// 색 (i + ly) % 4. 잡동사니는 정본이 `rnd(−0.5, 0.5)`로 흩뿌린다 — 결정적 hash로 바꿔 매 런 같은 모양(y = 1.4 + (i%3)·0.18).
        /// </summary>
        public static void WellBricks(List<WellBrick> into)
        {
            if (into == null) throw new ArgumentNullException(nameof(into));
            into.Clear();
            for (int ly = 0; ly < BrickLayers; ly++)
                for (int i = 0; i < BricksPerLayer; i++)
                {
                    double a = (i + (ly % 2) * 0.5) / BricksPerLayer * Math.PI * 2;
                    into.Add(new WellBrick
                    {
                        X = Math.Cos(a) * BrickRadius, Y = BrickY0 + ly * BrickDy, Z = -Math.Sin(a) * BrickRadius,
                        Layer = ly, Variant = (i + ly) % 4,
                    });
                }
            for (int i = 0; i < WellJunkCount; i++)
                into.Add(new WellBrick
                {
                    // 정본 rnd(−.5,.5) 대체: 결정적 hash(시각 차이만, 판정 무관).
                    X = FieldGen.Hash(i * 1.7 + 0.3) - 0.5, Y = 1.4 + (i % 3) * 0.18, Z = FieldGen.Hash(i * 2.9 + 0.7) - 0.5,
                    Layer = 5, Variant = i % 5, Junk = true,
                });
        }

        /// <summary>우물 벽돌 그림 크기 0.54×0.3 m(정본 RR), 외곽선 0.03 m.</summary>
        public const double BrickW = 0.54, BrickH = 0.3;
    }
}
