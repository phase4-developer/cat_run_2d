using jjevol;
using MV.Data;
using TMPro;
using UnityEngine;

namespace MV.UI
{
    /// <summary>
    /// 디자이너 <c>Slot_Weapon</c>(·변형 <c>Slot_Weapon_Stack</c>)의 값 연결(MV-UI2-HUD). 겉(배치·그림)은 프리팹이 정하고, 등급 틀·무기 그림·공격력만 넣는다.
    /// 등급 틀 = 09_uiResource 등급 틀(103_item.tier 번호), 무기 그림 = 103_item.icon(Atlas_UI_Icon).
    /// </summary>
    public sealed class HudWeaponSlot : MonoBehaviour
    {
        [SerializeField] AddressableImage grade;
        [SerializeField] AddressableImage icon;
        /// <summary>공격력 글자(<c>Slot_Attack/Value Text</c>). 스택 변형에서 꺼져 있으면 넣기만 한다.</summary>
        [SerializeField] TMP_Text attack;

        public int Tier { get; private set; }
        public string GradeSprite => grade ? grade.spriteName : null;
        public string IconSprite => icon ? icon.spriteName : null;
        public string AttackText => attack ? attack.text : null;

        /// <summary>단계 tier의 103 행으로 칸을 채운다. dmgMul = 공격력 카드 배수.</summary>
        public void Set(MvTables t, int tier, double dmgMul)
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
