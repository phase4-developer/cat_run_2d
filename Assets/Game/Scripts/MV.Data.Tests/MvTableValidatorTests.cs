using System.Linq;
using NUnit.Framework;

namespace MV.Data.Tests
{
    /// <summary>게임 검증기 양·음 사례(기획서 07 §7.13, §7.14).</summary>
    public sealed class MvTableValidatorTests
    {
        static void AssertNoErrors(MvValidationReport report) =>
            Assert.That(report.HasError, Is.False, report.Summary(60));

        static void AssertError(MvValidationReport report, string table, string fragment) =>
            Assert.That(report.Contains(MvIssueLevel.Error, table, fragment), Is.True,
                $"[{table}] `{fragment}` 오류가 없습니다.\n{report.Summary(60)}");

        static string R(params string[] cells) => string.Join("\t", cells);

        [Test]
        public void RepositoryTsv_HasNoErrors()
        {
            var report = TsvFixture.Validate();
            AssertNoErrors(report);
            TestContext.WriteLine(report.Summary(200));
        }

        [Test]
        public void RuntimeValidation_OnLoadedTables_HasNoErrors()
        {
            AssertNoErrors(MvTableValidator.ValidateRuntime(TsvFixture.LoadTables()));
        }

        // ---------------- 7.14 확장 예시 ----------------

        [Test]
        public void Extension_MouseHoleTrap_Passes()
        {
            var hp = TsvFixture.Append(TsvFixture.Read("114_hpRule"),
                R("id", "key", "type", "mul", "fixed", "useScale", "useObstMul"),
                R("114001008", "trap03", "AreaHpMul", "0.3", "", "0", "0"));
            var move = TsvFixture.Append(TsvFixture.Read("109_moveAI"),
                R("id", "key", "type", "aggro", "speedMin", "speedMax", "xFollow", "touchX", "touchZ", "touchAction", "stealBase", "stealPerZone", "stealShake", "stealSound", "stealEffect", "despawnBehind"),
                R("109001004", "mouseHole", "Static", "", "", "", "", "0.9", "0.5", "Steal", "3", "2", "shake.steal", "steal", "run.steal", "0"));
            var kind = TsvFixture.Append(TsvFixture.Read("105_objectKind"),
                R("id", "key", "sort", "active", "hw", "hd", "hittable", "breakable", "blocks", "hpRuleId", "hitReactionId", "chargeGateId", "receiveId", "moveAiId", "enemyId", "rewardSource", "rewardType", "rewardId", "rewardValue", "rewardValuePerZone", "rewardValueRule", "rewardShares", "sprite", "spritePick", "labelKind", "hitSound", "breakSound", "hitEffect", "breakEffect"),
                R("105001014", "mouseHole", "14", "1", "0.8", "0.5", "Always", "1", "0", "114001008", "106001004", "0", "108001001", "109001004", "0", "Row", "602", "0", "5", "3", "Flat", "1", "World_Obj_Trap_MouseHole", "Single", "BoxHp", "", "wood", "run.coinHit", "run.wallBreak"));
            var placed = TsvFixture.Append(TsvFixture.Read("311_templateObject"),
                R("id", "templateId", "seq", "kindId", "x", "z", "groupId", "active", "#kind"),
                R("311003012", "310000003", "12", "105001014", "0", "120", "0", "1", "mouseHole"));

            var report = TsvFixture.Validate(("114_hpRule", hp), ("109_moveAI", move), ("105_objectKind", kind), ("311_templateObject", placed));
            AssertNoErrors(report);

            var tables = new MvTables();
            var source = new MvMemoryTableSource(TsvFixture.Source()).Set("114_hpRule", hp).Set("109_moveAI", move)
                .Set("105_objectKind", kind).Set("311_templateObject", placed);
            tables.LoadAll(source);
            var row = tables.ObjectKind.ByKey("mouseHole");
            Assert.That(row.reward[0].Type, Is.EqualTo(ConsumeType.RunGold));
            Assert.That(row.reward[0].ValueAtZone(2), Is.EqualTo(5 + 3 * 2));
            Assert.That(tables.MoveAi.Get(row.moveAiId).touchAction, Is.EqualTo(TouchAction.Steal));
        }

