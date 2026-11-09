using System;
using MV.Data;
using NUnit.Framework;

namespace MV.Run.Tests.Rule
{
    /// <summary>
    /// RunModifiers 7필드(04 §4.20.4). 검증 예(Area 1 easy, 강화·카드 0)와 「0이면 정본 식과 비트 단위로 같다」,
    /// 「정본 식 끝에 (1 + m/100) 또는 + m/100으로 붙는다」를 본다.
    /// </summary>
    public sealed class RunModifierTests
    {
        static RunState State(RunModifiers m, double? rate = null)
        {
            var setup = new RunSetup { Area = 0, Zone = 0, Level = 1, Preset = "easy", Modifiers = m, RateOverride = rate };
            setup.Deck[1] = 1;
            return new RunLevelBuilder().NewRun(setup, RunTestTables.Load(), new ConstRng(0.5));
        }

        static long Bits(double v) => BitConverter.DoubleToInt64Bits(v);

        static void Same(double want, double got, string what) =>
            Assert.AreEqual(Bits(want), Bits(got), $"{what}: 정본 {want:R} / 지금 {got:R}");

        [Test]
        public void Dmg()
        {
            var t = RunTestTables.Load();
            var z = State(RunModifiers.Zero);
            var power = t.RebirthPerk.ByKey("power");
            double canon = RunFormulas.WeaponDmg(t, 1) * RunFormulas.CardMul(z, CardEffectType.DmgMul) * (power.@base + power.perLv * 0);
            Same(canon, RunFormulas.BaseDmg(z), "Dmg 0");
            var m = State(new RunModifiers { Dmg = 10 });
            Same(canon * (1 + 10 / 100.0), RunFormulas.BaseDmg(m), "Dmg 10 위치");
            Assert.AreEqual(11, RunFormulas.BaseDmg(m), 1e-9);
        }

        [Test]
        public void FireRate()
        {
            var z = State(RunModifiers.Zero, 2);
            double cm = RunFormulas.CardMul(z, CardEffectType.FireRateMul);
            double canon = 1 / (2 * cm * (1 + 0.0));
            Same(canon, RunFormulas.FireInterval(z), "FireRate 0");
            var m = State(new RunModifiers { FireRate = 20 }, 2);
            Same(1 / (2 * cm * (1 + 0.0) * (1 + 20 / 100.0)), RunFormulas.FireInterval(m), "FireRate 20 위치");
            Assert.AreEqual(1 / 2.4, RunFormulas.FireInterval(m), 1e-12);
        }

        [Test]
        public void Range()
        {
            var t = RunTestTables.Load();
            double canon = t.RunConfig.D(RunConfigKeys.PROJ_RANGE) * (1 + 0.0);
            Same(canon, RunFormulas.ProjRange(State(RunModifiers.Zero)), "Range 0");
            var m = State(new RunModifiers { Range = 10 });
            Same(canon * (1 + 10 / 100.0), RunFormulas.ProjRange(m), "Range 10 위치");
            Assert.AreEqual(24.2, RunFormulas.ProjRange(m), 1e-9);
        }

        [Test]
        public void CritDmg()
        {
            var t = RunTestTables.Load();
            var crit = t.RebirthPerk.ByKey("crit");
            double canon = crit.@base + crit.perLv * 0;
            Same(canon, RunFormulas.CritMul(State(RunModifiers.Zero)), "CritDmg 0");
            var m = State(new RunModifiers { CritDmg = 50 });
            Same(canon + 50 / 100.0, RunFormulas.CritMul(m), "CritDmg 50 위치");
            Assert.AreEqual(2.7, RunFormulas.CritMul(m), 1e-12);
        }

        [Test]
        public void Gold()
        {
            var t = RunTestTables.Load();
            var z = State(RunModifiers.Zero);
            double canon = (1 + t.RunConfig.D(RunConfigKeys.INCOME_PER_LV) * 0) * RunFormulas.CardMul(z, CardEffectType.GoldMul);
            Same(canon, RunFormulas.GoldMul(z), "Gold 0");
            var m = State(new RunModifiers { Gold = 10 });
            Same(canon * (1 + 10 / 100.0), RunFormulas.GoldMul(m), "Gold 10 위치");
            Assert.AreEqual(1.1, RunFormulas.GoldMul(m), 1e-12);
        }

        [Test]
        public void CritChance()
        {
            var z = State(RunModifiers.Zero);
            double canon = RunFormulas.CardSum(z, CardEffectType.CritChance) + 0.0;
            Same(canon, RunFormulas.CritChance(z), "CritChance 0");
            var m = State(new RunModifiers { CritChance = 5 });
            Same(canon + 5 / 100.0, RunFormulas.CritChance(m), "CritChance 5 위치");
            Assert.AreEqual(0.05, RunFormulas.CritChance(m), 1e-12);
            Assert.AreEqual(Draws(RunModifiers.Zero, RngUse.Crit), Draws(new RunModifiers { CritChance = 100 }, RngUse.Crit),
                "치명 난수는 확률과 무관하게 볼리마다 1회");
        }

        [Test]
        public void DoubleChance()
        {
            var z = State(RunModifiers.Zero);
            double canon = Math.Min(1, RunFormulas.CardSum(z, CardEffectType.DoubleChance) + 0.0);
            Same(canon, RunFormulas.DoubleChance(z), "DoubleChance 0");
            var m = State(new RunModifiers { DoubleChance = 10 });
            Same(Math.Min(1, canon + 10 / 100.0), RunFormulas.DoubleChance(m), "DoubleChance 10 위치");
            Assert.AreEqual(0.1, RunFormulas.DoubleChance(m), 1e-12);
            Assert.AreEqual(1, RunFormulas.DoubleChance(State(new RunModifiers { DoubleChance = 500 })), "상한 1");
        }

        [Test]
        public void Validate_RejectsNegative()
        {
            Assert.IsNull(RunModifiers.Zero.Validate());
            Assert.IsNull(new RunModifiers { FireRate = -99 }.Validate());
            Assert.IsNotNull(new RunModifiers { FireRate = -100 }.Validate());
            Assert.IsNotNull(new RunModifiers { Dmg = -1 }.Validate());
            Assert.IsNotNull(new RunModifiers { DoubleChance = -0.5 }.Validate());
            var setup = new RunSetup { Modifiers = new RunModifiers { Gold = -1 } };
            setup.Deck[1] = 1;
            Assert.Throws<ArgumentException>(() => RunSim.Create(setup, RunTestTables.Load(), new ConstRng(0.5), new HandLevel()));
        }

        /// <summary>빈 레벨에서 3초 동안 뽑은 사용처 수.</summary>
        static int Draws(RunModifiers m, RngUse use)
        {
            var setup = new RunSetup { Modifiers = m };
            setup.Deck[1] = 1;
            var sim = RunSim.Create(setup, RunTestTables.Load(), new ConstRng(0.5), new HandLevel());
            sim.State.RngLog = new System.Collections.Generic.List<RngDraw>();
            for (int i = 0; i < 180; i++) sim.Tick(1 / 60.0, RunInput.None);
            return sim.State.RngLog.FindAll(d => d.Use == use).Count;
        }
    }
}
