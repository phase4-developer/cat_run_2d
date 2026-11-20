using System;
using MV.Data;
using MV.Meta;
using MV.Run;
using MV.Save;
using MV.View;

namespace MV.Boot
{
    /// <summary>런 순번 → 저장 스냅숏·시드로 런 시뮬을 만든다. 로비 미리보기와 런 시작이 같은 배치를 쓴다.</summary>
    static class MvRunFactory
    {
        /// <summary>다음 런 순번 미리보기(<see cref="MvMeta.BeginRun"/>을 부르지 않는다). 마지막 정산 순번 + 1.</summary>
        public static long PeekRunSeq(MvTables tables) => Snapshot(tables, out _).LastSettledRunSeq + 1;

        public static RunSim Create(MvTables tables, WorldView world, long runSeq, out MvRunSnapshot snap, out ulong seed)
        {
            snap = Snapshot(tables, out var store);
            seed = MvRunSnapshot.SeedFor(runSeq);
            var builder = new RunLevelBuilder();
            var ctx = MvRunStart.CreateContext(snap, tables, seed);
            if (store != null) ctx.MetaModifiers = MV.Content.MvResearch.Modifiers(store, tables); // 연구 단계 → 런 메타 능력치(MV-META-RESEARCH)
            var sim = RunSim.Create(ctx, new MainRunRule(builder), new Pcg32Rng(seed), builder);
            // 투사체 판정 = 오브젝트 프리팹 Collider(Unity Physics). 월드에 판정 세계가 없으면 런을 시작하지 않는다.
            sim.HitQuery = world.Hits != null ? world.Hits : throw new InvalidOperationException("WorldView에 RunHitWorld가 없습니다(오브젝트 뷰 필요).");
            // 우물 게이지는 계단 경험치 오브가 도착하는 순간 받는다(MV-UI3-STAIRFX3, 사용자 2026-11-20). 화면 없는 시험은 108 지연 그대로.
            sim.WellGaugeOnArrival = true;
            return sim;
        }

        /// <summary>저장이 있으면 그 스냅숏, 없으면 기본값. <paramref name="store"/>는 쓸 수 있는 저장(없으면 null).</summary>
        static MvRunSnapshot Snapshot(MvTables tables, out MvSaveStore store)
        {
            store = MvSave.Current;
            if (store != null && store.IsDisposed) store = null;
            return store != null ? MvRunSnapshot.FromStore(store, tables.FloorCount())
                : MvRunSnapshot.Defaults(MvBootSave.CreateConfig(tables));
        }
    }
}