        [Test]
        public void Extension_Area31_Passes()
        {
            var area = TsvFixture.Append(TsvFixture.Read("302_area"),
                R("id", "areaNo", "zoneId", "sort", "active", "themeId", "hp", "needSouls", "floorGroup", "bgm", "mapIcon", "clearRewardType", "clearRewardId", "clearRewardValue"),
                R("302001031", "31", "301001006", "31", "1", "304001015", "69000", "1755", "31", "", "", "", "", ""));
            var floorHeader = R("id", "group", "floor", "h0", "h1", "rows", "souls", "cellMode");
            var floor = TsvFixture.Read("303_areaFloor");
            floor = TsvFixture.Append(floor, floorHeader, R("303031001", "31", "1", "3700", "1200000", "14", "18", "Hash"));
            floor = TsvFixture.Append(floor, floorHeader, R("303031002", "31", "2", "1200000", "12000000", "16", "23", "Hash"));
            floor = TsvFixture.Append(floor, floorHeader, R("303031003", "31", "3", "12000000", "24000000", "18", "29", "Hash"));
            // Zone 범위는 302 zoneId로 계산하므로 301은 고치지 않는다(옛 areaTo 열 삭제).
            // PLAYABLE_AREAS는 그대로 둔다: Area 17~30이 active 0(D-71 대기)이라 31로 올리면 `≤ active Area 수` 규칙에 걸린다.
            AssertNoErrors(TsvFixture.Validate(("302_area", area), ("303_areaFloor", floor)));
        }

        [Test]
        public void Extension_Area31_InEarlierZone_ReportsZoneOrder()
        {
            // Zone은 301 sort 순으로 Area를 이어서 덮는다. 마지막 Area를 첫 Zone에 넣으면 Zone 범위가 겹친다.
            var area = TsvFixture.Append(TsvFixture.Read("302_area"),
                R("id", "areaNo", "zoneId", "sort", "active", "themeId", "hp", "needSouls", "floorGroup"),
                R("302001031", "31", "301001001", "31", "1", "304001015", "69000", "1755", "1"));
            AssertError(TsvFixture.Validate(("302_area", area)), "302_area", "301 sort 순으로 이어지지 않습니다");
        }

        // ---------------- 305 ----------------

        [Test]
        public void StageObject_UnknownStageMap_IsError()
        {
            var rows = TsvFixture.Append(TsvFixture.Read("305_stageObject"),
                R("id", "stageMap", "kind", "reward", "prefab"), R("305009001", "Stage99", "arch", "", "Stage1_Arch"));
            AssertError(TsvFixture.Validate(("305_stageObject", rows)), "305_stageObject", "302_area.stageMap에 없는 맵");
        }

        [Test]
        public void StageObject_SameMapKindReward_IsError()
        {
            var rows = TsvFixture.Append(TsvFixture.Read("305_stageObject"),
                R("id", "stageMap", "kind", "reward", "prefab"), R("305009001", "Stage1", "wall", "Gold", "Stage1_Block_Blue"));
            AssertError(TsvFixture.Validate(("305_stageObject", rows)), "305_stageObject", "두 번 있습니다");
        }

        [Test]
        public void StageObject_Find_PrefersRewardRowThenAnyRow()
        {
            var rows = TsvFixture.LoadTables().StageObject.Datas;
            Assert.That(StageObjectData.Find(rows, "Stage1", "wall", StageObjectReward.Dia), Is.EqualTo("Stage1_Block_Blue"));
            Assert.That(StageObjectData.Find(rows, "Stage1", "arch", StageObjectReward.Gold), Is.EqualTo("Stage1_Arch"));
            Assert.That(StageObjectData.Find(rows, "Stage1", "tower", StageObjectReward.Any), Is.Null);
            Assert.That(StageObjectData.Find(rows, "Stage2", "arch", StageObjectReward.Any), Is.Null);
        }

        // ---------------- 형식·참조 ----------------

        /// <summary>사람용 name 열(07 §7.1.1a, JJEvol 표 규격 v1.1 §3.1): 없음·키 열 다음이 아님·빈칸은 오류, 있으면 모르는 열 경고 없음.</summary>
        [TestCase("missing", "사람용 name 열이 없습니다")]
        [TestCase("moved", "키 열 `id` 바로 다음")]
        [TestCase("empty", "사람용 name 값이 비었습니다")]
        public void Format_HumanNameColumn_IsRequired(string broken, string fragment)
        {
            var good = TsvFixture.Read("11_stairLevel");
            Assert.That(TsvFixture.Validate(("11_stairLevel", good)).Contains(MvIssueLevel.Warning, "11_stairLevel", "열 정의에 없는 열"), Is.False);
            var lines = good.Replace("\r\n", "\n").TrimEnd('\n').Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                var c = lines[i].Split('\t').ToList();
                if (broken == "missing") c.RemoveAt(1);
                else if (broken == "moved") { var n = c[1]; c.RemoveAt(1); c.Add(n); }
                else if (i == 1) c[1] = "";
                lines[i] = string.Join("\t", c);
            }
            AssertError(TsvFixture.Validate(("11_stairLevel", string.Join("\n", lines) + "\n")), "11_stairLevel", fragment);
        }

