using System.Linq;
using MV.Data;
using MV.Save;
using NUnit.Framework;

namespace MV.Meta.Tests
{
    /// <summary>성장 구매(07 §7.8.6·7.8.7, 04 §4.12.1~4.12.3): 비용 P=0~10 대조, 차감·단계, MAX·부족 거절, 덱 머지.</summary>
    public sealed class MvMetaGrowthTests : MvMetaTestBase
    {
        // 07 §7.8.6 생성 결과(L = 1부터) 앞 11개. max가 10 이하면 max개.
        static readonly int[] Stair = { 25, 30, 35, 41, 48, 57, 67, 80, 94, 111, 131 };
        static readonly int[] Weapon = { 30, 31, 33, 34, 36, 37, 39, 40, 42, 44, 46 };
        static readonly int[] Income = { 28, 36, 44, 52, 60, 68, 76, 84, 92, 100, 108 };
        static readonly int[] RateRange = { 40, 64, 102, 164, 262, 419, 671, 1074, 1718, 2749 };
        // MV-SPEC-02 Q-09: 분신 max 30, 16_levelPrice group 13(100 × 1.6^(P))
        static readonly int[] Clone = { 100, 160, 256, 410, 655 }; // 최대 5단계(기획자확인 PC-18)
        static readonly int[] CritDbl = { 80, 128, 205, 328, 524, 839, 1342, 2147, 3436, 5498 };
        // MV-SPEC-02 571~572 관측 「1→2단계 : 50」: 16021002·16022002 = 50(MV-SPEC2-FIX1), 나머지는 50 × 1.5^(L−1).
        static readonly int[] PowerCrit = { 50, 50, 113, 169, 253, 380, 570, 854, 1281, 1922, 2883 };
        static readonly int[] Obst = { 60, 90, 135, 203, 304, 456, 683, 1025, 1538, 2307 };
        // MV-SPEC-02 574: 블루 마법석 획득량 10 → 20 = base 10 + 2 × max 5(MV-SPEC2-FIX1).
        static readonly int[] Dcap = { 60, 90, 135, 203, 304 };

        const long Plenty = 1_000_000_000L;

        void BuyCostsMatch(MvGrowth growth, string key, int wealthId, int[] expected, bool maxAfter)
        {
            for (int p = 0; p < expected.Length; p++)
            {
                Assert.That(Meta.Level(growth, key), Is.EqualTo(p), $"{key} P");
                var price = Meta.NextPrice(growth, key);
                Assert.That(price, Is.Not.Null, $"{key} P={p} 비용");
                Assert.That(price.Count, Is.EqualTo(1));
                Assert.That(price[0].Id, Is.EqualTo(wealthId), $"{key} 재화");
                Assert.That(price[0].Value, Is.EqualTo(expected[p]), $"{key} P={p} 비용");
                Assert.That(Meta.CanBuy(growth, key), Is.True);

                long before = W(wealthId);
                MvMetaResponse response = Meta.Buy(growth, key);
                Assert.That(response.Ok, Is.True, $"{key} P={p} {response}");
                Assert.That(before - W(wealthId), Is.EqualTo(expected[p]), $"{key} P={p} 차감");
                Assert.That(Meta.Level(growth, key), Is.EqualTo(p + 1));
            }
            if (!maxAfter) return;
            Assert.That(Meta.IsMax(growth, key), Is.True, key + " MAX");
            Assert.That(Meta.NextPrice(growth, key), Is.Null);
            long left = W(wealthId);
            Assert.That(Meta.Buy(growth, key).Code, Is.EqualTo(MvMetaErrors.Max));
            Assert.That(W(wealthId), Is.EqualTo(left), "MAX 거절은 재화를 바꾸지 않는다");
        }

        [Test]
        public void LobbyUpgrade_CostsMatchSpec_P0To10()
        {
            SetWealth((MvWealthIds.Gold, Plenty));
            BuyCostsMatch(MvGrowth.Lobby, MvMetaRules.LobbyStair, MvWealthIds.Gold, Stair, false);
            BuyCostsMatch(MvGrowth.Lobby, MvMetaRules.LobbyWeapon, MvWealthIds.Gold, Weapon, false);
            BuyCostsMatch(MvGrowth.Lobby, MvMetaRules.LobbyIncome, MvWealthIds.Gold, Income, false);
            Assert.That(Meta.GoldMul(), Is.EqualTo(1 + 0.05 * 11).Within(1e-9));
        }

