using System;
using Cysharp.Threading.Tasks;
using jjevol;
using MV.Sound;
using UnityEngine;

namespace MV.UI
{
    /// <summary>설정 팝업 데이터. 닫힐 때 <see cref="OnClosed"/>(로비 다시 그리기).</summary>
    public sealed class SettingsPopupData : IPopupData
    {
        public Action OnClosed;
    }

    /// <summary>
    /// <c>UIPopup_Setting</c> 컨트롤러(층 500, MV-UI2-SETTING). 판단은 <see cref="SettingsPresenter"/>, 겉은 <see cref="SettingsDesignView"/>.
    /// 노션 [팝업] 설정: 정크버스터즈 설정과 같은 배치·기능, 계정(탈퇴·로그아웃·계정연동)·버전·UID 제외.
    /// </summary>
    public class SettingsPopup : UIPopup
    {
        [SerializeField] SettingsDesignView view;

        [Header("연출·연결 값(프리팹 직렬화)")]
        [Tooltip("슬라이더를 멈춘 뒤 저장까지 초(정크버스터즈 1초)")]
        [SerializeField] float saveDelaySeconds = 1f;
        [Tooltip("Privacy Policy 주소(정크버스터즈와 같은 주소)")]
        [SerializeField] string privacyPolicyUrl = "https://www.phase4.co.kr/policies/en/privacy-policy";
        [Tooltip("Support 메일 주소(정크버스터즈와 같은 주소)")]
        [SerializeField] string supportEmail = "support@phase4.co.kr";

        SettingsPresenter _presenter;
        float _saveAt = -1f;
        bool _bound;

        /// <summary>판단 쪽(시험용). 기본 입구는 <see cref="MvSettingsPort"/>.</summary>
        public SettingsPresenter Presenter => _presenter ??= new SettingsPresenter(Port ?? MvSettingsPort.Instance);

        /// <summary>시험 주입용. null이면 <see cref="MvSettingsPort"/>.</summary>
        public ISettingsPort Port { get; set; }

        protected override void Awake()
        {
            base.Awake();
            Bind();
        }

        void Bind()
        {
            if (_bound || view == null) return;
            _bound = true;
            view.BgmVolumeChanged += v => { Presenter.SetBgmVolume(v); _saveAt = Time.unscaledTime + saveDelaySeconds; };
            view.SfxVolumeChanged += v => { Presenter.SetSfxVolume(v); _saveAt = Time.unscaledTime + saveDelaySeconds; };
            view.BgmMuteChanged += m => { Presenter.SetBgmMute(m); Click(); };
            view.SfxMuteChanged += m => { Presenter.SetSfxMute(m); Click(); };
            view.HapticChanged += on => { Presenter.SetHaptic(on); Click(); if (on) MvHaptics.Vibrate(); };
            view.LanguagePressed += OpenLanguage;
            view.PolicyPressed += OpenPolicy;
            view.SupportPressed += OpenSupport;
            view.ClosePressed += () => { Click(); Close(); };
            Presenter.Changed += Draw;
            UIRuntimeOptions.LocaleChanged += Draw;
        }

        protected override void _DrawContents()
        {
            Bind();
            Presenter.Open();
        }

        void Update()
        {
            if (_saveAt >= 0f && Time.unscaledTime >= _saveAt)
            {
                _saveAt = -1f;
                Presenter.FlushSound();
            }
        }

        void Draw()
        {
            if (view == null) return;
            var lang = MvUiLanguage.Service;
            view.DrawTexts();
            view.Draw(Presenter.Values, Presenter.BgmMuted, Presenter.SfxMuted, lang != null ? lang.DisplayName(lang.Current) : "");
        }

        static void Click() => MvSound.PlayUi(MvUiSound.ButtonClick);

        void OpenLanguage()
        {
            Click();
            MvUiNav.OpenLanguageAsync(new LanguagePopupData { OnSelected = Draw }).Forget(Debug.LogException);
        }

        void OpenPolicy()
        {
            Click();
            if (!string.IsNullOrEmpty(privacyPolicyUrl)) Application.OpenURL(privacyPolicyUrl);
        }

        /// <summary>문의 메일(정크버스터즈와 같은 방식, UID 제외): 제목·본문은 언어팩, 앱 버전·기기·OS·언어를 채운다.</summary>
        void OpenSupport()
        {
            Click();
            if (string.IsNullOrEmpty(supportEmail)) return;
            Application.OpenURL(SettingsPresenter.SupportMailUrl(supportEmail, Application.productName, Application.version,
                SystemInfo.deviceModel, SystemInfo.operatingSystem, MvUiLanguage.Service?.Current));
        }

        /// <summary>
        /// <c>mailto:</c> 주소. 제목 <c>ui.setting.mailSubject</c>({0}=앱 이름), 본문 <c>ui.setting.mailBody</c>({0}=앱 버전 {1}=기기 {2}=OS {3}=언어).
        /// 시트 칸은 한 줄이라 본문의 줄바꿈은 글자 <c>\n</c>으로 적는다. 빈 값은 「-」.
        /// </summary>
        /// <summary>뒤로 가기·Esc도 닫기 버튼과 같은 길로(<see cref="MetaPanelPopup"/>과 같은 이유: OnClosed를 건너뛰지 않게).</summary>
        public override UniTask<bool> OnBackRequestAsync()
        {
            if (IsOpen && !IsTransitioning) Close();
            return UniTask.FromResult(false);
        }

        /// <summary>남은 소리 값을 저장하고 닫는다. 실제로 닫혔을 때만 OnClosed를 한 번 부른다.</summary>
        void Close()
        {
            if (!IsOpen) return;
            _presenter?.FlushSound();
            _saveAt = -1f;
            var data = popupData as SettingsPopupData;
            ClosePopup(true);
            if (IsOpen) return;
            data?.OnClosed?.Invoke();
        }

        public override void HideUI()
        {
            _presenter?.FlushSound(); // 강제 닫힘에도 저장
            _saveAt = -1f;
            base.HideUI();
        }

        protected override void OnDestroy()
        {
            UIRuntimeOptions.LocaleChanged -= Draw;
            if (_presenter != null) _presenter.Changed -= Draw;
            base.OnDestroy();
        }
    }
}
