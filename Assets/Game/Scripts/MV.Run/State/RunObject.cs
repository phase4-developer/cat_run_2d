using MV.Data;

namespace MV.Run
{
    /// <summary>
    /// 런 오브젝트 1개(프로토 `R.objs`의 원소, 04 §4.3·§4.14). MV-RUN-GEN이 만들고 MV-RUN-SIM이 진행한다.
    /// 동작은 종류 이름이 아니라 <see cref="Kind"/>(105)와 그 행이 가리키는 컴포넌트 행(106~109·114)으로만 정한다.
    /// GEN 채움 규칙: <see cref="RunObjectFactory.Create"/>로 만들고(컴포넌트 캐시·hw/hd 채움) 위치·HP·보상만 넣는다.
    /// </summary>
    public sealed class RunObject
    {
        /// <summary>런 안에서 유일한 번호(생성 순서, 1부터). <see cref="RunState.AddObj"/>가 붙인다.</summary>
        public int Id;

        // ---- 종류와 컴포넌트(표 행 참조) ----
        public ObjectKindData Kind;
        /// <summary>Kind.enemyId가 가리키는 102 행. 없으면 null.</summary>
        public EnemyData Enemy;
        public HitReactionData Reaction;
        public ChargeGateData Charge;
        public ReceiveConditionData Receive;
        /// <summary>Kind.moveAiId, 없으면 Enemy.moveAiId의 109 행. 없으면 null.</summary>
        public MoveAiData Move;
        public HpRuleData HpRule;

        // ---- 위치·판정 ----
        public double X, Z;
        public double Hw, Hd;

        // ---- 체력 ----
        /// <summary>false면 체력 없음(무한, 우물·게이트·계단·아치).</summary>
        public bool HasHp;
        public double Hp;
        public double Max;

        // ---- 상태 ----
        public bool Dead;
        /// <summary>게이트 통과 처리 끝(`o.done`).</summary>
        public bool Done;
        /// <summary>계단 사용 끝(`o.used`).</summary>
        public bool Used;
        /// <summary>지난 런에 이미 부순 장애물(`o.pre`, 보상 없음).</summary>
        public bool Pre;
        /// <summary>buildLevel의 level(`o.lvl`).</summary>
        public int Lvl;

        // ---- 충전 게이트(107) ----
        public double F;
        /// <summary>sgate 모은 계단 수(`o.n`).</summary>
        public int N;
        /// <summary>cgate 완성 무기 수(`o.made`).</summary>
        public int Made;
        /// <summary>통과 후 가라앉기(그림, 0이면 안 함).</summary>
        public double Sink;

        // ---- 우물(ChargeWell) ----
        public double Pend;
        /// <summary>묶음 타이머(실시간).</summary>
        public double Ft;

        // ---- 이동 AI(109) ----
        public double Spd;

        // ---- 보상(GEN이 zone·규칙까지 풀어 둔 값) ----
        /// <summary>파괴 보상. None이면 없음. 602는 지급 때 × goldMul.</summary>
        public ConsumeType RewardType;
        public int RewardId;
        public double RewardAmount;
        /// <summary>파괴 시 추가 추첨 묶음(801_drop.dropGroup, 쥐 8% 캣닢). 0이면 없음.</summary>
        public int DropGroup;

        // ---- 골목 장애물 칸 ----
        /// <summary>골목 번호 0..2(`o.bar`). 필드 칸이 아니면 −1.</summary>
        public int Bar = -1;
        public int Row, Lane;
        /// <summary>fieldSpec idx(`o.idx`). 필드 칸이 아니면 −1.</summary>
        public int Idx = -1;
        /// <summary>칸 보상 종류(`o.rew`). 지급은 RewardType/RewardAmount로 한다.</summary>
        public FieldReward Rew;

        // ---- 표시용 ----
        /// <summary>아치 문구 키(`ui.run.floorN`·`ui.run.areaGate`)와 인자.</summary>
        public string LabelKey;
        public int LabelArg;

        // ---- 신 효과 ----
        public bool Burning;
        public double BurnT, BurnDps, BurnTick;
        /// <summary>프로토에 세우는 코드 없음(§4.16). 휴면.</summary>
        public double Chill;

        public bool IsField => Idx >= 0;

        public override string ToString() => $"#{Id} {Kind?.key} x={X} z={Z} hp={(HasHp ? Hp.ToString() : "inf")}{(Dead ? " dead" : "")}";
    }

    /// <summary>오브젝트를 종류 행에서 만든다(컴포넌트 캐시를 한 곳에서 채운다).</summary>
    public static class RunObjectFactory
    {
        public static RunObject Create(MvTables t, ObjectKindData kind, double x, double z)
        {
            var o = new RunObject { Kind = kind, X = x, Z = z, Hw = kind.hw, Hd = kind.hd };
            o.Enemy = kind.enemyId != 0 ? t.Enemy.Get(kind.enemyId) : null;
            o.Reaction = kind.hitReactionId != 0 ? t.HitReaction.Get(kind.hitReactionId) : null;
            o.Charge = kind.chargeGateId != 0 ? t.ChargeGate.Get(kind.chargeGateId) : null;
            o.Receive = kind.receiveId != 0 ? t.ReceiveCondition.Get(kind.receiveId) : null;
            int moveId = kind.moveAiId != 0 ? kind.moveAiId : (o.Enemy?.moveAiId ?? 0);
            o.Move = moveId != 0 ? t.MoveAi.Get(moveId) : null;
            int hpId = o.Enemy != null && o.Enemy.hpRuleId != 0 ? o.Enemy.hpRuleId : kind.hpRuleId;
            o.HpRule = hpId != 0 ? t.HpRule.Get(hpId) : null;
            if (o.Enemy != null)
            {
                if (o.Enemy.hw.HasValue) o.Hw = o.Enemy.hw.Value;
                if (o.Enemy.hd.HasValue) o.Hd = o.Enemy.hd.Value;
                o.DropGroup = o.Enemy.dropId;
            }
            return o;
        }
    }
}
