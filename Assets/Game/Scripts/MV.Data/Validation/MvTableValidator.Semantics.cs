using System;
using System.Collections.Generic;
using System.Linq;

namespace MV.Data
{
    // 7.13.3 의미 규칙과 801~804 확률 표 규칙.
    public static partial class MvTableValidator
    {
        /// <summary>화면이 key로 찾는 광고 자리(07 §7.13.3 08).</summary>
        static readonly string[] s_requiredAdKeys = { "dia", "skip", "collect3", "interstitial", "modeTicket" };

        static void CheckSemantics(MvTables t, MvValidationReport r, MvValidationOptions o)
        {
            CheckBalancePreset(t, r);
            CheckWorld(t, r);
            CheckAreaFloor(t, r);
            CheckTemplates(t, r);
            CheckTemplateCycle(t, r);
            CheckSpawnGroup(t, r);
            CheckObjectKind(t, r, o);
            CheckComponents(t, r);
            CheckCards(t, r);
            CheckLevelPrices(t, r);
            CheckAdPlacement(t, r);
            CheckContentOpen(t, r);
            CheckUiResource(t, r);
            CheckValueFormats(t, r);
            CheckPairValues(t, r);
            CheckContentData(t, r);
            CheckMetaSystems(t, r);
            CheckModesAndShop(t, r);
        }

        // ---------------- 06 ----------------

        static void CheckBalancePreset(MvTables t, MvValidationReport r)
        {
            int n = t.BalancePreset.Datas.Count(p => p.isDefault);
            if (n != 1) r.Error(BalancePresetData.Table, null, "isDefault", $"isDefault 1인 행이 정확히 1개여야 합니다(지금 {n}개)");
        }

        // ---------------- 301·302 ----------------

        static void CheckWorld(MvTables t, MvValidationReport r)
        {
            var areas = t.Area.Datas.OrderBy(a => a.areaNo).ToList();
            for (int i = 0; i < areas.Count; i++)
                if (areas[i].areaNo != i + 1)
                {
                    r.Error(AreaData.Table, areas[i].id, "areaNo", $"areaNo는 1부터 연속이어야 합니다({i + 1}이 와야 하는데 {areas[i].areaNo})");
                    break;
                }

            var zones = t.Zone.Datas.OrderBy(z => z.areaFrom).ToList();
            int expect = 1;
            foreach (var z in zones)
            {
                if (z.areaFrom > z.areaTo) r.Error(ZoneData.Table, z.id, "areaTo", $"areaFrom {z.areaFrom} > areaTo {z.areaTo}입니다");
                if (z.areaFrom < expect) r.Error(ZoneData.Table, z.id, "areaFrom", $"Zone 범위가 겹칩니다(areaFrom {z.areaFrom} < {expect})");
                else if (z.areaFrom > expect) r.Error(ZoneData.Table, z.id, "areaFrom", $"Zone 범위에 빈틈이 있습니다(Area {expect}~{z.areaFrom - 1})");
                expect = Math.Max(expect, z.areaTo + 1);
            }
            if (areas.Count > 0 && expect - 1 != areas[areas.Count - 1].areaNo)
                r.Error(ZoneData.Table, null, "areaTo", $"Zone 범위 끝 {expect - 1}이 마지막 Area {areas[areas.Count - 1].areaNo}와 다릅니다");

            foreach (var a in areas)
            {
                var zone = t.Zone.Get(a.zoneId);
                if (zone != null && (a.areaNo < zone.areaFrom || a.areaNo > zone.areaTo))
                    r.Error(AreaData.Table, a.id, "zoneId", $"areaNo {a.areaNo}가 Zone {a.zoneId} 범위({zone.areaFrom}~{zone.areaTo}) 밖입니다");
            }

            if (t.RunConfig.Has(RunConfigKeys.PLAYABLE_AREAS) && TryConfig(t, RunConfigKeys.PLAYABLE_AREAS, out double playable))
            {
                int active = areas.Count(a => a.active);
                if (playable > active)
                    r.Error(RunConfigData.Table, null, RunConfigKeys.PLAYABLE_AREAS, $"PLAYABLE_AREAS {playable}가 active Area 수 {active}보다 큽니다");
            }
        }

        static bool TryConfig(MvTables t, string key, out double value)
        {
            value = 0;
            try { value = t.RunConfig.D(key); return true; }
            catch (Exception) { return false; } // 형식 오류는 CheckRunConfigKeys가 낸다.
        }

