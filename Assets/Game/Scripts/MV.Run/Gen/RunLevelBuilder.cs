using System;
using System.Collections.Generic;
using MV.Data;

namespace MV.Run
{
    /// <summary>
    /// 정본 레벨 생성(`buildLevel`·`newRun`, 04 §4.14.4~4.14.5)의 <see cref="IRunLevelBuilder"/> 구현. 표만 보고 채운다.
    /// <see cref="FieldSpec"/>은 마지막 <see cref="NewRun"/>의 표·프리셋·부활 특성을 쓴다(런마다 하나씩 쓰는 것을 전제로 한다).
    /// </summary>
    public sealed class RunLevelBuilder : IRunLevelBuilder, IRunStateFactory
    {
        /// <summary>313_fieldGen 행 key(모드는 402_modeRule.fieldGenKey로 바꾼다).</summary>
        public string FieldGenKey = "default";

        RunState _state;

        // ---------------- newRun (§4.14.5) ----------------

        public RunState NewRun(RunSetup setup, MvTables tables, IRng rng)
        {
            var s = NewState(setup, tables, rng);
            BuildLevel(s, 0, setup.Level, setup.KeepBars ? setup.Bars : null);
            return s;
        }

        /// <summary>newRun 1~5단계(BuildLevel 앞까지). 6단계는 SIM이 BuildCourse 훅으로 부른다(04 §4.20.2).</summary>
        public RunState NewState(RunSetup setup, MvTables tables, IRng rng)
        {
            if (setup == null) throw new ArgumentNullException(nameof(setup));
            if (tables == null) throw new ArgumentNullException(nameof(tables));
            if (rng == null) throw new ArgumentNullException(nameof(rng));

            var s = new RunState(setup, tables, rng);
            var t = tables;
            s.Bal = Preset(t, setup.Preset);
            s.Area = setup.Area;
            // 정본 보상의 zone은 save.zone(스냅숏). fieldSpec의 zi는 FieldGen이 zoneOf(A)로 따로 구한다.
            s.ZoneIndex = setup.Zone;
            s.ZoneHp = setup.ZoneHp ?? LevelGen.AreaRow(t, setup.Area).hp;
            s.StartLevel = setup.Level;

            // 신·속성·카드 풀(setGodCards)
            s.God = t.God.ByKey(setup.God) ?? t.God.ByKey("thunder");
            s.El = s.God?.element;
            BuildCardPool(s);

            // 공방 지급 등급·시작 덱
            if (setup.FixedWeaponTier >= 1 && setup.FixedWeaponTier <= 8)
            {
                s.Bt = setup.FixedWeaponTier;
                Array.Clear(s.W, 0, s.W.Length);
                s.W[setup.FixedWeaponTier] = 1;
            }
            else
            {
                s.Bt = BaseTier(t, setup.UpWeapon);
                if (setup.Deck != null) Array.Copy(setup.Deck, s.W, Math.Min(setup.Deck.Length, s.W.Length));
            }

            // 게이지 시작값 kegStart
            var (lv, step, need) = KegStart(t, setup.UpStair);
            var well = WellRow(t);
            s.GaugeLv = lv;
            int gaugeMax = well != null && well.cap > 0 ? well.cap : int.MaxValue;
            s.GaugeFill = lv < gaugeMax && need > 0 ? (step / (double)need) * GaugeNeed(s, well, lv) : 0;

            s.Clones = Math.Min(t.RunConfig.I(RunConfigKeys.MAX_CLONES), setup.AupClone);
            var phase = t.RunConfig.List(RunConfigKeys.FIRE_PHASE);
            for (int i = 0; i < s.FireT.Length; i++) s.FireT[i] = i < phase.Count ? phase[i] : 0;
            s.P.X = s.P.Z = s.P.Tx = 0;

            _state = s;
            return s;
        }

        // ---------------- buildLevel (§4.14.4) ----------------

