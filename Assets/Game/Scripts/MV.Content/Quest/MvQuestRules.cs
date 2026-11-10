using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using jjevol;
using MV.Data;
using MV.Meta;
using MV.Save;
using Newtonsoft.Json.Linq;
using ConsumeData = MV.Data.ConsumeData;

namespace MV.Content
{
    /// <summary>퀘스트 1칸 상태.</summary>
    public enum MvQuestState { Progress, Claimable, Done }

    /// <summary>퀘스트 1칸(413 행 1개, 또는 업적 묶음의 지금 단계).</summary>
    public sealed class MvQuestEntry
    {
        /// <summary>413_repeatQuest 또는 420_achievement id(받기 요청 id).</summary>
        public int Id;
        public int Group;
        public QuestCategory Category;
        public QuestGroupType GroupType;
        public QuestClearType ClearType;
        public int Condition;
        public long Count;
        public long Target;
        /// <summary>업적 앞 단계 값(진행 막대 시작). 그 밖은 0.</summary>
        public long PrevTarget;
        /// <summary>받은 횟수(월간 반복은 여러 번, 그 밖은 0/1).</summary>
        public int Claimed;
        /// <summary>월간 반복(clearValueStep &gt; 0).</summary>
        public bool Repeat;
        /// <summary>업적 단계(1부터)와 단계 수. 그 밖은 0.</summary>
        public int Step;
        public int StepCount;
        public IReadOnlyList<ConsumeData> Reward = Array.Empty<ConsumeData>();
        public MvQuestState State;
    }

    /// <summary>퀘스트 묶음 1개(410 행 1개).</summary>
    public sealed class MvQuestGroupView
    {
        public QuestGroupData Row;
        /// <summary>다음 초기화 시각(ms). 기간 없음이면 0.</summary>
        public long ResetAtMs;
        public readonly List<MvQuestEntry> Entries = new List<MvQuestEntry>();
    }

    /// <summary>
    /// 지금 시각 기준으로 넘긴(roll) <c>mvQuest</c> 값 사본. 기간이 바뀐 묶음은 빈 값이다(읽기만 — 저장은 다음 요청 때).
    /// </summary>
    public sealed class MvQuestSnapshot
    {
        public JObject Daily;
        /// <summary>월간 칸(저장 이름은 <see cref="MvQuestRules.MonthlySlot"/>).</summary>
        public JObject Monthly;
        public JObject Achieve;
    }

    /// <summary>
    /// 퀘스트·일일 종합·업적 규칙(08 §8.5.1). 순수 함수만 둔다 — 상태를 바꾸지 않고 새 <c>mvQuest</c> 값만 계산한다.
    /// 카운터는 평생(achieve)·일일·월간 칸에 동시에 더한다. 일일 초기화는 UTC 0시(D-77 A), 월간은 UTC 매월 1일 0시.
    /// 표 행이 늘면 칸·단계가 그대로 늘어난다(12 §12.6-C): 묶음은 410, 칸은 413, 단계는 420 행에서만 만든다.
    /// </summary>
    public static class MvQuestRules
    {
        public const string DailySlot = "daily";
        /// <summary>월간 칸(09 §9.3.5 <c>mvQuest.monthly</c>, 기간 키 <c>yyyyMM</c>).</summary>
        public const string MonthlySlot = "monthly";
        public const string AchieveSlot = "achieve";

        /// <summary>해금 키(05_contentOpen): 일일·월간은 quest, 업적은 achievement.</summary>
        public const string QuestUnlockKey = "quest";
        public const string AchievementUnlockKey = "achievement";

        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        // ───────── 기간 ─────────

        public static string DayKey(long utcMs) => Utc(utcMs).ToString("yyyy-MM-dd", Inv);
        public static string MonthKey(long utcMs) => Utc(utcMs).ToString("yyyyMM", Inv);

        /// <summary>다음 UTC 0시(ms).</summary>
        public static long NextDayMs(long utcMs) => Ms(Utc(utcMs).Date.AddDays(1));

