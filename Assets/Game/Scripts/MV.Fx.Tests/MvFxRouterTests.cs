using System;
using System.Collections.Generic;
using System.Linq;
using MV.Data;
using MV.Run;
using NUnit.Framework;
using UnityEngine;

namespace MV.Fx.Tests
{
    /// <summary>RunEvent → 04_effect 그룹 key가 정본 이펙트 호출 지점(06 §6.4.2~6.4.5)과 1:1인지.</summary>
    public sealed class MvFxRouterTests
    {
        static MvFxOutput Route(MvTables t, FakeFxWorld w, float roll, params RunEvent[] events)
        {
            var r = new MvFxRouter(() => t, () => roll);
            var all = new MvFxOutput();
            var o = new MvFxOutput();
            foreach (var e in events)
            {
                o.Clear();
                r.Route(e, w, o);
                all.Groups.AddRange(o.Groups); all.Spawns.AddRange(o.Spawns); all.Pops.AddRange(o.Pops); all.Flies.AddRange(o.Flies);
                all.Flashes.AddRange(o.Flashes); all.Shakes.AddRange(o.Shakes);
                if (o.WarpStage >= 0) all.WarpStage = o.WarpStage;
                all.WarpBegin |= o.WarpBegin; all.WarpEnd |= o.WarpEnd;
            }
            return all;
        }

        static RunEvent Ev(RunEventType type, int objId = 0, int kindId = 0, int a = 0, int b = 0, int c = 0, double x = 0, double z = 0,
            double amount = 0, double amount2 = 0, bool flag = false, bool quiet = false, string key = null, int[] ids = null) =>
            new RunEvent(type, objId: objId, kindId: kindId, a: a, b: b, c: c, x: x, z: z, amount: amount, amount2: amount2, flag: flag, quiet: quiet, key: key, ids: ids);

        sealed class Site
        {
            public string Line;
            public Func<MvTables, RunEvent[]> Events;
            public string[] Groups;
        }

        static Site S(string line, Func<MvTables, RunEvent[]> ev, params string[] groups) => new Site { Line = line, Events = ev, Groups = groups };

        static int K(MvTables t, string k) => FxTestTables.Kind(t, k);

