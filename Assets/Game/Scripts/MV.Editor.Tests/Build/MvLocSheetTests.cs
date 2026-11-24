using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using MV.Editor.Build;
using NUnit.Framework;

namespace MV.Editor.Tests.Build
{
    /// <summary>
    /// 언어팩 시트 사본 검사(MV-LOC). 사본은 Framework 「Localization Sheet Import」 창이 시트에서 받아 쓴다(D-LOC-FWUNIFY).
    /// 자산은 만들지 않고 사본 TSV 글자만 읽는다.
    /// </summary>
    public class MvLocSheetTests
    {
        static readonly string[] Locales = { "ko", "en", "ja" };

        sealed class Sheet
        {
            public string Collection;
            public string[] Locales;
            public Dictionary<string, string[]> Rows = new Dictionary<string, string[]>(StringComparer.Ordinal);
        }

        static List<Sheet> ReadSheets()
        {
            var dir = MvDataToolSettings.LocalizationSheetDir;
            Assert.That(Directory.Exists(dir), Is.True, "사본 폴더 없음: " + dir);
            var result = new List<Sheet>();
            foreach (var path in Directory.GetFiles(dir, "*.tsv").OrderBy(p => p, StringComparer.Ordinal))
            {
                var lines = File.ReadAllText(path, Encoding.UTF8).Replace("\r\n", "\n").TrimStart('﻿').Split('\n');
                var head = lines[0].Split('\t');
                var sheet = new Sheet { Collection = Path.GetFileNameWithoutExtension(path), Locales = head.Skip(1).Select(h => h.Trim()).ToArray() };
                Assert.That(head[0], Is.EqualTo("Key"), sheet.Collection + ": 첫 열은 Key");
                for (int i = 1; i < lines.Length; i++)
                {
                    if (lines[i].Length == 0) continue;
                    var cells = lines[i].Split('\t');
                    var values = new string[sheet.Locales.Length];
                    for (int c = 0; c < values.Length; c++) values[c] = c + 1 < cells.Length ? cells[c + 1] : "";
                    Assert.That(sheet.Rows.ContainsKey(cells[0]), Is.False, $"{sheet.Collection}: 키 중복 {cells[0]}");
                    sheet.Rows[cells[0]] = values;
                }
                result.Add(sheet);
            }
            return result;
        }

        /// <summary>모든 탭이 같은 로케일(ko·en·ja), Framework가 찾는 appInfo/LanguageDisplayName이 있다.</summary>
        [Test]
        public void RepoSheets_AreConsistent()
        {
            var sheets = ReadSheets();
            foreach (var s in sheets) Assert.That(s.Locales, Is.EqualTo(Locales), s.Collection + " 로케일 열");
            Assert.That(sheets.Select(s => s.Collection), Is.SupersetOf(new[] { "ui", "msg", "toastMessage", "name", "description", "appInfo" }));
            var appInfo = sheets.Single(s => s.Collection == "appInfo");
            Assert.That(appInfo.Rows.TryGetValue("LanguageDisplayName", out var v) && v.All(x => x.Length > 0), Is.True);
        }

        /// <summary>코드가 글자 그대로 부르는 키(MvUiText.Ui·Toast·Msg)는 시트에 있고 세 언어가 다 채워져 있다(없으면 화면에 #키).</summary>
        [Test]
        public void CodeKeys_ExistInSheets_WithAllLocales()
        {
            var rows = ReadSheets().Where(s => s.Collection == "ui" || s.Collection == "toastMessage" || s.Collection == "msg")
                .SelectMany(s => s.Rows).ToDictionary(r => r.Key, r => r.Value);
            var call = new Regex("MvUiText\\.(?:Ui|Toast|Msg)\\(\\s*\"((?:ui|toastMessage|msg)\\.[A-Za-z0-9_.]+)\"");
            var missing = new List<string>();
            int used = 0;
            foreach (var file in Directory.GetFiles("Assets/Game/Scripts", "*.cs", SearchOption.AllDirectories))
            {
                foreach (Match m in call.Matches(File.ReadAllText(file)))
                {
                    used++;
                    string key = m.Groups[1].Value;
                    if (!rows.TryGetValue(key, out var v) || v.Any(string.IsNullOrEmpty)) missing.Add(key + " @ " + file);
                }
            }
            Assert.That(used, Is.GreaterThan(0), "검색이 키를 하나도 못 찾으면 시험이 비어 있다");
            Assert.That(missing, Is.Empty, string.Join("\n", missing));
        }
    }
}
