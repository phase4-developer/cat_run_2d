using System.Linq;
using NUnit.Framework;

namespace MV.Data.Tests
{
    /// <summary>400~402·410~420·499·700~702 표의 대표 값(08 예시 행)과 새 검증 규칙 양·음 사례(기획서 11 MV-DATA 2차).</summary>
    public sealed class MvModeShopTablesTests
    {
        static void AssertError(MvValidationReport report, string table, string fragment) =>
            Assert.That(report.Contains(MvIssueLevel.Error, table, fragment), Is.True, report.Summary());

        static void AssertNoError(MvValidationReport report, string table, string fragment) =>
            Assert.That(report.Contains(MvIssueLevel.Error, table, fragment), Is.False, report.Summary());

        static MvValidationReport With(string table, System.Func<string, string> edit) =>
            TsvFixture.Validate((table, edit(TsvFixture.Read(table))));

        // ---------------- 대표 값 ----------------

        [Test]
        public void Modes_RepresentativeRows_MatchSpec08()
        {
            var t = TsvFixture.LoadTables();
            Assert.That(t.Content.ByKey("main").hudId, Is.EqualTo(ContentData.DefaultHud));
            var war = t.Content.ByKey("alleyWar");
            Assert.That(war.hudId, Is.EqualTo("Defence"));
            Assert.That(war.spendResource.Single().TypeId, Is.EqualTo((int)ConsumeType.PlayResource));
            Assert.That(war.spendResource.Single().Id, Is.EqualTo(2001001));
            Assert.That(t.Content.ByKey("bigMouse").active, Is.False); // 2차.

            Assert.That(t.ModeStage.LastStage(1), Is.EqualTo(16));
            Assert.That(t.ModeStage.LastStage(2), Is.EqualTo(15));
            Assert.That(t.ModeStage.LastStage(3), Is.EqualTo(9));
            Assert.That(t.ModeStage.LastStage(4), Is.EqualTo(9));
            var last = t.ModeStage.Get(1, 16);
            Assert.That(last.enemyHp, Is.EqualTo(7600));
            Assert.That(last.boxHp, Is.EqualTo(15200));
            Assert.That(last.clearReward.Select(x => x.Value), Is.EqualTo(new double[] { 60, 3400, 1 }));
            var rush0 = t.ModeStage.Get(2, 0);
            Assert.That(rush0.floorGroup, Is.EqualTo(901));
            Assert.That(rush0.baseHp, Is.EqualTo(ModeStageData.DefaultBaseHp));
            Assert.That(t.ModeStage.Get(2, 15).hpMul, Is.EqualTo(5.2).Within(1e-9));
            Assert.That(t.ModeStage.Get(3, 9).rewardMul, Is.EqualTo(3.25).Within(1e-9));
            Assert.That(t.ModeStage.Get(4, 5).clearReward.Count, Is.EqualTo(4));

            var rule = t.ModeRule.ByKey("alleyWar");
            Assert.That(rule.cardTierWeight, Is.EqualTo(new[] { 50, 25, 15, 8, 2 }));
            Assert.That(rule.arenaLaneX, Is.EqualTo(new[] { -2.4, 0, 2.4 }));
            Assert.That(rule.fieldGenKey, Is.EqualTo(ModeRuleData.DefaultKey));
            var rapid = t.ModeRule.ByKey("rapidRush");
            Assert.That(rapid.fixedWeaponTier, Is.EqualTo(4));
            Assert.That(rapid.craftPerMade, Is.EqualTo(3));
            Assert.That(t.ModeRule.ByKey("churuRush").runRewardToAccount, Is.EqualTo(ModeRunReward.GoldDia));
            // 07 §7.9.5 츄르 털이 전용 템플릿, 08 §8.4.6 모드 Floor.
            Assert.That(t.Template.Get(310000102).endZ, Is.EqualTo(230));
            Assert.That(t.AreaFloor.GetGroup(902).Count, Is.EqualTo(3));
        }

        [Test]
        public void Quests_RepresentativeRows_MatchSpec08()
        {
            var t = TsvFixture.LoadTables();
            Assert.That(t.QuestGroup.GetGroup(106).Single().periodType, Is.EqualTo(QuestPeriodType.Event));
            var monthly = t.RepeatQuest.Get(413003004);
            Assert.That(monthly.clearType, Is.EqualTo(QuestClearType.LobbyUpgrade));
            Assert.That(monthly.clearCondition, Is.EqualTo(3));
            Assert.That(monthly.TargetAfter(2), Is.EqualTo(15));
            var pass = t.PassQuest.Get(414101010);
            Assert.That(pass.clearValue, Is.EqualTo(275));
            Assert.That(pass.reward.Single().Value, Is.EqualTo(5000));
            Assert.That(pass.premiumReward.Single().Id, Is.EqualTo(1001002));
            Assert.That(t.PassQuest.Get(414106010).clearValue, Is.EqualTo(1650));
            Assert.That(t.Achievement.Datas.Count, Is.EqualTo(19));
            var ach = t.Achievement.Get(420300005);
            Assert.That(ach.prevClearValue, Is.EqualTo(300));
            Assert.That(ach.clearValue, Is.EqualTo(1000));
        }