        /// <summary>다음 달 1일 UTC 0시(ms).</summary>
        public static long NextMonthMs(long utcMs)
        {
            var d = Utc(utcMs);
            return Ms(new DateTime(d.Year, d.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(1));
        }

        static DateTime Utc(long ms) => DateTimeOffset.FromUnixTimeMilliseconds(ms).UtcDateTime;
        static long Ms(DateTime utc) => new DateTimeOffset(utc, TimeSpan.Zero).ToUnixTimeMilliseconds();

        public static long ResetAt(QuestPeriodType period, long nowMs)
            => period == QuestPeriodType.Daily ? NextDayMs(nowMs) : period == QuestPeriodType.Monthly ? NextMonthMs(nowMs) : 0;

        // ───────── 저장 값 읽기 ─────────

        /// <summary>저장 값을 지금 기간으로 넘긴 사본.</summary>
        public static MvQuestSnapshot Read(MvQuestContainer quest, long nowMs)
            => Read(quest != null ? JObject.Parse(quest.ToJsonString()) : null, nowMs);

        /// <summary>빈 저장(새 계정) 기준 사본.</summary>
        public static MvQuestSnapshot Empty(long nowMs) => Read((JObject)null, nowMs);

        public static MvQuestSnapshot Read(JObject quest, long nowMs) => new MvQuestSnapshot
        {
            Daily = Rolled(quest?[DailySlot] as JObject, DayKey(nowMs)),
            Monthly = Rolled(quest?[MonthlySlot] as JObject, MonthKey(nowMs)),
            Achieve = new JObject
            {
                ["cnt"] = (quest?[AchieveSlot]?["cnt"] as JObject)?.DeepClone() ?? new JObject(),
                ["got"] = (quest?[AchieveSlot]?["got"] as JArray)?.DeepClone() ?? new JArray(),
            },
        };

        static JObject Rolled(JObject slot, string period)
        {
            if (slot != null && slot.Value<string>("period") == period)
                return new JObject
                {
                    ["period"] = period,
                    ["cnt"] = (slot["cnt"] as JObject)?.DeepClone() ?? new JObject(),
                    ["got"] = (slot["got"] as JArray)?.DeepClone() ?? new JArray(),
                    ["claims"] = (slot["claims"] as JObject)?.DeepClone() ?? new JObject(),
                };
            return new JObject { ["period"] = period, ["cnt"] = new JObject(), ["got"] = new JArray(), ["claims"] = new JObject() };
        }

        /// <summary>저장 값의 기간(일일·월간)이 지금과 다른지(굴려서 다시 써야 하는지).</summary>
        public static bool Stale(MvQuestContainer quest, long nowMs)
            => quest != null && (quest.Period(DailySlot) != DayKey(nowMs) || quest.Period(MonthlySlot) != MonthKey(nowMs));

        /// <summary>응답 Data의 <c>mvQuest</c> 값(바꾸는 세 칸 전체 — 저장은 최상위 필드 병합).</summary>
        public static JObject Write(MvQuestSnapshot s) => new JObject
        {
            [DailySlot] = s.Daily.DeepClone(),
            [MonthlySlot] = s.Monthly.DeepClone(),
            [AchieveSlot] = s.Achieve.DeepClone(),
        };

        /// <summary>묶음이 쓰는 칸. 1차 밖(패스·이벤트 주기)이면 null.</summary>
        public static JObject Slot(MvQuestSnapshot s, QuestGroupData g)
        {
            if (g == null || !Supported(g)) return null;
            if (g.progressScope == QuestProgressScope.Lifetime) return s.Achieve;
            return g.periodType == QuestPeriodType.Daily ? s.Daily : g.periodType == QuestPeriodType.Monthly ? s.Monthly : null;
        }

        /// <summary>1차 범위 묶음(08 §8.8): active, 일일·월간·업적. 패스(100)는 2차.</summary>
        public static bool Supported(QuestGroupData g)
        {
            if (g == null || !g.active) return false;
            switch (g.category)
            {
                case QuestCategory.Daily:
                case QuestCategory.Monthly:
                    return g.progressScope == QuestProgressScope.Lifetime
                           || g.periodType == QuestPeriodType.Daily || g.periodType == QuestPeriodType.Monthly;
                case QuestCategory.Achievement:
                    return true;
                default:
                    return false;
            }
        }

        public static string UnlockKey(QuestCategory category) => category == QuestCategory.Achievement ? AchievementUnlockKey : QuestUnlockKey;

        // ───────── 카운터 ─────────

        /// <summary>카운터 키: 조건 0 = <c>"{clearType}"</c>, 조건 있음 = <c>"{clearType}:{cond}"</c>.</summary>
        public static string CounterKey(QuestClearType type, int condition)
            => condition == 0 ? ((int)type).ToString(Inv) : ((int)type).ToString(Inv) + ":" + condition.ToString(Inv);

        public static long Count(JObject slot, QuestClearType type, int condition)
            => slot?["cnt"]?.Value<long?>(CounterKey(type, condition)) ?? 0;

        /// <summary>평생·일일·월간에 동시에 더한다. 조건이 있으면 전체(0)와 그 조건 둘 다.</summary>
        public static void Add(MvQuestSnapshot s, QuestClearType type, int condition, long amount)
        {
            if (amount <= 0) return;
            foreach (var slot in new[] { s.Achieve, s.Daily, s.Monthly })
            {
                AddTo(slot, CounterKey(type, 0), amount);
                if (condition != 0) AddTo(slot, CounterKey(type, condition), amount);
            }
        }

        static void AddTo(JObject slot, string key, long amount)
        {
            var cnt = (JObject)slot["cnt"];
            long next = Math.Min(int.MaxValue, (cnt.Value<long?>(key) ?? 0) + amount); // 저장 칸은 int(MvSaveNormalize IntMap)
            cnt[key] = (int)next;
        }

        static bool Got(JObject slot, int id) => slot["got"] is JArray got && got.Any(t => t.Type == JTokenType.Integer && t.Value<long>() == id);

        static void MarkGot(JObject slot, int id)
        {
            if (!Got(slot, id)) ((JArray)slot["got"]).Add(id);
        }

        static int ClaimCount(JObject slot, int id) => slot["claims"]?.Value<int?>(id.ToString(Inv)) ?? 0;

        // ───────── 목록 ─────────

        /// <summary>413 행 1칸. 월간 칸은 받을 때마다 목표 = clearValue + clearValueStep × 받은 횟수(step 0이면 1번).</summary>
        public static MvQuestEntry Entry(RepeatQuestData row, QuestGroupData g, MvQuestSnapshot s)
        {
            JObject slot = Slot(s, g);
            bool monthly = slot != null && ReferenceEquals(slot, s.Monthly);
            int claimed = slot == null ? 0 : monthly ? ClaimCount(slot, row.id) : Got(slot, row.id) ? 1 : 0;
            bool repeat = monthly && row.clearValueStep > 0;
            var e = new MvQuestEntry
            {
                Id = row.id, Group = g.group, Category = g.category, GroupType = g.groupType,
                ClearType = row.clearType, Condition = row.clearCondition,
                Count = Count(slot, row.clearType, row.clearCondition),
                Target = repeat ? row.TargetAfter(claimed) : row.clearValue,
                Claimed = claimed, Repeat = repeat, Reward = row.reward ?? Array.Empty<ConsumeData>(),
            };
            e.State = !repeat && claimed > 0 ? MvQuestState.Done : e.Count >= e.Target ? MvQuestState.Claimable : MvQuestState.Progress;
            return e;
        }

        /// <summary>업적 묶음의 지금 단계 1칸(progress 순으로 차례로). 다 받았으면 마지막 단계 Done.</summary>
        public static MvQuestEntry StepEntry(IReadOnlyList<AchievementData> rows, QuestGroupData g, MvQuestSnapshot s)
        {
            var steps = Steps(rows);
            if (steps.Count == 0) return null;
            JObject slot = Slot(s, g);
            int index = steps.FindIndex(r => slot == null || !Got(slot, r.id));
            bool all = index < 0;
            var row = all ? steps[steps.Count - 1] : steps[index];
            var e = new MvQuestEntry
            {
                Id = row.id, Group = g.group, Category = g.category, GroupType = g.groupType,
                ClearType = row.clearType, Condition = row.clearCondition,
                Count = Count(slot, row.clearType, row.clearCondition),
                Target = row.clearValue, PrevTarget = row.prevClearValue,
                Claimed = all ? steps.Count : index, Step = all ? steps.Count : index + 1, StepCount = steps.Count,
                Reward = row.reward ?? Array.Empty<ConsumeData>(),
            };
            e.State = all ? MvQuestState.Done : e.Count >= e.Target ? MvQuestState.Claimable : MvQuestState.Progress;
            return e;
        }

        static List<AchievementData> Steps(IReadOnlyList<AchievementData> rows)
            => rows.Where(r => r.active).OrderBy(r => r.progress).ThenBy(r => r.id).ToList();

        static IEnumerable<RepeatQuestData> ListRows(MvTables t, QuestGroupData g)
            => t.RepeatQuest.GetGroup(g.group).Where(r => r.active).OrderBy(r => r.sort).ThenBy(r => r.id);

        /// <summary>1차 묶음 전부(410 순서). 묶음마다 칸 = 413 행(목록·포인트형) 또는 업적 지금 단계 1칸(단계형).</summary>
        public static List<MvQuestGroupView> Board(MvTables t, MvQuestSnapshot s, long nowMs)
        {
            var result = new List<MvQuestGroupView>();
            foreach (var g in t.QuestGroup.Sorted)
            {
                if (!Supported(g)) continue;
                var view = new MvQuestGroupView { Row = g, ResetAtMs = g.progressScope == QuestProgressScope.Lifetime ? 0 : ResetAt(g.periodType, nowMs) };
                if (g.groupType == QuestGroupType.Step)
                {
                    var e = StepEntry(t.Achievement.GetGroup(g.group), g, s);
                    if (e != null) view.Entries.Add(e);
                }
                else
                {
                    foreach (var row in ListRows(t, g)) view.Entries.Add(Entry(row, g, s));
                }
                result.Add(view);
            }
            return result;
        }

        // ───────── 판정(로컬 백엔드 처리기가 부른다) ─────────

        /// <summary>
        /// 카운터 더하기. <paramref name="events"/> = [{t, c, v}]. 일일 포인트(20)는 받기에서만 오르므로 거절한다.
        /// 성공이면 Data = {mvQuest}.
        /// </summary>
        public static MvMetaResponse Record(MvQuestSnapshot s, JArray events)
        {
            if (events == null || events.Count == 0) return MvMetaResponse.Reject(MvMetaErrors.BadParams, "ev");
            var result = new JArray();
            foreach (JToken item in events)
            {
                if (!(item is JObject ev)) return MvMetaResponse.Reject(MvMetaErrors.BadParams, "ev");
                int type = ev.Value<int?>("t") ?? 0;
                int cond = ev.Value<int?>("c") ?? 0;
                long amount = ev.Value<long?>("v") ?? 0;
                if (!Enum.IsDefined(typeof(QuestClearType), type) || type == (int)QuestClearType.DailyPoint)
                    return MvMetaResponse.Reject(MvMetaErrors.BadParams, "t " + type);
                if (cond < 0 || amount <= 0) return MvMetaResponse.Reject(MvMetaErrors.BadParams, "c/v");
                Add(s, (QuestClearType)type, cond, amount);
                result.Add(new JObject { ["t"] = type, ["c"] = cond, ["v"] = amount });
            }
            return MvMetaResponse.Success(new JObject { [MvSaveKeys.Quest] = Write(s) }, new JObject { ["ev"] = result });
        }

        /// <summary>
        /// 보상 받기 1번. 목표 미달 <see cref="MvMetaErrors.NotDone"/>, 이미 받음 <see cref="MvMetaErrors.AlreadyClaimed"/>,
        /// 잠김 <see cref="MvMetaErrors.Locked"/>, 지급할 수 없는 보상 <see cref="MvMetaErrors.NoReward"/>.
        /// 보상: 재화(1)는 재화 행, 일일 포인트(300)는 카운터 dailyPoint(20)로 — 같은 저장 한 번.
        /// </summary>
        public static MvMetaResponse Claim(MvTables t, MvQuestSnapshot s, ResourceService res, int id, Func<string, bool> isOpen)
        {
            MvQuestEntry entry;
            QuestGroupData g;
            var repeat = t.RepeatQuest.Get(id);
            if (repeat != null)
            {
                g = t.QuestGroup.GetGroup(repeat.questGroup).FirstOrDefault();
                if (!repeat.active || Slot(s, g) == null || g.groupType == QuestGroupType.Step) return MvMetaResponse.Reject(MvMetaErrors.UnknownKey, "id " + id);
                entry = Entry(repeat, g, s);
            }
            else
            {
                var ach = t.Achievement.Get(id);
                g = ach != null ? t.QuestGroup.GetGroup(ach.questGroup).FirstOrDefault() : null;
                if (ach == null || !ach.active || Slot(s, g) == null) return MvMetaResponse.Reject(MvMetaErrors.UnknownKey, "id " + id);
                entry = StepEntry(t.Achievement.GetGroup(g.group), g, s);
                if (entry.Id != id)
                {
                    bool before = Got(s.Achieve, id);
                    return MvMetaResponse.Reject(before ? MvMetaErrors.AlreadyClaimed : MvMetaErrors.NotDone, "id " + id + " 단계 순서");
                }
            }
            if (isOpen != null && !isOpen(UnlockKey(g.category))) return MvMetaResponse.Reject(MvMetaErrors.Locked, UnlockKey(g.category));
            if (entry.State == MvQuestState.Done) return MvMetaResponse.Reject(MvMetaErrors.AlreadyClaimed, "id " + id);
            if (entry.State != MvQuestState.Claimable) return MvMetaResponse.Reject(MvMetaErrors.NotDone, entry.Count + " < " + entry.Target);

            var wealth = new MvWealthChange();
            var rewards = new JArray();
            long points = 0;
            foreach (var r in entry.Reward)
            {
                long v = (long)Math.Floor(r.Value);
                switch (r.Type)
                {
                    case ConsumeType.Wealth:
                        if (MvWealthIds.TypeOf(r.Id) == null) return MvMetaResponse.Reject(MvMetaErrors.NoReward, r.ToString());
                        wealth.Gain(r.Id, v);
                        break;
                    case ConsumeType.DailyQuestPoint:
                        points += v;
                        break;
                    default:
                        // 상자·패스 XP 등 2차 보상은 그 계약이 처리한다.
                        return MvMetaResponse.Reject(MvMetaErrors.NoReward, r.ToString());
                }
                rewards.Add(new JObject { ["t"] = r.TypeId, ["i"] = r.Id, ["v"] = v });
            }

            JObject slot = Slot(s, g);
            if (ReferenceEquals(slot, s.Monthly))
            {
                var claims = (JObject)slot["claims"];
                claims[id.ToString(Inv)] = entry.Claimed + 1;
            }
            else MarkGot(slot, id);
            Add(s, QuestClearType.DailyPoint, 0, points);

            var data = new JObject { [MvSaveKeys.Quest] = Write(s) };
            if (!wealth.TryWrite(res, data, out string fail)) return MvMetaResponse.Reject(MvMetaErrors.NoReward, fail);
            return MvMetaResponse.Success(data, new JObject { ["id"] = id, ["claimed"] = entry.Claimed + 1, ["rewards"] = rewards });
        }
    }
}
