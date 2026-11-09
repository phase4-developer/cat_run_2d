using System;
using System.Collections.Generic;
using MV.Data;

namespace MV.Run
{
    /// <summary>
    /// 레벨 생성 표 해석(정본 `tplFor`·`makeObjs`와 오브젝트 HP·보상 규칙, 04 §4.14.1~4.14.2, 07 §7.5~7.6).
    /// 종류 이름으로 분기하지 않는다. 105 행이 가리키는 114·102·109 행과 311·314 배치 행만 본다.
    /// </summary>
    public static class LevelGen
    {
        /// <summary>정본 `tplFor`(L857) → 310 id. 312_templateCycle 묶음 cycleGroup(본편 0).</summary>
        public static int TplFor(MvTables t, int level, int cycleGroup = 0)
        {
            int id = t.TemplateCycle.TemplateFor(cycleGroup, level);
            if (id == 0) throw new InvalidOperationException($"312_templateCycle: cycleGroup {cycleGroup} level {level}을 덮는 행이 없습니다");
            return id;
        }

        /// <summary>0기반 area의 302 행.</summary>
        public static AreaData AreaRow(MvTables t, int area)
        {
            foreach (var a in t.Area.Datas)
                if (a.areaNo == area + 1) return a;
            throw new InvalidOperationException($"302_area: areaNo {area + 1} 행이 없습니다");
        }

        /// <summary>오브젝트 종류의 체력 규칙(102 행이 있으면 그 hpRuleId가 우선).</summary>
        public static HpRuleData HpRuleOf(MvTables t, ObjectKindData kind)
        {
            if (kind == null) return null;
            int id = kind.hpRuleId;
            if (kind.enemyId != 0)
            {
                var e = t.Enemy.Get(kind.enemyId);
                if (e != null && e.hpRuleId != 0) id = e.hpRuleId;
            }
            return id != 0 ? t.HpRule.Get(id) : null;
        }

        /// <summary>obstMul() = max(floor, base + perLv·obst) — 14_rebirthPerk[obst].</summary>
        public static double ObstMul(MvTables t, int obst)
        {
            var p = t.RebirthPerk.ByKey("obst");
            return Math.Max(p.floor ?? double.NegativeInfinity, p.@base + p.perLv * obst);
        }

        /// <summary>diaCap() = base + perLv·dcap — 14_rebirthPerk[dcap].</summary>
        public static double DiaCap(MvTables t, int dcap)
        {
            var p = t.RebirthPerk.ByKey("dcap");
            return p.@base + p.perLv * dcap;
        }

        /// <summary>
        /// 처음 체력(114). AreaHpMul = round(zoneHp × mul [× scale]), Fixed = fixed [× scale]. useObstMul이면 max(1, round(v × obstMul)).
        /// None·ModeStage(모드가 채움)·FieldLerp(필드 칸은 BuildLevel이 채움)는 체력 없음으로 둔다.
        /// </summary>
        public static void InitHp(RunState s, RunObject o, HpRuleData rule)
        {
            o.HasHp = false;
            o.Hp = o.Max = 0;
            if (rule == null) return;
            double v;
            switch (rule.type)
            {
                case HpRuleType.AreaHpMul: v = s.ZoneHp * rule.mul; break;
                case HpRuleType.Fixed: v = rule.@fixed; break;
                default: return;
            }
            if (rule.useScale) v *= s.Bal.scale;
            v = FieldGen.JsRound(v);
            if (rule.useObstMul) v = Math.Max(1, FieldGen.JsRound(v * ObstMul(s.Tables, s.Setup.PerkObst)));
            o.HasHp = true;
            o.Hp = o.Max = v;
        }

        /// <summary>consumeData 1항목의 값(zone·DiaCap 규칙 반영, goldMul 전).</summary>
        public static double RewardValue(RunState s, in ConsumeData c)
        {
            double cap = DiaCap(s.Tables, s.Setup.PerkDcap);
            switch (c.Rule)
            {
                case RewardValueRule.DiaCap: return cap;
                case RewardValueRule.HalfDiaCapCeil: return Math.Ceiling(cap / 2);
                default: return c.ValueAtZone(s.ZoneIndex);
            }
        }

