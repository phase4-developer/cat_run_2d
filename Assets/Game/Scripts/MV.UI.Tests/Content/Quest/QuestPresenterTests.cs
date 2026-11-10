using System.Collections.Generic;
using System.Linq;
using MV.Data;
using NUnit.Framework;

namespace MV.UI.Tests
{
    /// <summary>퀘스트 화면 코드(05 §5.16): 탭·칸 수·받기 버튼 켜짐·모두 받기. 겉(프리팹)은 NOT_RUN.</summary>
    public sealed class QuestPresenterTests
    {
        sealed class FakeQuestPort : IQuestUiPort
        {
            public bool Ready { get; set; } = true;
            public long NowMs => 0;
            public readonly List<QuestGroupItem> List = new List<QuestGroupItem>();
            public readonly List<int> Claims = new List<int>();
            public IReadOnlyList<QuestGroupItem> Groups() => List;

            public MetaResult Claim(int id)
            {
                Claims.Add(id);
                foreach (var i in List.SelectMany(g => g.Items).Where(i => i.Id == id)) i.State = QuestItemState.Done;
                return MetaResult.Success(id);
            }

            public QuestGroupItem Group(QuestCategory c, int rowId, params QuestItemState[] states)
            {
                var g = new QuestGroupItem { RowId = rowId, Category = c, Name = "g" + rowId, ResetAtMs = c == QuestCategory.Achievement ? 0 : 3_600_000 };
                for (int i = 0; i < states.Length; i++)
                    g.Items.Add(new QuestItem { Id = rowId * 10 + i, ClearType = QuestClearType.RunComplete, Count = 1, Target = 3, State = states[i] });
                List.Add(g);
                return g;
            }
        }

        static (QuestPresenter p, FakeQuestPort q, FakeMetaPort m) Make()
        {
            var m = new FakeMetaPort(MetaUiTestKit.Base());
            var q = new FakeQuestPort();
            var p = new QuestPresenter();
            p.SetPort(m);
            p.SetQuestPort(q);
            return (p, q, m);
        }

        [TestCase(1)]
        [TestCase(4)]
        public void Rows_FollowGroupsAndItems(int items)
        {
            var (p, q, _) = Make();
            q.Group(QuestCategory.Daily, 1, Enumerable.Repeat(QuestItemState.Progress, items).ToArray());
            q.Group(QuestCategory.Daily, 2, QuestItemState.Progress);
            q.Group(QuestCategory.Achievement, 3, QuestItemState.Progress);
            p.Open();
            Assert.That(p.Model.Tabs.Select(t => t.Text), Is.EqualTo(new[] { "일일", "업적" }), "탭 = 묶음 분류");
            Assert.That(p.Model.Count(MetaRowKind.Header), Is.EqualTo(2));
            Assert.That(p.Model.Count(MetaRowKind.Row), Is.EqualTo(items + 1), "칸 = 지금 탭 묶음의 항목");
            Assert.That(p.Model.Sub, Does.StartWith("초기화까지"));
            p.SelectTab(QuestPresenter.TabId(QuestCategory.Achievement));
            Assert.That(p.Model.Count(MetaRowKind.Row), Is.EqualTo(1));
            Assert.That(p.Model.Sub, Is.Empty, "업적은 초기화 없음");
        }

        [TestCase(QuestItemState.Progress, false)]
        [TestCase(QuestItemState.Claimable, true)]
        [TestCase(QuestItemState.Done, false)]
        public void ClaimButton_OnlyWhenClaimable(QuestItemState state, bool enabled)
        {
            var (p, q, _) = Make();
            q.Group(QuestCategory.Daily, 1, state);
            p.Open();
            var a = p.Model.FindAction(QuestPresenter.ClaimId(10));
            Assert.That(a.Enabled, Is.EqualTo(enabled));
            Assert.That(p.Press(a.Id), Is.EqualTo(enabled));
            Assert.That(q.Claims.Count, Is.EqualTo(enabled ? 1 : 0));
            Assert.That(p.Model.Confirm, Is.Null, "받은 뒤 받을 칸이 없으면 모두 받기 없음");
        }

        [Test]
        public void ClaimAll_ClaimsEveryClaimableInTab_AndNothingWhenMetaMissing()
        {
            var (p, q, m) = Make();
            q.Group(QuestCategory.Daily, 1, QuestItemState.Claimable, QuestItemState.Progress, QuestItemState.Claimable);
            q.Group(QuestCategory.Monthly, 2, QuestItemState.Claimable);
            p.Open();
            Assert.That(p.Press(QuestPresenter.ClaimAllId), Is.True);
            Assert.That(q.Claims, Is.EqualTo(new[] { 10, 12 }), "지금 탭(일일)만");

            q.Group(QuestCategory.Daily, 3, QuestItemState.Claimable);
            m.Ready = false;
            p.Draw();
            Assert.That(p.Press(QuestPresenter.ClaimAllId), Is.False);
            Assert.That(q.Claims.Count, Is.EqualTo(2));
        }
    }
}
