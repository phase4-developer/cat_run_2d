using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;
using R = MV.Save.MvJsonRead;

namespace MV.Save
{
    /// <summary>
    /// 참조 정리(09 §9.6)가 보는 표 값. MV.Save는 표 타입을 모르므로 Boot가 <c>MvTables.Current</c>에서 채워 넘긴다.
    /// 비운 칸(null·음수)은 그 규칙을 건너뛴다(표가 아직 없음).
    /// </summary>
    public sealed class MvSaveReferenceCatalog
    {
        /// <summary><c>113_god.key</c>. <c>mvGod.sel</c>이 여기 없으면 <c>00_config.startGodKey</c>.</summary>
        public ICollection<string> GodKeys { get; set; }
        /// <summary><c>103_item</c> 최대 tier. 덱 길이 = 이 값 + 1. 음수면 건너뜀.</summary>
        public int MaxWeaponTier { get; set; } = -1;
        /// <summary><c>302_area</c> 마지막 행의 Area 인덱스(0기반 = areaNo − 1). 음수면 건너뜀.</summary>
        public int MaxAreaIndex { get; set; } = -1;
        /// <summary>Area 인덱스 → Floor별 최대 칸 수(rows×3). 표에 없는 Area는 키가 없다. null이면 건너뜀.</summary>
        public IReadOnlyDictionary<int, int[]> BarCellLimits { get; set; }
        /// <summary><c>10_lobbyUpgrade</c> key → max.</summary>
        public IReadOnlyDictionary<string, int> LobbyUpMax { get; set; }
        /// <summary><c>13_areaUpgrade</c> key → max.</summary>
        public IReadOnlyDictionary<string, int> AreaUpMax { get; set; }
        /// <summary><c>14_rebirthPerk</c> key → max.</summary>
        public IReadOnlyDictionary<string, int> PerkMax { get; set; }
        /// <summary><c>08_adPlacement.key</c>(<c>mvDaily.ads</c>).</summary>
        public ICollection<string> AdPlacementKeys { get; set; }
        /// <summary><c>400_content.key</c>(<c>mvDaily.modePlays</c>). 표가 아직 없으면 null.</summary>
        public ICollection<string> ModeContentKeys { get; set; }
        /// <summary><c>05_contentOpen.key</c>(<c>mvUnlock</c> 세 목록).</summary>
        public ICollection<string> ContentOpenKeys { get; set; }
    }

    /// <summary>
    /// 표가 바뀐 뒤 저장 정리(09 §9.6, 07 §7.13.4). 저장을 연 직후(부팅 6단계 끝) 돈다.
    /// 바뀐 것이 있으면 한 번의 <c>ApplyAndSave</c>로 저장하고 경고 로그 1줄(바뀐 필드 목록)을 남긴다.
    /// </summary>
    public static class MvSaveReferenceFix
    {
        /// <summary>정리하고 저장한다. 돌려주는 값은 바뀐 필드(<c>컨테이너.필드</c>) 목록, 없으면 빈 목록.</summary>
        public static IReadOnlyList<string> Run(MvSaveStore store, MvSaveReferenceCatalog catalog, MvSaveConfig config)
        {
            if (store == null) throw new ArgumentNullException(nameof(store));
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            if (config == null) throw new ArgumentNullException(nameof(config));

            JObject payload = MvSaveJson.ParseObject(store.ExportPayloadJson());
            var changed = new List<string>();
            JObject data = Fix(payload, catalog, config, changed);
            if (changed.Count == 0) return Array.AsReadOnly(Array.Empty<string>());

            store.ApplyAndSave(data, data.Properties().Select(p => p.Name).ToArray());
            Debug.LogWarning("[MvSave] 표에 맞춰 저장을 정리했습니다: " + string.Join(", ", changed));
            return changed.AsReadOnly();
        }

        /// <summary>
        /// 정리 결과 중 값이 바뀐 컨테이너만 담은 묶음(병합용, 컨테이너 값 전체). 각 컨테이너는 MvSaveNormalize를 거친 뒤 비교한다.
        /// </summary>
        internal static JObject Fix(JObject payload, MvSaveReferenceCatalog catalog, MvSaveConfig config, List<string> changed)
        {
            var data = new JObject();
            Apply(data, payload, MvSaveKeys.God, config, changed, value => FixGod(value, catalog, config));
            Apply(data, payload, MvSaveKeys.Lobby, config, changed, value => FixLobby(value, catalog));
            Apply(data, payload, MvSaveKeys.Progress, config, changed, value => FixProgress(value, catalog));
            Apply(data, payload, MvSaveKeys.AreaUp, config, changed, value => ClampMap(value, "lv", catalog.AreaUpMax));
            Apply(data, payload, MvSaveKeys.Rebirth, config, changed, value => ClampMap(value, "perks", catalog.PerkMax));
            Apply(data, payload, MvSaveKeys.Daily, config, changed, value =>
            {
                KeepKeys(value, "ads", catalog.AdPlacementKeys);
                KeepKeys(value, "modePlays", catalog.ModeContentKeys);
            });
            Apply(data, payload, MvSaveKeys.Unlock, config, changed, value =>
            {
                KeepListed(value, "opened", catalog.ContentOpenKeys);
                KeepListed(value, "seenUnlock", catalog.ContentOpenKeys);
                KeepListed(value, "seenNew", catalog.ContentOpenKeys);
            });
            // 09 §9.3.6: 마지막으로 정리·저장한 표 묶음 버전.
            Apply(data, payload, MvSaveKeys.Meta, config, changed, value => value["tableVersion"] = config.TableVersion);
            return data;
        }

