using System;
using System.Collections.Generic;
using System.Linq;
using MV.Data;
using MV.Meta;
using NUnit.Framework;

namespace MV.UI.Tests
{
    /// <summary>
    /// 로비 화면 코드(D-92 ③, 05 §5.4·§5.5.1·§5.14): 표 행 추가 → 강화 버튼 추가, <c>05_contentOpen</c> 잠금,
    /// 메타가 없으면 막힘, 강화 1번 → 메타 구매 1번, 시설 단계 문턱(정본 azStages).
    /// </summary>
    public sealed class LobbyPresenterTests
    {
        sealed class Source : ILobbySource
        {
            public bool TryRead(out LobbyState s)
            {
                s = new LobbyState { Level = 3, AreaNo = 1, Souls = 40, NeedSouls = 100 };
                return true;
            }

            public int FacilityStage(string key, out int extra) { extra = 0; return 1; }
        }

        /// <summary>잠금·구매 수를 정하고 호출을 세는 가짜 메타 입구.</summary>
        sealed class Port : IMetaUiPort
        {
            public bool Ready { get; set; } = true;
            public MvTables Tables { get; set; }
            public int AreaIndex => 0;
            public int Cp => 0;
            public long Wealth(int wealthId) => 1000;

            public readonly HashSet<string> Opened = new HashSet<string>();
            public readonly Dictionary<string, int> Levels = new Dictionary<string, int>();
            public bool CanBuyResult = true;
            public int BuyCalls;
            public (MvGrowth growth, string key) LastBuy;
            public bool RbOpen;

            public Port(MvTables tables) { Tables = tables; }

            public int Level(MvGrowth growth, string key) => Levels.TryGetValue(key, out int v) ? v : 0;
            public bool IsMax(MvGrowth growth, string key) => false;
            public IReadOnlyList<ConsumeData> NextPrice(MvGrowth growth, string key) => new[] { new ConsumeData(ConsumeType.Wealth, 1001001, 25) };
            public bool CanBuy(MvGrowth growth, string key) => CanBuyResult;
            public MetaResult Buy(MvGrowth growth, string key) { BuyCalls++; LastBuy = (growth, key); return MetaResult.Success(); }

            public string SelectedGod => "thunder";
            public bool GodUnlocked(string key) => true;
            public MetaResult SelectGod(string key) => MetaResult.Success();

            public int RebirthLevel => 0;
            public int RebirthPending => 0;
            public bool RebirthOpen => RbOpen;
            public long RebirthCooldownLeftMs => 0;
            public bool RebirthArmed => false;
            public MetaResult? PressRebirth() => null;

            public int AdsLeft(string key) => 3;
            public void WatchAdForReward(string key, Action<MetaResult> done) => done?.Invoke(MetaResult.Success());

            public bool IsOpen(string key) => Opened.Contains(key);
            public IReadOnlyList<string> PendingUnlockShows() => Array.Empty<string>();
            public MetaResult MarkUnlockSeen(string key) => MetaResult.Success();
            public MetaResult SendDebug(string requestName, string key = null, long amount = 0) => MetaResult.Success();
            public IMvAdService Ads => null;
        }

        static LobbyPresenter Make(Port port, bool devBuild = false)
        {
            var p = new LobbyPresenter { DevBuild = devBuild };
            p.SetPort(port);
            p.SetSource(new Source());
            p.Draw();
            return p;
        }

        [Test]
        public void UpgradeRowAdded_ShowsOneMoreButton()
        {
            int before = Make(new Port(MetaUiTestKit.Base())).Model.Upgrades.Count;
            var after = Make(new Port(MetaUiTestKit.WithExtraRow("10_lobbyUpgrade"))).Model.Upgrades;
            Assert.That(before, Is.EqualTo(3));
            Assert.That(after.Count, Is.EqualTo(before + 1));
            Assert.That(after.Last().Key, Does.EndWith("_t"));
            Assert.IsTrue(after.All(u => u.Enabled && u.Price.Length > 0));
        }

