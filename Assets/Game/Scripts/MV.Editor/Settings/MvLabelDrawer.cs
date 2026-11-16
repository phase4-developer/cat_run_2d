using MV.Data;
using UnityEditor;
using UnityEngine;

namespace MV.Editor.Settings
{
    /// <summary><see cref="MvLabelAttribute"/>의 한글 이름으로 그린다(툴팁은 필드의 [Tooltip] 그대로).</summary>
    [CustomPropertyDrawer(typeof(MvLabelAttribute))]
    public sealed class MvLabelDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var text = new GUIContent(((MvLabelAttribute)attribute).Text, label.tooltip);
            EditorGUI.PropertyField(position, property, text, true);
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label) =>
            EditorGUI.GetPropertyHeight(property, label, true);
    }
}