        private static void Apply(JObject data, JObject payload, string key, MvSaveConfig config, List<string> changed, Action<JObject> fix)
        {
            JObject current = payload[key] as JObject ?? new JObject();
            var next = (JObject)current.DeepClone();
            fix(next);
            next = MvSaveNormalize.For(key, config)(next);
            bool any = false;
            foreach (string field in current.Properties().Select(p => p.Name).Union(next.Properties().Select(p => p.Name), StringComparer.Ordinal))
            {
                if (JToken.DeepEquals(current[field], next[field])) continue;
                changed.Add(key + "." + field);
                any = true;
            }
            if (any) data[key] = next;
        }

        private static void FixGod(JObject value, MvSaveReferenceCatalog catalog, MvSaveConfig config)
        {
            if (catalog.GodKeys == null) return;
            string selected = R.Str(value["sel"], null);
            if (selected == null || !catalog.GodKeys.Contains(selected)) value["sel"] = config.StartGodKey;
        }

        /// <summary>덱 길이를 최대 tier + 1로 맞춘다. 모자라면 0으로 채우고, 넘치는 등급의 수는 최대 등급 칸에 더한다.</summary>
        private static void FixLobby(JObject value, MvSaveReferenceCatalog catalog)
        {
            ClampMap(value, "up", catalog.LobbyUpMax);
            if (catalog.MaxWeaponTier < 1 || value["deck"] is not JArray deck) return;
            int length = catalog.MaxWeaponTier + 1;
            if (deck.Count == length) return;
            var counts = new long[length];
            for (int i = 0; i < deck.Count; i++)
            {
                long n = R.Long(deck[i], 0);
                int slot = Math.Min(i, length - 1);
                counts[slot] = Math.Min(int.MaxValue, counts[slot] + n);
            }
            value["deck"] = new JArray(counts.Select(n => (object)(int)n).ToArray());
        }

        private static void FixProgress(JObject value, MvSaveReferenceCatalog catalog)
        {
            if (catalog.MaxAreaIndex >= 0)
            {
                if (R.Int(value["area"], 0) > catalog.MaxAreaIndex)
                {
                    value["area"] = catalog.MaxAreaIndex;
                    value["cp"] = 0;
                    value["bars"] = new JObject();
                }
                if (R.Int(value["bestArea"], 0) > catalog.MaxAreaIndex) value["bestArea"] = catalog.MaxAreaIndex;
            }
            if (catalog.BarCellLimits == null || value["bars"] is not JObject bars) return;
            foreach (JProperty area in bars.Properties().ToList())
            {
                if (!BarsFit(area, catalog.BarCellLimits))
                {
                    Debug.LogWarning("[MvSave] 표와 맞지 않는 벽 기록을 버립니다: " + area.Name);
                    bars.Remove(area.Name);
                }
            }
        }

        /// <summary>Area가 표에 있고 Floor별 칸 수가 rows×3 이하인지(09 §9.3.1).</summary>
        private static bool BarsFit(JProperty area, IReadOnlyDictionary<int, int[]> limits)
        {
            if (area.Name.Length < 2 || !int.TryParse(area.Name.Substring(1), System.Globalization.NumberStyles.None,
                    System.Globalization.CultureInfo.InvariantCulture, out int index)) return false;
            if (!limits.TryGetValue(index, out int[] floors) || area.Value is not JArray cells) return false;
            for (int floor = 0; floor < cells.Count; floor++)
            {
                int count = cells[floor] is JArray list ? list.Count : 0;
                int limit = floor < floors.Length ? floors[floor] : 0;
                if (count > limit) return false;
            }
            return true;
        }

        /// <summary>강화 단계: 표에 없는 key는 버리고, max를 넘으면 max로 자른다(09 §9.3.2).</summary>
        private static void ClampMap(JObject value, string field, IReadOnlyDictionary<string, int> max)
        {
            if (max == null || value[field] is not JObject map) return;
            foreach (JProperty entry in map.Properties().ToList())
            {
                if (!max.TryGetValue(entry.Name, out int limit)) map.Remove(entry.Name);
                else if (R.Int(entry.Value, 0) > limit) entry.Value = Math.Max(0, limit);
            }
        }

        private static void KeepKeys(JObject value, string field, ICollection<string> keys)
        {
            if (keys == null || value[field] is not JObject map) return;
            foreach (JProperty entry in map.Properties().ToList())
                if (!keys.Contains(entry.Name)) map.Remove(entry.Name);
        }

        private static void KeepListed(JObject value, string field, ICollection<string> keys)
        {
            if (keys == null || value[field] is not JArray list) return;
            value[field] = new JArray(list.Where(item => item.Type == JTokenType.String && keys.Contains(item.Value<string>())));
        }
    }
}
