using System;
using System.Collections.Generic;
using System.Linq;
using jjevol;
using jjevol.Data;
using NUnit.Framework;

namespace MV.Data.Tests
{
    /// <summary>801·802 추첨(기획서 07 §7.9.3, D-81·D-82·D-85).</summary>
    public sealed class MvDropRollerTests
    {
        const int EnemyCatnip = 801001001;
        const int TestBox = 802001001;
        const int Trials = 100000;
        const double Tolerance = 0.005; // ±0.5%p

        static string R(params string[] cells) => string.Join("\t", cells);

        /// <summary>n개 묶음이 50으로 이어진 사슬(깊이 n). 마지막 묶음은 츄르 1.</summary>
        internal static string Chain(int n)
        {
            const string header = "id\tdropGroup\trewardType\trewardId\trewardValue_min\trewardValue_max\trewardValueRule\tweight";
            var text = TsvFixture.Read("801_drop");
            for (int i = 0; i < n; i++)
            {
                int g = 801010001 + i * 1000;
                bool last = i == n - 1;
                text = TsvFixture.Append(text, header, last
                    ? R(g.ToString(), g.ToString(), "1", "1001001", "1", "1", "Flat", "10000")
                    : R(g.ToString(), g.ToString(), "50", (g + 1000).ToString(), "1", "1", "Flat", "10000"));
            }
            return text;
        }

        /// <summary>시험 상자: 칸 1 = 츄르 1~3(30%)·꽝, 칸 2 = 적 캣닢 묶음 1회(50%)·캣닢 10(50%).</summary>
        internal static string BoxText()
        {
            const string header = "id\tgroup\tsubGroup\trewardType\trewardId\trewardValue_min\trewardValue_max\tweight";
            var text = TsvFixture.Read("802_randomBox");
            text = TsvFixture.Append(text, header, R("802001001", "802001001", "1", "1", "1001001", "1", "3", "3000"));
            text = TsvFixture.Append(text, header, R("802001002", "802001001", "1", "0", "0", "0", "0", "7000"));
            text = TsvFixture.Append(text, header, R("802001003", "802001001", "2", "50", "801001001", "1", "1", "5000"));
            text = TsvFixture.Append(text, header, R("802001004", "802001001", "2", "1", "1001002", "10", "10", "5000"));
            return text;
        }

        static MvTables Tables(params (string table, string text)[] changes)
        {
            var source = new MvMemoryTableSource(TsvFixture.Source());
            foreach (var c in changes) source.Set(c.table, c.text);
            return TsvFixture.LoadTables(source);
        }

        [TearDown]
        public void TearDown()
        {
            TableRepository.Clear();
            GameTableSource.Configure(new PackagedTableSource(false));
            MvTables.Current = null;
        }

        [Test]
        public void EnemyCatnip_SameU_MatchesProtoThreshold()
        {
            var roller = new MvDropRoller(Tables());
            var us = Enumerable.Range(0, 2001).Select(i => i / 2000.0 * 0.999999)
                .Concat(new[] { 0.0, 0.08, 0.08 - 1e-15, 0.08 + 1e-15, 0.0799999, 0.0800001 });
            var results = new List<MvDropResult>();
            foreach (var u in us)
            {
                results.Clear();
                int calls = 0;
                roller.RollDrop(EnemyCatnip, () => { calls++; return u; }, results);
                bool proto = u < 0.08;
                Assert.That(results.Count == 1, Is.EqualTo(proto), $"u={u:R}");
                Assert.That(calls, Is.EqualTo(1), "난수 호출 수는 정본과 같은 1회");
                if (proto)
                {
                    Assert.That(results[0].Type, Is.EqualTo(ConsumeType.RunDia));
                    Assert.That(results[0].Rule, Is.EqualTo(RewardValueRule.HalfDiaCapCeil));
                    Assert.That(results[0].Value, Is.EqualTo(0));
                }
            }
        }

        [Test]
        public void EnemyCatnip_FrequencyWithinHalfPercent()
        {
            var roller = new MvDropRoller(Tables());
            var rng = new Random(20261109);
            var results = new List<MvDropResult>();
            for (int i = 0; i < Trials; i++) roller.RollDrop(EnemyCatnip, rng.NextDouble, results);
            double freq = results.Count / (double)Trials;
            var odds = roller.DropOdds(EnemyCatnip);
            Assert.That(odds.First(o => o.RowId == 801001001).Probability, Is.EqualTo(0.08).Within(1e-12));
            Assert.That(freq, Is.EqualTo(0.08).Within(Tolerance));
        }

