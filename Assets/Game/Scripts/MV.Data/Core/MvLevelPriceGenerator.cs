using System;
using System.Collections.Generic;

namespace MV.Data
{
    /// <summary>
    /// 16_levelPrice 행 생성 규칙(07 §7.8.6). 비용 열(costFormula·costBase·costGrowth)은 이 생성기만 읽는다.
    /// 그룹 1~24는 <see cref="Generate"/>, 08 메타 묶음(301·302·304·306~313)은 <see cref="GenerateContent"/>.
    /// 검증기는 표와 생성 결과가 다르면 경고만 낸다(손으로 조정한 값 허용).
    /// </summary>
    public static class MvLevelPriceGenerator
    {
        /// <summary>D-80 재화 id.</summary>
        public const int Gold = 1001001, Dia = 1001002, Token = 1001004;

        /// <summary>정본 반올림(`Math.round`, 0.5 위로).</summary>
        public static long JsRound(double x) => (long)Math.Floor(x + 0.5);

        /// <summary>targetLevel 1..max의 가격.</summary>
        public static long[] Prices(CostFormula formula, double costBase, double costGrowth, int max)
        {
            var list = new long[Math.Max(0, max)];
            for (int level = 1; level <= max; level++)
                list[level - 1] = formula == CostFormula.LinearNext
                    ? JsRound(costBase + costGrowth * level)
                    : JsRound(costBase * Math.Pow(costGrowth, level - 1));
            return list;
        }

        public static long[] Prices(LobbyUpgradeData row) => Prices(row.costFormula, row.costBase, row.costGrowth, row.max);
        public static long[] Prices(AreaUpgradeData row) => Prices(CostFormula.Pow, row.costBase, row.costGrowth, row.max);
        public static long[] Prices(RebirthPerkData row) => Prices(CostFormula.Pow, row.costBase, row.costGrowth, row.max);

        public readonly struct Row
        {
            public readonly int Group, TargetLevel, WealthId;
            public readonly long Price;
            public Row(int group, int targetLevel, int wealthId, long price)
            {
                Group = group;
                TargetLevel = targetLevel;
                WealthId = wealthId;
                Price = price;
            }
        }

        /// <summary>10(골드)·13(다이아)·14(토큰) 성장 표가 쓰는 묶음 전체.</summary>
        public static List<Row> Generate(MvTables t)
        {
            var rows = new List<Row>();
            foreach (var r in t.LobbyUpgrade.Datas) Add(rows, r.levelPriceGroup, Gold, Prices(r));
            foreach (var r in t.AreaUpgrade.Datas) Add(rows, r.levelPriceGroup, Dia, Prices(r));
            foreach (var r in t.RebirthPerk.Datas) Add(rows, r.levelPriceGroup, Token, Prices(r));
            rows.Sort((a, b) => a.Group != b.Group ? a.Group.CompareTo(b.Group) : a.TargetLevel.CompareTo(b.TargetLevel));
            return rows;
        }

        static void Add(List<Row> rows, int group, int wealthId, long[] prices)
        {
            for (int i = 0; i < prices.Length; i++) rows.Add(new Row(group, i + 1, wealthId, prices[i]));
        }

        /// <summary>16 표 id 규칙: 16 + 묶음 3자리 + 단계 3자리.</summary>
        public static int RowId(int group, int targetLevel) => 16000000 + group * 1000 + targetLevel;

        // ================= 08 메타 묶음(301 펫, 302 친구, 304 신 레벨, 306~313 장비) =================

        /// <summary>08이 정한 메타 성장 묶음 번호(07 §7.8.6 그룹 배정).</summary>
        public const int PetGroup = 301, FriendGroup = 302, GodGroup = 304, GearGroupFirst = 306, GearGroupLast = 313;

        /// <summary>D-80 재화 id: 6 사료, 11 공용 간식.</summary>
        public const int PetFood = 1001006, FriendUp = 1001011;

        /// <summary>08 §8.5.2 펫 최대 Lv, §8.5.4 친구 maxLevel, §8.3.5 신 레벨 5, §8.5.8 장비 강화 상한.</summary>
        public const int PetMaxLevel = 40, FriendMaxLevel = 30, GodMaxLevel = 5, GearMaxLevel = 30;

        /// <summary>08 §8.3.5 신 레벨 targetLevel 2~5 조각 수.</summary>
        static readonly long[] s_godShard = { 5, 10, 20, 40 };

