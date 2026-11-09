using System;
using jjevol;
using MV.Meta;
using MV.Sound;
using UnityEngine;

namespace MV.UI
{
    /// <summary>
    /// <c>UIPopup_Result</c> 컨트롤러(05 §5.6, 1차: 정산 결과 표시 + 「받기」×1). ×3 광고는 2차.
    /// 「받기」는 정산 명령을 한 번만 부르고 팝업을 닫는다. 로비로 가는 일은 <see cref="ResultViewData.OnCollected"/>(게임 루프).
    /// </summary>
    public class ResultPopup : UIPopup
    {
        [SerializeField] MonoBehaviour view;

        IResultView _view;
        bool _collected;

        public ResultViewData Data => popupData as ResultViewData;

        /// <summary>이번에 열린 뒤 정산을 불렀는지.</summary>
        public bool Collected => _collected;

        /// <summary>마지막 정산 결과(없으면 null).</summary>
        public MvSettleResult LastSettle { get; private set; }

        public IResultView View
        {
            get
            {
                if (_view == null) SetView(view as IResultView ?? GetComponent<IResultView>());
                return _view;
            }
        }

        public void SetView(IResultView next)
        {
            if (_view != null) _view.CollectPressed -= PressCollect;
            _view = next;
            if (_view != null) _view.CollectPressed += PressCollect;
        }

        protected override void Awake()
        {
            base.Awake();
            hudSettings.displayMode = UIHudDisplayMode.ForceHide; // 열림: HUD 숨김(05 §5.6)
            MvUi.EnsureLayer(gameObject, MvUiLayer.Result, true);
        }

        public override void ShowUI()
        {
            _collected = false;
            LastSettle = null;
            base.ShowUI();
            // 소리 win(관문 도달)·fail(350ms 뒤)은 런 결과 쪽 소리(MV-SND 라우터)가 낸다.
        }

        protected override void _DrawContents()
        {
            var v = View;
            var d = Data;
            if (v == null || d == null) return;
            v.SetTitle(TitleText(d), d.Kind == ResultKind.Fail ? MvUi.Hex("#ffb08a") : MvUi.Gold);
            v.SetProgress(ProgressLeft(d), d.SoulsHave + " / " + d.SoulsNeed,
                d.SoulsNeed > 0 ? Mathf.Clamp01((float)d.SoulsHave / d.SoulsNeed) : 1f);
            v.SetRewards("+" + MvUiFormat.Fmt(d.Gold), "+" + d.Dia, "+" + d.Tok);
            v.SetStat(d.Stat ?? "");
            v.SetCollectInteractable(!_collected);
        }

        /// <summary>「받기」(×1, 05 §5.6). 한 번 열린 동안 정산 명령은 한 번만 부른다.</summary>
        public void PressCollect()
        {
            var d = Data;
            if (_collected || d == null || d.Outcome == null) return;
            _collected = true;
            View?.SetCollectInteractable(false);
            MvSound.PlayUi(MvUiSound.ButtonClick);

            var settler = d.Settler ?? MvMetaResultSettler.Instance;
            MvSettleResult result = null;
            try
            {
                result = settler.Settle(d.Outcome, 1);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
            LastSettle = result;
            if (result != null && !result.Ok) Debug.LogWarning("[ResultPopup] settle failed: " + result.Code);
            // 전면 광고(result.ShowInterstitial)는 2차(MV-PLAT-ADS). 게임 루프가 OnCollected에서 판단할 수 있다.
            ClosePopup(true);
            d.OnCollected?.Invoke(result);
        }

        // 문구: ui:ui.result.* (LocalizationManager.GetLoc 교체 지점)
        public static string TitleText(ResultViewData d)
        {
            switch (d.Kind)
            {
                case ResultKind.ZoneClear: return "Zone " + d.ZoneNo + " 클리어!"; // ui.result.zoneClear
                case ResultKind.AreaOpen: return "Area " + (d.AreaNo + 1) + " 개방!"; // ui.result.areaOpen
                case ResultKind.LevelDone: return "LEVEL COMPLETED!";                 // ui.result.levelDone
                default: return "아지트로 귀환";                                       // ui.result.fail
            }
        }

        static string ProgressLeft(ResultViewData d) =>
            d.Kind == ResultKind.ZoneClear ? "Zone " + d.ZoneNo + " · 업데이트 예정" // ui.result.progZone
                                           : "Area " + (d.AreaNo + 1) + " 개방까지 생선"; // ui.result.progNext

        protected override void OnDestroy()
        {
            if (_view != null) _view.CollectPressed -= PressCollect;
            base.OnDestroy();
        }
    }
}