        // ---------------- 303 ----------------

        static void CheckAreaFloor(MvTables t, MvValidationReport r)
        {
            const string name = AreaFloorData.Table;
            int floorCount = TryConfig(t, RunConfigKeys.FLOOR_COUNT, out double fc) ? (int)fc : 0;
            var field = t.FieldGen.ByKey("default") ?? t.FieldGen.Datas.FirstOrDefault();
            int soulMinIdx = field?.soulMinIdx ?? 0;

            // Area가 쓰는 묶음은 floor 1~FLOOR_COUNT, 모드 전용 묶음(401만 씀, 901~)은 1부터 마지막 floor까지 빈틈 없이(08 §8.4.6).
            var areaGroups = new HashSet<int>(t.Area.Datas.Select(a => a.floorGroup));
            foreach (var g in t.AreaFloor.Groups)
            {
                var rows = t.AreaFloor.GetGroup(g);
                int last = areaGroups.Contains(g) ? floorCount : rows.Max(x => x.floor);
                for (int f = 1; f <= last; f++)
                    if (!rows.Any(x => x.floor == f)) r.Error(name, null, "floor", $"floorGroup {g}에 floor {f}가 없습니다");
            }

            foreach (var x in t.AreaFloor.Datas)
            {
                if (x.rows < 1) r.Error(name, x.id, "rows", "rows는 1 이상이어야 합니다");
                if (x.h0 > x.h1) r.Error(name, x.id, "h1", $"h0 {x.h0} > h1 {x.h1}입니다");
                if (x.cellMode != CellMode.Baked) continue;

                if (x.soulCells.Count != x.soulAmts.Count)
                    r.Error(name, x.id, "soulAmts", $"soulCells {x.soulCells.Count}개와 soulAmts {x.soulAmts.Count}개가 다릅니다");
                int sum = x.soulAmts.Sum();
                if (sum != x.souls) r.Error(name, x.id, "soulAmts", $"soulAmts 합 {sum}이 souls {x.souls}와 다릅니다");
                int cells = x.rows * 3;
                foreach (var idx in x.soulCells.Concat(x.diaCells))
                    if (idx < 0 || idx >= cells) r.Error(name, x.id, "soulCells", $"칸 idx {idx}가 rows×3 = {cells} 범위 밖입니다");
                foreach (var idx in x.soulCells)
                    if (idx < soulMinIdx) r.Error(name, x.id, "soulCells", $"생선 칸 idx {idx}가 soulMinIdx {soulMinIdx}보다 작습니다");
                foreach (var idx in x.soulCells.Intersect(x.diaCells))
                    r.Error(name, x.id, "diaCells", $"생선·캣닢 칸이 겹칩니다(idx {idx})");
            }

            // 탈출 가능(08 §8.2.4, MV-SPEC-02 Q-32): 개방 때 넘친 구출 고양이는 남지 않으므로 Area마다 결계 고양이 합 ≥ 요구 증가분.
            // 경고로 둔다(D-70 「경고로 둔다」). Area 30 원작 1,755는 Area 30 결계 고양이 임시 값으로 맞춘다.
            int prevNeed = 0;
            foreach (var a in t.Area.Datas.OrderBy(x => x.areaNo))
            {
                int cats = t.AreaFloor.GetGroup(a.floorGroup).Sum(x => x.souls);
                if (a.needSouls - prevNeed > cats)
                    r.Warn(AreaData.Table, a.id, "needSouls", $"요구 증가분 {a.needSouls - prevNeed}이 결계 고양이 합 {cats}보다 커서 이 Area를 나갈 수 없습니다");
                prevNeed = a.needSouls;
            }

            foreach (var g in t.Area.Datas.GroupBy(a => a.floorGroup).Where(g => g.Count() > 1))
                if (t.AreaFloor.GetGroup(g.Key).Any(x => x.cellMode == CellMode.Baked))
                    r.Warn(AreaData.Table, null, "floorGroup",
                        $"Baked floorGroup {g.Key}를 여러 Area가 공유합니다({string.Join(", ", g.Select(a => a.areaNo))})");
        }

        // ---------------- 310·311 ----------------

