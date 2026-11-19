using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MV.UI
{
    /// <summary>
    /// 디자이너 <c>Popup_Setting</c> 겉(UIBase 래퍼 <c>UIPopup_Setting</c>가 중첩, 원본 무변경). 노션 [팝업] 설정 = 정크버스터즈 설정과 같은 배치·기능.
    /// 효과음·배경음 슬라이더와 Mute, 진동 On/Off, 언어, Privacy Policy·Support. Restore Purchase는 결제 미연결이라 숨긴다(사용자 2026-11-19 결정 카드: 정크버스터즈와 같게, 복원은 숨김).
    /// </summary>
    public class SettingsDesignView : MonoBehaviour
    {
        [Header("소리")]
        [SerializeField] Slider sfxSlider;
        [SerializeField] Toggle sfxMute;
        [SerializeField] Slider bgmSlider;
        [SerializeField] Toggle bgmMute;

        [Header("진동")]
        [SerializeField] Toggle hapticOn;
        [SerializeField] Toggle hapticOff;

        [Header("버튼")]
        [SerializeField] Button languageButton;
        [SerializeField] TMP_Text languageLabel;
        [SerializeField] Button policyButton;
        [SerializeField] Button supportButton;
        [SerializeField] GameObject restorePurchase;
        [SerializeField] Button closeButton;

        [Header("문구(언어팩 키로 채움)")]
        [SerializeField] TMP_Text title;
        [SerializeField] TMP_Text[] muteLabels;
        [SerializeField] TMP_Text onLabel;
        [SerializeField] TMP_Text offLabel;
        [SerializeField] TMP_Text policyLabel;
        [SerializeField] TMP_Text supportLabel;

        bool _drawing;

        public event Action<float> BgmVolumeChanged;
        public event Action<float> SfxVolumeChanged;
        public event Action<bool> BgmMuteChanged;
        public event Action<bool> SfxMuteChanged;
        public event Action<bool> HapticChanged;
        public event Action LanguagePressed;
        public event Action PolicyPressed;
        public event Action SupportPressed;
        public event Action ClosePressed;

        void Awake()
        {
            if (restorePurchase) restorePurchase.SetActive(false);
            if (bgmSlider) bgmSlider.onValueChanged.AddListener(v => { if (!_drawing) BgmVolumeChanged?.Invoke(v); });
            if (sfxSlider) sfxSlider.onValueChanged.AddListener(v => { if (!_drawing) SfxVolumeChanged?.Invoke(v); });
            if (bgmMute) bgmMute.onValueChanged.AddListener(v => { if (!_drawing) BgmMuteChanged?.Invoke(v); });
            if (sfxMute) sfxMute.onValueChanged.AddListener(v => { if (!_drawing) SfxMuteChanged?.Invoke(v); });
            if (hapticOn) hapticOn.onValueChanged.AddListener(v => { if (!_drawing && v) HapticChanged?.Invoke(true); });
            if (hapticOff) hapticOff.onValueChanged.AddListener(v => { if (!_drawing && v) HapticChanged?.Invoke(false); });
            if (languageButton) languageButton.onClick.AddListener(() => LanguagePressed?.Invoke());
            if (policyButton) policyButton.onClick.AddListener(() => PolicyPressed?.Invoke());
            if (supportButton) supportButton.onClick.AddListener(() => SupportPressed?.Invoke());
            if (closeButton) closeButton.onClick.AddListener(() => ClosePressed?.Invoke());
        }

        /// <summary>값을 그린다(이벤트는 다시 내지 않는다).</summary>
        public void Draw(SettingsValues v, bool bgmMuted, bool sfxMuted, string languageName)
        {
            _drawing = true;
            try
            {
                if (bgmSlider) bgmSlider.SetValueWithoutNotify(v.BgmVolume);
                if (sfxSlider) sfxSlider.SetValueWithoutNotify(v.SfxVolume);
                if (bgmMute) bgmMute.SetIsOnWithoutNotify(bgmMuted);
                if (sfxMute) sfxMute.SetIsOnWithoutNotify(sfxMuted);
                if (hapticOn) hapticOn.SetIsOnWithoutNotify(v.Haptic);
                if (hapticOff) hapticOff.SetIsOnWithoutNotify(!v.Haptic);
                if (languageLabel) languageLabel.text = languageName ?? "";
                // 켜짐 그림은 토글 연출(CrossFadeAlpha)에 맡기지 않고 값대로 바로 보이고 숨긴다(진동 켜기·끄기가 둘 다 켜져 보이던 문제).
                ShowState(bgmMute); ShowState(sfxMute); ShowState(hapticOn); ShowState(hapticOff);
            }
            finally
            {
                _drawing = false;
            }
        }

        static void ShowState(Toggle t)
        {
            if (t != null && t.graphic != null) t.graphic.canvasRenderer.SetAlpha(t.isOn ? 1f : 0f);
        }

        /// <summary>고정 문구를 지금 언어로(07 §7.12 ui.setting.*).</summary>
        public void DrawTexts()
        {
            if (title) title.text = MvUiText.Ui("ui.setting.title");
            if (muteLabels != null)
                foreach (var l in muteLabels)
                    if (l) l.text = MvUiText.Ui("ui.setting.mute");
            if (onLabel) onLabel.text = MvUiText.Ui("ui.setting.on");
            if (offLabel) offLabel.text = MvUiText.Ui("ui.setting.off");
            if (policyLabel) policyLabel.text = MvUiText.Ui("ui.setting.privacy");
            if (supportLabel) supportLabel.text = MvUiText.Ui("ui.setting.support");
        }

        // 시험용 읽기
        public float BgmSliderValue => bgmSlider ? bgmSlider.value : 0f;
        public bool BgmMuteShown => bgmMute && bgmMute.isOn;
        public bool RestoreHidden => !restorePurchase || !restorePurchase.activeSelf;
        public string LanguageText => languageLabel ? languageLabel.text : null;
    }
}
