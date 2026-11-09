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

        /// <summary>정본 호출 인자로 만든 기대 재생(옛 하드코딩 값 그대로). NaN = 종류 기본값.</summary>
        static FxSpawn Sp(string key, double x, double y, double z, string hex = null, float n = float.NaN, float spd = float.NaN, float size = float.NaN,
            float life = float.NaN, float grav = float.NaN, float r = float.NaN, bool noRing = false, bool crit = false)
        {
            var s = FxSpawn.Of(key, new Vector3((float)x, (float)y, (float)z));
            if (hex != null) s = s.WithColor(MvFxRouter.C(hex));
            s.Count = n; s.Speed = spd; s.Size = size; s.Life = life; s.Grav = grav; s.Radius = r; s.NoRing = noRing; s.Crit = crit;
            return s;
        }

        static void AssertSpawns(MvFxOutput o, string what, params FxSpawn[] expected) =>
            CollectionAssert.AreEqual(expected.Select(s => s.ToString()).ToArray(), o.Spawns.Select(s => s.ToString()).ToArray(), what);

        static string TierHex(MvTables t, int tier) => t.CardTier.Datas.First(r => r.tierIndex == tier && !string.IsNullOrEmpty(r.color)).color;

        [Test]
        public void PartGroups_ReproduceCanonCallArgs()
        {
            var t = FxTestTables.Load();
            AssertSpawns(Route(t, World(), 0f, Ev(RunEventType.ProjImpact, 1, x: 0.5, z: 7)), "L2107 projHit 보통",
                Sp("fx.impact", 0.5, 1, 7), Sp("fx.sparks", 0.5, 1, 7, "#ffffff", 4, 3.4f, 0.3f, 0.3f, 0));
            AssertSpawns(Route(t, World(), 0f, Ev(RunEventType.Break, 4, K(t, "coin"), x: 2, z: 16, a: -1)), "L1963 coinBreak coin",
                Sp("fx.dust", 2, 0.6, 16, n: 4), Sp("fx.sparks", 2, 1, 16, "#ffd65a", 18, 6, 0.4f, 0.8f));
            AssertSpawns(Route(t, World(), 0f, Ev(RunEventType.Break, 5, K(t, "gem"), x: 2, z: 18, a: -1)), "L1964 coinBreak gem",
                Sp("fx.dust", 2, 0.6, 18, n: 4), Sp("fx.sparks", 2, 1.2, 18, "#7feaff", 20, 6, 0.45f, 0.9f));
            AssertSpawns(Route(t, World(), 0f, Ev(RunEventType.Break, 3, K(t, "wall"), x: -1, z: 14, a: (int)FieldReward.Gold)), "L1935 wallBreak 츄르",
                Sp("fx.debris", -1, 1.3, 14, "#93673f", 14, size: 0.32f), Sp("fx.debris", -1, 1.6, 14, "#5b606c", 4, size: 0.22f),
                Sp("fx.dust", -1, 1, 14, n: 7), Sp("fx.boom", -1, 1.2, 14, "#ffd6a0", r: 1.5f, noRing: true));
            AssertSpawns(Route(t, World(), 0f, Ev(RunEventType.Break, 2, K(t, "enemy"), x: 1, z: 12, a: -1)), "L1925 enemyKill",
                Sp("fx.debris", 1, 0.8, 12, "#5a8a3a", 8, size: 0.2f), Sp("fx.sparks", 1, 1, 12, "#ffd65a", 12, 5, 0.4f, 0.6f), Sp("fx.dust", 1, 0.8, 12, n: 4));
            AssertSpawns(Route(t, World(), 0f, Ev(RunEventType.GodStormStrike, x: 0, z: 5, key: "bolt")), "L2163 storm bolt",
                Sp("fx.paw", 0, 0.1, 5), Sp("fx.dust", 0, 0.5, 5, n: 5), Sp("fx.boom", 0, 0.6, 5, "#9fe0ff", r: 1.5f));
            AssertSpawns(Route(t, World(), 0f, Ev(RunEventType.GodStormStrike, x: 0, z: 5, key: "fire")), "L2163 storm fire",
                Sp("fx.fireball", 0, 0.6, 5, size: 1.4f), Sp("fx.boom", 0, 0.6, 5, "#ff7a2a", r: 1.5f));
            AssertSpawns(Route(t, World(), 0f, Ev(RunEventType.GodBlast, x: 0, z: 5, key: "bolt")), "L2175 blast bolt",
                Sp("fx.hiss", 0, 0.6, 5), Sp("fx.boom", 0, 1, 5, "#9fe0ff", r: 4.4f), Sp("fx.boom", 0, 1.6, 5, "#ffffff", r: 2.2f, noRing: true));
            AssertSpawns(Route(t, World(), 0f, Ev(RunEventType.GodBlast, x: 0, z: 5, key: "fire")), "L2175 blast fire(rnd 0 → −1)",
                Sp("fx.fireball", -1, 0.6, 4, size: 2.4f), Sp("fx.fireball", -1, 0.6, 4, size: 2.4f), Sp("fx.fireball", -1, 0.6, 4, size: 2.4f),
                Sp("fx.boom", 0, 1, 5, "#ff5a1f", r: 4.4f), Sp("fx.boom", 0, 1.6, 5, "#ffffff", r: 2.2f, noRing: true));

            var flush = Route(t, World(), 0f, Ev(RunEventType.WellFlush, 8, a: 3, amount: 10));
            AssertSpawns(flush, "L1861 brickFlush 벽돌 3",
                Sp("fx.brickChunk", 2.7, 1.2, 24), Sp("fx.brickChunk", 2.7, 1.2, 24), Sp("fx.brickChunk", 2.7, 1.2, 24),
                Sp("fx.sparks", 3, 1.4, 23.2, "#ffb04a", 4, 3, 0.3f, 0.35f));
            CollectionAssert.AreEqual(new[] { 0, 1, 2 }, flush.Spawns.Take(3).Select(s => s.Variant).ToArray());
            CollectionAssert.AreEqual(new[] { 1f, -1f, 1f }, flush.Spawns.Take(3).Select(s => s.Dir).ToArray());
            Assert.AreEqual(3, flush.Flies.Count); Assert.AreEqual(10, flush.Flies[2].Amount);

            var cfg = t.Climb.Config();
            var start = Ev(RunEventType.ClimbStart, a: 100, b: 40, amount: 2.5, x: 10, z: 39);
            int si = Math.Max(0, Math.Min(39, (int)Math.Floor(30 / 2.5)));
            double y = (si + 1) * cfg["stepH"], z = 10 + si * cfg["stepD"];
            AssertSpawns(Route(t, World(), 0f, start, Ev(RunEventType.ClimbTierUp, a: 2, b: 30)), "L2282 climb.tier",
                Sp("fx.boom", 0, y + 0.6, z, TierHex(t, 2), r: 2.2f), Sp("fx.sparks", 0, y + 1, z, TierHex(t, 2), 16, 5, 0.5f, 0.9f));
            si = Math.Min(39, (int)Math.Ceiling(1 * cfg["lap"] / 2.5) - 1);
            y = (si + 1) * cfg["stepH"]; z = 10 + si * cfg["stepD"];
            string lapHex = TierHex(t, (int)Math.Round(cfg["lapTier"]));
            AssertSpawns(Route(t, World(), 0f, start, Ev(RunEventType.ClimbLap, a: 1, b: 75)), "L2276 climb.lap",
                Sp("fx.boom", 0, y + 2, z, lapHex, r: 3, noRing: true), Sp("fx.sparks", 0, y + 3, z, lapHex, 24, 7, 0.55f, 1.1f));
        }

        [Test]
        public void PartGroups_FollowEffectTable()
        {
            var t = FxTestTables.WithCells(EffectData.Table,
                ("run.enemyKill", "children", "run.enemyKill.dust/run.enemyKill.sparks/run.enemyKill.dust"),
                ("run.enemyKill.sparks", "count", "30"),
                ("run.projHit.impact", "when", "crit"),
                ("run.brickFlush.sparks", "dz", "0.5"),
                ("god.blast.fireball", "jx", "0"));
            var kill = Route(t, World(), 0f, Ev(RunEventType.Break, 2, K(t, "enemy"), x: 1, z: 12, a: -1));
            CollectionAssert.AreEqual(new[] { "fx.dust", "fx.sparks", "fx.dust" }, kill.Spawns.Select(s => s.Key).ToArray(), "children 순서·반복");
            Assert.AreEqual(30f, kill.Spawns[1].Count, "part 인자");
            CollectionAssert.AreEqual(new[] { "fx.sparks" }, Route(t, World(), 0f, Ev(RunEventType.ProjImpact, 1, z: 5)).Spawns.Select(s => s.Key).ToArray(), "when crit");
            CollectionAssert.AreEqual(new[] { "fx.impact", "fx.sparks" }, Route(t, World(), 0f, Ev(RunEventType.ProjImpact, 1, z: 5, flag: true)).Spawns.Select(s => s.Key).ToArray());
            var flush = Route(t, World(), 0f, Ev(RunEventType.WellFlush, 8, a: 1));
            Assert.AreEqual(24.5f, flush.Spawns[1].Pos.z, 1e-4f, "dz");
            var blast = Route(t, World(), 0f, Ev(RunEventType.GodBlast, x: 0, z: 5, key: "fire"));
            Assert.AreEqual(0f, blast.Spawns[0].Pos.x, 1e-5f, "jx 0이면 x 흔들기 없음");
            Assert.AreEqual(4f, blast.Spawns[0].Pos.z, 1e-5f, "jz는 그대로");
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
