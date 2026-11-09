using MV.Save;
using NUnit.Framework;

namespace MV.Save.Tests
{
    /// <summary>
    /// 09 §9.5.3 이관 체인 — v0(정본 프로토 저장) → v1. 고정 JSON → 이관 → 기대 JSON과 같음.
    /// 같은 변환이 D-67 프로토 붙여넣기 변환 함수다(09 §9.10 #9 표본 3개).
    /// </summary>
    public sealed class MvSaveMigrationTests : MvSaveFixture
    {
        // 표본 1: 진행이 있는 정본 저장(오늘 광고 사용, 부활 해금, Floor 객체·빈 칸 있는 벽 기록, 소수 츄르).
        private const string ProtoSample =
            "{\"level\":7,\"area\":2,\"zone\":0,\"gold\":1234.6,\"souls\":56,\"dia\":310," +
            "\"bars\":{\"A2\":{\"0\":[0,3,null,5],\"1\":[],\"2\":[2]}},\"cp\":1," +
            "\"up\":{\"stair\":3,\"weapon\":2,\"income\":1},\"deck\":[0,2,1,0,0,0,0,0,0],\"god\":\"fire\",\"escaped\":false,\"runs\":12," +
            "\"aup\":{\"rate\":1,\"range\":0,\"clone\":2,\"crit\":0,\"dbl\":1}," +
            "\"rb\":{\"lv\":1,\"tokens\":40,\"pending\":5,\"perks\":{\"power\":2,\"crit\":1,\"obst\":0,\"dcap\":0},\"cdUntil\":1700000100000,\"open\":true}," +
            "\"ads\":{\"day\":\"2023-11-14\",\"dia\":1,\"skip\":4},\"zoneClear\":false}";

        private const string ExpectedV1 =
            "{\"mvProgress\":{\"level\":7,\"area\":2,\"cp\":1,\"bars\":{\"A2\":[[0,3,null,5],[],[2]]},\"runs\":12,\"zoneClear\":false,\"escaped\":false,\"bestArea\":2,\"lastSettledRunSeq\":0}," +
            "\"mvLobby\":{\"up\":{\"stair\":3,\"weapon\":2,\"income\":1},\"deck\":[0,2,1,0,0,0,0,0,0]}," +
            "\"mvAreaUp\":{\"lv\":{\"rate\":1,\"range\":0,\"clone\":2,\"crit\":0,\"dbl\":1}}," +
            "\"mvRebirth\":{\"lv\":1,\"pending\":5,\"perks\":{\"power\":2,\"crit\":1,\"obst\":0,\"dcap\":0},\"cdUntil\":1700000100000}," +
            "\"mvGod\":{\"sel\":\"fire\",\"gods\":{}}," +
            "\"mvUnlock\":{\"opened\":[\"rebirth\"],\"seenUnlock\":[\"rebirth\"],\"seenNew\":[]}," +
            "\"mvDaily\":{\"day\":\"2023-11-14\",\"ads\":{\"dia\":2,\"skip\":0},\"modePlays\":{},\"free\":{}}," +
            "\"mvMeta\":{\"createdAt\":1700000000000,\"lastSaveAt\":1700000000000,\"importSource\":\"proto-paste\",\"tableVersion\":1}}";

        private static readonly string[] MappedKeys =
        {
            MvSaveKeys.Progress, MvSaveKeys.Lobby, MvSaveKeys.AreaUp, MvSaveKeys.Rebirth, MvSaveKeys.God, MvSaveKeys.Unlock, MvSaveKeys.Daily, MvSaveKeys.Meta
        };

        private static string Defaults() => MvSaveDefaults.CreateJson(MvSaveConfig.CreateDefault(), MvSaveTestKit.Now);

        private static string WithoutMappedAndWealth(string json)
            => MvSaveJson.Without(MvSaveJson.Without(json, MappedKeys), MvSaveKeys.Wealth);

        [Test]
        public void V0ToV1_FixedProtoJson_MatchesExpected()
        {
            string payload = MvProtoSave.ToPayloadJson(ProtoSample, MvSaveConfig.CreateDefault(), MvSaveTestKit.Now);
            Assert.That(MvSaveJson.DeepEquals(MvSaveJson.Pick(payload, MappedKeys), ExpectedV1), Is.True, payload);
            Assert.That(MvSaveJson.DeepEquals(WithoutMappedAndWealth(payload), WithoutMappedAndWealth(Defaults())), Is.True, "정본에 없는 컨테이너는 기본값");
        }

