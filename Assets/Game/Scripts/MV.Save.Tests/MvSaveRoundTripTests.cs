using System;
using System.IO;
using jjevol;
using MV.Save;
using NUnit.Framework;

namespace MV.Save.Tests
{
    /// <summary>저장 → 재시작 → 로드 왕복, 원자 저장, 쓰기 실패, 진행 초기화(D-69).</summary>
    public sealed class MvSaveRoundTripTests : MvSaveFixture
    {
        private const string Changes =
            "{\"wealthInfos\":[{\"wealthType\":\"gold\",\"quantity\":777},{\"wealthType\":\"dia\",\"quantity\":12},{\"wealthType\":\"rbToken\",\"quantity\":3}]," +
            "\"mvProgress\":{\"level\":9,\"area\":3,\"cp\":1,\"bars\":{\"A3\":[[5,0,null],[2.5],[]]},\"runs\":8,\"zoneClear\":true,\"lastSettledRunSeq\":8}," +
            "\"mvLobby\":{\"up\":{\"stair\":2,\"income\":1},\"deck\":[0,3,1,0,0,0,0,0,0]}," +
            "\"mvAreaUp\":{\"lv\":{\"rate\":4}}," +
            "\"mvRebirth\":{\"lv\":2,\"pending\":6,\"perks\":{\"power\":1},\"cdUntil\":1700000360000}," +
            "\"mvGod\":{\"sel\":\"fire\",\"gods\":{\"fire\":{\"lv\":2,\"shard\":5}}}," +
            "\"mvUnlock\":{\"opened\":[\"rebirth\",\"mode\"],\"seenUnlock\":[\"rebirth\"],\"seenNew\":[\"rebirth\"]}," +
            "\"mvDaily\":{\"day\":\"2023-11-14\",\"ads\":{\"dia\":2},\"modePlays\":{\"tower\":1}}," +
            "\"mvPlayRes\":{\"ticket\":{\"n\":3,\"at\":1699999990000}}," +
            "\"mvMode\":{\"tower\":{\"best\":4,\"first\":[3,1,2,2]}}," +
            "\"mvQuest\":{\"daily\":{\"period\":\"2023-11-14\",\"cnt\":{\"run\":2},\"got\":[\"q1\"]},\"attend\":{\"period\":\"2023-11-13\",\"days\":2,\"lastDay\":\"2023-11-14\"}}," +
            "\"mvResearch\":{\"lv\":{\"atk\":1},\"run\":{\"key\":\"hp\",\"finishAt\":1700000600000}}," +
            "\"mvShop\":{\"buys\":{\"starter\":{\"n\":1,\"at\":1700000000000}},\"guarantee\":{\"804001001\":3}}," +
            "\"mvPet\":{\"future\":{\"a\":1}}," +
            "\"mvSettings\":{\"muted\":true,\"contentTab\":\"mode\"}}";

        private static readonly string[] ChangedKeys =
        {
            MvSaveKeys.Wealth, MvSaveKeys.Progress, MvSaveKeys.Lobby, MvSaveKeys.AreaUp, MvSaveKeys.Rebirth, MvSaveKeys.God, MvSaveKeys.Unlock,
            MvSaveKeys.Daily, MvSaveKeys.PlayRes, MvSaveKeys.Mode, MvSaveKeys.Quest, MvSaveKeys.Research, MvSaveKeys.Shop, MvSaveKeys.Pet, MvSaveKeys.Settings
        };

        [Test]
        public void SaveRestartLoad_RestoresSameState()
        {
            var clock = new TestClock();
            var options = MvSaveTestKit.Options(new MemoryPrefs(), clock);
            MvSaveStore save = Track(MvSaveStore.Open(options));
            clock.Now += 5000;
            Assert.That(save.ApplyAndSaveJson(Changes, false, ChangedKeys), Is.True);
            string before = save.ExportPayloadJson();
            save.Dispose();

            MvSaveOpenResult reopened = MvSaveStore.Open(options);
            MvSaveStore loaded = Track(reopened);
            Assert.That(reopened.Status, Is.EqualTo(MvSaveOpenStatus.Opened));
            Assert.That(MvSaveJson.DeepEquals(before, loaded.ExportPayloadJson()), Is.True, before + "\n" + loaded.ExportPayloadJson());

            Assert.That(loaded.GetWealth(MvWealthIds.Gold), Is.EqualTo(777));
            Assert.That(loaded.GetWealth(MvWealthIds.Souls), Is.EqualTo(0));
            Assert.That(loaded.GetWealth(MvWealthIds.RbToken), Is.EqualTo(3));
            Assert.That(loaded.Progress.Level, Is.EqualTo(9));
            Assert.That(loaded.Progress.BestArea, Is.EqualTo(3), "bestArea는 area 이상");
            Assert.That(loaded.Progress.BarAreaKeys, Is.EqualTo(new[] { "A3" }));
            Assert.That(loaded.Lobby.GetUp("stair"), Is.EqualTo(2));
            Assert.That(loaded.Lobby.GetUp("weapon"), Is.EqualTo(0));
            Assert.That(loaded.Lobby.Deck, Is.EqualTo(new[] { 0, 3, 1, 0, 0, 0, 0, 0, 0 }));
            Assert.That(loaded.AreaUp.GetLevel("rate"), Is.EqualTo(4));
            Assert.That(loaded.Rebirth.GetPerk("power"), Is.EqualTo(1));
            Assert.That(loaded.God.Selected, Is.EqualTo("fire"));
            Assert.That(loaded.God.GetGodShard("fire"), Is.EqualTo(5));
            Assert.That(loaded.Unlock.IsOpened("mode"), Is.True);
            Assert.That(loaded.Daily.GetAdUsed("dia"), Is.EqualTo(2));
            Assert.That(loaded.PlayRes.TryGet("ticket", out int tickets, out long chargedAt), Is.True);
            Assert.That(tickets, Is.EqualTo(3));
            Assert.That(chargedAt, Is.EqualTo(1699999990000L));
            Assert.That(loaded.Mode.GetFirstCleared("tower"), Is.EqualTo(new[] { 1, 2, 3 }));
            Assert.That(loaded.Quest.AttendDays, Is.EqualTo(2));
            Assert.That(loaded.Research.RunningKey, Is.EqualTo("hp"));
            Assert.That(loaded.Shop.GetGuarantee(804001001), Is.EqualTo(3));
            Assert.That(loaded.Pet.IsEmpty, Is.False);
            Assert.That(loaded.Settings.Muted, Is.True);
            Assert.That(loaded.Meta.CreatedAt, Is.EqualTo(MvSaveTestKit.Now));
            Assert.That(loaded.Meta.LastSaveAt, Is.EqualTo(MvSaveTestKit.Now + 5000));
        }