        /// <summary>08 §8.5.8 471_gearRarity 「upgrade 츄르/레벨」, 희귀도 0~7(묶음 306~313).</summary>
        static readonly long[] s_gearUpgradeGold = { 100, 200, 400, 800, 1600, 3200, 6400, 12800 };

        /// <summary>08 메타 묶음 1행. Price는 16 price 열 그대로(여러 재화 가능).</summary>
        public readonly struct ContentRow
        {
            public readonly int Group, TargetLevel;
            public readonly ConsumeData[] Price;
            /// <summary>301 펫 카드 수(08 §8.5.2 `needPiece` 열). 그 밖 묶음 0.</summary>
            public readonly int NeedPiece;

            public ContentRow(int group, int targetLevel, int needPiece, params ConsumeData[] price)
            {
                Group = group;
                TargetLevel = targetLevel;
                NeedPiece = needPiece;
                Price = price;
            }

            public int Id => RowId(Group, TargetLevel);
        }

        public static bool IsContentGroup(int group) =>
            group == PetGroup || group == FriendGroup || group == GodGroup || (group >= GearGroupFirst && group <= GearGroupLast);

        /// <summary>08 공식·표 값으로 만든 메타 묶음 전체(묶음, targetLevel 순).</summary>
        public static List<ContentRow> GenerateContent()
        {
            var rows = new List<ContentRow>();
            // 08 §8.5.2: targetLevel = 현재 Lv + 1(2~40). 카드 needPiece + 사료.
            for (int level = 2; level <= PetMaxLevel; level++)
            {
                int c = level - 1;
                rows.Add(new ContentRow(PetGroup, level, PetPiece(c), new ConsumeData(ConsumeType.Wealth, PetFood, PetFoodPrice(c))));
            }
            // 08 §8.5.4: 공용 간식 20 + 10c ; 전용 코인(621, id 0 = 이 친구) 2 + c, c = targetLevel − 1.
            for (int level = 2; level <= FriendMaxLevel; level++)
            {
                int c = level - 1;
                rows.Add(new ContentRow(FriendGroup, level, 0,
                    new ConsumeData(ConsumeType.Wealth, FriendUp, 20 + 10 * c), new ConsumeData(ConsumeType.FriendCoin, 0, 2 + c)));
            }
            // 08 §8.3.5: 622 GodShard, id 0 = 지금 올리는 신.
            for (int level = 2; level <= GodMaxLevel; level++)
                rows.Add(new ContentRow(GodGroup, level, 0, new ConsumeData(ConsumeType.GodShard, 0, s_godShard[level - 2])));
            // 08 §8.5.8: 그룹 306 + 희귀도, 값 = upgradeGold × 목표 레벨(1~30).
            for (int group = GearGroupFirst; group <= GearGroupLast; group++)
                for (int level = 1; level <= GearMaxLevel; level++)
                    rows.Add(new ContentRow(group, level, 0, new ConsumeData(ConsumeType.Wealth, Gold, s_gearUpgradeGold[group - GearGroupFirst] * level)));
            return rows;
        }

        /// <summary>08 §8.5.2 카드: 현재 Lv 1~3 → 2, 4~8 → 3, 9~12 → 4, 13~17 → 5, 18~22 → 6, 23~27 → 7, 28~31 → 8, 32~36 → 9, 37~39 → 10.</summary>
        public static int PetPiece(int currentLevel)
        {
            if (currentLevel <= 3) return 2;
            if (currentLevel <= 8) return 3;
            if (currentLevel <= 12) return 4;
            if (currentLevel <= 17) return 5;
            if (currentLevel <= 22) return 6;
            if (currentLevel <= 27) return 7;
            if (currentLevel <= 31) return 8;
            if (currentLevel <= 36) return 9;
            return 10;
        }

        /// <summary>08 §8.5.2 사료: 현재 Lv 1~25 → 10×Lv, 26~35 → 275 + 25×(Lv−26), 36 → 550, 37 → 600, 38 → 650, 39 → 750.</summary>
        public static long PetFoodPrice(int currentLevel)
        {
            if (currentLevel <= 25) return 10L * currentLevel;
            if (currentLevel <= 35) return 275 + 25L * (currentLevel - 26);
            switch (currentLevel)
            {
                case 36: return 550;
                case 37: return 600;
                case 38: return 650;
                default: return 750;
            }
        }
    }
}
