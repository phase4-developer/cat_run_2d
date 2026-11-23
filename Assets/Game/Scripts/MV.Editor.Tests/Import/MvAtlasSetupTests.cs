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
    /// <summary>Addressables 그룹은 로컬·따로 묶기(D-05, 사용자 「원격은 언어팩만」). 아틀라스·주소 자동 생성 검사는 MV-ART-IMPORTOWN으로 뺐다.</summary>
    public sealed class MvAtlasSetupTests
    {

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
    }
}
