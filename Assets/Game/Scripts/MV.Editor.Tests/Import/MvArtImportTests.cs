using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using MV.Editor.Import;
using NUnit.Framework;

namespace MV.Editor.Tests
{
    /// <summary>아트 폴더 규칙 읽기(MvArtRules.TryParse). Import 설정 자동 적용·검사는 MV-ART-IMPORTOWN(2026-11-23 사용자 「자동화는 하지마」)으로 뺐다.</summary>
    public sealed class MvArtImportTests
    {
        [Test]
        public void ArtRules_ParseFolderRules()
        {
            Assert.That(MvArtRules.TryParse("Assets/Game/Art/Source/Sprite/Atlas_UI_Icon/UI_Icon_Gold.png", out var ui), Is.True);
            Assert.That(ui.Kind, Is.EqualTo(MvArtKind.Ui));
            Assert.That(ui.Atlas, Is.EqualTo("Atlas_UI_Icon"));
            Assert.That(ui.Name, Is.EqualTo("UI_Icon_Gold"));
            Assert.That(ui.File, Is.EqualTo("Sprite/Atlas_UI_Icon/UI_Icon_Gold.png"));

            Assert.That(MvArtRules.TryParse("Assets/Game/Art/Temp/Sprite/Atlas_World_Theme_snow/World_Enemy_BigMouse_03.png", out var world), Is.True);
            Assert.That(world.Kind, Is.EqualTo(MvArtKind.World));
            Assert.That(world.IsTemp, Is.True);

            // 로비 `_2` 나눔(03 §3.8.1)도 로비 PPU 160
            Assert.That(MvArtRules.TryParse("Assets/Game/Art/Source/Sprite/Atlas_World_Lobby_2/Lobby_Fac_Tower_1.png", out var lobby2), Is.True);
            Assert.That(MvArtRules.DefaultPpu(lobby2), Is.EqualTo(MvArtRules.LobbyPpu));

            Assert.That(MvArtRules.TryParse("Assets/Game/Art/Source/Texture/Tex_God_Flame.png", out var tex), Is.True);
            Assert.That(tex.Kind, Is.EqualTo(MvArtKind.Texture));
            Assert.That(tex.Atlas, Is.Null);

            Assert.That(MvArtRules.TryParse("Assets/Game/Art/Source/manifest.json", out _), Is.False);
            Assert.That(MvArtRules.TryParse("Assets/Game/UI/Some.png", out _), Is.False);
        }

        /// <summary>
        /// UI 프리팹은 아이콘·카드 아틀라스 그림을 직접 참조하지 않는다. 데이터(07_consumeType·15_grade 등)나 AddressableImage 키로만 연결한다
        /// (MV-UI-ICONSRC, 사용자 2026-11-23 「직접 가리키면 안되는거야 … 아이콘들은 다 데이터가 존재하는 정보잖아」). 월드 프리팹(BG)은 범위 밖.
        /// </summary>
        [Test]
        public void UiPrefabs_DoNotReferenceIconOrCardSpritesDirectly()
        {
            var guids = new Dictionary<string, string>();
            foreach (var root in new[] { "Assets/Game/Art/Source/Sprite/Atlas_UI_Icon", "Assets/Game/Art/Source/Sprite/Atlas_UI_Card" })
                foreach (var png in Directory.GetFiles(root, "*.png", SearchOption.AllDirectories))
                {
                    var m = Regex.Match(File.ReadAllText(png + ".meta"), @"guid: (\w+)");
                    if (m.Success) guids[m.Groups[1].Value] = Path.GetFileNameWithoutExtension(png);
                }
            var hits = new List<string>();
            foreach (var dir in new[] { "Assets/Game/Art/UI/Prefab", "Assets/Game/Addressables/Local/UI" })
                foreach (var prefab in Directory.GetFiles(dir, "*.prefab", SearchOption.AllDirectories))
                    foreach (Match m in Regex.Matches(File.ReadAllText(prefab), @"\{fileID: 21300000, guid: (\w+)"))
                        if (guids.TryGetValue(m.Groups[1].Value, out var name)) hits.Add(prefab.Replace('\\', '/') + " → " + name);
            Assert.That(hits, Is.Empty, string.Join("\n", hits.Distinct()));
        }
    }
}
