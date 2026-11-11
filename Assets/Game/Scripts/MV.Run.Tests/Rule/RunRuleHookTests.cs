using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace MV.Run.Tests.Rule
{
    /// <summary>훅 호출 자리(04 §4.20.2): Build·BuildCourse·OnHit(가호 뒤, 안쪽부터)·OnEvent·Tick·CheckEnd → finish(ModeClear)·Settle.</summary>
    public sealed class RunRuleHookTests
    {
        /// <summary>정본 규칙을 감싸 호출을 기록하고, 상자가 깨지면 Clear를 내는 시험용 모드.</summary>
        sealed class ProbeRule : IRunRule
        {
            readonly MainRunRule _main;
            public readonly List<string> Log = new List<string>();
            public readonly List<HitInfo> Hits = new List<HitInfo>();
            public int Ticks, Events;
            public bool Broken;
            public double ModeRoll;

            public ProbeRule(IRunLevelBuilder b) { _main = new MainRunRule(b); }

            public RunSetup Build(RunContext ctx) { Log.Add("Build"); var s = _main.Build(ctx); s.Layout = 2; return s; }
            public void BuildCourse(RunState s, CourseRequest req) { Log.Add($"BuildCourse L{req.Level}"); _main.BuildCourse(s, req); }
            public void OnHit(RunState s, in HitInfo h) { Hits.Add(h); if (h.Killed) Broken = true; }
            public void Tick(RunState s, double dt) { Ticks++; ModeRoll = s.ModeRng.NextDouble(); }
            public RunEnd CheckEnd(RunState s) => Broken ? RunEnd.Clear : RunEnd.None;
            public RunResult Settle(RunState s, FinishReason reason) { Log.Add($"Settle {reason}"); return _main.Settle(s, reason); }
            public void OnEvent(RunState s, in RunEvent e) { Events++; }
        }

        /// <summary>HandLevel을 「상태 만들기」와 「코스 만들기」로 나눈다(BuildCourse 훅 경로 시험용).</summary>
        sealed class SplitLevel : IRunLevelBuilder, IRunStateFactory
        {
            readonly HandLevel _h;
            public SplitLevel(HandLevel h) { _h = h; }

            public RunState NewState(RunSetup setup, MV.Data.MvTables t, IRng rng)
            {
                var place = new List<System.Action<RunState>>(_h.Place);
                _h.Place.Clear();
                try { return _h.NewRun(setup, t, rng); }
                finally { _h.Place.AddRange(place); }
            }

            public RunState NewRun(RunSetup setup, MV.Data.MvTables t, IRng rng) => _h.NewRun(setup, t, rng);
            public void BuildLevel(RunState s, double baseZ, int level, BarRecord saved) => _h.BuildLevel(s, baseZ, level, saved);
            public IReadOnlyList<FieldCell> FieldSpec(int area, int floor) => _h.FieldSpec(area, floor);
        }

        [Test]
        public void ModeClear_HooksInOrder_NoRunRngFromHooks()
        {
            var t = RunTestTables.Load();
            var lv = new HandLevel().Add("coin", 0, 20);
            var split = new SplitLevel(lv);
            var probe = new ProbeRule(split);
            var snap = new RunSetup { Preset = "easy" };
            snap.Deck[5] = 1; // 피해 10인 단계(103 더미 5단계, MV-SPEC2-FIX1)
            var ctx = new RunContext
            {
                Tables = t, Snapshot = snap, MetaModifiers = new RunModifiers { Dmg = 10 },
                Companions = { new CompanionSpec(1, CompanionTrigger.Seconds, 9, 100, 1) },
            };
            var rngProbe = new LoggingRng(new ConstRng(0.5));
            var sim = RunSim.Create(ctx, probe, rngProbe, split);
            Assert.AreEqual(new[] { "Build", "BuildCourse L1" }, probe.Log.ToArray());
            Assert.AreEqual(2, sim.State.Setup.Layout);
            Assert.AreEqual(10, sim.State.Setup.Modifiers.Dmg);
            Assert.AreEqual(1, sim.State.Setup.Companions.Count);
            Assert.AreEqual(0, snap.Companions.Count, "Build는 스냅숏을 바꾸지 않는다");

            var main = RunSim.Create(new RunSetup { Deck = new[] { 0, 0, 0, 0, 0, 1, 0, 0, 0 }, Preset = "easy", Modifiers = new RunModifiers { Dmg = 10 } },
                t, new LoggingRng(new ConstRng(0.5)), lv);
            int frames = 0;
            while (!sim.State.Fin && frames < 600) { sim.Tick(1 / 60.0, RunInput.None); main.Tick(1 / 60.0, RunInput.None); frames++; }

            var s = sim.State;
            Assert.IsTrue(s.Fin, "상자가 깨지면 CheckEnd Clear로 끝난다");
            Assert.AreEqual(FinishReason.ModeClear, s.Reason);
            Assert.AreEqual(FinishReason.ModeClear, s.Result.Reason);
            Assert.AreEqual("Settle ModeClear", probe.Log.Last());
            Assert.AreEqual(1, sim.State.Events.Count(e => e.Type == RunEventType.RunFinished));
            Assert.Greater(probe.Ticks, 0);
            Assert.AreEqual(sim.State.Events.Count, probe.Events, "Emit마다 OnEvent");
            var box = s.Objs[0];
            Assert.IsTrue(probe.Hits.All(h => h.ObjId == box.Id && h.Kind == box.Kind.id && h.Source == HitSource.Projectile));
            Assert.AreEqual(1, probe.Hits.Count(h => h.Killed));
            Assert.IsTrue(probe.Hits.Last().Killed);
            var hitEv = s.Events.Where(e => e.Type == RunEventType.Hit).Select(e => (int)e.Amount).ToArray();
            CollectionAssert.AreEqual(hitEv, probe.Hits.Select(h => h.Dmg).ToArray(), "HitInfo.Dmg = 반올림 뒤 피해");
            // 같은 프레임 수 동안 정본 규칙과 RunRng 소비가 같다(Tick의 ModeRng는 별도 줄기).
            Assert.AreEqual(((LoggingRng)main.State.Rng).Values.Count, rngProbe.Values.Count);
            Assert.AreNotEqual(0.5, probe.ModeRoll);
        }

        [Test]
        public void ModeRng_CompanionRng_AreSeparateStreamsFromSeed()
        {
            var a = new RunState(new RunSetup { Seed = 11 }, null, new ConstRng(0.5));
            var b = new RunState(new RunSetup { Seed = 11 }, null, new ConstRng(0.5));
            var c = new RunState(new RunSetup { Seed = 12 }, null, new ConstRng(0.5));
            double m = a.ModeRng.NextDouble(), k = a.CompanionRng.NextDouble();
            Assert.AreEqual(m, b.ModeRng.NextDouble(), "같은 시드 → 같은 줄기");
            Assert.AreEqual(k, b.CompanionRng.NextDouble());
            Assert.AreNotEqual(m, k, "ModeRng ≠ CompanionRng");
            Assert.AreNotEqual(m, c.ModeRng.NextDouble());
            Assert.AreNotEqual(new Pcg32Rng(11).NextDouble(), m, "RunRng(같은 시드 기본 줄기)와도 다르다");
        }
    }
}
