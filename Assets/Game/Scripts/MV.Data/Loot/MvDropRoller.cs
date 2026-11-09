using System;
using System.Collections.Generic;
using System.Linq;

namespace MV.Data
{
    /// <summary>추첨 결과 1개(07 §7.9.3). Rule이 Flat이 아니면 Value는 0이고 호출자가 14_rebirthPerk로 수량을 정한다.</summary>
    public readonly struct MvDropResult
    {
        public readonly ConsumeType Type;
        public readonly int Id;
        public readonly long Value;
        public readonly RewardValueRule Rule;
        /// <summary>뽑힌 801·802 행 id.</summary>
        public readonly int RowId;

        public MvDropResult(ConsumeType type, int id, long value, RewardValueRule rule, int rowId)
        {
            Type = type;
            Id = id;
            Value = value;
            Rule = rule;
            RowId = rowId;
        }

        public override string ToString() => $"{(int)Type}/{Id}/{Value}({Rule}) row {RowId}";
    }

    /// <summary>확률 표시 1줄. Probability는 「기대 등장 횟수」(하위 묶음이 여러 번 펼쳐지면 1을 넘을 수 있음).</summary>
    public readonly struct MvDropOdds
    {
        public readonly ConsumeType Type;
        public readonly int Id;
        public readonly long Min;
        public readonly long Max;
        public readonly RewardValueRule Rule;
        public readonly int RowId;
        /// <summary>802 칸 번호(801은 1). 하위 묶음 행은 부모 칸.</summary>
        public readonly int Slot;
        public readonly double Probability;

        public MvDropOdds(ConsumeType type, int id, long min, long max, RewardValueRule rule, int rowId, int slot, double probability)
        {
            Type = type;
            Id = id;
            Min = min;
            Max = max;
            Rule = rule;
            RowId = rowId;
            Slot = slot;
            Probability = probability;
        }
    }

    /// <summary>
    /// 801_drop·802_randomBox 추첨과 확률 계산(같은 그래프). 난수는 균등 [0,1) 하나씩(런 = RunRng, 메타 = MetaRng).
    /// </summary>
    public interface IMvDropSource
    {
        /// <summary>Drop(50) 묶음을 times번 실행해 결과를 더한다. 결과가 RandomBox(8)면 상자로 준다(열지 않음).</summary>
        void RollDrop(int dropGroup, Func<double> next, List<MvDropResult> results, int times = 1);
        /// <summary>RandomBox(8) 상자를 times번 연다. 칸마다 1행씩 모두 지급, 칸 결과 Drop(50)은 펼친다.</summary>
        void OpenBox(int boxGroup, Func<double> next, List<MvDropResult> results, int times = 1);
        IReadOnlyList<MvDropOdds> DropOdds(int dropGroup);
        IReadOnlyList<MvDropOdds> BoxOdds(int boxGroup);
    }

    /// <summary>
    /// 게임 추첨 구현(D-85).
    /// Framework 대체: `com.jjevol.economy.drop` 고정본이 없다. 고정본이 들어오면 같은 인터페이스의 어댑터 `MvDropSource`로 바꾼다(표·행 그대로).
    /// 행 순서 = id 오름차순, 균등 난수 u 1개로 `u &lt; 누적/(double)합`인 첫 행을 고른다(정본 `Math.random() &lt; p`와 같은 호출 수).
    /// 수량 범위(min &lt; max)일 때만 난수를 하나 더 쓴다.
    /// </summary>
    public sealed class MvDropRoller : IMvDropSource
    {
        public const int MaxDepth = MvDropGraph.MaxDepth;

        readonly MvTables _tables;

        /// <summary>그래프 오류(순환·깊이 초과)가 있으면 <see cref="InvalidOperationException"/>(로드 오류).</summary>
        public MvDropRoller(MvTables tables)
        {
            _tables = tables ?? throw new ArgumentNullException(nameof(tables));
            var errors = MvDropGraph.FindGraphErrors(tables);
            if (errors.Count > 0)
                throw new InvalidOperationException("[MvDropRoller] " + string.Join("; ", errors.Select(e => $"[{e.Table}] id={e.RowId}: {e.Message}")));
        }

        // ---------------- 추첨 ----------------

        public void RollDrop(int dropGroup, Func<double> next, List<MvDropResult> results, int times = 1)
        {
            if (next == null) throw new ArgumentNullException(nameof(next));
            if (results == null) throw new ArgumentNullException(nameof(results));
            for (int i = 0; i < times; i++) RollDropOnce(dropGroup, next, results, 1);
        }

