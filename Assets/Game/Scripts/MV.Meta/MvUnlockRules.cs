using System;
using System.Collections.Generic;
using MV.Data;

namespace MV.Meta
{
    /// <summary>해금 단계(07 §7.8.9). 숨김 → 잠금 표시 → 사용 가능.</summary>
    public enum MvUnlockState
    {
        /// <summary>행이 없거나 꺼짐(active 0), 또는 잠금 표시 조건이 아직 안 됨.</summary>
        Hidden = 0,
        /// <summary>보이지만 잠김(잠금 표시 조건 성립, 사용 가능 조건 미성립).</summary>
        Locked = 1,
        /// <summary>사용 가능(조건 모두 성립 또는 sticky 기록).</summary>
        Open = 2,
    }

    /// <summary>해금 판정에 쓰는 진행 값(<c>mvProgress</c>). 조건 종류가 늘면 여기에 값을 더한다.</summary>
    public readonly struct MvUnlockProgress
    {
        /// <summary>0기반 현재 Area(<c>save.area</c>).</summary>
        public readonly int Area;
        /// <summary>본편 런 정산 수.</summary>
        public readonly int Runs;
        /// <summary>Level(런 1회 = +1).</summary>
        public readonly int Level;

        public MvUnlockProgress(int area, int runs, int level)
        {
            Area = area;
            Runs = runs;
            Level = level;
        }
    }

    /// <summary>
    /// 해금 판정 한 곳(07 §7.8.9). 표 <c>05_contentOpen</c> 행 + 진행 값 + sticky 기록(<c>mvUnlock.opened</c>)으로
    /// 숨김·잠금 표시·사용 가능을 정한다. 조건은 단계마다 모두 성립해야 하고, 사용 가능은 잠금 표시 조건도 함께 성립해야 한다.
    /// 새 조건 종류는 <see cref="UnlockConditionType"/> 값 1개 + <see cref="Met"/> 한 줄 + <see cref="Raise"/> 한 줄.
    /// </summary>
    public static class MvUnlockRules
    {
        /// <summary>조건 1개 성립. 모르는 종류는 성립하지 않는다(검증기가 표에서 막는다).</summary>
        public static bool Met(UnlockCondition c, MvUnlockProgress p)
        {
            switch (c.type)
            {
                case UnlockConditionType.Always: return true;
                case UnlockConditionType.Area: return p.Area + 1 >= c.value;
                case UnlockConditionType.Runs: return p.Runs >= c.value;
                case UnlockConditionType.Level: return p.Level >= c.value;
                default: return false;
            }
        }

        /// <summary>목록의 조건이 모두 성립(빈 목록은 성립).</summary>
        public static bool AllMet(IReadOnlyList<UnlockCondition> conditions, MvUnlockProgress p)
        {
            if (conditions == null) return true;
            foreach (var c in conditions)
                if (!Met(c, p)) return false;
            return true;
        }

        /// <summary>조건만으로(sticky 기록 없이) 사용 가능. active 0 행은 열리지 않는다(지도 「업데이트 예정」).</summary>
        public static bool ConditionsMet(ContentOpenData row, MvUnlockProgress p)
            => row != null && row.active && AllMet(row.showConditions, p) && AllMet(row.openConditions, p);

        /// <summary>단계 판정. sticky 행이 <paramref name="opened"/>에 있으면 진행이 되돌아가도(부활) 사용 가능.</summary>
        public static MvUnlockState State(ContentOpenData row, MvUnlockProgress p, IReadOnlyCollection<string> opened)
        {
            if (row == null || !row.active) return MvUnlockState.Hidden;
            if (row.sticky && opened != null && !string.IsNullOrEmpty(row.key) && Contains(opened, row.key)) return MvUnlockState.Open;
            if (!AllMet(row.showConditions, p)) return MvUnlockState.Hidden;
            return AllMet(row.openConditions, p) ? MvUnlockState.Open : MvUnlockState.Locked;
        }

        public static bool IsOpen(ContentOpenData row, MvUnlockProgress p, IReadOnlyCollection<string> opened)
            => State(row, p, opened) == MvUnlockState.Open;

        /// <summary>
        /// 이 행이 사용 가능해지도록 진행 값을 올린 결과(테스트 패널 「전부 해금」). Area는 <paramref name="lastArea"/>(0기반)를 넘지 않는다.
        /// 진행 값은 내리지 않는다.
        /// </summary>
        public static MvUnlockProgress Raise(ContentOpenData row, MvUnlockProgress p, int lastArea)
        {
            if (row == null) return p;
            p = Raise(row.showConditions, p, lastArea);
            return Raise(row.openConditions, p, lastArea);
        }

        static MvUnlockProgress Raise(IReadOnlyList<UnlockCondition> conditions, MvUnlockProgress p, int lastArea)
        {
            int area = p.Area, runs = p.Runs, level = p.Level;
            foreach (var c in conditions)
                switch (c.type)
                {
                    case UnlockConditionType.Area: area = Math.Max(area, Math.Min(lastArea, c.value - 1)); break;
                    case UnlockConditionType.Runs: runs = Math.Max(runs, c.value); break;
                    case UnlockConditionType.Level: level = Math.Max(level, c.value); break;
                }
            return new MvUnlockProgress(area, runs, level);
        }

        /// <summary>
        /// 지도에서 이 기능을 둘 Area 번호(1기반): 잠금 표시 조건, 없으면 사용 가능 조건의 첫 Area 조건 값. Area 조건이 없으면 0.
        /// </summary>
        public static int MapArea(ContentOpenData row)
        {
            if (row == null) return 0;
            int area = FirstArea(row.showConditions);
            return area > 0 ? area : FirstArea(row.openConditions);
        }

        static int FirstArea(IReadOnlyList<UnlockCondition> conditions)
        {
            foreach (var c in conditions)
                if (c.type == UnlockConditionType.Area) return c.value;
            return 0;
        }

        static bool Contains(IReadOnlyCollection<string> set, string key)
        {
            if (set is ICollection<string> c) return c.Contains(key);
            foreach (var s in set)
                if (s == key) return true;
            return false;
        }
    }
}
