using System;
using System.Collections.Generic;
using MV.Data;
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
            v.SetProgress(Progress(d));
            v.SetRewards(Rewards(d));
            v.SetStat(d.Stat ?? "");
            v.SetCollectInteractable(!Collected && !AdPending);
            (v as IResultAdView)?.SetX3(Watcher.Mul, CanX3);
        }

        /// <summary>채움 비율(0~1). 필요량이 0이면 가득.</summary>
        public static float Fill01(int souls, int need) => need > 0 ? Mathf.Clamp01((float)souls / need) : 1f;

        /// <summary>진척도: 정산 전(<see cref="ResultViewData.SoulsBefore"/>) → 정산 후(<see cref="ResultViewData.SoulsHave"/>), % 정수(소수점 버림, 100 상한).</summary>
        public static ResultProgress Progress(ResultViewData d)
        {
            float to = Fill01(d.SoulsHave, d.SoulsNeed);
            int pct = d.SoulsNeed > 0 ? (int)Math.Min(100L, (long)Math.Max(0, d.SoulsHave) * 100 / d.SoulsNeed) : 100;
            return new ResultProgress(Mathf.Min(Fill01(d.SoulsBefore, d.SoulsNeed), to), to, pct, d.ZoneNo, d.AreaName ?? "",
                ProgressLeft(d), d.SoulsHave + " / " + d.SoulsNeed);
        }

        /// <summary>보상 칸(정본 3칸 = 골드 R.gold, 블루 마법석 R.dia, 황금 생선 R.tok, 05 §5.6·MV-SPEC-02 12-3).</summary>
        public static IReadOnlyList<ResultRewardItem> Rewards(ResultViewData d) => new[]
        {
            new ResultRewardItem(d.Tables, MvWealthIds.TypeOf(MvWealthIds.Gold), d.GoldName, MvUiFormat.Fmt(d.Gold)),
            new ResultRewardItem(d.Tables, MvWealthIds.TypeOf(MvWealthIds.Dia), d.DiaName, d.Dia.ToString()),
            new ResultRewardItem(d.Tables, MvWealthIds.TypeOf(MvWealthIds.RbToken), d.TokName, d.Tok.ToString()),
        };

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

        /// <summary>
        /// 제목(05 §5.6 제목 4종, 언어팩 <c>ui:ui.result.*</c>). 디자이너 왕관·리본 그림은 그대로 두고 글자만 바꾼다(사용자 2026-11-19 「글자만 바꾸는데 언어팩으로 연결해」).
        /// 레벨 완주의 대체 문구는 디자이너 시안 글자와 같다.
        /// </summary>
        public static string TitleText(ResultViewData d)
        {
            switch (d.Kind)
            {
                case ResultKind.ZoneClear: return string.Format(MvUiText.Get("ui", "ui.result.zoneClear", "Zone {0} 클리어!"), d.ZoneNo);
                case ResultKind.AreaOpen: return string.Format(MvUiText.Get("ui", "ui.result.areaOpen", "Area {0} 개방!"), d.AreaNo + 1);
                case ResultKind.LevelDone: return MvUiText.Get("ui", "ui.result.levelDone", LevelDoneFallback);
                default: return MvUiText.Get("ui", "ui.result.fail", "아지트로 귀환");
            }
        }

        /// <summary>디자이너 <c>Obj_Complete/Label Text (TMP)</c> 글자.</summary>
        public const string LevelDoneFallback = "<size=65>Level</size>\nComplete !";

        public static string ProgressLeft(ResultViewData d) =>
            d.Kind == ResultKind.ZoneClear
                ? string.Format(MvUiText.Get("ui", "ui.result.progZone", "Zone {0} · 업데이트 예정"), d.ZoneNo)
                : string.Format(MvUiText.Get("ui", "ui.result.progNext", "Area {0} 개방까지 {1}"), d.AreaNo + 1, d.SoulsName);
    }
}
