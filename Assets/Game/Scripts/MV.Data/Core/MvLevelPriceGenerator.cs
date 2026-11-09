using System;
using System.Collections.Generic;

namespace MV.Data
{
    /// <summary>
    /// 16_levelPrice 행 생성 규칙(07 §7.8.6). 비용 열(costFormula·costBase·costGrowth)은 이 생성기만 읽는다.
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
    }
}
