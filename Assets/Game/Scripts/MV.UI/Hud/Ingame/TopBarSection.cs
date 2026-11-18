using jjevol;
using MV.Data;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MV.UI
{
    /// <summary>
    /// 상단 줄 섹션(<c>IngameScreen/Top</c>, MV-UI3-HUD5, HUD_구조_설계안 3.3 TopBarSection): 레벨 글자·배속 토글(1x/2x)·재화 칸 2개.
    /// 재화 칸은 공용 부품 <see cref="HudCurrencySlot"/>(<c>Slot_Currency</c> 프리팹 루트, 로비와 같은 컴포넌트)에 01_wealth 종류와 글자만 넘긴다.
    /// 배속 토글은 <see cref="MvRunSpeed.Set"/>으로 런에 요청하고, 표시는 런 배속 값(스냅샷)을 따른다.
    /// </summary>
    public sealed class TopBarSection : UIBase
    {
        [SerializeField] TMP_Text levelText;
        /// <summary>배속 토글 버튼(<c>Toggle PlaySpeed</c>).</summary>
        [SerializeField] Button speedButton;
        /// <summary>디자이너가 1x 자식에 둔 Toggle. 클릭을 가로채지 않게 끄고 버튼 하나로 받는다.</summary>
        [SerializeField] Toggle speedToggle;
        /// <summary>배속 상태 자식(1x, 2x …). i번째 = 배속 i+1. 토글 상태 수 = 이 개수.</summary>
        [SerializeField] GameObject[] speedStates;
        [Tooltip("왼쪽 재화 칸(Slot_Currency)과 01_wealth.type")]
        [SerializeField] HudCurrencySlot goldSlot;
        [SerializeField] string goldType = "gold";
        [Tooltip("오른쪽 재화 칸(Slot_Currency)과 01_wealth.type")]
        [SerializeField] HudCurrencySlot diaSlot;
        [SerializeField] string diaType = "dia";

        int _speed = 1;
        bool _ready;
        MvTables _tables;

        public int SpeedStateCount => speedStates?.Length ?? 0;
        public int ShownSpeedState { get { if (speedStates != null) for (int i = 0; i < speedStates.Length; i++) if (speedStates[i] && speedStates[i].activeSelf) return i; return -1; } }
        public string LevelText => levelText ? levelText.text : null;
        public HudCurrencySlot Gold => goldSlot;
        public HudCurrencySlot Dia => diaSlot;

        void Awake() => Init();

        void Init()
        {
            if (_ready) return;
            _ready = true;
            if (speedToggle) speedToggle.enabled = false;
            if (speedButton) speedButton.onClick.AddListener(OnSpeedClick);
        }

        /// <summary>표가 바뀌면(런 시작) 재화 칸 그림을 다시 넣는다.</summary>
        public void BindTables(MvTables t)
        {
            Init();
            if (t == _tables) return;
            _tables = t;
            if (goldSlot) goldSlot.ShowUI(new CurrencySlotData(t, goldType, goldSlot.ValueText));
            if (diaSlot) diaSlot.ShowUI(new CurrencySlotData(t, diaType, diaSlot.ValueText));
        }

        public void SetLevel(string text) { if (levelText) levelText.text = text; }
        public void SetGold(string text) { if (goldSlot) goldSlot.SetText(text); }
        public void SetDia(string text) { if (diaSlot) diaSlot.SetText(text); }

        /// <summary>런 배속(스냅샷). 보이는 상태 자식 하나만 켠다.</summary>
        public void SetSpeed(int timeScale)
        {
            Init();
            _speed = System.Math.Max(1, timeScale);
            int shown = MvRunSpeed.StateIndex(_speed, SpeedStateCount);
            if (speedStates != null) for (int i = 0; i < speedStates.Length; i++) if (speedStates[i]) speedStates[i].SetActive(i == shown);
        }

        void OnSpeedClick() => MvRunSpeed.Set?.Invoke(MvRunSpeed.Next(_speed, SpeedStateCount));

        /// <summary>시험: 배속 토글을 누른 것처럼.</summary>
        public void ClickSpeed() => OnSpeedClick();
    }
}
