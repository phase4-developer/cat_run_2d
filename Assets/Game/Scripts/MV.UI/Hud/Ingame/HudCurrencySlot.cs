using jjevol;
using MV.Data;
using TMPro;
using UnityEngine;

namespace MV.UI
{
    /// <summary>재화 칸 표시 데이터: 01_wealth.type(그림 = 그 행 icon)과 보일 글자.</summary>
    public readonly struct CurrencySlotData
    {
        public readonly MvTables Tables;
        public readonly string WealthType;
        public readonly string Text;

        public CurrencySlotData(MvTables tables, string wealthType, string text)
        {
            Tables = tables;
            WealthType = wealthType;
            Text = text;
        }
    }

    /// <summary>
    /// 디자이너 <c>Slot_Currency</c>(공용 재화 칸)의 값 연결(공용 부품 MV-UI3-HUD2). 겉(배경·배치)은 프리팹이 정하고,
    /// 아이콘 = <see cref="MvConsumeIcon"/>(07_consumeType 재화 행 → 01_wealth.icon, MV-UI-ICONSRC), 글자 = 받은 문자열(숫자 모양은 부르는 쪽이 정한다).
    /// 인게임 상단 줄과 로비가 같은 프리팹·같은 컴포넌트를 쓴다.
    /// </summary>
    public sealed class HudCurrencySlot : UIBase
    {
        [SerializeField] AddressableImage icon;
        /// <summary>수량 글자(<c>Value Text (TMP)</c>).</summary>
        [SerializeField] TMP_Text value;

        CurrencySlotData _data;

        public string WealthType => _data.WealthType;
        public string IconSprite => icon ? icon.spriteName : null;
        public string ValueText => value ? value.text : null;

        /// <summary>데이터를 넣고 켜서 그린다.</summary>
        public void ShowUI(in CurrencySlotData data)
        {
            _data = data;
            ShowUI();
        }

        /// <summary>글자만 바꾼다(그림은 그대로). 꺼져 있으면 값만 두고 켜질 때 그린다.</summary>
        public void SetText(string text)
        {
            _data = new CurrencySlotData(_data.Tables, _data.WealthType, text);
            if (value) value.text = text;
        }

        /// <summary>재화 아이콘 그림 이름(<see cref="MvConsumeIcon.TryWealth"/>). 없으면 null.</summary>
        public static string IconOf(MvTables t, string wealthType) =>
            MvConsumeIcon.TryWealth(t, wealthType, out _, out var sprite) ? sprite : null;

        protected override void _DrawContents()
        {
            MvConsumeIcon.TryWealth(_data.Tables, _data.WealthType, out var atlas, out var sprite);
            HudImage.SetOrHide(icon, atlas, sprite);
            if (value) value.text = _data.Text ?? "";
        }
    }
}
