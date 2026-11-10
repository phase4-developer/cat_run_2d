using System.Collections.Generic;
using System.Linq;
using MV.Data;
using NUnit.Framework;
using static MV.Run.Tests.Gen.GenTestKit;

namespace MV.Run.Tests.Gen
{
    /// <summary>tplFor·newRun 초기화·장애물 HP 지속·규칙 난수 사용(04 §4.14). 생성 결과 전체는 GenDumpTests가 본다.</summary>
    public sealed class LevelGenTests
    {
        [Test]
        public void TplForFollowsTemplateCycle()
        {
            var t = Tables();
            var want = new Dictionary<int, int> { [1] = 1, [2] = 2, [5] = 5, [8] = 8, [9] = 3, [14] = 8, [15] = 3, [20] = 8, [21] = 3 };
            foreach (var kv in want)
                Assert.AreEqual("tpl" + kv.Value, t.Template.Get(LevelGen.TplFor(t, kv.Key)).key, $"level {kv.Key}");
        }

        [Test]
        public void NewRunFillsStartState()
        {
            var t = Tables();
            var setup = new RunSetup { Area = 0, Zone = 0, Level = 1, UpWeapon = 8, UpStair = 3, AupClone = 9, God = "thunder", Preset = "easy" };
            setup.Deck[1] = 2;
            setup.Deck[3] = 1;
            var s = new RunLevelBuilder().NewRun(setup, t, new ConstRng(0.5));

            Assert.AreEqual("easy", s.Bal.key);
            Assert.AreEqual(100, s.ZoneHp);
            Assert.AreEqual(1, s.StartLevel);
            Assert.AreEqual("bolt", s.El);
            Assert.AreEqual(4 + 7, s.CardPool.Count, "공용 active 4 + 신 active 7");
            Assert.AreEqual(2, s.Bt, "baseTier(8) = 무기 Level 2");
            Assert.AreEqual(1, s.GaugeLv, "kegStart(3) Lv 1");
            Assert.AreEqual(3.0 / 7 * 150, s.GaugeFill, 1e-9, "fill = step/need × gaugeNeed(1)");
            // MV-SPEC-02 Q-09: 분신 상한 = min(MAX_CLONES 30, 314 위치 수) → aup.clone 9 그대로
            Assert.AreEqual(System.Math.Min(RunFormulas.CloneCap(t), setup.AupClone), s.Clones, "min(CloneCap, aup.clone)");
            Assert.AreEqual(9, s.Clones);
            // FIRE_PHASE는 5칸, 나머지 분신 칸은 0(표에 위상 값 없음)
            Assert.AreEqual(1 + RunFormulas.CloneCap(t), s.FireT.Length);
            CollectionAssert.AreEqual(new[] { 0, 0.13, 0.26, 0.07, 0.19 }, s.FireT.Take(5));
            Assert.IsTrue(s.FireT.Skip(5).All(x => x == 0));
            // MV-SPEC-02 Q-08: W 길이 = 103 active 상한 등급 + 1(12등급 → 13)
            Assert.AreEqual(RunFormulas.WeaponTierMax(t) + 1, s.W.Length);
            CollectionAssert.AreEqual(new[] { 0, 2, 0, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0 }, s.W);
            Assert.AreNotSame(setup.Deck, s.W);
            Assert.AreEqual(0, s.P.X);
            Assert.AreEqual(0, s.P.Z);

            setup.UpStair = 7;
            var s2 = new RunLevelBuilder().NewRun(setup, t, new ConstRng(0.5));
            Assert.AreEqual(2, s2.GaugeLv, "kegStart(7) Lv 2");
            Assert.AreEqual(0, s2.GaugeFill);
        }

        [Test]
        public void EnemySpeedIsOnlyRuleDrawPerMouse()
        {
            var t = Tables();
            var rng = new CountingRng(0.25);
            var s = new RunLevelBuilder().NewRun(new RunSetup { Level = 1 }, t, rng);
            int mice = s.Objs.FindAll(o => o.Move != null && o.Move.speedMax > 0).Count;
            Assert.AreEqual(5, mice);
            Assert.AreEqual(mice, rng.Count, "규칙 난수는 쥐 속도 1회씩만");
            foreach (var o in s.Objs.FindAll(o => o.Move != null && o.Move.speedMax > 0))
                Assert.AreEqual(1.1 + 0.25 * (1.6 - 1.1), o.Spd, 1e-12);
        }

        [Test]
        public void SavedBarsKeepHpAndMarkBroken()
        {
            var t = Tables();
            var bars = new BarRecord();
            bars.Set(0, 0, 0);
            bars.Set(0, 1, 2);
            bars.Set(0, 2, 999);
            var setup = new RunSetup { Level = 1, Bars = bars };
            var s = new RunLevelBuilder().NewRun(setup, t, new ConstRng(0.5));
            var walls = s.Objs.FindAll(o => o.IsField && o.Bar == 0);
            Assert.IsTrue(walls[0].Dead && walls[0].Pre, "기록 0 → 부서진 채 시작");
            Assert.AreEqual(2, walls[1].Hp);
            Assert.AreEqual(3, walls[1].Max);
            Assert.AreEqual(3, walls[2].Hp, "기록이 최대보다 크면 최대");
            Assert.IsFalse(walls[2].Dead);
            Assert.AreSame(bars, s.SavedBars);

            setup.KeepBars = false;
            var s2 = new RunLevelBuilder().NewRun(setup, t, new ConstRng(0.5));
            Assert.IsNull(s2.SavedBars);
            Assert.IsFalse(s2.Objs.Find(o => o.IsField).Dead);
        }

        [Test]
        public void CheckpointStartsAtFloorAndBuilderFieldSpecMatchesLevel()
        {
            var t = Tables();
            var b = new RunLevelBuilder();
            var s = b.NewRun(new RunSetup { Level = 3, Area = 1, Zone = 0, Cp = 5, Preset = "mid" }, t, new ConstRng(0.5));
            Assert.AreEqual(2, s.Cp, "cp = min(CP_MAX, save.cp)");
            Assert.AreEqual(1, s.Gates.Count);
            Assert.AreEqual(3, s.Gates[0].Floor);
            var spec = b.FieldSpec(1, 2);
            var walls = s.Objs.FindAll(o => o.IsField);
            Assert.AreEqual(spec.Count, walls.Count);
            for (int i = 0; i < spec.Count; i++)
            {
                Assert.AreEqual(spec[i].Idx, walls[i].Idx);
                Assert.AreEqual(spec[i].Rew, walls[i].Rew);
                Assert.AreEqual(spec[i].Amt, walls[i].RewardAmount);
            }
            Assert.AreEqual("archGoal", s.Objs[s.Objs.Count - 1].Kind.key);
            Assert.AreEqual(2, s.Objs[s.Objs.Count - 1].LabelArg, "Area 2 돌파");
        }

        sealed class CountingRng : IRng
        {
            readonly double _v;
            public int Count;
            public CountingRng(double v) { _v = v; }
            public double NextDouble() { Count++; return _v; }
        }
    }
}
