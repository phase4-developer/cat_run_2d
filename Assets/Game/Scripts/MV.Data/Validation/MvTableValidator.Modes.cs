using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace MV.Data
{
    // 400~402 모드, 410~420 퀘스트, 499 콘텐츠 상수, 700~702 상점 규칙(08 §8.4, §8.5.1, §8.5.6, §8.5.7, §8.5.13).
    public static partial class MvTableValidator
    {
        /// <summary>contentType → IModeRule 구현(08 §8.4.2·8.4.4).</summary>
        static readonly Dictionary<ContentType, string> s_ruleOfContent = new Dictionary<ContentType, string>
        {
            [ContentType.Defence] = ModeRuleData.DefenceRule,
            [ContentType.FixedLoadout] = ModeRuleData.FixedLoadoutRule,
            [ContentType.Course] = ModeRuleData.CourseRule,
            [ContentType.Boss] = ModeRuleData.BossRule,
        };

        static void CheckModesAndShop(MvTables t, MvValidationReport r)
        {
            CheckContents(t, r);
            CheckModeRules(t, r);
            CheckModeStages(t, r);
            CheckQuests(t, r);
            CheckContentConfig(t, r);
            CheckShop(t, r);
        }

        // ---------------- 400 ----------------

        static void CheckContents(MvTables t, MvValidationReport r)
        {
            const string name = ContentData.Table;
            int mains = t.Content.Datas.Count(c => c.contentType == ContentType.Main);
            if (mains != 1) r.Error(name, null, "contentType", $"본편(contentType 0) 행이 정확히 1개여야 합니다(지금 {mains}개)");
            foreach (var c in t.Content.Datas)
            {
                if (!Enum.IsDefined(typeof(ContentType), c.contentType))
                {
                    r.Error(name, c.id, "contentType", $"contentType {(int)c.contentType}는 0~4가 아닙니다");
                    continue;
                }
                if (c.contentType == ContentType.Main) continue;
                var rule = t.ModeRule.Get(c.modeRuleId);
                if (rule == null) r.Error(name, c.id, "modeRuleId", "모드 행은 modeRuleId가 있어야 합니다");
                else if (rule.ruleClass != s_ruleOfContent[c.contentType])
                    r.Error(name, c.id, "modeRuleId", $"contentType {c.contentType}에는 {s_ruleOfContent[c.contentType]}가 와야 합니다(402 {rule.id}는 {rule.ruleClass})");
                if (!t.ModeStage.HasGroup(c.stageGroup)) r.Error(name, c.id, "stageGroup", $"401_modeStage에 묶음 {c.stageGroup}가 없습니다");
                if (c.openId != 0 && t.ContentOpen.Get(c.openId) is { } open && open.contentId != 0 && open.contentId != c.id)
                    r.Error(name, c.id, "openId", $"05_contentOpen {open.id}의 contentId {open.contentId}가 이 행과 다릅니다");
                if (c.spendResource.Count > 1) r.Error(name, c.id, "spendResourceType", "입장 비용은 1항목이어야 합니다");
                if (c.active && c.sweepActive && c.spendResource.Count == 0)
                    r.Warn(name, c.id, "sweepActive", "입장 비용 없이 소탕이 켜져 있습니다");
            }
        }

        // ---------------- 402 ----------------

        static void CheckModeRules(MvTables t, MvValidationReport r)
        {
            const string name = ModeRuleData.Table;
            int tiers = t.CardTier.Datas.Count;
            int weaponTiers = t.Item.Datas.Count == 0 ? 0 : t.Item.Datas.Max(i => i.tier);
            foreach (var m in t.ModeRule.Datas)
            {
                if (!s_ruleOfContent.ContainsValue(m.ruleClass))
                    r.Error(name, m.id, "ruleClass", $"ruleClass `{m.ruleClass}`는 IModeRule 구현 이름이 아닙니다({string.Join("/", s_ruleOfContent.Values)})");
                if (!Enum.IsDefined(typeof(ModeLayout), m.layout)) r.Error(name, m.id, "layout", $"layout {(int)m.layout}는 0~2가 아닙니다");
                if (!Enum.IsDefined(typeof(ModeHpSource), m.hpSource)) r.Error(name, m.id, "hpSource", $"hpSource {(int)m.hpSource}는 0·1이 아닙니다");
                if (!Enum.IsDefined(typeof(ModeRunReward), m.runRewardToAccount))
                    r.Error(name, m.id, "runRewardToAccount", $"runRewardToAccount {(int)m.runRewardToAccount}는 0~2가 아닙니다");
                if (m.fixedWeaponTier < 0 || m.fixedWeaponTier > weaponTiers)
                    r.Error(name, m.id, "fixedWeaponTier", $"fixedWeaponTier {m.fixedWeaponTier}가 0~{weaponTiers}(103 등급) 밖입니다");
                if (m.craftPerMade < 1) r.Error(name, m.id, "craftPerMade", "craftPerMade는 1 이상이어야 합니다");
                if (m.cycleGroup != 0 && !t.TemplateCycle.Datas.Any(c => c.cycleGroup == m.cycleGroup))
                    r.Error(name, m.id, "cycleGroup", $"312_templateCycle에 cycleGroup {m.cycleGroup}가 없습니다");
                if (m.layout != ModeLayout.Arena && m.forwardSpeed <= 0) r.Error(name, m.id, "forwardSpeed", "아레나가 아니면 forwardSpeed > 0이어야 합니다");
                if (m.layout == ModeLayout.Arena)
                {
                    if (m.arenaLaneX.Count == 0) r.Error(name, m.id, "arenaLaneX", "아레나는 레인 x 목록이 있어야 합니다");
                    if (m.arenaSpawnZ <= m.arenaLineZ) r.Error(name, m.id, "arenaSpawnZ", $"arenaSpawnZ {m.arenaSpawnZ}가 방어선 arenaLineZ {m.arenaLineZ}보다 커야 합니다");
                    if (m.arenaJitter < 0) r.Error(name, m.id, "arenaJitter", "arenaJitter는 0 이상이어야 합니다");
                }
                if (m.ruleClass == ModeRuleData.DefenceRule)
                {
                    if (m.baseHpMax < 1) r.Error(name, m.id, "baseHpMax", "DefenceRule은 baseHpMax ≥ 1이어야 합니다");
                    if (m.xpPerKill < 1 || m.xpBase < 1 || m.xpStep < 0) r.Error(name, m.id, "xpBase", "DefenceRule은 xpPerKill ≥ 1, xpBase ≥ 1, xpStep ≥ 0이어야 합니다");
                    if (m.cardChoices < 1) r.Error(name, m.id, "cardChoices", "DefenceRule은 cardChoices ≥ 1이어야 합니다");
                    if (m.cardTierWeight.Count != tiers || m.cardTierWeight.Any(w => w < 0) || m.cardTierWeight.Sum() <= 0)
                        r.Error(name, m.id, "cardTierWeight", $"cardTierWeight는 115_cardTier 등급 수 {tiers}개, 0 이상, 합 > 0이어야 합니다(지금 {m.cardTierWeight.Count}개)");
                }
                if (!t.Content.Datas.Any(c => c.modeRuleId == m.id)) r.Warn(name, m.id, "id", "어느 400_content도 쓰지 않는 규칙입니다");
            }
        }

        // ---------------- 401 ----------------

        static void CheckModeStages(MvTables t, MvValidationReport r)
        {
            const string name = ModeStageData.Table;
            foreach (var g in t.ModeStage.Groups)
            {
                var rows = t.ModeStage.GetGroup(g).OrderBy(x => x.stage).ToList();
                for (int i = 0; i < rows.Count; i++)
                    if (rows[i].stage != i)
                    {
                        r.Error(name, rows[i].id, "stage", $"묶음 {g}의 stage는 0부터 연속이어야 합니다({i}가 와야 하는데 {rows[i].stage})");
                        break;
                    }
                var content = t.Content.Datas.FirstOrDefault(c => c.stageGroup == g && c.contentType != ContentType.Main);
                if (content == null) { r.Warn(name, rows[0].id, "group", $"묶음 {g}를 쓰는 400_content가 없습니다"); continue; }
                var rule = t.ModeRule.Get(content.modeRuleId);
                if (rule == null) continue; // 400 규칙에서 오류.
                foreach (var s in rows) CheckStageColumns(t, rule, s, r);
            }
            foreach (var s in t.ModeStage.Datas)
            {
                int expect = 401000000 + s.group * 1000 + s.stage + 1;
                if (s.id != expect) r.Error(name, s.id, "id", $"id는 401{{group 3}}{{stage+1 3}} = {expect}이어야 합니다");
                if (s.clearReward.Count == 0) r.Error(name, s.id, "clearRewardType", "클리어 보상이 비어 있습니다");
            }
        }

        static void CheckStageColumns(MvTables t, ModeRuleData rule, ModeStageData s, MvValidationReport r)
        {
            const string name = ModeStageData.Table;
            void Need(bool ok, string column) { if (!ok) r.Error(name, s.id, column, $"{rule.ruleClass} 단계 행에 {column}이 필요합니다(> 0)"); }
            switch (rule.ruleClass)
            {
                case ModeRuleData.DefenceRule:
                    Need(s.survivalSec > 0, "survivalSec"); Need(s.minKills > 0, "minKills"); Need(s.maxAlive > 0, "maxAlive");
                    Need(s.spawnMs > 0, "spawnMs"); Need(s.enemyHp > 0, "enemyHp"); Need(s.enemySpeed > 0, "enemySpeed");
                    Need(s.boxMs > 0, "boxMs"); Need(s.boxHp > 0, "boxHp"); Need(s.enemyId > 0, "enemyId");
                    break;
                case ModeRuleData.BossRule:
                    Need(s.bossHp > 0, "bossHp"); Need(s.bossSpeed > 0, "bossSpeed"); Need(s.summonMs > 0, "summonMs");
                    Need(s.summonCount > 0, "summonCount"); Need(s.boxMs > 0, "boxMs"); Need(s.enemyId > 0, "enemyId");
                    break;
                case ModeRuleData.FixedLoadoutRule:
                    Need(s.templateId > 0, "templateId"); Need(s.floorGroup > 0, "floorGroup"); Need(s.hpMul > 0, "hpMul"); Need(s.baseHp > 0, "baseHp");
                    break;
                case ModeRuleData.CourseRule:
                    Need(s.templateId > 0, "templateId"); Need(s.hpMul > 0, "hpMul"); Need(s.rewardMul > 0, "rewardMul"); Need(s.baseHp > 0, "baseHp");
                    break;
            }
            if (s.floorGroup != 0 && rule.layout != ModeLayout.Main)
                r.Warn(name, s.id, "floorGroup", $"layout {rule.layout}는 Floor를 쓰지 않는데 floorGroup이 있습니다");
        }

        // ---------------- 410·413·414·420 ----------------

        static void CheckQuests(MvTables t, MvValidationReport r)
        {
            const string groupName = QuestGroupData.Table;
            foreach (var g in t.QuestGroup.Datas)
            {
                if (t.QuestGroup.GetGroup(g.group).Count > 1) r.Error(groupName, g.id, "group", $"group {g.group}가 중복됩니다");
                if (g.id != 410001000 + g.group) r.Error(groupName, g.id, "id", $"id는 410001000 + group = {410001000 + g.group}이어야 합니다");
                if (!Enum.IsDefined(typeof(QuestPeriodType), g.periodType)) r.Error(groupName, g.id, "periodType", $"periodType {(int)g.periodType}는 0·2·3·5가 아닙니다");
                if (!Enum.IsDefined(typeof(QuestCategory), g.category)) r.Error(groupName, g.id, "category", $"category {(int)g.category}는 1·2·3·100이 아닙니다");
                if (!Enum.IsDefined(typeof(QuestGroupType), g.groupType)) r.Error(groupName, g.id, "groupType", $"groupType {(int)g.groupType}는 1~3이 아닙니다");
                if (!Enum.IsDefined(typeof(QuestProgressScope), g.progressScope)) r.Error(groupName, g.id, "progressScope", $"progressScope {(int)g.progressScope}는 1·2가 아닙니다");
            }

            foreach (var q in t.RepeatQuest.Datas)
            {
                var g = CheckQuestRow(t, RepeatQuestData.Table, q, r, QuestCategory.Daily, QuestCategory.Monthly);
                if (g != null && q.periodType != g.periodType)
                    r.Error(RepeatQuestData.Table, q.id, "periodType", $"periodType {(int)q.periodType}가 410 묶음 {g.group}의 {(int)g.periodType}와 다릅니다");
                if (q.clearValue < 1) r.Error(RepeatQuestData.Table, q.id, "clearValue", "clearValue는 1 이상이어야 합니다");
                if (q.clearValueStep < 0) r.Error(RepeatQuestData.Table, q.id, "clearValueStep", "clearValueStep은 0 이상이어야 합니다");
                if (g != null && g.periodType == QuestPeriodType.Daily && q.clearValueStep != 0)
                    r.Error(RepeatQuestData.Table, q.id, "clearValueStep", "일일 퀘스트는 반복하지 않으므로 clearValueStep 0이어야 합니다");
            }
            // 일일 종합 목표는 같은 날 얻을 수 있는 일일 점수 합 이하(08 §8.5.1 「합 120 = 전부 완료」).
            long dailyPoints = t.RepeatQuest.Datas.Where(q => q.periodType == QuestPeriodType.Daily)
                .Sum(q => q.reward.Where(x => x.TypeId == (int)ConsumeType.DailyQuestPoint).Sum(x => (long)x.Value));
            foreach (var q in t.RepeatQuest.Datas.Where(q => q.clearType == QuestClearType.DailyPoint && q.clearValue > dailyPoints))
                r.Error(RepeatQuestData.Table, q.id, "clearValue", $"일일 점수 목표 {q.clearValue}가 하루 최대 점수 {dailyPoints}보다 큽니다");

            foreach (var q in t.PassQuest.Datas)
            {
                var g = CheckQuestRow(t, PassQuestData.Table, q, r, QuestCategory.Pass);
                if (g != null && q.periodType != g.periodType)
                    r.Error(PassQuestData.Table, q.id, "periodType", $"periodType {(int)q.periodType}가 410 묶음 {g.group}의 {(int)g.periodType}와 다릅니다");
                if (q.id != 414000000 + q.questGroup * 1000 + q.step) r.Error(PassQuestData.Table, q.id, "id", "id는 414{group 3}{step 3}이어야 합니다");
            }
            CheckSteps(t.PassQuest.Datas, PassQuestData.Table, "step", x => x.step, r);

            foreach (var a in t.Achievement.Datas)
            {
                CheckQuestRow(t, AchievementData.Table, a, r, QuestCategory.Achievement);
                if (a.id != 420000000 + a.questGroup * 1000 + a.progress) r.Error(AchievementData.Table, a.id, "id", "id는 420{group 3}{progress 3}이어야 합니다");
            }
            CheckSteps(t.Achievement.Datas, AchievementData.Table, "progress", x => x.progress, r);
            foreach (var g in t.Achievement.Datas.GroupBy(a => a.questGroup))
            {
                long prev = 0;
                foreach (var a in g.OrderBy(x => x.progress))
                {
                    if (a.prevClearValue != prev) r.Error(AchievementData.Table, a.id, "prevClearValue", $"prevClearValue는 앞 단계 clearValue {prev}여야 합니다");
                    prev = a.clearValue;
                }
            }
        }

        static QuestGroupData CheckQuestRow(MvTables t, string table, QuestRowData q, MvValidationReport r, params QuestCategory[] categories)
        {
            if (!Enum.IsDefined(typeof(QuestClearType), q.clearType)) r.Error(table, q.id, "clearType", $"clearType {(int)q.clearType}는 QuestClearType에 없습니다");
            else if (q.clearType == QuestClearType.LobbyUpgrade && (q.clearCondition < 0 || q.clearCondition > 3))
                r.Error(table, q.id, "clearCondition", "lobbyUpgrade clearCondition은 0~3이어야 합니다");
            else if (q.clearType == QuestClearType.ModeClear && q.clearCondition != 0 && !t.Content.Datas.Any(c => (int)c.contentType == q.clearCondition))
                r.Error(table, q.id, "clearCondition", $"modeClear clearCondition {q.clearCondition}인 contentType이 400_content에 없습니다");
            if (q.reward.Count == 0) r.Error(table, q.id, "rewardType", "보상이 비어 있습니다");
            var rows = t.QuestGroup.GetGroup(q.questGroup);
            var g = rows.Count > 0 ? rows[0] : null;
            if (g != null && !categories.Contains(g.category))
                r.Error(table, q.id, "questGroup", $"410 묶음 {g.group}의 category {g.category}는 이 표({string.Join("/", categories)})에 맞지 않습니다");
            return g;
        }

        /// <summary>묶음마다 단계 1부터 연속, clearValue 오름차순.</summary>
        static void CheckSteps<T>(IEnumerable<T> rows, string table, string column, Func<T, int> step, MvValidationReport r) where T : QuestRowData
        {
            foreach (var g in rows.GroupBy(x => x.questGroup))
            {
                var list = g.OrderBy(step).ToList();
                for (int i = 0; i < list.Count; i++)
                {
                    if (step(list[i]) != i + 1) { r.Error(table, list[i].id, column, $"묶음 {g.Key}의 {column}은 1부터 연속이어야 합니다"); break; }
                    if (i > 0 && list[i].clearValue <= list[i - 1].clearValue)
                        r.Error(table, list[i].id, "clearValue", $"clearValue는 {column} 순으로 커져야 합니다({list[i - 1].clearValue} → {list[i].clearValue})");
                }
            }
        }

        // ---------------- 499 ----------------

        static void CheckContentConfig(MvTables t, MvValidationReport r)
        {
            const string name = ContentConfigData.Table;
            var code = new HashSet<string>(ContentConfigKeys.All, StringComparer.Ordinal);
            var groupNames = t.ContentConfig.Datas.Where(x => x.IsGroupName).ToDictionary(x => x.GroupNo, x => x.key);
            foreach (var row in t.ContentConfig.Datas)
            {
                if (row.IsGroupName)
                {
                    if (row.value.Length > 0) r.Error(name, row.id, "value", "묶음 이름 행(순번 000)은 value가 비어야 합니다");
                    continue;
                }
                if (!code.Contains(row.key)) r.Error(name, row.id, "key", $"코드 상수(ContentConfigKeys)에 없는 키 `{row.key}`입니다");
                if (!groupNames.TryGetValue(row.GroupNo, out var group)) r.Error(name, row.id, "id", $"묶음 {row.GroupNo:000}의 이름 행(순번 000)이 없습니다");
                else if (!row.key.StartsWith(group + ".", StringComparison.Ordinal))
                    r.Error(name, row.id, "key", $"키 `{row.key}`가 묶음 이름 `{group}.`으로 시작하지 않습니다");
                if (row.unit == ContentConfigTable.ConsumeUnit)
                {
                    if (!ContentConfigTable.TryConsume(row.value, out var data)) r.Error(name, row.id, "value", $"`{row.value}`는 type/id/value가 아닙니다");
                    else CheckConsumeRef(t, name, row.id.ToString(CultureInfo.InvariantCulture), "value", data.TypeId, data.Id, false, r);
                }
                else if (!IsNumber(row.value)) r.Error(name, row.id, "value", $"`{row.value}`는 숫자가 아닙니다");
            }
            foreach (var key in ContentConfigKeys.All)
                if (!t.ContentConfig.Has(key)) r.Error(name, null, "key", $"코드 상수 `{key}`의 행이 없습니다");
        }

        // ---------------- 700·701·702 ----------------

        static void CheckShop(MvTables t, MvValidationReport r)
        {
            var seen = new Dictionary<int, int>();
            foreach (var s in t.ShopSection.Datas)
                foreach (var tab in s.tabIds)
                {
                    if (seen.TryGetValue(tab, out int other)) r.Error(ShopSectionData.Table, s.id, "tabIds", $"탭 {tab}가 칸 {other}에도 있습니다");
                    else seen.Add(tab, s.id);
                }
            foreach (var tab in t.ProductTab.Datas)
                if (!seen.ContainsKey(tab.id)) r.Error(ProductTabData.Table, tab.id, "id", "어느 702_shopSection에도 들어 있지 않은 탭입니다");

            const string name = ProductData.Table;
            bool adProduct = false;
            foreach (var p in t.Product.Datas)
            {
                if (p.id / 1000 % 1000 != p.tabId % 1000) r.Error(name, p.id, "tabId", $"id 가운데 3자리가 탭 {p.tabId} 순번과 다릅니다");
                if (!Enum.IsDefined(typeof(ProductChargeType), p.chargeType)) r.Error(name, p.id, "chargeType", $"chargeType {(int)p.chargeType}는 0~2가 아닙니다");
                else if (p.chargeType != ProductChargeType.Unlimited && p.chargedQuantity < 1)
                    r.Error(name, p.id, "chargedQuantity", "구매 제한이 있으면 chargedQuantity ≥ 1이어야 합니다");
                if (p.reward.Count == 0) r.Error(name, p.id, "rewardType", "보상이 비어 있습니다");
                if (p.price.Count != 1) { r.Error(name, p.id, "priceType", "가격은 정확히 1항목이어야 합니다"); continue; }
                var type = p.PriceType;
                if (type != ConsumeType.Wealth && type != ConsumeType.AD && type != ConsumeType.Inapp)
                    r.Error(name, p.id, "priceType", $"priceType {(int)type}는 1 Wealth·100 AD·101 Inapp이 아닙니다");
                if (type == ConsumeType.AD && p.active) adProduct = true;
                foreach (var sku in new[] { ("googleProductId", p.googleProductId), ("appleProductId", p.appleProductId) })
                    if (sku.Item2.Length > 0 && (type != ConsumeType.Inapp || !sku.Item2.StartsWith(ProductData.SkuPrefix, StringComparison.Ordinal)))
                        r.Error(name, p.id, sku.Item1, $"스토어 SKU는 Inapp 상품에만, `{ProductData.SkuPrefix}` 접두로 적습니다");
                if (p.minArea < 0 || p.cooldownHours < 0) r.Error(name, p.id, "minArea", "minArea·cooldownHours는 0 이상이어야 합니다");
            }
            if (adProduct && !t.AdPlacement.HasKey("shopFree"))
                r.Error(AdPlacementData.Table, null, "key", "광고 가격 상품이 있는데 key `shopFree` 행이 없습니다");
        }
    }
}
