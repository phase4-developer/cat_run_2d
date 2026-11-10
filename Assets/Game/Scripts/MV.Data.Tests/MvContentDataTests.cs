using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using NUnit.Framework;

namespace MV.Data.Tests
{
    /// <summary>08 콘텐츠 자료(야옹신·카드·메타 성장 비용·상자) 대표 값과 콘텐츠 의미 규칙(기획서 08 §8.3, §8.5, 07 §7.7, §7.8.6).</summary>
    public sealed class MvContentDataTests
    {
        static readonly string[] s_slots = { "orb", "proc", "chain", "storm", "blast", "frate", "spear" };
        static readonly int[] s_slotTiers = { 1, 2, 2, 3, 4, 4, 4 };

        static string R(params string[] cells) => string.Join("\t", cells);

        static void AssertError(MvValidationReport report, string table, string fragment) =>
            Assert.That(report.Contains(MvIssueLevel.Error, table, fragment), Is.True,
                $"[{table}] `{fragment}` 오류가 없습니다.\n{report.Summary(60)}");

        static CardEffectData EffectOf(MvTables t, int cardId) => t.CardEffect.Get(t.Card.Get(cardId).effectId);

        // ---------------- 대표 값 ----------------

        [Test]
        public void Gods_MatchSpec08Rows()
        {
            var t = TsvFixture.LoadTables();
            Assert.That(t.God.Datas.Count, Is.EqualTo(12));
            // MV-SPEC-02 Q-21(D-74 대체): 1번 물병자리냥만 공개, 메뉴 10종(11·12번은 데이터만).
            Assert.That(t.God.Datas.Where(g => g.active).Select(g => g.key), Is.EquivalentTo(new[] { "thunder" }));
            Assert.That(t.God.Datas.Where(g => !g.showInMenu).Select(g => g.id), Is.EquivalentTo(new[] { 113001011, 113001012 }));

            // Q-20: 3~12번은 id 유지, 새 기준 key(GodRefill 1절).
            var wind = t.God.ByKey("pisces");
            Assert.That(wind.id, Is.EqualTo(113001003));
            Assert.That(wind.cardGroup, Is.EqualTo(3));
            Assert.That(wind.unlockType, Is.EqualTo(2));
            Assert.That(wind.unlockPrice.Count, Is.EqualTo(1));
            Assert.That(wind.unlockPrice[0].Type, Is.EqualTo(ConsumeType.Wealth));
            Assert.That(wind.unlockPrice[0].Id, Is.EqualTo(1001008)); // 야옹신 방울
            Assert.That(wind.unlockPrice[0].Value, Is.EqualTo(50));
            Assert.That(wind.passiveStat, Is.EqualTo("FireRate"));
            Assert.That(wind.gearSet, Is.True);
            Assert.That(wind.projTint, Is.EqualTo(wind.color)); // 빈칸 = color

            var fish = t.God.ByKey("cancer");
            Assert.That(fish.unlockPrice[0].Value, Is.EqualTo(200));
            Assert.That(fish.passivePerLv, Is.EqualTo(8));
            Assert.That(fish.gearSet, Is.False);
            Assert.That(fish.portrait, Is.EqualTo("Tex_God_Cancer"));
        }