        static IEnumerable<Site> Sites()
        {
            yield return S("L2107 투사체 적중", t => new[] { Ev(RunEventType.ProjImpact, 1, x: 0, z: 5, flag: true) }, "run.projHit");
            yield return S("L1912 tower 피격(확률 0.3 통과)", t => new[] { Ev(RunEventType.Hit, 1, K(t, "tower"), amount: 3) }, "run.towerHit");
            yield return S("L1921 enemy 피격(이펙트 없음)", t => new[] { Ev(RunEventType.Hit, 2, K(t, "enemy"), amount: 3) });
            yield return S("L1933 wall 피격", t => new[] { Ev(RunEventType.Hit, 3, K(t, "wall"), amount: 3) }, "run.wallHit");
            yield return S("L1961 coin 피격", t => new[] { Ev(RunEventType.Hit, 4, K(t, "coin"), amount: 3) }, "run.coinHit");
            yield return S("L1961 gem 피격", t => new[] { Ev(RunEventType.Hit, 5, K(t, "gem"), amount: 3) }, "run.coinHit");
            yield return S("L1925 쥐 처치(102 breakEffect)", t => new[] { Ev(RunEventType.Break, 2, K(t, "enemy"), a: -1) }, "run.enemyKill");
            yield return S("L1935~1937 wall 파괴 + 생선", t => new[] { Ev(RunEventType.Break, 3, K(t, "wall"), a: (int)FieldReward.Soul) }, "run.wallBreak", "run.wallRewardSoul");
            yield return S("L1935~1939 wall 파괴 + 캣닢", t => new[] { Ev(RunEventType.Break, 3, K(t, "wall"), a: (int)FieldReward.Dia) }, "run.wallBreak", "run.wallRewardDia");
            yield return S("L1935 wall 파괴 + 츄르(추가 불꽃 없음)", t => new[] { Ev(RunEventType.Break, 3, K(t, "wall"), a: (int)FieldReward.Gold) }, "run.wallBreak");
            yield return S("L1963 coin 상자 파괴", t => new[] { Ev(RunEventType.Break, 4, K(t, "coin"), a: -1) }, "run.coinBreak");
            yield return S("L1964 gem 상자 파괴", t => new[] { Ev(RunEventType.Break, 5, K(t, "gem"), a: -1) }, "run.coinBreak");
            yield return S("L1987 신 번개 발동", t => new[] { Ev(RunEventType.GodProc, 2, key: "bolt") }, "run.procBolt");
            yield return S("L1988 신 화상 발동", t => new[] { Ev(RunEventType.GodProc, 2, key: "fire") }, "run.procFire");
            yield return S("L1997 번개 연쇄", t => new[] { Ev(RunEventType.GodChain, 2, key: "bolt", x: 1, z: 3) }, "run.chainBolt");
            yield return S("L2000 불씨 폭발", t => new[] { Ev(RunEventType.GodChain, key: "fire", x: 1, z: 3, amount: 1.9) }, "run.chainFire");
            yield return S("L2046 계단 관문 통과", t => new[] { Ev(RunEventType.GateTake, 6, K(t, "sgate"), a: 3) }, "run.statGatePass");
            yield return S("L2051 동료 관문 통과", t => new[] { Ev(RunEventType.GateTake, 7, K(t, "cgate"), a: 2) }, "run.cloneGatePass");
            yield return S("L2063 츄르 빼앗김", t => new[] { Ev(RunEventType.Steal, 2, a: 5, x: 0, z: 4) }, "run.steal");
            yield return S("L2068 막힘", t => new[] { Ev(RunEventType.Blocked, 3, x: 0, z: 9) }, "run.blocked");
            yield return S("L2137 화상 틱", t => new[] { Ev(RunEventType.BurnTick, 2) }, "run.burnTick");
            yield return S("L2143 궤도 구슬", t => new[] { Ev(RunEventType.OrbTick, ids: new[] { 2, 3 }) }, "run.orbHit", "run.orbHit");
            yield return S("L1861 우물 벽돌", t => new[] { Ev(RunEventType.WellFlush, 8, a: 2, amount: 10) }, "run.brickFlush");
            yield return S("L2157 폭풍 불구름(fire만)", t => new[] { Ev(RunEventType.GodStormStart, x: 0, z: 5, key: "fire", amount: 1.7), Ev(RunEventType.GodStormStart, key: "bolt") }, "god.storm");
            yield return S("L2163 폭풍 낙하", t => new[] { Ev(RunEventType.GodStormStrike, x: 0, z: 5, key: "bolt") }, "god.storm");
            yield return S("L2175 신 대폭발", t => new[] { Ev(RunEventType.GodBlast, x: 0, z: 5, key: "fire") }, "god.blast");
            yield return S("L2276 우다다 한 바퀴", t => new[] { Ev(RunEventType.ClimbStart, a: 100, b: 40, amount: 2.5, x: 10, z: 39), Ev(RunEventType.ClimbLap, a: 1, b: 75) }, "climb.lap");
            yield return S("L2282 우다다 등급", t => new[] { Ev(RunEventType.ClimbTierUp, a: 2, b: 30) }, "climb.tier");
            yield return S("L3165 우다다 단계(0은 연출 없음)", t => new[] { Ev(RunEventType.ClimbWarpStage, a: 0), Ev(RunEventType.ClimbWarpStage, a: 1) }, "climb.stage");
            yield return S("L2286 우다다 착지", t => new[] { Ev(RunEventType.ClimbEnd, a: 100, b: 2) }, "climb.land");
            yield return S("L2370 룰렛 확정", t => new[] { Ev(RunEventType.RouletteChosen, a: 0, b: 1, c: 3) }, "roulette.pick");
            yield return S("소리·UI만 있는 사건(이펙트 없음)", t => new[]
            {
                Ev(RunEventType.FloorEnter, a: 1), Ev(RunEventType.GaugeLevelUp, a: 2), Ev(RunEventType.Hitstop, amount: 0.03), Ev(RunEventType.GateMiss, 6, K(t, "sgate"), a: 2),
                Ev(RunEventType.RouletteFlip, a: 0), Ev(RunEventType.CardApplied, b: 1), Ev(RunEventType.Toast, key: "x"),
            });
        }

        static FakeFxWorld World() => new FakeFxWorld()
            .Obj(1, "tower", 0, 10).Obj(2, "enemy", 1, 12).Obj(3, "wall", -1, 14, rew: FieldReward.Soul).Obj(4, "coin", 2, 16).Obj(5, "gem", 2, 18)
            .Obj(6, "sgate", 0, 20).Obj(7, "cgate", 0, 22).Obj(8, "well", 3, 24);

        [Test]
        public void RunEventsMapToCanonGroups()
        {
            var t = FxTestTables.Load();
            var fails = new List<string>();
            foreach (var s in Sites())
            {
                var o = Route(t, World(), 0f, s.Events(t));
                if (!o.Groups.SequenceEqual(s.Groups))
                    fails.Add($"{s.Line}: 기대 [{string.Join(",", s.Groups)}] 실제 [{string.Join(",", o.Groups)}]");
                foreach (var g in o.Groups)
                    if (t.Effect.ByKey(g) == null) fails.Add($"{s.Line}: 04_effect에 없는 key {g}");
                foreach (var sp in o.Spawns)
                {
                    var row = t.Effect.ByKey(sp.Key);
                    if (row == null || string.IsNullOrEmpty(row.prefab)) fails.Add($"{s.Line}: 기본 종류 key {sp.Key}에 prefab 없음");
                }
            }
            Assert.IsEmpty(fails, string.Join("\n", fails));
        }

