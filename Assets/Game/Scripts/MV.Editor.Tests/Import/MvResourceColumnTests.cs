using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using MV.Data;
using MV.Editor.Import;
using NUnit.Framework;

namespace MV.Editor.Tests
{
    /// <summary>표 리소스 열 검사기(07 §7.10, 03 §3.8.6) — 티켓 04. 분류는 D-MVDATA-RES(그림 없음 = 경고, 이름은 있는데 틀림 = 오류).</summary>
    public sealed class MvResourceColumnTests
    {
        [Test]
        public void AllTableResourceColumns_ResolveOrAreReported()
        {
            var report = MvResourceColumnChecker.CheckProject();
            TestContext.WriteLine(report.Summary());

            Assert.That(report.Checked, Is.GreaterThan(500), "확인한 값 수");
            Assert.That(report.Of(MvResIssueKind.Error).Select(e => e.ToString()), Is.Empty, "리소스 열 오류(아틀라스·대소문자·주소가 틀림)");
            Assert.That(report.Of(MvResIssueKind.Warning).Select(e => e.ToString()), Is.Empty, "스키마에 Res로 선언되지 않은 리소스 열");
            Assert.That(report.Of(MvResIssueKind.OtherOwner).Where(e => e.Table != "03_sound" && e.Table != "04_effect").Select(e => e.ToString()), Is.Empty,
                "다른 계약 주소 미생성은 소리·이펙트 표만");

            var path = Path.GetFullPath(MvResourceColumnChecker.UnextractedListPath);
            Assert.That(File.Exists(path), Is.True, "미추출 보고 목록 " + MvResourceColumnChecker.UnextractedListPath);
            var listed = MvResourceColumnChecker.ReadUnextractedListKeys(path);
            var current = MvResourceColumnChecker.UnextractedIssues(report).ToList();
            var stale = listed.Except(current.Select(MvResourceColumnChecker.ListKey)).ToList();
            if (stale.Count > 0) TestContext.WriteLine($"미추출 보고 목록에서 해소된 줄 {stale.Count}(메뉴 MV/Tables/Write Unextracted Resource List로 지운다):\n" + string.Join("\n", stale));

            var notListed = current.Where(i => !listed.Contains(MvResourceColumnChecker.ListKey(i))).Select(i => i.Kind + "\t" + MvResourceColumnChecker.ListKey(i)).ToList();
            if (notListed.Count > 0) TestContext.WriteLine("미추출 보고 목록에 없는 줄:\n" + string.Join("\n", notListed));
            Assert.That(notListed, Is.Empty, "미추출 경고는 모두 보고 목록에 있어야 한다");
        }

