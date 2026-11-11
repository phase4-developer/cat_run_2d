using System;
using System.Linq;
using NUnit.Framework;

namespace MV.Data.Tests
{
    /// <summary>4xx 메타 시스템 표(08 §8.5, §8.3.5) 대표 값과 의미 규칙 양·음 사례.</summary>
    public sealed class MvMetaTablesTests
    {
        static readonly Lazy<MvValidationReport> s_base = new Lazy<MvValidationReport>(() => TsvFixture.Validate());
        static readonly Lazy<MvTables> s_tables = new Lazy<MvTables>(() => TsvFixture.LoadTables());

        /// <summary>저장소 TSV 그대로의 검증 결과(양 사례 기준).</summary>
        static MvValidationReport Base => s_base.Value;
        static MvTables T => s_tables.Value;

        static void AssertError(MvValidationReport report, string table, string fragment) =>
            Assert.That(report.Contains(MvIssueLevel.Error, table, fragment), Is.True,
                $"[{table}] `{fragment}` 오류가 없습니다.\n{report.Summary(60)}");

        static void AssertNoError(MvValidationReport report, string table, string fragment) =>
            Assert.That(report.Contains(MvIssueLevel.Error, table, fragment), Is.False,
                $"[{table}] `{fragment}` 오류가 있으면 안 됩니다.\n{report.Summary(60)}");

        static void AssertWarning(MvValidationReport report, string table, string fragment) =>
            Assert.That(report.Contains(MvIssueLevel.Warning, table, fragment), Is.True,
                $"[{table}] `{fragment}` 경고가 없습니다.\n{report.Summary(60)}");

        static void AssertNoWarning(MvValidationReport report, string table, string fragment) =>
            Assert.That(report.Contains(MvIssueLevel.Warning, table, fragment), Is.False,
                $"[{table}] `{fragment}` 경고가 있으면 안 됩니다.\n{report.Summary(60)}");

        static string R(params string[] cells) => string.Join("\t", cells);

        static string Set(string table, string id, string column, string value) => TsvFixture.Set(TsvFixture.Read(table), id, column, value);

        /// <summary>16_levelPrice에 시험용 묶음(targetLevel from~to)을 붙인다.</summary>
        static string WithPriceGroup(int group, int from, int to)
        {
            var text = TsvFixture.Read(LevelPriceData.Table);
            var header = R("id", "group", "targetLevel", "priceType", "priceId", "priceValue");
            for (int lv = from; lv <= to; lv++)
                text = TsvFixture.Append(text, header, R($"16{group}{lv:000}", group.ToString(), lv.ToString(), "1", "1001001", "1"));
            return text;
        }

        // ================= 대표 값(08 예시 행) =================

        [Test]
        public void Pet_RepresentativeRows()
        {
            Assert.That(T.Pet.Datas.Count(), Is.EqualTo(16));
            var p = T.Pet.ByKey("moonRabbit");
            Assert.That(p.id, Is.EqualTo(430001015));
            Assert.That(p.rarity, Is.EqualTo(4));
            Assert.That(p.statKey, Is.EqualTo(MetaStat.CritDmg));
            Assert.That(p.value, Is.EqualTo(60));
            Assert.That(p.skillEveryMs, Is.EqualTo(1200));
            Assert.That(p.SkillPctAt(2), Is.EqualTo(103));
            Assert.That(p.levelPriceGroup, Is.EqualTo(301));
            Assert.That(p.icon, Is.EqualTo("UI_Icon_Pet_moonRabbit"));
            Assert.That(T.Pet.ByKey("sparrow").skillDmgPct, Is.EqualTo(20));

            var b = T.PetBuild.Get(431004003);
            Assert.That(b.tier, Is.EqualTo(3));
            Assert.That(b.tierRate, Is.EqualTo(500));
            Assert.That(b.statKey, Is.EqualTo(MetaStat.CritDmg));
            Assert.That((b.valueMin, b.valueMax), Is.EqualTo((25, 50)));
            Assert.That(T.PetBuild.Datas.Count(), Is.EqualTo(16));
        }

