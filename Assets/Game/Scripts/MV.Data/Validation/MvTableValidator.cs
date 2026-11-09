using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using jjevol;

namespace MV.Data
{
    /// <summary>
    /// 게임 표 검증기(기획서 07 §7.13). 같은 검사를 두 곳에서 부른다:
    /// 에디터 TableBuilder의 `TableAuthoringOptions.ValidateTables`(<see cref="ValidateAuthoring"/>)와
    /// 부팅·핫 리로드(<see cref="ValidateRuntime"/>). 오류 = 로드 실패, 경고 = 로그.
    /// </summary>
    public static partial class MvTableValidator
    {
        static readonly Regex s_key = new Regex("^[A-Za-z0-9._]+$", RegexOptions.CultureInvariant);
        static readonly Regex s_color = new Regex("^#([0-9a-fA-F]{6}|[0-9a-fA-F]{8})$", RegexOptions.CultureInvariant);
        static readonly CultureInfo C = CultureInfo.InvariantCulture;

        /// <summary>id 0을 「지금 고른 것」으로 쓰는 type(07 §7.8.12 valueMeaning).</summary>
        static readonly HashSet<int> s_zeroIdAllowed = new HashSet<int> { 3, 500, 601, 621, 622, 630 };

        /// <summary>런 표 밖에서도 쓸 수 있는 600번대(07 §7.13.2).</summary>
        /// 610·620·621·622·630은 메타 보관 재화(장비·펫 카드·친구 코인·신 조각·축복)라 상점·16·광고 보상에도 쓴다(07 §7.13.2 동기화).
        static readonly HashSet<int> s_metaRunTypes = new HashSet<int> { 608, 609, 610, 620, 621, 622, 630 };

        /// <summary>
        /// 작성 검증. 표 원문 묶음으로 새 <see cref="MvTables"/>를 채우고 검사한다. 원문 첫 줄로 중복 열도 본다.
        /// </summary>
        /// <param name="fallback">tableTexts에 없는 표를 읽을 원본(TableBuilder가 일부 표만 빌드할 때 TSV 폴더).</param>
        public static MvValidationReport ValidateAuthoring(IEnumerable<KeyValuePair<string, string>> tableTexts, MvValidationOptions options = null,
            ITableSource fallback = null)
        {
            var source = new MvMemoryTableSource(fallback);
            foreach (var pair in tableTexts) source.Set(pair.Key, pair.Value);
            var report = new MvValidationReport();
            var tables = new MvTables();
            if (!TryLoad(tables, source, report)) return report;
            options ??= new MvValidationOptions();
            if (options.Headers == null)
                options.Headers = name => source.TryGetText(name, out var text) ? FirstLine(text)
                    : fallback is MvTsvDirectorySource dir ? dir.ReadHeaders(name) : null;
            Validate(tables, report, options);
            return report;
        }

        /// <summary>작성 검증(빌드 입력에 없는 표는 TSV 폴더에서). Editor 어셈블리가 jjevol 표 타입 없이 부르는 입구.</summary>
        public static MvValidationReport ValidateAuthoringTsv(IEnumerable<KeyValuePair<string, string>> tableTexts, string tsvDirectory) =>
            ValidateAuthoring(tableTexts, null, new MvTsvDirectorySource(tsvDirectory));

        /// <summary>원본에서 표마다 읽는다. 표가 없거나 읽기가 실패하면 오류로 남기고 false.</summary>
        public static bool TryLoad(MvTables tables, ITableSource source, MvValidationReport report)
        {
            bool ok = true;
            foreach (var t in tables.All)
            {
                if (!source.HasTable(t.TableName))
                {
                    report.Error(t.TableName, null, null, "표 파일이 없습니다");
                    ok = false;
                    continue;
                }
                try
                {
                    t.Table.LoadDatas(source);
                }
                catch (Exception e)
                {
                    report.Error(t.TableName, null, null, "읽기 실패: " + e.Message);
                    ok = false;
                }
            }
            return ok;
        }

        /// <summary>부팅·핫 리로드 검증(07 §7.1.8). 표는 이미 로드되어 있어야 한다.</summary>
        public static MvValidationReport ValidateRuntime(MvTables tables, MvValidationOptions options = null)
        {
            if (tables == null) throw new ArgumentNullException(nameof(tables));
            var report = new MvValidationReport();
            Validate(tables, report, options ?? new MvValidationOptions());
            return report;
        }