        [Test]
        public void Checker_FlagsBrokenNamesAtlasesAndPending()
        {
            var dir = MvTableLoader.TsvDirectory;
            string Read(string table) => File.ReadAllText(Path.Combine(dir, table + ".txt"), new UTF8Encoding(false));

            var item = Read("103_item").Replace("UI_Icon_Weapon_1\t", "UI_Icon_Weapon_Nope\t").Replace("UI_Icon_Weapon_2\t", "ui_icon_weapon_2\t")
                .Replace("UI_Icon_Weapon_3\t", "UI_Icon_Only_In_Card\t");
            var theme = Read("304_theme").Replace("World_Prop_Snowman", "World_Prop_Fake");
            var kind = Read("105_objectKind").Replace("World_Obj_Arch_Post_Gold", "World_Obj_Only_Somewhere");
            var ui = Read("09_uiResource").Replace("Atlas_UI_Icon\tUI_Emoji_Close\t", "Atlas_UI_Nope\tUI_Emoji_NotYet\t");
            var god = Read("113_god").Replace("Tex_God_Pisces", "Tex_Only_File").Replace("Tex_God_Aries", "UI_Icon_Weapon_1").Replace("Tex_God_Thunder", "Tex_God_thunder");
            var pet = Read("430_pet").Replace("\tPet_sparrow\t", "\tPet_NotYetMade\t");
            Assume.That(item, Does.Contain("UI_Icon_Weapon_Nope").And.Contain("UI_Icon_Only_In_Card"));
            Assume.That(theme, Does.Contain("World_Prop_Fake"));
            Assume.That(kind, Does.Contain("World_Obj_Only_Somewhere"));
            Assume.That(ui, Does.Contain("UI_Emoji_NotYet"));
            Assume.That(god, Does.Contain("Tex_Only_File").And.Contain("UI_Icon_Weapon_1").And.Contain("Tex_God_thunder"));
            Assume.That(pet, Does.Contain("Pet_NotYetMade"));

            var source = new MvMemoryTableSource(new MvTsvDirectorySource(dir)).Set("103_item", item).Set("304_theme", theme).Set("105_objectKind", kind)
                .Set("09_uiResource", ui).Set("113_god", god).Set("430_pet", pet);
            var tables = new MvTables();
            tables.LoadAll(source);

            var catalog = MvArtCatalog.FromProject();
            catalog.AddSprite("Atlas_World_Theme_market", "World_Obj_Only_Somewhere"); // 한 테마에만 있음 → 나머지 테마에서 오류
            catalog.AddSprite(MvAtlas.UiCard, "UI_Icon_Only_In_Card"); // 다른 아틀라스에만 있음 → 오류
            var tempList = new Dictionary<string, string> { ["World_Prop_Fake"] = "A" };
            var artFiles = new HashSet<string>(StringComparer.Ordinal) { "Tex_Only_File" }; // 파일은 있으나 주소 없음
            var report = MvResourceColumnChecker.Check(tables, catalog, tempList, null, artFiles);

            var errors = report.Of(MvResIssueKind.Error).ToList();
            var unextracted = report.Of(MvResIssueKind.Unextracted).ToList();

            // 그림이 어디에도 없음 → 경고(Unextracted), 오류 아님
            Assert.That(unextracted.Any(e => e.Table == "103_item" && e.Value == "UI_Icon_Weapon_Nope"), Is.True, "없는 이름은 미추출 경고");
            Assert.That(errors.Any(e => e.Value == "UI_Icon_Weapon_Nope"), Is.False, "없는 이름은 오류가 아님");
            var petSprite = unextracted.Where(e => e.Table == "430_pet" && e.Value == "Pet_NotYetMade").ToList();
            Assert.That(petSprite.Select(e => e.Atlas), Is.EqualTo(new[] { MvAtlas.WorldPet }), "430_pet.sprite는 Atlas_World_Pet 하나에서(테마마다 아님)");
            Assert.That(unextracted.Any(e => e.Table == "113_god" && e.Value == "Tex_God_Sagittarius"), Is.True, "없는 주소는 미추출 경고");

            // 이름은 있는데 틀림 → 오류
            var cased = errors.FirstOrDefault(e => e.Table == "103_item" && e.Value == "ui_icon_weapon_2");
            Assert.That(cased, Is.Not.Null, "대소문자 다른 이름");
            Assert.That(cased.Message, Does.Contain("UI_Icon_Weapon_2"));
            var wrongAtlas = errors.FirstOrDefault(e => e.Table == "103_item" && e.Value == "UI_Icon_Only_In_Card");
            Assert.That(wrongAtlas, Is.Not.Null, "다른 아틀라스에만 있는 이름");
            Assert.That(wrongAtlas.Message, Does.Contain(MvAtlas.UiCard));
            Assert.That(errors.Any(e => e.Table == "09_uiResource" && e.Value == "UI_Emoji_NotYet" && e.Atlas == "Atlas_UI_Nope"), Is.True,
                "규칙에 없는 아틀라스는 그림이 없어도 오류");
            var godCase = errors.FirstOrDefault(e => e.Table == "113_god" && e.Value == "Tex_God_thunder");
            Assert.That(godCase, Is.Not.Null, "주소 대소문자");
            Assert.That(godCase.Message, Does.Contain("Tex_God_Thunder"));
            Assert.That(errors.Any(e => e.Table == "113_god" && e.Value == "Tex_Only_File"), Is.True, "그림 파일은 있으나 주소 없음");
            Assert.That(errors.Any(e => e.Table == "113_god" && e.Value == "UI_Icon_Weapon_1"), Is.True, "주소 열에 아틀라스 스프라이트 이름");

            var themed = errors.Where(e => e.Table == "105_objectKind" && e.Value == "World_Obj_Only_Somewhere").Select(e => e.Atlas).ToList();
            Assert.That(themed, Does.Not.Contain("Atlas_World_Theme_market"), "@theme: 있는 테마는 통과");
            Assert.That(themed.Count, Is.EqualTo(catalog.ThemeAtlases.Count - 1), "@theme: 모든 304_theme.worldAtlas에서 확인");

            Assert.That(report.Of(MvResIssueKind.Pending).Any(e => e.Value == "World_Prop_Fake" && e.Atlas == "Atlas_World_Theme_snow"), Is.True,
                "roadsideProps는 그 행의 테마 아틀라스에서, 미추출 이름은 Pending");
        }

