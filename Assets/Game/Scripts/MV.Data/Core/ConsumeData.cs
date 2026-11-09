using System;
using System.Collections.Generic;
using System.Globalization;

namespace MV.Data
{
    /// <summary>
    /// 보상·비용 1항목(기획서 07 §7.1.6). 정크버스터즈 `ImportDatas` 규칙:
    /// `{p}Type`·`{p}Id`·`{p}Value`를 `;`로 자르고, type·id가 value보다 짧으면 마지막 것을 반복한다.
    /// </summary>
    public readonly struct ConsumeData
    {
        public readonly ConsumeType Type;
        public readonly int Id;
        public readonly double Value;
        /// <summary>`{p}ValuePerZone`: 값에 `perZone × zoneIndex`를 더한다(07 §7.1.7).</summary>
        public readonly double PerZone;
        public readonly RewardValueRule Rule;

        public ConsumeData(ConsumeType type, int id, double value, double perZone = 0, RewardValueRule rule = RewardValueRule.Flat)
        {
            Type = type;
            Id = id;
            Value = value;
            PerZone = perZone;
            Rule = rule;
        }

        public int TypeId => (int)Type;

        /// <summary>Zone 배수를 반영한 값(Flat 규칙). DiaCap 계열은 호출자가 14_rebirthPerk로 계산한다.</summary>
        public double ValueAtZone(int zoneIndex) => Value + PerZone * zoneIndex;

        public override string ToString() => $"{TypeId}/{Id}/{Value.ToString(CultureInfo.InvariantCulture)}";

        public const char Separator = ';';

        /// <summary>행의 consumeData 열 묶음을 읽는다. 열이 비면 빈 목록(보상 없음). 형식 오류는 예외.</summary>
        public static List<ConsumeData> Import(TSVRow row, string prefix = "reward")
        {
            if (row == null) throw new ArgumentNullException(nameof(row));
            if (!TryParse(row.Get(prefix + "Type"), row.Get(prefix + "Id"), row.Get(prefix + "Value"),
                    row.Get(prefix + "ValuePerZone"), row.Get(prefix + "ValueRule"), out var result, out var error))
                throw new FormatException($"consumeData {prefix}: {error}");
            return result;
        }

        /// <summary>원문 문자열로 읽는다. 검증기와 <see cref="Import"/>가 같은 규칙을 쓴다.</summary>
        public static bool TryParse(string types, string ids, string values, string perZones, string rules,
            out List<ConsumeData> result, out string error)
        {
            result = new List<ConsumeData>();
            error = null;
            var t = Split(types);
            var d = Split(ids);
            var v = Split(values);
            var z = Split(perZones);
            var r = Split(rules);
            int n = v.Count;
            if (n == 0)
            {
                if (t.Count > 0 || d.Count > 0)
                {
                    error = "Type·Id가 있는데 Value가 비었습니다";
                    return false;
                }
                return true;
            }

            if (t.Count == 0)
            {
                error = "Value가 있는데 Type이 비었습니다";
                return false;
            }
            if (t.Count != 1 && t.Count != n)
            {
                error = $"Type 개수 {t.Count}가 1 또는 Value 개수 {n}와 맞지 않습니다";
                return false;
            }
            if (d.Count != 0 && d.Count != 1 && d.Count != n)
            {
                error = $"Id 개수 {d.Count}가 0, 1 또는 Value 개수 {n}와 맞지 않습니다";
                return false;
            }
            if (z.Count != 0 && z.Count != 1 && z.Count != n)
            {
                error = $"ValuePerZone 개수 {z.Count}가 0, 1 또는 Value 개수 {n}와 맞지 않습니다";
                return false;
            }
            if (r.Count != 0 && r.Count != 1 && r.Count != n)
            {
                error = $"ValueRule 개수 {r.Count}가 0, 1 또는 Value 개수 {n}와 맞지 않습니다";
                return false;
            }

            for (int i = 0; i < n; i++)
            {
                string ts = t[Math.Min(i, t.Count - 1)];
                if (!TryParseType(ts, out var type))
                {
                    error = $"Type `{ts}`을 읽을 수 없습니다";
                    return false;
                }

                int id = 0;
                if (d.Count > 0)
                {
                    string ds = d[Math.Min(i, d.Count - 1)];
                    if (!int.TryParse(ds, NumberStyles.Integer, CultureInfo.InvariantCulture, out id))
                    {
                        error = $"Id `{ds}`가 정수가 아닙니다";
                        return false;
                    }
                }

                if (!TryParseDouble(v[i], out double value))
                {
                    error = $"Value `{v[i]}`가 숫자가 아닙니다";
                    return false;
                }

                double perZone = 0;
                if (z.Count > 0 && !TryParseDouble(z[Math.Min(i, z.Count - 1)], out perZone))
                {
                    error = $"ValuePerZone `{z[Math.Min(i, z.Count - 1)]}`가 숫자가 아닙니다";
                    return false;
                }

                var rule = RewardValueRule.Flat;
                if (r.Count > 0 && !TryParseEnum(r[Math.Min(i, r.Count - 1)], out rule))
                {
                    error = $"ValueRule `{r[Math.Min(i, r.Count - 1)]}`을 읽을 수 없습니다";
                    return false;
                }

                result.Add(new ConsumeData(type, id, value, perZone, rule));
            }

            return true;
        }

        /// <summary>숫자(`602`) 또는 enum 이름(`RunGold`, 대소문자 무시).</summary>
        public static bool TryParseType(string text, out ConsumeType type)
        {
            type = ConsumeType.None;
            if (string.IsNullOrEmpty(text)) return false;
            if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int number))
            {
                type = (ConsumeType)number;
                return true;
            }
            return TryParseEnum(text, out type);
        }

        internal static bool TryParseEnum<T>(string text, out T value) where T : struct, Enum
        {
            value = default;
            if (string.IsNullOrEmpty(text)) return false;
            // 숫자 문자열은 enum 이름이 아니다(Enum.TryParse는 "5"를 받아 준다).
            if (char.IsDigit(text[0]) || text[0] == '-' || text[0] == '+') return false;
            return Enum.TryParse(text, true, out value) && Enum.IsDefined(typeof(T), value);
        }

        internal static bool TryParseDouble(string text, out double value) =>
            double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value);

        internal static List<string> Split(string text)
        {
            var list = new List<string>();
            if (string.IsNullOrWhiteSpace(text)) return list;
            foreach (var part in text.Split(Separator)) list.Add(part.Trim());
            return list;
        }
    }
}
