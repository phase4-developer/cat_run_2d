using System.Collections.Generic;
using System.Linq;
using MV.Data;
using NUnit.Framework;
using static MV.Run.RunFormulas;

namespace MV.Run.Tests
{
    /// <summary>04 공식 단위 시험(공식마다 1개 이상). 값은 04 본문 예시(Area 1 easy, 강화·카드 0).</summary>
    public sealed class RunFormulaTests
    {
        static RunState NewState(RunSetup setup = null, HandLevel lv = null)
        {
            var t = RunTestTables.Load();
            return RunSim.Create(setup ?? new RunSetup { Deck = new[] { 0, 1, 0, 0, 0, 0, 0, 0, 0 } }, t, new ConstRng(0.5), lv ?? new HandLevel()).State;
        }

        [Test]
        public void JsRound_HalfUp() { Assert.AreEqual(3, JsRound(2.5)); Assert.AreEqual(1, JsRound(0.5)); Assert.AreEqual(2, JsRound(2.4999)); }

        [Test]
        public void BaseDmg_TierAndModifier()
        {
            Assert.AreEqual(10, BaseDmg(NewState()), 1e-12);
            var s = NewState(new RunSetup { Deck = new[] { 0, 1, 0, 0, 0, 0, 0, 0, 0 }, Modifiers = new RunModifiers { Dmg = 10 } });
            Assert.AreEqual(11, BaseDmg(s), 1e-9);
            Assert.AreEqual(new[] { 10, 18, 32, 58, 105, 189, 340, 612 }, Enumerable.Range(1, 8).Select(i => (int)WeaponDmg(s.Tables, i)).ToArray());
        }

        [Test]
        public void FireInterval_EasyAndModifier()
        {
            Assert.AreEqual(0.5, FireInterval(NewState()), 1e-12);
            var s = NewState(new RunSetup { Modifiers = new RunModifiers { FireRate = 20 } });
            Assert.AreEqual(1 / 2.4, FireInterval(s), 1e-12);
        }

        [Test]
        public void Range_Crit_Gold_Chances()
        {
            var s = NewState();
            Assert.AreEqual(22, ProjRange(s), 1e-12);
            Assert.AreEqual(2.2, CritMul(s), 1e-12);
            Assert.AreEqual(1, GoldMul(s), 1e-12);
            Assert.AreEqual(0, CritChance(s), 1e-12);
            Assert.AreEqual(0, DoubleChance(s), 1e-12);
            var m = NewState(new RunSetup { Modifiers = new RunModifiers { Range = 10, CritDmg = 50, Gold = 10, CritChance = 5, DoubleChance = 10 } });
            Assert.AreEqual(24.2, ProjRange(m), 1e-9);
            Assert.AreEqual(2.7, CritMul(m), 1e-9);
            Assert.AreEqual(1.1, GoldMul(m), 1e-9);
            Assert.AreEqual(0.05, CritChance(m), 1e-12);
            Assert.AreEqual(0.1, DoubleChance(m), 1e-12);
        }

        [Test]
        public void ObstMul_DiaCap()
        {
            var t = RunTestTables.Load();
            Assert.AreEqual(1, ObstMul(t, 0), 1e-12);
            Assert.AreEqual(0.5, ObstMul(t, 10), 1e-12);
            Assert.AreEqual(10, DiaCap(t, 0), 1e-12);
            Assert.AreEqual(14, DiaCap(t, 2), 1e-12);
        }

        [Test]
        public void ChargeThresholds_Area1Easy()
        {
            var s = NewState();
            var t = s.Tables;
            Assert.AreEqual(11.4, Threshold(t.ChargeGate.ByKey("sgate"), 0, s.Bal, s.ZoneHp), 1e-9);
            var cg = t.ChargeGate.ByKey("cgate");
            Assert.AreEqual(new[] { 5.7, 17.1, 28.5, 39.9 }, Enumerable.Range(0, 4).Select(k => System.Math.Round(Threshold(cg, k, s.Bal, s.ZoneHp), 9)).ToArray());
            var need = Enumerable.Range(1, 7).Select(lv => System.Math.Round(GaugeNeed(s, lv), 2)).ToArray();
            Assert.AreEqual(new[] { 150, 240, 384, 614.4, 983.04, 1572.86, 2516.58 }, need);
        }

        [Test]
        public void Climb_KCompressionAndTiers()
        {
            var t = RunTestTables.Load(); var c = t.Climb.Config();
            // MV-SPEC-02 6-5d·e(Q-07): N ≤ 30 그대로, 31~99 C 4, 100~149 C 5 …, T = min(1.5, 0.5 + 0.1(K − 1)).
            Assert.AreEqual(30, ClimbK(c, 30)); Assert.AreEqual(31, ClimbK(c, 31)); Assert.AreEqual(48, ClimbK(c, 99)); Assert.AreEqual(44, ClimbK(c, 100)); Assert.AreEqual(152, ClimbK(c, 1000));
            Assert.AreEqual(0.5, ClimbT(c, 1), 1e-12); Assert.AreEqual(1.4, ClimbT(c, 10), 1e-12); Assert.AreEqual(1.5, ClimbT(c, 150), 1e-12);
            void Tiers(int n, params int[] e) => Assert.AreEqual(e, ClimbTiers(t, n).ToArray(), $"n={n}");
            Tiers(4); Tiers(5, 0); Tiers(74, 3); Tiers(75, 4); Tiers(80, 4, 0); Tiers(150, 4, 4); Tiers(200, 4, 4, 3); Tiers(225, 4, 4, 4); Tiers(400, 4, 4, 4);
        }

