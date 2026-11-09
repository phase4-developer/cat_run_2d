using System.Collections.Generic;
using MV.Data;
using MV.Run;

namespace MV.Fx.Tests
{
    /// <summary>저장소 TSV 정본으로 표를 읽는다(파일은 바꾸지 않는다).</summary>
    internal static class FxTestTables
    {
        static MvTables s_cached;

        public static MvTables Load()
        {
            if (s_cached != null) return s_cached;
            var t = new MvTables();
            t.LoadAll(new MvTsvDirectorySource(MvTableLoader.TsvDirectory));
            t.BuildIndexes();
            s_cached = t;
            return t;
        }

        public static int Kind(MvTables t, string key) => t.ObjectKind.ByKey(key).id;
    }

    /// <summary>오브젝트 몇 개만 둔 가짜 런 상태.</summary>
    internal sealed class FakeFxWorld : IMvFxWorld
    {
        public readonly Dictionary<int, FxObj> Objs = new Dictionary<int, FxObj>();
        public double PlayerX { get; set; }
        public double PlayerZ { get; set; }
        public double WorldY { get; set; }
        public bool HitStopActive { get; set; }
        public int TimeScale { get; set; } = 1;

        public bool TryObj(int id, out FxObj o) => Objs.TryGetValue(id, out o);

        public FakeFxWorld Obj(int id, string kind, double x, double z, double hd = 0.5, FieldReward rew = FieldReward.Gold)
        {
            Objs[id] = new FxObj { KindKey = kind, X = x, Z = z, Hd = hd, Rew = rew, IsField = kind == "wall" };
            return this;
        }
    }
}