        [Test]
        public void ChanceRowsSkipWhenRollFails()
        {
            var t = FxTestTables.Load();
            var o = Route(t, World(), 0.99f, Ev(RunEventType.Hit, 1, K(t, "tower"), amount: 3), Ev(RunEventType.Hit, 3, K(t, "wall"), amount: 3));
            Assert.IsEmpty(o.Groups);
            Assert.AreEqual(2, o.Pops.Count, "피해 숫자는 확률과 무관");
        }

        [Test]
        public void SpawnValuesFollowCanonCallSites()
        {
            var t = FxTestTables.Load();
            // L2107~2108 치명
            var o = Route(t, World(), 0f, Ev(RunEventType.ProjImpact, 1, x: 0.5, z: 7, flag: true));
            Assert.AreEqual(2, o.Spawns.Count);
            Assert.AreEqual("fx.impact", o.Spawns[0].Key);
            Assert.IsTrue(o.Spawns[0].Crit);
            var sp = o.Spawns[1];
            Assert.AreEqual("fx.sparks", sp.Key);
            Assert.AreEqual(new Color32(0xff, 0x6a, 0x3d, 255), sp.Color);
            Assert.AreEqual(8f, sp.Count); Assert.AreEqual(3.4f, sp.Speed, 1e-5f); Assert.AreEqual(0.5f, sp.Size, 1e-5f);
            Assert.AreEqual(0.3f, sp.Life, 1e-5f); Assert.AreEqual(0f, sp.Grav);
            Assert.AreEqual(1f, sp.Pos.y, 1e-5f);

            // L1925 쥐 처치: debris 8 + sparks 12 + dust 4
            o = Route(t, World(), 0f, Ev(RunEventType.Break, 2, K(t, "enemy"), x: 1, z: 12, a: -1));
            CollectionAssert.AreEqual(new[] { "fx.debris", "fx.sparks", "fx.dust" }, o.Spawns.Select(s => s.Key).ToArray());
            CollectionAssert.AreEqual(new[] { 8f, 12f, 4f }, o.Spawns.Select(s => s.Count).ToArray());

            // L1935~1937 wall 파괴(생선): debris 14 영혼색 + debris 4 + dust 7 + boom 1.5 noRing + sparks 16 (표 행 run.wallRewardSoul)
            o = Route(t, World(), 0f, Ev(RunEventType.Break, 3, K(t, "wall"), x: -1, z: 14, a: (int)FieldReward.Soul));
            CollectionAssert.AreEqual(new[] { "fx.debris", "fx.debris", "fx.dust", "fx.boom", "fx.sparks" }, o.Spawns.Select(s => s.Key).ToArray());
            Assert.AreEqual(new Color32(0x5a, 0x54, 0x90, 255), o.Spawns[0].Color);
            Assert.AreEqual(14f, o.Spawns[0].Count);
            Assert.IsTrue(o.Spawns[3].NoRing);
            Assert.AreEqual(1.5f, o.Spawns[3].Radius, 1e-5f);
            Assert.AreEqual(new Color32(0x5f, 0xd6, 0xc9, 255), o.Spawns[4].Color);
            Assert.AreEqual(16f, o.Spawns[4].Count); Assert.AreEqual(0.9f, o.Spawns[4].Life, 1e-5f);
            Assert.AreEqual(2f, o.Spawns[4].Pos.y, 1e-5f);

            // L2046 계단 관문: 표 행 sparks #ffb05a 16·5·0.5·0.7·중력 2 @1.5 + 계단 비행 +n
            o = Route(t, World(), 0f, Ev(RunEventType.GateTake, 6, K(t, "sgate"), a: 3));
            Assert.AreEqual(new Color32(0xff, 0xb0, 0x5a, 255), o.Spawns[0].Color);
            Assert.AreEqual(2f, o.Spawns[0].Grav); Assert.AreEqual(1.5f, o.Spawns[0].Pos.y, 1e-5f);
            Assert.AreEqual(1, o.Flies.Count); Assert.AreEqual(FlyTarget.Stair, o.Flies[0].Target); Assert.AreEqual(3, o.Flies[0].Amount);

            // L2052 동료 관문: 아이콘 min(n,5)개 70ms 간격
            o = Route(t, World(), 0f, Ev(RunEventType.GateTake, 7, K(t, "cgate"), a: 7));
            CollectionAssert.AreEqual(new[] { 0, 70, 140, 210, 280 }, o.Flies.Select(f => f.DelayMs).ToArray());

            // L2175 신 대폭발(fire): fireball 3 + boom 4.4 + boom 흰 2.2 noRing + 플래시 0.35/60ms
            o = Route(t, World(), 0f, Ev(RunEventType.GodBlast, x: 0, z: 5, key: "fire"));
            CollectionAssert.AreEqual(new[] { "fx.fireball", "fx.fireball", "fx.fireball", "fx.boom", "fx.boom" }, o.Spawns.Select(s => s.Key).ToArray());
            Assert.AreEqual(4.4f, o.Spawns[3].Radius, 1e-5f);
            Assert.AreEqual(1, o.Flashes.Count); Assert.AreEqual(0.35f, o.Flashes[0].Peak, 1e-5f); Assert.AreEqual(60, o.Flashes[0].HoldMs);

            // 흔들림은 MV.Run Shake 이벤트 값 그대로(표 shake 열로 중복 없음)
            o = Route(t, World(), 0f, Ev(RunEventType.Break, 3, K(t, "wall"), a: (int)FieldReward.Gold), Ev(RunEventType.Shake, amount: 0.16, amount2: 0.14));
            Assert.AreEqual(1, o.Shakes.Count);
            Assert.AreEqual(0.16f, o.Shakes[0].x, 1e-5f); Assert.AreEqual(0.14f, o.Shakes[0].y, 1e-5f);
        }

