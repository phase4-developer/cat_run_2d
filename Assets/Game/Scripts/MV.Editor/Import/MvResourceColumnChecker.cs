using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using MV.Data;

namespace MV.Editor.Import
{
    public enum MvResIssueKind
    {
        /// <summary>표 값이 실제 스프라이트·주소로 풀리지 않는다(이름·아틀라스 오류, 아틀라스 자산 없음).</summary>
        Error,
        /// <summary>03 §3.5 대응표 이름인데 아직 추출 전(MV-TOOL-ART 진행 중, temp_art_list 갈래 A).</summary>
        Pending,
        /// <summary>다른 계약이 만들 주소(소리 MV-TOOL-SND, 이펙트 프리팹 MV-FX)가 아직 없다.</summary>
        OtherOwner,
        /// <summary>정보: 스키마에 Res로 선언되지 않은 리소스 이름 열 등.</summary>
        Warning,
    }

    public sealed class MvResIssue
    {
        public string Table;
        public string RowId;
        public string Column;
        public string Value;
        public string Atlas;
        public MvResIssueKind Kind;
        public string Message;

        public override string ToString() => $"{Kind} {Table}[{RowId}].{Column} `{Value}` @{Atlas}: {Message}";
    }

    public sealed class MvResourceCheckReport
    {
        public readonly List<MvResIssue> Issues = new List<MvResIssue>();
        /// <summary>확인한 (값, 아틀라스) 수.</summary>
        public int Checked;

        public IEnumerable<MvResIssue> Of(MvResIssueKind kind) => Issues.Where(i => i.Kind == kind);
        public int Count(MvResIssueKind kind) => Issues.Count(i => i.Kind == kind);

        public string Summary(int max = 400)
        {
            var sb = new StringBuilder();
            sb.Append($"리소스 열 확인 {Checked}: 오류 {Count(MvResIssueKind.Error)}, 미추출(TOOL-ART 진행 중) {Count(MvResIssueKind.Pending)}, ")
              .Append($"다른 계약 주소 미생성 {Count(MvResIssueKind.OtherOwner)}, 경고 {Count(MvResIssueKind.Warning)}");
            int n = 0;
            foreach (var issue in Issues.OrderBy(i => i.Kind).ThenBy(i => i.Table, StringComparer.Ordinal))
            {
                if (n++ >= max) { sb.Append("\n…"); break; }
                sb.Append('\n').Append(issue);
            }
            return sb.ToString();
        }
    }

    /// <summary>
    /// 모든 표의 리소스 열(기획서 07 §7.10, 03 §3.8.6)이 실제 스프라이트·주소로 풀리는지 확인한다.
    /// 아틀라스 결정: 행 `atlas`(`iconAtlas`) → 표 정의 기본(`MvSchema.Res`의 Atlas) → `@theme` = 모든 304_theme.worldAtlas.
    /// 표가 늘면(4xx·7xx) 스키마의 Res 열을 그대로 따라간다. 표 값은 고치지 않는다(오류는 MV-DATA 변경 요청).
    /// </summary>
    public static class MvResourceColumnChecker
    {
        /// <summary>07 §7.10.1의 리소스 열 이름과 기본 아틀라스. 스키마에 Res로 없을 때의 대체 규칙.</summary>
        public static readonly IReadOnlyDictionary<string, string> NameDefaults = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["icon"] = MvAtlas.UiIcon,
            ["largeIcon"] = MvAtlas.UiIcon,
            ["mapIcon"] = MvAtlas.UiLobby,
            ["frameSprite"] = MvAtlas.UiCard,
            ["illust"] = MvAtlas.Address,
            ["bg"] = MvAtlas.Address,
            ["portrait"] = MvAtlas.Address,
            ["address"] = MvAtlas.Address,
            ["sprite"] = MvAtlas.Theme,
            ["projSprite"] = MvAtlas.Theme,
            ["spriteSoul"] = MvAtlas.Theme,
            ["worldBadge"] = MvAtlas.Theme,
            ["lobbySprite"] = MvAtlas.WorldLobby,
            ["facSprite"] = MvAtlas.WorldLobby,
            ["worldAtlas"] = MvAtlas.AtlasName,
        };

