using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace MV.Fx.Tests
{
    /// <summary>숫자 팝업 풀(06 §6.7), HUD 비행(06 §6.8), 우다다 단계(06 §6.11, 330_climb).</summary>
    public sealed class MvFxLayerTests
    {
        sealed class RecPopView : IMvPopView
        {
            public readonly HashSet<int> Shown = new HashSet<int>();
            public int Hides;
            public void Show(int slot, string text, Color32 color, float fontU) => Shown.Add(slot);
            public void Place(int slot, Vector2 screen, float scale, float alpha) { }
            public void Hide(int slot) { Shown.Remove(slot); Hides++; }
        }

        [Test]
        public void PopPoolCapsAt61AndReusesOldest()
        {
            var pool = MvPopPool.FromTables(FxTestTables.Load());
            Assert.AreEqual(61, pool.Max, "pop.max");
            var view = new RecPopView();
            pool.View = view;
            var slots = new List<int>();
            for (int i = 0; i < 61; i++) slots.Add(pool.Add(new PopRequest { World = new Vector3(0, i, 0), Text = i.ToString() }));
            Assert.AreEqual(61, pool.ActiveCount);
            Assert.AreEqual(61, slots.Distinct().Count());
            // 61개일 때 하나 더: 정본 `POPS.length > 60`이면 가장 오래된 것 제거
            int s62 = pool.Add(new PopRequest { World = new Vector3(0, 99, 0), Text = "x" });
            Assert.AreEqual(61, pool.ActiveCount);
            Assert.AreEqual(slots[0], s62, "가장 오래된 슬롯 재사용");
            Assert.AreEqual(99f, pool.WorldOf(s62).y);
            for (int i = 0; i < 100; i++) pool.Add(new PopRequest { Text = "y" });
            Assert.AreEqual(61, pool.ActiveCount);
            Assert.AreEqual(61, view.Shown.Count);
            // 실시간 0.8 s 뒤 모두 사라짐, 월드 y는 초당 1.4 m
            int last = pool.Order[pool.Order.Count - 1];
            pool.Step(0.5f);
            Assert.AreEqual(0.7f, pool.WorldOf(last).y, 1e-4f);
            pool.Step(0.31f);
            Assert.AreEqual(0, pool.ActiveCount);
            Assert.AreEqual(0, view.Shown.Count);
        }

        [Test]
        public void PopCurvesMatchCanon()
        {
            var pool = MvPopPool.FromTables(FxTestTables.Load());
            Assert.AreEqual(1.45f, pool.ScaleAt(0), 1e-5f);
            Assert.AreEqual(1f, pool.ScaleAt(0.15f), 1e-5f);
            Assert.AreEqual(1f, pool.AlphaAt(0.6f), 1e-5f);
            Assert.AreEqual(0.5f, pool.AlphaAt(0.8f), 1e-5f);
            Assert.AreEqual(0f, pool.AlphaAt(1f), 1e-5f);
        }

        [Test]
        public void FlyBezierMatchesSampleTable()
        {
            var fly = MvFlyLayer.FromTables(FxTestTables.Load());
            float[] xs = { 0.1f, 0.2f, 0.3f, 0.4f, 0.5f, 0.6f, 0.7f, 0.8f, 0.9f };
            float[] ys = { 0.008f, 0.034f, 0.078f, 0.141f, 0.227f, 0.335f, 0.467f, 0.624f, 0.804f };
            for (int i = 0; i < xs.Length; i++) Assert.AreEqual(ys[i], fly.Curve.Evaluate(xs[i]), 0.0015f, "fly x=" + xs[i]);
            Assert.AreEqual(0.033f, fly.CardCurve.Evaluate(0.25f), 0.0015f);
            Assert.AreEqual(0.164f, fly.CardCurve.Evaluate(0.5f), 0.0015f);
            Assert.AreEqual(0.471f, fly.CardCurve.Evaluate(0.75f), 0.0015f);
        }

        [Test]
        public void FlyArrivesAt560msRealTime()
        {
            var fly = MvFlyLayer.FromTables(FxTestTables.Load());
            Assert.AreEqual(0.56f, fly.RemoveSec, 1e-5f);
            Assert.AreEqual(0.62f, fly.CardRemoveSec, 1e-5f);
            var got = new List<string>();
            fly.Arrived += (t, n) => got.Add(t + ":" + n);
            fly.FlyTo(new FlyRequest { Target = FlyTarget.Stair, Amount = 3 });
            fly.FlyTo(new FlyRequest { Target = FlyTarget.Hand, Amount = 1, DelayMs = 70 });
            for (int i = 0; i < 55; i++) fly.Step(0.01f);
            Assert.IsEmpty(got, "0.55 s 이동 끝, 아직 제거 전");
            fly.Step(0.011f);
            CollectionAssert.AreEqual(new[] { "Stair:3" }, got);
            for (int i = 0; i < 8; i++) fly.Step(0.01f);
            CollectionAssert.AreEqual(new[] { "Stair:3", "Hand:1" }, got, "70 ms 늦게 출발한 것은 630 ms 무렵 도착");
            Assert.AreEqual(0, fly.ActiveCount);
        }

        [Test]
        public void WarpStagesFollowClimbTable()
        {
            var m = MvWarpModel.FromTables(FxTestTables.Load());
            CollectionAssert.AreEqual(new[] { 0, 25, 75, 150 }, m.Stages.Select(s => s.At).ToArray());
            CollectionAssert.AreEqual(new[] { "#ffd84a", "#ff9a5a", "#9ab8ff", "#fff0a0" }, m.Stages.Select(s => s.Accent.ToLowerInvariant()).ToArray());
            CollectionAssert.AreEqual(new[] { 0, 0.2, 0.65, 0.8 }, m.Stages.Select(s => s.Night).ToArray());
            Assert.AreEqual(0, m.StageFor(24));
            Assert.AreEqual(1, m.StageFor(25));
            Assert.AreEqual(2, m.StageFor(149));
            Assert.AreEqual(3, m.StageFor(150));
            Assert.AreEqual(0.5, m.Alpha(0.175), 1e-9);
            Assert.AreEqual(1, m.Alpha(1), 1e-9);
            Assert.AreEqual(1 + 2 * 0.35 + 1.5, MvWarpModel.Speed(2, 100), 1e-9);
            Assert.AreEqual(9, m.RoofCount);
            Assert.AreEqual(16, m.StepCount);
            Assert.AreEqual(18, m.SpeedLines);
            Assert.AreEqual(0.7, m.OutSec, 1e-9);
            Assert.AreEqual(0, m.StageNameAlpha(1, 1.8), 1e-9);
            Assert.AreEqual(1, m.StageNameAlpha(1, 0.5), 1e-9);
        }
    }
}