        static void CheckTemplates(MvTables t, MvValidationReport r)
        {
            const string name = TemplateObjectData.Table;
            double laneMax = TryConfig(t, RunConfigKeys.LANE_X_MAX, out double lm) ? lm : double.MaxValue;
            var byTemplate = t.TemplateObject.Datas.GroupBy(x => x.templateId).ToDictionary(g => g.Key, g => g.ToList());

            foreach (var tpl in t.Template.Datas)
            {
                byTemplate.TryGetValue(tpl.id, out var objs);
                objs ??= new List<TemplateObjectData>();
                if (!objs.Any(x => t.ObjectKind.Get(x.kindId)?.key == "stair"))
                    r.Error(TemplateData.Table, tpl.id, null, "템플릿에 stair 오브젝트가 없습니다");
                foreach (var x in objs)
                {
                    if (x.z >= tpl.endZ) r.Error(name, x.id, "z", $"z {x.z}가 템플릿 endZ {tpl.endZ} 이상입니다");
                    var kind = t.ObjectKind.Get(x.kindId);
                    if (kind == null) continue;
                    if (Math.Abs(x.x) > laneMax + kind.hw + 1e-9)
                        r.Error(name, x.id, "x", $"|x| {Math.Abs(x.x)}가 LANE_X_MAX + hw = {laneMax + kind.hw}를 넘습니다");
                    if (kind.blocks) r.Error(name, x.id, "kindId", $"막는 종류(blocks=1) `{kind.key}`는 템플릿에 둘 수 없습니다");
                }
            }
        }

        // ---------------- 312 ----------------

        static void CheckTemplateCycle(MvTables t, MvValidationReport r)
        {
            const string name = TemplateCycleData.Table;
            var rows = t.TemplateCycle.GetGroup(0).OrderBy(x => x.levelFrom).ToList();
            if (rows.Count == 0) { r.Error(name, null, "cycleGroup", "cycleGroup 0 행이 없습니다"); return; }
            int expect = 1;
            for (int i = 0; i < rows.Count; i++)
            {
                var x = rows[i];
                if (x.templates.Count == 0) r.Error(name, x.id, "templates", "templates가 비었습니다");
                if (x.levelFrom != expect)
                    r.Error(name, x.id, "levelFrom", x.levelFrom > expect ? $"Level {expect}~{x.levelFrom - 1}을 덮는 행이 없습니다" : $"Level {x.levelFrom}이 겹칩니다");
                bool last = i == rows.Count - 1;
                if (x.levelTo == 0)
                {
                    if (!last) r.Error(name, x.id, "levelTo", "끝없음(levelTo 0) 행 뒤에 행이 또 있습니다");
                    return;
                }
                if (x.levelTo < x.levelFrom) r.Error(name, x.id, "levelTo", $"levelTo {x.levelTo} < levelFrom {x.levelFrom}입니다");
                expect = x.levelTo + 1;
            }
            r.Error(name, rows[rows.Count - 1].id, "levelTo", "cycleGroup 0의 마지막 행은 levelTo 0(끝없음)이어야 합니다");
        }

        // ---------------- 314 ----------------

        static void CheckSpawnGroup(MvTables t, MvValidationReport r)
        {
            if (!TryConfig(t, RunConfigKeys.MAX_CLONES, out double maxClones)) return;
            var clone = t.SpawnGroup.Datas.Where(x => x.key == "clone").ToList();
            if (clone.Count < maxClones)
                r.Error(SpawnGroupData.Table, null, "group", $"clone 그룹 행 {clone.Count}개가 MAX_CLONES {maxClones}보다 적습니다");
        }

        // ---------------- 105 ----------------

