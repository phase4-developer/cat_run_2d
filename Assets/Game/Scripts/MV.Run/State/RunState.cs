using System.Collections.Generic;
using MV.Data;

namespace MV.Run
{
    /// <summary>플레이어(`R.p`).</summary>
    public sealed class RunPlayer
    {
        public double X, Z, Tx;
        /// <summary>달리기 위상(그림).</summary>
        public double Phase;
        /// <summary>던지기 자세 남은 시간(그림).</summary>
        public double ThrowT;
    }

    /// <summary>투사체(`R.projs`의 원소, 04 §4.2).</summary>
    public sealed class RunProjectile
    {
        public int Id;
        public double X, Z, Sz;
        public double D;
        public bool Crit;
        public int Tier;
        public int Pierce;
        public bool Spear;
        public bool Dead;
        /// <summary>이미 맞힌 오브젝트 id(`b.hit`).</summary>
        public List<int> HitIds;
    }

    /// <summary>골목·Area 아치 통과 표시(`R.gates`, 04 §4.7.3). GEN이 채운다.</summary>
    public sealed class RunGateMark
    {
        public double Z;
        /// <summary>골목 번호 1..3.</summary>
        public int Floor;
        public bool Passed;
    }

    /// <summary>공방 무기 지급 대기(`R.pendingW`, 게임 시간, updRun 안에서만 줄어든다).</summary>
    public sealed class PendingWeapon
    {
        public double T;
        public int N;
        public bool Done;
    }

    /// <summary>
    /// 수령 지연(108 행 preDelayMs·delayMs·clock). 계단 수령(sgatePass 560ms 실시간)·우물 게이지(wellGauge 260 게임 + 560 실시간).
    /// D-21: 표시와 분리된 결정적 타이머.
    /// </summary>
    public sealed class PendingReceive
    {
        public ReceiveConditionData Cond;
        /// <summary>앞 지연(게임 시간 sdt, 모든 모드). 초.</summary>
        public double PreLeft;
        /// <summary>본 지연(Cond.clock 시계). 초.</summary>
        public double Left;
        public ConsumeType Type;
        public double Amount;
        /// <summary>게이지 수령(GaugeFill)이 쓰는 107 행.</summary>
        public ChargeGateData Charge;
        public int ObjId;
        public bool Done;
    }

    /// <summary>동행 1개의 진행(04 §4.20.5). T = Seconds 타이머(초기값 Every), Armed = Hits·Chance 예약.</summary>
    public sealed class CompanionRuntime
    {
        public CompanionSpec Spec;
        public double T;
        public bool Armed;
        /// <summary>쏜 횟수(표시·시험용).</summary>
        public int Shots;
    }

    /// <summary>폭풍 진행(`godStorm` FX 타이머, sdt, 04 §4.9.3).</summary>
    public sealed class StormJob
    {
        public double Cx, Cz;
        public int N;
        public double K;
        public int Done;
        public bool Finished;
    }

    /// <summary>유성 낙하(휴면, D-20).</summary>
    public sealed class MeteorJob
    {
        public double X, Z, T;
        public int N;
        public bool Finished;
    }

    /// <summary>계단 오르기(`CN`, 04 §4.8).</summary>
    public sealed class ClimbState
    {
        public int StairObjId;
        public int N, K;
        public double Pw, Z0, ZEnd, DH, T;
        public double Time;
        public double Pos;
        /// <summary>칸 생성 간격(MV-SPEC-02 6-5e, <see cref="RunFormulas.ClimbStepEvery"/>).</summary>
        public double StepEvery;
        public int Shown;
        public int Tier = -1;
        public int Lap;
        public int Rem;
        public readonly List<int> Cards = new List<int>();
        /// <summary>보이기 시작한 칸 수(ClimbStepShown 이벤트용).</summary>
        public int StepsShown;
        public int WarpStage = -1;
    }

    /// <summary>룰렛 카드 1장(04 §4.8.4).</summary>
    public sealed class RouletteCard
    {
        public int Tier;
        public CardData Pick;
        public int Step;
        public double Next;
        public double EndAt;
        public bool Chosen;
        public double FlyAt = -1;
        public bool Flying;
        /// <summary>비행 남은 시간(실시간, flyCard 620ms).</summary>
        public double FlyLeft;
        public bool Applied;
        public int Flips;
    }

