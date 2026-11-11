using System.Linq;
using NUnit.Framework;

namespace MV.Data.Tests
{
    /// <summary>05_contentOpen 해금 조건 열(07 §7.8.9): 냥환생 2단계 행 값, 조건 목록 검증(개수·종류).</summary>
    public sealed class MvContentOpenTableTests
    {
        static MvValidationReport With(System.Func<string, string> edit) =>
            TsvFixture.Validate((ContentOpenData.Table, edit(TsvFixture.Read(ContentOpenData.Table))));

        /// <summary>MV-SPEC-02 01_코어게임루프 11-3 「해금 조건」: Area 2 잠금 표시 → Level 15 사용 가능.</summary>
        [Test]
        public void Rebirth_ShowAtArea2_OpenAtLevel15()
        {
            var row = TsvFixture.LoadTables().ContentOpen.ByKey("rebirth");
            Assert.That(row.showConditions.Select(c => (c.type, c.value)),
                Is.EqualTo(new[] { (UnlockConditionType.Area, 2) }));
            Assert.That(row.openConditions.Select(c => (c.type, c.value)),
                Is.EqualTo(new[] { (UnlockConditionType.Level, 15) }));
            Assert.That(row.sticky, Is.True);
        }

        [Test]
        public void Validator_ConditionCountMismatch_IsError()
        {
            var report = With(x => TsvFixture.SetByKey(x, "rebirth", "conditionValue", "15/3"));
            Assert.That(report.Contains(MvIssueLevel.Error, ContentOpenData.Table, "개수"), Is.True, report.Summary());
        }

        [Test]
        public void Validator_UnknownConditionType_IsError()
        {
            var report = With(x => TsvFixture.SetByKey(x, "rebirth", "showConditionType", "99"));
            Assert.That(report.Contains(MvIssueLevel.Error, ContentOpenData.Table, "UnlockConditionType"), Is.True, report.Summary());
        }

        [Test]
        public void Validator_TableAsIs_HasNoConditionError()
        {
            var report = With(x => x);
            Assert.That(report.Contains(MvIssueLevel.Error, ContentOpenData.Table, "조건"), Is.False, report.Summary());
        }
    }
}