        static void CheckObjectKind(MvTables t, MvValidationReport r, MvValidationOptions o)
        {
            const string name = ObjectKindData.Table;
            var dcap = t.RebirthPerk.ByKey("dcap");
            foreach (var k in t.ObjectKind.Datas)
            {
                if (o.IsComponentImplemented != null)
                {
                    CheckImpl(name, k.id, "hittable", k.hittable.ToString(), r, o);
                    if (t.HitReaction.Get(k.hitReactionId) is { } hr) CheckImpl(name, k.id, "hitReactionId", hr.type.ToString(), r, o);
                    if (t.ReceiveCondition.Get(k.receiveId) is { } rc) CheckImpl(name, k.id, "receiveId", rc.type.ToString(), r, o);
                    if (t.MoveAi.Get(k.moveAiId) is { } mv) CheckImpl(name, k.id, "moveAiId", mv.type.ToString(), r, o);
                    if (t.HpRule.Get(k.hpRuleId) is { } hp) CheckImpl(name, k.id, "hpRuleId", hp.type.ToString(), r, o);
                }
                if (k.enemyId != 0 && k.rewardSource != RewardSource.Enemy)
                    r.Error(name, k.id, "rewardSource", "enemyId가 있으면 rewardSource는 Enemy여야 합니다");
                if (k.blocks && k.hpRuleId == 0) r.Error(name, k.id, "hpRuleId", "blocks=1이면 hpRuleId가 있어야 합니다");
                if (k.hittable == Hittable.BeforePass && t.ReceiveCondition.Get(k.receiveId)?.type != ReceiveType.OnPass)
                    r.Error(name, k.id, "receiveId", "hittable BeforePass면 receive가 OnPass여야 합니다");
                if (k.key == "gem" && dcap != null)
                    foreach (var rw in k.reward)
                        if (rw.Rule == RewardValueRule.Flat && Math.Abs(rw.Value - dcap.@base) > 1e-9)
                            r.Warn(name, k.id, "rewardValue", $"gem rewardValue {rw.Value}가 14_rebirthPerk[dcap].base {dcap.@base}와 다릅니다");
            }
        }

        static void CheckImpl(string table, int id, string column, string type, MvValidationReport r, MvValidationOptions o)
        {
            if (!o.IsComponentImplemented(column, type)) r.Error(table, id, column, $"컴포넌트 type `{type}`의 구현이 등록되지 않았습니다");
        }

        // ---------------- 107·108·109 ----------------

        static void CheckComponents(MvTables t, MvValidationReport r)
        {
            foreach (var g in t.ChargeGate.Datas)
                if (g.growth == Growth.Pow || g.growth == Growth.Add)
                {
                    if (g.step <= 0) r.Error(ChargeGateData.Table, g.id, "step", $"growth {g.growth}면 step > 0이어야 합니다");
                    if (g.cap < 0) r.Error(ChargeGateData.Table, g.id, "cap", "cap은 0 이상이어야 합니다");
                }
            foreach (var c in t.ReceiveCondition.Datas)
                if (c.type == ReceiveType.OnPass)
                {
                    if (c.margin <= 0) r.Error(ReceiveConditionData.Table, c.id, "margin", "OnPass면 margin > 0이어야 합니다");
                    if (c.insideX <= 0) r.Error(ReceiveConditionData.Table, c.id, "insideX", "OnPass면 insideX > 0이어야 합니다");
                }
            foreach (var m in t.MoveAi.Datas)
                if (m.type == MoveAiType.Chase)
                {
                    if (m.speedMin > m.speedMax) r.Error(MoveAiData.Table, m.id, "speedMax", "speedMin ≤ speedMax여야 합니다");
                    if (m.aggro <= 0) r.Error(MoveAiData.Table, m.id, "aggro", "Chase면 aggro > 0이어야 합니다");
                }
        }

        // ---------------- 110·111·115 ----------------

        static void CheckCards(MvTables t, MvValidationReport r)
        {
            var tiers = new HashSet<int>(t.CardTier.Datas.Select(x => x.tierIndex));
            foreach (var c in t.Card.Datas)
                if (!tiers.Contains(c.tier)) r.Error(CardData.Table, c.id, "tier", $"tier {c.tier}가 115_cardTier에 없습니다");

            bool commonHasTier0 = t.Card.GetGroup(0).Any(c => c.tier == 0 && c.active);
            var godGroups = new HashSet<int>(t.God.Datas.Select(g => g.cardGroup));
            bool godHasTier0 = t.Card.Datas.Any(c => c.tier == 0 && c.active && godGroups.Contains(c.group));
            foreach (var g in t.CardPool.Groups)
            {
                bool ok = false;
                foreach (var e in t.CardPool.GetGroup(g))
                {
                    if (e.weight <= 0) continue;
                    if (e.entryType == CardPoolEntryType.Card && t.Card.Get(e.cardId) is { tier: 0 }) ok = true;
                    else if (e.entryType == CardPoolEntryType.CommonActive && commonHasTier0) ok = true;
                    else if (e.entryType == CardPoolEntryType.GodCards && godHasTier0) ok = true;
                }
                if (!ok) r.Error(CardPoolData.Table, null, "group", $"카드 풀 {g}에 tier 0 카드가 없습니다");
            }

            var ordered = t.CardTier.Datas.OrderBy(x => x.tierIndex).ToList();
            for (int i = 1; i < ordered.Count; i++)
                if (ordered[i].need <= ordered[i - 1].need)
                    r.Error(CardTierData.Table, ordered[i].id, "need", "need는 tierIndex 순으로 오름차순이어야 합니다");
            var climb = t.Climb.Config();
            if (ordered.Count > 0 && climb != null && Math.Abs(ordered[ordered.Count - 1].need - climb["lap"]) > 1e-9)
                r.Error(CardTierData.Table, ordered[ordered.Count - 1].id, "need", $"마지막 need {ordered[ordered.Count - 1].need}가 330_climb.lap {climb["lap"]}과 다릅니다");
        }