        [Test]
        public void Tokens_Formula()
        {
            var t = RunTestTables.Load();
            Assert.AreEqual(1, Tokens(t, 0, false, false));
            Assert.AreEqual(1 + 1 + 2 + 3, Tokens(t, 5, true, true));
        }
    }

    /// <summary>시뮬레이션 규칙 시험(손으로 놓은 레벨 + ConstRng/Pcg32).</summary>
    public sealed class RunSimRuleTests
    {
        static RunSetup Setup() => new RunSetup { Deck = new[] { 0, 1, 0, 0, 0, 0, 0, 0, 0 } };
        static RunSim Sim(HandLevel lv, RunSetup s = null, IRng rng = null) => RunSim.Create(s ?? Setup(), RunTestTables.Load(), rng ?? new ConstRng(0.5), lv);
        static void Run(RunSim sim, int frames, RunInput input = default) { for (int i = 0; i < frames; i++) sim.Tick(1 / 60.0, input); }
        static List<RunEvent> Ev(RunSim sim, RunEventType t) => sim.State.Events.Where(e => e.Type == t).ToList();

        [Test]
        public void Move_FollowAndClamp()
        {
            var sim = Sim(new HandLevel());
            sim.Tick(0.05, RunInput.Drag(9));
            Assert.AreEqual(3.2, sim.State.P.Tx, 1e-12);
            Assert.AreEqual(3.2 * 0.7, sim.State.P.X, 1e-12);
            Assert.AreEqual(0.2, sim.State.P.Z, 1e-12);
            sim.Tick(1.0, RunInput.Key(-1)); // dt 상한 0.05
            Assert.AreEqual(3.2 - 0.05 * 9, sim.State.P.Tx, 1e-12);
        }

        [Test]
        public void Wall_BlocksAndFinishes()
        {
            var sim = Sim(new HandLevel().Wall(0, 5, 99999));
            Run(sim, 60 * 6); // MV-SPEC-02 6-8: 막힌 뒤 STUCK_END_SEC초 뒤 종료
            var s = sim.State;
            Assert.IsTrue(s.Fin);
            Assert.AreEqual(FinishReason.Wall, s.Result.Reason);
            Assert.AreEqual(5 - 0.55 - 0.35, s.P.Z, 1e-9);
            Assert.AreEqual(1, Ev(sim, RunEventType.Blocked).Count);
        }

        [Test]
        public void Box_HitRewardAndBreak()
        {
            var sim = Sim(new HandLevel().Add("coin", 0, 30));
            Run(sim, 480);
            var s = sim.State;
            var box = s.Objs[0];
            Assert.IsTrue(box.Dead);
            int hits = Ev(sim, RunEventType.Hit).Count(e => e.ObjId == box.Id);
            Assert.AreEqual(hits * 1 + 20, s.Gold, 1e-9, "타격당 츄르 1 + 파괴 20(zone 0)");
            Assert.AreEqual(1, Ev(sim, RunEventType.Break).Count);
            Assert.AreEqual(0.03, Ev(sim, RunEventType.Hitstop).First().Amount, 1e-12);
        }

        [Test]
        public void StairGate_ChargePassAndDelayedReceive()
        {
            var sim = Sim(new HandLevel().Add("sgate", 0, 10));
            var s = sim.State;
            int frames = 0;
            while (!s.Objs[0].Done && frames++ < 600) sim.Tick(1 / 60.0, default);
            Assert.Greater(s.Objs[0].N, 0);
            Assert.AreEqual(0, s.Stairs, "수령은 560ms 뒤");
            Assert.AreEqual(1, Ev(sim, RunEventType.GateTake).Count);
            Run(sim, 40);
            Assert.AreEqual(s.Objs[0].N, s.Stairs);
        }

        [Test]
        public void Enemy_StealsGold()
        {
            var sim = Sim(new HandLevel().Add("enemy", 3.0, 3, o => { o.HasHp = true; o.Hp = 99999; }));
            sim.State.Gold = 12.7;
            Run(sim, 120, RunInput.Drag(3.2));
            Assert.AreEqual(1, Ev(sim, RunEventType.Steal).Count);
            Assert.AreEqual(12.7 - 5, sim.State.Gold, 1e-9);
        }

