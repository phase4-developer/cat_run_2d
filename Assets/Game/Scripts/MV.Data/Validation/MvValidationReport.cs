using System;
using System.Collections.Generic;
using System.Text;
using jjevol;

namespace MV.Data
{
    public enum MvIssueLevel { Error, Warning }

    /// <summary>검증 결과 1건. 문구 형식 `[표 파일] id=값 열=이름: 내용`(07 §7.13).</summary>
    public sealed class MvIssue
    {
        public MvIssueLevel Level { get; }
        public string Table { get; }
        public string Id { get; }
        public string Column { get; }
        public string Message { get; }

        public MvIssue(MvIssueLevel level, string table, string id, string column, string message)
        {
            Level = level;
            Table = table ?? string.Empty;
            Id = id ?? string.Empty;
            Column = column ?? string.Empty;
            Message = message ?? string.Empty;
        }

        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.Append('[').Append(Table).Append(']');
            if (Id.Length > 0) sb.Append(" id=").Append(Id);
            if (Column.Length > 0) sb.Append(" 열=").Append(Column);
            sb.Append(": ").Append(Message);
            return sb.ToString();
        }
    }

    public sealed class MvValidationReport
    {
        readonly List<MvIssue> _issues = new List<MvIssue>();

        public IReadOnlyList<MvIssue> Issues => _issues;
        public bool HasError { get; private set; }
        public int ErrorCount { get; private set; }
        public int WarningCount { get; private set; }

        public void Error(string table, object id, string column, string message)
        {
            _issues.Add(new MvIssue(MvIssueLevel.Error, table, id?.ToString(), column, message));
            HasError = true;
            ErrorCount++;
        }

        public void Warn(string table, object id, string column, string message)
        {
            _issues.Add(new MvIssue(MvIssueLevel.Warning, table, id?.ToString(), column, message));
            WarningCount++;
        }

        public IEnumerable<MvIssue> Errors
        {
            get
            {
                foreach (var i in _issues)
                    if (i.Level == MvIssueLevel.Error) yield return i;
            }
        }

        /// <summary>표 이름·내용 일부로 찾는다(시험용).</summary>
        public bool Contains(MvIssueLevel level, string table, string fragment)
        {
            foreach (var i in _issues)
                if (i.Level == level && i.Table == table && (fragment == null || i.ToString().Contains(fragment)))
                    return true;
            return false;
        }

        public string Summary(int maxLines = 40)
        {
            var sb = new StringBuilder();
            sb.Append("표 검증: 오류 ").Append(ErrorCount).Append(", 경고 ").Append(WarningCount);
            int n = 0;
            foreach (var level in new[] { MvIssueLevel.Error, MvIssueLevel.Warning })
                foreach (var i in _issues)
                {
                    if (i.Level != level) continue;
                    if (n++ >= maxLines) { sb.Append("\n…"); return sb.ToString(); }
                    sb.Append('\n').Append(level == MvIssueLevel.Error ? "오류 " : "경고 ").Append(i);
                }
            return sb.ToString();
        }

        public override string ToString() => Summary();
    }

    public sealed class MvTableValidationException : Exception
    {
        public MvValidationReport Report { get; }
        public MvTableValidationException(MvValidationReport report) : base(report?.Summary()) => Report = report;
    }

    /// <summary>리소스 카탈로그(03 §3.8.6: 아틀라스별 sprite_catalog.tsv, Addressables 주소 목록). MV-TOOL-ART·MV-ART-ATLAS가 만든다.</summary>
    public interface IMvResourceCatalog
    {
        /// <summary>atlas가 <see cref="MvAtlas.Address"/>면 Addressables 주소, <see cref="MvAtlas.AtlasName"/>면 아틀라스 이름 자체를 확인한다.</summary>
        bool Has(string atlas, string name);
        /// <summary>`@theme` 열을 확인할 모든 테마 아틀라스.</summary>
        IReadOnlyList<string> ThemeAtlases { get; }
    }

    public sealed class MvValidationOptions
    {
        /// <summary>표의 원문 첫 줄(열 이름). 있으면 중복 열·필수 열을 그것으로 본다.</summary>
        public Func<string, IReadOnlyList<string>> Headers { get; set; }
        /// <summary>없으면 리소스 열 검사를 건너뛴다(카탈로그는 MV-TOOL-ART 뒤).</summary>
        public IMvResourceCatalog Resources { get; set; }
        /// <summary>없거나 준비 전이면 `name` 문구 검사를 건너뛴다.</summary>
        public ITableLocalization Localization { get; set; }
    }
}