        [Test]
        public void Research_RepresentativeRows()
        {
            Assert.That(T.Research.Datas.Count(), Is.EqualTo(25));
            var first = T.Research.Get(1, 1);
            Assert.That(first.id, Is.EqualTo(440001001));
            Assert.That(first.icon, Is.EqualTo("UI_Icon_Research_claw"));
            Assert.That(first.statType, Is.EqualTo(MetaStat.Dmg));
            Assert.That(first.statValue, Is.EqualTo(2));
            Assert.That(first.price.Count, Is.EqualTo(1));
            Assert.That(first.price[0].Type, Is.EqualTo(ConsumeType.Wealth));
            Assert.That(first.price[0].Id, Is.EqualTo(1001002));
            Assert.That(first.price[0].Value, Is.EqualTo(10));
            Assert.That(first.seconds, Is.EqualTo(50));

            var last = T.Research.Get(5, 5);
            Assert.That(last.statType, Is.EqualTo(MetaStat.Gold));
            Assert.That(last.statValue, Is.EqualTo(25));
            Assert.That(last.price[0].Value, Is.EqualTo(50));
            Assert.That(last.seconds, Is.EqualTo(4800));
            Assert.That(T.Research.Get(5, 6), Is.Null);
        }

        [Test]
        public void Friend_RepresentativeRows()
        {
            Assert.That(T.Friend.Datas.Count(), Is.EqualTo(10));
            var f = T.Friend.ByKey("boss");
            Assert.That(f.Trigger, Is.EqualTo(FriendSkillTrigger.Seconds));
            Assert.That((f.skillEvery, f.skillDmgPct, f.skillTargets), Is.EqualTo((6000, 250.0, 3)));
            Assert.That(f.maxLevel, Is.EqualTo(30));
            Assert.That(f.unlockPrice[0].Id, Is.EqualTo(1001010));
            Assert.That(f.unlockPrice[0].Value, Is.EqualTo(150));
            Assert.That(f.ValueAt(30), Is.EqualTo(15 + 3 * 29));
            Assert.That(f.levelPriceGroup, Is.EqualTo(302));
            Assert.That(T.Friend.ByKey("calico").Trigger, Is.EqualTo(FriendSkillTrigger.Chance));

            var power = T.FriendPower.Get(451001002);
            Assert.That(power.unlockLevel, Is.EqualTo(10));
            Assert.That(power.price[0].Id, Is.EqualTo(1001011));
            Assert.That(power.price[0].Value, Is.EqualTo(300));
            Assert.That((power.statKey, power.value), Is.EqualTo((MetaStat.Dmg, 10.0)));
        }

        [Test]
        public void RuneAndGear_RepresentativeRows()
        {
            var rune = T.Rune.Get(460001004);
            Assert.That(rune.color, Is.EqualTo("#ffd23a"));
            Assert.That(rune.randomBoxRowId, Is.EqualTo(802460004));

            Assert.That(T.GearPart.Datas.Count(), Is.EqualTo(7));
            Assert.That(T.GearPart.ByKey("cape").statKey, Is.EqualTo(MetaStat.DoubleChance));

            var g = T.GearRarity.Get(471001008);
            Assert.That(g.rarity, Is.EqualTo(7));
            Assert.That(g.color, Is.EqualTo("#ffd23a"));
            Assert.That(g.value, Is.EqualTo(80));
            Assert.That(g.ValueAt(30), Is.EqualTo(110));
            Assert.That(g.sellPrice[0].Id, Is.EqualTo(1001001));
            Assert.That(g.sellPrice[0].Value, Is.EqualTo(25000));
            Assert.That(g.levelPriceGroup, Is.EqualTo(313));
        }

