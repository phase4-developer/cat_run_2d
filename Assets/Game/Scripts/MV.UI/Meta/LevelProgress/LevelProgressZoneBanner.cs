using jjevol;
using TMPro;
using UnityEngine;

namespace MV.UI
{
    /// <summary>LevelProgress Zone 배너 표시 데이터.</summary>
    public readonly struct LevelProgressZoneData
    {
        public readonly int ZoneNo;
        public readonly string ZoneName;

        public LevelProgressZoneData(int zoneNo, string zoneName)
        {
            ZoneNo = zoneNo;
            ZoneName = zoneName;
        }
    }

    /// <summary>
    /// 디자이너 프리팹 <c>Element_Progress_Banner_Zone</c>의 루트 부품(MV-UI3-LVPROG). Zone 번호·이름 글자만 넣는다.
    /// <c>Zone Texture</c> 그림은 Zone별 그림 열이 정해지지 않아 디자이너 그림을 그대로 둔다(열린 결정, <see cref="HudAreaLabel"/>과 같음).
    /// </summary>
    public sealed class LevelProgressZoneBanner : UIBase
    {
        /// <summary><c>Zone Number Text (TMP)</c>, 「Zone {0}」(<see cref="HudAreaLabel.ZoneFormat"/>).</summary>
        [SerializeField] TMP_Text zoneNumberText;
        /// <summary><c>Zone Title Text (TMP)</c>, <c>name:&lt;301_zone id&gt;</c>.</summary>
        [SerializeField] TMP_Text zoneTitleText;

        LevelProgressZoneData _data;

        public string ZoneNumberText => zoneNumberText ? zoneNumberText.text : null;
        public string ZoneTitleText => zoneTitleText ? zoneTitleText.text : null;
        public bool HasRefs => zoneNumberText && zoneTitleText;

        public void ShowUI(in LevelProgressZoneData data)
        {
            _data = data;
            ShowUI();
        }

        protected override void _DrawContents()
        {
            if (zoneNumberText) zoneNumberText.text = string.Format(HudAreaLabel.ZoneFormat, _data.ZoneNo);
            if (zoneTitleText) zoneTitleText.text = _data.ZoneName ?? "";
        }
    }
}
