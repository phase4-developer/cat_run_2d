using System;
using System.Collections.Generic;
using jjevol;
using MV.Data;
using TMPro;
using UnityEngine;
using UnityEngine.Pool;
using UnityEngine.UI;
using MvConsume = MV.Data.ConsumeData;

namespace MV.UI
{
    /// <summary>환생 Header 상태(노션 [팝업] 환생 업그레이드 1~3).</summary>
    public enum RebirthHeaderState
    {
        /// <summary>미해금: Lock 켬(해금 조건 진행), 쿨타임 줄·환생 버튼 끔.</summary>
        Locked,
        /// <summary>쿨타임: 타이머·스킵 버튼, 환생 버튼 Disable(작동 X).</summary>
        Cooldown,
        /// <summary>환생 가능: 바 1·완료 글자, 환생 버튼 켬.</summary>
        Ready,
    }

    /// <summary>환생 디자이너 겉이 읽는 값(<see cref="MetaPanelModel.Data"/>). 특전 칸 값은 각 행의 <see cref="MetaRow.Data"/>.</summary>
    public sealed class RebirthViewData
    {
        public MvTables Tables;
        public ResourceService Wallet;
        public string Title;
        public readonly List<AreaUpgradeCurrency> Currencies = new List<AreaUpgradeCurrency>();
        public int Level;
        public RebirthHeaderState State;

        public string UnlockText;
        public int UnlockCurrent;
        public int UnlockTarget;

        public string CooldownLabel;
        public string CooldownText;
        public string CompleteText;
        public float CooldownFill;

        public string AdLabel;
        public string AdActionId;
        public string SkipDiaLabel;
        public string SkipDiaActionId;
        public IReadOnlyList<MvConsume> SkipDiaPrice = Array.Empty<MvConsume>();

        public string GoLabel;
        public string GainText;
        public string GoActionId;
    }

    /// <summary>
    /// 디자이너 <c>Popup_RebirthUpgrade</c>(Art/UI/Prefab/UIPopup)을 환생 팝업 겉으로 쓰는 연결(MV-UI2-REBIRTH, 노션 [팝업] 환생 업그레이드).
    /// <c>UIPopup_Rebirth</c> 래퍼 프리팹 루트에 붙고, 참조는 래퍼 안 중첩 인스턴스를 직렬화로 가리킨다(원본 무변경, Area 업그레이드와 같은 방식).
    /// 특전 칸은 디자이너가 넣어 둔 첫 <c>Element_Upgrade</c>를 원형으로 표 행 수만큼 풀에서 빌린다(칸 수는 코드에 없음).
    /// 시안에 없는 기획서 요소(광고 남은 횟수, 부제·경고 문구)는 그리지 않는다.
    /// </summary>
    public sealed class RebirthDesignView : MonoBehaviour, IMetaPanelView
    {
        const string PrefabPath = "Assets/Game/Addressables/Local/UI/Meta/UIPopup_Rebirth.prefab";

        [SerializeField] Button closeButton;
        [SerializeField] TMP_Text title;
        [SerializeField] LobbyCurrencyParts[] currencies;

        [Header("Header / Rebirth Level")]
        /// <summary><c>Rebirth Level/Image/Count Text</c>: 환생 횟수.</summary>
        [SerializeField] TMP_Text levelCount;

        [Header("Header / Lock")]
        [SerializeField] RectTransform lockRoot;
        [SerializeField] Slider lockBar;
        [SerializeField] TMP_Text lockCount;
        [SerializeField] TMP_Text lockSubtitle;

        [Header("Header / Rebirth CoolTime")]
        [SerializeField] RectTransform cooldownRoot;
        [SerializeField] TMP_Text cooldownLabel;
        [SerializeField] Slider cooldownBar;
        [SerializeField] TMP_Text cooldownCount;
        [SerializeField] TMP_Text cooldownComplete;
        [SerializeField] Button adButton;
        [SerializeField] TMP_Text adLabel;
        [SerializeField] Button skipDiaButton;
        /// <summary><c>Button_Skip</c>의 Framework 가격 버튼(가격 글자 = <c>Slot_Price/Count Text</c>, 부족 색·누름 잠금).</summary>
        [SerializeField] PriceBtn skipDiaPrice;
        [SerializeField] TMP_Text skipDiaLabel;

        [Header("Upgrade List Scroll View")]
        [SerializeField] RectTransform listContent;
        /// <summary>디자이너가 넣어 둔 첫 칸. 원형이자 첫 칸으로 쓴다.</summary>
        [SerializeField] UpgradeElement elementTemplate;

