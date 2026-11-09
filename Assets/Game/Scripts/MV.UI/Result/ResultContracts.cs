using System;
using MV.Data;
using MV.Meta;
using MV.Run;
using UnityEngine;

namespace MV.UI
{
    /// <summary>결과 제목 종류(05 §5.6).</summary>
    public enum ResultKind { ZoneClear, AreaOpen, LevelDone, Fail }

    /// <summary>
    /// 결과 화면 데이터(05 §5.6 <c>ResultViewData</c>). 게임 루프가 <see cref="Create"/>로 만들고
    /// <see cref="MvUiNav.OpenResultAsync"/>로 연다(Framework 팝업 데이터 껍데기 <see cref="ResultPopupData"/>로 감싼다).
    /// </summary>
    public sealed class ResultViewData
    {
        public ResultKind Kind;
        /// <summary>제목에 쓰는 Zone 번호(<c>301_zone.sort</c>).</summary>
        public int ZoneNo;
        /// <summary>현재 Area 1기반 번호. 개방 제목은 AreaNo + 1.</summary>
        public int AreaNo;
        public int SoulsHave;
        public int SoulsNeed;
        public double Gold;
        public int Dia;
        public int Tok;
        /// <summary>통계 줄(<c>msg:result.stat1~4</c>). 1차는 짧은 요약.</summary>
        public string Stat;
        /// <summary>정산 명령에 넘길 값.</summary>
        public MvRunOutcome Outcome;
        /// <summary>정산이 끝난 뒤(성공·실패 모두) 부른다. 게임 루프가 로비로 보낸다.</summary>
        public Action<MvSettleResult> OnCollected;
        /// <summary>비우면 <see cref="MvMetaResultSettler"/>(<c>MvMeta.Current</c>).</summary>
        public IResultSettler Settler;

        /// <summary>런 결과 → 결과 화면 값 + 정산 값(MV.Meta는 MV.Run을 모르므로 여기서 옮긴다).</summary>
        /// <param name="savedArea">정산 전 <c>save.area</c>(0기반).</param>
        /// <param name="savedSouls">정산 전 <c>save.souls</c>.</param>
        public static ResultViewData Create(RunResult r, long runSeq, int savedArea, int savedSouls, MvTables tables)
        {
            if (r == null) throw new ArgumentNullException(nameof(r));
            bool gate = r.Reason == FinishReason.Gate;
            var d = new ResultViewData
            {
                Kind = r.ZoneEnd ? ResultKind.ZoneClear : r.Pass ? ResultKind.AreaOpen : gate ? ResultKind.LevelDone : ResultKind.Fail,
                AreaNo = savedArea + 1,
                SoulsHave = savedSouls + r.Souls,
                Gold = r.Gold,
                Dia = r.Dia,
                Tok = r.Tok,
                Stat = "골목 " + r.Walls + " 파괴 · 계단 Lv " + r.GaugeLv + " · 생선 " + r.Souls, // msg:result.stat* 자리(1차 요약)
                Outcome = new MvRunOutcome
                {
                    RunSeq = runSeq,
                    End = gate ? MvRunEnd.Gate : r.Reason == FinishReason.Wall ? MvRunEnd.Wall : MvRunEnd.Other,
                    Gold = r.Gold,
                    Souls = r.Souls,
                    Dia = r.Dia,
                    CpRun = r.CpRun,
                    Bars = r.BarRec?.Floors,
                    PendingSettle = r.PendingSettle,
                },
            };
            if (tables != null)
                foreach (var a in tables.Area.Datas)
                    if (a.areaNo == d.AreaNo)
                    {
                        d.SoulsNeed = a.needSouls;
                        d.ZoneNo = tables.Zone.Get(a.zoneId)?.sort ?? 1;
                        break;
                    }
            return d;
        }
    }

    /// <summary>결과 「받기」가 부르는 정산 명령. 기본은 <see cref="MvMetaResultSettler"/>.</summary>
    public interface IResultSettler
    {
        MvSettleResult Settle(MvRunOutcome outcome, int mul);
    }

    /// <summary><see cref="MvMeta.Current"/>.Settle(정산·저장 1회).</summary>
    public sealed class MvMetaResultSettler : IResultSettler
    {
        public static readonly MvMetaResultSettler Instance = new MvMetaResultSettler();

        public MvSettleResult Settle(MvRunOutcome outcome, int mul)
        {
            var meta = MvMeta.Current;
            if (meta == null) throw new InvalidOperationException("MvMeta.Current is not set.");
            return meta.Settle(outcome, mul);
        }
    }

    /// <summary>결과 겉(D-92). 컨트롤러 <see cref="ResultPopup"/>는 이 인터페이스로만 겉을 만진다.</summary>
    public interface IResultView
    {
        /// <summary>「받기」(×1)를 눌렀다.</summary>
        event Action CollectPressed;
        void SetTitle(string text, Color color);
        void SetProgress(string left, string right, float fill01);
        /// <summary>보상 3칸: 츄르·캣닢·목숨 조각(05 §5.6).</summary>
        void SetRewards(string gold, string dia, string tok);
        void SetStat(string text);
        void SetCollectInteractable(bool interactable);
    }
}
