using System.Collections.Generic;
using System.Globalization;
using MV.Data;
using MV.Meta;

namespace MV.UI
{
    /// <summary>
    /// 해금 조건 문구(07 §7.8.9 <c>05_contentOpen</c>). 조건 값은 표에서만 읽는다. 판정은 <see cref="MvUnlockRules"/>가 하고
    /// 여기서는 「무엇이 더 필요한지」를 글로만 만든다. 새 조건 종류는 <see cref="Phrase"/>에 한 줄.
    /// </summary>
    public static class MvUnlockText
    {
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        /// <summary>조건 1개: <c>Area 2</c> / <c>런 3번</c> / <c>Level 15</c>. 항상(0)·모르는 종류는 빈 문자열.</summary>
        public static string Phrase(UnlockCondition c)
        {
            string v = c.value.ToString(Inv);
            switch (c.type)
            {
                case UnlockConditionType.Area: return "Area " + v;
                case UnlockConditionType.Runs: return "런 " + v + "번";
                case UnlockConditionType.Level: return "Level " + v;
                default: return "";
            }
        }

        /// <summary>
        /// 지금 단계에서 더 필요한 조건 문구(` · `로 잇는다). 숨김이면 잠금 표시 조건 + 사용 가능 조건, 그 밖은 사용 가능 조건.
        /// 조건이 없거나 모두 「항상」이면 빈 문자열(호출자가 「업데이트 예정」 등으로 대신한다).
        /// </summary>
        public static string Need(ContentOpenData row, MvUnlockState state = MvUnlockState.Locked)
        {
            if (row == null) return "";
            var parts = new List<string>(2);
            if (state == MvUnlockState.Hidden) Add(parts, row.showConditions);
            Add(parts, row.openConditions);
            return string.Join(" · ", parts);
        }

        static void Add(List<string> parts, IReadOnlyList<UnlockCondition> conditions)
        {
            foreach (var c in conditions)
            {
                string p = Phrase(c);
                if (p.Length > 0 && !parts.Contains(p)) parts.Add(p);
            }
        }
    }
}
