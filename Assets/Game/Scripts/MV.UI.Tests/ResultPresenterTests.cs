using System;
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
            public string Title, Left, Right, Gold, Dia, Tok, Stat;
            public Color TitleColor;
            public float Fill;
            public bool Interactable = true;
            public void Press() => CollectPressed?.Invoke();
            public void SetTitle(string text, Color color) { Title = text; TitleColor = color; }
            public void SetProgress(string left, string right, float fill01) { Left = left; Right = right; Fill = fill01; }
            public void SetRewards(string gold, string dia, string tok) { Gold = gold; Dia = dia; Tok = tok; }
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
            p.Open(Data(new CountingSettler()));
            Assert.AreEqual("Area 2 개방!", view.Title);
            Assert.AreEqual("Area 2 개방까지 생선", view.Left);
            Assert.AreEqual("80 / 100", view.Right);
            Assert.AreEqual(0.8f, view.Fill, 1e-5f);
            Assert.AreEqual("+1,234", view.Gold);
            Assert.AreEqual("+2", view.Dia);
            Assert.AreEqual("+1", view.Tok);
            Assert.IsTrue(view.Interactable);
        }

        [Test]
        public void Create_MapsRunResultToOutcome()
        {
            var r = GateResult(true, false);
            var d = ResultViewData.Create(r, 42, 2, 10, null);
            Assert.AreEqual(ResultKind.AreaOpen, d.Kind);
            Assert.AreEqual(3, d.AreaNo);
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
