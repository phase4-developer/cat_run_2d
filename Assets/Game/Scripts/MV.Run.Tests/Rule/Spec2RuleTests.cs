using System;
using System.Collections.Generic;
using System.Linq;
using MV.Data;
using NUnit.Framework;
using static MV.Run.RunFormulas;

namespace MV.Run.Tests
{
    /// <summary>
    /// MV-SPEC-02 새 규칙(MV-RUN-SPEC2, DiffReport 결정 기록). 상수·한도는 표에서 읽은 값으로 기대값을 만든다(코드에 숫자 없음).
    /// </summary>
    public sealed class Spec2RuleTests
    {
        static RunSetup Setup() => new RunSetup { Deck = new[] { 0, 1, 0, 0, 0, 0, 0, 0, 0 } };
        static RunSim Sim(HandLevel lv, RunSetup s = null, MvTables t = null, IRng rng = null) =>
            RunSim.Create(s ?? Setup(), t ?? RunTestTables.Load(), rng ?? new ConstRng(0.5), lv);
        static void Run(RunSim sim, int frames, RunInput input = default) { for (int i = 0; i < frames && !sim.State.Fin; i++) sim.Tick(1 / 60.0, input); }
        static List<RunEvent> Ev(RunSim sim, RunEventType t) => sim.State.Events.Where(e => e.Type == t).ToList();

        /// <summary>TSV에서 <paramref name="matchCol"/> = <paramref name="matchVal"/>인 행의 <paramref name="col"/> 칸을 바꾼다(matchVal null = 모든 데이터 행).</summary>
        static string SetCell(string tsv, string matchCol, string matchVal, string col, string val)
        {
            var lines = tsv.Replace("\r\n", "\n").TrimEnd('\n').Split('\n');
            var header = lines[0].Split('\t');
            int mi = Array.IndexOf(header, matchCol), ci = Array.IndexOf(header, col);
            if (mi < 0 || ci < 0) throw new ArgumentException($"{matchCol}/{col}");
            for (int i = 1; i < lines.Length; i++)
            {
                var c = lines[i].Split('\t');
                if (c.Length <= Math.Max(mi, ci) || (matchVal != null && c[mi] != matchVal)) continue;
                c[ci] = val;
                lines[i] = string.Join("\t", c);
            }
            return string.Join("\n", lines) + "\n";
        }

        static MvTables WithConfig(string key, string value) =>
            RunTestTables.Load(new Dictionary<string, string> { ["300_runConfig"] = SetCell(RunTestTables.ReadTsv("300_runConfig"), "key", key, "value", value) });

        // ---------------- Q-07 계단 오르기 ----------------

        [TestCase(1)]
        [TestCase(11)]
        [TestCase(50)]
        public void Climb_StepEvery_FromClimbColumns(int k)
        {
            var c = RunTestTables.Load().Climb.Config();
            double t = Math.Min(c["riseCap"], c["riseBase"] + c["risePerStep"] * (k - 1));
            Assert.AreEqual(t, ClimbT(c, k), 1e-12);
            Assert.AreEqual(Math.Max(0, (t - c["riseLead"]) / Math.Max(1, k - 1)), ClimbStepEvery(c, k), 1e-12);
        }

        [Test]
        public void Climb_EndsWithinRiseCap_AllStepsShown()
        {
            var t = RunTestTables.Load(); var c = t.Climb.Config();
            var sim = Sim(new HandLevel().Add("stair", 0, 3), t: t);
            sim.State.Stairs = 500;
            Run(sim, 60 * 4);
            var start = Ev(sim, RunEventType.ClimbStart).Single();
            Assert.AreEqual(ClimbK(c, 500), start.B);
            Assert.LessOrEqual(start.Amount3, c["riseCap"]);
            Assert.AreEqual(start.B, Ev(sim, RunEventType.ClimbStepShown).Count);
            Assert.AreEqual(1, Ev(sim, RunEventType.ClimbEnd).Count);
        }