        [Test]
        public void Format_BoolNotZeroOne_IsError() =>
            AssertError(TsvFixture.Validate(("10_lobbyUpgrade", TsvFixture.Set(TsvFixture.Read("10_lobbyUpgrade"), "10001001", "active", "2"))),
                "10_lobbyUpgrade", "0/1이 아닙니다");

        [Test]
        public void Format_IntWithText_IsError() =>
            AssertError(TsvFixture.Validate(("11_stairLevel", TsvFixture.Set(TsvFixture.Read("11_stairLevel"), "11001001", "need", "1.5"))),
                "11_stairLevel", "정수가 아닙니다");

        [Test]
        public void Format_IdPrefix_IsError()
        {
            var text = TsvFixture.Set(TsvFixture.Read("320_camera"), TsvFixture.Read("320_camera").Split('\n')[1].Split('\t')[0], "id", "999000001");
            AssertError(TsvFixture.Validate(("320_camera", text)), "320_camera", "표 번호");
        }

        [Test]
        public void Format_IdNotTableTimesMillion_IsError()
        {
            // 접두 `13`은 맞지만 id / 1,000,000 = 130 ≠ 13(07 §7.1.1a id 입력 열).
            AssertError(TsvFixture.Validate(("13_areaUpgrade", TsvFixture.Set(TsvFixture.Read("13_areaUpgrade"), "13001001", "id", "130001001"))),
                "13_areaUpgrade", "1,000,000");
            AssertError(TsvFixture.Validate(("02_playResource", TsvFixture.Set(TsvFixture.Read("02_playResource"), "2001001", "id", "101"))),
                "02_playResource", "1,000,000");
        }

        [Test]
        public void Format_TwoDefaultPresets_IsError()
        {
            var text = TsvFixture.Read("06_balancePreset");
            foreach (var line in text.Split('\n').Skip(1).Where(l => l.Length > 0))
                text = TsvFixture.Set(text, line.Split('\t')[0], "isDefault", "1");
            AssertError(TsvFixture.Validate(("06_balancePreset", text)), "06_balancePreset", "isDefault");
        }

        [Test]
        public void Reference_MissingFk_IsError() =>
            AssertError(TsvFixture.Validate(("12_weaponLevel", TsvFixture.Set(TsvFixture.Read("12_weaponLevel"), "12001001", "itemId", "103999999"))),
                "12_weaponLevel", "103_item에 id 103999999가 없습니다");

        [Test]
        public void Reference_UnknownConsumeType_IsError() =>
            AssertError(TsvFixture.Validate(("16_levelPrice", TsvFixture.Set(TsvFixture.Read("16_levelPrice"), "16001001", "priceType", "77"))),
                "16_levelPrice", "consumeType");

        [Test]
        public void Reference_RunConsumeOutsideRunTable_IsError() =>
            AssertError(TsvFixture.Validate(("16_levelPrice", TsvFixture.Set(TsvFixture.Set(TsvFixture.Read("16_levelPrice"), "16001001", "priceType", "602"), "16001001", "priceId", "0"))),
                "16_levelPrice", "런 전용 consumeType 602");

        // ---------------- 의미 ----------------

        [Test]
        public void Semantics_AreaNoGap_IsError() =>
            AssertError(TsvFixture.Validate(("302_area", TsvFixture.Set(TsvFixture.Read("302_area"), "302001005", "areaNo", "40"))),
                "302_area", "1부터 연속");

        [Test]
        public void Semantics_PlayableAreasOverActive_IsError() =>
            AssertError(TsvFixture.Validate(("300_runConfig", TsvFixture.SetByKey(TsvFixture.Read("300_runConfig"), RunConfigKeys.PLAYABLE_AREAS, "value", "99"))),
                "300_runConfig", "PLAYABLE_AREAS");

        [Test]
        public void RunConfig_AddressUnitWithPath_IsError() =>
            AssertError(TsvFixture.Validate(("300_runConfig", TsvFixture.SetByKey(TsvFixture.Read("300_runConfig"), RunConfigKeys.Proj_Prefab, "value", "Prefab/projectile"))),
                "300_runConfig", "주소(파일 이름)가 아닙니다");

