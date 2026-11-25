using System.Collections.Generic;
using System.Linq;
using MV.Data;
using MV.Meta;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace MV.UI.Tests
{
    /// <summary>
    /// MV-UI2-REBIRTH: 디자이너 Popup_RebirthUpgrade를 환생 팝업 겉(UIPopup_Rebirth 래퍼)으로 쓰기 —
    /// 직렬화 참조, 노션 상태 3가지(미해금·쿨타임·가능), 특전 칸 = 14_rebirthPerk active 행 수(풀),
    /// 환생 = 확인 팝업 OK 뒤 1번, 캣닢 스킵 1번. 화면 배치·연출 확인은 사람 확인(NOT_RUN 별도).
    /// </summary>
    public sealed class RebirthDesignViewTests
    {
        const string PopupPath = "Assets/Game/Addressables/Local/UI/Meta/UIPopup_Rebirth.prefab";
        const string WealthTable = "01_wealth";
        static MvTables T => MetaUiTestKit.Base();
        static int ActiveRows => T.RebirthPerk.Datas.Count(r => r.active);

        readonly List<GameObject> _made = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (var go in _made) if (go != null) Object.DestroyImmediate(go);
            _made.Clear();
        }

        (GameObject root, RebirthDesignView view) NewPopup()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PopupPath);
            Assert.That(prefab, Is.Not.Null, PopupPath);
            var go = Object.Instantiate(prefab);
            _made.Add(go);
            var view = go.GetComponent<RebirthDesignView>();
            Assert.That(view, Is.Not.Null, "래퍼 루트의 RebirthDesignView");
            return (go, view);
        }

        (RebirthPresenter p, FakeMetaPort port, List<string> asked) NewPresenter(RebirthDesignView view, bool answer = true)
        {
            var port = new FakeMetaPort(T);
            var p = new RebirthPresenter();
            var asked = new List<string>();
            p.AskConfirm = (text, done) => { asked.Add(text); done(answer); };
            p.SetPort(port);
            p.SetView(view);
            view.ActionPressed += id => p.Press(id);
            return (p, port, asked);
        }

        [Test]
        public void Prefab_NestsDesignerPopup_WithAllReferences()
        {
            var (go, view) = NewPopup();
            Assert.That(view.MissingRefs(), Is.Empty, "RebirthDesignView 참조");
            var ctrl = go.GetComponent("RebirthPopup");
            Assert.That(ctrl, Is.Not.Null, "RebirthPopup 컨트롤러");
            var so = new SerializedObject(ctrl);
            Assert.That(so.FindProperty("view").objectReferenceValue, Is.SameAs(view), "RebirthPopup.view = RebirthDesignView");
            Assert.That(so.FindProperty("transition").objectReferenceValue, Is.Not.Null, "디자이너 UITransition");
            Assert.That(go.transform.Find("Popup_RebirthUpgrade"), Is.Not.Null, "중첩 디자이너 Popup_RebirthUpgrade");
            Assert.That(go.GetComponent("MetaPanelView"), Is.Null, "코드 생성 패널은 뗐다");
            var wealth = T.Find(WealthTable);
            Assert.That(view.Currencies.All(c => wealth.HasKey(c.wealthType)), Is.True, "재화 칸은 01_wealth.type 참조");
            Assert.That(view.AdButton, Is.Not.SameAs(view.SkipDiaButton));
        }

        [Test]
        public void CurrencySlot_UsesInspectorWealthType_WhenRedrawn()
        {
            var (_, view) = NewPopup();
            var slot = view.Currencies.First().slot;
            var settings = new SerializedObject(view);
            var type = settings.FindProperty("currencies").GetArrayElementAtIndex(0).FindPropertyRelative("wealthType");
            type.stringValue = "gold";
            settings.ApplyModifiedPropertiesWithoutUndo();
            var (p, port, _) = NewPresenter(view);
            Assert.That(slot.WealthType, Is.EqualTo("gold"));

            const string goldenFish = "goldenFish";
            type.stringValue = goldenFish;
            settings.ApplyModifiedPropertiesWithoutUndo();
            int wealthId = T.Find(WealthTable).RawRows.Single(row => row.Get("type") == goldenFish).GetInt("id");
            p.Draw();

            Assert.That(slot.gameObject.activeSelf, Is.True);
            Assert.That(slot.WealthType, Is.EqualTo(goldenFish));
            Assert.That(slot.ValueText, Is.EqualTo(MvUiFormat.Fmt(port.Wealth(wealthId))));
            Assert.That(MvConsumeIcon.TryWealth(T, goldenFish, out _, out var sprite), Is.True);
            Assert.That(slot.IconSprite, Is.EqualTo(sprite));
        }

        [Test]
        public void Ready_ShowsFullBar_CompleteText_AndRebirthButton()
        {
            var (_, view) = NewPopup();
            var (p, port, _) = NewPresenter(view);
            port.RebirthLevel = 3;
            p.Open();

            Assert.That(view.LockShown, Is.False);
            Assert.That(view.CooldownShown, Is.True);
            Assert.That(view.CooldownValue, Is.EqualTo(1f));
            Assert.That(view.CooldownCountShown, Is.False);
            Assert.That(view.CompleteShown, Is.True);
            Assert.That(view.AdButton.gameObject.activeSelf || view.SkipDiaButton.gameObject.activeSelf, Is.False, "스킵 버튼은 쿨타임 중에만");
            Assert.That(view.RebirthShown && !view.RebirthDisableShown, Is.True);
            Assert.That(view.RebirthButton.interactable, Is.True);
            Assert.That(view.LevelText, Is.EqualTo("3"));
            Assert.That(view.GainText, Does.Contain("10"));

            Assert.That(view.Elements.Count, Is.EqualTo(ActiveRows), "칸 수 = 14_rebirthPerk active 행 수");
            p.Draw();
            Assert.That(view.Elements.Count, Is.EqualTo(ActiveRows), "다시 그려도 칸이 늘지 않는다(풀 재사용)");
        }

        [Test]
        public void Cooldown_ShowsTimer_SkipButtons_AndDisabledRebirth()
        {
            var (_, view) = NewPopup();
            var (p, port, asked) = NewPresenter(view);
            port.RebirthCooldownTotalMs = 6L * 3600L * 1000L;
            port.RebirthCooldownLeftMs = 3L * 3600L * 1000L;
            p.Open();

            Assert.That(view.LockShown, Is.False);
            Assert.That(view.CooldownCountShown && !view.CompleteShown, Is.True);
            Assert.That(view.CooldownValue, Is.EqualTo(0.5f).Within(0.001f));
            Assert.That(view.AdButton.gameObject.activeSelf && view.SkipDiaButton.gameObject.activeSelf, Is.True);
            Assert.That(view.RebirthShown && view.RebirthDisableShown, Is.True, "쿨타임: Disable 켬");
            Assert.That(view.RebirthButton.interactable, Is.False, "쿨타임: 버튼 작동 X");

            view.RebirthButton.onClick.Invoke();
            Assert.That(asked, Is.Empty);
            Assert.That(port.RebirthNowCalls, Is.EqualTo(0));

            Assert.That(p.Press(RebirthPresenter.SkipDiaId), Is.True, "캣닢 스킵");
            Assert.That(port.SkipDiaCalls, Is.EqualTo(1));
            Assert.That(view.CompleteShown, Is.True, "스킵 뒤 다시 그림 = 가능");
        }

        [Test]
        public void Locked_OpensWithLock_UnlockProgress_AndNoRebirthButton()
        {
            var (_, view) = NewPopup();
            var (p, port, _) = NewPresenter(view);
            port.RebirthOpen = false;
            port.UnlockStateOverride = MvUnlockState.Locked;
            var row = T.ContentOpen.ByKey(MvMetaRules.RebirthKey);
            var cond = row.openConditions.First(c => c.type != UnlockConditionType.Always);
            int half = cond.value / 2;
            port.UnlockProgress = cond.type == UnlockConditionType.Area ? new MvUnlockProgress(half - 1, 0, 0)
                : cond.type == UnlockConditionType.Runs ? new MvUnlockProgress(0, half, 0) : new MvUnlockProgress(0, 0, half);
            p.Open();

            Assert.That(view.LockShown, Is.True);
            Assert.That(view.CooldownShown, Is.False);
            Assert.That(view.RebirthShown, Is.False, "미해금: 환생 버튼 끔");
            Assert.That(view.LockCountText, Is.EqualTo(half + "/" + cond.value), "해금 조건 진행(05_contentOpen[rebirth])");
            Assert.That(view.LockValue, Is.EqualTo((float)half / cond.value).Within(0.001f));
        }

        [TestCase(true)]
        [TestCase(false)]
        public void RebirthButton_AsksConfirm_ThenRebirthsOnlyOnOk(bool ok)
        {
            var (_, view) = NewPopup();
            var (p, port, asked) = NewPresenter(view, ok);
            bool closed = false;
            p.CloseRequested += () => closed = true;
            p.Open();

            view.RebirthButton.onClick.Invoke();
            Assert.That(asked.Count, Is.EqualTo(1), "공용 확인 팝업 1번");
            Assert.That(port.RebirthNowCalls, Is.EqualTo(ok ? 1 : 0));
            Assert.That(port.PressRebirthCalls, Is.EqualTo(0), "두 번 누름 규칙은 쓰지 않는다");
            Assert.That(closed, Is.EqualTo(ok));
        }

        [Test]
        public void PressPerk_BuysOnce()
        {
            var (_, view) = NewPopup();
            var (p, port, _) = NewPresenter(view);
            p.Open();
            var first = T.RebirthPerk.Sorted.First(r => r.active);
            Assert.That(view.Elements[0].Data.ActionId, Is.EqualTo(RebirthPresenter.BuyPrefix + first.key), "sort 순서");
            view.Elements[0].OkButton.onClick.Invoke();
            Assert.That(port.BuyCalls, Is.EqualTo(1));
            Assert.That(port.LastBuy, Is.EqualTo((MvGrowth.Perk, first.key)));
        }
    }
}
