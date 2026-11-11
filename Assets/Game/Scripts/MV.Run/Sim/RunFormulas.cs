using System;
using System.Collections.Generic;
using MV.Data;

namespace MV.Run
{
    /// <summary>
    /// 공통 스탯 함수(04 §4.0.2, §4.2, §4.4, §4.5, §4.8, §4.11). 모두 표 행에서 값을 읽는다.
    /// 곱·합 순서는 정본 식 그대로 둔다(double 비트 일치, 04 §4.20.1).
    /// </summary>
    public static class RunFormulas
    {
        /// <summary>JS Math.round(.5에서 +∞, 음수 없음 전제). 04 §4.0.2.</summary>
        public static double JsRound(double x) => Math.Floor(x + 0.5);

        public static double Clamp(double v, double a, double b) => v < a ? a : (v > b ? b : v);

        // ---------------- 카드 합산 ----------------

        /// <summary>효과 type의 곱 Π(1 + p1·n), 풀 순서대로(정본 식의 카드 순서).</summary>
        public static double CardMul(RunState s, CardEffectType type)
        {
            double m = 1;
            foreach (var c in s.CardPool)
            {
                var e = s.Tables.CardEffect.Get(c.effectId);
                if (e == null || e.type != type) continue;
                m *= 1 + e.p1 * s.CardN(c.id);
            }
            return m;
        }

        /// <summary>효과 type의 합 Σ p1·n(잔재 카드 포함, 얻은 카드만).</summary>
        public static double CardSum(RunState s, CardEffectType type)
        {
            double sum = 0;
            foreach (var id in s.CardOrder)
            {
                var c = s.Tables.Card.Get(id);
                var e = c != null ? s.Tables.CardEffect.Get(c.effectId) : null;
                if (e == null || e.type != type) continue;
                sum += e.p1 * s.CardN(id);
            }
            return sum;
        }

        /// <summary>효과 type을 가진 얻은 카드(첫 행)와 장수. 없으면 (null, 0).</summary>
        /// <summary>그 효과 종류의 112 행: 손에 든 카드의 효과, 없으면 표의 첫 행(id 순). 표에도 없으면 null.</summary>
        public static CardEffectData EffectRow(RunState s, CardEffectType type)
        {
            var (e, _) = CardOf(s, type);
            if (e != null) return e;
            foreach (var row in s.Tables.CardEffect.Datas) if (row.type == type) return row;
            return null;
        }

        public static (CardEffectData eff, int n) CardOf(RunState s, CardEffectType type)
        {
            foreach (var id in s.CardOrder)
            {
                var c = s.Tables.Card.Get(id);
                var e = c != null ? s.Tables.CardEffect.Get(c.effectId) : null;
                if (e != null && e.type == type) return (e, s.CardN(id));
            }
            return (null, 0);
        }

        // ---------------- 스탯 ----------------

        /// <summary>냥환생 영구 업그레이드 행(14_rebirthPerk, statKey로 찾음 — 항목 이름을 코드에 두지 않는다, MV-SPEC-02 Q-27).</summary>
        public static RebirthPerkData PerkRow(MvTables t, RebirthStatKey stat)
        {
            foreach (var p in t.RebirthPerk.Datas) if (p.statKey == stat) return p;
            return null;
        }

        /// <summary>Area 강화 행(13_areaUpgrade, statKey로 찾음).</summary>
        public static AreaUpgradeData AupRow(MvTables t, AreaStatKey stat)
        {
            foreach (var a in t.AreaUpgrade.Datas) if (a.statKey == stat) return a;
            return null;
        }

