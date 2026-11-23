using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using MV.Data;
using UnityEditor.AddressableAssets;

namespace MV.Editor.Import
{
    /// <summary>
    /// 프로젝트의 실제 상태로 만든 리소스 카탈로그(기획서 03 §3.8.6). 아틀라스별 스프라이트 이름은 그 아틀라스가 묶는 PNG 파일 이름,
    /// 주소는 Addressables 설정의 모든 항목. MV-DATA 검증기의 <see cref="IMvResourceCatalog"/>로도 넘길 수 있다.
    /// </summary>
    public sealed class MvArtCatalog : IMvResourceCatalog
    {
        readonly Dictionary<string, HashSet<string>> _sprites = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        readonly HashSet<string> _atlasAssets = new HashSet<string>(StringComparer.Ordinal);
        readonly Dictionary<string, string> _addresses = new Dictionary<string, string>(StringComparer.Ordinal);
        readonly List<string> _themeAtlases = new List<string>();

        public IReadOnlyDictionary<string, HashSet<string>> Sprites => _sprites;
        public IReadOnlyDictionary<string, string> Addresses => _addresses;

        /// <summary>`@theme` 확인 대상. 표를 아는 호출자가 <see cref="SetThemeAtlases"/>로 304_theme 값을 넣는다. 기본은 테마 폴더 전부.</summary>
        public IReadOnlyList<string> ThemeAtlases => _themeAtlases;

        static readonly Regex s_guid = new Regex(@"guid: ([0-9a-f]{32})", RegexOptions.CultureInvariant);

        // 읽기 전용 찾기 도우미(옛 MvArtSetup에서 옮김, MV-ART-IMPORTOWN 2026-11-23 자동화 제거). 아무 에셋도 쓰지 않는다.

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

        /// <summary>
        /// 아틀라스 에셋이 실제로 묶는 PNG 경로(패킹 대상이 폴더면 그 아래 PNG 전부, 파일이면 그 파일). 에셋이 정본이므로
        /// 규칙 폴더 밖(예: `Art/UI/...`)을 Inspector에서 넣은 것도 포함한다.
        /// </summary>
        public static List<string> PackedPngPaths(string atlasPath)
        {
            var list = new List<string>();
            if (!File.Exists(atlasPath)) return list;
            foreach (var guid in ReadPackableGuids(atlasPath))
            {
                string p = AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrEmpty(p)) continue;
                if (Directory.Exists(p))
                    list.AddRange(Directory.GetFiles(p, "*.png", SearchOption.AllDirectories).Select(f => f.Replace('\\', '/')));
                else if (p.EndsWith(".png", StringComparison.OrdinalIgnoreCase)) list.Add(p);
            }
            return list;
        }

        /// <summary>프로젝트의 모든 아틀라스 에셋(UI·월드·캐릭터 폴더): 이름(확장자 제외) → 경로.</summary>
        public static SortedDictionary<string, string> FindAtlasAssets()
        {
            var map = new SortedDictionary<string, string>(StringComparer.Ordinal);
            foreach (var folder in new[] { MvArtRules.UiAtlasFolder, MvArtRules.WorldAtlasFolder, MvArtRules.CharacterAtlasFolder })
            {
                if (!Directory.Exists(folder)) continue;
                foreach (var file in Directory.GetFiles(folder, "*" + MvArtRules.AtlasExtension))
                    map[Path.GetFileNameWithoutExtension(file)] = file.Replace('\\', '/');
            }
            return map;
        }

