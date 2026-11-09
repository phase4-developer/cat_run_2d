using System;
using System.Collections.Generic;
using System.Linq;
using MV.Data;
using MV.Run;
using NUnit.Framework;

namespace MV.Sound.Tests
{
    /// <summary>GAP(시계 주입), 피치 범위, 음소거, 지연 재생, BGM 레이어 전환 순서.</summary>
    public sealed class MvSoundPlayerTests
    {
        FakeClock _clock;
        RecordingOut _out;
        List<string> _warnings;

        MvSoundPlayer Make(MvTables t = null, Random rng = null)
        {
            _clock = new FakeClock();
            _out = new RecordingOut();
            _warnings = new List<string>();
            t ??= SoundTestTables.Load();
            return new MvSoundPlayer(_out, _clock, () => t, rng ?? new Random(1)) { Warn = _warnings.Add };
        }

        // ---------------- GAP ----------------

        [Test]
        public void Gap_DropsRequestsInsideGapMs()
        {
            var p = Make();
            _clock.Now = 10.0;
            Assert.That(p.Play("hit"), Is.True);
            _clock.Now = 10.034;
            Assert.That(p.Play("hit"), Is.False, "35ms 안은 버린다");
            _clock.Now = 10.036;
            Assert.That(p.Play("hit"), Is.True, "버린 요청은 기록하지 않는다(큐잉 없음)");
            _clock.Now = 10.06;
            Assert.That(p.Play("hit"), Is.False);
            Assert.That(_out.Addresses(), Is.EqualTo(new[] { "Sfx_Hit", "Sfx_Hit" }));
        }

        [Test]
        public void Gap_UsesRowGapMs_DefaultTwentyAndThrowSeventy()
        {
            var p = Make();
            _clock.Now = 1.0; Assert.That(p.Play("click"), Is.True);
            _clock.Now = 1.019; Assert.That(p.Play("click"), Is.False);
            _clock.Now = 1.021; Assert.That(p.Play("click"), Is.True);
            _clock.Now = 2.0; Assert.That(p.Play("throw"), Is.True);
            _clock.Now = 2.069; Assert.That(p.Play("throw"), Is.False);
            _clock.Now = 2.071; Assert.That(p.Play("throw"), Is.True);
        }

        [Test]
        public void Gap_SameMomentDuplicatePlaysOnce_OtherKeysIndependent()
        {
            var p = Make();
            _clock.Now = 3.0;
            Assert.That(p.Play("coin"), Is.True);
            Assert.That(p.Play("coin"), Is.False);
            Assert.That(p.Play("wood"), Is.True);
            Assert.That(p.Play("hit"), Is.True);
            Assert.That(p.Play("crit"), Is.True, "hit와 crit는 다른 키");
            Assert.That(_out.Addresses(), Is.EqualTo(new[] { "Sfx_Coin", "Sfx_Wood", "Sfx_Hit", "Sfx_Crit" }));
        }

        [Test]
        public void Gap_VariantKeysShareOneGroup()
        {
            var p = Make();
            _clock.Now = 5.0; Assert.That(p.Play("tierup_1"), Is.True);
            _clock.Now = 5.01; Assert.That(p.Play("tierup_3"), Is.False, "tierup 묶음 하나로 잰다");
            _clock.Now = 5.025; Assert.That(p.Play("tierup_2"), Is.True);
            _clock.Now = 5.03; Assert.That(p.Play("card_4"), Is.True, "card는 다른 묶음");
            Assert.That(_out.Addresses(), Is.EqualTo(new[] { "Sfx_Tierup_1", "Sfx_Tierup_2", "Sfx_Card_4" }));
        }

        [Test]
        public void Play_PassesAddressAndVolume_UnknownKeyWarnsOnce()
        {
            var p = Make();
            Assert.That(p.Play("throw"), Is.True);
            Assert.That(_out.Played[0].Address, Is.EqualTo("Sfx_Throw"));
            Assert.That(_out.Played[0].Volume, Is.EqualTo(1f));
            Assert.That(p.Play("boom"), Is.False, "잔재 키(D-55)는 행이 없다");
            _clock.Now = 1;
            Assert.That(p.Play("boom"), Is.False);
            Assert.That(p.Play("bgm_run"), Is.False, "음악 행은 효과음으로 울리지 않는다");
            Assert.That(_warnings.Count, Is.EqualTo(2));
            Assert.That(_out.Played.Count, Is.EqualTo(1));
        }

        // ---------------- 피치 ----------------

        [Test]
        public void Pitch_CanonicalRowsAreExactlyOne()
        {
            var p = Make();
            var t = SoundTestTables.Load();
            foreach (var row in t.Sound.Datas.Where(r => r.soundType == SoundType.Sfx))
            {
                _clock.Now += 1;
                Assert.That(p.Play(row.key), Is.True, row.key);
            }
            Assert.That(_out.Played.Count, Is.EqualTo(31));
            Assert.That(_out.Played.All(s => s.Pitch == 1f), Is.True, "정본 pitchRand = 0");
        }

