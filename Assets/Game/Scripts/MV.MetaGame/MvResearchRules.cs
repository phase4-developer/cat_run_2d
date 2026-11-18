using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using MV.Data;

namespace MV.Meta
{
    /// <summary>연구 계열 1개의 지금 상태(08 §8.5.3).</summary>
    public enum MvResearchStatus
    {
        /// <summary>다음 단계를 시작할 수 있다(비용 확인은 따로).</summary>
        Idle,
        /// <summary>이 계열이 진행 중이고 아직 끝 시각 전.</summary>
        Running,
        /// <summary>끝 시각이 지나 수령만 남았다.</summary>
        Ready,
        /// <summary>표의 마지막 단계까지 받았다.</summary>
        Max,
        /// <summary>다른 계열이 진행 중이다(저장 <c>mvResearch.run</c>이 1칸, D-RESEARCH-1 후보).</summary>
        Busy,
    }

    /// <summary>진행 중 연구 1건(<c>mvResearch.run</c>). <see cref="Group"/> 0 = 없음.</summary>
    public readonly struct MvResearchRun
    {
        public readonly int Group;
        public readonly long FinishAtMs;

        public MvResearchRun(int group, long finishAtMs) { Group = group; FinishAtMs = finishAtMs; }

        public bool Exists => Group != 0;
        public static MvResearchRun None => default;
    }

    /// <summary>
    /// 연구 규칙(08 §8.5.3, 표 <c>440_research</c>). 순수 함수만 둔다 — 로컬 백엔드 판정과 화면·런 시작 합산이 같은 함수를 쓴다.
    /// 행 추가만으로 계열·단계가 늘어난다(12 §12.6-C): 계열 = active 행의 group, 단계 = 그 group의 active 행(level 순).
    /// </summary>
    public static class MvResearchRules
    {
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        /// <summary>해금 행 key(<c>05_contentOpen 5001009</c>).</summary>
        public const string UnlockKey = "research";

        /// <summary>저장 <c>mvResearch.lv</c>·<c>run.key</c>의 계열 key(group 숫자 문자열).</summary>
        public static string Key(int group) => group.ToString(Inv);

        public static bool TryParseKey(string key, out int group)
            => int.TryParse(key, NumberStyles.Integer, Inv, out group) && group > 0;

        /// <summary>active 행이 있는 계열(오름차순).</summary>
        public static IReadOnlyList<int> Groups(MvTables t)
            => t.Research.Datas.Where(r => r.active && r.group > 0).Select(r => r.group).Distinct().OrderBy(g => g).ToArray();

        /// <summary>계열의 단계 행(active, level 오름차순).</summary>
        public static IReadOnlyList<ResearchData> Steps(MvTables t, int group)
            => t.Research.GetGroup(group).Where(r => r.active && r.level > 0).OrderBy(r => r.level).ToArray();

        /// <summary>계열 최고 단계(행이 없으면 0).</summary>
        public static int MaxLevel(MvTables t, int group)
        {
            var steps = Steps(t, group);
            return steps.Count == 0 ? 0 : steps[steps.Count - 1].level;
        }

        /// <summary>그 단계의 active 행. 없으면 null.</summary>
        public static ResearchData Row(MvTables t, int group, int level)
        {
            if (level <= 0) return null;
            var row = t.Research.Get(group, level);
            return row != null && row.active ? row : null;
        }

        /// <summary>지금 단계 다음 행(올릴 대상). MAX면 null.</summary>
        public static ResearchData Next(MvTables t, int group, int level) => Row(t, group, level + 1);

        /// <summary>시작 시각 + <c>seconds</c>(ms).</summary>
        public static long FinishAt(ResearchData row, long startMs) => checked(startMs + Math.Max(0, row.seconds) * 1000L);

        /// <summary>완료 판정: 끝 시각과 같거나 지났으면 완료(앱이 꺼져 있어도 시각으로 판정).</summary>
        public static bool IsDone(long finishAtMs, long nowMs) => nowMs >= finishAtMs;

        /// <summary>남은 ms(0 이상).</summary>
        public static long LeftMs(long finishAtMs, long nowMs) => Math.Max(0, finishAtMs - nowMs);

        /// <summary>계열 상태. <paramref name="level"/> = 받은 단계.</summary>
        public static MvResearchStatus Status(MvTables t, int group, int level, MvResearchRun run, long nowMs)
        {
            if (run.Exists && run.Group == group) return IsDone(run.FinishAtMs, nowMs) ? MvResearchStatus.Ready : MvResearchStatus.Running;
            if (Next(t, group, level) == null) return MvResearchStatus.Max;
            return run.Exists ? MvResearchStatus.Busy : MvResearchStatus.Idle;
        }

        /// <summary>
        /// 받은 단계의 누적 값(<c>statValue</c>는 그 단계까지의 누적 %). 저장 단계가 표보다 높으면(행 삭제·비활성) 그 아래 가장 높은 active 행.
        /// 0단계면 0.
        /// </summary>
        public static ResearchData Reached(MvTables t, int group, int level)
        {
            if (level <= 0) return null;
            ResearchData best = null;
            foreach (var row in Steps(t, group))
                if (row.level <= level) best = row;
            return best;
        }

        /// <summary>
        /// 메타 능력치 합(08 §8.5.0, % 단위). 계열마다 받은 단계 행의 <c>statValue</c>를 <c>statType</c>에 더한다. 연구 0이면 빈 사전(= 정본).
        /// </summary>
        public static IReadOnlyDictionary<MetaStat, double> Totals(MvTables t, Func<int, int> levelOf)
        {
            if (t == null) throw new ArgumentNullException(nameof(t));
            if (levelOf == null) throw new ArgumentNullException(nameof(levelOf));
            var sum = new Dictionary<MetaStat, double>();
            foreach (int group in Groups(t))
            {
                var row = Reached(t, group, levelOf(group));
                if (row == null || row.statValue == 0) continue;
                sum.TryGetValue(row.statType, out double now);
                sum[row.statType] = now + row.statValue;
            }
            return sum;
        }
    }
}
