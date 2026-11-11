using System.Collections.Generic;
using MV.Data;
using MV.Save;
using NUnit.Framework;

namespace MV.Meta.Tests
{
    /// <summary>결과 정산(04 §4.11.1~4.11.2 collect): wall·gate·pass·zoneEnd·×3·중복 runSeq·정산 대기.</summary>
    public sealed class MvMetaSettleTests : MvMetaTestBase
    {
        [Test]
        public void Wall_PaysFloorGold_RecordsBar_AndTokens()
        {
            long seq = Meta.BeginRun();
            Assert.That(seq, Is.EqualTo(1));
            MvSettleResult r = Meta.Settle(Outcome(seq, MvRunEnd.Wall, gold: 10.7, souls: 6, dia: 2, cpRun: 1, bars: OneBar()));

            Assert.That(r.Ok, Is.True, r.Code);
            Assert.That(r.Pass, Is.False);
            Assert.That(r.ZoneEnd, Is.False);
            Assert.That(r.Tokens, Is.EqualTo(1 + 6 / 4), "TOK_BASE + floor(souls/4)");
            Assert.That(W(MvWealthIds.Gold), Is.EqualTo(10), "floor(gold × 1)");
            Assert.That(W(MvWealthIds.Souls), Is.EqualTo(6));
            Assert.That(W(MvWealthIds.Dia), Is.EqualTo(2));
            Assert.That(W(MvWealthIds.RbToken), Is.EqualTo(0), "조각은 대기분으로만 쌓인다");
            Assert.That(Store.Rebirth.Pending, Is.EqualTo(2));
            Assert.That(Store.Progress.Level, Is.EqualTo(2));
            Assert.That(Store.Progress.Runs, Is.EqualTo(1));
            Assert.That(Store.Progress.Area, Is.EqualTo(0));
            Assert.That(Store.Progress.Cp, Is.EqualTo(1));
            Assert.That(Store.Progress.LastSettledRunSeq, Is.EqualTo(seq));
            Assert.That(Store.Progress.BarAreaKeys, Is.EqualTo(new[] { MvProgressContainer.BarKey(0) }));
            Assert.That(r.ShowInterstitial, Is.False, "area 0 < interstitialMinArea");
        }

        [Test]
        public void DuplicateRunSeq_IsRejected_AndChangesNothing()
        {
            long seq = Meta.BeginRun();
            Assert.That(Meta.Settle(Outcome(seq, MvRunEnd.Wall, gold: 5, souls: 1)).Ok, Is.True);
            MvSettleResult again = Meta.Settle(Outcome(seq, MvRunEnd.Wall, gold: 5, souls: 1));
            Assert.That(again.Ok, Is.False);
            Assert.That(again.Code, Is.EqualTo(MvMetaErrors.AlreadySettled));
            MvSettleResult older = Meta.Settle(Outcome(seq - 1, MvRunEnd.Wall, gold: 5));
            Assert.That(older.Code, Is.EqualTo(MvMetaErrors.AlreadySettled));
            Assert.That(W(MvWealthIds.Gold), Is.EqualTo(5));
            Assert.That(Store.Progress.Runs, Is.EqualTo(1));
            Assert.That(Store.Rebirth.Pending, Is.EqualTo(1));
            Assert.That(Meta.BeginRun(), Is.EqualTo(seq + 1));
        }

        [Test]
        public void Gate_WithoutEnoughSouls_DoesNotPass()
        {
            MvSettleResult r = Meta.Settle(Outcome(Meta.BeginRun(), MvRunEnd.Gate, souls: 10));
            Assert.That(r.Ok, Is.True, r.Code);
            Assert.That(r.Pass, Is.False);
            Assert.That(r.ZoneEnd, Is.False);
            Assert.That(r.Tokens, Is.EqualTo(1 + 2 + 2), "1 + floor(10/4) + gate 2");
            Assert.That(Store.Progress.Area, Is.EqualTo(0));
        }