    /// <summary>룰렛(`RL`). 결과는 시작할 때 확정.</summary>
    public sealed class RouletteState
    {
        public readonly List<int> Tiers = new List<int>();
        public int M;
        public double T;
        public readonly List<RouletteCard> Cards = new List<RouletteCard>();
        /// <summary>모두 적용된 뒤 남은 여유(rlEndPad).</summary>
        public double EndT = -1;
    }

    /// <summary>
    /// 런 상태 루트(프로토 `R` + mode + 시계, 04 §4.14.5). MV.Run 밖은 읽기만 한다.
    /// 채우는 쪽: GEN(<see cref="IRunLevelBuilder.NewRun"/>)이 「GEN」 표시 필드를, SIM이 나머지를 진행한다.
    /// </summary>
    public sealed class RunState : IRunViewState
    {
        public RunState(RunSetup setup, MvTables tables, IRng rng)
        {
            Setup = setup;
            Tables = tables;
            Rng = rng;
            ulong seed = setup?.Seed ?? 0;
            ModeRng = new Pcg32Rng(seed, RunRngStreams.Mode);
            CompanionRng = new Pcg32Rng(seed, RunRngStreams.Companion);
            // 크기는 표에서(MV-SPEC-02 Q-08·Q-09): 지팡이 단계 = 켜진 103_item tier 최대, 사격 유닛 = 본체 + 분신 상한.
            W = new int[RunFormulas.WeaponTierMax(tables) + 1];
            FireT = new double[1 + (tables != null ? RunFormulas.CloneCap(tables) : 0)];
        }

        // ---- 주입 ----
        public readonly RunSetup Setup;
        public readonly MvTables Tables;
        /// <summary>규칙 난수 RunRng(04 §4.17). 반드시 <see cref="Draw"/>로 뽑는다.</summary>
        public readonly IRng Rng;
        /// <summary>null이 아니면 뽑은 규칙 난수를 기록한다(대조·재현).</summary>
        public List<RngDraw> RngLog;
        /// <summary>06_balancePreset 행(GEN).</summary>
        public BalancePresetData Bal;

        // ---- 확장 지점(04 §4.20) ----
        /// <summary>이 런의 규칙. RunSim.Create가 붙인다. <see cref="Emit"/>마다 OnEvent를 부른다.</summary>
        public IRunRule Rule { get; internal set; }
        /// <summary>모드 규칙 난수(Seed 파생 별도 줄기). 정본 경로는 뽑지 않는다.</summary>
        public readonly IRng ModeRng;
        /// <summary>동행 사격 난수(Seed 파생 별도 줄기). Chance 판정만 뽑는다.</summary>
        public readonly IRng CompanionRng;
        /// <summary>동행 진행 상태(Setup.Companions 순서 = 처리 순서).</summary>
        public readonly List<CompanionRuntime> Companions = new List<CompanionRuntime>();

        // ---- 상태 기계 ----
        public RunPhase Phase = RunPhase.Run;
        public RouletteState Roulette;
        public ClimbState Climb;
        public double HitStop;

        // ---- Area·레벨(GEN) ----
        public int Area;
        public int ZoneIndex;
        /// <summary>`R.Z.hp`(모드면 Setup.ZoneHp).</summary>
        public double ZoneHp;
        public int StartLevel;
        public int Level;
        public int Tpl;
        public double Base;
        public double Gz;
        /// <summary>HUD 현재 골목 0..3.</summary>
        public int Floor;
        public int Cp, CpRun;
        public int LvClimbed;
        public BarRecord SavedBars;
        public readonly List<RunGateMark> Gates = new List<RunGateMark>();

        // ---- 오브젝트(GEN 생성 순서 = 판정 순서) ----
        public readonly List<RunObject> Objs = new List<RunObject>();
        public readonly List<RunProjectile> Projs = new List<RunProjectile>();
        public int NextObjId = 1;
        public int NextProjId = 1;

        // ---- 플레이어 ----
        public readonly RunPlayer P = new RunPlayer();

        // ---- 성장(GEN 초기화) ----
        public int GaugeLv = 1;
        public double GaugeFill;
        /// <summary>공방 지급 등급 `R.bt`.</summary>
        public int Bt = 1;
        /// <summary>등급별 무기 장수 `R.w[0..최고 단계]`(0 미사용). 길이 = 103_item 켜진 tier 최대 + 1.</summary>
        public readonly int[] W;
        public int Clones;
        /// <summary>출전 신(113)과 속성 `R.el`(bolt/fire).</summary>
        public GodData God;
        public string El;
        /// <summary>카드 뽑기 풀(110 행, 뽑기 순서). GEN이 111_cardPool로 채운다.</summary>
        public readonly List<CardData> CardPool = new List<CardData>();
        /// <summary>얻은 카드 장수(110 id → n). 순서는 <see cref="CardOrder"/>.</summary>
        public readonly Dictionary<int, int> Cards = new Dictionary<int, int>();
        public readonly List<int> CardOrder = new List<int>();