        static void Validate(MvTables t, MvValidationReport r, MvValidationOptions o)
        {
            foreach (var table in t.All) CheckFormat(t, table, r, o);
            foreach (var table in t.All) CheckReferences(t, table, r, o);
            CheckConsumeTypeTable(t, r);
            CheckRunConfigKeys(t, r);
            CheckSemantics(t, r, o);
            CheckLoot(t, r);
            if (o.Localization != null && o.Localization.IsReady) CheckNames(t, r, o.Localization);
        }

        internal static IReadOnlyList<string> FirstLine(string text)
        {
            if (string.IsNullOrEmpty(text)) return null;
            int end = text.IndexOfAny(new[] { '\r', '\n' });
            return (end < 0 ? text : text.Substring(0, end)).Split('\t');
        }

        static string RowId(IMvTable table, TSVRow row) => row.Get(table.Schema.IdColumn) ?? "?";

        // ================= 7.13.1 형식 =================

        static void CheckFormat(MvTables t, IMvTable table, MvValidationReport r, MvValidationOptions o)
        {
            var s = table.Schema;
            var name = table.TableName;
            var rows = table.RawRows;
            var headers = o.Headers?.Invoke(name);

            if (headers != null)
            {
                var seen = new HashSet<string>(StringComparer.Ordinal);
                foreach (var h in headers)
                {
                    if (!seen.Add(h)) r.Error(name, null, h, "같은 열 이름이 두 번 있습니다");
                    else if (h.StartsWith("#")) r.Warn(name, null, h, "주석 열이 남아 있습니다(내보내기 도구가 빼야 합니다)");
                    else if (h.Length > 0 && s.Find(h) == null && !IsConsumeColumn(s, h)) r.Warn(name, null, h, "열 정의에 없는 열입니다");
                }
            }

            foreach (var c in s.Columns)
            {
                if (!c.Required) continue;
                bool present = headers != null ? headers.Contains(c.Name) : rows.Count == 0 || rows[0].Contains(c.Name);
                if (!present) r.Error(name, null, c.Name, "필수 열이 없습니다");
            }

            if (s.SingleRow && rows.Count != 1) r.Error(name, null, null, $"정확히 1행이어야 합니다(지금 {rows.Count}행)");

            var ids = new HashSet<string>(StringComparer.Ordinal);
            var keys = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
            foreach (var row in rows)
            {
                var id = RowId(table, row);
                if (!ids.Add(id)) r.Error(name, id, s.IdColumn, "id가 중복됩니다");
                if (!s.FreeIds && !id.StartsWith(s.IdPrefix, StringComparison.Ordinal))
                    r.Error(name, id, s.IdColumn, $"id가 표 번호 `{s.IdPrefix}`로 시작하지 않습니다");

                foreach (var c in s.Columns)
                {
                    var raw = row.Get(c.Name);
                    if (string.IsNullOrEmpty(raw))
                    {
                        if (c.Required && (headers == null || headers.Contains(c.Name))) r.Error(name, id, c.Name, "필수 값이 비었습니다");
                        continue;
                    }
                    if (raw != raw.Trim()) r.Error(name, id, c.Name, "값 앞뒤에 공백이 있습니다");
                    var err = TypeError(c, raw);
                    if (err != null) r.Error(name, id, c.Name, err);
                    if (c.Kind == MvColKind.Key)
                    {
                        var scope = s.KeyPerGroup && s.GroupColumn != null ? c.Name + "@" + row.Get(s.GroupColumn) : c.Name;
                        if (!keys.TryGetValue(scope, out var set)) keys.Add(scope, set = new HashSet<string>(StringComparer.Ordinal));
                        if (!set.Add(raw)) r.Error(name, id, c.Name, $"key `{raw}`가 중복됩니다");
                    }
                }

                foreach (var cc in s.Consumes) CheckConsumeFormat(name, id, row, cc, r);
            }
        }

        static bool IsConsumeColumn(MvSchema s, string header)
        {
            foreach (var cc in s.Consumes)
            {
                var p = cc.Prefix;
                if (header == p + "Type" || header == p + "Id") return true;
                if (cc.Range ? header == p + "Value_min" || header == p + "Value_max" : header == p + "Value") return true;
                if (header == p + "ValuePerZone" || header == p + "ValueRule") return true;
            }
            return false;
        }

