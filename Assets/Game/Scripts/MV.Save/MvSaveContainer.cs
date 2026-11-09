using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using jjevol;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace MV.Save
{
    /// <summary>
    /// MoonVillage 저장 컨테이너 공통 부모. 상태는 정리된 JSON 객체 하나이고 만든 뒤 바꾸지 않는다.
    /// 병합(<c>replace = false</c>)은 받은 최상위 필드만 바꾸고 나머지는 지금 값을 유지한다. 필드에 <c>null</c>을 주면 그 필드를 지운다
    /// (고정 필드는 기본값으로 돌아간다). 교체(<c>replace = true</c>)는 빠진 필드를 기본값으로 채운다.
    /// 바뀐 항목 키 = 값이 달라진 최상위 필드 이름.
    /// </summary>
    public abstract class MvSaveContainer : DataContainer
    {
        private sealed class Snapshot
        {
            internal readonly JObject Value;
            internal Snapshot(JObject value) { Value = value; }
        }

        private readonly Func<JObject, JObject> _normalize;

        protected MvSaveContainer(string dataKey, Func<JObject, JObject> normalize)
            : base(dataKey, new Snapshot(Normalized(normalize, new JObject())))
        {
            _normalize = normalize;
        }

        private static JObject Normalized(Func<JObject, JObject> normalize, JObject value)
        {
            if (normalize == null) throw new ArgumentNullException(nameof(normalize));
            return normalize(value) ?? throw new InvalidOperationException("정리 결과가 없습니다.");
        }

        /// <summary>현재 값. 읽기 전용으로만 쓴다(상태 객체를 바꾸면 안 된다).</summary>
        protected JObject Value => ((Snapshot)State).Value;

        /// <summary>현재 값 JSON(최상위 키 안쪽). 개발 도구·시험용.</summary>
        public string ToJsonString() => Value.ToString(Formatting.None);

        protected override StateChange ParseState(JObject data, bool replace)
        {
            var current = (Snapshot)State;
            if (!data.TryGetValue(DataKey, out JToken token))
            {
                if (replace) throw new InvalidDataException("교체할 컨테이너 값이 없습니다: " + DataKey);
                return new StateChange(current, Array.Empty<string>());
            }
            if (token is not JObject input) throw new InvalidDataException("컨테이너 값은 객체여야 합니다: " + DataKey);

            JObject merged = replace ? new JObject() : (JObject)current.Value.DeepClone();
            foreach (JProperty property in input.Properties())
            {
                if (property.Value.Type == JTokenType.Null) merged.Remove(property.Name);
                else merged[property.Name] = property.Value.DeepClone();
            }
            JObject next = Normalized(_normalize, merged);

            string[] changed = current.Value.Properties().Select(p => p.Name)
                .Union(next.Properties().Select(p => p.Name), StringComparer.Ordinal)
                .Where(name => !JToken.DeepEquals(current.Value[name], next[name]))
                .ToArray();
            return changed.Length == 0 ? new StateChange(current, changed) : new StateChange(new Snapshot(next), changed);
        }

        protected override JObject WriteState(object state) => new JObject { [DataKey] = ((Snapshot)state).Value.DeepClone() };

        // 읽기 도우미
        protected int ReadInt(string field) => Value.Value<int>(field);
        protected long ReadLong(string field) => Value.Value<long>(field);
        protected bool ReadBool(string field) => Value.Value<bool>(field);
        protected string ReadString(string field) => Value.Value<string>(field);

        protected static IReadOnlyDictionary<string, int> IntMapOf(JToken map)
            => map is JObject source
                ? source.Properties().ToDictionary(p => p.Name, p => p.Value.Value<int>(), StringComparer.Ordinal)
                : new Dictionary<string, int>(StringComparer.Ordinal);

        protected static IReadOnlyList<string> StringListOf(JToken list)
            => list is JArray source ? Array.AsReadOnly(source.Select(item => item.ToString()).ToArray()) : Array.AsReadOnly(Array.Empty<string>());
    }
}
