using System;
using System.Collections.Generic;
using System.Linq;

namespace MV.Data
{
    // 4xx 메타 시스템 표 의미 규칙(08 §8.5.2~8.5.11, §8.3.5). FK·consumeData·형식은 Schema 선언으로 공통 검증기가 본다.
    public static partial class MvTableValidator
    {
        static void CheckMetaSystems(MvTables t, MvValidationReport r)
        {
            var covered = new HashSet<(int group, int from, int to)>();
            CheckPets(t, r, covered);
            CheckPetBuild(t, r);
            CheckResearch(t, r);
            CheckFriends(t, r, covered);
            CheckRunes(t, r);
            CheckGear(t, r, covered);
            CheckBlessings(t, r);
            CheckAttendance(t, r);
            CheckRoadMap(t, r);
            CheckGodLevels(t, r, covered);
        }

        /// <summary>16_levelPrice 묶음이 targetLevel from~to를 빈틈없이 덮는지. 같은 (묶음, 범위)는 한 번만 본다. 묶음이 없으면 FK 오류가 따로 난다.</summary>
        static void CheckLevelPriceRange(MvTables t, MvValidationReport r, HashSet<(int, int, int)> covered,
            string table, int id, int group, int from, int to)
        {
            if (to < from || !t.LevelPrice.HasGroup(group) || !covered.Add((group, from, to))) return;
            var have = new HashSet<int>(t.LevelPrice.GetGroup(group).Select(x => x.targetLevel));
            var missing = Enumerable.Range(from, to - from + 1).Where(l => !have.Contains(l)).ToList();
            if (missing.Count > 0)
                r.Error(table, id, "levelPriceGroup",
                    $"16_levelPrice 묶음 {group}에 targetLevel {string.Join(",", missing.Take(5))}{(missing.Count > 5 ? "…" : "")}가 없습니다({from}~{to})");
        }

        // ---------------- 430 ----------------

        static void CheckPets(MvTables t, MvValidationReport r, HashSet<(int, int, int)> covered)
        {
            const string name = PetData.Table;
            foreach (var p in t.Pet.Datas.OrderBy(x => x.id))
            {
                if (p.rarity < 0 || p.rarity > 4) r.Error(name, p.id, "rarity", $"rarity {p.rarity}는 0~4 밖입니다");
                if (p.skillEveryMs <= 0) r.Error(name, p.id, "skillEveryMs", "skillEveryMs는 1 이상이어야 합니다");
                if (p.skillTargets < 1) r.Error(name, p.id, "skillTargets", "skillTargets는 1 이상이어야 합니다");
                CheckLevelPriceRange(t, r, covered, name, p.id, p.levelPriceGroup, 2, PetData.MaxLevel);
            }
        }

        // ---------------- 431 ----------------

        static void CheckPetBuild(MvTables t, MvValidationReport r)
        {
            const string name = PetBuildData.Table;
            var rows = t.PetBuild.Datas.OrderBy(x => x.id).ToList();
            foreach (var b in rows)
            {
                if (b.tier < 0 || b.tier > 3) r.Error(name, b.id, "tier", $"tier {b.tier}는 0~3 밖입니다");
                if (b.valueMin < 0 || b.valueMin > b.valueMax)
                    r.Error(name, b.id, "valueMin", $"0 ≤ valueMin ≤ valueMax여야 합니다(지금 {b.valueMin}~{b.valueMax})");
                if (b.tierRate < 0) r.Error(name, b.id, "tierRate", "tierRate는 0 이상이어야 합니다");
            }
            if (rows.Count == 0) return;

            long sum = 0;
            for (int tier = 0; tier <= 3; tier++)
            {
                var inTier = rows.Where(x => x.tier == tier).ToList();
                if (inTier.Count == 0) { r.Error(name, null, "tier", $"tier {tier} 행이 없습니다"); continue; }
                int rate = inTier[0].tierRate;
                foreach (var b in inTier.Skip(1).Where(x => x.tierRate != rate))
                    r.Error(name, b.id, "tierRate", $"tier {tier}의 tierRate가 행마다 다릅니다({rate} ≠ {b.tierRate})");
                foreach (var dup in inTier.GroupBy(x => x.statKey).Where(g => g.Count() > 1))
                    r.Error(name, dup.Skip(1).First().id, "statKey", $"tier {tier}에 statKey {dup.Key}가 두 번 있습니다");
                sum += Math.Max(0, rate);
            }
            if (sum <= 0) r.Error(name, null, "tierRate", "tierRate 합이 0입니다");
            else if (sum != 10000) r.Warn(name, null, "tierRate", $"tier별 tierRate 합이 {sum}입니다(10000 권장)");
        }

        // ---------------- 440 ----------------