        public void OpenBox(int boxGroup, Func<double> next, List<MvDropResult> results, int times = 1)
        {
            if (next == null) throw new ArgumentNullException(nameof(next));
            if (results == null) throw new ArgumentNullException(nameof(results));
            for (int i = 0; i < times; i++) OpenBoxOnce(boxGroup, next, results, 1);
        }

        void RollDropOnce(int group, Func<double> next, List<MvDropResult> results, int depth)
        {
            Guard(depth, DropData.Table, group);
            var rows = _tables.Drop.GetGroup(group);
            if (rows.Count == 0) throw new KeyNotFoundException($"801_drop 묶음 {group}가 없습니다");
            var row = Pick(rows, next());
            if (row == null) return;
            Emit(row, row.rewardValueRule, next, results, depth, expandBox: false);
        }

        void OpenBoxOnce(int group, Func<double> next, List<MvDropResult> results, int depth)
        {
            Guard(depth, RandomBoxData.Table, group);
            var rows = _tables.RandomBox.GetGroup(group);
            if (rows.Count == 0) throw new KeyNotFoundException($"802_randomBox 상자 {group}가 없습니다");
            foreach (var slot in Slots(rows))
            {
                var row = Pick(slot, next());
                if (row != null) Emit(row, RewardValueRule.Flat, next, results, depth, expandBox: false);
            }
        }

        void Emit(LootRowData row, RewardValueRule rule, Func<double> next, List<MvDropResult> results, int depth, bool expandBox)
        {
            if (row.IsEmpty) return;
            long value = rule == RewardValueRule.Flat ? Quantity(row, next) : 0;
            if (row.Type == ConsumeType.Drop)
            {
                for (long i = 0; i < value; i++) RollDropOnce(row.rewardId, next, results, depth + 1);
                return;
            }
            if (row.Type == ConsumeType.RandomBox && expandBox)
            {
                for (long i = 0; i < value; i++) OpenBoxOnce(row.rewardId, next, results, depth + 1);
                return;
            }
            results.Add(new MvDropResult(row.Type, row.rewardId, value, rule, row.id));
        }

        static long Quantity(LootRowData row, Func<double> next)
        {
            long min = row.rewardValueMin, max = row.rewardValueMax;
            if (max <= min) return min;
            long span = max - min + 1;
            long add = (long)Math.Floor(next() * span);
            return min + Math.Min(add, span - 1);
        }

        /// <summary>id 오름차순 누적 비중. u ∈ [0,1). 합이 0이면 null.</summary>
        static T Pick<T>(IReadOnlyList<T> rows, double u) where T : LootRowData
        {
            long total = 0;
            foreach (var r in rows) total += Math.Max(0, r.weight);
            if (total <= 0) return null;
            long cum = 0;
            T last = null;
            foreach (var r in Ordered(rows))
            {
                if (r.weight <= 0) continue;
                cum += r.weight;
                last = r;
                if (u < cum / (double)total) return r;
            }
            return last; // u가 1에 아주 가까운 반올림 방어.
        }

        static IEnumerable<T> Ordered<T>(IReadOnlyList<T> rows) where T : LootRowData => rows.OrderBy(r => r.id);

        static IEnumerable<List<RandomBoxData>> Slots(IReadOnlyList<RandomBoxData> rows) =>
            rows.GroupBy(r => r.subGroup).OrderBy(g => g.Key).Select(g => g.OrderBy(r => r.id).ToList());

        static void Guard(int depth, string table, int group)
        {
            if (depth > MaxDepth) throw new InvalidOperationException($"[{table}] 묶음 {group}: 추첨 깊이가 {MaxDepth}를 넘었습니다");
        }

        // ---------------- 확률 표시(같은 그래프) ----------------

        public IReadOnlyList<MvDropOdds> DropOdds(int dropGroup)
        {
            var list = new List<MvDropOdds>();
            AddDropOdds(dropGroup, 1.0, 1, list, 1);
            return list;
        }

        public IReadOnlyList<MvDropOdds> BoxOdds(int boxGroup)
        {
            var list = new List<MvDropOdds>();
            var rows = _tables.RandomBox.GetGroup(boxGroup);
            foreach (var slot in Slots(rows)) AddRows(slot, 1.0, slot[0].subGroup, RewardValueRule.Flat, list, 1);
            return list;
        }