        public void BuildLevel(RunState s, double baseZ, int level, BarRecord saved)
        {
            if (s == null) throw new ArgumentNullException(nameof(s));
            var t = s.Tables;
            var g = FieldGenRow(t);
            _state = s;

            int cp = s.Setup.UseCheckpoint ? Math.Min(t.RunConfig.I(RunConfigKeys.CP_MAX), Math.Max(0, s.Setup.Cp)) : 0;
            int tplId = LevelGen.TplFor(t, level);
            var tpl = t.Template.Get(tplId) ?? throw new InvalidOperationException($"310_template: {tplId} 행이 없습니다");

            // ① 템플릿
            var objs = new List<RunObject>(160);
            LevelGen.MakeObjs(s, tplId, baseZ, objs);

            // ② 보너스 상자 줄
            double z = baseZ + tpl.endZ;
            var coinKind = t.ObjectKind.Get(g.bonusCoinKindId);
            var gemKind = t.ObjectKind.Get(g.bonusGemKindId);
            for (int r = 0; r < g.bonusRows; r++)
                for (int li = 0; li < g.bonusLaneX.Count; li++)
                {
                    bool gem = r == g.bonusGemRow && li == g.bonusGemLane;
                    objs.Add(LevelGen.Spawn(s, gem ? gemKind : coinKind, g.bonusLaneX[li], z + g.bonusZ0 + r * g.bonusRowStep));
                }
            z += g.bonusToArch;

            // ③ 골목 아치·필드
            s.Gates.Clear();
            var archKind = t.ObjectKind.Get(g.archKindId);
            var wallKind = t.ObjectKind.Get(g.wallKindId);
            var wallRule = LevelGen.HpRuleOf(t, wallKind);
            double obst = wallRule != null && wallRule.useObstMul ? LevelGen.ObstMul(t, s.Setup.PerkObst) : 1;
            int floors = t.RunConfig.I(RunConfigKeys.FLOOR_COUNT);
            for (int f = cp; f < floors; f++)
            {
                var arch = LevelGen.Spawn(s, archKind, 0, z);
                arch.LabelKey = archKind.labelKey;
                arch.LabelArg = f + 1;
                objs.Add(arch);
                s.Gates.Add(new RunGateMark { Z = z, Floor = f + 1 });

                double z0 = z + g.archToField, zEnd = z0;
                foreach (var c in FieldSpecFor(s, f))
                {
                    double mx = Math.Max(1, FieldGen.JsRound(c.Hp * obst * s.Setup.WallHpMul));
                    double hp = saved != null && saved.TryGet(f, c.Idx, out double kept) ? Math.Min(kept, mx) : mx;
                    var o = RunObjectFactory.Create(t, wallKind, g.laneX0 + c.L * g.laneStep, z0 + c.R * g.rowStep);
                    o.Bar = f; o.Row = c.R; o.Lane = c.L; o.Idx = c.Idx; o.Rew = c.Rew;
                    o.HasHp = true; o.Hp = hp; o.Max = mx;
                    o.RewardType = FieldGen.RewardTypeOf(g, c.Rew);
                    o.RewardId = 0;
                    o.RewardAmount = c.Amt;
                    if (hp <= 0) { o.Dead = true; o.Pre = true; }
                    objs.Add(o);
                    zEnd = Math.Max(zEnd, o.Z);
                }
                z = zEnd + g.fieldToNext;
            }

            // ④ Area 아치
            double gz = z;
            var goalKind = t.ObjectKind.Get(g.archGoalKindId);
            var goal = LevelGen.Spawn(s, goalKind, 0, gz);
            goal.LabelKey = goalKind.labelKey;
            goal.LabelArg = s.Area + 1;
            objs.Add(goal);

            foreach (var o in objs) o.Lvl = level;
            s.Level = level;
            s.Tpl = tplId;
            s.Base = baseZ;
            s.Gz = gz;
            s.Floor = 0;
            s.Cp = cp;
            s.CpRun = cp;
            s.LvClimbed = 0;
            s.SavedBars = saved;
            foreach (var o in objs) s.AddObj(o);
        }

        // ---------------- fieldSpec (§4.14.3) ----------------

        public IReadOnlyList<FieldCell> FieldSpec(int area, int floor)
        {
            var s = _state ?? throw new InvalidOperationException("FieldSpec: NewRun 전에는 표·프리셋을 알 수 없습니다");
            return FieldSpec(s.Tables, s.Bal, s.Setup.PerkDcap, area, floor, FieldGenKey);
        }

        /// <summary>상태 없이 부르는 fieldSpec(A, f). A·f는 0기반.</summary>
        public static List<FieldCell> FieldSpec(MvTables t, BalancePresetData bal, int perkDcap, int area, int floor,
            string fieldGenKey = "default", FieldGen.HashLog log = null)
        {
            var g = FieldGenRow(t, fieldGenKey);
            var input = FieldGen.InputFor(t, g, bal, LevelGen.DiaCap(t, perkDcap), area, floor);
            return FieldGen.Build(g, input, log);
        }

        List<FieldCell> FieldSpecFor(RunState s, int floor) => FieldSpec(s.Tables, s.Bal, s.Setup.PerkDcap, s.Area, floor, FieldGenKey);

        FieldGenData FieldGenRow(MvTables t) => FieldGenRow(t, FieldGenKey);

        static FieldGenData FieldGenRow(MvTables t, string key) =>
            t.FieldGen.ByKey(key) ?? throw new InvalidOperationException($"313_fieldGen: key {key} 행이 없습니다");

        // ---------------- newRun 보조 ----------------