        [Test]
        public void RunConfig_AddressUnit_AcceptsFileName()
        {
            var report = TsvFixture.Validate();
            Assert.That(report.Contains(MvIssueLevel.Error, "300_runConfig", "주소(파일 이름)"), Is.False, report.Summary(60));
            Assert.That(report.Contains(MvIssueLevel.Error, "300_runConfig", "숫자(또는"), Is.False, report.Summary(60));
        }

        [Test]
        public void Semantics_FloorMissing_IsError() =>
            AssertError(TsvFixture.Validate(("303_areaFloor", TsvFixture.Remove(TsvFixture.Read("303_areaFloor"), "303001002"))),
                "303_areaFloor", "floor 2가 없습니다");

        [Test]
        public void Semantics_BakedSoulSumMismatch_IsError()
        {
            var text = TsvFixture.Read("303_areaFloor");
            text = TsvFixture.Set(text, "303001001", "cellMode", "Baked");
            text = TsvFixture.Set(text, "303001001", "soulCells", "4/8/15");
            text = TsvFixture.Set(text, "303001001", "soulAmts", "3/3/3");
            text = TsvFixture.Set(text, "303001001", "diaCells", "9/16/18");
            AssertError(TsvFixture.Validate(("303_areaFloor", text)), "303_areaFloor", "soulAmts 합 9");
        }

        [Test]
        public void Semantics_TemplateCycleGap_IsError() =>
            AssertError(TsvFixture.Validate(("312_templateCycle", TsvFixture.Set(TsvFixture.Read("312_templateCycle"), "312000002", "levelFrom", "10"))),
                "312_templateCycle", "Level 9~9");

        [Test]
        public void Semantics_TemplateBlockingKind_IsError()
        {
            var placed = TsvFixture.Append(TsvFixture.Read("311_templateObject"),
                R("id", "templateId", "seq", "kindId", "x", "z", "groupId", "active"),
                R("311003099", "310000003", "99", "105001010", "0", "100", "0", "1"));
            AssertError(TsvFixture.Validate(("311_templateObject", placed)), "311_templateObject", "blocks=1");
        }

        [Test]
        public void Semantics_CloneGroupShort_IsError() =>
            AssertError(TsvFixture.Validate(("314_spawnGroup", TsvFixture.Remove(TsvFixture.Read("314_spawnGroup"), "314002004"))),
                "314_spawnGroup", "MAX_CLONES");

        [Test]
        public void Semantics_ChaseSpeedReversed_IsError()
        {
            var text = TsvFixture.Read("109_moveAI");
            var chase = text.Split('\n').Skip(1).First(l => l.Contains("\tChase\t")).Split('\t')[0];
            text = TsvFixture.Set(text, chase, "speedMin", "99");
            AssertError(TsvFixture.Validate(("109_moveAI", text)), "109_moveAI", "speedMin");
        }

        [Test]
        public void Semantics_LastTierNeedNotLap_IsError() =>
            AssertError(TsvFixture.Validate(("115_cardTier", TsvFixture.Set(TsvFixture.Read("115_cardTier"), "115001005", "need", "80"))),
                "115_cardTier", "330_climb.lap");

        [Test]
        public void Semantics_LevelPriceGap_IsError() =>
            AssertError(TsvFixture.Validate(("16_levelPrice", TsvFixture.Remove(TsvFixture.Read("16_levelPrice"), "16001002"))),
                "10_lobbyUpgrade", "targetLevel 2");

        [Test]
        public void Semantics_StairMaxFilled_IsError() =>
            // 계단 max는 Σ11.need로 계산하므로 표 칸은 비운다(사용자 결정 「행 수·합계로 계산되는 값은 계산」).
            AssertError(TsvFixture.Validate(("10_lobbyUpgrade", TsvFixture.Set(TsvFixture.Read("10_lobbyUpgrade"), "10001001", "max", "30"))),
                "10_lobbyUpgrade", "stair max는 11·12 합계로 계산");

        [Test]
        public void Semantics_MissingAdKey_IsError() =>
            AssertError(TsvFixture.Validate(("08_adPlacement", TsvFixture.Remove(TsvFixture.Read("08_adPlacement"), "8001003"))),
                "08_adPlacement", "`skip`");

