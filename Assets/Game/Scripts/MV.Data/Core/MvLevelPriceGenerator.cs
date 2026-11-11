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

        /// <summary>로비 강화 행. 계단·지팡이 상한은 표에 적지 않고 <see cref="MvTables.LevelMax"/>로 계산한다.</summary>
        public static long[] Prices(MvTables t, LobbyUpgradeData row) => Prices(row.costFormula, row.costBase, row.costGrowth, t.LevelMax(row));
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
            foreach (var r in t.LobbyUpgrade.Datas) Add(rows, r.levelPriceGroup, Gold, Prices(t, r));
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

        /// <summary>D-80 재화 id: 6 사료, 11 공용 간식.</summary>
        public const int PetFood = 1001006, FriendUp = 1001011;

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

        /// <summary>
        /// 08 공식·표 값으로 만든 메타 묶음 전체(묶음, targetLevel 순). 묶음 번호는 각 표 행의 <c>levelPriceGroup</c>,
        /// 최대 Lv은 표에서 읽는다: 펫 = 16 묶음 targetLevel 최댓값(표 열 없음), 친구 = <c>450.maxLevel</c>, 신 = <c>495.level</c> 최댓값,
        /// 장비 = <c>499 gear.maxUpgrade</c>. 가격 공식(조각·사료 구간, 신 조각, 장비 츄르)은 08 문장 그대로인 생성 도구 입력이고 정본은 16 행이다(검증기는 경고만).
        /// </summary>
        public static List<ContentRow> GenerateContent(MvTables t)
        {
            if (t == null) throw new ArgumentNullException(nameof(t));
            var rows = new List<ContentRow>();
            // 08 §8.5.2: targetLevel = 현재 Lv + 1(2~최대). 카드 needPiece + 사료.
            foreach (int group in Distinct(t.Pet.Datas, x => x.levelPriceGroup))
                for (int level = 2, max = t.LevelPriceMaxLevel(group); level <= max; level++)
                {
                    int c = level - 1;
                    rows.Add(new ContentRow(group, level, PetPiece(c), new ConsumeData(ConsumeType.Wealth, PetFood, PetFoodPrice(c))));
                }
            // 08 §8.5.4: 공용 간식 20 + 10c ; 전용 코인(621, id 0 = 이 친구) 2 + c, c = targetLevel − 1.
            foreach (int group in Distinct(t.Friend.Datas, x => x.levelPriceGroup))
            {
                int max = 0;
                foreach (var f in t.Friend.Datas) if (f.levelPriceGroup == group && f.maxLevel > max) max = f.maxLevel;
                for (int level = 2; level <= max; level++)
                {
                    int c = level - 1;
                    rows.Add(new ContentRow(group, level, 0,
                        new ConsumeData(ConsumeType.Wealth, FriendUp, 20 + 10 * c), new ConsumeData(ConsumeType.FriendCoin, 0, 2 + c)));
                }
            }
            // 08 §8.3.5: 622 GodShard, id 0 = 지금 올리는 신. 495 행 level마다(1레벨은 가격 없음).
            foreach (var g in t.GodLevel.Datas)
                if (g.level >= 2 && g.level - 2 < s_godShard.Length)
                    rows.Add(new ContentRow(g.levelPriceGroup, g.level, 0, new ConsumeData(ConsumeType.GodShard, 0, s_godShard[g.level - 2])));
            // 08 §8.5.8: 희귀도별 묶음(471.levelPriceGroup), 값 = upgradeGold × 목표 레벨(1~gear.maxUpgrade).
            int gearMax = t.GearMaxLevel();
            foreach (var x in t.GearRarity.Datas)
                if (x.rarity >= 0 && x.rarity < s_gearUpgradeGold.Length)
                    for (int level = 1; level <= gearMax; level++)
                        rows.Add(new ContentRow(x.levelPriceGroup, level, 0, new ConsumeData(ConsumeType.Wealth, Gold, s_gearUpgradeGold[x.rarity] * level)));
            rows.Sort((a, b) => a.Group != b.Group ? a.Group.CompareTo(b.Group) : a.TargetLevel.CompareTo(b.TargetLevel));
            return rows;
        }

        static List<int> Distinct<T>(IEnumerable<T> rows, Func<T, int> group)
        {
            var list = new List<int>();
            foreach (var r in rows) { int g = group(r); if (!list.Contains(g)) list.Add(g); }
            return list;
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