        [Test]
        public void Pitch_RandomStaysWithinPitchRand()
        {
            var tsv = SoundTestTables.SetByKey(SoundTestTables.ReadTsv("03_sound"), "hit", "pitchRand", "0.2");
            var t = SoundTestTables.Load(new Dictionary<string, string> { ["03_sound"] = tsv });
            var p = Make(t, new Random(12345));
            for (int i = 0; i < 400; i++)
            {
                _clock.Now += 1;
                Assert.That(p.Play("hit"), Is.True);
            }
            var pitches = _out.Played.Select(s => s.Pitch).ToList();
            Assert.That(pitches.All(x => x >= 0.8f - 1e-6f && x <= 1.2f + 1e-6f), Is.True, $"범위 밖: {pitches.Min()}~{pitches.Max()}");
            Assert.That(pitches.Min(), Is.LessThan(0.9f));
            Assert.That(pitches.Max(), Is.GreaterThan(1.1f));

            var q = Make(t, new Random(12345));
            for (int i = 0; i < 400; i++) { _clock.Now += 1; q.Play("hit"); }
            Assert.That(_out.Played.Select(s => s.Pitch), Is.EqualTo(pitches), "피치 난수는 주입한 Random만 쓴다");
        }

        // ---------------- 음소거·지연 ----------------

        [Test]
        public void Mute_BlocksSfxWithoutRecordingGap_MusicTrackOnly()
        {
            var p = Make();
            p.EnterLobby();
            p.Muted = true;
            _clock.Now = 1.0;
            Assert.That(p.Play("hit"), Is.False);
            p.Muted = false;
            Assert.That(p.Play("hit"), Is.True, "음소거 중 요청은 GAP을 기록하지 않는다(L1678 muted 먼저)");
            Assert.That(_out.Music, Is.EqualTo(new[] { "music:Bgm_Lobby:0:start", "on:false", "on:true" }));
        }

        [Test]
        public void RunFinished_FailPlaysAfter350ms_GateWinImmediately()
        {
            var p = Make();
            _clock.Now = 0;
            p.OnRunEvent(new RunEvent(RunEventType.RunFinished, a: (int)FinishReason.Wall, flag: false));
            Assert.That(_out.Played, Is.Empty);
            Assert.That(p.PendingCount, Is.EqualTo(1));
            _clock.Now = 0.349; p.Tick();
            Assert.That(_out.Played, Is.Empty);
            _clock.Now = 0.351; p.Tick();
            Assert.That(_out.Addresses(), Is.EqualTo(new[] { "Sfx_Fail" }));
            Assert.That(p.PendingCount, Is.EqualTo(0));

            p.OnRunEvent(new RunEvent(RunEventType.RunFinished, a: (int)FinishReason.Gate));
            Assert.That(_out.Addresses(), Is.EqualTo(new[] { "Sfx_Fail", "Sfx_Win" }));
        }

        [Test]
        public void Delayed_ChecksMuteWhenItFires_AndKeepsOrder()
        {
            var p = Make();
            _clock.Now = 0;
            p.PlayDelayed("merge", 450);
            p.PlayDelayed("fail", 350);
            _clock.Now = 1; p.Tick();
            Assert.That(_out.Addresses(), Is.EqualTo(new[] { "Sfx_Fail", "Sfx_Merge" }));

            p.PlayDelayed("win", 350);
            p.Muted = true;
            _clock.Now = 2; p.Tick();
            Assert.That(_out.Played.Count, Is.EqualTo(2), "울릴 때 음소거면 막는다(정본 setTimeout → SND.play)");
        }

        [Test]
        public void RunEvents_WallBreakSoulPlaysWoodThenSoul()
        {
            var p = Make();
            var t = SoundTestTables.Load();
            int wall = t.ObjectKind.ByKey("wall").id;
            _clock.Now = 4;
            p.OnRunEvents(new[]
            {
                new RunEvent(RunEventType.Hit, objId: 3, kindId: wall),
                new RunEvent(RunEventType.Break, objId: 3, kindId: wall, a: (int)FieldReward.Soul),
                new RunEvent(RunEventType.Gain, objId: 3, a: (int)ConsumeType.RunSouls),
                new RunEvent(RunEventType.Hitstop, amount: 0.06),
            });
            Assert.That(_out.Addresses(), Is.EqualTo(new[] { "Sfx_Wood", "Sfx_Soul" }));
        }

        // ---------------- BGM 레이어 ----------------

        [Test]
        public void Bgm_LayerSwitchOrder_KeepsPositionWithCrossfade()
        {
            var p = Make();
            p.EnterLobby();
            p.EnterLobby();
            p.OnRunEvent(new RunEvent(RunEventType.RunStarted, a: 0, b: 1, c: 0));
            p.EnterRun();
            p.EnterLobby();
            p.Muted = true;
            p.EnterRun();
            p.Muted = false;
            p.SetBgmLayer(MvBgmLayer.None);
            p.EnterRun();
            Assert.That(_out.Music, Is.EqualTo(new[]
            {
                "music:Bgm_Lobby:0:start",
                "music:Bgm_Run:0.03:keep",
                "music:Bgm_Lobby:0.03:keep",
                "on:false",
                "music:Bgm_Run:0.03:keep",
                "on:true",
                "stop",
                "music:Bgm_Run:0:start",
            }));
            Assert.That(p.BgmLayer, Is.EqualTo(MvBgmLayer.Run));
            Assert.That(_out.Played, Is.Empty, "RunStarted는 효과음이 없다");
        }

        [Test]
        public void Bgm_RunKeyFromCaller_MissingKeyWarns()
        {
            var p = Make();
            p.RunBgmKey = "bgm_none";
            p.EnterRun();
            Assert.That(_out.Music, Is.Empty);
            Assert.That(_warnings.Count, Is.EqualTo(1));
            p.RunBgmKey = MvSoundKeys.BgmRun;
            p.EnterLobby();
            Assert.That(_out.Music, Is.EqualTo(new[] { "music:Bgm_Lobby:0.03:keep" }));
        }
    }
}