        [Test]
        public void BlessingAttendanceRoadMapGodLevel_RepresentativeRows()
        {
            var energy = T.Blessing.ByKey("energy");
            Assert.That(energy.EffectType, Is.EqualTo(BlessingEffectType.Stamina));
            Assert.That(energy.active, Is.False);
            var rebirth = T.Blessing.ByKey("rebirth");
            Assert.That((rebirth.value1, rebirth.value2, rebirth.adRuns), Is.EqualTo((20.0, -20.0, 10)));
            Assert.That(T.Blessing.ByKey("gear").active, Is.True);

            var day6 = T.Attendance.Datas.Single(a => a.day == 6);
            Assert.That(day6.reward.Select(x => x.Type), Is.All.EqualTo(ConsumeType.PlayResource));
            Assert.That(day6.reward.Select(x => x.Id), Is.EqualTo(new[] { 2001001, 2001002, 2001003 }));
            var day7 = T.Attendance.Datas.Single(a => a.day == 7);
            Assert.That(day7.reward.Count, Is.EqualTo(2));
            Assert.That((day7.reward[1].Id, day7.reward[1].Value), Is.EqualTo((1001015, 1.0)));

            Assert.That(T.RoadMap.Datas.Count(), Is.EqualTo(6));
            Assert.That(T.RoadMap.Datas.All(x => !x.active), Is.True);
            var road = T.RoadMap.Get(490001003);
            Assert.That((road.zoneId, road.gainPerRun, road.maxRange, road.catchUpGain, road.startGap),
                Is.EqualTo((301001003, 5, 100, 15, 30)));

            var god = T.GodLevel.Get(495001004);
            Assert.That((god.level, god.dmgPct, god.passiveMul, god.levelPriceGroup), Is.EqualTo((4, 35, 4, 304)));
        }

        // ================= 16_levelPrice 범위 =================

        [TestCase(FriendData.Table, "450001001", 2, 30)]
        [TestCase(GearRarityData.Table, "471001001", 1, 30)]
        [TestCase(GodLevelData.Table, "495001001", 2, 5)]
        public void LevelPriceGroup_MustCoverAllTargetLevels(string table, string id, int from, int to)
        {
            var row = Set(table, id, "levelPriceGroup", "399");
            AssertNoError(TsvFixture.Validate((LevelPriceData.Table, WithPriceGroup(399, from, to)), (table, row)), table, "묶음 399에 targetLevel");
            AssertError(TsvFixture.Validate((LevelPriceData.Table, WithPriceGroup(399, from, to - 1)), (table, row)), table,
                $"묶음 399에 targetLevel {to}가 없습니다");
        }

        [Test]
        public void Pet_LevelPriceGroup_GapIsError()
        {
            // 펫 최대 Lv은 표 열이 없어 16 묶음 targetLevel 최댓값으로 정한다. 2~최댓값 사이 빈틈은 오류.
            var row = Set(PetData.Table, "430001001", "levelPriceGroup", "399");
            AssertNoError(TsvFixture.Validate((LevelPriceData.Table, WithPriceGroup(399, 2, 40)), (PetData.Table, row)), PetData.Table, "묶음 399에 targetLevel");
            var gap = TsvFixture.Remove(WithPriceGroup(399, 2, 40), "16399020");
            AssertError(TsvFixture.Validate((LevelPriceData.Table, gap), (PetData.Table, row)), PetData.Table, "묶음 399에 targetLevel 20가 없습니다");
        }

        // ================= 430·431 =================

        [Test]
        public void Pet_BadValues_AreErrors()
        {
            foreach (var f in new[] { "rarity", "skillEveryMs", "skillTargets" }) AssertNoError(Base, PetData.Table, f);
            var text = TsvFixture.Read(PetData.Table);
            text = TsvFixture.Set(text, "430001001", "rarity", "5");
            text = TsvFixture.Set(text, "430001002", "skillEveryMs", "0");
            text = TsvFixture.Set(text, "430001003", "skillTargets", "0");
            var report = TsvFixture.Validate((PetData.Table, text));
            AssertError(report, PetData.Table, "rarity 5는 0~4");
            AssertError(report, PetData.Table, "skillEveryMs는 1 이상");
            AssertError(report, PetData.Table, "skillTargets는 1 이상");
        }

