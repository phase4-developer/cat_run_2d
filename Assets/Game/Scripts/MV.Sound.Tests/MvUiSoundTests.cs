using System;
using System.Collections.Generic;
using MV.Data;
using NUnit.Framework;

namespace MV.Sound.Tests
{
    /// <summary>UI 쪽 정본 SND 호출 지점 ↔ <see cref="MvUiSound"/>, 전역 입구 <see cref="MvSound"/>.</summary>
    public sealed class MvUiSoundTests
    {
        [TearDown]
        public void TearDown() => MvSound.Install(null);

        static readonly (string line, MvUiSound sound, string expected)[] Sites =
        {
            ("L1193 HAND 머지", MvUiSound.HandMerge, "merge"),
            ("L1221 변신!(런 중)", MvUiSound.HandTierUp, "lvup"),
            ("L2444 결과 다음", MvUiSound.ButtonClick, "click"),
            ("L2445 결과 ×3", MvUiSound.ButtonClick, "click"),
            ("L2530 Area 강화", MvUiSound.AreaUpgradeBuy, "buy"),
            ("L2553 부활 특성", MvUiSound.RebirthPerkBuy, "lvup"),
            ("L2563 부활", MvUiSound.RebirthDone, "win"),
            ("L2610 지도", MvUiSound.ButtonClick, "click"),
            ("L2613 신기능 해금", MvUiSound.UnlockOverlay, "lvup"),
            ("L2644 로비 강화", MvUiSound.LobbyUpgradeBuy, "buy"),
            ("L2644 덱 머지 450ms", MvUiSound.LobbyDeckMerge, "merge@450ms"),
            ("L2739 전방 장애물", MvUiSound.ObstacleWarn, "warn"),
            ("L3856 소리 버튼", MvUiSound.ButtonClick, "click"),
        };

        [Test]
        public void UiCues_MatchCanonicalSndCallSites()
        {
            var t = SoundTestTables.Load();
            var fails = new List<string>();
            foreach (var (line, sound, expected) in Sites)
            {
                var cue = MvUiSounds.Cue(sound);
                if (cue.ToString() != expected) fails.Add($"{line}: 기대 {expected} 실제 {cue}");
            }
            foreach (MvUiSound s in Enum.GetValues(typeof(MvUiSound)))
            {
                var row = t.Sound.ByKey(MvUiSounds.Cue(s).Key);
                if (row == null || row.soundType != SoundType.Sfx) fails.Add($"{s}: 03_sound에 효과음 `{MvUiSounds.Cue(s).Key}` 없음");
            }
            Assert.That(fails, Is.Empty, string.Join("\n", fails));
        }

        [Test]
        public void MvSound_FacadeRoutesUiAndIgnoresCallsBeforeInstall()
        {
            MvSound.Install(null);
            Assert.That(MvSound.PlayUi("click"), Is.False);
            MvSound.PlayUi(MvUiSound.ButtonClick);
            MvSound.Muted = true;
            Assert.That(MvSound.Muted, Is.False);

            var t = SoundTestTables.Load();
            var clock = new FakeClock { Now = 10 };
            var output = new RecordingOut();
            MvSound.Install(new MvSoundPlayer(output, clock, () => t, new Random(3)));
            MvSound.PlayUi(MvUiSound.LobbyUpgradeBuy);
            MvSound.PlayUi(MvUiSound.LobbyDeckMerge);
            Assert.That(output.Addresses(), Is.EqualTo(new[] { "Sfx_Buy" }));
            clock.Now = 10.451;
            MvSound.Tick();
            Assert.That(output.Addresses(), Is.EqualTo(new[] { "Sfx_Buy", "Sfx_Merge" }));

            MvSound.Muted = true;
            Assert.That(MvSound.PlayUi("click"), Is.False);
            MvSound.Muted = false;
            Assert.That(MvSound.PlayUi("click"), Is.True);
        }
    }
}
