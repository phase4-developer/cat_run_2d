using jjevol;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MV.UI
{
    /// <summary>Area 이름표 표시 데이터: Zone 번호와 Area 이름.</summary>
    public readonly struct AreaLabelData
    {
        public readonly int ZoneNo;
        public readonly string AreaName;

        public AreaLabelData(int zoneNo, string areaName)
        {
            ZoneNo = zoneNo;
            AreaName = areaName;
        }
    }

    /// <summary>
    /// 디자이너 공용 프리팹 <c>Slot_AreaLabel</c>(Area 그림·Zone·Area 이름)의 루트 부품(MV-UI3-LOBBYCUR, 프리팹 단위 관리).
    /// 겉(배치·그림)은 프리팹이 정하고, 글자만 넣는다. 로비·Element_Progress_Item·Popup_AreaUpgrade·Popup_LevelComplete가 같은 프리팹·같은 컴포넌트를 쓴다.
    /// Area 그림은 표에 열이 없어 디자이너 그림을 그대로 둔다(열린 결정).
    /// </summary>
    public sealed class HudAreaLabel : UIBase
    {
        /// <summary>「Zone {0}」(노션).</summary>
        public const string ZoneFormat = "Zone {0}";

        [SerializeField] Image areaTexture;
        /// <summary><c>Zone Level Text (TMP)</c>. 디자이너가 꺼 둔 상태면 켜고 끄지 않고 글만 넣는다.</summary>
        [SerializeField] TMP_Text zoneText;
        /// <summary><c>Area Text (TMP)</c>.</summary>
        [SerializeField] TMP_Text areaText;

        AreaLabelData _data;

        public Image AreaTexture => areaTexture;
        public string ZoneText => zoneText ? zoneText.text : null;
        public string AreaText => areaText ? areaText.text : null;
        public bool HasRefs => areaTexture && zoneText && areaText;

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
        }
    }
}
