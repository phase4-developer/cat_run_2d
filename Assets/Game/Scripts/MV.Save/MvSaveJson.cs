using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Numerics;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace MV.Save
{
    /// <summary>저장 JSON 문자열 도구. 개발 도구·시험이 Newtonsoft 형식 없이 비교할 때 쓴다.</summary>
    public static class MvSaveJson
    {
        /// <summary>날짜 문자열을 날짜로 바꾸지 않고 객체 하나를 읽는다(Framework 저장 읽기와 같은 설정).</summary>
        public static JObject ParseObject(string json)
        {
            if (json == null) throw new ArgumentNullException(nameof(json));
            using var reader = new JsonTextReader(new StringReader(json)) { DateParseHandling = DateParseHandling.None, MaxDepth = 128 };
            JObject result = JObject.Load(reader, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
            if (reader.Read()) throw new InvalidDataException("JSON 뒤에 남는 내용이 있습니다.");
            return result;
        }

        public static string ToJson(JToken token) => token.ToString(Formatting.None);

        /// <summary>속성 순서와 무관하게 같은지.</summary>
        public static bool DeepEquals(string left, string right) => JToken.DeepEquals(ParseObject(left), ParseObject(right));

        /// <summary>최상위 키 일부만 남긴다(없는 키는 건너뜀).</summary>
        public static string Pick(string json, params string[] keys)
        {
            JObject source = ParseObject(json);
            var result = new JObject();
            foreach (string key in keys)
                if (source.TryGetValue(key, out JToken value)) result[key] = value.DeepClone();
            return ToJson(result);
        }

        /// <summary>최상위 키 일부를 뺀다.</summary>
        public static string Without(string json, params string[] keys)
        {
            JObject source = ParseObject(json);
            foreach (string key in keys) source.Remove(key);
            return ToJson(source);
        }
    }

    /// <summary>관대한 읽기(09 §9.1 원칙 5): 모르는 필드 무시, 빠진 필드 기본값, 범위 밖 값 자르기.</summary>
    internal static class MvJsonRead
    {
        internal static bool TryLong(JToken token, out long value)
        {
            value = 0;
            if (token == null) return false;
            try
            {
                switch (token.Type)
                {
                    case JTokenType.Integer:
                        object raw = ((JValue)token).Value;
                        if (raw is BigInteger big) value = big > long.MaxValue ? long.MaxValue : big < long.MinValue ? long.MinValue : (long)big;
                        else if (raw is ulong unsigned) value = unsigned > long.MaxValue ? long.MaxValue : (long)unsigned;
                        else value = Convert.ToInt64(raw, CultureInfo.InvariantCulture);
                        return true;
                    case JTokenType.Float:
                        double number = token.Value<double>();
                        if (double.IsNaN(number) || double.IsInfinity(number)) return false;
                        number = Math.Floor(number);
                        value = number >= long.MaxValue ? long.MaxValue : number <= long.MinValue ? long.MinValue : (long)number;
                        return true;
                    default:
                        return false;
                }
            }
            catch (Exception exception) when (exception is OverflowException || exception is InvalidCastException || exception is FormatException)
            {
                return false;
            }
        }

        internal static long Long(JToken token, long fallback, long min = 0, long max = long.MaxValue)
            => TryLong(token, out long value) ? Math.Min(max, Math.Max(min, value)) : fallback;

        internal static int Int(JToken token, int fallback, int min = 0, int max = int.MaxValue)
            => TryLong(token, out long value) ? (int)Math.Min(max, Math.Max(min, value)) : fallback;

        internal static bool Bool(JToken token, bool fallback)
        {
            if (token == null) return fallback;
            if (token.Type == JTokenType.Boolean) return token.Value<bool>();
            if (token.Type == JTokenType.Integer && TryLong(token, out long value)) return value != 0;
            return fallback;
        }

        internal static string Str(JToken token, string fallback)
            => token != null && token.Type == JTokenType.String ? token.Value<string>() : fallback;

        /// <summary>JS 참·거짓(정본 <c>if (s &amp;&amp; s.up)</c>·<c>rb.open</c> 판정).</summary>
        internal static bool Truthy(JToken token)
        {
            if (token == null) return false;
            switch (token.Type)
            {
                case JTokenType.Null:
                case JTokenType.Undefined:
                    return false;
                case JTokenType.Boolean:
                    return token.Value<bool>();
                case JTokenType.Integer:
                case JTokenType.Float:
                    double number = token.Value<double>();
                    return number != 0 && !double.IsNaN(number);
                case JTokenType.String:
                    return token.Value<string>().Length != 0;
                default:
                    return true;
            }
        }

        /// <summary>비지 않은 문자열 목록. 중복 제거, 순서 유지.</summary>
        internal static JArray StringList(JToken token)
        {
            var result = new JArray();
            if (token is not JArray array) return result;
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (JToken item in array)
                if (item.Type == JTokenType.String && !string.IsNullOrWhiteSpace(item.Value<string>()) && seen.Add(item.Value<string>()))
                    result.Add(item.Value<string>());
            return result;
        }

        /// <summary>문자열 또는 정수 key 목록(퀘스트 수령 기록). 중복 제거, 순서 유지.</summary>
        internal static JArray KeyList(JToken token)
        {
            var result = new JArray();
            if (token is not JArray array) return result;
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (JToken item in array)
            {
                if (item.Type == JTokenType.String && !string.IsNullOrWhiteSpace(item.Value<string>()))
                {
                    if (seen.Add("s:" + item.Value<string>())) result.Add(item.Value<string>());
                }
                else if (item.Type == JTokenType.Integer && TryLong(item, out long number) && seen.Add("i:" + number)) result.Add(number);
            }
            return result;
        }

        /// <summary>key → 0 이상 정수. 고정 키는 항상 있고(기본 0) 먼저 온다. 나머지는 이름순.</summary>
        internal static JObject IntMap(JToken token, params string[] fixedKeys)
        {
            var source = token as JObject;
            var result = new JObject();
            foreach (string key in fixedKeys) result[key] = Int(source?[key], 0);
            if (source == null) return result;
            foreach (JProperty property in source.Properties().OrderBy(p => p.Name, StringComparer.Ordinal))
            {
                if (result.ContainsKey(property.Name) || string.IsNullOrWhiteSpace(property.Name)) continue;
                if (TryLong(property.Value, out _)) result[property.Name] = Int(property.Value, 0);
            }
            return result;
        }

        /// <summary>key → 객체 묶음. 각 객체는 <paramref name="entry"/>로 정리하고, 객체가 아닌 값은 버린다.</summary>
        internal static JObject EntryMap(JToken token, Func<JObject, JObject> entry)
        {
            var result = new JObject();
            if (token is not JObject source) return result;
            foreach (JProperty property in source.Properties().OrderBy(p => p.Name, StringComparer.Ordinal))
                if (!string.IsNullOrWhiteSpace(property.Name) && property.Value is JObject value) result[property.Name] = entry(value);
            return result;
        }

        /// <summary>0 이상 정수 목록. 정렬·중복 제거.</summary>
        internal static JArray SortedIntSet(JToken token)
        {
            var values = new SortedSet<long>();
            if (token is JArray array)
                foreach (JToken item in array)
                    if (TryLong(item, out long number) && number >= 0) values.Add(Math.Min(number, int.MaxValue));
            return new JArray(values.Select(value => (object)(int)value).ToArray());
        }

        internal static JObject Object(JToken token) => token is JObject value ? (JObject)value.DeepClone() : new JObject();
    }
}
