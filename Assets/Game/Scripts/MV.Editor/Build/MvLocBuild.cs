using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEditor.Localization;
using UnityEditor.Localization.Addressables;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;

namespace MV.Editor.Build
{
    /// <summary>
    /// 언어팩 빌드(MV-LOC, D-LOC-PLAN). 문구 원본은 Google Sheet 「MoonVillage Localization」(탭 = 컬렉션, 열 Key·로케일 코드).
    /// AI가 시트를 <see cref="SheetDir"/>에 탭별 <c>&lt;컬렉션&gt;.tsv</c> 사본으로 받고, 이 빌드가 사본으로
    /// LocalizationSettings·Locale·String Table 컬렉션과 Addressables 그룹 <see cref="GroupName"/>(레이블 <see cref="TableLabel"/>)을 만든다.
    /// 사본과 산출물은 손으로 고치지 않는다. 시트에 없는 키는 지운다(전체 교체, Framework authoring과 같은 의미).
    /// 중앙 실행기: <c>unity_job.py submit --project moonvillage --modes setup --setup-method MV.Editor.Build.MvLocBuild.BuildStringTablesJob</c>.
    /// </summary>
    public static class MvLocBuild
    {
        public const string Root = "Assets/Game/GameData/Localization";
        public const string SheetDir = Root + "/Sheet";
        public const string TablesDir = Root + "/Tables";
        public const string LocalesDir = Root + "/Locales";
        public const string SettingsPath = Root + "/LocalizationSettings.asset";
        public const string GroupRulesPath = Root + "/LocalizationGroupRules.asset";
        /// <summary>언어팩 그룹. 원격 덧씌우기는 서버가 생길 때 별도 계약(D-LOC-PLAN ④). 지금은 Local 경로로 앱에 넣는다.</summary>
        public const string GroupName = "Local_Localization";
        /// <summary>LocalizationManager가 String Table을 미리 불러올 때 찾는 레이블(Framework TABLE_LABEL).</summary>
        public const string TableLabel = "Localization";
        const string KeyHeader = "Key";
        const string LogPrefix = "[MvLocBuild] ";

        public sealed class Sheet
        {
            public string Collection;
            public List<string> Locales = new List<string>();
            /// <summary>키 순서대로. 값 배열은 <see cref="Locales"/> 순서.</summary>
            public List<KeyValuePair<string, string[]>> Rows = new List<KeyValuePair<string, string[]>>();
        }

        public static void BuildStringTablesJob()
        {
            int code;
            try { code = Build() ? 0 : 1; }
            catch (Exception e) { Debug.LogError(LogPrefix + "예외로 중단: " + e); code = 1; }
            Debug.Log(LogPrefix + "종료 코드 " + code);
            EditorApplication.Exit(code);
        }

        [MenuItem("MV/Localization/Build String Tables")]
        static void BuildMenu() => Build();

        /// <summary>사본을 모두 읽고 검사한 뒤 자산을 만든다. 검사 오류면 자산을 바꾸지 않고 false.</summary>
        public static bool Build()
        {
            var sheets = ReadSheets(SheetDir, out var errors);
            if (errors.Count > 0)
            {
                foreach (var e in errors) Debug.LogError(LogPrefix + e);
                return false;
            }
            var localeCodes = sheets[0].Locales;

            EnsureFolder(Root); EnsureFolder(TablesDir); EnsureFolder(LocalesDir);
            var aa = AddressableAssetSettingsDefaultObject.GetSettings(true);
            var group = EnsureGroup(aa);
            EnsureGroupRules(group);
            EnsureSettings();
            var locales = localeCodes.Select(EnsureLocale).ToList();

            foreach (var sheet in sheets)
                WriteCollection(sheet, locales, aa);

            aa.SetDirty(AddressableAssetSettings.ModificationEvent.BatchModification, null, true, true);
            AssetDatabase.SaveAssets();
            Debug.Log(LogPrefix + $"컬렉션 {sheets.Count}, 로케일 {string.Join("/", localeCodes)}, 키 {sheets.Sum(s => s.Rows.Count)}");
            return true;
        }

