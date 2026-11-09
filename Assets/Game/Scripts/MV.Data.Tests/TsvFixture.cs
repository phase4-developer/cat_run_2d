using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace MV.Data.Tests
{
    /// <summary>저장소 TSV 정본을 읽어 메모리에서만 고치는 도구(파일은 바꾸지 않는다).</summary>
    internal static class TsvFixture
    {
        public static string Dir => MvTableLoader.TsvDirectory;

        public static string Read(string table) => File.ReadAllText(Path.Combine(Dir, table + ".txt"), new UTF8Encoding(false));

        public static MvTsvDirectorySource Source() => new MvTsvDirectorySource(Dir);

        public static MvTables LoadTables(jjevol.ITableSource source = null)
        {
            var tables = new MvTables();
            tables.LoadAll(source ?? Source());
            return tables;
        }

        static (string[] header, List<string[]> rows) Parse(string text)
        {
            var lines = text.Replace("\r\n", "\n").Split('\n').Where(l => l.Length > 0).ToList();
            return (lines[0].Split('\t'), lines.Skip(1).Select(l => l.Split('\t')).ToList());
        }

        static string Join(string[] header, IEnumerable<string[]> rows)
        {
            var sb = new StringBuilder();
            sb.Append(string.Join("\t", header)).Append('\n');
            foreach (var r in rows) sb.Append(string.Join("\t", r)).Append('\n');
            return sb.ToString();
        }

        /// <summary>다른 열 순서로 적힌 행(기획서 예시)을 표 열 순서에 맞춰 붙인다. 표에 없는 열은 버린다(`#` 열 등).</summary>
        public static string Append(string text, string rowHeader, string row)
        {
            var (header, rows) = Parse(text);
            var names = rowHeader.Split('\t');
            var values = row.Split('\t');
            var map = new Dictionary<string, string>(StringComparer.Ordinal);
            for (int i = 0; i < names.Length; i++) map[names[i]] = i < values.Length ? values[i] : string.Empty;
            foreach (var n in names)
                if (!header.Contains(n) && !n.StartsWith("#")) throw new ArgumentException($"표에 열 {n}가 없습니다");
            rows.Add(header.Select(h => map.TryGetValue(h, out var v) ? v : string.Empty).ToArray());
            return Join(header, rows);
        }

        /// <summary>id 행의 열 값을 바꾼다.</summary>
        public static string Set(string text, string id, string column, string value, string idColumn = "id")
        {
            var (header, rows) = Parse(text);
            int c = Array.IndexOf(header, column);
            int k = Array.IndexOf(header, idColumn);
            if (c < 0) throw new ArgumentException($"열 {column}가 없습니다");
            int index = rows.FindIndex(r => r[k] == id);
            if (index < 0) throw new ArgumentException($"id {id}가 없습니다");
            var row = rows[index];
            if (row.Length <= c) Array.Resize(ref row, header.Length);
            row[c] = value;
            rows[index] = row;
            return Join(header, rows);
        }

        /// <summary>key 열로 찾는 Set.</summary>
        public static string SetByKey(string text, string key, string column, string value) => Set(text, key, column, value, "key");

        public static string Remove(string text, string id)
        {
            var (header, rows) = Parse(text);
            int k = Array.IndexOf(header, "id");
            return Join(header, rows.Where(r => r[k] != id));
        }

        /// <summary>고친 표만 넘기고 나머지는 TSV 폴더에서 읽어 작성 검증을 돌린다.</summary>
        public static MvValidationReport Validate(params (string table, string text)[] changes) =>
            MvTableValidator.ValidateAuthoringTsv(changes.Select(c => new KeyValuePair<string, string>(c.table, c.text)), Dir);
    }
}
