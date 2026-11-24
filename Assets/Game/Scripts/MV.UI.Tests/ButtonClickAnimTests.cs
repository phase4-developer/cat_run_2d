using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace MV.UI.Tests
{
    /// <summary>
    /// 공용 버튼 눌림 연출(MV-UI-BTNANIM, 노션 「Button Animation (공통)」): 누름 In, 뗌 Out, 꺼진 버튼은 재생 안 함,
    /// Controller가 없으면 경고만. 샘플 프리팹 원본에 부품이 붙어 있는지.
    /// </summary>
    public sealed class ButtonClickAnimTests
    {
        const string SamplePrefab = "Assets/Game/Art/UI/Prefab/HUD/Element_MainUpgrade_Stair.prefab";

        readonly List<Object> _made = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (var o in _made) if (o != null) Object.DestroyImmediate(o);
            _made.Clear();
        }

        ButtonClickAnim NewButton(bool withController, bool interactable = true)
        {
            var go = new GameObject("Btn", typeof(RectTransform));
            _made.Add(go);
            var button = go.AddComponent<Button>();
            button.interactable = interactable;
            var anim = go.AddComponent<ButtonClickAnim>();
            Assert.That(go.GetComponent<Animator>(), Is.Not.Null, "RequireComponent로 Animator가 함께 붙는다");
            if (!withController) go.GetComponent<Animator>().runtimeAnimatorController = null; // 스크립트 기본 참조(공용 Controller)가 Reset으로 들어간 것을 비운다
            if (withController)
            {
                var ctrl = new AnimatorController();
                _made.Add(ctrl);
                ctrl.AddLayer("Base");
                ctrl.AddParameter("In", AnimatorControllerParameterType.Trigger);
                ctrl.AddParameter("Out", AnimatorControllerParameterType.Trigger);
                go.GetComponent<Animator>().runtimeAnimatorController = ctrl;
            }
            return anim;
        }

        static PointerEventData Left() => new PointerEventData(null) { button = PointerEventData.InputButton.Left };

        [Test]
        public void Down_Then_Up_Toggles_Pressed()
        {
            var a = NewButton(true);
            a.OnPointerDown(Left());
            Assert.That(a.IsPressed, Is.True);
            a.OnPointerUp(Left());
            Assert.That(a.IsPressed, Is.False);
        }

        [Test]
        public void NotInteractable_Does_Not_Play()
        {
            var a = NewButton(true, interactable: false);
            a.OnPointerDown(Left());
            Assert.That(a.IsPressed, Is.False);
        }

        [Test]
        public void NonLeft_Button_Ignored()
        {
            var a = NewButton(true);
            a.OnPointerDown(new PointerEventData(null) { button = PointerEventData.InputButton.Right });
            Assert.That(a.IsPressed, Is.False);
        }

        [Test]
        public void Missing_Controller_Warns_Once_And_Skips()
        {
            var a = NewButton(false);
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex(@"\[ButtonClickAnim\].*Btn"));
            a.OnPointerDown(Left());
            a.OnPointerDown(Left());
            Assert.That(a.IsPressed, Is.False);
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void Sample_Prefab_Has_Component_On_Button()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(SamplePrefab);
            Assert.That(prefab, Is.Not.Null, SamplePrefab);
            var a = prefab.GetComponent<ButtonClickAnim>();
            Assert.That(a, Is.Not.Null, "샘플 프리팹 원본 뿌리의 ButtonClickAnim");
            Assert.That(prefab.GetComponent<Button>(), Is.Not.Null);
            Assert.That(prefab.GetComponent<Animator>(), Is.Not.Null);
        }
    }
}
