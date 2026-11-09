using System;
using MV.Save;
using NUnit.Framework;

namespace MV.Meta.Tests
{
    /// <summary>부활(04 §4.12.3), 광고 한도·UTC 날짜 넘김(04 §4.12.4), 해금·신 선택(07 §7.8.9, 04 §4.12.5).</summary>
    public sealed class MvMetaRebirthAdTests : MvMetaTestBase
    {
        const long CooldownMs = 21600L * 1000L;

        /// <summary>Area 2(0기반 2) 진행 중인 저장: 부활 열림.</summary>
        void ArrangeMidGame()
        {
            SetWealth((MvWealthIds.Gold, 500), (MvWealthIds.Souls, 70), (MvWealthIds.Dia, 40), (MvWealthIds.RbToken, 1), (MvWealthIds.Medal, 5));
            SetContainer(MvSaveKeys.Progress, "\"area\":2,\"level\":7,\"runs\":5,\"cp\":1,\"lastSettledRunSeq\":12,\"bars\":{\"A2\":[[1]]}");
            SetContainer(MvSaveKeys.Rebirth, "\"pending\":9,\"perks\":{\"power\":2}");
            SetContainer(MvSaveKeys.Lobby, "\"up\":{\"stair\":3},\"deck\":[0,2,1,0,0,0,0,0,0]");
            SetContainer(MvSaveKeys.AreaUp, "\"lv\":{\"rate\":2}");
            SetContainer(MvSaveKeys.God, "\"sel\":\"thunder\"");
        }

        [Test]
        public void Rebirth_LockedAtArea0()
        {
            Assert.That(Meta.RebirthOpen, Is.False);
            Assert.That(Meta.Rebirth().Code, Is.EqualTo(MvMetaErrors.Locked));
            Assert.That(Meta.PressRebirth().Code, Is.EqualTo(MvMetaErrors.Locked));
            SetContainer(MvSaveKeys.Progress, "\"area\":1");
            Assert.That(Meta.RebirthOpen, Is.True, "rbOpen = area ≥ 1");
        }

        [Test]
        public void Rebirth_MovesPendingToTokens_ResetsRun_KeepsKeepFields()
        {
            ArrangeMidGame();
            MvMetaResponse response = Meta.Rebirth();
            Assert.That(response.Ok, Is.True, response.Code);

            Assert.That(W(MvWealthIds.RbToken), Is.EqualTo(1 + 9), "tokens += pending");
            Assert.That(Store.Rebirth.Pending, Is.EqualTo(0));
            Assert.That(Store.Rebirth.Lv, Is.EqualTo(1));
            Assert.That(Store.Rebirth.CdUntil, Is.EqualTo(T0 + CooldownMs));
            Assert.That(Store.Rebirth.GetPerk("power"), Is.EqualTo(2), "특성 유지");
            Assert.That(W(MvWealthIds.Gold), Is.EqualTo(0));
            Assert.That(W(MvWealthIds.Souls), Is.EqualTo(0));
            Assert.That(W(MvWealthIds.Dia), Is.EqualTo(40), "캣닢 유지");
            Assert.That(Store.Progress.Area, Is.EqualTo(0));
            Assert.That(Store.Progress.Cp, Is.EqualTo(0));
            Assert.That(Store.Progress.BarAreaKeys, Is.Empty);
            Assert.That(Store.Progress.Level, Is.EqualTo(7), "Level 유지(D-25 정본)");
            Assert.That(Store.Progress.Runs, Is.EqualTo(5));
            Assert.That(Store.Progress.LastSettledRunSeq, Is.EqualTo(12), "D-META-01 정산 순번 유지");
            Assert.That(Store.Progress.BestArea, Is.EqualTo(2));
            Assert.That(Store.Lobby.Up.Values, Is.All.EqualTo(0));
            Assert.That(Store.Lobby.Deck, Is.EqualTo(new[] { 0, 1, 0, 0, 0, 0, 0, 0, 0 }));
            Assert.That(Store.AreaUp.Levels.Values, Is.All.EqualTo(0));
            Assert.That(Store.God.Selected, Is.EqualTo("thunder"));
            Assert.That(Store.Unlock.Opened, Does.Contain(MvMetaRules.RebirthKey), "rb.open 유지(sticky)");
            Assert.That(Meta.RebirthOpen, Is.True);

            Assert.That(Meta.CanRebirth, Is.False);
            Assert.That(Meta.Rebirth().Code, Is.EqualTo(MvMetaErrors.Cooldown));
            Clock.Advance(TimeSpan.FromHours(6));
            Assert.That(Meta.RebirthCooldownLeftMs, Is.EqualTo(0));
            Assert.That(Meta.CanRebirth, Is.True);
        }

