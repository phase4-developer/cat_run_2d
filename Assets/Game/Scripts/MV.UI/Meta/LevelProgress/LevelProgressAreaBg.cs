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
        /// <summary>배너 칸에 그릴 Zone 배너 데이터(<see cref="WithBanner"/>일 때만).</summary>
        public readonly LevelProgressZoneData Zone;

        public LevelProgressAreaBgData(Color? color, bool withBanner, in LevelProgressZoneData zone = default)
        {
            Color = color;
            WithBanner = withBanner;
            Zone = zone;
        }
    }

    /// <summary>
    /// 디자이너 프리팹 <c>Element_Progress_AreaBG</c>의 루트 부품(MV-UI3-LVPROG). <c>Color_Area</c> 칸들의 색과 배너 칸 켜고 끄기,
    /// 배너 칸 안의 Zone 배너(<see cref="LevelProgressZoneBanner"/>)에 데이터 넘기기를 맡는다.
    /// Zone 배너가 배경 층에 있는 까닭: 진척 게이지 뒤에 그려져야 한다(사용자 2026-11-23 「존 배경이 게이지 뒤로 가야하는데」, 결정 카드 「배너를 배경 층으로 이동」).
    /// Area 칸(<c>Element_LevelProgress</c>)에는 같은 높이의 빈 자리(<c>Space_Banner_Zone</c>)만 있다.
    /// </summary>
    public sealed class LevelProgressAreaBg : UIBase
    {
        /// <summary>관문 칸 뒤 <c>Color_Area</c> Image들.</summary>
        [SerializeField] Image[] colorSlots = new Image[0];
        /// <summary>Zone 배너 뒤 칸(<c>Color_Area</c>, 형제 순서 첫째 = 아래). 배너가 없는 Area에서는 끈다.</summary>
        [SerializeField] Image bannerSlot;
        /// <summary>배너 칸(<see cref="bannerSlot"/>) 안의 <c>Element_Progress_Banner_Zone</c>.</summary>
        [SerializeField] LevelProgressZoneBanner zoneBanner;

        LevelProgressAreaBgData _data;

        public bool HasRefs => colorSlots != null && colorSlots.Length > 0 && bannerSlot && zoneBanner && zoneBanner.HasRefs;
        public Image BannerSlot => bannerSlot;
        public LevelProgressZoneBanner ZoneBanner => zoneBanner;
        public int SlotCount => colorSlots != null ? colorSlots.Length : 0;

        public void ShowUI(in LevelProgressAreaBgData data)
        {
            _data = data;
            ShowUI();
        }

        protected override void _DrawContents()
        {
            if (bannerSlot) bannerSlot.gameObject.SetActive(_data.WithBanner);
            if (zoneBanner)
            {
                if (_data.WithBanner) zoneBanner.ShowUI(_data.Zone);
                else zoneBanner.HideUI();
            }
            // 관문 칸은 늘 켠다(디자이너 예시 칸에서 꺼 둔 칸이 남지 않게).
            if (colorSlots != null) foreach (var img in colorSlots) if (img) img.gameObject.SetActive(true);
            if (_data.Color is not Color c) return;
            if (bannerSlot) bannerSlot.color = c;
            if (colorSlots == null) return;
            foreach (var img in colorSlots) if (img) img.color = c;
        }
    }
}