        [Header("Button_Rebirth")]
        [SerializeField] Button rebirthButton;
        /// <summary><c>Button_Rebirth/Diable</c>: 쿨타임 중 덮개.</summary>
        [SerializeField] RectTransform rebirthDisable;
        [SerializeField] TMP_Text rebirthLabel;
        /// <summary><c>Slot_RebirthCurrency/Count Text</c>: 받을 황금 생선.</summary>
        [SerializeField] TMP_Text rebirthGain;

        public event Action ClosePressed;
        public event Action<string> ActionPressed;
        /// <summary>막 누름: 디자이너 팝업에 막 버튼이 없어 내지 않는다.</summary>
        public event Action DimPressed { add { } remove { } }
        /// <summary>탭 없음.</summary>
        public event Action<string> TabPressed { add { } remove { } }

        readonly List<UpgradeElement> _elements = new List<UpgradeElement>();
        ObjectPool<UpgradeElement> _pool;
        bool _templateTaken;
        string _adActionId, _skipDiaActionId, _goActionId;
        bool _ready, _warned;

        public IReadOnlyList<UpgradeElement> Elements => _elements;
        public IReadOnlyList<LobbyCurrencyParts> Currencies => currencies;
        public bool LockShown => lockRoot && lockRoot.gameObject.activeSelf;
        public bool CooldownShown => cooldownRoot && cooldownRoot.gameObject.activeSelf;
        public bool CooldownCountShown => cooldownCount && cooldownCount.gameObject.activeSelf;
        public bool CompleteShown => cooldownComplete && cooldownComplete.gameObject.activeSelf;
        public float CooldownValue => cooldownBar ? cooldownBar.value : -1f;
        public float LockValue => lockBar ? lockBar.value : -1f;
        public string LockCountText => lockCount ? lockCount.text : null;
        public string LevelText => levelCount ? levelCount.text : null;
        public Button AdButton => adButton;
        public Button SkipDiaButton => skipDiaButton;
        public Button RebirthButton => rebirthButton;
        public bool RebirthShown => rebirthButton && rebirthButton.gameObject.activeSelf;
        public bool RebirthDisableShown => rebirthDisable && rebirthDisable.gameObject.activeSelf;
        public string GainText => rebirthGain ? rebirthGain.text : null;

        /// <summary>프리팹 연결 확인(시험·검사용): 빠진 참조 이름 목록.</summary>
        public string MissingRefs()
        {
            var miss = new List<string>();
            void Need(UnityEngine.Object o, string n) { if (!o) miss.Add(n); }
            Need(closeButton, nameof(closeButton));
            Need(title, nameof(title));
            if (currencies == null || currencies.Length == 0) miss.Add(nameof(currencies));
            else for (int i = 0; i < currencies.Length; i++)
                    if (currencies[i] == null || !currencies[i].slot || string.IsNullOrEmpty(currencies[i].wealthType)) miss.Add("currencies[" + i + "]");
            Need(levelCount, nameof(levelCount));
            Need(lockRoot, nameof(lockRoot));
            Need(lockBar, nameof(lockBar));
            Need(lockCount, nameof(lockCount));
            Need(lockSubtitle, nameof(lockSubtitle));
            Need(cooldownRoot, nameof(cooldownRoot));
            Need(cooldownLabel, nameof(cooldownLabel));
            Need(cooldownBar, nameof(cooldownBar));
            Need(cooldownCount, nameof(cooldownCount));
            Need(cooldownComplete, nameof(cooldownComplete));
            Need(adButton, nameof(adButton));
            Need(adLabel, nameof(adLabel));
            Need(skipDiaButton, nameof(skipDiaButton));
            Need(skipDiaPrice, nameof(skipDiaPrice));
            Need(skipDiaLabel, nameof(skipDiaLabel));
            Need(listContent, nameof(listContent));
            Need(elementTemplate, nameof(elementTemplate));
            if (elementTemplate)
            {
                string e = elementTemplate.MissingRefs();
                if (e.Length > 0) miss.Add("elementTemplate(" + e + ")");
            }
            Need(rebirthButton, nameof(rebirthButton));
            Need(rebirthDisable, nameof(rebirthDisable));
            Need(rebirthLabel, nameof(rebirthLabel));
            Need(rebirthGain, nameof(rebirthGain));
            return string.Join(", ", miss);
        }

