using System;
using System.Collections.Generic;
using jjevol;
using MV.Data;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using FwConsume = jjevol.ConsumeData;
using MvConsume = MV.Data.ConsumeData;

namespace MV.UI
{
    /// <summary>강화 칸 버튼 상태(노션 [팝업] Area 업그레이드 Button Group).</summary>
    public enum UpgradeElementState
    {
        /// <summary>살 수 있음: 업그레이드 버튼 켬.</summary>
        Ready,
        /// <summary>재화 부족 등: 업그레이드 버튼의 <c>Disable</c>을 켜고 누름 막음.</summary>
        Disabled,
        /// <summary>최대 단계: <c>Disable</c> 켜고 가격 자리에 「MAX」, 다음 값 숨김.</summary>
        Max,
        /// <summary>환생 조건 미달(이미 1레벨 이상): 내용은 보이고 잠김 버튼에 필요한 환생 수 표시.</summary>
        Locked,
        /// <summary>1레벨부터 환생 조건 미달: 칸 전체 <c>Lock</c>(내용·버튼 숨김)에 필요한 환생 수 표시(사용자 2026-11-19).</summary>
        LockedAll,
    }

    /// <summary>강화 칸 1개 표시 데이터. 값 글자는 부르는 쪽이 표 <c>valueFormat</c>으로 만든다.</summary>
    public readonly struct UpgradeElementData
    {
        /// <summary>누르면 넘길 행동 id(화면 코드의 <see cref="MetaAction.Id"/>).</summary>
        public readonly string ActionId;
        /// <summary>표 icon 열(<c>Atlas_UI_Icon</c>). 비면 디자이너 그림을 그대로 둔다.</summary>
        public readonly string IconSprite;
        public readonly string Name;
        public readonly int Level;
        public readonly string CurrentValue;
        /// <summary>다음 단계 값. 최대면 null.</summary>
        public readonly string NextValue;
        /// <summary>가격 글자(숫자만, 아이콘은 프리팹). 최대면 「MAX」.</summary>
        public readonly string PriceText;
        public readonly UpgradeElementState State;
        /// <summary>잠김일 때 필요한 환생 수.</summary>
        public readonly int NeedRebirth;
        /// <summary>다음 단계 가격(최대면 null). <see cref="Wallet"/>이 있으면 <c>PriceLabel</c>이 이 값으로 글자·부족 색을 그린다.</summary>
        public readonly IReadOnlyList<MvConsume> Price;
        /// <summary>Framework 재화 서비스. 없으면 <see cref="PriceText"/>만 쓴다.</summary>
        public readonly ResourceService Wallet;

        public UpgradeElementData(string actionId, string iconSprite, string name, int level, string currentValue, string nextValue,
            string priceText, UpgradeElementState state, int needRebirth, IReadOnlyList<MvConsume> price = null, ResourceService wallet = null)
        {
            ActionId = actionId; IconSprite = iconSprite; Name = name; Level = level;
            CurrentValue = currentValue; NextValue = nextValue; PriceText = priceText; State = state; NeedRebirth = needRebirth;
            Price = price; Wallet = wallet;
        }

        public UpgradeElementData WithState(UpgradeElementState state)
            => new UpgradeElementData(ActionId, IconSprite, Name, Level, CurrentValue, NextValue, PriceText, state, NeedRebirth, Price, Wallet);

        /// <summary>1레벨부터 잠겼으면 칸 전체 잠금, 아니면 버튼 잠금.</summary>
        public static UpgradeElementState LockState(int level) => level <= 0 ? UpgradeElementState.LockedAll : UpgradeElementState.Locked;
    }

    /// <summary>
    /// 디자이너 공용 프리팹 <c>Element_Upgrade</c>의 루트 부품(MV-UI2-AREAUP). 환생 업그레이드·Area 업그레이드 팝업이 같은 프리팹·같은 컴포넌트를 쓴다.
    /// 겉(배치·그림)은 프리팹이 정하고 코드는 글자·켜고 끄기만 한다. 가격 글자는 Framework <c>PriceLabel</c>(보유량 부족이면 부족 색, 재화 바뀌면 다시 그림).
    /// 칸 전체 잠김 <c>Lock</c>은 1레벨부터 잠긴 칸에만 켠다.
    /// </summary>
    public sealed class UpgradeElement : UIBase
    {
        /// <summary>「Lv.{0}」(시안 <c>Slot_Level</c>).</summary>
        public const string LevelKey = "ui.aup.lv";
        public const string LevelFallback = "Lv.{0}";

