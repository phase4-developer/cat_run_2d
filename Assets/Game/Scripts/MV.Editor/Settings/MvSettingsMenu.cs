using MV.Data;
using UnityEditor;

namespace MV.Editor.Settings
{
    /// <summary>설정 에셋을 메뉴에서 바로 연다(Project 창에서 경로를 찾지 않아도 되게). 선택만 하고 값은 바꾸지 않는다.</summary>
    public static class MvSettingsMenu
    {
        [MenuItem("MoonVillage/설정/카메라 설정")]
        static void OpenCamera() => Open(MvCameraSettings.AssetPath);

        [MenuItem("MoonVillage/설정/캐릭터 설정(이동·공격속도)")]
        static void OpenCharacter() => Open(MvCharacterSettings.AssetPath);

        static void Open(string path)
        {
            var asset = AssetDatabase.LoadMainAssetAtPath(path);
            if (asset == null)
            {
                EditorUtility.DisplayDialog("설정 에셋 없음", path + " 에셋을 찾지 못했습니다.", "확인");
                return;
            }
            Selection.activeObject = asset;
            EditorGUIUtility.PingObject(asset);
        }
    }
}
