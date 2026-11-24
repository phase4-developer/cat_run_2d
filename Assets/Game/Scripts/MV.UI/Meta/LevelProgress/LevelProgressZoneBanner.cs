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
        /// <summary><c>301_zone.mapIcon</c>(Local_Texture 주소). 비면 프리팹 그림을 그대로 둔다.</summary>
        public readonly string ZoneIcon;

        public LevelProgressZoneData(int zoneNo, string zoneName, string zoneIcon = null)
        {
            ZoneNo = zoneNo;
            ZoneName = zoneName;
            ZoneIcon = zoneIcon;
        }
    }

    /// <summary>
    /// 디자이너 프리팹 <c>Element_Progress_Banner_Zone</c>의 루트 부품(MV-UI3-LVPROG). Zone 번호·이름 글자와
    /// <c>Zone Texture</c> 그림(<c>301_zone.mapIcon</c>, MV-UI3-ZONETEX)을 넣는다.
    /// </summary>
    public sealed class LevelProgressZoneBanner : UIBase
    {
        /// <summary><c>Zone Number Text (TMP)</c>, 「Zone {0}」(<see cref="MvUiText.ZoneNumber"/>).</summary>
        [SerializeField] TMP_Text zoneNumberText;
        /// <summary><c>Zone Title Text (TMP)</c>, <c>name:&lt;301_zone id&gt;</c>.</summary>
        [SerializeField] TMP_Text zoneTitleText;
        /// <summary><c>Zone Texture</c>(AddressableTexture). 주소는 <c>301_zone.mapIcon</c>.</summary>
        [SerializeField] AddressableTexture zoneTexture;

        LevelProgressZoneData _data;

        public string ZoneNumberText => zoneNumberText ? zoneNumberText.text : null;
        public string ZoneTitleText => zoneTitleText ? zoneTitleText.text : null;
        public string ZoneTextureKey => zoneTexture ? zoneTexture.spriteKey : null;
        public bool HasRefs => zoneNumberText && zoneTitleText && zoneTexture;

        public void ShowUI(in LevelProgressZoneData data)
        {
            _data = data;
            ShowUI();
        }

        protected override void _DrawContents()
        {
            if (zoneNumberText) zoneNumberText.text = MvUiText.ZoneNumber(_data.ZoneNo);
            if (zoneTitleText) zoneTitleText.text = _data.ZoneName ?? "";
            HudImage.Set(zoneTexture, _data.ZoneIcon);
        }
    }
}
