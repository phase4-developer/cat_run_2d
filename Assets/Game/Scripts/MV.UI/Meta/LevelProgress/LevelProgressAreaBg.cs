using jjevol;
using UnityEngine;
using UnityEngine.UI;

namespace MV.UI
{
    /// <summary>LevelProgress Area 배경 한 칸 표시 데이터.</summary>
    public readonly struct LevelProgressAreaBgData
    {
        /// <summary>칠할 색(<c>302_area.mapColor</c>). null이면 디자이너 색을 그대로 둔다.</summary>
        public readonly Color? Color;
        /// <summary>Zone 배너가 있는 Area면 배너 뒤 칸까지 켠다(노션: 배너 Area는 4칸, 나머지 3칸).</summary>
        public readonly bool WithBanner;

        public LevelProgressAreaBgData(Color? color, bool withBanner)
        {
            Color = color;
            WithBanner = withBanner;
        }
    }

    /// <summary>
    /// 디자이너 프리팹 <c>Element_Progress_AreaBG</c>의 루트 부품(MV-UI3-LVPROG). <c>Color_Area</c> 칸들의 색과 배너 칸 켜고 끄기만 맡는다.
    /// </summary>
    public sealed class LevelProgressAreaBg : UIBase
    {
        /// <summary>관문 칸 뒤 <c>Color_Area</c> Image들.</summary>
        [SerializeField] Image[] colorSlots = new Image[0];
        /// <summary>Zone 배너 뒤 칸(<c>Color_Area</c>, 형제 순서 첫째 = 아래). 배너가 없는 Area에서는 끈다.</summary>
        [SerializeField] Image bannerSlot;

        LevelProgressAreaBgData _data;

        public bool HasRefs => colorSlots != null && colorSlots.Length > 0 && bannerSlot;
        public Image BannerSlot => bannerSlot;
        public int SlotCount => colorSlots != null ? colorSlots.Length : 0;

        public void ShowUI(in LevelProgressAreaBgData data)
        {
            _data = data;
            ShowUI();
        }

        protected override void _DrawContents()
        {
            if (bannerSlot) bannerSlot.gameObject.SetActive(_data.WithBanner);
            // 관문 칸은 늘 켠다(디자이너 예시 칸에서 꺼 둔 칸이 남지 않게).
            if (colorSlots != null) foreach (var img in colorSlots) if (img) img.gameObject.SetActive(true);
            if (_data.Color is not Color c) return;
            if (bannerSlot) bannerSlot.color = c;
            if (colorSlots == null) return;
            foreach (var img in colorSlots) if (img) img.color = c;
        }
    }
}
