using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace MV.UI.Tests
{
    /// <summary>공용 시스템 팝업 래퍼(MV-UI2-SYSPOPUP, 노션 「[팝업] 시스템 기본 팝업」): 참조·알림/선택 버튼. 화면 배치는 사람 확인.</summary>
    public sealed class SystemPopupPrefabTests
    {
        public const string Path = "Assets/Game/Addressables/Local/UI/Common/UIPopup_CommonSystem.prefab";

        GameObject _go;

        [TearDown]
        public void TearDown()
        {
            if (_go) Object.DestroyImmediate(_go);
        }

        SystemPopupView Spawn()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Path);
            Assert.That(prefab, Is.Not.Null, Path);
            _go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            var view = _go.GetComponent<SystemPopupView>();
            Assert.That(view, Is.Not.Null);
            return view;
        }

        [Test]
        public void View_AllReferencesSet()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Path);
            Assert.That(prefab, Is.Not.Null, Path);
            var missing = new List<string>();
            // 시험 어셈블리는 UIPopup 어셈블리를 참조하지 않으므로 컨트롤러는 직렬화로만 본다.
            string popupType = null;
            var behaviours = prefab.GetComponents<MonoBehaviour>();
            Assert.That(behaviours.Length, Is.EqualTo(2), "SystemPopup + SystemPopupView");
            foreach (var c in behaviours)
            {
                Assert.That(c, Is.Not.Null, "스크립트 누락");
                var so = new SerializedObject(c);
                var pt = so.FindProperty("PopupType");
                if (pt != null) popupType = pt.stringValue;
                var it = so.GetIterator();
                for (bool enter = true; it.NextVisible(enter); enter = false)
                    if (it.propertyType == SerializedPropertyType.ObjectReference && it.name != "m_Script" && it.name != "canvasBinder" && it.objectReferenceValue == null)
                        missing.Add(c.GetType().Name + "." + it.propertyPath);
            }
            Assert.That(missing, Is.Empty);
            Assert.That(popupType, Is.EqualTo(MvUiIds.CommonSystem));
        }

        /// <summary>알림: OK 끔, Cancel과 X 둘 다 닫기(취소)로 간다(사용자 2026-11-19 「OK 버튼을 꺼서 Cancel하고 X 두개로 닫기게해」).</summary>
        [Test]
        public void Notice_HidesOk_CancelAndCloseBothCancel()
        {
            var view = Spawn();
            int ok = 0, cancel = 0;
            view.OkPressed += () => ok++;
            view.CancelPressed += () => cancel++;
            view.Draw("T", "본문", false, "OK", "Cancel");
            Assert.That(view.OkShown, Is.False);
            Assert.That(view.TitleShown, Is.EqualTo("T"));
            Assert.That(view.ContentsShown, Is.EqualTo("본문"));
            view.ClickCancel();
            view.ClickClose();
            Assert.That(cancel, Is.EqualTo(2));
            Assert.That(ok, Is.EqualTo(0));
        }

        [Test]
        public void Confirm_ShowsOk_OkPressed()
        {
            var view = Spawn();
            int ok = 0, cancel = 0;
            view.OkPressed += () => ok++;
            view.CancelPressed += () => cancel++;
            view.Draw("T", "C", true, "OK", "Cancel");
            Assert.That(view.OkShown, Is.True);
            Assert.That(view.CancelShown, Is.EqualTo("Cancel"));
            view.ClickOk();
            Assert.That(ok, Is.EqualTo(1));
            Assert.That(cancel, Is.EqualTo(0));
        }
    }
}
