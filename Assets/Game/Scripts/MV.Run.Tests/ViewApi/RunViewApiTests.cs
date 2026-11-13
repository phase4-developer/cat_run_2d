using System.Collections.Generic;
using System.Linq;
using MV.Data;
using NUnit.Framework;
using UnityEngine.TestTools.Constraints; // AllocatingGCMemory 확장 메서드
using AllocIs = UnityEngine.TestTools.Constraints.Is;

namespace MV.Run.Tests
{
    /// <summary>
    /// 표시용 읽기 인터페이스 <see cref="IRunViewState"/> 시험. 기대값은 정본 식(04 §4.8.1·§4.8.3, L2227·L2292):
    /// startClimb에서 <c>R.cliff = {z:zEnd, low:WY, high:WY + dH}</c>, endClimb에서 <c>WY += dH</c>.
    /// zEnd = 계단 z + z0Gap + k·stepD, dH = k·stepH(330_climb).
    /// </summary>
    public sealed class RunViewApiTests
    {
        const double Eps = 1e-9;

        static RunSetup Setup() => new RunSetup { Deck = new[] { 0, 1, 0, 0, 0, 0, 0, 0, 0 } };
        static RunSim Sim(HandLevel lv, IRng rng = null) => RunSim.Create(Setup(), RunTestTables.Load(), rng ?? new ConstRng(0.5), lv);

        static void TickUntil(RunSim sim, System.Func<RunState, bool> done, int max = 60 * 40)
        {
            for (int f = 0; f < max && !done(sim.State); f++) sim.Tick(1 / 60.0, default);
            Assert.IsTrue(done(sim.State), "조건에 닿지 못함");
        }

        static void AssertCliff(RunCliff c, double z, double low, double high, string at)
        {
            Assert.AreEqual(z, c.Z, Eps, at + " z");
            Assert.AreEqual(low, c.Low, Eps, at + " low");
            Assert.AreEqual(high, c.High, Eps, at + " high");
        }

        [Test]
        public void WorldYAndCliff_BeforeDuringAfterClimb()
        {
            var t = RunTestTables.Load(); var c = t.Climb.Config();
            const double stairZ = 3; const int n = 80;
            int k = RunFormulas.ClimbK(c, n);
            double zEnd = stairZ + c["z0Gap"] + k * c["stepD"], dH = k * c["stepH"];
            // MV-SPEC-02 6-5: 압축 단수 k, stepD 0.72·stepH 0.42(Obj_ClimbStep 블록, 2026-11-13)
            Assert.AreEqual(43, k);
            Assert.AreEqual(3 + 1.4 + 43 * 0.72, zEnd, Eps, "330_climb 값");
            Assert.AreEqual(43 * 0.42, dH, Eps, "330_climb 값");

            var sim = Sim(new HandLevel().Add("stair", 0, stairZ));
            sim.State.Stairs = n;
            IRunViewState v = sim.State;

            // 전: WY 0, 옹벽 없음
            Assert.AreEqual(0, v.WorldY, Eps);
            Assert.IsFalse(v.HasCliff);

            // 오르기 시작: 옹벽 {zEnd, WY, WY + dH}, WY는 아직 그대로
            TickUntil(sim, s => s.Phase == RunPhase.Climb);
            Assert.IsTrue(v.HasCliff);
            AssertCliff(v.Cliff, zEnd, 0, dH, "시작");
            Assert.AreEqual(0, v.WorldY, Eps, "오르기 중 WY는 endClimb 전까지 그대로");
            var start = sim.State.Events.Last(e => e.Type == RunEventType.ClimbStart);
            Assert.AreEqual(start.Z, v.Cliff.Z, Eps, "ClimbStart.z = zEnd");
            Assert.AreEqual(start.Amount2, v.Cliff.High - v.Cliff.Low, Eps, "ClimbStart.amount2 = dH");

            // 중: 몇 프레임 뒤에도 같다
            for (int f = 0; f < 30; f++) sim.Tick(1 / 60.0, default);
            Assert.AreEqual(RunPhase.Climb, sim.State.Phase);
            Assert.AreEqual(0, v.WorldY, Eps);
            AssertCliff(v.Cliff, zEnd, 0, dH, "중");

            // 후: WY += dH, 옹벽 그대로, 착지 zEnd + landGap(옹벽 위쪽)
            TickUntil(sim, s => s.Phase != RunPhase.Climb);
            Assert.AreEqual(n, sim.State.Climbed);
            Assert.AreEqual(dH, v.WorldY, Eps);
            AssertCliff(v.Cliff, zEnd, 0, dH, "후");
            Assert.Greater(sim.State.P.Z, v.Cliff.Z);
        }

