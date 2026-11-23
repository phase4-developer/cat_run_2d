using jjevol;
using MV.Data;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MV.UI
{
    /// <summary>Area 이름표 표시 데이터: Zone 번호, Area 이름, Area 그림 주소.</summary>
    public readonly struct AreaLabelData
    {
        public readonly int ZoneNo;
        public readonly string AreaName;
        /// <summary><c>302_area.mapIcon</c>(Local_Texture 주소). 비면 프리팹 그림을 그대로 둔다.</summary>
        public readonly string AreaIcon;

        public AreaLabelData(int zoneNo, string areaName, string areaIcon = null)
        {
            ZoneNo = zoneNo;
            AreaName = areaName;
            AreaIcon = areaIcon;
        }
    }

    /// <summary>
    /// 디자이너 공용 프리팹 <c>Slot_AreaLabel</c>(Area 그림·Zone·Area 이름)의 루트 부품(MV-UI3-LOBBYCUR, 프리팹 단위 관리).
    /// 겉(배치)은 프리팹이 정하고, 글자와 Area 그림(<c>302_area.mapIcon</c>, MV-UI3-ZONETEX)을 넣는다.
    /// 로비·Element_Progress_Item·Popup_AreaUpgrade·Popup_LevelComplete가 같은 프리팹·같은 컴포넌트를 쓴다.
    /// </summary>
    public sealed class HudAreaLabel : UIBase
    {
        /// <summary>「Zone {0}」(노션).</summary>
        public const string ZoneFormat = "Zone {0}";

        /// <summary><c>AreaTexture</c>(AddressableTexture). 주소는 <c>302_area.mapIcon</c>.</summary>
        [SerializeField] AddressableTexture areaTexture;
        /// <summary><c>Zone Level Text (TMP)</c>. 디자이너가 꺼 둔 상태면 켜고 끄지 않고 글만 넣는다.</summary>
        [SerializeField] TMP_Text zoneText;
        /// <summary><c>Area Text (TMP)</c>.</summary>
        [SerializeField] TMP_Text areaText;

        AreaLabelData _data;

        public Image AreaTexture => areaTexture;
        public string AreaTextureKey => areaTexture ? areaTexture.spriteKey : null;
        public string ZoneText => zoneText ? zoneText.text : null;
        public string AreaText => areaText ? areaText.text : null;
        public bool HasRefs => areaTexture && zoneText && areaText;

        /// <summary>areaNo(1기반)의 <c>302_area.mapIcon</c>. 없으면 null.</summary>
        public static string AreaIconOf(MvTables tables, int areaNo)
        {
            if (tables == null) return null;
            foreach (var a in tables.Area.Datas)
                if (a.areaNo == areaNo) return string.IsNullOrEmpty(a.mapIcon) ? null : a.mapIcon;
            return null;
        }

        /// <summary>데이터를 넣고 켜서 그린다.</summary>
        public void ShowUI(in AreaLabelData data)
        {
            _data = data;
            ShowUI();
        }

        protected override void _DrawContents()
        {
            if (zoneText) zoneText.text = string.Format(ZoneFormat, _data.ZoneNo);
            if (areaText) areaText.text = _data.AreaName ?? "";
            HudImage.Set(areaTexture, _data.AreaIcon);
        }
    }
}