        [Test]
        public void AreaUpgrade_CostsMatchSpec_AndMax()
        {
            SetWealth((MvWealthIds.Dia, Plenty));
            BuyCostsMatch(MvGrowth.AreaUp, "rate", MvWealthIds.Dia, RateRange, true);
            BuyCostsMatch(MvGrowth.AreaUp, "range", MvWealthIds.Dia, RateRange, true);
            BuyCostsMatch(MvGrowth.AreaUp, "clone", MvWealthIds.Dia, Clone, true);
            BuyCostsMatch(MvGrowth.AreaUp, "crit", MvWealthIds.Dia, CritDbl, true);
            BuyCostsMatch(MvGrowth.AreaUp, "dbl", MvWealthIds.Dia, CritDbl, true);
            Assert.That(Meta.AreaUpValue("clone"), Is.EqualTo(5).Within(1e-9));
            Assert.That(Meta.AreaUpValue("rate"), Is.EqualTo(1.0).Within(1e-9));
        }

        [Test]
        public void RebirthPerk_CostsMatchSpec_AndValue()
        {
            SetWealth((MvWealthIds.RbToken, Plenty));
            BuyCostsMatch(MvGrowth.Perk, "power", MvWealthIds.RbToken, PowerCrit, false);
            BuyCostsMatch(MvGrowth.Perk, "crit", MvWealthIds.RbToken, PowerCrit, false);
            BuyCostsMatch(MvGrowth.Perk, "obst", MvWealthIds.RbToken, Obst, true);
            BuyCostsMatch(MvGrowth.Perk, "dcap", MvWealthIds.RbToken, Dcap, true);
            Assert.That(Meta.PerkValue("power"), Is.EqualTo(1 + 0.15 * 11).Within(1e-9));
            Assert.That(Meta.PerkValue("obst"), Is.EqualTo(0.5).Within(1e-9), "obst floor 0.5");
            Assert.That(Meta.PerkValue("dcap"), Is.EqualTo(10 + 2 * 5).Within(1e-9), "dcap 최대 20");
            Assert.That(Meta.PerkValue("crit"), Is.EqualTo(2 + 0.2 * 11).Within(1e-9), "치명 피해 200% + 20%p × Lv");
        }

        [Test]
        public void Buy_NotEnough_RejectsWithoutChange()
        {
            SetWealth((MvWealthIds.Gold, 24));
            Assert.That(Meta.CanBuy(MvGrowth.Lobby, MvMetaRules.LobbyStair), Is.False);
            MvLobbyBuyResult result = Meta.BuyLobbyUp(MvMetaRules.LobbyStair);
            Assert.That(result.Ok, Is.False);
            Assert.That(result.Response.Code, Is.EqualTo(MvMetaErrors.NotEnough));
            Assert.That(W(MvWealthIds.Gold), Is.EqualTo(24));
            Assert.That(Store.Lobby.GetUp(MvMetaRules.LobbyStair), Is.EqualTo(0));
            Assert.That(Meta.BuyAreaUp("nope").Code, Is.EqualTo(MvMetaErrors.UnknownKey));
        }

        /// <summary>MV-UI2-AREAUP: 다음 단계 <c>16_levelPrice.needRebirth</c>보다 부활 레벨이 낮으면 잠김(살 수 없음·거절·재화 그대로). 레벨별(사용자 2026-11-19).</summary>
        [Test]
        public void NeedRebirth_LocksBuy_PerLevel_UntilRebirthLevel()
        {
            Assert.That(Meta.IsRebirthLocked(MvGrowth.AreaUp, "rate"), Is.False, "rate 1단계는 표에 잠김 없음");

            int G(ILevelPriced r) => r.levelPriceGroup;
            var rate = Tables.AreaUpgrade.ByKey("rate");
            var range = Tables.AreaUpgrade.ByKey("range");
            var power = Tables.RebirthPerk.ByKey("power");
            var locked = new MvTables();
            locked.LoadAll(new MvMemoryTableSource(new MvTsvDirectorySource(MvTableLoader.TsvDirectory))
                .Set(LevelPriceData.Table, WithNeedRebirth((G(rate), 1, 2), (G(power), 1, 2), (G(range), 2, 2))));
            var meta = new MvMeta(() => Store, () => Res, locked, Clock, Ads);
            SetWealth((MvWealthIds.Dia, Plenty), (MvWealthIds.RbToken, Plenty));

            Assert.That(Store.Rebirth.Lv, Is.EqualTo(0));
            foreach (var (growth, key, wealth) in new[] { (MvGrowth.AreaUp, "rate", MvWealthIds.Dia), (MvGrowth.Perk, "power", MvWealthIds.RbToken) })
            {
                Assert.That(meta.NeedRebirth(growth, key), Is.EqualTo(2), key);
                Assert.That(meta.IsRebirthLocked(growth, key), Is.True, key);
                Assert.That(meta.CanBuy(growth, key), Is.False, key + " 잠김이면 버튼 꺼짐");
                long before = W(wealth);
                Assert.That(meta.Buy(growth, key).Code, Is.EqualTo(MvMetaErrors.Locked), key);
                Assert.That(W(wealth), Is.EqualTo(before), key + " 잠김 거절은 재화를 바꾸지 않는다");
                Assert.That(meta.Level(growth, key), Is.EqualTo(0), key);
            }

            // range: 1단계는 열림, 2단계부터 잠김
            Assert.That(meta.IsRebirthLocked(MvGrowth.AreaUp, "range"), Is.False);
            Assert.That(meta.Buy(MvGrowth.AreaUp, "range").Ok, Is.True);
            Assert.That(meta.Level(MvGrowth.AreaUp, "range"), Is.EqualTo(1));
            Assert.That(meta.NeedRebirth(MvGrowth.AreaUp, "range"), Is.EqualTo(2), "다음 단계(2)의 조건");
            Assert.That(meta.IsRebirthLocked(MvGrowth.AreaUp, "range"), Is.True);
            Assert.That(meta.Buy(MvGrowth.AreaUp, "range").Code, Is.EqualTo(MvMetaErrors.Locked));
        }

