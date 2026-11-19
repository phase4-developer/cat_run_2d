using System.Collections.Generic;
using System.Linq;
using MV.Editor.Build;
using NUnit.Framework;

namespace MV.Editor.Tests.Build
{
    /// <summary>언어팩 시트 사본 읽기(MV-LOC). 자산은 만들지 않는다(setup 잡 몫).</summary>
    public class MvLocBuildTests
    {
        [Test]
        public void ParseSheet_ReadsLocalesAndRows_KeepsPlaceholders()
        {
            var errors = new List<string>();
            var s = MvLocBuild.ParseSheet("ui", "﻿Key\tko\ten\tja\r\nui.aup.lv\tLv.{0}\tLv.{0}\tLv.{0}\r\nui.a\t가\tA\r\n", errors);
            Assert.That(errors, Is.Empty);
            Assert.That(s.Locales, Is.EqualTo(new[] { "ko", "en", "ja" }));
            Assert.That(s.Rows.Select(r => r.Key), Is.EqualTo(new[] { "ui.aup.lv", "ui.a" }));
            Assert.That(s.Rows[0].Value[1], Is.EqualTo("Lv.{0}"));
            Assert.That(s.Rows[1].Value[2], Is.EqualTo(""), "짧은 행의 빠진 칸은 빈 번역");
        }

        [TestCase("ko\ten\nx\ta\tb\n", TestName = "ParseSheet_RejectsMissingKeyHeader")]
        [TestCase("Key\tko\tko\nx\ta\tb\n", TestName = "ParseSheet_RejectsDuplicateLocale")]
        [TestCase("Key\tko\nx\ta\nx\tb\n", TestName = "ParseSheet_RejectsDuplicateKey")]
        [TestCase("Key\tko\n\ta\n", TestName = "ParseSheet_RejectsEmptyKey")]
        [TestCase("Key\tko\nx\ta\tb\n", TestName = "ParseSheet_RejectsExtraCells")]
        public void ParseSheet_Errors(string text)
        {
            var errors = new List<string>();
            MvLocBuild.ParseSheet("t", text, errors);
            Assert.That(errors, Is.Not.Empty);
        }

        /// <summary>저장소 사본: 모든 탭이 같은 로케일(ko·en·ja), Framework가 찾는 appInfo/LanguageDisplayName이 있다.</summary>
        [Test]
        public void RepoSheets_AreConsistent()
        {
            var sheets = MvLocBuild.ReadSheets(MvLocBuild.SheetDir, out var errors);
            Assert.That(errors, Is.Empty, string.Join("\n", errors));
            Assert.That(sheets[0].Locales, Is.EqualTo(new[] { "ko", "en", "ja" }));
            Assert.That(sheets.Select(s => s.Collection), Is.SupersetOf(new[] { "ui", "msg", "toastMessage", "name", "description", "appInfo" }));
            var appInfo = sheets.Single(s => s.Collection == "appInfo");
            Assert.That(appInfo.Rows.Any(r => r.Key == "LanguageDisplayName" && r.Value.All(v => v.Length > 0)), Is.True);
        }

        /// <summary>코드가 글자 그대로 부르는 키(MvUiText.Ui·Toast·Msg)는 시트에 있고 세 언어가 다 채워져 있다(없으면 화면에 #키).</summary>
        [Test]
        public void CodeKeys_ExistInSheets_WithAllLocales()
        {
            var sheets = MvLocBuild.ReadSheets(MvLocBuild.SheetDir, out var errors);
            Assert.That(errors, Is.Empty, string.Join("\n", errors));
            var rows = sheets.Where(s => s.Collection == "ui" || s.Collection == "toastMessage" || s.Collection == "msg")
                .SelectMany(s => s.Rows).ToDictionary(r => r.Key, r => r.Value);
            var call = new System.Text.RegularExpressions.Regex("MvUiText\\.(?:Ui|Toast|Msg)\\(\\s*\"((?:ui|toastMessage|msg)\\.[A-Za-z0-9_.]+)\"");
            var missing = new List<string>();
            int used = 0;
            foreach (var file in System.IO.Directory.GetFiles("Assets/Game/Scripts", "*.cs", System.IO.SearchOption.AllDirectories))
            {
                foreach (System.Text.RegularExpressions.Match m in call.Matches(System.IO.File.ReadAllText(file)))
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
