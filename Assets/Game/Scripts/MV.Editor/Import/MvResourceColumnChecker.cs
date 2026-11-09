using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using MV.Data;
using UnityEditor;
using UnityEngine;

namespace MV.Editor.Import
{
    public enum MvResIssueKind
    {
        /// <summary>
        /// 표 값이 틀렸다: 그림·주소는 프로젝트에 있는데 아틀라스·대소문자·주소가 맞지 않거나, 규칙에 없는 아틀라스, 아틀라스 자산·주소 없음.
        /// 표 반영을 막는다(07 §7.10.5).
        /// </summary>
        Error,
        /// <summary>03 §3.5 대응표 이름인데 아직 추출 전(MV-TOOL-ART 진행 중, temp_art_list 갈래 A).</summary>
        Pending,
        /// <summary>그 이름의 그림이 프로젝트 어디에도 없다(미추출, D-MVDATA-RES). 경고 — 미추출 보고 목록(<see cref="MvResourceColumnChecker.UnextractedListPath"/>)에 남긴다.</summary>
        Unextracted,
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
              .Append($"미추출(그림 없음) {Count(MvResIssueKind.Unextracted)}, ")
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
    /// 분류(D-MVDATA-RES): 이름이 프로젝트 어디에도 없으면 <see cref="MvResIssueKind.Unextracted"/>(경고),
    /// 이름은 있는데 아틀라스·대소문자·주소가 틀리면 <see cref="MvResIssueKind.Error"/>.
    /// </summary>
    public static class MvResourceColumnChecker
    {
        /// <summary>미추출 보고 목록(MV-TOOL-ART가 읽는다). 열: kind, table, rowId, column, value, atlas. 메뉴 「MV/Tables/Write Unextracted Resource List」로 다시 쓴다.</summary>
        public const string UnextractedListPath = "Assets/Game/GameData/Table/TSV/Report/res_unextracted.tsv";
        const string UnextractedListHeader = "kind\ttable\trowId\tcolumn\tvalue\tatlas";

        /// <summary>그림 파일을 찾는 폴더(추출 원본·임시 아트·Addressables 단독 텍스처).</summary>
        static readonly string[] s_artFileRoots = { MvArtRules.ArtRoot, MvArtRules.AddressablesRemote };

        /// <summary>03 §3.8.1 아틀라스(+ `_2` 나눔) 와 D-MVDATA-PET `Atlas_World_Pet`. 이 밖의 아틀라스 이름은 오류.</summary>
        static readonly Regex s_ruleAtlas = new Regex(
            @"^Atlas_(UI_(Common|Hud|Lobby|Icon|Card)|World_(Theme_[A-Za-z0-9]+|Cat_T\d+|Friend_T\d+|Lobby|Pet)|Fx_Common)(_\d+)?$",
            RegexOptions.CultureInvariant);

        public static bool IsRuleAtlas(string atlas) => atlas != null && s_ruleAtlas.IsMatch(atlas);
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
            return Check(tables, MvArtCatalog.FromProject(), MvArtManifest.ReadTempList(), source.ReadHeaders, ProjectArtFileNames());
        }

