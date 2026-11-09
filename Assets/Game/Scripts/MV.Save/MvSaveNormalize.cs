using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;
using R = MV.Save.MvJsonRead;

namespace MV.Save
{
    /// <summary>
    /// 컨테이너별 값 정리(09 §9.3 필드표). 입력은 컨테이너 값(최상위 키 안쪽)이고 출력은 항상 같은 모양의 새 객체다.
    /// 빈 객체를 넣으면 기본값(= 정본 DEF 대응)이 나온다.
    /// </summary>
    internal static class MvSaveNormalize
    {
        internal static readonly string[] LobbyUpKeys = { "stair", "weapon", "income" };
        internal static readonly string[] AreaUpKeys = { "rate", "range", "clone", "crit", "dbl" };
        internal static readonly string[] PerkKeys = { "power", "crit", "obst", "dcap" };

        internal static Func<JObject, JObject> For(string key, MvSaveConfig config)
        {
            switch (key)
            {
                case MvSaveKeys.Progress: return value => Progress(value, config);
                case MvSaveKeys.Lobby: return value => Lobby(value, config);
                case MvSaveKeys.AreaUp: return AreaUp;
                case MvSaveKeys.Rebirth: return Rebirth;
                case MvSaveKeys.God: return value => God(value, config);
                case MvSaveKeys.Unlock: return Unlock;
                case MvSaveKeys.Daily: return Daily;
                case MvSaveKeys.PlayRes: return PlayRes;
                case MvSaveKeys.Mode: return Mode;
                case MvSaveKeys.Quest: return Quest;
                case MvSaveKeys.Research: return Research;
                case MvSaveKeys.Shop: return Shop;
                case MvSaveKeys.Pet:
                case MvSaveKeys.Friend:
                case MvSaveKeys.Gear:
                case MvSaveKeys.Pass:
                case MvSaveKeys.Blessing:
                    return Reserved;
                case MvSaveKeys.Settings: return Settings;
                case MvSaveKeys.Meta: return value => Meta(value, config);
                default: throw new ArgumentException("정리 규칙이 없는 컨테이너: " + key, nameof(key));
            }
        }

        // 9.3.1
        internal static JObject Progress(JObject v, MvSaveConfig c)
        {
            int area = R.Int(v["area"], 0);
            return new JObject
            {
                ["level"] = R.Int(v["level"], 1, 1),
                ["area"] = area,
                ["cp"] = R.Int(v["cp"], 0, 0, c.CpMax),
                ["bars"] = Bars(v["bars"], c),
                ["runs"] = R.Int(v["runs"], 0),
                ["zoneClear"] = R.Bool(v["zoneClear"], false),
                ["escaped"] = R.Bool(v["escaped"], false),
                ["bestArea"] = Math.Max(area, R.Int(v["bestArea"], 0)),
                ["lastSettledRunSeq"] = R.Long(v["lastSettledRunSeq"], 0),
            };
        }

        /// <summary>
        /// 벽 기록 <c>{"A{area}": [Floor별 [칸 idx → 남은 HP | null]]}</c>. 정본은 Floor를 <c>{"0":[..],"1":[..],"2":[..]}</c>
        /// 객체로 쓰므로 둘 다 받는다. 형식이 어긋난 Area 기록은 버린다(경고). 칸 수 ≤ rows×3 검사는 표가 필요해 참조 정리 몫이다.
        /// </summary>
        internal static JObject Bars(JToken token, MvSaveConfig c)
        {
            var result = new JObject();
            if (token is not JObject source) return result;
            foreach (JProperty property in source.Properties().OrderBy(p => p.Name, StringComparer.Ordinal))
            {
                JArray floors = BarFloors(property.Name, property.Value, c.FloorCount);
                if (floors == null) Debug.LogWarning("[MvSave] 형식이 어긋난 벽 기록을 버립니다: " + property.Name);
                else result[property.Name] = floors;
            }
            return result;
        }

        private static JArray BarFloors(string areaKey, JToken value, int floorCount)
        {
            if (areaKey.Length < 2 || areaKey[0] != 'A' || !areaKey.Skip(1).All(char.IsDigit)) return null;
            var floors = new JToken[floorCount];
            if (value is JArray array)
            {
                if (array.Count > floorCount) return null;
                for (int i = 0; i < array.Count; i++) floors[i] = array[i];
            }
            else if (value is JObject byIndex)
            {
                foreach (JProperty floor in byIndex.Properties())
                {
                    if (!int.TryParse(floor.Name, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out int index) || index >= floorCount) return null;
                    floors[index] = floor.Value;
                }
            }
            else return null;

            var result = new JArray();
            foreach (JToken floor in floors)
            {
                var cells = new JArray();
                if (floor != null && floor.Type != JTokenType.Null)
                {
                    if (floor is not JArray source) return null;
                    foreach (JToken cell in source)
                    {
                        if (cell.Type == JTokenType.Null) { cells.Add(JValue.CreateNull()); continue; }
                        if (cell.Type != JTokenType.Integer && cell.Type != JTokenType.Float) return null;
                        double hp = cell.Value<double>();
                        if (double.IsNaN(hp) || double.IsInfinity(hp) || hp < 0) return null;
                        // 정본 HP는 소수일 수 있다. 정수면 정수로 둔다.
                        cells.Add(Math.Floor(hp) == hp && hp <= long.MaxValue ? new JValue((long)hp) : new JValue(hp));
                    }
                }
                result.Add(cells);
            }
            return result;
        }

        // 9.3.2
        internal static JObject Lobby(JObject v, MvSaveConfig c) => new JObject
        {
            ["up"] = R.IntMap(v["up"], LobbyUpKeys),
            ["deck"] = Deck(v["deck"], c.StartDeck),
        };