        [Test]
        public void GodCards_SevenSlotsPerGod_WithSpecEffects()
        {
            var t = TsvFixture.LoadTables();
            foreach (var god in t.God.Datas)
            {
                var cards = t.Card.GetGroup(god.cardGroup);
                Assert.That(cards.Select(c => c.key), Is.EqualTo(s_slots), god.key);
                Assert.That(cards.Select(c => c.tier), Is.EqualTo(s_slotTiers), god.key);
                Assert.That(cards.All(c => c.active == god.active), Is.True, god.key + " 카드 active = 신 active");
            }

            // MV-SPEC-02 GodRefill 2절 [컨셉 가안]: 대표 스킬 슬롯.
            Assert.That(EffectOf(t, 110003002).type, Is.EqualTo(CardEffectType.CloneAdd)); // pisces proc 물 분신
            Assert.That(EffectOf(t, 110004002).type, Is.EqualTo(CardEffectType.DoubleChance)); // sagittarius proc 다중 화살
            Assert.That(EffectOf(t, 110005002).type, Is.EqualTo(CardEffectType.ProcBurn)); // scorpio proc 독 베기
            Assert.That(EffectOf(t, 110006002).type, Is.EqualTo(CardEffectType.DmgMul)); // aries proc 전쟁 버프
            Assert.That(EffectOf(t, 110007004).type, Is.EqualTo(CardEffectType.Beam)); // virgo storm 빛줄기
            Assert.That(EffectOf(t, 110007007).type, Is.EqualTo(CardEffectType.Shield)); // virgo spear 보호막
            Assert.That(EffectOf(t, 110008004).type, Is.EqualTo(CardEffectType.Meteor)); // taurus storm 낙석
            Assert.That(EffectOf(t, 110009004).type, Is.EqualTo(CardEffectType.ProcChill)); // libra storm 시간 정지
            Assert.That(EffectOf(t, 110011003).type, Is.EqualTo(CardEffectType.ChainSplash)); // gemini chain 분열 창
            Assert.That(EffectOf(t, 110012007).type, Is.EqualTo(CardEffectType.Pierce)); // cancer spear 관통 투사체

            var chill = EffectOf(t, 110009002); // libra proc 둔화
            Assert.That(chill.type, Is.EqualTo(CardEffectType.ProcChill));
            Assert.That(new[] { chill.p1, chill.p2, chill.p3, chill.p4 }, Is.EqualTo(new[] { 0.4, 2, 0.25, 0.5 }));
            var cloneDmg = t.CardEffect.ByKey("cloneDmg"); // 쓰는 카드 없음(가안 뒤 보관)
            Assert.That(cloneDmg.type, Is.EqualTo(CardEffectType.CloneDmgMul));
            Assert.That(cloneDmg.p1, Is.EqualTo(0.5));
            var drop = EffectOf(t, 110010002); // capricorn proc 처치 시 추가 효과
            Assert.That(drop.type, Is.EqualTo(CardEffectType.DropBonus));
            Assert.That(new[] { drop.p1, drop.p2 }, Is.EqualTo(new[] { 0.08, 0.3 }));
            var chain = t.CardEffect.ByKey("chainLightning"); // 쓰는 카드 없음(가안 뒤 보관)
            Assert.That(chain.type, Is.EqualTo(CardEffectType.ChainJump));
            Assert.That(chain.p1, Is.EqualTo(2));

            // 07 §7.7.5 잔재 frost 행은 그대로(정본 값 p3만).
            var frost = t.CardEffect.ByKey("frost");
            Assert.That(frost.p1, Is.EqualTo(0));
            Assert.That(frost.p3, Is.EqualTo(0.25));
            // 새 효과 행은 2차(active 0).
            Assert.That(t.CardEffect.Datas.Where(e => e.id >= 112001022).All(e => !e.active), Is.True);
        }

        [Test]
        public void LevelPrice_ContentGroups_MatchGenerator()
        {
            var t = TsvFixture.LoadTables();
            var needPiece = t.LevelPrice.RawRows.ToDictionary(r => r.Get("id"), r => r.Get("needPiece"));
            var generated = MvLevelPriceGenerator.GenerateContent();
            Assert.That(generated.Count, Is.EqualTo(39 + 29 + 4 + 8 * 30));
            foreach (var g in generated)
            {
                var row = t.LevelPrice.Get(g.Group, g.TargetLevel);
                Assert.That(row, Is.Not.Null, $"{g.Group}/{g.TargetLevel}");
                Assert.That(row.id, Is.EqualTo(g.Id));
                Assert.That(row.price.Select(p => p.ToString()), Is.EqualTo(g.Price.Select(p => p.ToString())), row.id.ToString(CultureInfo.InvariantCulture));
                var raw = needPiece[row.id.ToString(CultureInfo.InvariantCulture)];
                Assert.That(string.IsNullOrEmpty(raw) ? 0 : int.Parse(raw, CultureInfo.InvariantCulture), Is.EqualTo(g.NeedPiece), row.id.ToString(CultureInfo.InvariantCulture));
            }
        }

