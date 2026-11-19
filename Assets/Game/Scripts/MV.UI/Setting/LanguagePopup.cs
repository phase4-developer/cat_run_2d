using System;
using Cysharp.Threading.Tasks;
using jjevol;
using MV.Sound;
using UnityEngine;

namespace MV.UI
{
    /// <summary>언어 팝업 데이터. 언어를 바꾸면 <see cref="OnSelected"/>(설정 팝업 언어 글자 갱신).</summary>
    public sealed class LanguagePopupData : IPopupData
    {
        public Action OnSelected;
    }

    /// <summary><c>UIPopup_SettingLanguage</c> 컨트롤러(층 500, D-LOC-PLAN ③). 고르면 언어를 바꾸고(저장 포함) 닫는다. 장면 다시 읽기는 없다.</summary>
    public class LanguagePopup : UIPopup
    {
        [SerializeField] LanguageDesignView view;

        bool _bound, _busy;

        protected override void Awake()
        {
            base.Awake();
            MvUi.EnsureLayer(gameObject, MvUiLayer.Popup, true);
            Bind();
        }

        void Bind()
        {
            if (_bound || view == null) return;
            _bound = true;
            view.LanguagePressed += code => SelectAsync(code).Forget(Debug.LogException);
            view.ClosePressed += () => { MvSound.PlayUi(MvUiSound.ButtonClick); Close(); };
        }

        protected override void _DrawContents()
        {
            Bind();
            var lang = MvUiLanguage.Service;
            if (view == null) return;
            if (lang == null)
            {
                Debug.LogWarning("[LanguagePopup] 언어 서비스가 없습니다(부트 7단계 MvBootLocalization 전).");
                view.Draw(Array.Empty<string>(), null, null);
                return;
            }
            view.Draw(lang.Codes, lang.Current, lang.DisplayName);
        }

        async UniTask SelectAsync(string code)
        {
            var lang = MvUiLanguage.Service;
            if (_busy || lang == null || string.IsNullOrEmpty(code)) return;
            MvSound.PlayUi(MvUiSound.ButtonClick);
            _busy = true;
            try
            {
                await lang.SetAsync(code);
            }
            finally
            {
                _busy = false;
            }
            (popupData as LanguagePopupData)?.OnSelected?.Invoke();
            Close();
        }

        public override UniTask<bool> OnBackRequestAsync()
        {
            if (IsOpen && !IsTransitioning) Close();
            return UniTask.FromResult(false);
        }

        void Close()
        {
            if (!IsOpen) return;
            ClosePopup(true);
        }
    }
}
