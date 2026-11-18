using jjevol;
using TMPro;
using UnityEngine;

namespace MV.UI
{
    /// <summary>
    /// 디자이너 공용 프리팹 <c>Element_UnlockRebirth</c>(환생 횟수 표기)의 루트 부품(MV-UI2-AREAUP, 노션 [팝업] Area 업그레이드).
    /// <c>Badge_Count/Count Text (TMP)</c>에 필요한 환생 수만 넣는다. 겉은 프리팹이 정한다.
    /// </summary>
    public sealed class UnlockRebirthBadge : UIBase
    {
        [SerializeField] TMP_Text countText;

        int _count;

        public string CountText => countText ? countText.text : null;
        public bool HasRefs => countText;

        /// <summary>필요한 환생 수를 넣고 켜서 그린다.</summary>
        public void ShowUI(int count)
        {
            _count = count;
            ShowUI();
        }

        protected override void _DrawContents()
        {
            if (countText) countText.text = _count.ToString();
        }
    }
}
