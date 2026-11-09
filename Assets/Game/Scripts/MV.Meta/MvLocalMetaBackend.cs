using System;
using System.Collections.Generic;
using System.Linq;
using jjevol;
using MV.Data;
using MV.Save;
using Newtonsoft.Json.Linq;

namespace MV.Meta
{
    /// <summary>
    /// 로컬 백엔드(서버 없음). 요청을 정본 규칙(04 §4.11~4.12, 07 §7.8, 09 §9.4)으로 판정하고 바뀔 컨테이너 값만 계산한다.
    /// 상태는 바꾸지 않는다 — 반영은 <see cref="MvMeta"/>가 한 번의 <c>ApplyAndSave</c>로 한다(09 원칙 3).
    /// Framework 대체: platform.network <c>INetworkLocalService</c> 자리. 서버가 생기면 같은 이름·파라미터를 서버 함수가 받는다.
    /// </summary>
    public sealed class MvLocalMetaBackend : IMvMetaBackend
    {
        private readonly Func<MvSaveStore> _store;
        private readonly Func<ResourceService> _resources;
        private readonly MvTables _t;
        private readonly IMvClock _clock;

        public MvLocalMetaBackend(Func<MvSaveStore> store, Func<ResourceService> resources, MvTables tables, IMvClock clock)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _resources = resources ?? throw new ArgumentNullException(nameof(resources));
            _t = tables ?? throw new ArgumentNullException(nameof(tables));
            _clock = clock ?? MvSystemClock.Instance;
        }

        private MvSaveStore S => _store();
        private ResourceService Res => _resources();
        private ConfigData Config => _t.Config.Value;

        public MvMetaResponse Handle(MvMetaRequest request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            MvSaveStore s = S;
            if (s == null || s.IsDisposed || Res == null || Res.IsDisposed) return MvMetaResponse.Reject(MvMetaErrors.NoStore);
            JObject p = request.Params;
            switch (request.Name)
            {
                case MvMetaRequestNames.Settle: return Settle(p);
                case MvMetaRequestNames.BuyLobbyUp: return BuyLobbyUp(Str(p, "key"));
                case MvMetaRequestNames.BuyAreaUp: return BuyAreaUp(Str(p, "key"));
                case MvMetaRequestNames.BuyPerk: return BuyPerk(Str(p, "key"));
                case MvMetaRequestNames.Rebirth: return Rebirth();
                case MvMetaRequestNames.SelectGod: return SelectGod(Str(p, "key"));
                case MvMetaRequestNames.GrantAd: return GrantAd(Str(p, "key"));
                case MvMetaRequestNames.UseAd: return UseAd(Str(p, "key"));
                case MvMetaRequestNames.RecordModePlay: return RecordModePlay(Str(p, "key"));
                case MvMetaRequestNames.MarkSeen: return MarkSeen(p);
                default: return MvMetaResponse.Reject(MvMetaErrors.UnknownRequest, request.Name);
            }
        }

        // ───────── 결과 정산(04 §4.11.2 collect) ─────────

