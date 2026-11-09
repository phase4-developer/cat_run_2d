using System;
using System.Collections.Generic;
using MV.Meta;
using MV.Run;
using NUnit.Framework;

namespace MV.UI.Tests
{
    /// <summary>
    /// 결과 ×3(05 §5.6·§5.12): 광고를 끝까지 봤을 때만 ×배수 정산 1번, 정산은 어느 길이든 1번,
    /// 켜짐 조건(츄르 ≥ 1 또는 캣닢 &gt; 0, 광고 입구 준비). 가짜 광고 팝업 콜백은 한 번만.
    /// </summary>
    public sealed class ResultAdTests
    {
        sealed class Settler : IResultSettler
        {
            public readonly List<int> Muls = new List<int>();
            public MvSettleResult Settle(MvRunOutcome outcome, int mul)
            {
                Muls.Add(mul);
                return new MvSettleResult { Mul = mul };
            }
        }

        /// <summary>광고 결과를 손으로 끝내는 가짜 입구.</summary>
        sealed class Watcher : IResultAdWatcher
        {
            public bool Ready { get; set; } = true;
            public int Mul => 3;
            public int Calls;
            public Action<int> Pending;
            public void WatchX3(Action<int> done) { Calls++; Pending = done; }
        }

        static ResultViewData Data(Settler s, Watcher w, double gold = 100, int dia = 0)
        {
            var r = new RunResult { Reason = FinishReason.Wall, Gold = gold, Dia = dia, Souls = 5 };
            var d = ResultViewData.Create(r, 9, 0, 0, null);
            d.Settler = s;
            d.AdWatcher = w;
            return d;
        }

        [Test]
        public void X3_Watched_SettlesOnceWithMul()
        {
            var s = new Settler();
            var w = new Watcher();
            var p = new ResultPresenter();
            p.Open(Data(s, w));
            bool? finished = null;

            Assert.IsTrue(p.TryCollectX3(ok => finished = ok));
            Assert.IsFalse(p.TryCollectX3());  // 광고 중 다시 누름
            Assert.IsFalse(p.TryCollect(1));   // 광고 중 「그냥 받기」
            w.Pending(3);

            Assert.AreEqual(true, finished);
            CollectionAssert.AreEqual(new[] { 3 }, s.Muls);
            Assert.IsFalse(p.TryCollect(1));
            Assert.IsFalse(p.TryCollectX3());
            Assert.AreEqual(1, w.Calls);
            Assert.AreEqual(1, s.Muls.Count);
        }

        [Test]
        public void X3_NotWatched_NoSettle_CollectStillOnce()
        {
            var s = new Settler();
            var w = new Watcher();
            var p = new ResultPresenter();
            p.Open(Data(s, w));
            bool? finished = null;

            p.TryCollectX3(ok => finished = ok);
            w.Pending(0);

            Assert.AreEqual(false, finished);
            Assert.IsEmpty(s.Muls);
            Assert.IsTrue(p.TryCollect(1));
            Assert.IsFalse(p.TryCollect(1));
            CollectionAssert.AreEqual(new[] { 1 }, s.Muls);
        }

        // goldCents: 츄르 × 100(소수 츄르 경계), ready·expected: 1 = 참.
        [TestCase(10000, 0, 1, 1)]
        [TestCase(0, 2, 1, 1)]
        [TestCase(50, 0, 1, 0)]
        [TestCase(10000, 0, 0, 0)]
        public void X3_Enabled_FollowsCanonCondition(int goldCents, int dia, int watcherReady, int expected)
        {
            var p = new ResultPresenter();
            p.Open(Data(new Settler(), new Watcher { Ready = watcherReady == 1 }, goldCents / 100.0, dia));
            Assert.AreEqual(expected == 1, p.CanX3);
        }

        [TestCase("watch")]
        [TestCase("close")]
        public void FakeAd_FinishesOnce(string first)
        {
            bool watchFirst = first == "watch";
            var calls = new List<bool>();
            var p = new FakeAdPresenter();
            p.Open(new FakeAdPopupData { Key = "collect3", OnFinished = calls.Add });

            if (watchFirst) Assert.IsTrue(p.PressWatch());
            else Assert.IsTrue(p.PressClose());
            Assert.IsFalse(p.PressWatch());
            Assert.IsFalse(p.PressClose());

            CollectionAssert.AreEqual(new[] { watchFirst }, calls);
            Assert.IsTrue(p.Done);
        }
    }
}
