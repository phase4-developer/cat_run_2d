using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using jjevol;

namespace MV.Data
{
    /// <summary>
    /// 폴더의 `NNN_name.txt`를 평문 TSV로 읽는 원본. 시험(TSV 원본 대조)과 에디터 개발 대체(표 반영 전),
    /// 개발 덮어쓰기(`TableOverride`, 07 §7.1.9)가 쓴다. 출시 빌드의 정본 경로는 PackagedTableSource다.
    /// </summary>
    public sealed class MvTsvDirectorySource : ITableSource
    {
        readonly string _directory;
        readonly ITableSource _fallback;

        /// <param name="fallback">파일이 없을 때 읽을 원본(없으면 예외).</param>
        public MvTsvDirectorySource(string directory, ITableSource fallback = null)
        {
            _directory = directory ?? throw new ArgumentNullException(nameof(directory));
            _fallback = fallback;
        }

        public string Directory => _directory;
        public bool RequiresRemoteUpdate => false;

        string PathOf(string tableName) => Path.Combine(_directory, tableName + ".txt");

        public bool HasTable(string tableName) => File.Exists(PathOf(tableName)) || (_fallback?.HasTable(tableName) ?? false);

        public List<TSVRow> Load(string tableName)
        {
            var path = PathOf(tableName);
            if (File.Exists(path)) return TSVRow.FromTsv(File.ReadAllText(path, new UTF8Encoding(false)));
            if (_fallback != null) return _fallback.Load(tableName);
            throw new FileNotFoundException($"[MvTsvDirectorySource] {tableName} not found", path);
        }

        public Task<List<TSVRow>> LoadAsync(string tableName) => Task.FromResult(Load(tableName));

        /// <summary>파일의 첫 줄(열 이름). 중복 열 검사용. 없으면 null.</summary>
        public IReadOnlyList<string> ReadHeaders(string tableName)
        {
            var path = PathOf(tableName);
            if (!File.Exists(path)) return null;
            using var reader = new StreamReader(path, new UTF8Encoding(false));
            var first = reader.ReadLine();
            return first == null ? null : first.Split('\t');
        }
    }

    /// <summary>메모리 TSV 원본. 작성 검증(TableBuilder 입력)과 시험의 일부러 깬 표가 쓴다.</summary>
    public sealed class MvMemoryTableSource : ITableSource
    {
        readonly Dictionary<string, string> _texts = new Dictionary<string, string>(StringComparer.Ordinal);
        readonly ITableSource _fallback;

        public MvMemoryTableSource(ITableSource fallback = null) => _fallback = fallback;

        public bool RequiresRemoteUpdate => false;

        /// <summary>표 원문을 넣는다(같은 이름이면 교체).</summary>
        public MvMemoryTableSource Set(string tableName, string tsvText)
        {
            _texts[tableName] = tsvText ?? string.Empty;
            return this;
        }

        public bool TryGetText(string tableName, out string text) => _texts.TryGetValue(tableName, out text);

        public bool HasTable(string tableName) => _texts.ContainsKey(tableName) || (_fallback?.HasTable(tableName) ?? false);

        public List<TSVRow> Load(string tableName)
        {
            if (_texts.TryGetValue(tableName, out var text)) return TSVRow.FromTsv(text);
            if (_fallback != null) return _fallback.Load(tableName);
            throw new FileNotFoundException($"[MvMemoryTableSource] {tableName} not found");
        }

        public Task<List<TSVRow>> LoadAsync(string tableName) => Task.FromResult(Load(tableName));
    }
}