        private MvMetaResponse Settle(JObject p)
        {
            MvSaveStore s = S;
            long seq = p.Value<long?>("runSeq") ?? 0;
            int mul = p.Value<int?>("mul") ?? 1;
            if (seq <= s.Progress.LastSettledRunSeq) return MvMetaResponse.Reject(MvMetaErrors.AlreadySettled, "runSeq " + seq);
            if (mul != 1 && mul != CollectAdMul()) return MvMetaResponse.Reject(MvMetaErrors.BadMul, "mul " + mul);
            if (!Enum.TryParse(Str(p, "end") ?? "", out MvRunEnd end)) return MvMetaResponse.Reject(MvMetaErrors.BadParams, "end");
            double gold = Math.Max(0, p.Value<double?>("gold") ?? 0);
            int souls = Math.Max(0, p.Value<int?>("souls") ?? 0);
            int dia = Math.Max(0, p.Value<int?>("dia") ?? 0);
            int cpRun = Math.Max(0, p.Value<int?>("cpRun") ?? 0);

            int area = s.Progress.Area;
            bool gate = end == MvRunEnd.Gate;
            bool enough = Res.GetRaw(MvWealthIds.Souls) + souls >= MvMetaRules.NeedSouls(_t, area);
            bool last = MvMetaRules.IsLastArea(_t, area);
            bool zoneEnd = gate && enough && last;
            bool pass = gate && enough && !last;
            int tok = MvMetaRules.Tokens(_t, souls, gate, pass || zoneEnd);

            var wealth = new WealthDelta();
            long goldGain = (long)Math.Floor(gold * mul);
            long diaGain = (long)dia * mul;
            wealth.Gain(MvWealthIds.Gold, goldGain);
            wealth.Gain(MvWealthIds.Dia, diaGain);
            wealth.Gain(MvWealthIds.Souls, souls);

            // 런 안 정산 대기(07 §7.9): 재화와 조각 대기만 반영한다.
            int pendingExtra = 0;
            var unsettled = new JArray();
            if (p["pending"] is JArray pendingList)
                foreach (JToken item in pendingList)
                {
                    int type = item.Value<int>("t"), id = item.Value<int>("i");
                    double value = item.Value<double>("v");
                    if (type == (int)ConsumeType.Wealth && MvWealthIds.TypeOf(id) != null) wealth.Gain(id, (long)Math.Floor(value));
                    else if (type == (int)ConsumeType.RbPending) pendingExtra += (int)Math.Floor(value);
                    else unsettled.Add(item.DeepClone());
                }

            int areaAfter = pass ? area + 1 : area;
            int runsAfter = s.Progress.Runs + 1;
            var progress = new JObject
            {
                ["level"] = s.Progress.Level + 1,
                ["runs"] = runsAfter,
                ["lastSettledRunSeq"] = seq,
                ["cp"] = pass ? 0 : Math.Max(s.Progress.Cp, cpRun),
                ["area"] = areaAfter,
            };
            var bars = new JObject();
            if (!pass && p["bars"] is JArray rec) bars[MvProgressContainer.BarKey(area)] = rec.DeepClone();
            progress["bars"] = bars;
            if (zoneEnd) progress["zoneClear"] = true;

            var data = new JObject
            {
                [MvSaveKeys.Progress] = progress,
                [MvSaveKeys.Rebirth] = new JObject { ["pending"] = s.Rebirth.Pending + tok + pendingExtra },
            };
            if (!wealth.TryWrite(Res, data, out string fail)) return MvMetaResponse.Reject(MvMetaErrors.NotEnough, fail);
            if (pass)
            {
                // 개방: 로비 강화 3종·시작 덱·Area 강화 초기화(L2433~2434). null = 기본값.
                data[MvSaveKeys.Lobby] = new JObject { ["up"] = null, ["deck"] = null };
                data[MvSaveKeys.AreaUp] = new JObject { ["lv"] = null };
            }

            IReadOnlyList<string> newly = WriteUnlocks(data, area, s.Progress.Runs, areaAfter, runsAfter);
            var result = new JObject
            {
                ["areaBefore"] = area, ["areaAfter"] = areaAfter, ["pass"] = pass, ["zoneEnd"] = zoneEnd, ["mul"] = mul,
                ["gold"] = goldGain, ["dia"] = diaGain, ["souls"] = souls, ["tok"] = tok,
                ["zoneChanged"] = MvMetaRules.ZoneIndex(_t, area) != MvMetaRules.ZoneIndex(_t, areaAfter),
                ["newlyOpened"] = new JArray(newly.Cast<object>().ToArray()),
                ["interstitial"] = mul == 1 && InterstitialDue(areaAfter),
                ["unsettled"] = unsettled,
            };
            return MvMetaResponse.Success(data, result);
        }

        private int CollectAdMul()
        {
            var row = _t.AdPlacement.ByKey("collect3");
            return row != null && row.mul > 0 ? row.mul : Config.collectAdMul;
        }

        /// <summary>D-24 전면 광고: <c>area ≥ minArea</c>(08 interstitial, 없으면 00_config). 광고 제거(noAds)면 건너뛴다.</summary>
        internal bool InterstitialDue(int areaAfter)
        {
            var row = _t.AdPlacement.ByKey("interstitial");
            if (row != null && !row.active) return false;
            int minArea = row?.minArea ?? Config.interstitialMinArea;
            if (areaAfter < minArea) return false;
            return !(row != null && row.noAdsSkip && Res.GetRaw(MvWealthIds.NoAds) > 0);
        }

        // ───────── 해금(07 §7.8.9) ─────────