        /// <summary>06_balancePreset[key], 없으면 isDefault 행(D-03 easy).</summary>
        public static BalancePresetData Preset(MvTables t, string key)
        {
            var p = t.BalancePreset.ByKey(key);
            if (p != null) return p;
            foreach (var row in t.BalancePreset.Sorted) if (row.isDefault) return row;
            throw new InvalidOperationException($"06_balancePreset: {key} 행과 기본 행이 없습니다");
        }

        /// <summary>정본 `kegStart(P)`: 11_stairLevel.need로 Lv를 올린다(상한 LOBBY_STAIR_MAX_LV).</summary>
        public static (int lv, int step, int need) KegStart(MvTables t, int purchases)
        {
            int max = t.RunConfig.I(RunConfigKeys.LOBBY_STAIR_MAX_LV);
            int lv = 1, p = purchases;
            while (lv < max && p >= StairNeed(t, lv)) { p -= StairNeed(t, lv); lv++; }
            return (lv, p, StairNeed(t, lv));
        }

        static int StairNeed(MvTables t, int lv)
        {
            foreach (var r in t.StairLevel.Datas) if (r.lv == lv) return r.need;
            throw new InvalidOperationException($"11_stairLevel: lv {lv} 행이 없습니다");
        }

        /// <summary>정본 `baseTier(P)` = wLevel(P).lv: 12_weaponLevel.need로 Lv를 올린다(상한 = 표의 최대 lv).</summary>
        public static int BaseTier(MvTables t, int purchases)
        {
            var rows = new List<WeaponLevelData>(t.WeaponLevel.Datas);
            rows.Sort((a, b) => a.lv.CompareTo(b.lv));
            int max = rows.Count > 0 ? rows[rows.Count - 1].lv : 1;
            int lv = 1, p = purchases;
            while (lv < max)
            {
                var row = rows.Find(r => r.lv == lv);
                if (row == null || p < row.need) break;
                p -= row.need;
                lv++;
            }
            return lv;
        }

        static ChargeGateData WellRow(MvTables t)
        {
            foreach (var g in t.ChargeGate.Datas) if (g.gateType == GateType.Well) return g;
            return null;
        }

        /// <summary>
        /// 정본 `gaugeNeed(lv)` = 150·1.6^(lv−1)·(Z.hp/100)·gauge(107 Well 행). 곱 순서는 정본 그대로.
        /// MV-RUN-SIM의 RunFormulas.GaugeNeed와 같은 식이다. 병합 뒤 그쪽 하나로 합칠 수 있다.
        /// </summary>
        static double GaugeNeed(RunState s, ChargeGateData g, int lv)
        {
            if (g == null) return 0;
            int k = lv - 1;
            double v = g.growth == Growth.Add ? g.@base + g.step * k : g.growth == Growth.Pow ? g.@base * Math.Pow(g.step, k) : g.@base;
            if (g.useScale) v *= s.Bal.scale;
            if (g.useAreaHp) v *= s.ZoneHp / 100;
            if (g.useGauge) v *= s.Bal.gauge;
            if (g.mul != 1) v *= g.mul;
            return v;
        }

        /// <summary>카드 풀(111_cardPool, 정본 `setGodCards` = 공용 active + 출전 신 active). Setup.CardPoolId 0이면 key main.</summary>
        static void BuildCardPool(RunState s)
        {
            var t = s.Tables;
            s.CardPool.Clear();
            int group = s.Setup.CardPoolId;
            if (group == 0)
            {
                var main = t.CardPool.ByKey("main");
                if (main == null) return;
                group = main.group;
            }
            var exclude = new HashSet<int>();
            foreach (var e in t.CardPool.GetGroup(group))
            {
                int w = Math.Max(1, e.weight);
                switch (e.entryType)
                {
                    case CardPoolEntryType.CommonActive: AddActive(s, t.Card.GetGroup(0), w); break;
                    case CardPoolEntryType.GodCards: if (s.God != null) AddActive(s, t.Card.GetGroup(s.God.cardGroup), w); break;
                    case CardPoolEntryType.Card:
                        var c = t.Card.Get(e.cardId);
                        if (c != null) for (int i = 0; i < w; i++) s.CardPool.Add(c);
                        break;
                    case CardPoolEntryType.Exclude: exclude.Add(e.cardId); break;
                }
            }
            if (exclude.Count > 0) s.CardPool.RemoveAll(c => exclude.Contains(c.id));
        }

        static void AddActive(RunState s, IReadOnlyList<CardData> cards, int w)
        {
            var list = new List<CardData>();
            foreach (var c in cards) if (c.active) list.Add(c);
            list.Sort((a, b) => a.sort != b.sort ? a.sort.CompareTo(b.sort) : a.id.CompareTo(b.id));
            foreach (var c in list) for (int i = 0; i < w; i++) s.CardPool.Add(c);
        }
    }
}
