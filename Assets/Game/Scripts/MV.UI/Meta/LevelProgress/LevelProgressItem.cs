using jjevol;
using TMPro;
using UnityEngine;

namespace MV.UI
{
    /// <summary>LevelProgress 관문 한 칸 표시 데이터.</summary>
    public readonly struct LevelProgressItemData
    {
        public readonly long Milestone;
        /// <summary>Area 첫 관문이면 Area 이름표(<c>Slot_AreaLabel</c>)를 켠다(노션: 「Area 첫번째 단계에서 동적으로 Active」).</summary>
        public readonly bool ShowLabel;
        public readonly AreaLabelData Label;
        /// <summary>Milestone 재화 아이콘(07_consumeType). 비면 숨긴다.</summary>
        public readonly string IconAtlas, Icon;

        public LevelProgressItemData(long milestone, bool showLabel, in AreaLabelData label, string iconAtlas = null, string icon = null)
        {
            Milestone = milestone;
            ShowLabel = showLabel;
            Label = label;
            IconAtlas = iconAtlas;
            Icon = icon;
        }
    }

    /// <summary>
    /// 디자이너 프리팹 <c>Element_Progress_Item</c>의 루트 부품(MV-UI3-LVPROG). Milestone 숫자와 Area 이름표만 맡는다.
    /// 막대·인디케이터 위치는 팝업(<see cref="LevelProgressView"/>)이 <see cref="MilestoneAnchor"/> 위치를 읽어 정한다.
    /// </summary>
    public sealed class LevelProgressItem : UIBase
    {
        /// <summary><c>Milestone</c>(영혼 아이콘 + 숫자 묶음). 막대 눈금 위치 기준.</summary>
        [SerializeField] RectTransform milestone;
        /// <summary><c>Milestone/Value Text (TMP)</c>.</summary>
        [SerializeField] TMP_Text milestoneText;
        /// <summary><c>Milestone/Icon (2)</c> 재화 아이콘(AddressableImage, 프리팹에 그림 없음).</summary>
        [SerializeField] AddressableImage milestoneIcon;
        /// <summary>중첩 <c>Slot_AreaLabel</c>의 <see cref="HudAreaLabel"/>.</summary>
        [SerializeField] HudAreaLabel areaLabel;

        LevelProgressItemData _data;

        public RectTransform MilestoneAnchor => milestone;
        public string MilestoneText => milestoneText ? milestoneText.text : null;
        public HudAreaLabel AreaLabel => areaLabel;
        public string MilestoneIconShown => milestoneIcon && milestoneIcon.enabled ? milestoneIcon.spriteName : null;
        public bool HasRefs => milestone && milestoneText && areaLabel && milestoneIcon;

        public void ShowUI(in LevelProgressItemData data)
        {
            _data = data;
            ShowUI();
        }

        protected override void _DrawContents()
        {
            if (milestoneText) milestoneText.text = MvUiFormat.Fmt(_data.Milestone);
            HudImage.SetOrHide(milestoneIcon, _data.IconAtlas, _data.Icon);
            if (!areaLabel) return;
            if (_data.ShowLabel) areaLabel.ShowUI(_data.Label);
            else areaLabel.HideUI();
        }
    }
}
