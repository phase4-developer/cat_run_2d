using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using MV.Data;
using MV.Run;
using NUnit.Framework;

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

        /// <summary>
        /// 표 하나의 칸 몇 개(key 행·열)만 바꾼 새 표 묶음(메모리, 저장소 파일은 그대로). 표만 바꿔 이펙트가 따라 바뀌는지(D-87) 보는 데 쓴다.
        /// </summary>
        public static MvTables WithCells(string table, params (string key, string column, string value)[] edits)
        {
            string text = File.ReadAllText(Path.Combine(MvTableLoader.TsvDirectory, table + ".txt"), new UTF8Encoding(false));
            string[] lines = text.Replace("\r\n", "\n").Split('\n');
            string[] header = lines[0].Split('\t');
            int keyCol = Array.IndexOf(header, "key");
            foreach (var (key, column, value) in edits)
            {
                int col = Array.IndexOf(header, column);
                Assert.That(keyCol >= 0 && col >= 0, Is.True, table + ": key·" + column + " 열");
                bool found = false;
                for (int i = 1; i < lines.Length; i++)
                {
                    string[] cells = lines[i].Split('\t');
                    if (cells.Length <= Math.Max(keyCol, col) || cells[keyCol] != key) continue;
                    cells[col] = value;
                    lines[i] = string.Join("\t", cells);
                    found = true;
                }
                Assert.That(found, Is.True, table + " " + key);
            }
            var t = new MvTables();
            t.LoadAll(new MvMemoryTableSource(new MvTsvDirectorySource(MvTableLoader.TsvDirectory)).Set(table, string.Join("\n", lines)));
            return t;
        }
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