        [Test]
        public void Rebirth_KeepsSettledRunSeq_SoOldRunCannotSettleAgain()
        {
            ArrangeMidGame();
            Assert.That(Meta.Rebirth().Ok, Is.True);
            Assert.That(Meta.Settle(Outcome(12, MvRunEnd.Wall, gold: 9)).Code, Is.EqualTo(MvMetaErrors.AlreadySettled));
            Assert.That(Meta.BeginRun(), Is.EqualTo(13));
        }

        [Test]
        public void PressRebirth_NeedsTwoPressesWithinArmSec()
        {
            ArrangeMidGame();
            Assert.That(Meta.PressRebirth(), Is.Null, "첫 누름은 대기");
            Assert.That(Meta.RebirthArmed, Is.True);
            Clock.Advance(TimeSpan.FromSeconds(3));
            Assert.That(Meta.RebirthArmed, Is.False, "2.5초 지나면 풀림");
            Assert.That(Meta.PressRebirth(), Is.Null, "다시 첫 누름");
            Assert.That(Store.Rebirth.Lv, Is.EqualTo(0));
            Clock.Advance(TimeSpan.FromSeconds(2));
            MvMetaResponse done = Meta.PressRebirth();
            Assert.That(done, Is.Not.Null);
            Assert.That(done.Ok, Is.True, done.Code);
            Assert.That(Store.Rebirth.Lv, Is.EqualTo(1));
        }

        [Test]
        public void DiaAd_DailyLimit_ResetsAtUtcMidnight()
        {
            // 2023-11-14T23:59:59Z.
            Clock.NowMs = DateTimeOffset.Parse("2023-11-14T23:59:59Z").ToUnixTimeMilliseconds();
            Assert.That(Meta.Today, Is.EqualTo("2023-11-14"));
            Assert.That(Meta.AdsLeft("dia"), Is.EqualTo(3));
            for (int i = 0; i < 3; i++)
            {
                MvMetaResponse got = null;
                Meta.WatchAdForReward("dia", r => got = r);
                Assert.That(got.Ok, Is.True, got.Code);
            }
            Assert.That(W(MvWealthIds.Dia), Is.EqualTo(300));
            Assert.That(Meta.AdsLeft("dia"), Is.EqualTo(0));
            MvMetaResponse limited = null;
            Meta.WatchAdForReward("dia", r => limited = r);
            Assert.That(limited.Code, Is.EqualTo(MvMetaErrors.DailyLimit));
            Assert.That(Ads.RewardShown, Is.EqualTo(3), "한도면 광고를 띄우지 않는다");
            Assert.That(W(MvWealthIds.Dia), Is.EqualTo(300));

            Clock.Advance(TimeSpan.FromSeconds(1));
            Assert.That(Meta.Today, Is.EqualTo("2023-11-15"));
            Assert.That(Meta.AdsLeft("dia"), Is.EqualTo(3), "UTC 날짜가 바뀌면 한도 회복");
            MvMetaResponse next = null;
            Meta.WatchAdForReward("dia", r => next = r);
            Assert.That(next.Ok, Is.True, next.Code);
            Assert.That(Store.Daily.Day, Is.EqualTo("2023-11-15"));
            Assert.That(Store.Daily.GetAdUsed("dia"), Is.EqualTo(1));
            Assert.That(W(MvWealthIds.Dia), Is.EqualTo(400));
        }