        // ---------------- Q-11 오르는 중 좌우 조작 ----------------

        [Test]
        public void Climb_KeepsLateralControl()
        {
            var sim = Sim(new HandLevel().Add("stair", 0, 1));
            sim.State.Stairs = 100;
            int f = 0;
            while (sim.State.Phase != RunPhase.Climb && f++ < 120) sim.Tick(1 / 60.0, default);
            Assert.AreEqual(RunPhase.Climb, sim.State.Phase);
            for (int i = 0; i < 20; i++) sim.Tick(1 / 60.0, RunInput.Drag(2));
            Assert.AreEqual(RunPhase.Climb, sim.State.Phase);
            Assert.AreEqual(2, sim.State.P.Tx, 1e-12);
            Assert.Greater(sim.State.P.X, 1, "오르는 중에도 끌기 목표로 따라간다(가운데로 당기지 않음)");
        }

        // ---------------- Q-06 계단 게이트 M ----------------

        [Test]
        public void StairGateM_AreaByStairLvTable()
        {
            var t = RunTestTables.Load();
            var g = t.ChargeGate.ByKey("sgate"); var bal = t.BalancePreset.Datas.First();
            var m = t.Area.Datas.First(a => a.areaNo == 1).sgateM;
            Assert.Greater(m.Count, 0);
            for (int lv = 1; lv <= m.Count; lv++)
                Assert.AreEqual(ApplyGateFlags(g, m[lv - 1], bal, 100), StairGateM(g, m, lv, bal, 100), 1e-12, $"Lv {lv}");
            Assert.AreEqual(StairGateM(g, m, m.Count, bal, 100), StairGateM(g, m, m.Count + 3, bal, 100), 1e-12, "Lv 상한 = 표 칸 수");
            Assert.Less(StairGateM(g, m, 1, bal, 100), StairGateM(g, m, m.Count, bal, 100), "Lv이 높을수록 M이 크다");
            Assert.AreEqual(Threshold(g, 0, bal, 100), StairGateM(g, null, 3, bal, 100), 1e-12, "표가 비면 107 base");
        }

        // ---------------- 6-2d·Q-27 계단 2배 / 지팡이 제작 2배 ----------------

        [Test]
        public void StairGate_DoubleChance_AddsTwiceLv()
        {
            var t = RunTestTables.Load();
            var row = PerkRow(t, RebirthStatKey.StairDouble);
            var setup = Setup(); setup.PerkStairDbl = (int)Math.Ceiling((1 - row.@base) / row.perLv); // 확률 1
            var sim = Sim(new HandLevel().Add("sgate", 0, 10), setup, t);
            sim.State.RngLog = new List<RngDraw>();
            Run(sim, 600);
            var fills = Ev(sim, RunEventType.GateFill).Where(e => e.A > 0).ToList();
            Assert.Greater(fills.Count, 0);
            foreach (var e in fills) Assert.AreEqual(0, e.A % (2 * sim.State.GaugeLv), "충전마다 +2×Lv");
            Assert.IsTrue(sim.State.RngLog.Any(d => d.Use == RngUse.StairDouble));

            var plain = Sim(new HandLevel().Add("sgate", 0, 10), t: t);
            plain.State.RngLog = new List<RngDraw>();
            Run(plain, 600);
            Assert.IsFalse(plain.State.RngLog.Any(d => d.Use == RngUse.StairDouble), "확률 0이면 난수를 뽑지 않는다");
        }

        [Test]
        public void CraftGate_DoubleChance_DeliversExtra()
        {
            var t = RunTestTables.Load();
            var row = PerkRow(t, RebirthStatKey.CraftDouble);
            var setup = Setup(); setup.PerkCraftDbl = (int)Math.Ceiling((1 - row.@base) / row.perLv);
            var sim = Sim(new HandLevel().Add("cgate", 0, 40), setup, t);
            Run(sim, 60 * 12);
            var o = sim.State.Objs[0];
            Assert.Greater(o.Made, 0);
            Assert.AreEqual(o.Made, o.Extra);
            Assert.AreEqual(2 * o.Made, Ev(sim, RunEventType.GateTake).Single().A);
        }