        /// <summary>RunSetup의 냥환생 Lv(statKey별 칸).</summary>
        public static int PerkLv(RunSetup st, RebirthStatKey stat)
        {
            switch (stat)
            {
                case RebirthStatKey.DmgMul: return st.PerkPower;
                case RebirthStatKey.CritMul: return st.PerkCrit;
                case RebirthStatKey.ObstMul: return st.PerkObst;
                case RebirthStatKey.DiaCap: return st.PerkDcap;
                case RebirthStatKey.CraftDouble: return st.PerkCraftDbl;
                case RebirthStatKey.StairDouble: return st.PerkStairDbl;
                case RebirthStatKey.StartGold: return st.PerkStartGold;
                case RebirthStatKey.StartDia: return st.PerkStartDia;
                default: return 0; // GoldCap: 효과 뜻 [미확인](MV-SPEC-02 575) — 런 규칙 미반영
            }
        }

        /// <summary>RunSetup의 Area 강화 Lv(statKey별 칸).</summary>
        public static int AupLv(RunSetup st, AreaStatKey stat)
        {
            switch (stat)
            {
                case AreaStatKey.FireRate: return st.AupRate;
                case AreaStatKey.Range: return st.AupRange;
                case AreaStatKey.Clone: return st.AupClone;
                case AreaStatKey.CritChance: return st.AupCrit;
                case AreaStatKey.DoubleChance: return st.AupDbl;
                case AreaStatKey.PierceChance: return st.AupPierce;
                case AreaStatKey.DiaPerLevel: return st.AupDiaLv;
                case AreaStatKey.GoldPerLevel: return st.AupGoldLv;
                default: return 0;
            }
        }

        /// <summary>냥환생 값 = max(floor, base + perLv·lv). 행이 없으면 <paramref name="none"/>.</summary>
        public static double PerkValue(MvTables t, RebirthStatKey stat, int lv, double none = 0)
        {
            var p = PerkRow(t, stat);
            return p != null ? p.ValueAt(lv) : none;
        }

        static double Perk(RunState s, RebirthStatKey stat, double none) => PerkValue(s.Tables, stat, PerkLv(s.Setup, stat), none);

        /// <summary>Area 강화 값 = perLv·lv. 행이 없으면 0.</summary>
        static double Aup(RunState s, AreaStatKey stat)
        {
            var a = AupRow(s.Tables, stat);
            return a != null ? a.perLv * AupLv(s.Setup, stat) : 0;
        }

        /// <summary>무기 등급 피해 `wDmg(t)` = 103_item.dmg.</summary>
        public static double WeaponDmg(MvTables t, int tier)
        {
            foreach (var it in t.Item.Datas) if (it.tier == tier) return it.dmg;
            return 0;
        }

        /// <summary>지팡이 최고 단계 = 켜진 103_item 행의 tier 최대(MV-SPEC-02 Q-08: 코드에 단계 수를 두지 않는다). 행이 없으면 1.</summary>
        public static int WeaponTierMax(MvTables t)
        {
            return t != null ? t.WeaponTierMax() : 1;
        }

        /// <summary>분신 상한 = min(MAX_CLONES, 314_spawnGroup clone 위치 수)(MV-SPEC-02 Q-09). 위치가 없는 분신은 만들지 않는다.</summary>
        public static int CloneCap(MvTables t)
        {
            int offs = 0;
            foreach (var g in t.SpawnGroup.Sorted) if (g.key == "clone") offs++;
            return Math.Max(0, Math.Min(t.RunConfig.I(RunConfigKeys.MAX_CLONES), offs));
        }

        /// <summary>공격력 % 합 = 카드(DmgMul p1·n) + 냥환생 공격력(perLv·lv), 둘 다 %로 바꿔 모디파이어 Dmg와 합산(MV-SPEC-02 4-2, Q-12).</summary>
        public static double AtkPct(RunState s)
        {
            var p = PerkRow(s.Tables, RebirthStatKey.DmgMul);
            double perk = p != null ? p.perLv * PerkLv(s.Setup, RebirthStatKey.DmgMul) : 0;
            return (CardSum(s, CardEffectType.DmgMul) + perk) * 100 + s.Setup.Modifiers.Dmg;
        }