        /// <summary>사본 폴더의 <c>*.tsv</c>를 읽는다. 머리 = Key + 로케일 코드(모든 파일 같은 순서), 키 중복·빈 키 금지.</summary>
        public static List<Sheet> ReadSheets(string dir, out List<string> errors)
        {
            errors = new List<string>();
            var result = new List<Sheet>();
            if (!Directory.Exists(dir)) { errors.Add("사본 폴더 없음: " + dir); return result; }
            foreach (var path in Directory.GetFiles(dir, "*.tsv").OrderBy(p => p, StringComparer.Ordinal))
            {
                var sheet = ParseSheet(Path.GetFileNameWithoutExtension(path), File.ReadAllText(path, Encoding.UTF8), errors);
                if (sheet != null) result.Add(sheet);
            }
            if (result.Count == 0 && errors.Count == 0) errors.Add("사본 없음: " + dir);
            for (int i = 1; i < result.Count; i++)
                if (!result[i].Locales.SequenceEqual(result[0].Locales))
                    errors.Add($"{result[i].Collection}: 로케일 열 {string.Join("/", result[i].Locales)}이 {result[0].Collection}의 {string.Join("/", result[0].Locales)}와 다름");
            return result;
        }

        public static Sheet ParseSheet(string collection, string text, List<string> errors)
        {
            var lines = text.Replace("\r\n", "\n").TrimStart('﻿').Split('\n');
            if (lines.Length == 0 || string.IsNullOrEmpty(lines[0])) { errors.Add($"{collection}: 머리 줄 없음"); return null; }
            var head = lines[0].Split('\t');
            if (head[0] != KeyHeader) { errors.Add($"{collection}: 첫 열은 {KeyHeader}"); return null; }
            var sheet = new Sheet { Collection = collection };
            for (int c = 1; c < head.Length; c++)
            {
                string code = head[c].Trim();
                if (code.Length == 0 || sheet.Locales.Contains(code)) { errors.Add($"{collection}: 로케일 열 {c + 1} 비었거나 중복"); return null; }
                sheet.Locales.Add(code);
            }
            if (sheet.Locales.Count == 0) { errors.Add($"{collection}: 로케일 열 없음"); return null; }
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 1; i < lines.Length; i++)
            {
                if (lines[i].Length == 0) continue;
                var cells = lines[i].Split('\t');
                string key = cells[0].Trim();
                if (key.Length == 0) { errors.Add($"{collection}: {i + 1}행 빈 키"); continue; }
                if (!seen.Add(key)) { errors.Add($"{collection}: 키 중복 {key}"); continue; }
                if (cells.Length > head.Length) { errors.Add($"{collection}: {key} 열이 머리보다 많음"); continue; }
                var values = new string[sheet.Locales.Count];
                for (int c = 0; c < values.Length; c++) values[c] = c + 1 < cells.Length ? cells[c + 1] : "";
                sheet.Rows.Add(new KeyValuePair<string, string[]>(key, values));
            }
            return sheet;
        }

