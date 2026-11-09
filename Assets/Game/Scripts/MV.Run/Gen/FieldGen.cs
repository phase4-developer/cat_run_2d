using System;
using System.Collections.Generic;
using MV.Data;

namespace MV.Run
{
    /// <summary>
    /// 골목 장애물 필드 칸 생성(정본 `hash`·`fieldSpec`, 04 §4.14.3, 07 §7.4.3·§7.6.4).
    /// 칸 위치는 <see cref="CellMode.Hash"/>면 런타임 해시(D-27), <see cref="CellMode.Baked"/>면 303 행의 굳힘 값(D-61).
    /// </summary>
    public static class FieldGen
    {
        /// <summary>
        /// 정본 `hash(n)`: frac(sin(n·127.1 + 311.7)·43758.5453). double 필수(float Mathf.Sin 금지).
        /// 곱·합을 나눠 써서 FMA 융합을 막는다. 상수는 표에 두지 않는다(바꾸면 모든 배치가 바뀜).
        /// sin은 <see cref="JsMath.Sin"/>(정본 V8과 비트 동일). 04 §4.14.3의 `Math.Sin`은 Windows에서 마지막 비트가 달라 바꿨다(D-27).
        /// </summary>
        public static double Hash(double n)
        {
            double a = n * 127.1;
            double b = a + 311.7;
            double x = JsMath.Sin(b) * 43758.5453;
            return x - Math.Floor(x);
        }

        /// <summary>JS `Math.round`(04 §4.0.2).</summary>
        public static double JsRound(double x) => Math.Floor(x + 0.5);

        /// <summary>fieldSpec 입력 1묶음(표에서 모은 값). 시험이 골든 파일의 `final` 값을 직접 넣을 수 있게 분리했다.</summary>
        public struct Input
        {
            public double H0, H1;
            public int Rows, Souls;
            /// <summary>시드에 넣는 A(0기반, 클램프 전 = 302.fieldSeedArea).</summary>
            public int SeedArea;
            /// <summary>칸 floor 0기반(시드의 f).</summary>
            public int Floor;
            /// <summary>츄르 값의 zoneOf(A).</summary>
            public int ZoneIndex;
            /// <summary>HP에 곱하는 배율(114 field.useScale이면 06.scale, 아니면 1).</summary>
            public double Scale;
            /// <summary>캣닢 칸 값(diaCap()).</summary>
            public double DiaCap;
            public CellMode Mode;
            public IReadOnlyList<int> SoulCells, SoulAmts, DiaCells;
        }

        /// <summary>해시 호출 기록(시험용). null이면 기록하지 않는다.</summary>
        public sealed class HashLog
        {
            public readonly List<(double x, double h)> Soul = new List<(double, double)>();
            public readonly List<(double x, double h)> Dia = new List<(double, double)>();
        }

        /// <summary>정본 `fieldSpec`. idx 오름차순 칸 목록.</summary>
        public static List<FieldCell> Build(FieldGenData g, in Input p, HashLog log = null)
        {
            int lanes = g.lanes;
            int n = p.Rows * lanes;
            var cells = new List<FieldCell>(n);
            int sN = Math.Max(1, (int)Math.Ceiling(p.Rows / (double)g.soulPerRows));
            double seed = (p.SeedArea + 1) * g.seedA + p.Floor * g.seedF + g.seedC;

            List<int> soulIdx;
            List<int> soulAmt = null;
            HashSet<int> diaSet = null;
            if (p.Mode == CellMode.Baked)
            {
                soulIdx = new List<int>(p.SoulCells ?? Array.Empty<int>());
                soulAmt = new List<int>(p.SoulAmts ?? Array.Empty<int>());
                diaSet = new HashSet<int>(p.DiaCells ?? Array.Empty<int>());
            }
            else
            {
                soulIdx = new List<int>(sN);
                for (int k = 0; soulIdx.Count < sN && k < g.soulTryMax; k++)
                {
                    double x = seed + k * g.soulHashStep;
                    double h = Hash(x);
                    log?.Soul.Add((x, h));
                    int i = g.soulMinIdx + (int)Math.Floor(h * (n - g.soulMinIdx));
                    if (!soulIdx.Contains(i)) soulIdx.Add(i);
                }
                soulIdx.Sort();
            }

            int baseAmt = (int)Math.Floor(p.Souls / (double)sN);
            int extra = p.Souls - baseAmt * sN;
            double span = Math.Max(1, p.Rows - 1);
            for (int idx = 0; idx < n; idx++)
            {
                int r = idx / lanes, l = idx % lanes;
                double hp = JsRound((p.H0 + (p.H1 - p.H0) * r / span) * p.Scale);
                var rew = FieldReward.Gold;
                double amt = JsRound((g.goldBase + g.goldPerZone * p.ZoneIndex) * (1 + g.goldPerRow * r));
                int si = soulIdx.IndexOf(idx);
                if (si >= 0)
                {
                    rew = FieldReward.Soul;
                    amt = soulAmt != null ? (si < soulAmt.Count ? soulAmt[si] : 0) : baseAmt + (si >= sN - extra ? 1 : 0);
                }
                else if (p.Mode == CellMode.Baked)
                {
                    if (diaSet.Contains(idx)) { rew = FieldReward.Dia; amt = p.DiaCap; }
                }
                else
                {
                    double x = seed + idx * g.diaHashStep + g.diaHashAdd;
                    double h = Hash(x);
                    log?.Dia.Add((x, h));
                    if (h < g.diaChance) { rew = FieldReward.Dia; amt = p.DiaCap; }
                }
                cells.Add(new FieldCell(idx, r, l, hp, rew, (int)amt));
            }
            return cells;
        }

        /// <summary>표에서 fieldSpec(A, f) 입력을 모은다. A·f는 0기반.</summary>
        public static Input InputFor(MvTables t, FieldGenData g, BalancePresetData bal, double diaCap, int area, int floor)
        {
            var areaRow = LevelGen.AreaRow(t, area);
            AreaFloorData row = null;
            foreach (var fr in t.AreaFloor.GetGroup(areaRow.floorGroup))
                if (fr.floor == floor + 1) { row = fr; break; }
            if (row == null) throw new InvalidOperationException($"303_areaFloor: group {areaRow.floorGroup} floor {floor + 1} 행이 없습니다");
            var wallRule = LevelGen.HpRuleOf(t, t.ObjectKind.Get(g.wallKindId));
            int zi = t.ZoneIndexOfArea(area + 1);
            return new Input
            {
                H0 = row.h0, H1 = row.h1, Rows = row.rows, Souls = row.souls,
                SeedArea = areaRow.fieldSeedArea, Floor = floor, ZoneIndex = zi < 0 ? 0 : zi,
                Scale = wallRule != null && wallRule.useScale ? bal.scale : 1,
                DiaCap = diaCap, Mode = row.cellMode,
                SoulCells = row.soulCells, SoulAmts = row.soulAmts, DiaCells = row.diaCells,
            };
        }

        /// <summary>보상 종류 → consumeType(313 열).</summary>
        public static ConsumeType RewardTypeOf(FieldGenData g, FieldReward rew) =>
            (ConsumeType)(rew == FieldReward.Soul ? g.soulRewardType : rew == FieldReward.Dia ? g.diaRewardType : g.goldRewardType);
    }
}