        /// <summary>baseDmg() = 지팡이 단계 기본 피해 × (1 + 공격력%/100)(MV-SPEC-02 4-2, Q-12). 치명 배율은 호출 쪽에서 곱한다.
        /// 내부 공격력은 정수: 소수점은 버린다(MV-SPEC2-FIX1, 사용자 결정). 지팡이 피해는 장착 단계의 103_item.dmg 그대로.</summary>
        public static double BaseDmg(RunState s) => Math.Floor(WeaponDmg(s.Tables, s.EqTier()) * (1 + AtkPct(s) / 100));

        /// <summary>critMul() = 14_rebirthPerk[CritMul] 값 + CritDmg/100.</summary>
        public static double CritMul(RunState s) => Perk(s, RebirthStatKey.CritMul, 1) + s.Setup.Modifiers.CritDmg / 100;

        /// <summary>obstMul() = max(floor, base + perLv·obst).</summary>
        public static double ObstMul(MvTables t, int obst) => PerkValue(t, RebirthStatKey.ObstMul, obst, 1);

        /// <summary>diaCap() = base + perLv·dcap.</summary>
        public static double DiaCap(MvTables t, int dcap) => PerkValue(t, RebirthStatKey.DiaCap, dcap);

        /// <summary>계단 게이트 충전 2배 확률(냥환생 「계단 2배」, MV-SPEC-02 6-2d·Q-27).</summary>
        public static double StairDoubleChance(RunState s) => Perk(s, RebirthStatKey.StairDouble, 0);

        /// <summary>공방 게이트 카드 2장 생산 확률(냥환생 「지팡이 제작 2배」, MV-SPEC-02 Q-27).</summary>
        public static double CraftDoubleChance(RunState s) => Perk(s, RebirthStatKey.CraftDouble, 0);

        /// <summary>
        /// 런 시작 골드 = 302_area.startGold(Area 2부터 150, MV-SPEC-02 513) + 냥환생 「시작 골드」 값(14 StartGold, 576). 모두 표 값.
        /// </summary>
        public static double StartGold(MvTables t, RunSetup st)
        {
            double area = 0;
            foreach (var a in t.Area.Datas) if (a.areaNo == st.Area + 1) area = a.startGold;
            return area + PerkValue(t, RebirthStatKey.StartGold, PerkLv(st, RebirthStatKey.StartGold));
        }

        /// <summary>런 시작 블루 마법석 = 냥환생 「시작 블루 마법석」 값(14 StartDia, MV-SPEC-02 577).</summary>
        public static int StartDia(MvTables t, RunSetup st) =>
            (int)Math.Floor(PerkValue(t, RebirthStatKey.StartDia, PerkLv(st, RebirthStatKey.StartDia)));

        /// <summary>
        /// 런 종료 「레벨당 획득」(13 DiaPerLevel·GoldPerLevel, MV-SPEC-02 529~530) = 강화 값(perLv × Lv) × 런 Level.
        /// 행이 없거나 Lv 0이면 0.
        /// </summary>
        public static double PerLevelReward(MvTables t, RunSetup st, AreaStatKey stat, int level)
        {
            var a = AupRow(t, stat);
            return a != null ? a.perLv * AupLv(st, stat) * Math.Max(0, level) : 0;
        }

        /// <summary>
        /// 보상 k번째 몫(1..n, MV-RUN-SPLITREWARD): floor(총량/n), 마지막 몫은 총량 − 앞 몫 합(나머지를 마지막에 더해 합이 총량과 같다).
        /// </summary>
        public static double RewardShare(double total, int n, int k)
        {
            if (n <= 1) return total;
            double per = Math.Floor(total / n);
            return k >= n ? total - per * (n - 1) : per;
        }

        /// <summary>누적 피해 dealt가 체력 max의 k/n 문턱을 몇 개 넘었나(0..n). max ≤ 0이면 0(파괴 때만 지급).</summary>
        public static int SharesDue(double dealt, double max, int n)
        {
            if (max <= 0 || n < 1 || dealt <= 0) return 0;
            if (dealt >= max) return n;
            return Math.Min(n, (int)Math.Floor(dealt * n / max));
        }

        /// <summary>탄 관통 +1 확률(Area 강화 「관통 확률」, MV-SPEC-02 Q-28).</summary>
        public static double PierceChance(RunState s) => Aup(s, AreaStatKey.PierceChance);

