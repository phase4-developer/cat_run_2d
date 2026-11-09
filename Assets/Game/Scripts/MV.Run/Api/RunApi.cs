using System;
using System.Collections.Generic;
using MV.Data;

namespace MV.Run
{
    /// <summary>런 결과(04 §4.11.1). 영속 반영은 MV.Meta.Collect만 한다.</summary>
    public sealed class RunResult
    {
        public FinishReason Reason;
        public double Gold;
        public int Souls;
        public int Dia;
        public int Tok;
        public bool Pass;
        public bool ZoneEnd;
        public int CpRun;
        public BarRecord BarRec;
        public int Climbed;
        public int Walls;
        public int GaugeLv;
        public int EqTier;
        public int Clones;
        public int CardCount;
        /// <summary>막힌 장애물의 골목 번호(없으면 −1).</summary>
        public int HitWallBar = -1;
        /// <summary>런 안 재화 외 정산 대기(07 §7.9).</summary>
        public List<ConsumeData> PendingSettle = new List<ConsumeData>();
    }

    /// <summary>
    /// MV-RUN-GEN 진입점(04 §4.14). GEN은 `S/MV.Run/Gen/`에 구현하고 표만 보고 채운다.
    /// SIM은 이 인터페이스로만 부른다(<see cref="RunSim.Create"/>에 주입).
    /// </summary>
    public interface IRunLevelBuilder
    {
        /// <summary>
        /// newRun(§4.14.5): <c>new RunState(setup, tables, rng)</c>를 만들고 다음을 채운 뒤 <see cref="BuildLevel"/>(0, setup.Level, bars)을 부른다.
        /// Bal, Area, ZoneIndex, ZoneHp, StartLevel, Cp, God, El, CardPool, GaugeLv/GaugeFill(kegStart), Bt, W(덱 복사), Clones,
        /// FireT(`300_runConfig.FIRE_PHASE`), P(0,0,tx 0). 규칙 난수는 <see cref="RunState.Draw"/>(쥐 속도 <see cref="RngUse.EnemySpeed"/>)로만 뽑는다.
        /// 이벤트(RunStarted·WeaponHandReset)는 SIM이 낸다.
        /// </summary>
        RunState NewRun(RunSetup setup, MvTables tables, IRng rng);

        /// <summary>
        /// buildLevel(§4.14.4): 오브젝트를 <see cref="RunState.AddObj"/>로 정본 순서대로 붙이고(<see cref="RunObjectFactory.Create"/>로 생성),
        /// Level, Tpl, Base, Gates, Gz, Floor=0, Cp, CpRun=Cp, LvClimbed=0, SavedBars를 채운다.
        /// 오브젝트 보상은 RewardType·RewardId·RewardAmount(zone·DiaCap 규칙 반영, goldMul 전)로, 필드 칸은 Bar·Row·Lane·Idx·Rew·Hp·Max·Pre로.
        /// </summary>
        void BuildLevel(RunState s, double baseZ, int level, BarRecord saved);

        /// <summary>fieldSpec(A, f)(§4.14.3). A는 0기반 area, f는 0기반 골목. finish의 floorSouls가 부른다.</summary>
        IReadOnlyList<FieldCell> FieldSpec(int area, int floor);
    }

    /// <summary>
    /// newRun(§4.14.5)의 1~5단계(6단계 BuildLevel 앞까지)만 하는 생성기. SIM은 이것으로 상태를 만들고
    /// 6단계 자리에서 <see cref="IRunRule.BuildCourse"/>를 부른다(04 §4.20.2 ②). 규칙 난수를 뽑지 않는다.
    /// </summary>
    public interface IRunStateFactory
    {
        RunState NewState(RunSetup setup, MvTables tables, IRng rng);
    }

    /// <summary>
    /// 런 쪽 컴포넌트 구현 등록(105 검증 「컴포넌트 구현 등록됨」). MV.Boot가
    /// <c>options.IsComponentImplemented = RunComponents.IsImplemented</c>로 연결한다(MV.Data는 MV.Run을 모른다).
    /// 새 type을 구현하면 여기 한 줄을 더한다.
    /// </summary>
    public static class RunComponents
    {
        static readonly Dictionary<string, HashSet<string>> Implemented = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal)
        {
            ["hittable"] = Set(nameof(Hittable.Always), nameof(Hittable.BeforePass), nameof(Hittable.Never)),
            ["hitReactionId"] = Set(nameof(HitReactionType.None), nameof(HitReactionType.Damage), nameof(HitReactionType.ChargeStairGate),
                nameof(HitReactionType.ChargeCraftGate), nameof(HitReactionType.ChargeWell)),
            ["receiveId"] = Set(nameof(ReceiveType.OnBreak), nameof(ReceiveType.OnPass), nameof(ReceiveType.OnFlush), nameof(ReceiveType.OnArrive)),
            ["moveAiId"] = Set(nameof(MoveAiType.Static), nameof(MoveAiType.Chase)),
            ["hpRuleId"] = Set(nameof(HpRuleType.None), nameof(HpRuleType.AreaHpMul), nameof(HpRuleType.FieldLerp), nameof(HpRuleType.Fixed)),
        };

        static HashSet<string> Set(params string[] v) => new HashSet<string>(v, StringComparer.Ordinal);

        public static bool IsImplemented(string column, string type) =>
            column != null && type != null && Implemented.TryGetValue(column, out var s) && s.Contains(type);
    }
}
