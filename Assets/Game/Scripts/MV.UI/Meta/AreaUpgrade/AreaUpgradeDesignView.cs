using System;
using System.Collections.Generic;
using jjevol;
using MV.Data;
using TMPro;
using UnityEngine;
using UnityEngine.Pool;
using UnityEngine.UI;

namespace MV.UI
{
    /// <summary>Area 업그레이드 디자이너 겉이 읽는 값(<see cref="MetaPanelModel.Data"/>). 칸 값은 각 행의 <see cref="MetaRow.Data"/>.</summary>
    public sealed class AreaUpgradeViewData
    {
        public MvTables Tables;
        public int ZoneNo;
        /// <summary><c>302_area.areaNo</c>(1기반).</summary>
        public int AreaNo;
        public string AreaName;
        /// <summary><c>302_area.mapIcon</c>(Local_Texture 주소).</summary>
        public string AreaIcon;
        public ResourceService Wallet;
        /// <summary>배너 보상 글자(시안 「x100」).</summary>
        public string AdGainText;
        /// <summary>광고 보상 첫 항목(08_adPlacement.adReward) 종류·id. 배너 아이콘은 그 07_consumeType 아이콘(MV-UI-ICONSRC).</summary>
        public int AdRewardType, AdRewardId;
        /// <summary>배너 버튼 행동 id(사용 가능 여부는 그 행동의 Enabled).</summary>
        public string AdActionId;
    }

    /// <summary>
    /// 디자이너 <c>Popup_AreaUpgrade</c>(Art/UI/Prefab/UIPopup)을 Area 업그레이드 팝업 겉으로 쓰는 연결(MV-UI2-AREAUP, 노션 [팝업] Area 업그레이드).
    /// <c>UIPopup_AreaUpgrade</c> 래퍼 프리팹 루트에 붙고, 참조는 래퍼 안 중첩 인스턴스를 직렬화로 가리킨다(로비 구조안 A와 같음). 디자이너 원본 팝업은 바꾸지 않는다.
    /// 강화 칸은 디자이너가 넣어 둔 첫 <c>Element_Upgrade</c>를 원형으로 표 행 수만큼 풀에서 빌린다(칸 수는 코드에 없음).
    /// 시안에 없는 기획서 요소(광고 남은 횟수, 제목 부제)는 그리지 않는다.
    /// </summary>
    public sealed class AreaUpgradeDesignView : MonoBehaviour, IMetaPanelView
    {
        /// <summary>배너 보상 글자 「x{0}」(시안).</summary>
        public const string AdGainKey = "ui.aup.adGain";
        public const string AdGainFallback = "x{0}";

        [SerializeField] Button closeButton;
        [SerializeField] LobbyCurrencyParts[] currencies;
        [SerializeField] HudAreaLabel areaLabel;

        [Header("Banner_AdsReward")]
        [SerializeField] Button adButton;
        /// <summary><c>Banner/Current Value Text (TMP)</c>.</summary>
        [SerializeField] TMP_Text adGain;
        /// <summary><c>Banner_AdsReward</c> 보상 재화 아이콘(AddressableImage, 프리팹에 그림 없음).</summary>
        [SerializeField] AddressableImage adRewardIcon;

        [Header("Upgrade List Scroll View")]
        [SerializeField] RectTransform listContent;
        /// <summary>디자이너가 넣어 둔 첫 칸. 원형이자 첫 칸으로 쓴다.</summary>
        [SerializeField] UpgradeElement elementTemplate;

        public event Action ClosePressed;
        public event Action<string> ActionPressed;
        /// <summary>막 누름: 디자이너 팝업에 막 버튼이 없어 내지 않는다(Area 강화는 막으로 닫히지 않음, 05 §5.8).</summary>
        public event Action DimPressed { add { } remove { } }
        /// <summary>탭 없음.</summary>
        public event Action<string> TabPressed { add { } remove { } }

        readonly List<UpgradeElement> _elements = new List<UpgradeElement>();
        ObjectPool<UpgradeElement> _pool;
        string _adActionId;
        bool _ready;