        [Test]
        public void RewardAd_NotWatched_GrantsNothing()
        {
            Ads.NextRewardResult = false;
            MvMetaResponse got = null;
            Meta.WatchAdForReward("dia", r => got = r);
            Assert.That(got.Code, Is.EqualTo(MvMetaErrors.AdNotWatched));
            Assert.That(W(MvWealthIds.Dia), Is.EqualTo(0));
            Assert.That(Meta.AdsLeft("dia"), Is.EqualTo(3));
        }

        [Test]
        public void SkipAd_CutsCooldown70Min_OnlyWhileCooling()
        {
            ArrangeMidGame();
            MvMetaResponse noCool = null;
            Meta.WatchAdForReward("skip", r => noCool = r);
            Assert.That(noCool.Code, Is.EqualTo(MvMetaErrors.NoCooldown));
            Assert.That(Ads.RewardShown, Is.EqualTo(0));

            Assert.That(Meta.Rebirth().Ok, Is.True);
            MvMetaResponse skipped = null;
            Meta.WatchAdForReward("skip", r => skipped = r);
            Assert.That(skipped.Ok, Is.True, skipped.Code);
            Assert.That(Store.Rebirth.CdUntil, Is.EqualTo(T0 + CooldownMs - 4200L * 1000L));
            Assert.That(Meta.AdsLeft("skip"), Is.EqualTo(3));
            Assert.That(Ads.LastPlacement, Is.EqualTo("rebirth_skip"));
        }

        [Test]
        public void Unlock_ByRunsAndArea_MarkSeen()
        {
            Assert.That(Meta.IsOpen("shop"), Is.True, "conditionType 0");
            Assert.That(Meta.IsOpen("attendance"), Is.False);
            Assert.That(Meta.IsOpen("mode.bigMouse"), Is.False, "active 0은 열리지 않는다");

            MvSettleResult r = Meta.Settle(Outcome(Meta.BeginRun(), MvRunEnd.Wall));
            Assert.That(r.NewlyOpened, Does.Contain("attendance"), "본편 런 정산 수 ≥ 1");
            Assert.That(r.NewlyOpened, Does.Not.Contain("shop"));
            Assert.That(Meta.IsOpen("attendance"), Is.True);
            Assert.That(Meta.IsNew("attendance"), Is.True);
            Assert.That(Meta.PendingUnlockShows(), Does.Contain("attendance"));

            Assert.That(Meta.MarkSeen(new[] { "attendance" }, new[] { "attendance" }, "mode").Ok, Is.True);
            Assert.That(Meta.PendingUnlockShows(), Does.Not.Contain("attendance"));
            Assert.That(Meta.IsNew("attendance"), Is.False);
            Assert.That(Store.Settings.ContentTab, Is.EqualTo("mode"));

            Assert.That(Meta.IsOpen("contentMenu"), Is.False);
            SetContainer(MvSaveKeys.Progress, "\"area\":2");
            Assert.That(Meta.IsOpen("contentMenu"), Is.True, "areaNo 3 ≥ 3");
        }

        [Test]
        public void SelectGod_OnlyUnlocked()
        {
            Assert.That(Meta.SelectGod("nope").Code, Is.EqualTo(MvMetaErrors.UnknownKey));
            Assert.That(Meta.GodUnlocked("flame"), Is.False);
            Assert.That(Meta.SelectGod("flame").Code, Is.EqualTo(MvMetaErrors.Locked));
            Assert.That(Store.God.Selected, Is.EqualTo("thunder"));

            SetContainer(MvSaveKeys.Progress, "\"area\":3");
            Assert.That(Meta.GodUnlocked("flame"), Is.True, "god.flame: areaNo 4");
            Assert.That(Meta.SelectGod("flame").Ok, Is.True);
            Assert.That(Store.God.Selected, Is.EqualTo("flame"));
        }
    }
}
