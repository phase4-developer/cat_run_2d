using MV.Data;
using MV.Save;
using NUnit.Framework;

namespace MV.Meta.Tests
{
    /// <summary>테스트 패널 요청 처리기(05 §5.14, 개발 빌드 전용)와 부활 확인 대기 해제(05 §5.9).</summary>
    public sealed class MvMetaDebugTests : MvMetaTestBase
    {
        const long HourMs = 3600L * 1000L;

        MvMetaResponse Debug(string name, string key = null, long amount = 0) => Meta.SendDebug(name, key, amount);

        static void Ok(MvMetaResponse r) => Assert.That(r.Ok, Is.True, r.ToString());

        [Test]
        public void DebugGrant_AddsWealthById_RejectsUnknownOrZero()
        {
            SetWealth((MvWealthIds.Gold, 20));
            Ok(Debug(MvMetaRequestNames.DebugGrant, MvWealthIds.Gold.ToString(), 500));
            Assert.That(W(MvWealthIds.Gold), Is.EqualTo(520));
            Assert.That(Debug(MvMetaRequestNames.DebugGrant, "999", 5).Code, Is.EqualTo(MvMetaErrors.UnknownKey));
            Assert.That(Debug(MvMetaRequestNames.DebugGrant, MvWealthIds.Gold.ToString()).Code, Is.EqualTo(MvMetaErrors.BadParams));
            Assert.That(W(MvWealthIds.Gold), Is.EqualTo(520), "거절은 저장하지 않는다");
        }

        [Test]
        public void DebugLevelUp_AddsOneLevel()
        {
            int before = Store.Progress.Level;
            Ok(Debug(MvMetaRequestNames.DebugLevelUp));
            Assert.That(Store.Progress.Level, Is.EqualTo(before + 1));
        }

        [Test]
        public void DebugReset_DefaultsProgress_KeepsSettings()
        {
            SetWealth((MvWealthIds.Gold, 500), (MvWealthIds.RbToken, 7));
            SetContainer(MvSaveKeys.Progress, "\"area\":2,\"level\":7,\"runs\":5");
            SetContainer(MvSaveKeys.Rebirth, "\"lv\":3,\"pending\":9");
            Ok(Meta.MarkSeen(contentTab: "growth"));

            Ok(Debug(MvMetaRequestNames.DebugReset));

            Assert.That(Store.Progress.Area, Is.EqualTo(0));
            Assert.That(Store.Progress.Runs, Is.EqualTo(0));
            Assert.That(Store.Rebirth.Lv, Is.EqualTo(0));
            Assert.That(Store.Rebirth.Pending, Is.EqualTo(0));
            Assert.That(W(MvWealthIds.Gold), Is.EqualTo(0));
            Assert.That(W(MvWealthIds.RbToken), Is.EqualTo(0));
            Assert.That(Store.Settings.ContentTab, Is.EqualTo("growth"), "D-69: mvSettings는 남긴다");
        }

        [Test]
        public void DebugOpenRebirth_OpensStickyAndAddsTenPending()
        {
            SetContainer(MvSaveKeys.Rebirth, "\"pending\":3");
            Assert.That(Meta.RebirthOpen, Is.False, "Area 0은 잠김");
            Ok(Debug(MvMetaRequestNames.DebugOpenRebirth));
            Assert.That(Meta.RebirthOpen, Is.True);
            Assert.That(Store.Unlock.Opened, Does.Contain(MvMetaRules.RebirthKey), "sticky 기록");
            Assert.That(Store.Rebirth.Pending, Is.EqualTo(3 + 10), "정본 rbOpen: pending += 10");
        }

        [Test]
        public void DebugClearCooldown_ZeroesCdUntil()
        {
            SetContainer(MvSaveKeys.Rebirth, "\"cdUntil\":" + (T0 + HourMs));
            Assert.That(Meta.RebirthCooldownLeftMs, Is.EqualTo(HourMs));
            Ok(Debug(MvMetaRequestNames.DebugClearCooldown));
            Assert.That(Store.Rebirth.CdUntil, Is.EqualTo(0));
            Assert.That(Meta.RebirthCooldownLeftMs, Is.EqualTo(0));
        }

