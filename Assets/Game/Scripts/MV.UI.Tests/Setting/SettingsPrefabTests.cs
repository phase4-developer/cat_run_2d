using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace MV.UI.Tests
{
    /// <summary>설정·언어 팝업 래퍼(MV-UI2-SETTING): 직렬화 참조와 입력 가로막힘. 화면 배치는 사람 확인.</summary>
    public sealed class SettingsPrefabTests
    {
        const string SettingPath = "Assets/Game/Addressables/Local/UI/Setting/UIPopup_Setting.prefab";
        const string LanguagePath = "Assets/Game/Addressables/Local/UI/Setting/UIPopup_SettingLanguage.prefab";

        /// <summary>그리는 순서(계층 앞 → 뒤)로 펼친다. 뒤에 오는 것이 위에 그려지고 먼저 터치를 받는다.</summary>
        static List<Transform> DrawOrder(Transform root)
        {
            var list = new List<Transform>();
            void Walk(Transform t) { list.Add(t); for (int i = 0; i < t.childCount; i++) Walk(t.GetChild(i)); }
            Walk(root);
            return list;
        }

        static bool Overlaps(RectTransform a, RectTransform b)
        {
            var ca = new Vector3[4]; var cb = new Vector3[4];
            a.GetWorldCorners(ca); b.GetWorldCorners(cb);
            return ca[0].x < cb[2].x && cb[0].x < ca[2].x && ca[0].y < cb[2].y && cb[0].y < ca[2].y;
        }

        /// <summary>버튼·토글·슬라이더보다 위에 그려지면서 터치를 받는 그림(그 입력 부품의 일부가 아닌 것) 이름.</summary>
        static List<string> Blockers(GameObject prefab)
        {
            var order = DrawOrder(prefab.transform);
            var result = new List<string>();
            foreach (var s in prefab.GetComponentsInChildren<Selectable>(true))
            {
                int si = order.IndexOf(s.transform);
                var sr = (RectTransform)s.transform;
                foreach (var g in prefab.GetComponentsInChildren<Graphic>(true))
                {
                    if (!g.raycastTarget || g.transform.IsChildOf(s.transform)) continue;
                    if (order.IndexOf(g.transform) <= si) continue;
                    if (g.GetComponentInParent<Selectable>(true) != null) continue; // 다른 입력 부품의 그림
                    if (Overlaps(sr, (RectTransform)g.transform)) result.Add(g.name + " > " + s.name);
                }
            }
            return result.Distinct().ToList();
        }

        [TestCase(SettingPath)]
        [TestCase(LanguagePath)]
        [TestCase(SystemPopupPrefabTests.Path)]
        public void Decor_DoesNotBlockInputs(string path)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Assert.That(prefab, Is.Not.Null, path);
            Assert.That(Blockers(prefab), Is.Empty, "장식 그림이 버튼·슬라이더 위에서 터치를 가로챔(디자이너 Deco_Frame 등)");
        }

        /// <summary>
        /// 켜짐 그림과 상태 전환 그림이 같은 토글(진동 켜기·끄기)은 전환을 끈다: 색 전환이 켜짐 그림 알파를 덮어써
        /// 마우스를 올린 뒤 둘 다 켜져 보이고 바뀌지 않았다(사용자 2026-11-19 「오버 동작 자체가 있을 필요가 없어」).
        /// </summary>
        [Test]
        public void Toggles_SharingCheckGraphic_HaveNoTransition()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(SettingPath);
            var bad = prefab.GetComponentsInChildren<Toggle>(true)
                .Where(t => t.graphic != null && t.targetGraphic == t.graphic && t.transition != Selectable.Transition.None)
                .Select(t => t.name).ToList();
            Assert.That(bad, Is.Empty);
        }

        [Test]
        public void SettingView_AllReferencesSet()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(SettingPath);
            var view = prefab.GetComponent<SettingsDesignView>();
            Assert.That(view, Is.Not.Null);
            var missing = new List<string>();
            var it = new SerializedObject(view).GetIterator();
            for (bool enter = true; it.NextVisible(enter); enter = false)
                if (it.propertyType == SerializedPropertyType.ObjectReference && it.name != "m_Script" && it.objectReferenceValue == null)
                    missing.Add(it.propertyPath);
            Assert.That(missing, Is.Empty);
        }
    }
}
