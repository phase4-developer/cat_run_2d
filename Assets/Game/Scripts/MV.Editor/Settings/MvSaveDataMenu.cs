using UnityEditor;
using UnityEngine;

namespace MV.Editor.Settings
{
    /// <summary>
    /// 에디터 시험용: 이 프로젝트의 PlayerPrefs를 모두 지운다. 게임 저장(두 칸)과 로그인 정보가 모두 PlayerPrefs에 있어
    /// 지우고 Play하면 첫 실행(새 저장)부터 시험할 수 있다. EditorPrefs(에디터 설정)는 건드리지 않는다.
    /// </summary>
    public static class MvSaveDataMenu
    {
        const string MenuPath = "MoonVillage/저장 데이터/PlayerPrefs 모두 지우기";

        [MenuItem(MenuPath)]
        static void ClearAll()
        {
            if (!EditorUtility.DisplayDialog("PlayerPrefs 모두 지우기",
                    "이 프로젝트의 PlayerPrefs(게임 저장·로그인 정보)를 모두 지웁니다.\n되돌릴 수 없습니다. 계속할까요?",
                    "지우기", "취소"))
                return;

            PlayerPrefs.DeleteAll();
            PlayerPrefs.Save();
            Debug.Log("[MvSave] 에디터 메뉴로 PlayerPrefs를 모두 지웠습니다.");
        }

        /// <summary>플레이 중에는 실행 중인 게임이 다시 저장하므로 막는다.</summary>
        [MenuItem(MenuPath, true)]
        static bool ClearAllValidate() => !EditorApplication.isPlayingOrWillChangePlaymode;
    }
}
