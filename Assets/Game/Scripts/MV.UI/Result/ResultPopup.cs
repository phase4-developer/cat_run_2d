using Cysharp.Threading.Tasks;
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
    /// <c>UIPopup_Result</c> 컨트롤러(05 §5.6): 정산 결과 표시 + 「괜찮아요」×1 + 「×N 보상」(N = 08_adPlacement 결과 광고 mul, 가짜 광고 경유, MV-SPEC-02 01 §12-3).
    /// 화면 코드는 <see cref="ResultPresenter"/>, 겉은 <see cref="IResultView"/>·<see cref="IResultAdView"/>(디자이너 겉 <see cref="ResultDesignView"/>, MV-UI3-RESULT).
    /// 받기는 어느 쪽이든 정산 명령을 한 번만 부르고, 팝업 닫기 전환이 끝난 뒤에 <see cref="ResultViewData.OnCollected"/>(게임 루프 → 로비)를 부른다
    /// (<see cref="ResultCloseSequence"/>, MV-UI3-RESULTCLOSE).
    /// </summary>
    public class ResultPopup : UIPopup, ResultCloseSequence.IPort
    {
        [SerializeField] MonoBehaviour view;

        readonly ResultPresenter _presenter = new ResultPresenter();
        IResultView _bound;
        ResultCloseSequence _close;

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
            Unhook();
            _bound = next;
            if (_bound != null) _bound.CollectPressed += PressCollect;
            if (_bound is IResultAdView ad) ad.X3Pressed += PressX3;
            _presenter.SetView(next);
        }

        void Unhook()
        {
            if (_bound == null) return;
            _bound.CollectPressed -= PressCollect;
            if (_bound is IResultAdView ad) ad.X3Pressed -= PressX3;
        }

        protected override void Awake()
        {
            base.Awake();
            hudSettings.displayMode = UIHudDisplayMode.ForceHide; // 열림: HUD 숨김(05 §5.6)
            onHide.AddListener(OnHidden);
        }

        public override void ShowUI()
        {
            if (!IsOpen) _close = new ResultCloseSequence(this);
            base.ShowUI(); // Refresh → _DrawContents → Open
            // 소리 win(관문 도달)·fail(350ms 뒤)은 런 결과 쪽 소리(MV-SND)가 낸다.
        }

        protected override void _DrawContents()
        {
            var data = (popupData as ResultPopupData)?.View;
            if (data != _presenter.Data) Presenter.Open(data);
            else Presenter.Draw();
        }

        /// <summary>×1 받기: 디자이너 겉은 Backboard 터치(노션 [팝업] 인게임 결과), 기획서 「괜찮아요」(MV-SPEC-02 01 §12-3).</summary>
        public void PressCollect()
        {
            if (!Presenter.TryCollect(1)) return;
            MvSound.PlayUi(MvUiSound.ButtonClick);
            Finish();
        }

        /// <summary>「×N 보상」(정본 L2445): 소리 click → 광고(§5.12 collect3) → 끝까지 봤으면 ×mul 정산 1번.</summary>
        public void PressX3()
        {
            if (!Presenter.CanX3) return;
            MvSound.PlayUi(MvUiSound.ButtonClick);
            var data = _presenter.Data;
            _presenter.TryCollectX3(ok =>
            {
                if (ok && _presenter.Data == data) Finish();
            });
        }

        /// <summary>
        /// 뒤로 가기·Esc로는 닫지 않는다. Framework 기본(true)은 받기 없이 팝업만 닫아 <see cref="ResultViewData.OnCollected"/>가
        /// 불리지 않고 게임 루프가 결과 단계에 남는다. 결과는 「괜찮아요」·「×N 보상」으로만 닫는다(기획서에 뒤로 가기 규칙 없음).
        /// </summary>
        public override UniTask<bool> OnBackRequestAsync() => UniTask.FromResult(false);

        /// <summary>
        /// 받기 뒤: 닫기 전환이 끝난 뒤에만 로비로 간다. 열림 전환 중 미리 터치하면(Backboard는 전환 CanvasGroup 밖이라 눌린다)
        /// 열림이 끝나길 기다렸다가 닫는다. 전에는 <c>ClosePopup</c>이 전환 중이라 무시된 채 바로 로비로 가서 결과 팝업이 남았다.
        /// </summary>
        void Finish()
        {
            var data = _presenter.Data;
            var result = _presenter.LastSettle;
            // 전면 광고(result.ShowInterstitial)는 2차(MV-PLAT-ADS). 게임 루프가 OnCollected에서 판단할 수 있다.
            _close ??= new ResultCloseSequence(this);
            _close.Begin(() => data?.OnCollected?.Invoke(result));
        }

        /// <summary>받기 뒤 닫기를 시작했다(로비 이동은 닫힘 완료 뒤).</summary>
        public bool Closing => _close != null && _close.Started && !_close.Done;

        void Update() => _close?.Tick();

        void OnHidden(UIBase _) => _close?.Closed();

        void ResultCloseSequence.IPort.Close() => ClosePopup(true);
        void ResultCloseSequence.IPort.ForceClose() => ForceClosePopup(true);

        protected override void OnDestroy()
        {
            Unhook();
            onHide.RemoveListener(OnHidden);
            base.OnDestroy();
            _close?.Closed(); // 닫힘 완료 없이 파괴돼도(씬 정리 등) 받기를 마쳤으면 로비로 간다
        }
    }
}
