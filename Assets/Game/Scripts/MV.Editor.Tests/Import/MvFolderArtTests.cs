using System.Collections.Generic;
using MV.Editor.Import;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.AddressableAssets;

namespace MV.Editor.Tests.Import
{
    /// <summary>MV-ART-2: 디자이너 배경·투사체·이펙트 프리팹 등록(그룹 = 최상단 폴더, 주소 = 파일 이름).</summary>
    public sealed class MvFolderArtTests
    {
        static void CheckGroup(SortedDictionary<string, string> entries, string group, List<string> failures)
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            foreach (var pair in entries)
            {
                var entry = settings.FindAssetEntry(AssetDatabase.AssetPathToGUID(pair.Value));
                if (entry == null) { failures.Add(pair.Key + ": 주소 없음"); continue; }
                if (entry.address != pair.Key) failures.Add($"{pair.Key}: 주소 {entry.address}");
                if (entry.parentGroup.Name != group) failures.Add($"{pair.Key}: 그룹 {entry.parentGroup.Name}(기대 {group})");
            }
        }

        [Test]
        public void FolderEntries_HaveNoDuplicateAddresses()
        {
            var dup = new List<string>();
            MvFolderArt.BgEntries(dup);
            MvFolderArt.ProjectileEntries(dup);
            MvFolderArt.EffectPrefabEntries(dup);
            Assert.That(dup, Is.Empty, string.Join("\n", dup));
        }

        [Test]
        public void BgProjectileEffectPrefabs_AreAddressedByFileName()
        {
            Assert.That(AddressableAssetSettingsDefaultObject.Settings, Is.Not.Null, "AddressableAssetSettings");
            var bg = MvFolderArt.BgEntries();
            var proj = MvFolderArt.ProjectileEntries();
            Assert.That(bg, Is.Not.Empty, "배경 프리팹 없음");
            Assert.That(proj, Is.Not.Empty, "투사체 리소스 없음");
            var failures = new List<string>();
            CheckGroup(bg, MvArtRules.BgGroup, failures);
            CheckGroup(proj, MvArtRules.ProjectileGroup, failures);
            CheckGroup(MvFolderArt.EffectPrefabEntries(), MvArtRules.EffectGroup, failures);
            Assert.That(failures, Is.Empty, string.Join("\n", failures));
        }

        /// <summary>MV-VIEW-PROJ-2: 103_item projSprite·projTail과 300 proj.prefab이 투사체 그룹에 등록된 주소다.</summary>
        [Test]
        public void ProjectileTableColumns_ReferenceProjectileGroup()
        {
            var tables = new MV.Data.MvTables();
            tables.LoadAll(new MV.Data.MvTsvDirectorySource(MV.Data.MvTableLoader.TsvDirectory));
            var proj = MvFolderArt.ProjectileEntries();
            var failures = new List<string>();
            void Check(string where, string address)
            {
                if (string.IsNullOrEmpty(address)) failures.Add(where + ": 빈칸");
                else if (!proj.ContainsKey(address)) failures.Add($"{where}: {address}가 투사체 폴더에 없음");
            }
            Assert.That(tables.Item.Datas, Is.Not.Empty, "103_item 행 없음");
            foreach (var row in tables.Item.Datas)
            {
                Check(row.id + ".projSprite", row.projSprite);
                Check(row.id + ".projTail", row.projTail);
            }
            Check("300 " + MV.Data.RunConfigKeys.Proj_Prefab, tables.RunConfig.Raw(MV.Data.RunConfigKeys.Proj_Prefab));
            Assert.That(failures, Is.Empty, string.Join("
", failures));
        }

        /// <summary>Addressables 전체에서 같은 주소가 두 항목에 붙지 않는다(주소 = 파일 이름 규칙의 충돌 검사).</summary>
        [Test]
        public void AllAddresses_AreUnique()
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            Assert.That(settings, Is.Not.Null, "AddressableAssetSettings");
            var seen = new Dictionary<string, string>();
            var failures = new List<string>();
            foreach (var group in settings.groups)
            {
                if (group == null) continue;
                foreach (var e in group.entries)
                {
                    if (seen.TryGetValue(e.address, out var other)) failures.Add($"{e.address}: {other}, {group.Name}");
                    else seen[e.address] = group.Name;
                }
            }
            Assert.That(failures, Is.Empty, string.Join("\n", failures));
        }
    }
}