        /// <summary>goldMul() = (1 + 0.05·income) × Π(1+0.5·gold) × (1 + Gold/100).</summary>
        public static double GoldMul(RunState s) =>
            (1 + s.Tables.RunConfig.D(RunConfigKeys.INCOME_PER_LV) * s.Setup.UpIncome) * CardMul(s, CardEffectType.GoldMul) * (1 + s.Setup.Modifiers.Gold / 100);

        /// <summary>발사 간격 iv(§4.2.2).</summary>
        public static double FireInterval(RunState s)
        {
            double rate = s.Setup.RateOverride ?? s.Bal.rate;
            return 1 / (rate * CardMul(s, CardEffectType.FireRateMul) * (1 + Aup(s, AreaStatKey.FireRate)) * (1 + s.Setup.Modifiers.FireRate / 100));
        }

        /// <summary>치명 확률 crC(상한 없음).</summary>
        public static double CritChance(RunState s) =>
            CardSum(s, CardEffectType.CritChance) + Aup(s, AreaStatKey.CritChance) + s.Setup.Modifiers.CritChance / 100;

        /// <summary>2연발 확률 dbC = min(1, …).</summary>
        public static double DoubleChance(RunState s) =>
            Math.Min(1, CardSum(s, CardEffectType.DoubleChance) + Aup(s, AreaStatKey.DoubleChance) + s.Setup.Modifiers.DoubleChance / 100);

        /// <summary>사거리 RANGE = 22 × (1 + 0.1·aup.range) × (1 + Range/100).</summary>
        public static double ProjRange(RunState s) =>
            s.Tables.RunConfig.D(RunConfigKeys.PROJ_RANGE) * (1 + Aup(s, AreaStatKey.Range)) * (1 + s.Setup.Modifiers.Range / 100);

        // ---------------- 충전 게이트(107) ----------------

        /// <summary>
        /// 문턱 1회 값. k = 성장 단계(Pow: lv−1, Add: made, None: 무시). 순서: 성장 → scale → Area 체력 → gauge → mul.
        /// sgate `GATE_MAX[0]·scale·(hp/100)·2`, cgate `CRAFT_T(k)·scale·(hp/100)`, well `150·1.6^(lv−1)·(hp/100)·gauge`와 같은 순서.
        /// </summary>
        public static double Threshold(ChargeGateData g, int k, BalancePresetData bal, double zoneHp)
        {
            double v = g.growth == Growth.Add ? g.@base + g.step * k : g.growth == Growth.Pow ? g.@base * Math.Pow(g.step, k) : g.@base;
            return ApplyGateFlags(g, v, bal, zoneHp);
        }

        /// <summary>문턱 기본값에 107 행 플래그를 순서대로 곱한다: scale → Area 체력 → gauge → mul.</summary>
        public static double ApplyGateFlags(ChargeGateData g, double v, BalancePresetData bal, double zoneHp)
        {
            if (g.useScale) v *= bal.scale;
            if (g.useAreaHp) v *= zoneHp / 100;
            if (g.useGauge) v *= bal.gauge;
            if (g.mul != 1) v *= g.mul;
            return v;
        }

        /// <summary>
        /// 계단 게이트 1회 충전 필요 피해 M(MV-SPEC-02 6-2e, Q-06·Q-19): 기본값 = <c>302_area.sgateM</c>[계단 Lv − 1]
        /// (Lv이 칸 수를 넘으면 마지막 칸 — Lv 상한은 표 칸 수), 칸이 비면 107 행 base. 그 뒤 107 행 플래그를 곱한다.
        /// </summary>
        public static double StairGateM(ChargeGateData g, IReadOnlyList<double> sgateM, int stairLv, BalancePresetData bal, double zoneHp)
        {
            double v = sgateM != null && sgateM.Count > 0 ? sgateM[Math.Min(sgateM.Count, Math.Max(1, stairLv)) - 1] : g.@base;
            return ApplyGateFlags(g, v, bal, zoneHp);
        }

