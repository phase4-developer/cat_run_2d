using System.Collections.Generic;
using MV.Data;
using NUnit.Framework;

namespace MV.Meta.Tests
{
    /// <summary>해금 판정 한 곳(<see cref="MvUnlockRules"/>, 07 §7.8.9): 조건 종류, 여러 조건(모두 성립), 잠금 표시·사용 가능 단계, sticky.</summary>
    public sealed class MvUnlockRulesTests
    {
        const string Header = "id\tname\tkey\tactive\tshowConditionType\tshowConditionValue\tconditionType\tconditionValue\tsticky";

        static ContentOpenData Row(string showType, string showValue, string type, string value, bool sticky = false, bool active = true)
        {
            string text = Header + "\n5001999\t시험\tfeature\t" + (active ? "1" : "0") + "\t" + showType + "\t" + showValue + "\t" + type + "\t" + value
                          + "\t" + (sticky ? "1" : "0");
            return new ContentOpenData(TSVRow.FromTsv(text)[0]);
        }

        static MvUnlockProgress P(int area = 0, int runs = 0, int level = 1) => new MvUnlockProgress(area, runs, level);

        static readonly string[] None = new string[0];

        [TestCase(UnlockConditionType.Always, 0, 0, 0, 1, true)]
        [TestCase(UnlockConditionType.Area, 3, 1, 0, 1, false)] // areaNo 2 < 3
        [TestCase(UnlockConditionType.Area, 3, 2, 0, 1, true)]  // areaNo 3
        [TestCase(UnlockConditionType.Runs, 2, 0, 1, 1, false)]
        [TestCase(UnlockConditionType.Runs, 2, 0, 2, 1, true)]
        [TestCase(UnlockConditionType.Level, 15, 0, 0, 14, false)]
        [TestCase(UnlockConditionType.Level, 15, 0, 0, 15, true)]
        public void Met_EachConditionType(UnlockConditionType type, int value, int area, int runs, int level, bool expected)
            => Assert.That(MvUnlockRules.Met(new UnlockCondition(type, value), P(area, runs, level)), Is.EqualTo(expected));

        [TestCase(1, 0, 4, MvUnlockState.Locked)] // 셋 모두 부족
        [TestCase(2, 2, 4, MvUnlockState.Locked)] // Level만 부족
        [TestCase(2, 0, 9, MvUnlockState.Locked)] // 런만 부족
        [TestCase(2, 2, 5, MvUnlockState.Open)]   // 셋 모두
        public void State_MultipleOpenConditions_AllMustHold(int area, int runs, int level, MvUnlockState expected)
        {
            var row = Row("", "", "1/2/3", "3/2/5");
            Assert.That(row.openConditions, Has.Count.EqualTo(3));
            Assert.That(MvUnlockRules.State(row, P(area, runs, level), None), Is.EqualTo(expected));
        }

        [TestCase(0, 20, MvUnlockState.Hidden)] // 잠금 표시 조건(Area 2) 전이면 Level이 넘어도 숨김
        [TestCase(1, 14, MvUnlockState.Locked)]
        [TestCase(1, 15, MvUnlockState.Open)]
        public void State_ShowStageThenOpenStage(int area, int level, MvUnlockState expected)
            => Assert.That(MvUnlockRules.State(Row("1", "2", "3", "15"), P(area, 0, level), None), Is.EqualTo(expected));

        [Test]
        public void State_EmptyShowConditions_IsVisibleLocked_InactiveIsHidden()
        {
            Assert.That(MvUnlockRules.State(Row("", "", "1", "3"), P(), None), Is.EqualTo(MvUnlockState.Locked));
            Assert.That(MvUnlockRules.State(Row("", "", "0", "0", active: false), P(), None), Is.EqualTo(MvUnlockState.Hidden));
            Assert.That(MvUnlockRules.State(null, P(), None), Is.EqualTo(MvUnlockState.Hidden));
        }

        [Test]
        public void State_StickyOpened_StaysOpenAfterProgressReset()
        {
            var row = Row("1", "2", "3", "15", sticky: true);
            var opened = new List<string> { "feature" };
            Assert.That(MvUnlockRules.State(row, P(0, 0, 15), opened), Is.EqualTo(MvUnlockState.Open));
            Assert.That(MvUnlockRules.State(Row("1", "2", "3", "15"), P(0, 0, 15), opened), Is.EqualTo(MvUnlockState.Hidden), "sticky 0은 기록을 보지 않는다");
        }

        [Test]
        public void Raise_ReachesEveryCondition_AreaCapped()
        {
            var row = Row("1", "9", "2/3", "4/15");
            MvUnlockProgress after = MvUnlockRules.Raise(row, P(1, 6, 3), lastArea: 4);
            Assert.That(after.Area, Is.EqualTo(4), "Area 9가 필요해도 마지막 Area(0기반 4)까지");
            Assert.That(after.Runs, Is.EqualTo(6), "이미 넘은 값은 내리지 않는다");
            Assert.That(after.Level, Is.EqualTo(15));
            Assert.That(MvUnlockRules.State(Row("1", "5", "2/3", "4/15"), MvUnlockRules.Raise(Row("1", "5", "2/3", "4/15"), P(), 4), None),
                Is.EqualTo(MvUnlockState.Open));
        }

        [TestCase("1", "2", "3", "15", 2)] // 잠금 표시 Area 먼저
        [TestCase("", "", "1", "4", 4)]
        [TestCase("", "", "2", "1", 0)]
        public void MapArea_FirstAreaCondition(string showType, string showValue, string type, string value, int expected)
            => Assert.That(MvUnlockRules.MapArea(Row(showType, showValue, type, value)), Is.EqualTo(expected));
    }
}
