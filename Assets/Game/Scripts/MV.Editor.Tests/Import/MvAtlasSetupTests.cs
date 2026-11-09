using System.Collections.Generic;
using System.IO;
using System.Linq;
using MV.Editor.Import;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEditor.U2D;

namespace MV.Editor.Tests
{
    /// <summary>SpriteAtlas v2 그룹·압축(03 §3.8.1, D-54)과 Addressables 주소·그룹(§3.8.5, D-05) — 티켓 02·03.</summary>
    public sealed class MvAtlasSetupTests
    {
        // 기획서 03 §3.8.1 표(최대 크기, Android·iOS 형식, Mipmap, 타이트, 패딩)와 §3.8.5 라벨을 코드와 따로 적어 대조한다.
        static readonly (string atlas, bool ui, int max, TextureImporterFormat format, bool mip, bool tight, int padding, string label)[] s_specTable =
        {
            ("Atlas_UI_Common", true, 2048, TextureImporterFormat.ASTC_4x4, false, false, 4, "ui.core"),
            ("Atlas_UI_Hud", true, 2048, TextureImporterFormat.ASTC_4x4, false, false, 4, "ui.core"),
            ("Atlas_UI_Icon", true, 2048, TextureImporterFormat.ASTC_4x4, false, false, 4, "ui.core"),
            ("Atlas_UI_Lobby", true, 2048, TextureImporterFormat.ASTC_4x4, false, false, 4, "ui.lobby"),
            ("Atlas_UI_Card", true, 2048, TextureImporterFormat.ASTC_4x4, false, false, 4, "ui.card"),
            ("Atlas_World_Theme_market", false, 2048, TextureImporterFormat.ASTC_6x6, true, true, 8, "theme.market"),
            ("Atlas_World_Theme_snowday", false, 2048, TextureImporterFormat.ASTC_6x6, true, true, 8, "theme.snowday"),
            ("Atlas_World_Cat_T8", false, 2048, TextureImporterFormat.ASTC_6x6, true, true, 8, "cat"),
            ("Atlas_World_Friend_T1", false, 2048, TextureImporterFormat.ASTC_6x6, true, true, 8, "cat"),
            ("Atlas_World_Lobby", false, 2048, TextureImporterFormat.ASTC_6x6, false, true, 4, "lobby"),
            ("Atlas_Fx_Common", false, 1024, TextureImporterFormat.ASTC_4x4, true, false, 8, "fx"),
        };

        [Test]
        public void AtlasSpec_FollowsSpecTable()
        {
            foreach (var row in s_specTable)
            {
                var s = MvArtRules.SpecFor(row.atlas);
                var at = row.atlas;
                Assert.That(s.IsUi, Is.EqualTo(row.ui), at);
                Assert.That(s.MaxSize, Is.EqualTo(row.max), at);
                Assert.That(s.Format, Is.EqualTo(row.format), at);
                Assert.That(s.Mipmap, Is.EqualTo(row.mip), at);
                Assert.That(s.Tight, Is.EqualTo(row.tight), at);
                Assert.That(s.Padding, Is.EqualTo(row.padding), at);
                Assert.That(s.Label, Is.EqualTo(row.label), at);
                Assert.That(s.AssetPath, Is.EqualTo((row.ui ? "Assets/Game/Addressables/Remote/UIAtlas/" : "Assets/Game/Addressables/Remote/WorldAtlas/") + at + ".spriteatlasv2"));
                Assert.That(s.Group, Is.EqualTo(row.ui ? "Remote_UIAtlas" : "Remote_WorldAtlas"), at);
            }
        }

        [Test]
        public void TextureRules_FollowSpecTable()
        {
            Assert.That(MvArtRules.TextureFormatFor("Tex_God_Thunder"), Is.EqualTo(TextureImporterFormat.ASTC_4x4));
            Assert.That(MvArtRules.TextureFormatFor("Tex_Lobby_Vignette"), Is.EqualTo(TextureImporterFormat.ASTC_4x4));
            Assert.That(MvArtRules.TextureFormatFor("Tex_Lobby_Room"), Is.EqualTo(TextureImporterFormat.ASTC_6x6));
            Assert.That(MvArtRules.TextureLabelFor("Tex_God_Flame"), Is.EqualTo("ui.god"));
            Assert.That(MvArtRules.TextureLabelFor("Tex_Lobby_Room"), Is.EqualTo("lobby"));
        }

        [Test]
        public void EveryArtFolder_HasSpriteAtlasV2_WithSpecSettings()
        {
            var folders = MvArtSetup.FindAtlasFolders();
            Assert.That(folders.Count, Is.GreaterThan(0));
            var failures = new List<string>();
            foreach (var pair in folders)
            {
                var spec = MvArtRules.SpecFor(pair.Key);
                if (!File.Exists(spec.AssetPath)) { failures.Add(spec.Name + ": 파일 없음 " + spec.AssetPath); continue; }

                var wanted = new HashSet<string>(pair.Value.Select(AssetDatabase.AssetPathToGUID));
                var packables = MvArtSetup.ReadPackableGuids(spec.AssetPath);
                if (!packables.SetEquals(wanted)) failures.Add($"{spec.Name}: packables {packables.Count} ≠ 폴더 {wanted.Count}");

                if (!(AssetImporter.GetAtPath(spec.AssetPath) is SpriteAtlasImporter importer)) { failures.Add(spec.Name + ": SpriteAtlasImporter 아님"); continue; }
                var diff = MvArtSetup.AtlasDifferences(importer, spec);
                if (diff.Count > 0) failures.Add(spec.Name + ": " + string.Join(", ", diff));
            }
            Assert.That(failures, Is.Empty, string.Join("\n", failures));
        }