        /// <summary>열림 = sticky 기록 또는 조건 성립.</summary>
        internal bool IsOpen(ContentOpenData row, int area, int runs, IReadOnlyCollection<string> opened)
            => row != null && row.active && ((row.sticky && opened.Contains(row.key)) || MvMetaRules.ConditionMet(row, area, runs));

        /// <summary>조건이 성립한 sticky 행을 opened에 넣고, 이전에 닫혀 있다가 새로 열린 key를 돌려준다.</summary>
        private IReadOnlyList<string> WriteUnlocks(JObject data, int areaBefore, int runsBefore, int areaAfter, int runsAfter)
        {
            var opened = S.Unlock.Opened.ToList();
            var newly = new List<string>();
            bool changed = false;
            foreach (var row in _t.ContentOpen.Sorted)
            {
                if (string.IsNullOrEmpty(row.key)) continue;
                bool before = IsOpen(row, areaBefore, runsBefore, opened);
                bool after = IsOpen(row, areaAfter, runsAfter, opened);
                if (row.sticky && after && !opened.Contains(row.key)) { opened.Add(row.key); changed = true; }
                if (after && !before) newly.Add(row.key);
            }
            if (changed) data[MvSaveKeys.Unlock] = new JObject { ["opened"] = new JArray(opened.Cast<object>().ToArray()) };
            return newly;
        }

        // ───────── 성장 구매(07 §7.8.7) ─────────

        private MvMetaResponse BuyLobbyUp(string key)
        {
            var row = _t.LobbyUpgrade.ByKey(key);
            if (row == null || !row.active) return MvMetaResponse.Reject(MvMetaErrors.UnknownKey, key);
            MvSaveStore s = S;
            int bought = s.Lobby.GetUp(key);
            var up = ToJObject(s.Lobby.Up);
            up[key] = bought + 1;
            var container = new JObject { ["up"] = up };
            var result = new JObject { ["key"] = key, ["before"] = 0, ["after"] = 0, ["tier"] = 0, ["merged"] = 0 };
            switch (row.effectType)
            {
                case LobbyEffectType.StartStairLv:
                    result["before"] = MvMetaRules.StairStart(_t, bought).Lv;
                    result["after"] = MvMetaRules.StairStart(_t, bought + 1).Lv;
                    break;
                case LobbyEffectType.DeckAdd:
                    int tier = MvMetaRules.WeaponLevel(_t, bought).Lv;
                    int[] deck = s.Lobby.Deck.ToArray();
                    if (deck.Length <= tier) Array.Resize(ref deck, tier + 1);
                    result["tier"] = tier;
                    result["merged"] = MvMetaRules.DeckAdd(deck, tier);
                    result["before"] = tier;
                    result["after"] = MvMetaRules.WeaponLevel(_t, bought + 1).Lv;
                    container["deck"] = new JArray(deck.Cast<object>().ToArray());
                    break;
            }
            return Buy(row, bought, MvSaveKeys.Lobby, container, result);
        }

        private MvMetaResponse BuyAreaUp(string key)
        {
            var row = _t.AreaUpgrade.ByKey(key);
            if (row == null || !row.active) return MvMetaResponse.Reject(MvMetaErrors.UnknownKey, key);
            int bought = S.AreaUp.GetLevel(key);
            var lv = ToJObject(S.AreaUp.Levels);
            lv[key] = bought + 1;
            return Buy(row, bought, MvSaveKeys.AreaUp, new JObject { ["lv"] = lv }, new JObject { ["key"] = key, ["after"] = bought + 1 });
        }

        private MvMetaResponse BuyPerk(string key)
        {
            var row = _t.RebirthPerk.ByKey(key);
            if (row == null || !row.active) return MvMetaResponse.Reject(MvMetaErrors.UnknownKey, key);
            int bought = S.Rebirth.GetPerk(key);
            var perks = ToJObject(S.Rebirth.Perks);
            perks[key] = bought + 1;
            return Buy(row, bought, MvSaveKeys.Rebirth, new JObject { ["perks"] = perks }, new JObject { ["key"] = key, ["after"] = bought + 1 });
        }

