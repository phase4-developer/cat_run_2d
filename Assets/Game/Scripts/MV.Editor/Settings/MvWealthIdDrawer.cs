using System.Collections.Generic;
using jjevol;
using MV.Data;
using UnityEditor;
using UnityEngine;

namespace MV.Editor.Settings
{
    [CustomPropertyDrawer(typeof(MvWealthIdAttribute))]
    public sealed class MvWealthIdDrawer : PropertyDrawer
    {
        const string SourcePath = "Assets/Game/GameData/Table/TSV/01_wealth.txt";

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var source = AssetDatabase.LoadAssetAtPath<TextAsset>(SourcePath);
            label = EditorGUI.BeginProperty(position, label, property);
            if (!source || property.propertyType != SerializedPropertyType.Integer)
            {
                EditorGUI.LabelField(position, label, new GUIContent($"재화 표 확인 필요 ({SourcePath})"));
                EditorGUI.EndProperty();
                return;
            }

            var ids = new List<int> { 0 };
            var names = new List<string> { "미지정" };
            foreach (var row in TSVRow.FromTsv(source.text))
            {
                int id = row.GetInt("id");
                ids.Add(id);
                names.Add($"{row.Get("name")} ({row.Get("type")})");
            }

            int selected = ids.IndexOf(property.intValue);
            if (selected < 0)
            {
                selected = ids.Count;
                ids.Add(property.intValue);
                names.Add($"등록되지 않은 ID ({property.intValue})");
            }

            bool mixed = EditorGUI.showMixedValue;
            EditorGUI.showMixedValue = property.hasMultipleDifferentValues;
            EditorGUI.BeginChangeCheck();
            int next = EditorGUI.Popup(position, label.text, selected, names.ToArray());
            if (EditorGUI.EndChangeCheck()) property.intValue = ids[next];
            EditorGUI.showMixedValue = mixed;
            EditorGUI.EndProperty();
        }
    }
}