        [Test]
        public void V0Document_MigratesBeforeOpen()
        {
            var options = MvSaveTestKit.Options(new MemoryPrefs());
            string v0 = "{\"_containerSchema\":\"" + MvSaveSchema.V0 + "\"," + ProtoSample.Substring(1);
            MvSaveStore.WriteDocumentForTests(options, v0);

            MvSaveOpenResult result = MvSaveStore.Open(options);
            MvSaveStore save = Track(result);
            Assert.That(result.Status, Is.EqualTo(MvSaveOpenStatus.Opened), result.Error);
            Assert.That(result.Migrated, Is.True);
            Assert.That(result.LoadedSchema, Is.EqualTo(MvSaveSchema.V0));
            Assert.That(MvSaveJson.DeepEquals(MvSaveJson.Pick(save.ExportPayloadJson(), MappedKeys), ExpectedV1), Is.True, save.ExportPayloadJson());
            Assert.That(save.GetWealth(MvWealthIds.Gold), Is.EqualTo(1234), "gold는 floor");
            Assert.That(save.GetWealth(MvWealthIds.Dia), Is.EqualTo(310));
            Assert.That(save.GetWealth(MvWealthIds.Souls), Is.EqualTo(56));
            Assert.That(save.GetWealth(MvWealthIds.RbToken), Is.EqualTo(40));
            Assert.That(save.GetWealth(MvWealthIds.Medal), Is.EqualTo(0));

            save.Dispose();
            MvSaveOpenResult again = MvSaveStore.Open(options);
            Track(again);
            Assert.That(again.Status, Is.EqualTo(MvSaveOpenStatus.Opened));
            Assert.That(again.Migrated, Is.False, "이관한 결과를 저장했으므로 다시 열 때는 v1");
            Assert.That(again.LoadedSchema, Is.EqualTo(MvSaveSchema.V1));
        }

        [Test]
        public void ProtoSamples_StaleAdsAndMissingUpFollowProtoLoad()
        {
            var config = MvSaveConfig.CreateDefault();
            // 표본 2: 광고 날짜가 오늘이 아님 → 사용 수 버림. 덱 길이 9 아님 → 시작 덱.
            string stale = MvProtoSave.ToPayloadJson(
                "{\"level\":3,\"up\":{\"stair\":1},\"deck\":[0,1],\"ads\":{\"day\":\"2023-11-13\",\"dia\":0,\"skip\":0},\"rb\":{\"tokens\":7}}",
                config, MvSaveTestKit.Now);
            Assert.That(MvSaveJson.DeepEquals(MvSaveJson.Pick(stale, MvSaveKeys.Daily), MvSaveJson.Pick(Defaults(), MvSaveKeys.Daily)), Is.True, stale);
            Assert.That(MvSaveJson.DeepEquals(MvSaveJson.Pick(stale, MvSaveKeys.Lobby),
                "{\"mvLobby\":{\"up\":{\"stair\":1,\"weapon\":0,\"income\":0},\"deck\":[0,1,0,0,0,0,0,0,0]}}"), Is.True, stale);
            Assert.That(MvSaveJson.DeepEquals(MvSaveJson.Pick(stale, MvSaveKeys.Unlock), MvSaveJson.Pick(Defaults(), MvSaveKeys.Unlock)), Is.True);

            // 표본 3: up이 없으면 정본 load()는 DEF() → 기본값과 같다.
            string noUp = MvProtoSave.ToPayloadJson("{\"level\":50,\"gold\":999}", config, MvSaveTestKit.Now);
            Assert.That(MvSaveJson.DeepEquals(MvSaveJson.Without(noUp, MvSaveKeys.Meta), MvSaveJson.Without(Defaults(), MvSaveKeys.Meta)), Is.True, noUp);
        }

        [Test]
        public void UnknownSchema_FailsWithoutTouchingSave()
        {
            var prefs = new MemoryPrefs();
            var options = MvSaveTestKit.Options(prefs);
            MvSaveStore.WriteDocumentForTests(options, "{\"_containerSchema\":\"mv:local:v9\",\"mvProgress\":{\"level\":3}}");
            var stored = prefs.Copy();

            MvSaveOpenResult result = MvSaveStore.Open(options);
            Track(result);
            Assert.That(result.Status, Is.EqualTo(MvSaveOpenStatus.GameMigrationFailed));
            Assert.That(result.Store, Is.Null);
            Assert.That(result.Notice, Is.EqualTo(MvSaveNotice.RecoveryPopup));
            Assert.That(result.LoadedSchema, Is.EqualTo("mv:local:v9"));
            Assert.That(prefs.Values, Is.EqualTo(stored), "이관 실패는 원문을 그대로 둔다");
        }

        [Test]
        public void DefaultChain_ConnectsOnlyKnownSchemas()
        {
            MvSaveMigrator migrator = MvSaveMigrator.CreateDefault(MvSaveConfig.CreateDefault(), () => MvSaveTestKit.Now);
            Assert.That(migrator.TargetSchema, Is.EqualTo(MvSaveSchema.Current));
            Assert.That(migrator.HasPath(MvSaveSchema.V0), Is.True);
            Assert.That(migrator.HasPath(MvSaveSchema.V1), Is.True);
            Assert.That(migrator.HasPath("mv:local:v9"), Is.False);
            Assert.That(migrator.HasPath(null), Is.False);
        }
    }
}
