using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using MV.Data;
using MV.Meta;
using MV.Run;
using NUnit.Framework;

namespace MV.UI.Tests
{
    /// <summary>
    /// 결과 「×N 보상」(MV-SPEC-02 01 §12-3, 05 §5.12): 광고를 끝까지 봤을 때만 ×배수 정산 1번, 정산은 어느 길이든 1번,
    /// 켜짐 조건(골드 ≥ 1 또는 블루 마법석 &gt; 0, 광고 입구 준비). 가짜 광고 팝업 콜백은 한 번만. 배수 N은 <c>08_adPlacement</c> 결과 광고 행 mul 한 곳.
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

        // goldCents: 골드 × 100(소수 골드 경계), ready·expected: 1 = 참.
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

        /// <summary>정본 표에서 결과 광고 행(<see cref="MvMetaResultAd.Key"/>)의 mul만 바꾼 표(파일은 바꾸지 않는다).</summary>
        static MvTables WithResultAdMul(int mul)
        {
            string dir = MvTableLoader.TsvDirectory;
            var lines = File.ReadAllText(Path.Combine(dir, AdPlacementData.Table + ".txt"), new UTF8Encoding(false)).Replace("\r\n", "\n").Split('\n');
            var head = lines[0].Split('\t');
            int keyCol = Array.IndexOf(head, "key"), mulCol = Array.IndexOf(head, "mul");
            for (int i = 1; i < lines.Length; i++)
            {
                var cells = lines[i].Split('\t');
                if (cells.Length <= mulCol || cells[keyCol] != MvMetaResultAd.Key) continue;
                cells[mulCol] = mul.ToString();
                lines[i] = string.Join("\t", cells);
            }
            var t = new MvTables();
            t.LoadAll(new MvMemoryTableSource(new MvTsvDirectorySource(dir)).Set(AdPlacementData.Table, string.Join("\n", lines)));
            return t;
        }

        /// <summary>정본 <c>01_wealth</c> TSV에서 type 행의 name 열(시험 어셈블리는 Economy를 참조하지 않아 원문을 직접 읽는다).</summary>
        static string WealthRawName(string type)
        {
            var lines = File.ReadAllText(Path.Combine(MvTableLoader.TsvDirectory, "01_wealth.txt"), new UTF8Encoding(false)).Replace("\r\n", "\n").Split('\n');
            var head = lines[0].Split('\t');
            int typeCol = Array.IndexOf(head, "type"), nameCol = Array.IndexOf(head, "name");
            for (int i = 1; i < lines.Length; i++)
            {
                var cells = lines[i].Split('\t');
                if (cells.Length > typeCol && cells[typeCol] == type) return cells[nameCol];
            }
            return null;
        }

        [Test]
        public void ResultAd_MulAndTexts_ReadFrom08AdPlacement()
        {
            int mul = MetaUiTestKit.Base().AdPlacement.ByKey(MvMetaResultAd.Key).mul + 4; // 정본과 다른 값으로 바꿔 표 한 곳을 읽는지 본다
            var t = WithResultAdMul(mul);

            Assert.AreEqual(mul, MvMetaResultAd.MulOf(t));
            Assert.AreEqual(0, MvMetaResultAd.MulOf(null), "표가 없으면 기본 배수를 지어내지 않는다");
            Assert.AreEqual("×" + mul + " 보상", ResultPresenter.AdText(MvMetaResultAd.MulOf(t)));

            string title = MvFakeAd.TitleOf(t.AdPlacement.ByKey(MvMetaResultAd.Key), t);
            StringAssert.Contains("×" + mul, title);
            StringAssert.Contains(WealthRawName("gold"), title);
            StringAssert.Contains(WealthRawName("dia"), title);
        }

        [Test]
        public void Create_CurrencyNames_From01Wealth()
        {
            var t = MetaUiTestKit.Base();
            var d = ResultViewData.Create(new RunResult { Reason = FinishReason.Gate, Souls = 3 }, 1, 0, 0, t);
            Assert.AreEqual(WealthRawName("gold"), d.GoldName);
            Assert.AreEqual(WealthRawName("dia"), d.DiaName);
            Assert.AreEqual(WealthRawName("goldenFish"), d.TokName);
            Assert.AreEqual(WealthRawName("rescueCat"), d.SoulsName);
            StringAssert.Contains(d.SoulsName, ResultPresenter.ProgressLeft(d));
        }
    }
}