        static void CheckResearch(MvTables t, MvValidationReport r)
        {
            const string name = ResearchData.Table;
            foreach (var g in t.Research.Groups.OrderBy(x => x))
            {
                var rows = t.Research.GetGroup(g).OrderBy(x => x.level).ToList();
                for (int i = 0; i < rows.Count; i++)
                {
                    var x = rows[i];
                    if (x.level != i + 1)
                    {
                        r.Error(name, x.id, "level", $"계열 {g}의 level은 1부터 연속이어야 합니다({i + 1}이 와야 하는데 {x.level})");
                        break;
                    }
                    if (x.statType != rows[0].statType)
                        r.Error(name, x.id, "statType", $"계열 {g}의 statType이 행마다 다릅니다({rows[0].statType} ≠ {x.statType})");
                    if (i > 0 && x.statValue < rows[i - 1].statValue)
                        r.Error(name, x.id, "statValue", $"statValue는 누적값이라 줄면 안 됩니다(level {x.level - 1} {rows[i - 1].statValue} > level {x.level} {x.statValue})");
                }
            }
            foreach (var x in t.Research.Datas)
                if (x.seconds <= 0) r.Error(name, x.id, "seconds", "seconds는 1 이상이어야 합니다");
        }

        // ---------------- 450·451 ----------------

        static void CheckFriends(MvTables t, MvValidationReport r, HashSet<(int, int, int)> covered)
        {
            const string name = FriendData.Table;
            int topLevel = 0;
            foreach (var f in t.Friend.Datas.OrderBy(x => x.id))
            {
                if (f.rarity < 0) r.Error(name, f.id, "rarity", "rarity는 0 이상이어야 합니다");
                if (f.maxLevel < 1) r.Error(name, f.id, "maxLevel", "maxLevel은 1 이상이어야 합니다");
                topLevel = Math.Max(topLevel, f.maxLevel);
                if (f.unlockPrice.Count == 0) r.Error(name, f.id, "unlockPrice", "영입 비용(unlockPrice)이 비었습니다");
                if (f.Trigger != FriendSkillTrigger.Hits && f.Trigger != FriendSkillTrigger.Chance && f.Trigger != FriendSkillTrigger.Seconds)
                    r.Error(name, f.id, "skillTrigger", $"skillTrigger {f.skillTrigger}는 1(Hits)·2(Chance)·3(Seconds)가 아닙니다");
                if (f.skillEvery <= 0) r.Error(name, f.id, "skillEvery", "skillEvery는 1 이상이어야 합니다");
                else if (f.Trigger == FriendSkillTrigger.Chance && f.skillEvery > 100)
                    r.Error(name, f.id, "skillEvery", $"Chance면 skillEvery는 확률 %라 100 이하여야 합니다(지금 {f.skillEvery})");
                if (f.skillTargets < 1) r.Error(name, f.id, "skillTargets", "skillTargets는 1 이상이어야 합니다");
                CheckLevelPriceRange(t, r, covered, name, f.id, f.levelPriceGroup, 2, f.maxLevel);
            }

            const string power = FriendPowerData.Table;
            int prev = 0;
            foreach (var p in t.FriendPower.Datas.OrderBy(x => x.id))
            {
                if (p.unlockLevel < 1 || (topLevel > 0 && p.unlockLevel > topLevel))
                    r.Error(power, p.id, "unlockLevel", $"unlockLevel {p.unlockLevel}는 1~{topLevel}(450 maxLevel) 밖입니다");
                else if (p.unlockLevel <= prev)
                    r.Error(power, p.id, "unlockLevel", $"우정 단계는 id 순으로 unlockLevel이 커져야 합니다({prev} 다음 {p.unlockLevel})");
                prev = Math.Max(prev, p.unlockLevel);
                if (p.price.Count == 0) r.Error(power, p.id, "price", "구매 비용(price)이 비었습니다");
            }
        }

        // ---------------- 460 ----------------

        static void CheckRunes(MvTables t, MvValidationReport r)
        {
            foreach (var x in t.Rune.Datas)
            {
                var box = t.RandomBox.Get(x.randomBoxRowId);
                if (box != null && box.Type != ConsumeType.Pass)
                    r.Error(RuneData.Table, x.id, "randomBoxRowId", $"802 행 {x.randomBoxRowId}의 rewardType {box.rewardType}가 500(Pass)이 아닙니다");
            }
        }

        // ---------------- 470·471 ----------------

        static void CheckGear(MvTables t, MvValidationReport r, HashSet<(int, int, int)> covered)
        {
            foreach (var dup in t.GearPart.Datas.OrderBy(x => x.id).GroupBy(x => x.statKey).Where(g => g.Count() > 1))
                r.Warn(GearPartData.Table, dup.Skip(1).First().id, "statKey", $"statKey {dup.Key}를 부위 여러 개가 씁니다");

            const string name = GearRarityData.Table;
            var rows = t.GearRarity.Datas.OrderBy(x => x.rarity).ThenBy(x => x.id).ToList();
            for (int i = 0; i < rows.Count; i++)
            {
                var x = rows[i];
                if (x.rarity != i)
                {
                    r.Error(name, x.id, "rarity", $"rarity는 0부터 연속이고 겹치지 않아야 합니다({i}이 와야 하는데 {x.rarity})");
                    break;
                }
                if (i > 0 && x.value <= rows[i - 1].value)
                    r.Warn(name, x.id, "value", $"value가 희귀도 순으로 커지지 않습니다({rows[i - 1].value} → {x.value})");
            }
            foreach (var x in rows)
            {
                if (x.sellPrice.Count == 0) r.Error(name, x.id, "sellPrice", "판매 가격(sellPrice)이 비었습니다");
                CheckLevelPriceRange(t, r, covered, name, x.id, x.levelPriceGroup, 1, GearRarityData.MaxLevel);
            }
        }

