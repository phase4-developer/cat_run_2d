using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

namespace MV.Meta.Tests
{
    /// <summary>
    /// 영속 변경의 유일한 진입점(10 §10, 11 MV-META): MV.Meta·MV.Save 밖의 게임 코드가 저장(<c>ApplyAndSave</c>)이나
    /// 보상 확정(<c>Commit</c>)을 직접 부르지 않는다. 소스 검색 시험이다.
    /// </summary>
    public sealed class MvMetaEntryPointRuleTests
    {
        static readonly Regex Forbidden = new Regex(@"\bApplyAndSave(Json)?\s*\(|\.\s*Commit\s*\(", RegexOptions.Compiled);
        static readonly Regex LineComment = new Regex(@"//.*$", RegexOptions.Compiled);

        static bool Allowed(string scriptsRoot, string file)
        {
            string relative = file.Substring(scriptsRoot.Length).TrimStart('/', '\\');
            string top = relative.Split('/', '\\')[0];
            return top.StartsWith("MV.Meta", StringComparison.Ordinal) || top.StartsWith("MV.Save", StringComparison.Ordinal);
        }

        [Test]
        public void NoPersistentWriteOutsideMetaAndSave()
        {
            string gameRoot = Path.Combine(Application.dataPath, "Game");
            string scriptsRoot = Path.Combine(gameRoot, "Scripts");
            Assert.That(Directory.Exists(scriptsRoot), Is.True, scriptsRoot);

            var hits = new List<string>();
            int scanned = 0;
            foreach (string file in Directory.EnumerateFiles(gameRoot, "*.cs", SearchOption.AllDirectories))
            {
                if (file.StartsWith(scriptsRoot, StringComparison.Ordinal) && Allowed(scriptsRoot, file)) continue;
                scanned++;
                string[] lines = File.ReadAllLines(file, Encoding.UTF8);
                for (int i = 0; i < lines.Length; i++)
                {
                    string code = LineComment.Replace(lines[i], string.Empty);
                    if (Forbidden.IsMatch(code)) hits.Add(file.Substring(gameRoot.Length + 1) + ":" + (i + 1) + "  " + lines[i].Trim());
                }
            }
            Assert.That(scanned, Is.GreaterThan(0), "검색한 파일이 없다");
            Assert.That(hits, Is.Empty, "MV.Meta·MV.Save 밖의 영속 쓰기:\n" + string.Join("\n", hits));
        }
    }
}
