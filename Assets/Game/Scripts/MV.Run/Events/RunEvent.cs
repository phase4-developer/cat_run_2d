using System.Globalization;
using System.Text;

namespace MV.Run
{
    /// <summary>
    /// 런 이벤트 종류(04 §4.18.1, §4.20.5). 구독자: MV.Fx·MV.Sound·MV.UI·MV.View. MV.Run은 이펙트·소리를 직접 부르지 않는다.
    /// </summary>
    public enum RunEventType
    {
        RunStarted, FloorEnter, Fire, ProjImpact, ProjRemoved, Hit, Break, Gain, Hitstop, Shake,
        GateFill, GateTake, GateMiss, StairsReceived, WeaponDeliver, WeaponHandReset, WellFlush, GaugeReceive, GaugeLevelUp,
        Steal, Blocked, GodProc, GodChain, BurnTick, GodStormStart, GodStormStrike, GodBlast, OrbTick, Beam, MeteorLaunch, MeteorImpact,
        ClimbStart, ClimbStepShown, ClimbLap, ClimbTierUp, ClimbWarpStage, ClimbEnd,
        RouletteStart, RouletteFlip, RouletteChosen, RouletteFly, CardApplied, RouletteEnd,
        Toast, Banner, RunFinished, CompanionShot,
        /// <summary>지팡이 스택이 가득 차 공방 지급이 거절됨(MV-SPEC-02 6-3, Q-17). A=거절 장 수, B=tier.</summary>
        WeaponRejected,
    }

    /// <summary>
    /// 런 이벤트 1개. 종류별 필드 뜻(04 §4.18.1 이름 → 이 구조체 칸):
    /// <list type="bullet">
    /// <item>RunStarted: A=area, B=level, C=tpl</item>
    /// <item>FloorEnter: A=floor(1~3), Flag=prevSoulsOk</item>
    /// <item>Fire: A=unit, ObjId=projId, X,Z, B=tier, C=pairIndex(−1/0/1), Flag=spear, Key=el</item>
    /// <item>ProjImpact: ObjId=projId, X,Z, Flag=crit / ProjRemoved: ObjId=projId</item>
    /// <item>Hit: ObjId, KindId, X, Z(=z−hd), Amount=dmg(int), Flag=crit, Color, Quiet</item>
    /// <item>Break: ObjId, KindId, X, Z, A=(int)FieldReward 또는 −1(None)</item>
    /// <item>Gain: A=(int)ConsumeType, Amount=표시값, X, Z</item>
    /// <item>Hitstop: Amount=sec / Shake: Amount=amp, Amount2=sec</item>
    /// <item>GateFill: ObjId, KindId, A=got, Amount=progress(0~1) / GateTake·GateMiss: ObjId, KindId, A=amount</item>
    /// <item>StairsReceived: A=n, B=total / WeaponDeliver: A=n, B=tier / WeaponHandReset: Ids=w[무기 상한 등급+1]</item>
    /// <item>WellFlush: ObjId, Amount=amount, A=bricks / GaugeReceive: Amount, A=lv, Amount2=fill / GaugeLevelUp: A=lv</item>
    /// <item>Steal: ObjId, A=amount, X, Z / Blocked: ObjId, X, Z</item>
    /// <item>GodProc: ObjId=target, Key=el / GodChain: Key=el, X, Z, Amount=radius, Ids=대상 / BurnTick: ObjId</item>
    /// <item>GodStormStart·GodStormStrike·GodBlast: X, Z, Key=el, Amount=life(Start)</item>
    /// <item>OrbTick: Ids / Beam·MeteorLaunch·MeteorImpact: X, Z</item>
    /// <item>ClimbStart: A=n, B=k, Amount=pw, X=z0, Z=zEnd, Amount2=dH, Amount3=T / ClimbStepShown: A=i</item>
    /// <item>ClimbLap: A=lap, B=stepIndex / ClimbTierUp: A=tier, B=stepIndex / ClimbWarpStage: A=stage / ClimbEnd: A=n, B=tier</item>
    /// <item>RouletteStart: Ids=tiers, Ids2=카드 id / RouletteFlip: A=i / RouletteChosen: A=i, B=cardId, C=tier</item>
    /// <item>RouletteFly: A=i, B=cardId / CardApplied: B=cardId, A=count / RouletteEnd</item>
    /// <item>Toast·Banner: Key, A=인자 / RunFinished: (결과는 RunState.Result)</item>
    /// <item>CompanionShot: A=sourceId, X,Z=from, Ids=targets, Amount=dmgEach</item>
    /// </list>
    /// </summary>
    public readonly struct RunEvent
    {
        public readonly RunEventType Type;
        public readonly int ObjId;
        public readonly int KindId;
        public readonly int A, B, C;
        public readonly double X, Z;
        public readonly double Amount, Amount2, Amount3;
        public readonly bool Flag;
        public readonly bool Quiet;
        public readonly HitColor Color;
        public readonly string Key;
        public readonly int[] Ids;
        public readonly int[] Ids2;

        public RunEvent(RunEventType type, int objId = 0, int kindId = 0, int a = 0, int b = 0, int c = 0,
            double x = 0, double z = 0, double amount = 0, double amount2 = 0, double amount3 = 0,
            bool flag = false, bool quiet = false, HitColor color = HitColor.Normal, string key = null, int[] ids = null, int[] ids2 = null)
        {
            Type = type; ObjId = objId; KindId = kindId; A = a; B = b; C = c; X = x; Z = z;
            Amount = amount; Amount2 = amount2; Amount3 = amount3; Flag = flag; Quiet = quiet; Color = color; Key = key; Ids = ids; Ids2 = ids2;
        }

        /// <summary>바이트 비교용 고정 표기(04 §4.17.2-6).</summary>
        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.Append(Type).Append('|').Append(ObjId).Append('|').Append(KindId).Append('|').Append(A).Append('|').Append(B).Append('|').Append(C)
              .Append('|').Append(R(X)).Append('|').Append(R(Z)).Append('|').Append(R(Amount)).Append('|').Append(R(Amount2)).Append('|').Append(R(Amount3))
              .Append('|').Append(Flag ? 1 : 0).Append(Quiet ? 1 : 0).Append('|').Append(Color).Append('|').Append(Key);
            Arr(sb, Ids); Arr(sb, Ids2);
            return sb.ToString();
        }

        static string R(double v) => v.ToString("R", CultureInfo.InvariantCulture);

        static void Arr(StringBuilder sb, int[] a)
        {
            sb.Append('|');
            if (a == null) return;
            for (int i = 0; i < a.Length; i++) { if (i > 0) sb.Append(','); sb.Append(a[i]); }
        }
    }
}
