using jjevol;
using MV.Data;
using TMPro;
using UnityEngine;

namespace MV.UI
{
    /// <summary>무기 칸 표시 데이터: 103_item 단계와 공격력 배수(공격력 카드).</summary>
    public readonly struct WeaponSlotData
    {
        public readonly MvTables Tables;
        public readonly int Tier;
        public readonly double DmgMul;

        public WeaponSlotData(MvTables tables, int tier, double dmgMul)
        {
            Tables = tables;
            Tier = tier;
            DmgMul = dmgMul;
        }
    }

    /// <summary>
    /// 디자이너 <c>Slot_Weapon</c>(·변형 <c>Slot_Weapon_Stack</c>)의 값 연결(MV-UI2-HUD, 공용 부품 MV-UI3-HUD2). 겉(배치·그림)은 프리팹이 정하고, 등급 틀·무기 그림·공격력만 넣는다.
    /// 등급 틀 = 09_uiResource 등급 틀(103_item.tier 번호), 무기 그림 = 103_item.icon(Atlas_UI_Icon).
    /// 섹션은 <see cref="ShowUI(in WeaponSlotData)"/>로 켜고 그린다. 연출(등장·모임·튐)은 무기 섹션 단계에서 이 부품에 붙인다.
    /// </summary>
    public sealed class HudWeaponSlot : UIBase
    {
        [SerializeField] AddressableImage grade;
        [SerializeField] AddressableImage icon;
        /// <summary>공격력 글자(<c>Slot_Attack/Value Text</c>). 스택 변형에서 꺼져 있으면 넣기만 한다.</summary>
        [SerializeField] TMP_Text attack;

        public int Tier { get; private set; }
        public string GradeSprite => grade ? grade.spriteName : null;
        public string IconSprite => icon ? icon.spriteName : null;
        public string AttackText => attack ? attack.text : null;

        WeaponSlotData _data;

        /// <summary>데이터를 넣고 켜서 그린다(UIBase 수명: 켜기 → Refresh → 그리기).</summary>
        public void ShowUI(in WeaponSlotData data)
        {
            _data = data;
            ShowUI();
        }

        protected override void _DrawContents() => Draw(_data.Tables, _data.Tier, _data.DmgMul);

        /// <summary>켜고 끄지 않고 지금 그린다(화면 조합 전 기존 호출용). 단계 tier의 103 행으로 칸을 채운다. dmgMul = 공격력 카드 배수.</summary>
        public void Set(MvTables t, int tier, double dmgMul)
        {
            _data = new WeaponSlotData(t, tier, dmgMul);
            Draw(t, tier, dmgMul);
        }

        void Draw(MvTables t, int tier, double dmgMul)
        {
            Tier = tier;
            ItemData item = null;
            if (t != null) foreach (var it in t.Item.Datas) if (it.tier == tier) { item = it; break; }
            HudImage.Set(grade, HudGrade.Frame(t, tier));
            if (item != null) HudImage.Set(icon, MvAtlas.UiIcon, item.icon);
            if (attack) attack.text = item != null ? MvUiFormat.Fmt(item.dmg * dmgMul) : "";
        }
    }

    /// <summary>AddressableImage에 표 그림을 넣는 공통 도우미(같은 그림이면 다시 읽지 않는다).</summary>
    public static class HudImage
    {
        public static void Set(AddressableImage img, UiResourceData row)
        {
            if (row != null) Set(img, row.atlas, row.sprite);
        }

        public static void Set(AddressableImage img, string atlas, string sprite)
        {
            if (!img || string.IsNullOrEmpty(atlas) || string.IsNullOrEmpty(sprite)) return;
            if (img.spriteName != sprite || img.atlasKey != atlas) img.SetAtlasSprite(atlas, sprite);
        }
    }
}