        const string ThemeTable = "304_theme";
        const string ConsumeTypeTable = "07_consumeType";
        const string SoundTable = "03_sound";
        const string EffectTable = "04_effect";

        /// <summary>저장소 TSV·프로젝트 상태로 확인한다(에디터·시험).</summary>
        public static MvResourceCheckReport CheckProject()
        {
            var source = new MvTsvDirectorySource(MvTableLoader.TsvDirectory);
            var tables = new MvTables();
            tables.LoadAll(source);
            return Check(tables, MvArtCatalog.FromProject(), MvArtManifest.ReadTempList(), source.ReadHeaders);
        }

        /// <param name="tempList">temp_art_list.tsv 이름 → 갈래. 갈래 `A`는 미추출로 센다.</param>
        /// <param name="headers">표 원문 열 이름(스키마 밖 리소스 열 찾기). 없으면 스키마만 본다.</param>
        public static MvResourceCheckReport Check(MvTables tables, MvArtCatalog catalog, IReadOnlyDictionary<string, string> tempList,
            Func<string, IReadOnlyList<string>> headers = null)
        {
            var report = new MvResourceCheckReport();
            var ctx = new Ctx { Catalog = catalog, TempList = tempList ?? new Dictionary<string, string>(), Report = report };

            var theme = tables.Find(ThemeTable);
            var themeAtlases = theme == null
                ? new List<string>()
                : theme.RawRows.Select(r => (r.Get("worldAtlas") ?? "").Trim()).Where(v => v.Length > 0).Distinct().ToList();
            catalog.SetThemeAtlases(themeAtlases);
            ctx.ThemeAtlases = themeAtlases;

            foreach (var table in tables.All)
            {
                var schema = table.Schema;
                var resColumns = schema.Columns.Where(c => c.Kind == MvColKind.Res).ToList();
                var extra = new List<string>();
                var head = headers?.Invoke(table.TableName);
                if (head != null)
                    foreach (var h in head)
                        if (NameDefaults.ContainsKey(h) && resColumns.All(c => c.Name != h) && !(table.TableName == ThemeTable && h == "roadsideProps"))
                        {
                            extra.Add(h);
                            ctx.Add(MvResIssueKind.Warning, table.TableName, "-", h, "", "", "스키마에 Res로 선언되지 않은 리소스 열(이름 규칙으로 확인, MV-DATA 확인 요청)");
                        }

                foreach (var row in table.RawRows)
                {
                    var rowId = (row.Get(schema.IdColumn) ?? row.Get("key") ?? "?").Trim();
                    if (rowId.Length == 0 || rowId.StartsWith("#", StringComparison.Ordinal)) continue;
                    ctx.Table = table.TableName;
                    ctx.RowId = rowId;
                    ctx.Row = row;

                    foreach (var c in resColumns) CheckColumn(ctx, c.Name, c.Atlas);
                    foreach (var h in extra) CheckColumn(ctx, h, NameDefaults[h]);

                    if (table.TableName == ThemeTable) CheckRoadsideProps(ctx);
                    if (table.TableName == ConsumeTypeTable) CheckFixedIcon(ctx);
                }
            }
            return report;
        }

        sealed class Ctx
        {
            public MvArtCatalog Catalog;
            public IReadOnlyDictionary<string, string> TempList;
            public MvResourceCheckReport Report;
            public List<string> ThemeAtlases;
            public string Table;
            public string RowId;
            public TSVRow Row;
            readonly HashSet<string> _atlasChecked = new HashSet<string>(StringComparer.Ordinal);

            public void Add(MvResIssueKind kind, string table, string rowId, string column, string value, string atlas, string message) =>
                Report.Issues.Add(new MvResIssue { Kind = kind, Table = table, RowId = rowId, Column = column, Value = value, Atlas = atlas, Message = message });

            public void Add(MvResIssueKind kind, string column, string value, string atlas, string message) => Add(kind, Table, RowId, column, value, atlas, message);

