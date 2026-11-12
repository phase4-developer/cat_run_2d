using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEditor.U2D;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MV.Editor.Import
{
    /// <summary>한 번의 Ensure 결과.</summary>
    public sealed class MvEnsureResult
    {
        public int ReimportedTextures;
        public int AtlasesWritten;
        public int AtlasSettingsFixed;
        public int GroupsChanged;
        public int EntriesChanged;
        public readonly List<string> Notes = new List<string>();

        public bool Changed => ReimportedTextures + AtlasesWritten + AtlasSettingsFixed + GroupsChanged + EntriesChanged > 0;

        public override string ToString() =>
            $"PNG 재임포트 {ReimportedTextures}, 아틀라스 씀 {AtlasesWritten}, 아틀라스 설정 {AtlasSettingsFixed}, 그룹 {GroupsChanged}, 주소 {EntriesChanged}" +
            (Notes.Count > 0 ? "\n- " + string.Join("\n- ", Notes) : "");
    }

    /// <summary>
    /// 아틀라스·임포트·Addressables를 폴더 규칙에서 멱등으로 맞춘다(기획서 03 §3.2.8, §3.8). 제품 도구.
    /// 1) 추출 PNG 임포트 값 2) `Art/(Source|Temp)/Sprite/Atlas_*` 폴더마다 SpriteAtlas v2 3) Remote_UIAtlas·Remote_WorldAtlas·Remote_Texture 그룹(이름만 Remote, 경로는 Local — 사용자 「원격 에셋은 안쓸거니 다 로컬로」)과 주소·라벨.
    /// 같은 입력이면 아무것도 쓰지 않는다. 전역 설정(프로필·빌드 경로·카탈로그)과 Default Local Group은 건드리지 않는다.
    /// </summary>
    public static class MvArtSetup
    {
        const string LogTag = "[MvArtSetup]";
        static readonly Regex s_guid = new Regex(@"guid: ([0-9a-f]{32})", RegexOptions.CultureInvariant);
        static bool s_running;
        static bool s_scheduled;

        [MenuItem("MV/Art/Ensure Import·Atlas·Addressables")]
        static void EnsureFromMenu() => Debug.Log($"{LogTag} {EnsureAll()}");

        /// <summary>배치 진입점(`-executeMethod MV.Editor.Import.MvArtSetup.RunBatch`). 바뀐 것이 없을 때까지 최대 3회.</summary>
        public static void RunBatch()
        {
            for (int i = 0; i < 3; i++)
            {
                var r = EnsureAll();
                Debug.Log($"{LogTag} pass {i + 1}: {r}");
                if (!r.Changed) return;
                AssetDatabase.Refresh();
            }
        }

        /// <summary>
        /// 임포트 후처리가 부른다. 배치 모드(실행기 compile)는 Editor 갱신 틱이 없으므로 바로, 대화형은 다음 틱에 1번 돈다.
        /// </summary>
        internal static void RequestEnsure()
        {
            if (AssetDatabase.IsAssetImportWorkerProcess()) return;
            if (Application.isBatchMode)
            {
                var r = EnsureAll();
                if (r.Changed) Debug.Log($"{LogTag} {r}");
                return;
            }
            if (s_scheduled) return;
            s_scheduled = true;
            EditorApplication.delayCall += () =>
            {
                s_scheduled = false;
                var r = EnsureAll();
                if (r.Changed) Debug.Log($"{LogTag} {r}");
            };
        }

        public static MvEnsureResult EnsureAll()
        {
            var r = new MvEnsureResult();
            if (s_running) return r;
            s_running = true;
            try
            {
                MvArtManifest.Invalidate();
                EnsureImportSettings(r);
                EnsureAtlases(r);
                EnsureCharacterAtlases(r);
                EnsureAddressables(r);
                if (r.Changed) AssetDatabase.SaveAssets();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                r.Notes.Add("예외: " + e.Message);
            }
            finally
            {
                s_running = false;
            }
            return r;
        }

        // ---------------- 1. PNG 임포트 값 ----------------

        /// <summary>규칙에 맞는 모든 추출 PNG 경로(파일 시스템 기준, 정렬).</summary>
        public static List<MvArtAsset> FindArtAssets()
        {
            var list = new List<MvArtAsset>();
            foreach (var root in MvArtRules.ArtRoots)
            {
                if (!Directory.Exists(root)) continue;
                foreach (var file in Directory.GetFiles(root, "*.png", SearchOption.AllDirectories))
                    if (MvArtRules.TryParse(file.Replace('\\', '/'), out var asset)) list.Add(asset);
            }
            list.Sort((a, b) => string.CompareOrdinal(a.AssetPath, b.AssetPath));
            return list;
        }

        static void EnsureImportSettings(MvEnsureResult r)
        {
            int noManifest = 0;
            foreach (var asset in FindArtAssets())
            {
                if (!(AssetImporter.GetAtPath(asset.AssetPath) is TextureImporter importer)) continue; // 아직 임포트 전: 전처리가 붙인다
                var desired = MvArtImport.Desired(asset);
                if (desired.FromDefaults) noManifest++;
                if (MvArtImport.Differences(importer, desired).Count == 0) continue;
                MvArtImport.Apply(importer, desired);
                importer.SaveAndReimport();
                r.ReimportedTextures++;
            }
            if (noManifest > 0) r.Notes.Add($"manifest에 없는 PNG {noManifest}장(기본값 적용)");
        }

        // ---------------- 2. SpriteAtlas v2 ----------------

        /// <summary>아틀라스 이름 → 묶을 폴더(정식 먼저, 임시 다음). 빈 폴더는 뺀다.</summary>
        public static SortedDictionary<string, List<string>> FindAtlasFolders()
        {
            var map = new SortedDictionary<string, List<string>>(StringComparer.Ordinal);
            foreach (var root in MvArtRules.ArtRoots)
            {
                var spriteRoot = root + "/" + MvArtRules.SpriteFolderName;
                if (!Directory.Exists(spriteRoot)) continue;
                foreach (var dir in Directory.GetDirectories(spriteRoot))
                {
                    var name = Path.GetFileName(dir);
                    if (!name.StartsWith("Atlas_", StringComparison.Ordinal)) continue;
                    if (Directory.GetFiles(dir, "*.png", SearchOption.AllDirectories).Length == 0) continue;
                    if (!map.TryGetValue(name, out var folders)) map[name] = folders = new List<string>();
                    folders.Add(spriteRoot + "/" + name);
                }
            }
            return map;
        }

        /// <summary>.spriteatlasv2 파일이 가리키는 packable GUID(텍스트 직렬화에서 읽음).</summary>
        public static HashSet<string> ReadPackableGuids(string atlasPath)
        {
            var set = new HashSet<string>(StringComparer.Ordinal);
            if (!File.Exists(atlasPath)) return set;
            foreach (Match m in s_guid.Matches(File.ReadAllText(atlasPath, Encoding.UTF8))) set.Add(m.Groups[1].Value);
            return set;
        }

        static void EnsureAtlases(MvEnsureResult r)
        {
            foreach (var pair in FindAtlasFolders())
            {
                var spec = MvArtRules.SpecFor(pair.Key);
                var wanted = new HashSet<string>(pair.Value.Select(AssetDatabase.AssetPathToGUID).Where(g => !string.IsNullOrEmpty(g)), StringComparer.Ordinal);
                if (wanted.Count != pair.Value.Count)
                {
                    r.Notes.Add($"{spec.Name}: 폴더 GUID 없음(임포트 전) — 다음 회차");
                    continue;
                }

                if (!File.Exists(spec.AssetPath) || !ReadPackableGuids(spec.AssetPath).SetEquals(wanted))
                {
                    WriteAtlas(spec, pair.Value);
                    r.AtlasesWritten++;
                }

                if (!(AssetImporter.GetAtPath(spec.AssetPath) is SpriteAtlasImporter importer))
                {
                    r.Notes.Add($"{spec.Name}: SpriteAtlasImporter 없음 — 다음 회차");
                    continue;
                }
                if (AtlasDifferences(importer, spec).Count == 0) continue;
                ApplyAtlas(importer, spec);
                importer.SaveAndReimport();
                r.AtlasSettingsFixed++;
            }

            foreach (var folder in new[] { MvArtRules.UiAtlasFolder, MvArtRules.WorldAtlasFolder })
            {
                if (!Directory.Exists(folder)) continue;
                var known = FindAtlasFolders();
                foreach (var file in Directory.GetFiles(folder, "*" + MvArtRules.AtlasExtension))
                {
                    var name = Path.GetFileNameWithoutExtension(file);
                    if (!known.ContainsKey(name)) r.Notes.Add($"{name}: 그림 폴더 없는 아틀라스(지우지 않음)");
                }
            }
        }

        /// <summary>캐릭터 묶음마다 SpriteAtlas v2 1개(<see cref="MvCharacterArt.FindAtlasSets"/>). 묶을 것은 PNG 파일이다.</summary>
        static void EnsureCharacterAtlases(MvEnsureResult r)
        {
            var sets = MvCharacterArt.FindAtlasSets();
            if (sets.Count == 0) return;
            if (!AssetDatabase.IsValidFolder(MvArtRules.CharacterAtlasFolder))
                AssetDatabase.CreateFolder(MvArtRules.CharacterRoot, Path.GetFileName(MvArtRules.CharacterAtlasFolder));
            foreach (var pair in sets)
            {
                var spec = MvArtRules.CharacterSpecFor(pair.Key);
                var wanted = new HashSet<string>(pair.Value.Select(AssetDatabase.AssetPathToGUID).Where(g => !string.IsNullOrEmpty(g)), StringComparer.Ordinal);
                if (wanted.Count != pair.Value.Count)
                {
                    r.Notes.Add($"{spec.Name}: PNG GUID 없음(임포트 전) — 다음 회차");
                    continue;
                }
                if (!File.Exists(spec.AssetPath) || !ReadPackableGuids(spec.AssetPath).SetEquals(wanted))
                {
                    WriteAtlas(spec, pair.Value);
                    r.AtlasesWritten++;
                }
                if (!(AssetImporter.GetAtPath(spec.AssetPath) is SpriteAtlasImporter importer))
                {
                    r.Notes.Add($"{spec.Name}: SpriteAtlasImporter 없음 — 다음 회차");
                    continue;
                }
                if (AtlasDifferences(importer, spec).Count == 0) continue;
                ApplyAtlas(importer, spec);
                importer.SaveAndReimport();
                r.AtlasSettingsFixed++;
            }
            if (Directory.Exists(MvArtRules.CharacterAtlasFolder))
                foreach (var file in Directory.GetFiles(MvArtRules.CharacterAtlasFolder, "*" + MvArtRules.AtlasExtension))
                {
                    var name = Path.GetFileNameWithoutExtension(file);
                    if (!sets.ContainsKey(name)) r.Notes.Add($"{name}: 그림 없는 캐릭터 아틀라스(지우지 않음)");
                }
        }

        /// <summary>묶을 것(폴더 또는 PNG 경로)으로 아틀라스 파일을 새로 쓴다.</summary>
        static void WriteAtlas(MvAtlasSpec spec, List<string> folders)
        {
            var atlas = new SpriteAtlasAsset();
            try
            {
                var packables = folders.Select(f => AssetDatabase.LoadAssetAtPath<Object>(f)).Where(o => o != null).ToArray();
                atlas.Add(packables);
                SpriteAtlasAsset.Save(atlas, spec.AssetPath);
            }
            finally
            {
                Object.DestroyImmediate(atlas);
            }
            AssetDatabase.ImportAsset(spec.AssetPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
        }

        public static List<string> AtlasDifferences(SpriteAtlasImporter importer, MvAtlasSpec spec)
        {
            var d = new List<string>();
            var pack = importer.packingSettings;
            if (pack.enableRotation) d.Add("rotation on");
            if (pack.enableTightPacking != spec.Tight) d.Add($"tight {pack.enableTightPacking}");
            if (pack.padding != spec.Padding) d.Add($"padding {pack.padding}≠{spec.Padding}");
            var tex = importer.textureSettings;
            if (tex.generateMipMaps != spec.Mipmap) d.Add($"mipmap {tex.generateMipMaps}");
            if (!tex.sRGB) d.Add("sRGB off");
            if (tex.readable) d.Add("readable on");
            if (tex.filterMode != FilterMode.Bilinear) d.Add($"filter {tex.filterMode}");
            if (tex.anisoLevel != 1) d.Add($"aniso {tex.anisoLevel}");
            if (importer.includeInBuild) d.Add("includeInBuild on");
            var def = importer.GetPlatformSettings("DefaultTexturePlatform");
            if (def.maxTextureSize != spec.MaxSize) d.Add($"default maxSize {def.maxTextureSize}≠{spec.MaxSize}");
            foreach (var platform in MvArtImport.MobilePlatforms)
            {
                var p = importer.GetPlatformSettings(platform);
                if (!p.overridden || p.format != spec.Format || p.maxTextureSize != spec.MaxSize)
                    d.Add($"{platform} {(p.overridden ? p.format + "/" + p.maxTextureSize : "not overridden")}≠{spec.Format}/{spec.MaxSize}");
            }
            return d;
        }

        static void ApplyAtlas(SpriteAtlasImporter importer, MvAtlasSpec spec)
        {
            var pack = importer.packingSettings;
            pack.enableRotation = false;
            pack.enableTightPacking = spec.Tight;
            pack.padding = spec.Padding;
            pack.blockOffset = 1;
            importer.packingSettings = pack;

            var tex = importer.textureSettings;
            tex.generateMipMaps = spec.Mipmap;
            tex.sRGB = true;
            tex.readable = false;
            tex.filterMode = FilterMode.Bilinear;
            tex.anisoLevel = 1;
            importer.textureSettings = tex;

            importer.includeInBuild = false;

            var def = importer.GetPlatformSettings("DefaultTexturePlatform");
            def.maxTextureSize = spec.MaxSize;
            importer.SetPlatformSettings(def);
            foreach (var platform in MvArtImport.MobilePlatforms)
            {
                var p = importer.GetPlatformSettings(platform);
                p.overridden = true;
                p.maxTextureSize = spec.MaxSize;
                p.format = spec.Format;
                importer.SetPlatformSettings(p);
            }
        }

        // ---------------- 3. Addressables ----------------

        /// <summary>단독 텍스처 원본(이름 → 경로). 정식(Source)·주소 폴더가 임시(Temp)보다 먼저다.</summary>
        public static SortedDictionary<string, string> FindTextures()
        {
            var map = new SortedDictionary<string, string>(StringComparer.Ordinal);
            void Scan(string dir, bool overwrite)
            {
                if (!Directory.Exists(dir)) return;
                foreach (var file in Directory.GetFiles(dir, "*.png", SearchOption.AllDirectories))
                {
                    var name = Path.GetFileNameWithoutExtension(file);
                    if (overwrite || !map.ContainsKey(name)) map[name] = file.Replace('\\', '/');
                }
            }
            Scan(MvArtRules.TempRoot + "/" + MvArtRules.TextureFolderName, true);
            Scan(MvArtRules.SourceRoot + "/" + MvArtRules.TextureFolderName, true);
            Scan(MvArtRules.TextureFolder, true);
            return map;
        }

        /// <summary>그룹별로 있어야 할 (guid → 주소, 라벨).</summary>
        public static Dictionary<string, Dictionary<string, (string address, string label)>> DesiredEntries(List<string> notes = null)
        {
            var desired = MvArtRules.OwnedGroups.ToDictionary(g => g, g => new Dictionary<string, (string, string)>(StringComparer.Ordinal));
            foreach (var name in FindAtlasFolders().Keys)
            {
                var spec = MvArtRules.SpecFor(name);
                var guid = AssetDatabase.AssetPathToGUID(spec.AssetPath);
                if (string.IsNullOrEmpty(guid) || !File.Exists(spec.AssetPath)) { notes?.Add($"{name}: 아틀라스 자산 없음 — 주소 보류"); continue; }
                if (spec.Label == null) notes?.Add($"{name}: 라벨 규칙 없음(03 §3.8.5)");
                desired[spec.Group][guid] = (spec.Address, spec.Label);
            }
            foreach (var pair in FindTextures())
            {
                var guid = AssetDatabase.AssetPathToGUID(pair.Value);
                if (string.IsNullOrEmpty(guid)) { notes?.Add($"{pair.Key}: GUID 없음 — 주소 보류"); continue; }
                var label = MvArtRules.TextureLabelFor(pair.Key);
                if (label == null) notes?.Add($"{pair.Key}: 라벨 규칙 없음(03 §3.8.5)");
                desired[MvArtRules.TextureGroup][guid] = (pair.Key, label);
            }
            foreach (var name in MvCharacterArt.FindAtlasSets().Keys)
            {
                var spec = MvArtRules.CharacterSpecFor(name);
                var guid = AssetDatabase.AssetPathToGUID(spec.AssetPath);
                if (string.IsNullOrEmpty(guid) || !File.Exists(spec.AssetPath)) { notes?.Add($"{name}: 캐릭터 아틀라스 자산 없음 — 주소 보류"); continue; }
                desired[MvArtRules.CharacterGroup][guid] = (spec.Address, spec.Label);
            }
            foreach (var pair in MvCharacterArt.FindPrefabs())
            {
                var guid = AssetDatabase.AssetPathToGUID(pair.Value);
                if (string.IsNullOrEmpty(guid)) { notes?.Add($"{pair.Key}: 프리팹 GUID 없음 — 주소 보류"); continue; }
                desired[MvArtRules.CharacterGroup][guid] = (pair.Key, MvArtRules.CharacterLabel);
            }
            return desired;
        }

        static void EnsureAddressables(MvEnsureResult r)
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null)
            {
                r.Notes.Add("AddressableAssetSettings 없음 — 그룹·주소 보류(총괄 MV-00)");
                return;
            }

            var desired = DesiredEntries(r.Notes);
            bool settingsChanged = false;
            foreach (var groupName in MvArtRules.OwnedGroups)
            {
                var group = EnsureGroup(settings, groupName, r);
                var wanted = desired[groupName];

                foreach (var entry in group.entries.ToList())
                {
                    if (wanted.ContainsKey(entry.guid)) continue;
                    group.RemoveAssetEntry(entry, false);
                    r.EntriesChanged++;
                }

                foreach (var pair in wanted.OrderBy(p => p.Value.address, StringComparer.Ordinal))
                {
                    var (address, label) = pair.Value;
                    var entry = settings.FindAssetEntry(pair.Key);
                    bool changed = false;
                    if (entry == null || entry.parentGroup != group)
                    {
                        entry = settings.CreateOrMoveEntry(pair.Key, group, false, false);
                        changed = true;
                    }
                    if (entry.address != address)
                    {
                        entry.SetAddress(address, false);
                        changed = true;
                    }
                    foreach (var old in entry.labels.ToList())
                    {
                        if (old == label) continue;
                        entry.SetLabel(old, false, false, false);
                        changed = true;
                    }
                    if (label != null && !entry.labels.Contains(label))
                    {
                        if (!settings.GetLabels().Contains(label))
                        {
                            settings.AddLabel(label, false);
                            settingsChanged = true;
                        }
                        entry.SetLabel(label, true, true, false);
                        changed = true;
                    }
                    if (changed) r.EntriesChanged++;
                }
                if (r.EntriesChanged > 0) EditorUtility.SetDirty(group);
            }

            if (settingsChanged || r.EntriesChanged > 0 || r.GroupsChanged > 0)
            {
                settings.SetDirty(AddressableAssetSettings.ModificationEvent.BatchModification, null, true, true);
            }
        }

        static AddressableAssetGroup EnsureGroup(AddressableAssetSettings settings, string name, MvEnsureResult r)
        {
            var group = settings.FindGroup(name);
            if (group == null)
            {
                group = settings.CreateGroup(name, false, false, false, null, typeof(BundledAssetGroupSchema), typeof(ContentUpdateGroupSchema));
                r.GroupsChanged++;
            }

            bool changed = false;
            var bundled = group.GetSchema<BundledAssetGroupSchema>();
            if (bundled == null)
            {
                bundled = group.AddSchema<BundledAssetGroupSchema>(false);
                changed = true;
            }
            if (bundled.BuildPath.GetName(settings) != AddressableAssetSettings.kLocalBuildPath)
            {
                bundled.BuildPath.SetVariableByName(settings, AddressableAssetSettings.kLocalBuildPath);
                changed = true;
            }
            if (bundled.LoadPath.GetName(settings) != AddressableAssetSettings.kLocalLoadPath)
            {
                bundled.LoadPath.SetVariableByName(settings, AddressableAssetSettings.kLocalLoadPath);
                changed = true;
            }
            if (bundled.BundleMode != BundledAssetGroupSchema.BundlePackingMode.PackSeparately)
            {
                bundled.BundleMode = BundledAssetGroupSchema.BundlePackingMode.PackSeparately;
                changed = true;
            }
            if (!bundled.IncludeInBuild)
            {
                bundled.IncludeInBuild = true;
                changed = true;
            }

            var update = group.GetSchema<ContentUpdateGroupSchema>();
            if (update == null)
            {
                update = group.AddSchema<ContentUpdateGroupSchema>(false);
                changed = true;
            }
            if (update.StaticContent)
            {
                update.StaticContent = false;
                changed = true;
            }

            if (changed)
            {
                EditorUtility.SetDirty(bundled);
                EditorUtility.SetDirty(update);
                EditorUtility.SetDirty(group);
                r.GroupsChanged++;
            }
            return group;
        }
    }
}