        // ---------------- 10·13·14·16 ----------------

        static void CheckLevelPrices(MvTables t, MvValidationReport r)
        {
            var priced = new List<(string table, ILevelPriced row)>();
            priced.AddRange(t.LobbyUpgrade.Datas.Select(x => (LobbyUpgradeData.Table, (ILevelPriced)x)));
            priced.AddRange(t.AreaUpgrade.Datas.Select(x => (AreaUpgradeData.Table, (ILevelPriced)x)));
            priced.AddRange(t.RebirthPerk.Datas.Select(x => (RebirthPerkData.Table, (ILevelPriced)x)));
            foreach (var (table, row) in priced)
            {
                var have = new HashSet<int>(t.LevelPrice.GetGroup(row.levelPriceGroup).Select(x => x.targetLevel));
                var missing = Enumerable.Range(1, Math.Max(0, row.max)).Where(l => !have.Contains(l)).ToList();
                if (missing.Count > 0)
                    r.Error(table, row.id, "levelPriceGroup",
                        $"16_levelPrice 묶음 {row.levelPriceGroup}에 targetLevel {string.Join(",", missing.Take(5))}{(missing.Count > 5 ? "…" : "")}가 없습니다(1~{row.max})");
            }

            if (t.LobbyUpgrade.ByKey("stair") is { } stair && TryConfig(t, RunConfigKeys.LOBBY_STAIR_MAX_LV, out double stairMaxLv))
            {
                int sum = t.StairLevel.Datas.Where(x => x.lv < stairMaxLv).Sum(x => x.need);
                if (stair.max != sum) r.Error(LobbyUpgradeData.Table, stair.id, "max", $"stair max {stair.max} ≠ Σ11.need(lv<{stairMaxLv}) = {sum}");
            }
            if (t.LobbyUpgrade.ByKey("weapon") is { } weapon && TryConfig(t, RunConfigKeys.WEAPON_EXTRA_BUYS, out double extra))
            {
                // 마지막 Level(12_weaponLevel lv 최대)에서는 게이지 대신 WEAPON_EXTRA_BUYS만큼 더 산다. 상한은 표 행 수에서 읽는다.
                int lastLv = t.WeaponLevel.Datas.Count > 0 ? t.WeaponLevel.Datas.Max(x => x.lv) : 1;
                int sum = t.WeaponLevel.Datas.Where(x => x.lv >= 1 && x.lv < lastLv).Sum(x => x.need) + (int)extra;
                if (weapon.max != sum) r.Error(LobbyUpgradeData.Table, weapon.id, "max", $"weapon max {weapon.max} ≠ Σ12.need(lv 1~{lastLv - 1}) + WEAPON_EXTRA_BUYS = {sum}");
            }

            List<MvLevelPriceGenerator.Row> generated;
            try { generated = MvLevelPriceGenerator.Generate(t); }
            catch (Exception) { return; }
            foreach (var g in generated)
            {
                var row = t.LevelPrice.Get(g.Group, g.TargetLevel);
                if (row == null) continue; // 빈틈은 위에서 오류.
                var p = row.price.Count == 1 ? row.price[0] : default;
                if (row.price.Count != 1 || p.Id != g.WealthId || Math.Abs(p.Value - g.Price) > 1e-9)
                    r.Warn(LevelPriceData.Table, row.id, "price", $"생성 도구 결과(1/{g.WealthId}/{g.Price})와 다릅니다");
            }
        }

        // ---------------- 08·09 ----------------

