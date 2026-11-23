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
    }
}