        // ---------------- Q-08 지팡이 단계 ----------------

        [Test]
        public void WeaponTier_MaxFromActiveItemRows_MergesPastEight()
        {
            var t = RunTestTables.Load();
            Assert.AreEqual(t.Item.Datas.Where(i => i.active).Max(i => i.tier), WeaponTierMax(t));
            var all = RunTestTables.Load(new Dictionary<string, string> { ["103_item"] = SetCell(RunTestTables.ReadTsv("103_item"), "key", null, "active", "1") });
            int max = all.Item.Datas.Max(i => i.tier);
            Assert.AreEqual(max, WeaponTierMax(all));

            RunSim Merge(MvTables tables)
            {
                var setup = new RunSetup { Deck = new[] { 0, 2, 2, 2, 2, 2, 2, 2, 2 } };
                var sim = Sim(new HandLevel().Add("cgate", 0, 6), setup, tables);
                Run(sim, 60 * 6);
                return sim;
            }
            var capped = Merge(t);
            Assert.AreEqual(WeaponTierMax(t) + 1, capped.State.W.Length);
            Assert.AreEqual(WeaponTierMax(t), capped.State.EqTier(), "켜진 최고 단계 위로는 머지하지 않는다");
            var open = Merge(all);
            Assert.AreEqual(max + 1, open.State.W.Length);
            Assert.Greater(open.State.EqTier(), WeaponTierMax(t), "켜진 행이 늘면 그 단계까지 머지한다");
        }

        // ---------------- Q-17 지팡이 스택 칸 수 ----------------

        [Test]
        public void WeaponStack_FullRejectsDelivery()
        {
            var t = WithConfig(RunConfigKeys.WEAPON_STACK_MAX, "1");
            var sim = Sim(new HandLevel().Add("cgate", 0, 40), t: t);
            Run(sim, 60 * 12);
            var take = Ev(sim, RunEventType.GateTake).Single();
            Assert.Greater(take.A, 0);
            Assert.AreEqual(1, sim.State.W.Sum(), "칸 수 1이 차 있으면 더 받지 않는다");
            Assert.AreEqual(take.A, sim.State.WeaponRejected);
            Assert.AreEqual(take.A, Ev(sim, RunEventType.WeaponRejected).Sum(e => e.A));
        }

        // ---------------- Q-09 분신 상한 ----------------

        [TestCase("4")]
        [TestCase("30")]
        [TestCase("99")]
        public void CloneCap_MinOfConfigAndOffsets(string maxClones)
        {
            var t = WithConfig(RunConfigKeys.MAX_CLONES, maxClones);
            int offsets = t.SpawnGroup.Sorted.Count(g => g.key == "clone");
            int want = Math.Min(int.Parse(maxClones), offsets);
            Assert.AreEqual(want, CloneCap(t));
            var setup = Setup(); setup.AupClone = 1000;
            var s = RunSim.Create(setup, t, new ConstRng(0.5), new RunLevelBuilder()).State;
            Assert.AreEqual(want, s.Clones);
            Assert.AreEqual(want + 1, s.FireT.Length);
        }

        // ---------------- Q-13 결계 막힘 ----------------

