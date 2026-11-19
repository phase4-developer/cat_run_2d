using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using jjevol;
using MV.Sound;
using UnityEngine;

namespace MV.UI
{
    /// <summary>메타 패널 팝업 데이터. 비우면 기본 입구(<see cref="MvMetaUiPort"/>).</summary>
    public class MetaPopupData : IPopupData
    {
        public IMetaUiPort Port;
        /// <summary>닫힌 뒤(로비 다시 그림 등). 게임 루프·로비가 넘긴다.</summary>
        public Action OnClosed;
    }

    /// <summary>신기능 해금 팝업 데이터. 비우면 <see cref="IMetaUiPort.PendingUnlockShows"/>.</summary>
    public sealed class FeatureUnlockPopupData : MetaPopupData
    {
        public IReadOnlyList<string> Keys;
    }

    /// <summary>
    /// 토스트 연결 지점. 토스트 층(MV-UI-HUD <c>Effect_Toast</c>)이 <see cref="Handler"/>를 넣는다. 없으면 로그만 남긴다.
    /// </summary>
    public static class MvUiToast
    {
        public static Action<string> Handler;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Handler = null;

        public static void Show(string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            if (Handler != null) Handler(text);
            else Debug.Log("[Toast] " + text);
        }
    }

    /// <summary>
    /// 메타 공통 패널 틀 컨트롤러(<c>MetaPanelPopup : UIPopup</c>, 05 §5.16). 열림·닫힘·소리·토스트만 맡고
    /// 화면 코드는 <see cref="MetaPanelPresenter"/>, 겉은 <see cref="IMetaPanelView"/>(기본 <see cref="MetaPanelView"/>).
    /// 새 메타 화면 = 이 클래스 하위 1개(<see cref="CreatePresenter"/>) + 프리팹 1개.
    /// </summary>
    public abstract class MetaPanelPopup : UIPopup
    {
        [SerializeField] MonoBehaviour view;

        MetaPanelPresenter _presenter;
        IMetaPanelView _bound;
        bool _viewBound;
        bool _needOpen = true;

        protected abstract MetaPanelPresenter CreatePresenter();

        MetaPanelPresenter P
        {
            get
            {
                if (_presenter != null) return _presenter;
                _presenter = CreatePresenter();
                _presenter.CloseRequested += Close;
                _presenter.Toast += MvUiToast.Show;
                _presenter.Sound += PlaySound;
                return _presenter;
            }
        }

        public MetaPanelPresenter Presenter
        {
            get
            {
                var p = P;
                if (!_viewBound) SetView(view as IMetaPanelView ?? GetComponent<IMetaPanelView>());
                return p;
            }
        }

        public void SetView(IMetaPanelView next)
        {
            if (_bound != null) Unhook(_bound);
            _viewBound = true;
            _bound = next;
            if (_bound != null)
            {
                _bound.ClosePressed += OnClosePressed;
                _bound.DimPressed += OnDimPressed;
                _bound.ActionPressed += OnActionPressed;
                _bound.TabPressed += OnTabPressed;
            }
            P.SetView(next);
        }

        void Unhook(IMetaPanelView v)
        {
            v.ClosePressed -= OnClosePressed;
            v.DimPressed -= OnDimPressed;
            v.ActionPressed -= OnActionPressed;
            v.TabPressed -= OnTabPressed;
        }

        protected override void Awake()
        {
            base.Awake();
        }

        protected MetaPopupData Data => popupData as MetaPopupData;

        public override void ShowUI()
        {
            _needOpen = true;
            base.ShowUI(); // Refresh → _DrawContents
        }

        /// <summary>열릴 때 한 번 열림 처리, 그 뒤 SetDirty 다시 그림은 그리기만.</summary>
        protected override void _DrawContents()
        {
            var p = Presenter;
            if (!_needOpen) { p.Draw(); return; }
            _needOpen = false;
            p.SetPort(Data?.Port);
            OnOpening(p);
            p.Open();
        }

        /// <summary>열기 직전(팝업 데이터를 화면 코드에 넘기는 자리).</summary>
        protected virtual void OnOpening(MetaPanelPresenter presenter) { }

        void Update()
        {
            if (IsOpen && _presenter != null) _presenter.Tick(Time.unscaledDeltaTime);
        }

        void OnClosePressed()
        {
            MvSound.PlayUi(MvUiSound.ButtonClick);
            Presenter.PressClose();
        }

        void OnDimPressed() => Presenter.PressDim();
        void OnActionPressed(string id) => Presenter.Press(id);
        void OnTabPressed(string id) => Presenter.SelectTab(id);

        /// <summary>
        /// 뒤로 가기·Esc(<c>UIManager.HandleBackButtonAsync</c>)와 Framework 닫기 버튼(<c>OnClickCloseBtn</c>)은
        /// 이 값이 true면 <c>ClosePopup</c>을 직접 불러 <see cref="MetaPopupData.OnClosed"/>를 건너뛴다(런 끝 지도 → 결과가 멈춤).
        /// 그래서 닫기 버튼과 같은 길(화면 코드 PressClose → <see cref="Close"/>)로 보내고 false를 돌려 Framework가 다시 닫지 않게 한다.
        /// </summary>
        public override UniTask<bool> OnBackRequestAsync()
        {
            if (IsOpen && !IsTransitioning) Presenter.PressClose();
            return UniTask.FromResult(false);
        }

        /// <summary>실제로 닫혔을 때만 <see cref="MetaPopupData.OnClosed"/>를 한 번 부른다(이미 닫힘·연출 중이라 무시된 닫기는 부르지 않는다).</summary>
        void Close()
        {
            if (!IsOpen) return;
            var data = Data;
            ClosePopup(true);
            if (IsOpen) return;
            data?.OnClosed?.Invoke();
        }

        static void PlaySound(MetaSound s)
        {
            switch (s)
            {
                case MetaSound.Click: MvSound.PlayUi(MvUiSound.ButtonClick); break;
                case MetaSound.AreaBuy: MvSound.PlayUi(MvUiSound.AreaUpgradeBuy); break;
                case MetaSound.PerkBuy: MvSound.PlayUi(MvUiSound.RebirthPerkBuy); break;
                case MetaSound.Rebirth: MvSound.PlayUi(MvUiSound.RebirthDone); break;
                case MetaSound.Unlock: MvSound.PlayUi(MvUiSound.UnlockOverlay); break;
            }
        }

        protected override void OnDestroy()
        {
            if (_bound != null) Unhook(_bound);
            if (_presenter != null)
            {
                _presenter.CloseRequested -= Close;
                _presenter.Toast -= MvUiToast.Show;
                _presenter.Sound -= PlaySound;
            }
            base.OnDestroy();
        }
    }
}