        [Test]
        public void PetBuild_BadValues_AreErrors()
        {
            foreach (var f in new[] { "tier", "valueMin", "tierRate", "statKey" }) AssertNoError(Base, PetBuildData.Table, f);
            AssertNoWarning(Base, PetBuildData.Table, "tierRate 합");
            var text = TsvFixture.Read(PetBuildData.Table);
            text = TsvFixture.Set(text, "431001002", "tierRate", "4000");
            text = TsvFixture.Set(text, "431002001", "valueMin", "99");
            text = TsvFixture.Set(text, "431002003", "statKey", "CritChance");
            foreach (var id in new[] { "431004001", "431004002", "431004003", "431004004" }) text = TsvFixture.Remove(text, id);
            var report = TsvFixture.Validate((PetBuildData.Table, text));
            AssertError(report, PetBuildData.Table, "tierRate가 행마다 다릅니다");
            AssertError(report, PetBuildData.Table, "valueMin ≤ valueMax");
            AssertError(report, PetBuildData.Table, "statKey CritChance가 두 번");
            AssertError(report, PetBuildData.Table, "tier 3 행이 없습니다");
        }

        [Test]
        public void PetBuild_TierOutOfRange_IsError_AndRateSumWarns()
        {
            AssertError(TsvFixture.Validate((PetBuildData.Table, Set(PetBuildData.Table, "431001001", "tier", "5"))),
                PetBuildData.Table, "tier 5는 0~3");

            var text = TsvFixture.Read(PetBuildData.Table);
            foreach (var id in new[] { "431001001", "431001002", "431001003", "431001004" }) text = TsvFixture.Set(text, id, "tierRate", "5001");
            var report = TsvFixture.Validate((PetBuildData.Table, text));
            AssertWarning(report, PetBuildData.Table, "tierRate 합이 10001");
            AssertNoError(report, PetBuildData.Table, "tierRate");
        }

        // ================= 440 =================

        [Test]
        public void Research_BadValues_AreErrors()
        {
            foreach (var f in new[] { "1부터 연속", "statType이 행마다", "누적값", "seconds" }) AssertNoError(Base, ResearchData.Table, f);
            var text = TsvFixture.Read(ResearchData.Table);
            text = TsvFixture.Set(text, "440001003", "level", "7");
            text = TsvFixture.Set(text, "440002002", "statType", "Gold");
            text = TsvFixture.Set(text, "440003003", "statValue", "1");
            text = TsvFixture.Set(text, "440004001", "seconds", "0");
            var report = TsvFixture.Validate((ResearchData.Table, text));
            AssertError(report, ResearchData.Table, "계열 1의 level은 1부터 연속");
            AssertError(report, ResearchData.Table, "계열 2의 statType이 행마다 다릅니다");
            AssertError(report, ResearchData.Table, "누적값");
            AssertError(report, ResearchData.Table, "seconds는 1 이상");
        }

        // ================= 450·451 =================

        [Test]
        public void Friend_BadValues_AreErrors()
        {
            foreach (var f in new[] { "skillTrigger", "100 이하", "skillTargets", "maxLevel", "영입 비용", "rarity" }) AssertNoError(Base, FriendData.Table, f);
            var text = TsvFixture.Read(FriendData.Table);
            text = TsvFixture.Set(text, "450001001", "skillTrigger", "4");
            text = TsvFixture.Set(text, "450001003", "skillEvery", "150");
            text = TsvFixture.Set(text, "450001004", "skillTargets", "0");
            text = TsvFixture.Set(text, "450001005", "maxLevel", "0");
            foreach (var c in new[] { "unlockPriceType", "unlockPriceId", "unlockPriceValue" }) text = TsvFixture.Set(text, "450001006", c, "");
            text = TsvFixture.Set(text, "450001007", "rarity", "-1");
            var report = TsvFixture.Validate((FriendData.Table, text));
            AssertError(report, FriendData.Table, "skillTrigger 4는");
            AssertError(report, FriendData.Table, "100 이하");
            AssertError(report, FriendData.Table, "skillTargets는 1 이상");
            AssertError(report, FriendData.Table, "maxLevel은 1 이상");
            AssertError(report, FriendData.Table, "영입 비용");
            AssertError(report, FriendData.Table, "rarity는 0 이상");
        }