        [Test]
        public void Gate_Pass_OpensNextArea_ResetsGrowth_AndUnlocksRebirth()
        {
            // 누적 생선 45 + 5 = 50 = 302_area[1].needSouls.
            SetWealth((MvWealthIds.Souls, 45), (MvWealthIds.Gold, 7), (MvWealthIds.Dia, 3));
            SetContainer(MvSaveKeys.Lobby, "\"up\":{\"stair\":3,\"income\":2},\"deck\":[0,2,1,0,0,0,0,0,0]");
            SetContainer(MvSaveKeys.AreaUp, "\"lv\":{\"rate\":2}");
            // 정산으로 Level +1 → 부활 사용 Level에 닿는다(05_contentOpen[rebirth]).
            SetContainer(MvSaveKeys.Progress, "\"cp\":2,\"level\":" + (RebirthUseLevel - 1) + ",\"bars\":{\"A0\":[[1,2]]}");
            Assert.That(Meta.IsOpen(MvMetaRules.RebirthKey), Is.False);

            MvSettleResult r = Meta.Settle(Outcome(Meta.BeginRun(), MvRunEnd.Gate, gold: 1, souls: 5, cpRun: 1, bars: OneBar()));

            Assert.That(r.Ok, Is.True, r.Code);
            Assert.That(r.Pass, Is.True);
            Assert.That(r.ZoneEnd, Is.False);
            Assert.That(r.AreaBefore, Is.EqualTo(0));
            Assert.That(r.AreaAfter, Is.EqualTo(1));
            Assert.That(r.ZoneChanged, Is.False);
            Assert.That(r.Tokens, Is.EqualTo(1 + 1 + 2 + 3));
            Assert.That(Store.Progress.Area, Is.EqualTo(1));
            Assert.That(Store.Progress.BestArea, Is.EqualTo(1));
            Assert.That(Store.Progress.Cp, Is.EqualTo(0));
            Assert.That(Store.Progress.BarAreaKeys, Is.Empty, "개방이면 bars 비움");
            Assert.That(Store.Lobby.Up.Values, Is.All.EqualTo(0), "로비 강화 초기화");
            Assert.That(Store.Lobby.Deck, Is.EqualTo(new[] { 0, 1, 0, 0, 0, 0, 0, 0, 0 }), "시작 덱 초기화");
            Assert.That(Store.AreaUp.Levels.Values, Is.All.EqualTo(0), "Area 강화 초기화");
            Assert.That(W(MvWealthIds.Gold), Is.EqualTo(8), "재화 유지");
            Assert.That(W(MvWealthIds.Dia), Is.EqualTo(3));
            Assert.That(W(MvWealthIds.Souls), Is.EqualTo(50));
            Assert.That(r.NewlyOpened, Does.Contain(MvMetaRules.RebirthKey));
            Assert.That(Store.Unlock.Opened, Does.Contain(MvMetaRules.RebirthKey), "rebirth는 sticky");
            Assert.That(Meta.RebirthOpen, Is.True);
        }

        [Test]
        public void Gate_AtLastArea_IsZoneEnd()
        {
            // PLAYABLE_AREAS 5 → area 4가 마지막.
            SetContainer(MvSaveKeys.Progress, "\"area\":4");
            SetWealth((MvWealthIds.Souls, 10_000));
            MvSettleResult r = Meta.Settle(Outcome(Meta.BeginRun(), MvRunEnd.Gate, gold: 3, bars: OneBar()));

            Assert.That(r.Ok, Is.True, r.Code);
            Assert.That(r.ZoneEnd, Is.True);
            Assert.That(r.Pass, Is.False);
            Assert.That(r.Tokens, Is.EqualTo(1 + 0 + 2 + 3));
            Assert.That(Store.Progress.Area, Is.EqualTo(4));
            Assert.That(Store.Progress.ZoneClear, Is.True);
            Assert.That(Store.Progress.BarAreaKeys, Is.EqualTo(new[] { "A4" }));
            Assert.That(r.ShowInterstitial, Is.True, "mul 1 && area ≥ 3");
        }

        [Test]
        public void Times3_MultipliesGoldAndDiaOnly()
        {
            // 배수는 08 collect3.mul 하나(MV-SPEC2-FIX1).
            int mul = MvMetaRules.CollectAdMul(Tables);
            Assert.That(mul, Is.GreaterThan(1), "08 collect3.mul");
            SetContainer(MvSaveKeys.Progress, "\"area\":3");
            MvSettleResult r = Meta.Settle(Outcome(Meta.BeginRun(), MvRunEnd.Wall, gold: 10.5, souls: 4, dia: 3), mul);

            Assert.That(r.Ok, Is.True, r.Code);
            Assert.That(W(MvWealthIds.Gold), Is.EqualTo((long)System.Math.Floor(10.5 * mul)), "floor(10.5 × mul)");
            Assert.That(W(MvWealthIds.Dia), Is.EqualTo(3 * mul));
            Assert.That(W(MvWealthIds.Souls), Is.EqualTo(4), "생선은 배수 없음");
            Assert.That(r.Tokens, Is.EqualTo(2), "조각은 배수 없음");
            Assert.That(Store.Rebirth.Pending, Is.EqualTo(2));
            Assert.That(Store.Progress.Level, Is.EqualTo(2), "Level은 +1");
            Assert.That(r.ShowInterstitial, Is.False, "×3이면 전면 광고 없음");
        }

