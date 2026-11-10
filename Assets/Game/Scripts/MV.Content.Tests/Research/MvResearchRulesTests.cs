using System;
using System.IO;
using System.Linq;
using System.Text;
using MV.Data;
using MV.Meta;
using MV.Run;
using NUnit.Framework;

namespace MV.Content.Tests
{
    /// <summary>
    /// 연구 규칙(08 §8.5.3)과 런 시작 메타 능력치(08 §8.5.0·§8.8): 연구 0 = 정본(Zero), 단계별 누적 합, 표 행 추가 → 계열·단계 추가(12 §12.6-C).
    /// </summary>
    public sealed class MvResearchRulesTests
    {
        static MvTables _base;

        static MvTables Base()
        {
            if (_base != null) return _base;
            var t = new MvTables();
            t.LoadAll(new MvTsvDirectorySource(MvTableLoader.TsvDirectory));
            return _base = t;
        }

        /// <summary><c>440_research</c>에 행 1개를 더한 표(파일은 바꾸지 않는다).</summary>
        static MvTables WithRow(string row)
        {
            string dir = MvTableLoader.TsvDirectory;
            string text = File.ReadAllText(Path.Combine(dir, ResearchData.Table + ".txt"), new UTF8Encoding(false)).Replace("\r\n", "\n").TrimEnd('\n');
            var t = new MvTables();
            t.LoadAll(new MvMemoryTableSource(new MvTsvDirectorySource(dir)).Set(ResearchData.Table, text + "\n" + row + "\n"));
            return t;
        }

        [Test]
        public void NoResearch_ModifiersZero_SameAsCanon()
        {
            var t = Base();
            Assert.That(MvResearchRules.Totals(t, _ => 0), Is.Empty);
            Assert.That(MvResearch.Modifiers(t, _ => 0), Is.EqualTo(RunModifiers.Zero));
            Assert.That(MvResearch.Modifiers((MV.Save.MvSaveStore)null, t), Is.EqualTo(RunModifiers.Zero), "저장 없음 = 정본");
        }

        /// <summary>5계열 같은 단계일 때 합(08 §8.5.3 표: Dmg·FireRate·Range 2/단계, CritDmg·Gold 5/단계 누적).</summary>
        [TestCase(1)]
        [TestCase(3)]
        [TestCase(5)]
        [TestCase(9)] // 저장 단계가 표보다 높으면 마지막 행 값
        public void Modifiers_SumReachedStepPerGroup(int level)
        {
            int lv = Math.Min(level, 5);
            RunModifiers m = MvResearch.Modifiers(Base(), _ => level);
            Assert.That(m.Dmg, Is.EqualTo(2 * lv));
            Assert.That(m.FireRate, Is.EqualTo(2 * lv));
            Assert.That(m.Range, Is.EqualTo(2 * lv));
            Assert.That(m.CritDmg, Is.EqualTo(5 * lv));
            Assert.That(m.Gold, Is.EqualTo(5 * lv));
            Assert.That(m.CritChance, Is.EqualTo(0));
            Assert.That(m.DoubleChance, Is.EqualTo(0));
            Assert.That(m.Validate(), Is.Null);
        }

        [Test]
        public void Modifiers_OnlyResearchedGroupsCount()
        {
            RunModifiers m = MvResearch.Modifiers(Base(), g => g == 4 ? 2 : 0);
            Assert.That(m.CritDmg, Is.EqualTo(10));
            Assert.That(m.Dmg + m.FireRate + m.Range + m.Gold, Is.EqualTo(0));
        }

        /// <summary>시험용 추가 행(<c>440_research</c> 열 순서).</summary>
        static string ExtraRow(string kind)
        {
            switch (kind)
            {
                case "level": return "440001006	1	6		Dmg	15	1	1001002	60	6000	1";
                case "group": return "440006001	6	1		CritChance	3	1	1001002	10	50	1";
                case "inactive": return "440001006	1	6		Dmg	15	1	1001002	60	6000	0";
                default: throw new ArgumentException(kind);
            }
        }

        /// <summary>표 확장(12 §12.6-C): 단계 행 1개 → 그 계열 단계 +1, 새 group 행 → 계열 +1, active 0 행은 늘지 않는다.</summary>
        [TestCase("level", 1, 6, 5, 15)]
        [TestCase("group", 6, 1, 6, 3)]
        [TestCase("inactive", 1, 5, 5, 10)]
        public void TableRowAdded_GrowsGroupsOrSteps(string kind, int group, int maxLevel, int groups, int top)
        {
            var t = WithRow(ExtraRow(kind));
            Assert.That(MvResearchRules.Groups(Base()).Count, Is.EqualTo(5), "기준 5계열");
            Assert.That(MvResearchRules.MaxLevel(Base(), 1), Is.EqualTo(5), "기준 5단계");
            Assert.That(MvResearchRules.Groups(t).Count, Is.EqualTo(groups));
            Assert.That(MvResearchRules.MaxLevel(t, group), Is.EqualTo(maxLevel));
            Assert.That(MvResearchRules.Steps(t, group).Select(r => r.level), Is.EqualTo(Enumerable.Range(1, maxLevel)));
            Assert.That(MvResearchRules.Reached(t, group, 99).statValue, Is.EqualTo(top), "최고 단계 누적값");
        }

        /// <summary>계열 상태: 진행 중 계열은 끝 시각 경계로 Running/Ready, 다른 계열은 Busy, 끝 단계는 Max.</summary>
        [TestCase(1, 0, 1, -1, "Running")]
        [TestCase(1, 0, 1, 0, "Ready")]
        [TestCase(2, 0, 1, 0, "Busy")]
        [TestCase(2, 0, 0, 0, "Idle")]
        [TestCase(2, 5, 0, 0, "Max")]
        [TestCase(2, 5, 1, 0, "Max")]
        public void Status_Boundaries(int group, int level, int runGroup, int nowMinusFinishMs, string expected)
        {
            const long finish = 1_700_000_050_000L;
            var run = runGroup == 0 ? MvResearchRun.None : new MvResearchRun(runGroup, finish);
            var status = MvResearchRules.Status(Base(), group, level, run, finish + nowMinusFinishMs);
            Assert.That(status.ToString(), Is.EqualTo(expected));
        }
    }
}