        [Test]
        public void WorldThemeAtlas_ImporterValues()
        {
            var importer = (SpriteAtlasImporter)AssetImporter.GetAtPath("Assets/Game/Addressables/Remote/WorldAtlas/Atlas_World_Theme_market.spriteatlasv2");
            Assert.That(importer, Is.Not.Null);
            Assert.That(importer.includeInBuild, Is.False, "Include in Build 끔(Addressables가 로드)");
            Assert.That(importer.packingSettings.enableRotation, Is.False);
            Assert.That(importer.packingSettings.enableTightPacking, Is.True);
            Assert.That(importer.packingSettings.padding, Is.EqualTo(8));
            Assert.That(importer.textureSettings.generateMipMaps, Is.True);
            foreach (var platform in new[] { "Android", "iPhone" })
            {
                var p = importer.GetPlatformSettings(platform);
                Assert.That(p.overridden, Is.True, platform);
                Assert.That(p.format, Is.EqualTo(TextureImporterFormat.ASTC_6x6), platform);
                Assert.That(p.maxTextureSize, Is.EqualTo(2048), platform);
            }

            var icon = (SpriteAtlasImporter)AssetImporter.GetAtPath("Assets/Game/Addressables/Remote/UIAtlas/Atlas_UI_Icon.spriteatlasv2");
            Assert.That(icon, Is.Not.Null);
            Assert.That(icon.GetPlatformSettings("Android").format, Is.EqualTo(TextureImporterFormat.ASTC_4x4));
            Assert.That(icon.textureSettings.generateMipMaps, Is.False);
            Assert.That(icon.packingSettings.enableTightPacking, Is.False);
        }

        [Test]
        public void AddressableGroups_AreRemoteAndPackSeparately()
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            Assert.That(settings, Is.Not.Null, "AddressableAssetSettings");
            foreach (var name in MvArtRules.OwnedGroups)
            {
                var group = settings.FindGroup(name);
                Assert.That(group, Is.Not.Null, name);
                var bundled = group.GetSchema<BundledAssetGroupSchema>();
                Assert.That(bundled, Is.Not.Null, name);
                Assert.That(bundled.BuildPath.GetName(settings), Is.EqualTo(AddressableAssetSettings.kRemoteBuildPath), name);
                Assert.That(bundled.LoadPath.GetName(settings), Is.EqualTo(AddressableAssetSettings.kRemoteLoadPath), name);
                Assert.That(bundled.BundleMode, Is.EqualTo(BundledAssetGroupSchema.BundlePackingMode.PackSeparately), name);
                Assert.That(bundled.IncludeInBuild, Is.True, name);
            }
        }

        [Test]
        public void EveryAtlasAndTexture_HasAddressAndLabel()
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            var failures = new List<string>();
            foreach (var name in MvArtSetup.FindAtlasFolders().Keys)
            {
                var spec = MvArtRules.SpecFor(name);
                var entry = settings.FindAssetEntry(AssetDatabase.AssetPathToGUID(spec.AssetPath));
                if (entry == null) { failures.Add(name + ": 주소 없음"); continue; }
                if (entry.address != name) failures.Add($"{name}: 주소 {entry.address}");
                if (entry.parentGroup.Name != spec.Group) failures.Add($"{name}: 그룹 {entry.parentGroup.Name}");
                var labels = string.Join(",", entry.labels.OrderBy(l => l));
                if (labels != (spec.Label ?? "")) failures.Add($"{name}: 라벨 {labels} ≠ {spec.Label}");
            }
            foreach (var pair in MvArtSetup.FindTextures())
            {
                var entry = settings.FindAssetEntry(AssetDatabase.AssetPathToGUID(pair.Value));
                if (entry == null) { failures.Add(pair.Key + ": 주소 없음"); continue; }
                if (entry.address != pair.Key) failures.Add($"{pair.Key}: 주소 {entry.address}");
                if (entry.parentGroup.Name != MvArtRules.TextureGroup) failures.Add($"{pair.Key}: 그룹 {entry.parentGroup.Name}");
            }
            Assert.That(failures, Is.Empty, string.Join("\n", failures));

            var god = settings.FindAssetEntry(AssetDatabase.AssetPathToGUID("Assets/Game/Art/Source/Texture/Tex_God_Thunder.png"));
            Assert.That(god, Is.Not.Null);
            Assert.That(god.address, Is.EqualTo("Tex_God_Thunder"));
            Assert.That(god.labels, Does.Contain("ui.god"));
        }
    }
}
