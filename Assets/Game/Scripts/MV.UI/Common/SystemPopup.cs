using System;
using Cysharp.Threading.Tasks;
using jjevol;
using MV.Sound;
using UnityEngine;

namespace MV.UI
{
    /// <summary>
    /// 공용 시스템 팝업 데이터(노션 「[팝업] 시스템 기본 팝업」, 디자이너 <c>Popup_CommonSystem</c>).
    /// 선택이면 OK를 켜고(<see cref="Confirm"/>), 알림만이면 OK를 끈다(<see cref="Notice"/>: Cancel·X로 닫음).
    /// 글자를 비우면 언어팩 기본 문구(제목 <c>ui.popup.notice</c>, 버튼 <c>ui.common.ok</c>·<c>ui.common.cancel</c>)를 쓴다.
    /// </summary>
    public sealed class SystemPopupData : IPopupData
    {
        public string Title;
        public string Contents;
        /// <summary>true = 선택(OK·Cancel), false = 알림(Cancel·X만).</summary>
        public bool ShowOk = true;
        public string OkText;
        public string CancelText;
        /// <summary>닫힐 때 한 번. true = OK, false = Cancel·X·뒤로·강제 닫힘.</summary>
        public Action<bool> OnClosed;

        public bool Done { get; private set; }

        /// <summary>처음 한 번만 <see cref="OnClosed"/>를 부른다. 불렀으면 true.</summary>
        public bool Finish(bool ok)
        {
            if (Done) return false;
            Done = true;
            OnClosed?.Invoke(ok);
            return true;
        }

        /// <summary>알림: OK 끔, Cancel·X로 닫는다.</summary>
        public static SystemPopupData Notice(string contents, string title = null, Action onClosed = null) => new SystemPopupData
        {
            Title = title,
            Contents = contents,
            ShowOk = false,
            OnClosed = onClosed == null ? (Action<bool>)null : _ => onClosed(),
        };

        /// <summary>선택: OK·Cancel(X도 Cancel과 같음).</summary>
        public static SystemPopupData Confirm(string contents, Action<bool> onClosed = null, string title = null) => new SystemPopupData
        {
            Title = title,
            Contents = contents,
            ShowOk = true,
            OnClosed = onClosed,
        };

        public string TitleText => string.IsNullOrEmpty(Title) ? MvUiText.Ui("ui.popup.notice") : Title;
        public string OkLabel => string.IsNullOrEmpty(OkText) ? MvUiText.Ui("ui.common.ok") : OkText;
        public string CancelLabel => string.IsNullOrEmpty(CancelText) ? MvUiText.Ui("ui.common.cancel") : CancelText;
    }

    /// <summary>
    /// <c>UIPopup_CommonSystem</c> 컨트롤러. OK → 결과 true, Cancel·X·뒤로 → false. 어떻게 닫히든 콜백은 한 번.
    /// 여는 곳은 <see cref="MvUiNav.OpenSystemAsync"/>·<see cref="MvUiNav.ConfirmAsync"/>·<see cref="MvUiNav.NoticeAsync"/>.
    /// </summary>
    public class SystemPopup : UIPopup
    {
        [SerializeField] SystemPopupView view;

        bool _bound;

        public SystemPopupView View => view;
        SystemPopupData Data => popupData as SystemPopupData;

        protected override void Awake()
        {
            base.Awake();
            hudSettings.displayMode = UIHudDisplayMode.KeepPrevious;
            Bind();
        }

        void Bind()
        {
            if (_bound || view == null) return;
            _bound = true;
            view.OkPressed += PressOk;
            view.CancelPressed += PressCancel;
        }

        protected override void _DrawContents()
        {
            Bind();
            var d = Data;
            if (view == null || d == null) return;
            view.Draw(d.TitleText, d.Contents, d.ShowOk, d.OkLabel, d.CancelLabel);
        }

        public void PressOk()
        {
            if (!IsOpen || IsTransitioning) return;
            MvSound.PlayUi(MvUiSound.ButtonClick);
            Data?.Finish(true);
            ClosePopup(true);
        }

        public void PressCancel()
        {
            if (!IsOpen || IsTransitioning) return;
            MvSound.PlayUi(MvUiSound.ButtonClick);
            Data?.Finish(false);
            ClosePopup(false);
        }

        public override UniTask<bool> OnBackRequestAsync()
        {
            if (IsOpen && !IsTransitioning)
            {
                Data?.Finish(false);
                ClosePopup(false);
            }
            return UniTask.FromResult(false);
        }

        public override void HideUI()
        {
            base.HideUI();
            Data?.Finish(false); // 강제 닫힘도 취소로 끝낸다
        }

        protected override void OnDestroy()
        {
            Data?.Finish(false);
            if (_bound && view != null) { view.OkPressed -= PressOk; view.CancelPressed -= PressCancel; }
            base.OnDestroy();
        }
    }
}