        [Test]
        public void HitPopsDamageNumberUnlessQuiet()
        {
            var t = FxTestTables.Load();
            int enemy = K(t, "enemy");
            var o = Route(t, World(), 0f, Ev(RunEventType.Hit, 2, enemy, amount: 12345.6, flag: true), Ev(RunEventType.Hit, 2, enemy, amount: 0.2),
                Ev(RunEventType.Hit, 2, enemy, amount: 50, quiet: true));
            Assert.AreEqual(2, o.Pops.Count);
            Assert.AreEqual("12.3K", o.Pops[0].Text);
            Assert.AreEqual(new Color32(0xff, 0xff, 0xff, 255), o.Pops[1].Color);
            Assert.AreEqual(1.3f, o.Pops[0].Size, 1e-5f);
            Assert.AreEqual("1", o.Pops[1].Text);
        }

        [Test]
        public void FmtMatchesCanon()
        {
            Assert.AreEqual("999", MvFxRouter.Fmt(999.9));
            Assert.AreEqual("9,999", MvFxRouter.Fmt(9999));
            Assert.AreEqual("10.0K", MvFxRouter.Fmt(10000));
            Assert.AreEqual("1.5M", MvFxRouter.Fmt(1500000));
        }

        [Test]
        public void ClimbEventsUseClimbStartGeometry()
        {
            var t = FxTestTables.Load();
            var cfg = t.Climb.Config();
            var o = Route(t, World(), 0f, Ev(RunEventType.ClimbStart, a: 100, b: 40, amount: 2.5, x: 10, z: 39, amount2: 16.8), Ev(RunEventType.ClimbLap, a: 1, b: 75),
                Ev(RunEventType.ClimbEnd, a: 100, b: 2));
            Assert.IsTrue(o.WarpBegin); Assert.IsTrue(o.WarpEnd);
            // 정본 L2275: si = min(k−1, ceil(lap·LAP/pw) − 1), y = (si+1)·STEP_H, z = z0 + si·STEP_D
            int si = Math.Min(39, (int)Math.Ceiling(1 * cfg["lap"] / 2.5) - 1);
            var boom = o.Spawns[0];
            Assert.AreEqual("fx.boom", boom.Key);
            Assert.AreEqual((float)((si + 1) * cfg["stepH"] + 2), boom.Pos.y, 1e-4f);
            Assert.AreEqual((float)(10 + si * cfg["stepD"]), boom.Pos.z, 1e-4f);
            Assert.IsTrue(boom.NoRing);
            var land = o.Spawns[o.Spawns.Count - 1];
            Assert.AreEqual(new Color32(0xff, 0xe0, 0x8a, 255), land.Color);
            Assert.AreEqual(2.2f, land.Radius, 1e-5f);
            Assert.AreEqual(16.8f + 0.3f, land.Pos.y, 1e-4f);
            Assert.AreEqual(39.5f, land.Pos.z, 1e-4f);
        }

        [Test]
        public void ProjectileElementsTrackedForTrails()
        {
            var t = FxTestTables.Load();
            var r = new MvFxRouter(() => t, () => 0f);
            var o = new MvFxOutput();
            r.Route(Ev(RunEventType.Fire, 11, key: "fire"), World(), o);
            r.Route(Ev(RunEventType.Fire, 12), World(), o);
            Assert.AreEqual(1, r.ProjectileElements.Count);
            Assert.AreEqual("fire", r.ProjectileElements[11]);
            r.Route(Ev(RunEventType.ProjRemoved, 11), World(), o);
            Assert.AreEqual(0, r.ProjectileElements.Count);
        }
    }
}