        [Test]
        public void Semantics_UiResourceBothFilled_IsError() =>
            AssertError(TsvFixture.Validate(("09_uiResource", TsvFixture.Set(TsvFixture.Read("09_uiResource"), "9001001", "address", "Tex_X"))),
                "09_uiResource", "하나만");

        [Test]
        public void Semantics_ValueFormatTwoHoles_IsError() =>
            AssertError(TsvFixture.Validate(("13_areaUpgrade", TsvFixture.Set(TsvFixture.Read("13_areaUpgrade"), "13001001", "valueFormat", "{0}/{0}"))),
                "13_areaUpgrade", "정확히 1개");

        [Test]
        public void Semantics_PairValueMismatch_IsWarning()
        {
            // 결과 광고 배수는 08 collect3.mul 하나만 둔다(00_config.collectAdMul 삭제, MV-SPEC2-FIX1). 남은 짝 값 108[sgatePass].delayMs ↔ 300 fly.removeMs로 본다.
            var report = TsvFixture.Validate(("108_receiveCondition", TsvFixture.Set(TsvFixture.Read("108_receiveCondition"), "108001002", "delayMs", "999")));
            Assert.That(report.Contains(MvIssueLevel.Warning, "108_receiveCondition", "sgatePass"), Is.True, report.Summary());
            Assert.That(report.HasError, Is.False, report.Summary());
        }

        // ---------------- 801~804 ----------------

        const string DropHeader = "id\tdropGroup\trewardType\trewardId\trewardValue_min\trewardValue_max\trewardValueRule\tweight";

        [Test]
        public void Loot_ZeroWeightGroup_IsError()
        {
            var text = TsvFixture.Set(TsvFixture.Set(TsvFixture.Read("801_drop"), "801001001", "weight", "0"), "801001002", "weight", "0");
            AssertError(TsvFixture.Validate(("801_drop", text)), "801_drop", "weight 합이 0");
        }

        [Test]
        public void Loot_GroupNotFirstRowId_IsError()
        {
            var text = TsvFixture.Append(TsvFixture.Read("801_drop"), DropHeader, R("801002001", "801002002", "0", "0", "0", "0", "Flat", "1"));
            text = TsvFixture.Append(text, DropHeader, R("801002002", "801002002", "0", "0", "0", "0", "Flat", "1"));
            AssertError(TsvFixture.Validate(("801_drop", text)), "801_drop", "묶음 첫 행 id");
        }

        [Test]
        public void Loot_EmptyRowWithValue_IsError() =>
            AssertError(TsvFixture.Validate(("801_drop", TsvFixture.Set(TsvFixture.Read("801_drop"), "801001002", "rewardValue_max", "3"))),
                "801_drop", "꽝 행");

        [Test]
        public void Loot_MinOverMax_IsError()
        {
            var text = TsvFixture.Append(TsvFixture.Read("801_drop"), DropHeader, R("801002001", "801002001", "1", "1001001", "5", "3", "Flat", "1"));
            AssertError(TsvFixture.Validate(("801_drop", text)), "801_drop", "rewardValue_min ≤ rewardValue_max");
        }

        [Test]
        public void Loot_Cycle_IsError()
        {
            var text = TsvFixture.Append(TsvFixture.Read("801_drop"), DropHeader, R("801002001", "801002001", "50", "801003001", "1", "1", "Flat", "1"));
            text = TsvFixture.Append(text, DropHeader, R("801003001", "801003001", "50", "801002001", "1", "1", "Flat", "1"));
            AssertError(TsvFixture.Validate(("801_drop", text)), "801_drop", "순환");
        }

        [Test]
        public void Loot_DepthOverFive_IsError()
        {
            AssertError(TsvFixture.Validate(("801_drop", MvDropRollerTests.Chain(6))), "801_drop", "깊이");
            AssertNoErrors(TsvFixture.Validate(("801_drop", MvDropRollerTests.Chain(5))));
        }

        [Test]
        public void Guarantee_BadValues_AreErrors()
        {
            var box = MvDropRollerTests.BoxText();
            var g = TsvFixture.Append(TsvFixture.Read("804_randomGuarantee"),
                R("id", "active", "countType", "triggerCount", "rewardMode", "rewardBoxGroup", "rewardBoxSubGroup", "priority"),
                R("804001001", "1", "3", "0", "1", "802001001", "0", "0"));
            var report = TsvFixture.Validate(("802_randomBox", box), ("804_randomGuarantee", g));
            AssertError(report, "804_randomGuarantee", "triggerCount");
            AssertError(report, "804_randomGuarantee", "countType 3");
        }
    }
}
