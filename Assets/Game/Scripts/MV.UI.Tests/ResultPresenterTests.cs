using System;
using System.Collections.Generic;
using MV.Meta;
using MV.Run;
using NUnit.Framework;
using UnityEngine;

namespace MV.UI.Tests
{
    /// <summary>결과 「받기」가 Meta 정산 명령을 한 번만 부르는지, 런 결과가 정산 값으로 옮겨지는지(05 §5.6).</summary>
    public sealed class ResultPresenterTests
    {
        sealed class CountingSettler : IResultSettler
        {
            public int Calls;
            public int LastMul;
            public MvRunOutcome Last;
            public MvSettleResult Settle(MvRunOutcome outcome, int mul)
            {
                Calls++;
                LastMul = mul;
                Last = outcome;
                return new MvSettleResult { Mul = mul };
            }
        }

        sealed class FakeView : IResultView
        {
            public event Action CollectPressed;
            public string Title, Stat;
            public Color TitleColor;
            public ResultProgress Progress;
            public IReadOnlyList<ResultRewardItem> Rewards;
            public bool Interactable = true;
            public void Press() => CollectPressed?.Invoke();
            public void SetTitle(string text, Color color) { Title = text; TitleColor = color; }
            public void SetProgress(in ResultProgress progress) => Progress = progress;
            public void SetRewards(IReadOnlyList<ResultRewardItem> items) => Rewards = items;
            public void SetStat(string text) => Stat = text;
            public void SetCollectInteractable(bool interactable) => Interactable = interactable;
        }

        static RunResult GateResult(bool pass, bool zoneEnd) => new RunResult
        {
            Reason = FinishReason.Gate, Gold = 1234.5, Souls = 30, Dia = 2, Tok = 1, Pass = pass, ZoneEnd = zoneEnd, CpRun = 1, Walls = 4, GaugeLv = 3,
        };

        static ResultViewData Data(CountingSettler settler)
        {
            var d = ResultViewData.Create(GateResult(true, false), 7, 0, 50, null);
            d.Settler = settler;
            d.SoulsNeed = 100;
            return d;
        }

        [Test]
        public void Collect_CallsSettleOnce()
        {
            var settler = new CountingSettler();
            var p = new ResultPresenter();
            var view = new FakeView();
            p.SetView(view);
            p.Open(Data(settler));

            Assert.IsTrue(p.TryCollect());
            Assert.IsFalse(p.TryCollect());
            Assert.IsFalse(p.TryCollect());

            Assert.AreEqual(1, settler.Calls);
            Assert.AreEqual(1, settler.LastMul);
            Assert.AreEqual(7, settler.Last.RunSeq);
            Assert.IsTrue(p.Collected);
            Assert.IsNotNull(p.LastSettle);
            Assert.IsFalse(view.Interactable);
        }

        [Test]
        public void Collect_ReopenWithNewData_AllowsOneMore()
        {
            var settler = new CountingSettler();
            var p = new ResultPresenter();
            p.Open(Data(settler));
            p.TryCollect();
            p.Open(Data(settler));
            p.TryCollect();
            p.TryCollect();
            Assert.AreEqual(2, settler.Calls);
        }

        [Test]
        public void Collect_WithoutOutcome_DoesNotSettle()
        {
            var settler = new CountingSettler();
            var p = new ResultPresenter();
            p.Open(new ResultViewData { Settler = settler });
            Assert.IsFalse(p.TryCollect());
            Assert.AreEqual(0, settler.Calls);
        }

        [Test]
        public void Open_DrawsResultValues()
        {
            var p = new ResultPresenter();
            var view = new FakeView();
            p.SetView(view);
            var d = Data(new CountingSettler());
            p.Open(d);
            Assert.AreEqual("Area 2 개방!", view.Title);
            Assert.AreEqual("Area 2 개방까지 " + d.SoulsName, view.Progress.Left);
            Assert.AreEqual("80 / 100", view.Progress.Right);
            Assert.AreEqual(0.5f, view.Progress.From01, 1e-5f, "정산 전 50/100에서 출발");
            Assert.AreEqual(0.8f, view.Progress.To01, 1e-5f);
            Assert.AreEqual(80, view.Progress.Percent);
            Assert.AreEqual(3, view.Rewards.Count, "정본 3칸: 골드·블루 마법석·황금 생선");
            Assert.AreEqual("gold", view.Rewards[0].WealthType);
            Assert.AreEqual(MvUiFormat.Fmt(1234.5), view.Rewards[0].Count);
            Assert.AreEqual("dia", view.Rewards[1].WealthType);
            Assert.AreEqual("2", view.Rewards[1].Count);
            Assert.AreEqual("goldenFish", view.Rewards[2].WealthType);
            Assert.AreEqual("1", view.Rewards[2].Count);
            Assert.AreEqual(d.GoldName, view.Rewards[0].Name);
            Assert.AreEqual(d.DiaName, view.Rewards[1].Name);
            Assert.AreEqual(d.TokName, view.Rewards[2].Name);
            Assert.IsTrue(view.Interactable);
        }

        [TestCase(0, 0, 100, 0, 0f)]
        [TestCase(50, 80, 100, 80, 0.5f)]
        [TestCase(99, 150, 100, 100, 0.99f)]
        [TestCase(1, 2, 3, 66, 0.3333333f)]
        [TestCase(10, 10, 0, 100, 1f)]
        public void Progress_PercentFloors_AndStartsFromBefore(int before, int have, int need, int pct, float from)
        {
            var d = new ResultViewData { SoulsBefore = before, SoulsHave = have, SoulsNeed = need };
            var p = ResultPresenter.Progress(d);
            Assert.AreEqual(pct, p.Percent, "소수점 버림, 100 상한");
            Assert.AreEqual(from, p.From01, 1e-5f);
            Assert.That(p.To01, Is.GreaterThanOrEqualTo(p.From01));
        }

        [Test]
        public void Create_MapsRunResultToOutcome()
        {
            var r = GateResult(true, false);
            var d = ResultViewData.Create(r, 42, 2, 10, null);
            Assert.AreEqual(ResultKind.AreaOpen, d.Kind);
            Assert.AreEqual(3, d.AreaNo);
            Assert.AreEqual(10, d.SoulsBefore);
            Assert.AreEqual(40, d.SoulsHave);
            Assert.AreEqual(42, d.Outcome.RunSeq);
            Assert.AreEqual(MvRunEnd.Gate, d.Outcome.End);
            Assert.AreEqual(1234.5, d.Outcome.Gold, 1e-9);
            Assert.AreEqual(30, d.Outcome.Souls);
            Assert.AreEqual(2, d.Outcome.Dia);
            Assert.AreEqual(1, d.Outcome.CpRun);
            Assert.IsNull(d.Outcome.Bars);
        }

        [Test]
        public void Create_KindFollowsCanonTitles()
        {
            Assert.AreEqual(ResultKind.ZoneClear, ResultViewData.Create(GateResult(false, true), 1, 0, 0, null).Kind);
            Assert.AreEqual(ResultKind.LevelDone, ResultViewData.Create(GateResult(false, false), 1, 0, 0, null).Kind);
            var wall = new RunResult { Reason = FinishReason.Wall };
            var d = ResultViewData.Create(wall, 1, 0, 0, null);
            Assert.AreEqual(ResultKind.Fail, d.Kind);
            Assert.AreEqual(MvRunEnd.Wall, d.Outcome.End);
            Assert.AreEqual("아지트로 귀환", ResultPresenter.TitleText(d));
        }
    }
}