        private MvMetaResponse Buy(ILevelPriced row, int bought, string containerKey, JObject container, JObject result)
        {
            if (bought >= row.max) return MvMetaResponse.Reject(MvMetaErrors.Max, row.key);
            var price = MvMetaRules.NextPrice(_t, row.levelPriceGroup, bought);
            if (price == null) return MvMetaResponse.Reject(MvMetaErrors.Max, row.key);
            var wealth = new WealthDelta();
            foreach (var cost in price)
            {
                if (cost.Type != ConsumeType.Wealth) return MvMetaResponse.Reject(MvMetaErrors.BadParams, "비용 종류 " + cost);
                wealth.Cost(cost.Id, (long)Math.Floor(cost.Value));
            }
            var data = new JObject { [containerKey] = container };
            if (!wealth.TryWrite(Res, data, out string fail)) return MvMetaResponse.Reject(MvMetaErrors.NotEnough, fail);
            return MvMetaResponse.Success(data, result);
        }

        // ───────── 부활(04 §4.12.3 doRebirth) ─────────

        internal bool RebirthOpen()
        {
            var row = _t.ContentOpen.ByKey(MvMetaRules.RebirthKey);
            return IsOpen(row, S.Progress.Area, S.Progress.Runs, S.Unlock.Opened);
        }

        internal long RebirthCooldownLeftMs() => Math.Max(0, S.Rebirth.CdUntil - _clock.NowMs);

        private MvMetaResponse Rebirth()
        {
            MvSaveStore s = S;
            if (!RebirthOpen()) return MvMetaResponse.Reject(MvMetaErrors.Locked, MvMetaRules.RebirthKey);
            if (RebirthCooldownLeftMs() > 0) return MvMetaResponse.Reject(MvMetaErrors.Cooldown);
            int got = s.Rebirth.Pending;
            var wealth = new WealthDelta();
            wealth.Set(MvWealthIds.Gold, 0);
            wealth.Set(MvWealthIds.Souls, 0);
            wealth.Gain(MvWealthIds.RbToken, got);
            var data = new JObject
            {
                // 유지: level·runs·bestArea·정산 순번(정본 keep L2560 + [창작] 09 §9.2). 나머지는 기본값(null).
                [MvSaveKeys.Progress] = new JObject { ["area"] = null, ["cp"] = null, ["bars"] = null, ["zoneClear"] = null, ["escaped"] = null },
                [MvSaveKeys.Lobby] = new JObject { ["up"] = null, ["deck"] = null },
                [MvSaveKeys.AreaUp] = new JObject { ["lv"] = null },
                [MvSaveKeys.Rebirth] = new JObject
                {
                    ["lv"] = s.Rebirth.Lv + 1,
                    ["pending"] = 0,
                    ["cdUntil"] = _clock.NowMs + (long)Config.rebirthCooldownSec * 1000L,
                },
            };
            if (!wealth.TryWrite(Res, data, out string fail)) return MvMetaResponse.Reject(MvMetaErrors.NotEnough, fail);
            // 지금 열려 있는 sticky 기능은 Area가 0으로 돌아가도 열린 채 둔다(rb.open 대응).
            WriteUnlocks(data, s.Progress.Area, s.Progress.Runs, s.Progress.Area, s.Progress.Runs);
            return MvMetaResponse.Success(data, new JObject { ["got"] = got, ["lv"] = s.Rebirth.Lv + 1 });
        }

        // ───────── 신 선택(04 §4.12.5) ─────────

        internal bool GodUnlocked(GodData god)
        {
            if (god == null || !god.active) return false;
            switch (god.unlockType)
            {
                case 0: return true;
                case 1:
                    var open = _t.ContentOpen.Get(god.openId);
                    return IsOpen(open, S.Progress.Area, S.Progress.Runs, S.Unlock.Opened);
                default: return S.God.GodKeys.Contains(god.key); // 2 구매: 08 신 시스템(MV-META-god)이 mvGod.gods에 넣는다.
            }
        }

        private MvMetaResponse SelectGod(string key)
        {
            var god = _t.God.ByKey(key);
            if (god == null) return MvMetaResponse.Reject(MvMetaErrors.UnknownKey, key);
            if (!GodUnlocked(god)) return MvMetaResponse.Reject(MvMetaErrors.Locked, key);
            return MvMetaResponse.Success(new JObject { [MvSaveKeys.God] = new JObject { ["sel"] = key } }, new JObject { ["key"] = key });
        }

        // ───────── 광고 한도·일일 초기화(04 §4.12.4, 09 §9.3.4) ─────────

