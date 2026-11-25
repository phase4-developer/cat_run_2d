using System;
using System.Collections.Generic;
using System.Linq;
using jjevol.Badge;
using MV.Content;
using MV.Data;
using MV.Meta;

namespace MV.UI
{
    public enum QuestItemState { Progress, Claimable, Done }

    /// <summary>퀘스트 화면 칸 1개(규칙 <see cref="MvQuestEntry"/>의 화면용 사본).</summary>
    public sealed class QuestItem
    {
        public int Id;
        public QuestClearType ClearType;
        public int Condition;
        public long Count;
        public long Target;
        public long PrevTarget;
        public int Claimed;
        public bool Repeat;
        public int Step;
        public int StepCount;
        public IReadOnlyList<ConsumeData> Reward = Array.Empty<ConsumeData>();
        public QuestItemState State;
    }

    /// <summary>퀘스트 묶음 1개(410 행).</summary>
    public sealed class QuestGroupItem
    {
        /// <summary>410 id(문구 <c>name:{id}</c>).</summary>
        public int RowId;
        public int Group;
        public string Name = "";
        public QuestCategory Category;
        public QuestGroupType GroupType;
        public long ResetAtMs;
        /// <summary>그 분류가 열렸는지(05_contentOpen quest·achievement).</summary>
        public bool Open = true;
        public readonly List<QuestItem> Items = new List<QuestItem>();
    }

    /// <summary>퀘스트 화면 입구. 기본은 <see cref="MvQuestUiPort"/>(<see cref="MvQuest.Current"/>). 시험은 가짜를 넣는다.</summary>
    public interface IQuestUiPort
    {
        bool Ready { get; }
        long NowMs { get; }
        IReadOnlyList<QuestGroupItem> Groups();
        MetaResult Claim(int id);
    }

    /// <summary>열린 분류의 수령 가능 상태를 Framework 배지 키로 모은다.</summary>
    internal sealed class QuestNotifications
    {
        const string RootKey = "lobby/quest";

        readonly ManualBadgeScheduler _scheduler = new ManualBadgeScheduler();
        readonly BadgeTree _tree;
        HashSet<QuestCategory> _shown = new HashSet<QuestCategory>();
        HashSet<QuestCategory> _next = new HashSet<QuestCategory>();

        public QuestNotifications() => _tree = new BadgeTree(_scheduler);

        public bool Any => _tree.Get(RootKey).On;
        public bool Has(QuestCategory category) => _tree.Get(CategoryKey(category)).On;

        public void Refresh(IReadOnlyList<QuestGroupItem> groups)
        {
            _next.Clear();
            foreach (var group in groups)
                if (group.Open && group.Items.Any(item => item.State == QuestItemState.Claimable))
                    _next.Add(group.Category);

            foreach (var category in _shown)
                if (!_next.Contains(category)) _tree.Remove(CategoryKey(category));
            foreach (var category in _next)
                if (!_shown.Contains(category)) _tree.Set(CategoryKey(category), BadgeValue.Dot);

            (_shown, _next) = (_next, _shown);
            _scheduler.Run();
        }

        static string CategoryKey(QuestCategory category) => BadgeKey.Child(RootKey, (int)category);
    }

    /// <summary><see cref="MvQuest.Current"/>를 매번 읽는 기본 입구.</summary>
    public sealed class MvQuestUiPort : IQuestUiPort
    {
        public static readonly MvQuestUiPort Instance = new MvQuestUiPort();

        static MvQuest Q => MvQuest.Current;

        public bool Ready => Q != null && Q.IsReady;
        public long NowMs => Q != null ? Q.NowMs : DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        public IReadOnlyList<QuestGroupItem> Groups()
        {
            var list = new List<QuestGroupItem>();
            if (!Ready) return list;
            foreach (var g in Q.Board())
            {
                var item = new QuestGroupItem
                {
                    RowId = g.Row.id, Group = g.Row.group, Name = g.Row.name, Category = g.Row.category, GroupType = g.Row.groupType,
                    ResetAtMs = g.ResetAtMs, Open = Q.IsOpen(g.Row.category),
                };
                foreach (var e in g.Entries)
                    item.Items.Add(new QuestItem
                    {
                        Id = e.Id, ClearType = e.ClearType, Condition = e.Condition, Count = e.Count, Target = e.Target, PrevTarget = e.PrevTarget,
                        Claimed = e.Claimed, Repeat = e.Repeat, Step = e.Step, StepCount = e.StepCount, Reward = e.Reward,
                        State = e.State == MvQuestState.Claimable ? QuestItemState.Claimable : e.State == MvQuestState.Done ? QuestItemState.Done : QuestItemState.Progress,
                    });
                list.Add(item);
            }
            return list;
        }

        public MetaResult Claim(int id)
        {
            if (!Ready) return MetaResult.Fail(MvMetaErrors.NoStore);
            MvMetaResponse r = Q.Claim(id);
            return r.Ok ? MetaResult.Success(id) : MetaResult.Fail(r.Code);
        }
    }
}