        [TestCase(LobbySideIds.Rebirth, "rebirth", "locked")]
        [TestCase(LobbySideIds.Rebirth, "rebirth", "open")]
        [TestCase(LobbySideIds.Content, "contentMenu", "locked")]
        [TestCase(LobbySideIds.Content, "contentMenu", "open")]
        public void SideLock_FollowsContentOpen(string side, string unlockKey, string state)
        {
            bool open = state == "open";
            var port = new Port(MetaUiTestKit.Base());
            if (open) port.Opened.Add(unlockKey);
            var p = Make(port);
            var requested = new List<string>();
            var toasts = new List<string>();
            p.SideRequested += requested.Add;
            p.Toast += toasts.Add;

            Assert.AreEqual(!open, p.Model.FindSide(side).Locked);
            Assert.AreEqual(open, p.PressSide(side));
            Assert.AreEqual(open ? 1 : 0, requested.Count);
            Assert.AreEqual(open ? 0 : 1, toasts.Count);
        }

        [Test]
        public void MetaNotReady_BlocksUpgradesAndMetaButtons()
        {
            var port = new Port(MetaUiTestKit.Base()) { Ready = false };
            var p = Make(port);
            var requested = new List<string>();
            p.SideRequested += requested.Add;

            Assert.IsTrue(p.Model.Upgrades.Count > 0);
            Assert.IsTrue(p.Model.Upgrades.All(u => !u.Enabled));
            foreach (var u in p.Model.Upgrades) Assert.IsFalse(p.PressUpgrade(u.Key));
            foreach (var id in new[] { LobbySideIds.God, LobbySideIds.AreaUpgrade, LobbySideIds.Rebirth, LobbySideIds.Map, LobbySideIds.Content })
                Assert.IsFalse(p.PressSide(id), id);
            Assert.AreEqual(0, port.BuyCalls);
            Assert.IsEmpty(requested);
            Assert.IsTrue(p.PressSide(LobbySideIds.Sound)); // 설정(소리)은 메타와 무관
        }

        [TestCase("stair", "tower")]
        [TestCase("weapon", "scratch")]
        [TestCase("income", "rack")]
        public void UpgradePress_BuysOnceAndBumpsFacility(string key, string facility)
        {
            var port = new Port(MetaUiTestKit.Base());
            var p = Make(port);
            var bumps = new List<string>();
            int bought = 0;
            p.FacilityBump += bumps.Add;
            p.Bought += () => bought++;

            Assert.IsTrue(p.PressUpgrade(key));
            Assert.AreEqual(1, port.BuyCalls);
            Assert.AreEqual((MvGrowth.Lobby, key), port.LastBuy);
            Assert.AreEqual(1, bought);
            CollectionAssert.AreEqual(new[] { facility }, bumps);
        }

        [Test]
        public void UpgradeCannotAfford_PressIgnored()
        {
            var port = new Port(MetaUiTestKit.Base()) { CanBuyResult = false };
            var p = Make(port);
            Assert.IsTrue(p.Model.Upgrades.All(u => !u.Enabled));
            Assert.IsFalse(p.PressUpgrade("stair"));
            Assert.AreEqual(0, port.BuyCalls);
        }

        [TestCase("dev")]
        [TestCase("release")]
        public void DebugButton_OnlyInDevBuild(string build)
        {
            bool dev = build == "dev";
            var p = Make(new Port(MetaUiTestKit.Base()), dev);
            Assert.AreEqual(dev, p.Model.FindSide(LobbySideIds.Debug).Visible);
            Assert.AreEqual(dev, p.PressSide(LobbySideIds.Debug));
        }

        [TestCase("tower", "stair", 6, 0, 1, 0)]
        [TestCase("tower", "stair", 7, 0, 2, 0)]
        [TestCase("tower", "stair", 30, 0, 3, 0)]
        [TestCase("rack", "income", 4, 0, 1, 3)]
        [TestCase("rack", "income", 5, 0, 2, 3)]
        [TestCase("rack", "income", 15, 0, 3, 6)]
        [TestCase("stove", "", 0, 0, 0, 0)]
        [TestCase("stove", "", 0, 1, 1, 0)]
        public void FacilityStage_FollowsCanonThresholds(string facility, string upKey, int bought, int rebirthOpen, int stage, int extra)
        {
            var port = new Port(MetaUiTestKit.Base()) { RbOpen = rebirthOpen == 1 };
            if (upKey.Length > 0) port.Levels[upKey] = bought;
            Assert.AreEqual(stage, LobbyFacilities.Stage(port, facility, out int got));
            Assert.AreEqual(extra, got);
        }
    }
}