        public IReadOnlyList<UpgradeElement> Elements => _elements;
        public HudAreaLabel AreaLabel => areaLabel;
        public IReadOnlyList<LobbyCurrencyParts> Currencies => currencies;
        public Button AdButton => adButton;
        public string AdGainText => adGain ? adGain.text : null;
        public string AdRewardIconShown => adRewardIcon && adRewardIcon.enabled ? adRewardIcon.spriteName : null;

        /// <summary>프리팹 연결 확인(시험·검사용): 빠진 참조 이름 목록.</summary>
        public string MissingRefs()
        {
            var miss = new List<string>();
            if (!closeButton) miss.Add(nameof(closeButton));
            if (currencies == null || currencies.Length == 0) miss.Add(nameof(currencies));
            else for (int i = 0; i < currencies.Length; i++)
                    if (currencies[i] == null || !currencies[i].slot || !currencies[i].slot.HasBalanceLabel || currencies[i].resourceId == 0) miss.Add("currencies[" + i + "]");
            if (!areaLabel) miss.Add(nameof(areaLabel));
            if (!adButton) miss.Add(nameof(adButton));
            if (!adGain) miss.Add(nameof(adGain));
            if (!adRewardIcon) miss.Add(nameof(adRewardIcon));
            if (!listContent) miss.Add(nameof(listContent));
            if (!elementTemplate) miss.Add(nameof(elementTemplate));
            else
            {
                string e = elementTemplate.MissingRefs();
                if (e.Length > 0) miss.Add("elementTemplate(" + e + ")");
            }
            return string.Join(", ", miss);
        }

        void Init()
        {
            if (_ready) return;
            _ready = true;
            if (closeButton) closeButton.onClick.AddListener(() => ClosePressed?.Invoke());
            if (adButton) adButton.onClick.AddListener(() => { if (!string.IsNullOrEmpty(_adActionId)) ActionPressed?.Invoke(_adActionId); });
            _pool = new ObjectPool<UpgradeElement>(CreateElement, e => e.gameObject.SetActive(true), e => e.gameObject.SetActive(false),
                e => { if (e && e != elementTemplate) Destroy(e.gameObject); });
            if (elementTemplate) elementTemplate.gameObject.SetActive(false);
        }

        /// <summary>첫 칸은 디자이너 원본을 그대로 쓰고, 그 뒤는 복제한다.</summary>
        UpgradeElement CreateElement()
        {
            UpgradeElement e;
            if (!_templateTaken && elementTemplate) { e = elementTemplate; _templateTaken = true; }
            else e = Instantiate(elementTemplate, listContent ? listContent : elementTemplate.transform.parent, false);
            e.Pressed += id => ActionPressed?.Invoke(id);
            return e;
        }

        bool _templateTaken;

        void Awake() => Init();

        public void Render(MetaPanelModel model)
        {
            Init();
            var d = model?.Data as AreaUpgradeViewData;
            if (d == null) return;

            if (areaLabel) areaLabel.ShowUI(new AreaLabelData(d.ZoneNo, d.AreaNo, d.AreaName, d.AreaIcon));
            LobbyCurrencyParts.BindBalances(currencies, d.Tables, d.Wallet);

            _adActionId = d.AdActionId;
            if (adGain) adGain.text = d.AdGainText ?? "";
            MvConsumeIcon.TryGet(d.Tables, d.AdRewardType, d.AdRewardId, out var adAtlas, out var adSprite);
            HudImage.SetOrHide(adRewardIcon, adAtlas, adSprite);
            if (adButton) adButton.interactable = model.FindAction(d.AdActionId)?.Enabled == true;

            int n = 0;
            foreach (var row in model.Rows)
            {
                if (!(row.Data is UpgradeElementData item)) continue;
                // 메타가 없으면(Ready false) 화면 코드가 행동을 끈다 → 살 수 있음도 막는다.
                if (item.State == UpgradeElementState.Ready && model.FindAction(item.ActionId)?.Enabled != true)
                    item = item.WithState(UpgradeElementState.Disabled);
                UpgradeElement e;
                if (n < _elements.Count) e = _elements[n];
                else { e = _pool.Get(); _elements.Add(e); }
                e.transform.SetSiblingIndex(n);
                e.ShowUI(item);
                n++;
            }
            for (int i = _elements.Count - 1; i >= n; i--)
            {
                _pool.Release(_elements[i]);
                _elements.RemoveAt(i);
            }
        }
    }
}