        [Test]
        public void LevelPrice_ContentRepresentativeRows_MatchSpec08Examples()
        {
            var t = TsvFixture.LoadTables();
            // 08 §8.5.2 예: 16301002 = 사료 10 + 카드 2, 16301026 = 250 + 7, 16301040 = 750 + 10.
            Assert.That(t.LevelPrice.Get(301, 2).price[0].ToString(), Is.EqualTo("1/1001006/10"));
            Assert.That(t.LevelPrice.Get(301, 26).price[0].Value, Is.EqualTo(250));
            Assert.That(t.LevelPrice.Get(301, 40).price[0].Value, Is.EqualTo(750));
            Assert.That(MvLevelPriceGenerator.PetPiece(25), Is.EqualTo(7));
            // 08 §8.5.4 예: 16302002 = 1;621 / 11;0 / 30;3.
            var friend = t.LevelPrice.Get(302, 2).price;
            Assert.That(friend.Select(p => p.ToString()), Is.EqualTo(new[] { "1/1001011/30", "621/0/3" }));
            // 08 §8.3.5: 16304002 = 622 / 0 / 5 … 16304005 = 40.
            Assert.That(t.LevelPrice.Get(304, 2).price[0].ToString(), Is.EqualTo("622/0/5"));
            Assert.That(t.LevelPrice.Get(304, 5).price[0].Value, Is.EqualTo(40));
            // 08 §8.5.8: 그룹 306 + 희귀도, upgradeGold × 목표 레벨.
            Assert.That(t.LevelPrice.Get(306, 1).price[0].ToString(), Is.EqualTo("1/1001001/100"));
            Assert.That(t.LevelPrice.Get(313, 30).price[0].Value, Is.EqualTo(12800 * 30));
        }

        [Test]
        public void RandomBox_Groups_MatchSpec08()
        {
            var t = TsvFixture.LoadTables();
            var pet = t.RandomBox.GetGroup(802430001);
            var petCards = pet.Where(x => x.subGroup == 1).ToList();
            Assert.That(petCards.Count, Is.EqualTo(16));
            Assert.That(petCards.All(x => x.Type == ConsumeType.PetCard), Is.True);
            Assert.That(petCards.Sum(x => x.weight), Is.EqualTo(10000));
            Assert.That(petCards[0].weight, Is.EqualTo(1098));
            var food = pet.Single(x => x.subGroup == 2);
            Assert.That((food.Type, food.rewardId, food.rewardValueMin), Is.EqualTo((ConsumeType.Wealth, 1001006, 10L)));

            Assert.That(t.RandomBox.GetGroup(802460001).Select(x => x.rewardValueMin), Is.EqualTo(new[] { 20L, 40L, 60L, 100L }));
            Assert.That(t.RandomBox.GetGroup(802460001).All(x => x.Type == ConsumeType.Pass && x.rewardId == 0), Is.True);
            Assert.That(t.RandomBox.GetGroup(802471001).Select(x => x.weight), Is.EqualTo(new[] { 7000, 2500, 500 }));
            Assert.That(t.RandomBox.GetGroup(802472001).Select(x => x.rewardId), Is.EqualTo(new[] { 471001001, 471001002, 471001003, 471001004, 471001005 }));
            Assert.That(t.RandomBox.GetGroup(802473001).Select(x => x.rewardId), Is.EqualTo(new[] { 471001002, 471001003, 471001004, 471001005, 471001006 }));
        }

        [Test]
        public void Effect_Anchor_ReadsEnumOrNull()
        {
            var t = TsvFixture.LoadTables();
            Assert.That(t.Effect.ByKey("god.storm").anchor, Is.EqualTo(EffectAnchor.hit));
            Assert.That(t.Effect.ByKey("run.burnTick").anchor, Is.EqualTo(EffectAnchor.obj));
            Assert.That(t.Effect.ByKey("climb.lap").anchor, Is.EqualTo(EffectAnchor.climbTop));
            Assert.That(t.Effect.ByKey("fx.sparks").anchor, Is.Null);
        }

        [Test]
        public void ConsumeType_NoneRow_IsInactiveZero()
        {
            var t = TsvFixture.LoadTables();
            var none = t.ConsumeType.Get(ConsumeType.None);
            Assert.That(none, Is.Not.Null);
            Assert.That(none.codeName, Is.EqualTo("None"));
            Assert.That(none.active, Is.False);
            Assert.That(none.UsesId, Is.False);
        }

        // ---------------- 콘텐츠 의미 규칙(CheckContentData) ----------------

        [Test]
        public void Repository_ContentTables_HaveNoErrors()
        {
            var report = TsvFixture.Validate();
            var tables = new HashSet<string> { "04_effect", "07_consumeType", "16_levelPrice", "110_card", "111_cardPool", "112_cardEffect", "113_god", "802_randomBox" };
            var errors = report.Errors.Where(e => tables.Contains(e.Table)).ToList();
            Assert.That(errors, Is.Empty, string.Join("\n", errors.Select(e => e.ToString())));
        }

