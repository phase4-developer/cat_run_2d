#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace MV.UI
{
    /// <summary>
    /// 에디터 전용 재화 지급 창(MV-DEV-CHEAT-wealth). 메뉴 <c>MoonVillage/치트/재화 지급(골드·다이아)</c>.
    /// Play 중 메타가 열려 있을 때만 지급한다. 지급은 <see cref="MvWealthCheat.Grant"/>(테스트 패널과 같은 메타 요청)로 보낸다.
    /// 글자는 개발자용 에디터 도구라 언어팩 대상이 아니다(화면·표·토스트 문구 아님). 단위 버튼 값은 게임 규칙 값이 아니라 도구 편의 값이다.
    /// </summary>
    public sealed class MvWealthCheatWindow : EditorWindow
    {
        const string MenuPath = "MoonVillage/치트/재화 지급(골드·다이아)";
        static readonly long[] Steps = { 1000, 10000, 100000, 1000000 };

        [SerializeField] long amount = 1000;

        [MenuItem(MenuPath)]
        static void Open() => GetWindow<MvWealthCheatWindow>(false, "재화 치트", true).Show();

        void OnInspectorUpdate()
        {
            if (EditorApplication.isPlaying) Repaint(); // 잔액이 게임 안에서 바뀌어도 따라 그린다
        }

        void OnGUI()
        {
            var port = MvMetaUiPort.Instance;
            if (!EditorApplication.isPlaying)
            {
                EditorGUILayout.HelpBox("Play 중에만 지급할 수 있습니다.", MessageType.Info);
                return;
            }
            if (!port.Ready)
            {
                EditorGUILayout.HelpBox("메타(저장)가 아직 열리지 않았습니다. 로비가 뜬 뒤 다시 누르세요.", MessageType.Warning);
                return;
            }

            amount = EditorGUILayout.LongField("지급량", amount);
            EditorGUILayout.Space();

            foreach (string type in MvWealthCheat.Types)
            {
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    EditorGUILayout.LabelField(Label(type), MvWealthCheat.Balance(port, type).ToString("N0"), EditorStyles.boldLabel);
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        using (new EditorGUI.DisabledScope(amount <= 0))
                            if (GUILayout.Button("+" + amount.ToString("N0"))) Grant(port, type, amount);
                        foreach (long step in Steps)
                            if (GUILayout.Button("+" + step.ToString("N0"))) Grant(port, type, step);
                    }
                }
            }
        }

        static string Label(string type) => type switch
        {
            "gold" => "골드 (gold)",
            "dia" => "다이아 (dia)",
            _ => type,
        };

        static void Grant(IMetaUiPort port, string type, long value)
        {
            var r = MvWealthCheat.Grant(port, type, value);
            if (r.Ok) Debug.Log($"[MvWealthCheat] {type} +{value:N0} → {MvWealthCheat.Balance(port, type):N0}");
            else Debug.LogWarning($"[MvWealthCheat] {type} +{value:N0} 지급 실패: {r.Code}");
        }
    }
}
#endif