        /// <summary>보상 1항목을 오브젝트에 넣는다. 목록이 비면 보상 없음.</summary>
        public static void SetReward(RunState s, RunObject o, IReadOnlyList<ConsumeData> list)
        {
            o.RewardType = ConsumeType.None;
            o.RewardId = 0;
            o.RewardAmount = 0;
            if (list == null || list.Count == 0) return;
            var c = list[0];
            o.RewardType = c.Type;
            o.RewardId = c.Id;
            o.RewardAmount = RewardValue(s, c);
        }

        /// <summary>종류 행의 보상(rewardSource Row·Enemy). FieldCell은 BuildLevel이 칸 값으로 채운다.</summary>
        public static void SetKindReward(RunState s, RunObject o, IReadOnlyList<ConsumeData> overrideReward = null)
        {
            if (overrideReward != null && overrideReward.Count > 0) { SetReward(s, o, overrideReward); return; }
            var k = o.Kind;
            if (k.rewardSource == RewardSource.Enemy && o.Enemy != null) SetReward(s, o, o.Enemy.reward);
            else if (k.rewardSource == RewardSource.Row) SetReward(s, o, k.reward);
            else SetReward(s, o, null);
        }

        /// <summary>종류 행으로 오브젝트 1개를 만든다(HP·보상 포함, 위치는 인자).</summary>
        public static RunObject Spawn(RunState s, ObjectKindData kind, double x, double z, int hpRuleOverride = 0,
            IReadOnlyList<ConsumeData> rewardOverride = null)
        {
            var t = s.Tables;
            var o = RunObjectFactory.Create(t, kind, x, z);
            var rule = hpRuleOverride != 0 ? t.HpRule.Get(hpRuleOverride) : HpRuleOf(t, kind);
            InitHp(s, o, rule);
            SetKindReward(s, o, rewardOverride);
            return o;
        }

        static IReadOnlyList<TemplateObjectData> TemplateRows(MvTables t, int templateId)
        {
            var rows = t.TemplateObject.InZOrder(templateId);
            if (rows.Count > 0) return rows;
            // BuildIndexes 전(시험 등)이면 직접 정렬한다(z 오름차순, 같은 z는 seq 순, 07 §7.6.2).
            var list = new List<TemplateObjectData>(t.TemplateObject.GetGroup(templateId));
            list.Sort((a, b) => a.z != b.z ? a.z.CompareTo(b.z) : a.seq.CompareTo(b.seq));
            return list;
        }

        /// <summary>
        /// 정본 `makeObjs`(L1314): 311 행을 z 순서로 깔고, groupId가 있으면 314 무리 행마다 1개씩(x는 ±ENEMY_X_MAX로 자름).
        /// 이동 AI에 속도 범위가 있으면 RunRng 1회(<see cref="RngUse.EnemySpeed"/>). 그림 위상(ph)은 FxRng 몫이라 여기서 뽑지 않는다.
        /// </summary>
        public static void MakeObjs(RunState s, int templateId, double baseZ, List<RunObject> outList)
        {
            var t = s.Tables;
            double xMax = t.RunConfig.D(RunConfigKeys.ENEMY_X_MAX);
            foreach (var row in TemplateRows(t, templateId))
            {
                if (!row.active) continue;
                double z = row.z + baseZ;
                var kind = t.ObjectKind.Get(row.kindId);
                if (row.groupId != 0)
                {
                    foreach (var g in t.SpawnGroup.GetGroup(row.groupId))
                    {
                        var gk = g.kindId != 0 ? t.ObjectKind.Get(g.kindId) : kind;
                        double x = Clamp(row.x + g.dx, -xMax, xMax);
                        var o = Spawn(s, gk, x, z + g.dz, row.hpRuleId, row.reward);
                        if (o.Move != null && o.Move.speedMax > 0)
                            o.Spd = o.Move.speedMin + s.Draw(RngUse.EnemySpeed) * (o.Move.speedMax - o.Move.speedMin);
                        outList.Add(o);
                    }
                }
                else
                {
                    outList.Add(Spawn(s, kind, row.x, z, row.hpRuleId, row.reward));
                }
            }
        }

        static double Clamp(double v, double a, double b) => v < a ? a : (v > b ? b : v);
    }
}
