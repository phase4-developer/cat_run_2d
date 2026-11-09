using System;
using MV.Meta;
using UnityEngine;

namespace MV.UI
{
    /// <summary>
    /// 결과 화면 코드(D-92 ③④): 표시 값 그리기와 「받기」 정산 1회. Framework 화면 타입과 떨어진 순수 클래스.
    /// 컨트롤러 <see cref="ResultPopup"/>가 열림·닫힘만 맡는다.
    /// </summary>
    public sealed class ResultPresenter
    {
        public IResultView View { get; private set; }
        public ResultViewData Data { get; private set; }
        /// <summary>이번에 열린 뒤 정산을 불렀는지.</summary>
        public bool Collected { get; private set; }
        /// <summary>마지막 정산 결과(없거나 예외면 null).</summary>
        public MvSettleResult LastSettle { get; private set; }

        public void SetView(IResultView view) => View = view;

        /// <summary>새로 열림: 정산 상태를 비우고 그린다.</summary>
        public void Open(ResultViewData data)
        {
            Data = data;
            Collected = false;
            LastSettle = null;
            Draw();
        }

        public void Draw()
        {
            var v = View;
            var d = Data;
            if (v == null || d == null) return;
            v.SetTitle(TitleText(d), d.Kind == ResultKind.Fail ? MvUi.Hex("#ffb08a") : MvUi.Gold);
            v.SetProgress(ProgressLeft(d), d.SoulsHave + " / " + d.SoulsNeed,
                d.SoulsNeed > 0 ? Mathf.Clamp01((float)d.SoulsHave / d.SoulsNeed) : 1f);
            v.SetRewards("+" + MvUiFormat.Fmt(d.Gold), "+" + d.Dia, "+" + d.Tok);
            v.SetStat(d.Stat ?? "");
            v.SetCollectInteractable(!Collected);
        }

        /// <summary>「받기」 ×<paramref name="mul"/>. 열린 동안 정산 명령은 한 번만 부른다. 이번에 불렀으면 true.</summary>
        public bool TryCollect(int mul = 1)
        {
            var d = Data;
            if (Collected || d == null || d.Outcome == null) return false;
            Collected = true;
            View?.SetCollectInteractable(false);
            var settler = d.Settler ?? MvMetaResultSettler.Instance;
            try
            {
                LastSettle = settler.Settle(d.Outcome, mul);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
            if (LastSettle != null && !LastSettle.Ok) Debug.LogWarning("[ResultPresenter] settle failed: " + LastSettle.Code);
            return true;
        }

        // 문구: ui:ui.result.* (LocalizationManager.GetLoc 교체 지점)
        public static string TitleText(ResultViewData d)
        {
            switch (d.Kind)
            {
                case ResultKind.ZoneClear: return "Zone " + d.ZoneNo + " 클리어!";     // ui.result.zoneClear
                case ResultKind.AreaOpen: return "Area " + (d.AreaNo + 1) + " 개방!";  // ui.result.areaOpen
                case ResultKind.LevelDone: return "LEVEL COMPLETED!";                  // ui.result.levelDone
                default: return "아지트로 귀환";                                        // ui.result.fail
            }
        }

        public static string ProgressLeft(ResultViewData d) =>
            d.Kind == ResultKind.ZoneClear ? "Zone " + d.ZoneNo + " · 업데이트 예정"  // ui.result.progZone
                                           : "Area " + (d.AreaNo + 1) + " 개방까지 생선"; // ui.result.progNext
    }
}
