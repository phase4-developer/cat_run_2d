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

        [TestCase(20, 10)]
        [TestCase(12, 10)]
        [TestCase(7, 3)]
        [TestCase(5, 1)]
        public void RewardShare_SumIsTotalRemainderLast(int total, int n)
        {
            double sum = 0;
            for (int k = 1; k <= n; k++) sum += RewardShare(total, n, k);
            Assert.AreEqual(total, sum, 1e-12);
            Assert.AreEqual(System.Math.Floor((double)total / n), RewardShare(total, n, 1), 1e-12);
            Assert.AreEqual(total - System.Math.Floor((double)total / n) * (n - 1), RewardShare(total, n, n), 1e-12);
        }

        [TestCase(0, 100, 10, 0)]
        [TestCase(9, 100, 10, 0)]
        [TestCase(10, 100, 10, 1)]
        [TestCase(35, 100, 10, 3)]
        [TestCase(99, 100, 10, 9)]
        [TestCase(100, 100, 10, 10)]
        [TestCase(50, 100, 1, 0)]
        public void SharesDue_ThresholdsCrossed(int dealt, int max, int n, int expect) => Assert.AreEqual(expect, SharesDue(dealt, max, n));

        [Test]
        public void JsRound_HalfUp() { Assert.AreEqual(3, JsRound(2.5)); Assert.AreEqual(1, JsRound(0.5)); Assert.AreEqual(2, JsRound(2.4999)); }

        [Test]
        public void BaseDmg_TierAndModifier()
        {
            var d = NewState();
            Assert.AreEqual(WeaponDmg(d.Tables, 1), BaseDmg(d), 1e-12, "장착 단계 103_item.dmg 그대로");
            var s = NewState(new RunSetup { Deck = new[] { 0, 1, 0, 0, 0, 0, 0, 0, 0 }, Modifiers = new RunModifiers { Dmg = 10 } });
            Assert.AreEqual(System.Math.Floor(WeaponDmg(s.Tables, 1) * 1.1), BaseDmg(s), 1e-9, "공격력 정수 버림(MV-SPEC2-FIX1)");
            // 103_item 더미(HE_07 전): 1 × 1.8^(단계−1) 반올림(MV-SPEC2-FIX1)
            Assert.AreEqual(new[] { 1, 2, 3, 6, 10, 19, 34, 61 }, Enumerable.Range(1, 8).Select(i => (int)WeaponDmg(s.Tables, i)).ToArray());
        }

        [Test]
        public void FireInterval_EasyAndModifier()
        {
            // 기본 연사는 06_balancePreset isDefault 행 rate(MV-SPEC-02 155 「기본 연사 0.95」, MV-SPEC2-FIX1). 값은 표에서 읽는다.
            var d = NewState();
            Assert.AreEqual(1 / d.Bal.rate, FireInterval(d), 1e-12);
            var s = NewState(new RunSetup { Modifiers = new RunModifiers { FireRate = 20 } });
            Assert.AreEqual(1 / (s.Bal.rate * 1.2), FireInterval(s), 1e-12);
        }

        [Test]
        public void Range_Crit_Gold_Chances()
        {
            var s = NewState();
            Assert.AreEqual(22, ProjRange(s), 1e-12);
            double crit0 = PerkValue(s.Tables, RebirthStatKey.CritMul, 0); // 14 CritMul base(MV-SPEC-02 572 200%)
            Assert.AreEqual(crit0, CritMul(s), 1e-12);
            Assert.AreEqual(1, GoldMul(s), 1e-12);
            Assert.AreEqual(0, CritChance(s), 1e-12);
            Assert.AreEqual(0, DoubleChance(s), 1e-12);
            var m = NewState(new RunSetup { Modifiers = new RunModifiers { Range = 10, CritDmg = 50, Gold = 10, CritChance = 5, DoubleChance = 10 } });
            Assert.AreEqual(24.2, ProjRange(m), 1e-9);
            Assert.AreEqual(crit0 + 0.5, CritMul(m), 1e-9);
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

        /// <summary>런 종료 「레벨당 골드·블루 마법석 획득」 = 13 perLv × 강화 Lv × 런 Level(MV-SPEC-02 529~530, MV-SPEC2-FIX1).</summary>
        [Test]
        public void PerLevelReward_FromAreaUpgradeRows()
        {
            var t = RunTestTables.Load();
            var st = new RunSetup { AupGoldLv = 2, AupDiaLv = 3 };
            Assert.AreEqual(AupRow(t, AreaStatKey.GoldPerLevel).perLv * 2 * 4, PerLevelReward(t, st, AreaStatKey.GoldPerLevel, 4), 1e-12);
            Assert.AreEqual(AupRow(t, AreaStatKey.DiaPerLevel).perLv * 3 * 4, PerLevelReward(t, st, AreaStatKey.DiaPerLevel, 4), 1e-12);
            Assert.AreEqual(0, PerLevelReward(t, new RunSetup(), AreaStatKey.GoldPerLevel, 4), 1e-12, "Lv 0이면 0");
        }

        /// <summary>
        /// Area 1 기본 난이도(06 easy = isDefault, scale 1·gauge 1, MV-SPEC2-FIX2). MV-SPEC-02 247 「임계값 T는 첫 장 1.9, 다음 5.7」,
        /// 479 「게이트 충전·탑 게이지는 Area 단계를 따른다」.
        /// </summary>
        [Test]
        public void ChargeThresholds_Area1Default()
        {
            var s = NewState(new RunSetup { Deck = new[] { 0, 1, 0, 0, 0, 0, 0, 0, 0 } });
            var t = s.Tables;
            Assert.AreEqual(1, s.Bal.scale, "기본 난이도 scale 1");
            // MV-SPEC-02: sgate는 107 mul1·useAreaHp0·diffMul0 + 지역 sgateM. 표가 비었을 때 107 base 1.9.
            Assert.AreEqual(1.9, Threshold(t.ChargeGate.ByKey("sgate"), 0, s.Bal, s.ZoneHp), 1e-9);
            var cg = t.ChargeGate.ByKey("cgate");
            Assert.AreEqual(new[] { 1.9, 5.7, 9.5, 13.3 }, Enumerable.Range(0, 4).Select(k => System.Math.Round(Threshold(cg, k, s.Bal, s.ZoneHp), 9)).ToArray());
            var need = Enumerable.Range(1, 7).Select(lv => System.Math.Round(GaugeNeed(s, lv), 2)).ToArray();
            Assert.AreEqual(new[] { 150, 240, 384, 614.4, 983.04, 1572.86, 2516.58 }, need);
        }

        [Test]
        public void Climb_KCompressionAndTiers()
        {
            var t = RunTestTables.Load(); var c = t.Climb.Config();
            // 새 HTML startClimb(2026-11-13 연출 정본): k = min(600, n), T = 0.8 + 0.09k.
            Assert.AreEqual(30, ClimbK(c, 30)); Assert.AreEqual(600, ClimbK(c, 600)); Assert.AreEqual(600, ClimbK(c, 1000));
            Assert.AreEqual(0.89, ClimbT(c, 1), 1e-12); Assert.AreEqual(1.7, ClimbT(c, 10), 1e-12); Assert.AreEqual(54.8, ClimbT(c, 600), 1e-9);
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
            // MV-SPEC-02: LANE_X_MAX 3.5, MOVE_FOLLOW_K 10 → 한 틱 추종 0.05 × 10
            Assert.AreEqual(3.5, sim.State.P.Tx, 1e-12);
            Assert.AreEqual(3.5 * 0.5, sim.State.P.X, 1e-12);
            Assert.AreEqual(0.2, sim.State.P.Z, 1e-12);
            sim.Tick(1.0, RunInput.Key(-1)); // dt 상한 0.05
            Assert.AreEqual(3.5 - 0.05 * 9, sim.State.P.Tx, 1e-12);
        }

        [Test]
        public void Wall_BlocksAndFinishes()
        {
            var sim = Sim(new HandLevel().Wall(0, 5, 99999));
            Run(sim, 60 * 6); // MV-SPEC-02 13-2 현재 기준: STUCK_END_SEC 0 = 닿는 프레임에 종료(값이 늘어도 6초 안)
            var s = sim.State;
            Assert.IsTrue(s.Fin);
            Assert.AreEqual(FinishReason.Wall, s.Result.Reason);
            Assert.AreEqual(5 - 0.55 - 0.35, s.P.Z, 1e-9);
            Assert.AreEqual(1, Ev(sim, RunEventType.Blocked).Count);
        }

        // 103 피해가 원작 단위(1단계 1)로 내려가 1단계로는 지나가기 전에 못 깬다 → 피해 10인 단계(103 더미 5단계)·easy 연사로 본다(MV-SPEC2-FIX1).
        static RunSetup BoxSetup(RunModifiers mods = default, int perkDcap = 0) =>
            new RunSetup { Deck = new[] { 0, 0, 0, 0, 0, 1, 0, 0, 0 }, Preset = "easy", Modifiers = mods, PerkDcap = perkDcap };

        /// <summary>상자 시험: 프로토 easy 연사 표(MV-SPEC2-FIX2, RunTestTables.ProtoEasy).</summary>
        static RunSim BoxSim(HandLevel lv, RunSetup setup, IRng rng = null) => RunSim.Create(setup, RunTestTables.ProtoEasy(), rng ?? new ConstRng(0.5), lv);

        static List<RunEvent> Gains(RunSim sim, RunObject o) => Ev(sim, RunEventType.Gain).Where(e => e.ObjId == o.Id).ToList();

        [Test]
        public void Box_SharesSumToTotalAndBreak()
        {
            // 2026-11-11 사용자 결정(MV-RUN-SPLITREWARD): 총량(rewardValue + perZone)을 rewardShares 등분해 피해 비율대로 준다. 타격·파괴 별도 보상 없음.
            var sim = BoxSim(new HandLevel().Add("coin", 0, 30), BoxSetup());
            Run(sim, 480);
            var s = sim.State;
            var box = s.Objs[0];
            Assert.IsTrue(box.Dead);
            Assert.AreEqual(box.Kind.rewardShares, box.SharesPaid);
            Assert.AreEqual(box.Kind.rewardShares, Gains(sim, box).Count, "몫마다 지급 1번(몫 > 0)");
            Assert.AreEqual(box.RewardAmount, s.Gold, 1e-9, "몫 합 = 총량(goldMul 1)");
            Assert.AreEqual(1, Ev(sim, RunEventType.Break).Count);
            Assert.AreEqual(0.03, Ev(sim, RunEventType.Hitstop).First().Amount, 1e-12);
        }

        [Test]
        public void Box_OneHitCrossingSeveralThresholdsPaysAll()
        {
            // 첫 타격 뒤 체력을 그 피해 × 2로 다시 맞추면, 다음 한 타격이 문턱 N/2개를 한 번에 넘는다.
            var sim = BoxSim(new HandLevel().Add("coin", 0, 30), BoxSetup());
            var box = sim.State.Objs[0];
            int frames = 0;
            while (Ev(sim, RunEventType.Hit).Count(e => e.ObjId == box.Id) == 0 && frames++ < 480) sim.Tick(1 / 60.0, default);
            double dmg = Ev(sim, RunEventType.Hit).First(e => e.ObjId == box.Id).Amount;
            Assert.IsFalse(box.Dead);
            box.Max = box.Hp = dmg * 2; box.SharesPaid = 0; sim.State.Gold = 0;
            int n = box.Kind.rewardShares;
            int before = Gains(sim, box).Count;
            while (Ev(sim, RunEventType.Hit).Count(e => e.ObjId == box.Id) < 2 && frames++ < 960) sim.Tick(1 / 60.0, default);
            int due = RunFormulas.SharesDue(dmg, box.Max, n);
            Assert.Greater(due, 1, "한 타격에 문턱 2개 이상");
            Assert.AreEqual(due, box.SharesPaid);
            Assert.AreEqual(due, Gains(sim, box).Count - before);
            double expect = 0;
            for (int k = 1; k <= due; k++) expect += RunFormulas.RewardShare(box.RewardAmount, n, k);
            Assert.AreEqual(expect, sim.State.Gold, 1e-9);
        }

        [Test]
        public void Box_RemainderOnLastShare()
        {
            // 보너스 금화 더미: zone 0 총량 12, 10등분 → 1 × 9 + 3.
            var sim = BoxSim(new HandLevel().Add("coinBonus", 0, 30), BoxSetup());
            Run(sim, 480);
            var box = sim.State.Objs[0];
            Assert.IsTrue(box.Dead);
            var g = Gains(sim, box);
            int n = box.Kind.rewardShares;
            Assert.AreEqual(n, g.Count);
            double per = System.Math.Floor(box.RewardAmount / n);
            for (int i = 0; i < n - 1; i++) Assert.AreEqual(per, g[i].Amount, 1e-9);
            Assert.AreEqual(box.RewardAmount - per * (n - 1), g[n - 1].Amount, 1e-9, "나머지는 마지막 몫");
            Assert.AreEqual(box.RewardAmount, sim.State.Gold, 1e-9);
        }

        [Test]
        public void Box_GoldPerkAppliedPerShare()
        {
            var sim = BoxSim(new HandLevel().Add("coin", 0, 30), BoxSetup(new RunModifiers { Gold = 10 }));
            double gm = GoldMul(sim.State);
            Run(sim, 480);
            var box = sim.State.Objs[0];
            Assert.IsTrue(box.Dead);
            int n = box.Kind.rewardShares;
            var g = Gains(sim, box);
            Assert.AreEqual(n, g.Count);
            for (int k = 1; k <= n; k++) Assert.AreEqual(RunFormulas.RewardShare(box.RewardAmount, n, k) * gm, g[k - 1].Amount, 1e-9, "몫마다 × goldMul");
            Assert.AreEqual(box.RewardAmount * gm, sim.State.Gold, 1e-9);
        }

        [TestCase(true)]
        [TestCase(false)]
        public void Ore_OneShareOnBreakRollsDrop(bool dropped)
        {
            double roll = dropped ? 0.01 : 0.99; // 801002001 누적 비중: 보상 1000 / 꽝 9000
            // 원석: rewardShares 1 = 부술 때 한 번. 그 몫에서 dropId(801002001, 10%) 추첨. 블루 마법석 획득량 특성(DiaCap)이 몫에 붙는다.
            var sim = BoxSim(new HandLevel().Add("gem", 0, 30), BoxSetup(perkDcap: 2), new ConstRng(roll));
            var ore = sim.State.Objs[0];
            Assert.AreEqual(1, ore.Kind.rewardShares);
            Run(sim, 480);
            Assert.IsTrue(ore.Dead);
            Assert.AreEqual(1, ore.SharesPaid);
            var ev = sim.State.Events;
            int breakAt = ev.FindIndex(e => e.Type == RunEventType.Break && e.ObjId == ore.Id);
            Assert.IsFalse(ev.Take(breakAt).Any(e => e.Type == RunEventType.Gain && e.ObjId == ore.Id), "부수기 전에는 지급 없음");
            Assert.AreEqual(dropped ? 1 : 0, Gains(sim, ore).Count);
            Assert.AreEqual(dropped ? (int)DiaCap(sim.State.Tables, 2) : 0, sim.State.Dia);
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

        /// <summary>PC-15: 훔치기는 런에서 얻은 몫(합계 − 시작 골드)만 가져간다. 시작 골드는 남는다.</summary>
        [TestCase(10.0, 12.0, 10.0)]
        [TestCase(10.0, 10.7, 10.7)]
        [TestCase(10.0, 10.0, 10.0)]
        public void Enemy_StealSkipsStartGold(double start, double total, double after)
        {
            var sim = Sim(new HandLevel().Add("enemy", 3.0, 3, o => { o.HasHp = true; o.Hp = 99999; }));
            sim.State.StartGold = start;
            sim.State.Gold = total;
            Run(sim, 120, RunInput.Drag(3.2));
            Assert.AreEqual(1, Ev(sim, RunEventType.Steal).Count);
            Assert.AreEqual(after, sim.State.Gold, 1e-9);
            Assert.GreaterOrEqual(sim.State.Gold, start);
        }

        /// <summary>PC-15: 정산 결과에 시작 몫을 따로 싣는다(합계에는 포함).</summary>
        [Test]
        public void Settlement_CarriesStartShare()
        {
            var b = new RunLevelBuilder();
            var s = b.NewRun(Setup(), RunTestTables.Load(), new ConstRng(0.5));
            s.StartGold = 7; s.Gold = 20;
            s.StartDia = 2; s.Dia = 5;
            var r = RunSettlement.Main(s, FinishReason.Wall, b);
            Assert.AreEqual(7, r.StartGold, 1e-9);
            Assert.AreEqual(2, r.StartDia);
            Assert.GreaterOrEqual(r.Gold, 20);
            Assert.GreaterOrEqual(r.Dia, 5);
        }

        [Test]
        public void Climb_RouletteAppliesCards()
        {
            var sim = Sim(new HandLevel().Add("stair", 0, 3));
            sim.State.Stairs = 80;
            Run(sim, 60 * 20);   // T = 0.8 + 0.09·80 = 8초 + 룰렛
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
                ["rewardShares"] = "2",
            });
            var t = RunTestTables.Load(RunTestTables.WithProtoEasy(new Dictionary<string, string> { ["105_objectKind"] = kinds })); // 프로토 easy 연사(MV-SPEC2-FIX2)
            Assert.IsNotNull(t.ObjectKind.ByKey("fishCrate"));
            var lv = new HandLevel().Add("fishCrate", 0, 20);
            // 피해 10인 단계(103 더미 5단계)·easy 연사(MV-SPEC2-FIX1: 1단계 피해 1).
            var sim = RunSim.Create(new RunSetup { Deck = new[] { 0, 0, 0, 0, 0, 1, 0, 0, 0 }, Preset = "easy" }, t, new ConstRng(0.5), lv);
            var o = sim.State.Objs[0];
            Assert.AreEqual(25, o.Hp, "bonus025: round(100 × 0.25)");
            for (int i = 0; i < 300; i++) sim.Tick(1 / 60.0, default);
            Assert.IsTrue(o.Dead);
            Assert.AreEqual(2, sim.State.Souls, "행에 적은 2등분 몫 합 = 총량");
            Assert.AreEqual(2, o.SharesPaid);
            Assert.AreEqual(0, sim.State.Gold, 1e-9, "타격 보상 열 없음(등분 지급만)");
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
