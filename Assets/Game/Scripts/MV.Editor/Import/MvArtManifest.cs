using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace MV.Editor.Import
{
    /// <summary>manifest의 그림 1장(프레임 묶음은 프레임마다 1개로 펼친다).</summary>
    public sealed class MvManifestEntry
    {
        public string Name;
        public string Atlas;
        /// <summary>뿌리 이름 + `/` + file(`Source/Sprite/Atlas_UI_Icon/UI_Icon_Gold.png`).</summary>
        public string Key;
        public Vector2 Pivot;
        public float Ppu;
        /// <summary>9-slice `[L, B, R, T]` px(03 §3.2.6) = Unity spriteBorder (x, y, z, w).</summary>
        public Vector4 Border;
    }

    /// <summary>
    /// MV-TOOL-ART 출력(`Art/Source/manifest.json`, `Art/Temp/manifest.json`, `sprite_catalog.tsv`, `temp_art_list.tsv`) 읽기.
    /// 파일이 바뀌면 다시 읽는다(쓰기 시각 비교).
    /// </summary>
    public static class MvArtManifest
    {
        [Serializable]
        sealed class FileJson
        {
            public ItemJson[] items;
        }

        [Serializable]
        sealed class ItemJson
        {
            public string name;
            public string atlas;
            public string file;
            public float[] pivot;
            public float ppu;
            public float[] border;
            public FrameJson[] frames;
        }

        [Serializable]
        sealed class FrameJson
        {
            public string file;
            public string name;
        }

        static Dictionary<string, MvManifestEntry> s_entries;
        static DateTime s_sourceStamp, s_tempStamp;

        /// <summary>뿌리 이름과 file로 찾는다. 없으면 null.</summary>
        public static MvManifestEntry Find(string root, string file)
        {
            EnsureLoaded();
            return s_entries.TryGetValue(root + "/" + file, out var e) ? e : null;
        }

        public static IReadOnlyCollection<MvManifestEntry> All
        {
            get
            {
                EnsureLoaded();
                return s_entries.Values;
            }
        }

        public static void Invalidate() => s_entries = null;

        static void EnsureLoaded()
        {
            var sourceStamp = Stamp(MvArtRules.SourceManifestPath);
            var tempStamp = Stamp(MvArtRules.TempManifestPath);
            if (s_entries != null && sourceStamp == s_sourceStamp && tempStamp == s_tempStamp) return;

            var entries = new Dictionary<string, MvManifestEntry>(StringComparer.Ordinal);
            Read(MvArtRules.SourceManifestPath, MvArtRules.SourceRootName, entries);
            Read(MvArtRules.TempManifestPath, MvArtRules.TempRootName, entries);
            s_entries = entries;
            s_sourceStamp = sourceStamp;
            s_tempStamp = tempStamp;
        }

        static DateTime Stamp(string path) => File.Exists(path) ? File.GetLastWriteTimeUtc(path) : DateTime.MinValue;

        static void Read(string path, string root, Dictionary<string, MvManifestEntry> into)
        {
            if (!File.Exists(path)) return;
            FileJson json;
            try
            {
                json = JsonUtility.FromJson<FileJson>(File.ReadAllText(path, Encoding.UTF8));
            }
            catch (Exception e)
            {
                Debug.LogError($"[MvArtManifest] {path} 읽기 실패: {e.Message}");
                return;
            }
            if (json?.items == null) return;

            foreach (var item in json.items)
            {
                var pivot = item.pivot != null && item.pivot.Length >= 2 ? new Vector2(item.pivot[0], item.pivot[1]) : new Vector2(0.5f, 0.5f);
                var border = item.border != null && item.border.Length >= 4 ? new Vector4(item.border[0], item.border[1], item.border[2], item.border[3]) : Vector4.zero;
                if (!string.IsNullOrEmpty(item.file)) Add(into, root, item.file, item.name, item, pivot, border);
                if (item.frames == null) continue;
                foreach (var frame in item.frames)
                    if (!string.IsNullOrEmpty(frame.file)) Add(into, root, frame.file, frame.name, item, pivot, border);
            }
        }

        static void Add(Dictionary<string, MvManifestEntry> into, string root, string file, string name, ItemJson item, Vector2 pivot, Vector4 border)
        {
            var key = root + "/" + file.Replace('\\', '/');
            into[key] = new MvManifestEntry { Name = name, Atlas = item.atlas, Key = key, Pivot = pivot, Ppu = item.ppu, Border = border };
        }

        /// <summary>`sprite_catalog.tsv`의 (atlas, name, temp) 행. 없으면 빈 목록.</summary>
        public static List<(string atlas, string name, bool temp)> ReadCatalog()
        {
            var list = new List<(string, string, bool)>();
            foreach (var cells in ReadTsv(MvArtRules.CatalogPath))
                if (cells.Length >= 2) list.Add((cells[0], cells[1], cells.Length >= 3 && cells[2] == "1"));
            return list;
        }

        /// <summary>`temp_art_list.tsv`의 이름 → 갈래(`A` 미추출, `C` 임시 아트, `NAME_CASE` 표 값 대소문자).</summary>
        public static Dictionary<string, string> ReadTempList()
        {
            var map = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var cells in ReadTsv(MvArtRules.TempListPath))
                if (cells.Length >= 3) map[cells[0]] = cells[2];
            return map;
        }

        static IEnumerable<string[]> ReadTsv(string path)
        {
            if (!File.Exists(path)) yield break;
            var lines = File.ReadAllText(path, Encoding.UTF8).Replace("\r\n", "\n").Split('\n');
            for (int i = 1; i < lines.Length; i++)
                if (lines[i].Length > 0) yield return lines[i].Split('\t');
        }
    }
}