        internal string Today() => MvSaveTime.DayKey(_clock.NowMs, Config.adDayUtcOffsetHour);

        /// <summary>오늘 기준 하루 값(Roll). 날이 바뀌었으면 빈 값. 읽기만 하고 저장하지 않는다.</summary>
        internal JObject RolledDaily(out bool rolled)
        {
            MvDailyContainer d = S.Daily;
            string today = Today();
            rolled = d.Day != today;
            return rolled
                ? new JObject { ["day"] = today, ["ads"] = new JObject(), ["modePlays"] = new JObject(), ["free"] = new JObject() }
                : new JObject { ["day"] = today, ["ads"] = ToJObject(d.Ads), ["modePlays"] = ToJObject(d.ModePlays), ["free"] = ToJObject(d.Free) };
        }

        internal int AdUsedToday(string key) => RolledDaily(out _)["ads"]?.Value<int?>(key) ?? 0;

        /// <summary>남은 횟수. 한도 없음(0)이면 −1, 모르는 자리면 0.</summary>
        internal int AdsLeft(string key)
        {
            var row = _t.AdPlacement.ByKey(key);
            if (row == null || !row.active) return 0;
            if (row.dailyLimit <= 0) return -1;
            return Math.Max(0, row.dailyLimit - AdUsedToday(key));
        }

        private MvMetaResponse GrantAd(string key)
        {
            var row = _t.AdPlacement.ByKey(key);
            if (row == null || !row.active) return MvMetaResponse.Reject(MvMetaErrors.UnknownKey, key);
            if (AdsLeft(key) == 0) return MvMetaResponse.Reject(MvMetaErrors.DailyLimit, key);
            if (row.adReward == null || row.adReward.Count == 0) return MvMetaResponse.Reject(MvMetaErrors.NoReward, key);

            var data = new JObject();
            var wealth = new WealthDelta();
            var result = new JObject { ["key"] = key };
            foreach (var reward in row.adReward)
            {
                switch (reward.Type)
                {
                    case ConsumeType.Wealth:
                        wealth.Gain(reward.Id, (long)Math.Floor(reward.Value));
                        result["wealthId"] = reward.Id;
                        result["amount"] = (long)Math.Floor(reward.Value);
                        break;
                    case ConsumeType.RebirthCooldownSec:
                        long cdUntil = S.Rebirth.CdUntil;
                        if (cdUntil <= _clock.NowMs) return MvMetaResponse.Reject(MvMetaErrors.NoCooldown, key);
                        data[MvSaveKeys.Rebirth] = new JObject { ["cdUntil"] = cdUntil - (long)Math.Floor(reward.Value) * 1000L };
                        result["cooldownSec"] = (long)Math.Floor(reward.Value);
                        break;
                    default:
                        // 입장권·축복 등 08 시스템 보상은 그 계약(MV-META-<시스템>)이 처리한다.
                        return MvMetaResponse.Reject(MvMetaErrors.NoReward, key + " " + reward);
                }
            }
            if (!wealth.TryWrite(Res, data, out string fail)) return MvMetaResponse.Reject(MvMetaErrors.NotEnough, fail);
            data[MvSaveKeys.Daily] = CountDaily("ads", key);
            return MvMetaResponse.Success(data, result);
        }

        /// <summary>보상 없이 횟수만 쓰는 광고(한도 있는 자리). 한도 없는 자리는 저장하지 않는다.</summary>
        private MvMetaResponse UseAd(string key)
        {
            var row = _t.AdPlacement.ByKey(key);
            if (row == null || !row.active) return MvMetaResponse.Reject(MvMetaErrors.UnknownKey, key);
            if (row.dailyLimit <= 0) return MvMetaResponse.Success(new JObject(), new JObject { ["key"] = key });
            if (AdsLeft(key) == 0) return MvMetaResponse.Reject(MvMetaErrors.DailyLimit, key);
            return MvMetaResponse.Success(new JObject { [MvSaveKeys.Daily] = CountDaily("ads", key) }, new JObject { ["key"] = key });
        }

        /// <summary>
        /// 모드 하루 입장 수 +1. 일일 한도 비교는 <c>400_content</c>(MV-DATA 2차)가 들어오면 MV-CONTENT가 그 값으로 먼저 한다.
        /// </summary>
        private MvMetaResponse RecordModePlay(string key)
        {
            if (string.IsNullOrWhiteSpace(key)) return MvMetaResponse.Reject(MvMetaErrors.BadParams, "key");
            return MvMetaResponse.Success(new JObject { [MvSaveKeys.Daily] = CountDaily("modePlays", key) }, new JObject { ["key"] = key });
        }

