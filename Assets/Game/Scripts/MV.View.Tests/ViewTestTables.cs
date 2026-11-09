using System;
using System.IO;
using System.Text;
using MV.Data;
using NUnit.Framework;

namespace MV.View.Tests
{
    /// <summary>시험용 표(저장소 TSV를 직접 읽는다. 사용자 저장·PlayerPrefs 안 씀).</summary>
    internal static class ViewTestTables
    {
        static MvTables _cached;

        public static MvTables Load()
        {
            if (_cached != null) return _cached;
            var t = new MvTables();
            t.LoadAll(new MvTsvDirectorySource(MvTableLoader.TsvDirectory));
            t.BuildIndexes();
            _cached = t;
            return t;
        }

        public static ViewConfig Config() => ViewConfig.From(Load().RunConfig);

        public static ThemeData Theme(string key) => Load().Theme.ByKey(key);

        /// <summary>
        /// 표 하나의 key 행 한 칸만 바꾼 새 표 묶음(나머지는 저장소 TSV). 표만 바꿔 화면이 따라 바뀌는지(D-87) 보는 데 쓴다. 캐시하지 않는다.
        /// </summary>
        public static MvTables WithCell(string table, string key, string column, string value)
        {
            string text = File.ReadAllText(Path.Combine(MvTableLoader.TsvDirectory, table + ".txt"), new UTF8Encoding(false));
            string[] lines = text.Replace("\r\n", "\n").Split('\n');
            string[] header = lines[0].Split('\t');
            int keyCol = Array.IndexOf(header, "key"), col = Array.IndexOf(header, column);
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
            var t = new MvTables();
            t.LoadAll(new MvMemoryTableSource(new MvTsvDirectorySource(MvTableLoader.TsvDirectory)).Set(table, string.Join("\n", lines)));
            return t;
        }
    }
}
