using System.Collections.Generic;
using System.Linq;
using MV.Data;
using MV.Meta;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace MV.UI.Tests
{
    /// <summary>
    /// MV-UI2-AREAUP: 디자이너 Popup_AreaUpgrade를 Area 업그레이드 팝업 겉(UIPopup_AreaUpgrade 래퍼)으로 쓰기 —
    /// 직렬화 참조, 표 행 ↔ Element_Upgrade 칸(풀), 버튼 상태(살 수 있음·부족·MAX·환생 잠김), 누름 → 구매 1번.
    /// 화면 배치·연출 확인은 사람 확인(NOT_RUN 별도).
    /// </summary>
    public sealed class AreaUpgradeDesignViewTests
    {
        const string PopupPath = "Assets/Game/Addressables/Local/UI/Meta/UIPopup_AreaUpgrade.prefab";
        const string ElementPath = "Assets/Game/Art/UI/Prefab/Common/Element_Upgrade.prefab";
        static MvTables T => MetaUiTestKit.Base();

        readonly List<GameObject> _made = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (var go in _made) if (go != null) Object.DestroyImmediate(go);
            _made.Clear();
        }

        (GameObject root, AreaUpgradeDesignView view) NewPopup()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PopupPath);
            Assert.That(prefab, Is.Not.Null, PopupPath);
            var go = Object.Instantiate(prefab);
            _made.Add(go);
            var view = go.GetComponent<AreaUpgradeDesignView>();
            Assert.That(view, Is.Not.Null, "래퍼 루트의 AreaUpgradeDesignView");
            return (go, view);
        }

        (AreaUpgradePresenter p, FakeMetaPort port) NewPresenter(AreaUpgradeDesignView view)
        {
            var port = new FakeMetaPort(T);
            var p = new AreaUpgradePresenter();
            p.SetPort(port);
            p.SetView(view);
            view.ActionPressed += id => p.Press(id);
            return (p, port);
        }

        static int ActiveRows => T.AreaUpgrade.Datas.Count(r => r.active);

        [Test]
        public void Prefabs_NestDesignerPopup_WithAllReferences()
        {
            var (go, view) = NewPopup();
            Assert.That(view.MissingRefs(), Is.Empty, "AreaUpgradeDesignView 참조");
            var ctrl = go.GetComponent("AreaUpgradePopup");
            Assert.That(ctrl, Is.Not.Null, "AreaUpgradePopup 컨트롤러");
            var so = new SerializedObject(ctrl);
            Assert.That(so.FindProperty("view").objectReferenceValue, Is.SameAs(view), "AreaUpgradePopup.view = AreaUpgradeDesignView");
            Assert.That(so.FindProperty("transition").objectReferenceValue, Is.Not.Null, "디자이너 UITransition");
            Assert.That(go.transform.Find("Popup_AreaUpgrade"), Is.Not.Null, "중첩 디자이너 Popup_AreaUpgrade");
            Assert.That(go.GetComponent("MetaPanelView"), Is.Null, "코드 생성 패널은 뗐다");
            Assert.That(view.Currencies.Select(c => c.wealthType), Is.EqualTo(AreaUpgradePresenter.CurrencyTypes), "재화 칸 = 골드·다이아(노션)");

            var element = AssetDatabase.LoadAssetAtPath<GameObject>(ElementPath).GetComponent<UpgradeElement>();
            Assert.That(element, Is.Not.Null, "Element_Upgrade 루트의 UpgradeElement(공용 부품)");
            Assert.That(element.MissingRefs(), Is.Empty);
            Assert.That(element.LockBadge.HasRefs, Is.True, "Element_UnlockRebirth 루트의 UnlockRebirthBadge");
            Assert.That(element.RowLockBadge.HasRefs, Is.True, "칸 전체 Lock 안 Element_UnlockRebirth");
            Assert.That(element.RowLockBadge, Is.Not.SameAs(element.LockBadge));
            var priceLabel = element.GetComponentInChildren(System.Type.GetType("jjevol.PriceLabel, JJEvol.UI.Economy.Runtime"), true);
            Assert.That(priceLabel, Is.Not.Null, "가격 글자의 Framework PriceLabel");
            Assert.That(new SerializedObject(priceLabel).FindProperty("priceText").objectReferenceValue, Is.Not.Null, "PriceLabel.priceText");
        }

        [Test]
        public void Render_OneElementPerActiveTableRow_FromPool()
        {
            var (_, view) = NewPopup();
            var (p, port) = NewPresenter(view);
            p.Open();
            Assert.That(view.Elements.Count, Is.EqualTo(ActiveRows), "칸 수 = 13_areaUpgrade active 행 수");
            Assert.That(view.Elements.All(e => e.gameObject.activeSelf), Is.True);
            Assert.That(view.Elements.Distinct().Count(), Is.EqualTo(ActiveRows));
            var first = T.AreaUpgrade.Sorted.First(r => r.active);
            Assert.That(view.Elements[0].Data.ActionId, Is.EqualTo(AreaUpgradePresenter.BuyPrefix + first.key), "sort 순서");
            Assert.That(view.Elements[0].LevelText, Is.EqualTo("Lv.0"));
            Assert.That(view.Elements[0].CurrentText, Is.EqualTo(MetaPanelPresenter.GrowthValue(first.valueFormat, first.valueBase, first.valueStep, 0)));
            Assert.That(view.Elements[0].UpgradeText, Is.EqualTo(MetaPanelPresenter.GrowthValue(first.valueFormat, first.valueBase, first.valueStep, 1)));
            Assert.That(view.Elements[0].PriceLabel, Is.EqualTo("40"), "가격은 숫자만(아이콘은 프리팹)");
            Assert.That(view.AreaLabel.AreaText, Is.Not.Empty);
            Assert.That(view.AdGainText, Does.StartWith("x"));

            p.Draw(); // 다시 그려도 칸이 늘지 않는다(풀 재사용)
            Assert.That(view.Elements.Count, Is.EqualTo(ActiveRows));
        }

        [Test]
        public void ButtonStates_Ready_Disabled_Max_Locked()
        {
            var (_, view) = NewPopup();
            var (p, port) = NewPresenter(view);
            var keys = T.AreaUpgrade.Sorted.Where(r => r.active).Select(r => r.key).ToList();
            port.MaxKeys.Add(keys[1]);
            port.NeedRebirthByKey[keys[2]] = 4;
            port.NeedRebirthByKey[keys[3]] = 5;
            port.LevelByKey[keys[3]] = 2;
            p.Open();

            var ready = view.Elements[0];
            Assert.That(ready.Data.State, Is.EqualTo(UpgradeElementState.Ready));
            Assert.That(ready.OkShown && !ready.OkDisabled && !ready.LockShown, Is.True);
            Assert.That(ready.OkButton.interactable, Is.True);

            var max = view.Elements[1];
            Assert.That(max.Data.State, Is.EqualTo(UpgradeElementState.Max));
            Assert.That(max.OkDisabled, Is.True);
            Assert.That(max.PriceLabel, Is.EqualTo("MAX"));
            Assert.That(max.OkButton.interactable, Is.False);

            // 1레벨부터 잠김 → 칸 전체 Lock(내용·버튼 숨김)
            var all = view.Elements[2];
            Assert.That(all.Data.State, Is.EqualTo(UpgradeElementState.LockedAll));
            Assert.That(all.RowLockShown && !all.ContentsShown && !all.OkShown, Is.True);
            Assert.That(all.RowLockBadge.CountText, Is.EqualTo("4"), "필요 환생 수");
            Assert.That(p.Model.FindAction(AreaUpgradePresenter.BuyPrefix + keys[2]).Enabled, Is.False);

            // 이미 레벨이 있는데 잠김 → 내용 보이고 버튼 잠금
            var locked = view.Elements[3];
            Assert.That(locked.Data.State, Is.EqualTo(UpgradeElementState.Locked));
            Assert.That(locked.LockShown && !locked.OkShown && locked.ContentsShown && !locked.RowLockShown, Is.True);
            Assert.That(locked.LockBadge.CountText, Is.EqualTo("5"));
            Assert.That(ready.RowLockShown || max.RowLockShown, Is.False);

            port.CanBuyResult = false;
            p.Draw();
            Assert.That(view.Elements[0].Data.State, Is.EqualTo(UpgradeElementState.Disabled));
            Assert.That(view.Elements[0].OkDisabled, Is.True, "살 수 없으면 Disable 켬");
            Assert.That(view.Elements[0].OkButton.interactable, Is.False);
        }

        [Test]
        public void PressElement_BuysOnce_AndLockedDoesNothing()
        {
            var (_, view) = NewPopup();
            var (p, port) = NewPresenter(view);
            var keys = T.AreaUpgrade.Sorted.Where(r => r.active).Select(r => r.key).ToList();
            port.NeedRebirthByKey[keys[1]] = 1;
            p.Open();

            view.Elements[0].OkButton.onClick.Invoke();
            Assert.That(port.BuyCalls, Is.EqualTo(1));
            Assert.That(port.LastBuy, Is.EqualTo((MvGrowth.AreaUp, keys[0])));

            view.Elements[1].OkButton.onClick.Invoke(); // 잠김: 버튼 숨김·누름 무시
            Assert.That(port.BuyCalls, Is.EqualTo(1));
        }

        /// <summary>
        /// 목록(스크롤 뷰) 위에 그려지는 장식 그림이 입력을 가로채지 않는다(사용자 확인 「스크롤도 안되고 버튼도 안눌려」).
        /// 그리는 순서(계층 순서)가 스크롤 뷰보다 뒤인 그림 중 버튼이 아닌 것은 목록 영역과 겹치면 raycastTarget이 꺼져 있어야 한다.
        /// </summary>
        [Test]
        public void NoRaycastBlocker_DrawnOverList()
        {
            var (go, _) = NewPopup();
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(1080, 1920);
            var scroll = go.GetComponentInChildren<ScrollRect>(true);
            Assert.That(scroll, Is.Not.Null, "목록 ScrollRect");
            Rect listRect = WorldRect((RectTransform)scroll.transform);
            Assert.That(listRect.width > 1 && listRect.height > 1, Is.True, "목록 영역 크기");

            var graphics = go.GetComponentsInChildren<Graphic>(true);
            int last = -1;
            for (int i = 0; i < graphics.Length; i++)
                if (graphics[i].transform == scroll.transform || graphics[i].transform.IsChildOf(scroll.transform)) last = i;
            var blockers = new List<string>();
            for (int i = last + 1; i < graphics.Length; i++)
            {
                var g = graphics[i];
                if (!g.raycastTarget || !g.enabled || !g.gameObject.activeInHierarchy) continue;
                if (g.GetComponentInParent<Selectable>(true) != null) continue;
                Rect r = WorldRect(g.rectTransform);
                if (!r.Overlaps(listRect)) continue;
                float w = Mathf.Min(r.xMax, listRect.xMax) - Mathf.Max(r.xMin, listRect.xMin);
                float h = Mathf.Min(r.yMax, listRect.yMax) - Mathf.Max(r.yMin, listRect.yMin);
                if (w > 1 && h > 1) blockers.Add(g.name);
            }
            Assert.That(blockers, Is.Empty, "목록 위를 덮어 입력을 막는 그림");
        }

        static Rect WorldRect(RectTransform t)
        {
            var c = new Vector3[4];
            t.GetWorldCorners(c);
            return Rect.MinMaxRect(c[0].x, c[0].y, c[2].x, c[2].y);
        }
    }
}
