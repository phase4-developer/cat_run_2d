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
            Assert.That(slot.GradeSprite, Is.EqualTo(HudGrade.Frame(T, 2).sprite), "09 등급 틀");
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

            CardData chosen = null;
            foreach (var c in T.Card.Datas) if (c.active) { chosen = c; break; }
            Assert.That(chosen, Is.Not.Null, "활성 카드 1장");
            card.ShowUI(new SkillCardData(T, chosen.tier, chosen));
            Assert.That(card.QuestionShown, Is.False);
            Assert.That(card.CardId, Is.EqualTo(chosen.id));
            Assert.That(card.FaceSprite, Is.EqualTo(chosen.icon), "확정 카드 그림");
            Assert.That(card.Title, Is.EqualTo(HudGrade.CardName(T, chosen)));
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
