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
    /// 메타가 없으면 막힘, 강화 1번 → 메타 구매 1번.
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
            Assert.IsTrue(after.All(u => u.InputAllowed && u.Cost != null && u.Cost.Count > 0), "가격 버튼(PriceBtn)에 넘길 비용·입력 허용");
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

            // 환생은 잠겨 있어도 팝업을 열고 팝업이 Lock을 보인다(노션 [팝업] 환생 업그레이드, MV-UI2-REBIRTH).
            bool opens = open || side == LobbySideIds.Rebirth;
            Assert.AreEqual(!open, p.Model.FindSide(side).Locked);
            Assert.AreEqual(opens, p.PressSide(side));
            Assert.AreEqual(opens ? 1 : 0, requested.Count);
            Assert.AreEqual(opens ? 0 : 1, toasts.Count);
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
            Assert.IsTrue(p.Model.Upgrades.All(u => !u.InputAllowed), "메타 없음이면 가격 버튼 입력 막음");
            foreach (var u in p.Model.Upgrades) Assert.IsFalse(p.PressUpgrade(u.Key));
            foreach (var id in new[] { LobbySideIds.God, LobbySideIds.AreaUpgrade, LobbySideIds.Rebirth, LobbySideIds.Map, LobbySideIds.Content })
                Assert.IsFalse(p.PressSide(id), id);
            Assert.AreEqual(0, port.BuyCalls);
            Assert.IsEmpty(requested);
            Assert.IsTrue(p.PressSide(LobbySideIds.Sound)); // 설정(소리)은 메타와 무관
        }

        [TestCase("stair")]
        [TestCase("weapon")]
        [TestCase("income")]
        public void UpgradePress_BuysOnce(string key)
        {
            var port = new Port(MetaUiTestKit.Base());
            var p = Make(port);
            int bought = 0;
            p.Bought += () => bought++;
            var added = new List<int>();
            p.DeckCardAdded += added.Add;

            Assert.IsTrue(p.PressUpgrade(key));
            Assert.AreEqual(1, port.BuyCalls);
            Assert.AreEqual((MvGrowth.Lobby, key), port.LastBuy);
            Assert.AreEqual(1, bought);
            // 스크래쳐(DeckAdd)만 덱 카드 연출을 부른다(MV-UI3-LOBBY-WEAPONFX). 구매 결과 등급이 없으면 사기 전 무기 Level = 등급.
            bool deckAdd = port.Tables.LobbyUpgrade.ByKey(key).effectType == MV.Data.LobbyEffectType.DeckAdd;
            Assert.AreEqual(deckAdd ? 1 : 0, added.Count, key + ": 덱 카드 연출");
            if (deckAdd) Assert.That(added[0], Is.GreaterThanOrEqualTo(1));
        }

        [Test]
        public void UpgradeCannotAfford_PressIgnored()
        {
            var port = new Port(MetaUiTestKit.Base()) { CanBuyResult = false };
            var p = Make(port);
            Assert.IsTrue(p.Model.Upgrades.All(u => !u.Enabled));
            Assert.IsTrue(p.Model.Upgrades.All(u => u.InputAllowed), "잔액 부족은 입력 허용과 별개(빨간 글자·잠금은 PriceBtn이 잔액으로 판단)");
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

        /// <summary>디자이너 칸 값: 가격 숫자·재화 아이콘, 부활 해금 진행 = Level(실패 횟수) / 05_contentOpen 사용 조건 Level.</summary>
        [Test]
        public void Draw_FillsDesignerSlotValues()
        {
            var t = MetaUiTestKit.Base();
            var p = Make(new Port(t));
            foreach (var u in p.Model.Upgrades)
            {
                Assert.That(u.Cost?.Count, Is.EqualTo(1), u.Key);
                Assert.AreEqual(25d, u.Cost[0].Value, u.Key + ": 16_levelPrice 비용 그대로 PriceBtn에 넘김");
                Assert.That(u.PriceIcon, Is.Not.Empty, u.Key + ": 01_wealth.icon");
            }
            Assert.IsTrue(p.Model.Upgrades.Single(u => u.Key == "weapon").IconIsItem);
            // 무기 칸 Slot_Count_Weapon = 산 수 / 12_weaponLevel 그 Level need(프리팹 자리 글자가 남지 않는다).
            var wu = p.Model.Upgrades.Single(u => u.Key == "weapon");
            var wl = MvMetaRules.WeaponLevel(t, 0);
            Assert.AreEqual("0", wu.CurValue, "산 수");
            Assert.AreEqual(t.WeaponLevel.Datas.First(r => r.lv == wl.Lv).need.ToString(), wu.NextValue, "필요 수 = 표 need");

            int need = 0;
            foreach (var c in t.ContentOpen.ByKey(LobbyPresenter.RebirthUnlockKey).openConditions)
                if (c.type == UnlockConditionType.Level) need = c.value;
            Assert.AreEqual(3, p.Model.Rebirth.UnlockNow, "Source.Level");
            Assert.AreEqual(need, p.Model.Rebirth.UnlockNeed);
            Assert.That(need, Is.GreaterThan(0), "부활 사용 조건에 Level이 있다");
        }
    }
}
