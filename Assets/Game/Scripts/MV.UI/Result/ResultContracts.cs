using System;
using MV.Data;
using MV.Meta;
using MV.Run;
using MV.Save;
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
        /// <summary>재화 이름(<c>01_wealth</c>, <see cref="MvUiText.WealthName"/>): 골드·블루 마법석·황금 생선·구출 고양이.</summary>
        public string GoldName, DiaName, TokName, SoulsName;
        /// <summary>통계 줄(<c>msg:result.stat1~4</c>). 1차는 짧은 요약.</summary>
        public string Stat;
        /// <summary>정산 명령에 넘길 값.</summary>
        public MvRunOutcome Outcome;
        /// <summary>정산이 끝난 뒤(성공·실패 모두) 부른다. 게임 루프가 로비로 보낸다.</summary>
        public Action<MvSettleResult> OnCollected;
        /// <summary>비우면 <see cref="MvMetaResultSettler"/>(<c>MvMeta.Current</c>).</summary>
        public IResultSettler Settler;
        /// <summary>결과 「×N 보상」 광고(<c>08_adPlacement collect3</c>, N은 그 행 mul). 비우면 <see cref="MvMetaResultAd"/>(가짜 광고 팝업 경유).</summary>
        public IResultAdWatcher AdWatcher;

        /// <summary>런 결과 → 결과 화면 값 + 정산 값(MV.Meta는 MV.Run을 모르므로 여기서 옮긴다).</summary>
        /// <param name="savedArea">정산 전 <c>save.area</c>(0기반).</param>
        /// <param name="savedSouls">정산 전 <c>save.souls</c>.</param>
        public static ResultViewData Create(RunResult r, long runSeq, int savedArea, int savedSouls, MvTables tables)
        {
            if (r == null) throw new ArgumentNullException(nameof(r));
            bool gate = r.Reason == FinishReason.Gate;
            string soulsName = MvUiText.WealthName(tables, MvWealthIds.TypeOf(MvWealthIds.Souls));
            var d = new ResultViewData
            {
                Kind = r.ZoneEnd ? ResultKind.ZoneClear : r.Pass ? ResultKind.AreaOpen : gate ? ResultKind.LevelDone : ResultKind.Fail,
                AreaNo = savedArea + 1,
                SoulsHave = savedSouls + r.Souls,
                Gold = r.Gold,
                Dia = r.Dia,
                Tok = r.Tok,
                GoldName = MvUiText.WealthName(tables, MvWealthIds.TypeOf(MvWealthIds.Gold)),
                DiaName = MvUiText.WealthName(tables, MvWealthIds.TypeOf(MvWealthIds.Dia)),
                TokName = MvUiText.WealthName(tables, MvWealthIds.TypeOf(MvWealthIds.RbToken)),
                SoulsName = soulsName,
                Stat = "골목 " + r.Walls + " 파괴 · 계단 Lv " + r.GaugeLv + " · " + soulsName + " " + r.Souls, // msg:result.stat* 자리(1차 요약)
                Outcome = new MvRunOutcome
                {
                    RunSeq = runSeq,
                    End = gate ? MvRunEnd.Gate : r.Reason == FinishReason.Wall ? MvRunEnd.Wall : MvRunEnd.Other,
                    Gold = r.Gold,
                    Souls = r.Souls,
                    Dia = r.Dia,
                    StartGold = r.StartGold, // PC-15: 시작 몫은 광고 ×N 대상 아님
                    StartDia = r.StartDia,
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

    /// <summary>결과 「×N 보상」 광고 입구. 정산은 하지 않고 「끝까지 봤으면 배수」만 알린다(정산은 <see cref="IResultSettler"/> 1번).</summary>
    public interface IResultAdWatcher
    {
        /// <summary>false면 광고 버튼을 막는다(메타 없음·자리 꺼짐).</summary>
        bool Ready { get; }
        /// <summary>결과 광고 배수. 출처는 <c>08_adPlacement</c> 결과 광고 행(collect3)의 <c>mul</c> 한 곳(MV-SPEC-02 01 §10·§12-3).</summary>
        int Mul { get; }
        /// <summary>광고 1번. <paramref name="done"/>(배수, 안 봤으면 0)을 한 번 부른다.</summary>
        void WatchX3(Action<int> done);
    }

    /// <summary>기본 결과 광고 입구: <see cref="MvMeta.Current"/> + <see cref="MvFakeAd.Watch"/>(공급자 없으면 가짜 광고 팝업).</summary>
    public sealed class MvMetaResultAd : IResultAdWatcher
    {
        public const string Key = "collect3";
        public static readonly MvMetaResultAd Instance = new MvMetaResultAd();

        public bool Ready
        {
            get
            {
                var m = MvMeta.Current;
                var row = m != null && m.IsReady ? m.Tables.AdPlacement.ByKey(Key) : null;
                return row != null && row.active;
            }
        }

        public int Mul => MulOf(MvMeta.Current?.Tables ?? MvTables.Current);

        /// <summary><c>08_adPlacement</c> 결과 광고 행의 <c>mul</c>. 행이 없으면 0(그때 버튼은 <see cref="Ready"/>로 막힌다).</summary>
        public static int MulOf(MvTables t) => t?.AdPlacement.ByKey(Key)?.mul ?? 0;

        public void WatchX3(Action<int> done)
        {
            if (!Ready) { done?.Invoke(0); return; }
            int mul = Mul;
            MvFakeAd.Watch(Key, watched => done?.Invoke(watched ? mul : 0));
        }
    }

    /// <summary>결과 겉의 「×N 보상」 광고 버튼 부분. 기존 <see cref="IResultView"/> 구현을 깨지 않도록 나눴다. 기본 겉 <see cref="ResultView"/>는 둘 다 구현한다.</summary>
    public interface IResultAdView
    {
        /// <summary>「×N 보상」(보상 광고, MV-SPEC-02 01 §12-3).</summary>
        event Action X3Pressed;
        void SetX3(string text, bool interactable);
    }

    /// <summary>결과 겉(D-92). 컨트롤러 <see cref="ResultPopup"/>는 이 인터페이스로만 겉을 만진다.</summary>
    public interface IResultView
    {
        /// <summary>「괜찮아요」(×1 받기)를 눌렀다.</summary>
        event Action CollectPressed;
        void SetTitle(string text, Color color);
        void SetProgress(string left, string right, float fill01);
        /// <summary>보상 3칸: 골드·블루 마법석·황금 생선(MV-SPEC-02 01 §12-3). 이름은 <c>01_wealth</c>에서.</summary>
        void SetRewards(ResultRewardText gold, ResultRewardText dia, ResultRewardText tok);
        void SetStat(string text);
        void SetCollectInteractable(bool interactable);
    }

    /// <summary>보상 칸 1개의 글: 재화 이름 + 값.</summary>
    public struct ResultRewardText
    {
        public string Name;
        public string Value;
        public ResultRewardText(string name, string value) { Name = name; Value = value; }
    }
}