        [Test]
        public void God_PurchaseWithoutPrice_IsError()
        {
            var text = TsvFixture.Read("113_god");
            foreach (var c in new[] { "unlockPriceType", "unlockPriceId", "unlockPriceValue" }) text = TsvFixture.Set(text, "113001003", c, "");
            AssertError(TsvFixture.Validate(("113_god", text)), "113_god", "unlockType 2");
        }

        [Test]
        public void God_ContentOpenWithoutOpenId_IsError() =>
            AssertError(TsvFixture.Validate(("113_god", TsvFixture.Set(TsvFixture.Read("113_god"), "113001002", "openId", ""))),
                "113_god", "openId가 있어야");

        [Test]
        public void God_UnknownPassiveStat_IsError() =>
            AssertError(TsvFixture.Validate(("113_god", TsvFixture.Set(TsvFixture.Read("113_god"), "113001004", "passiveStat", "Speed"))),
                "113_god", "statKey");

        [Test]
        public void God_CardGroupWithoutCards_IsError()
        {
            var report = TsvFixture.Validate(("113_god", TsvFixture.Set(TsvFixture.Read("113_god"), "113001012", "cardGroup", "13")));
            AssertError(report, "113_god", "group 13 카드가 없습니다");
            AssertError(report, "110_card", "group 12을 cardGroup으로");
        }

        [Test]
        public void God_DuplicateCardGroup_IsError() =>
            AssertError(TsvFixture.Validate(("113_god", TsvFixture.Set(TsvFixture.Read("113_god"), "113001012", "cardGroup", "11"))),
                "113_god", "cardGroup 11를 113 행");

        [Test]
        public void Card_IdNotMatchingGroup_IsError() =>
            AssertError(TsvFixture.Validate(("110_card", TsvFixture.Append(TsvFixture.Read("110_card"),
                    R("id", "group", "key", "tier", "effectId", "sort", "active"), R("110003008", "4", "extra", "1", "112001005", "8", "0")))),
                "110_card", "형식이 아닙니다");

        [Test]
        public void LevelPrice_IdNotMatchingTargetLevel_IsError() =>
            AssertError(TsvFixture.Validate(("16_levelPrice", TsvFixture.Set(TsvFixture.Read("16_levelPrice"), "16304005", "targetLevel", "6"))),
                "16_levelPrice", "16304006이 아닙니다");

        [Test]
        public void LevelPrice_ContentGap_IsError() =>
            AssertError(TsvFixture.Validate(("16_levelPrice", TsvFixture.Remove(TsvFixture.Read("16_levelPrice"), "16301020"))),
                "16_levelPrice", "묶음 301에 targetLevel 20");

        [Test]
        public void LevelPrice_ContentValueEdited_IsWarning()
        {
            var text = TsvFixture.Set(TsvFixture.Set(TsvFixture.Read("16_levelPrice"), "16313030", "priceValue", "1"), "16301026", "needPiece", "3");
            var report = TsvFixture.Validate(("16_levelPrice", text));
            Assert.That(report.Contains(MvIssueLevel.Warning, "16_levelPrice", "id=16313030"), Is.True, report.Summary(60));
            Assert.That(report.Contains(MvIssueLevel.Warning, "16_levelPrice", "needPiece 3"), Is.True, report.Summary(60));
            Assert.That(report.Contains(MvIssueLevel.Error, "16_levelPrice", "16313030"), Is.False, report.Summary(60));
        }

        [TestCase("4006001", "children", "fx.debris/fx.dust", "기본 종류 1개만")]
        [TestCase("4006001", "children", "run.wallHit", "기본 종류여야")]
        [TestCase("4006009", "when", "shiny", "when `shiny`")]
        [TestCase("4002001", "when", "crit", "part 행에만")]
        public void EffectPart_BadChildOrWhen_IsError(string id, string column, string value, string fragment) =>
            AssertError(TsvFixture.Validate(("04_effect", TsvFixture.Set(TsvFixture.Read("04_effect"), id, column, value))), "04_effect", fragment);

        [Test]
        public void Effect_UnknownAnchor_IsError() =>
            AssertError(TsvFixture.Validate(("04_effect", TsvFixture.Set(TsvFixture.Read("04_effect"), "4003001", "anchor", "top"))),
                "04_effect", "EffectAnchor");
    }
}