        /// <summary>16_levelPrice에서 (group, targetLevel) 행의 needRebirth만 바꾼 글.</summary>
        static string WithNeedRebirth(params (int group, int level, int need)[] set)
        {
            string text = System.IO.File.ReadAllText(System.IO.Path.Combine(MvTableLoader.TsvDirectory, LevelPriceData.Table + ".txt"));
            var lines = text.Replace("\r\n", "\n").Split('\n').Where(l => l.Length > 0).ToList();
            var head = lines[0].Split('\t').ToList();
            int gc = head.IndexOf("group"), lc = head.IndexOf("targetLevel"), nc = head.IndexOf("needRebirth");
            Assert.That(nc, Is.GreaterThanOrEqualTo(0), "16_levelPrice needRebirth 열");
            int hit = 0;
            for (int i = 1; i < lines.Count; i++)
            {
                var c = lines[i].Split('\t');
                foreach (var s in set)
                {
                    if (c[gc] != s.group.ToString() || c[lc] != s.level.ToString()) continue;
                    c[nc] = s.need.ToString();
                    hit++;
                }
                lines[i] = string.Join("\t", c);
            }
            Assert.That(hit, Is.EqualTo(set.Length), "바꿀 행");
            return string.Join("\n", lines) + "\n";
        }

        [Test]
        public void Weapon_AddsCurrentTierCard_AndMergesThree()
        {
            SetWealth((MvWealthIds.Gold, Plenty));
            Assert.That(Store.Lobby.Deck, Is.EqualTo(new[] { 0, 1, 0, 0, 0, 0, 0, 0, 0 }), "시작 덱");

            MvLobbyBuyResult first = Meta.BuyLobbyUp(MvMetaRules.LobbyWeapon);
            Assert.That(first.Ok, Is.True);
            Assert.That(first.AddedTier, Is.EqualTo(1));
            Assert.That(first.MergedTier, Is.EqualTo(0));
            Assert.That(Store.Lobby.Deck.Take(3), Is.EqualTo(new[] { 0, 2, 0 }));
            Assert.That(Store.Lobby.Deck.Count, Is.EqualTo(Tables.WeaponTierMax() + 1), "덱 길이 = 103 active 상한 등급 + 1(MV-SPEC-02 Q-08)");

            MvLobbyBuyResult second = Meta.BuyLobbyUp(MvMetaRules.LobbyWeapon);
            Assert.That(second.MergedTier, Is.EqualTo(2), "1등급 3장 → 2등급");
            Assert.That(Store.Lobby.Deck.Take(3), Is.EqualTo(new[] { 0, 0, 1 }));

            // wLevel: 12_weaponLevel lv1 need 8 → 8번째 구매(P 7→8)에서 무기 Level 2.
            for (int i = 2; i < 7; i++) Meta.BuyLobbyUp(MvMetaRules.LobbyWeapon);
            MvLobbyBuyResult eighth = Meta.BuyLobbyUp(MvMetaRules.LobbyWeapon);
            Assert.That(eighth.LvBefore, Is.EqualTo(1));
            Assert.That(eighth.LvAfter, Is.EqualTo(2));
            Assert.That(Meta.WeaponLevel().Lv, Is.EqualTo(2));
            // 8번째까지 1등급 8장(+시작 1장) → 3등급 1장. 9번째는 2등급 카드.
            Assert.That(Store.Lobby.Deck, Is.EqualTo(new[] { 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0 }));
            MvLobbyBuyResult ninth = Meta.BuyLobbyUp(MvMetaRules.LobbyWeapon);
            Assert.That(ninth.AddedTier, Is.EqualTo(2));
            Assert.That(Store.Lobby.Deck, Is.EqualTo(new[] { 0, 0, 1, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0 }));
        }