        [Test]
        public void FriendPower_BadValues_AreErrors()
        {
            foreach (var f in new[] { "unlockLevel", "구매 비용" }) AssertNoError(Base, FriendPowerData.Table, f);
            AssertError(TsvFixture.Validate((FriendPowerData.Table, Set(FriendPowerData.Table, "451001003", "unlockLevel", "31"))),
                FriendPowerData.Table, "unlockLevel 31는 1~30");
            AssertError(TsvFixture.Validate((FriendPowerData.Table, Set(FriendPowerData.Table, "451001002", "unlockLevel", "5"))),
                FriendPowerData.Table, "unlockLevel이 커져야");

            var text = TsvFixture.Read(FriendPowerData.Table);
            foreach (var c in new[] { "priceType", "priceId", "priceValue" }) text = TsvFixture.Set(text, "451001001", c, "");
            AssertError(TsvFixture.Validate((FriendPowerData.Table, text)), FriendPowerData.Table, "구매 비용");
        }

        // ================= 460 =================

        [Test]
        public void Rune_BoxRowMustBePass()
        {
            const string boxHeader = "id\tgroup\tsubGroup\trewardType\trewardId\trewardValue_min\trewardValue_max\tweight";
            var box = TsvFixture.Append(TsvFixture.Read(RandomBoxData.Table), boxHeader, R("802999001", "802999001", "1", "500", "0", "20", "20", "1"));
            box = TsvFixture.Append(box, boxHeader, R("802999002", "802999002", "1", "1", "1001001", "5", "5", "1"));

            var pass = TsvFixture.Validate((RandomBoxData.Table, box), (RuneData.Table, Set(RuneData.Table, "460001001", "randomBoxRowId", "802999001")));
            AssertNoError(pass, RuneData.Table, "500(Pass)");

            var gold = TsvFixture.Validate((RandomBoxData.Table, box), (RuneData.Table, Set(RuneData.Table, "460001001", "randomBoxRowId", "802999002")));
            AssertError(gold, RuneData.Table, "802 행 802999002의 rewardType 1가 500(Pass)이 아닙니다");
        }

        // ================= 470·471 =================

        [Test]
        public void GearPart_DuplicateStat_Warns()
        {
            AssertNoWarning(Base, GearPartData.Table, "부위 여러 개");
            AssertWarning(TsvFixture.Validate((GearPartData.Table, Set(GearPartData.Table, "470001002", "statKey", "Dmg"))),
                GearPartData.Table, "statKey Dmg를 부위 여러 개가");
        }

        [Test]
        public void GearRarity_BadValues_AreErrors()
        {
            AssertNoError(Base, GearRarityData.Table, "rarity");
            AssertNoError(Base, GearRarityData.Table, "판매 가격");
            AssertNoWarning(Base, GearRarityData.Table, "커지지 않습니다");

            AssertError(TsvFixture.Validate((GearRarityData.Table, Set(GearRarityData.Table, "471001003", "rarity", "9"))),
                GearRarityData.Table, "rarity는 0부터 연속");
            AssertWarning(TsvFixture.Validate((GearRarityData.Table, Set(GearRarityData.Table, "471001002", "value", "1"))),
                GearRarityData.Table, "커지지 않습니다");

            var text = TsvFixture.Read(GearRarityData.Table);
            foreach (var c in new[] { "sellPriceType", "sellPriceId", "sellPriceValue" }) text = TsvFixture.Set(text, "471001001", c, "");
            AssertError(TsvFixture.Validate((GearRarityData.Table, text)), GearRarityData.Table, "판매 가격");
        }

        // ================= 480 =================

        [Test]
        public void Blessing_BadValues_AreErrors()
        {
            foreach (var f in new[] { "effectType", "adRuns", "D-79" }) AssertNoError(Base, BlessingData.Table, f);
            var text = TsvFixture.Read(BlessingData.Table);
            text = TsvFixture.Set(text, "480001001", "effectType", "5");
            text = TsvFixture.Set(text, "480001002", "adRuns", "0");
            var report = TsvFixture.Validate((BlessingData.Table, text));
            AssertError(report, BlessingData.Table, "effectType 5는");
            AssertError(report, BlessingData.Table, "adRuns는 1 이상");
        }

