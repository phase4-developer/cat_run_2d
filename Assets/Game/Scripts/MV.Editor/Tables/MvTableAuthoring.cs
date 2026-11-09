using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using jjevol;
using MV.Data;
using MV.Editor.Import;
using UnityEditor;
using UnityEngine;

namespace MV.Editor.Tables
{
    /// <summary>
    /// 표 반영 경로 등록(기획서 07 §7.1.1, D-84): Framework TableBuilder(표 제작 창)가 빌드할 때
    /// 게임 검증기 <see cref="MvTableValidator"/>를 부르고, 오류면 반영을 막는다. 암호화는 끈다(부팅도 useEncrypt false).
    /// </summary>
    [InitializeOnLoad]
    public static class MvTableAuthoring
    {
        /// <summary>검증 규칙이 바뀌면 올린다(같은 입력도 다시 검증하게).</summary>
        public const string ValidationVersion = "mv-data-1";

        public const string TsvAssetPath = "Assets/Game/GameData/Table/TSV";
        public const string OutputAssetPath = "Assets/Game/GameData/Table/Resources";

        static MvTableAuthoring() => Register();

        public static void Register()
        {
            TableAuthoringOptions.ValidateTables = ValidateBuildInputs;
            TableAuthoringOptions.ValidationVersion = ValidationVersion;
            TableAuthoringOptions.UseEncryption = false;
            TableAuthoringOptions.EncryptBytes = null;
        }

        /// <summary>
        /// TableBuilder 콜백. 빌드 입력에 없는 표는 TSV 폴더에서 읽어 함께 검사한다. 오류면 요약을 로그로 남기고 예외(빌드 거부).
        /// </summary>
        public static void ValidateBuildInputs(IReadOnlyList<TableBuildInput> inputs)
        {
            var report = Validate(inputs.Select(i => new KeyValuePair<string, string>(i.TableName, i.Text)));
            if (report.HasError)
            {
                Debug.LogError("[MvTableAuthoring] 표 반영 거부\n" + report.Summary());
                throw new InvalidDataException(report.Summary(10));
            }
            if (report.WarningCount > 0) Debug.LogWarning("[MvTableAuthoring] " + report.Summary());
        }

        /// <summary>
        /// 표 검증 + 리소스 열 확인(<see cref="MvResourceColumnChecker"/>, 07 §7.10·03 §3.8.6)을 한 보고로 합친다.
        /// 리소스 오류(그림은 있는데 아틀라스·대소문자·주소가 틀림)는 오류, 미추출(그림 없음, D-MVDATA-RES)·다른 계약 주소·경고는 경고로 넣는다.
        /// </summary>
        public static MvValidationReport Validate(IEnumerable<KeyValuePair<string, string>> tableTexts)
        {
            var texts = tableTexts.ToList();
            var dir = Path.GetFullPath(TsvAssetPath);
            var report = MvTableValidator.ValidateAuthoringTsv(texts, dir);
            if (report.HasError) return report; // 표 자체가 깨지면 리소스 확인은 같은 표로 할 수 없다.

            var fileSource = new MvTsvDirectorySource(dir);
            var source = new MvMemoryTableSource(fileSource);
            foreach (var pair in texts) source.Set(pair.Key, pair.Value);
            var tables = new MvTables();
            tables.LoadAll(source);
            var res = MvResourceColumnChecker.Check(tables, MvArtCatalog.FromProject(), MvArtManifest.ReadTempList(),
                name => source.TryGetText(name, out var text) ? FirstLine(text) : fileSource.ReadHeaders(name),
                MvResourceColumnChecker.ProjectArtFileNames());
            MergeResourceIssues(report, res);
            return report;
        }

        /// <summary>리소스 확인 결과를 표 검증 보고에 옮긴다. 오류만 반영을 막는다.</summary>
        public static void MergeResourceIssues(MvValidationReport report, MvResourceCheckReport res)
        {
            foreach (var i in res.Issues)
            {
                var message = $"리소스 `{i.Value}` @{i.Atlas}: {i.Message}";
                if (i.Kind == MvResIssueKind.Error) report.Error(i.Table, i.RowId, i.Column, message);
                else report.Warn(i.Table, i.RowId, i.Column, $"[{i.Kind}] {message}");
            }
        }

        static IReadOnlyList<string> FirstLine(string text)
        {
            if (string.IsNullOrEmpty(text)) return null;
            int end = text.IndexOfAny(new[] { '\r', '\n' });
            return (end < 0 ? text : text.Substring(0, end)).Split('\t');
        }

        [MenuItem("MV/Tables/Validate TSV")]
        static void ValidateTsvMenu()
        {
            var report = Validate(Array.Empty<KeyValuePair<string, string>>());
            if (report.HasError) Debug.LogError(report.Summary(200));
            else Debug.Log(report.Summary(200));
        }
    }

    /// <summary>
    /// 시트 → TSV 내보내기(07 §7.1.1). 시트 머리(JJEvol 표 규격 v1 §4)는 1줄 설명 → 2줄 형식 → 3줄 열 이름 → 데이터다.
    /// 열 이름 줄 위 2줄과 `#` 열을 빼고, 줄바꿈 LF·BOM 없음으로 쓴다. 출력은 TableBuilder로 반영한다(직접 Resources 복사 금지).
    /// </summary>
    public static class MvSheetTsvExporter
    {
        public const int HumanHeaderLines = 2;

        /// <summary>시트에서 복사한 TSV 원문을 표 TSV로 바꾼다.</summary>
        public static string Convert(string sheetText)
        {
            if (sheetText == null) throw new ArgumentNullException(nameof(sheetText));
            if (sheetText.Length > 0 && sheetText[0] == '﻿') sheetText = sheetText.Substring(1);
            var lines = sheetText.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n').ToList();
            while (lines.Count > 0 && lines[lines.Count - 1].Length == 0) lines.RemoveAt(lines.Count - 1);
            if (lines.Count <= HumanHeaderLines) throw new FormatException("시트에 열 이름 줄이 없습니다(설명·형식 2줄 다음이 열 이름)");

            var header = lines[HumanHeaderLines].Split('\t');
            var keep = Enumerable.Range(0, header.Length).Where(i => header[i].Length > 0 && !header[i].StartsWith("#")).ToArray();
            var outLines = new List<string>();
            for (int l = HumanHeaderLines; l < lines.Count; l++)
            {
                var cells = lines[l].Split('\t');
                if (l > HumanHeaderLines && cells.All(c => c.Length == 0)) continue; // 빈 줄
                outLines.Add(string.Join("\t", keep.Select(i => i < cells.Length ? cells[i] : string.Empty)));
            }
            return string.Join("\n", outLines) + "\n";
        }

        [MenuItem("MV/Tables/Export Sheet TSV…")]
        static void ExportMenu()
        {
            var src = EditorUtility.OpenFilePanel("시트에서 내려받은 TSV", "", "tsv,txt");
            if (string.IsNullOrEmpty(src)) return;
            var name = Path.GetFileNameWithoutExtension(src);
            var dst = EditorUtility.SaveFilePanel("표 TSV 저장", Path.GetFullPath(MvTableAuthoring.TsvAssetPath), name, "txt");
            if (string.IsNullOrEmpty(dst)) return;
            File.WriteAllText(dst, Convert(File.ReadAllText(src)), new System.Text.UTF8Encoding(false));
            AssetDatabase.Refresh();
            Debug.Log($"[MvSheetTsvExporter] {dst} — 표 제작 창(TableBuilder)으로 반영하세요.");
        }
    }
}
