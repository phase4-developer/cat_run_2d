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
            ("Atlas_World_Lobby_2", false, 2048, TextureImporterFormat.ASTC_6x6, false, true, 4, "lobby"),
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
                Assert.That(s.AssetPath, Is.EqualTo((row.ui ? "Assets/Game/Addressables/Local/UIAtlas/" : "Assets/Game/Addressables/Local/WorldAtlas/") + at + ".spriteatlasv2"));
                Assert.That(s.Group, Is.EqualTo(row.ui ? "Local_UIAtlas" : "Local_WorldAtlas"), at);
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

        /// <summary>
        /// 규칙 폴더마다 아틀라스 에셋이 있다. 패킹 대상·설정은 에셋(Inspector)이 정본이라 규칙값과 같을 필요가 없다(MV-UI2-ATLAS,
        /// 2026-11-18 사용자 「Unity Inspector에서 설정한 패킹 대상, Padding, Tight Packing, Include in Build 등을 자동으로 덮어쓰지 않습니다」).
        /// 규칙과 다른 값·빠진 규칙 폴더는 보고만 남긴다.
        /// </summary>
        [Test]
        public void EveryArtFolder_HasSpriteAtlasV2()
        {
            var folders = MvArtSetup.FindAtlasFolders();
            Assert.That(folders.Count, Is.GreaterThan(0));
            var failures = new List<string>();
            foreach (var pair in folders)
            {
                var spec = MvArtRules.SpecFor(pair.Key);
                if (!File.Exists(spec.AssetPath)) { failures.Add(spec.Name + ": 파일 없음 " + spec.AssetPath); continue; }
                if (!(AssetImporter.GetAtPath(spec.AssetPath) is SpriteAtlasImporter importer)) { failures.Add(spec.Name + ": SpriteAtlasImporter 아님"); continue; }

                var packables = MvArtSetup.ReadPackableGuids(spec.AssetPath);
                int missing = pair.Value.Count(p => !packables.Contains(AssetDatabase.AssetPathToGUID(p)));
                var diff = MvArtSetup.AtlasDifferences(importer, spec);
                if (missing > 0 || diff.Count > 0)
                    TestContext.WriteLine($"[보고] {spec.Name}: 빠진 규칙 폴더 {missing}, 규칙과 다른 값 {string.Join(", ", diff)}");
            }
            Assert.That(failures, Is.Empty, string.Join("\n", failures));
        }

        /// <summary>
        /// 완료 조건 1: 아틀라스를 손으로 고친 뒤 Art 파일을 다시 Import해도 그 값이 남는다. 실제 아틀라스 하나의 Padding·Include in Build·
        /// 패킹 대상을 바꾸고, 규칙 폴더의 PNG를 다시 Import(후처리 → <see cref="MvArtSetup.EnsureAll"/>)한 뒤 값을 본다. 끝나면 파일을 원래 바이트로 되돌린다.
        /// </summary>
        [Test]
        public void ManualAtlasEdit_SurvivesArtReimport()
        {
            var folders = MvArtSetup.FindAtlasFolders();
            Assert.That(folders.ContainsKey("Atlas_UI_Hud"), "Atlas_UI_Hud 규칙 폴더");
            var spec = MvArtRules.SpecFor("Atlas_UI_Hud");
            string metaPath = spec.AssetPath + ".meta";
            byte[] atlasBytes = File.ReadAllBytes(spec.AssetPath), metaBytes = File.ReadAllBytes(metaPath);
            string png = Directory.GetFiles(folders["Atlas_UI_Hud"][0], "*.png", SearchOption.AllDirectories).First().Replace('\\', '/');
            const string extra = "Assets/Game/Art/UI/GUI/Slot/Grade";
            string extraGuid = AssetDatabase.AssetPathToGUID(extra);
            Assert.That(extraGuid, Is.Not.Empty, extra);
            try
            {
                var importer = (SpriteAtlasImporter)AssetImporter.GetAtPath(spec.AssetPath);
                var pack = importer.packingSettings;
                int padding = pack.padding == 2 ? 8 : 2;
                pack.padding = padding;
                importer.packingSettings = pack;
                importer.includeInBuild = true;
                importer.SaveAndReimport();
                var atlas = AssetDatabase.LoadAssetAtPath<UnityEngine.U2D.SpriteAtlas>(spec.AssetPath);
                Assert.That(atlas, Is.Not.Null);
                var asset = SpriteAtlasAsset.Load(spec.AssetPath);
                asset.Add(new UnityEngine.Object[] { AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(extra) });
                SpriteAtlasAsset.Save(asset, spec.AssetPath);
                AssetDatabase.ImportAsset(spec.AssetPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);

                AssetDatabase.ImportAsset(png, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
                var r = MvArtSetup.EnsureAll();

                var after = (SpriteAtlasImporter)AssetImporter.GetAtPath(spec.AssetPath);
                Assert.That(after.packingSettings.padding, Is.EqualTo(padding), "Padding 유지\n" + r);
                Assert.That(after.includeInBuild, Is.True, "Include in Build 유지");
                Assert.That(MvArtSetup.ReadPackableGuids(spec.AssetPath), Does.Contain(extraGuid), "Inspector에서 넣은 패킹 대상(Art/UI) 유지");
                Assert.That(MvArtSetup.PackedPngPaths(spec.AssetPath).Any(p => p.StartsWith(extra + "/")), "패킹 대상 PNG 목록에 Art/UI 그림");
            }
            finally
            {
                File.WriteAllBytes(spec.AssetPath, atlasBytes);
                File.WriteAllBytes(metaPath, metaBytes);
                AssetDatabase.ImportAsset(spec.AssetPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            }
        }

        /// <summary>규칙값은 없는 아틀라스를 처음 만들 때만 쓴다: 규칙 표 자체(SpecFor)는 그대로 시험한다(<see cref="AtlasSpec_FollowsSpecTable"/>).</summary>
        [Test]
        public void EnsureAll_DoesNotWriteExistingAtlases()
        {
            var before = MvArtSetup.FindAtlasAssets().ToDictionary(p => p.Key, p => File.ReadAllText(p.Value) + File.ReadAllText(p.Value + ".meta"));
            var r = MvArtSetup.EnsureAll();
            Assert.That(r.AtlasSettingsFixed, Is.EqualTo(0), r.ToString());
            foreach (var p in MvArtSetup.FindAtlasAssets())
                if (before.TryGetValue(p.Key, out var text))
                    Assert.That(File.ReadAllText(p.Value) + File.ReadAllText(p.Value + ".meta"), Is.EqualTo(text), p.Key + " 바뀜");
        }

        [Test]
        public void AddressableGroups_AreLocalAndPackSeparately()
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            Assert.That(settings, Is.Not.Null, "AddressableAssetSettings");
            foreach (var name in MvArtRules.OwnedGroups)
            {
                var group = settings.FindGroup(name);
                Assert.That(group, Is.Not.Null, name);
                var bundled = group.GetSchema<BundledAssetGroupSchema>();
                Assert.That(bundled, Is.Not.Null, name);
                Assert.That(bundled.BuildPath.GetName(settings), Is.EqualTo(AddressableAssetSettings.kLocalBuildPath), name);
                Assert.That(bundled.LoadPath.GetName(settings), Is.EqualTo(AddressableAssetSettings.kLocalLoadPath), name);
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
