using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using jjevol;
using MV.Data;
using MV.Run;
using MV.Save;
using MV.View;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MV.UI
{
    /// <summary>
    /// 디자이너 공용 모듈 <c>Slot_Currency</c> 1칸. 그리기는 인게임과 같은 공용 부품 <see cref="HudCurrencySlot"/>가 맡고,
    /// 로비는 보일 재화(<see cref="wealthType"/>, <c>01_wealth.type</c>)만 Inspector에서 정한다.
    /// </summary>
    [Serializable]
    public sealed class LobbyCurrencyParts
    {
        /// <summary><c>01_wealth.type</c>(gold·dia·rescueCat …). 비우면 칸을 끈다.</summary>
        public string wealthType;
        /// <summary><c>Slot_Currency</c> 루트의 공용 재화 칸.</summary>
        public HudCurrencySlot slot;
    }

    /// <summary>디자이너 <c>Element_MainUpgrade_*</c> 1칸. 표 행과는 <see cref="key"/>(<c>10_lobbyUpgrade.key</c>)로 짝짓는다.</summary>
    [Serializable]
    public sealed class LobbyUpgradeParts
    {
        public string key;
        public GameObject root;
        /// <summary>누름 영역. 디자이너 원본에 버튼이 없어 로비 래퍼 프리팹이 칸 루트에 덧붙였다.</summary>
        public Button button;
        /// <summary><c>Backboard/Disable</c>: 살 수 없으면 켠다(노션).</summary>
        public GameObject disable;
        /// <summary><c>Slot_Icon/Icon</c>: 무기 강화만 지금 등급 무기 그림으로 바꾼다.</summary>
        public Image icon;
        public TMP_Text currentValue;
        public TMP_Text nextValue;
        /// <summary><c>Title/Slot_Level/Level Text (TMP)</c>.</summary>
        public TMP_Text level;
        public GameObject maxText;
        /// <summary><c>Slot_Price</c>: 최대면 끈다(노션).</summary>
        public GameObject price;
        public Image priceIcon;
        public TMP_Text priceText;
        /// <summary>
        /// <c>Slot_Price</c>에 래퍼가 덧붙인 Framework 가격 버튼. 가격 글자(게임 숫자 표기)·부족 색(빨강)·누름 잠금을 재화 서비스로 직접 정한다
        /// (사용자 2026-11-19 「Framework PriceBtn 사용」). 아이콘은 디자이너 Image(<see cref="priceIcon"/>)에 둔다.
        /// </summary>
        public PriceBtn priceButton;
        public GameObject redDot;
    }

    /// <summary>
    /// 디자이너 <c>LobbyScreen</c>(Art/UI/Prefab/UIScreen)을 로비 겉으로 쓰는 연결(MV-UI2-LOBBY, 노션 [HUD] 로비스크린, 구조안 A).
    /// <c>UIScreen_Lobby</c> 래퍼 프리팹 루트에 붙고, 참조는 래퍼 안 중첩 인스턴스를 직렬화로 가리킨다. 디자이너 원본은 바꾸지 않는다.
    /// 겉(배치·그림·크기)은 프리팹이 정하고 코드는 값·켜고 끄기만 한다. 디자이너 시안에 없는 기획서 요소(지도·야옹신·컨텐츠·퀘스트·진행 상자·덱 위젯)는 그리지 않는다(사용자 2026-11-19).
    /// </summary>
    public sealed class LobbyDesignView : MonoBehaviour, ILobbyView, ILobbyMenuView, ILobbyStateView, ILobbyDeckView
    {
        [Header("출발")]
        [SerializeField] LobbyStartArea startArea;

        [Header("Top")]
        [SerializeField] TMP_Text levelText;
        [SerializeField] HudAreaLabel areaLabel;
        /// <summary>
        /// 상단 Area 이름표를 누르면 지도(LevelProgress)를 연다(노션 「로비의 상단 Slot_AreaLabel을 클릭 시」, MV-UI3-LVPROG).
        /// 공용 부품 <see cref="HudAreaLabel"/>은 클릭을 받지 않고, 로비 래퍼 프리팹이 이 이름표에만 Button을 덧붙인다
        /// (같은 부품이 「Area 업그레이드」 버튼 안에도 있어 부품이 클릭을 받으면 그 버튼이 눌리지 않았다, 사용자 2026-11-19 「구조로 바꿔」).
        /// </summary>
        [SerializeField] Button areaLabelButton;
        /// <summary>설정 버튼. 설정 팝업이 아직 없어 기획서 설정 자리(05 §5.14 소리 켜고 끄기)로 보낸다.</summary>
        [SerializeField] Button settingButton;
        [SerializeField] Button speedButton;
        /// <summary>디자이너가 1x 자식에 둔 Toggle. 클릭을 가로채지 않게 끄고 버튼 하나로 받는다(인게임 HUD와 같은 처리).</summary>
        [SerializeField] Toggle speedToggle;
        /// <summary>배속 상태 자식(1x, 2x …). i번째 = 배속 i+1.</summary>
        [SerializeField] GameObject[] speedStates;
        [SerializeField] LobbyCurrencyParts[] currencies;

        [Header("Mid/Weapon")]
        /// <summary>
        /// 장착 칸 + 쌓이는 무기(<c>Mid/Weapon</c>)의 로비 연결. 스택·줄 서기·머지 연출은 인게임과 같은 공용 부품 <see cref="WeaponStackView"/>가 맡는다
        /// (MV-UI3-LOBBY-WEAPONFX, 노션 「LobbyScreen : WeaponUpgrade시 무기 획득 및 Stack 연출」).
        /// </summary>
        [SerializeField] LobbyWeaponStack weaponStack;

        [Header("Mid/Stair")]
        [SerializeField] Image stairGrade;
        [SerializeField] TMP_Text stairValue;
        [SerializeField] TMP_Text stairCount;
        [SerializeField] Slider stairGauge;
        [SerializeField] Image gaugeGrade;
        [SerializeField] TMP_Text gaugeLevel;
        [SerializeField] Image gaugeNextGrade;
        [SerializeField] TMP_Text gaugeNextLevel;
        /// <summary>계단 등급 업 연출(MV-UI3-STAIRFX, 노션 「Stair 업그레이드 연출」): MainUpgrade로 계단 레벨이 오르면 Slot_Stair 크기·Flash·Sparkle. 인게임 계단 칸과 같은 부품.</summary>
        [SerializeField] HudGradeUpFx stairGradeUpFx;

        [Header("Bottom")]
        [SerializeField] LobbyUpgradeParts[] upgrades;
        [SerializeField] Button rebirthButton;
        [SerializeField] Slider rebirthUnlock;
        [SerializeField] TMP_Text rebirthUnlockCount;
        [SerializeField] Slider rebirthCool;
        [SerializeField] TMP_Text rebirthCoolCount;
        [SerializeField] GameObject rebirthCoolComplete;
        [SerializeField] TMP_Text rebirthReward;
        /// <summary>환생 버튼 보상 재화 아이콘(<c>Image (2)</c>). 그림은 환생 지급 재화의 07_consumeType 아이콘(MV-UI-ICONSRC).</summary>
        [SerializeField] Image rebirthRewardIcon;
        [SerializeField] Button areaUpgradeButton;
        [SerializeField] HudAreaLabel areaUpgradeLabel;
        [SerializeField] GameObject areaUpgradeDot;

        // 문구: 노션 [HUD] 로비스크린 서식(키 자리 ui.lobby.*)
        public const string StairValueFormat = "+{0}";
        public const string StairCountFormat = "x{0}";
        public const string UpgradeLevelFormat = "Level {0}";
        public const string RebirthUnlockFormat = "{0}/{1}";
        public const string RebirthRewardFormat = "+{0}";

        public event Action StartPressed;
        public event Action<string> SidePressed;
        public event Action<string> UpgradePressed;

        readonly WorldSpriteCache _sprites = new WorldSpriteCache();
        readonly Dictionary<Image, (string atlas, string sprite)> _wanted = new Dictionary<Image, (string, string)>();
        MvTables _tables;
        bool _ready, _disposed;
        readonly HashSet<string> _missingKeys = new HashSet<string>();
        readonly HashSet<string> _missingPrice = new HashSet<string>();

        /// <summary>강화 칸 가격 버튼이 잔액을 읽는 재화 서비스(<see cref="MvMetaUiPort.Wallet"/>). 화면(<see cref="LobbyScreen"/>)이 넣는다. 없으면 가격 칸을 끈다.</summary>
        public ResourceService Wallet { get; set; }

        // ---- 시험·QA ----
        public LobbyStartArea StartArea => startArea;
        public string LevelText => levelText ? levelText.text : null;
        public IReadOnlyList<LobbyUpgradeParts> Upgrades => upgrades;
        public IReadOnlyList<LobbyCurrencyParts> Currencies => currencies;
        public LobbyWeaponStack WeaponStack { get { Init(); return weaponStack; } }
        public int SpeedStateCount => speedStates?.Length ?? 0;
        public int ShownSpeedState { get { if (speedStates != null) for (int i = 0; i < speedStates.Length; i++) if (speedStates[i] && speedStates[i].activeSelf) return i; return -1; } }
        public string StairValueText => stairValue ? stairValue.text : null;
        public string StairCountText => stairCount ? stairCount.text : null;
        public string RebirthUnlockText => rebirthUnlockCount ? rebirthUnlockCount.text : null;
        public string RebirthRewardText => rebirthReward ? rebirthReward.text : null;
        public bool RebirthShown => rebirthButton && rebirthButton.gameObject.activeSelf;
        public bool RebirthUnlockShown => rebirthUnlock && rebirthUnlock.gameObject.activeSelf;
        public bool AreaUpgradeDotShown => areaUpgradeDot && areaUpgradeDot.activeSelf;
        public string AreaNameText => areaLabel ? areaLabel.AreaText : null;
        public HudAreaLabel AreaLabel => areaLabel;
        public HudAreaLabel AreaUpgradeLabel => areaUpgradeLabel;

        /// <summary>직렬화 참조 중 빈 칸 이름(시험·QA: 디자이너가 오브젝트를 지우면 여기 나온다).</summary>
        public List<string> MissingRefs()
        {
            var miss = new List<string>();
            void Need(UnityEngine.Object o, string name) { if (!o) miss.Add(name); }
            Need(startArea, nameof(startArea));
            Need(levelText, nameof(levelText));
            Need(areaLabel, nameof(areaLabel));
            if (areaLabel && !areaLabel.HasRefs) miss.Add("areaLabel(HudAreaLabel 참조)");
            Need(settingButton, nameof(settingButton));
            Need(speedButton, nameof(speedButton));
            if (speedStates == null || speedStates.Length == 0) miss.Add(nameof(speedStates));
            else for (int i = 0; i < speedStates.Length; i++) Need(speedStates[i], "speedStates[" + i + "]");
            if (currencies == null || currencies.Length == 0) miss.Add(nameof(currencies));
            else for (int i = 0; i < currencies.Length; i++)
            {
                var c = currencies[i];
                Need(c?.slot, "currencies[" + i + "].slot");
            }
            Need(weaponStack, nameof(weaponStack));
            if (weaponStack) foreach (var m in weaponStack.MissingRefs()) miss.Add("weaponStack." + m);
            Need(stairGrade, nameof(stairGrade));
            Need(stairValue, nameof(stairValue));
            Need(stairCount, nameof(stairCount));
            Need(stairGauge, nameof(stairGauge));
            Need(gaugeGrade, nameof(gaugeGrade));
            Need(gaugeLevel, nameof(gaugeLevel));
            Need(gaugeNextGrade, nameof(gaugeNextGrade));
            Need(gaugeNextLevel, nameof(gaugeNextLevel));
            if (upgrades == null || upgrades.Length == 0) miss.Add(nameof(upgrades));
            else for (int i = 0; i < upgrades.Length; i++)
            {
                var u = upgrades[i];
                string n = "upgrades[" + i + "]";
                if (u == null || string.IsNullOrEmpty(u.key)) miss.Add(n + ".key");
                Need(u?.root, n + ".root");
                Need(u?.button, n + ".button");
                Need(u?.disable, n + ".disable");
                Need(u?.icon, n + ".icon");
                Need(u?.level, n + ".level");
                Need(u?.maxText, n + ".maxText");
                Need(u?.price, n + ".price");
                Need(u?.priceIcon, n + ".priceIcon");
                Need(u?.priceText, n + ".priceText");
                Need(u?.priceButton, n + ".priceButton");
                Need(u?.redDot, n + ".redDot");
                Need(u?.currentValue, n + ".currentValue");
                Need(u?.nextValue, n + ".nextValue");
            }
            Need(rebirthButton, nameof(rebirthButton));
            Need(rebirthUnlock, nameof(rebirthUnlock));
            Need(rebirthUnlockCount, nameof(rebirthUnlockCount));
            Need(rebirthCool, nameof(rebirthCool));
            Need(rebirthCoolCount, nameof(rebirthCoolCount));
            Need(rebirthCoolComplete, nameof(rebirthCoolComplete));
            Need(rebirthReward, nameof(rebirthReward));
            Need(rebirthRewardIcon, nameof(rebirthRewardIcon));
            Need(areaLabelButton, nameof(areaLabelButton));
            Need(areaUpgradeButton, nameof(areaUpgradeButton));
            Need(areaUpgradeLabel, nameof(areaUpgradeLabel));
            if (areaUpgradeLabel && !areaUpgradeLabel.HasRefs) miss.Add("areaUpgradeLabel(HudAreaLabel 참조)");
            Need(areaUpgradeDot, nameof(areaUpgradeDot));
            return miss;
        }

        void Awake() => Init();

        void Init()
        {
            if (_ready) return;
            _ready = true;
            // 버튼(Selectable) 아래 그림과 출발 영역만 누를 수 있다(상단 Area 이름표도 래퍼가 붙인 Button 아래). 나머지 글·패널은 누름을 받지 않아 출발 영역으로 지나간다(05 §5.4.1).
            foreach (var g in GetComponentsInChildren<Graphic>(true))
                g.raycastTarget = g.GetComponentInParent<Selectable>(true) != null || (startArea && g.gameObject == startArea.gameObject);
            if (areaLabelButton) areaLabelButton.onClick.AddListener(() => SidePressed?.Invoke(LobbySideIds.Map));
            if (startArea) startArea.Pressed += OnStart;
            if (speedToggle) speedToggle.enabled = false;
            if (speedButton) speedButton.onClick.AddListener(OnSpeedClick);
            if (settingButton) settingButton.onClick.AddListener(() => SidePressed?.Invoke(LobbySideIds.Sound));
            if (rebirthButton) rebirthButton.onClick.AddListener(() => SidePressed?.Invoke(LobbySideIds.Rebirth));
            if (areaUpgradeButton) areaUpgradeButton.onClick.AddListener(() => SidePressed?.Invoke(LobbySideIds.AreaUpgrade));
            if (upgrades != null)
                foreach (var u in upgrades)
                {
                    if (u?.button == null) continue;
                    string key = u.key;
                    u.button.transition = Selectable.Transition.None; // 정본은 누름 효과가 없다(05 §5.1.8)
                    u.button.onClick.AddListener(() => UpgradePressed?.Invoke(key));
                }

            SetSpeedShown(MvRunSpeed.Current);
            if (MissingRefs() is { Count: > 0 } miss)
                Debug.LogError("LobbyDesignView: UIScreen_Lobby 참조가 비었습니다 — " + string.Join(", ", miss), this);
        }

        // 화면을 다시 열면 지난 레벨 변화를 연출하지 않는다(첫 그리기는 기준만 잡는다).
        void OnDisable() => _drawnStairLv = -1;

        void OnDestroy()
        {
            _disposed = true;
            if (startArea) startArea.Pressed -= OnStart;
            _sprites.Dispose();
        }

        void OnStart() => StartPressed?.Invoke();

        void OnSpeedClick()
        {
            int next = MvRunSpeed.Next(MvRunSpeed.Current, SpeedStateCount);
            MvRunSpeed.Set?.Invoke(next);
            SetSpeedShown(MvRunSpeed.Get != null ? MvRunSpeed.Current : next);
        }

        /// <summary>시험: 배속 토글을 누른 것처럼.</summary>
        public void ClickSpeed() => OnSpeedClick();

        void SetSpeedShown(int timeScale)
        {
            int shown = MvRunSpeed.StateIndex(timeScale, SpeedStateCount);
            if (speedStates != null) for (int i = 0; i < speedStates.Length; i++) if (speedStates[i]) speedStates[i].SetActive(i == shown);
        }

        void LateUpdate()
        {
            // 다른 곳(인게임 HUD·테스트 패널)에서 바꾼 배속을 따라간다.
            if (speedStates != null && speedStates.Length > 0 && ShownSpeedState != MvRunSpeed.StateIndex(MvRunSpeed.Current, SpeedStateCount))
                SetSpeedShown(MvRunSpeed.Current);
        }

        // ---------------- ILobbyView(문자열) ----------------

        public void SetLevel(string text) { Init(); if (levelText) levelText.text = text; }

        /// <summary>Area 줄은 원값(<see cref="SetState"/>)으로 그린다.</summary>
        public void SetArea(string text) { }

        public void SetWealth(string gold, string souls, string dia)
        {
            Init();
            if (currencies == null) return;
            foreach (var c in currencies)
            {
                if (c == null || !c.slot) continue;
                switch (c.wealthType)
                {
                    case "gold": c.slot.SetText(gold); break;           // 01_wealth.type
                    case "rescueCat": c.slot.SetText(souls); break;
                    case "dia": c.slot.SetText(dia); break;
                }
            }
        }

        // ---------------- ILobbyStateView(원값) ----------------

        public void SetState(in LobbyState s, MvTables t)
        {
            Init();
            _tables = t;
            string area = s.AreaName ?? ""; // 언어팩 name:<302 id>만
            string areaIcon = HudAreaLabel.AreaIconOf(t, s.AreaNo);
            if (areaLabel) areaLabel.ShowUI(new AreaLabelData(s.ZoneNo, area, areaIcon));
            if (areaUpgradeLabel) areaUpgradeLabel.ShowUI(new AreaLabelData(s.ZoneNo, area, areaIcon));
            DrawCurrencies(t);
            DrawWeapons(t, s.Deck);
        }

        void DrawCurrencies(MvTables t)
        {
            if (currencies == null) return;
            foreach (var c in currencies)
            {
                if (c == null || !c.slot) continue;
                if (string.IsNullOrEmpty(c.wealthType)) { c.slot.HideUI(); continue; }
                c.slot.ShowUI(new CurrencySlotData(t, c.wealthType, c.slot.ValueText)); // 글자는 SetWealth가 먼저 넣은 값
            }
        }

        /// <summary>시작 덱(등급별 장수)을 장착 칸 + 스택으로. 그리기·버림·머지는 공용 부품(<see cref="LobbyWeaponStack"/> → <see cref="WeaponStackView"/>, 인게임 손패와 같은 규칙).</summary>
        void DrawWeapons(MvTables t, int[] deck)
        {
            if (weaponStack) weaponStack.Show(t, deck);
        }

        /// <summary>스크래쳐 구매로 덱에 카드 1장이 들어갔다: Spwan → Dst → 줄 → 스택(노션, MV-UI3-LOBBY-WEAPONFX).</summary>
        public void AddDeckCard(int tier)
        {
            if (weaponStack) weaponStack.Add(tier);
        }

        // ---------------- ILobbyMenuView(화면 코드 모델) ----------------

        public void Render(LobbyMenuModel model)
        {
            Init();
            if (model == null) return;
            var t = _tables ?? MvTables.Current;
            DrawStair(t, model);
            DrawUpgrades(t, model);
            DrawRebirth(model.Rebirth);
            if (areaUpgradeDot) areaUpgradeDot.SetActive(model.AreaUpgradeDot);
            if (areaUpgradeButton)
            {
                var side = model.FindSide(LobbySideIds.AreaUpgrade);
                areaUpgradeButton.gameObject.SetActive(side == null || side.Visible);
            }
        }

        void DrawStair(MvTables t, LobbyMenuModel m)
        {
            int lv = m.StairLv;
            // 등급 업 연출 시작 경로는 여기 하나(그린 레벨이 오를 때).
            if (_drawnStairLv >= 0 && lv > _drawnStairLv && stairGradeUpFx) stairGradeUpFx.Play();
            _drawnStairLv = lv;
            var cap = t != null ? RunFormulas.GaugeRow(t) : null;
            int next = cap != null && cap.cap > 0 ? Math.Min(cap.cap, lv + 1) : lv + 1;
            SetFrame(stairGrade, t, lv);
            SetFrame(gaugeGrade, t, lv);
            SetFrame(gaugeNextGrade, t, next);
            if (stairValue) stairValue.text = string.Format(StairValueFormat, lv);
            if (stairCount) stairCount.text = string.Format(StairCountFormat, 0); // 관문으로 얻은 계단 수: 로비는 런 전이라 0
            if (gaugeLevel) gaugeLevel.text = lv.ToString();
            if (gaugeNextLevel) gaugeNextLevel.text = next.ToString();
            if (stairGauge) stairGauge.normalizedValue = m.StairNeed > 0 ? Mathf.Clamp01((float)m.StairStep / m.StairNeed) : 1f;
        }

        void SetFrame(Image img, MvTables t, int grade)
        {
            var row = HudGrade.Frame(t, grade);
            if (row != null) SetSprite(img, row.atlas, row.sprite);
        }

        void DrawUpgrades(MvTables t, LobbyMenuModel m)
        {
            if (upgrades == null) return;
            foreach (var p in upgrades)
            {
                if (p == null) continue;
                var u = m.FindUpgrade(p.key);
                if (p.root) p.root.SetActive(u != null);
                if (u == null) continue;
                if (p.disable) p.disable.SetActive(!u.Enabled);
                if (p.redDot) p.redDot.SetActive(u.Enabled);
                if (p.level) p.level.text = string.Format(UpgradeLevelFormat, u.LevelNo);
                if (p.currentValue && u.CurValue.Length > 0) p.currentValue.text = u.CurValue;
                if (p.nextValue && u.NextValue.Length > 0) p.nextValue.text = u.NextValue;
                if (p.maxText) p.maxText.SetActive(u.Max);
                DrawPrice(p, u);
                if (u.PriceIcon.Length > 0) SetSprite(p.priceIcon, u.PriceIconAtlas, u.PriceIcon);
                if (u.IconIsItem && u.Icon.Length > 0) SetSprite(p.icon, MvAtlas.UiIcon, u.Icon);
            }
            foreach (var u in m.Upgrades)
                if (Find(u.Key) == null && _missingKeys.Add(u.Key))
                    Debug.LogError("LobbyDesignView: 10_lobbyUpgrade key " + u.Key + "에 맞는 디자이너 강화 칸이 없습니다(UIScreen_Lobby upgrades)", this);
        }

        /// <summary>
        /// 가격 칸: 최대가 아니고 재화 서비스가 있으면 PriceBtn이 글자·부족 색·누름 잠금을 그린다. 그 밖(최대·메타 없음·재화 아닌 비용)은 칸을 끄고 누름을 막는다.
        /// PriceBtn 참조가 없으면 만들거나 대신 그리지 않고 경로만 남긴다.
        /// </summary>
        void DrawPrice(LobbyUpgradeParts p, LobbyUpgradeItem u)
        {
            if (!p.priceButton && _missingPrice.Add(p.key))
                Debug.LogWarning("LobbyDesignView: 강화 칸 " + p.key + "의 PriceBtn이 없습니다(UIScreen_Lobby upgrades[].priceButton → Assets/Game/Art/UI/Prefab/HUD/Element_MainUpgrade_Stair.prefab 루트, Button과 같은 객체)", this);
            bool shown = !u.Max && MvPrice.Show(p.priceButton, Wallet, u.Cost, u.InputAllowed);
            if (p.price) p.price.SetActive(shown); // PriceBtn은 버튼 루트에 있어 가격 칸(Slot_Price)은 따로 켜고 끈다
            if (shown) return;
            if (p.button) p.button.interactable = false;
        }

        LobbyUpgradeParts Find(string key)
        {
            if (upgrades != null) foreach (var p in upgrades) if (p != null && p.key == key) return p;
            return null;
        }

        void DrawRebirth(LobbyRebirthInfo r)
        {
            if (rebirthButton) rebirthButton.gameObject.SetActive(r.Visible);
            if (!r.Visible) return;
            if (rebirthUnlock) rebirthUnlock.gameObject.SetActive(r.Locked);
            if (rebirthCool) rebirthCool.gameObject.SetActive(!r.Locked);
            if (r.Locked)
            {
                int now = r.UnlockNeed > 0 ? Math.Min(r.UnlockNow, r.UnlockNeed) : r.UnlockNow;
                if (rebirthUnlock) rebirthUnlock.normalizedValue = r.UnlockNeed > 0 ? (float)now / r.UnlockNeed : 0f;
                if (rebirthUnlockCount) rebirthUnlockCount.text = string.Format(RebirthUnlockFormat, now, r.UnlockNeed);
            }
            else
            {
                bool ready = r.CoolLeftMs <= 0;
                if (rebirthCool) rebirthCool.normalizedValue = ready || r.CoolTotalMs <= 0 ? 1f : 1f - Mathf.Clamp01((float)r.CoolLeftMs / r.CoolTotalMs);
                if (rebirthCoolCount)
                {
                    rebirthCoolCount.gameObject.SetActive(!ready);
                    if (!ready) rebirthCoolCount.text = MetaPanelPresenter.Hms(r.CoolLeftMs);
                }
                if (rebirthCoolComplete) rebirthCoolComplete.SetActive(ready);
            }
            if (rebirthReward) rebirthReward.text = string.Format(RebirthRewardFormat, r.Pending);
            if (MvConsumeIcon.TryWealth(_tables ?? MvTables.Current, MvWealthIds.TypeOf(MvWealthIds.RbToken), out var rbAtlas, out var rbSprite))
                SetSprite(rebirthRewardIcon, rbAtlas, rbSprite);
        }

        // ---------------- 일반 Image에 표 그림 넣기 ----------------

        /// <summary>
        /// 디자이너가 일반 Image로 둔 칸(등급 틀·강화 아이콘·가격 아이콘)에 아틀라스 그림을 넣는다. 아틀라스는 처음 한 번 읽고 이 화면이 없어질 때 놓는다.
        /// 에디터 시험(재생 전)에서는 읽지 않고 원하는 그림 이름만 남긴다.
        /// </summary>
        void SetSprite(Image img, string atlas, string sprite)
        {
            if (!img || string.IsNullOrEmpty(atlas) || string.IsNullOrEmpty(sprite)) return;
            _wanted[img] = (atlas, sprite);
            // 그림 칸이 AddressableImage(프리팹은 키만, MV-UI-ICONSRC)면 키로 넣는다. 직접 sprite를 넣으면 칸이 켜질 때 자기 키 그림으로 되돌아간다.
            if (img is AddressableImage addressable) { HudImage.Set(addressable, atlas, sprite); return; }
            if (!Application.isPlaying) return;
            if (_sprites.IsLoaded(atlas)) { Apply(img); return; }
            LoadThenApply(img, atlas).Forget();
        }

        async UniTaskVoid LoadThenApply(Image img, string atlas)
        {
            if (!await _sprites.LoadAtlasAsync(atlas, this.GetCancellationTokenOnDestroy()) || _disposed) return;
            Apply(img);
        }

        void Apply(Image img)
        {
            if (!img || !_wanted.TryGetValue(img, out var w) || !_sprites.IsLoaded(w.atlas)) return;
            var s = _sprites.Get(w.atlas, w.sprite);
            if (s) img.sprite = s;
        }

        /// <summary>시험·QA: 그 칸에 넣으려는 그림 이름(없으면 null).</summary>
        public string WantedSprite(Image img) => img && _wanted.TryGetValue(img, out var w) ? w.sprite : null;
        public Image StairGradeImage => stairGrade;
        public HudGradeUpFx StairGradeUpFx => stairGradeUpFx;
        int _drawnStairLv = -1;
    }
}