        [Test]
        public void Box_EverySlotPaysOnce_FrequencyMatchesOdds()
        {
            var roller = new MvDropRoller(Tables(("802_randomBox", BoxText())));
            var odds = roller.BoxOdds(TestBox).Where(o => o.Type != ConsumeType.None).ToList();
            Assert.That(odds.Select(o => o.RowId), Is.EquivalentTo(new[] { 802001001, 801001001, 802001004 }));
            Assert.That(odds.First(o => o.RowId == 801001001).Probability, Is.EqualTo(0.5 * 0.08).Within(1e-12));

            var rng = new Random(7);
            var counts = new Dictionary<int, int>();
            var quantity = new int[4];
            var results = new List<MvDropResult>();
            for (int i = 0; i < Trials; i++)
            {
                results.Clear();
                roller.OpenBox(TestBox, rng.NextDouble, results);
                Assert.That(results.Count(r => r.RowId == 802001004) + results.Count(r => r.RowId == 801001001), Is.LessThanOrEqualTo(1), "칸 2는 1행만");
                foreach (var r in results)
                {
                    counts[r.RowId] = counts.TryGetValue(r.RowId, out var c) ? c + 1 : 1;
                    if (r.RowId == 802001001) quantity[r.Value]++;
                }
            }
            foreach (var o in odds)
            {
                double freq = (counts.TryGetValue(o.RowId, out var c) ? c : 0) / (double)Trials;
                Assert.That(freq, Is.EqualTo(o.Probability).Within(Tolerance), $"row {o.RowId}");
            }
            int paid = quantity.Sum();
            Assert.That(quantity[0], Is.EqualTo(0));
            for (int v = 1; v <= 3; v++) Assert.That(quantity[v] / (double)paid, Is.EqualTo(1 / 3.0).Within(0.01), $"수량 {v}");
        }

        [Test]
        public void DropToBox_GrantsBoxWithoutOpening()
        {
            const string header = "id\tdropGroup\trewardType\trewardId\trewardValue_min\trewardValue_max\trewardValueRule\tweight";
            var drop = TsvFixture.Append(TsvFixture.Read("801_drop"), header, R("801002001", "801002001", "8", "802001001", "2", "2", "Flat", "10000"));
            var roller = new MvDropRoller(Tables(("801_drop", drop), ("802_randomBox", BoxText())));
            var results = new List<MvDropResult>();
            roller.RollDrop(801002001, () => 0.5, results);
            Assert.That(results.Count, Is.EqualTo(1));
            Assert.That(results[0].Type, Is.EqualTo(ConsumeType.RandomBox));
            Assert.That(results[0].Id, Is.EqualTo(TestBox));
            Assert.That(results[0].Value, Is.EqualTo(2));
        }

        [Test]
        public void Cycle_IsLoadError()
        {
            const string header = "id\tdropGroup\trewardType\trewardId\trewardValue_min\trewardValue_max\trewardValueRule\tweight";
            var text = TsvFixture.Append(TsvFixture.Read("801_drop"), header, R("801002001", "801002001", "50", "801003001", "1", "1", "Flat", "1"));
            text = TsvFixture.Append(text, header, R("801003001", "801003001", "8", "802001001", "1", "1", "Flat", "1"));
            var box = TsvFixture.Append(BoxText(), "id\tgroup\tsubGroup\trewardType\trewardId\trewardValue_min\trewardValue_max\tweight",
                R("802001005", "802001001", "3", "50", "801002001", "1", "1", "1"));

            Assert.Throws<InvalidOperationException>(() => new MvDropRoller(Tables(("801_drop", text), ("802_randomBox", box))));

            var source = new MvMemoryTableSource(TsvFixture.Source()).Set("801_drop", text).Set("802_randomBox", box);
            var e = Assert.Throws<MvTableValidationException>(() => MvTableLoader.LoadAsync(source).GetAwaiter().GetResult());
            Assert.That(e.Report.Contains(MvIssueLevel.Error, "801_drop", "순환") || e.Report.Contains(MvIssueLevel.Error, "802_randomBox", "순환"), Is.True, e.Report.Summary());
            Assert.That(MvTables.Current, Is.Null);
        }

        [Test]
        public void DepthOverFive_IsLoadError()
        {
            Assert.Throws<InvalidOperationException>(() => new MvDropRoller(Tables(("801_drop", Chain(6)))));
            var roller = new MvDropRoller(Tables(("801_drop", Chain(5))));
            var results = new List<MvDropResult>();
            roller.RollDrop(801010001, () => 0.3, results);
            Assert.That(results.Single().Id, Is.EqualTo(1001001));
        }
    }
}
