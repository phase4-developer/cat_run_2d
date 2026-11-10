using System.Collections.Generic;
using System.Linq;
using jjevol;
using jjevol.Data;
using NUnit.Framework;

namespace MV.Data.Tests
{
    /// <summary>모든 표가 읽히고 행 수·대표 값(07·08 예시 행)이 맞는지(기획서 11 MV-DATA 검증).</summary>
    public sealed class MvTablesLoadTests
    {
        static readonly Dictionary<string, int> s_rowCounts = new Dictionary<string, int>
        {
            ["00_config"] = 1, ["01_wealth"] = 18, ["02_playResource"] = 5, ["03_sound"] = 33, ["04_effect"] = 75,
            ["05_contentOpen"] = 18, ["06_balancePreset"] = 3, ["07_consumeType"] = 26, ["08_adPlacement"] = 7, ["09_uiResource"] = 12,
            ["10_lobbyUpgrade"] = 3, ["11_stairLevel"] = 4, ["12_weaponLevel"] = 8, ["13_areaUpgrade"] = 8, ["14_rebirthPerk"] = 9,
            ["16_levelPrice"] = 812,
            ["101_cat"] = 2, ["102_enemy"] = 3, ["103_item"] = 8, ["105_objectKind"] = 13, ["106_hitReaction"] = 7,
            ["107_chargeGate"] = 3, ["108_receiveCondition"] = 7, ["109_moveAI"] = 3, ["110_card"] = 92, ["111_cardPool"] = 11,
            ["112_cardEffect"] = 26, ["113_god"] = 12, ["114_hpRule"] = 7, ["115_cardTier"] = 5,
            ["300_runConfig"] = 168, ["301_zone"] = 6, ["302_area"] = 30, ["303_areaFloor"] = 94, ["304_theme"] = 15,
            ["310_template"] = 10, ["311_templateObject"] = 123, ["312_templateCycle"] = 2, ["313_fieldGen"] = 1,
            ["314_spawnGroup"] = 9, ["320_camera"] = 3, ["330_climb"] = 5,
            ["400_content"] = 5, ["401_modeStage"] = 53, ["402_modeRule"] = 4, ["410_questGroup"] = 14, ["413_repeatQuest"] = 16,
            ["414_passQuest"] = 60, ["420_achievement"] = 19, ["499_contentConfig"] = 42,
            ["700_productTab"] = 6, ["701_product"] = 29, ["702_shopSection"] = 4,
            ["430_pet"] = 16, ["431_petBuild"] = 16, ["440_research"] = 25, ["450_friend"] = 10, ["451_friendPower"] = 3,
            ["460_rune"] = 4, ["470_gearPart"] = 7, ["471_gearRarity"] = 8, ["480_blessing"] = 4, ["485_attendance"] = 7,
            ["490_roadMap"] = 6, ["495_godLevel"] = 5,
            ["801_drop"] = 2, ["802_randomBox"] = 34, ["803_randomGuaranteeLink"] = 0, ["804_randomGuarantee"] = 0,
        };

        [TearDown]
        public void TearDown()
        {
            TableRepository.Clear();
            GameTableSource.Configure(new PackagedTableSource(false));
            MvTables.Current = null;
        }

        [Test]
        public void AllTables_LoadFromTsv_WithExpectedRowCounts()
        {
            var tables = TsvFixture.LoadTables();
            Assert.That(tables.All.Select(t => t.TableName), Is.EquivalentTo(s_rowCounts.Keys), "등록 표 목록");
            foreach (var t in tables.All)
                Assert.That(t.RawRows.Count, Is.EqualTo(s_rowCounts[t.TableName]), t.TableName);
        }

        [Test]
        public void LoadAsync_ThroughRepository_ValidatesAndSetsCurrent()
        {
            var task = MvTableLoader.LoadAsync(TsvFixture.Source());
            var tables = task.GetAwaiter().GetResult(); // TSV 원본은 동기 완료 Task라 여기서 막히지 않는다.
            Assert.That(TableRepository.loaded, Is.True);
            Assert.That(MvTables.Current, Is.SameAs(tables));
            Assert.That(MvTableLoader.LastReport.HasError, Is.False, MvTableLoader.LastReport.Summary());
            Assert.That(tables.Drops, Is.Not.Null);
            Assert.That(tables.Config.Datas.Count, Is.EqualTo(1));
        }

