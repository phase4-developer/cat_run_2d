using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;

namespace MV.UI.Tests
{
    /// <summary>
    /// 시험용: 아틀라스 에셋(.spriteatlasv2)이 묶는 그림에 이름이 있는지. 아틀라스 에셋이 정본이라(MV-UI2-ATLAS) Inspector에서 넣은 폴더(예: Art/UI)도 본다.
    /// </summary>
    public static class HudArtTestKit
    {
        public const string UiAtlasFolder = "Assets/Game/Addressables/Local/UIAtlas";

        public static bool AtlasHas(string atlas, string sprite)
        {
            if (string.IsNullOrEmpty(sprite)) return false;
            string path = UiAtlasFolder + "/" + atlas + ".spriteatlasv2";
            if (!File.Exists(path)) return false;
            string text = File.ReadAllText(path);
            int at = text.IndexOf("packables:");
            if (at < 0) return false;
            foreach (Match m in Regex.Matches(text.Substring(at), @"guid: ([0-9a-f]{32})"))
            {
                string p = AssetDatabase.GUIDToAssetPath(m.Groups[1].Value);
                if (string.IsNullOrEmpty(p)) continue;
                if (Directory.Exists(p))
                {
                    if (Directory.GetFiles(p, sprite + ".png", SearchOption.AllDirectories).Any()) return true;
                }
                else if (Path.GetFileNameWithoutExtension(p) == sprite) return true;
            }
            return false;
        }
    }
}
