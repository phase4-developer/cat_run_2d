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

        static double Perk(RunState s, string key, int lv)
        {
            var p = s.Tables.RebirthPerk.ByKey(key);
            return p.@base + p.perLv * lv;
        }

        static double Aup(RunState s, string key, int lv) => s.Tables.AreaUpgrade.ByKey(key).perLv * lv;

        /// <summary>무기 등급 피해 `wDmg(t)` = 103_item.dmg.</summary>
        public static double WeaponDmg(MvTables t, int tier)
        {
            foreach (var it in t.Item.Datas) if (it.tier == tier) return it.dmg;
            return 0;
        }

        /// <summary>baseDmg() = wDmg(eqTier) × Π(1+0.2·atk) × (1+0.15·power) × (1+Dmg/100).</summary>
        public static double BaseDmg(RunState s) =>
            WeaponDmg(s.Tables, s.EqTier()) * CardMul(s, CardEffectType.DmgMul) * Perk(s, "power", s.Setup.PerkPower) * (1 + s.Setup.Modifiers.Dmg / 100);

        /// <summary>critMul() = 2.2 + 0.2·perk.crit + CritDmg/100.</summary>
        public static double CritMul(RunState s) => Perk(s, "crit", s.Setup.PerkCrit) + s.Setup.Modifiers.CritDmg / 100;

        /// <summary>obstMul() = max(0.5, 1 − 0.05·obst).</summary>
        public static double ObstMul(MvTables t, int obst)
        {
            var p = t.RebirthPerk.ByKey("obst");
            return Math.Max(p.floor ?? double.NegativeInfinity, p.@base + p.perLv * obst);
        }

        /// <summary>diaCap() = 10 + 2·dcap.</summary>
        public static double DiaCap(MvTables t, int dcap)
        {
            var p = t.RebirthPerk.ByKey("dcap");
            return p.@base + p.perLv * dcap;
        }

        /// <summary>goldMul() = (1 + 0.05·income) × Π(1+0.5·gold) × (1 + Gold/100).</summary>
        public static double GoldMul(RunState s) =>
            (1 + s.Tables.RunConfig.D(RunConfigKeys.INCOME_PER_LV) * s.Setup.UpIncome) * CardMul(s, CardEffectType.GoldMul) * (1 + s.Setup.Modifiers.Gold / 100);

        /// <summary>발사 간격 iv(§4.2.2).</summary>
        public static double FireInterval(RunState s)
        {
            double rate = s.Setup.RateOverride ?? s.Bal.rate;
            return 1 / (rate * CardMul(s, CardEffectType.FireRateMul) * (1 + Aup(s, "rate", s.Setup.AupRate)) * (1 + s.Setup.Modifiers.FireRate / 100));
        }

        /// <summary>치명 확률 crC(상한 없음).</summary>
        public static double CritChance(RunState s) =>
            CardSum(s, CardEffectType.CritChance) + Aup(s, "crit", s.Setup.AupCrit) + s.Setup.Modifiers.CritChance / 100;

        /// <summary>2연발 확률 dbC = min(1, …).</summary>
        public static double DoubleChance(RunState s) =>
            Math.Min(1, CardSum(s, CardEffectType.DoubleChance) + Aup(s, "dbl", s.Setup.AupDbl) + s.Setup.Modifiers.DoubleChance / 100);

        /// <summary>사거리 RANGE = 22 × (1 + 0.1·aup.range) × (1 + Range/100).</summary>
        public static double ProjRange(RunState s) =>
            s.Tables.RunConfig.D(RunConfigKeys.PROJ_RANGE) * (1 + Aup(s, "range", s.Setup.AupRange)) * (1 + s.Setup.Modifiers.Range / 100);

        // ---------------- 충전 게이트(107) ----------------

        /// <summary>
        /// 문턱 1회 값. k = 성장 단계(Pow: lv−1, Add: made, None: 무시). 순서: 성장 → scale → Area 체력 → gauge → mul.
        /// sgate `GATE_MAX[0]·scale·(hp/100)·2`, cgate `CRAFT_T(k)·scale·(hp/100)`, well `150·1.6^(lv−1)·(hp/100)·gauge`와 같은 순서.
        /// </summary>
        public static double Threshold(ChargeGateData g, int k, BalancePresetData bal, double zoneHp)
        {
            double v = g.growth == Growth.Add ? g.@base + g.step * k : g.growth == Growth.Pow ? g.@base * Math.Pow(g.step, k) : g.@base;
            if (g.useScale) v *= bal.scale;
            if (g.useAreaHp) v *= zoneHp / 100;
            if (g.useGauge) v *= bal.gauge;
            if (g.mul != 1) v *= g.mul;
            return v;
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

        /// <summary>칸 수 k(§4.8.1): n ≤ 60 → n, 아니면 min(150, 60 + ceil((n−60)/3)).</summary>
        public static int ClimbK(ClimbData c, int n)
        {
            int direct = (int)c["kDirectMax"];
            if (n <= direct) return n;
            return (int)Math.Min(c["kMax"], direct + Math.Ceiling((n - direct) / c["kCompress"]));
        }

        /// <summary>오르는 시간 T = min(9, 1.6 + 0.05k).</summary>
        public static double ClimbT(ClimbData c, int k) => Math.Min(c["tMax"], c["tBase"] + k * c["tPerStep"]);

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