        [Test]
        public void Climb_RouletteAppliesCards()
        {
            var sim = Sim(new HandLevel().Add("stair", 0, 3));
            sim.State.Stairs = 80;
            Run(sim, 60 * 12);
            var s = sim.State;
            Assert.AreEqual(80, s.Climbed);
            Assert.AreEqual(RunPhase.Run, s.Phase);
            Assert.IsNull(s.Roulette);
            Assert.AreEqual(2, Ev(sim, RunEventType.CardApplied).Count);
            Assert.AreEqual(new[] { 4, 0 }, Ev(sim, RunEventType.RouletteStart)[0].Ids);
        }

        [Test]
        public void Climb_NoStairsToastAndContinue()
        {
            var sim = Sim(new HandLevel().Add("stair", 0, 3));
            Run(sim, 60);
            Assert.AreEqual(RunPhase.Run, sim.State.Phase);
            Assert.AreEqual(1, Ev(sim, RunEventType.Toast).Count);
        }

        [Test]
        public void Gate_FinishAtGz()
        {
            var lv = new HandLevel { Gz = 2 };
            var sim = Sim(lv);
            Run(sim, 60);
            Assert.AreEqual(FinishReason.Gate, sim.State.Result.Reason);
            Assert.AreEqual(3, sim.State.Result.Tok, "조각 1 + gate 2(생선 부족 → 개방 아님)");
        }

        [Test]
        public void Replay_SameSeedSameEvents()
        {
            string RunOnce(out string rng)
            {
                var lv = new HandLevel().Add("tower", 0, 8).Add("sgate", -2.25, 20).Add("cgate", 2.25, 30).Add("coin", 0, 40)
                    .Add("enemy", 0, 60).Add("stair", 0, 70).Wall(0, 90, 400);
                var setup = Setup(); setup.AupCrit = 3; setup.AupDbl = 3;
                var sim = Sim(lv, setup, new Pcg32Rng(42));
                sim.State.RngLog = new List<RngDraw>();
                var all = new List<RunEvent>();
                for (int f = 0; f < 60 * 40 && !sim.State.Fin; f++)
                {
                    sim.Tick(1 / 60.0, f % 120 < 60 ? RunInput.Drag(-1.5) : RunInput.Drag(1.5));
                    all.AddRange(sim.DrainEvents());
                }
                rng = string.Join(";", sim.State.RngLog);
                return string.Join("\n", all);
            }
            var a = RunOnce(out var ra);
            var b = RunOnce(out var rb);
            Assert.AreEqual(a, b);
            Assert.AreEqual(ra, rb);
            StringAssert.Contains("Hit|", a);
        }
    }

    /// <summary>12 §12.6-A: 105에 행만 추가한 새 종류가 행에 적은 대로 피격·보상·수령한다(코드 변경 없음).</summary>
    public sealed class RunNewKindRowTests
    {
        [Test]
        public void NewKindRow_BehavesByComponents()
        {
            var kinds = RunTestTables.CloneRow(RunTestTables.ReadTsv("105_objectKind"), "coin", new Dictionary<string, string>
            {
                ["id"] = "105001099", ["key"] = "fishCrate", ["hpRuleId"] = "114001004", ["rewardType"] = "603", ["rewardValue"] = "2", ["rewardValuePerZone"] = "0",
            });
            var t = RunTestTables.Load(new Dictionary<string, string> { ["105_objectKind"] = kinds });
            Assert.IsNotNull(t.ObjectKind.ByKey("fishCrate"));
            var lv = new HandLevel().Add("fishCrate", 0, 20);
            var sim = RunSim.Create(new RunSetup { Deck = new[] { 0, 1, 0, 0, 0, 0, 0, 0, 0 } }, t, new ConstRng(0.5), lv);
            var o = sim.State.Objs[0];
            Assert.AreEqual(25, o.Hp, "bonus025: round(100 × 0.25)");
            for (int i = 0; i < 300; i++) sim.Tick(1 / 60.0, default);
            Assert.IsTrue(o.Dead);
            Assert.AreEqual(2, sim.State.Souls);
            int hits = sim.State.Events.Count(e => e.Type == RunEventType.Hit && e.ObjId == o.Id);
            Assert.AreEqual(hits, sim.State.Gold, 1e-9, "hitReward(타격당 츄르 1)는 복사한 행 그대로");
        }

        [Test]
        public void Components_AllUsedTypesRegistered()
        {
            var t = RunTestTables.Load();
            foreach (var k in t.ObjectKind.Datas)
            {
                Assert.IsTrue(RunComponents.IsImplemented("hittable", k.hittable.ToString()), k.key);
                if (t.HitReaction.Get(k.hitReactionId) is { } hr) Assert.IsTrue(RunComponents.IsImplemented("hitReactionId", hr.type.ToString()), k.key);
                if (t.ReceiveCondition.Get(k.receiveId) is { } rc) Assert.IsTrue(RunComponents.IsImplemented("receiveId", rc.type.ToString()), k.key);
                if (t.HpRule.Get(k.hpRuleId) is { } hp && hp.type != HpRuleType.ModeStage) Assert.IsTrue(RunComponents.IsImplemented("hpRuleId", hp.type.ToString()), k.key);
            }
        }
    }
}