        /// <summary>덱은 0 이상 정수의 비지 않은 배열. 아니면 시작 덱. 길이 맞춤(103 최대 tier+1)은 참조 정리 몫.</summary>
        internal static JArray Deck(JToken token, IReadOnlyList<int> fallback)
        {
            if (token is JArray array && array.Count > 0 && array.All(item => R.TryLong(item, out long n) && n >= 0))
                return new JArray(array.Select(item => (object)R.Int(item, 0)).ToArray());
            return new JArray(fallback.Select(n => (object)n).ToArray());
        }

        internal static JObject AreaUp(JObject v) => new JObject { ["lv"] = R.IntMap(v["lv"], AreaUpKeys) };

        internal static JObject Rebirth(JObject v) => new JObject
        {
            ["lv"] = R.Int(v["lv"], 0),
            ["pending"] = R.Int(v["pending"], 0),
            ["perks"] = R.IntMap(v["perks"], PerkKeys),
            ["cdUntil"] = R.Long(v["cdUntil"], 0),
        };

        internal static JObject God(JObject v, MvSaveConfig c)
        {
            string selected = R.Str(v["sel"], null);
            return new JObject
            {
                ["sel"] = string.IsNullOrWhiteSpace(selected) ? c.StartGodKey : selected,
                ["gods"] = R.EntryMap(v["gods"], god => new JObject { ["lv"] = R.Int(god["lv"], 0), ["shard"] = R.Int(god["shard"], 0) }),
            };
        }

        // 9.3.3
        internal static JObject Unlock(JObject v) => new JObject
        {
            ["opened"] = R.StringList(v["opened"]),
            ["seenUnlock"] = R.StringList(v["seenUnlock"]),
            ["seenNew"] = R.StringList(v["seenNew"]),
        };

        // 9.3.4 — 저장 칸만. 하루 넘김(Roll)은 MV.Meta 몫이다.
        internal static JObject Daily(JObject v)
        {
            string day = R.Str(v["day"], "");
            return new JObject
            {
                ["day"] = MvSaveTime.IsDayKey(day) ? day : "",
                ["ads"] = R.IntMap(v["ads"]),
                ["modePlays"] = R.IntMap(v["modePlays"]),
                ["free"] = R.IntMap(v["free"]),
            };
        }

        // 9.3.5 — 값 자체가 key별 묶음인 컨테이너는 최상위 키가 항목 key다.
        internal static JObject PlayRes(JObject v) => R.EntryMap(v, entry => new JObject { ["n"] = R.Int(entry["n"], 0), ["at"] = R.Long(entry["at"], 0) });

        internal static JObject Mode(JObject v) => R.EntryMap(v, entry => new JObject { ["best"] = R.Int(entry["best"], 0), ["first"] = R.SortedIntSet(entry["first"]) });

        internal static JObject Quest(JObject v) => new JObject
        {
            ["daily"] = QuestPeriod(v["daily"] as JObject ?? new JObject()),
            ["weekly"] = QuestPeriod(v["weekly"] as JObject ?? new JObject()),
            ["achieve"] = new JObject { ["cnt"] = R.IntMap((v["achieve"] as JObject)?["cnt"]), ["got"] = R.KeyList((v["achieve"] as JObject)?["got"]) },
            ["attend"] = Attend(v["attend"] as JObject ?? new JObject()),
            ["pass"] = R.Object(v["pass"]),
        };

        private static JObject QuestPeriod(JObject v) => new JObject
        {
            ["period"] = R.Str(v["period"], ""),
            ["cnt"] = R.IntMap(v["cnt"]),
            ["got"] = R.KeyList(v["got"]),
        };

        private static JObject Attend(JObject v) => new JObject
        {
            ["period"] = R.Str(v["period"], ""),
            ["days"] = R.Int(v["days"], 0),
            ["lastDay"] = R.Str(v["lastDay"], ""),
        };

        internal static JObject Research(JObject v)
        {
            JToken run = JValue.CreateNull();
            if (v["run"] is JObject source && !string.IsNullOrWhiteSpace(R.Str(source["key"], null)) && R.TryLong(source["finishAt"], out _))
                run = new JObject { ["key"] = R.Str(source["key"], null), ["finishAt"] = R.Long(source["finishAt"], 0) };
            return new JObject { ["lv"] = R.IntMap(v["lv"]), ["run"] = run };
        }

        /// <summary><c>guarantee</c> = 천장 셈 값(D-83, 키 <c>804_randomGuarantee</c> id). 1차는 비어 있다.</summary>
        internal static JObject Shop(JObject v) => new JObject
        {
            ["buys"] = R.EntryMap(v["buys"], entry => new JObject { ["n"] = R.Int(entry["n"], 0), ["at"] = R.Long(entry["at"], 0) }),
            ["guarantee"] = R.IntMap(v["guarantee"]),
        };

        /// <summary>2차 시스템 예약 칸. 1차는 <c>{}</c>, 받은 객체는 그대로 둔다.</summary>
        internal static JObject Reserved(JObject v) => (JObject)v.DeepClone();

        // 9.3.6
        internal static JObject Settings(JObject v) => new JObject
        {
            ["muted"] = R.Bool(v["muted"], false),
            ["contentTab"] = R.Str(v["contentTab"], ""),
        };

        internal static JObject Meta(JObject v, MvSaveConfig c) => new JObject
        {
            ["createdAt"] = R.Long(v["createdAt"], 0),
            ["lastSaveAt"] = R.Long(v["lastSaveAt"], 0),
            ["importSource"] = R.Str(v["importSource"], ""),
            ["tableVersion"] = R.Int(v["tableVersion"], c.TableVersion),
        };
    }
}