        [Header("Contents")]
        /// <summary><c>Slot_Icon/Icon</c>. 표 icon이 있을 때만 바꾼다(AddressableImage가 없으면 그림 유지).</summary>
        [SerializeField] AddressableImage icon;
        [SerializeField] TMP_Text label;
        [SerializeField] TMP_Text level;
        [SerializeField] TMP_Text currentValue;
        [SerializeField] TMP_Text upgradeValue;
        /// <summary><c>Stat/Icon</c>(현재 → 다음 화살표).</summary>
        [SerializeField] GameObject arrow;

        [Header("Button Group")]
        [SerializeField] GameObject okRoot;
        [SerializeField] Button okButton;
        [SerializeField] GameObject okDisable;
        /// <summary><c>Button_Ok/Icon</c>(가격 재화 아이콘). 최대면 끈다.</summary>
        [SerializeField] GameObject priceIcon;
        [SerializeField] TMP_Text priceText;
        /// <summary>가격 글자에 붙은 Framework <c>PriceLabel</c>(아이콘은 비워 프리팹 그림 유지).</summary>
        [SerializeField] jjevol.PriceLabel priceLabel;
        [SerializeField] GameObject lockRoot;
        [SerializeField] UnlockRebirthBadge lockBadge;

        [Header("Lock (칸 전체)")]
        [SerializeField] GameObject contentsRoot;
        [SerializeField] GameObject buttonGroup;
        [SerializeField] GameObject rowLock;
        [SerializeField] UnlockRebirthBadge rowLockBadge;

        UpgradeElementData _data;
        bool _hooked;

        /// <summary>업그레이드 버튼을 눌렀다(<see cref="UpgradeElementData.ActionId"/>). <see cref="UpgradeElementState.Ready"/>일 때만.</summary>
        public event Action<string> Pressed;

        public UpgradeElementData Data => _data;
        public string LabelText => label ? label.text : null;
        public string LevelText => level ? level.text : null;
        public string CurrentText => currentValue ? currentValue.text : null;
        public string UpgradeText => upgradeValue ? upgradeValue.text : null;
        public string PriceLabel => priceText ? priceText.text : null;
        public bool OkShown => okRoot && okRoot.activeSelf;
        public bool OkDisabled => okDisable && okDisable.activeSelf;
        public bool LockShown => lockRoot && lockRoot.activeSelf;
        public UnlockRebirthBadge LockBadge => lockBadge;
        public bool RowLockShown => rowLock && rowLock.activeSelf;
        public bool ContentsShown => contentsRoot && contentsRoot.activeSelf;
        public UnlockRebirthBadge RowLockBadge => rowLockBadge;
        public bool HasPriceLabel => priceLabel;
        public Button OkButton => okButton;

        /// <summary>프리팹 연결 확인(시험·검사용): 빠진 참조 이름 목록.</summary>
        public string MissingRefs()
        {
            var miss = new System.Collections.Generic.List<string>();
            if (!label) miss.Add(nameof(label));
            if (!level) miss.Add(nameof(level));
            if (!currentValue) miss.Add(nameof(currentValue));
            if (!upgradeValue) miss.Add(nameof(upgradeValue));
            if (!okRoot) miss.Add(nameof(okRoot));
            if (!okButton) miss.Add(nameof(okButton));
            if (!okDisable) miss.Add(nameof(okDisable));
            if (!priceText) miss.Add(nameof(priceText));
            if (!lockRoot) miss.Add(nameof(lockRoot));
            if (!lockBadge) miss.Add(nameof(lockBadge));
            if (!priceLabel) miss.Add(nameof(priceLabel));
            if (!contentsRoot) miss.Add(nameof(contentsRoot));
            if (!buttonGroup) miss.Add(nameof(buttonGroup));
            if (!rowLock) miss.Add(nameof(rowLock));
            if (!rowLockBadge) miss.Add(nameof(rowLockBadge));
            return string.Join(", ", miss);
        }