        static void WriteCollection(Sheet sheet, List<Locale> locales, AddressableAssetSettings aa)
        {
            EnsureFolder(TablesDir + "/" + sheet.Collection);
            var collection = LocalizationEditorSettings.GetStringTableCollection(sheet.Collection)
                             ?? LocalizationEditorSettings.CreateStringTableCollection(sheet.Collection, TablesDir + "/" + sheet.Collection, locales);
            foreach (var locale in locales)
                if (!collection.ContainsTable(locale.Identifier))
                    collection.AddNewTable(locale.Identifier);

            var keep = new HashSet<string>(sheet.Rows.Select(r => r.Key), StringComparer.Ordinal);
            foreach (var stale in collection.SharedData.Entries.Select(e => e.Key).Where(k => !keep.Contains(k)).ToList())
                collection.RemoveEntry(stale);

            for (int li = 0; li < locales.Count; li++)
            {
                var table = (StringTable)collection.GetTable(locales[li].Identifier);
                foreach (var row in sheet.Rows)
                {
                    var entry = table.GetEntry(row.Key);
                    string value = row.Value[li];
                    if (entry == null) table.AddEntry(row.Key, value);
                    else if (entry.Value != value) entry.Value = value;
                }
                EditorUtility.SetDirty(table);
            }
            EditorUtility.SetDirty(collection.SharedData);
            EditorUtility.SetDirty(collection);

            if (!collection.IsPreloadTableFlagSet()) collection.SetPreloadTableFlag(true);
            collection.RefreshAddressables();
            aa.AddLabel(TableLabel);
            foreach (var table in collection.StringTables)
            {
                var entry = aa.FindAssetEntry(AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(table)));
                if (entry == null) { Debug.LogError(LogPrefix + $"{sheet.Collection}/{table.LocaleIdentifier.Code} Addressables 항목 없음"); continue; }
                entry.SetLabel(TableLabel, true, true, false);
            }
        }

        static void EnsureSettings()
        {
            var settings = LocalizationEditorSettings.ActiveLocalizationSettings;
            if (settings != null) return;
            settings = AssetDatabase.LoadAssetAtPath<LocalizationSettings>(SettingsPath);
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<LocalizationSettings>();
                AssetDatabase.CreateAsset(settings, SettingsPath);
            }
            LocalizationEditorSettings.ActiveLocalizationSettings = settings;
        }

        static Locale EnsureLocale(string code)
        {
            var locale = LocalizationEditorSettings.GetLocale(new LocaleIdentifier(code));
            if (locale != null) return locale;
            string path = $"{LocalesDir}/{code}.asset";
            locale = AssetDatabase.LoadAssetAtPath<Locale>(path);
            if (locale == null)
            {
                locale = Locale.CreateLocale(new LocaleIdentifier(code));
                AssetDatabase.CreateAsset(locale, path);
            }
            LocalizationEditorSettings.AddLocale(locale);
            return locale;
        }

        /// <summary>Locale·공유 데이터·String Table을 모두 한 그룹에 넣는다(그룹 = 최상단 분류, 10 §10.6).</summary>
        static void EnsureGroupRules(AddressableAssetGroup group)
        {
            var rules = AssetDatabase.LoadAssetAtPath<AddressableGroupRules>(GroupRulesPath);
            if (rules == null)
            {
                rules = ScriptableObject.CreateInstance<AddressableGroupRules>();
                AssetDatabase.CreateAsset(rules, GroupRulesPath);
            }
            rules.LocaleResolver = new GroupResolver(group);
            rules.StringTablesResolver = new GroupResolver(group);
            rules.AssetTablesResolver = new GroupResolver(group);
            rules.AssetResolver = new GroupResolver(group);
            EditorUtility.SetDirty(rules);
            AssetDatabase.SaveAssetIfDirty(rules);
            AddressableGroupRules.Instance = rules;
        }

        static AddressableAssetGroup EnsureGroup(AddressableAssetSettings aa)
        {
            var group = aa.FindGroup(GroupName)
                        ?? aa.CreateGroup(GroupName, false, false, false, null, typeof(BundledAssetGroupSchema), typeof(ContentUpdateGroupSchema));
            var bundled = group.GetSchema<BundledAssetGroupSchema>() ?? group.AddSchema<BundledAssetGroupSchema>(false);
            bundled.BuildPath.SetVariableByName(aa, AddressableAssetSettings.kLocalBuildPath);
            bundled.LoadPath.SetVariableByName(aa, AddressableAssetSettings.kLocalLoadPath);
            bundled.BundleMode = BundledAssetGroupSchema.BundlePackingMode.PackSeparately;
            bundled.IncludeInBuild = true;
            EditorUtility.SetDirty(bundled);
            EditorUtility.SetDirty(group);
            return group;
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
