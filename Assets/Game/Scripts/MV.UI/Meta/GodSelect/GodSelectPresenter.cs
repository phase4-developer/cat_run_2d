using MV.Data;

namespace MV.UI
{
    /// <summary>
    /// 야옹신 화면 코드(05 §5.7). 탭 = <c>113_god</c>(sort) 행 전부, 본문 = 보고 있는 신 정보 + 고유 카드(<c>110_card</c> group = <c>cardGroup</c>).
    /// 「사용」은 <see cref="IMetaUiPort.SelectGod"/> 1번 → 성공이면 닫힘. 잠긴 탭은 토스트만.
    /// </summary>
    public sealed class GodSelectPresenter : MetaPanelPresenter
    {
        public const string UseId = "use";

        /// <summary>보고 있는 신 key.</summary>
        public string Viewing { get; private set; }

        public override bool DimCloses => true;

        protected override void OnOpen() => Viewing = Port.SelectedGod; // 열기: 보고 있는 신 = 사용 중 신(L2510)

        protected override void Build(MetaPanelModel m, MvTables tables)
        {
            m.Title = "달의 야옹신"; // ui.god.title
            var gods = tables.God.Sorted;
            if (gods.Count == 0) return;
            GodData view = tables.God.ByKey(Viewing);
            if (view == null) { view = gods[0]; Viewing = view.key; }
            string selected = Port.SelectedGod;

            foreach (var g in gods)
            {
                bool open = Port.GodUnlocked(g.key);
                m.Tabs.Add(new MetaTab
                {
                    Id = g.key,
                    Text = NameOf(g.id, g.key),
                    Sub = g.key == selected ? "사용 중" : open ? "해금" : "잠김", // ui.god.inUse / ui.god.open / ui.god.locked
                    Selected = g.key == view.key,
                    Locked = !open,
                });
            }

            m.Add(new MetaRow("info", MetaRowKind.Row, NameOf(view.id, view.key))
            {
                Icon = view.portrait,
                Value = "Level 1 · " + DescOf(view.id), // ui.god.level(고정) + description:<113_god id>
                Desc = "", // ui.god.note(정본 L669 고정 문구) 자리
            });

            int count = 0;
            foreach (var c in tables.Card.GetGroup(view.cardGroup)) if (c.active) count++;
            m.Add(new MetaRow("cards", MetaRowKind.Header, "고유 가호 (" + count + "장)")); // ui.god.cardsTitle
            foreach (var c in tables.Card.GetGroup(view.cardGroup))
            {
                if (!c.active) continue;
                m.Add(new MetaRow("card:" + c.id, MetaRowKind.Grid, NameOf(c.id, c.key))
                {
                    Icon = c.icon,
                    Value = TierName(tables, c.tier),
                    Desc = DescOf(c.id),
                });
            }

            bool inUse = view.key == selected;
            bool canUse = !inUse && Port.GodUnlocked(view.key);
            m.Confirm = new MetaAction(UseId, inUse ? "사용 중" : "이 야옹신 사용", canUse); // ui.god.inUse / ui.god.use
        }

        static string TierName(MvTables tables, int tierIndex)
        {
            foreach (var t in tables.CardTier.Datas)
                if (t.tierIndex == tierIndex) return NameOf(t.id, t.key);
            return "";
        }

        protected override bool OnTab(string tabId)
        {
            var tables = Port.Tables;
            var g = tables?.God.ByKey(tabId);
            if (g == null) return false;
            if (!Port.GodUnlocked(g.key))
            {
                Say(NameOf(g.id, g.key) + " · " + DescOf(g.id) + " — 업데이트에서 해금 예정"); // toastMessage:godLocked
                return false;
            }
            if (Viewing == g.key) return false;
            Viewing = g.key;
            return true;
        }

        protected override bool OnAction(string actionId)
        {
            if (actionId != UseId) return false;
            var r = Port.SelectGod(Viewing);
            if (!r.Ok) { Say(r.Code); return false; }
            RequestClose(); // 사용 → 패널 닫힘(L2513). 로비 다시 그림은 OnClosed.
            return true;
        }
    }

}