        [Test]
        public void Wall_StuckKeepsShootingThenEndsAfterStuckEndSec()
        {
            var t = RunTestTables.Load();
            double end = t.RunConfig.D(RunConfigKeys.STUCK_END_SEC);
            var sim = Sim(new HandLevel().Wall(0, 5, 99999), t: t);
            int f = 0;
            while (Ev(sim, RunEventType.Blocked).Count == 0 && f++ < 600) sim.Tick(1 / 60.0, default);
            Assert.AreEqual(1, Ev(sim, RunEventType.Blocked).Count);
            int fires = Ev(sim, RunEventType.Fire).Count;
            Run(sim, (int)Math.Floor(end * 60) - 2);
            Assert.IsFalse(sim.State.Fin, "STUCK_END_SEC 전에는 끝나지 않는다");
            Assert.Greater(Ev(sim, RunEventType.Fire).Count, fires, "막힌 동안 계속 쏜다");
            Run(sim, 10);
            Assert.IsTrue(sim.State.Fin);
            Assert.AreEqual(FinishReason.Wall, sim.State.Result.Reason);
            Assert.AreEqual(1, Ev(sim, RunEventType.Blocked).Count, "막힘 알림은 막히기 시작할 때 1번");
        }

        // ---------------- Q-15 중간 구역 감속 ----------------

        [TestCase(0, 100, true)]
        [TestCase(50, 100, false)]
        public void MidZone_SlowsForwardSpeed(double from, double to, bool inside)
        {
            var tsv = RunTestTables.ReadTsv("310_template");
            tsv = SetCell(SetCell(tsv, "key", "tpl1", "midFromZ", from.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                "key", "tpl1", "midToZ", to.ToString(System.Globalization.CultureInfo.InvariantCulture));
            var t = RunTestTables.Load(new Dictionary<string, string> { ["310_template"] = tsv });
            var sim = Sim(new HandLevel(), t: t);
            sim.State.Tpl = t.Template.ByKey("tpl1").id;
            sim.Tick(0.05, default);
            double speed = inside ? t.RunConfig.D(RunConfigKeys.RUN_SPEED_MID) : t.RunConfig.D(RunConfigKeys.RUN_SPEED);
            Assert.AreEqual(speed * 0.05, sim.State.P.Z, 1e-12);
        }

        // ---------------- Q-12 피해 공식 ----------------

        [Test]
        public void BaseDmg_AtkPercentIsSummed()
        {
            var t = RunTestTables.Load();
            var p = PerkRow(t, RebirthStatKey.DmgMul);
            var setup = Setup(); setup.PerkPower = 2; setup.Modifiers = new RunModifiers { Dmg = 10 };
            var s = Sim(new HandLevel(), setup, t).State;
            double atk = p.perLv * 2 * 100 + 10;
            Assert.AreEqual(atk, AtkPct(s), 1e-9);
            Assert.AreEqual(WeaponDmg(t, 1) * (1 + atk / 100), BaseDmg(s), 1e-9, "공격력 %는 곱하지 않고 합산");
        }

        // ---------------- Q-28 관통 확률 / Q-16 원석 드롭 ----------------

        [Test]
        public void PierceChance_AddsOnePierce()
        {
            var t = RunTestTables.Load();
            var a = AupRow(t, AreaStatKey.PierceChance);
            var setup = Setup(); setup.AupPierce = (int)Math.Ceiling(1 / a.perLv);
            var sim = Sim(new HandLevel(), setup, t);
            sim.Tick(1 / 60.0, default);
            Assert.AreEqual(1, sim.State.Projs[0].Pierce);
            var plain = Sim(new HandLevel(), t: t);
            plain.State.RngLog = new List<RngDraw>();
            plain.Tick(1 / 60.0, default);
            Assert.AreEqual(0, plain.State.Projs[0].Pierce);
            Assert.IsFalse(plain.State.RngLog.Any(d => d.Use == RngUse.PierceChance));
        }

        [Test]
        public void ObjectKindDropId_SetsDropGroup()
        {
            var t = RunTestTables.Load();
            foreach (var k in t.ObjectKind.Datas.Where(k => k.dropId != 0))
                Assert.AreEqual(k.dropId, RunObjectFactory.Create(t, k, 0, 0).DropGroup, k.key);
        }
    }
}
