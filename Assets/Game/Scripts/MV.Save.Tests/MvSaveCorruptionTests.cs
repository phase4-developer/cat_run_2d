using System;
using System.Text;
using jjevol;
using MV.Save;
using NUnit.Framework;

namespace MV.Save.Tests
{
    /// <summary>09 §9.7 손상·실패 처리(메모리 PlayerPrefs).</summary>
    public sealed class MvSaveCorruptionTests : MvSaveFixture
    {
        /// <summary>칸 0 = 첫 실행(level 1, 리비전 0), 칸 1 = level 5(리비전 1).</summary>
        private MvSaveOptions TwoRevisions(MemoryPrefs prefs)
        {
            var options = MvSaveTestKit.Options(prefs);
            MvSaveStore save = Track(MvSaveStore.Open(options));
            save.ApplyAndSaveJson("{\"mvProgress\":{\"level\":5}}", false, MvSaveKeys.Progress);
            save.Dispose();
            Assert.That(prefs.HasKey(MvSaveTestKit.Slot(options, 0)) && prefs.HasKey(MvSaveTestKit.Slot(options, 1)), Is.True);
            return options;
        }

        [Test]
        public void LatestSlotCorrupt_OpensRecoveredBackupAndKeepsSaving()
        {
            var prefs = new MemoryPrefs();
            MvSaveOptions options = TwoRevisions(prefs);
            string damaged = prefs.Values[MvSaveTestKit.Slot(options, 1)];
            MvSaveTestKit.CorruptSlot(prefs, MvSaveTestKit.Slot(options, 1));
            string corrupted = prefs.Values[MvSaveTestKit.Slot(options, 1)];
            Assert.That(corrupted, Is.Not.EqualTo(damaged));

            MvSaveOpenResult result = MvSaveStore.Open(options);
            MvSaveStore save = Track(result);
            Assert.That(result.Status, Is.EqualTo(MvSaveOpenStatus.RecoveredBackup), result.Error);
            Assert.That(result.Notice, Is.EqualTo(MvSaveNotice.ToastRecovered));
            Assert.That(result.RemovedSlotValues, Is.EqualTo(new[] { corrupted }), "지운 손상 칸 원문을 돌려준다");
            Assert.That(save.Progress.Level, Is.EqualTo(1), "이전 칸(리비전 0)으로 연다");

            Assert.That(save.ApplyAndSaveJson("{\"mvProgress\":{\"level\":3}}", false, MvSaveKeys.Progress), Is.True);
            save.Dispose();
            MvSaveOpenResult again = MvSaveStore.Open(options);
            Track(again);
            Assert.That(again.Status, Is.EqualTo(MvSaveOpenStatus.Opened));
            Assert.That(again.Store.Progress.Level, Is.EqualTo(3));
        }

        [Test]
        public void OlderSlotCorrupt_OpensLatestRevision()
        {
            var prefs = new MemoryPrefs();
            MvSaveOptions options = TwoRevisions(prefs);
            MvSaveTestKit.CorruptSlot(prefs, MvSaveTestKit.Slot(options, 0));

            MvSaveOpenResult result = MvSaveStore.Open(options);
            Track(result);
            Assert.That(result.Status, Is.EqualTo(MvSaveOpenStatus.RecoveredBackup), result.Error);
            Assert.That(result.Store.Progress.Level, Is.EqualTo(5));
        }

        [Test]
        public void BothSlotsCorrupt_ShowsRecoveryPopupAndStartFreshCreatesDefaults()
        {
            var prefs = new MemoryPrefs();
            MvSaveOptions options = TwoRevisions(prefs);
            MvSaveTestKit.CorruptSlot(prefs, MvSaveTestKit.Slot(options, 0));
            MvSaveTestKit.CorruptSlot(prefs, MvSaveTestKit.Slot(options, 1));
            var stored = prefs.Copy();

            MvSaveOpenResult result = MvSaveStore.Open(options);
            Track(result);
            Assert.That(result.Status, Is.EqualTo(MvSaveOpenStatus.Corrupt));
            Assert.That(result.Store, Is.Null);
            Assert.That(result.Notice, Is.EqualTo(MvSaveNotice.RecoveryPopup));
            Assert.That(prefs.Values, Is.EqualTo(stored), "자동으로 새로 만들지 않고 원문을 둔다");

            Assert.That(MvSaveStore.Open(options).Status, Is.EqualTo(MvSaveOpenStatus.Corrupt), "다시 시도도 같은 결과");

            MvSaveOpenResult fresh = MvSaveStore.OpenFresh(options);
            MvSaveStore save = Track(fresh);
            Assert.That(fresh.Status, Is.EqualTo(MvSaveOpenStatus.Created));
            Assert.That(fresh.RemovedSlotValues, Is.EquivalentTo(stored.Values));
            Assert.That(save.Progress.Level, Is.EqualTo(1));
        }

        [Test]
        public void DifferentKey_ProtectionFailedAndSaveUntouched()
        {
            var prefs = new MemoryPrefs();
            MvSaveOptions options = TwoRevisions(prefs);
            var stored = prefs.Copy();
            options.KeyProvider = new TestSaveKey("mv-test-2", "MoonVillage.Test.Key.B");

            MvSaveOpenResult result = MvSaveStore.Open(options);
            Track(result);
            Assert.That(result.Status, Is.EqualTo(MvSaveOpenStatus.ProtectionFailed));
            Assert.That(result.Notice, Is.EqualTo(MvSaveNotice.RecoveryPopup));
            Assert.That(prefs.Values, Is.EqualTo(stored));
        }

        [Test]
        public void NewerSaveVersion_AsksForUpdate()
        {
            var prefs = new MemoryPrefs();
            var key = new TestSaveKey();
            var options = MvSaveTestKit.Options(prefs, key: key);
            var encryption = new LocalSaveEncryption(key.KeyId, key.GetMasterKey());
            string envelope = "{\"saveVersion\":" + (LocalSaveCodec.CurrentVersion + 1) + ",\"profileId\":\"" + options.ProfileId +
                              "\",\"stateRevision\":0,\"savedAt\":\"2023-11-14T22:13:20.0000000Z\",\"payload\":{},\"integrity\":\"x\"}";
            prefs.Values[MvSaveTestKit.Slot(options, 0)] = Convert.ToBase64String(encryption.Encrypt(Encoding.UTF8.GetBytes(envelope), options.ProfileId));
            var stored = prefs.Copy();

            MvSaveOpenResult result = MvSaveStore.Open(options);
            Track(result);
            Assert.That(result.Status, Is.EqualTo(MvSaveOpenStatus.UnsupportedVersion));
            Assert.That(result.Notice, Is.EqualTo(MvSaveNotice.UpdatePopup));
            Assert.That(prefs.Values, Is.EqualTo(stored));
        }
    }
}
