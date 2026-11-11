using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using MV.Data;

namespace MV.UI
{
    /// <summary>
    /// 퀘스트 화면 코드(05 §5.16 메타 공통 틀, 08 §8.5.1). 탭 = 410 분류(일일·월간·업적), 본문 = 묶음 머리 + 칸(413 행 / 업적 지금 단계).
    /// 칸 행동 「받기」는 받을 수 있을 때만 켜진다. 아래 큰 버튼 「모두 받기」는 받을 칸이 있을 때만.
    /// 문구 키는 주석(ui.quest.*), 1차 더미 겉은 한국어 기본 문구.
    /// </summary>
    public sealed class QuestPresenter : MetaPanelPresenter
    {
        public const string ClaimPrefix = "claim:";
        public const string ClaimAllId = "claimAll";

        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        IQuestUiPort _quest;
        QuestCategory? _tab;
        float _tick;

        public IQuestUiPort Quest => _quest ?? MvQuestUiPort.Instance;
        public QuestCategory? SelectedTab => _tab;

        /// <summary>null이면 <see cref="MvQuestUiPort.Instance"/>.</summary>
        public void SetQuestPort(IQuestUiPort port) => _quest = port;

        public static string TabId(QuestCategory c) => "tab:" + ((int)c).ToString(Inv);
        public static string ClaimId(int id) => ClaimPrefix + id.ToString(Inv);

        protected override void OnOpen() => _tab = null;

        protected override void Build(MetaPanelModel m, MvTables tables)
        {
            var open = tables.ContentOpen.ByKey(MV.Content.MvQuestRules.QuestUnlockKey);
            m.Title = open != null ? NameOf(open.id, "퀘스트") : "퀘스트"; // name:<05_contentOpen id>
            m.Currencies = "캣닢 " + MvUiFormat.Fmt(Port.Wealth(MV.Save.MvWealthIds.Dia));

            var groups = Quest.Groups();
            var categories = groups.Select(g => g.Category).Distinct().OrderBy(c => (int)c).ToList();
            if (_tab == null || !categories.Contains(_tab.Value)) _tab = categories.Count > 0 ? categories[0] : (QuestCategory?)null;
            foreach (var c in categories)
                m.Tabs.Add(new MetaTab
                {
                    Id = TabId(c), Text = TabName(c), Selected = c == _tab,
                    Sub = groups.Any(g => g.Category == c && g.Open && g.Items.Any(i => i.State == QuestItemState.Claimable)) ? "●" : "",
                    Locked = groups.Where(g => g.Category == c).All(g => !g.Open),
                });
            if (_tab == null) return;

            long now = Quest.NowMs;
            long reset = groups.Where(g => g.Category == _tab && g.ResetAtMs > 0).Select(g => g.ResetAtMs).DefaultIfEmpty(0).Min();
            if (reset > 0) m.Sub = "초기화까지 " + Hms(reset - now); // ui.meta.resetIn

            bool any = false;
            foreach (var g in groups.Where(g => g.Category == _tab))
            {
                m.Add(new MetaRow("g:" + g.RowId.ToString(Inv), MetaRowKind.Header, NameOf(g.RowId, g.Name)) { Locked = !g.Open });
                foreach (var q in g.Items)
                {
                    bool can = g.Open && q.State == QuestItemState.Claimable;
                    any |= can;
                    var row = m.Add(new MetaRow("q:" + q.Id.ToString(Inv), MetaRowKind.Row, ItemTitle(q))
                    {
                        Value = ProgressText(q),
                        Desc = RewardText(q.Reward),
                        Highlight = can,
                        Locked = !g.Open,
                    });
                    row.Act(ClaimId(q.Id), q.State == QuestItemState.Done ? "완료" : "받기", can); // ui.meta.done / ui.quest.claim
                }
            }
            if (any) m.Confirm = new MetaAction(ClaimAllId, "모두 받기", true); // ui.meta.claimAll
        }

        protected override bool OnTab(string tabId)
        {
            foreach (QuestCategory c in new[] { QuestCategory.Daily, QuestCategory.Monthly, QuestCategory.Achievement, QuestCategory.Pass })
                if (TabId(c) == tabId)
                {
                    if (_tab == c) return false;
                    _tab = c;
                    Play(MetaSound.Click);
                    return true;
                }
            return false;
        }

