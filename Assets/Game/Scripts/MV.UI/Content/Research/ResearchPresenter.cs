using System;
using System.Globalization;
using MV.Data;
using MV.Meta;
using MV.Save;

namespace MV.UI
{
    /// <summary>
    /// 연구 화면 코드(<c>UIPopup_Research</c>, 08 §8.5.3, 05 §5.16 공통 틀). 탭 = 계열(표 active 행의 group), 본문 = 그 계열 단계 목록.
    /// 다음 단계 칸에만 행동 버튼: 올리기(비용) / 진행 중(남은 시간, 꺼짐) / 받기 / 다른 연구 중(꺼짐). 받은 단계는 「완료」.
    /// 진행 변경은 <see cref="IResearchUiPort"/>(MV.Meta 요청)로만 한다. 행 추가 → 탭·단계 칸 추가(12 §12.6-C).
    /// </summary>
    public sealed class ResearchPresenter : MetaPanelPresenter
    {
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public const string TabPrefix = "g:";
        public const string StepPrefix = "lv:";
        public const string StartPrefix = "start:";
        public const string ClaimPrefix = "claim:";
        public const string WaitPrefix = "wait:";
        public const string BusyPrefix = "busy:";
        public const string DonePrefix = "done:";
        public const string SumId = "sum";
        public const string LockedId = "locked";

        IResearchUiPort _research;
        int _group;
        float _tick;

        public IResearchUiPort Research => _research ?? Port as IResearchUiPort ?? MvResearchUiPort.Instance;
        /// <summary>null이면 메타 입구가 함께 구현했으면 그것, 아니면 <see cref="MvResearchUiPort.Instance"/>.</summary>
        public void SetResearchPort(IResearchUiPort port) => _research = port;

        /// <summary>보고 있는 계열(0 = 아직 없음).</summary>
        public int Group => _group;

        protected override void OnOpen()
        {
            _group = 0;
            _tick = 0;
        }

        protected override bool OnTab(string tabId)
        {
            if (tabId == null || !tabId.StartsWith(TabPrefix, StringComparison.Ordinal)) return false;
            if (!int.TryParse(tabId.Substring(TabPrefix.Length), NumberStyles.Integer, Inv, out int g) || g == _group) return false;
            _group = g;
            return true;
        }

        protected override void Build(MetaPanelModel m, MvTables tables)
        {
            m.Title = MvUiText.Ui("ui.research.title");
            m.Sub = MvUiText.Ui("ui.research.sub");
            m.Currencies = MvUiText.WealthNameById(tables, MvWealthIds.Dia) + " " + MvUiFormat.Fmt(Port.Wealth(MvWealthIds.Dia));

            var groups = MvResearchRules.Groups(tables);
            if (groups.Count == 0) return;
            if (!Contains(groups, _group)) _group = groups[0];

            var r = Research;
            long now = r.NowMs;
            MvResearchRun run = r.ResearchRun;
            foreach (int g in groups)
            {
                int glv = r.ResearchLevel(g);
                var st = MvResearchRules.Status(tables, g, glv, run, now);
                m.Tabs.Add(new MetaTab
                {
                    Id = TabPrefix + g.ToString(Inv),
                    Text = NameOf(HeadId(g), MvUiText.Ui("ui.research.group", g)),
                    Sub = st == MvResearchStatus.Ready ? MvUiText.Ui("ui.research.tabReady") : st == MvResearchStatus.Running ? MvUiText.Ui("ui.research.tabRunning")
                        : MvUiText.Ui("ui.research.lvOf", glv, MvResearchRules.MaxLevel(tables, g)),
                    Selected = g == _group,
                });
            }

            bool open = Port.IsOpen(MvResearchRules.UnlockKey);
            if (!open)
            {
                var unlock = tables.ContentOpen.ByKey(MvResearchRules.UnlockKey);
                string need = MvUnlockText.Need(unlock, Port.UnlockState(MvResearchRules.UnlockKey));
                m.Add(new MetaRow(LockedId, MetaRowKind.Text, need.Length > 0
                    ? MvUiText.Ui("ui.content.lockAt", need) : MvUiText.Toast("toastMessage.locked")));
            }

            int group = _group;
            int level = r.ResearchLevel(group);
            var steps = MvResearchRules.Steps(tables, group);
            var reached = MvResearchRules.Reached(tables, group, level);
            var status = MvResearchRules.Status(tables, group, level, run, now);
            MetaStat stat = steps.Count > 0 ? steps[0].statType : MetaStat.Dmg;
            m.Add(new MetaRow(SumId, MetaRowKind.Header, NameOf(HeadId(group), MvUiText.Ui("ui.research.group", group)))
            {
                Icon = steps.Count > 0 ? steps[0].icon : "",
                Value = MvUiText.Ui("ui.research.sum", StatName(stat), Pct(reached?.statValue ?? 0), level, MvResearchRules.MaxLevel(tables, group)),
            });

            foreach (var row in steps)
            {
                string id = row.level.ToString(Inv);
                var cell = m.Add(new MetaRow(StepPrefix + id, MetaRowKind.Row, MvUiText.Ui("ui.research.lv", row.level))
                {
                    Icon = row.icon,
                    Value = StatName(row.statType) + " +" + Pct(row.statValue),
                    Desc = PriceText(row.price, tables) + " · " + Hms(row.seconds * 1000L), // ui.research.cost
                });
                if (row.level <= level)
                {
                    cell.Act(DonePrefix + id, MvUiText.Ui("ui.meta.done"), false);
                    continue;
                }
                bool isNext = row.level == MvResearchRules.Next(tables, group, level)?.level;
                if (!isNext)
                {
                    cell.Locked = true;
                    continue;
                }
                cell.Highlight = true;
                string gid = group.ToString(Inv);
                switch (status)
                {
                    case MvResearchStatus.Idle:
                        cell.Act(StartPrefix + gid, MvUiText.Ui("ui.research.start", PriceText(row.price, tables)), open && r.CanAfford(row.price));
                        break;
                    case MvResearchStatus.Running:
                        cell.Act(WaitPrefix + gid, Hms(MvResearchRules.LeftMs(run.FinishAtMs, now)), false);
                        break;
                    case MvResearchStatus.Ready:
                        cell.Act(ClaimPrefix + gid, MvUiText.Ui("ui.research.claim"), true);
                        break;
                    case MvResearchStatus.Busy:
                        cell.Act(BusyPrefix + gid, MvUiText.Ui("ui.research.busy"), false);
                        break;
                }
            }
        }

