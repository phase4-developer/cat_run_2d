using System.Collections.Generic;
using MV.Data;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace MV.UI.Tests
{
    /// <summary>
    /// 공용 HUD 부품(MV-UI3-HUD2): 디자이너 프리팹마다 UIBase 부품 컴포넌트가 붙어 있고, <c>ShowUI(데이터)</c>가 꺼진 칸을 켜고 표 값으로 그린다.
    /// 보장 범위: 부품 단위 표시. 섹션 조합·연출·화면 모습은 다음 계약과 사람 확인(NOT_RUN)으로 본다.
    /// </summary>
    public sealed class MvHudPartsTests
    {
        const string Dir = "Assets/Game/Art/UI/Prefab/";
        static MvTables T => MetaUiTestKit.Base();

        readonly List<GameObject> _made = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (var go in _made) if (go != null) Object.DestroyImmediate(go);
            _made.Clear();
        }

        /// <summary>프리팹을 일반 복제로 만들고 꺼 둔다(풀에서 막 꺼낸 칸과 같은 상태).</summary>
        TPart NewHidden<TPart>(string path) where TPart : Component
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Dir + path);
            Assert.That(prefab, Is.Not.Null, path);
            var go = Object.Instantiate(prefab);
            _made.Add(go);
            go.SetActive(false);
            var part = go.GetComponent<TPart>();
            Assert.That(part, Is.Not.Null, path + " 루트에 " + typeof(TPart).Name);
            return part;
        }

        static void AssertRefs(Object o, params string[] optional)
        {
            var it = new SerializedObject(o).GetIterator();
            while (it.NextVisible(true))
                if (it.propertyType == SerializedPropertyType.ObjectReference && it.propertyPath != "m_Script" && System.Array.IndexOf(optional, it.propertyPath) < 0)
                    Assert.That(it.objectReferenceValue, Is.Not.Null, o.name + "." + it.propertyPath);
        }

        [Test]
        public void WeaponSlot_ShowUI_ActivatesAndDrawsTableRow()
        {
            var slot = NewHidden<HudWeaponSlot>("HUD/Slot_Weapon.prefab");
            AssertRefs(slot, "flash", "sparkle");   // 머지 연출 리소스(선택, 디자이너 리소스 대기)
            ItemData item = null;
            foreach (var it in T.Item.Datas) if (it.tier == 2) { item = it; break; }
            Assert.That(item, Is.Not.Null, "103_item tier 2");

            slot.ShowUI(new WeaponSlotData(T, 2, 1));

            Assert.That(slot.gameObject.activeSelf, Is.True, "ShowUI가 켠다");
            Assert.That(slot.Tier, Is.EqualTo(2));
            Assert.That(slot.IconSprite, Is.EqualTo(item.icon), "103_item.icon");
            Assert.That(slot.GradeSprite, Is.EqualTo(HudGrade.Frame(T, 2).sprite), "15_grade 등급 틀");
            Assert.That(slot.AttackText, Is.EqualTo(MvUiFormat.Fmt(item.dmg * 1.0)), "공격력");
        }

        [Test]
        public void ClimbCard_ShowUI_DrawsTierAndFill()
        {
            var card = NewHidden<HudClimbCard>("HUD/Card_Class.prefab");
            AssertRefs(card);
            var row = HudGrade.CardTier(T, 1);
            Assert.That(row, Is.Not.Null, "115_cardTier 1");

            card.ShowUI(new ClimbCardData(T, 1, 0.5f));

            Assert.That(card.gameObject.activeSelf, Is.True);
            Assert.That(card.Tier, Is.EqualTo(1));
            Assert.That(card.GradeSprite, Is.EqualTo(row.frameSprite), "115_cardTier.frameSprite");
            Assert.That(card.Title, Is.EqualTo(HudGrade.CardTierName(T, row)));
            Assert.That(card.Fill, Is.EqualTo(0.5f).Within(1e-4), "게이지");
        }

        [Test]
        public void SkillCard_ShowUI_QuestionThenChosenCard()
        {
            var card = NewHidden<HudSkillCard>("HUD/Card_Skill.prefab");
            AssertRefs(card, "maskBack");

            card.ShowUI(new SkillCardData(T, 0, null));
            Assert.That(card.gameObject.activeSelf, Is.True);
            Assert.That(card.QuestionShown, Is.True, "카드 없음 = 「?」");
            Assert.That(card.CardId, Is.EqualTo(0));
            Assert.That(card.Title, Is.Empty, "카드가 정해지기 전에는 제목 없음");

            CardData chosen = null;
            foreach (var c in T.Card.Datas) if (c.active) { chosen = c; break; }
            Assert.That(chosen, Is.Not.Null, "활성 카드 1장");
            card.ShowUI(new SkillCardData(T, chosen.tier, chosen));
            Assert.That(card.QuestionShown, Is.False);
            Assert.That(card.CardId, Is.EqualTo(chosen.id));
            Assert.That(card.FaceSprite, Is.EqualTo(chosen.icon), "확정 카드 그림");
            Assert.That(card.Title, Is.EqualTo(HudGrade.CardName(T, chosen)));
        }

        /// <summary>icon이 빈 카드는 그림 칸을 숨기고, 다음 카드에서 다시 켠다(앞 카드 그림이 남지 않음, MV-UI3-RESAUDIT).</summary>
        [Test]
        public void SkillCard_EmptyIcon_HidesFace_ThenShowsNext()
        {
            var card = NewHidden<HudSkillCard>("HUD/Card_Skill.prefab");
            CardData withIcon = null, noIcon = null;
            foreach (var c in T.Card.Datas)
            {
                if (withIcon == null && !string.IsNullOrEmpty(c.icon)) withIcon = c;
                if (noIcon == null && string.IsNullOrEmpty(c.icon)) noIcon = c;
            }
            Assert.That(withIcon, Is.Not.Null);
            Assert.That(noIcon, Is.Not.Null, "110_card에 icon 빈 행(더미 채움 전)");
            card.ShowUI(new SkillCardData(T, withIcon.tier, withIcon));
            Assert.That(card.FaceShown, Is.True);
            card.ShowUI(new SkillCardData(T, noIcon.tier, noIcon));
            Assert.That(card.FaceShown, Is.False, "icon 빈 카드는 앞 카드 그림을 남기지 않는다");
            card.ShowUI(new SkillCardData(T, withIcon.tier, withIcon));
            Assert.That(card.FaceShown, Is.True);
            Assert.That(card.FaceSprite, Is.EqualTo(withIcon.icon));
        }

        /// <summary>다시 쓰는 보상 칸: 등급 없는 재화는 디자이너 틀 등급으로 돌아가고, icon 빈 재화는 아이콘을 숨긴다(MV-UI3-RESAUDIT).</summary>
        [Test]
        public void ItemSlot_Reuse_GradeZeroBackToDesignerFrame_EmptyIconHidden()
        {
            var slot = NewHidden<ItemSlot>("Common/Slot_Item.prefab");
            // 에디터에서는 AddressableImage가 프리팹 그림을 이름(spriteName)으로 옮겨 둔다. 실기에서는 sprite가 남아 ItemSlot이 그 이름을 읽는다.
            var frameImage = slot.transform.Find("Backboard_Grade").GetComponent<UnityEngine.UI.Image>();
            var designerFrame = !string.IsNullOrEmpty(slot.GradeSprite) ? slot.GradeSprite : frameImage.sprite ? frameImage.sprite.name : null;
            Assert.That(designerFrame, Is.Not.Null.And.Not.Empty, "Slot_Item Backboard_Grade 디자이너 틀");
            int designerGrade = ItemSlot.GradeOfFrame(T, designerFrame);
            Assert.That(designerGrade, Is.GreaterThan(0), "디자이너 틀 " + designerFrame + "이 09_uiResource 등급 틀 중 하나");

            string graded = null, zero = null, noIcon = null;
            foreach (var r in T.Wealth.RawRows)
            {
                string type = r.Get("type"), wIcon = r.Get("icon");
                int g = ItemSlot.GradeOf(T, type);
                if (graded == null && g > 0 && g != designerGrade) graded = type;
                if (zero == null && g == 0 && !string.IsNullOrEmpty(wIcon)) zero = type;
                if (noIcon == null && string.IsNullOrEmpty(wIcon)) noIcon = type;
            }
            Assert.That(graded, Is.Not.Null);
            Assert.That(zero, Is.Not.Null);
            slot.ShowUI(new ItemSlotData(T, graded, "1"));
            Assert.That(slot.GradeSprite, Is.EqualTo(MvUiGrade.Frame(T, ItemSlot.GradeOf(T, graded)).sprite));
            slot.ShowUI(new ItemSlotData(T, zero, "1"));
            Assert.That(slot.GradeSprite, Is.EqualTo(designerFrame), "등급 없는 재화 = 디자이너 틀(앞 보상 틀이 남지 않음)");
            Assert.That(slot.IconShown, Is.True);
            if (noIcon != null)
            {
                slot.ShowUI(new ItemSlotData(T, noIcon, "1"));
                Assert.That(slot.IconShown, Is.False, noIcon + ": icon 빈 재화는 아이콘 숨김");
            }
        }

        /// <summary>
        /// 재화 칸은 어느 화면이든 표 아이콘(Icon, 01_wealth.icon)을 보이고 디자이너 고정 그림 Icon_Dummy는 끈다.
        /// 래퍼 프리팹이 정한다(로비 2026-11-19, 인게임·Area 강화·환생 사용자 2026-11-23 「지금 표 아이콘으로 전환」, MV-UI3-RESAUDIT).
        /// </summary>
        [TestCase("Assets/Game/Addressables/Local/UI/Hud/UIHud_Run.prefab")]
        [TestCase("Assets/Game/Addressables/Local/UI/Lobby/UIScreen_Lobby.prefab")]
        [TestCase("Assets/Game/Addressables/Local/UI/Meta/UIPopup_AreaUpgrade.prefab")]
        [TestCase("Assets/Game/Addressables/Local/UI/Meta/UIPopup_Rebirth.prefab")]
        public void CurrencySlots_InWrappers_ShowTableIcon_NotDummy(string path)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Assert.That(prefab, Is.Not.Null, path);
            var slots = prefab.GetComponentsInChildren<HudCurrencySlot>(true);
            Assert.That(slots, Is.Not.Empty, path);
            foreach (var slot in slots)
            {
                var icon = slot.transform.Find("Icon");
                var dummy = slot.transform.Find("Icon_Dummy");
                Assert.That(icon, Is.Not.Null, slot.name);
                Assert.That(icon.gameObject.activeSelf, Is.True, path + " " + slot.name + ": 표 아이콘 Icon 켬");
                if (dummy) Assert.That(dummy.gameObject.activeSelf, Is.False, path + " " + slot.name + ": Icon_Dummy 끔");
            }
        }

        [Test]
        public void CurrencySlot_OnPrefab_ShowUI_DrawsWealthIconAndText()
        {
            var slot = NewHidden<HudCurrencySlot>("Common/Slot_Currency.prefab");
            AssertRefs(slot);
            var icon = HudCurrencySlot.IconOf(T, "gold");
            Assert.That(icon, Is.Not.Null.And.Not.Empty, "01_wealth gold.icon");

            slot.ShowUI(new CurrencySlotData(T, "gold", "1,234"));
            Assert.That(slot.gameObject.activeSelf, Is.True);
            Assert.That(slot.IconSprite, Is.EqualTo(icon));
            Assert.That(slot.ValueText, Is.EqualTo("1,234"));

            slot.SetText("99");
            Assert.That(slot.ValueText, Is.EqualTo("99"), "글자만 갱신");
            Assert.That(slot.IconSprite, Is.EqualTo(icon), "그림은 그대로");
        }
    }
}
