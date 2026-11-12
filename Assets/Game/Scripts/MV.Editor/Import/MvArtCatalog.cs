using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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

        public static MvArtCatalog FromProject()
        {
            var c = new MvArtCatalog();
            foreach (var pair in MvArtSetup.FindAtlasFolders())
            {
                var names = new HashSet<string>(StringComparer.Ordinal);
                foreach (var folder in pair.Value)
                foreach (var file in Directory.GetFiles(folder, "*.png", SearchOption.AllDirectories))
                    names.Add(Path.GetFileNameWithoutExtension(file));
                c._sprites[pair.Key] = names;
                if (File.Exists(MvArtRules.SpecFor(pair.Key).AssetPath)) c._atlasAssets.Add(pair.Key);
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
