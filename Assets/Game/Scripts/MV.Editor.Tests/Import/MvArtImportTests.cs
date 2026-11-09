using System.Collections.Generic;
using System.IO;
using System.Linq;
using MV.Editor.Import;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace MV.Editor.Tests
{
    /// <summary>추출 PNG 임포트 설정(기획서 03 §3.2.8, §3.8.2) — MV-ART-ATLAS 티켓 01.</summary>
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

            Assert.That(MvArtRules.TryParse("Assets/Game/Art/Source/Texture/Tex_God_Flame.png", out var tex), Is.True);
            Assert.That(tex.Kind, Is.EqualTo(MvArtKind.Texture));
            Assert.That(tex.Atlas, Is.Null);

            Assert.That(MvArtRules.TryParse("Assets/Game/Art/Source/manifest.json", out _), Is.False);
            Assert.That(MvArtRules.TryParse("Assets/Game/UI/Some.png", out _), Is.False);
        }

        [Test]
        public void AllArtPngs_MatchImportRules()
        {
            var assets = MvArtSetup.FindArtAssets();
            Assert.That(assets.Count, Is.GreaterThan(1000), "추출 PNG 수");
            var failures = new List<string>();
            int noManifest = 0;
            foreach (var asset in assets)
            {
                var importer = AssetImporter.GetAtPath(asset.AssetPath) as TextureImporter;
                if (importer == null)
                {
                    failures.Add(asset.AssetPath + ": TextureImporter 없음");
                    continue;
                }
                var desired = MvArtImport.Desired(asset);
                if (desired.FromDefaults) noManifest++;
                var diff = MvArtImport.Differences(importer, desired);
                if (diff.Count > 0) failures.Add(asset.AssetPath + ": " + string.Join(", ", diff));
            }
            TestContext.WriteLine($"PNG {assets.Count}, manifest 없음 {noManifest}");
            Assert.That(failures, Is.Empty, string.Join("\n", failures.Take(30)));
            Assert.That(noManifest, Is.Zero, "manifest에 없는 PNG(기본값 적용)");
        }

        [Test]
        public void ImportSettings_FollowSpecTable()
        {
            // 월드: Sprite Single, Tight, PPU 128, Mipmap·Read/Write·물리 모양 끔, 개별 압축 없음
            var world = Importer("Assets/Game/Art/Source/Sprite/Atlas_World_Theme_market", out var worldSettings);
            Assert.That(world.textureType, Is.EqualTo(TextureImporterType.Sprite));
            Assert.That(world.spriteImportMode, Is.EqualTo(SpriteImportMode.Single));
            Assert.That(worldSettings.spriteMeshType, Is.EqualTo(SpriteMeshType.Tight));
            Assert.That(world.spritePixelsPerUnit, Is.EqualTo(128f));
            Assert.That(world.mipmapEnabled, Is.False);
            Assert.That(world.isReadable, Is.False);
            Assert.That(worldSettings.spriteGenerateFallbackPhysicsShape, Is.False);
            Assert.That(world.textureCompression, Is.EqualTo(TextureImporterCompression.Uncompressed));
            Assert.That(world.wrapMode, Is.EqualTo(TextureWrapMode.Clamp));
            Assert.That(world.filterMode, Is.EqualTo(FilterMode.Bilinear));

            // UI: Full Rect
            var ui = Importer("Assets/Game/Art/Source/Sprite/Atlas_UI_Icon", out var uiSettings);
            Assert.That(uiSettings.spriteMeshType, Is.EqualTo(SpriteMeshType.FullRect));
            Assert.That(ui.alphaIsTransparency, Is.True);
            Assert.That(ui.sRGBTexture, Is.True);

            // 로비: PPU 160(03 §3.3.2)
            var lobby = Importer("Assets/Game/Art/Source/Sprite/Atlas_World_Lobby", out _);
            Assert.That(lobby.spritePixelsPerUnit, Is.EqualTo(160f));

            // 단독 텍스처: Full Rect, Android·iOS ASTC 4x4(신 초상), Mipmap 끔
            var tex = (TextureImporter)AssetImporter.GetAtPath("Assets/Game/Art/Source/Texture/Tex_God_Flame.png");
            Assert.That(tex, Is.Not.Null);
            Assert.That(tex.mipmapEnabled, Is.False);
            foreach (var platform in new[] { "Android", "iPhone" })
            {
                var p = tex.GetPlatformTextureSettings(platform);
                Assert.That(p.overridden, Is.True, platform);
                Assert.That(p.format, Is.EqualTo(TextureImporterFormat.ASTC_4x4), platform);
            }
        }

        [Test]
        public void ManifestPivot_IsAppliedToFrames()
        {
            const string path = "Assets/Game/Art/Source/Sprite/Atlas_World_Cat_T1/World_Cat_P0_T1_Climb_00.png";
            var entry = MvArtManifest.Find("Source", "Sprite/Atlas_World_Cat_T1/World_Cat_P0_T1_Climb_00.png");
            Assert.That(entry, Is.Not.Null, "manifest 프레임 항목");
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            var s = new TextureImporterSettings();
            importer.ReadTextureSettings(s);
            Assert.That(s.spriteAlignment, Is.EqualTo((int)SpriteAlignment.Custom));
            Assert.That(s.spritePivot.x, Is.EqualTo(entry.Pivot.x).Within(1e-4f));
            Assert.That(s.spritePivot.y, Is.EqualTo(entry.Pivot.y).Within(1e-4f));
            Assert.That(entry.Pivot.y, Is.LessThan(0.5f), "Feet 피벗은 아래쪽");
        }

        [Test]
        public void SpriteCatalog_MatchesPngFiles()
        {
            var files = new HashSet<string>();
            foreach (var pair in MvArtSetup.FindAtlasFolders())
            foreach (var folder in pair.Value)
            foreach (var f in Directory.GetFiles(folder, "*.png", SearchOption.AllDirectories))
                files.Add(pair.Key + "/" + Path.GetFileNameWithoutExtension(f));
            foreach (var root in MvArtRules.ArtRoots)
            {
                var dir = root + "/" + MvArtRules.TextureFolderName;
                if (Directory.Exists(dir))
                    foreach (var f in Directory.GetFiles(dir, "*.png", SearchOption.AllDirectories))
                        files.Add("Texture/" + Path.GetFileNameWithoutExtension(f));
            }

            var catalog = new HashSet<string>(MvArtManifest.ReadCatalog().Select(r => r.atlas + "/" + r.name));
            Assert.That(catalog.Except(files).ToList(), Is.Empty, "카탈로그에만 있는 이름(PNG 없음)");
            Assert.That(files.Except(catalog).ToList(), Is.Empty, "PNG만 있고 카탈로그에 없는 이름");
        }

        static TextureImporter Importer(string folder, out TextureImporterSettings settings)
        {
            var file = Directory.GetFiles(folder, "*.png").OrderBy(f => f).First().Replace('\\', '/');
            var importer = (TextureImporter)AssetImporter.GetAtPath(file);
            Assert.That(importer, Is.Not.Null, file);
            settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            return importer;
        }
    }
}
