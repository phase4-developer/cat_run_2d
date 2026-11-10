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
            m.Title = "고양이 공부방"; // name:5001009
            m.Sub = "계열마다 단계를 올려 런 능력치를 키워요 · 한 번에 하나씩"; // ui.research.sub
            m.Currencies = "캣닢 " + MvUiFormat.Fmt(Port.Wealth(MvWealthIds.Dia));

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
                    Text = NameOf(HeadId(g), "계열 " + g),
                    Sub = st == MvResearchStatus.Ready ? "받기!" : st == MvResearchStatus.Running ? "연구 중" : "Lv " + glv + "/" + MvResearchRules.MaxLevel(tables, g), // ui.research.tabSub
                    Selected = g == _group,
                });
            }

            bool open = Port.IsOpen(MvResearchRules.UnlockKey);
            if (!open)
            {
                var unlock = tables.ContentOpen.ByKey(MvResearchRules.UnlockKey);
                m.Add(new MetaRow(LockedId, MetaRowKind.Text, unlock != null && unlock.conditionType == 1
                    ? "Area " + unlock.conditionValue + "에서 열려요" : "아직 열리지 않았어요")); // toastMessage:contentLocked
            }

            int group = _group;
            int level = r.ResearchLevel(group);
            var steps = MvResearchRules.Steps(tables, group);
            var reached = MvResearchRules.Reached(tables, group, level);
            var status = MvResearchRules.Status(tables, group, level, run, now);
            MetaStat stat = steps.Count > 0 ? steps[0].statType : MetaStat.Dmg;
            m.Add(new MetaRow(SumId, MetaRowKind.Header, NameOf(HeadId(group), "계열 " + group))
            {
                Icon = steps.Count > 0 ? steps[0].icon : "",
                Value = StatName(stat) + " +" + Pct(reached?.statValue ?? 0) + " · Lv " + level + "/" + MvResearchRules.MaxLevel(tables, group), // ui.research.sum
            });

            foreach (var row in steps)
            {
                string id = row.level.ToString(Inv);
                var cell = m.Add(new MetaRow(StepPrefix + id, MetaRowKind.Row, "Lv " + row.level) // ui.research.lv
                {
                    Icon = row.icon,
                    Value = StatName(row.statType) + " +" + Pct(row.statValue),
                    Desc = PriceText(row.price, tables) + " · " + Hms(row.seconds * 1000L), // ui.research.cost
                });
                if (row.level <= level)
                {
                    cell.Act(DonePrefix + id, "완료", false); // ui.meta.done
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
                        cell.Act(StartPrefix + gid, "올리기 · " + PriceText(row.price, tables), open && r.CanAfford(row.price)); // ui.research.start
                        break;
                    case MvResearchStatus.Running:
                        cell.Act(WaitPrefix + gid, Hms(MvResearchRules.LeftMs(run.FinishAtMs, now)), false);
                        break;
                    case MvResearchStatus.Ready:
                        cell.Act(ClaimPrefix + gid, "받기", true); // ui.research.claim
                        break;
                    case MvResearchStatus.Busy:
                        cell.Act(BusyPrefix + gid, "다른 연구 중", false); // ui.research.busy
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
                case MetaStat.Dmg: return "공격력";
                case MetaStat.FireRate: return "공격 속도";
                case MetaStat.Range: return "사거리";
                case MetaStat.CritDmg: return "치명타 피해";
                case MetaStat.Gold: return "츄르 획득";
                case MetaStat.CritChance: return "치명타 확률";
                case MetaStat.DoubleChance: return "두 번 공격";
                default: return s.ToString();
            }
        }

        static string Pct(double v) => (Math.Abs(v - Math.Round(v)) < 1e-9 ? Math.Round(v).ToString("0", Inv) : v.ToString("0.##", Inv)) + "%";

        public static string FailText(string code)
        {
            switch (code)
            {
                case MvMetaErrors.NotEnough: return "캣닢이 부족해요";              // toastMessage:notEnough
                case MvMetaErrors.Busy: return "다른 연구가 진행 중이에요";          // toastMessage:researchBusy
                case MvMetaErrors.NotDone: return "아직 연구 중이에요";              // toastMessage:researchNotDone
                case MvMetaErrors.Locked: return "아직 열리지 않았어요";
                case MvMetaErrors.Max: return "최고 단계예요";
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