        /// <summary>원문 문자열 타입 확인(07 §7.1.2). 오류면 문구, 맞으면 null.</summary>
        internal static string TypeError(MvColumn c, string raw)
        {
            switch (c.Kind)
            {
                case MvColKind.Int:
                case MvColKind.Fk:
                case MvColKind.FkGroup:
                    return int.TryParse(raw, NumberStyles.Integer, C, out _) ? null : $"`{raw}`는 정수가 아닙니다";
                case MvColKind.Long:
                    return long.TryParse(raw, NumberStyles.Integer, C, out _) ? null : $"`{raw}`는 정수가 아닙니다";
                case MvColKind.Float:
                    return IsNumber(raw) ? null : $"`{raw}`는 숫자가 아닙니다";
                case MvColKind.Bool01:
                    return raw == "0" || raw == "1" ? null : $"`{raw}`는 0/1이 아닙니다";
                case MvColKind.Key:
                    return s_key.IsMatch(raw) ? null : $"key `{raw}`에 영문·숫자·`.`·`_` 밖의 글자가 있습니다";
                case MvColKind.Enum:
                    return EnumOk(c.EnumType, raw) ? null : $"`{raw}`는 {c.EnumType.Name} 값이 아닙니다";
                case MvColKind.Color:
                    return s_color.IsMatch(raw) ? null : $"`{raw}`는 #rrggbb 색이 아닙니다";
                case MvColKind.ColorList:
                    foreach (var p in raw.Split('/'))
                        if (!s_color.IsMatch(p.Trim())) return $"`{p}`는 #rrggbb 색이 아닙니다";
                    return null;
                case MvColKind.IntList:
                case MvColKind.FkList:
                    foreach (var p in raw.Split('/'))
                        if (!int.TryParse(p.Trim(), NumberStyles.Integer, C, out _)) return $"목록 값 `{p}`는 정수가 아닙니다";
                    return null;
                case MvColKind.FloatList:
                    foreach (var p in raw.Split('/'))
                        if (!IsNumber(p.Trim())) return $"목록 값 `{p}`는 숫자가 아닙니다";
                    return null;
                default:
                    return null;
            }
        }

        static bool IsNumber(string raw) =>
            double.TryParse(raw, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, C, out var v) && !double.IsNaN(v) && !double.IsInfinity(v);

