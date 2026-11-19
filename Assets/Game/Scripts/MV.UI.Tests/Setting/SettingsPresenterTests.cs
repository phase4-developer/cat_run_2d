using System.Collections.Generic;
using NUnit.Framework;

namespace MV.UI.Tests
{
    /// <summary>설정 팝업 판단(MV-UI2-SETTING, 정크버스터즈 설정과 같은 기능).</summary>
    public sealed class SettingsPresenterTests
    {
        sealed class FakePort : ISettingsPort
        {
            public SettingsValues Stored = new SettingsValues { BgmVolume = 0.9f, SfxVolume = 1f, Haptic = true };
            public readonly List<string> Calls = new List<string>();
            public (bool bgm, bool sfx) LastApplied;

            public SettingsValues Read() => Stored;

            public bool SaveSound(SettingsValues v)
            {
                Stored.BgmVolume = v.BgmVolume; Stored.SfxVolume = v.SfxVolume; Stored.BgmMute = v.BgmMute; Stored.SfxMute = v.SfxMute;
                Calls.Add("sound:" + SettingsPresenter.ToPercent(v.BgmVolume) + "/" + SettingsPresenter.ToPercent(v.SfxVolume) + "/" + v.BgmMute + "/" + v.SfxMute);
                return true;
            }

            public bool SaveHaptic(bool on) { Stored.Haptic = on; Calls.Add("haptic:" + on); return true; }

            public void Apply(SettingsValues v, bool bgmMuted, bool sfxMuted) => LastApplied = (bgmMuted, sfxMuted);
        }

        static (SettingsPresenter, FakePort) Open()
        {
            var port = new FakePort();
            var p = new SettingsPresenter(port);
            p.Open();
            return (p, port);
        }

        [Test]
        public void Slider_AppliesNow_SavesOnlyOnFlush()
        {
            var (p, port) = Open();
            p.SetBgmVolume(0.42f);
            Assert.That(p.Values.BgmVolume, Is.EqualTo(0.42f));
            Assert.That(port.Calls, Is.Empty, "슬라이더는 바로 저장하지 않는다(화면이 잠시 뒤 저장)");
            Assert.That(p.SoundDirty, Is.True);
            Assert.That(p.FlushSound(), Is.True);
            Assert.That(port.Calls, Is.EqualTo(new[] { "sound:42/100/False/False" }));
            Assert.That(p.FlushSound(), Is.False, "바뀐 것이 없으면 다시 저장하지 않는다");
        }

        [Test]
        public void ZeroVolume_CountsAsMuted_UnmuteRestoresHalf()
        {
            var (p, port) = Open();
            p.SetSfxVolume(0f);
            Assert.That(p.SfxMuted, Is.True, "볼륨 0 = 음소거로 보임");
            Assert.That(port.LastApplied.sfx, Is.True);
            p.SetSfxMute(false);
            Assert.That(p.Values.SfxVolume, Is.EqualTo(SettingsPresenter.UnmuteVolume), "0에서 Mute를 풀면 0.5");
            Assert.That(p.SfxMuted, Is.False);
            Assert.That(port.Calls, Is.EqualTo(new[] { "sound:90/50/False/False" }), "Mute는 바로 저장");
        }

        [Test]
        public void Mute_KeepsVolume_SavesImmediately()
        {
            var (p, port) = Open();
            p.SetBgmMute(true);
            Assert.That(p.BgmMuted, Is.True);
            Assert.That(p.Values.BgmVolume, Is.EqualTo(0.9f), "Mute는 볼륨을 바꾸지 않는다");
            Assert.That(port.LastApplied, Is.EqualTo((true, false)));
            Assert.That(port.Calls, Is.EqualTo(new[] { "sound:90/100/True/False" }));
        }

        [Test]
        public void Haptic_SavesOnlyWhenChanged()
        {
            var (p, port) = Open();
            p.SetHaptic(true);
            Assert.That(port.Calls, Is.Empty, "같은 값이면 저장 안 함");
            p.SetHaptic(false);
            Assert.That(port.Calls, Is.EqualTo(new[] { "haptic:False" }));
            Assert.That(p.Values.Haptic, Is.False);
        }

        [TestCase(0.9f, 90)]
        [TestCase(0.455f, 46)]
        [TestCase(1.5f, 100)]
        [TestCase(-1f, 0)]
        public void ToPercent_RoundsAndClamps(float v, int expected) => Assert.That(SettingsPresenter.ToPercent(v), Is.EqualTo(expected));

        [Test]
        public void LanguageButtons_FollowDesignOrder_UnknownLast()
        {
            var names = new Dictionary<string, string> { { "en", "English" }, { "ja", "日本語" }, { "ko", "한국어" }, { "fr", "Français" } };
            var order = LanguageDesignView.DesignOrder(new[] { "en", "fr", "ja", "ko" }, new[] { "English", "한국어", "日本語" }, c => names[c]);
            Assert.That(order, Is.EqualTo(new[] { "en", "ko", "ja", "fr" }), "시안 English → 한국어 → 日本語, 시안에 없는 언어는 뒤");
        }

        [Test]
        public void SupportMail_EscapesAndFillsDash()
        {
            string url = SettingsPresenter.SupportMailUrl("a@b.c", "Moon", "1.0", "", "OS 1", "ko");
            StringAssert.StartsWith("mailto:a@b.c?subject=", url);
            StringAssert.Contains("&body=", url);
            StringAssert.DoesNotContain(" ", url, "공백은 이스케이프");
        }
    }
}
