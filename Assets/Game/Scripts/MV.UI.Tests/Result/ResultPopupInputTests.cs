using System.Collections.Generic;
using jjevol;
using MV.Meta;
using MV.Run;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MV.UI.Tests
{
    /// <summary>
    /// MV-UI3-RESULTCLOSE2: 결과 팝업 열림 연출 중에는 받기(래퍼 Backboard 터치 = ×1)가 눌리지 않는다(사용자 2026-11-23 「연출중에는 안눌리게」).
    /// EditMode에서는 전환 tween이 진행되지 않아 ShowUI 뒤 계속 열림 연출 중이다. 컨트롤러 형식은 이름으로 찾는다(UIPopup 어셈블리를 시험 asmdef에 더하지 않음).
    /// </summary>
    public sealed class ResultPopupInputTests
    {
        const string PopupPath = "Assets/Game/Addressables/Local/UI/Result/UIPopup_Result.prefab";

        sealed class Settler : IResultSettler
        {
            public readonly List<int> Muls = new List<int>();
            public MvSettleResult Settle(MvRunOutcome outcome, int mul) { Muls.Add(mul); return new MvSettleResult { Mul = mul }; }
        }

        GameObject _go;

        [TearDown]
        public void TearDown()
        {
            if (_go != null) Object.DestroyImmediate(_go);
        }

        static bool Prop(Component c, string name) => (bool)c.GetType().GetProperty(name).GetValue(c);

        [Test]
        public void PressDuringShowTransition_IsIgnored()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PopupPath);
            Assert.That(prefab, Is.Not.Null, PopupPath);
            _go = Object.Instantiate(prefab);
            var popup = _go.GetComponent("ResultPopup");
            var view = _go.GetComponent<ResultDesignView>();
            Assert.That(popup, Is.Not.Null);
            Assert.That(view, Is.Not.Null);

            var settler = new Settler();
            var r = new RunResult { Reason = FinishReason.Gate, Gold = 100, Souls = 5 };
            var data = ResultViewData.Create(r, 1, 0, 0, MetaUiTestKit.Base());
            data.Settler = settler;
            int collected = 0;
            data.OnCollected = _ => collected++;

            popup.GetType().GetMethod("ShowUI", new[] { typeof(IPopupData) }).Invoke(popup, new object[] { new ResultPopupData(data) });
            Assert.That(Prop(popup, "IsOpen"), Is.True);
            Assert.That(Prop(popup, "IsTransitioning"), Is.True, "전제: 열림 연출 중");
            Assert.That(Prop(popup, "AcceptsInput"), Is.False, "열림 연출 중에는 입력을 받지 않는다");

            view.Backboard.onClick.Invoke();
            popup.GetType().GetMethod("PressCollect").Invoke(popup, null);

            Assert.That(settler.Muls, Is.Empty, "연출 중 받기 입력은 정산하지 않는다");
            Assert.That(collected, Is.Zero, "연출 중 입력으로 로비로 가지 않는다");
            Assert.That(Prop(popup, "IsOpen"), Is.True, "팝업은 그대로 열려 있다(연출 뒤 다시 누를 수 있음)");
            Assert.That(Prop(popup, "Closing"), Is.False);
        }
    }
}
