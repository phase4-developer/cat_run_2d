using System;
using System.Collections.Generic;
using System.Linq;
using jjevol;
using Newtonsoft.Json.Linq;

namespace MV.Save
{
    /// <summary>
    /// 새 저장(09 §9.5.4, 정본 <c>DEF()</c> L844 대응). 모든 컨테이너 기본값 JSON을 만든다.
    /// 시작 덱·출전 신은 <see cref="MvSaveConfig"/>(= <c>00_config</c>)에서 읽는다.
    /// </summary>
    public static class MvSaveDefaults
    {
        public static JObject Create(MvSaveConfig config, long nowMs)
        {
            config ??= MvSaveConfig.CreateDefault();
            config.Validate();
            var payload = new JObject { [MvSaveKeys.Wealth] = WealthRows(null) };
            foreach (string key in MvSaveKeys.All)
            {
                if (key == MvSaveKeys.Wealth) continue;
                payload[key] = MvSaveNormalize.For(key, config)(new JObject());
            }
            payload[MvSaveKeys.Meta] = MvSaveNormalize.Meta(new JObject
            {
                ["createdAt"] = nowMs,
                ["lastSaveAt"] = nowMs,
                ["tableVersion"] = config.TableVersion,
            }, config);
            return payload;
        }

        public static string CreateJson(MvSaveConfig config, long nowMs) => MvSaveJson.ToJson(Create(config, nowMs));

        /// <summary>
        /// 재화 행 배열(<c>WealthContainer</c> 형식, type 이름순). 모든 재화를 0으로 두고 <paramref name="quantities"/>만 채운다.
        /// 재화 목록은 <see cref="MvWealthIds"/> — 표(<c>01_wealth</c>)가 들어오면 그 행으로 바꿀 자리.
        /// </summary>
        internal static JArray WealthRows(IReadOnlyDictionary<int, long> quantities)
        {
            var rows = new JArray();
            foreach (int id in MvWealthIds.AllIds.OrderBy(MvWealthIds.TypeOf, StringComparer.Ordinal))
            {
                long quantity = quantities != null && quantities.TryGetValue(id, out long value) ? Math.Max(0, value) : 0;
                rows.Add(new JObject { [WealthContainer.TypeKey] = MvWealthIds.TypeOf(id), [WealthContainer.QuantityKey] = quantity });
            }
            return rows;
        }
    }
}