        [Test]
        public void Weapon_TierMaxFromActiveItemRows_MergesIntoLastTier()
        {
            // MV-SPEC-02 Q-08: 상한 등급은 103 active 행 tier 최대(표 설정만으로 12등급).
            int max = Tables.WeaponTierMax();
            Assert.That(max, Is.EqualTo(Tables.Item.Datas.Where(i => i.active).Max(i => i.tier)));
            Assert.That(max, Is.EqualTo(Tables.WeaponLevel.Datas.Max(r => r.lv)), "12_weaponLevel 행 수 = 103 상한 등급");
            var deck = new int[max + 1];
            deck[max - 1] = 2;
            Assert.That(MvMetaRules.DeckAdd(deck, max - 1, Tables.RunConfig.I(RunConfigKeys.MERGE_N)), Is.EqualTo(max), "상한 직전 3장 → 상한 등급");
            Assert.That(deck[max], Is.EqualTo(1));
            deck[max] = 2;
            Assert.That(MvMetaRules.DeckAdd(deck, max, Tables.RunConfig.I(RunConfigKeys.MERGE_N)), Is.EqualTo(0), "상한 등급은 더 머지하지 않는다");
            Assert.That(deck[max], Is.EqualTo(3));
            // 무기 단계가 오르면 그보다 낮은 카드는 버린다(사용자 2026-11-19 「낮은 무기 버림」).
            var low = new int[max + 1];
            low[1] = 2; low[2] = 1; low[3] = 1;
            Assert.That(MvMetaRules.DiscardBelow(low, 3), Is.EqualTo(3), "3등급 아래 3장 버림");
            Assert.That(low.Take(4), Is.EqualTo(new[] { 0, 0, 0, 1 }), "그 단계 카드는 남긴다");
            // 같은 단계 3장 이상은 밑에서부터 합친다(옛 짧은 덱 상한에 쌓인 카드).
            var stack = new int[max + 1];
            stack[2] = 3; stack[3] = 2;
            MvMetaRules.MergeUp(stack, 3);
            Assert.That(stack.Skip(1).Take(3), Is.EqualTo(new[] { 0, 0, 0 }), "2단계 3장 → 3단계 3장 → 4단계");
            Assert.AreEqual(1, stack[4]);
            // 저장 덱이 상한보다 짧아도 MAX 등급까지 산다: 무기 Level 마지막 = 상한.
            Assert.That(MvMetaRules.WeaponLevel(Tables, Tables.LevelMax(Tables.LobbyUpgrade.ByKey("weapon"))).Lv, Is.EqualTo(max));
        }

        [Test]
        public void Stair_StartGaugeLevel_FollowsStairLevelNeed()
        {
            // 11_stairLevel need 7/10/13/16, 상한 Lv 4.
            Assert.That(MvMetaRules.StairStart(Tables, 0).Lv, Is.EqualTo(1));
            Assert.That(MvMetaRules.StairStart(Tables, 6).Lv, Is.EqualTo(1));
            Assert.That(MvMetaRules.StairStart(Tables, 7).Lv, Is.EqualTo(2));
            Assert.That(MvMetaRules.StairStart(Tables, 17).Lv, Is.EqualTo(3));
            Assert.That(MvMetaRules.StairStart(Tables, 30).Lv, Is.EqualTo(4));
            Assert.That(MvMetaRules.StairStart(Tables, 30).Step, Is.EqualTo(0));
            Assert.That(MvMetaRules.StairStart(Tables, 30).Need, Is.EqualTo(16));

            SetWealth((MvWealthIds.Gold, Plenty));
            for (int i = 0; i < 6; i++) Meta.BuyLobbyUp(MvMetaRules.LobbyStair);
            MvLobbyBuyResult seventh = Meta.BuyLobbyUp(MvMetaRules.LobbyStair);
            Assert.That(seventh.LvBefore, Is.EqualTo(1));
            Assert.That(seventh.LvAfter, Is.EqualTo(2));
            Assert.That(Meta.StairStart().Lv, Is.EqualTo(2));
        }
    }
}