        static void CheckAdPlacement(MvTables t, MvValidationReport r)
        {
            const string name = AdPlacementData.Table;
            var placements = new HashSet<string>(StringComparer.Ordinal);
            foreach (var a in t.AdPlacement.Datas.OrderBy(x => x.id))
            {
                if (!string.IsNullOrEmpty(a.placement) && !placements.Add(a.placement))
                    r.Error(name, a.id, "placement", $"placement `{a.placement}`가 중복됩니다");
                if (a.dailyLimit < 0) r.Error(name, a.id, "dailyLimit", "dailyLimit은 0 이상이어야 합니다");
            }
            foreach (var key in s_requiredAdKeys)
                if (!t.AdPlacement.HasKey(key)) r.Error(name, null, "key", $"화면이 찾는 key `{key}` 행이 없습니다");
        }

        /// <summary>05_contentOpen 조건 목록(07 §7.13.3): 종류·값 개수 같음, 종류는 <see cref="UnlockConditionType"/> 값, 값 0 이상.</summary>
        static void CheckContentOpen(MvTables t, MvValidationReport r)
        {
            const string name = ContentOpenData.Table;
            foreach (var o in t.ContentOpen.Datas.OrderBy(x => x.id))
            {
                CheckUnlockConditions(name, o.id, "showCondition", o.showConditionType, o.showConditionValue, r);
                CheckUnlockConditions(name, o.id, "condition", o.conditionType, o.conditionValue, r);
            }
        }

        static void CheckUnlockConditions(string table, int id, string prefix, IReadOnlyList<int> types, IReadOnlyList<int> values, MvValidationReport r)
        {
            if (types.Count != values.Count)
                r.Error(table, id, prefix + "Value", $"{prefix}Type {types.Count}개와 {prefix}Value {values.Count}개의 개수가 다릅니다");
            foreach (int type in types)
                if (!Enum.IsDefined(typeof(UnlockConditionType), type))
                    r.Error(table, id, prefix + "Type", $"조건 종류 {type}는 UnlockConditionType 값이 아닙니다");
            foreach (int value in values)
                if (value < 0) r.Error(table, id, prefix + "Value", $"조건 값 {value}는 0 이상이어야 합니다");
        }

        static void CheckUiResource(MvTables t, MvValidationReport r)
        {
            foreach (var u in t.UiResource.Datas)
            {
                bool sprite = !string.IsNullOrEmpty(u.atlas) && !string.IsNullOrEmpty(u.sprite);
                bool partial = string.IsNullOrEmpty(u.atlas) != string.IsNullOrEmpty(u.sprite);
                bool address = !string.IsNullOrEmpty(u.address);
                if (partial) r.Error(UiResourceData.Table, u.id, "sprite", "atlas와 sprite는 함께 채워야 합니다");
                else if (sprite == address) r.Error(UiResourceData.Table, u.id, "address", "`atlas+sprite`와 `address` 중 하나만 채워야 합니다");
            }
        }

        // ---------------- 13·14 valueFormat ----------------

        static void CheckValueFormats(MvTables t, MvValidationReport r)
        {
            foreach (var x in t.AreaUpgrade.Datas) CheckFormatHole(AreaUpgradeData.Table, x.id, x.valueFormat, r);
            foreach (var x in t.RebirthPerk.Datas) CheckFormatHole(RebirthPerkData.Table, x.id, x.valueFormat, r);
        }

        static void CheckFormatHole(string table, int id, string format, MvValidationReport r)
        {
            int n = 0, at = 0;
            while (format != null && (at = format.IndexOf("{0}", at, StringComparison.Ordinal)) >= 0) { n++; at += 3; }
            if (n != 1) r.Error(table, id, "valueFormat", $"`{{0}}`가 정확히 1개여야 합니다(지금 {n}개)");
        }

        // ---------------- 짝 값 ----------------

        static void CheckPairValues(MvTables t, MvValidationReport r)
        {
            var climb = t.Climb.Config();
            if (climb != null && TryConfig(t, RunConfigKeys.FlyCard_RemoveMs, out double flyCard) && Math.Abs(climb["rlFlyMs"] - flyCard) > 1e-9)
                r.Warn(ClimbData.Table, climb.id, "rlFlyMs", $"rlFlyMs {climb["rlFlyMs"]} ≠ 300_runConfig flyCard.removeMs {flyCard}");

            var gate = t.ReceiveCondition.ByKey("sgatePass");
            if (gate != null && TryConfig(t, RunConfigKeys.Fly_RemoveMs, out double fly) && Math.Abs(gate.delayMs - fly) > 1e-9)
                r.Warn(ReceiveConditionData.Table, gate.id, "delayMs", $"sgatePass delayMs {gate.delayMs} ≠ 300_runConfig fly.removeMs {fly}");
        }