        [Test]
        public void ContentConfig_KeysMatchCode_AndReadValues()
        {
            var t = TsvFixture.LoadTables();
            Assert.That(t.ContentConfig.Datas.Count(x => !x.IsGroupName), Is.EqualTo(ContentConfigKeys.All.Length));
            Assert.That(t.ContentConfig.D(ContentConfigKeys.Mode_SweepChuruRatio), Is.EqualTo(0.8).Within(1e-9));
            Assert.That(t.ContentConfig.I(ContentConfigKeys.Gear_MaxUpgrade), Is.EqualTo(GearRarityData.MaxLevel));
            Assert.That(t.ContentConfig.B(ContentConfigKeys.Energy_Enabled), Is.False); // D-79
            Assert.That(t.ContentConfig.B(ContentConfigKeys.Horde_Enabled), Is.False);  // D-78
            var price = t.ContentConfig.Consume(ContentConfigKeys.Pet_SummonPrice);
            Assert.That((price.TypeId, price.Id, price.Value), Is.EqualTo((1, 1001002, 100.0)));
            Assert.That(t.ContentConfig.Has("pet"), Is.False, "묶음 이름 행은 값 키가 아니다");
        }

        [Test]
        public void Shop_RepresentativeRows_MatchSpec08()
        {
            var t = TsvFixture.LoadTables();
            var free = t.Product.Get(701001001);
            Assert.That(free.PriceType, Is.EqualTo(ConsumeType.AD));
            Assert.That(free.chargeType, Is.EqualTo(ProductChargeType.Daily));
            var noAds = t.Product.Get(701002004);
            Assert.That(noAds.chargeType, Is.EqualTo(ProductChargeType.Lifetime));
            Assert.That(noAds.reward.Single().Id, Is.EqualTo(1001017));
            var fund = t.Product.Get(701004004);
            Assert.That(fund.reward.Single().TypeId, Is.EqualTo((int)ConsumeType.Pass));
            Assert.That(fund.reward.Single().Id, Is.EqualTo(410001104));
            Assert.That(fund.active, Is.False); // 2차.
            var cheer = t.Product.Get(701005002);
            Assert.That((cheer.minArea, cheer.cooldownHours, cheer.perArea), Is.EqualTo((3, 24, true)));
            Assert.That(t.ShopSection.Get(702001001).tabIds, Is.EqualTo(new[] { 700001001, 700001002, 700001005 }));
        }

        // ---------------- 모드 규칙 ----------------

        [Test]
        public void Content_RuleClassMismatch_IsError() =>
            AssertError(With("400_content", x => TsvFixture.Set(x, "400001002", "modeRuleId", "402001001")), "400_content", "FixedLoadoutRule");

        [Test]
        public void Content_OpenIdOfOtherContent_IsError() =>
            AssertError(With("400_content", x => TsvFixture.Set(x, "400001003", "openId", "5001006")), "400_content", "contentId");

        [Test]
        public void ModeRule_UnknownRuleClass_IsError() =>
            AssertError(With("402_modeRule", x => TsvFixture.Set(x, "402001003", "ruleClass", "FooRule")), "402_modeRule", "IModeRule");

        [Test]
        public void ModeRule_TierWeightCount_IsError() =>
            AssertError(With("402_modeRule", x => TsvFixture.Set(x, "402001001", "cardTierWeight", "50/50")), "402_modeRule", "cardTierWeight");

        [Test]
        public void ModeRule_ArenaSpawnBehindLine_IsError() =>
            AssertError(With("402_modeRule", x => TsvFixture.Set(x, "402001004", "arenaSpawnZ", "0.5")), "402_modeRule", "arenaSpawnZ");

        [Test]
        public void ModeStage_DefenceWithoutSpawnMs_IsError() =>
            AssertError(With("401_modeStage", x => TsvFixture.Set(x, "401001001", "spawnMs", "")), "401_modeStage", "spawnMs");

        [Test]
        public void ModeStage_CourseWithoutTemplate_IsError() =>
            AssertError(With("401_modeStage", x => TsvFixture.Set(x, "401003001", "templateId", "")), "401_modeStage", "templateId");

        [Test]
        public void ModeStage_StageGap_IsError() =>
            AssertError(With("401_modeStage", x => TsvFixture.Remove(x, "401001005")), "401_modeStage", "0부터 연속");

        [Test]
        public void AreaFloor_ModeGroupWithFewerFloors_Passes_ButGapIsError()
        {
            var report = TsvFixture.Validate();
            Assert.That(report.Contains(MvIssueLevel.Error, "303_areaFloor", "floorGroup 901"), Is.False, report.Summary());
            AssertError(With("303_areaFloor", x => TsvFixture.Remove(x, "303902002")), "303_areaFloor", "floorGroup 902");
        }

