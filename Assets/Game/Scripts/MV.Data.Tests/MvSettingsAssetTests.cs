using System.IO;
using NUnit.Framework;
using UnityEditor;

namespace MV.Data.Tests
{
    /// <summary>
    /// 설정 에셋(MV-SETTINGS-LIVE): Inspector와 같은 길(`SerializedObject` 수정 → `OnValidate`)로 값을 바꾸면
    /// ① 런타임 사본(<see cref="MvTables.Character"/>)이 새 값으로 바뀌고 ② 저장하면 `.asset` 파일(Git 대상)에 값이 써진다.
    /// 사본을 바꿔도 에셋은 바뀌지 않는다. 시험은 끝에 파일 바이트를 원래대로 되돌린다.
    /// </summary>
    public sealed class MvSettingsAssetTests
    {
        [Test]
        public void CharacterAsset_InspectorEdit_RefreshesRuntimeCopy_CopyDoesNotWriteAsset()
        {
            var asset = MvCharacterSettings.Load();
            Assert.AreEqual(MvCharacterSettings.AssetPath, AssetDatabase.GetAssetPath(asset), "Resources로 읽은 에셋이 저장소 에셋이 아니다");
            var t = new MvTables();
            double original = t.Character.RunSpeed;
            int v0 = t.CharacterVersion;
            var so = new SerializedObject(asset);
            try
            {
                so.FindProperty("runSpeed").doubleValue = original + 1;
                so.ApplyModifiedPropertiesWithoutUndo();   // Unity가 OnValidate를 부른다(Inspector 수정과 같은 길)
                Assert.AreEqual(original + 1, t.Character.RunSpeed, 1e-12, "에셋 수정이 런타임 사본에 반영되지 않았다");
                Assert.Greater(t.CharacterVersion, v0);

                t.Character.RunSpeed = 999;   // 런타임 계산 쪽 변경
                Assert.AreEqual(original + 1, asset.runSpeed, 1e-12, "런타임 사본 변경이 에셋에 써졌다");
            }
            finally
            {
                so.Update();
                so.FindProperty("runSpeed").doubleValue = original;
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.ClearDirty(asset);
            }
            Assert.AreEqual(original, asset.runSpeed, 1e-12);
        }

        [TestCase(MvCharacterSettings.AssetPath, "runSpeed", "runSpeed: ")]
        [TestCase(MvCharacterSettings.AssetPath, "projSpeed", "projSpeed: ")]
        [TestCase(MvCameraSettings.AssetPath, "run.posY", "posY: ")]
        public void InspectorEdit_Save_WritesAssetFile(string path, string property, string yamlKey)
        {
            var asset = AssetDatabase.LoadMainAssetAtPath(path);
            Assert.IsNotNull(asset, path);
            byte[] originalBytes = File.ReadAllBytes(path);
            var so = new SerializedObject(asset);
            var prop = so.FindProperty(property);
            Assert.IsNotNull(prop, property);
            double original = prop.doubleValue;
            double edited = original + 0.5;
            try
            {
                prop.doubleValue = edited;
                so.ApplyModifiedPropertiesWithoutUndo();
                AssetDatabase.SaveAssetIfDirty(asset);   // Save Project·Ctrl+S 때 Unity가 하는 에셋 저장
                string text = File.ReadAllText(path);
                StringAssert.Contains(yamlKey + edited.ToString(System.Globalization.CultureInfo.InvariantCulture), text,
                    "저장한 값이 .asset 파일에 써지지 않았다");
            }
            finally
            {
                File.WriteAllBytes(path, originalBytes);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            }
            CollectionAssert.AreEqual(originalBytes, File.ReadAllBytes(path), "시험 뒤 .asset 파일이 원래대로 돌아오지 않았다");
            var reloaded = new SerializedObject(AssetDatabase.LoadMainAssetAtPath(path));
            Assert.AreEqual(original, reloaded.FindProperty(property).doubleValue, 1e-12, "다시 읽은 값이 원래 값이 아니다");
        }
    }
}