        // ================= 801~804 =================

        static void CheckLoot(MvTables t, MvValidationReport r)
        {
            CheckLootTable(t, t.Drop, DropData.Table, "dropGroup", r);
            CheckLootTable(t, t.RandomBox, RandomBoxData.Table, "group", r);
            foreach (var e in MvDropGraph.FindGraphErrors(t))
                r.Error(e.Table, e.RowId, "rewardId", e.Message);

            foreach (var g in t.Guarantee.Datas)
            {
                const string name = GuaranteeData.Table;
                if (g.triggerCount < 1) r.Error(name, g.id, "triggerCount", "triggerCount는 1 이상이어야 합니다");
                if (g.countType != 1 && g.countType != 2) r.Error(name, g.id, "countType", $"countType {g.countType}는 1(FailStreak)·2(AttemptCycle)가 아닙니다");
                if (g.rewardMode != 1 && g.rewardMode != 2) r.Error(name, g.id, "rewardMode", $"rewardMode {g.rewardMode}는 1(Replace)·2(Add)가 아닙니다");
                if (g.rewardBoxSubGroup != 0 && !t.RandomBox.GetGroup(g.rewardBoxGroup).Any(x => x.subGroup == g.rewardBoxSubGroup))
                    r.Error(name, g.id, "rewardBoxSubGroup", $"802 상자 {g.rewardBoxGroup}에 칸 {g.rewardBoxSubGroup}가 없습니다");
            }
            foreach (var l in t.GuaranteeLink.Datas)
                if (l.sourceBoxSubGroup != 0 && !t.RandomBox.GetGroup(l.sourceBoxGroup).Any(x => x.subGroup == l.sourceBoxSubGroup))
                    r.Error(GuaranteeLinkData.Table, l.id, "sourceBoxSubGroup", $"802 상자 {l.sourceBoxGroup}에 칸 {l.sourceBoxSubGroup}가 없습니다");
        }

        static void CheckLootTable<T>(MvTables t, MvTable<T> table, string name, string groupColumn, MvValidationReport r) where T : LootRowData
        {
            foreach (var g in table.Groups)
            {
                var rows = table.GetGroup(g);
                int first = rows.Min(x => x.id);
                if (g != first) r.Error(name, first, groupColumn, $"{groupColumn} {g}가 묶음 첫 행 id {first}와 다릅니다");
                foreach (var slot in rows.GroupBy(x => x.Slot))
                {
                    long sum = slot.Sum(x => (long)Math.Max(0, x.weight));
                    string where = typeof(T) == typeof(RandomBoxData) ? $"상자 {g} 칸 {slot.Key}" : $"묶음 {g}";
                    if (sum <= 0) r.Error(name, first, "weight", $"{where}의 weight 합이 0입니다");
                    else if (sum != 10000) r.Warn(name, first, "weight", $"{where}의 weight 합이 {sum}입니다(10000 권장)");
                }
            }
            foreach (var x in table.Datas)
            {
                if (x.weight < 0) r.Error(name, x.id, "weight", "weight는 0 이상 정수여야 합니다");
                if (x is RandomBoxData box && box.subGroup < 1) r.Error(name, x.id, "subGroup", "subGroup은 1 이상이어야 합니다");
                var rule = x is DropData d ? d.rewardValueRule : RewardValueRule.Flat;
                if (x.IsEmpty)
                {
                    if (x.rewardId != 0 || x.rewardValueMin != 0 || x.rewardValueMax != 0)
                        r.Error(name, x.id, "rewardId", "꽝 행(rewardType 0)은 rewardId·rewardValue_min·_max가 모두 0이어야 합니다");
                    continue;
                }
                if (rule != RewardValueRule.Flat)
                {
                    if (x.rewardValueMin != 0 || x.rewardValueMax != 0)
                        r.Error(name, x.id, "rewardValue_min", $"rewardValueRule {rule}이면 rewardValue_min·_max는 0이어야 합니다");
                }
                else if (x.rewardValueMin <= 0 || x.rewardValueMin > x.rewardValueMax)
                    r.Error(name, x.id, "rewardValue_min", $"0 < rewardValue_min ≤ rewardValue_max여야 합니다(지금 {x.rewardValueMin}~{x.rewardValueMax})");
                CheckConsumeRef(t, name, x.id.ToString(C), "rewardType", x.rewardType, x.rewardId, true, r);
            }
        }
    }
}