        [Test]
        public void Blessing_StaminaOnWithoutStamina_IsError()
        {
            var on = Set(BlessingData.Table, "480001003", "active", "1");
            AssertError(TsvFixture.Validate((BlessingData.Table, on)), BlessingData.Table, "D-79");

            var stamina = Set(PlayResourceData.Table, "2001005", "active", "1");
            AssertNoError(TsvFixture.Validate((BlessingData.Table, on), (PlayResourceData.Table, stamina)), BlessingData.Table, "D-79");
        }

        // ================= 485 =================

        [Test]
        public void Attendance_Days_MustBeOneToSeven()
        {
            foreach (var f in new[] { "day", "보상이 비었습니다" }) AssertNoError(Base, AttendanceData.Table, f);
            AssertError(TsvFixture.Validate((AttendanceData.Table, TsvFixture.Remove(TsvFixture.Read(AttendanceData.Table), "485001003"))),
                AttendanceData.Table, "day 3 행이 없습니다");
            AssertError(TsvFixture.Validate((AttendanceData.Table, Set(AttendanceData.Table, "485001004", "day", "3"))),
                AttendanceData.Table, "day 3가 중복됩니다");
            // 한 바퀴 일수 = day 최댓값(표 행으로 계산). 7 → 8로 바꾸면 day 7이 빈다.
            AssertError(TsvFixture.Validate((AttendanceData.Table, Set(AttendanceData.Table, "485001007", "day", "8"))),
                AttendanceData.Table, "day 7 행이 없습니다");

            var text = TsvFixture.Read(AttendanceData.Table);
            foreach (var c in new[] { "rewardType", "rewardId", "rewardValue" }) text = TsvFixture.Set(text, "485001001", c, "");
            AssertError(TsvFixture.Validate((AttendanceData.Table, text)), AttendanceData.Table, "보상이 비었습니다");
        }

        // ================= 490 =================

        [Test]
        public void RoadMap_BadValues_AreErrors()
        {
            foreach (var f in new[] { "zoneId", "gainPerRun", "maxRange", "catchUpGain", "startGap" }) AssertNoError(Base, RoadMapData.Table, f);
            AssertNoWarning(Base, RoadMapData.Table, "행이 없습니다");

            var dup = TsvFixture.Validate((RoadMapData.Table, Set(RoadMapData.Table, "490001002", "zoneId", "301001001")));
            AssertError(dup, RoadMapData.Table, "zoneId 301001001 행이 두 개");
            AssertWarning(dup, RoadMapData.Table, "Zone 301001002 행이 없습니다");

            var text = TsvFixture.Read(RoadMapData.Table);
            text = TsvFixture.Set(text, "490001001", "maxRange", "0");
            text = TsvFixture.Set(text, "490001002", "gainPerRun", "-1");
            text = TsvFixture.Set(text, "490001003", "catchUpGain", "-1");
            text = TsvFixture.Set(text, "490001004", "startGap", "-1");
            var report = TsvFixture.Validate((RoadMapData.Table, text));
            AssertError(report, RoadMapData.Table, "maxRange는 1 이상");
            AssertError(report, RoadMapData.Table, "gainPerRun은 0 이상");
            AssertError(report, RoadMapData.Table, "catchUpGain은 0 이상");
            AssertError(report, RoadMapData.Table, "startGap은 0 이상");
        }

        // ================= 495 =================

        [Test]
        public void GodLevel_BadValues_AreErrors()
        {
            foreach (var f in new[] { "level은 1부터", "passiveMul" }) AssertNoError(Base, GodLevelData.Table, f);
            AssertNoWarning(Base, GodLevelData.Table, "dmgPct");

            AssertError(TsvFixture.Validate((GodLevelData.Table, Set(GodLevelData.Table, "495001003", "level", "9"))),
                GodLevelData.Table, "level은 1부터 연속");
            AssertError(TsvFixture.Validate((GodLevelData.Table, Set(GodLevelData.Table, "495001002", "passiveMul", "-1"))),
                GodLevelData.Table, "passiveMul은 0 이상");
            AssertWarning(TsvFixture.Validate((GodLevelData.Table, Set(GodLevelData.Table, "495001003", "dmgPct", "5"))),
                GodLevelData.Table, "dmgPct가 레벨 순으로 줄어듭니다");
        }
    }
}
