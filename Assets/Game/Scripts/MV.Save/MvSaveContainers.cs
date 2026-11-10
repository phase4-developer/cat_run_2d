using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace MV.Save
{
    // 09 §9.2 컨테이너 묶음. 재화(wealthInfos)는 Framework WealthContainer를 그대로 쓴다.

    /// <summary><c>mvProgress</c> — Level, Area, 체크포인트, 벽 기록, 런 수, 최고 Area, 정산 순번(09 §9.3.1).</summary>
    public sealed class MvProgressContainer : MvSaveContainer
    {
        public MvProgressContainer(MvSaveConfig config) : base(MvSaveKeys.Progress, value => MvSaveNormalize.Progress(value, config)) { }
        public int Level => ReadInt("level");
        public int Area => ReadInt("area");
        public int Cp => ReadInt("cp");
        public int Runs => ReadInt("runs");
        public bool ZoneClear => ReadBool("zoneClear");
        /// <summary>정본 잔재. 읽기만 한다.</summary>
        public bool Escaped => ReadBool("escaped");
        public int BestArea => ReadInt("bestArea");
        public long LastSettledRunSeq => ReadLong("lastSettledRunSeq");
        /// <summary>벽 기록이 있는 Area 키(<c>"A{area}"</c>).</summary>
        public IReadOnlyList<string> BarAreaKeys => ((JObject)Value["bars"]).Properties().Select(p => p.Name).ToList().AsReadOnly();
        public static string BarKey(int area) => "A" + area;
    }

    /// <summary><c>mvLobby</c> — 로비 강화 3종, 시작 덱(09 §9.3.2).</summary>
    public sealed class MvLobbyContainer : MvSaveContainer
    {
        public MvLobbyContainer(MvSaveConfig config) : base(MvSaveKeys.Lobby, value => MvSaveNormalize.Lobby(value, config)) { }
        public IReadOnlyDictionary<string, int> Up => IntMapOf(Value["up"]);
        public int GetUp(string key) => Value["up"]?[key]?.Value<int>() ?? 0;
        public IReadOnlyList<int> Deck => Value["deck"].Values<int>().ToList().AsReadOnly();
    }

    /// <summary><c>mvAreaUp</c> — Area 강화 5종.</summary>
    public sealed class MvAreaUpContainer : MvSaveContainer
    {
        public MvAreaUpContainer() : base(MvSaveKeys.AreaUp, MvSaveNormalize.AreaUp) { }
        public IReadOnlyDictionary<string, int> Levels => IntMapOf(Value["lv"]);
        public int GetLevel(string key) => Value["lv"]?[key]?.Value<int>() ?? 0;
    }

    /// <summary><c>mvRebirth</c> — 부활 Lv, 적립 대기, 특성 4종, 쿨타임 끝 시각. 목숨 조각 보유량은 재화 <c>rbToken</c>.</summary>
    public sealed class MvRebirthContainer : MvSaveContainer
    {
        public MvRebirthContainer() : base(MvSaveKeys.Rebirth, MvSaveNormalize.Rebirth) { }
        public int Lv => ReadInt("lv");
        public int Pending => ReadInt("pending");
        public IReadOnlyDictionary<string, int> Perks => IntMapOf(Value["perks"]);
        public int GetPerk(string key) => Value["perks"]?[key]?.Value<int>() ?? 0;
        public long CdUntil => ReadLong("cdUntil");
    }

    /// <summary><c>mvGod</c> — 출전 신, 신 레벨·조각.</summary>
    public sealed class MvGodContainer : MvSaveContainer
    {
        public MvGodContainer(MvSaveConfig config) : base(MvSaveKeys.God, value => MvSaveNormalize.God(value, config)) { }
        public string Selected => ReadString("sel");
        public IReadOnlyList<string> GodKeys => ((JObject)Value["gods"]).Properties().Select(p => p.Name).ToList().AsReadOnly();
        public int GetGodLevel(string key) => Value["gods"]?[key]?["lv"]?.Value<int>() ?? 0;
        public int GetGodShard(string key) => Value["gods"]?[key]?["shard"]?.Value<int>() ?? 0;
    }

    /// <summary><c>mvUnlock</c> — 열린 기능(sticky), 해금 연출 본 기록, NEW 본 기록(09 §9.3.3).</summary>
    public sealed class MvUnlockContainer : MvSaveContainer
    {
        public MvUnlockContainer() : base(MvSaveKeys.Unlock, MvSaveNormalize.Unlock) { }
        public IReadOnlyList<string> Opened => StringListOf(Value["opened"]);
        public IReadOnlyList<string> SeenUnlock => StringListOf(Value["seenUnlock"]);
        public IReadOnlyList<string> SeenNew => StringListOf(Value["seenNew"]);
        public bool IsOpened(string key) => Opened.Contains(key);
    }

    /// <summary><c>mvDaily</c> — 하루 키, 광고 사용 수, 모드 하루 입장 수, 그 밖의 무료 횟수(09 §9.3.4). 저장 칸만 둔다.</summary>
    public sealed class MvDailyContainer : MvSaveContainer
    {
        public MvDailyContainer() : base(MvSaveKeys.Daily, MvSaveNormalize.Daily) { }
        public string Day => ReadString("day");
        public IReadOnlyDictionary<string, int> Ads => IntMapOf(Value["ads"]);
        public int GetAdUsed(string placementKey) => Value["ads"]?[placementKey]?.Value<int>() ?? 0;
        public IReadOnlyDictionary<string, int> ModePlays => IntMapOf(Value["modePlays"]);
        public IReadOnlyDictionary<string, int> Free => IntMapOf(Value["free"]);
    }

    /// <summary><c>mvPlayRes</c> — <c>{resourceKey: {n, at}}</c>(09 §9.3.5).</summary>
    public sealed class MvPlayResContainer : MvSaveContainer
    {
        public MvPlayResContainer() : base(MvSaveKeys.PlayRes, MvSaveNormalize.PlayRes) { }
        public bool TryGet(string resourceKey, out int amount, out long chargedAt)
        {
            JToken entry = Value[resourceKey];
            amount = entry?["n"]?.Value<int>() ?? 0;
            chargedAt = entry?["at"]?.Value<long>() ?? 0;
            return entry != null;
        }
    }

    /// <summary><c>mvMode</c> — <c>{contentKey: {best, first: int[]}}</c>.</summary>
    public sealed class MvModeContainer : MvSaveContainer
    {
        public MvModeContainer() : base(MvSaveKeys.Mode, MvSaveNormalize.Mode) { }
        public int GetBest(string contentKey) => Value[contentKey]?["best"]?.Value<int>() ?? 0;
        public IReadOnlyList<int> GetFirstCleared(string contentKey)
            => (Value[contentKey]?["first"] as JArray)?.Values<int>().ToList().AsReadOnly() ?? new List<int>().AsReadOnly();
    }

    /// <summary><c>mvQuest</c> — 일간·주간 기간 카운터·수령, 업적, 출석, 패스.</summary>
    public sealed class MvQuestContainer : MvSaveContainer
    {
        public MvQuestContainer() : base(MvSaveKeys.Quest, MvSaveNormalize.Quest) { }
        public string DailyPeriod => Value["daily"].Value<string>("period");
        public string MonthlyPeriod => Value["monthly"].Value<string>("period");
        /// <summary>칸(daily·monthly·achieve)의 카운터 값. 없으면 0.</summary>
        public long Count(string slot, string counterKey) => Value[slot]?["cnt"]?.Value<long?>(counterKey) ?? 0;
        /// <summary>칸의 받은 1회형 id 목록에 있는지.</summary>
        public bool Got(string slot, long id) => (Value[slot]?["got"] as JArray)?.Any(t => t.Type == JTokenType.Integer && t.Value<long>() == id) ?? false;
        /// <summary>반복형 받은 횟수(<c>claims</c>).</summary>
        public int Claims(string slot, long id) => Value[slot]?["claims"]?.Value<int?>(id.ToString(System.Globalization.CultureInfo.InvariantCulture)) ?? 0;
        /// <summary>칸의 기간 키(daily·monthly). 업적 칸은 null.</summary>
        public string Period(string slot) => Value[slot]?.Value<string>("period");
        public int AttendDays => Value["attend"].Value<int>("days");
    }

    /// <summary><c>mvResearch</c> — 연구 단계, 진행 중 연구 1개와 끝 시각.</summary>
    public sealed class MvResearchContainer : MvSaveContainer
    {
        public MvResearchContainer() : base(MvSaveKeys.Research, MvSaveNormalize.Research) { }
        public int GetLevel(string key) => Value["lv"]?[key]?.Value<int>() ?? 0;
        /// <summary>진행 중 연구 key. 없으면 null.</summary>
        public string RunningKey => (Value["run"] as JObject)?.Value<string>("key");
        public long RunningFinishAt => (Value["run"] as JObject)?.Value<long>("finishAt") ?? 0;
    }

    /// <summary><c>mvShop</c> — 상품 구매 기록, 천장 셈 값(D-83).</summary>
    public sealed class MvShopContainer : MvSaveContainer
    {
        public MvShopContainer() : base(MvSaveKeys.Shop, MvSaveNormalize.Shop) { }
        public int GetBuyCount(string productKey) => Value["buys"]?[productKey]?["n"]?.Value<int>() ?? 0;
        public long GetLastBuyAt(string productKey) => Value["buys"]?[productKey]?["at"]?.Value<long>() ?? 0;
        public IReadOnlyDictionary<string, int> Guarantee => IntMapOf(Value["guarantee"]);
        public int GetGuarantee(int guaranteeId) => Value["guarantee"]?[guaranteeId.ToString(System.Globalization.CultureInfo.InvariantCulture)]?.Value<int>() ?? 0;
    }

    /// <summary>2차 시스템 예약 칸(1차는 빈 객체).</summary>
    public abstract class MvReservedContainer : MvSaveContainer
    {
        protected MvReservedContainer(string dataKey) : base(dataKey, MvSaveNormalize.Reserved) { }
        public bool IsEmpty => !Value.HasValues;
    }

    public sealed class MvPetContainer : MvReservedContainer { public MvPetContainer() : base(MvSaveKeys.Pet) { } }
    public sealed class MvFriendContainer : MvReservedContainer { public MvFriendContainer() : base(MvSaveKeys.Friend) { } }
    public sealed class MvGearContainer : MvReservedContainer { public MvGearContainer() : base(MvSaveKeys.Gear) { } }
    public sealed class MvPassContainer : MvReservedContainer { public MvPassContainer() : base(MvSaveKeys.Pass) { } }
    public sealed class MvBlessingContainer : MvReservedContainer { public MvBlessingContainer() : base(MvSaveKeys.Blessing) { } }

    /// <summary><c>mvSettings</c> — 소리 끄기, 컨텐츠 메뉴 마지막 탭. 진행 초기화에서 유지한다.</summary>
    public sealed class MvSettingsContainer : MvSaveContainer
    {
        public MvSettingsContainer() : base(MvSaveKeys.Settings, MvSaveNormalize.Settings) { }
        public bool Muted => ReadBool("muted");
        public string ContentTab => ReadString("contentTab");
    }

    /// <summary><c>mvMeta</c> — 만든 시각, 마지막 저장 시각, 가져오기 출처, 표 묶음 버전.</summary>
    public sealed class MvMetaContainer : MvSaveContainer
    {
        public MvMetaContainer(MvSaveConfig config) : base(MvSaveKeys.Meta, value => MvSaveNormalize.Meta(value, config)) { }
        public long CreatedAt => ReadLong("createdAt");
        public long LastSaveAt => ReadLong("lastSaveAt");
        public string ImportSource => ReadString("importSource");
        public int TableVersion => ReadInt("tableVersion");
    }
}
