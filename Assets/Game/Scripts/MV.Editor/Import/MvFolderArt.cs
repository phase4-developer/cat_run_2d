using System;
using System.Collections.Generic;
using System.IO;

namespace MV.Editor.Import
{
    /// <summary>
    /// 폴더 단위 Addressables 규칙(AGENTS.md: 그룹 = 최상단 분류 폴더, 주소 = 파일 이름, 하위 폴더는 정리용).
    /// 디자이너 배경·투사체·이펙트 프리팹처럼 파일을 그대로 주소로 쓰는 묶음에 쓴다. 개수·이름은 파일에서 읽는다.
    /// </summary>
    public static class MvFolderArt
    {
        /// <summary>주소(파일 이름, 확장자 제외) → 경로. 폴더 아래 모든 하위 폴더. 같은 주소가 둘 이상이면 <paramref name="duplicates"/>에 적고 먼저 찾은 것(경로 정렬)을 쓴다.</summary>
        public static SortedDictionary<string, string> Find(string folder, string pattern, List<string> duplicates = null)
        {
            var map = new SortedDictionary<string, string>(StringComparer.Ordinal);
            if (!Directory.Exists(folder)) return map;
            var files = Directory.GetFiles(folder, pattern, SearchOption.AllDirectories);
            Array.Sort(files, StringComparer.Ordinal);
            foreach (var file in files)
            {
                var path = file.Replace('\\', '/');
                var address = Path.GetFileNameWithoutExtension(path);
                if (map.TryGetValue(address, out var first)) { duplicates?.Add($"주소 {address} 중복: {first}, {path}"); continue; }
                map[address] = path;
            }
            return map;
        }

        /// <summary>배경 그룹 항목: `BG/Prefab/**/*.prefab`.</summary>
        public static SortedDictionary<string, string> BgEntries(List<string> duplicates = null) =>
            Find(MvArtRules.BgPrefabFolder, "*.prefab", duplicates);

        /// <summary>투사체 그룹 항목: `Projectile/Prefab/**/*.prefab` + `Projectile/Image/**/*.png`.</summary>
        public static SortedDictionary<string, string> ProjectileEntries(List<string> duplicates = null)
        {
            var map = Find(MvArtRules.ProjectilePrefabFolder, "*.prefab", duplicates);
            foreach (var pair in Find(MvArtRules.ProjectileImageFolder, "*.png", duplicates))
            {
                if (map.TryGetValue(pair.Key, out var first)) { duplicates?.Add($"주소 {pair.Key} 중복: {first}, {pair.Value}"); continue; }
                map[pair.Key] = pair.Value;
            }
            return map;
        }

        /// <summary>이펙트 그룹에 더할 항목: `Effect/Prefab/**/*.prefab`.</summary>
        public static SortedDictionary<string, string> EffectPrefabEntries(List<string> duplicates = null) =>
            Find(MvArtRules.EffectPrefabFolder, "*.prefab", duplicates);
    }
}
