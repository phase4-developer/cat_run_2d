using System.Linq;
using MV.Data;
using MV.Meta;
using NUnit.Framework;

namespace MV.UI.Tests
{
    /// <summary>
    /// 메타 화면(D-92 ③): 표 행 추가 → 화면 항목 추가, 메타가 없으면 버튼이 막힘, 버튼 1번 → 메타 명령 1번(05 §5.7~§5.11·§5.14).
    /// </summary>
    public sealed class MetaPanelTests
    {
        static int Measure(MetaPanelModel m, string metric)
        {
            switch (metric)
            {
                case "tabs": return m.Tabs.Count;
                case "banners": return m.Count(MetaRowKind.Banner);
                case "rows": return m.Count(MetaRowKind.Row);
                case "balance": return m.FindRow("balance").Actions.Count;
                default: return -1;
            }
        }

        static MetaPanelModel Draw(string screen, MvTables tables)
        {
            var p = MetaUiTestKit.Make(screen);
            p.SetPort(new FakeMetaPort(tables));
            p.Open();
            return p.Model;
        }

        [TestCase("13_areaUpgrade", "area", "rows")]
        [TestCase("14_rebirthPerk", "rebirth", "rows")]
        [TestCase("113_god", "god", "tabs")]
        [TestCase("302_area", "map", "banners")]
        [TestCase("05_contentOpen", "map", "rows")]
        [TestCase("06_balancePreset", "debug", "balance")]
        public void TableRowAdded_ShowsOneMoreItem(string table, string screen, string metric)
        {
            int before = Measure(Draw(screen, MetaUiTestKit.Base()), metric);
            int after = Measure(Draw(screen, MetaUiTestKit.WithExtraRow(table)), metric);
            Assert.That(before, Is.GreaterThan(0));
            Assert.That(after, Is.EqualTo(before + 1));
        }

        [TestCase("god")]
        [TestCase("area")]
        [TestCase("rebirth")]
        [TestCase("unlock")]
        [TestCase("debug")]
        public void MetaNull_BlocksEveryButton(string screen)
        {
            var savedMeta = MvMeta.Current;
            var savedTables = MvTables.Current;
            try
            {
                MvMeta.Current = null;
                MvTables.Current = MetaUiTestKit.Base();
                var p = MetaUiTestKit.Make(screen);
                p.SetPort(MvMetaUiPort.Instance);
                p.Open();
                var actions = p.Model.AllActions().ToList();
                Assert.That(actions, Is.Not.Empty);
                Assert.That(actions.All(a => !a.Enabled), Is.True);
                foreach (var a in actions) Assert.That(p.Press(a.Id), Is.False, a.Id);
            }
            finally
            {
                MvMeta.Current = savedMeta;
                MvTables.Current = savedTables;
            }
        }

        [Test]
        public void AreaUpgrade_Buy_CallsMetaOnce()
        {
            var port = new FakeMetaPort(MetaUiTestKit.Base());
            var p = new AreaUpgradePresenter();
            p.SetPort(port);
            p.Open();

            Assert.That(p.Press(AreaUpgradePresenter.BuyPrefix + "rate"), Is.True);
            Assert.That(port.BuyCalls, Is.EqualTo(1));
            Assert.That(port.LastBuy, Is.EqualTo((MvGrowth.AreaUp, "rate")));

            port.CanBuyResult = false; // 부족·MAX: 버튼이 꺼져 명령을 부르지 않는다
            p.Draw();
            Assert.That(p.Press(AreaUpgradePresenter.BuyPrefix + "rate"), Is.False);
            Assert.That(port.BuyCalls, Is.EqualTo(1));
        }

        [Test]
        public void RebirthPerk_Buy_CallsMetaOnce()
        {
            var port = new FakeMetaPort(MetaUiTestKit.Base());
            var p = new RebirthPresenter();
            p.SetPort(port);
            p.Open();

            Assert.That(p.Press(RebirthPresenter.BuyPrefix + "power"), Is.True);
            Assert.That(port.BuyCalls, Is.EqualTo(1));
            Assert.That(port.LastBuy, Is.EqualTo((MvGrowth.Perk, "power")));
        }

        [Test]
        public void GodSelect_Use_CallsMetaOnceAndCloses()
        {
            var port = new FakeMetaPort(MetaUiTestKit.Base()) { SelectedGod = "thunder" };
            var p = new GodSelectPresenter();
            bool closed = false;
            p.CloseRequested += () => closed = true;
            p.SetPort(port);
            p.Open();
            Assert.That(p.Model.Confirm.Enabled, Is.False); // 사용 중인 신

            p.SelectTab("flame");
            Assert.That(p.Press(GodSelectPresenter.UseId), Is.True);
            Assert.That(port.SelectGodCalls, Is.EqualTo(1));
            Assert.That(port.LastGod, Is.EqualTo("flame"));
            Assert.That(closed, Is.True);
        }

        [Test]
        public void FeatureUnlock_Confirm_MarksEachSeenOnce()
        {
            var port = new FakeMetaPort(MetaUiTestKit.Base()) { Pending = { "rebirth", "quest" } };
            var p = new FeatureUnlockPresenter();
            bool closed = false;
            p.CloseRequested += () => closed = true;
            p.SetPort(port);
            p.Open();
            Assert.That(p.Model.Rows.Single().Id, Is.EqualTo("rebirth"));

            Assert.That(p.Press(FeatureUnlockPresenter.OkId), Is.True);
            Assert.That(port.Seen, Is.EqualTo(new[] { "rebirth" }));
            Assert.That(closed, Is.False);
            Assert.That(p.Model.Rows.Single().Id, Is.EqualTo("quest"));

            Assert.That(p.Press(FeatureUnlockPresenter.OkId), Is.True);
            Assert.That(port.Seen, Is.EqualTo(new[] { "rebirth", "quest" }));
            Assert.That(closed, Is.True);
        }

        [Test]
        public void Debug_Grant_SendsOneMetaRequest()
        {
            var port = new FakeMetaPort(MetaUiTestKit.Base());
            var p = new DebugPanelPresenter();
            p.SetPort(port);
            p.Open();

            Assert.That(p.Press("grant.gold"), Is.True);
            Assert.That(port.Debug.Count, Is.EqualTo(1));
            Assert.That(port.Debug[0], Is.EqualTo((MvDebugRequests.Grant, "1001001", 500L)));
        }
    }
}
