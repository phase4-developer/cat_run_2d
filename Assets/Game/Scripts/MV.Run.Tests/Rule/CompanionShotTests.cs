using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace MV.Run.Tests.Rule
{
    /// <summary>
    /// 동행 자동 사격(04 §4.20.5·§4.20.6-3). 벽은 레인 끝(x 3.2)에 두어 본체 투사체·막힘과 겹치지 않게 하고 동행만 맞힌다.
    /// </summary>
    public sealed class CompanionShotTests
    {
        const double Dt = 1 / 60.0;
        const int SpearCardId = 110001007; // thunder spear(PierceEveryN, 2볼리마다)
        const int ProcCardId = 110001002;  // thunder procBolt(ProcExtraHit)

        static RunSetup Setup(params CompanionSpec[] comps)
        {
            var s = new RunSetup { Preset = "easy" }; // 발사 횟수 기준은 프로토 easy 연사 2.0(RunTestTables.ProtoEasy, MV-SPEC2-FIX2)
            s.Deck[1] = 1;
            s.Companions.AddRange(comps);
            return s;
        }

        static void GiveCard(RunState s, int id) { s.Cards[id] = 1; s.CardOrder.Add(id); }

        [Test]
        public void Seconds_HoldsWithoutTarget()
        {
            // 벽 앞면 z = 40 − 0.55. 사거리 22라 P.z가 17.45를 넘는 프레임(약 4.4초)부터 대상이 생긴다.
            var lv = new HandLevel().Wall(3.2, 40, 1e9);
            var sim = RunSim.Create(Setup(new CompanionSpec(430001, CompanionTrigger.Seconds, 0.5, 100, 1)),
                RunTestTables.ProtoEasy(), new ConstRng(0.5), lv);
            var s = sim.State;
            var wall = s.Objs[0];
            double range = RunFormulas.ProjRange(s);
            var shotFrames = new List<int>();
            bool prevInRange = false;
            int enterFrame = -1;
            for (int f = 0; f < 420; f++)
            {
                sim.Tick(Dt, RunInput.None);
                bool inRange = wall.Z - wall.Hd - s.P.Z <= range;
                if (inRange && !prevInRange) enterFrame = f;
                prevInRange = inRange;
                foreach (var e in sim.DrainEvents().Where(e => e.Type == RunEventType.CompanionShot))
                {
                    shotFrames.Add(f);
                    CollectionAssert.AreEqual(new[] { wall.Id }, e.Ids);
                    Assert.AreEqual(430001, e.A);
                    Assert.AreEqual(RunFormulas.BaseDmg(s), e.Amount, 1e-12);
                }
            }
            Assert.Greater(enterFrame, 60, "시작 뒤 Every(0.5초)보다 한참 늦게 대상이 생겨야 보류를 볼 수 있다");
            Assert.IsNotEmpty(shotFrames);
            Assert.AreEqual(enterFrame, shotFrames[0], "보류했다가 대상이 생기는 프레임에 쏜다");
            Assert.GreaterOrEqual(shotFrames.Count, 2);
            Assert.AreEqual(30, shotFrames[1] - shotFrames[0], 1, "다음 발은 Every 뒤(보류 시간만큼 몰아 쏘지 않는다)");
            Assert.Less(wall.Hp, 1e9, "동행 피해가 들어갔다");
        }

        [Test]
        public void Hits_EveryNVolleysIncludingSpear()
        {
            var lv = new HandLevel().Wall(3.2, 15, 1e9);
            var setup = Setup(new CompanionSpec(450001, CompanionTrigger.Hits, 3, 50, 1));
            setup.ForwardSpeed = 0; // 제자리(벽이 계속 사거리 안)
            var sim = RunSim.Create(setup, RunTestTables.ProtoEasy(), new ConstRng(0.5), lv);
            var s = sim.State;
            GiveCard(s, SpearCardId);
            int shots = 0, spearShots = 0;
            for (int f = 0; f < 900; f++)
            {
                int vol0 = s.Vol;
                sim.Tick(Dt, RunInput.None);
                var ev = sim.DrainEvents();
                int n = ev.Count(e => e.Type == RunEventType.CompanionShot);
                bool spear = ev.Any(e => e.Type == RunEventType.Fire && e.Flag);
                bool want = s.Vol != vol0 && s.Vol % 3 == 0;
                Assert.AreEqual(want ? 1 : 0, n, $"f{f} vol {vol0}→{s.Vol}");
                shots += n;
                if (n > 0 && spear) spearShots++;
            }
            Assert.GreaterOrEqual(s.Vol, 20);
            Assert.AreEqual(s.Vol / 3, shots);
            Assert.Greater(spearShots, 0, "창 볼리도 R.vol에 든다");
        }

        [Test]
        public void Chance_DoesNotChangeRunRngOrder()
        {
            var plain = ChanceRun(false);
            var with = ChanceRun(true);
            Assert.Greater(with.shots, 0, "동행이 실제로 쐈다");
            Assert.AreEqual(0, plain.shots);
            Assert.AreEqual(1, with.buildDraws, "생성 단계 규칙 난수는 쥐 속도 1개");
            Assert.AreEqual(plain.mouseSpd, with.mouseSpd, "쥐 속도");
            Assert.AreEqual(plain.draws.Count, with.draws.Count, "RunRng 소비 수");
            for (int i = 0; i < plain.draws.Count; i++) Assert.AreEqual(plain.draws[i], with.draws[i], $"RunRng {i}번째(사용처:값)");
            Assert.Greater(plain.draws.Count(d => d.StartsWith(((int)RngUse.Proc).ToString() + ":")), 0, "신 가호 난수가 섞여 있어야 순서 시험이 된다");
        }

        /// <summary>계단 게이트(동행 제외)를 본체가 맞히고 procBolt가 RunRng를 뽑는 동안 Chance 동행이 옆 벽만 쏜다.</summary>
        static (List<string> draws, int shots, int buildDraws, double mouseSpd) ChanceRun(bool companion)
        {
            var lv = new HandLevel().Add("enemy", -3.2, 300).Add("sgate", 0, 12).Wall(3.2, 12, 1e9);
            var setup = companion ? Setup(new CompanionSpec(450002, CompanionTrigger.Chance, 0.5, 100, 2)) : Setup();
            setup.ForwardSpeed = 0;
            var rng = new LoggingRng(new Pcg32Rng(4242));
            var sim = RunSim.Create(setup, RunTestTables.ProtoEasy(), rng, lv);
            var s = sim.State;
            int buildDraws = rng.Values.Count;
            GiveCard(s, ProcCardId);
            s.RngLog = new List<RngDraw>();
            int shots = 0;
            for (int f = 0; f < 900; f++)
            {
                sim.Tick(Dt, RunInput.None);
                shots += sim.DrainEvents().Count(e => e.Type == RunEventType.CompanionShot);
            }
            Assert.AreEqual(rng.Values.Count, buildDraws + s.RngLog.Count, "RunRng은 RunState.Draw로만 뽑는다");
            var draws = new List<string>();
            for (int i = 0; i < buildDraws; i++) draws.Add("build:" + rng.Values[i].ToString("R", System.Globalization.CultureInfo.InvariantCulture));
            foreach (var d in s.RngLog) draws.Add(d.ToString());
            return (draws, shots, buildDraws, s.Objs[0].Spd);
        }
    }
}
