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
    /// 공용 <c>Slot_Currency</c>의 데이터 연결. 런 스냅샷은 화면에서 받은 값을 표시하고,
    /// 확정 보유량은 같은 칸에 연결된 Framework <see cref="ResourceLabel"/>이 표시한다.
    /// </summary>
    public sealed class HudCurrencySlot : UIBase
    {
        [SerializeField] AddressableImage icon;
        /// <summary>수량 글자(<c>Value Text (TMP)</c>).</summary>
        [SerializeField] TMP_Text value;
        [SerializeField] ResourceLabel resourceLabel;

        CurrencySlotData _data;
        int _boundResourceId;
        MvConsumePresentation _boundPresentation;

        public string WealthType => _data.WealthType;
        public string IconSprite => icon ? icon.spriteName : null;
        public string ValueText => value ? value.text : null;
        public bool HasBalanceLabel => resourceLabel != null;

        public void ShowBalance(ResourceService wallet, int resourceId, MvConsumePresentation presentation)
        {
            if (!resourceLabel || wallet == null || resourceId == 0)
            {
                if (resourceLabel) resourceLabel.enabled = false;
                HideUI();
                return;
            }
            if (resourceLabel.enabled && gameObject.activeSelf && ReferenceEquals(resourceLabel.Resources, wallet)
                && _boundResourceId == resourceId && ReferenceEquals(_boundPresentation, presentation)) return;

            _boundResourceId = resourceId;
            _boundPresentation = presentation;
            resourceLabel.enabled = true;
            resourceLabel.ShowUI(wallet, wallet.WealthTypeId, resourceId, ResourceQuantityView.Confirmed, presentation);
        }

        /// <summary>데이터를 넣고 켜서 그린다.</summary>
        public void ShowUI(in CurrencySlotData data)
        {
            if (resourceLabel) resourceLabel.enabled = false;
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
            if (resourceLabel && resourceLabel.enabled) return;
            MvConsumeIcon.TryWealth(_data.Tables, _data.WealthType, out var atlas, out var sprite);
            HudImage.SetOrHide(icon, atlas, sprite);
            if (value) value.text = _data.Text ?? "";
        }
    }
}