        [Test]
        public void DebugUnlockAll_OpensEveryActiveRow_WithinPlayableAreas()
        {
            Ok(Debug(MvMetaRequestNames.DebugUnlockAll));
            foreach (var row in Tables.ContentOpen.Sorted)
                if (row.active) Assert.That(Meta.IsOpen(row.key), Is.True, row.key);
            Assert.That(Store.Progress.Area, Is.LessThan(Tables.RunConfig.I(RunConfigKeys.PLAYABLE_AREAS)), "마지막 Area를 넘지 않는다");
            Assert.That(Store.Unlock.Opened, Does.Contain(MvMetaRules.RebirthKey), "sticky 행은 기록");
        }

        [Test]
        public void DebugResetDaily_EmptiesTodayCounts()
        {
            SetContainer(MvSaveKeys.Daily, "\"day\":\"" + Meta.Today + "\",\"ads\":{\"dia\":3},\"modePlays\":{\"alleyWar\":2}");
            Assert.That(Meta.AdsLeft("dia"), Is.EqualTo(0));
            Ok(Debug(MvMetaRequestNames.DebugResetDaily));
            Assert.That(Store.Daily.Day, Is.EqualTo(Meta.Today));
            Assert.That(Store.Daily.Ads, Is.Empty);
            Assert.That(Store.Daily.ModePlays, Is.Empty);
            Assert.That(Meta.AdsLeft("dia"), Is.EqualTo(3));
        }

        [Test]
        public void DebugAddTime_ShiftsMetaClock_WithoutSaving()
        {
            SetContainer(MvSaveKeys.Rebirth, "\"cdUntil\":" + (T0 + HourMs));
            Ok(Debug(MvMetaRequestNames.DebugAddTime, amount: 3600));
            Assert.That(Meta.Clock.NowMs, Is.EqualTo(T0 + HourMs));
            Assert.That(Meta.RebirthCooldownLeftMs, Is.EqualTo(0), "+1시간이면 1시간 쿨타임이 끝난다");
            Assert.That(Store.Rebirth.CdUntil, Is.EqualTo(T0 + HourMs), "저장 값은 그대로");
            Assert.That(Debug(MvMetaRequestNames.DebugAddTime).Code, Is.EqualTo(MvMetaErrors.BadParams));
        }

        [Test]
        public void ReleaseBuild_DebugRequestsAreUnknown()
        {
            var release = new MvLocalMetaBackend(() => Store, () => Res, Tables, Clock, debugRequests: false);
            Assert.That(release.DebugRequestsEnabled, Is.False);
            foreach (string name in MvMetaRequestNames.DebugAll)
                Assert.That(release.Handle(MvMetaRequest.ForDebug(name, "1", 1)).Code,
                    Is.EqualTo(MvMetaErrors.UnknownRequest), name);
            Assert.That(new MvLocalMetaBackend(() => Store, () => Res, Tables, Clock, debugRequests: true).DebugRequestsEnabled, Is.True);
            Assert.That(MvMetaRequestNames.DebugAll, Has.Length.EqualTo(8));
        }

        [Test]
        public void DisarmRebirth_ClearsConfirmWait()
        {
            SetContainer(MvSaveKeys.Progress, "\"area\":1");
            Assert.That(Meta.PressRebirth(), Is.Null, "첫 누름 = 확인 대기");
            Assert.That(Meta.RebirthArmed, Is.True);
            Meta.DisarmRebirth();
            Assert.That(Meta.RebirthArmed, Is.False);
            Assert.That(Meta.PressRebirth(), Is.Null, "해제 뒤 누름은 다시 확인 대기(부활하지 않음)");
            Assert.That(Store.Rebirth.Lv, Is.EqualTo(0));
        }
    }
}