        [Test]
        public void UnextractedList_RoundTripsKeys()
        {
            var report = new MvResourceCheckReport();
            report.Issues.Add(new MvResIssue { Kind = MvResIssueKind.Unextracted, Table = "430_pet", RowId = "430001002", Column = "icon", Value = "UI_Icon_Pet_b", Atlas = MvAtlas.UiIcon });
            report.Issues.Add(new MvResIssue { Kind = MvResIssueKind.Pending, Table = "09_uiResource", RowId = "9002002", Column = "sprite", Value = "UI_Icon_Brick", Atlas = MvAtlas.UiIcon });
            report.Issues.Add(new MvResIssue { Kind = MvResIssueKind.Error, Table = "103_item", RowId = "1", Column = "icon", Value = "x", Atlas = MvAtlas.UiIcon });
            report.Issues.Add(new MvResIssue { Kind = MvResIssueKind.OtherOwner, Table = "03_sound", RowId = "1", Column = "soundName", Value = "Sfx_A", Atlas = MvAtlas.Address });

            var text = MvResourceColumnChecker.FormatUnextractedList(report);
            var lines = text.Split('\n').Where(l => l.Length > 0).ToList();
            Assert.That(lines.Count, Is.EqualTo(3), "머리 줄 + 미추출 2(오류·다른 계약은 빠짐)");
            Assert.That(lines[1], Does.StartWith("Pending\t09_uiResource"), "표 이름 순 정렬");

            var tmp = Path.Combine(Path.GetTempPath(), "mv_res_unextracted_" + Guid.NewGuid().ToString("N") + ".tsv");
            try
            {
                File.WriteAllText(tmp, text, new UTF8Encoding(false));
                var keys = MvResourceColumnChecker.ReadUnextractedListKeys(tmp);
                Assert.That(keys, Is.EquivalentTo(MvResourceColumnChecker.UnextractedIssues(report).Select(MvResourceColumnChecker.ListKey)));
            }
            finally { File.Delete(tmp); }
        }

        [Test]
        public void Catalog_ImplementsDataValidatorContract()
        {
            var catalog = MvArtCatalog.FromProject();
            Assert.That(catalog.Has(MvAtlas.UiIcon, "UI_Icon_Weapon_1"), Is.True);
            Assert.That(catalog.Has(MvAtlas.UiIcon, "UI_Icon_Weapon_Nope"), Is.False);
            Assert.That(catalog.Has(MvAtlas.AtlasName, "Atlas_World_Theme_market"), Is.True, "아틀라스 주소");
            Assert.That(catalog.Has(MvAtlas.Address, "Tex_God_Flame"), Is.True, "단독 텍스처 주소");
            Assert.That(catalog.Has("Atlas_World_Theme_market", "World_Enemy_Mouse"), Is.True, "프레임 묶음 접두(_00)");
            Assert.That(catalog.ThemeAtlases.Count, Is.EqualTo(15), "D-72 테마 15");
        }
    }
}
