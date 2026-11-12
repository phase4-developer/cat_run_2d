using System;
using System.Collections.Generic;
using System.IO;

namespace MV.Editor.Import
{
    /// <summary>
    /// 디자이너 캐릭터 리소스의 폴더 규칙(디자이너 PDF 「(신작)배경 리소스 관리 예상」, MV-SPEC-02 ArtResourcePlan §4·단계 1).
    /// `Local/Character/Image/&lt;폴더&gt;/*.png`를 파일 이름 마지막 토막(세트 번호·종족)으로 묶어 아틀라스 1개씩 만들고,
    /// `Local/Character/Prefab/*.prefab`은 파일 이름을 Addressables 주소로 쓴다. 세트·종족 수는 파일에서 세며 코드에 두지 않는다.
    /// </summary>
    public static class MvCharacterArt
    {
        /// <summary>아틀라스 이름 → 묶을 PNG 경로(정렬). 예: `Player/cat_run_A_0.png` → `atlas_player_0`, `Clone/cat_run_A_siamese.png` → `atlas_clone_siamese`.</summary>
        public static SortedDictionary<string, List<string>> FindAtlasSets()
        {
            var map = new SortedDictionary<string, List<string>>(StringComparer.Ordinal);
            if (!Directory.Exists(MvArtRules.CharacterImageFolder)) return map;
            foreach (var dir in Directory.GetDirectories(MvArtRules.CharacterImageFolder))
            {
                var folder = Path.GetFileName(dir);
                foreach (var file in Directory.GetFiles(dir, "*.png", SearchOption.AllDirectories))
                {
                    var name = AtlasNameFor(folder, Path.GetFileNameWithoutExtension(file));
                    if (name == null) continue;
                    if (!map.TryGetValue(name, out var list)) map[name] = list = new List<string>();
                    list.Add(file.Replace('\\', '/'));
                }
            }
            foreach (var list in map.Values) list.Sort(StringComparer.Ordinal);
            return map;
        }

        /// <summary>이미지 하위 폴더와 파일 이름으로 아틀라스 이름. 마지막 `_` 뒤 토막이 없으면 null.</summary>
        public static string AtlasNameFor(string folder, string fileName)
        {
            if (string.IsNullOrEmpty(folder) || string.IsNullOrEmpty(fileName)) return null;
            int at = fileName.LastIndexOf('_');
            if (at <= 0 || at == fileName.Length - 1) return null;
            return MvArtRules.CharacterAtlasPrefix + folder.ToLowerInvariant() + "_" + fileName.Substring(at + 1);
        }

        /// <summary>프리팹 주소(파일 이름) → 경로.</summary>
        public static SortedDictionary<string, string> FindPrefabs()
        {
            var map = new SortedDictionary<string, string>(StringComparer.Ordinal);
            if (!Directory.Exists(MvArtRules.CharacterPrefabFolder)) return map;
            foreach (var file in Directory.GetFiles(MvArtRules.CharacterPrefabFolder, "*.prefab", SearchOption.AllDirectories))
                map[Path.GetFileNameWithoutExtension(file)] = file.Replace('\\', '/');
            return map;
        }
    }
}
