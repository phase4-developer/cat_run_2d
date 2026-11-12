using System.Collections.Generic;
using System.IO;
using System.Linq;
using MV.Editor.Import;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.U2D;
using UnityEngine;

namespace MV.Editor.Tests.Import
{
    /// <summary>MV-ART-CHAR-1: 디자이너 캐릭터 리소스 등록(아틀라스·Addressables·임포트 값).</summary>
    public sealed class MvCharacterArtTests
    {
        [Test]
        public void AtlasNameFor_UsesFolderAndLastToken()
        {
            Assert.That(MvCharacterArt.AtlasNameFor("Player", "cat_run_A_0"), Is.EqualTo("atlas_player_0"));
            Assert.That(MvCharacterArt.AtlasNameFor("Player", "cat_happy_11"), Is.EqualTo("atlas_player_11"));
            Assert.That(MvCharacterArt.AtlasNameFor("Clone", "cat_run_B_siamese"), Is.EqualTo("atlas_clone_siamese"));
            Assert.That(MvCharacterArt.AtlasNameFor("Player", "noset"), Is.Null);
            Assert.That(MvCharacterArt.AtlasNameFor("Player", "trailing_"), Is.Null);
        }

        [Test]
        public void EveryCharacterPng_IsInOneAtlasSet()
        {
            var sets = MvCharacterArt.FindAtlasSets();
            Assert.That(sets, Is.Not.Empty, "캐릭터 이미지 없음");
            var pngs = Directory.GetFiles(MvArtRules.CharacterImageFolder, "*.png", SearchOption.AllDirectories).Select(p => p.Replace('\\', '/')).OrderBy(p => p).ToList();
            var packed = sets.Values.SelectMany(v => v).OrderBy(p => p).ToList();
            Assert.That(packed, Is.EqualTo(pngs));
        }

        [Test]
        public void CharacterPngImport_IsUncompressedSpriteWithResourcePivot()
        {
            var failures = new List<string>();
            foreach (var path in MvCharacterArt.FindAtlasSets().Values.SelectMany(v => v))
            {
                var ti = AssetImporter.GetAtPath(path) as TextureImporter;
                if (ti == null) { failures.Add(path + ": 임포터 없음"); continue; }
                if (ti.textureType != TextureImporterType.Sprite) failures.Add(path + ": Sprite 아님");
                if (ti.textureCompression != TextureImporterCompression.Uncompressed) failures.Add(path + ": 원본 압축됨(아틀라스가 압축)");
                var s = new TextureImporterSettings();
                ti.ReadTextureSettings(s);
                if (s.spriteAlignment != (int)SpriteAlignment.Custom) failures.Add(path + ": 피벗이 리소스 기준(Custom)이 아님");
            }
            Assert.That(failures, Is.Empty, string.Join("\n", failures));
        }

        [Test]
        public void CharacterAtlases_ExistWithSpecSettings()
        {
            var failures = new List<string>();
            foreach (var pair in MvCharacterArt.FindAtlasSets())
            {
                var spec = MvArtRules.CharacterSpecFor(pair.Key);
                var importer = AssetImporter.GetAtPath(spec.AssetPath) as SpriteAtlasImporter;
                if (importer == null) { failures.Add(pair.Key + ": 아틀라스 없음"); continue; }
                var diff = MvArtSetup.AtlasDifferences(importer, spec);
                if (diff.Count > 0) failures.Add(pair.Key + ": " + string.Join(", ", diff));
                var wanted = new HashSet<string>(pair.Value.Select(AssetDatabase.AssetPathToGUID));
                if (!MvArtSetup.ReadPackableGuids(spec.AssetPath).SetEquals(wanted)) failures.Add(pair.Key + ": 묶음 PNG 다름");
            }
            Assert.That(failures, Is.Empty, string.Join("\n", failures));
        }

        [Test]
        public void CharacterAtlasesAndPrefabs_AreAddressedInCharacterGroup()
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            Assert.That(settings, Is.Not.Null, "AddressableAssetSettings");
            var failures = new List<string>();
            void Check(string address, string path)
            {
                var entry = settings.FindAssetEntry(AssetDatabase.AssetPathToGUID(path));
                if (entry == null) { failures.Add(address + ": 주소 없음"); return; }
                if (entry.address != address) failures.Add($"{address}: 주소 {entry.address}");
                if (entry.parentGroup.Name != MvArtRules.CharacterGroup) failures.Add($"{address}: 그룹 {entry.parentGroup.Name}");
                if (!entry.labels.Contains(MvArtRules.CharacterLabel)) failures.Add($"{address}: 라벨 없음");
            }
            foreach (var name in MvCharacterArt.FindAtlasSets().Keys) Check(name, MvArtRules.CharacterSpecFor(name).AssetPath);
            var prefabs = MvCharacterArt.FindPrefabs();
            Assert.That(prefabs, Is.Not.Empty, "캐릭터 프리팹 없음");
            foreach (var pair in prefabs) Check(pair.Key, pair.Value);
            Assert.That(failures, Is.Empty, string.Join("\n", failures));
        }

        /// <summary>116_charLook의 프리팹·아틀라스·그림 이름이 등록된 캐릭터 리소스에 있다(그림은 그 행 아틀라스 묶음 안).</summary>
        [Test]
        public void CharLookRows_ReferenceRegisteredArt()
        {
            var tables = new MV.Data.MvTables();
            tables.LoadAll(new MV.Data.MvTsvDirectorySource(MV.Data.MvTableLoader.TsvDirectory));
            var sets = MvCharacterArt.FindAtlasSets();
            var prefabs = MvCharacterArt.FindPrefabs();
            var failures = new List<string>();
            Assert.That(tables.CharLook.Datas, Is.Not.Empty, "116_charLook 행 없음");
            foreach (var row in tables.CharLook.Datas)
            {
                if (!prefabs.ContainsKey(row.prefab)) failures.Add($"{row.id}: 프리팹 {row.prefab} 없음");
                if (!sets.TryGetValue(row.atlas, out var pngs)) { failures.Add($"{row.id}: 아틀라스 {row.atlas} 없음"); continue; }
                var names = new HashSet<string>(pngs.Select(Path.GetFileNameWithoutExtension));
                foreach (var image in new[] { row.walkA, row.walkB, row.atkA, row.atkB, row.happy, row.stair })
                    if (!names.Contains(image)) failures.Add($"{row.id}: 그림 {image}가 {row.atlas}에 없음");
            }
            Assert.That(failures, Is.Empty, string.Join("\n", failures));
        }

        [Test]
        public void CharacterPrefabs_RootAtOrigin()
        {
            foreach (var pair in MvCharacterArt.FindPrefabs())
            {
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(pair.Value);
                Assert.That(go, Is.Not.Null, pair.Key);
                Assert.That(go.transform.localPosition, Is.EqualTo(Vector3.zero), pair.Key);
            }
        }
    }
}