        /// <summary>PC-15: 결과 광고 ×N은 런에서 얻은 몫에만. 시작 골드·블루 마법석은 ×1로 지급한다.</summary>
        [Test]
        public void Times3_SkipsStartShare_StartPaidOnce()
        {
            int mul = MvMetaRules.CollectAdMul(Tables);
            var outcome = Outcome(Meta.BeginRun(), MvRunEnd.Wall, gold: 10.5, dia: 3);
            outcome.StartGold = 4;
            outcome.StartDia = 1;
            MvSettleResult r = Meta.Settle(outcome, mul);

            Assert.That(r.Ok, Is.True, r.Code);
            Assert.That(W(MvWealthIds.Gold), Is.EqualTo((long)System.Math.Floor((10.5 - 4) * mul + 4)), "floor(얻은 몫 × mul + 시작 몫)");
            Assert.That(W(MvWealthIds.Dia), Is.EqualTo((3 - 1) * mul + 1));
            Assert.That(r.GoldGained, Is.EqualTo(W(MvWealthIds.Gold)));
        }

        /// <summary>PC-15: ×1 정산은 시작 몫을 포함한 합계를 그대로 지급한다.</summary>
        [Test]
        public void Times1_PaysStartShare()
        {
            var outcome = Outcome(Meta.BeginRun(), MvRunEnd.Wall, gold: 150.5, dia: 2);
            outcome.StartGold = 150;
            outcome.StartDia = 2;
            Assert.That(MvMeta.CanCollectX3(outcome), Is.False, "얻은 몫이 없으면 ×N 버튼 꺼짐");
            MvSettleResult r = Meta.Settle(outcome);

            Assert.That(r.Ok, Is.True, r.Code);
            Assert.That(W(MvWealthIds.Gold), Is.EqualTo(150));
            Assert.That(W(MvWealthIds.Dia), Is.EqualTo(2));
        }

        [Test]
        public void BadMul_IsRejected()
        {
            MvSettleResult r = Meta.Settle(Outcome(Meta.BeginRun(), MvRunEnd.Wall, gold: 10), MvMetaRules.CollectAdMul(Tables) + 1);
            Assert.That(r.Code, Is.EqualTo(MvMetaErrors.BadMul));
            Assert.That(W(MvWealthIds.Gold), Is.EqualTo(0));
            Assert.That(Store.Progress.Runs, Is.EqualTo(0));
        }

        [Test]
        public void SettleWithAd_WatchedTimes3_NotWatchedSettlesNothing()
        {
            var outcome = Outcome(Meta.BeginRun(), MvRunEnd.Wall, gold: 2, dia: 1);
            Assert.That(MvMeta.CanCollectX3(outcome), Is.True);
            Assert.That(MvMeta.CanCollectX3(Outcome(1, MvRunEnd.Wall, gold: 0.5)), Is.False);

            Ads.NextRewardResult = false;
            MvSettleResult skipped = null;
            Meta.SettleWithAd(outcome, r => skipped = r);
            Assert.That(skipped.Code, Is.EqualTo(MvMetaErrors.AdNotWatched));
            Assert.That(Store.Progress.Runs, Is.EqualTo(0));

            Ads.NextRewardResult = true;
            MvSettleResult done = null;
            Meta.SettleWithAd(outcome, r => done = r);
            Assert.That(done.Ok, Is.True, done.Code);
            int mul = MvMetaRules.CollectAdMul(Tables);
            Assert.That(done.Mul, Is.EqualTo(mul));
            Assert.That(W(MvWealthIds.Gold), Is.EqualTo(2 * mul));
            Assert.That(W(MvWealthIds.Dia), Is.EqualTo(1 * mul));
            Assert.That(Ads.LastPlacement, Is.EqualTo("result_x3"));
            Assert.That(Meta.AdsLeft("collect3"), Is.EqualTo(-1), "×3은 한도 없음");
        }

        [Test]
        public void PendingSettle_AppliesWealthAndRbPendingOnly()
        {
            var pending = new List<ConsumeData>
            {
                new ConsumeData(ConsumeType.Wealth, MvWealthIds.Medal, 2),
                new ConsumeData(ConsumeType.RbPending, 0, 3),
                new ConsumeData(ConsumeType.Card, 110001001, 1),
            };
            MvSettleResult r = Meta.Settle(Outcome(Meta.BeginRun(), MvRunEnd.Wall, pending: pending));
            Assert.That(r.Ok, Is.True, r.Code);
            Assert.That(W(MvWealthIds.Medal), Is.EqualTo(2));
            Assert.That(Store.Rebirth.Pending, Is.EqualTo(1 + 3), "조각 1 + 정산 대기 608 3");
            Assert.That(r.Unsettled.Count, Is.EqualTo(1));
            Assert.That(r.Unsettled[0].Type, Is.EqualTo(ConsumeType.Card));
        }
    }
}