        protected override bool OnAction(string actionId)
        {
            if (actionId == ClaimAllId) return ClaimAll() > 0;
            if (actionId == null || !actionId.StartsWith(ClaimPrefix)) return false;
            if (!int.TryParse(actionId.Substring(ClaimPrefix.Length), NumberStyles.Integer, Inv, out int id)) return false;
            var r = Quest.Claim(id);
            if (!r.Ok) { Say(r.Code); return false; }
            Play(MetaSound.Click);
            return true;
        }

        /// <summary>지금 탭의 받을 칸을 차례로 받는다. 일일 퀘스트를 받아 일일 종합이 열리면 그것도 받는다(최대 묶음 수만큼 다시 본다).</summary>
        int ClaimAll()
        {
            int done = 0;
            int passes = Quest.Groups().Count(); // 최대 묶음 수(410 묶음 수, 옛 상수 8)
            for (int pass = 0; pass < passes; pass++)
            {
                var ids = Quest.Groups().Where(g => g.Category == _tab && g.Open)
                    .SelectMany(g => g.Items).Where(i => i.State == QuestItemState.Claimable).Select(i => i.Id).ToList();
                if (ids.Count == 0) break;
                int before = done;
                foreach (int id in ids)
                {
                    var r = Quest.Claim(id);
                    if (r.Ok) done++;
                    else Say(r.Code);
                }
                if (done == before) break;
            }
            if (done > 0) Play(MetaSound.Click);
            return done;
        }

        public override void Tick(float unscaledDt)
        {
            _tick += unscaledDt;
            if (_tick < 1f) return;
            _tick = 0;
            Draw(); // 초기화 남은 시간
        }

        // ───────── 표기 ─────────

        static string TabName(QuestCategory c)
        {
            switch (c)
            {
                case QuestCategory.Daily: return "일일";    // ui.quest.tabDaily
                case QuestCategory.Monthly: return "월간";  // ui.quest.tabMonthly
                case QuestCategory.Achievement: return "업적"; // ui.quest.tabAchieve
                default: return "패스";
            }
        }

        /// <summary>표 행 이름 <c>name:{id}</c>, 없으면 카운터 이름 + 목표.</summary>
        static string ItemTitle(QuestItem q)
        {
            string fallback = ClearName(q.ClearType) + " " + MvUiFormat.Fmt(q.Target);
            if (q.StepCount > 0) fallback += " (" + q.Step.ToString(Inv) + "/" + q.StepCount.ToString(Inv) + ")";
            return NameOf(q.Id, fallback);
        }

        public static string ProgressText(QuestItem q)
        {
            long shown = q.Count < q.Target ? q.Count : q.Target;
            return MvUiFormat.Fmt(shown) + "/" + MvUiFormat.Fmt(q.Target);
        }

        public static string RewardText(IReadOnlyList<ConsumeData> reward)
        {
            if (reward == null || reward.Count == 0) return "";
            var sb = new StringBuilder();
            foreach (var r in reward)
            {
                if (sb.Length > 0) sb.Append("  ");
                string name = r.Type == ConsumeType.Wealth ? MvUiText.Name(r.Id, WealthFallback(r.Id))
                    : r.Type == ConsumeType.DailyQuestPoint ? "일일 포인트" // name:consumeType 300
                    : r.Type.ToString();
                sb.Append(name).Append(" +").Append(MvUiFormat.Fmt(r.Value));
            }
            return sb.ToString();
        }

        static string WealthFallback(int id)
        {
            switch (id)
            {
                case MV.Save.MvWealthIds.Gold: return "츄르";
                case MV.Save.MvWealthIds.Dia: return "캣닢";
                case MV.Save.MvWealthIds.Souls: return "생선";
                case MV.Save.MvWealthIds.Medal: return "메달";
                default: return id.ToString(Inv);
            }
        }

        static string ClearName(QuestClearType t)
        {
            switch (t)
            {
                case QuestClearType.RunComplete: return "달리기 완료";
                case QuestClearType.StairClimb: return "계단 오르기";
                case QuestClearType.WallBreak: return "벽 부수기";
                case QuestClearType.GoldEarn: return "츄르 모으기";
                case QuestClearType.SoulEarn: return "생선 모으기";
                case QuestClearType.WeaponCraft: return "무기 만들기";
                case QuestClearType.AreaUpgrade: return "장난감 바구니";
                case QuestClearType.LobbyUpgrade: return "로비 강화";
                case QuestClearType.AreaOpen: return "Area 열기";
                case QuestClearType.ModeClear: return "모드 클리어";
                case QuestClearType.AdWatch: return "광고 보기";
                case QuestClearType.Rebirth: return "부활";
                case QuestClearType.DailyPoint: return "일일 포인트";
                default: return t.ToString();
            }
        }
    }
}