        private JObject CountDaily(string field, string key)
        {
            JObject daily = RolledDaily(out _);
            var map = (JObject)daily[field];
            map[key] = (map.Value<int?>(key) ?? 0) + 1;
            return daily;
        }

        // ───────── 본 기록(09 §9.3.3) ─────────

        private MvMetaResponse MarkSeen(JObject p)
        {
            var unlock = new JObject();
            if (p["unlock"] is JArray seenUnlock) unlock["seenUnlock"] = Union(S.Unlock.SeenUnlock, seenUnlock);
            if (p["new"] is JArray seenNew) unlock["seenNew"] = Union(S.Unlock.SeenNew, seenNew);
            var data = new JObject();
            if (unlock.HasValues) data[MvSaveKeys.Unlock] = unlock;
            if (p["contentTab"]?.Type == JTokenType.String) data[MvSaveKeys.Settings] = new JObject { ["contentTab"] = p.Value<string>("contentTab") };
            return MvMetaResponse.Success(data);
        }

        private static JArray Union(IReadOnlyList<string> current, JArray add)
        {
            var list = current.ToList();
            foreach (JToken item in add)
            {
                string key = item.Type == JTokenType.String ? item.Value<string>() : null;
                if (!string.IsNullOrEmpty(key) && !list.Contains(key)) list.Add(key);
            }
            return new JArray(list.Cast<object>().ToArray());
        }

        // ───────── 도우미 ─────────

        private static string Str(JObject p, string name) => p[name]?.Type == JTokenType.String ? p.Value<string>(name) : null;

        private static JObject ToJObject(IReadOnlyDictionary<string, int> map)
        {
            var o = new JObject();
            foreach (var pair in map) o[pair.Key] = pair.Value;
            return o;
        }

        /// <summary>
        /// 재화 변경 묶음. 수량 확인은 Framework <see cref="ResourceService"/>(예약 제외 <c>Get</c>)로 하고, 결과 행은
        /// <c>WealthContainer</c> 형식으로 같은 <c>ApplyAndSave</c>에 넣는다(09 §9.4 — 재화만 먼저 저장하지 않는다).
        /// Framework 대체: <c>LocalRewardService.PrepareMutation/Commit</c>은 재화 컨테이너만 저장하므로 다른 컨테이너와 한 번에 묶을 수 없다.
        /// </summary>
        private sealed class WealthDelta
        {
            private readonly Dictionary<int, long> _cost = new Dictionary<int, long>();
            private readonly Dictionary<int, long> _gain = new Dictionary<int, long>();
            private readonly Dictionary<int, long> _set = new Dictionary<int, long>();

            public void Cost(int id, long amount) => Add(_cost, id, amount);
            public void Gain(int id, long amount) => Add(_gain, id, amount);
            public void Set(int id, long amount) => _set[id] = Math.Max(0, amount);

            private static void Add(Dictionary<int, long> map, int id, long amount)
            {
                if (amount <= 0) return;
                map.TryGetValue(id, out long now);
                map[id] = checked(now + amount);
            }

            public bool TryWrite(ResourceService res, JObject data, out string fail)
            {
                fail = null;
                var rows = new JArray();
                foreach (int id in _cost.Keys.Union(_gain.Keys).Union(_set.Keys).OrderBy(id => id))
                {
                    string type = MvWealthIds.TypeOf(id);
                    if (type == null) { fail = "모르는 재화 " + id; return false; }
                    _cost.TryGetValue(id, out long cost);
                    _gain.TryGetValue(id, out long gain);
                    long raw = res.GetRaw(id);
                    if (res.Get(id) < cost) { fail = $"재화 {id} 부족: {res.Get(id)} < {cost}"; return false; }
                    long next = _set.TryGetValue(id, out long fixedValue) ? fixedValue + gain : checked(raw - cost + gain);
                    if (next == raw) continue;
                    rows.Add(new JObject { [WealthContainer.TypeKey] = type, [WealthContainer.QuantityKey] = next });
                }
                if (rows.Count > 0) data[MvSaveKeys.Wealth] = rows;
                return true;
            }
        }
    }
}
