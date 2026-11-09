using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MV.Data;
using MV.Editor.Import;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace MV.Editor.Tests
{
    /// <summary>아틀라스 크기·포맷·개수 예산(03 §3.8.1, §3.8.11, §3.8.12) — 티켓 05.</summary>
    public sealed class MvAtlasBudgetTests
    {
        static List<MvAtlasEstimate> s_estimates;

        static List<MvAtlasEstimate> Estimates => s_estimates ??= MvAtlasBudget.EstimateProject();

        [Test]
        public void EveryAtlas_FitsOnePageOfMaxSize()
        {
            foreach (var e in Estimates) TestContext.WriteLine(e);
            var over = Estimates.Where(e => e.LowerBoundRatio > 1.0).Select(e => e.ToString()).ToList();
            Assert.That(over, Is.Empty, "확실한 하한이 최대 크기 1페이지를 넘는 아틀라스(03 §3.8.1: `_2`로 나눠야 함)");
            Assert.That(Estimates.Where(e => !e.Spec.Tight && !e.RectFit).Select(e => e.Spec.Name), Is.Empty, "Full Rect 아틀라스는 사각 추정이 곧 실제");
            // 타이트 아틀라스가 사각(알파 경계) 추정으로 1페이지에 안 들어가면 Unity 타이트 패킹 실측이 판정한다(Sprite Packer 모드 요청, 보고).
            var risk = Estimates.Where(e => !e.RectFit).Select(e => $"{e.Spec.Name} 하한 {e.LowerBoundRatio:P0} 사각 {e.AreaRatio:P0}").ToList();
            if (risk.Count > 0) TestContext.WriteLine("실측 필요 위험(사각 추정 1페이지 초과): " + string.Join(", ", risk));
        }

        [Test]
        public void CompressionFormats_MatchDecisionD54()
        {
            foreach (var e in Estimates)
            {
                var expected = e.Spec.IsUi || e.Spec.Name.StartsWith("Atlas_Fx_", StringComparison.Ordinal)
                    ? TextureImporterFormat.ASTC_4x4
                    : TextureImporterFormat.ASTC_6x6;
                Assert.That(e.Spec.Format, Is.EqualTo(expected), e.Spec.Name);
            }
        }

        [Test]
        public void RunScreenTextureMemory_WithinTarget()
        {
            var mb = MvAtlasBudget.RunScreenMegabytes(Estimates, out var breakdown);
            TestContext.WriteLine(breakdown);
            Assert.That(mb, Is.LessThanOrEqualTo(MvAtlasBudget.MemoryTargetMb), "03 §3.8.11 목표 48 MB");
        }

        [Test]
        public void AtlasCounts_MatchSpec()
        {
            var names = Estimates.Select(e => e.Spec.Name).ToList();
            Assert.That(names.Count(n => n.StartsWith(MvArtRules.CatAtlasPrefix, StringComparison.Ordinal)), Is.EqualTo(8), "Atlas_World_Cat_T{1..8}");
            Assert.That(names.Count(n => n.StartsWith(MvArtRules.FriendAtlasPrefix, StringComparison.Ordinal)), Is.EqualTo(6), "Atlas_World_Friend_T{1..6}");
            Assert.That(names, Does.Contain(MvArtRules.LobbyAtlas));
            Assert.That(names, Does.Contain("Atlas_Fx_Common"));
            Assert.That(names, Does.Contain("Atlas_UI_Icon"));
            Assert.That(names, Does.Contain("Atlas_UI_Card"));

            // 테마마다 런 그림 1장(D-58): 304_theme의 worldAtlas 전부가 있고, 테마 폴더는 모두 표에서 쓰인다.
            var tables = new MvTables();
            tables.LoadAll(new MvTsvDirectorySource(MvTableLoader.TsvDirectory));
            var themeRows = tables.Theme.RawRows.Select(r => (r.Get("worldAtlas") ?? "").Trim()).Where(v => v.Length > 0).ToList();
            var themeFolders = names.Where(n => MvArtRules.ThemeKeyOf(n) != null).ToList();
            Assert.That(themeFolders, Is.EquivalentTo(themeRows), "테마 아틀라스 = 304_theme.worldAtlas");

            foreach (var e in Estimates.Where(e => e.Spec.Name.StartsWith(MvArtRules.CatAtlasPrefix, StringComparison.Ordinal)))
                Assert.That(e.SpriteCount, Is.EqualTo(37), e.Spec.Name + " 프레임 수(03 §3.8.1)");
            foreach (var e in Estimates.Where(e => e.Spec.Name.StartsWith(MvArtRules.FriendAtlasPrefix, StringComparison.Ordinal)))
                Assert.That(e.SpriteCount, Is.EqualTo(100), e.Spec.Name + " 프레임 수(03 §3.8.1)");
        }

        [Test]
        public void ThemeAtlases_ShareCommonNames()
        {
            var catalog = MvArtCatalog.FromProject();
            var themes = catalog.ThemeAtlases;
            Assert.That(themes.Count, Is.GreaterThan(0));

            // 테마 전용 소품(304_theme.roadsideProps)만 테마마다 달라도 된다(03 §3.4.5).
            var tables = new MvTables();
            tables.LoadAll(new MvTsvDirectorySource(MvTableLoader.TsvDirectory));
            var themeOnly = new HashSet<string>(tables.Theme.RawRows.SelectMany(r => (r.Get("roadsideProps") ?? "").Split('/')).Select(s => s.Trim()));

            var union = new HashSet<string>(themes.SelectMany(t => catalog.Sprites[t]).Where(n => !themeOnly.Contains(n)));
            var missing = new List<string>();
            foreach (var t in themes)
            {
                var lack = union.Except(catalog.Sprites[t]).ToList();
                if (lack.Count > 0) missing.Add($"{t}: {string.Join(", ", lack.Take(10))}");
            }
            Assert.That(missing, Is.Empty, "공통 필수 이름이 빠진 테마 아틀라스");
        }

        [Test]
        public void Estimator_PacksKnownLayout()
        {
            var spec = MvArtRules.SpecFor("Atlas_UI_Icon");
            // 4 × (252+4)² = 512×512에 정확히 들어간다.
            var rects = Enumerable.Repeat(new Vector2Int(256, 256), 4).ToList();
            var e = MvAtlasBudget.Estimate(spec, rects);
            Assert.That(e.RectFit, Is.True);
            Assert.That(e.PageWidth * e.PageHeight, Is.EqualTo(512 * 512));
            Assert.That(MvAtlasBudget.CompressedBytes(2048, 2048, TextureImporterFormat.ASTC_6x6, false), Is.EqualTo(342L * 342 * 16), "03 §3.8.11 1.87 MB");
            Assert.That(MvAtlasBudget.CompressedBytes(1024, 1024, TextureImporterFormat.ASTC_4x4, false), Is.EqualTo(1024L * 1024), "4x4 = 8 bpp");
        }
    }
}