            /// <summary>아틀라스가 로드 가능한지 한 번만 확인(이 계약의 산출물: 자산·주소).</summary>
            public bool AtlasUsable(string column, string value, string atlas)
            {
                if (Catalog.IsAtlasLoadable(atlas)) return true;
                if (!Catalog.HasAtlasFolder(atlas)) return false; // 그림 자체가 없음 — 이름 단계에서 분류
                if (_atlasChecked.Add(atlas))
                    Add(MvResIssueKind.Error, column, value, atlas,
                        Catalog.HasAtlasAsset(atlas) ? "아틀라스 주소가 Addressables에 없음" : "SpriteAtlas 자산 없음");
                return false;
            }
        }

        static IEnumerable<string> Parts(string raw) =>
            (raw ?? "").Split('/').Select(p => p.Trim()).Where(p => p.Length > 0);

        static void CheckColumn(Ctx ctx, string column, string defaultAtlas)
        {
            var raw = ctx.Row.Get(column);
            if (string.IsNullOrWhiteSpace(raw)) return;

            if (column == "spritePrefix" && ctx.Row.Contains("atlasPattern"))
            {
                CheckSpritePrefix(ctx, raw.Trim());
                return;
            }

            if (defaultAtlas == MvAtlas.Address)
            {
                foreach (var part in Parts(raw)) CheckAddress(ctx, column, part);
                return;
            }

            if (defaultAtlas == MvAtlas.AtlasName)
            {
                if (column == "worldAtlas")
                {
                    foreach (var part in Parts(raw))
                    {
                        ctx.Report.Checked++;
                        if (ctx.Catalog.IsAtlasLoadable(part)) continue;
                        ctx.Add(MvResIssueKind.Error, column, part, part, ctx.Catalog.HasAtlasFolder(part) ? "아틀라스 자산·주소 없음" : "아틀라스 그림 폴더 없음");
                    }
                    return;
                }
                // 09_uiResource.sprite: 아틀라스는 행의 atlas 열
                var rowAtlas = (ctx.Row.Get("atlas") ?? "").Trim();
                if (rowAtlas.Length == 0)
                {
                    ctx.Add(MvResIssueKind.Error, column, raw.Trim(), "", "행의 atlas 열이 비어 있음");
                    return;
                }
                foreach (var part in Parts(raw)) CheckSprite(ctx, column, part, rowAtlas);
                return;
            }

            var atlas = defaultAtlas;
            var overrideAtlas = (ctx.Row.Get("atlas") ?? "").Trim();
            if (overrideAtlas.StartsWith("Atlas_", StringComparison.Ordinal)) atlas = overrideAtlas;

            foreach (var part in Parts(raw))
            {
                if (atlas == MvAtlas.Theme)
                    foreach (var themeAtlas in ctx.ThemeAtlases) CheckSprite(ctx, column, part, themeAtlas);
                else CheckSprite(ctx, column, part, atlas);
            }
        }

        static void CheckSprite(Ctx ctx, string column, string name, string atlas)
        {
            ctx.Report.Checked++;
            // 그림은 있으나 아틀라스 자산·주소가 없으면 AtlasUsable이 아틀라스당 오류를 1번 남긴다.
            ctx.AtlasUsable(column, name, atlas);
            if (ctx.Catalog.HasSprite(atlas, name)) return;

            if (ctx.TempList.TryGetValue(name, out var kind) && kind == "A")
            {
                ctx.Add(MvResIssueKind.Pending, column, name, atlas, "미추출(TOOL-ART 진행 중)");
                return;
            }

            string hint;
            var elsewhere = ctx.Catalog.AtlasesContaining(name).Take(3).ToList();
            var cased = ctx.Catalog.FindCaseInsensitive(name);
            if (elsewhere.Count > 0) hint = $"{atlas}에 없음 — 실제 아틀라스: {string.Join(", ", elsewhere)}(행 atlas 열 또는 기본 아틀라스 확인)";
            else if (cased != null && cased != name) hint = $"대소문자 다름 — 실제 이름 `{cased}`";
            else if (!ctx.Catalog.HasAtlasFolder(atlas)) hint = $"아틀라스 {atlas} 그림 폴더 없음";
            else hint = $"{atlas}에 없음";
            ctx.Add(MvResIssueKind.Error, column, name, atlas, hint);
        }