        /// <summary>프로젝트의 모든 그림 파일 이름(확장자 없음). 「이름이 어디에도 없음」 판정에 쓴다.</summary>
        public static HashSet<string> ProjectArtFileNames()
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var root in s_artFileRoots)
            {
                if (!Directory.Exists(root)) continue;
                foreach (var file in Directory.GetFiles(root, "*.png", SearchOption.AllDirectories))
                    names.Add(Path.GetFileNameWithoutExtension(file));
            }
            return names;
        }

        /// <param name="tempList">temp_art_list.tsv 이름 → 갈래. 갈래 `A`는 미추출로 센다.</param>
        /// <param name="headers">표 원문 열 이름(스키마 밖 리소스 열 찾기). 없으면 스키마만 본다.</param>
        /// <param name="artFiles">프로젝트의 그림 파일 이름(<see cref="ProjectArtFileNames"/>). 없으면 카탈로그(아틀라스 그림·주소)만 본다.</param>
        public static MvResourceCheckReport Check(MvTables tables, MvArtCatalog catalog, IReadOnlyDictionary<string, string> tempList,
            Func<string, IReadOnlyList<string>> headers = null, ISet<string> artFiles = null)
        {
            var report = new MvResourceCheckReport();
            var ctx = new Ctx
            {
                Catalog = catalog, TempList = tempList ?? new Dictionary<string, string>(), Report = report,
                ArtFiles = artFiles ?? new HashSet<string>(StringComparer.Ordinal),
            };

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
            public ISet<string> ArtFiles;
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

            /// <summary>규칙에 있는 아틀라스인가: 그림 폴더가 있거나, 304_theme 값이거나, 03 §3.8.1 이름 규칙.</summary>
            public bool KnownAtlas(string atlas) => Catalog.HasAtlasFolder(atlas) || ThemeAtlases.Contains(atlas) || IsRuleAtlas(atlas);

            /// <summary>그림 파일 이름(대소문자 무시). 정확히 같으면 그 이름, 대소문자만 다르면 실제 이름, 없으면 null.</summary>
            public string ArtFileNamed(string name)
            {
                if (ArtFiles.Contains(name)) return name;
                foreach (var f in ArtFiles)
                    if (string.Equals(f, name, StringComparison.OrdinalIgnoreCase)) return f;
                return null;
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
                // 이펙트 프리팹 주소(`Fx/Sparks`)처럼 `/`가 주소의 일부인 값은 나누지 않는다.
                var whole = raw.Trim();
                if (ctx.Table == EffectTable || ctx.Catalog.HasAddress(whole)) CheckAddress(ctx, column, whole);
                else foreach (var part in Parts(raw)) CheckAddress(ctx, column, part);
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

            if (!ctx.KnownAtlas(atlas))
            {
                ctx.Add(MvResIssueKind.Error, column, name, atlas, "규칙(03 §3.8.1)에 없는 아틀라스 이름");
                return;
            }

            if (ctx.TempList.TryGetValue(name, out var kind) && kind == "A")
            {
                ctx.Add(MvResIssueKind.Pending, column, name, atlas, "미추출(TOOL-ART 진행 중)");
                return;
            }

            var elsewhere = ctx.Catalog.AtlasesContaining(name).Take(3).ToList();
            var cased = ctx.Catalog.FindCaseInsensitive(name);
            var file = ctx.ArtFileNamed(name);
            string hint;
            if (elsewhere.Count > 0) hint = $"{atlas}에 없음 — 실제 아틀라스: {string.Join(", ", elsewhere)}(행 atlas 열 또는 기본 아틀라스 확인)";
            else if (cased != null && cased != name) hint = $"대소문자 다름 — 실제 이름 `{cased}`";
            else if (file != null && file != name) hint = $"대소문자 다름 — 실제 그림 파일 `{file}`";
            else if (file != null) hint = $"그림 파일은 있으나 {atlas} 그림 폴더 밖(아틀라스에 묶이지 않음)";
            else
            {
                ctx.Add(MvResIssueKind.Unextracted, column, name, atlas, "그림이 프로젝트 어디에도 없음(미추출, MV-TOOL-ART)");
                return;
            }
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
            var file = ctx.ArtFileNamed(address);
            string hint;
            if (cased != null && cased != address) hint = $"대소문자 다름 — 실제 이름 `{cased}`";
            else if (cased != null) hint = $"아틀라스 스프라이트 이름({string.Join(", ", ctx.Catalog.AtlasesContaining(address).Take(3))}) — 주소 열에는 단독 텍스처 주소를 쓴다";
            else if (file != null && file != address) hint = $"대소문자 다름 — 실제 그림 파일 `{file}`";
            else if (file != null) hint = "그림 파일은 있으나 Addressables 주소 없음";
            else
            {
                ctx.Add(MvResIssueKind.Unextracted, column, address, MvAtlas.Address, "그림이 프로젝트 어디에도 없음(미추출, MV-TOOL-ART)");
                return;
            }
            ctx.Add(MvResIssueKind.Error, column, address, MvAtlas.Address, hint);
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

        // ---------------- 미추출 보고 목록 ----------------

        /// <summary>미추출 보고 목록에 들어가는 항목: 갈래 A 미추출(Pending)과 그림 없음(Unextracted).</summary>
        public static IEnumerable<MvResIssue> UnextractedIssues(MvResourceCheckReport report) =>
            report.Issues.Where(i => i.Kind == MvResIssueKind.Pending || i.Kind == MvResIssueKind.Unextracted);

        /// <summary>목록 한 줄의 비교 키(kind 제외): table, rowId, column, value, atlas.</summary>
        public static string ListKey(MvResIssue i) => string.Join("\t", i.Table, i.RowId, i.Column, i.Value, i.Atlas);

        /// <summary>목록 원문(LF, 머리 줄 + 표·행·열·값·아틀라스 순 정렬, 중복 없음).</summary>
        public static string FormatUnextractedList(MvResourceCheckReport report)
        {
            var lines = UnextractedIssues(report)
                .OrderBy(i => i.Table, StringComparer.Ordinal).ThenBy(i => i.RowId, StringComparer.Ordinal)
                .ThenBy(i => i.Column, StringComparer.Ordinal).ThenBy(i => i.Value, StringComparer.Ordinal).ThenBy(i => i.Atlas, StringComparer.Ordinal)
                .Select(i => i.Kind + "\t" + ListKey(i)).Distinct();
            var sb = new StringBuilder(UnextractedListHeader).Append('\n');
            foreach (var line in lines) sb.Append(line).Append('\n');
            return sb.ToString();
        }

        /// <summary>목록 파일의 키 집합. 파일이 없으면 빈 집합.</summary>
        public static HashSet<string> ReadUnextractedListKeys(string path)
        {
            var keys = new HashSet<string>(StringComparer.Ordinal);
            if (!File.Exists(path)) return keys;
            foreach (var line in File.ReadAllText(path, new UTF8Encoding(false)).Split('\n'))
            {
                var l = line.TrimEnd('\r');
                if (l.Length == 0 || l == UnextractedListHeader) continue;
                int tab = l.IndexOf('\t');
                if (tab > 0) keys.Add(l.Substring(tab + 1));
            }
            return keys;
        }

        [MenuItem("MV/Tables/Write Unextracted Resource List")]
        static void WriteUnextractedListMenu()
        {
            var report = CheckProject();
            var path = Path.GetFullPath(UnextractedListPath);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, FormatUnextractedList(report), new UTF8Encoding(false));
            AssetDatabase.ImportAsset(UnextractedListPath);
            Debug.Log($"[MvResourceColumnChecker] {UnextractedListPath} — {UnextractedIssues(report).Count()}줄\n" + report.Summary(50));
        }
    }
}