        /// <summary>게이지 행(107 gateType Well 첫 행).</summary>
        public static ChargeGateData GaugeRow(MvTables t)
        {
            foreach (var g in t.ChargeGate.Datas) if (g.gateType == GateType.Well) return g;
            return null;
        }

        /// <summary>gaugeNeed(lv).</summary>
        public static double GaugeNeed(RunState s, int lv) => Threshold(GaugeRow(s.Tables), lv - 1, s.Bal, s.ZoneHp);

        // ---------------- 계단 ----------------

        /// <summary>
        /// 표시 칸 수 K(MV-SPEC-02 6-5d, Q-07): N ≤ cmpFrom → N, 아니면 cmpFrom + ceil((N − cmpFrom) / C).
        /// C = <c>cmpBandC</c>[i], i = <c>cmpBandFrom</c>[i] ≤ N인 마지막 칸(첫 칸보다 작으면 첫 칸). 상수는 모두 330_climb 설정 행.
        /// </summary>
        public static int ClimbK(ClimbData c, int n)
        {
            int from = (int)c["cmpFrom"];
            if (n <= from) return n;
            return from + (int)Math.Ceiling((n - from) / (double)CompressC(c, n));
        }

        /// <summary>압축 구간 C(N).</summary>
        public static int CompressC(ClimbData c, int n)
        {
            var bf = c.cmpBandFrom; var bc = c.cmpBandC;
            int count = Math.Min(bf?.Count ?? 0, bc?.Count ?? 0);
            if (count == 0) return 1;
            int pick = 0;
            for (int i = 0; i < count; i++) if (n >= bf[i]) pick = i;
            return Math.Max(1, bc[pick]);
        }

        /// <summary>오르는 시간 T = min(riseCap, riseBase + risePerStep·(K − 1))(MV-SPEC-02 6-5e, 약 1.5초 안).</summary>
        public static double ClimbT(ClimbData c, int k) => Math.Min(c["riseCap"], c["riseBase"] + c["risePerStep"] * Math.Max(0, k - 1));

        /// <summary>칸 생성 간격 = max(0, (T − riseLead) / max(1, K − 1))(MV-SPEC-02 6-5e). 0이면 모든 칸이 바로 보인다.</summary>
        public static double ClimbStepEvery(ClimbData c, int k) => Math.Max(0, (ClimbT(c, k) - c["riseLead"]) / Math.Max(1, k - 1));

        /// <summary>남은 칸 rem의 카드 등급(115 need 이하 최고), 없으면 −1.</summary>
        public static int TierFor(MvTables t, int rem)
        {
            int best = -1;
            foreach (var row in t.CardTier.Datas) if (rem >= row.need && row.tierIndex > best) best = row.tierIndex;
            return best;
        }

        /// <summary>계단 n칸의 카드 등급 목록(§4.8.2 결과 예).</summary>
        public static List<int> ClimbTiers(MvTables t, int n)
        {
            var c = t.Climb.Config();
            int lap = (int)c["lap"], max = (int)c["maxCards"], lapTier = (int)c["lapTier"];
            var tiers = new List<int>();
            int laps = 0;
            while (laps < max - 1 && n >= (laps + 1) * lap) { laps++; tiers.Add(lapTier); }
            int tier = TierFor(t, Math.Min(lap, n - laps * lap));
            if (tier >= 0) tiers.Add(tier);
            return tiers;
        }

        // ---------------- 종료 ----------------

        /// <summary>목숨 조각 tok(§4.11.1).</summary>
        public static int Tokens(MvTables t, int runSouls, bool gate, bool open)
        {
            var rc = t.RunConfig;
            return rc.I(RunConfigKeys.TOK_BASE) + (int)Math.Floor(runSouls / rc.D(RunConfigKeys.TOK_SOUL_DIV))
                + (gate ? rc.I(RunConfigKeys.TOK_GATE) : 0) + (open ? rc.I(RunConfigKeys.TOK_OPEN) : 0);
        }
    }
}