        static void CheckAddress(Ctx ctx, string column, string address)
        {
            ctx.Report.Checked++;
            if (ctx.Catalog.HasAddress(address)) return;

            if (ctx.Table == SoundTable || address.StartsWith("Sfx_", StringComparison.Ordinal) || address.StartsWith("Bgm_", StringComparison.Ordinal))
            {
                ctx.Add(MvResIssueKind.OtherOwner, column, address, MvAtlas.Address, "소리 주소 없음(MV-TOOL-SND 산출물)");
                return;
            }
            if (ctx.Table == EffectTable)
            {
                ctx.Add(MvResIssueKind.OtherOwner, column, address, MvAtlas.Address, "이펙트 프리팹 주소 없음(MV-FX 산출물)");
                return;
            }
            if (ctx.TempList.TryGetValue(address, out var kind) && kind == "A")
            {
                ctx.Add(MvResIssueKind.Pending, column, address, MvAtlas.Address, "미추출(TOOL-ART 진행 중)");
                return;
            }
            var cased = ctx.Catalog.FindCaseInsensitive(address);
            ctx.Add(MvResIssueKind.Error, column, address, MvAtlas.Address,
                cased != null && cased != address ? $"대소문자 다름 — 실제 주소 `{cased}`" : "Addressables 주소 없음");
        }

        /// <summary>101_cat: 접두 + atlasPattern(`{t}` = 1..tierCount), 접두의 `{p}` = 1..patternCount.</summary>
        static void CheckSpritePrefix(Ctx ctx, string prefix)
        {
            var pattern = (ctx.Row.Get("atlasPattern") ?? "").Trim();
            int tiers = ctx.Row.GetInt("tierCount", 0);
            int patterns = Math.Max(1, ctx.Row.GetInt("patternCount", 1));
            if (pattern.Length == 0 || tiers <= 0)
            {
                ctx.Add(MvResIssueKind.Error, "spritePrefix", prefix, pattern, "atlasPattern·tierCount 없음");
                return;
            }
            for (int t = 1; t <= tiers; t++)
            {
                var atlas = pattern.Replace("{t}", t.ToString(CultureInfo.InvariantCulture));
                var ps = prefix.Contains("{p}") ? Enumerable.Range(1, patterns).Select(p => prefix.Replace("{p}", p.ToString(CultureInfo.InvariantCulture))) : new[] { prefix };
                foreach (var pre in ps)
                {
                    ctx.Report.Checked++;
                    ctx.AtlasUsable("spritePrefix", pre, atlas);
                    if (ctx.Catalog.HasPrefix(atlas, pre)) continue;
                    ctx.Add(MvResIssueKind.Error, "spritePrefix", pre, atlas, ctx.Catalog.HasAtlasFolder(atlas) ? $"`{pre}_*` 프레임 없음" : $"아틀라스 {atlas} 그림 폴더 없음");
                }
            }
        }

        /// <summary>304_theme.roadsideProps: 그 테마 아틀라스에만 있으면 된다(03 §3.4.5).</summary>
        static void CheckRoadsideProps(Ctx ctx)
        {
            var atlas = (ctx.Row.Get("worldAtlas") ?? "").Trim();
            var raw = ctx.Row.Get("roadsideProps");
            if (atlas.Length == 0 || string.IsNullOrWhiteSpace(raw)) return;
            foreach (var part in Parts(raw)) CheckSprite(ctx, "roadsideProps", part, atlas);
        }

        /// <summary>07_consumeType.iconColumn `=이름` → iconAtlas(07 §7.10.2 단계 2). 열 이름이면 그 표 열이 따로 확인된다.</summary>
        static void CheckFixedIcon(Ctx ctx)
        {
            var raw = (ctx.Row.Get("iconColumn") ?? "").Trim();
            if (!raw.StartsWith("=", StringComparison.Ordinal)) return;
            var atlas = (ctx.Row.Get("iconAtlas") ?? "").Trim();
            if (atlas.Length == 0) atlas = MvAtlas.UiIcon;
            CheckSprite(ctx, "iconColumn", raw.Substring(1).Trim(), atlas);
        }
    }
}