        // ---------------- 480 ----------------

        static void CheckBlessings(MvTables t, MvValidationReport r)
        {
            const string name = BlessingData.Table;
            bool stamina = t.PlayResource.Datas.Any(p => p.type == "stamina" && p.active);
            foreach (var b in t.Blessing.Datas)
            {
                if (b.effectType < 1 || b.effectType > 4)
                    r.Error(name, b.id, "effectType", $"effectType {b.effectType}는 1(부활)·2(일일)·3(스태미나)·4(능력치)가 아닙니다");
                if (b.adRuns < 1) r.Error(name, b.id, "adRuns", "adRuns는 1 이상이어야 합니다");
                if (b.active && b.EffectType == BlessingEffectType.Stamina && !stamina)
                    r.Error(name, b.id, "active", "스태미나 축복이 켜져 있지만 active인 02_playResource stamina 행이 없습니다(D-79)");
            }
        }

        // ---------------- 485 ----------------

        static void CheckAttendance(MvTables t, MvValidationReport r)
        {
            const string name = AttendanceData.Table;
            var seen = new HashSet<int>();
            foreach (var a in t.Attendance.Datas.OrderBy(x => x.id))
            {
                if (a.day < 1 || a.day > AttendanceData.CycleDays)
                    r.Error(name, a.id, "day", $"day {a.day}는 1~{AttendanceData.CycleDays} 밖입니다");
                else if (!seen.Add(a.day)) r.Error(name, a.id, "day", $"day {a.day}가 중복됩니다");
                if (a.reward.Count == 0) r.Error(name, a.id, "reward", "보상이 비었습니다");
            }
            for (int d = 1; d <= AttendanceData.CycleDays; d++)
                if (!seen.Contains(d)) r.Error(name, null, "day", $"day {d} 행이 없습니다");
        }

        // ---------------- 490 ----------------

        static void CheckRoadMap(MvTables t, MvValidationReport r)
        {
            const string name = RoadMapData.Table;
            var zones = new HashSet<int>();
            foreach (var x in t.RoadMap.Datas.OrderBy(m => m.id))
            {
                if (!zones.Add(x.zoneId)) r.Error(name, x.id, "zoneId", $"zoneId {x.zoneId} 행이 두 개입니다");
                if (x.gainPerRun < 0) r.Error(name, x.id, "gainPerRun", "gainPerRun은 0 이상이어야 합니다");
                if (x.maxRange <= 0) r.Error(name, x.id, "maxRange", "maxRange는 1 이상이어야 합니다");
                if (x.catchUpGain < 0) r.Error(name, x.id, "catchUpGain", "catchUpGain은 0 이상이어야 합니다");
                if (x.startGap < 0) r.Error(name, x.id, "startGap", "startGap은 0 이상이어야 합니다");
            }
            if (zones.Count == 0) return;
            foreach (var z in t.Zone.Datas.OrderBy(x => x.id))
                if (!zones.Contains(z.id)) r.Warn(name, null, "zoneId", $"Zone {z.id} 행이 없습니다");
        }

        // ---------------- 495 ----------------

        static void CheckGodLevels(MvTables t, MvValidationReport r, HashSet<(int, int, int)> covered)
        {
            const string name = GodLevelData.Table;
            var rows = t.GodLevel.Datas.OrderBy(x => x.level).ThenBy(x => x.id).ToList();
            for (int i = 0; i < rows.Count; i++)
            {
                var x = rows[i];
                if (x.level != i + 1)
                {
                    r.Error(name, x.id, "level", $"level은 1부터 연속이고 겹치지 않아야 합니다({i + 1}이 와야 하는데 {x.level})");
                    return;
                }
                if (x.passiveMul < 0) r.Error(name, x.id, "passiveMul", "passiveMul은 0 이상이어야 합니다");
                if (i > 0 && x.dmgPct < rows[i - 1].dmgPct)
                    r.Warn(name, x.id, "dmgPct", $"dmgPct가 레벨 순으로 줄어듭니다({rows[i - 1].dmgPct} → {x.dmgPct})");
            }
            if (rows.Count == 0) return;
            int maxLevel = rows[rows.Count - 1].level;
            foreach (var g in rows.GroupBy(x => x.levelPriceGroup))
                CheckLevelPriceRange(t, r, covered, name, g.First().id, g.Key, 2, maxLevel);
        }
    }
}