        static bool EnumOk(Type enumType, string raw)
        {
            if (raw.Length == 0 || char.IsDigit(raw[0]) || raw[0] == '-' || raw[0] == '+') return false;
            foreach (var n in Enum.GetNames(enumType))
                if (string.Equals(n, raw, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        static void CheckConsumeFormat(string table, string id, TSVRow row, MvConsumeColumns cc, MvValidationReport r)
        {
            var p = cc.Prefix;
            if (cc.Range) return; // 801·802 묶음 규칙은 CheckLoot에서.
            if (!ConsumeData.TryParse(row.Get(p + "Type"), row.Get(p + "Id"), row.Get(p + "Value"),
                    cc.RunExtension ? row.Get(p + "ValuePerZone") : null, cc.RunExtension ? row.Get(p + "ValueRule") : null,
                    out _, out var error))
                r.Error(table, id, p, "consumeData: " + error);
            if (!cc.RunExtension && (!string.IsNullOrEmpty(row.Get(p + "ValuePerZone")) || !string.IsNullOrEmpty(row.Get(p + "ValueRule"))))
                r.Error(table, id, p, "ValuePerZone·ValueRule은 런 보상 열에만 씁니다(07 §7.1.7)");
        }

        // ================= 7.13.2 참조 =================

        static void CheckReferences(MvTables t, IMvTable table, MvValidationReport r, MvValidationOptions o)
        {
            var s = table.Schema;
            var name = table.TableName;
            foreach (var row in table.RawRows)
            {
                var id = RowId(table, row);
                foreach (var c in s.Columns)
                {
                    var raw = row.Get(c.Name);
                    if (string.IsNullOrEmpty(raw) || TypeError(c, raw) != null) continue;
                    switch (c.Kind)
                    {
                        case MvColKind.Fk:
                        case MvColKind.FkGroup:
                        {
                            int v = int.Parse(raw, C);
                            if (v == 0) break;
                            var target = t.Find(c.Target);
                            if (target == null) { r.Error(name, id, c.Name, $"대상 표 {c.Target}가 없습니다"); break; }
                            bool found = c.Kind == MvColKind.Fk ? target.HasId(v) : target.HasGroup(v);
                            if (!found) r.Error(name, id, c.Name, $"{c.Target}에 {(c.Kind == MvColKind.Fk ? "id" : "묶음")} {v}가 없습니다");
                            break;
                        }
                        case MvColKind.FkList:
                        {
                            var target = t.Find(c.Target);
                            foreach (var part in raw.Split('/'))
                            {
                                int v = int.Parse(part.Trim(), C);
                                if (target == null || !target.HasId(v)) r.Error(name, id, c.Name, $"{c.Target}에 id {v}가 없습니다");
                            }
                            break;
                        }
                        case MvColKind.FkKey:
                        {
                            var target = t.Find(c.Target);
                            if (target == null || !target.HasKey(raw)) r.Error(name, id, c.Name, $"{c.Target}에 key `{raw}`가 없습니다");
                            break;
                        }
                        case MvColKind.Sound:
                            if (!t.Sound.HasKey(raw)) r.Error(name, id, c.Name, $"03_sound에 key `{raw}`가 없습니다");
                            break;
                        case MvColKind.Effect:
                            if (!t.Effect.HasKey(raw)) r.Error(name, id, c.Name, $"04_effect에 key `{raw}`가 없습니다");
                            break;
                        case MvColKind.RunConfigKey:
                            if (c.Prefix)
                            {
                                if (!t.RunConfig.Has(raw + ".amp") || !t.RunConfig.Has(raw + ".time"))
                                    r.Error(name, id, c.Name, $"300_runConfig에 `{raw}.amp`·`{raw}.time`이 없습니다");
                            }
                            else if (!t.RunConfig.Has(raw)) r.Error(name, id, c.Name, $"300_runConfig에 key `{raw}`가 없습니다");
                            break;
                        case MvColKind.Res:
                            CheckResource(t, name, id, c, raw, r, o.Resources);
                            break;
                    }
                }

                foreach (var cc in s.Consumes)
                {
                    if (cc.Range) continue;
                    if (!ConsumeData.TryParse(row.Get(cc.Prefix + "Type"), row.Get(cc.Prefix + "Id"), row.Get(cc.Prefix + "Value"),
                            null, null, out var list, out _)) continue;
                    foreach (var d in list) CheckConsumeRef(t, name, id, cc.Prefix, d.TypeId, d.Id, s.AllowRunConsume, r);
                }
            }

            // 04_effect children은 같은 표 key 목록.
            if (table == t.Effect)
                foreach (var e in t.Effect.Datas)
                    foreach (var child in e.children)
                        if (!t.Effect.HasKey(child)) r.Error(name, e.id, "children", $"04_effect에 key `{child}`가 없습니다");
        }

        static void CheckResource(MvTables t, string table, string id, MvColumn c, string raw, MvValidationReport r, IMvResourceCatalog catalog)
        {
            if (catalog == null) return; // 카탈로그 전(MV-TOOL-ART·MV-ART-ATLAS): 건너뜀(TaskBoard MV-DATA 남김).
            foreach (var part in raw.Split('/'))
            {
                var nameValue = part.Trim();
                if (c.Atlas == MvAtlas.Theme)
                {
                    foreach (var atlas in catalog.ThemeAtlases)
                        if (!catalog.Has(atlas, nameValue)) r.Error(table, id, c.Name, $"{atlas}에 `{nameValue}`가 없습니다");
                }
                else if (!catalog.Has(c.Atlas, nameValue)) r.Error(table, id, c.Name, $"{c.Atlas}에 `{nameValue}`가 없습니다");
            }
        }

        /// <summary>consumeData 1항목의 type·id 확인(07 §7.1.6, §7.13.2).</summary>
        internal static void CheckConsumeRef(MvTables t, string table, string id, string column, int type, int consumeId, bool runTable, MvValidationReport r)
        {
            var def = t.ConsumeType.Get(type);
            if (def == null) { r.Error(table, id, column, $"consumeType {type}가 07_consumeType에 없습니다"); return; }
            if (!def.active) { r.Error(table, id, column, $"consumeType {type}는 active 0입니다"); return; }
            if (type >= 600 && type < 700 && !runTable && !s_metaRunTypes.Contains(type))
                r.Error(table, id, column, $"런 전용 consumeType {type}를 런 밖 표에 썼습니다");

            if (!def.UsesId)
            {
                if (consumeId != 0) r.Error(table, id, column, $"consumeType {type}는 id를 쓰지 않습니다(0이어야 함, 지금 {consumeId})");
                return;
            }
            if (consumeId == 0 && s_zeroIdAllowed.Contains(type)) return;
            var target = t.Find(def.idTable);
            if (target == null)
            {
                r.Warn(table, id, column, $"consumeType {type}의 idTable {def.idTable}가 아직 없어 id {consumeId}를 확인하지 못했습니다");
                return;
            }
            bool found = def.idKind == IdKind.group ? target.HasGroup(consumeId) : target.HasId(consumeId);
            if (!found) r.Error(table, id, column, $"{def.idTable}에 {(def.idKind == IdKind.group ? "묶음" : "id")} {consumeId}가 없습니다");
        }

        static void CheckConsumeTypeTable(MvTables t, MvValidationReport r)
        {
            const string name = ConsumeTypeTable.Name;
            var enumValues = (ConsumeType[])Enum.GetValues(typeof(ConsumeType));
            foreach (var row in t.ConsumeType.Datas)
            {
                if (!Enum.IsDefined(typeof(ConsumeType), row.typeId))
                    r.Error(name, row.typeId, "typeId", "C# ConsumeType enum에 없는 값입니다");
                else if (((ConsumeType)row.typeId).ToString() != row.codeName)
                    r.Error(name, row.typeId, "codeName", $"C# enum 이름 {(ConsumeType)row.typeId}와 다릅니다");

                if (row.UsesId == (row.idKind == IdKind.none))
                    r.Error(name, row.typeId, "idKind", "idTable `-`이면 idKind none, 표가 있으면 row/group이어야 합니다");
                if (row.UsesId && row.idKind == IdKind.group && row.idTable != DropData.Table && row.idTable != RandomBoxData.Table)
                    r.Error(name, row.typeId, "idKind", "group은 801_drop·802_randomBox만 씁니다");
                if (row.FixedIcon == null && row.iconColumn.Length > 0 && !row.UsesId)
                    r.Error(name, row.typeId, "iconColumn", "id 표가 없으면 `=스프라이트이름`으로 적어야 합니다");
                if (!string.IsNullOrEmpty(row.settleTo) && row.settleTo != "rbPending")
                {
                    var parts = row.settleTo.Split('/');
                    if (parts.Length != 2 || !int.TryParse(parts[0], NumberStyles.Integer, C, out int st) || !int.TryParse(parts[1], NumberStyles.Integer, C, out int sid))
                        r.Error(name, row.typeId, "settleTo", $"`{row.settleTo}`는 `type/id` 형식이 아닙니다");
                    else CheckConsumeRef(t, name, row.typeId.ToString(C), "settleTo", st, sid, false, r);
                }
            }
            foreach (var v in enumValues)
                if (v != ConsumeType.None && t.ConsumeType.Get((int)v) == null)
                    r.Error(name, (int)v, "typeId", $"C# ConsumeType.{v}에 해당하는 행이 없습니다");
        }

        static void CheckRunConfigKeys(MvTables t, MvValidationReport r)
        {
            var name = RunConfigData.Table;
            var code = new HashSet<string>(RunConfigKeys.All, StringComparer.Ordinal);
            foreach (var row in t.RunConfig.Datas)
            {
                if (!code.Contains(row.key)) r.Error(name, row.id, "key", $"코드 상수(RunConfigKeys)에 없는 키 `{row.key}`입니다");
                if (row.unit == "color")
                {
                    if (!s_color.IsMatch(row.value)) r.Error(name, row.id, "value", $"`{row.value}`는 색이 아닙니다");
                }
                else
                {
                    foreach (var part in row.value.Split('/'))
                        if (!IsNumber(part.Trim())) { r.Error(name, row.id, "value", $"`{row.value}`는 숫자(또는 `/` 목록)가 아닙니다"); break; }
                }
            }
            foreach (var key in RunConfigKeys.All)
                if (!t.RunConfig.Has(key)) r.Error(name, null, "key", $"코드 상수 `{key}`의 행이 없습니다");
        }

        static void CheckNames(MvTables t, MvValidationReport r, ITableLocalization loc)
        {
            foreach (var table in t.All)
            {
                if (table.Schema.Find("active") == null) continue;
                foreach (var row in table.RawRows)
                {
                    var activeRaw = row.Get("active");
                    if (activeRaw == "0") continue;
                    var id = RowId(table, row);
                    if (loc.GetString(TableData.NAME_COLLECTION, id) == null)
                        r.Warn(table.TableName, id, "name", $"문구 `name:{id}`가 없습니다");
                }
            }
        }
    }
}