        [Test]
        public void RepresentativeValues_MatchSpecExampleRows()
        {
            var t = TsvFixture.LoadTables();
            // D-80: 재화 id는 1001001 식.
            Assert.That(t.Wealth.Get(MvLevelPriceGenerator.Gold), Is.Not.Null);
            // 07 §7.8.1 로비 강화.
            var stair = t.LobbyUpgrade.ByKey("stair");
            Assert.That(stair.max, Is.EqualTo(30));
            Assert.That(stair.costBase, Is.EqualTo(25));
            Assert.That(t.LobbyUpgrade.ByKey("weapon").max, Is.EqualTo(149));
            // 07 §7.8.4·7.8.5.
            Assert.That(t.AreaUpgrade.ByKey("rate").valueFormat, Is.EqualTo("{0}%"));
            Assert.That(t.RebirthPerk.ByKey("dcap").@base, Is.EqualTo(10));
            // 07 §7.9.3 적 캣닢 8%.
            var enemyDrop = t.Drop.GetGroup(801001001);
            Assert.That(enemyDrop.Select(d => d.weight), Is.EqualTo(new[] { 800, 9200 }));
            Assert.That(enemyDrop[0].rewardType, Is.EqualTo((int)ConsumeType.RunDia));
            Assert.That(enemyDrop[0].rewardValueRule, Is.EqualTo(RewardValueRule.HalfDiaCapCeil));
            // 07 §7.6.5 무리·분신.
            Assert.That(t.SpawnGroup.GetGroup(2).Count, Is.EqualTo(4));
            Assert.That(t.SpawnGroup.Get(314001002).dz, Is.EqualTo(6.9).Within(1e-9));
            // 07 §7.7.2 가호 카드 등급, 330 lap.
            Assert.That(t.CardTier.Datas.OrderBy(x => x.tierIndex).Select(x => x.need), Is.EqualTo(new[] { 5, 10, 20, 40, 75 }));
            Assert.That(t.Climb.Config()["lap"], Is.EqualTo(75));
            // 300 키 = 코드 상수.
            Assert.That(t.RunConfig.Datas.Count, Is.EqualTo(RunConfigKeys.All.Length));
            Assert.That(t.RunConfig.I(RunConfigKeys.MAX_CLONES), Is.EqualTo(4));
            // 312 Level → 템플릿(정본 tplFor).
            Assert.That(t.TemplateCycle.TemplateFor(0, 1), Is.EqualTo(310000001));
            Assert.That(t.TemplateCycle.TemplateFor(0, 9), Is.EqualTo(310000003));
            Assert.That(t.TemplateCycle.TemplateFor(0, 15), Is.EqualTo(310000003));
            // 16 비용 = 생성 도구.
            var gen = MvLevelPriceGenerator.Generate(t);
            var first = gen.First(g => g.Group == stair.levelPriceGroup && g.TargetLevel == 1);
            Assert.That(t.LevelPrice.Get(first.Group, 1).price[0].Value, Is.EqualTo(first.Price));
        }

        /// <summary>3차 요청 열·행(MV-VIEW·MV-FX·MV-META): 표 구조와 정본 근거 값.</summary>
        [Test]
        public void Requests3_ColumnsAndRows_MatchSpec()
        {
            var t = TsvFixture.LoadTables();
            // 02 §2.7.3 지면 끝, 330 출렁임(정본 L2758).
            Assert.That(t.RunConfig.D(RunConfigKeys.View_GroundFar), Is.EqualTo(120));
            Assert.That(t.Climb.Config()["bumpH"], Is.EqualTo(0.12).Within(1e-9));
            // 304: 길가 소품·줄 종류는 deco 규칙(정본 drawMoonProp L3572, drawClothes L3547) — 15행 모두.
            foreach (var th in t.Theme.Datas)
            {
                Assert.That(th.roadsideProps, Is.Not.Empty, th.key);
                Assert.That(th.lineKind, Is.EqualTo(th.deco == "snow" || th.deco == "roof" ? "Bulbs" : "Clothes"), th.key);
                Assert.That(th.roadsideProps, Is.EqualTo(t.Theme.ByKey(th.deco).roadsideProps), th.key);
            }
            // 105 viewShape = ObjectViewFactory.ShapeOf 분기.
            Assert.That(t.ObjectKind.ByKey("tower").viewShape, Is.EqualTo(ObjectViewShape.Well));
            Assert.That(t.ObjectKind.ByKey("enemy").viewShape, Is.EqualTo(ObjectViewShape.Mouse));
            Assert.That(t.ObjectKind.ByKey("wall").viewShape, Is.EqualTo(ObjectViewShape.Junk));
            Assert.That(t.ObjectKind.ByKey("archGoal").viewShape, Is.EqualTo(ObjectViewShape.WarpGate));
            Assert.That(t.ObjectKind.ByKey("cardbox").viewShape, Is.EqualTo(ObjectViewShape.Single));
            // 04: 자식이 여럿인 묶음은 part 행만 자식으로 갖고, part는 기본 종류 1개.
            foreach (var g in t.Effect.Datas.Where(e => e.type == EffectData.GroupType && e.children.Count > 1))
                Assert.That(g.children.All(c => t.Effect.ByKey(c).type == EffectData.PartType), Is.True, g.key);
            var crit = t.Effect.ByKey("run.projHit.sparksCrit");
            Assert.That(crit.children, Is.EqualTo(new[] { "fx.sparks" }));
            Assert.That(crit.when, Is.EqualTo(new[] { "crit" }));
            Assert.That(crit.hasGrav && crit.grav == 0, Is.True, "중력 0은 빈칸과 다르다");
            Assert.That(t.Effect.ByKey("god.blast").children.Count(c => c == "god.blast.fireball"), Is.EqualTo(3));
            Assert.That(t.Effect.ByKey("run.brickFlush.sparks").dz, Is.EqualTo(-0.8).Within(1e-9));
            // 400 하루 입장 한도(MV-META modePlays): 0 = 한도 없음.
            Assert.That(t.Content.Datas.Select(c => c.dailyLimit), Is.All.EqualTo(0));
        }

        [Test]
        public void ConsumeTypeTable_MatchesEnumOneToOne()
        {
            var t = TsvFixture.LoadTables();
            foreach (ConsumeType v in System.Enum.GetValues(typeof(ConsumeType)))
            {
                if (v == ConsumeType.None) continue;
                var row = t.ConsumeType.Get(v);
                Assert.That(row, Is.Not.Null, v.ToString());
                Assert.That(row.codeName, Is.EqualTo(v.ToString()));
            }
        }
    }
}