        void Hook()
        {
            if (_hooked) return;
            _hooked = true;
            if (okButton) okButton.onClick.AddListener(OnOk);
        }

        void OnOk()
        {
            if (_data.State != UpgradeElementState.Ready || string.IsNullOrEmpty(_data.ActionId)) return;
            Pressed?.Invoke(_data.ActionId);
        }

        /// <summary>데이터를 넣고 켜서 그린다.</summary>
        public void ShowUI(in UpgradeElementData data)
        {
            _data = data;
            Hook();
            ShowUI();
        }

        protected override void _DrawContents()
        {
            var d = _data;
            if (!string.IsNullOrEmpty(d.IconSprite)) HudImage.Set(icon, MvAtlas.UiIcon, d.IconSprite);
            if (label) label.text = d.Name ?? "";
            if (level) level.text = string.Format(MvUiText.Get("ui", LevelKey, LevelFallback), d.Level);
            if (currentValue) currentValue.text = d.CurrentValue ?? "";
            bool hasNext = d.State != UpgradeElementState.Max && d.NextValue != null;
            if (upgradeValue)
            {
                upgradeValue.gameObject.SetActive(hasNext);
                upgradeValue.text = d.NextValue ?? "";
            }
            if (arrow) arrow.SetActive(hasNext);

            bool all = d.State == UpgradeElementState.LockedAll;
            if (contentsRoot) contentsRoot.SetActive(!all);
            if (buttonGroup) buttonGroup.SetActive(!all);
            if (rowLock) rowLock.SetActive(all);
            if (all && rowLockBadge) rowLockBadge.ShowUI(d.NeedRebirth);

            bool locked = d.State == UpgradeElementState.Locked;
            if (okRoot) okRoot.SetActive(!locked && !all);
            if (okDisable) okDisable.SetActive(d.State == UpgradeElementState.Disabled || d.State == UpgradeElementState.Max);
            if (okButton) okButton.interactable = d.State == UpgradeElementState.Ready;
            if (priceIcon) priceIcon.SetActive(d.State != UpgradeElementState.Max);
            DrawPrice(d);
            if (lockRoot) lockRoot.SetActive(locked);
            if (locked && lockBadge) lockBadge.ShowUI(d.NeedRebirth);
        }

        /// <summary>
        /// 재화 서비스가 있으면 <c>PriceLabel</c>(부족 색은 Framework, 숫자는 게임 기본 표기 05 §5.1.12 — MvUiBoot이 넣는 MvUiFormat.Profile, MV-FW-NUMFMT),
        /// 없으면 글자만. 최대는 「MAX」를 무료 글자로 넘겨 보통 색으로 둔다.
        /// </summary>
        void DrawPrice(in UpgradeElementData d)
        {
            bool live = priceLabel && d.Wallet != null && !d.Wallet.IsDisposed;
            if (priceLabel) priceLabel.enabled = live;
            if (live)
            {
                if (d.State == UpgradeElementState.Max && !string.IsNullOrWhiteSpace(d.PriceText)) { priceLabel.ShowFree(d.Wallet, d.PriceText); return; }
                var costs = ToFramework(d.Price);
                if (costs.Count > 0 && d.State != UpgradeElementState.Locked && d.State != UpgradeElementState.LockedAll)
                {
                    priceLabel.ShowCosts(d.Wallet, costs);
                    return;
                }
                priceLabel.enabled = false;
            }
            if (priceText) priceText.text = d.PriceText ?? "";
        }

        static List<FwConsume> ToFramework(IReadOnlyList<MvConsume> price)
        {
            var list = new List<FwConsume>();
            if (price == null) return list;
            foreach (var c in price)
                list.Add(new FwConsume(new ConsumeTypeId(c.TypeId), c.Id, (long)Math.Ceiling(c.Value)));
            return list;
        }

        protected override void OnDestroy()
        {
            if (okButton) okButton.onClick.RemoveListener(OnOk);
            base.OnDestroy();
        }
    }
}
