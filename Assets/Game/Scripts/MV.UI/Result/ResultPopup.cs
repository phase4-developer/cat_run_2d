using jjevol;
using MV.Sound;
using UnityEngine;

namespace MV.UI
{
    /// <summary>Framework 팝업 데이터 껍데기. 내용은 <see cref="ResultViewData"/>.</summary>
    public sealed class ResultPopupData : IPopupData
    {
        public readonly ResultViewData View;
        public ResultPopupData(ResultViewData view) { View = view; }
    }

    /// <summary>
    /// <c>UIPopup_Result</c> 컨트롤러(05 §5.6, 1차: 정산 결과 표시 + 「받기」×1). ×3 광고는 2차.
    /// 화면 코드는 <see cref="ResultPresenter"/>, 겉은 <see cref="IResultView"/>(기본 <see cref="ResultView"/>).
    /// 「받기」는 정산 명령을 한 번만 부르고 팝업을 닫은 뒤 <see cref="ResultViewData.OnCollected"/>(게임 루프 → 로비)를 부른다.
    /// </summary>
    public class ResultPopup : UIPopup
    {
        [SerializeField] MonoBehaviour view;

        readonly ResultPresenter _presenter = new ResultPresenter();
        IResultView _bound;

        public ResultPresenter Presenter
        {
            get
            {
                if (_bound == null) SetView(view as IResultView ?? GetComponent<IResultView>());
                return _presenter;
            }
        }

        public void SetView(IResultView next)
        {
            if (_bound != null) _bound.CollectPressed -= PressCollect;
            _bound = next;
            if (_bound != null) _bound.CollectPressed += PressCollect;
            _presenter.SetView(next);
        }

        protected override void Awake()
        {
            base.Awake();
            hudSettings.displayMode = UIHudDisplayMode.ForceHide; // 열림: HUD 숨김(05 §5.6)
            MvUi.EnsureLayer(gameObject, MvUiLayer.Result, true);
        }

        public override void ShowUI()
        {
            base.ShowUI(); // Refresh → _DrawContents → Open
            // 소리 win(관문 도달)·fail(350ms 뒤)은 런 결과 쪽 소리(MV-SND)가 낸다.
        }

        protected override void _DrawContents()
        {
            var data = (popupData as ResultPopupData)?.View;
            if (data != _presenter.Data) Presenter.Open(data);
            else Presenter.Draw();
        }

        /// <summary>「받기」(×1, 05 §5.6).</summary>
        public void PressCollect()
        {
            if (!Presenter.TryCollect(1)) return;
            MvSound.PlayUi(MvUiSound.ButtonClick);
            var data = _presenter.Data;
            var result = _presenter.LastSettle;
            // 전면 광고(result.ShowInterstitial)는 2차(MV-PLAT-ADS). 게임 루프가 OnCollected에서 판단할 수 있다.
            ClosePopup(true);
            data.OnCollected?.Invoke(result);
        }

        protected override void OnDestroy()
        {
            if (_bound != null) _bound.CollectPressed -= PressCollect;
            base.OnDestroy();
        }
    }
}
