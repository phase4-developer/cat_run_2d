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
        /// <summary>결과 광고를 보는 중(그동안 두 버튼 모두 막음).</summary>
        public bool AdPending { get; private set; }

        IResultAdWatcher Watcher => Data?.AdWatcher ?? MvMetaResultAd.Instance;

        /// <summary>「×N 보상」 버튼 켜짐: 아직 안 받음 && 광고 중 아님 && (골드 ≥ 1 또는 블루 마법석 &gt; 0) && 광고 입구 준비(05 §5.6, 정본 L2414).</summary>
        public bool CanX3
        {
            get
            {
                var d = Data;
                return d != null && !Collected && !AdPending && d.Outcome != null && MvMeta.CanCollectX3(d.Outcome) && Watcher.Ready;
            }
        }

        public void SetView(IResultView view) => View = view;

        /// <summary>새로 열림: 정산 상태를 비우고 그린다.</summary>
        public void Open(ResultViewData data)
        {
            Data = data;
            Collected = false;
            AdPending = false;
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
            v.SetRewards(new ResultRewardText(d.GoldName, "+" + MvUiFormat.Fmt(d.Gold)),
                new ResultRewardText(d.DiaName, "+" + d.Dia),
                new ResultRewardText(d.TokName, "+" + d.Tok));
            v.SetStat(d.Stat ?? "");
            v.SetCollectInteractable(!Collected && !AdPending);
            (v as IResultAdView)?.SetX3(AdText(Watcher.Mul), CanX3);
        }

        /// <summary>결과 광고 버튼 글 「×N 보상」(MV-SPEC-02 01 §12-3). N은 <c>08_adPlacement</c> 결과 광고 행 <c>mul</c>.</summary>
        public static string AdText(int mul) => "×" + mul + " 보상"; // ui.result.adX3

        /// <summary>
        /// 결과 「×N 보상」 광고(MV-SPEC-02 01 §12-3, 08_adPlacement collect3). 광고를 끝까지 봤으면 ×배수 정산 1번, 안 봤으면 정산 없이 다시 고를 수 있다.
        /// <paramref name="finished"/>(정산했는지)를 광고가 끝난 뒤 한 번 부른다. 광고를 시작했으면 true.
        /// </summary>
        public bool TryCollectX3(Action<bool> finished = null)
        {
            if (!CanX3) return false;
            var d = Data;
            AdPending = true;
            Draw();
            Watcher.WatchX3(mul =>
            {
                if (Data != d) { finished?.Invoke(false); return; } // 그사이 다시 열림
                AdPending = false;
                bool ok = mul > 0 && TryCollect(mul);
                if (!ok) Draw();
                finished?.Invoke(ok);
            });
            return true;
        }

        /// <summary>「받기」 ×<paramref name="mul"/>. 열린 동안 정산 명령은 한 번만 부른다. 이번에 불렀으면 true.</summary>
        public bool TryCollect(int mul = 1)
        {
            var d = Data;
            if (Collected || AdPending || d == null || d.Outcome == null) return false;
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
                                           : "Area " + (d.AreaNo + 1) + " 개방까지 " + d.SoulsName; // ui.result.progNext
    }
}