        [Test]
        public void SecondClimb_CliffStartsAtPreviousWorldY()
        {
            var t = RunTestTables.Load(); var c = t.Climb.Config();
            const double z1 = 3, z2 = 10; const int n1 = 80, n2 = 10;
            int k1 = RunFormulas.ClimbK(c, n1), k2 = RunFormulas.ClimbK(c, n2);
            double dH1 = k1 * c["stepH"], dH2 = k2 * c["stepH"];
            double shift1 = k1 * c["stepD"] + c["shiftExtra"]; // shiftWorld L: 두 번째 계단을 이만큼 민다
            double zEnd2 = z2 + shift1 + c["z0Gap"] + k2 * c["stepD"];

            var sim = Sim(new HandLevel().Add("stair", 0, z1).Add("stair", 0, z2));
            sim.State.Stairs = n1;
            IRunViewState v = sim.State;
            TickUntil(sim, s => s.Phase == RunPhase.Climb);
            sim.State.Stairs = n2; // 첫 오르기에서 0이 된 뒤 두 번째 계단용
            TickUntil(sim, s => s.Climbed == n1 + n2 && s.Phase != RunPhase.Climb);

            Assert.AreEqual(dH1 + dH2, v.WorldY, Eps);
            Assert.IsTrue(v.HasCliff);
            AssertCliff(v.Cliff, zEnd2, dH1, dH1 + dH2, "두 번째(정본은 최근 1개로 덮어씀)");
        }

        [Test]
        public void Reading_DoesNotChangeRun()
        {
            string RunOnce(bool read, out string rng, out string view)
            {
                var lv = new HandLevel().Add("tower", 0, 8).Add("coin", 0, 20).Add("enemy", 0, 30).Add("stair", 0, 40).Add("coin", 0, 70);
                var sim = Sim(lv, new Pcg32Rng(42));
                sim.State.Stairs = 30;
                sim.State.RngLog = new List<RngDraw>();
                IRunViewState v = sim.State;
                var all = new List<RunEvent>();
                double sum = 0;
                for (int f = 0; f < 60 * 30 && !sim.State.Fin; f++)
                {
                    if (read) for (int r = 0; r < 3; r++) { sum += v.WorldY + (v.HasCliff ? v.Cliff.Z + v.Cliff.Low + v.Cliff.High : 0); }
                    sim.Tick(1 / 60.0, f % 120 < 60 ? RunInput.Drag(-1.5) : RunInput.Drag(1.5));
                    all.AddRange(sim.DrainEvents());
                }
                Assert.AreEqual(30, sim.State.Climbed, "시험 레벨이 계단을 올라야 한다");
                if (read) Assert.Greater(sum, 0);
                rng = string.Join(";", sim.State.RngLog);
                view = $"{v.WorldY}|{v.HasCliff}|{v.Cliff}";
                return string.Join("\n", all);
            }
            var a = RunOnce(false, out var ra, out var va);
            var b = RunOnce(true, out var rb, out var vb);
            Assert.AreEqual(a, b, "이벤트 열");
            Assert.AreEqual(ra, rb, "규칙 난수 소비");
            Assert.AreEqual(va, vb);
        }

        [Test]
        public void Reading_DoesNotAllocate()
        {
            var sim = Sim(new HandLevel().Add("stair", 0, 3));
            sim.State.Stairs = 20;
            TickUntil(sim, s => s.Phase == RunPhase.Climb);
            IRunViewState v = sim.State;
            double sink = 0;
            Assert.That(() =>
            {
                for (int i = 0; i < 100; i++) { var c = v.Cliff; sink += v.WorldY + c.Z + c.Low + c.High + (v.HasCliff ? 1 : 0); }
            }, AllocIs.Not.AllocatingGCMemory());
            Assert.Greater(sink, 0);
        }
    }
}
