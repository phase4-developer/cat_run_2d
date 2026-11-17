using System;
using System.Collections.Generic;
using MV.Data;

namespace MV.Run.Tests
{
    /// <summary>
    /// 규칙 시험용 RunSim 생성: <see cref="RunSim.Create(RunSetup, MvTables, IRng, IRunLevelBuilder)"/>에 규칙 시험용 판정 질의(<see cref="BoxHitQuery"/>)를 붙인다.
    /// 실제 판정(프리팹 Collider + Unity Physics)은 MV.View.Tests의 RunHitWorld 시험이 본다.
    /// </summary>
    internal static class TestSims
    {
        public static RunSim Create(RunSetup setup, MvTables tables, IRng rng, IRunLevelBuilder builder) =>
            Attach(RunSim.Create(setup, tables, rng, builder));

        public static RunSim Create(RunContext ctx, IRunRule rule, IRng rng, IRunLevelBuilder builder) =>
            Attach(RunSim.Create(ctx, rule, rng, builder));

        public static RunSim Create(RunSetup setup, MvTables tables, IRng rng, IRunLevelBuilder builder, IRunRule rule) =>
            Attach(RunSim.Create(setup, tables, rng, builder, rule));

        static RunSim Attach(RunSim sim)
        {
            sim.HitQuery = BoxHitQuery.Instance;
            return sim;
        }
    }

    /// <summary>
    /// 규칙 시험 전용 판정 대역: 대상 표 hw·hd 상자(Physics 없음). 피해·관통·중복·보상 규칙을 기하와 떼어 시험하려는 것이다.
    /// 게임은 이것을 쓰지 않는다(판정 형상 정본 = 프리팹 Collider). 이 대역의 결과로 Physics 전환 PASS를 내지 않는다.
    /// </summary>
    internal sealed class BoxHitQuery : IProjectileHitQuery
    {
        public static readonly BoxHitQuery Instance = new BoxHitQuery();

        static readonly Comparer<ProjectileHit> Order = Comparer<ProjectileHit>.Create((a, b) =>
        {
            int c = a.Z.CompareTo(b.Z);
            return c != 0 ? c : a.Obj.Id.CompareTo(b.Obj.Id);
        });

        public void Prepare(RunState s) { }

        public void Cast(RunState s, double x, double z0, double z1, List<ProjectileHit> hits)
        {
            int start = hits.Count;
            foreach (var o in s.Objs)
            {
                if (o == null || o.Dead || Math.Abs(x - o.X) > o.Hw) continue;
                double f = o.Z - o.Hd;
                if (f <= z1 && o.Z + o.Hd >= z0) hits.Add(new ProjectileHit(o, f));
            }
            hits.Sort(start, hits.Count - start, Order);
        }
    }
}