        void AddDropOdds(int group, double parent, int slot, List<MvDropOdds> list, int depth)
        {
            Guard(depth, DropData.Table, group);
            foreach (var row in Ordered(_tables.Drop.GetGroup(group)))
                AddRow(row, Share(_tables.Drop.GetGroup(group), row) * parent, slot, row.rewardValueRule, list, depth);
        }

        void AddRows<T>(IReadOnlyList<T> rows, double parent, int slot, RewardValueRule rule, List<MvDropOdds> list, int depth) where T : LootRowData
        {
            foreach (var row in Ordered(rows)) AddRow(row, Share(rows, row) * parent, slot, rule, list, depth);
        }

        void AddRow(LootRowData row, double p, int slot, RewardValueRule rule, List<MvDropOdds> list, int depth)
        {
            if (row.Type == ConsumeType.Drop && !row.IsEmpty)
            {
                double times = (row.rewardValueMin + row.rewardValueMax) / 2.0;
                AddDropOdds(row.rewardId, p * times, slot, list, depth + 1);
                return;
            }
            list.Add(new MvDropOdds(row.Type, row.rewardId, row.rewardValueMin, row.rewardValueMax, rule, row.id, slot, p));
        }

        static double Share<T>(IReadOnlyList<T> rows, T row) where T : LootRowData
        {
            long total = 0;
            foreach (var r in rows) total += Math.Max(0, r.weight);
            return total <= 0 ? 0 : Math.Max(0, row.weight) / (double)total;
        }
    }

    /// <summary>801·802가 50(Drop)·8(RandomBox)으로 서로 가리키는 그래프 검사(순환 없음, 깊이 ≤ 5).</summary>
    public static class MvDropGraph
    {
        public const int MaxDepth = 5;

        public readonly struct Issue
        {
            public readonly string Table;
            public readonly int RowId;
            public readonly string Message;

            public Issue(string table, int rowId, string message)
            {
                Table = table;
                RowId = rowId;
                Message = message;
            }
        }

        enum Mark { None, Visiting, Done }

        /// <summary>모든 묶음에서 시작해 순환·깊이 초과를 찾는다. 같은 원인은 한 번만 낸다.</summary>
        public static List<Issue> FindGraphErrors(MvTables t)
        {
            var issues = new List<Issue>();
            var marks = new Dictionary<(bool box, int group), Mark>();
            var height = new Dictionary<(bool box, int group), int>();
            var reported = new HashSet<int>();

            foreach (var g in t.Drop.Groups) Visit(t, (false, g), marks, height, issues, reported);
            foreach (var g in t.RandomBox.Groups) Visit(t, (true, g), marks, height, issues, reported);
            return issues;
        }

        /// <returns>이 묶음부터 내려가는 깊이(자신 = 1). 순환이면 -1.</returns>
        static int Visit(MvTables t, (bool box, int group) node, Dictionary<(bool, int), Mark> marks,
            Dictionary<(bool, int), int> height, List<Issue> issues, HashSet<int> reported)
        {
            if (marks.TryGetValue(node, out var m))
            {
                if (m == Mark.Visiting) return -1;
                return height[node];
            }
            marks[node] = Mark.Visiting;
            int best = 1;
            IEnumerable<LootRowData> rows = node.box
                ? t.RandomBox.GetGroup(node.group).Cast<LootRowData>()
                : t.Drop.GetGroup(node.group).Cast<LootRowData>();
            string table = node.box ? RandomBoxData.Table : DropData.Table;
            foreach (var row in rows.OrderBy(r => r.id))
            {
                bool toBox = row.Type == ConsumeType.RandomBox;
                if (row.Type != ConsumeType.Drop && !toBox) continue;
                var child = (toBox, row.rewardId);
                bool exists = toBox ? t.RandomBox.HasGroup(row.rewardId) : t.Drop.HasGroup(row.rewardId);
                if (!exists) continue; // 없는 묶음은 참조 검사가 오류로 낸다.
                int h = Visit(t, child, marks, height, issues, reported);
                if (h < 0)
                {
                    if (reported.Add(row.id)) issues.Add(new Issue(table, row.id, $"묶음 {row.rewardId}로 가는 참조가 순환합니다"));
                    continue;
                }
                best = Math.Max(best, h + 1);
                if (h + 1 > MaxDepth && reported.Add(row.id))
                    issues.Add(new Issue(table, row.id, $"추첨 깊이 {h + 1}이 {MaxDepth}를 넘습니다"));
            }
            marks[node] = Mark.Done;
            height[node] = best;
            return best;
        }
    }
}