        [Test]
        public void InvalidContainerValue_ChangesNothing()
        {
            var prefs = new MemoryPrefs();
            var options = MvSaveTestKit.Options(prefs);
            MvSaveStore save = Track(MvSaveStore.Open(options));
            string before = save.ExportPayloadJson();
            var stored = prefs.Copy();

            // 재화는 바르고 진행 값이 객체가 아니다 → 묶음 전체가 거절돼야 한다.
            Assert.Throws<InvalidDataException>(() => save.ApplyAndSaveJson(
                "{\"wealthInfos\":[{\"wealthType\":\"gold\",\"quantity\":50}],\"mvProgress\":5}", false, MvSaveKeys.Wealth, MvSaveKeys.Progress));
            Assert.That(save.GetWealth(MvWealthIds.Gold), Is.EqualTo(0));
            Assert.That(MvSaveJson.DeepEquals(before, save.ExportPayloadJson()), Is.True);
            Assert.That(prefs.Values, Is.EqualTo(stored));
            Assert.That(save.RequiresReopen, Is.False);
        }

        [Test]
        public void WriteFailure_RequiresReopen_ReopenKeepsConfirmedState()
        {
            var prefs = new MemoryPrefs();
            var options = MvSaveTestKit.Options(prefs);
            MvSaveStore save = Track(MvSaveStore.Open(options));
            save.ApplyAndSaveJson("{\"mvProgress\":{\"level\":4}}", false, MvSaveKeys.Progress);

            prefs.FailNextSet = true;
            Assert.Throws<LocalSaveWriteException>(() => save.ApplyAndSaveJson("{\"mvProgress\":{\"level\":5}}", false, MvSaveKeys.Progress));
            Assert.That(save.Progress.Level, Is.EqualTo(4), "저장 확인 전에는 메모리를 바꾸지 않는다");
            Assert.That(save.RequiresReopen, Is.True);

            MvSaveOpenResult reopened = save.Reopen();
            MvSaveStore again = Track(reopened);
            Assert.That(reopened.Status, Is.EqualTo(MvSaveOpenStatus.Opened));
            Assert.That(again.Progress.Level, Is.EqualTo(4));
            Assert.That(again.ApplyAndSaveJson("{\"mvProgress\":{\"level\":5}}", false, MvSaveKeys.Progress), Is.True);
        }

        [Test]
        public void DevResetProgress_KeepsSettingsAndResetsTheRest()
        {
            var clock = new TestClock();
            var options = MvSaveTestKit.Options(new MemoryPrefs(), clock);
            MvSaveStore save = Track(MvSaveStore.Open(options));
            save.ApplyAndSaveJson(Changes, false, ChangedKeys);
            clock.Now += 60000;

            save.DevResetProgress();

            string defaults = MvSaveDefaults.CreateJson(MvSaveConfig.CreateDefault(), clock.Now);
            Assert.That(MvSaveJson.DeepEquals(
                MvSaveJson.Without(save.ExportPayloadJson(), MvSaveKeys.Settings),
                MvSaveJson.Without(defaults, MvSaveKeys.Settings)), Is.True, save.ExportPayloadJson());
            Assert.That(save.Settings.Muted, Is.True, "소리 설정은 진행 초기화에서 유지");
            Assert.That(save.Settings.ContentTab, Is.EqualTo("mode"));
            Assert.That(save.Meta.CreatedAt, Is.EqualTo(clock.Now));

            save.Dispose();
            MvSaveStore again = Track(MvSaveStore.Open(options));
            Assert.That(again.Progress.Level, Is.EqualTo(1));
            Assert.That(again.GetWealth(MvWealthIds.Gold), Is.EqualTo(0));
            Assert.That(again.Settings.Muted, Is.True);
        }

        [Test]
        public void SaveIfDirty_DoesNothingWhenClean()
        {
            var prefs = new MemoryPrefs();
            MvSaveStore save = Track(MvSaveStore.Open(MvSaveTestKit.Options(prefs)));
            var stored = prefs.Copy();
            Assert.That(save.IsDirty, Is.False);
            Assert.That(save.SaveIfDirty(), Is.False);
            Assert.That(prefs.Values, Is.EqualTo(stored));
        }
    }
}