        void Init()
        {
            if (_ready) return;
            _ready = true;
            string miss = MissingRefs();
            if (miss.Length > 0 && !_warned)
            {
                _warned = true;
                Debug.LogWarning("[RebirthDesignView] 프리팹 참조가 비었습니다(" + PrefabPath + "): " + miss);
            }
            if (closeButton) closeButton.onClick.AddListener(() => ClosePressed?.Invoke());
            if (adButton) adButton.onClick.AddListener(() => Fire(_adActionId));
            if (skipDiaButton) skipDiaButton.onClick.AddListener(() => Fire(_skipDiaActionId));
            if (rebirthButton) rebirthButton.onClick.AddListener(() => Fire(_goActionId));
            _pool = new ObjectPool<UpgradeElement>(CreateElement, e => e.gameObject.SetActive(true), e => e.gameObject.SetActive(false),
                e => { if (e && e != elementTemplate) Destroy(e.gameObject); });
            if (elementTemplate) elementTemplate.gameObject.SetActive(false);
        }

        void Fire(string id)
        {
            if (!string.IsNullOrEmpty(id)) ActionPressed?.Invoke(id);
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

        void Awake() => Init();

        public void Render(MetaPanelModel model)
        {
            Init();
            var d = model?.Data as RebirthViewData;
            if (d == null) return;

            if (title) title.text = d.Title ?? "";
            if (currencies != null)
                foreach (var c in currencies)
                {
                    if (c == null || !c.slot) continue;
                    string text = null;
                    foreach (var v in d.Currencies) if (v.WealthType == c.wealthType) { text = v.Text; break; }
                    if (text == null) { c.slot.gameObject.SetActive(false); continue; }
                    c.slot.ShowUI(new CurrencySlotData(d.Tables, c.wealthType, text));
                }
            if (levelCount) levelCount.text = MvUiFormat.Fmt(d.Level);

            RenderHeader(model, d);
            RenderList(model);
            RenderRebirthButton(model, d);
        }

        void RenderHeader(MetaPanelModel model, RebirthViewData d)
        {
            bool locked = d.State == RebirthHeaderState.Locked;
            bool cooling = d.State == RebirthHeaderState.Cooldown;
            SetActive(lockRoot, locked);
            if (locked)
            {
                if (lockBar) lockBar.value = d.UnlockTarget > 0 ? Mathf.Clamp01((float)d.UnlockCurrent / d.UnlockTarget) : 0f;
                if (lockCount) lockCount.text = d.UnlockCurrent + "/" + d.UnlockTarget;
                if (lockSubtitle) lockSubtitle.text = d.UnlockText ?? "";
            }

            SetActive(cooldownRoot, !locked);
            if (locked) return;
            if (cooldownLabel) cooldownLabel.text = d.CooldownLabel ?? "";
            if (cooldownBar) cooldownBar.value = d.CooldownFill;
            SetActive(cooldownCount, cooling);
            if (cooling && cooldownCount) cooldownCount.text = d.CooldownText ?? "";
            SetActive(cooldownComplete, !cooling);
            if (!cooling && cooldownComplete) cooldownComplete.text = d.CompleteText ?? "";

            // 스킵 버튼은 쿨타임 중에만(노션 시안 2·3).
            _adActionId = d.AdActionId;
            SetActive(adButton, cooling);
            if (cooling)
            {
                if (adLabel) adLabel.text = d.AdLabel ?? "";
                if (adButton) adButton.interactable = model.FindAction(d.AdActionId)?.Enabled == true;
            }

            _skipDiaActionId = d.SkipDiaActionId;
            SetActive(skipDiaButton, cooling);
            if (!cooling) return;
            if (skipDiaLabel) skipDiaLabel.text = d.SkipDiaLabel ?? "";
            bool allowed = model.FindAction(d.SkipDiaActionId)?.Enabled == true;
            // 가격·부족 색·누름 잠금은 PriceBtn이 재화 서비스로 판단한다. 서비스가 없으면(메타 없음) 누름만 끈다.
            if (!MvPrice.Show(skipDiaPrice, d.Wallet, d.SkipDiaPrice, allowed) && skipDiaButton)
                skipDiaButton.interactable = false;
        }

        void RenderList(MetaPanelModel model)
        {
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

        void RenderRebirthButton(MetaPanelModel model, RebirthViewData d)
        {
            _goActionId = d.GoActionId;
            bool shown = d.State != RebirthHeaderState.Locked; // 미해금: 버튼 끔(노션 1)
            SetActive(rebirthButton, shown);
            if (!shown) return;
            bool usable = model.FindAction(d.GoActionId)?.Enabled == true;
            if (rebirthButton) rebirthButton.interactable = usable;
            SetActive(rebirthDisable, d.State == RebirthHeaderState.Cooldown); // 쿨타임: Disable 켬·작동 X(노션 2)
            if (rebirthLabel) rebirthLabel.text = d.GoLabel ?? "";
            if (rebirthGain) rebirthGain.text = d.GainText ?? "";
        }

        static void SetActive(Component c, bool on)
        {
            if (c && c.gameObject.activeSelf != on) c.gameObject.SetActive(on);
        }
    }
}