        // ---- 사격 ----
        /// <summary>유닛별 다음 발사까지 남은 시간. 길이 = 1 + 분신 상한(<see cref="RunFormulas.CloneCap"/>).</summary>
        public readonly double[] FireT;
        public int Vol;

        // ---- 계단·게이트 ----
        public int Stairs;
        public int Climbed;
        public readonly List<int> Climbs = new List<int>();
        public readonly List<PendingWeapon> PendingW = new List<PendingWeapon>();
        public readonly List<PendingReceive> PendingReceives = new List<PendingReceive>();

        // ---- 재화(런 안, 실수 누적) ----
        public double Gold;
        public int Souls;
        public int Dia;
        public int Walls;
        /// <summary>결계에 막혀 있는 연속 시간(MV-SPEC-02 6-8, Q-13). 막히지 않은 프레임에 0.</summary>
        public double StuckT;
        /// <summary>공방 지급이 지팡이 스택 칸 수(WEAPON_STACK_MAX)에 막혀 거절된 장수(MV-SPEC-02 6-3, Q-17).</summary>
        public int WeaponRejected;
        /// <summary>계단 오르기로 앞으로 민 거리 합(ShiftWorld L 합). 템플릿 기준 z = P.Z − Base − ShiftSum.</summary>
        public double ShiftSum;
        /// <summary>런 안 재화 외 보상(정산 대기, 07 §7.9). 지급은 MV.Meta.</summary>
        public readonly List<ConsumeData> PendingSettle = new List<ConsumeData>();

        // ---- 신 효과 ----
        public double StormCd, BlastCd;
        public double BeamT, MeteorT, SpinA, SpinT;
        public readonly List<StormJob> Storms = new List<StormJob>();
        public readonly List<MeteorJob> Meteors = new List<MeteorJob>();

        // ---- 시간·표시 ----
        /// <summary>`R.time`(게임 시간 누적).</summary>
        public double Time;
        /// <summary>실시간 누적(재현 확인용).</summary>
        public double RealTime;
        public long Frame;
        /// <summary>지면 높이 `WY`(표시용, 계단 오르기가 끝날 때 +dH). <see cref="IRunViewState.WorldY"/>.</summary>
        public double WorldY { get; internal set; }
        /// <summary>옹벽이 생겼는가(정본 `R.cliff`). <see cref="IRunViewState.HasCliff"/>.</summary>
        public bool HasCliff { get; internal set; }
        /// <summary>가장 최근 옹벽(정본 `R.cliff`, 계단 오르기 시작 때 덮어씀). <see cref="IRunViewState.Cliff"/>.</summary>
        public RunCliff Cliff { get; internal set; }

        // ---- 종료 ----
        public bool Fin;
        public FinishReason Reason;
        public RunObject HitWall;
        public RunResult Result;

        // ---- 이벤트 ----
        public readonly List<RunEvent> Events = new List<RunEvent>();

        /// <summary>규칙 난수 1개. 사용처를 기록한다.</summary>
        public double Draw(RngUse use)
        {
            double v = Rng.NextDouble();
            RngLog?.Add(new RngDraw(use, v));
            return v;
        }

        /// <summary>`R.objs` 끝에 붙이고 id를 준다(GEN·모드 BuildCourse).</summary>
        public RunObject AddObj(RunObject o)
        {
            o.Id = NextObjId++;
            Objs.Add(o);
            return o;
        }

        public RunObject FindObj(int id)
        {
            for (int i = 0; i < Objs.Count; i++) if (Objs[i].Id == id) return Objs[i];
            return null;
        }

        /// <summary>카드 장수 `cardN(id)`.</summary>
        public int CardN(int cardId) => Cards.TryGetValue(cardId, out var n) ? n : 0;

        /// <summary>장착 최고 등급 `eqTier()`.</summary>
        public int EqTier()
        {
            for (int t = W.Length - 1; t >= 1; t--) if (W[t] > 0) return t;
            return 1;
        }

        /// <summary>이벤트를 쌓고 규칙의 OnEvent를 부른다(§4.20.2).</summary>
        public void Emit(in RunEvent e)
        {
            Events.Add(e);
            Rule?.OnEvent(this, e);
        }
    }
}
