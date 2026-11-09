using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using MV.Data;
using MV.Editor.Import;
using NUnit.Framework;

namespace MV.Editor.Tests
{
    /// <summary>표 리소스 열 검사기(07 §7.10, 03 §3.8.6) — 티켓 04.</summary>
    public sealed class MvResourceColumnTests
    {
        /// <summary>
        /// 표 값이 틀려 풀리지 않는 칸 = MV-DATA 변경 요청(이 계약은 표를 고치지 않는다). 형식 `표|행|열|값`.
        /// 여기 없는 오류가 나오면 실패한다. MV-DATA가 고쳐 오류가 사라진 줄은 시험 로그에 「해소」로 나온다(지워도 된다).
        /// </summary>
        static readonly HashSet<string> s_dataRequests = new HashSet<string>
        {
            // 행 iconAtlas = Atlas_UI_Hud, 그림은 Atlas_UI_Icon에 있음
            "07_consumeType|605|iconColumn|UI_Icon_Stair",
            // 행 atlas = Atlas_UI_Common(그림 없음), 그림은 Atlas_UI_Icon(03 §3.4.1 UI_Emoji_ 기본 아틀라스)
            "09_uiResource|9001001|sprite|UI_Emoji_Close",
            "09_uiResource|9001002|sprite|UI_Emoji_Play",
            "09_uiResource|9001003|sprite|UI_Emoji_Sound",
            "09_uiResource|9001004|sprite|UI_Emoji_Lock",
            // 행 atlas = Atlas_UI_Hud, 그림은 Atlas_UI_Icon
            "09_uiResource|9002001|sprite|UI_Icon_Stair",
            "09_uiResource|9002003|sprite|UI_Icon_Fac_Tower_2",
            // 행 atlas = Atlas_UI_Lobby, 그림은 Atlas_UI_Icon
            "09_uiResource|9003001|sprite|UI_Emoji_Basket",
            "09_uiResource|9003002|sprite|UI_Emoji_Map",
            // 대소문자: 실제 주소 Tex_God_Thunder·Tex_God_Flame
            "113_god|113001001|portrait|Tex_God_thunder",
            "113_god|113001002|portrait|Tex_God_flame",
        };

        static string Key(MvResIssue i) => $"{i.Table}|{i.RowId}|{i.Column}|{i.Value}";

        [Test]
        public void AllTableResourceColumns_ResolveOrAreReported()
        {
            var report = MvResourceColumnChecker.CheckProject();
            TestContext.WriteLine(report.Summary());

            Assert.That(report.Checked, Is.GreaterThan(500), "확인한 값 수");
            var errors = report.Of(MvResIssueKind.Error).ToList();
            var unexpected = errors.Where(e => !s_dataRequests.Contains(Key(e))).ToList();
            var resolved = s_dataRequests.Except(errors.Select(Key)).ToList();
            if (resolved.Count > 0) TestContext.WriteLine("MV-DATA 변경 요청 해소: " + string.Join(", ", resolved));
            Assert.That(unexpected.Select(e => e.ToString()), Is.Empty, "MV-DATA 변경 요청 목록 밖의 오류");
        }

        [Test]
        public void Checker_FlagsBrokenNamesAtlasesAndPending()
        {
            var dir = MvTableLoader.TsvDirectory;
            string Read(string table) => File.ReadAllText(Path.Combine(dir, table + ".txt"), new UTF8Encoding(false));

            var item = Read("103_item").Replace("UI_Icon_Weapon_1\t", "UI_Icon_Weapon_Nope\t").Replace("UI_Icon_Weapon_2\t", "ui_icon_weapon_2\t");
            var theme = Read("304_theme").Replace("World_Prop_Snowman", "World_Prop_Fake");
            var kind = Read("105_objectKind").Replace("World_Obj_Arch_Post_Gold", "World_Obj_Only_Somewhere");
            Assume.That(item, Does.Contain("UI_Icon_Weapon_Nope"));
            Assume.That(theme, Does.Contain("World_Prop_Fake"));
            Assume.That(kind, Does.Contain("World_Obj_Only_Somewhere"));

            var source = new MvMemoryTableSource(new MvTsvDirectorySource(dir)).Set("103_item", item).Set("304_theme", theme).Set("105_objectKind", kind);
            var tables = new MvTables();
            tables.LoadAll(source);

            var catalog = MvArtCatalog.FromProject();
            catalog.AddSprite("Atlas_World_Theme_market", "World_Obj_Only_Somewhere"); // 한 테마에만 있음 → 나머지 테마에서 오류
            var tempList = new Dictionary<string, string> { ["World_Prop_Fake"] = "A" };
            var report = MvResourceColumnChecker.Check(tables, catalog, tempList);

            var errors = report.Of(MvResIssueKind.Error).ToList();
            Assert.That(errors.Any(e => e.Table == "103_item" && e.Value == "UI_Icon_Weapon_Nope"), Is.True, "없는 이름");
            var cased = errors.FirstOrDefault(e => e.Table == "103_item" && e.Value == "ui_icon_weapon_2");
            Assert.That(cased, Is.Not.Null, "대소문자 다른 이름");
            Assert.That(cased.Message, Does.Contain("UI_Icon_Weapon_2"));

            var themed = errors.Where(e => e.Table == "105_objectKind" && e.Value == "World_Obj_Only_Somewhere").Select(e => e.Atlas).ToList();
            Assert.That(themed, Does.Not.Contain("Atlas_World_Theme_market"), "@theme: 있는 테마는 통과");
            Assert.That(themed.Count, Is.EqualTo(catalog.ThemeAtlases.Count - 1), "@theme: 모든 304_theme.worldAtlas에서 확인");

            Assert.That(report.Of(MvResIssueKind.Pending).Any(e => e.Value == "World_Prop_Fake" && e.Atlas == "Atlas_World_Theme_snow"), Is.True,
                "roadsideProps는 그 행의 테마 아틀라스에서, 미추출 이름은 Pending");
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