        // ---------------- 퀘스트 ----------------

        [Test]
        public void PassQuest_StepGap_IsError() =>
            AssertError(With("414_passQuest", x => TsvFixture.Remove(x, "414101005")), "414_passQuest", "1부터 연속");

        [Test]
        public void PassQuest_ClearValueNotIncreasing_IsError() =>
            AssertError(With("414_passQuest", x => TsvFixture.Set(x, "414102003", "clearValue", "2")), "414_passQuest", "커져야");

        [Test]
        public void Achievement_PrevValueMismatch_IsError() =>
            AssertError(With("420_achievement", x => TsvFixture.Set(x, "420300002", "prevClearValue", "9")), "420_achievement", "prevClearValue");

        [Test]
        public void RepeatQuest_DailyPointOverMax_IsError() =>
            AssertError(With("413_repeatQuest", x => TsvFixture.Set(x, "413002003", "clearValue", "200")), "413_repeatQuest", "하루 최대");

        [Test]
        public void RepeatQuest_PeriodMismatch_IsError() =>
            AssertError(With("413_repeatQuest", x => TsvFixture.Set(x, "413003001", "periodType", "2")), "413_repeatQuest", "periodType");

        [Test]
        public void Quest_WrongCategoryGroup_IsError() =>
            AssertError(With("420_achievement", x => TsvFixture.Set(x, "420300001", "questGroup", "1")), "420_achievement", "category");

        [Test]
        public void Quest_UnknownClearType_IsError() =>
            AssertError(With("413_repeatQuest", x => TsvFixture.Set(x, "413001001", "clearType", "19")), "413_repeatQuest", "QuestClearType");

        // ---------------- 499 ----------------

        [Test]
        public void ContentConfig_UnknownKey_IsError()
        {
            var report = With("499_contentConfig", x => TsvFixture.Set(x, "499001001", "key", "mode.foo"));
            AssertError(report, "499_contentConfig", "ContentConfigKeys");
            AssertError(report, "499_contentConfig", ContentConfigKeys.Mode_AdTicketDaily);
        }

        [Test]
        public void ContentConfig_KeyOutsideGroup_IsError() =>
            AssertError(With("499_contentConfig", x => TsvFixture.Set(x, "499001000", "key", "modes")), "499_contentConfig", "`modes.`");

        [Test]
        public void ContentConfig_BadConsume_IsError() =>
            AssertError(With("499_contentConfig", x => TsvFixture.Set(x, "499003001", "value", "1/1001002")), "499_contentConfig", "type/id/value");

        // ---------------- 상점 ----------------

        [Test]
        public void Shop_TabInTwoSections_IsError() =>
            AssertError(With("702_shopSection", x => TsvFixture.Set(x, "702001002", "tabIds", "700001003/700001001")), "702_shopSection", "700001001");

        [Test]
        public void Shop_LimitWithoutQuantity_IsError() =>
            AssertError(With("701_product", x => TsvFixture.Set(x, "701002004", "chargedQuantity", "0")), "701_product", "chargedQuantity");

        [Test]
        public void Shop_PriceTypeNotAllowed_IsError()
        {
            var text = TsvFixture.Set(TsvFixture.Read("701_product"), "701006001", "priceType", "3");
            text = TsvFixture.Set(text, "701006001", "priceId", "2001001");
            AssertError(TsvFixture.Validate(("701_product", text)), "701_product", "priceType 3");
        }

        [Test]
        public void Shop_Sku_NeedsPrefixOnInapp()
        {
            AssertNoError(With("701_product", x => TsvFixture.Set(x, "701002001", "googleProductId", "mv_dia_100")), "701_product", "SKU");
            AssertError(With("701_product", x => TsvFixture.Set(x, "701002001", "googleProductId", "dia_100")), "701_product", "SKU");
            AssertError(With("701_product", x => TsvFixture.Set(x, "701006001", "appleProductId", "mv_x")), "701_product", "SKU");
        }

        [Test]
        public void Shop_PassRewardMustBeQuestGroupRow() =>
            AssertError(With("701_product", x => TsvFixture.Set(x, "701004001", "rewardId", "101")), "701_product", "410_questGroup");

        // ---------------- 변형 소리 `_n` (07 §7.13.2) ----------------

        [Test]
        public void Sound_VariantKey_n_ChecksNumberedKeys()
        {
            AssertNoError(With("115_cardTier", x => TsvFixture.Set(x, "115001001", "cardSound", "card_n")), "115_cardTier", "card_n");
            AssertError(With("115_cardTier", x => TsvFixture.Set(x, "115001001", "cardSound", "wood_n")), "115_cardTier", "wood_");
        }
    }
}
