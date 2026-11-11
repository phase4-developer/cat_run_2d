using System.Collections.Generic;
using MvConsume = MV.Data.ConsumeData;

namespace MV.Meta
{
    /// <summary>런 종료 사유(정본 <c>finish(reason)</c>). 정산 판정에는 Area 돌파 아치 통과(gate)인지만 쓴다.</summary>
    public enum MvRunEnd { Gate, Wall, Other }

    /// <summary>
    /// 본편 런 결과 중 정산이 읽는 값(04 §4.11.1 <c>RunResult</c>). MV.Meta는 MV.Run을 참조하지 않으므로(10 §10 의존 방향)
    /// 결과 화면(MV-UI)이 <c>RunResult</c>를 이 모양으로 옮긴다. pass·zoneEnd·조각은 정산이 정본 공식으로 다시 판정한다.
    /// </summary>
    public sealed class MvRunOutcome
    {
        /// <summary><see cref="MvMeta.BeginRun"/>이 준 런 순번. 같은 순번은 한 번만 정산한다(09 §9.3.1 lastSettledRunSeq).</summary>
        public long RunSeq;
        public MvRunEnd End;
        /// <summary>런 츄르 합계(시작 몫 포함, goldMul 반영 뒤 소수 가능). 지급은 <c>floor((gold − startGold) × mul + startGold)</c>(PC-15).</summary>
        public double Gold;
        public int Souls;
        public int Dia;
        /// <summary>
        /// <see cref="Gold"/>·<see cref="Dia"/> 중 런 시작 몫(302 startGold + 14 StartGold·StartDia). PC-15: 결과 광고 ×N은
        /// 런에서 얻은 몫(합계 − 시작 몫)에만 적용하고 시작 몫은 ×1로 지급한다.
        /// </summary>
        public double StartGold;
        public int StartDia;
        /// <summary>이번 런이 확보한 체크포인트(<c>R.cpRun</c>).</summary>
        public int CpRun;
        /// <summary>
        /// 이번 Area 벽 기록(<c>R.barRec</c>): Floor별 칸 idx → 남은 HP. null이면 기록을 남기지 않는다.
        /// <c>BarRecord.Floors</c>를 그대로 넘긴다.
        /// </summary>
        public IReadOnlyList<IReadOnlyDictionary<int, double>> Bars;
        /// <summary>런 안 재화 외 정산 대기(07 §7.9, <c>RunResult.PendingSettle</c>). Wealth·RbPending만 반영하고 나머지는 결과에 돌려준다.</summary>
        public IReadOnlyList<MvConsume> PendingSettle;
    }

    /// <summary>정산 결과(결과 화면·로비 배너·해금 연출용).</summary>
    public sealed class MvSettleResult
    {
        public MvMetaResponse Response;
        public bool Ok => Response != null && Response.Ok;
        public string Code => Response?.Code;
        /// <summary>정산 전 Area(0기반).</summary>
        public int AreaBefore;
        /// <summary>정산 뒤 Area.</summary>
        public int AreaAfter;
        public bool Pass;
        public bool ZoneEnd;
        public bool ZoneChanged;
        public int Mul;
        public long GoldGained;
        public long DiaGained;
        public int SoulsGained;
        public int Tokens;
        /// <summary>이번 정산으로 새로 열린 <c>05_contentOpen.key</c>(해금 연출 대상).</summary>
        public IReadOnlyList<string> NewlyOpened = new string[0];
        /// <summary>전면 광고를 띄울지(D-24, <c>mul == 1 && area ≥ 08[interstitial].minArea</c>, 광고 제거면 false).</summary>
        public bool ShowInterstitial;
        /// <summary>정산하지 못한 정산 대기 항목(종류 미구현).</summary>
        public IReadOnlyList<MvConsume> Unsettled = new MvConsume[0];
    }

    /// <summary>로비 강화 구매 결과.</summary>
    public sealed class MvLobbyBuyResult
    {
        public MvMetaResponse Response;
        public bool Ok => Response != null && Response.Ok;
        public string Key;
        /// <summary>구매 전·후 Lv(stair = 시작 게이지 Lv, weapon = 무기 Level, income = 0).</summary>
        public int LvBefore;
        public int LvAfter;
        /// <summary>weapon: 덱에 넣은 카드 등급.</summary>
        public int AddedTier;
        /// <summary>weapon: 머지로 생긴 마지막 등급(없으면 0). 0이 아니면 450ms 뒤 merge 소리.</summary>
        public int MergedTier;
    }
}