        public static MvArtCatalog FromProject()
        {
            var c = new MvArtCatalog();
            foreach (var pair in FindAtlasFolders())
            {
                var names = new HashSet<string>(StringComparer.Ordinal);
                foreach (var folder in pair.Value)
                foreach (var file in Directory.GetFiles(folder, "*.png", SearchOption.AllDirectories))
                    names.Add(Path.GetFileNameWithoutExtension(file));
                c._sprites[pair.Key] = names;
                if (File.Exists(MvArtRules.SpecFor(pair.Key).AssetPath)) c._atlasAssets.Add(pair.Key);
            }
            // 아틀라스 에셋이 정본(MV-UI2-ATLAS): Inspector에서 넣은 규칙 폴더 밖 그림(예: Art/UI)도 그 아틀라스의 스프라이트다.
            foreach (var pair in FindAtlasAssets())
            {
                if (!c._sprites.TryGetValue(pair.Key, out var names))
                    c._sprites[pair.Key] = names = new HashSet<string>(StringComparer.Ordinal);
                foreach (var png in PackedPngPaths(pair.Value)) names.Add(Path.GetFileNameWithoutExtension(png));
                c._atlasAssets.Add(pair.Key);
            }

            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings != null)
                foreach (var group in settings.groups.Where(g => g != null))
                foreach (var entry in group.entries)
                    if (!string.IsNullOrEmpty(entry.address)) c._addresses[entry.address] = group.Name;

            c._themeAtlases.AddRange(c._sprites.Keys.Where(a => MvArtRules.ThemeKeyOf(a) != null));
            return c;
        }

        /// <summary>시험용 빈 카탈로그에 값을 넣는다.</summary>
        public void AddSprite(string atlas, string name, bool atlasAssetAndAddress = true)
        {
            if (!_sprites.TryGetValue(atlas, out var set)) _sprites[atlas] = set = new HashSet<string>(StringComparer.Ordinal);
            set.Add(name);
            if (!atlasAssetAndAddress) return;
            _atlasAssets.Add(atlas);
            _addresses[atlas] = MvArtRules.SpecFor(atlas).Group;
        }

        public void AddAddress(string address, string group = "test") => _addresses[address] = group;

        public void SetThemeAtlases(IEnumerable<string> atlases)
        {
            _themeAtlases.Clear();
            _themeAtlases.AddRange(atlases.Where(a => !string.IsNullOrEmpty(a)).Distinct());
        }

        public bool HasAtlasFolder(string atlas) => atlas != null && _sprites.ContainsKey(atlas);
        public bool HasAtlasAsset(string atlas) => atlas != null && _atlasAssets.Contains(atlas);
        public bool HasAddress(string address) => address != null && _addresses.ContainsKey(address);

        /// <summary>정확한 이름 또는 프레임 묶음 접두(`name_00`).</summary>
        public bool HasSprite(string atlas, string name) =>
            atlas != null && _sprites.TryGetValue(atlas, out var set) && (set.Contains(name) || set.Contains(name + "_00"));

        public IEnumerable<string> AtlasesContaining(string name) =>
            _sprites.Where(p => p.Value.Contains(name) || p.Value.Contains(name + "_00")).Select(p => p.Key);

        /// <summary>대소문자만 다른 실제 이름(스프라이트·주소). 없으면 null.</summary>
        public string FindCaseInsensitive(string name)
        {
            foreach (var set in _sprites.Values)
            foreach (var n in set)
                if (string.Equals(n, name, StringComparison.OrdinalIgnoreCase)) return n;
            foreach (var a in _addresses.Keys)
                if (string.Equals(a, name, StringComparison.OrdinalIgnoreCase)) return a;
            return null;
        }

        /// <summary>아틀라스가 실제로 로드 가능한지: 그림 폴더 + .spriteatlasv2 + 주소.</summary>
        public bool IsAtlasLoadable(string atlas) => HasAtlasFolder(atlas) && HasAtlasAsset(atlas) && HasAddress(atlas);

        // IMvResourceCatalog
        public bool Has(string atlas, string name)
        {
            if (atlas == MvAtlas.Address) return HasAddress(name);
            if (atlas == MvAtlas.AtlasName) return IsAtlasLoadable(name);
            return IsAtlasLoadable(atlas) && HasSprite(atlas, name);
        }
    }
}