        protected override bool OnAction(string actionId)
        {
            bool start = actionId.StartsWith(StartPrefix, StringComparison.Ordinal);
            bool claim = actionId.StartsWith(ClaimPrefix, StringComparison.Ordinal);
            if (!start && !claim) return false;
            string rest = actionId.Substring(start ? StartPrefix.Length : ClaimPrefix.Length);
            if (!int.TryParse(rest, NumberStyles.Integer, Inv, out int group)) return false;
            MetaResult res = start ? Research.StartResearch(group) : Research.ClaimResearch(group);
            if (!res.Ok)
            {
                Say(FailText(res.Code));
                return false;
            }
            Play(start ? MetaSound.Click : MetaSound.PerkBuy);
            return true;
        }

        /// <summary>진행 중이면 1초마다 남은 시간을 다시 그린다.</summary>
        public override void Tick(float unscaledDt)
        {
            if (!Research.ResearchRun.Exists) { _tick = 0; return; }
            _tick += unscaledDt;
            if (_tick < 1f) return;
            _tick = 0;
            Draw();
        }

        // ───────── 문구 ─────────

        /// <summary>계열 이름 키 <c>440{group 3}000</c>(08 §8.5.3 계열 머리 행).</summary>
        public static int HeadId(int group) => 440000000 + group * 1000;

        public static string StatName(MetaStat s)
        {
            switch (s)
            {
                case MetaStat.Dmg: return MvUiText.Ui("ui.stat.dmg");
                case MetaStat.FireRate: return MvUiText.Ui("ui.stat.fireRate");
                case MetaStat.Range: return MvUiText.Ui("ui.stat.range");
                case MetaStat.CritDmg: return MvUiText.Ui("ui.stat.critDmg");
                case MetaStat.Gold: return MvUiText.Ui("ui.stat.gold");
                case MetaStat.CritChance: return MvUiText.Ui("ui.stat.critChance");
                case MetaStat.DoubleChance: return MvUiText.Ui("ui.stat.doubleChance");
                default: return s.ToString();
            }
        }

        static string Pct(double v) => (Math.Abs(v - Math.Round(v)) < 1e-9 ? Math.Round(v).ToString("0", Inv) : v.ToString("0.##", Inv)) + "%";

        public static string FailText(string code)
        {
            switch (code)
            {
                case MvMetaErrors.NotEnough: return MvUiText.Toast("toastMessage.researchNotEnough");
                case MvMetaErrors.Busy: return MvUiText.Toast("toastMessage.researchBusy");
                case MvMetaErrors.NotDone: return MvUiText.Toast("toastMessage.researchNotDone");
                case MvMetaErrors.Locked: return MvUiText.Toast("toastMessage.locked");
                case MvMetaErrors.Max: return MvUiText.Toast("toastMessage.researchMax");
                default: return code;
            }
        }

        static bool Contains(System.Collections.Generic.IReadOnlyList<int> list, int v)
        {
            foreach (int x in list) if (x == v) return true;
            return false;
        }
    }
}
